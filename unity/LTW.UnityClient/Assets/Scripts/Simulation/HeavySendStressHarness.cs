using System.IO;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class HeavySendStressHarness : MonoBehaviour
    {
        private UnityCommandAdapter commands = null!;
        private DevicePerformanceSampler sampler = null!;
        private bool running;
        private float elapsed;
        private float nextBurst;
        private int peakCreeps;
        private int peakPresentationObjects;
        private int burstIndex;
        private float worstFrameMilliseconds;

        public void Initialize(UnityCommandAdapter commandAdapter, DevicePerformanceSampler performanceSampler)
        {
            commands = commandAdapter;
            sampler = performanceSampler;
        }

        public void StartRun()
        {
            running = true;
            elapsed = nextBurst = worstFrameMilliseconds = 0f;
            peakCreeps = peakPresentationObjects = 0;
            burstIndex = 0;
        }

        private void Update()
        {
            if (!running) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= nextBurst)
            {
                commands.SendStressReviewWave(burstIndex++);
                nextBurst += 1.25f;
            }
            peakCreeps = Mathf.Max(peakCreeps, sampler.ActiveCreepCount);
            peakPresentationObjects = Mathf.Max(peakPresentationObjects, sampler.ActivePresentationObjectCount);
            worstFrameMilliseconds = Mathf.Max(worstFrameMilliseconds, sampler.AverageFrameMilliseconds);
            if (elapsed < 60f) return;

            running = false;
            var directory = Path.Combine(Application.persistentDataPath, "Diagnostics");
            Directory.CreateDirectory(directory);
            var report = $"{{\"durationSeconds\":60,\"peakCreeps\":{peakCreeps},\"peakPresentationObjects\":{peakPresentationObjects},\"worstAverageFrameMilliseconds\":{worstFrameMilliseconds:F2},\"managedMemoryBytes\":{sampler.ManagedMemoryBytes}}}";
            File.WriteAllText(Path.Combine(directory, "heavy-send-stress.json"), report);
        }
    }
}
