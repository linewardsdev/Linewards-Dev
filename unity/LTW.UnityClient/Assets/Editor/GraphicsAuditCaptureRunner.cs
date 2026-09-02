#nullable enable

using System.IO;
using System.Reflection;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using LTW.UnityClient.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Graphics audit capture set: board close-ups, every tower and creep in the roster at a
    /// reviewable size, live combat as frame sequences, and the bot lanes — the frames a senior
    /// art review needs and that neither of the existing runners stages.
    /// </summary>
    /// <remarks>
    /// Board-only, rendered through a dedicated audit camera that mirrors the presentation
    /// camera's URP setup (post-processing, SMAA, shadows) into a RenderTexture. A separate camera
    /// is required: <c>UnityVerticalSliceRenderer</c> re-applies its own framing to the
    /// presentation camera every LateUpdate and OnPreCull, so an override written to that camera
    /// is gone before the frame renders. IMGUI is absent by construction; the HUD has its own
    /// runner. Run WITHOUT -nographics.
    ///
    /// Every close-up keeps the shipped 30 degree tilt at a larger magnification, so what is judged
    /// is the shipped angle, not a different camera. "Shipped" steps copy the presentation camera
    /// exactly, letterbox rect included.
    /// </remarks>
    public static class GraphicsAuditCaptureRunner
    {
        private const float TiltDegrees = 30f;
        private const float OrbitRadius = 18.44f;
        private const float LaneSpacing = 9f;
        private const float LaneLength = 16f;

        private static string outputDirectory = "";
        private static int width = 1536;
        private static int height = 2048;
        private static bool running;
        private static bool seeded;
        private static int step;
        private static double startedAt;
        private static double nextActionAt;
        private static bool setupDone;
        private static int burstRemaining;
        private static double burstInterval;
        private static string burstName = "";
        private static int burstIndex;
        private static bool previousPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousPlayModeOptions;
        private static Camera? auditCamera;
        private static bool mirrorShippedCamera;
        private static int foundryLane = -1;
        private static int groveLane = -1;

        private readonly struct Step
        {
            public Step(string name, System.Action? setup, double settle, int burst = 1, double interval = 0d)
            {
                Name = name;
                Setup = setup;
                Settle = settle;
                Burst = burst;
                Interval = interval;
            }

            public string Name { get; }
            public System.Action? Setup { get; }
            public double Settle { get; }
            public int Burst { get; }
            public double Interval { get; }
        }

        private static readonly Step[] Steps =
        {
            // ---- every creep walking lane 1's upper rows, ahead of its towers' reach ----
            new("10-creeps-seq", () => { SendRoster(1, 0, 5); Focus(1, 3f, 2.5f, 3.6f); }, 2.4d, 8, 0.12d),
            new("11-creeps-seq-b", () => { SendRoster(1, 5, 10); Focus(1, 3f, 2.5f, 3.6f); }, 2.4d, 8, 0.12d),
            new("12-creeps-seq-c", () => { SendRoster(1, 10, 15); Focus(1, 3f, 2.5f, 3.6f); }, 2.4d, 8, 0.12d),
            new("13-creeps-wide", () => Focus(1, 3f, 7.5f, 9.6f), 0.2d),
            new("14-creeps-mid-closeup", () => Focus(1, 3f, 5f, 2.6f), 0.4d),

            // ---- board close-ups ----
            new("01-board-lane1-wide", () => Focus(1, 3f, 7.5f, 9.6f), 2.0d),
            new("02-board-tiles-closeup", () => Focus(1, 4f, 5f, 2.2f), 0.6d),
            new("03-spawn-gate-closeup", () => Framing(1, LaneCameraFraming.SpawnGateFocus), 0.6d),
            new("04-leak-gate-closeup", () => Framing(1, LaneCameraFraming.LeakGateFocus), 0.6d),
            new("05-builder-avatar-closeup", () => Focus(1, 3f, 13f, 2.2f), 0.6d),
            new("06-placement-ghost-closeup", () => { Touch()?.BeginControlTowerPlacement(); Focus(1, 3f, 13f, 2.2f); }, 0.8d),
            new("07-placement-ghost-shipped", () => { Shipped(1); }, 0.4d),

            // ---- every tower, one line per lane (placed at seed) ----
            new("20-arcane-lane1-wide", () => { Touch()?.CancelPlacement(); Focus(1, 3f, 10.5f, 8.4f); }, 0.8d),
            new("21-arcane-closeup-a", () => Focus(1, 3f, 13.5f, 2.6f), 0.6d),
            new("22-arcane-closeup-b", () => Focus(1, 3f, 11.5f, 2.6f), 0.6d),
            new("23-arcane-closeup-c", () => Focus(1, 3f, 9.5f, 2.6f), 0.6d),
            new("24-foundry-lane-wide", () => Focus(foundryLane, 3f, 10.5f, 8.4f), 0.6d),
            new("25-foundry-closeup-a", () => Focus(foundryLane, 3f, 13.5f, 2.6f), 0.6d),
            new("26-foundry-closeup-b", () => Focus(foundryLane, 3f, 11.5f, 2.6f), 0.6d),
            new("27-foundry-closeup-c", () => Focus(foundryLane, 3f, 9.5f, 2.6f), 0.6d),
            new("28-grove-lane-wide", () => Focus(groveLane, 3f, 10.5f, 8.4f), 0.6d),
            new("29-grove-closeup-a", () => Focus(groveLane, 3f, 13.5f, 2.6f), 0.6d),
            new("30-grove-closeup-b", () => Focus(groveLane, 3f, 11.5f, 2.6f), 0.6d),
            new("31-grove-closeup-c", () => Focus(groveLane, 3f, 9.5f, 2.6f), 0.6d),
            new("32-tower-motion-seq", () => Focus(1, 3f, 13.5f, 2.6f), 0.3d, 6, 0.15d),
            new("33-selected-tower-ring", () => { SelectTower(2, 14); Focus(1, 3f, 13.5f, 2.6f); }, 0.8d),
            // Wave 3 finding #15: Pulse's Ring mesh at lane 1 (4,11) replaced with a flat decal —
            // burst across several rotation phases so an edge-on alias would still show if the
            // mesh-disable/decal swap didn't actually take.
            new("34-pulse-ring-seq", () => Focus(1, 4f, 11f, 1.8f), 0.2d, 6, 0.15d),
            // Wave 3 finding #16: Tesla's new steam bursts fire every 0.6s from foundryLane (4,13) —
            // sampled slower than the burst period so at least one frame should catch a puff mid-rise.
            new("35-tesla-steam-seq", () => Focus(foundryLane, 4f, 13f, 1.8f), 0.2d, 6, 0.3d),

            // ---- live combat against each line ----
            new("40-combat-seq", () => { Deselect(); SendPressure(1); Focus(1, 3f, 11.5f, 4.6f); }, 3.4d, 12, 0.1d),
            new("41-combat-wide", () => Focus(1, 3f, 10.5f, 8.4f), 0.2d),
            // World (5.52, 0.02, 2.24) is BuilderRestCell's ground position (Wave 2 finding #7's
            // scale + gutter-position change) — framed tight, right after Deselect() re-shows it,
            // so the builder is verified visible at its actual rest spot rather than inferred from
            // a wide shot where it is easy to mistake for a similarly gold/teal utility tower.
            new("41b-builder-recheck", () => Focus(1, 5.52f, 12.76f, 1.4f), 0.4d),
            new("42-combat-closeup", () => Focus(1, 3f, 13.5f, 2.6f), 0.4d),
            new("43-combat-seq-late", () => Focus(1, 3f, 13f, 3.2f), 1.0d, 8, 0.1d),
            new("44-foundry-combat-seq", () => { SendPressure(foundryLane); Focus(foundryLane, 3f, 11.5f, 4.6f); }, 3.4d, 8, 0.12d),
            new("45-grove-combat-seq", () => { SendPressure(groveLane); Focus(groveLane, 3f, 11.5f, 4.6f); }, 3.4d, 8, 0.12d),
            new("46-reduced-effects-combat", () => { PresentationPreferences.ReducedEffects = true; SendPressure(1); Focus(1, 3f, 11.5f, 4.6f); }, 3.6d),
            new("47-reduced-effects-off", () => { PresentationPreferences.ReducedEffects = false; }, 0.2d),

            // ---- the whole board, a bot lane, and the shipped framing ----
            new("50-all-lanes", () => Framing(1, LaneCameraFraming.AllLanes), 1.0d),
            new("51-bot-lane-wide", () => Focus(BotLane(), 3f, 9.5f, 8.4f), 0.6d),
            new("52-bot-lane-closeup", () => Focus(BotLane(), 3f, 12f, 3.0f), 0.6d),
            new("53-active-lane-shipped-framing", () => Shipped(1), 0.8d),
        };

        public static void Run()
        {
            outputDirectory = ReadArg("-ltwCaptureOutputDir") ?? Path.Combine(Path.GetTempPath(), "ltw-graphics-audit");
            width = int.TryParse(ReadArg("-ltwCaptureWidth"), out var w) && w > 0 ? w : width;
            height = int.TryParse(ReadArg("-ltwCaptureHeight"), out var h) && h > 0 ? h : height;
            Directory.CreateDirectory(outputDirectory);

            step = 0;
            seeded = false;
            setupDone = false;
            burstRemaining = 0;
            running = true;
            auditCamera = null;
            foundryLane = -1;
            groveLane = -1;

            MobileViewportLayout.SetCaptureViewportOverride(width, height, new Rect(0f, 0f, width, height));

            previousPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying = true;

            startedAt = EditorApplication.timeSinceStartup;
            nextActionAt = startedAt + 3.5d;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!running)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - startedAt > 300d)
            {
                Finish("timed out");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (!seeded && !Seed())
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < nextActionAt)
            {
                return;
            }

            if (burstRemaining > 0)
            {
                WriteFrame(Path.Combine(outputDirectory, $"{burstName}-{burstIndex:D2}.png"));
                burstIndex++;
                burstRemaining--;
                nextActionAt = EditorApplication.timeSinceStartup + burstInterval;
                if (burstRemaining == 0)
                {
                    step++;
                    setupDone = false;
                }

                return;
            }

            if (step >= Steps.Length)
            {
                Finish(null);
                return;
            }

            var entry = Steps[step];
            if (!setupDone)
            {
                try
                {
                    entry.Setup?.Invoke();
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"GFXAUDIT setup for {entry.Name} threw: {exception.Message}");
                }

                setupDone = true;
                nextActionAt = EditorApplication.timeSinceStartup + entry.Settle;
                return;
            }

            if (entry.Burst > 1)
            {
                burstName = entry.Name;
                burstIndex = 0;
                burstRemaining = entry.Burst;
                burstInterval = entry.Interval;
                return;
            }

            WriteFrame(Path.Combine(outputDirectory, entry.Name + ".png"));
            step++;
            setupDone = false;
        }

        // ------------------------------------------------------------------ scene staging

        private static bool Seed()
        {
            var driver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            var sim = Simulation();
            if (driver == null || sim == null || Camera.main == null)
            {
                return false;
            }

            // Bots off, by clearing the simulation's bot table before the match starts. Two runs
            // proved they cannot coexist with an asset review: they pick a tower line for every
            // lane inside StartMatch itself (so no lane is free for a staged lineup), and the one
            // feeding lane 1 mass-sends flyers that eliminate seat 1 inside forty seconds. Bot
            // lanes were captured on those earlier runs; this pass is about the assets.
            var botsField = typeof(LocalVerticalSlice).GetField("bots", BindingFlags.Instance | BindingFlags.NonPublic);
            if (botsField?.GetValue(sim) is System.Collections.IDictionary bots)
            {
                bots.Clear();
                Debug.Log("GFXAUDIT bots disabled");
            }

            driver.StartMatch();
            sim.GrantLocalPlaytestGold(new PlayerId(1), new Gold(9000));
            sim.GrantLocalPlaytestIncome(new PlayerId(1), new Income(400));
            seeded = true;
            Debug.Log("GFXAUDIT seeded match");
            // Before the first tick: no bot has placed a tower yet, so no seat has chosen a line
            // and lanes 2 and 3 are empty. Placing here is the only way to guarantee a lane per
            // line — by the time the first capture lands, every bot has built and locked in.
            PlaceLineups();
            return true;
        }

        private static void PlaceLineups()
        {
            var sim = Simulation();
            if (sim == null)
            {
                return;
            }

            // Rows 9-14 only: the prism's range 4 then reaches no higher than row 6, which keeps
            // rows 1-5 clean for the creep roster sequences.
            Place(sim, 1, SampleVerticalSliceContent.TowerId, 2, 14);
            Place(sim, 1, SampleVerticalSliceContent.ControlTowerId, 4, 13);
            Place(sim, 1, SampleVerticalSliceContent.UtilityTowerId, 2, 12);
            Place(sim, 1, SampleVerticalSliceContent.PulseTowerId, 4, 11);
            Place(sim, 1, SampleVerticalSliceContent.PrismTowerId, 2, 10);
            Place(sim, 1, SampleVerticalSliceContent.TwinCrescentTowerId, 4, 9);

            foundryLane = 2;
            groveLane = 3;
            sim.GrantLocalPlaytestGold(new PlayerId(foundryLane), new Gold(3000));
            Place(sim, foundryLane, SampleVerticalSliceContent.GatlingTowerId, 2, 14);
            Place(sim, foundryLane, SampleVerticalSliceContent.TeslaTowerId, 4, 13);
            Place(sim, foundryLane, SampleVerticalSliceContent.FoundryTowerId, 2, 12);
            Place(sim, foundryLane, SampleVerticalSliceContent.BarricadeTowerId, 4, 11);
            Place(sim, foundryLane, SampleVerticalSliceContent.RepairDroneTowerId, 2, 10);

            sim.GrantLocalPlaytestGold(new PlayerId(groveLane), new Gold(3000));
            Place(sim, groveLane, SampleVerticalSliceContent.ElderCanopyTowerId, 2, 14);
            Place(sim, groveLane, SampleVerticalSliceContent.SaplingTowerId, 4, 13);
            Place(sim, groveLane, SampleVerticalSliceContent.SaplingTowerId, 4, 12);
            Place(sim, groveLane, SampleVerticalSliceContent.BloomheartTowerId, 2, 12);
            Place(sim, groveLane, SampleVerticalSliceContent.ThornSnareTowerId, 4, 10);
            Place(sim, groveLane, SampleVerticalSliceContent.SporeCloudTowerId, 2, 10);

            // Tier looks: arcane line to tier 3, arrow to tier 3, control to tier 2.
            Log("tier2 arcane", sim.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, 0, 2));
            Log("tier3 arcane", sim.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, 0, 3));
            Log("upgrade arrow", sim.UpgradeTower(new PlayerId(1), new LaneId(1), new GridPosition(2, 14)));
            Log("upgrade arrow again", sim.UpgradeTower(new PlayerId(1), new LaneId(1), new GridPosition(2, 14)));
            Log("upgrade control", sim.UpgradeTower(new PlayerId(1), new LaneId(1), new GridPosition(4, 13)));

            Object.FindAnyObjectByType<UnitySimulationDriver>()?.RefreshSnapshot(drainEvents: true);
        }

        private static int BotLane()
        {
            for (var lane = 8; lane >= 2; lane--)
            {
                if (lane != foundryLane && lane != groveLane)
                {
                    return lane;
                }
            }

            return 2;
        }

        private static void Place(LocalVerticalSlice sim, int player, ContentId towerId, int x, int y) =>
            Log($"place {towerId.Value} lane {player} ({x},{y})", sim.PlaceTower(new PlayerId(player), new LaneId(player), towerId, new GridPosition(x, y)));

        private static readonly ContentId[] Roster =
        {
            SampleVerticalSliceContent.CreepId,
            SampleVerticalSliceContent.BruteCreepId,
            SampleVerticalSliceContent.SwarmCreepId,
            SampleVerticalSliceContent.ShadeCreepId,
            SampleVerticalSliceContent.SiegeCreepId,
            SampleVerticalSliceContent.WispCreepId,
            SampleVerticalSliceContent.RevenantCreepId,
            SampleVerticalSliceContent.ObsidianBruteCreepId,
            SampleVerticalSliceContent.SerpentCreepId,
            SampleVerticalSliceContent.TurretWalkerCreepId,
            SampleVerticalSliceContent.ZephyrCreepId,
            SampleVerticalSliceContent.BurrowerCreepId,
            SampleVerticalSliceContent.StalkerCreepId,
            SampleVerticalSliceContent.WardenCreepId,
            SampleVerticalSliceContent.ColossusCreepId,
        };

        private static PlayerId? Sender(LocalVerticalSlice sim, int lane)
        {
            var sender = sim.LocalPlaytestSenderForLane(new LaneId(lane));
            if (sender is null)
            {
                Debug.LogWarning($"GFXAUDIT no sender for lane {lane}");
                return null;
            }

            sim.GrantLocalPlaytestGold(sender.Value, new Gold(20000));
            sim.ClearLocalPlaytestSendCooldown(sender.Value);
            return sender;
        }

        private static void SendRoster(int lane, int from, int to)
        {
            var sim = Simulation();
            if (sim == null || lane <= 0 || Sender(sim, lane) is not { } sender)
            {
                return;
            }

            for (var index = from; index < to && index < Roster.Length; index++)
            {
                sim.ClearLocalPlaytestSendCooldown(sender);
                Log($"send {Roster[index].Value} -> lane {lane}", sim.QueueSend(sender, Roster[index], 1));
            }
        }

        private static void SendPressure(int lane)
        {
            var sim = Simulation();
            if (sim == null || lane <= 0 || Sender(sim, lane) is not { } sender)
            {
                return;
            }

            Log("pressure runner", sim.QueueSend(sender, SampleVerticalSliceContent.CreepId, 8));
            sim.ClearLocalPlaytestSendCooldown(sender);
            Log("pressure brute", sim.QueueSend(sender, SampleVerticalSliceContent.BruteCreepId, 3));
            sim.ClearLocalPlaytestSendCooldown(sender);
            Log("pressure swarm", sim.QueueSend(sender, SampleVerticalSliceContent.SwarmCreepId, 10));
            sim.ClearLocalPlaytestSendCooldown(sender);
            Log("pressure walker", sim.QueueSend(sender, SampleVerticalSliceContent.TurretWalkerCreepId, 2));
            sim.ClearLocalPlaytestSendCooldown(sender);
            Log("pressure siege", sim.QueueSend(sender, SampleVerticalSliceContent.SiegeCreepId, 2));
        }

        private static void SelectTower(int x, int y)
        {
            var touch = Touch();
            if (touch == null)
            {
                return;
            }

            var method = typeof(TouchPlacementController).GetMethod("SelectTowerAt", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(touch, new object[] { new Vector2Int(x, y) });
        }

        private static void Deselect()
        {
            var touch = Touch();
            if (touch == null)
            {
                return;
            }

            typeof(TouchPlacementController).GetField("selectedTower", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(touch, null);
            typeof(TouchPlacementController).GetMethod("HideSelectionRing", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(touch, null);
            // SelectTowerAt hides the builder avatar while a tower's info panel is up
            // (TouchPlacementController.Selection.cs) and only the real deselect flow shows it
            // again. Clearing selectedTower by reflection above skips that, so the builder stayed
            // hidden for the rest of every capture after step 33 selected a tower — a gap in this
            // harness, not a product bug (confirmed via the temporary GFXBUILDER diagnostic logging
            // in BuilderAvatar.cs, which showed isPlacing=False, isLocalSeatEliminated=False at the
            // hide call — nothing else was responsible).
            typeof(TouchPlacementController).GetMethod("UpdateBuilderAvatar", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(touch, null);
        }

        // ------------------------------------------------------------------ camera

        private static Camera EnsureAuditCamera()
        {
            if (auditCamera != null)
            {
                return auditCamera;
            }

            var main = Camera.main;
            var cameraObject = new GameObject("LTW Audit Camera");
            auditCamera = cameraObject.AddComponent<Camera>();
            auditCamera.enabled = false;
            auditCamera.orthographic = true;
            auditCamera.nearClipPlane = 0.1f;
            auditCamera.farClipPlane = 80f;
            auditCamera.clearFlags = CameraClearFlags.SolidColor;
            auditCamera.backgroundColor = main != null ? main.backgroundColor : new Color(0.06f, 0.08f, 0.12f);
            auditCamera.cullingMask = main != null ? main.cullingMask : -1;
            auditCamera.allowHDR = true;

            var data = auditCamera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.Low;
                data.renderShadows = true;
            }

            return auditCamera;
        }

        private static void Framing(int lane, LaneCameraFraming framing)
        {
            var renderer = Renderer();
            if (renderer == null)
            {
                return;
            }

            renderer.SetActiveLaneCameraId(lane);
            renderer.SetCameraFraming(framing);
            mirrorShippedCamera = true;
        }

        private static void Shipped(int lane)
        {
            Renderer()?.SetActiveLaneCameraId(lane);
            mirrorShippedCamera = true;
        }

        /// <summary>Shipped tilt, custom magnification, focus on a grid cell of a lane.</summary>
        private static void Focus(int lane, float gridX, float gridY, float orthographicSize)
        {
            if (lane <= 0)
            {
                lane = 1;
            }

            // Keep the presentation camera on the same lane so the renderer's per-lane detail
            // decisions (active-lane meters, labels) match what the audit camera is looking at.
            Renderer()?.SetActiveLaneCameraId(lane);
            mirrorShippedCamera = false;

            var camera = EnsureAuditCamera();
            var focus = new Vector3((lane - 1) * LaneSpacing + gridX, 0.35f, LaneLength - 1f - gridY);
            var tilt = TiltDegrees * Mathf.Deg2Rad;
            camera.orthographicSize = orthographicSize;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.transform.position = focus + new Vector3(0f, OrbitRadius * Mathf.Cos(tilt), -OrbitRadius * Mathf.Sin(tilt));
            camera.transform.LookAt(focus);
        }

        private static void SyncAuditCamera()
        {
            var camera = EnsureAuditCamera();
            var main = Camera.main;
            if (!mirrorShippedCamera || main == null)
            {
                return;
            }

            camera.orthographicSize = main.orthographicSize;
            camera.rect = main.rect;
            camera.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        }

        // ------------------------------------------------------------------ capture

        private static void WriteFrame(string path)
        {
            SyncAuditCamera();
            var camera = EnsureAuditCamera();
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, camera.backgroundColor);

                camera.enabled = true;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    Debug.LogWarning("GFXAUDIT render request unsupported");
                }

                camera.enabled = false;

                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
                Debug.Log($"GFXAUDIT captured {Path.GetFileName(path)}");
            }
            finally
            {
                camera.enabled = false;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(texture);
            }
        }

        private static void Finish(string? error)
        {
            running = false;
            EditorApplication.update -= Tick;
            MobileViewportLayout.ClearCaptureViewportOverride();
            PresentationPreferences.ReducedEffects = false;
            EditorSettings.enterPlayModeOptionsEnabled = previousPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousPlayModeOptions;

            if (error != null)
            {
                Debug.LogError($"GFXAUDIT {error}");
            }

            var files = Directory.GetFiles(outputDirectory, "*.png").Length;
            Debug.Log($"GFXAUDIT finished: {files} frame(s) in {outputDirectory}");
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(error == null ? 0 : 1);
            }
        }

        // ------------------------------------------------------------------ lookups

        private static UnityVerticalSliceRenderer? Renderer() => Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();

        private static TouchPlacementController? Touch() => Object.FindAnyObjectByType<TouchPlacementController>();

        private static LocalVerticalSlice? Simulation()
        {
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (commands == null)
            {
                return null;
            }

            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(commands) as LocalVerticalSlice;
        }

        private static void Log(string label, VerticalSliceCommandResult result) =>
            Debug.Log($"GFXAUDIT {label}: accepted={result.Accepted} reason={result.RejectionReason}");

        private static string? ReadArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == name)
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
