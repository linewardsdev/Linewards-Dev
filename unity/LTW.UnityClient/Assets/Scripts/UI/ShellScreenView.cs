#nullable enable

using System.Linq;
using LTW.Simulation.Economy;
using LTW.UnityClient.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace LTW.UnityClient.UI
{
    /// <summary>Which full-screen shell composition currently owns the display.</summary>
    internal enum ShellScreen
    {
        None,
        Title,
        FirstRunOffer,
        HowToPlay,
        Codex,
        Pause,
        Results
    }

    /// <summary>
    /// The actions a shell screen can invoke, implemented by the component that owns session flow.
    /// </summary>
    /// <remarks>
    /// An interface rather than a direct reference to <c>LocalSessionFlowOverlay</c> so the
    /// dependency runs one way: the overlay decides what state the session is in and drives this
    /// view; the view never inspects or mutates session state, it only reports taps. That is what
    /// keeps principle 3 of docs/GAME_MENU_AND_RUNTIME_FLOW.md — opening a menu must not mutate
    /// simulation state — enforced by structure rather than by discipline.
    /// </remarks>
    public interface IShellScreenActions
    {
        void StartGame();

        /// <summary>Title PRACTICE and the first-run offer's PRACTICE FIRST: begin the guided match.</summary>
        void StartPractice();

        /// <summary>The first-run offer's JUST PLAY: decline practice and start a normal match.</summary>
        void StartGameNow();

        /// <summary>
        /// Kept for the overlay, which still implements it; the view no longer calls it. HOW TO
        /// PLAY is a UI Toolkit screen now and the view shows it itself, see <see cref="ShellScreenView.Show"/>.
        /// </summary>
        void ShowHowToPlay();

        void ShowCodex();

        void CloseCodex();

        void OpenSettings();

        void QuitGame();

        void ResumeMatch();

        void ResetToReady();

        void ReturnToTitle();

        void Rematch();
    }

    /// <summary>
    /// The title, first-run offer, how-to-play, codex, pause and results screens, built in UI
    /// Toolkit, plus the coach strip that rides over a practice match.
    /// </summary>
    /// <remarks>
    /// These were three IMGUI cards floating over a live board — the title was literally a 348x284
    /// panel with a 2x2 button grid. They are now full-screen compositions with a real hierarchy:
    /// one dominant mark, one obviously primary action, and subordinate actions that are shorter,
    /// outlined or unframed rather than merely a different colour.
    ///
    /// UI Toolkit rather than IMGUI for one structural reason: IMGUI is immediate-mode, so there is
    /// no retained element to transition. Every fade, ease and state tween on this screen is a USS
    /// transition on an element that persists between frames, which IMGUI cannot express at all.
    /// See OPEN_ITEMS.md item 10.
    ///
    /// This view does NOT gate input. <see cref="RuntimeUiChrome.ModalScreenActive"/> is still what
    /// makes a shell screen modal, and the overlay still publishes it: a UI Toolkit panel and the
    /// IMGUI HUD read input independently, so a full-screen UI Toolkit backdrop covers the HUD
    /// visually while doing nothing whatsoever about the HUD's clicks.
    /// </remarks>
    public sealed class ShellScreenView : MonoBehaviour
    {
        private const string PanelSettingsResource = "UI/LineWardsShellPanelSettings";
        private const string DocumentResource = "UI/ShellScreens";
        private const string StyleSheetResource = "UI/ShellScreens";

        private UnitySimulationDriver simulationDriver = null!;
        private IShellScreenActions actions = null!;

        private UIDocument? document;
        private VisualElement? shellRoot;
        private VisualElement? titleScreen;
        private VisualElement? firstRunScreen;
        private VisualElement? howToPlayScreen;
        private VisualElement? codexScreen;
        private VisualElement? pauseScreen;
        private VisualElement? resultsScreen;
        private CodexScreenView? codex;
        private HowToPlayScreenView? howToPlay;
        private VisualElement? resultsTable;
        private Label? pauseLives;
        private Label? pauseGold;
        private Label? pauseIncome;
        private Label? resultsHeadline;
        private Label? resultsNote;
        private Button? signInButton;
        private Button? playOnlineButton;

        private Texture2D? fieldGradient;
        private Texture2D? wardGlow;

        private ShellScreen currentScreen = ShellScreen.None;

        /// <summary>
        /// Whether HOW TO PLAY is open over the title.
        /// </summary>
        /// <remarks>
        /// The one piece of navigation the view owns rather than the overlay. The overlay drives
        /// <see cref="Show"/> every frame from session state, and HOW TO PLAY is not session state:
        /// it is a screen of copy, opened from the title and closed back to it, with no match to
        /// consult. So the overlay keeps saying Title while this is set, and the view resolves that
        /// to the how-to screen; anything other than Title clears it, so leaving the title by any
        /// route also leaves the how-to.
        /// </remarks>
        private bool howToPlayOpen;

        /// <summary>
        /// The summary the results table was last built from, compared by reference.
        /// </summary>
        /// <remarks>
        /// By reference, not by completion tick, and the difference is not academic. The first
        /// version keyed on <c>CompletedAtTick</c> and a capture caught it out immediately: two
        /// matches forced to end at the same tick produced two different summaries with the same
        /// key, and the screen went on showing the first one — announcing a defeat over the winner's
        /// own scoreboard. Every match end mints a new MatchSummary, so identity is exact where the
        /// tick is merely usually-unique.
        /// </remarks>
        private MatchSummary? renderedSummary;
        private bool built;

        /// <summary>
        /// The practice coach strip. Null only before <see cref="Initialize"/> or when the document
        /// failed to build, which is already reported as an error.
        /// </summary>
        internal CoachStripView CoachStrip { get; private set; } = null!;

        internal void Initialize(UnitySimulationDriver driver, IShellScreenActions shellActions)
        {
            simulationDriver = driver;
            actions = shellActions;
            Build();
        }

        /// <summary>
        /// Shows one screen and hides the rest, and refreshes whatever that screen displays.
        /// </summary>
        /// <remarks>
        /// Idempotent per screen: re-showing the screen already up does not restart its transition,
        /// which matters because the overlay calls this every frame from Update. The live content
        /// (pause stats, results table) is still refreshed, because those do change while shown.
        /// </remarks>
        internal void Show(ShellScreen screen)
        {
            if (!built)
            {
                Build();
                if (!built)
                {
                    return;
                }
            }

            // HOW TO PLAY is view-local, see howToPlayOpen: asking for it directly opens it, asking
            // for the title keeps it, asking for anything else closes it.
            if (screen == ShellScreen.HowToPlay)
            {
                howToPlayOpen = true;
            }
            else if (screen != ShellScreen.Title)
            {
                howToPlayOpen = false;
            }

            var resolved = screen == ShellScreen.Title && howToPlayOpen ? ShellScreen.HowToPlay : screen;

            if (resolved != currentScreen)
            {
                // Leaving the codex or the how-to stops its stage. Each owns a camera pointed at a
                // render texture, and a camera with a target renders every frame whether or not
                // anything reads it — so a player who opens the codex once and then plays a match
                // would otherwise pay a full extra render pass for the rest of the session with
                // nothing on screen to show for it.
                if (currentScreen == ShellScreen.Codex)
                {
                    codex?.Close();
                }

                if (currentScreen == ShellScreen.HowToPlay)
                {
                    howToPlay?.Close();
                }

                HideAll();
                currentScreen = resolved;
                renderedSummary = null;
                Enter(ScreenElement(resolved));
            }

            RefreshContent(resolved);
        }

        /// <summary>Title: HOW TO PLAY. The view's own navigation; nothing about the session changes.</summary>
        private void OpenHowToPlay()
        {
            Show(ShellScreen.HowToPlay);
        }

        /// <summary>How to play: BACK. Returns to whatever the overlay is asking for, which is the title.</summary>
        private void CloseHowToPlay()
        {
            howToPlayOpen = false;
            Show(ShellScreen.Title);
        }

        /// <summary>
        /// SIGN IN WITH GOOGLE: real PlayFab identity, iOS only for now — see
        /// <see cref="LTW.UnityClient.Online.GoogleSignInIOS"/>'s remarks for why (Google archived
        /// the Unity Sign-In plugin; Android needs a separate Google Play Games Services flow this
        /// build does not implement). On other platforms the button still works, it just always
        /// reports "not implemented" rather than silently doing nothing.
        /// </summary>
        private void OnSignInWithGoogleTapped()
        {
            if (signInButton == null)
            {
                return;
            }

            if (LTW.UnityClient.Online.PlayFabSession.IsSignedIn)
            {
                return;
            }

            signInButton.SetEnabled(false);
            signInButton.text = "SIGNING IN...";

            LTW.UnityClient.Online.PlayFabLoginService.SignInWithGoogle(
                new LTW.UnityClient.Online.GoogleSignInIOS(),
                onSuccess: playFabId =>
                {
                    if (signInButton != null)
                    {
                        signInButton.text = "SIGNED IN";
                    }
                },
                onFailure: message =>
                {
                    Debug.LogWarning($"SHELL Google sign-in failed: {message}");
                    if (signInButton != null)
                    {
                        signInButton.text = "SIGN IN WITH GOOGLE";
                        signInButton.SetEnabled(true);
                    }
                });
        }

        /// <summary>
        /// PLAY ONLINE: creates and joins a private match over the wire, using the signed-in
        /// PlayFab identity to claim the seat. See docs/MULTIPLAYER_ROLLOUT.md's MP-06 for what
        /// this does and does not prove — this is the first entry point that can reach any of it.
        /// </summary>
        /// <remarks>
        /// No explicit "connecting" screen (an acceptance check MP-06 already records as not done)
        /// — just the button's own text, the same minimal affordance SIGN IN WITH GOOGLE uses. On
        /// success this does not need to change the screen itself: initializing the driver flips
        /// HasStarted, and LocalSessionFlowOverlay's own per-frame poll of that flag switches away
        /// from Title on its own, exactly as it would for a local match starting.
        /// </remarks>
        private async void OnPlayOnlineTapped()
        {
            if (playOnlineButton == null)
            {
                return;
            }

            if (!LTW.UnityClient.Online.PlayFabSession.IsSignedIn)
            {
                playOnlineButton.text = "SIGN IN FIRST";
                return;
            }

            playOnlineButton.SetEnabled(false);
            playOnlineButton.text = "CONNECTING...";

            var client = await LTW.UnityClient.Online.OnlineMatchService.CreateAndJoinAsync(onFailure: message =>
            {
                Debug.LogWarning($"SHELL play online failed: {message}");
            });

            if (client == null)
            {
                if (playOnlineButton != null)
                {
                    playOnlineButton.text = "PLAY ONLINE";
                    playOnlineButton.SetEnabled(true);
                }

                return;
            }

            simulationDriver.Initialize(client);
            if (simulationDriver.TryGetComponent<LTW.UnityClient.Simulation.UnityCommandAdapter>(out var commandAdapter))
            {
                commandAdapter.Initialize(client, simulationDriver);
            }
            else
            {
                Debug.LogError("SHELL play online: no UnityCommandAdapter on the simulation driver's GameObject — commands will not reach the server.");
            }
        }

        private void OnDisable()
        {
            // Leaves the panel empty rather than frozen on whatever screen was last up.
            currentScreen = ShellScreen.None;
            howToPlayOpen = false;
            codex?.Close();
            howToPlay?.Close();
            HideAll();
            CoachStrip?.Hide();
        }

        private void OnDestroy()
        {
            if (fieldGradient != null)
            {
                Destroy(fieldGradient);
            }

            if (wardGlow != null)
            {
                Destroy(wardGlow);
            }
        }

        private VisualElement? ScreenElement(ShellScreen screen) => screen switch
        {
            ShellScreen.Title => titleScreen,
            ShellScreen.FirstRunOffer => firstRunScreen,
            ShellScreen.HowToPlay => howToPlayScreen,
            ShellScreen.Codex => codexScreen,
            ShellScreen.Pause => pauseScreen,
            ShellScreen.Results => resultsScreen,
            _ => null
        };

        private void HideAll()
        {
            Hide(titleScreen);
            Hide(firstRunScreen);
            Hide(howToPlayScreen);
            Hide(codexScreen);
            Hide(pauseScreen);
            Hide(resultsScreen);
        }

        private static void Hide(VisualElement? screen)
        {
            if (screen is null)
            {
                return;
            }

            screen.RemoveFromClassList("is-shown");
            screen.RemoveFromClassList("is-entered");
        }

        /// <summary>
        /// Shows a screen and starts its enter transition on the following frame.
        /// </summary>
        /// <remarks>
        /// The deferral is not decoration. A USS transition needs a resolved start value, and an
        /// element that was display:none has none — adding both classes in the same frame lands the
        /// end state immediately and the screen snaps in. One scheduled frame later, the layout has
        /// run, opacity 0 and the 22px offset are real, and the transition has something to run from.
        /// </remarks>
        private void Enter(VisualElement? screen)
        {
            if (screen is null)
            {
                return;
            }

            screen.AddToClassList("is-shown");
            screen.schedule.Execute(() => screen.AddToClassList("is-entered")).ExecuteLater(1);
        }

        private void Build()
        {
            if (built)
            {
                return;
            }

            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            if (panelSettings == null)
            {
                Debug.LogError(
                    $"SHELL missing PanelSettings at Resources/{PanelSettingsResource}. " +
                    "Run Line Wards/UI/Create Shell Panel Settings and commit the asset.");
                return;
            }

            var tree = Resources.Load<VisualTreeAsset>(DocumentResource);
            if (tree == null)
            {
                Debug.LogError($"SHELL missing UXML at Resources/{DocumentResource}.");
                return;
            }

            document = GetComponent<UIDocument>();
            if (document == null)
            {
                document = gameObject.AddComponent<UIDocument>();
            }

            document.panelSettings = panelSettings;
            document.visualTreeAsset = tree;

            var root = document.rootVisualElement;
            if (root is null)
            {
                Debug.LogError("SHELL UIDocument produced no root visual element.");
                return;
            }

            // UIDocument's own root must never take a pick. It spans the whole panel, and a pickable
            // full-screen element would sit under the live HUD swallowing pointer events for a shell
            // that is not even showing.
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;

            // The UXML declares its own <Style>, but the resolution of that reference is an import
            // concern; loading it here as well when nothing arrived keeps a runtime build from
            // rendering an unstyled stack of default buttons.
            if (root.styleSheets.count == 0)
            {
                var sheet = Resources.Load<StyleSheet>(StyleSheetResource);
                if (sheet != null)
                {
                    root.styleSheets.Add(sheet);
                }
                else
                {
                    Debug.LogError($"SHELL missing USS at Resources/{StyleSheetResource}.");
                }
            }

            shellRoot = root.Q<VisualElement>("shell-root");
            titleScreen = root.Q<VisualElement>("screen-title");
            firstRunScreen = root.Q<VisualElement>("screen-firstrun");
            howToPlayScreen = root.Q<VisualElement>("screen-howto");
            codexScreen = root.Q<VisualElement>("screen-codex");
            pauseScreen = root.Q<VisualElement>("screen-pause");
            resultsScreen = root.Q<VisualElement>("screen-results");
            resultsTable = root.Q<VisualElement>("results-table");
            pauseLives = root.Q<Label>("pause-lives");
            pauseGold = root.Q<Label>("pause-gold");
            pauseIncome = root.Q<Label>("pause-income");
            resultsHeadline = root.Q<Label>("results-headline");
            resultsNote = root.Q<Label>("results-note");

            if (shellRoot is null || titleScreen is null || firstRunScreen is null || howToPlayScreen is null
                || codexScreen is null || pauseScreen is null || resultsScreen is null)
            {
                Debug.LogError("SHELL UXML did not contain the expected screen elements.");
                return;
            }

            PaintBackdrops(root);
            WireActions(root);

            // Built here rather than lazily on first open, so a missing element in the codex block
            // is reported at launch alongside every other shell wiring error instead of on the tap
            // that first needs it.
            codex = new CodexScreenView(codexScreen, transform, simulationDriver);
            howToPlay = new HowToPlayScreenView(howToPlayScreen, transform, simulationDriver);

            // The strip's own taps go to whoever subscribes to it (the practice director), not to
            // IShellScreenActions: NEXT and SKIP are steps of a lesson, not session flow.
            CoachStrip = new CoachStripView(root);

            // Neither inset can be resolved until the panel has a size, and the panel is resized
            // whenever the surface changes, so these recompute rather than reading once.
            shellRoot.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                ApplyViewportColumn();
                ApplySafeArea();
            });
            ApplyViewportColumn();
            ApplySafeArea();

            built = true;
        }

        private void WireActions(VisualElement root)
        {
            Wire(root, "title-start", () => actions.StartGame());
            Wire(root, "title-practice", () => actions.StartPractice());

            // HOW TO PLAY is the one title button that is not a session action: it opens a screen
            // of copy, so the view shows it itself and BACK closes it the same way. The codex is
            // different only because the overlay keeps its own PreMatchScreen state for it.
            Wire(root, "title-howto", OpenHowToPlay);
            Wire(root, "howto-back", CloseHowToPlay);

            Wire(root, "firstrun-practice", () => actions.StartPractice());
            Wire(root, "firstrun-skip", () => actions.StartGameNow());

            Wire(root, "title-codex", () => actions.ShowCodex());

            // Not a session action — see IShellScreenActions' remarks and OpenHowToPlay's own
            // comment above: identity has nothing to do with match/session state, so this view
            // handles it directly rather than routing through the overlay.
            signInButton = root.Q<Button>("title-signin");
            Wire(root, "title-signin", OnSignInWithGoogleTapped);

            // Also not a session action, same reasoning as SIGN IN WITH GOOGLE just above — see
            // docs/MULTIPLAYER_ROLLOUT.md's MP-06. Self-contained here rather than routed through
            // IShellScreenActions/LocalSessionFlowOverlay deliberately: that overlay's ActiveShellScreen
            // already polls simulationDriver.HasStarted/IsPaused every frame and switches away from
            // Title automatically once they flip — it does not care WHO called
            // UnitySimulationDriver.Initialize(MatchWireClient), only the resulting state. Routing
            // this through the overlay would mean touching its state machine for no behavioral gain.
            playOnlineButton = root.Q<Button>("title-play-online");
            Wire(root, "title-play-online", OnPlayOnlineTapped);

            Wire(root, "title-settings", () => actions.OpenSettings());
            Wire(root, "title-quit", () => actions.QuitGame());

            // The codex's own PREV/NEXT/tab/chip buttons are wired by CodexScreenView, because they
            // change what that screen shows rather than where the session is. Only BACK is a
            // session action, and so only BACK is wired here.
            Wire(root, "codex-back", () => actions.CloseCodex());

            Wire(root, "pause-resume", () => actions.ResumeMatch());
            Wire(root, "pause-settings", () => actions.OpenSettings());
            Wire(root, "pause-reset", () => actions.ResetToReady());
            Wire(root, "pause-menu", () => actions.ReturnToTitle());

            Wire(root, "results-rematch", () => actions.Rematch());
            Wire(root, "results-settings", () => actions.OpenSettings());
            Wire(root, "results-menu", () => actions.ReturnToTitle());
        }

        private static void Wire(VisualElement root, string name, System.Action handler)
        {
            var button = root.Q<Button>(name);
            if (button == null)
            {
                Debug.LogError($"SHELL missing button '{name}' in the shell document.");
                return;
            }

            button.clicked += handler;
        }

        /// <summary>
        /// Assigns the two generated textures the composition is built on.
        /// </summary>
        /// <remarks>
        /// Generated rather than authored, and rather than expressed in USS, for two separate
        /// reasons. USS in this Unity version has no gradient function, so a smooth field would
        /// otherwise have to be faked with a stack of banded translucent boxes, which shows its
        /// seams on an OLED phone. And an imported PNG would be a second copy of colours that
        /// already live in the branding palette, free to drift; deriving them here cannot.
        /// </remarks>
        private void PaintBackdrops(VisualElement root)
        {
            fieldGradient ??= CreateVerticalGradient(
                new Color(0.020f, 0.031f, 0.071f, 1f),
                new Color(0.075f, 0.098f, 0.184f, 1f));

            // Alpha 0.15, down from the 0.40 this was first authored at. UI Toolkit composites in
            // linear here, so 0.40 of ward violet over near-black measured sRGB (70,58,147) — a
            // purple haze across the top third rather than a bloom behind the wordmark.
            wardGlow ??= CreateRadialGlow(new Color(0.44f, 0.36f, 0.92f, 0.15f));

            foreach (var fieldName in new[] { "title-field", "firstrun-field", "howto-field" })
            {
                var field = root.Q<VisualElement>(fieldName);
                if (field != null)
                {
                    field.style.backgroundImage = new StyleBackground(fieldGradient);
                }
            }

            foreach (var glowName in new[] { "title-glow", "firstrun-glow", "howto-glow", "results-glow" })
            {
                var glow = root.Q<VisualElement>(glowName);
                if (glow != null)
                {
                    glow.style.backgroundImage = new StyleBackground(wardGlow);
                }
            }
        }

        /// <summary>
        /// Pads every screen in by the device's safe-area insets, in panel units.
        /// </summary>
        /// <remarks>
        /// The insets are taken as fractions of the viewport and multiplied by the panel's own
        /// resolved size, rather than converted through the panel scale factor. The scale factor is
        /// derived state that this component would have to recompute and keep in step with the
        /// PanelSettings; the panel's resolved width is the answer it already produced.
        ///
        /// <see cref="MobileViewportLayout"/> is the source rather than <c>Screen.safeArea</c>
        /// directly, so a capture that declares its own surface gets the surface it declared —
        /// exactly as the IMGUI HUD already does.
        ///
        /// Note the resolved size is the portrait column, not the window, since
        /// <see cref="ApplyViewportColumn"/> insets the root first. On a handset the two are the
        /// same and this is exact. On a surface wider than 9:19.5 the column is centred and a
        /// screen-edge cutout falls outside it entirely, so scaling the inset down with the column
        /// errs towards padding that is not needed rather than a cutout that is not cleared.
        /// </remarks>
        /// <summary>
        /// Confines the shell to the same portrait column the board and the IMGUI HUD occupy.
        /// </summary>
        /// <remarks>
        /// A <c>PanelSettings</c> panel always fills the whole window; it has no viewport concept.
        /// Everything else in the game is laid out inside <see cref="MobileViewportLayout.CameraRect"/>,
        /// a centred, full-height column whose width collapses as the window gets wider than the
        /// 9:19.5 target. Left alone the shell is the only surface spanning the full window, so in a
        /// 16:9 Game view the board sits in 26 percent of the width with the menu drawn across all
        /// of it — the two read as different aspect ratios because they are.
        ///
        /// Insetting the root rather than scaling it keeps every length in the USS a reference unit:
        /// the panel still resolves those against the full window, and the reference resolution's
        /// 9:19.5 aspect is what makes the design's width land on the column width. This only moves
        /// the column's edges into place.
        ///
        /// Mirroring <see cref="MobileViewportLayout.CameraRect"/> exactly was itself too strict,
        /// per finding #14 of the 2026-09-01 render review: on a 2064x2752 iPad the board's own
        /// on-screen column is only ~61% of the width, and mirroring it left the whole shell — the
        /// wordmark, the backdrop art, everything — composed inside that same narrow strip with true
        /// black either side, because nothing else draws out there. The board genuinely cannot use
        /// that margin (its camera is locked to a fixed vertical framing, see
        /// <see cref="MobileViewportLayout.CameraRect"/>'s own remarks), but the shell is flat UI
        /// with no live footage behind it to stay aligned with, so it does not need to give up that
        /// margin the same way. This claims back half of it — continuously, the same "give a wide
        /// screen's margin real work instead of leaving it dark" spirit
        /// <see cref="MobileViewportLayout.HasSideRails"/>'s own board-column math already applies to
        /// the HUD's side rails, just expressed here as a plain fraction rather than a rail layout. A
        /// portrait phone still gets exactly 0 (unaffected — this is additive), and the explicit 16%
        /// ceiling below is what keeps an ultra-wide monitor from stretching the column out
        /// unreasonably even though the underlying board math already floors it well short of that on
        /// its own.
        /// </remarks>
        private void ApplyViewportColumn()
        {
            if (shellRoot is null)
            {
                return;
            }

            // Percent rather than pixels on purpose. A pixel inset needs the panel's width, and the
            // nearest thing to hand is the parent, which is the UXML TemplateContainer — that does
            // not stretch to the panel by default, so its resolved width is not dependable. A
            // percentage is resolved against the containing block by the layout engine itself, which
            // needs no width read here and stays correct through a resize.
            var boardInset = Mathf.Clamp01(MobileViewportLayout.CameraRect().xMin);
            var inset = Mathf.Min(boardInset * 0.5f, 0.16f) * 100f;

            // Writing a style that is already set still schedules another geometry pass, and this
            // runs from the geometry callback, so an unguarded assignment loops every frame.
            var current = shellRoot.style.left;
            if (current.keyword == StyleKeyword.Undefined
                && current.value.unit == LengthUnit.Percent
                && Mathf.Approximately(current.value.value, inset))
            {
                return;
            }

            shellRoot.style.left = Length.Percent(inset);
            shellRoot.style.right = Length.Percent(inset);
        }

        private void ApplySafeArea()
        {
            if (shellRoot is null)
            {
                return;
            }

            var width = shellRoot.resolvedStyle.width;
            var height = shellRoot.resolvedStyle.height;
            if (width <= 0f || height <= 0f || float.IsNaN(width) || float.IsNaN(height))
            {
                return;
            }

            var viewportWidth = Mathf.Max(1, MobileViewportLayout.ViewportWidth);
            var viewportHeight = Mathf.Max(1, MobileViewportLayout.ViewportHeight);
            var safeArea = MobileViewportLayout.SafeArea;

            var left = Mathf.Clamp01(safeArea.xMin / viewportWidth) * width;
            var right = Mathf.Clamp01((viewportWidth - safeArea.xMax) / viewportWidth) * width;

            // Screen.safeArea has its origin at the bottom-left; UI Toolkit's padding is top-down.
            var top = Mathf.Clamp01((viewportHeight - safeArea.yMax) / viewportHeight) * height;
            var bottom = Mathf.Clamp01(safeArea.yMin / viewportHeight) * height;

            foreach (var safe in shellRoot.Query<VisualElement>(className: "ltw-safe").ToList())
            {
                safe.style.paddingLeft = left;
                safe.style.paddingRight = right;
                safe.style.paddingTop = top;
                safe.style.paddingBottom = bottom;
            }
        }

        private void RefreshContent(ShellScreen screen)
        {
            switch (screen)
            {
                case ShellScreen.Codex:
                    codex?.Refresh();
                    break;

                case ShellScreen.HowToPlay:
                    howToPlay?.Refresh();
                    break;

                case ShellScreen.Pause:
                    RefreshPauseStats();
                    break;

                case ShellScreen.Results:
                    RefreshResults();
                    break;
            }
        }

        private void RefreshPauseStats()
        {
            if (simulationDriver == null)
            {
                return;
            }

            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            var seat = snapshot.Players.Get(simulationDriver.LocalPlayerId);
            SetText(pauseLives, seat.Lives.Amount.ToString());
            SetText(pauseGold, seat.Gold.Amount.ToString());

            // EffectiveIncome, matching HudView: what the next payout will actually pay, which for
            // an eliminated seat is nothing even though Income still reads what it built.
            SetText(pauseIncome, $"+{seat.EffectiveIncome.Amount}");
        }

        private void RefreshResults()
        {
            if (simulationDriver == null)
            {
                return;
            }

            var summary = simulationDriver.LatestMatchSummary;
            if (summary is null || resultsTable is null)
            {
                return;
            }

            // A completed match's summary never changes, so the table is rebuilt once per result
            // rather than every frame the screen is up.
            if (ReferenceEquals(renderedSummary, summary))
            {
                return;
            }

            renderedSummary = summary;

            var localWon = summary.WinnerId.Equals(simulationDriver.LocalPlayerId);
            SetText(resultsHeadline, localWon ? "VICTORY" : "DEFEAT");
            resultsHeadline?.EnableInClassList("ltw-headline--won", localWon);
            SetText(
                resultsNote,
                localWon
                    ? $"You held the last line at tick {summary.CompletedAtTick.Value}."
                    : $"Seat {summary.WinnerId.Value} took the carousel at tick {summary.CompletedAtTick.Value}.");

            resultsTable.Clear();
            resultsTable.Add(BuildHeaderRow());

            // Sorted by finish rather than by seat number. A results table listing P1..P8 in seat
            // order buries the one thing it exists to say. Seats with no placement (0) sort last,
            // which is what an older summary built without elimination order produces.
            foreach (var player in summary.Players
                .OrderBy(player => player.Placement == 0 ? int.MaxValue : player.Placement)
                .ThenBy(player => player.PlayerId.Value))
            {
                resultsTable.Add(BuildPlayerRow(player, summary.WinnerId.Value));
            }
        }

        private static VisualElement BuildHeaderRow()
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("ltw-row");
            row.AddToClassList("ltw-row--head");
            row.Add(Cell("#", "ltw-cell--rank"));
            row.Add(Cell("SEAT", "ltw-cell--seat"));
            row.Add(Cell("STATE", null));
            row.Add(Cell("LIVES", "ltw-cell--num"));
            row.Add(Cell("INCOME", "ltw-cell--num"));
            row.Add(Cell("GOLD", "ltw-cell--num"));
            return row;
        }

        private VisualElement BuildPlayerRow(PlayerEconomySummary player, int winnerSeat)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("ltw-row");

            var isWinner = player.PlayerId.Value == winnerSeat;
            if (isWinner)
            {
                row.AddToClassList("ltw-row--winner");
            }
            else if (player.PlayerId.Equals(simulationDriver.LocalPlayerId))
            {
                row.AddToClassList("ltw-row--local");
            }

            var seatLabel = player.PlayerId.Equals(simulationDriver.LocalPlayerId)
                ? $"P{player.PlayerId.Value} (YOU)"
                : $"P{player.PlayerId.Value}";

            // The word, not the colour. A seat's fate is spelled out in the row so it survives a
            // greyscale screenshot and a colour-blind player, per docs/BRANDING_GUIDE.md.
            var state = isWinner ? "WON" : player.IsEliminated ? "OUT" : "ALIVE";

            // Every defeated seat used to render as "OUT" and nothing else, so a seven-way loss
            // read as a seven-way tie. The order they fell in was known and thrown away. Ordinals
            // rather than bare numbers because "2nd" cannot be misread as a seat id in a table whose
            // next column is one.
            var placement = player.Placement > 0 ? Ordinal(player.Placement) : "—";

            row.Add(Cell(placement, "ltw-cell--rank"));
            row.Add(Cell(seatLabel, "ltw-cell--seat"));
            row.Add(Cell(state, player.IsEliminated && !isWinner ? "ltw-cell--out" : null));
            row.Add(Cell(player.Lives.Amount.ToString(), "ltw-cell--num"));
            row.Add(Cell($"+{player.Income.Amount}", "ltw-cell--num"));
            row.Add(Cell(player.Gold.Amount.ToString(), "ltw-cell--num"));
            return row;
        }

        /// <summary>1 to 1st, 2 to 2nd, and so on.</summary>
        /// <remarks>
        /// The 11-13 exception is the whole reason this is a method rather than a format string:
        /// 11th, 12th and 13th break the last-digit rule that gives 1st, 2nd and 3rd. Eight seats
        /// never reach it today, and a table that starts printing "11st" the first time the mode
        /// grows is not worth the two lines saved.
        /// </remarks>
        private static string Ordinal(int placement)
        {
            var lastTwo = placement % 100;
            if (lastTwo is >= 11 and <= 13)
            {
                return $"{placement}th";
            }

            return (placement % 10) switch
            {
                1 => $"{placement}st",
                2 => $"{placement}nd",
                3 => $"{placement}rd",
                _ => $"{placement}th"
            };
        }

        private static Label Cell(string text, string? modifier)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ltw-cell");
            if (!string.IsNullOrEmpty(modifier))
            {
                label.AddToClassList(modifier);
            }

            return label;
        }

        private static void SetText(Label? label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static Texture2D CreateVerticalGradient(Color top, Color bottom)
        {
            const int height = 256;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                name = "LTW Shell Field",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[height];
            for (var y = 0; y < height; y++)
            {
                // Row 0 is the bottom of a Unity texture, and the element paints it upright, so the
                // gradient is written bottom-up to land dark at the top of the screen.
                var t = y / (height - 1f);
                pixels[y] = Color.Lerp(bottom, top, t);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateRadialGlow(Color inner)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "LTW Shell Glow",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            var centre = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - centre) / centre;
                    var dy = (y - centre) / centre;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    // Smoothstep rather than a linear ramp: a linear falloff leaves a visible ring
                    // where the alpha derivative jumps at the edge of the blob.
                    var falloff = Mathf.Clamp01(1f - distance);
                    falloff *= falloff * (3f - 2f * falloff);
                    pixels[(y * size) + x] = new Color(inner.r, inner.g, inner.b, inner.a * falloff);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
