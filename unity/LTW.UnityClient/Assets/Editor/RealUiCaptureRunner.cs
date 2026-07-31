using System.IO;
using System.Linq;
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
    /// Captures the REAL runtime UI, unlike <see cref="VisualReviewCaptureRunner"/>, whose captures
    /// contain the board only and miss IMGUI entirely.
    /// </summary>
    /// <remarks>
    /// The difference is the capture mechanism. Reading pixels back from a RenderTexture in
    /// batchmode misses IMGUI entirely, because OnGUI does not draw into an offscreen target.
    /// ScreenCapture grabs the composited frame the editor actually presented, IMGUI included.
    ///
    /// <see cref="VisualReviewCaptureRunner"/> used to paper over that gap with a CPU-painted mock
    /// of the HUD — convincing enough to review, but it silently drifted out of date (it kept
    /// painting the pre-expansion 5-creep send menu long after the roster reached 10) and produced
    /// several UI "defects" that were artefacts of the paint code, not the game. That mock was
    /// deleted (see docs/GAMEPLAY_REVIEW_FINDINGS.md, "The painted HUD mock (DELETED)"); an empty
    /// region in a <see cref="VisualReviewCaptureRunner"/> capture is now honest about missing IMGUI
    /// rather than a convincing painting of stale UI. This runner is how to actually see the UI.
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
        private static bool previousPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousPlayModeOptions;

        // Each entry is captured in order; the action runs one frame before the shot is taken.
        private static readonly (string Name, System.Action Setup)[] Shots =
        {
            ("real-01-default-hud", null),
            ("real-02-send-dock-open", OpenSendDock),
            ("real-03-send-category-two", OpenSendCategoryTwo),
            // The build palette has its own category picker, and it is the one that had a fixed
            // panel height while the send dock's grew — worth a shot of its own so the two can be
            // compared rather than assumed to match.
            ("real-04-build-palette-open", OpenBuildPalette),
            // The selected-tower panel, which is where a placed tower is upgraded.
            ("real-05-selected-tower", SelectAnUpgradeableTower),
            // A freshly built tower with NO line tier bought - the state every match starts in,
            // and the one where the upgrade control used to vanish entirely.
            ("real-06-selected-tower-no-tier", SelectAFreshTower),
            // Three Arrows side by side at tiers 1, 2 and 3, so the visual tell can be compared
            // rather than taken on trust.
            ("real-07-tier-comparison", ShowTierComparison),
        };

        /// <summary>The portrait surface the HUD is authored against, matching MotionCaptureRunner.</summary>
        private const int CaptureWidth = 1080;
        private const int CaptureHeight = 1920;

        public static void Run()
        {
            outputDirectory = ReadArg("-ltwCaptureOutputDir") ?? Path.Combine(Path.GetTempPath(), "ltw-real-ui");
            Directory.CreateDirectory(outputDirectory);
            shot = 0;
            seeded = false;
            running = true;

            // Declare the portrait surface the UI is designed for, exactly as MotionCaptureRunner
            // and VisualReviewCaptureRunner already do. Without it the HUD lays out against the raw
            // editor Game view — 3840x2160 landscape when the window is maximised — while the board
            // camera letterboxes itself to a portrait strip through MobileViewportLayout.CameraRect.
            // The two then disagree, and the resulting capture shows panels sprawling past the board
            // and card art diverging from its own content. That is an artefact of this runner, not a
            // layout bug, and it made the ONLY tool in the repo that can see IMGUI untrustworthy for
            // judging the thing it exists to judge.
            MobileViewportLayout.SetCaptureViewportOverride(
                CaptureWidth, CaptureHeight, new Rect(0f, 0f, CaptureWidth, CaptureHeight));

            // Saved and restored in Finish. These are persisted project settings, not per-run
            // state: leaving DisableDomainReload on changed how play mode behaves for everyone —
            // static state survives entering play mode — and the change was committed as a silent
            // side effect of running a screenshot tool.
            previousPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousPlayModeOptions = EditorSettings.enterPlayModeOptions;
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

        /// <summary>
        /// Buys a line tier, builds a tower under it, and selects that tower — so the shot shows
        /// the upgrade button live rather than capped.
        /// </summary>
        private static void SelectAnUpgradeableTower()
        {
            var touch = Object.FindAnyObjectByType<TouchPlacementController>();
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (touch == null || commands == null)
            {
                return;
            }

            var dock = Dock();
            if (dock != null)
            {
                SetPrivate(dock, "isExpanded", false);
            }

            SetPrivate(touch, "isPaletteExpanded", false);

            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(commands) is not LocalVerticalSlice sim)
            {
                return;
            }

            var lane = sim.LocalPlayerLaneId;
            var cell = new GridPosition(2, 6);
            sim.PlaceTower(sim.LocalPlayerId, lane, SampleVerticalSliceContent.TowerId, cell);
            // ARCANE to tier 2, so the tower below it has somewhere to be upgraded to.
            sim.BuyCategoryTier(sim.LocalPlayerId, LTW.Simulation.Commands.CategoryKind.TowerLine, 0, 2);

            var tower = sim.GetSnapshot().Towers.FirstOrDefault(t =>
                t.OwnerId.Equals(sim.LocalPlayerId) && t.Position.X == cell.X && t.Position.Y == cell.Y);
            if (tower != null)
            {
                SetPrivate(touch, "selectedTower", tower);
            }
        }

        /// <summary>Three otherwise identical Arrows at tier 1, 2 and 3, side by side.</summary>
        private static void ShowTierComparison()
        {
            var touch = Object.FindAnyObjectByType<TouchPlacementController>();
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (touch == null || commands == null)
            {
                return;
            }

            SetPrivate(touch, "isPaletteExpanded", false);
            SetPrivate(touch, "selectedTower", null);
            var dock = Dock();
            if (dock != null)
            {
                SetPrivate(dock, "isExpanded", false);
            }

            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(commands) is not LocalVerticalSlice sim)
            {
                return;
            }

            var lane = sim.LocalPlayerLaneId;
            var cells = new[] { new GridPosition(1, 7), new GridPosition(2, 7), new GridPosition(4, 7) };
            foreach (var cell in cells)
            {
                sim.PlaceTower(sim.LocalPlayerId, lane, SampleVerticalSliceContent.TowerId, cell);
            }

            sim.BuyCategoryTier(sim.LocalPlayerId, LTW.Simulation.Commands.CategoryKind.TowerLine, 0, 2);
            sim.BuyCategoryTier(sim.LocalPlayerId, LTW.Simulation.Commands.CategoryKind.TowerLine, 0, 3);
            // Leave cells[0] at tier 1, take cells[1] to 2 and cells[2] to 3.
            sim.UpgradeTower(sim.LocalPlayerId, lane, cells[1]);
            sim.UpgradeTower(sim.LocalPlayerId, lane, cells[2]);
            sim.UpgradeTower(sim.LocalPlayerId, lane, cells[2]);

            foreach (var t in sim.GetSnapshot().Towers.Where(t => cells.Any(c => c.X == t.Position.X && c.Y == t.Position.Y)))
            {
                Debug.Log($"REALUI tierComparison cell=({t.Position.X},{t.Position.Y}) tier={t.Tier}");
            }
        }

        private static void SelectAFreshTower()
        {
            var touch = Object.FindAnyObjectByType<TouchPlacementController>();
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (touch == null || commands == null)
            {
                return;
            }

            var dock = Dock();
            if (dock != null)
            {
                SetPrivate(dock, "isExpanded", false);
            }

            SetPrivate(touch, "isPaletteExpanded", false);

            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(commands) is not LocalVerticalSlice sim)
            {
                return;
            }

            // A GROVE tower, whose line has had no tier bought, so the control shows what it needs.
            var cell = new GridPosition(4, 10);
            sim.PlaceTower(sim.LocalPlayerId, sim.LocalPlayerLaneId, new LTW.Simulation.Content.ContentId("tower.sapling"), cell);
            var tower = sim.GetSnapshot().Towers.FirstOrDefault(t =>
                t.OwnerId.Equals(sim.LocalPlayerId) && t.Position.X == cell.X && t.Position.Y == cell.Y);
            if (tower != null)
            {
                SetPrivate(touch, "selectedTower", tower);
            }
        }

        private static void OpenBuildPalette()
        {
            var touch = Object.FindAnyObjectByType<TouchPlacementController>();
            if (touch == null)
            {
                Debug.LogWarning("REALUI no TouchPlacementController found");
                return;
            }

            var dock = Dock();
            if (dock != null)
            {
                // The palette hides itself while the send dock is expanded, so close that first.
                SetPrivate(dock, "isExpanded", false);
            }

            SetPrivate(touch, "isPaletteExpanded", true);
            SetPrivate(touch, "selectedTowerCategory", -1);
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

            MobileViewportLayout.ClearCaptureViewportOverride();

            EditorSettings.enterPlayModeOptionsEnabled = previousPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousPlayModeOptions;

            Debug.Log($"REALUI DONE shots={shot} dir={outputDirectory}");

            // Exiting the process while still in play mode leaves a Temp/__Backupscenes entry
            // behind, and the next editor launch restores that backup instead of the real scene —
            // which presents as the editor simply never finishing loading.
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
