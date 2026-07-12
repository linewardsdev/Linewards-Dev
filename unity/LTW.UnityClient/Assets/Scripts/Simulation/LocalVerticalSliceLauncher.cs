using UnityEngine;

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

            renderer.Initialize(driver);
            replayExporter.Initialize(driver);
            playtestRecorder.Initialize(driver, replayExporter);
            performanceSampler.Initialize(driver, renderer);
            stressHarness.Initialize(commands, performanceSampler);
            results.Initialize(driver);
            sessionOverlay.Initialize(driver);
            controls.Initialize(commands, driver, renderer, replayExporter, playtestRecorder, stressHarness);
            bootstrapper.Initialize(driver, commands);
            CreateCamera();
        }

        private static void CreateCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Local Match Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 29f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.transform.position = new Vector3(3f, 42f, -8f);
            camera.transform.LookAt(new Vector3(3f, 0f, 28f));
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }
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

        public void Initialize(UnityCommandAdapter commandAdapter, UnitySimulationDriver simulationDriver, UnityVerticalSliceRenderer presentationRenderer, LocalReplayExporter exporter, LocalPlaytestRecorder recorder, HeavySendStressHarness harness)
        {
            commands = commandAdapter;
            driver = simulationDriver;
            renderer = presentationRenderer;
            replayExporter = exporter;
            playtestRecorder = recorder;
            stressHarness = harness;
        }

        private void Update()
        {
            if (commands == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.B)) commands.PlaceSampleTower(2, 2);
            if (Input.GetKeyDown(KeyCode.C)) commands.PlaceControlTower(3, 2);
            if (Input.GetKeyDown(KeyCode.U)) commands.PlaceUtilityTower(4, 2);
            if (Input.GetKeyDown(KeyCode.S)) commands.SendSampleCreep();
            if (Input.GetKeyDown(KeyCode.V)) commands.SendBruteCreep();
            if (Input.GetKeyDown(KeyCode.W)) commands.SendSwarmCreep();
            if (Input.GetKeyDown(KeyCode.X)) commands.SellLastSampleTower();
            if (Input.GetKeyDown(KeyCode.Space)) driver.TogglePause();
            if (Input.GetKeyDown(KeyCode.R))
            {
                driver.ResetMatch();
                playtestRecorder.ResetRecorder();
            }
            if (Input.GetKeyDown(KeyCode.E)) replayExporter.ExportCurrentReplay();
            if (Input.GetKeyDown(KeyCode.P)) playtestRecorder.ExportNow();
            if (Input.GetKeyDown(KeyCode.H)) stressHarness.StartRun();
            if (Input.GetKeyDown(KeyCode.M)) PresentationPreferences.ToggleAudioMuted();
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) PresentationPreferences.AdjustFeedbackVolume(-0.1f);
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) PresentationPreferences.AdjustFeedbackVolume(0.1f);
            if (Input.GetKeyDown(KeyCode.F)) PresentationPreferences.ReducedEffects = !PresentationPreferences.ReducedEffects;
            if (Input.GetKeyDown(KeyCode.Alpha1)) renderer.SetPresentationDetail(PresentationDetail.Full);
            if (Input.GetKeyDown(KeyCode.Alpha2)) renderer.SetPresentationDetail(PresentationDetail.Simplified);
            if (Input.GetKeyDown(KeyCode.Alpha3)) renderer.SetPresentationDetail(PresentationDetail.Disabled);
        }

    }
}
