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

        private static void StartCombat(UnitySimulationDriver driver, UnityCommandAdapter commands)
        {
            driver.StartMatch();
            commands.PlaceSampleTower(2, 13);
            commands.PlaceControlTower(4, 12);
            commands.SendSampleCreep();
            commands.SendBruteCreep();
            commands.SendSwarmCreep();
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
                WriteImmediateCapture(path);
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

        private static void WriteImmediateCapture(string path)
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
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>();
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
            RoleLineup
        }
    }
}
