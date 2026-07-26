using System.Globalization;
using System.IO;
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
    /// </remarks>
    public static class MotionCaptureRunner
    {
        private const int FrameCount = 16;
        private const double IntervalSeconds = 0.45d;
        private const int Width = 1080;
        private const int Height = 1920;

        private static string outputDirectory;
        private static int captured;
        private static double nextCaptureAt;
        private static double startedAt;
        private static bool running;
        private static bool previousPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousPlayModeOptions;

        private static float minDelta;
        private static float maxDelta;
        private static double deltaSum;
        private static int deltaSamples;

        [MenuItem("Line Wards/Review/Capture Motion Sequence")]
        public static void CaptureMotionSequence()
        {
            outputDirectory = ReadArgumentValue("-ltwCaptureOutputDir")
                              ?? Path.Combine(Path.GetTempPath(), "ltw-motion");
            Directory.CreateDirectory(outputDirectory);

            captured = 0;
            minDelta = float.MaxValue;
            maxDelta = 0f;
            deltaSum = 0d;
            deltaSamples = 0;
            running = true;

            MobileViewportLayout.SetCaptureViewportOverride(Width, Height, new Rect(0f, 0f, Width, Height));

            previousPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying = true;

            startedAt = EditorApplication.timeSinceStartup;
            nextCaptureAt = startedAt + 1.5d;

            EditorApplication.update -= Update;
            EditorApplication.update += Update;
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

            nextCaptureAt = EditorApplication.timeSinceStartup + IntervalSeconds;
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
            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
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
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
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
                "MOTION captured {0} frame(s) to {1} | frame time avg {2:F2} ms, worst {3:F2} ms | " +
                "avg {4:F1} fps, worst {5:F1} fps | samples {6}",
                captured, outputDirectory, averageDelta * 1000d, maxDelta * 1000f, averageFps, worstFps, deltaSamples));

            EditorApplication.isPlaying = false;

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

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
