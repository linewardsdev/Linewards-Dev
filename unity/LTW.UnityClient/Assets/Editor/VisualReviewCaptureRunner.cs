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
using UnityEngine.Rendering;

namespace LTW.UnityClient.Editor
{
    /// <remarks>
    /// THESE CAPTURES CONTAIN THE BOARD ONLY. In batchmode IMGUI does not draw into an offscreen
    /// RenderTexture, so the HUD is simply absent from the output. That is deliberate. This runner
    /// used to reconstruct the HUD on the CPU after readback with hand-rolled 3x7 bitmap glyphs,
    /// and the painting was indistinguishable from the real thing at a glance: one review pass
    /// filed five UI defects that were every one an artefact of the mock, and two attempts to fix
    /// the game's font produced byte-identical captures, because the game's font was not what was
    /// in the picture. It also drifted — it still painted the pre-expansion 5-creep send menu long
    /// after the roster grew to 10 — and it failed silently, depicting an older build rather than
    /// erroring. An empty region is honest; a convincing painting of a stale HUD is not.
    ///
    /// For UI, use RealUiCaptureRunner, which drives a real Game view so IMGUI actually renders.
    ///
    /// Run WITHOUT -nographics. That flag disables the graphics device, so captures come out as a
    /// single flat colour while still reporting success and writing the expected files — the
    /// blank result is only visible by inspecting the pixels. Use:
    ///     Unity -batchmode -projectPath &lt;project&gt; -executeMethod &lt;method&gt; -logFile &lt;log&gt;
    /// </remarks>
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
            exitAfterRun = ShouldExitAfterRun();
            writeGrayscaleCopies = HasArgument("-ltwCaptureGrayscale");
            // MobileViewportLayout falls back to Screen, which in batch mode is a small landscape
            // surface, and ConfigureDefaultCamera turns that into a letterboxed camera rect during
            // LateUpdate. The override has to be in place for the whole session, not just at
            // readback, because the rect is set on play frames well before any capture happens.
            if (capturePlan == null && !MobileViewportLayout.HasCaptureViewportOverride)
            {
                const int captureWidth = 1080;
                const int captureHeight = 1920;
                MobileViewportLayout.SetCaptureViewportOverride(
                    captureWidth, captureHeight, new Rect(0f, 0f, captureWidth, captureHeight));
            }

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
                    PlaceReviewDefenceLine(commands);
                    LogCommandResult("canonical runner x10", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.CreepId, 10));
                    ScheduleCaptureThenAdvance("runner-10-pressure", 4.5d);
                    break;

                case CaptureState.SwarmPressure:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 3, 5000);
                    PlaceReviewDefenceLine(commands);
                    LogCommandResult("canonical swarm heavy", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.SwarmCreepId, 24));
                    ScheduleCaptureThenAdvance("swarm-heavy-pressure", 4.5d);
                    break;

                case CaptureState.HeavyPressure:
                    PlaceReviewDefenceLine(commands);
                    stress.StartRun((SenderFeedingLane(commands, FramedLaneId()) ?? new PlayerId(3)).Value);
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
                    PlaceReviewDefenceLine(commands);
                    LogCommandResult("checklist runner x10", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.CreepId, 10));
                    ScheduleCaptureThenAdvance("runner-10-pressure", 4.5d);
                    break;

                case CaptureState.OpenBuildMenu:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 3, 5000);
                    PlaceReviewDefenceLine(commands);
                    LogCommandResult("checklist swarm x24", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.SwarmCreepId, 24));
                    ScheduleCaptureThenAdvance("swarm-heavy-pressure", 4.5d);
                    state = CaptureState.OpenSendMenu;
                    break;

                case CaptureState.OpenSendMenu:
                    ResetChecklistScenario(commands, placement, sendDock, laneToggle, activeLaneId: 1);
                    driver.StartMatch();
                    GrantPlaytestGold(commands, 3, 5000);
                    PlaceReviewDefenceLine(commands);
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

        /// <summary>
        /// Places a defending line in lane 1 so a pressure state shows creeps being shot at rather
        /// than walking an empty board.
        /// </summary>
        /// <remarks>
        /// ResetChecklistScenario calls ResetMatch, which clears the towers StartCombat placed. The
        /// states after active-combat therefore captured zero towers on camera, so tower aim, muzzle
        /// anchoring, recoil and the ring VFX could not be reviewed under load — which is most of
        /// what those states exist to show.
        /// </remarks>
        private static void PlaceReviewDefenceLine(UnityCommandAdapter commands)
        {
            GrantPlaytestGold(commands, 1, 4000);
            LogCommandResult("review defence Arrow", commands.PlaceSampleTower(2, 13));
            LogCommandResult("review defence Control", commands.PlaceControlTower(4, 12));
            LogCommandResult("review defence Relay", commands.PlaceUtilityTower(2, 9));
            LogCommandResult("review defence Pulse", commands.PlacePulseTower(4, 8));
            LogCommandResult("review defence Prism", commands.PlacePrismTower(2, 5));
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
            GrantPlaytestGold(commands, 1, 6000);

            LogCommandResult("lineup Arrow tower", commands.PlaceSampleTower(1, 14));
            LogCommandResult("lineup Control tower", commands.PlaceControlTower(5, 14));
            LogCommandResult("lineup Relay tower", commands.PlaceUtilityTower(1, 11));
            LogCommandResult("lineup Pulse tower", commands.PlacePulseTower(5, 11));
            LogCommandResult("lineup Prism tower", commands.PlacePrismTower(2, 8));

            // Roles 5..14 are the Foundry and Grove lines. Placed down the two free columns beside
            // the lane so all fifteen meshes appear in one frame — this state exists to review
            // silhouettes, and reviewing only a third of the roster defeats it.
            // Outer columns 0 and 6, five rows each. The original five sit at x 1, 2 and 5, and
            // y 16 is out of bounds, so an earlier pass down x 1/5 from y 16 lost three towers to
            // CellOccupied and one to InvalidLane without that being obvious in the capture.
            for (var role = 5; role < 15; role++)
            {
                var slot = role - 5;
                var x = slot < 5 ? 0 : 6;
                var y = 15 - slot % 5 * 2;
                LogCommandResult($"lineup role {role}", commands.PlaceTowerByRole(role, x, y));
            }

            GrantPlaytestGold(commands, 3, 2000);
            LogCommandResult("lineup Runner visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.CreepId, 1));
            LogCommandResult("lineup Brute visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.BruteCreepId, 1));
            LogCommandResult("lineup Swarm visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.SwarmCreepId, 3));
            LogCommandResult("lineup Shade visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.ShadeCreepId, 1));
            LogCommandResult("lineup Siege visible send", QueueVisibleLineupCreep(commands, SampleVerticalSliceContent.SiegeCreepId, 1));
            driver.RefreshSnapshot(drainEvents: true);
        }

        private static void CreateContactSheetLight(string name, Vector3 eulerAngles, Color color, float intensity)
        {
            var lightObject = new GameObject($"RoleContactSheet{name}Light");
            lightObject.transform.rotation = Quaternion.Euler(eulerAngles);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = name == "Key" ? LightShadows.Soft : LightShadows.None;
            if (light.shadows == LightShadows.Soft)
            {
                light.shadowStrength = 0.55f;
            }
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

            // Mirror the runtime rig from LocalVerticalSliceLauncher so the sheet reviews what the
            // game actually renders. A single key over flat ambient flatters models the game never
            // shows that way.
            CreateContactSheetLight("Key", new Vector3(50f, -35f, 0f), new Color(1f, 0.957f, 0.878f), 1.2f);
            CreateContactSheetLight("Fill", new Vector3(30f, 145f, 0f), new Color(0.722f, 0.804f, 1f), 0.35f);
            CreateContactSheetLight("Rim", new Vector3(15f, 180f, 0f), new Color(0.851f, 0.902f, 1f), 0.5f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.322f, 0.361f, 0.451f);
            RenderSettings.ambientEquatorColor = new Color(0.212f, 0.227f, 0.259f);
            RenderSettings.ambientGroundColor = new Color(0.114f, 0.125f, 0.157f);

            // Match the game's post-processing so the sheet is graded the same way a match is.
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var profile = Resources.Load<VolumeProfile>("LTW_PostProcessing");
                if (profile != null)
                {
                    var volumeObject = new GameObject("RoleContactSheetPostProcessing");
                    var volume = volumeObject.AddComponent<Volume>();
                    volume.isGlobal = true;
                    volume.sharedProfile = profile;
                    var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
                                     ?? camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    cameraData.renderPostProcessing = true;
                }
            }


            CreateContactSheetBackdrop();

            // These must track TowerVisualLibrary and CreepVisualLibrary. The sheet previously
            // rendered the pre-3D primitive prefabs, so no review captured through it ever showed
            // the meshes the game loads.
            var towerPrefabs = new[]
            {
                ("ARROW", "Assets/Prefabs/Towers/Tower_Arrow_3D.prefab"),
                ("CONTROL", "Assets/Prefabs/Towers/Tower_Control_3D.prefab"),
                ("RELAY", "Assets/Prefabs/Towers/Tower_Relay_3D.prefab"),
                ("PULSE", "Assets/Prefabs/Towers/Tower_Pulse_3D.prefab"),
                ("PRISM", "Assets/Prefabs/Towers/Tower_Prism_3D.prefab"),
            };

            var creepPrefabs = new[]
            {
                ("RUNNER", "Assets/Prefabs/Creeps/Creep_Runner_3D.prefab"),
                ("BRUTE", "Assets/Prefabs/Creeps/Creep_Brute_3D.prefab"),
                ("SWARM", "Assets/Prefabs/Creeps/Creep_Swarm_3D.prefab"),
                ("SHADE", "Assets/Prefabs/Creeps/Creep_Shade_3D.prefab"),
                ("SIEGE", "Assets/Prefabs/Creeps/Creep_Siege_3D.prefab"),
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

                // The first couple of frames after building this scene render measurably
                // different (and visibly washed-out on lit models) from the third and every
                // subsequent frame, which are all pixel-identical to each other. The scene is
                // brand new (fresh empty scene, camera and lights created moments ago), so
                // something - most likely URP compiling/resolving the correct shader variant for
                // the freshly bound metallic/emission keywords, though this was not root-caused
                // further - has not converged yet on frame 0/1. Rendering a couple of throwaway
                // warm-up frames first makes the capture deterministic and avoids reporting a
                // transient artifact as if it were the material's real appearance.
                const int warmupFrames = 2;
                for (var w = 0; w < warmupFrames; w++)
                {
                    RenderCameraToTarget(camera, texture);
                }

                RenderCameraToTarget(camera, texture);
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

                // See the matching comment in RenderRoleContactSheet: the first couple of frames
                // after building a fresh scene render measurably different from every frame after,
                // which are all stable. Warm up before the real capture.
                const int warmupFrames = 2;
                for (var w = 0; w < warmupFrames; w++)
                {
                    RenderCameraToTarget(camera, texture);
                }

                RenderCameraToTarget(camera, texture);
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
            ?? LTW.UnityClient.Simulation.RenderCompat.Lit;

        private static void GrantPlaytestGold(UnityCommandAdapter commands, int playerId, int amount)
        {
            var simulation = GetLocalSimulation(commands);
            simulation?.GrantLocalPlaytestGold(new PlayerId(playerId), new Gold(amount));
        }

        /// <summary>
        /// The player whose sends land in the lane the camera is framing, asked of the live
        /// simulation rather than derived here.
        /// </summary>
        /// <remarks>
        /// This used to compute the sender locally as laneId - 1, which duplicated both the
        /// routing rule and the lane count, and went wrong the moment a player was eliminated:
        /// NextActiveOpponent then skips past them, so the sends landed in a lane the camera was
        /// not framing and the capture looked idle. Before that it was hardcoded to player 3,
        /// which with 8 lanes always meant lane 4.
        /// </remarks>
        private static PlayerId? SenderFeedingLane(UnityCommandAdapter commands, int laneId)
        {
            var simulation = GetLocalSimulation(commands);
            return simulation?.LocalPlaytestSenderForLane(new LaneId(laneId));
        }

        private static int FramedLaneId()
        {
            var renderer = UnityEngine.Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            return renderer != null ? renderer.ActiveLaneCameraId : 1;
        }

        private static VerticalSliceCommandResult QueueVisibleLineupCreep(UnityCommandAdapter commands, ContentId creepId, int quantity)
        {
            var simulation = GetLocalSimulation(commands);
            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(LTW.Simulation.Commands.CommandRejectionReason.MatchPaused);
            }

            var lane = FramedLaneId();
            var sender = simulation.LocalPlaytestSenderForLane(new LaneId(lane));
            if (sender is null)
            {
                Debug.LogWarning($"CAPTURE no active sender routes to lane {lane}; skipping send");
                return VerticalSliceCommandResult.Reject(LTW.Simulation.Commands.CommandRejectionReason.InvalidPlayer);
            }

            simulation.GrantLocalPlaytestGold(sender.Value, new Gold(5000));
            simulation.ClearLocalPlaytestSendCooldown(sender.Value);
            return simulation.QueueSend(sender.Value, creepId, quantity);
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

        /// <summary>
        /// Records what was actually on the board when a state was captured.
        /// </summary>
        /// <remarks>
        /// A review state that produces no load looks identical in the log to one that works: the
        /// capture succeeds and the file is written either way, and the shortfall is only visible
        /// by opening the image and counting. That is how `heavy-pressure` came to be captured with
        /// a single creep on the board without anyone noticing. Printing the counts next to each
        /// capture means a scenario that failed to populate says so in the log.
        /// </remarks>
        private static void LogBoardContents(string label)
        {
            var driver = UnityEngine.Object.FindAnyObjectByType<UnitySimulationDriver>();
            var snapshot = driver?.LatestSnapshot;
            if (snapshot is null)
            {
                Debug.Log($"CAPTURE {label}: no snapshot");
                return;
            }

            // Board-wide totals are not what the reviewer sees. The camera frames one lane, so a
            // state can hold hundreds of creeps and still capture an empty board if they are all
            // somewhere else. Report the framed lane separately from the total.
            var renderer = UnityEngine.Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            var lane = renderer != null ? renderer.ActiveLaneCameraId : 0;
            var laneCreeps = 0;
            for (var index = 0; index < snapshot.Creeps.Count; index++)
            {
                if (snapshot.Creeps[index].LaneId.Value == lane)
                {
                    laneCreeps++;
                }
            }

            var laneTowers = 0;
            for (var index = 0; index < snapshot.Towers.Count; index++)
            {
                if (snapshot.Towers[index].LaneId.Value == lane)
                {
                    laneTowers++;
                }
            }

            var byLane = new System.Collections.Generic.Dictionary<int, int>();
            for (var index = 0; index < snapshot.Creeps.Count; index++)
            {
                var id = snapshot.Creeps[index].LaneId.Value;
                byLane[id] = byLane.TryGetValue(id, out var n) ? n + 1 : 1;
            }

            var spread = string.Join(",", System.Linq.Enumerable.Select(
                System.Linq.Enumerable.OrderBy(byLane, kv => kv.Key), kv => $"L{kv.Key}={kv.Value}"));

            Debug.Log(
                $"CAPTURE {label}: lane={lane} onCamera creeps={laneCreeps} towers={laneTowers} " +
                $"| boardWide creeps={snapshot.Creeps.Count} towers={snapshot.Towers.Count} | {spread}");
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

            // Without an override, MobileViewportLayout reads Screen, which in batch mode is a
            // small landscape surface. CameraRect() then letterboxes the presentation camera to
            // roughly 42% of the target width. The Built-in path happened to tolerate that; a
            // scriptable pipeline honours the viewport rect and the board rendered about half its
            // width. Describing the actual capture surface fixes it for both pipelines.
            try
            {
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.black);
                RenderActiveCameras(renderTexture);

                // A scriptable pipeline binds its own targets while rendering and does not restore
                // this one, so the active target has to be re-established before reading back or
                // ReadPixels samples whatever surface the pipeline left bound.
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);

                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
                LogBoardContents(label);
                Debug.Log($"Saved visual review capture {path}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        /// <summary>
        /// Renders one camera into a target, using whichever path the active render pipeline
        /// supports.
        /// </summary>
        /// <remarks>
        /// <c>Camera.Render()</c> is a Built-in pipeline call and does nothing under a scriptable
        /// pipeline. When URP was first assigned, every capture came back blank white: not because
        /// the game had broken, but because this harness silently rendered nothing. A capture tool
        /// that fails quietly during a pipeline migration is worse than no tool, since a blank
        /// frame reads as catastrophic damage.
        ///
        /// Both paths are kept because main is still Built-in while the migration runs on a
        /// branch, so captures have to stay comparable across the two.
        /// </remarks>
        private static void RenderCameraToTarget(Camera camera, RenderTexture renderTexture)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                camera.Render();
                return;
            }

            // URP derives the projection from the request's destination texture, so a manually
            // pinned Camera.aspect is ignored while still being reported by the property. Leaving
            // ours pinned made the board render at the batch-mode screen aspect instead of the
            // capture's portrait aspect, shrinking it to roughly a third of its baseline width.
            // Releasing the override lets the pipeline derive the correct projection.
            var pinnedTarget = camera.targetTexture;
            camera.targetTexture = null;
            camera.ResetAspect();

            var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest
            {
                destination = renderTexture
            };

            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                if (HasArgument("-ltwCaptureDebugCamera"))
                {
                    Debug.Log($"CAMDEBUG name={camera.name} ortho={camera.orthographic} size={camera.orthographicSize:F3} " +
                              $"aspect={camera.aspect:F4} rt={renderTexture.width}x{renderTexture.height} " +
                              $"screen={Screen.width}x{Screen.height}");
                }

                RenderPipeline.SubmitRenderRequest(camera, request);
                camera.targetTexture = pinnedTarget;

                if (HasArgument("-ltwCaptureDebugCamera"))
                {
                    Debug.Log($"CAMDEBUG after submit: aspect={camera.aspect:F4}");
                }

                return;
            }

            camera.targetTexture = pinnedTarget;
            Debug.LogError(
                $"Active render pipeline {GraphicsSettings.currentRenderPipeline.GetType().Name} rejected a " +
                $"single camera render request for '{camera.name}'. The capture would be blank, so it is being " +
                "reported rather than saved as if it were a real frame.");
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
                RenderCameraToTarget(camera, renderTexture);
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
            ChecklistEvidence
        }
    }
}
