#nullable enable

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

        void ShowHowToPlay();

        void OpenSettings();

        void QuitGame();

        void ResumeMatch();

        void ResetToReady();

        void ReturnToTitle();

        void Rematch();
    }

    /// <summary>
    /// The title, pause and results screens, built in UI Toolkit.
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
        private VisualElement? pauseScreen;
        private VisualElement? resultsScreen;
        private VisualElement? resultsTable;
        private Label? pauseLives;
        private Label? pauseGold;
        private Label? pauseIncome;
        private Label? resultsHeadline;
        private Label? resultsNote;

        private Texture2D? fieldGradient;
        private Texture2D? wardGlow;

        private ShellScreen currentScreen = ShellScreen.None;

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

            if (screen != currentScreen)
            {
                Hide(titleScreen);
                Hide(pauseScreen);
                Hide(resultsScreen);
                currentScreen = screen;
                renderedSummary = null;
                Enter(ScreenElement(screen));
            }

            RefreshContent(screen);
        }

        private void OnDisable()
        {
            // Leaves the panel empty rather than frozen on whatever screen was last up.
            currentScreen = ShellScreen.None;
            Hide(titleScreen);
            Hide(pauseScreen);
            Hide(resultsScreen);
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
            ShellScreen.Pause => pauseScreen,
            ShellScreen.Results => resultsScreen,
            _ => null
        };

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
            pauseScreen = root.Q<VisualElement>("screen-pause");
            resultsScreen = root.Q<VisualElement>("screen-results");
            resultsTable = root.Q<VisualElement>("results-table");
            pauseLives = root.Q<Label>("pause-lives");
            pauseGold = root.Q<Label>("pause-gold");
            pauseIncome = root.Q<Label>("pause-income");
            resultsHeadline = root.Q<Label>("results-headline");
            resultsNote = root.Q<Label>("results-note");

            if (shellRoot is null || titleScreen is null || pauseScreen is null || resultsScreen is null)
            {
                Debug.LogError("SHELL UXML did not contain the expected screen elements.");
                return;
            }

            PaintBackdrops(root);
            WireActions(root);

            // Safe-area insets cannot be resolved until the panel has a size, and the panel is
            // resized whenever the surface changes, so this recomputes rather than reading once.
            shellRoot.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
            ApplySafeArea();

            built = true;
        }

        private void WireActions(VisualElement root)
        {
            Wire(root, "title-start", () => actions.StartGame());
            Wire(root, "title-howto", () => actions.ShowHowToPlay());
            Wire(root, "title-settings", () => actions.OpenSettings());
            Wire(root, "title-quit", () => actions.QuitGame());

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

            var titleField = root.Q<VisualElement>("title-field");
            if (titleField != null)
            {
                titleField.style.backgroundImage = new StyleBackground(fieldGradient);
            }

            foreach (var glowName in new[] { "title-glow", "results-glow" })
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
        /// </remarks>
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
            foreach (var player in summary.Players)
            {
                resultsTable.Add(BuildPlayerRow(player, summary.WinnerId.Value));
            }
        }

        private static VisualElement BuildHeaderRow()
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("ltw-row");
            row.AddToClassList("ltw-row--head");
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

            row.Add(Cell(seatLabel, "ltw-cell--seat"));
            row.Add(Cell(state, player.IsEliminated && !isWinner ? "ltw-cell--out" : null));
            row.Add(Cell(player.Lives.Amount.ToString(), "ltw-cell--num"));
            row.Add(Cell($"+{player.Income.Amount}", "ltw-cell--num"));
            row.Add(Cell(player.Gold.Amount.ToString(), "ltw-cell--num"));
            return row;
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
