using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LTW.Simulation.Bridge;
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
    /// Measures how much of the screen each tower's firing effect actually lights up, in pixels,
    /// at the real board camera.
    /// </summary>
    /// <remarks>
    /// TOWER_WEAPON_VFX_PROPOSAL.md opens its verification list with "effects that look right in
    /// isolation have twice this week turned out invisible in play". The tower idle pass then showed
    /// the same thing quantitatively: four idles that were authored to characterise their tower were
    /// moving under two pixels. This asks the same question of the weapons.
    ///
    /// Method mirrors TowerMotionAmplitudeProbe. Take a baseline frame, fire ONE tower's attack cue,
    /// then diff every frame of its lifetime against that baseline. Reported per tower:
    ///
    ///   Lit px   peak count of pixels brightened past a visible margin
    ///   Peak     the strongest single-pixel brightening, 0-255
    ///   Frames   how many sampled frames showed any of it, which is the effect's real duration
    ///
    /// The simulation is paused first so nothing else fires into the diff. Idle tower motion still
    /// runs, but the baseline is taken one frame before the shot, so it contributes almost nothing.
    ///
    /// IMPORTANT: run WITHOUT -nographics, or every frame reads back a flat colour and the whole
    /// table comes out zero — the same trap MotionCaptureRunner documents.
    /// </remarks>
    public static class WeaponEffectVisibilityProbe
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const int Width = 1080;
        private const int Height = 1920;

        /// <summary>Per-channel brightening that counts as visible against this board.</summary>
        private const int VisibleDelta = 8;

        /// <summary>Frames watched after firing. At editor framerate this covers well past 0.3s.</summary>
        private const int WatchFrames = 24;

        private sealed class Result
        {
            public string Role = string.Empty;
            public int PeakLitPixels;
            public int PeakDelta;
            public int VisibleFrames;
            public int AmbientLitPixels;
            public int AmbientFrames;
        }

        private static readonly List<Result> Results = new();
        private static readonly List<(string Role, GridPosition Cell)> Placed = new();

        private static bool started;
        private static bool placed;
        private static int bindAtFrame;
        private static int frames;
        private static int towerIndex;
        private static int watchedFrames;
        private static bool baselineTaken;
        private static bool measuringAmbient;
        private static Color32[] baseline = System.Array.Empty<Color32>();
        private static Result? current;
        private static double startedAt;
        private static string outputPath = string.Empty;

        public static void Run()
        {
            started = false;
            placed = false;
            baselineTaken = false;
            frames = 0;
            towerIndex = 0;
            measuringAmbient = true;
            Results.Clear();
            Placed.Clear();
            outputPath = ReadArg("-ltwProbeOutput") ?? Path.Combine(Path.GetTempPath(), "weapon-effect-visibility.md");

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            MobileViewportLayout.SetCaptureViewportOverride(Width, Height, new Rect(0f, 0f, Width, Height));
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying = true;
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - startedAt > 300d) Finish("timed out");
                return;
            }

            var driver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            var renderer = Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            if (driver == null || commands == null || renderer == null)
            {
                if (EditorApplication.timeSinceStartup - startedAt > 300d) Finish("no driver");
                return;
            }

            if (!started)
            {
                driver.StartMatch();
                started = true;
                return;
            }

            frames++;
            if (frames < 6) return;

            if (!placed)
            {
                if (!PlaceOneOfEachTower(commands)) return;
                placed = true;
                bindAtFrame = frames + 4;
                return;
            }

            if (frames < bindAtFrame) return;

            // Paused so no other tower fires into the diff and no creep moves through it.
            driver.PauseMatch();

            if (towerIndex >= Placed.Count)
            {
                Report();
                Finish(null);
                return;
            }

            // TIME IS FROZEN for the measurement, which is what makes it exact. The board never
            // stops moving on its own — rings and dishes spin, spore fog churns, every tower
            // breathes — and a diff taken against a live board counts all of that as the weapon.
            // Measured live the first time, every one of the fifteen came back at 24/24 frames and
            // 24-40k lit pixels: that is the board's animation with the shot lost inside it. An
            // ambient control run did not rescue it either, because ambient drifts further from its
            // own baseline the longer a window runs, so several towers scored NEGATIVE against it.
            //
            // At timeScale 0 the ambient contribution is not estimated, it is zero, and what remains
            // is exactly the geometry the shot added.
            //
            // The cost is that effects which GROW are caught at their spawn size — expanding rings
            // open from a fraction of their final radius, so this understates them. That is called
            // out per tower in the report rather than hidden, and it is the honest reading of a ring
            // anyway: an effect that only registers at the end of its life registers late.
            if (!baselineTaken)
            {
                Time.timeScale = 0f;
                baseline = CapturePixels();
                baselineTaken = true;
                current = new Result { Role = Placed[towerIndex].Role };
                FireCue(renderer, driver, Placed[towerIndex].Cell);
                watchedFrames = 0;
                return;
            }

            Measure();
            watchedFrames++;
            if (watchedFrames < 2) return;

            Results.Add(current!);
            Debug.Log($"WEAPONVIS {current!.Role} lit={current.PeakLitPixels} peak={current.PeakDelta}");
            towerIndex++;
            baselineTaken = false;
        }

        private static bool PlaceOneOfEachTower(UnityCommandAdapter commands)
        {
            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(commands) is not LocalVerticalSlice sim) return false;

            sim.GrantLocalPlaytestGold(sim.LocalPlayerId, new Gold(100000));

            var x = 0;
            var y = 1;
            foreach (var entry in TowerCatalog.Entries)
            {
                var landed = false;
                while (!landed && y < 15)
                {
                    var cell = new GridPosition(x, y);
                    if (sim.PlaceTower(sim.LocalPlayerId, sim.LocalPlayerLaneId, new LTW.Simulation.Content.ContentId(entry.ContentId), cell).Accepted)
                    {
                        Placed.Add((entry.ShortLabel, cell));
                        landed = true;
                    }

                    x++;
                    if (x > 6)
                    {
                        x = 0;
                        y++;
                    }
                }
            }

            return Placed.Count > 0;
        }

        /// <summary>
        /// Fires one tower's attack cue at a point three cells up its own lane.
        /// </summary>
        private static void FireCue(UnityVerticalSliceRenderer renderer, UnitySimulationDriver driver, GridPosition cell)
        {
            var gridToWorld = typeof(UnityVerticalSliceRenderer).GetMethod(
                "GridToWorld", BindingFlags.Static | BindingFlags.NonPublic);
            var cue = typeof(UnityVerticalSliceRenderer).GetMethod(
                "SpawnTowerAttackCue",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Vector3), typeof(Vector3), typeof(string), typeof(int), typeof(Transform), typeof(int) },
                null);
            if (gridToWorld == null || cue == null)
            {
                Finish("could not reflect the cue entry points");
                return;
            }

            var snapshot = driver.LatestSnapshot;
            var tower = snapshot?.Towers.FirstOrDefault(candidate =>
                candidate.LaneId.Equals(driver.LocalPlayerLaneId) && candidate.Position.Equals(cell));
            if (tower == null) return;

            var from = (Vector3)gridToWorld.Invoke(null, new object[] { cell, driver.LocalPlayerLaneId });
            var target = new GridPosition(Mathf.Clamp(cell.X + 2, 0, 6), Mathf.Clamp(cell.Y + 3, 0, 15));
            var to = (Vector3)gridToWorld.Invoke(null, new object[] { target, driver.LocalPlayerLaneId });

            // Damage 6 so damage-scaled tells (Grovebond, Crowd Bloom, Tesla's chain) sit mid-range
            // rather than at either extreme, and tier 1 so nothing is flattered by the tier boost.
            cue.Invoke(renderer, new object[] { from, to, tower.TowerId.Value, 6, null, 1 });
        }

        private static void Measure()
        {
            var pixels = CapturePixels();
            if (pixels.Length != baseline.Length || current == null) return;

            var lit = 0;
            var peak = 0;
            for (var index = 0; index < pixels.Length; index++)
            {
                var deltaR = pixels[index].r - baseline[index].r;
                var deltaG = pixels[index].g - baseline[index].g;
                var deltaB = pixels[index].b - baseline[index].b;
                var brightest = Mathf.Max(deltaR, Mathf.Max(deltaG, deltaB));
                if (brightest < VisibleDelta) continue;

                lit++;
                if (brightest > peak) peak = brightest;
            }

            current.PeakLitPixels = Mathf.Max(current.PeakLitPixels, lit);
            current.PeakDelta = Mathf.Max(current.PeakDelta, peak);
            if (lit > 0) current.VisibleFrames++;
        }

        private static Color32[] CapturePixels()
        {
            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.black);
                var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                System.Array.Sort(cameras, static (l, r) => l.depth.CompareTo(r.depth));
                foreach (var camera in cameras)
                {
                    if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy) continue;
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture };
                    if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                }

                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }

        private static void Report()
        {
            var report = new StringBuilder();
            report.AppendLine("# Tower Weapon Effect Visibility At Board Scale");
            report.AppendLine();
            report.AppendLine($"Each tower fired once at damage 6, tier 1, with the match paused so nothing else");
            report.AppendLine($"draws into the measurement. Frames diffed against a baseline taken one frame before");
            report.AppendLine($"the shot, at {Width}x{Height}. A pixel counts as lit at +{VisibleDelta}/255 on any channel.");
            report.AppendLine();
            report.AppendLine("| Tower | Lit px | Peak | Reads |");
            report.AppendLine("| --- | --- | --- | --- |");

            foreach (var result in Results)
            {
                // A few hundred lit pixels on a 1080x1920 board is a thin line most of a phone
                // screen away from the player's eye; a couple of thousand is a shot you notice.
                var reads = result.PeakLitPixels >= 2000 ? "yes" : result.PeakLitPixels >= 600 ? "marginal" : "NO";
                report.AppendLine($"| {result.Role} | {result.PeakLitPixels} | {result.PeakDelta} | {reads} |");
            }

            File.WriteAllText(outputPath, report.ToString());
            Debug.Log($"WEAPONVIS wrote {outputPath}");
        }

        private static void Finish(string? error)
        {
            EditorApplication.update -= Tick;
            if (error != null) Debug.LogError($"WEAPONVIS FAILED: {error}");
            Time.timeScale = 1f;
            MobileViewportLayout.ClearCaptureViewportOverride();
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }

        private static string? ReadArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == name) return args[index + 1];
            }

            return null;
        }
    }
}
