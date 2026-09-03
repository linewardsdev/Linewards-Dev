using System.Globalization;
using System.IO;
using System.Reflection;
using LTW.Simulation.Bridge;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LTW.UnityClient.UI;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Captures a timed sequence of frames from a single play session, plus frame timing.
    /// </summary>
    /// <remarks>
    /// The state-driven review capture answers "what does each screen look like". It cannot answer
    /// "does anything move", which is where the remaining risk sits after the URP migration:
    /// animated board elements were deliberately excluded from the board mesh bake, and nobody has
    /// watched them run. Sampling at a fixed interval and comparing consecutive frames shows
    /// whether motion is happening at all.
    ///
    /// Frame timing is recorded alongside because bloom has a real cost on mobile and none has been
    /// measured.
    ///
    /// IMPORTANT: run this WITHOUT -nographics. That flag disables the graphics device entirely,
    /// so every captured frame comes out a single flat colour instead of failing loudly — the
    /// capture "succeeds", writes the expected file count, and the blank result is only caught by
    /// actually inspecting the pixels. Use:
    ///     Unity -batchmode -projectPath &lt;project&gt; -executeMethod &lt;method&gt; -logFile &lt;log&gt;
    /// </remarks>
    public static class MotionCaptureRunner
    {
        private const int FrameCount = 16;
        private const double DefaultIntervalSeconds = 0.45d;
        private const int DefaultWidth = 1080;
        private const int DefaultHeight = 1920;

        /// <summary>
        /// Seconds between captured frames, overridable with <c>-ltwCaptureIntervalSeconds</c>.
        /// </summary>
        /// <remarks>
        /// The default of 0.45s is longer than one simulation tick (0.25s at the shipped 4/s), so
        /// every consecutive pair of frames necessarily straddles a tick boundary. That answers "is
        /// anything moving at all" and cannot answer "does it move BETWEEN ticks", which is the
        /// question any change that reorganises per-frame versus per-snapshot work has to survive:
        /// work moved to the snapshot rate stutters at 4 Hz, and a sequence sampled slower than 4 Hz
        /// cannot see the difference. Sampling well inside one tick makes the stutter visible as
        /// consecutive frames that do not change.
        ///
        /// A short interval is only honoured if a frame can be WRITTEN faster than it: at the default
        /// 1080x1920 one capture costs about 170 ms (measured — asking for 0.05s intervals produced
        /// 0.17s ones and dragged the game itself down to 6 fps), so sub-tick sampling wants
        /// <c>-ltwCaptureWidth</c>/<c>-ltwCaptureHeight</c> lowered alongside it. 270x480 costs about
        /// a sixteenth of that and leaves a creep around ten pixels across, which is ample for
        /// frame-to-frame deltas even though it is far too coarse to review as artwork.
        /// </remarks>
        private static double intervalSeconds = DefaultIntervalSeconds;

        private static int width = DefaultWidth;
        private static int height = DefaultHeight;

        private static string outputDirectory;
        private static int captured;
        private static double nextCaptureAt;
        private static double startedAt;
        private static bool running;
        private static bool matchSeeded;
        private static bool previousPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousPlayModeOptions;

        private static float minDelta;
        private static float maxDelta;
        private static double deltaSum;
        private static int deltaSamples;

        [MenuItem("Line Wars/Review/Capture Motion Sequence")]
        public static void CaptureMotionSequence()
        {
            outputDirectory = ReadArgumentValue("-ltwCaptureOutputDir")
                              ?? Path.Combine(Path.GetTempPath(), "ltw-motion");
            Directory.CreateDirectory(outputDirectory);

            intervalSeconds = double.TryParse(
                ReadArgumentValue("-ltwCaptureIntervalSeconds"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsedInterval) && parsedInterval > 0d
                ? parsedInterval
                : DefaultIntervalSeconds;
            width = ReadIntArgument("-ltwCaptureWidth") ?? DefaultWidth;
            height = ReadIntArgument("-ltwCaptureHeight") ?? DefaultHeight;

            captured = 0;
            minDelta = float.MaxValue;
            maxDelta = 0f;
            deltaSum = 0d;
            deltaSamples = 0;
            running = true;
            matchSeeded = false;

            MobileViewportLayout.SetCaptureViewportOverride(width, height, new Rect(0f, 0f, width, height));

            previousPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying = true;

            startedAt = EditorApplication.timeSinceStartup;
            nextCaptureAt = startedAt + 3.5d;

            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        /// <summary>
        /// Starts a match and puts creeps on the board, so the captured frames actually contain
        /// something moving.
        /// </summary>
        /// <remarks>
        /// Without this the runner entered play mode against an idle scene: it rendered the board
        /// correctly but there were no creeps, so all 16 frames came out byte-identical and the
        /// sequence proved nothing. Seeds one of every creep whose motion is procedural or newly
        /// rigged, which is exactly the set that cannot be validated from a still.
        ///
        /// Returns false until the driver and command adapter exist, since they are created a few
        /// frames into play mode; the caller retries.
        /// </remarks>
        private static bool SeedMatch()
        {
            var driver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (driver == null || commands == null)
            {
                return false;
            }

            // The queue/gold entry points are not on the adapter itself; VisualReviewCaptureRunner
            // reaches the LocalVerticalSlice behind it the same way for its own scenario setup.
            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(commands) is not LocalVerticalSlice simulation)
            {
                return false;
            }

            driver.StartMatch();
            // Player 3 is the sender, so its gold is what gates the queued creeps.
            simulation.GrantLocalPlaytestGold(new PlayerId(3), new Gold(5000));

            var roster = new[]
            {
                SampleVerticalSliceContent.WispCreepId,
                SampleVerticalSliceContent.SerpentCreepId,
                SampleVerticalSliceContent.RevenantCreepId,
                SampleVerticalSliceContent.ObsidianBruteCreepId,
                SampleVerticalSliceContent.TurretWalkerCreepId,
                SampleVerticalSliceContent.BruteCreepId,
                // Added with their rigs (OPEN_ITEMS item 11, wave 2.3). Both are "newly rigged",
                // which is exactly the set the remark above says this roster is for; Serpent was
                // already here as a procedural-motion creep and now qualifies twice over.
                SampleVerticalSliceContent.SiegeCreepId,
                // The Runner's id is the bare `CreepId`, not `RunnerCreepId` — it was the first
                // creep in the slice and never got renamed when the rest gained prefixes.
                SampleVerticalSliceContent.CreepId,
            };

            foreach (var creepId in roster)
            {
                var result = simulation.QueueSend(new PlayerId(3), creepId, 2);
                Debug.Log($"MOTION seed {creepId.Value}: accepted={result.Accepted} reason={result.RejectionReason}");
            }

            matchSeeded = true;
            return true;
        }

        private static void Update()
        {
            if (!running)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                // Still entering play mode.
                if (EditorApplication.timeSinceStartup - startedAt > 60d)
                {
                    Finish(1, "Timed out waiting for play mode.");
                }

                return;
            }

            if (!matchSeeded && !SeedMatch())
            {
                // The driver/adapter appear a frame or two after play mode starts.
                if (EditorApplication.timeSinceStartup - startedAt > 60d)
                {
                    Finish(1, "Timed out waiting for the simulation driver.");
                }

                return;
            }

            if (Time.deltaTime > 0f)
            {
                minDelta = Mathf.Min(minDelta, Time.deltaTime);
                maxDelta = Mathf.Max(maxDelta, Time.deltaTime);
                deltaSum += Time.deltaTime;
                deltaSamples++;
            }

            if (EditorApplication.timeSinceStartup < nextCaptureAt)
            {
                return;
            }

            nextCaptureAt = EditorApplication.timeSinceStartup + intervalSeconds;
            var path = Path.Combine(outputDirectory, $"motion-{captured:D2}.png");
            WriteFrame(path);
            captured++;

            if (captured >= FrameCount)
            {
                Finish(0, null);
            }
        }

        private static void WriteFrame(string path)
        {
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.black);

                var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                System.Array.Sort(cameras, static (l, r) => l.depth.CompareTo(r.depth));
                foreach (var camera in cameras)
                {
                    if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (GraphicsSettings.currentRenderPipeline == null)
                    {
                        var previousTarget = camera.targetTexture;
                        camera.targetTexture = renderTexture;
                        camera.Render();
                        camera.targetTexture = previousTarget;
                        continue;
                    }

                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture };
                    if (RenderPipeline.SupportsRenderRequest(camera, request))
                    {
                        RenderPipeline.SubmitRenderRequest(camera, request);
                    }
                }

                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
            }
            finally
            {
                RenderTexture.active = previousActive;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(texture);
            }
        }

        private static void Finish(int exitCode, string error)
        {
            running = false;
            EditorApplication.update -= Update;
            MobileViewportLayout.ClearCaptureViewportOverride();
            EditorSettings.enterPlayModeOptionsEnabled = previousPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousPlayModeOptions;

            if (error != null)
            {
                Debug.LogError(error);
            }

            var averageDelta = deltaSamples > 0 ? deltaSum / deltaSamples : 0d;
            var averageFps = averageDelta > 0d ? 1d / averageDelta : 0d;
            var worstFps = maxDelta > 0f ? 1f / maxDelta : 0f;

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "MOTION captured {0} frame(s) at {7:F3}s intervals, {8}x{9}, to {1} | frame time avg {2:F2} ms, worst {3:F2} ms | " +
                "avg {4:F1} fps, worst {5:F1} fps | samples {6}",
                captured, outputDirectory, averageDelta * 1000d, maxDelta * 1000f, averageFps, worstFps, deltaSamples, intervalSeconds, width, height));

            EditorApplication.isPlaying = false;

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static int? ReadIntArgument(string name) =>
            int.TryParse(ReadArgumentValue(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : (int?)null;

        private static string ReadArgumentValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == name)
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
