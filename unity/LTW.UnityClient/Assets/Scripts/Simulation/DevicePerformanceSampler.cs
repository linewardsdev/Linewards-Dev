using UnityEngine;
using UnityEngine.Profiling;

namespace LTW.UnityClient.Simulation
{
    /// <summary>Lightweight local metrics for device-validation runs; it has no gameplay effect.</summary>
    public sealed class DevicePerformanceSampler : MonoBehaviour
    {
        [SerializeField] private UnitySimulationDriver simulationDriver = null!;
        [SerializeField] private UnityVerticalSliceRenderer presentationRenderer = null!;

        private float elapsed;
        private int frames;

        public float AverageFrameMilliseconds { get; private set; }
        public long ManagedMemoryBytes { get; private set; }
        public int ActiveCreepCount { get; private set; }
        public int ActiveTowerCount { get; private set; }
        public int ActivePresentationObjectCount { get; private set; }

        public void Initialize(UnitySimulationDriver driver, UnityVerticalSliceRenderer renderer)
        {
            simulationDriver = driver;
            presentationRenderer = renderer;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < 1f) return;

            AverageFrameMilliseconds = elapsed * 1000f / frames;
            ManagedMemoryBytes = Profiler.GetMonoUsedSizeLong();
            var snapshot = simulationDriver.LatestSnapshot;
            ActiveCreepCount = snapshot?.Creeps.Count ?? 0;
            ActiveTowerCount = snapshot?.Towers.Count ?? 0;
            ActivePresentationObjectCount = presentationRenderer.ActivePresentationObjectCount;
            elapsed = 0f;
            frames = 0;
        }
    }
}
