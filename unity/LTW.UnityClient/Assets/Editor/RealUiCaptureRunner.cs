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
using UnityEngine.UIElements;

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
        private static int captureWidth = CaptureWidth;
        private static int captureHeight = CaptureHeight;
        private static bool setupDone;
        private static EditorWindow gameView;
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
            // The build palette's actual tower grid, not just its category picker above — the one
            // state that would have caught the tablet unit-icon enlargement and specialty-line work
            // (2026-08-30) not being mirrored onto the tower side the way it was on the send dock's
            // real-03. Placed here, in the live-match group, rather than appended at the end: every
            // shot after real-12 resets or ends the match (see the shell-screens group below), so a
            // shot needing a live TouchPlacementController cannot run after that point.
            ("real-20-build-category-one", OpenBuildCategoryOne),
            // The selected-tower panel, which is where a placed tower is upgraded.
            ("real-05-selected-tower", SelectAnUpgradeableTower),
            // A freshly built tower with NO line tier bought - the state every match starts in,
            // and the one where the upgrade control used to vanish entirely.
            ("real-06-selected-tower-no-tier", SelectAFreshTower),
            // Three Arrows side by side at tiers 1, 2 and 3, so the visual tell can be compared
            // rather than taken on trust.
            ("real-07-tier-comparison", ShowTierComparison),
            // The category picker with a line that actually has work to do, so the whole-line
            // upgrade button is shown carrying a real count and price rather than its empty state.
            ("real-08-line-batch-ready", ShowLineBatchReady),
            // Multi-select with a real selection, so the batch panel is shown carrying counts and
            // both action prices rather than its empty prompt.
            ("real-09-multi-select", ShowMultiSelection),
            // Multi-select turned on and THEN the send dock opened. The dock must own the screen and
            // the mode must be gone: while it stayed live its buttons sat under the dock, so board
            // taps kept toggling towers into a batch the player could not see.
            ("real-10-send-over-multi-select", ShowSendOverMultiSelect),
            // The local seat eliminated, with BOTH bottom panels deliberately open on the frame it
            // happens. Nothing else in the repo could see this state: the dock stayed open and
            // browsable over the elimination banner and the HUD went on advertising +10 income, and
            // it survived to a device build because no capture ever drove a match this far.
            ("real-11-local-seat-eliminated", EliminateTheLocalSeat),
            // 2.5s later, after forcing both panels back open behind the HUD's back. "Closed once"
            // and "cannot be open" look identical in a single frame; this is the shot that tells
            // them apart, and it is the half of item 31 that was actually about input gating.
            ("real-12-eliminated-reopen-refused", ForcePanelsOpenWhileEliminated),
            // ---------------------------------------------------------------- shell screens
            // The three full-screen UI Toolkit compositions. They come last because each one
            // resets or ends the match, which would take the board out from under every shot
            // above; within the group they run live -> results -> title, so the pause shot still
            // has a populated board behind its veil and the title shot can reset freely.
            ("real-13-shell-pause", ShowPausedMatch),
            ("real-14-shell-results-defeat", ShowResultsWithLocalSeatBeaten),
            ("real-15-shell-results-victory", ShowResultsWithLocalSeatWinning),
            ("real-16-shell-title", ShowTitle),
            // Settings is still IMGUI and is reachable from all three shell screens, so this is
            // the shot that says whether an IMGUI panel lands over or under a runtime UI Toolkit
            // panel. If it lands under, the shell has to stand down while settings is open.
            ("real-17-settings-over-title", ShowSettingsOverTitle),
            // The codex, whose preview card is a live camera rendering into the panel. Worth a shot
            // for the composition, but note what it CANNOT settle: whether the stage drew anything
            // is a question about pixels inside that one element, and a page that renders an empty
            // frame photographs as a deliberately empty panel. CodexScreenCheck is what answers it.
            ("real-18-shell-codex", ShowCodex),
            // The creeps half, captured separately because it is the harder of the two to frame:
            // the authored model scales run 0.208 to 1.36 across it, against 0.59 to 1.05 on the
            // towers. If the stage's auto-fit is going to crop or strand anything, it is here.
            ("real-19-shell-codex-creeps", ShowCodexCreeps),
            // The HOW TO PLAY panel as it ships — the only teaching surface the game has, and the
            // starting point for the 2026-09-02 how-to-play / tutorial pass.
            // Three shots, not one: the title must be laid out for a frame before its button can be
            // pressed (Clickable rejects a press on an element with an empty rect, which is what
            // a screen switched this same frame has), and BACK must be pressed afterwards because
            // the view keeps the how-to open across the overlay's per-frame Show(Title).
            ("real-23a-title-before-how-to", ShowTitle),
            ("real-23-shell-how-to-play", () => PressShellButton("title-howto", "how-to-play")),
            ("real-23b-how-to-back", () => PressShellButton("howto-back", "how-to-back")),
            // Reported from a real device (13" M4 iPad Pro, 2026-08-30): every category card
            // showed overlapping, crushed text. Seed() calls StartMatch() directly, which sets
            // IsOpeningBuildCountdown = false, so none of the shots above have ever opened the
            // BUILD rail during the 30-second opening countdown — the exact window the countdown's
            // own on-screen text ("Place opening towers") tells a player to use it for. This shot
            // reproduces that specific combination instead of assuming real-20 already covers it.
            // Placed last, not alongside real-20: BeginOpeningBuildCountdown() sets HasStarted
            // false and IsPaused true, which every earlier shot in the live-match group above
            // assumes is NOT the case, and there is no per-shot teardown to undo it afterward.
            ("real-21-build-during-opening-countdown", OpenBuildCategoryOneDuringOpeningCountdown),
            // Both rails open at once — a tablet-only combination that was structurally impossible
            // to reach before 2026-08-30: SendDockController closed itself the instant BUILD's
            // palette expanded, and DrawTowerPalette refused to draw BUILD's own launcher while
            // SEND was expanded, both unconditionally rather than only on a phone's single bottom
            // drawer. On a rail the two occupy separate, non-overlapping columns, so there was
            // never a layout reason for either exclusion — only a phone-mode assumption that
            // leaked in. Reported live as "the buttons have no function... tapping them does
            // nothing," which matches exactly: whichever launcher opened second was never drawn,
            // so there was nothing there to tap. This shot is the regression test for the fix.
            ("real-22-both-rails-open", OpenBothRailsAtOnce),
            // 2026-09-02 how-to-play / tutorial pass. Both reset the match, so they stay last.
            ("real-24-first-run-offer", ShowFirstRunOffer),
            ("real-25-practice-coach-strip", ShowPracticeCoachStrip),
        };

        /// <summary>The portrait surface the HUD is authored against, matching MotionCaptureRunner.</summary>
        private const int CaptureWidth = 1080;
        private const int CaptureHeight = 1920;

        /// <summary>Seconds between a shot's setup and its capture, so transitions can finish.</summary>
        /// <remarks>
        /// 0.6 comfortably clears the shell screens' 200ms fade and 280ms lift with room for the
        /// frame the class change lands on. Long enough to photograph the settled state; short
        /// enough that seventeen shots still run in under a minute.
        /// </remarks>
        private const double SettleSeconds = 0.6d;

        public static void Run()
        {
            outputDirectory = ReadArg("-ltwCaptureOutputDir") ?? Path.Combine(Path.GetTempPath(), "ltw-real-ui");

            // Surface size is overridable so a tablet aspect can be captured at all. Pinned to a
            // 1080x1920 phone, this runner could not see anything that only happens on a wide
            // screen — which is every side-rail layout decision.
            captureWidth = int.TryParse(ReadArg("-ltwCaptureWidth"), out var overrideWidth) && overrideWidth > 0 ? overrideWidth : CaptureWidth;
            captureHeight = int.TryParse(ReadArg("-ltwCaptureHeight"), out var overrideHeight) && overrideHeight > 0 ? overrideHeight : CaptureHeight;
            Directory.CreateDirectory(outputDirectory);
            shot = 0;
            seeded = false;
            setupDone = false;
            running = true;

            PinGameViewToPortrait();

            // Declare the portrait surface the UI is designed for, exactly as MotionCaptureRunner
            // and VisualReviewCaptureRunner already do. Without it the HUD lays out against the raw
            // editor Game view — 3840x2160 landscape when the window is maximised — while the board
            // camera letterboxes itself to a portrait strip through MobileViewportLayout.CameraRect.
            // The two then disagree, and the resulting capture shows panels sprawling past the board
            // and card art diverging from its own content. That is an artefact of this runner, not a
            // layout bug, and it made the ONLY tool in the repo that can see IMGUI untrustworthy for
            // judging the thing it exists to judge.
            MobileViewportLayout.SetCaptureViewportOverride(
                captureWidth, captureHeight, new Rect(0f, 0f, captureWidth, captureHeight));

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

        /// <summary>
        /// Forces the Game view itself to 1080x1920, rather than only telling the HUD it is.
        /// </summary>
        /// <remarks>
        /// <see cref="MobileViewportLayout.SetCaptureViewportOverride"/> makes IMGUI lay out as if
        /// the surface were portrait, and for a long time that was enough, because IMGUI is the only
        /// thing that reads it. It is not enough any more. A UI Toolkit panel scales against the real
        /// <c>Screen</c>, so on a maximised 3840x2160 editor Game view the shell screens compose
        /// themselves for a 16:9 landscape display while the HUD next to them composes for a
        /// portrait phone — one capture, two different surfaces, and neither of them the product.
        ///
        /// Pinning the Game view makes the declared surface and the real one the same thing, which
        /// also retires the discrepancy for the existing shots: they stop being portrait panels
        /// anchored in the corner of a landscape canvas and become an actual phone frame.
        ///
        /// Reflection, because <c>GameViewSizes</c> and <c>GameView</c> are both editor-internal.
        /// It reports rather than throws if the shape ever changes — a landscape capture is a
        /// degraded capture, not a failed run, and the size is logged with every shot so a reviewer
        /// can see which they are looking at.
        /// </remarks>
        private static void PinGameViewToPortrait()
        {
            try
            {
                var editorAssembly = typeof(EditorWindow).Assembly;
                var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
                var sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                if (sizesType == null || sizeType == null || sizeKindType == null || gameViewType == null)
                {
                    Debug.LogWarning("REALUI could not reach the Game view size API; capturing at the editor's own size.");
                    return;
                }

                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var group = sizesType.GetProperty("currentGroup")?.GetValue(instance);
                if (group == null)
                {
                    Debug.LogWarning("REALUI could not reach the current Game view size group.");
                    return;
                }

                var groupType = group.GetType();
                var constructor = sizeType.GetConstructor(new[] { sizeKindType, typeof(int), typeof(int), typeof(string) });
                var size = constructor?.Invoke(new[]
                {
                    System.Enum.Parse(sizeKindType, "FixedResolution"), captureWidth, captureHeight, (object)PortraitSizeName
                });
                if (size == null)
                {
                    Debug.LogWarning("REALUI could not build a fixed-resolution Game view size.");
                    return;
                }

                // Re-added every run rather than reused: the custom size list is a persisted editor
                // preference, and a stale entry from an older run is exactly the kind of thing that
                // silently captures at the wrong resolution months later.
                var total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                var builtin = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null);
                for (var index = total - 1; index >= builtin; index--)
                {
                    var existing = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
                    var name = sizeType.GetProperty("baseText")?.GetValue(existing) as string;
                    if (name == PortraitSizeName)
                    {
                        groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                    }
                }

                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                var selected = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;

                gameView = EditorWindow.GetWindow(gameViewType, false, "Game", true);
                gameViewType
                    .GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(gameView, new object[] { selected, null });

                // Maximised so nothing docked alongside it can take the tab. A run was lost to
                // exactly that: the AI Assistant package raised a window mid-run, the Game view
                // stopped being the visible tab, and seven ScreenCapture calls logged success and
                // wrote no files at all — the same silent failure -batchmode produces.
                gameView.maximized = true;
                gameView.Repaint();

                Debug.Log($"REALUI pinned the Game view to {captureWidth}x{captureHeight} (size index {selected}).");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"REALUI could not pin the Game view: {exception.Message}");
            }
        }

        private const string PortraitSizeName = "LTW Portrait 1080x1920";

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

            // Setup runs a beat BEFORE the shot, which is what the comment on Shots always claimed
            // and what the code never did. It captured in the same editor tick the setup ran in, so
            // anything that needs a frame to become visible was photographed before it did.
            //
            // Nothing caught that while the UI was entirely IMGUI, because IMGUI is immediate: a
            // panel drawn from state set microseconds earlier is already on screen. It stopped being
            // true the moment a screen had an enter transition — the first UI Toolkit shell captures
            // came back showing an empty board, because a 200ms fade had not started yet.
            if (!setupDone)
            {
                entry.Setup?.Invoke();
                setupDone = true;
                nextShotAt = EditorApplication.timeSinceStartup + SettleSeconds;
                return;
            }

            // Re-asserted per shot rather than once at the start, for the same reason it is
            // maximised: ScreenCapture reads the presented frame, so a Game view that is not the
            // front tab produces no frame and no error.
            if (gameView != null)
            {
                gameView.Focus();
            }

            var path = Path.Combine(outputDirectory, entry.Name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"REALUI captured {entry.Name} -> {path}");

            shot++;
            setupDone = false;
            // ScreenCapture writes at end of frame; leave room for the file to land.
            nextShotAt = EditorApplication.timeSinceStartup + 1.5d;
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

        /// <summary>
        /// Through the property, not the field. SelectedCategory's setter keeps a static layout
        /// mirror in step (selectedCategoryForLayout, which PanelRect reads from a static
        /// context); writing the field directly left that mirror stale, so these shots measured
        /// the panel for whichever state the PREVIOUS shot was in.
        /// </summary>
        private static void SetSelectedCategory(object dock, int category) =>
            dock.GetType()
                .GetProperty("SelectedCategory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dock, category);

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

            // Three queued sends, so the QUEUE readout and the launcher badge (item 15) are in
            // frame carrying real numbers rather than photographed in their empty state. Colossus
            // deliberately: at 208G against the 100G opening bank it cannot be paid for, so it
            // STAYS queued — a cheap creep would be drained and spawned the next tick, and the
            // queue would photograph empty.
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (commands != null)
            {
                commands.SendColossusCreep();
                commands.SendColossusCreep();
                commands.SendColossusCreep();
            }

            // Drive the same private state a tap would set, so the captured panel is the real one.
            SetPrivate(dock, "isExpanded", true);
            SetSelectedCategory(dock, -1);
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

        /// <summary>
        /// The category picker showing the whole-line upgrade button in BOTH of its states at once:
        /// Arcane carrying a real count and price, Foundry and Grove with nothing to raise.
        /// </summary>
        /// <remarks>
        /// Every shot shares one simulation and they run in order, so Arcane arrives here already at
        /// tier 3 from the tier-comparison shot. Towers are born at their line's current tier, which
        /// means the ones placed below are NOT what the button is counting — the pending towers come
        /// from the earlier shots. That is left as it is because the point of this capture is the two
        /// button states side by side, and it produces them reliably; it is not a fixture for
        /// asserting a particular count, which is what the unit tests are for.
        /// </remarks>
        private static void ShowLineBatchReady()
        {
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (commands == null)
            {
                return;
            }

            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(commands) is not LocalVerticalSlice sim)
            {
                return;
            }

            var lane = sim.LocalPlayerLaneId;
            // Mixed types, so the total is a sum of different prices rather than a multiple of one.
            var plan = new[]
            {
                (SampleVerticalSliceContent.TowerId, new GridPosition(1, 4)),
                (SampleVerticalSliceContent.TowerId, new GridPosition(3, 4)),
                (SampleVerticalSliceContent.ControlTowerId, new GridPosition(1, 6)),
                (SampleVerticalSliceContent.UtilityTowerId, new GridPosition(3, 6)),
                (SampleVerticalSliceContent.PulseTowerId, new GridPosition(1, 8)),
                (SampleVerticalSliceContent.PrismTowerId, new GridPosition(3, 8)),
            };

            foreach (var (towerId, cell) in plan)
            {
                sim.PlaceTower(sim.LocalPlayerId, lane, towerId, cell);
            }

            sim.BuyCategoryTier(sim.LocalPlayerId, LTW.Simulation.Commands.CategoryKind.TowerLine, 0, 2);
            OpenBuildPalette();
        }

        /// <summary>Multi-select mode on, with three of the player's towers picked.</summary>
        private static void ShowMultiSelection()
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

            var toggle = typeof(TouchPlacementController).GetMethod("ToggleInMultiSelection", BindingFlags.Instance | BindingFlags.NonPublic);
            var setMode = typeof(TouchPlacementController).GetMethod("SetMultiSelectMode", BindingFlags.Instance | BindingFlags.NonPublic);
            if (toggle == null || setMode == null)
            {
                Debug.LogWarning("REALUI could not reflect the multi-select entry points");
                return;
            }

            setMode.Invoke(touch, new object[] { true });
            var owned = sim.GetSnapshot().Towers
                .Where(tower => tower.OwnerId.Equals(sim.LocalPlayerId) && tower.LaneId.Equals(sim.LocalPlayerLaneId))
                .Take(3)
                .ToList();
            foreach (var tower in owned)
            {
                toggle.Invoke(touch, new object[] { tower });
            }
        }

        private static void ShowSendOverMultiSelect()
        {
            ShowMultiSelection();
            var dock = Dock();
            var touch = Object.FindAnyObjectByType<TouchPlacementController>();
            if (dock == null || touch == null) return;

            // Exactly what the SEND button does, so the capture exercises the real path.
            touch.CloseBottomPanelsForSend();
            SetPrivate(dock, "isExpanded", true);
        }

        /// <summary>
        /// Opens both bottom panels and then takes the local seat's last life.
        /// </summary>
        /// <remarks>
        /// The panels are opened FIRST on purpose. A capture of the eliminated HUD taken from a
        /// clean state proves nothing — the panels would have been shut anyway. Opening them and
        /// then eliminating makes the shot answer the actual question: do they go away.
        ///
        /// RefreshSnapshot is called explicitly because the shot is taken in this same frame, and
        /// the HUD reads elimination off the driver's snapshot rather than off the simulation. The
        /// driver would refresh on its own next frame; without this the capture would be one frame
        /// stale, which is exactly the frame being captured.
        /// </remarks>
        private static void EliminateTheLocalSeat()
        {
            var touch = Object.FindAnyObjectByType<TouchPlacementController>();
            var driver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            var dock = Dock();
            var sim = Simulation();
            if (touch == null || driver == null || dock == null || sim == null)
            {
                Debug.LogWarning("REALUI could not reach the components needed for the eliminated state");
                return;
            }

            SetPrivate(touch, "isPaletteExpanded", true);
            SetPrivate(touch, "selectedTowerCategory", -1);
            SetPrivate(dock, "isExpanded", true);

            sim.EliminateForLocalPlaytest(sim.LocalPlayerId);
            driver.RefreshSnapshot();

            var seat = sim.GetSnapshot().Players.Get(sim.LocalPlayerId);
            Debug.Log($"REALUI eliminated P{sim.LocalPlayerId.Value}: isEliminated={seat.IsEliminated} income={seat.Income.Amount} effective={seat.EffectiveIncome.Amount} driverSaysOut={driver.IsLocalSeatEliminated}");
        }

        /// <summary>Sets both panels' open flags again, from outside, to prove they cannot stay open.</summary>
        private static void ForcePanelsOpenWhileEliminated()
        {
            var touch = Object.FindAnyObjectByType<TouchPlacementController>();
            var dock = Dock();
            if (touch == null || dock == null)
            {
                return;
            }

            SetPrivate(touch, "isPaletteExpanded", true);
            SetPrivate(dock, "isExpanded", true);
            Debug.Log($"REALUI forced panels open while eliminated; paletteExpanded={touch.IsTowerPaletteExpanded} dockExpanded={dock.IsExpanded}");
        }

        private static LocalSessionFlowOverlay Overlay() => Object.FindAnyObjectByType<LocalSessionFlowOverlay>();

        private static UnitySimulationDriver Driver() => Object.FindAnyObjectByType<UnitySimulationDriver>();

        /// <summary>
        /// A clean, populated, running board — then paused, so the pause screen has something real
        /// behind its veil.
        /// </summary>
        /// <remarks>
        /// Rebuilt rather than inherited from the shots above. Shot 11 eliminates the local seat and
        /// wipes its lane, so continuing from there would capture the pause screen over an empty
        /// half-board reporting zero lives — a picture of a state a player would almost never pause
        /// in, which is not what this shot is for.
        /// </remarks>
        private static void ShowPausedMatch()
        {
            var driver = Driver();
            var sim = Simulation();
            var touch = Object.FindAnyObjectByType<TouchPlacementController>();
            var dock = Dock();
            if (driver == null || sim == null)
            {
                Debug.LogWarning("REALUI could not reach the driver/simulation for the pause shot");
                return;
            }

            if (touch != null)
            {
                SetPrivate(touch, "isPaletteExpanded", false);
                SetPrivate(touch, "selectedTower", null);
            }

            if (dock != null)
            {
                SetPrivate(dock, "isExpanded", false);
            }

            driver.ResetMatch();
            driver.StartMatch();
            sim.GrantLocalPlaytestGold(sim.LocalPlayerId, new Gold(4000));

            var lane = sim.LocalPlayerLaneId;
            foreach (var cell in new[] { new GridPosition(1, 5), new GridPosition(3, 5), new GridPosition(2, 8), new GridPosition(4, 9) })
            {
                sim.PlaceTower(sim.LocalPlayerId, lane, SampleVerticalSliceContent.TowerId, cell);
            }

            // Long enough for the bots to open and for creeps to be walking somewhere.
            for (var index = 0; index < 160; index++)
            {
                sim.AdvanceOneTick();
            }

            driver.RefreshSnapshot(drainEvents: true);
            driver.PauseMatch();
        }

        private static void ShowResultsWithLocalSeatBeaten() => ForceMatchEnd(localSeatWins: false);

        private static void ShowResultsWithLocalSeatWinning() => ForceMatchEnd(localSeatWins: true);

        /// <summary>
        /// Ends the match by taking every seat but one, so the results screen has a real summary.
        /// </summary>
        /// <remarks>
        /// Both outcomes are captured because the headline is the one place on that screen where the
        /// composition changes rather than the numbers: VICTORY is gold and DEFEAT is cloud, and a
        /// single shot would only ever show one of them. The state is produced through
        /// <c>EliminateForLocalPlaytest</c> and a real tick — the same path a leak takes — rather
        /// than by writing a MatchSummary into the driver, so what is captured is a state the game
        /// can actually be in.
        /// </remarks>
        private static void ForceMatchEnd(bool localSeatWins)
        {
            var driver = Driver();
            var sim = Simulation();
            if (driver == null || sim == null)
            {
                Debug.LogWarning("REALUI could not reach the driver/simulation for the results shot");
                return;
            }

            driver.ResetMatch();
            driver.StartMatch();

            var seats = sim.GetSnapshot().Players.Players.Select(player => player.PlayerId).ToList();
            var survivor = localSeatWins
                ? sim.LocalPlayerId
                : seats.FirstOrDefault(seat => !seat.Equals(sim.LocalPlayerId));

            // A few ticks of real play first, so the scoreboard carries gold and income that the
            // match produced rather than eight identical starting rows.
            for (var index = 0; index < 120; index++)
            {
                sim.AdvanceOneTick();
            }

            foreach (var seat in seats.Where(seat => !seat.Equals(survivor)))
            {
                sim.EliminateForLocalPlaytest(seat);
            }

            // The summary is created during a tick, not by the elimination itself.
            sim.AdvanceOneTick();
            driver.RefreshSnapshot(drainEvents: true);

            Debug.Log(
                $"REALUI forced match end: survivor=P{survivor.Value} localWins={localSeatWins} " +
                $"summary={(driver.LatestMatchSummary is null ? "NULL" : $"winner P{driver.LatestMatchSummary.WinnerId.Value} at T{driver.LatestMatchSummary.CompletedAtTick.Value}")}");
        }

        private static void ShowTitle()
        {
            var overlay = Overlay();
            if (overlay == null)
            {
                Debug.LogWarning("REALUI no LocalSessionFlowOverlay found for the title shot");
                return;
            }

            overlay.ReturnToTitle();
        }

        private static void ShowCodex()
        {
            var overlay = Overlay();
            if (overlay == null)
            {
                Debug.LogWarning("REALUI no LocalSessionFlowOverlay found for the codex shot");
                return;
            }

            // Back to the title first, because the shot before this one leaves the IMGUI settings
            // panel open and nothing about opening the codex closes it — in the real UI that state
            // is unreachable, since the settings panel covers the button. Returning to the title is
            // what clears it.
            overlay.ReturnToTitle();
            overlay.ShowCodex();
        }

        /// <summary>
        /// Presses a named UI Toolkit button on the shell document, or warns. The element must
        /// already be laid out (on a screen shown in an earlier shot): a press on an empty rect
        /// is rejected by Clickable, so a helper that switches screens and presses in the same
        /// call photographs the screen it started on.
        /// </summary>
        private static void PressShellButton(string name, string shotLabel)
        {
            var document = Object.FindAnyObjectByType<UIDocument>();
            var button = document != null && document.rootVisualElement != null
                ? document.rootVisualElement.Q<Button>(name)
                : null;
            if (button == null)
            {
                Debug.LogWarning($"REALUI no '{name}' button found for the {shotLabel} shot");
                return;
            }

            // Press and release through the Clickable manipulator, for the reason ShowCodexCreeps
            // documents: a bare ClickEvent (or a submit event) is received and does nothing.
            var centre = button.worldBound.center;
            var local = button.WorldToLocal(centre);
            SendPointer<PointerDownEvent>(button, centre, local);
            SendPointer<PointerUpEvent>(button, centre, local);
        }

        /// <summary>The first-run offer: START GAME pressed with the tutorial never seen.</summary>
        private static void ShowFirstRunOffer()
        {
            var overlay = Overlay();
            if (overlay == null)
            {
                Debug.LogWarning("REALUI no LocalSessionFlowOverlay found for the first-run shot");
                return;
            }

            // Cleared directly rather than through PresentationPreferences so this runner does
            // not depend on the flag's accessor name — the key is the contract.
            PlayerPrefs.DeleteKey("ltw.tutorial.seen");
            overlay.ReturnToTitle();
            overlay.StartGame();
        }

        /// <summary>Practice started from the title: the coach strip over the opening countdown.</summary>
        private static void ShowPracticeCoachStrip()
        {
            var overlay = Overlay();
            if (overlay == null)
            {
                Debug.LogWarning("REALUI no LocalSessionFlowOverlay found for the practice shot");
                return;
            }

            overlay.ReturnToTitle();
            overlay.StartPractice();
        }

        /// <summary>
        /// The codex, switched to its creeps half.
        /// </summary>
        /// <remarks>
        /// Presses the real tab button rather than reaching for the presenter's state. Which half
        /// is shown belongs to <c>CodexScreenView</c> and is deliberately not public — exposing a
        /// setter so a capture could pose the screen would make the capture the reason a private
        /// thing became public, and a captured state that no button can reach is not evidence about
        /// the shipped screen anyway.
        /// </remarks>
        private static void ShowCodexCreeps()
        {
            ShowCodex();

            var document = Object.FindAnyObjectByType<UIDocument>();
            var tab = document != null && document.rootVisualElement != null
                ? document.rootVisualElement.Q<Button>("codex-tab-creeps")
                : null;

            if (tab == null)
            {
                Debug.LogWarning("REALUI could not find codex-tab-creeps for the creeps shot");
                return;
            }

            // A press and a release, not a synthesized ClickEvent. Button answers through its
            // Clickable manipulator, which tracks the pointer down and only then raises the click —
            // so a bare ClickEvent is received and does nothing, which is exactly what the first
            // version of this shot captured: the wards half, labelled as the creeps one.
            var centre = tab.worldBound.center;
            var local = tab.WorldToLocal(centre);
            SendPointer<PointerDownEvent>(tab, centre, local);
            SendPointer<PointerUpEvent>(tab, centre, local);
        }

        private static void SendPointer<T>(VisualElement element, Vector2 world, Vector2 local)
            where T : EventBase<T>, new()
        {
            using var evt = EventBase<T>.GetPooled();
            evt.target = element;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = evt.GetType();
            type.GetProperty("position", Flags)?.SetValue(evt, (Vector3)world);
            type.GetProperty("localPosition", Flags)?.SetValue(evt, (Vector3)local);
            type.GetProperty("button", Flags)?.SetValue(evt, 0);
            type.GetProperty("pointerId", Flags)?.SetValue(evt, PointerId.mousePointerId);

            element.SendEvent(evt);
        }

        private static void ShowSettingsOverTitle()
        {
            var overlay = Overlay();
            if (overlay == null)
            {
                return;
            }

            overlay.OpenSettings();
        }

        /// <summary>The live simulation behind the command adapter, or null if it is not up yet.</summary>
        private static LocalVerticalSlice Simulation()
        {
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (commands == null)
            {
                return null;
            }

            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(commands) as LocalVerticalSlice;
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
            SetSelectedCategory(dock, 1);
        }

        private static void OpenBuildCategoryOne()
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
                SetPrivate(dock, "isExpanded", false);
            }

            SetPrivate(touch, "isPaletteExpanded", true);
            SetPrivate(touch, "selectedTowerCategory", 0);
        }

        private static void OpenBuildCategoryOneDuringOpeningCountdown()
        {
            var driver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            if (driver == null)
            {
                Debug.LogWarning("REALUI no UnitySimulationDriver found");
                return;
            }

            // Puts the driver back into the state a fresh match actually starts in — HasStarted
            // false, IsOpeningBuildCountdown true — which every earlier shot skips past by calling
            // StartMatch() once in Seed() and never revisiting it.
            driver.BeginOpeningBuildCountdown();

            // The CATEGORY PICKER (ARCANE/FOUNDRY/GROVE), not a category's tower grid — that is
            // what the device screenshot showed overlapping, and OpenBuildCategoryOne opens the
            // grid one level past it.
            OpenBuildPalette();
        }

        private static void OpenBothRailsAtOnce()
        {
            var touch = Object.FindAnyObjectByType<TouchPlacementController>();
            var dock = Dock();
            if (touch == null || dock == null)
            {
                Debug.LogWarning("REALUI no TouchPlacementController or SendDockController found");
                return;
            }

            // Deliberately NOT via OpenSendDock()/OpenBuildPalette() — both of those force the
            // OTHER dock closed as part of their own setup, which is exactly the phone-only
            // assumption this shot exists to prove is gone. Set both open directly instead.
            SetPrivate(dock, "isExpanded", true);
            SetSelectedCategory(dock, 0);
            SetPrivate(touch, "isPaletteExpanded", true);
            SetPrivate(touch, "selectedTowerCategory", 0);
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

            var written = Directory.Exists(outputDirectory) ? Directory.GetFiles(outputDirectory, "*.png").Length : 0;
            if (error == null && written < Shots.Length)
            {
                Debug.LogError(
                    $"REALUI only {written} of {Shots.Length} screenshots reached disk. ScreenCapture " +
                    "reports nothing when the Game view is not presenting, so this is the only place it shows.");
            }

            Debug.Log($"REALUI DONE shots={shot} written={written} dir={outputDirectory}");

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
