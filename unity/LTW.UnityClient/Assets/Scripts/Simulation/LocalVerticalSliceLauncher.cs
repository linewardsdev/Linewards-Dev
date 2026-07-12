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
            var performanceSampler = matchObject.AddComponent<DevicePerformanceSampler>();
            var stressHarness = matchObject.AddComponent<HeavySendStressHarness>();
            var results = new GameObject("Match Results").AddComponent<MatchResultsBillboard>();
            var controls = matchObject.AddComponent<LocalVerticalSliceDevelopmentControls>();

            renderer.Initialize(driver);
            replayExporter.Initialize(driver);
            performanceSampler.Initialize(driver, renderer);
            stressHarness.Initialize(commands, performanceSampler);
            results.Initialize(driver);
            controls.Initialize(commands, driver, renderer, replayExporter, stressHarness);
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
            camera.orthographicSize = 16.8f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.transform.position = new Vector3(5.5f, 30f, -11.5f);
            camera.transform.LookAt(new Vector3(5.5f, 0f, 14f));
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
        private HeavySendStressHarness stressHarness = null!;

        public void Initialize(UnityCommandAdapter commandAdapter, UnitySimulationDriver simulationDriver, UnityVerticalSliceRenderer presentationRenderer, LocalReplayExporter exporter, HeavySendStressHarness harness)
        {
            commands = commandAdapter;
            driver = simulationDriver;
            renderer = presentationRenderer;
            replayExporter = exporter;
            stressHarness = harness;
        }

        private void Update()
        {
            if (commands == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.B)) commands.PlaceSampleTower(2, 1);
            if (Input.GetKeyDown(KeyCode.C)) commands.PlaceControlTower(3, 1);
            if (Input.GetKeyDown(KeyCode.U)) commands.PlaceUtilityTower(4, 1);
            if (Input.GetKeyDown(KeyCode.S)) commands.SendSampleCreep();
            if (Input.GetKeyDown(KeyCode.V)) commands.SendBruteCreep();
            if (Input.GetKeyDown(KeyCode.W)) commands.SendSwarmCreep();
            if (Input.GetKeyDown(KeyCode.X)) commands.SellLastSampleTower();
            if (Input.GetKeyDown(KeyCode.R)) driver.ResetMatch();
            if (Input.GetKeyDown(KeyCode.E)) replayExporter.ExportCurrentReplay();
            if (Input.GetKeyDown(KeyCode.H)) stressHarness.StartRun();
            if (Input.GetKeyDown(KeyCode.Alpha1)) renderer.SetPresentationDetail(PresentationDetail.Full);
            if (Input.GetKeyDown(KeyCode.Alpha2)) renderer.SetPresentationDetail(PresentationDetail.Simplified);
            if (Input.GetKeyDown(KeyCode.Alpha3)) renderer.SetPresentationDetail(PresentationDetail.Disabled);
        }

    }
}
