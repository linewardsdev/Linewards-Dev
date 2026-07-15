#nullable enable

using System;
using System.IO;
using System.Reflection;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using LTW.UnityClient.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class VisualReviewCaptureRunner
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private static readonly string DefaultOutputDirectory = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "..",
            "docs",
            "screenshot-reviews",
            "art-creep-starter-set-kickoff",
            "captures"));

        private static string outputDirectory = DefaultOutputDirectory;
        private static CaptureState state;
        private static double nextActionAt;
        private static string? pendingCapturePath;
        private static string? pendingCaptureLabel;
        private static string? delayedCaptureLabel;
        private static int captureIndex;
        private static double startedAt;
        private static bool exitAfterRun;
        private static bool writeGrayscaleCopies;
        private static bool previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousEnterPlayModeOptions;
        private static CaptureMode captureMode;
        private static bool roleLineupPrepared;

        [MenuItem("Line Wards/Review/Capture Visual Review Set")]
        public static void CaptureVisualReviewSet()
        {
            BeginCapture(CaptureMode.FullReview);
        }

        [MenuItem("Line Wards/Review/Capture Role Lineup Review Set")]
        public static void CaptureRoleLineupReviewSet()
        {
            BeginCapture(CaptureMode.RoleLineup);
        }

        [MenuItem("Line Wards/Review/Capture Checklist Evidence Set")]
        public static void CaptureChecklistEvidenceSet()
        {
            BeginCapture(CaptureMode.ChecklistEvidence);
        }

        [MenuItem("Line Wards/Review/Capture Role Contact Sheet")]
        public static void CaptureRoleContactSheet()
        {
            outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);
            writeGrayscaleCopies = HasArgument("-ltwCaptureGrayscale");

            var exitCode = 0;
            try
            {
                var path = Path.Combine(outputDirectory, "01-role-contact-sheet.png");
                RenderRoleContactSheet(path);
                if (writeGrayscaleCopies)
                {
                    WriteGrayscaleCopy("role-contact-sheet", path);
                }

                Debug.Log($"LTW role contact sheet captured in {outputDirectory}");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                Debug.LogException(exception);
            }

            if (ShouldExitAfterRun() || InternalEditorUtility.inBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void BeginCapture(CaptureMode mode)
        {
            if (InternalEditorUtility.inBatchMode && HasArgument("-nographics"))
            {
                Debug.LogError("LTW visual review capture cannot run with -nographics because the capture path renders active cameras. Run batch capture without -nographics, or use the in-editor Line Wards/Review menu item.");
                EditorApplication.Exit(1);
                return;
            }

            captureMode = mode;
            outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);
            captureIndex = 1;
            pendingCapturePath = null;
            pendingCaptureLabel = null;
            delayedCaptureLabel = null;
            roleLineupPrepared = false;
            exitAfterRun = ShouldExitAfterRun();
            writeGrayscaleCopies = HasArgument("-ltwCaptureGrayscale");
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            state = CaptureState.WaitForPlayMode;
            nextActionAt = EditorApplication.timeSinceStartup + 0.5d;
            startedAt = EditorApplication.timeSinceStartup;

            EditorApplication.update -= Update;
            EditorApplication.update += Update;

            EditorSceneManager.OpenScene(ScenePath);
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup - startedAt > 90d)
            {
                Finish("Timed out while capturing visual review screenshots.");
                return;
            }

            if (pendingCapturePath != null)
            {
                if (!File.Exists(pendingCapturePath))
                {
                    return;
                }

                var completedPath = pendingCapturePath;
                pendingCapturePath = null;
                var completedLabel = pendingCaptureLabel;
                pendingCaptureLabel = null;
                if (writeGrayscaleCopies)
                {
                    WriteGrayscaleCopy(completedLabel, completedPath);
                }

                nextActionAt = EditorApplication.timeSinceStartup + 0.5d;
                AdvanceState(completedLabel);
                return;
            }

            if (EditorApplication.timeSinceStartup < nextActionAt)
            {
                return;
            }

            if (delayedCaptureLabel != null)
            {
                var label = delayedCaptureLabel;
                delayedCaptureLabel = null;
                QueueCapture(label);
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            var driver = UnityEngine.Object.FindAnyObjectByType<UnitySimulationDriver>();
            var commands = UnityEngine.Object.FindAnyObjectByType<UnityCommandAdapter>();
            var placement = UnityEngine.Object.FindAnyObjectByType<TouchPlacementController>();
            var sendDock = UnityEngine.Object.FindAnyObjectByType<SendDockController>();
            var laneToggle = UnityEngine.Object.FindAnyObjectByType<LaneViewToggleController>();
            var stress = UnityEngine.Object.FindAnyObjectByType<HeavySendStressHarness>();

            if (driver == null || commands == null || placement == null || sendDock == null || laneToggle == null || stress == null)
            {
                return;
            }

            if (captureMode == CaptureMode.RoleLineup)
            {
                UpdateRoleLineup(driver, commands, placement, sendDock, laneToggle);
                return;
            }

            if (captureMode == CaptureMode.ChecklistEvidence)
            {
                UpdateChecklistEvidence(driver, commands, placement, sendDock, laneToggle);
                return;
            }

            switch (state)
            {
                case CaptureState.WaitForPlayMode:
                    QueueCapture("default-hud");
                    break;

                case CaptureState.OpenBuildMenu:
                    SetPrivateBool(placement, "isPaletteExpanded", true);
                    ScheduleCaptureThenAdvance("build-menu-open");
                    break;

                case CaptureState.OpenSendMenu:
                    SetPrivateBool(placement, "isPaletteExpanded", false);
                    SetPrivateBool(sendDock, "isExpanded", true);
                    ScheduleCaptureThenAdvance("send-menu-open");
                    break;

                case CaptureState.OpenLaneSelector:
                    SetPrivateBool(sendDock, "isExpanded", false);
                    laneToggle.ToggleView();
                    ScheduleCaptureThenAdvance("lane-selector-open");
                    break;

                case CaptureState.ActiveCombat:
                    laneToggle.ShowLaneView();
                    StartCombat(driver, commands);
                    ScheduleCaptureThenAdvance("active-combat");
                    break;

                case CaptureState.HeavyPressure:
                    stress.StartRun();
                    ScheduleCaptureThenAdvance("heavy-pressure", 4d);
                    break;

                case CaptureState.ReducedEffects:
                    PresentationPreferences.ReducedEffects = true;
                    ScheduleCaptureThenAdvance("reduced-effects-heavy", 2d);
                    break;

                case CaptureState.Results:
                    TryAccelerateMatch(driver);
                    if (driver.LatestMatchSummary == null && EditorApplication.timeSinceStartup - startedAt < 80d)
                    {
                        nextActionAt = EditorApplication.timeSinceStartup + 0.5d;
                        return;
                    }

                    QueueCapture("results-or-late-match");
                    break;

                case CaptureState.Done:
                    Finish(null);
                    break;
            }
        }

        private static void UpdateRoleLineup(
            UnitySimulationDriver driver,
            UnityCommandAdapter commands,
            TouchPlacementController placement,
            SendDockController sendDock,
            LaneViewToggleController laneToggle)
        {
            switch (state)
            {
                case CaptureState.WaitForPlayMode:
                    SetPrivateBool(placement, "isPaletteExpanded", false);
                    SetPrivateBool(sendDock, "isExpanded", false);
                    laneToggle.ShowLaneView();
                    PrepareRoleLineup(driver, commands);
                    ScheduleCaptureThenAdvance("role-lineup", 4.5d);
                    break;

                case CaptureState.OpenBuildMenu:
                    PresentationPreferences.ReducedEffects = true;
                    ScheduleCaptureThenAdvance("role-lineup-reduced-effects", 0.75d);
                    break;

                case CaptureState.OpenSendMenu:
                    state = CaptureState.Done;
                    break;

                case CaptureState.Done:
                    Finish(null);
                    break;
            }
        }

        private static void UpdateChecklistEvidence(
            UnitySimulationDriver driver,
            UnityCommandAdapter commands,
            TouchPlacementController placement,
            SendDockController sendDock,
            LaneViewToggleController laneToggle)
        {
            switch (state)
            {
                case CaptureState.WaitForPlayMode:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 3, 5000);
                    LogCommandResult("checklist runner x10", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.CreepId, 10));
                    ScheduleCaptureThenAdvance("runner-10-pressure", 4.5d);
                    break;

                case CaptureState.OpenBuildMenu:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 3, 5000);
                    LogCommandResult("checklist swarm x24", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.SwarmCreepId, 24));
                    ScheduleCaptureThenAdvance("swarm-heavy-pressure", 4.5d);
                    break;

                case CaptureState.OpenSendMenu:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 3, 5000);
                    LogCommandResult("checklist shade x6", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.ShadeCreepId, 6));
                    ScheduleCaptureThenAdvance("shade-readability", 4.5d);
                    break;

                case CaptureState.OpenLaneSelector:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 2);
                    driver.StartMatch();
                    LogCommandResult("checklist damaged transfer", commands.CreateDamagedTransferReviewCreep());
                    ScheduleCaptureThenAdvance("damaged-transfer-health", 0.2d);
                    break;

                case CaptureState.ActiveCombat:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    PresentationPreferences.ReducedEffects = true;
                    StartCombat(driver, commands);
                    ScheduleCaptureThenAdvance("reduced-effects-critical-cues", 3d);
                    break;

                case CaptureState.HeavyPressure:
                    state = CaptureState.Done;
                    break;

                case CaptureState.Done:
                    Finish(null);
                    break;
            }
        }

        private static void ResetChecklistScenario(
            UnityCommandAdapter commands,
            TouchPlacementController placement,
            SendDockController sendDock,
            LaneViewToggleController laneToggle,
            int activeLaneId)
        {
            commands.ResetMatch();
            PresentationPreferences.ReducedEffects = false;
            SetPrivateBool(placement, "isPaletteExpanded", false);
            SetPrivateBool(sendDock, "isExpanded", false);
            laneToggle.ShowLaneView();
            var renderer = UnityEngine.Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            renderer?.SetActiveLaneCameraId(activeLaneId);
        }

        private static void StartCombat(UnitySimulationDriver driver, UnityCommandAdapter commands)
        {
            driver.StartMatch();
            GrantPlaytestGold(commands, 1, 2000);
            GrantPlaytestGold(commands, 3, 2000);
            LogCommandResult("reduced effects Arrow tower", commands.PlaceSampleTower(2, 13));
            LogCommandResult("reduced effects Control tower", commands.PlaceControlTower(4, 12));
            LogCommandResult("reduced effects Runner pressure", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.CreepId, 6));
            LogCommandResult("reduced effects Brute pressure", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.BruteCreepId, 2));
            LogCommandResult("reduced effects Swarm pressure", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.SwarmCreepId, 12));
        }

        private static void PrepareRoleLineup(UnitySimulationDriver driver, UnityCommandAdapter commands)
        {
            if (roleLineupPrepared)
            {
                return;
            }

            roleLineupPrepared = true;
            driver.StartMatch();
            GrantPlaytestGold(commands, 1, 2000);

            LogCommandResult("lineup Arrow tower", commands.PlaceSampleTower(1, 14));
            LogCommandResult("lineup Control tower", commands.PlaceControlTower(5, 14));
            LogCommandResult("lineup Relay tower", commands.PlaceUtilityTower(1, 11));
            LogCommandResult("lineup Pulse tower", commands.PlacePulseTower(5, 11));
            LogCommandResult("lineup Prism tower", commands.PlacePrismTower(2, 8));

            GrantPlaytestGold(commands, 3, 2000);
            LogCommandResult("lineup Runner visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.CreepId, 1));
            LogCommandResult("lineup Brute visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.BruteCreepId, 1));
            LogCommandResult("lineup Swarm visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.SwarmCreepId, 3));
            LogCommandResult("lineup Shade visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.ShadeCreepId, 1));
            LogCommandResult("lineup Siege visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.SiegeCreepId, 1));
            driver.RefreshSnapshot(drainEvents: true);
        }

        private static void RenderRoleContactSheet(string path)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.44f, 0.5f, 0.58f);

            var cameraObject = new GameObject("RoleContactSheetCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.09f);
            camera.orthographic = true;
            camera.orthographicSize = 4.35f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;
            camera.transform.position = new Vector3(0f, 7.5f, -8.5f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.45f, 0f) - camera.transform.position);

            var lightObject = new GameObject("RoleContactSheetKeyLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

            CreateContactSheetBackdrop();

            var towerPrefabs = new[]
            {
                ("ARROW", "Assets/Prefabs/Towers/Tower_Arrow.prefab"),
                ("CONTROL", "Assets/Prefabs/Towers/Tower_Control.prefab"),
                ("RELAY", "Assets/Prefabs/Towers/Tower_Relay.prefab"),
                ("PULSE", "Assets/Prefabs/Towers/Tower_Pulse.prefab"),
                ("PRISM", "Assets/Prefabs/Towers/Tower_Prism.prefab"),
            };

            var creepPrefabs = new[]
            {
                ("RUNNER", "Assets/Prefabs/Creeps/Creep_Runner.prefab"),
                ("BRUTE", "Assets/Prefabs/Creeps/Creep_Brute.prefab"),
                ("SWARM", "Assets/Prefabs/Creeps/Creep_Swarm.prefab"),
                ("SHADE", "Assets/Prefabs/Creeps/Creep_Shade.prefab"),
                ("SIEGE", "Assets/Prefabs/Creeps/Creep_Siege.prefab"),
            };

            for (var index = 0; index < towerPrefabs.Length; index++)
            {
                var x = -3.8f + index * 1.9f;
                InstantiateContactPrefab(towerPrefabs[index].Item2, new Vector3(x, 0f, 1.8f), 0.75f, Quaternion.identity);
                AddContactLabel(towerPrefabs[index].Item1, new Vector3(x, 0.05f, 2.72f), camera);
            }

            for (var index = 0; index < creepPrefabs.Length; index++)
            {
                var x = -3.8f + index * 1.9f;
                InstantiateContactPrefab(creepPrefabs[index].Item2, new Vector3(x, 0f, -1.5f), 1.2f, Quaternion.Euler(0f, 180f, 0f));
                AddContactLabel(creepPrefabs[index].Item1, new Vector3(x, 0.05f, -0.48f), camera);
            }

            AddContactLabel("TOWERS", new Vector3(-5.15f, 0.05f, 1.8f), camera, 0.08f, new Color(0.38f, 0.93f, 1f));
            AddContactLabel("CREEPS", new Vector3(-5.15f, 0.05f, -1.5f), camera, 0.08f, new Color(0.95f, 0.84f, 0.38f));

            var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                var output = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                output.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                output.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(output));
                UnityEngine.Object.DestroyImmediate(output);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void CreateContactSheetBackdrop()
        {
            var material = new Material(FindContactSheetShader())
            {
                color = new Color(0.07f, 0.1f, 0.16f)
            };

            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "ContactSheetBackdrop";
            backdrop.transform.position = new Vector3(0f, -0.08f, 0f);
            backdrop.transform.localScale = new Vector3(11f, 0.04f, 6.8f);
            if (backdrop.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }

            if (backdrop.TryGetComponent<Collider>(out var collider))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void InstantiateContactPrefab(string assetPath, Vector3 position, float scale, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Missing contact-sheet prefab at {assetPath}.");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab);
            }

            instance.name = prefab.name;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.transform.localScale = Vector3.one * scale;
            HideContactSheetOnlyChild(instance, "RangeHalo");
        }

        private static void HideContactSheetOnlyChild(GameObject instance, string childName)
        {
            var child = instance.transform.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static void AddContactLabel(
            string label,
            Vector3 position,
            Camera camera,
            float characterSize = 0.055f,
            Color? color = null)
        {
            var labelObject = new GameObject("Label_" + label);
            var text = labelObject.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = characterSize;
            text.fontSize = 28;
            text.color = color ?? new Color(0.85f, 0.92f, 1f);
            labelObject.transform.position = position;
            labelObject.transform.rotation = camera.transform.rotation;
        }

        private static Shader FindContactSheetShader() =>
            Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Standard");

        private static void GrantPlaytestGold(UnityCommandAdapter commands, int playerId, int amount)
        {
            var simulation = GetLocalSimulation(commands);
            simulation?.GrantLocalPlaytestGold(new PlayerId(playerId), new Gold(amount));
        }

        private static VerticalSliceCommandResult QueueVisibleLineupCreep(UnityCommandAdapter commands, ContentId creepId, int quantity)
        {
            var simulation = GetLocalSimulation(commands);
            return simulation is null
                ? VerticalSliceCommandResult.Reject(LTW.Simulation.Commands.CommandRejectionReason.MatchPaused)
                : simulation.QueueSend(new PlayerId(3), creepId, quantity);
        }

        private static LocalVerticalSlice? GetLocalSimulation(UnityCommandAdapter commands)
        {
            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(commands) as LocalVerticalSlice;
        }

        private static void LogCommandResult(string label, VerticalSliceCommandResult result)
        {
            if (result.Accepted)
            {
                Debug.Log($"LTW role lineup accepted: {label}");
            }
            else
            {
                Debug.LogWarning($"LTW role lineup rejected: {label} ({result.RejectionReason})");
            }
        }

        private static void TryAccelerateMatch(UnitySimulationDriver driver)
        {
            var field = typeof(UnitySimulationDriver).GetField("ticksPerSecond", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(driver, 1200f);
            Time.timeScale = 20f;
            driver.StartMatch();
        }

        private static void ScheduleCaptureThenAdvance(string label, double delaySeconds = 0.75d)
        {
            nextActionAt = EditorApplication.timeSinceStartup + delaySeconds;
            state = (CaptureState)((int)state + 1);
            delayedCaptureLabel = label;
        }

        private static void QueueCapture(string label)
        {
            var path = Path.Combine(outputDirectory, $"{captureIndex:00}-{label}.png");
            captureIndex++;
            if (InternalEditorUtility.inBatchMode)
            {
                WriteImmediateCapture(path, label);
                if (writeGrayscaleCopies)
                {
                    WriteGrayscaleCopy(label, path);
                }

                nextActionAt = EditorApplication.timeSinceStartup + 0.5d;
                AdvanceState(label);
                return;
            }

            pendingCapturePath = path;
            pendingCaptureLabel = label;
            ScreenCapture.CaptureScreenshot(path);
        }

        private static void WriteImmediateCapture(string path, string label)
        {
            var width = Math.Max(1080, Screen.width);
            var height = Math.Max(1920, Screen.height);
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.black);
                RenderActiveCameras(renderTexture);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                PaintBatchHudOverlay(texture, label);
                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
                Debug.Log($"Saved visual review capture {path}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void RenderActiveCameras(RenderTexture renderTexture)
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Array.Sort(cameras, static (left, right) => left.depth.CompareTo(right.depth));
            for (var index = 0; index < cameras.Length; index++)
            {
                var camera = cameras[index];
                if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var previousTarget = camera.targetTexture;
                var previousAspect = camera.aspect;
                camera.targetTexture = renderTexture;
                camera.aspect = renderTexture.width / (float)renderTexture.height;
                camera.Render();
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
            }
        }

        private static void PaintBatchHudOverlay(Texture2D texture, string label)
        {
            if (!InternalEditorUtility.inBatchMode)
            {
                return;
            }

            var panel = new Color32(13, 20, 37, 230);
            var panelStrong = new Color32(9, 14, 28, 245);
            var blue = new Color32(77, 163, 255, 255);
            var mint = new Color32(89, 225, 182, 255);
            var gold = new Color32(255, 200, 74, 255);
            var violet = new Color32(155, 108, 255, 255);
            var red = new Color32(255, 97, 112, 255);
            var cloud = new Color32(244, 247, 255, 255);

            PaintRect(texture, 150, 1782, 780, 70, panelStrong);
            PaintRect(texture, 150, 1777, 780, 6, gold);
            PaintText(texture, "STATS", 192, 1832, mint, 4);
            PaintText(texture, "220V 75G +10 P0", 405, 1832, cloud, 4);
            PaintRect(texture, 890, 1744, 104, 54, mint);
            PaintText(texture, "PLAY", 914, 1780, panelStrong, 4);

            PaintRect(texture, 158, 62, 112, 84, panelStrong);
            PaintRect(texture, 158, 58, 112, 7, mint);
            PaintText(texture, "BUILD", 176, 116, mint, 3);
            PaintRect(texture, 810, 62, 112, 84, panelStrong);
            PaintRect(texture, 810, 58, 112, 7, gold);
            PaintText(texture, "SEND", 838, 116, gold, 3);

            switch (label)
            {
                case "build-menu-open":
                    PaintBuildMenuOverlay(texture, panel, blue, mint, gold, violet, cloud);
                    break;
                case "send-menu-open":
                    PaintSendMenuOverlay(texture, panel, blue, mint, gold, violet, red);
                    break;
                case "lane-selector-open":
                    PaintLaneSelectorOverlay(texture, panelStrong, blue, cloud);
                    break;
                case "results-or-late-match":
                    PaintResultsOverlay(texture, panelStrong, gold, cloud);
                    break;
            }
        }

        private static void PaintBuildMenuOverlay(Texture2D texture, Color32 panel, Color32 blue, Color32 mint, Color32 gold, Color32 violet, Color32 cloud)
        {
            PaintRect(texture, 160, 168, 760, 210, panel);
            PaintRect(texture, 160, 164, 760, 7, mint);
            PaintText(texture, "WARD PALETTE", 196, 350, mint, 4);
            PaintCard(texture, 230, 260, "ARROW", "25G", blue);
            PaintCard(texture, 390, 260, "CTRL", "35G", violet);
            PaintCard(texture, 550, 260, "RELAY", "40G", gold);
            PaintCard(texture, 310, 190, "PULSE", "45G", mint);
            PaintCard(texture, 470, 190, "PRISM", "60G", cloud);
        }

        private static void PaintSendMenuOverlay(Texture2D texture, Color32 panel, Color32 blue, Color32 mint, Color32 gold, Color32 violet, Color32 red)
        {
            PaintRect(texture, 160, 155, 760, 232, panel);
            PaintRect(texture, 160, 150, 760, 7, gold);
            PaintText(texture, "SEND PRESSURE", 196, 358, gold, 4);
            PaintText(texture, "GOLD 75", 690, 358, mint, 3);
            PaintCard(texture, 230, 265, "RUN", "10G +1", blue);
            PaintCard(texture, 390, 265, "BRUTE", "18G +2", violet);
            PaintCard(texture, 550, 265, "SWARM", "18G +3", gold);
            PaintCard(texture, 310, 190, "SHADE", "24G +3", mint);
            PaintCard(texture, 470, 190, "SIEGE", "40G +4", red);
        }

        private static void PaintLaneSelectorOverlay(Texture2D texture, Color32 panel, Color32 blue, Color32 cloud)
        {
            PaintRect(texture, 1004, 760, 54, 740, panel);
            PaintText(texture, "R", 1024, 1450, cloud, 4);
            PaintText(texture, "L1", 1015, 1320, blue, 4);
            PaintText(texture, "L2", 1015, 1190, blue, 4);
            PaintText(texture, "L3", 1015, 1060, blue, 4);
        }

        private static void PaintResultsOverlay(Texture2D texture, Color32 panel, Color32 gold, Color32 cloud)
        {
            PaintRect(texture, 210, 820, 660, 280, panel);
            PaintRect(texture, 210, 815, 660, 8, gold);
            PaintText(texture, "MATCH COMPLETE", 298, 1040, gold, 5);
            PaintText(texture, "WINNER P1", 380, 960, cloud, 5);
            PaintText(texture, "RESTART", 432, 885, gold, 4);
        }

        private static void PaintCard(Texture2D texture, int x, int y, string title, string meta, Color32 accent)
        {
            var panel = new Color32(
                (byte)Mathf.Clamp(22 + accent.r / 10, 0, 255),
                (byte)Mathf.Clamp(28 + accent.g / 10, 0, 255),
                (byte)Mathf.Clamp(48 + accent.b / 10, 0, 255),
                238);
            PaintRect(texture, x, y, 140, 58, panel);
            PaintRect(texture, x, y, 140, 6, accent);
            PaintText(texture, title, x + 12, y + 47, new Color32(244, 247, 255, 255), 3);
            PaintText(texture, meta, x + 12, y + 24, accent, 3);
        }

        private static void PaintRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            var maxX = Mathf.Clamp(x + width, 0, texture.width);
            var maxY = Mathf.Clamp(y + height, 0, texture.height);
            var startX = Mathf.Clamp(x, 0, texture.width);
            var startY = Mathf.Clamp(y, 0, texture.height);
            for (var py = startY; py < maxY; py++)
            {
                for (var px = startX; px < maxX; px++)
                {
                    BlendPixel(texture, px, py, color);
                }
            }
        }

        private static void PaintText(Texture2D texture, string text, int x, int baselineY, Color32 color, int scale)
        {
            var cursor = x;
            foreach (var character in text.ToUpperInvariant())
            {
                if (character == ' ')
                {
                    cursor += 4 * scale;
                    continue;
                }

                var rows = GlyphRows(character);
                for (var row = 0; row < rows.Length; row++)
                {
                    var glyphRow = rows[row];
                    for (var col = 0; col < glyphRow.Length; col++)
                    {
                        if (glyphRow[col] != '1')
                        {
                            continue;
                        }

                        var px = cursor + col * scale;
                        var py = baselineY - row * scale;
                        PaintRect(texture, px, py, scale, scale, color);
                    }
                }

                cursor += 6 * scale;
            }
        }

        private static void BlendPixel(Texture2D texture, int x, int y, Color32 source)
        {
            if (source.a == 255)
            {
                texture.SetPixel(x, y, source);
                return;
            }

            var destination = texture.GetPixel(x, y);
            var alpha = source.a / 255f;
            var inverse = 1f - alpha;
            texture.SetPixel(x, y, new Color(
                source.r / 255f * alpha + destination.r * inverse,
                source.g / 255f * alpha + destination.g * inverse,
                source.b / 255f * alpha + destination.b * inverse,
                1f));
        }

        private static string[] GlyphRows(char character) => character switch
        {
            '0' => new[] { "111", "101", "101", "101", "101", "101", "111" },
            '1' => new[] { "010", "110", "010", "010", "010", "010", "111" },
            '2' => new[] { "111", "001", "001", "111", "100", "100", "111" },
            '3' => new[] { "111", "001", "001", "111", "001", "001", "111" },
            '4' => new[] { "101", "101", "101", "111", "001", "001", "001" },
            '5' => new[] { "111", "100", "100", "111", "001", "001", "111" },
            '6' => new[] { "111", "100", "100", "111", "101", "101", "111" },
            '7' => new[] { "111", "001", "001", "010", "010", "010", "010" },
            '8' => new[] { "111", "101", "101", "111", "101", "101", "111" },
            '9' => new[] { "111", "101", "101", "111", "001", "001", "111" },
            'A' => new[] { "010", "101", "101", "111", "101", "101", "101" },
            'B' => new[] { "110", "101", "101", "110", "101", "101", "110" },
            'C' => new[] { "111", "100", "100", "100", "100", "100", "111" },
            'D' => new[] { "110", "101", "101", "101", "101", "101", "110" },
            'E' => new[] { "111", "100", "100", "110", "100", "100", "111" },
            'F' => new[] { "111", "100", "100", "110", "100", "100", "100" },
            'G' => new[] { "111", "100", "100", "101", "101", "101", "111" },
            'H' => new[] { "101", "101", "101", "111", "101", "101", "101" },
            'I' => new[] { "111", "010", "010", "010", "010", "010", "111" },
            'J' => new[] { "001", "001", "001", "001", "101", "101", "111" },
            'K' => new[] { "101", "101", "110", "100", "110", "101", "101" },
            'L' => new[] { "100", "100", "100", "100", "100", "100", "111" },
            'M' => new[] { "101", "111", "111", "101", "101", "101", "101" },
            'N' => new[] { "101", "111", "111", "111", "101", "101", "101" },
            'O' => new[] { "111", "101", "101", "101", "101", "101", "111" },
            'P' => new[] { "111", "101", "101", "111", "100", "100", "100" },
            'Q' => new[] { "111", "101", "101", "101", "111", "001", "001" },
            'R' => new[] { "110", "101", "101", "110", "110", "101", "101" },
            'S' => new[] { "111", "100", "100", "111", "001", "001", "111" },
            'T' => new[] { "111", "010", "010", "010", "010", "010", "010" },
            'U' => new[] { "101", "101", "101", "101", "101", "101", "111" },
            'V' => new[] { "101", "101", "101", "101", "101", "101", "010" },
            'W' => new[] { "101", "101", "101", "101", "111", "111", "101" },
            'X' => new[] { "101", "101", "101", "010", "101", "101", "101" },
            'Y' => new[] { "101", "101", "101", "010", "010", "010", "010" },
            'Z' => new[] { "111", "001", "001", "010", "100", "100", "111" },
            '+' => new[] { "000", "010", "010", "111", "010", "010", "000" },
            ':' => new[] { "000", "010", "000", "000", "010", "000", "000" },
            '-' => new[] { "000", "000", "000", "111", "000", "000", "000" },
            _ => new[] { "111", "001", "010", "010", "000", "010", "000" }
        };

        private static void RenderBatchHudOverlay(RenderTexture renderTexture, string label)
        {
            if (!InternalEditorUtility.inBatchMode)
            {
                return;
            }

            var root = new GameObject("LTW Batch HUD Capture Overlay");
            var cameraObject = new GameObject("LTW Batch HUD Capture Camera");
            var overlayCamera = cameraObject.AddComponent<Camera>();
            try
            {
                const int overlayLayer = 31;
                root.layer = overlayLayer;
                overlayCamera.clearFlags = CameraClearFlags.Depth;
                overlayCamera.backgroundColor = Color.clear;
                overlayCamera.orthographic = true;
                overlayCamera.orthographicSize = 9.6f;
                overlayCamera.nearClipPlane = 0.1f;
                overlayCamera.farClipPlane = 20f;
                overlayCamera.cullingMask = 1 << overlayLayer;
                overlayCamera.targetTexture = renderTexture;
                overlayCamera.transform.position = new Vector3(0f, 0f, 10f);
                overlayCamera.transform.rotation = Quaternion.identity;

                DrawBatchHudScaffold(root, overlayLayer);
                switch (label)
                {
                    case "build-menu-open":
                        DrawBuildMenuOverlay(root, overlayLayer);
                        break;
                    case "send-menu-open":
                        DrawSendMenuOverlay(root, overlayLayer);
                        break;
                    case "lane-selector-open":
                        DrawLaneSelectorOverlay(root, overlayLayer);
                        break;
                    case "results-or-late-match":
                        DrawResultsOverlay(root, overlayLayer);
                        break;
                }

                overlayCamera.Render();
            }
            finally
            {
                overlayCamera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void DrawBatchHudScaffold(GameObject root, int layer)
        {
            var panel = new Color(0.08f, 0.12f, 0.22f, 0.92f);
            var blue = new Color(0.302f, 0.639f, 1f, 1f);
            var mint = new Color(0.349f, 0.882f, 0.714f, 1f);
            var gold = new Color(1f, 0.784f, 0.29f, 1f);
            var cloud = new Color(0.957f, 0.969f, 1f, 1f);

            AddOverlayRect(root, layer, "TopBar", new Vector2(0f, 8.64f), new Vector2(4.78f, 0.56f), panel);
            AddOverlayRect(root, layer, "TopBarAccent", new Vector2(0f, 8.36f), new Vector2(4.78f, 0.05f), gold);
            AddOverlayText(root, layer, "STATS", new Vector2(-1.75f, 8.64f), mint, 0.22f);
            AddOverlayText(root, layer, "220♥ 75G +10 P0", new Vector2(0.15f, 8.64f), cloud, 0.22f);
            AddOverlayRect(root, layer, "PlayButton", new Vector2(3.76f, 8.06f), new Vector2(0.74f, 0.42f), mint);
            AddOverlayText(root, layer, "PLAY", new Vector2(3.76f, 8.06f), panel, 0.18f);

            AddOverlayRect(root, layer, "BuildButton", new Vector2(-4.35f, -8.55f), new Vector2(0.74f, 0.64f), panel);
            AddOverlayRect(root, layer, "BuildAccent", new Vector2(-4.35f, -8.86f), new Vector2(0.74f, 0.05f), mint);
            AddOverlayText(root, layer, "BUILD", new Vector2(-4.35f, -8.55f), mint, 0.16f);
            AddOverlayRect(root, layer, "SendButton", new Vector2(4.35f, -8.55f), new Vector2(0.74f, 0.64f), panel);
            AddOverlayRect(root, layer, "SendAccent", new Vector2(4.35f, -8.86f), new Vector2(0.74f, 0.05f), gold);
            AddOverlayText(root, layer, "SEND", new Vector2(4.35f, -8.55f), gold, 0.16f);
        }

        private static void DrawBuildMenuOverlay(GameObject root, int layer)
        {
            var panel = new Color(0.08f, 0.12f, 0.22f, 0.94f);
            var mint = new Color(0.349f, 0.882f, 0.714f, 1f);
            var gold = new Color(1f, 0.784f, 0.29f, 1f);
            var violet = new Color(0.608f, 0.424f, 1f, 1f);
            var blue = new Color(0.302f, 0.639f, 1f, 1f);
            var cloud = new Color(0.957f, 0.969f, 1f, 1f);

            AddOverlayRect(root, layer, "BuildPanel", new Vector2(0f, -7.2f), new Vector2(4.7f, 1.75f), panel);
            AddOverlayRect(root, layer, "BuildPanelAccent", new Vector2(0f, -8.05f), new Vector2(4.7f, 0.05f), mint);
            AddOverlayText(root, layer, "WARD PALETTE", new Vector2(-1.45f, -6.48f), mint, 0.2f);
            DrawOverlayCard(root, layer, new Vector2(-1.52f, -7.05f), "ARROW", "25G", blue);
            DrawOverlayCard(root, layer, new Vector2(0f, -7.05f), "CTRL", "35G", violet);
            DrawOverlayCard(root, layer, new Vector2(1.52f, -7.05f), "RELAY", "40G", gold);
            DrawOverlayCard(root, layer, new Vector2(-0.78f, -7.67f), "PULSE", "45G", mint);
            DrawOverlayCard(root, layer, new Vector2(0.78f, -7.67f), "PRISM", "60G", cloud);
        }

        private static void DrawSendMenuOverlay(GameObject root, int layer)
        {
            var panel = new Color(0.08f, 0.12f, 0.22f, 0.94f);
            var mint = new Color(0.349f, 0.882f, 0.714f, 1f);
            var gold = new Color(1f, 0.784f, 0.29f, 1f);
            var violet = new Color(0.608f, 0.424f, 1f, 1f);
            var blue = new Color(0.302f, 0.639f, 1f, 1f);
            var red = new Color(1f, 0.38f, 0.44f, 1f);

            AddOverlayRect(root, layer, "SendPanel", new Vector2(0f, -7.2f), new Vector2(4.7f, 1.9f), panel);
            AddOverlayRect(root, layer, "SendPanelAccent", new Vector2(0f, -8.12f), new Vector2(4.7f, 0.05f), gold);
            AddOverlayText(root, layer, "SEND PRESSURE", new Vector2(-1.35f, -6.38f), gold, 0.2f);
            AddOverlayText(root, layer, "GOLD 75", new Vector2(1.45f, -6.38f), mint, 0.16f);
            DrawOverlayCard(root, layer, new Vector2(-1.52f, -7.02f), "RUN", "10G +1", blue);
            DrawOverlayCard(root, layer, new Vector2(0f, -7.02f), "BRUTE", "18G +2", violet);
            DrawOverlayCard(root, layer, new Vector2(1.52f, -7.02f), "SWARM", "18G +3", gold);
            DrawOverlayCard(root, layer, new Vector2(-0.78f, -7.68f), "SHADE", "24G +3", mint);
            DrawOverlayCard(root, layer, new Vector2(0.78f, -7.68f), "SIEGE", "40G +4", red);
        }

        private static void DrawLaneSelectorOverlay(GameObject root, int layer)
        {
            var panel = new Color(0.08f, 0.12f, 0.22f, 0.92f);
            var blue = new Color(0.302f, 0.639f, 1f, 1f);
            AddOverlayRect(root, layer, "LaneRail", new Vector2(4.72f, 2.4f), new Vector2(0.36f, 5.8f), panel);
            AddOverlayText(root, layer, "R", new Vector2(4.72f, 5.05f), Color.white, 0.18f);
            AddOverlayText(root, layer, "L1", new Vector2(4.72f, 4.05f), blue, 0.2f);
            AddOverlayText(root, layer, "L2", new Vector2(4.72f, 3.15f), blue, 0.2f);
            AddOverlayText(root, layer, "L3", new Vector2(4.72f, 2.25f), blue, 0.2f);
        }

        private static void DrawResultsOverlay(GameObject root, int layer)
        {
            var panel = new Color(0.08f, 0.12f, 0.22f, 0.96f);
            var gold = new Color(1f, 0.784f, 0.29f, 1f);
            var cloud = new Color(0.957f, 0.969f, 1f, 1f);
            AddOverlayRect(root, layer, "ResultsPanel", new Vector2(0f, 0.2f), new Vector2(4.2f, 2.2f), panel);
            AddOverlayRect(root, layer, "ResultsAccent", new Vector2(0f, -0.9f), new Vector2(4.2f, 0.06f), gold);
            AddOverlayText(root, layer, "MATCH COMPLETE", new Vector2(0f, 0.85f), gold, 0.26f);
            AddOverlayText(root, layer, "WINNER: P1", new Vector2(0f, 0.2f), cloud, 0.22f);
            AddOverlayText(root, layer, "RESTART", new Vector2(0f, -0.45f), gold, 0.2f);
        }

        private static void DrawOverlayCard(GameObject root, int layer, Vector2 center, string title, string meta, Color accent)
        {
            var panel = new Color(0.08f + accent.r * 0.08f, 0.12f + accent.g * 0.08f, 0.22f + accent.b * 0.08f, 0.94f);
            AddOverlayRect(root, layer, title + "Card", center, new Vector2(1.32f, 0.54f), panel);
            AddOverlayRect(root, layer, title + "Accent", center + new Vector2(0f, -0.25f), new Vector2(1.32f, 0.05f), accent);
            AddOverlayText(root, layer, title, center + new Vector2(0f, 0.09f), Color.white, 0.14f);
            AddOverlayText(root, layer, meta, center + new Vector2(0f, -0.12f), accent, 0.12f);
        }

        private static void AddOverlayRect(GameObject root, int layer, string name, Vector2 center, Vector2 size, Color color)
        {
            var rect = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rect.name = name;
            rect.layer = layer;
            rect.transform.SetParent(root.transform, false);
            rect.transform.localPosition = new Vector3(center.x, center.y, 0f);
            rect.transform.localScale = new Vector3(size.x, size.y, 0.04f);
            if (rect.TryGetComponent<Collider>(out var collider))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            SetOverlayColor(rect, color);
        }

        private static void AddOverlayText(GameObject root, int layer, string text, Vector2 center, Color color, float characterSize)
        {
            var label = new GameObject("Text_" + text);
            label.layer = layer;
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(center.x, center.y, 0.08f);
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 42;
            mesh.characterSize = characterSize;
            mesh.color = color;
        }

        private static void SetOverlayColor(GameObject instance, Color color)
        {
            if (!instance.TryGetComponent<Renderer>(out var renderer))
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                color = color
            };
            renderer.sharedMaterial = material;
        }

        private static void AdvanceState(string? completedLabel)
        {
            if (state == CaptureState.WaitForPlayMode)
            {
                state = CaptureState.OpenBuildMenu;
                return;
            }

            if (string.Equals(completedLabel, "results-or-late-match", StringComparison.OrdinalIgnoreCase))
            {
                state = CaptureState.Done;
            }
        }

        private static void Finish(string? error)
        {
            EditorApplication.update -= Update;
            Time.timeScale = 1f;
            PresentationPreferences.ReducedEffects = false;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            if (error == null)
            {
                Debug.Log($"LTW visual review screenshots captured in {outputDirectory}");
            }
            else
            {
                Debug.LogWarning(error);
            }

            if (exitAfterRun || InternalEditorUtility.inBatchMode)
            {
                EditorApplication.Exit(error == null ? 0 : 1);
            }
        }

        private static void SetPrivateBool(object target, string fieldName, bool value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        private static string ResolveOutputDirectory()
        {
            var explicitOutput = ReadArgumentValue("-ltwCaptureOutputDir");
            if (!string.IsNullOrWhiteSpace(explicitOutput))
            {
                return Path.GetFullPath(explicitOutput);
            }

            return DefaultOutputDirectory;
        }

        private static bool ShouldExitAfterRun() => HasArgument("-ltwExitAfterCapture");

        private static bool HasArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? ReadArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static void WriteGrayscaleCopy(string? label, string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return;
            }

            var bytes = File.ReadAllBytes(sourcePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return;
            }

            var pixels = texture.GetPixels32();
            for (var index = 0; index < pixels.Length; index++)
            {
                var pixel = pixels[index];
                var value = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f), 0, 255);
                pixels[index] = new Color32(value, value, value, pixel.a);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var grayscaleDirectory = Path.Combine(outputDirectory, "grayscale");
            Directory.CreateDirectory(grayscaleDirectory);
            File.WriteAllBytes(Path.Combine(grayscaleDirectory, Path.GetFileName(sourcePath)), ImageConversion.EncodeToPNG(texture));
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private enum CaptureState
        {
            WaitForPlayMode,
            OpenBuildMenu,
            OpenSendMenu,
            OpenLaneSelector,
            ActiveCombat,
            HeavyPressure,
            ReducedEffects,
            Results,
            Done
        }

        private enum CaptureMode
        {
            FullReview,
            RoleLineup,
            ChecklistEvidence
        }
    }
}
