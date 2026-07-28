using System.IO;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class HeavySendStressHarness : MonoBehaviour
    {
        private UnityCommandAdapter commands = null!;
        private DevicePerformanceSampler sampler = null!;
        private int sender = 3;
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

        /// <summary>
        /// Starts the stress run, sending as <paramref name="senderPlayerId"/>.
        /// </summary>
        /// <remarks>
        /// The sender determines which lane the waves land in — a send goes to the home lane of the
        /// sender's next active opponent. Review captures frame one lane, so a run that sends as the
        /// wrong player floods lanes the camera never shows and the captured board looks idle.
        /// </remarks>
        public void StartRun(int senderPlayerId = 3)
        {
            sender = senderPlayerId;
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
                // A rejected wave used to vanish silently, which reads downstream as "the stress
                // scenario ran and the board was quiet" rather than "nothing was ever sent".
                var result = commands.SendStressReviewWave(burstIndex, sender);
                if (!result.Accepted)
                {
                    Debug.LogWarning($"STRESS burst {burstIndex} rejected: {result.RejectionReason}");
                }

                burstIndex++;
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
