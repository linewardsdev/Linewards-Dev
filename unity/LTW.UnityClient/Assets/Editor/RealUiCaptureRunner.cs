using System.IO;
using System.Reflection;
using LTW.Simulation.Bridge;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using LTW.UnityClient.UI;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Captures the REAL runtime UI, unlike <see cref="VisualReviewCaptureRunner"/>, whose output
    /// contains a CPU-painted mock of the HUD rather than anything the game drew.
    /// </summary>
    /// <remarks>
    /// The difference is the capture mechanism. Reading pixels back from a RenderTexture in
    /// batchmode misses IMGUI entirely, because OnGUI does not draw into an offscreen target —
    /// which is why the other runner has to paint a replacement, and why that replacement silently
    /// drifts out of date. ScreenCapture grabs the composited frame the editor actually presented,
    /// IMGUI included.
    ///
    /// The cost is that this needs a real Game view, so it must run WITHOUT -batchmode:
    ///
    ///     Unity -projectPath &lt;project&gt; -executeMethod \
    ///         LTW.UnityClient.Editor.RealUiCaptureRunner.Run -logFile &lt;log&gt;
    ///
    /// It quits the editor when finished so it can still be driven from a script.
    /// </remarks>
    public static class RealUiCaptureRunner
    {
        private static string outputDirectory = "";
        private static bool running;
        private static bool seeded;
        private static int shot;
        private static double startedAt;
        private static double nextShotAt;

        // Each entry is captured in order; the action runs one frame before the shot is taken.
        private static readonly (string Name, System.Action Setup)[] Shots =
        {
            ("real-01-default-hud", null),
            ("real-02-send-dock-open", OpenSendDock),
            ("real-03-send-category-two", OpenSendCategoryTwo),
        };

        public static void Run()
        {
            outputDirectory = ReadArg("-ltwCaptureOutputDir") ?? Path.Combine(Path.GetTempPath(), "ltw-real-ui");
            Directory.CreateDirectory(outputDirectory);
            shot = 0;
            seeded = false;
            running = true;

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying = true;

            startedAt = EditorApplication.timeSinceStartup;
            nextShotAt = startedAt + 4d;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!running)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - startedAt > 180d)
            {
                Finish("timed out");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (!seeded && !Seed())
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < nextShotAt)
            {
                return;
            }

            if (shot >= Shots.Length)
            {
                Finish(null);
                return;
            }

            var entry = Shots[shot];
            entry.Setup?.Invoke();

            var path = Path.Combine(outputDirectory, entry.Name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"REALUI captured {entry.Name} -> {path}");

            shot++;
            // ScreenCapture writes at end of frame; leave room for the file to land and for any
            // setup action to be reflected in the next frame's UI.
            nextShotAt = EditorApplication.timeSinceStartup + 2.5d;
        }

        private static bool Seed()
        {
            var driver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (driver == null || commands == null)
            {
                return false;
            }

            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(commands) is not LocalVerticalSlice sim)
            {
                return false;
            }

            driver.StartMatch();
            sim.GrantLocalPlaytestGold(new PlayerId(1), new Gold(9000));
            sim.GrantLocalPlaytestGold(new PlayerId(3), new Gold(9000));
            seeded = true;
            return true;
        }

        private static SendDockController Dock() => Object.FindAnyObjectByType<SendDockController>();

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            f?.SetValue(target, value);
        }

        private static void OpenSendDock()
        {
            var dock = Dock();
            if (dock == null)
            {
                Debug.LogWarning("REALUI no SendDockController found");
                return;
            }

            // Drive the same private state a tap would set, so the captured panel is the real one.
            SetPrivate(dock, "isExpanded", true);
            SetPrivate(dock, "selectedCategory", -1);
        }

        private static void OpenSendCategoryTwo()
        {
            var dock = Dock();
            if (dock == null)
            {
                return;
            }

            SetPrivate(dock, "isExpanded", true);
            SetPrivate(dock, "selectedCategory", 1);
        }

        private static void Finish(string error)
        {
            running = false;
            EditorApplication.update -= Tick;
            if (error != null)
            {
                Debug.LogError($"REALUI {error}");
            }

            Debug.Log($"REALUI DONE shots={shot} dir={outputDirectory}");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(error == null ? 0 : 1);
        }

        private static string ReadArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
