#nullable enable

using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Owns session flow: which shell state the app is in, and what each shell action does.
    /// </summary>
    /// <remarks>
    /// Title, pause and results are no longer drawn here. They are full-screen UI Toolkit
    /// compositions in <see cref="UI.ShellScreenView"/>, and this component drives that view and
    /// receives its taps through <see cref="IShellScreenActions"/>. What stays in IMGUI is the
    /// second rank of panels — READY, SETTINGS, the opening build countdown and the live
    /// pause/reset rail — because those are not the screens this pass was asked to replace and
    /// migrating them would have meant migrating the whole HUD with them. HOW TO PLAY left that
    /// rank with the practice pass: the shell view draws it itself now, and the coach strip the
    /// view also owns is driven from here through <see cref="TutorialDirector"/>.
    ///
    /// The split does not move the decision anywhere. This component still computes the session
    /// state, still publishes <see cref="RuntimeUiChrome.ModalScreenActive"/> from it, and still
    /// performs every action; the view only renders and reports. Principle 3 of
    /// docs/GAME_MENU_AND_RUNTIME_FLOW.md — opening a menu must not mutate simulation state — is
    /// therefore still enforced in one place.
    ///
    /// Practice is a match, not a mode. It is the ordinary match with every bot on the passive
    /// profile and the coach strip running, entered from the title at any time or offered once on
    /// the first START GAME. It ends the way any match ends — reset, rematch or exit — and each of
    /// those restores the normal options, so there is no state in which a "normal" match could
    /// quietly start with passive bots.
    /// </remarks>
    public sealed class LocalSessionFlowOverlay : MonoBehaviour, IShellScreenActions
    {
        private static readonly Color Cloud = new Color(0.957f, 0.969f, 1f, 1f);
        private static readonly Color MutedCloud = new Color(0.62f, 0.72f, 0.88f, 1f);
        private static readonly Color PanelInk = new Color(0.027f, 0.047f, 0.082f, 1f);
        private static readonly Color MintSignal = new Color(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color SignalGold = new Color(1f, 0.784f, 0.29f, 1f);
        private static readonly Color ArcaneBlue = new Color(0.247f, 0.557f, 0.957f, 1f);
        private static readonly Color WarningRose = new Color(1f, 0.384f, 0.455f, 1f);

        private UnitySimulationDriver simulationDriver = null!;
        private LocalPlaytestRecorder? playtestRecorder;
        private ShellScreenView? shellScreens;
        private GUIStyle? buttonStyle;
        private GUIStyle? titleStyle;
        private GUIStyle? subtitleStyle;
        private GUIStyle? bodyStyle;
        private GUIStyle? smallStyle;
        private bool showSettings;
        private PreMatchScreen preMatchScreen = PreMatchScreen.Title;
        private TutorialDirector? tutorialDirector;

        private enum PreMatchScreen
        {
            Title,
            Ready,
            Codex,
            FirstRunOffer
        }

        public void Initialize(UnitySimulationDriver driver, LocalPlaytestRecorder recorder, ShellScreenView? shellScreenView = null)
        {
            simulationDriver = driver;
            playtestRecorder = recorder;
            shellScreens = shellScreenView;
            shellScreens?.Initialize(driver, this);
        }

        /// <summary>
        /// Whether a session panel currently owns the display, readable outside this assembly.
        /// </summary>
        /// <remarks>
        /// A passthrough rather than a second source of truth. `RuntimeUiChrome` is internal, so
        /// editor-side checks cannot read it directly, and widening that type's visibility to suit a
        /// test is the wrong trade — the concept belongs to this overlay, which is what sets it.
        /// </remarks>
        public static bool ModalScreenActive => RuntimeUiChrome.ModalScreenActive;

        /// <summary>
        /// Publishes whether a session screen owns the display, before any OnGUI runs this frame.
        /// </summary>
        /// <remarks>
        /// In Update rather than OnGUI on purpose. Unity runs every Update before any OnGUI, so the
        /// HUD components read a value that is already settled for the frame; setting it during the
        /// overlay's own OnGUI would be too late, because the overlay draws last.
        /// </remarks>
        private void Update()
        {
            RuntimeUiChrome.ModalScreenActive = simulationDriver is not null && OwnsDisplay;


            // Driven from the same Update that publishes modality, off the same state, so the
            // rendered screen and the input gate cannot disagree for a frame.
            var screen = ActiveShellScreen;

            // TITLE is a UI Toolkit composition, and UI Toolkit's runtime panel paints after IMGUI
            // regardless of which one issued its draw calls first — so the 0.94-alpha settings box
            // (and the modal scrim behind it) do nothing to it; the logo and menu text render straight
            // through, fully bright. Captured 2026-08-29 ("settings-over-title"): "LINE WARDS" and the
            // button list plainly legible on top of the SETTINGS panel.
            //
            // Scoped to TITLE only, not PAUSE or RESULTS: those two sit in front of the live/final
            // board, and hiding the shell there would reveal it dimmed only by the IMGUI scrim — the
            // exact "dropping them onto a dimmed board" regression the remark below already fixed
            // once. TITLE has no board behind it, so hiding it costs nothing and there is no bleed
            // fix possible on the IMGUI side alone.
            var visibleScreen = showSettings && screen == ShellScreen.Title ? ShellScreen.None : screen;
            shellScreens?.Show(visibleScreen);

            // The music is told a menu is up, from the same state and the same frame. Title,
            // pause and results are all "no board to answer", so all three score the same way.
            LTWAudioDirector.Instance?.SetMenuScored(screen != ShellScreen.None);

            // Same frame, same state again: the coach strip hides under exactly the panels that
            // own the display — pause, results, settings — and not under the build countdown,
            // which is playable and is where its first two steps happen.
            tutorialDirector?.Tick(suppressed: simulationDriver is not null && OwnsDisplay);
        }

        private void OnDisable()
        {
            // Otherwise a disabled overlay leaves the HUD permanently suppressed.
            RuntimeUiChrome.ModalScreenActive = false;
            shellScreens?.Show(ShellScreen.None);
            LTWAudioDirector.Instance?.SetMenuScored(false);
        }

        private void OnDestroy()
        {
            // The director subscribed to the strip's events; the strip may outlive this component.
            tutorialDirector?.Dispose();
            tutorialDirector = null;
        }

        /// <summary>
        /// Which full-screen shell composition should be up this frame, if any.
        /// </summary>
        /// <remarks>
        /// Deliberately a narrower question than <see cref="OwnsDisplay"/>, and the two are not
        /// interchangeable. READY, SETTINGS and the opening build countdown are all still IMGUI,
        /// so they own the display without a UI Toolkit screen behind them; the tests below are
        /// ordered to match <see cref="OnGUI"/> exactly so the two can never disagree about which
        /// state is active.
        ///
        /// SETTINGS is the one case where a shell screen stays up underneath: the IMGUI settings
        /// panel draws over the runtime panel, so leaving the title or pause composition behind it
        /// keeps the app in the place the player opened settings from instead of dropping them onto
        /// a dimmed board for the duration.
        /// </remarks>
        private ShellScreen ActiveShellScreen
        {
            get
            {
                if (simulationDriver is null || simulationDriver.IsOpeningBuildCountdown)
                {
                    return ShellScreen.None;
                }

                if (simulationDriver.LatestMatchSummary is not null)
                {
                    return ShellScreen.Results;
                }

                if (!simulationDriver.HasStarted)
                {
                    return preMatchScreen switch
                    {
                        PreMatchScreen.Title => ShellScreen.Title,
                        PreMatchScreen.Codex => ShellScreen.Codex,
                        PreMatchScreen.FirstRunOffer => ShellScreen.FirstRunOffer,
                        _ => ShellScreen.None
                    };
                }

                return simulationDriver.IsPaused ? ShellScreen.Pause : ShellScreen.None;
            }
        }

        /// <summary>
        /// Whether a full-screen session panel is up, as opposed to a playable phase.
        /// </summary>
        /// <remarks>
        /// The opening build countdown must short-circuit to false BEFORE the HasStarted and
        /// IsPaused tests, not merely be absent from them. `BeginOpeningBuildCountdown` sets
        /// `HasStarted = false` AND `IsPaused = true`, so a countdown is caught by both of those
        /// conditions — dropping it from the list changed nothing, which is exactly what the second
        /// bug report said: still no build or send button during the build phase.
        ///
        /// It is playable despite both flags. `UnityCommandAdapter.PlaceTower` gates on neither —
        /// it goes straight to the simulation — so towers CAN be placed during the countdown, which
        /// is what its panel means by "Place opening towers". Sends are a different matter and
        /// correctly refuse, because `SendCreep` DOES check both, matching "Sends unlock when LIVE
        /// begins".
        /// </remarks>
        private bool OwnsDisplay
        {
            get
            {
                if (showSettings || simulationDriver.LatestMatchSummary is not null)
                {
                    return true;
                }

                if (simulationDriver.IsOpeningBuildCountdown)
                {
                    return false;
                }

                return !simulationDriver.HasStarted || simulationDriver.IsPaused;
            }
        }

        private void OnGUI()
        {
            if (simulationDriver is null)
            {
                return;
            }

            EnsureStyle();

            var scale = MobileViewportLayout.UiScale();
            if (showSettings)
            {
                RuntimeUiChrome.DrawModalScrim();
                DrawSettingsPanel(scale);
                return;
            }

            if (simulationDriver.IsOpeningBuildCountdown)
            {
                // No scrim: the build phase is playable, so dimming the board would be dimming the
                // thing the player is being asked to place towers on. See OwnsDisplay.
                DrawBuildCountdownPanel(scale);
                return;
            }

            // Results, title, codex and pause are full-screen UI Toolkit compositions now. They
            // paint their own field, so there is no scrim to draw and nothing for IMGUI to add here.
            if (simulationDriver.LatestMatchSummary is not null)
            {
                return;
            }

            if (!simulationDriver.HasStarted)
            {
                // Every pre-match state that IS a UI Toolkit screen bails here. Testing only for
                // Title would leave the codex (or the first-run offer) falling through to
                // DrawReadyPanel below, which would draw the IMGUI READY card straight over the top
                // of it — the failure is not a missing screen but two screens at once, so it does
                // not look like a wiring bug.
                if (preMatchScreen is PreMatchScreen.Title or PreMatchScreen.Codex or PreMatchScreen.FirstRunOffer)
                {
                    return;
                }

                RuntimeUiChrome.DrawModalScrim();
                DrawReadyPanel(scale);
                return;
            }

            if (simulationDriver.IsPaused)
            {
                return;
            }

            // The live rail is the one state that is NOT modal — it sits alongside the HUD during
            // play rather than taking the screen, so it gets no scrim and blocks nothing.
            DrawLiveRail(scale);
        }

        private void DrawReadyPanel(float scale)
        {
            var panel = CenteredPanel(scale, 336f, 224f);
            DrawPanel(panel, scale);

            DrawLabel(panel.x + 22f * scale, panel.y + 20f * scale, panel.width - 44f * scale, 28f * scale, "LINE WARDS", titleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 22f * scale, panel.y + 50f * scale, panel.width - 44f * scale, 20f * scale, "LOCAL VERTICAL SLICE", subtitleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(
                panel.x + 28f * scale,
                panel.y + 82f * scale,
                panel.width - 56f * scale,
                44f * scale,
                $"Tap PLAY to begin an {Mathf.RoundToInt(simulationDriver.OpeningBuildCountdownDuration)} second build window.",
                bodyStyle!,
                TextAnchor.MiddleCenter);

            var buttonY = panel.yMax - 78f * scale;
            var buttonWidth = 112f * scale;
            var buttonHeight = 30f * scale;
            var gap = 8f * scale;
            var startX = panel.center.x - buttonWidth - gap * 0.5f;

            if (DrawButton(new Rect(startX, buttonY, buttonWidth, buttonHeight), "PLAY", MintSignal, scale))
            {
                showSettings = false;
                simulationDriver.BeginOpeningBuildCountdown();
            }

            if (DrawButton(new Rect(startX + buttonWidth + gap, buttonY, buttonWidth, buttonHeight), "SETTINGS", ArcaneBlue, scale))
            {
                showSettings = true;
            }

            var resetWidth = 84f * scale;
            if (DrawButton(new Rect(panel.center.x - resetWidth * 0.5f, buttonY + buttonHeight + gap, resetWidth, 24f * scale), "MENU", Cloud, scale, 10f))
            {
                ResetToTitle();
            }
        }

        /// <summary>
        /// The coach strip's geometry in the shell panel's reference units, mirrored from
        /// <c>.ltw-coach</c> in Assets/Resources/UI/ShellScreens.uss so the countdown panel can
        /// stay out from under it.
        /// </summary>
        /// <remarks>
        /// Reference units, NOT overlay <c>scale</c> units. The shell panel is
        /// <c>ScaleWithScreenSize</c> at 1080x2340 with <c>match = 1</c>, so one of its units is
        /// <c>ViewportHeight / 2340</c> device pixels; the overlay's <c>UiScale</c> is a different
        /// ratio (min of width/430, height/932) and on the iPad frame is 2.5x larger. Converting
        /// with the wrong one would have dropped the panel to 39% of the screen.
        ///
        /// The height is the two-line-body case (2 border + 14 pad + 44 head + 6 + 2x30 body + 10 +
        /// 58 foot + 16 pad + 2 border), because MAZE's body wraps at the strip's 780-unit maximum
        /// and MAZE is shown during the countdown. A one-line step leaves a slightly larger gap,
        /// which is the safe direction: the failure being fixed is the panel covering the body.
        /// </remarks>
        private const float CoachStripTopReferenceUnits = 176f;
        private const float CoachStripHeightReferenceUnits = 212f;
        private const float ShellPanelReferenceHeight = 2340f;

        private void DrawBuildCountdownPanel(float scale)
        {
            var frame = MobileViewportLayout.ScreenRect();
            var margin = MobileViewportLayout.EdgeMargin(scale);
            var width = Mathf.Min(318f * scale, frame.width - margin * 2f);
            var height = 112f * scale;
            var top = frame.y + 132f * scale;

            // Measured on the real-25 practice captures: at 2064x2752 this panel's default top
            // (14% of screen height) sat inside the strip (11-17%), covering step 1's body line;
            // at 1080x1920 the two were edge to edge. The strip is docked under the HUD header on
            // purpose and does not move, so the panel yields: while the strip is up, its top is
            // pushed to the strip's bottom edge plus a gap. Only ever pushed down, never up, so a
            // frame where the strip is hidden draws exactly where it always did.
            if (tutorialDirector is { StripVisible: true })
            {
                var referenceUnit = MobileViewportLayout.ViewportHeight / ShellPanelReferenceHeight;
                var safeTopInset = MobileViewportLayout.ViewportHeight - MobileViewportLayout.SafeArea.yMax;
                var stripBottom = safeTopInset + (CoachStripTopReferenceUnits + CoachStripHeightReferenceUnits) * referenceUnit;
                top = Mathf.Max(top, stripBottom + 12f * scale);
            }

            var panel = new Rect(frame.center.x - width * 0.5f, top, width, height);
            DrawPanel(panel, scale);

            var seconds = Mathf.CeilToInt(simulationDriver.OpeningBuildCountdownRemaining);
            DrawLabel(panel.x + 18f * scale, panel.y + 10f * scale, panel.width - 36f * scale, 20f * scale, "BUILD PHASE", subtitleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 24f * scale, panel.y + 31f * scale, panel.width - 48f * scale, 32f * scale, seconds.ToString(), titleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 22f * scale, panel.y + 62f * scale, panel.width - 44f * scale, 18f * scale, "Place opening towers. Sends unlock when LIVE begins.", bodyStyle!, TextAnchor.MiddleCenter);

            var buttonWidth = 96f * scale;
            var buttonHeight = 24f * scale;
            var gap = 8f * scale;
            var rowY = panel.yMax - 30f * scale;
            var startX = panel.center.x - buttonWidth - gap * 0.5f;

            if (DrawButton(new Rect(startX, rowY, buttonWidth, buttonHeight), "START NOW", MintSignal, scale, 9f))
            {
                simulationDriver.StartMatch();
            }

            if (DrawButton(new Rect(startX + buttonWidth + gap, rowY, buttonWidth, buttonHeight), "MENU", Cloud, scale, 9f))
            {
                ResetToTitle();
            }
        }

        private void DrawSettingsPanel(float scale)
        {
            // Grown for the health-bar toggle below: one row (34f) plus 12f of clearance. The
            // extra 12f is new, not carried over — the four-row panel already ran the last row's
            // bottom 8f past BACK's top, and a straight +34f would have reproduced that same
            // overlap one row lower rather than fixing it.
            var panel = CenteredPanel(scale, 336f, 290f);
            DrawPanel(panel, scale);

            DrawLabel(panel.x + 22f * scale, panel.y + 18f * scale, panel.width - 44f * scale, 28f * scale, "SETTINGS", titleStyle!, TextAnchor.MiddleCenter);
            DrawLabel(panel.x + 22f * scale, panel.y + 50f * scale, panel.width - 44f * scale, 22f * scale, "Presentation toggles for quick playtest reads.", bodyStyle!, TextAnchor.MiddleCenter);

            var labelX = panel.x + 26f * scale;
            var rowX = panel.x + 146f * scale;
            var rowY = panel.y + 86f * scale;
            var rowHeight = 28f * scale;
            var buttonHeight = 24f * scale;
            var wideWidth = 112f * scale;
            var gap = 6f * scale;

            DrawLabel(labelX, rowY, 110f * scale, rowHeight, "Audio", smallStyle!, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(rowX, rowY + 2f * scale, wideWidth, buttonHeight), PresentationPreferences.AudioMuted ? "MUTED" : "ON", PresentationPreferences.AudioMuted ? WarningRose : MintSignal, scale, 10f))
            {
                PresentationPreferences.ToggleAudioMuted();
            }

            rowY += rowHeight + gap;
            DrawLabel(labelX, rowY, 110f * scale, rowHeight, "Effects", smallStyle!, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(rowX, rowY + 2f * scale, wideWidth, buttonHeight), PresentationPreferences.ReducedEffects ? "REDUCED" : "FULL", PresentationPreferences.ReducedEffects ? SignalGold : MintSignal, scale, 10f))
            {
                PresentationPreferences.ReducedEffects = !PresentationPreferences.ReducedEffects;
            }

            // Unified with the toggle rows above (owner's call, 2026-08-30, chosen from a rendered
            // comparison against the original bare +/- pair with the value in the row label): every
            // row's right-hand control now occupies the same wideWidth footprint a toggle pill
            // does, range rows split into minus / value / plus so the value moves off the row label
            // and into a chip that looks like the toggle rows' pill.
            var stepperMinusWidth = 28f * scale;
            var stepperPlusWidth = 28f * scale;
            var stepperValueWidth = wideWidth - stepperMinusWidth - stepperPlusWidth - gap * 2f;

            rowY += rowHeight + gap;
            DrawLabel(labelX, rowY, 110f * scale, rowHeight, "Text", smallStyle!, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(rowX, rowY + 2f * scale, stepperMinusWidth, buttonHeight), "-", Cloud, scale, 12f))
            {
                PresentationPreferences.TextScale = Mathf.Clamp(PresentationPreferences.TextScale - 0.1f, 0.8f, 1.5f);
            }

            DrawButton(new Rect(rowX + stepperMinusWidth + gap, rowY + 2f * scale, stepperValueWidth, buttonHeight), $"{PresentationPreferences.TextScale:0.0}x", Cloud, scale, 10f);

            if (DrawButton(new Rect(rowX + stepperMinusWidth + gap + stepperValueWidth + gap, rowY + 2f * scale, stepperPlusWidth, buttonHeight), "+", Cloud, scale, 12f))
            {
                PresentationPreferences.TextScale = Mathf.Clamp(PresentationPreferences.TextScale + 0.1f, 0.8f, 1.5f);
            }

            rowY += rowHeight + gap;
            DrawLabel(labelX, rowY, 110f * scale, rowHeight, "Volume", smallStyle!, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(rowX, rowY + 2f * scale, stepperMinusWidth, buttonHeight), "-", Cloud, scale, 12f))
            {
                PresentationPreferences.AdjustFeedbackVolume(-0.1f);
            }

            DrawButton(new Rect(rowX + stepperMinusWidth + gap, rowY + 2f * scale, stepperValueWidth, buttonHeight), $"{Mathf.RoundToInt(PresentationPreferences.FeedbackVolume * 100f)}%", Cloud, scale, 10f);

            if (DrawButton(new Rect(rowX + stepperMinusWidth + gap + stepperValueWidth + gap, rowY + 2f * scale, stepperPlusWidth, buttonHeight), "+", Cloud, scale, 12f))
            {
                PresentationPreferences.AdjustFeedbackVolume(0.1f);
            }

            rowY += rowHeight + gap;
            DrawLabel(labelX, rowY, 110f * scale, rowHeight, "Health Bars", smallStyle!, TextAnchor.MiddleLeft);
            if (DrawButton(new Rect(rowX, rowY + 2f * scale, wideWidth, buttonHeight), PresentationPreferences.HealthBarsVisible ? "ON" : "OFF", PresentationPreferences.HealthBarsVisible ? MintSignal : Cloud, scale, 10f))
            {
                PresentationPreferences.HealthBarsVisible = !PresentationPreferences.HealthBarsVisible;
            }

            var backWidth = 96f * scale;
            if (DrawButton(new Rect(panel.center.x - backWidth * 0.5f, panel.yMax - 38f * scale, backWidth, 26f * scale), "BACK", ArcaneBlue, scale, 10f))
            {
                showSettings = false;
            }
        }

        private void DrawLiveRail(float scale)
        {
            var frame = MobileViewportLayout.ScreenRect();
            var buttonWidth = 62f * scale;
            var buttonHeight = 26f * scale;
            var gap = 4f * scale;
            var y = frame.y + 58f * scale;
            var x = frame.xMax - buttonWidth - MobileViewportLayout.EdgeMargin(scale);

            if (DrawButton(new Rect(x, y, buttonWidth, buttonHeight), "PAUSE", SignalGold, scale, 11f))
            {
                simulationDriver.TogglePause();
            }

            // Labelled RESET rather than "R", and the same width as PAUSE.
            //
            // It abandons the match in progress. A one-letter label on a destructive control, sat
            // directly under the pause button and 34px wide against PAUSE's 62, is a misclick away
            // from throwing away a game — and nothing about "R" tells you that before you press it.
            // Matching the width also squares the two into a column instead of leaving the smaller
            // one right-aligned against the larger.
            if (DrawButton(new Rect(x, y + buttonHeight + gap, buttonWidth, buttonHeight), "RESET", WarningRose, scale, 10f))
            {
                ResetToReady();
            }
        }

        private Rect CenteredPanel(float scale, float targetWidth, float targetHeight)
        {
            var frame = MobileViewportLayout.ScreenRect();
            var margin = MobileViewportLayout.EdgeMargin(scale);
            var width = Mathf.Min(targetWidth * scale, frame.width - margin * 2f);
            var height = Mathf.Min(targetHeight * scale, frame.height - margin * 2f);
            return new Rect(frame.center.x - width * 0.5f, frame.center.y - height * 0.5f, width, height);
        }

        /// <summary>
        /// Session-flow panel background, delegated to the shared chrome.
        /// </summary>
        /// <remarks>
        /// This drew a flat rect plus four 1px hairline edges — hard corners, no bevel, no depth —
        /// while the rest of the HUD drew Unity's stock skin box and the buttons drew a chamfered
        /// bevel. Three looks, none of them shared. All four now use
        /// <see cref="RuntimeUiChrome.DrawPanel"/>.
        /// </remarks>
        private void DrawPanel(Rect rect, float scale) =>
            RuntimeUiChrome.DrawPanel(rect, PanelInk, scale);

        private void DrawLabel(float x, float y, float width, float height, string text, GUIStyle style, TextAnchor alignment)
        {
            var priorAlignment = style.alignment;
            style.alignment = alignment;
            GUI.Label(new Rect(x, y, width, height), text, style);
            style.alignment = priorAlignment;
        }

        private bool DrawButton(Rect rect, string label, Color accent, float scale, float baseFontSize = 11f)
        {
            buttonStyle!.fontSize = Mathf.RoundToInt(baseFontSize * scale * PresentationPreferences.TextScale);
            return RuntimeUiChrome.DrawPanelButton(rect, label, accent, scale, buttonStyle);
        }

        // ------------------------------------------------------------ IShellScreenActions
        //
        // Every one of these is the body the corresponding IMGUI button used to run, unchanged.
        // The shell screens moved to UI Toolkit; what the buttons do did not, which is the point:
        // START GAME still resets to READY and then opens the build countdown, REMATCH does the
        // same pair, RESET still resets, MENU still returns to the title.

        /// <summary>Title: START GAME. Offers practice once, then behaves as it always did.</summary>
        /// <remarks>
        /// The offer is a screen change and nothing else — no reset, no countdown — so declining
        /// it later costs the player nothing they had not already agreed to (principle 3). The
        /// real start lives in <see cref="StartGameNow"/>, which the offer's decline path and every
        /// later START GAME both reach.
        /// </remarks>
        public void StartGame()
        {
            if (!PresentationPreferences.TutorialSeen)
            {
                preMatchScreen = PreMatchScreen.FirstRunOffer;
                return;
            }

            StartGameNow();
        }

        /// <summary>First-run offer: START GAME (decline practice). Also what START GAME does once seen.</summary>
        /// <remarks>
        /// Declining counts as seen: a player who said "just let me play" should not be asked
        /// again on every launch. PRACTICE on the title remains the way back in.
        /// </remarks>
        public void StartGameNow()
        {
            PresentationPreferences.TutorialSeen = true;
            ResetToReady();
            simulationDriver.BeginOpeningBuildCountdown();
        }

        /// <summary>Title and first-run offer: PRACTICE.</summary>
        /// <remarks>
        /// Goes straight into the build countdown rather than through READY: the coach strip's
        /// first step is "place three wards", and the countdown is the window in which that is
        /// possible before anything is live. The rebuild is what makes the passive profile real —
        /// see <see cref="UnitySimulationDriver.RebuildMatchFromPendingOptions"/> for why a reset
        /// alone would have left the bots exactly as aggressive as before.
        ///
        /// Always a fresh board, even if practice was already pending: "PRACTICE brings you back
        /// any time" promises the walkthrough from the top, not a resume.
        /// </remarks>
        public void StartPractice()
        {
            showSettings = false;
            preMatchScreen = PreMatchScreen.Ready;
            tutorialDirector?.Stop();
            LocalMatchRuntimeOptions.EnterPractice();
            simulationDriver.RebuildMatchFromPendingOptions();
            playtestRecorder?.ResetRecorder();
            simulationDriver.BeginOpeningBuildCountdown();
            EnsureTutorialDirector()?.Begin();
        }

        /// <summary>Title: HOW TO PLAY.</summary>
        /// <remarks>
        /// Deliberately nothing. The shell view presents <c>ShellScreen.HowToPlay</c> itself and
        /// returns to the title on its own; the IMGUI panel that used to live here is gone. The
        /// method stays because it is part of <see cref="IShellScreenActions"/>, and an action the
        /// overlay does not need to act on is still not an action it should mutate state for.
        /// </remarks>
        public void ShowHowToPlay()
        {
        }

        /// <summary>Title: CODEX.</summary>
        /// <remarks>
        /// Nothing but a screen change, which is the point. The codex reads the authored content
        /// catalog directly and never touches the driver, so opening it cannot disturb a session —
        /// principle 3 of docs/GAME_MENU_AND_RUNTIME_FLOW.md, kept by there being nothing here to
        /// get wrong rather than by remembering not to.
        /// </remarks>
        public void ShowCodex()
        {
            preMatchScreen = PreMatchScreen.Codex;
        }

        /// <summary>Codex: BACK. Returns to the title, the same way the how-to panel's BACK does.</summary>
        public void CloseCodex()
        {
            preMatchScreen = PreMatchScreen.Title;
        }

        /// <summary>Title, pause and results: SETTINGS. Still an IMGUI panel.</summary>
        public void OpenSettings()
        {
            showSettings = true;
        }

        /// <summary>Pause: RESUME.</summary>
        public void ResumeMatch()
        {
            simulationDriver.TogglePause();
        }

        /// <summary>Results: REMATCH. The same pair of calls as <see cref="StartGameNow"/> — always a normal match, even after practice.</summary>
        public void Rematch()
        {
            ResetToReady();
            simulationDriver.BeginOpeningBuildCountdown();
        }

        /// <summary>Pause and results: EXIT TO TITLE.</summary>
        public void ReturnToTitle()
        {
            ResetToTitle();
        }

        /// <summary>Title: QUIT.</summary>
        public void QuitGame()
        {
            QuitApplication();
        }

        /// <summary>Pause: RESET MATCH. Public because the shell screens call it.</summary>
        public void ResetToReady()
        {
            showSettings = false;
            preMatchScreen = PreMatchScreen.Ready;
            ResetMatchLeavingPractice();
            playtestRecorder?.ResetRecorder();
        }

        private void ResetToTitle()
        {
            showSettings = false;
            preMatchScreen = PreMatchScreen.Title;
            ResetMatchLeavingPractice();
            playtestRecorder?.ResetRecorder();
        }

        /// <summary>
        /// The one reset every exit from a match goes through, so practice cannot leak past it.
        /// </summary>
        /// <remarks>
        /// Reset, rematch and exit-to-title all land here. If practice options are pending, the
        /// normal ones are restored and the match is REBUILT rather than reset, because a reset
        /// keeps the bots it was constructed with. When nothing is pending this is exactly the old
        /// <c>ResetMatch()</c> call, so a normal session never pays for the rebuild.
        ///
        /// The director is stopped, not finished: leaving mid-way is not seeing the tutorial.
        /// </remarks>
        private void ResetMatchLeavingPractice()
        {
            tutorialDirector?.Stop();
            if (LocalMatchRuntimeOptions.PracticePending)
            {
                LocalMatchRuntimeOptions.LeavePractice();
                simulationDriver.RebuildMatchFromPendingOptions();
                return;
            }

            simulationDriver.ResetMatch();
        }

        /// <summary>
        /// The director, created on first use against the shell view's coach strip.
        /// </summary>
        /// <remarks>
        /// Lazy because the strip belongs to the view, which is optional here (editor checks
        /// initialise this overlay without one). Practice still runs without a director in that
        /// case — passive bots and all — it just has nobody narrating it.
        /// </remarks>
        private TutorialDirector? EnsureTutorialDirector()
        {
            if (tutorialDirector is not null)
            {
                return tutorialDirector;
            }

            var strip = shellScreens?.CoachStrip;
            if (strip is null)
            {
                return null;
            }

            tutorialDirector = new TutorialDirector(simulationDriver, strip);
            return tutorialDirector;
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void EnsureStyle()
        {
            buttonStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };

            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };

            subtitleStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = MintSignal }
            };

            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = MutedCloud }
            };

            smallStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };

            titleStyle.fontSize = Mathf.RoundToInt(20f * MobileViewportLayout.UiScale() * PresentationPreferences.TextScale);
            subtitleStyle.fontSize = Mathf.RoundToInt(11f * MobileViewportLayout.UiScale() * PresentationPreferences.TextScale);
            bodyStyle.fontSize = Mathf.RoundToInt(10f * MobileViewportLayout.UiScale() * PresentationPreferences.TextScale);
            smallStyle.fontSize = Mathf.RoundToInt(10f * MobileViewportLayout.UiScale() * PresentationPreferences.TextScale);
        }
    }
}
