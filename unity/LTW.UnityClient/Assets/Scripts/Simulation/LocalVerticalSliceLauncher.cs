using UnityEngine;
using LTW.UnityClient.UI;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Creates the local vertical slice when a development scene is played. It deliberately
    /// composes client components only; match rules remain in LTW.Simulation.
    /// </summary>
    public static class LocalVerticalSliceLauncher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Object.FindAnyObjectByType<UnitySimulationDriver>() != null)
            {
                return;
            }

            var matchObject = new GameObject("LTW Local Vertical Slice");
            var driver = matchObject.AddComponent<UnitySimulationDriver>();
            var commands = matchObject.AddComponent<UnityCommandAdapter>();
            var bootstrapper = matchObject.AddComponent<UnityMatchBootstrapper>();
            var renderer = matchObject.AddComponent<UnityVerticalSliceRenderer>();
            var replayExporter = matchObject.AddComponent<LocalReplayExporter>();
            var playtestRecorder = matchObject.AddComponent<LocalPlaytestRecorder>();
            var performanceSampler = matchObject.AddComponent<DevicePerformanceSampler>();
            var stressHarness = matchObject.AddComponent<HeavySendStressHarness>();
            var results = new GameObject("Match Results").AddComponent<MatchResultsBillboard>();
            var sessionOverlay = matchObject.AddComponent<LocalSessionFlowOverlay>();
            var controls = matchObject.AddComponent<LocalVerticalSliceDevelopmentControls>();
            var feedback = matchObject.AddComponent<PlacementFeedbackView>();
            var hud = matchObject.AddComponent<HudView>();
            var sendDock = matchObject.AddComponent<SendDockController>();
            var placement = matchObject.AddComponent<TouchPlacementController>();
            var laneViewToggle = matchObject.AddComponent<LaneViewToggleController>();
            var camera = CreateCamera();

            renderer.Initialize(driver);
            renderer.SetPresentationCamera(camera);
            laneViewToggle.Initialize(renderer);
            hud.Initialize(driver);
            replayExporter.Initialize(driver);
            playtestRecorder.Initialize(driver, replayExporter);
            performanceSampler.Initialize(driver, renderer);
            stressHarness.Initialize(commands, performanceSampler);
            results.Initialize(driver);
            sessionOverlay.Initialize(driver, playtestRecorder, laneViewToggle);
            controls.Initialize(commands, driver, renderer, replayExporter, playtestRecorder, stressHarness, placement, laneViewToggle, feedback);
            bootstrapper.Initialize(driver, commands);
            renderer.SetCameraFraming(renderer.CameraFraming);
            CreateRuntimeHud(matchObject, camera, commands, feedback, sendDock, placement);
        }

        private static Camera CreateCamera()
        {
            if (Camera.main != null)
            {
                return Camera.main;
            }

            var cameraObject = new GameObject("Local Match Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 15.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.transform.position = new Vector3(12f, 28f, -10f);
            camera.transform.LookAt(new Vector3(12f, 0f, 8.5f));
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            return camera;
        }

        private static void CreateRuntimeHud(
            GameObject matchObject,
            Camera camera,
            UnityCommandAdapter commands,
            PlacementFeedbackView feedback,
            SendDockController sendDock,
            TouchPlacementController placement)
        {
            var ghost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ghost.name = "Placement Ghost";
            ghost.transform.SetParent(matchObject.transform, false);
            ghost.transform.localScale = new Vector3(0.62f, 0.78f, 0.62f);
            ghost.SetActive(false);

            placement.Initialize(camera, commands, feedback, ghost);
            sendDock.Initialize(commands, feedback);
        }
    }

    public sealed class LocalVerticalSliceDevelopmentControls : MonoBehaviour
    {
        private UnityCommandAdapter commands = null!;
        private UnitySimulationDriver driver = null!;
        private UnityVerticalSliceRenderer renderer = null!;
        private LocalReplayExporter replayExporter = null!;
        private LocalPlaytestRecorder playtestRecorder = null!;
        private HeavySendStressHarness stressHarness = null!;
        private TouchPlacementController placement = null!;
        private LaneViewToggleController laneViewToggle = null!;
        private PlacementFeedbackView feedback = null!;

        public void Initialize(
            UnityCommandAdapter commandAdapter,
            UnitySimulationDriver simulationDriver,
            UnityVerticalSliceRenderer presentationRenderer,
            LocalReplayExporter exporter,
            LocalPlaytestRecorder recorder,
            HeavySendStressHarness harness,
            TouchPlacementController placementController,
            LaneViewToggleController viewToggleController,
            PlacementFeedbackView feedbackView)
        {
            commands = commandAdapter;
            driver = simulationDriver;
            renderer = presentationRenderer;
            replayExporter = exporter;
            playtestRecorder = recorder;
            stressHarness = harness;
            placement = placementController;
            laneViewToggle = viewToggleController;
            feedback = feedbackView;
        }

        private void Update()
        {
            if (commands == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.B)) placement.BeginTowerPlacement();
            if (Input.GetKeyDown(KeyCode.C)) placement.BeginControlTowerPlacement();
            if (Input.GetKeyDown(KeyCode.U)) placement.BeginUtilityTowerPlacement();
            if (Input.GetKeyDown(KeyCode.Return)) placement.ConfirmPlacement();
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (placement.IsPlacing)
                {
                    placement.ConfirmPlacement();
                }
                else
                {
                    driver.TogglePause();
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape)) placement.CancelPlacement();
            if (Input.GetKeyDown(KeyCode.UpArrow)) placement.NudgeUp();
            if (Input.GetKeyDown(KeyCode.DownArrow)) placement.NudgeDown();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) placement.NudgeLeft();
            if (Input.GetKeyDown(KeyCode.RightArrow)) placement.NudgeRight();
            if (Input.GetKeyDown(KeyCode.S)) ShowSendResult(commands.SendSampleCreep(), "Runner sent");
            if (Input.GetKeyDown(KeyCode.V)) ShowSendResult(commands.SendBruteCreep(), "Brute sent");
            if (Input.GetKeyDown(KeyCode.W)) ShowSendResult(commands.SendSwarmCreep(), "Swarm sent");
            if (Input.GetKeyDown(KeyCode.X)) commands.SellLastSampleTower();
            if (Input.GetKeyDown(KeyCode.R))
            {
                driver.ResetMatch();
                playtestRecorder.ResetRecorder();
            }
            if (Input.GetKeyDown(KeyCode.E)) replayExporter.ExportCurrentReplay();
            if (Input.GetKeyDown(KeyCode.P))
            {
                var reportPath = playtestRecorder.ExportNow();
                feedback.ShowEconomy(reportPath is null ? "Finish match first" : $"Saved {System.IO.Path.GetFileName(reportPath)}");
            }
            if (Input.GetKeyDown(KeyCode.H)) stressHarness.StartRun();
            if (Input.GetKeyDown(KeyCode.M)) PresentationPreferences.ToggleAudioMuted();
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) PresentationPreferences.AdjustFeedbackVolume(-0.1f);
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) PresentationPreferences.AdjustFeedbackVolume(0.1f);
            if (Input.GetKeyDown(KeyCode.F)) PresentationPreferences.ReducedEffects = !PresentationPreferences.ReducedEffects;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                laneViewToggle.ToggleView();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) renderer.SetPresentationDetail(PresentationDetail.Full);
            if (Input.GetKeyDown(KeyCode.Alpha2)) renderer.SetPresentationDetail(PresentationDetail.Simplified);
            if (Input.GetKeyDown(KeyCode.Alpha3)) renderer.SetPresentationDetail(PresentationDetail.Disabled);
        }

        private void ShowSendResult(LTW.Simulation.Bridge.VerticalSliceCommandResult result, string successMessage)
        {
            if (result.Accepted)
            {
                feedback.ShowEconomy(successMessage);
                return;
            }

            feedback.ShowRejected(result.RejectionReason);
        }
    }
}
