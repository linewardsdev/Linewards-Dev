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
        private static bool aiProofGameplayPrepared;
        private static string captureOutputRoot = DefaultOutputDirectory;
        private static VisualCapturePlan? capturePlan;
        private static VisualCaptureManifest? captureManifest;
        private static bool requireManagedImprovementCycle;

        [MenuItem("Line Wards/Review/Capture Visual Review Set")]
        public static void CaptureVisualReviewSet()
        {
            requireManagedImprovementCycle = false;
            BeginCapture(CaptureMode.FullReview);
        }

        [MenuItem("Line Wards/Review/Capture Mobile Improvement Cycle")]
        public static void CaptureMobileImprovementCycle()
        {
            requireManagedImprovementCycle = true;
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

        [MenuItem("Line Wards/Review/Capture AI Proof Gameplay Review Set")]
        public static void CaptureAiProofGameplayReviewSet()
        {
            BeginCapture(CaptureMode.AiProofGameplay);
        }

        [MenuItem("Line Wards/Review/Capture Role Contact Sheet")]
        public static void CaptureRoleContactSheet()
        {
            outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);
            writeGrayscaleCopies = capturePlan != null || HasArgument("-ltwCaptureGrayscale");

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

        [MenuItem("Line Wards/Review/Capture Stylized Weapon Kit Contact Sheet")]
        public static void CaptureStylizedWeaponKitContactSheet()
        {
            outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);
            writeGrayscaleCopies = HasArgument("-ltwCaptureGrayscale");

            var exitCode = 0;
            try
            {
                var path = Path.Combine(outputDirectory, "01-stylized-weapon-kit-contact-sheet.png");
                RenderStylizedWeaponKitContactSheet(path);
                if (writeGrayscaleCopies)
                {
                    WriteGrayscaleCopy("stylized-weapon-kit-contact-sheet", path);
                }

                Debug.Log($"LTW stylized weapon kit contact sheet captured in {outputDirectory}");
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
            captureOutputRoot = ResolveOutputDirectory();
            capturePlan = mode == CaptureMode.FullReview ? ResolveCapturePlan() : null;
            if (capturePlan != null && !InternalEditorUtility.inBatchMode)
            {
                throw new InvalidOperationException(
                    "The multi-profile mobile capture plan must run in batch mode so every profile uses explicit render dimensions.");
            }

            captureManifest = capturePlan == null ? null : VisualCaptureManifest.Create(capturePlan);
            outputDirectory = capturePlan == null
                ? captureOutputRoot
                : capturePlan.GetOutputDirectory(captureOutputRoot, capturePlan.Profiles[0].name);
            Directory.CreateDirectory(outputDirectory);
            captureIndex = 1;
            pendingCapturePath = null;
            pendingCaptureLabel = null;
            delayedCaptureLabel = null;
            roleLineupPrepared = false;
            aiProofGameplayPrepared = false;
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

            if (captureMode == CaptureMode.AiProofGameplay)
            {
                UpdateAiProofGameplay(driver, commands, placement, sendDock, laneToggle);
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

                case CaptureState.BuildCardSelected:
                    SetPrivateBool(placement, "isPaletteExpanded", true);
                    SetPrivateField(placement, "highlightedTowerRole", 0);
                    ScheduleCaptureThenAdvance("build-card-selected");
                    break;

                case CaptureState.OpenSendMenu:
                    SetPrivateBool(placement, "isPaletteExpanded", false);
                    SetPrivateField(sendDock, "reviewGoldOverride", -1);
                    SetPrivateBool(sendDock, "isExpanded", true);
                    ScheduleCaptureThenAdvance("send-menu-open");
                    break;

                case CaptureState.SendCardDisabled:
                    SetPrivateBool(placement, "isPaletteExpanded", false);
                    SetPrivateBool(sendDock, "isExpanded", true);
                    SetPrivateField(sendDock, "highlightedCreepRole", -1);
                    SetPrivateField(sendDock, "reviewGoldOverride", 0);
                    ScheduleCaptureThenAdvance("send-card-disabled");
                    break;

                case CaptureState.OpenLaneSelector:
                    SetPrivateField(sendDock, "reviewGoldOverride", -1);
                    SetPrivateBool(sendDock, "isExpanded", false);
                    laneToggle.ToggleView();
                    ScheduleCaptureThenAdvance("lane-selector-open");
                    break;

                case CaptureState.ActiveCombat:
                    laneToggle.ShowLaneView();
                    StartCombat(driver, commands);
                    ScheduleCaptureThenAdvance("active-combat");
                    break;

                case CaptureState.RunnerPressure:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 3, 5000);
                    LogCommandResult("canonical runner x10", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.CreepId, 10));
                    ScheduleCaptureThenAdvance("runner-10-pressure", 4.5d);
                    break;

                case CaptureState.SwarmPressure:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 3, 5000);
                    LogCommandResult("canonical swarm heavy", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.SwarmCreepId, 24));
                    ScheduleCaptureThenAdvance("swarm-heavy-pressure", 4.5d);
                    break;

                case CaptureState.HeavyPressure:
                    stress.StartRun();
                    ScheduleCaptureThenAdvance("heavy-pressure", 4d);
                    break;

                case CaptureState.ReducedEffects:
                    PresentationPreferences.ReducedEffects = true;
                    ScheduleCaptureThenAdvance("reduced-effects-heavy", 2d);
                    break;

                case CaptureState.BoardOverview:
                    SetPrivateBool(stress, "running", false);
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    HidePlacementReviewObjects(placement);
                    driver.RefreshSnapshot(drainEvents: true);
                    SetRendererFraming(LaneCameraFraming.BoardOverview);
                    ScheduleCaptureThenAdvance("board-overview", 1.0d);
                    break;

                case CaptureState.SpawnGateFocus:
                    SetRendererFraming(LaneCameraFraming.SpawnGateFocus);
                    ScheduleCaptureThenAdvance("spawn-gate-focus", 0.75d);
                    break;

                case CaptureState.LeakGateFocus:
                    SetRendererFraming(LaneCameraFraming.LeakGateFocus);
                    ScheduleCaptureThenAdvance("leak-gate-focus", 0.75d);
                    break;

                case CaptureState.Results:
                    SetRendererFraming(LaneCameraFraming.ActiveLane);
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
                    state = CaptureState.Done;
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
                    state = CaptureState.OpenSendMenu;
                    break;

                case CaptureState.OpenSendMenu:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 3, 5000);
                    LogCommandResult("checklist shade x6", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.ShadeCreepId, 6));
                    ScheduleCaptureThenAdvance("shade-readability", 4.5d);
                    state = CaptureState.OpenLaneSelector;
                    break;

                case CaptureState.OpenLaneSelector:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 2);
                    driver.StartMatch();
                    LogCommandResult("checklist damaged transfer", commands.CreateDamagedTransferReviewCreep());
                    ScheduleCaptureThenAdvance("damaged-transfer-health", 0.2d);
                    state = CaptureState.ActiveCombat;
                    break;

                case CaptureState.ActiveCombat:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    PresentationPreferences.ReducedEffects = true;
                    StartCombat(driver, commands);
                    ScheduleCaptureThenAdvance("reduced-effects-critical-cues", 3d);
                    state = CaptureState.HeavyPressure;
                    break;

                case CaptureState.HeavyPressure:
                    state = CaptureState.Done;
                    break;

                case CaptureState.Done:
                    Finish(null);
                    break;
            }
        }

        private static void UpdateAiProofGameplay(
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
                    ApplyAiProofVisualOverrides();
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 1, 5000);
                    GrantPlaytestGold(commands, 3, 5000);
                    LogCommandResult("ai proof Runner pressure", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.CreepId, 8));
                    ScheduleCaptureThenAdvance("ai-v04-active-lane", 2.25d);
                    break;

                case CaptureState.OpenBuildMenu:
                    PresentationPreferences.ReducedEffects = true;
                    ScheduleCaptureThenAdvance("ai-v04-active-lane-reduced-effects", 1.25d);
                    state = CaptureState.Done;
                    break;

                case CaptureState.OpenSendMenu:
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
            SetPrivateField(sendDock, "reviewGoldOverride", -1);
            laneToggle.ShowLaneView();
            var renderer = UnityEngine.Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            renderer?.SetActiveLaneCameraId(activeLaneId);
            ClearRendererPresentation(renderer);
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

        private static void ApplyAiProofVisualOverrides()
        {
            if (aiProofGameplayPrepared)
            {
                return;
            }

            aiProofGameplayPrepared = true;

            var renderer = UnityEngine.Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning("Unable to apply AI proof visual overrides because UnityVerticalSliceRenderer was not found.");
                return;
            }

            var arrowProof = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Towers/Tower_Arrow_AIPlate.prefab");
            var runnerProof = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Creeps/Creep_Runner_AIPlate.prefab");
            if (arrowProof == null || runnerProof == null)
            {
                Debug.LogWarning("Unable to apply AI proof visual overrides because proof prefabs were missing.");
                return;
            }

            var towerLibrary = Resources.Load<TowerVisualLibrary>("TowerVisualLibrary");
            var creepLibrary = Resources.Load<CreepVisualLibrary>("CreepVisualLibrary");
            if (towerLibrary == null || creepLibrary == null)
            {
                Debug.LogWarning("Unable to apply AI proof visual overrides because runtime visual libraries were missing.");
                return;
            }

            var towerClone = UnityEngine.Object.Instantiate(towerLibrary);
            towerClone.name = "TowerVisualLibrary_AIProofRuntimeClone";
            var creepClone = UnityEngine.Object.Instantiate(creepLibrary);
            creepClone.name = "CreepVisualLibrary_AIProofRuntimeClone";

            OverrideTowerProfile(towerClone, "tower.arrow", arrowProof, new Vector3(1.18f, 1.28f, 1.18f), 0.12f);
            OverrideCreepProfile(creepClone, "creep.runner", runnerProof, new Vector3(0.94f, 0.54f, 1.26f));
            LogAiProofProfileState(towerClone, creepClone);

            SetPrivateField(renderer, "towerVisualLibrary", towerClone);
            SetPrivateField(renderer, "creepVisualLibrary", creepClone);
            SetPrivateField(renderer, "suppressCreepGameplayOverlays", true);
            ClearPrivateDictionary(renderer, "creepPrefabPools");
            ClearPrivateDictionary(renderer, "activeCreepPoolKeys");
            Debug.Log("Applied review-only AI proof visual overrides to UnityVerticalSliceRenderer.");
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
                ("ARROW", PreferAiProofPrefab("Assets/Prefabs/Towers/Tower_Arrow_AIPlate.prefab", "Assets/Prefabs/Towers/Tower_Arrow.prefab")),
                ("CONTROL", "Assets/Prefabs/Towers/Tower_Control.prefab"),
                ("RELAY", "Assets/Prefabs/Towers/Tower_Relay.prefab"),
                ("PULSE", "Assets/Prefabs/Towers/Tower_Pulse.prefab"),
                ("PRISM", "Assets/Prefabs/Towers/Tower_Prism.prefab"),
            };

            var creepPrefabs = new[]
            {
                ("RUNNER", PreferAiProofPrefab("Assets/Prefabs/Creeps/Creep_Runner_AIPlate.prefab", "Assets/Prefabs/Creeps/Creep_Runner.prefab")),
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

        private static string PreferAiProofPrefab(string proofPath, string fallbackPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(proofPath) != null ? proofPath : fallbackPath;
        }

        private static void RenderStylizedWeaponKitContactSheet(string path)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.5f, 0.54f, 0.62f);

            var cameraObject = new GameObject("StylizedWeaponKitContactSheetCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.09f);
            camera.orthographic = true;
            camera.orthographicSize = 5.85f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.transform.position = new Vector3(0f, 9.25f, -8.8f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.35f, 0f) - camera.transform.position);

            var lightObject = new GameObject("StylizedWeaponKitContactSheetKeyLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.transform.rotation = Quaternion.Euler(54f, -34f, 0f);

            CreateStylizedWeaponKitContactSheetBackdrop();

            var prefabs = new[]
            {
                ("AXE BASIC 1", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Axes/PrefabsAxes/AxeBasic1_2.prefab"),
                ("AXE BASIC 2", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Axes/PrefabsAxes/AxeBasic2_1.prefab"),
                ("AXE EVO", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Axes/PrefabsAxes/AxeEvolving3_3_2.prefab"),
                ("DAGGER 1", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Daggers/_PrefabsDaggers/Dagger1_3_5.prefab"),
                ("DAGGER 4", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Daggers/_PrefabsDaggers/Dagger4_1_3.prefab"),
                ("HAMMER", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Hammers/_PrefabsHammers/Hammer1_1_3.prefab"),
                ("MUSKET", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Musket/_Prefabs_Musket/Musket1_2_1.prefab"),
                ("POLEARM", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Polearms/_Prefabs_Polearms/Polearm2_2_2.prefab"),
                ("SCYTHE", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Scythes/_Prefabs_Scythes/Scythe1_3_2.prefab"),
                ("SHIELD 2", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Shields/_PrefabsShields/Shield2_1_2.prefab"),
                ("SHIELD 3", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Shields/_PrefabsShields/Shield3_1_1.prefab"),
                ("STAFF 2", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Staves/_PrefabsStaves/Staff2_2_6.prefab"),
                ("STAFF 4", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Staves/_PrefabsStaves/Staff4_1_1.prefab"),
                ("STAFF 5", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Staves/_PrefabsStaves/Staff5_1_1.prefab"),
                ("SWORD 1", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Swords/_PrefabsSwords/Sword1_1_3.prefab"),
                ("SWORD 2", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Swords/_PrefabsSwords/Sword2_3_3.prefab"),
                ("SWORD 3", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Swords/_PrefabsSwords/Sword3_1_3.prefab"),
                ("SWORD 5", "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Swords/_PrefabsSwords/Sword5_3_2.prefab"),
            };

            const int columns = 6;
            const float columnSpacing = 2f;
            const float rowSpacing = 2.85f;
            for (var index = 0; index < prefabs.Length; index++)
            {
                var column = index % columns;
                var row = index / columns;
                var x = -5f + column * columnSpacing;
                var z = 3.15f - row * rowSpacing;
                InstantiateNormalizedContactPrefab(prefabs[index].Item2, new Vector3(x, 0f, z), 1.45f, Quaternion.Euler(0f, 180f, 0f));
                AddContactLabel(prefabs[index].Item1, new Vector3(x, 0.05f, z + 1.05f), camera, 0.07f);
            }

            AddContactLabel("STYLIZED WEAPON KIT - SOURCE ASSET TRIAGE", new Vector3(0f, 0.08f, 4.78f), camera, 0.1f, new Color(0.38f, 0.93f, 1f));
            AddContactLabel("Use as Line Wards wrapper-prefab parts; do not depend on vendor paths at runtime.", new Vector3(0f, 0.08f, -4.68f), camera, 0.065f, new Color(0.95f, 0.84f, 0.38f));

            var texture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
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

        private static void CreateStylizedWeaponKitContactSheetBackdrop()
        {
            var material = new Material(FindContactSheetShader())
            {
                color = new Color(0.065f, 0.09f, 0.145f)
            };

            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "StylizedWeaponKitContactSheetBackdrop";
            backdrop.transform.position = new Vector3(0f, -0.08f, 0f);
            backdrop.transform.localScale = new Vector3(13.2f, 0.04f, 10.4f);
            if (backdrop.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }

            if (backdrop.TryGetComponent<Collider>(out var collider))
            {
                UnityEngine.Object.DestroyImmediate(collider);
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

        private static void InstantiateNormalizedContactPrefab(string assetPath, Vector3 position, float targetMaxSize, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Missing stylized weapon kit contact-sheet prefab at {assetPath}.");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab);
            }

            instance.name = prefab.name;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = rotation;
            instance.transform.localScale = Vector3.one;

            var bounds = CalculateRendererBounds(instance);
            var maxSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            var scale = maxSize > 0.001f ? targetMaxSize / maxSize : 1f;
            instance.transform.localScale = Vector3.one * scale;

            bounds = CalculateRendererBounds(instance);
            var offset = position - bounds.center;
            instance.transform.position += offset;
        }

        private static Bounds CalculateRendererBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(instance.transform.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
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
            if (capturePlan != null && captureManifest != null)
            {
                foreach (var profile in capturePlan.Profiles)
                {
                    var safeArea = new Rect(
                        profile.safeAreaInsets.left,
                        profile.safeAreaInsets.bottom,
                        profile.width - profile.safeAreaInsets.left - profile.safeAreaInsets.right,
                        profile.height - profile.safeAreaInsets.top - profile.safeAreaInsets.bottom);
                    var capturePath = capturePlan.GetCapturePath(captureOutputRoot, profile.name, label);
                    try
                    {
                        MobileViewportLayout.SetCaptureViewportOverride(profile.width, profile.height, safeArea);
                        WriteImmediateCapture(capturePath, label, profile.width, profile.height);
                        if (writeGrayscaleCopies)
                        {
                            WriteGrayscaleCopy(label, capturePath);
                        }

                        captureManifest.MarkResult(profile.name, label, success: true);
                    }
                    catch (Exception exception)
                    {
                        captureManifest.MarkResult(profile.name, label, success: false, exception.Message);
                        WriteManagedCaptureArtifacts();
                        throw;
                    }
                    finally
                    {
                        MobileViewportLayout.ClearCaptureViewportOverride();
                    }
                }

                WriteManagedCaptureArtifacts();
                nextActionAt = EditorApplication.timeSinceStartup + 0.5d;
                AdvanceState(label);
                return;
            }

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

        private static void WriteImmediateCapture(
            string path,
            string label,
            int? requestedWidth = null,
            int? requestedHeight = null)
        {
            var width = requestedWidth ?? Math.Max(1080, Screen.width);
            var height = requestedHeight ?? Math.Max(1920, Screen.height);
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.black);
                RenderActiveCameras(renderTexture);
                if (!IsBoardFocusLabel(label))
                {
                    RenderBatchHudOverlay(renderTexture, label);
                }

                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                if (!IsBoardFocusLabel(label))
                {
                    PaintBatchHudOverlay(texture, label);
                }

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

        private static bool IsBoardFocusLabel(string label) =>
            string.Equals(label, "board-overview", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "spawn-gate-focus", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "leak-gate-focus", StringComparison.OrdinalIgnoreCase);

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

            PaintReferenceRect(texture, 360, 1784, 360, 68, panelStrong);
            PaintReferenceRect(texture, 380, 1779, 320, 5, gold);
            PaintReferenceRect(texture, 378, 1800, 70, 34, new Color32(9, 22, 34, 238));
            PaintReferenceRect(texture, 456, 1800, 190, 34, new Color32(9, 18, 32, 238));
            PaintReferenceRect(texture, 654, 1800, 52, 34, new Color32(13, 39, 48, 238));
            PaintReferenceText(texture, "LINE", 388, 1826, mint, 3);
            PaintReferenceText(texture, "L220 G75 +10", 470, 1826, cloud, 3);
            PaintReferenceText(texture, "P0", 668, 1826, blue, 3);
            PaintReferenceActionButton(texture, 866, 1746, 146, 54, "PLAY", 900, 1781, mint, mint, 4);

            PaintReferenceActionButton(texture, 132, 68, 160, 64, "BUILD", 170, 112, mint, mint, 3);
            PaintReferenceActionButton(texture, 788, 68, 160, 64, "SEND", 832, 112, gold, gold, 3);

            switch (label)
            {
                case "build-menu-open":
                    PaintBuildMenuOverlay(texture, panel, blue, mint, gold, violet, cloud);
                    break;
                case "build-card-selected":
                    PaintBuildMenuOverlay(texture, panel, blue, mint, gold, violet, cloud, selectedArrow: true);
                    break;
                case "send-menu-open":
                    PaintSendMenuOverlay(texture, panel, blue, mint, gold, violet, red);
                    break;
                case "send-card-disabled":
                    PaintSendMenuOverlay(texture, panel, blue, mint, gold, violet, red, disabled: true);
                    break;
                case "lane-selector-open":
                    PaintLaneSelectorOverlay(texture, panelStrong, blue, cloud);
                    break;
                case "results-or-late-match":
                    PaintResultsOverlay(texture, panelStrong, gold, cloud);
                    break;
            }
        }

        private static void PaintBuildMenuOverlay(Texture2D texture, Color32 panel, Color32 blue, Color32 mint, Color32 gold, Color32 violet, Color32 cloud, bool selectedArrow = false)
        {
            PaintReferenceRect(texture, 140, 230, 800, 312, panel);
            PaintReferenceRect(texture, 140, 226, 800, 8, mint);
            PaintReferenceText(texture, "BUILD", 184, 504, mint, 4);
            PaintCard(texture, 196, 390, "ARW", "20G", blue, "ui_icon_tower_arrow_v01", selectedArrow);
            PaintCard(texture, 390, 390, "CTRL", "35G", violet, "ui_icon_tower_control_v01");
            PaintCard(texture, 584, 390, "RLY", "40G", gold, "ui_icon_tower_relay_v01");
            PaintCard(texture, 292, 276, "PLS", "45G", mint, "ui_icon_tower_pulse_v01");
            PaintCard(texture, 486, 276, "PRM", "60G", cloud, "ui_icon_tower_prism_v01");
        }

        private static void PaintSendMenuOverlay(Texture2D texture, Color32 panel, Color32 blue, Color32 mint, Color32 gold, Color32 violet, Color32 red, bool disabled = false)
        {
            PaintReferenceRect(texture, 140, 220, 800, 322, panel);
            PaintReferenceRect(texture, 140, 216, 800, 8, gold);
            PaintReferenceText(texture, "SEND", 184, 504, gold, 4);
            PaintReferenceText(texture, disabled ? "G0" : "G75", 760, 504, mint, 4);
            PaintCard(texture, 196, 390, "RUN", "10G +1", blue, "ui_icon_send_runner_v01", disabled: disabled);
            PaintCard(texture, 390, 390, "BRT", "18G +2", violet, "ui_icon_send_brute_v01", disabled: disabled);
            PaintCard(texture, 584, 390, "SWM", "18G +3", gold, "ui_icon_send_swarm_v01", disabled: disabled);
            PaintCard(texture, 292, 276, "SHD", "24G +3", mint, "ui_icon_send_shade_v01", disabled: disabled);
            PaintCard(texture, 486, 276, "SGE", "40G +4", red, "ui_icon_send_siege_v01", disabled: disabled);
        }

        private static void PaintLaneSelectorOverlay(Texture2D texture, Color32 panel, Color32 blue, Color32 cloud)
        {
            PaintReferenceRect(texture, 980, 1080, 76, 276, panel);
            PaintControlButton(texture, 996, 1292, 48, "L1", blue, true);
            PaintControlButton(texture, 996, 1212, 48, "L2", blue, false);
            PaintControlButton(texture, 996, 1132, 48, "L3", blue, false);
            PaintControlButton(texture, 1000, 1002, 56, "L1", new Color32(88, 225, 182, 255), true);
        }

        private static void PaintResultsOverlay(Texture2D texture, Color32 panel, Color32 gold, Color32 cloud)
        {
            PaintReferenceRect(texture, 210, 820, 660, 280, panel);
            PaintReferenceRect(texture, 210, 815, 660, 8, gold);
            PaintReferenceText(texture, "MATCH COMPLETE", 298, 1040, gold, 5);
            PaintReferenceText(texture, "WINNER P1", 380, 960, cloud, 5);
            PaintReferenceText(texture, "RESTART", 432, 885, gold, 4);
        }

        private static void PaintCard(Texture2D texture, int x, int y, string title, string meta, Color32 accent, string iconName, bool selected = false, bool disabled = false)
        {
            const int width = 176;
            const int height = 98;
            var displayAccent = disabled ? new Color32(106, 114, 128, 225) : accent;
            var panel = new Color32(
                (byte)Mathf.Clamp(18 + displayAccent.r / 14, 0, 255),
                (byte)Mathf.Clamp(23 + displayAccent.g / 14, 0, 255),
                (byte)Mathf.Clamp(34 + displayAccent.b / 14, 0, 255),
                disabled ? (byte)220 : (byte)242);
            var edge = disabled ? new Color32(58, 62, 70, 220) : new Color32(78, 83, 88, 236);
            var labelColor = disabled ? new Color32(140, 150, 168, 255) : new Color32(244, 247, 255, 255);
            PaintReferenceRect(texture, x, y, width, height, new Color32(5, 7, 11, 232));
            PaintReferenceRect(texture, x + 3, y + 3, width - 6, height - 6, edge);
            PaintReferenceRect(texture, x + 6, y + 6, width - 12, height - 12, panel);
            PaintReferenceRect(texture, x + 12, y + height - 10, width - 24, 3, edge);
            PaintReferenceRect(texture, x + 12, y + 7, width - 24, 3, edge);
            PaintReferenceRect(texture, x + 7, y + height - 17, 3, 10, displayAccent);
            PaintReferenceRect(texture, x + width - 10, y + height - 17, 3, 10, displayAccent);
            PaintReferenceRect(texture, x + 7, y + 7, 12, 3, displayAccent);
            PaintReferenceRect(texture, x + width - 19, y + 7, 12, 3, displayAccent);
            PaintReferenceRect(texture, x + 20, y + 5, width - 40, 5, displayAccent);
            PaintReferenceRect(texture, x + 56, y + 38, 64, 48, new Color32(6, 10, 16, 214));
            PaintReferenceIcon(texture, iconName, x + 59, y + 41, 58);
            if (disabled)
            {
                PaintReferenceRect(texture, x + 56, y + 38, 64, 48, new Color32(30, 34, 42, 126));
            }

            if (selected)
            {
                PaintReferenceRect(texture, x + 3, y + height - 7, width - 6, 4, displayAccent);
                PaintReferenceRect(texture, x + 3, y + 3, width - 6, 4, displayAccent);
                PaintReferenceRect(texture, x + 3, y + 3, 4, height - 6, displayAccent);
                PaintReferenceRect(texture, x + width - 7, y + 3, 4, height - 6, displayAccent);
                PaintReferenceRect(texture, x + 31, y + height - 17, width - 62, 6, displayAccent);
                PaintReferenceRect(texture, x + 11, y + 43, 6, 24, displayAccent);
                PaintReferenceRect(texture, x + width - 17, y + 43, 6, 24, displayAccent);
                PaintReferenceRect(texture, x + 64, y + 46, 48, 32, new Color32(displayAccent.r, displayAccent.g, displayAccent.b, 72));
            }

            PaintReferenceText(texture, title, x + 58, y + 31, labelColor, 3);
            PaintReferenceText(texture, meta, x + 56, y + 14, displayAccent, 2);
        }

        private static void PaintControlButton(Texture2D texture, int x, int y, int size, string label, Color32 accent, bool active)
        {
            var face = active
                ? new Color32((byte)Mathf.Clamp(accent.r, 0, 255), (byte)Mathf.Clamp(accent.g, 0, 255), (byte)Mathf.Clamp(accent.b, 0, 255), 232)
                : new Color32(20, 28, 42, 236);
            var edge = active ? new Color32(220, 246, 255, 210) : new Color32(88, 94, 104, 220);
            PaintReferenceCircleButton(texture, x, y, size, face, edge, accent);
            var glyph = active ? new Color32(18, 28, 42, 160) : new Color32(accent.r, accent.g, accent.b, 160);
            PaintReferenceRect(texture, x + 11, y + 13, 3, size - 26, glyph);
            PaintReferenceRect(texture, x + 7, y + 14, 4, 4, glyph);
            PaintReferenceRect(texture, x + 7, y + size / 2 - 2, 4, 4, glyph);
            PaintReferenceRect(texture, x + 7, y + size - 18, 4, 4, glyph);
            PaintReferenceText(texture, label, x + 18, y + size / 2 + 10, active ? new Color32(12, 22, 34, 255) : accent, 3);
        }

        private static void PaintReferenceActionButton(Texture2D texture, int x, int y, int width, int height, string label, int textX, int baselineY, Color32 accent, Color32 textColor, int textScale)
        {
            var px = ReferenceX(texture, x);
            var py = ReferenceY(texture, y);
            var w = ReferenceWidth(texture, width);
            var h = ReferenceHeight(texture, height);
            PaintChamferedButton(texture, px, py, w, h, new Color32(9, 14, 28, 236), new Color32(124, 130, 124, 190), accent);
            PaintReferenceText(texture, label, textX, baselineY, textColor, textScale);
        }

        private static void PaintReferenceCircleButton(Texture2D texture, int x, int y, int size, Color32 face, Color32 edge, Color32 accent)
        {
            PaintCircleButton(texture, ReferenceX(texture, x), ReferenceY(texture, y), ReferenceScale(texture, size), face, edge, accent);
        }

        private static void PaintReferenceRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            PaintRect(texture, ReferenceX(texture, x), ReferenceY(texture, y), ReferenceWidth(texture, width), ReferenceHeight(texture, height), color);
        }

        private static void PaintReferenceText(Texture2D texture, string text, int x, int baselineY, Color32 color, int scale)
        {
            PaintText(texture, text, ReferenceX(texture, x), ReferenceY(texture, baselineY), color, ReferenceScale(texture, scale));
        }

        private static void PaintReferenceIcon(Texture2D texture, string iconName, int x, int y, int size)
        {
            PaintIcon(texture, iconName, ReferenceX(texture, x), ReferenceY(texture, y), ReferenceScale(texture, size));
        }

        private static int ReferenceX(Texture2D texture, int value) => Mathf.RoundToInt(value * texture.width / 1080f);

        private static int ReferenceY(Texture2D texture, int value) => Mathf.RoundToInt(value * texture.height / 1920f);

        private static int ReferenceWidth(Texture2D texture, int value) => Mathf.Max(1, Mathf.RoundToInt(value * texture.width / 1080f));

        private static int ReferenceHeight(Texture2D texture, int value) => Mathf.Max(1, Mathf.RoundToInt(value * texture.height / 1920f));

        private static int ReferenceScale(Texture2D texture, int value)
        {
            var scale = Mathf.Min(texture.width / 1080f, texture.height / 1920f);
            return Mathf.Max(1, Mathf.RoundToInt(value * scale));
        }

        private static void PaintIcon(Texture2D texture, string iconName, int x, int y, int size)
        {
            var path = Path.Combine(Application.dataPath, "Resources", "Art", "UI", "Icons", iconName + ".png");
            if (!File.Exists(path))
            {
                return;
            }

            var icon = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(icon, File.ReadAllBytes(path)))
                {
                    return;
                }

                var pixels = icon.GetPixels32();
                for (var py = 0; py < size; py++)
                {
                    var sourceY = Mathf.Clamp(py * icon.height / size, 0, icon.height - 1);
                    for (var px = 0; px < size; px++)
                    {
                        var sourceX = Mathf.Clamp(px * icon.width / size, 0, icon.width - 1);
                        var pixel = pixels[sourceY * icon.width + sourceX];
                        if (pixel.a == 0)
                        {
                            continue;
                        }

                        BlendPixel(texture, x + px, y + py, pixel);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(icon);
            }
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

        private static void PaintChamferedButton(Texture2D texture, int x, int y, int width, int height, Color32 face, Color32 edge, Color32 accent)
        {
            var cut = Mathf.Max(4, Mathf.RoundToInt(Mathf.Min(width, height) * 0.22f));
            for (var py = y; py < y + height; py++)
            {
                for (var px = x; px < x + width; px++)
                {
                    var localX = px - x;
                    var localY = py - y;
                    var outside =
                        localX + localY < cut ||
                        width - 1 - localX + localY < cut ||
                        localX + height - 1 - localY < cut ||
                        width - 1 - localX + height - 1 - localY < cut;
                    if (outside)
                    {
                        continue;
                    }

                    var border =
                        localX < 4 ||
                        localX >= width - 4 ||
                        localY < 4 ||
                        localY >= height - 4 ||
                        localX + localY < cut + 5 ||
                        width - 1 - localX + localY < cut + 5 ||
                        localX + height - 1 - localY < cut + 5 ||
                        width - 1 - localX + height - 1 - localY < cut + 5;
                    BlendPixel(texture, px, py, border ? edge : face);
                }
            }

            var railY = y + Mathf.Max(4, height / 10);
            var railX = x + width / 8;
            PaintRect(texture, railX, railY, width - width / 4, Mathf.Max(3, height / 13), new Color32(accent.r, accent.g, accent.b, 238));
            PaintRect(texture, x + 8, y + height / 2 - 2, 14, 4, new Color32(60, 121, 150, 130));
            PaintRect(texture, x + width - 22, y + height / 2 - 2, 14, 4, new Color32(60, 121, 150, 130));
        }

        private static void PaintCircleButton(Texture2D texture, int x, int y, int size, Color32 face, Color32 edge, Color32 accent)
        {
            var center = (size - 1) * 0.5f;
            for (var py = y; py < y + size; py++)
            {
                for (var px = x; px < x + size; px++)
                {
                    var dx = px - x - center;
                    var dy = py - y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy) / center;
                    if (distance > 1f)
                    {
                        continue;
                    }

                    if (distance > 0.78f)
                    {
                        BlendPixel(texture, px, py, edge);
                    }
                    else if (distance > 0.64f)
                    {
                        BlendPixel(texture, px, py, new Color32(6, 8, 12, 235));
                    }
                    else
                    {
                        BlendPixel(texture, px, py, face);
                    }
                }
            }

            var tick = Mathf.Max(2, size / 14);
            var glow = new Color32(accent.r, accent.g, accent.b, 170);
            PaintRect(texture, x + size / 2 - tick / 2, y + size - size / 5, tick, tick * 2, glow);
            PaintRect(texture, x + size / 2 - tick / 2, y + size / 5 - tick * 2, tick, tick * 2, glow);
            PaintRect(texture, x + size / 5 - tick * 2, y + size / 2 - tick / 2, tick * 2, tick, glow);
            PaintRect(texture, x + size - size / 5, y + size / 2 - tick / 2, tick * 2, tick, glow);
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
                    case "build-card-selected":
                        DrawBuildMenuOverlay(root, overlayLayer);
                        break;
                    case "send-menu-open":
                        DrawSendMenuOverlay(root, overlayLayer);
                        break;
                    case "send-card-disabled":
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

            AddOverlayRect(root, layer, "TopBar", new Vector2(0f, 8.64f), new Vector2(2.2f, 0.5f), panel);
            AddOverlayRect(root, layer, "TopBarAccent", new Vector2(0f, 8.39f), new Vector2(1.96f, 0.04f), gold);
            AddOverlayRect(root, layer, "TopLineCell", new Vector2(-0.78f, 8.64f), new Vector2(0.52f, 0.28f), panel);
            AddOverlayRect(root, layer, "TopSummaryCell", new Vector2(0.06f, 8.64f), new Vector2(1.02f, 0.28f), panel);
            AddOverlayRect(root, layer, "TopPressureCell", new Vector2(0.82f, 8.64f), new Vector2(0.34f, 0.28f), panel);
            AddOverlayText(root, layer, "LINE", new Vector2(-0.78f, 8.64f), mint, 0.16f);
            AddOverlayText(root, layer, "L220 G75 +10", new Vector2(0.06f, 8.64f), cloud, 0.15f);
            AddOverlayText(root, layer, "P0", new Vector2(0.82f, 8.64f), blue, 0.15f);
            AddOverlayTexture(root, layer, "PlayButton", "Art/UI/Chrome/ui_panel_button_option_04_v03", new Vector2(3.76f, 8.06f), new Vector2(0.86f, 0.34f), Color.white);
            AddOverlayText(root, layer, "PLAY", new Vector2(3.76f, 8.06f), mint, 0.15f);

            AddOverlayTexture(root, layer, "BuildButton", "Art/UI/Chrome/ui_panel_button_option_04_v03", new Vector2(-4.28f, -8.55f), new Vector2(0.9f, 0.42f), Color.white);
            AddOverlayText(root, layer, "BUILD", new Vector2(-4.28f, -8.55f), mint, 0.16f);
            AddOverlayTexture(root, layer, "SendButton", "Art/UI/Chrome/ui_panel_button_option_04_v03", new Vector2(4.28f, -8.55f), new Vector2(0.9f, 0.42f), Color.white);
            AddOverlayText(root, layer, "SEND", new Vector2(4.28f, -8.55f), gold, 0.16f);
        }

        private static void DrawBuildMenuOverlay(GameObject root, int layer)
        {
            var panel = new Color(0.08f, 0.12f, 0.22f, 0.94f);
            var mint = new Color(0.349f, 0.882f, 0.714f, 1f);
            var gold = new Color(1f, 0.784f, 0.29f, 1f);
            var violet = new Color(0.608f, 0.424f, 1f, 1f);
            var blue = new Color(0.302f, 0.639f, 1f, 1f);
            var cloud = new Color(0.957f, 0.969f, 1f, 1f);

            AddOverlayRect(root, layer, "BuildPanel", new Vector2(0f, -7.2f), new Vector2(4.85f, 1.95f), panel);
            AddOverlayRect(root, layer, "BuildPanelAccent", new Vector2(0f, -8.14f), new Vector2(4.85f, 0.05f), mint);
            AddOverlayText(root, layer, "BUILD", new Vector2(-1.72f, -6.36f), mint, 0.17f);
            DrawOverlayCard(root, layer, new Vector2(-1.58f, -6.98f), "ARW", "20G", blue);
            DrawOverlayCard(root, layer, new Vector2(0f, -6.98f), "CTRL", "35G", violet);
            DrawOverlayCard(root, layer, new Vector2(1.58f, -6.98f), "RLY", "40G", gold);
            DrawOverlayCard(root, layer, new Vector2(-0.8f, -7.74f), "PLS", "45G", mint);
            DrawOverlayCard(root, layer, new Vector2(0.8f, -7.74f), "PRM", "60G", cloud);
        }

        private static void DrawSendMenuOverlay(GameObject root, int layer)
        {
            var panel = new Color(0.08f, 0.12f, 0.22f, 0.94f);
            var mint = new Color(0.349f, 0.882f, 0.714f, 1f);
            var gold = new Color(1f, 0.784f, 0.29f, 1f);
            var violet = new Color(0.608f, 0.424f, 1f, 1f);
            var blue = new Color(0.302f, 0.639f, 1f, 1f);
            var red = new Color(1f, 0.38f, 0.44f, 1f);

            AddOverlayRect(root, layer, "SendPanel", new Vector2(0f, -7.2f), new Vector2(4.85f, 2.04f), panel);
            AddOverlayRect(root, layer, "SendPanelAccent", new Vector2(0f, -8.18f), new Vector2(4.85f, 0.05f), gold);
            AddOverlayText(root, layer, "SEND", new Vector2(-1.72f, -6.32f), gold, 0.17f);
            AddOverlayText(root, layer, "G75", new Vector2(1.58f, -6.32f), mint, 0.15f);
            DrawOverlayCard(root, layer, new Vector2(-1.58f, -6.96f), "RUN", "10G +1", blue);
            DrawOverlayCard(root, layer, new Vector2(0f, -6.96f), "BRT", "18G +2", violet);
            DrawOverlayCard(root, layer, new Vector2(1.58f, -6.96f), "SWM", "18G +3", gold);
            DrawOverlayCard(root, layer, new Vector2(-0.8f, -7.74f), "SHD", "24G +3", mint);
            DrawOverlayCard(root, layer, new Vector2(0.8f, -7.74f), "SGE", "40G +4", red);
        }

        private static void DrawLaneSelectorOverlay(GameObject root, int layer)
        {
            var panel = new Color(0.08f, 0.12f, 0.22f, 0.92f);
            var blue = new Color(0.302f, 0.639f, 1f, 1f);
            var mint = new Color(0.349f, 0.882f, 0.714f, 1f);
            AddOverlayRect(root, layer, "LaneRail", new Vector2(4.58f, 2.85f), new Vector2(0.58f, 2.2f), panel);
            DrawOverlayControl(root, layer, new Vector2(4.58f, 3.72f), "L1", blue, true);
            DrawOverlayControl(root, layer, new Vector2(4.58f, 3.12f), "L2", blue, false);
            DrawOverlayControl(root, layer, new Vector2(4.58f, 2.52f), "L3", blue, false);
            DrawOverlayControl(root, layer, new Vector2(4.58f, 1.18f), "L1", mint, true);
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
            AddOverlayTexture(root, layer, title + "Card", "Art/UI/Chrome/ui_command_card_normal_option_04", center, new Vector2(1.36f, 0.82f), Color.white);
            AddOverlayRect(root, layer, title + "BottomRail", center + new Vector2(0f, -0.34f), new Vector2(1.08f, 0.035f), accent);
            AddOverlayText(root, layer, title, center + new Vector2(0.12f, 0.11f), Color.white, 0.11f);
            AddOverlayText(root, layer, meta, center + new Vector2(0.12f, -0.14f), accent, 0.09f);
        }

        private static void DrawOverlayControl(GameObject root, int layer, Vector2 center, string label, Color accent, bool active)
        {
            var text = active ? Color.white : accent;
            AddOverlayTexture(root, layer, label + "ControlChrome" + center.y, "Art/UI/Chrome/ui_round_button_option_01_v03", center, new Vector2(0.48f, 0.48f), active ? Color.white : new Color(0.75f, 0.82f, 0.92f, 0.8f));
            AddOverlayRect(root, layer, label + "ControlV" + center.y, center + new Vector2(-0.1f, 0f), new Vector2(0.025f, 0.22f), text);
            AddOverlayRect(root, layer, label + "ControlA" + center.y, center + new Vector2(-0.15f, 0.09f), new Vector2(0.035f, 0.035f), text);
            AddOverlayRect(root, layer, label + "ControlB" + center.y, center + new Vector2(-0.15f, 0f), new Vector2(0.035f, 0.035f), text);
            AddOverlayRect(root, layer, label + "ControlC" + center.y, center + new Vector2(-0.15f, -0.09f), new Vector2(0.035f, 0.035f), text);
            AddOverlayText(root, layer, label, center + new Vector2(0.045f, 0f), text, 0.14f);
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

        private static void AddOverlayTexture(GameObject root, int layer, string name, string resourcePath, Vector2 center, Vector2 size, Color tint)
        {
            var texture = Resources.Load<Texture2D>(resourcePath)
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/" + resourcePath + ".png");
            if (texture == null)
            {
                AddOverlayRect(root, layer, name + "Fallback", center, size, tint);
                return;
            }

            var spriteObject = new GameObject(name);
            spriteObject.layer = layer;
            spriteObject.transform.SetParent(root.transform, false);
            spriteObject.transform.localPosition = new Vector3(center.x, center.y, 0.02f);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = 50;
            var spriteSize = renderer.bounds.size;
            spriteObject.transform.localScale = new Vector3(
                spriteSize.x > 0f ? size.x / spriteSize.x : 1f,
                spriteSize.y > 0f ? size.y / spriteSize.y : 1f,
                1f);
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

        private static void SetRendererFraming(LaneCameraFraming framing)
        {
            var renderer = UnityEngine.Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            renderer?.SetCameraFraming(framing);
        }

        private static void ClearRendererPresentation(UnityVerticalSliceRenderer? renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var method = typeof(UnityVerticalSliceRenderer).GetMethod("ReleaseAllActiveObjects", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(renderer, Array.Empty<object>());
        }

        private static void HidePlacementReviewObjects(TouchPlacementController placement)
        {
            SetPrivateBool(placement, "isPlacing", false);
            HidePrivateGameObject(placement, "ghost");
            HidePrivateGameObject(placement, "builderAvatar");
            HidePrivateGameObject(placement, "selectionRing");
        }

        private static void HidePrivateGameObject(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(target) is GameObject gameObject)
            {
                gameObject.SetActive(false);
            }
        }

        private static void Finish(string? error)
        {
            EditorApplication.update -= Update;
            Time.timeScale = 1f;
            PresentationPreferences.ReducedEffects = false;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            MobileViewportLayout.ClearCaptureViewportOverride();
            if (captureManifest != null)
            {
                WriteManagedCaptureArtifacts();
            }

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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        private static void ClearPrivateDictionary(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(target) is System.Collections.IDictionary dictionary)
            {
                dictionary.Clear();
            }
        }

        private static void LogAiProofProfileState(TowerVisualLibrary towerLibrary, CreepVisualLibrary creepLibrary)
        {
            var towerProfile = towerLibrary.FindProfile("tower.arrow");
            var creepProfile = creepLibrary.FindProfile("creep.runner");
            Debug.Log(
                $"AI proof profile state: tower.arrow prefab='{towerProfile?.Prefab?.name ?? "<missing>"}', " +
                $"creep.runner prefab='{creepProfile?.Prefab?.name ?? "<missing>"}', " +
                $"scale='{(creepProfile != null ? creepProfile.Scale.ToString() : "<missing>")}', " +
                $"bodyPath='{creepProfile?.BodyRendererPath ?? "<missing>"}', " +
                $"senderPaths={creepProfile?.SenderAccentRendererPaths.Count ?? -1}, " +
                $"damagePaths={creepProfile?.DamageRendererPaths.Count ?? -1}.");
        }

        private static void OverrideTowerProfile(TowerVisualLibrary library, string towerId, GameObject prefab, Vector3 scale, float lift)
        {
            var serializedLibrary = new SerializedObject(library);
            var profiles = serializedLibrary.FindProperty("profiles");
            var profile = FindProfileProperty(profiles, "towerId", towerId);
            if (profile == null)
            {
                Debug.LogWarning($"Unable to apply AI proof tower override for '{towerId}' because the profile was not found.");
                return;
            }

            profile.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            profile.FindPropertyRelative("scale").vector3Value = scale;
            profile.FindPropertyRelative("lift").floatValue = lift;
            profile.FindPropertyRelative("rangeHaloRendererPath").stringValue = "RangeHaloInactiveForAiProofReview";
            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void OverrideCreepProfile(CreepVisualLibrary library, string creepId, GameObject prefab, Vector3 scale)
        {
            var serializedLibrary = new SerializedObject(library);
            var profiles = serializedLibrary.FindProperty("profiles");
            var profile = FindProfileProperty(profiles, "creepId", creepId);
            if (profile == null)
            {
                Debug.LogWarning($"Unable to apply AI proof creep override for '{creepId}' because the profile was not found.");
                return;
            }

            profile.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            profile.FindPropertyRelative("scale").vector3Value = scale;
            profile.FindPropertyRelative("bodyRendererPath").stringValue = "BodyInactiveForAiProofReview";
            profile.FindPropertyRelative("senderAccentRendererPaths").arraySize = 0;
            profile.FindPropertyRelative("damageRendererPaths").arraySize = 0;
            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty? FindProfileProperty(SerializedProperty profiles, string idProperty, string id)
        {
            for (var index = 0; index < profiles.arraySize; index++)
            {
                var profile = profiles.GetArrayElementAtIndex(index);
                if (string.Equals(profile.FindPropertyRelative(idProperty).stringValue, id, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            return null;
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

        private static VisualCapturePlan? ResolveCapturePlan()
        {
            var runId = ReadArgumentValue("-ltwCaptureRunId");
            if (string.IsNullOrWhiteSpace(runId))
            {
                if (!requireManagedImprovementCycle)
                {
                    return null;
                }

                runId = "mobile-improvement-cycle";
            }

            return VisualCapturePlan.FromValues(
                runId!,
                ReadArgumentValue("-ltwCapturePhase") ?? "after",
                ReadArgumentValue("-ltwCaptureSeed") ?? "1",
                ReadArgumentValue("-ltwCaptureProfiles"));
        }

        private static void WriteManagedCaptureArtifacts()
        {
            if (capturePlan == null || captureManifest == null)
            {
                return;
            }

            var runDirectory = Path.Combine(captureOutputRoot, capturePlan.RunId);
            Directory.CreateDirectory(runDirectory);
            var phaseManifestName = $"{capturePlan.PhaseName}-capture-manifest.json";
            var phaseReviewName = $"{capturePlan.PhaseName}-review.md";
            var manifestJson = captureManifest.ToJson();
            var reviewMarkdown = captureManifest.GenerateReviewMarkdown();
            File.WriteAllText(
                Path.Combine(runDirectory, "capture-manifest.json"),
                manifestJson);
            File.WriteAllText(
                Path.Combine(runDirectory, phaseManifestName),
                manifestJson);
            File.WriteAllText(
                Path.Combine(runDirectory, "review.md"),
                reviewMarkdown);
            File.WriteAllText(
                Path.Combine(runDirectory, phaseReviewName),
                reviewMarkdown);
            VisualImprovementCycleReport.WriteArtifacts(
                capturePlan,
                captureManifest,
                captureOutputRoot,
                ReadArgumentValue("-ltwCapturePackage") ?? "GD-Mobile-Regression",
                ReadArgumentValue("-ltwIntensity") ?? "standard");
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

            var grayscaleDirectory = Path.Combine(Path.GetDirectoryName(sourcePath)!, "grayscale");
            Directory.CreateDirectory(grayscaleDirectory);
            File.WriteAllBytes(Path.Combine(grayscaleDirectory, Path.GetFileName(sourcePath)), ImageConversion.EncodeToPNG(texture));
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private enum CaptureState
        {
            WaitForPlayMode,
            OpenBuildMenu,
            BuildCardSelected,
            OpenSendMenu,
            SendCardDisabled,
            OpenLaneSelector,
            ActiveCombat,
            RunnerPressure,
            SwarmPressure,
            HeavyPressure,
            ReducedEffects,
            BoardOverview,
            SpawnGateFocus,
            LeakGateFocus,
            Results,
            Done
        }

        private enum CaptureMode
        {
            FullReview,
            RoleLineup,
            ChecklistEvidence,
            AiProofGameplay
        }
    }
}
