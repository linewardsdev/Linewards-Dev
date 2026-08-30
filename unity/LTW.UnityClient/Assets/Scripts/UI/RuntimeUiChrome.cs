#nullable enable

using LTW.Simulation.Bridge;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    internal enum CommandCardState
    {
        Normal,
        Selected,
        Disabled,
        Error
    }

    internal static class RuntimeUiChrome
    {
        private static readonly Color CardBack = new(0.038f, 0.052f, 0.078f, 0.96f);
        private static readonly Color CardInset = new(0.075f, 0.093f, 0.122f, 0.94f);
        private static readonly Color SlateEdge = new(0.34f, 0.36f, 0.38f, 0.92f);
        private static readonly Color DeepEdge = new(0.012f, 0.016f, 0.022f, 0.92f);
        private static readonly Color DisabledEdge = new(0.24f, 0.25f, 0.28f, 0.84f);
        private static readonly Color ErrorRed = new(0.94f, 0.24f, 0.18f, 1f);
        private static Texture2D? generatedPanelButton;
        private static Texture2D? generatedRoundButton;
        private static Texture2D? generatedPanel;
        private static Texture2D? generatedPanelShadow;
        private static GUIStyle? panelStyle;
        private static GUIStyle? panelShadowStyle;

        /// <summary>Corner chamfer of the shared panel, in texture pixels.</summary>
        /// <remarks>
        /// Larger than the buttons' 12 because a panel is bigger and the cut has to stay readable
        /// at the same physical size. The 9-slice border below must exceed it or the corner gets
        /// stretched and the chamfer turns into a curve at one end and a point at the other.
        /// </remarks>
        private const int PanelChamfer = 16;

        /// <summary>9-slice border for the panel texture. Must exceed <see cref="PanelChamfer"/>.</summary>
        private const int PanelBorder = 20;

        private const int PanelTile = 64;

        /// <summary>
        /// Draws the shared panel background: chamfered, bevelled, with a soft drop shadow.
        /// </summary>
        /// <remarks>
        /// Every panel in the HUD used to draw its own background, and they had drifted into two
        /// different looks, neither of which was ours:
        ///
        /// - Three of them (`SendDockController`, `TouchPlacementController`,
        ///   `PlacementFeedbackView`) built a `GUIStyle` from `GUI.skin.box` and only overrode
        ///   border and padding, so the actual background was **Unity's built-in editor-skin box**,
        ///   tinted navy. Tinting the engine default does not stop it looking like the engine
        ///   default; it is one of the fastest "unfinished" reads in a game.
        /// - The fourth (`LocalSessionFlowOverlay`) drew a flat rect plus four 1px hairline edges.
        ///   Hard corners, no bevel, no depth.
        ///
        /// So panels disagreed with each other AND with the buttons, which have had a chamfered
        /// bevel this whole time. This unifies them on the buttons' language, which is the one that
        /// was already deliberate.
        ///
        /// The drop shadow matters more than the chamfer. These panels sit over a dark board, and
        /// with no shadow a dark panel on a dark board has nothing separating them except a hairline
        /// — the panel reads as a hole rather than as something on top.
        /// </remarks>
        public static void DrawPanel(Rect rect, Color tint, float scale)
        {
            EnsurePanelStyles();

            var priorColor = GUI.color;

            // Offset down and out, so the shadow reads as cast rather than as a second border.
            var spread = Mathf.Max(2f, 5f * scale);
            var shadowRect = new Rect(rect.x - spread, rect.y - spread * 0.5f, rect.width + spread * 2f, rect.height + spread * 2f);
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.Box(shadowRect, GUIContent.none, panelShadowStyle);

            GUI.color = tint;
            GUI.Box(rect, GUIContent.none, panelStyle);

            GUI.color = priorColor;
        }

        /// <summary>
        /// Whether a session-flow screen currently owns the display, so the HUD must stand down.
        /// </summary>
        /// <remarks>
        /// Set once per frame from <c>LocalSessionFlowOverlay.Update</c> and read by every HUD
        /// component's <c>OnGUI</c>. Unity runs all Update calls before any OnGUI, so this is
        /// stable for the whole GUI pass regardless of component order.
        ///
        /// It is a state flag rather than a drawn scrim because a scrim CANNOT block input here.
        /// IMGUI dispatches an event to components in draw order, and the first control under the
        /// cursor consumes it — so a full-screen button drawn last blocks only what is drawn after
        /// it, which is nothing. The overlay draws last, which is right for painting over the HUD
        /// and exactly wrong for intercepting its clicks. Asking each component not to draw is the
        /// only ordering-independent answer.
        /// </remarks>
        public static bool ModalScreenActive { get; set; }

        /// <summary>
        /// The build drawer's contribution in GUI pixels, owned by TouchPlacementController.
        /// </summary>
        /// <remarks>
        /// Published rather than queried for the same reason <see cref="ModalScreenActive"/> is: the
        /// board camera has to know about a HUD panel it cannot see, and the alternative is the
        /// renderer reaching into two UI controllers to ask.
        ///
        /// Set from OnGUI, so the camera acts on it one frame later. That lag is deliberate and
        /// harmless — a drawer opens on a tap, and a single frame of delay is invisible, whereas
        /// hoisting the panel's own expanded/collapsed state into Update to avoid it would put the
        /// layout in two places.
        /// </remarks>
        public static float BuildDockInset { get; set; }

        /// <summary>The send drawer's contribution, owned by SendDockController.</summary>
        public static float SendDockInset { get; set; }

        /// <summary>Whichever open drawer covers the most, or 0 when none is.</summary>
        /// <remarks>
        /// A field per drawer rather than one shared number. Sharing it meant whichever drew last
        /// won, so the two had to know about each other to avoid clobbering — and each had to
        /// remember to clear it, from inside an OnGUI with four early returns above the clearing
        /// line. Picking a tower took one of those returns and stranded the inset, leaving the board
        /// permanently short. Owning a field each removes the coordination entirely.
        /// </remarks>
        public static float BottomDockInset => Mathf.Max(BuildDockInset, SendDockInset);

        /// <summary>
        /// Dims everything behind a modal panel.
        /// </summary>
        /// <remarks>
        /// Call immediately BEFORE drawing a panel that owns the screen. This is the visual half of
        /// modality; <see cref="ModalScreenActive"/> is the input half, and both are needed.
        ///
        /// Captured on 2026-07-31: the pre-match title panel rendered over a fully drawn, fully
        /// interactive build palette — two panels overlapping, neither dimmed, and the one
        /// underneath still taking input. A modal that was modal in neither sense.
        ///
        /// Alpha is 0.45, down from the 0.72 this shipped with for a few hours. That first value was
        /// picked against a bright editor viewport and was far too strong for the actual game: this
        /// board is already dark navy, so 0.72 of near-black on top of it measured a mean luminance
        /// of 0.164 on a real device screenshot — the board stopped reading as a backdrop and became
        /// a black void, and the first report from playing it was "the game is very dark".
        ///
        /// A scrim over a dark scene needs far less alpha than one over a light scene to achieve the
        /// same separation, which is the thing that is easy to get wrong when tuning against an
        /// editor window rather than the shipped frame.
        /// </remarks>
        public static void DrawModalScrim()
        {
            var priorColor = GUI.color;
            GUI.color = new Color(0.004f, 0.008f, 0.016f, 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = priorColor;
        }

        private static void EnsurePanelStyles()
        {
            if (panelStyle != null && panelShadowStyle != null && generatedPanel != null && generatedPanelShadow != null)
            {
                return;
            }

            panelStyle = new GUIStyle
            {
                normal = { background = GeneratedPanel() },
                border = new RectOffset(PanelBorder, PanelBorder, PanelBorder, PanelBorder),
                margin = ZeroOffset(),
                padding = ZeroOffset(),
            };

            panelShadowStyle = new GUIStyle
            {
                normal = { background = GeneratedPanelShadow() },
                border = new RectOffset(PanelBorder, PanelBorder, PanelBorder, PanelBorder),
                margin = ZeroOffset(),
                padding = ZeroOffset(),
            };
        }

        /// <summary>Chamfered panel fill with a bevelled rim, generated rather than authored.</summary>
        /// <remarks>
        /// Generated for the same reason the button textures are: it is two colours and a corner
        /// rule, and an imported PNG would be a binary asset that no diff can review and that has
        /// to be kept in sync with the colours above by hand.
        /// </remarks>
        private static Texture2D GeneratedPanel()
        {
            if (generatedPanel != null)
            {
                return generatedPanel;
            }

            var texture = NewChromeTexture("LTW Panel");
            var inner = new Color(0.026f, 0.036f, 0.055f, 0.97f);
            // Deliberately dimmer than the buttons' bevel. A panel is a surface, not an affordance;
            // matching the button rim exactly made every panel read as a giant button.
            var bevel = new Color(0.20f, 0.23f, 0.27f, 0.90f);
            var inset = new Color(0.055f, 0.070f, 0.095f, 0.95f);

            for (var y = 0; y < PanelTile; y++)
            {
                for (var x = 0; x < PanelTile; x++)
                {
                    if (OutsideChamfer(x, y, PanelChamfer))
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var depth = EdgeDepth(x, y, PanelChamfer);
                    var color = depth switch
                    {
                        <= 1 => bevel,
                        <= 3 => inset,
                        _ => inner,
                    };

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply(false, true);
            generatedPanel = texture;
            return generatedPanel;
        }

        /// <summary>Soft chamfered blob used as the panel's cast shadow.</summary>
        private static Texture2D GeneratedPanelShadow()
        {
            if (generatedPanelShadow != null)
            {
                return generatedPanelShadow;
            }

            var texture = NewChromeTexture("LTW Panel Shadow");
            const int falloff = 10;

            for (var y = 0; y < PanelTile; y++)
            {
                for (var x = 0; x < PanelTile; x++)
                {
                    if (OutsideChamfer(x, y, PanelChamfer))
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // Ramp in over `falloff` pixels so the shadow has no visible edge of its own.
                    var depth = EdgeDepth(x, y, PanelChamfer);
                    var alpha = Mathf.Clamp01(depth / (float)falloff);
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            texture.Apply(false, true);
            generatedPanelShadow = texture;
            return generatedPanelShadow;
        }

        private static Texture2D NewChromeTexture(string name) => new(PanelTile, PanelTile, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        /// <summary>Whether a texel falls outside the chamfered corner, matching the button rule.</summary>
        private static bool OutsideChamfer(int x, int y, int cut) =>
            x + y < cut ||
            PanelTile - 1 - x + y < cut ||
            x + PanelTile - 1 - y < cut ||
            PanelTile - 1 - x + PanelTile - 1 - y < cut;

        /// <summary>How many texels a point sits inside the chamfered outline.</summary>
        private static int EdgeDepth(int x, int y, int cut) => Mathf.Min(
            Mathf.Min(x, PanelTile - 1 - x),
            Mathf.Min(
                Mathf.Min(y, PanelTile - 1 - y),
                Mathf.Min(
                    Mathf.Min(x + y - cut, PanelTile - 1 - x + y - cut),
                    Mathf.Min(x + PanelTile - 1 - y - cut, PanelTile - 1 - x + PanelTile - 1 - y - cut))));

        /// <summary>
        /// Draws a command card and returns whether it was pressed.
        /// </summary>
        /// <remarks>
        /// <paramref name="hitRect"/> narrows the clickable region without changing what is drawn.
        /// A card that carries its own button — a category card with a tier upgrade on it — MUST
        /// pass one, because this method's GUI.Button consumes the click for the whole card.
        /// GUI.Button calls Event.Use() on both MouseDown and MouseUp, so any button drawn
        /// afterwards inside the same rect never sees the event at all and silently does nothing.
        /// That is not a z-order problem a later draw call can win; the event is already gone.
        /// </remarks>
        public static bool DrawCommandCard(Rect rect, Color accent, CommandCardState state, float scale, Rect? hitRect = null)
        {
            DrawCommandCardChrome(rect, accent, state, scale);

            var previousEnabled = GUI.enabled;
            GUI.enabled = state is not CommandCardState.Disabled and not CommandCardState.Error;
            var pressed = GUI.Button(hitRect ?? rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = previousEnabled;
            return pressed;
        }

        // Action-row geometry, in unscaled units, measured up from the card's bottom edge. The card
        // now carries TWO buttons stacked above its bottom margin — the batch row above the tier row —
        // and all three rects derive from these numbers. If they drift, the card's own button either
        // steals a button's clicks or leaves a dead strip that selects nothing.
        private const float CategoryTierRowBottomInset = 26f;

        // 26 rather than 20, raised with the type inside them. Reported from iPad play as an
        // upgrade control that is hard to read and too small, and the measurement bears it out:
        // these rows carried 8pt and 9pt text, the smallest in the game, on the one control that
        // answers "can I upgrade this, and what does it cost". Everything else a player reads at a
        // glance — send card labels, the HUD readout — sits at 9 to 11.
        //
        // The row grows rather than the text shrinking to fit, which is the trap this has already
        // fallen into once: 8pt exists only because "NEED +140" did not fit at 9.
        private const float CategoryTierRowHeight = 26f;
        private const float CategoryBatchRowGap = 3f;
        private const float CategoryBatchRowHeight = 26f;
        private const float CategoryBatchRowBottomInset =
            CategoryTierRowBottomInset + CategoryTierRowHeight + CategoryBatchRowGap;

        /// <summary>
        /// Where the tier row sits on a category card.
        /// </summary>
        /// <remarks>
        /// The row's two halves are sized against 11pt type, and the CARD was widened to hold them
        /// — the drawers went from 430 to 520 units wide and 282 to 330 tall. Both halves at a
        /// readable size genuinely did not fit the old card: raising the type clipped the button to
        /// "IEED +7(", and widening the button then wrapped "TIER 1" onto two lines. That is the
        /// card being too small, not the split being wrong, and shrinking the text is the move this
        /// control has already made once — 8pt existed only because "NEED +140" would not fit at 9.
        /// </remarks>
        public static Rect CategoryTierRowRect(Rect card, float scale) => new(
            card.x + 7f * scale,
            card.yMax - (CategoryTierRowBottomInset + CategoryTierRowHeight) * scale,
            card.width - 14f * scale,
            CategoryTierRowHeight * scale);

        /// <summary>Where the whole-line upgrade row sits: directly above the tier row.</summary>
        public static Rect CategoryBatchRowRect(Rect card, float scale) => new(
            card.x + 7f * scale,
            card.yMax - (CategoryBatchRowBottomInset + CategoryBatchRowHeight) * scale,
            card.width - 14f * scale,
            CategoryBatchRowHeight * scale);

        /// <summary>
        /// The part of a category card that selects the category: everything above its action rows.
        /// </summary>
        /// <remarks>
        /// Derived from the HIGHEST row rather than from the tier row directly. When the batch row
        /// was added above it, a select rect still measured against the tier row would have covered
        /// the new button — and DrawCommandCard's GUI.Button calls Event.Use() on both MouseDown and
        /// MouseUp, so the batch button would never have seen a single click. That is not a z-order
        /// problem a later draw call can win; the event is already gone.
        /// </remarks>
        public static Rect CategoryCardSelectRect(Rect card, float scale)
        {
            var row = CategoryBatchRowRect(card, scale);
            return new Rect(card.x, card.y, card.width, Mathf.Max(1f, row.y - card.y));
        }

        public static Rect CommandCardIconRect(Rect rect, float scale)
        {
            var size = Mathf.Min(52f * scale, rect.height - 28f * scale);
            return new Rect(rect.x + (rect.width - size) * 0.5f, rect.y + 8f * scale, size, size);
        }

        public static Rect CommandCardLabelRect(Rect rect, float scale)
        {
            return new Rect(rect.x + 7f * scale, rect.yMax - 32f * scale, rect.width - 14f * scale, 15f * scale);
        }

        /// <summary>
        /// The aspect (width / height) the command card art is authored at.
        /// </summary>
        /// <remarks>
        /// ui_command_card_*_option_04 are 192x232 — noticeably TALLER than they are wide. The art
        /// is drawn with ScaleMode.StretchToFill, so a card whose rect does not roughly match this
        /// does not crop, it distorts: the frame's corner returns, its piston and its gold boss all
        /// stretch with the rect. The category pickers used to lay three cards out as full-width
        /// rows about 800x104, an aspect of 7.7 against the authored 0.83, and the art was visibly
        /// smeared across every one of them.
        /// </remarks>
        public const float CommandCardArtAspect = 192f / 232f;

        /// <summary>
        /// Draws the tier row on a category picker card: the tier it is at, and an upgrade button.
        /// </summary>
        /// <remarks>
        /// Occupies its own strip carved out of the card's growth rather than borrowing space from
        /// the existing stack. That stack has no room to lend: CommandCardMetaRect runs to
        /// yMax - 5*scale and the accent strip starts at yMax - 4*scale, leaving a single scaled
        /// pixel between them, and a previous attempt to put a second line of text through there
        /// drew it straight across the cost digits.
        ///
        /// Returns true when the upgrade was pressed. At max tier the button is replaced by a
        /// static "MAX" so the card never offers a purchase that would be rejected, and when the
        /// player cannot afford it the button uses the same Disabled state the send and build cards
        /// already use for unaffordable, so "cannot buy this" reads identically everywhere.
        /// </remarks>
        /// <summary>
        /// The whole-line upgrade button on a category card.
        /// </summary>
        /// <remarks>
        /// Labels what the tap will ACTUALLY do, not what the player might wish it did. When gold
        /// covers everything it reads "RAISE 5 · 84G"; when it does not it reads "RAISE 3/5 · 52G",
        /// because a button offering five and silently delivering three is the shape of partial
        /// result that reads as a bug rather than as a budget. With nothing eligible it says so
        /// instead of disappearing — a control that vanishes when it cannot act teaches the player
        /// nothing about why.
        /// </remarks>
        public static bool DrawCategoryBatchRow(
            Rect card,
            BatchUpgradeQuote quote,
            Color accent,
            float scale,
            GUIStyle labelStyle,
            GUIStyle buttonStyle)
        {
            var row = CategoryBatchRowRect(card, scale);

            if (!quote.HasWork)
            {
                labelStyle.fontSize = Mathf.RoundToInt(9f * scale);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.normal.textColor = new Color(accent.r, accent.g, accent.b, 0.5f);
                GUI.Label(row, "ALL AT TIER", labelStyle);
                labelStyle.alignment = TextAnchor.MiddleLeft;
                return false;
            }

            var label = quote.IsGoldLimited
                ? $"RAISE {quote.Affordable}/{quote.Eligible}  {quote.AffordableCost}G"
                : $"RAISE {quote.Eligible}  {quote.TotalCost}G";

            // No GUI.enabled guard: DrawPanelButton rolls its own MouseUp check and ignores it
            // entirely, so setting it would grey the button without actually blocking the click.
            // Letting an unaffordable press through is also the behaviour this panel already
            // settled on — the caller answers "not enough gold" out loud, which teaches more than a
            // dead control that silently swallows the tap.
            buttonStyle.fontSize = Mathf.RoundToInt(11f * scale);
            return DrawPanelButton(row, label, quote.Affordable > 0 ? accent : DisabledEdge, scale, buttonStyle);
        }

        public static bool DrawCategoryTierRow(
            Rect card,
            int tier,
            int maxTier,
            int upgradeCost,
            bool canAfford,
            Color accent,
            float scale,
            GUIStyle tierStyle,
            GUIStyle buttonStyle,
            int requiredIncome = 0,
            int currentIncome = 0)
        {
            var row = CategoryTierRowRect(card, scale);

            tierStyle.fontSize = Mathf.RoundToInt(11f * scale);
            tierStyle.alignment = TextAnchor.MiddleLeft;
            tierStyle.normal.textColor = accent;
            GUI.Label(new Rect(row.x, row.y, row.width * 0.40f, row.height), $"TIER {tier}", tierStyle);

            // The button takes two thirds of the row, not a little over half. At the old split the
            // longer label clipped to "IEED +7(" as soon as the type was raised — the split was
            // sized around 8pt text, so it had to move with it. "TIER 3" is six characters and
            // fixed; the button carries up to "NEED +675", which is nine.
            //
            // This clipped again on a tablet rail (2026-08-29), at both the send and build category
            // pickers — not from this split being wrong, but from both rail cards being narrower
            // (264 units at three columns) than the width this split was tuned against (321-520,
            // the drawer's own range at a comparable scale). Narrowing the tier LABEL further to
            // buy the button more room wrapped "TIER 1" onto two lines at that same 264 instead —
            // trading one clipped string for another. Fixed at the actual source in both callers
            // (send and tower category pickers now use 2 rail columns, not 3, landing at 404 units)
            // rather than here, so this split stays the one number both card widths already agree
            // with.
            var buttonRect = new Rect(row.x + row.width * 0.42f, row.y, row.width * 0.58f, row.height);
            if (tier >= maxTier)
            {
                tierStyle.alignment = TextAnchor.MiddleRight;
                tierStyle.normal.textColor = new Color(accent.r, accent.g, accent.b, 0.65f);
                GUI.Label(buttonRect, "MAX", tierStyle);
                tierStyle.alignment = TextAnchor.MiddleLeft;
                return false;
            }

            // Income is named BEFORE gold, because the two are fixed by opposite actions and only
            // one of them is fixed by waiting. "UP 140G" on a card that will be refused for income
            // tells a player to keep banking, which is the one thing that cannot help — income only
            // rises by sending, and sending spends the gold they were told to save.
            var incomeShort = currentIncome < requiredIncome;
            var label = incomeShort ? $"NEED +{requiredIncome}" : $"UP {upgradeCost}G";
            var enabled = canAfford && !incomeShort;

            var previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            // Both states at the same size now. The income-short label was a point smaller purely
            // because it is the longer string, which made the hardest-to-satisfy state the hardest
            // to read — exactly backwards, since that is the one a player needs to act on.
            buttonStyle.fontSize = Mathf.RoundToInt(11f * scale);
            var pressed = DrawPanelButton(buttonRect, label, enabled ? accent : DisabledEdge, scale, buttonStyle);
            GUI.enabled = previousEnabled;
            buttonStyle.fontSize = Mathf.RoundToInt(11f * scale);
            return pressed;
        }

        /// <summary>
        /// Rect for one card in a category picker: a centred ROW of cards, each sized to
        /// <see cref="CommandCardArtAspect"/>.
        /// </summary>
        /// <remarks>
        /// Shared by the send dock and the build palette because card geometry has now been got wrong
        /// three times, in both of them, the same way. First a FIXED height: at two categories the
        /// cards fitted, a third pushed the last one's bottom edge to 352 inside a 282-tall panel, so
        /// it hung over the board with its lower half off-screen. Then a derived height, which fixed
        /// the overflow but stacked three full-width rows — geometrically safe and visually wrong,
        /// because it stretched portrait art across a 7.7 aspect.
        ///
        /// Both failures came from treating the card as a box to fill rather than as a frame with an
        /// aspect of its own. Deriving BOTH dimensions from the art removes that whole class: adding a
        /// category narrows the row, and a panel too short to hold it narrows the cards further, but
        /// nothing here can produce a distorted card or one that escapes its panel.
        ///
        /// <paramref name="maxHeight"/> is the caller's job to compute, not this method's: a
        /// multi-row wrap (see <see cref="CategoryPickerRowMaxHeight"/>) must split the panel's
        /// height EVENLY across every row before drawing any card, or row 0 sizes itself as if it
        /// owned the whole panel and row 1 is left with whatever is left over — reported live
        /// 2026-08-29 as a GROVE/ELITE card in the trailing row rendering tiny with its text lines
        /// overlapping, because that row's own leftover budget was a fraction of a real card's
        /// height while its label/meta text still drew at the normal, unscaled font size.
        /// </remarks>
        public static Rect CategoryCardRect(
            Rect panel,
            float contentTop,
            float gap,
            int index,
            int cardCount,
            float scale,
            float maxHeight)
        {
            if (cardCount <= 0)
            {
                return Rect.zero;
            }

            var sideMargin = 12f * scale;
            var available = panel.width - sideMargin * 2f - gap * (cardCount - 1);
            var width = Mathf.Max(1f, available / cardCount);
            var height = width / CommandCardArtAspect;

            // If the panel is too short for that, the card gives up WIDTH to keep its aspect rather
            // than being squashed. A squashed card is the exact failure this exists to prevent, and
            // silently flattening one to fit would reintroduce it by a different route.
            if (height > maxHeight)
            {
                height = Mathf.Max(1f, maxHeight);
                width = height * CommandCardArtAspect;
            }

            // Centred as a row, so narrowing to preserve aspect stays symmetrical instead of
            // leaving the cards hard against the panel's left edge.
            var rowWidth = width * cardCount + gap * (cardCount - 1);
            var x = panel.x + (panel.width - rowWidth) * 0.5f + index * (width + gap);
            return new Rect(x, contentTop, width, height);
        }

        /// <summary>
        /// The height budget every row in a wrapped category picker must share, so
        /// <see cref="CategoryCardRect"/> clamps every row to the same value rather than letting an
        /// early row spend space a later one needs. Callers compute this once before their loop and
        /// pass the result into every <see cref="CategoryCardRect"/> call for that grid.
        /// </summary>
        public static float CategoryPickerRowMaxHeight(Rect panel, float contentTop, float gap, int rowCount, float scale)
        {
            if (rowCount <= 0)
            {
                return 1f;
            }

            var totalAvailable = panel.yMax - 12f * scale - contentTop - gap * (rowCount - 1);
            return Mathf.Max(1f, totalAvailable / rowCount);
        }

        public static Rect CommandCardMetaRect(Rect rect, float scale)
        {
            return new Rect(rect.x + 7f * scale, rect.yMax - 17f * scale, rect.width - 14f * scale, 12f * scale);
        }

        public static bool DrawControlButton(Rect rect, string label, Color accent, bool active, float scale, GUIStyle labelStyle)
        {
            DrawControlChrome(rect, accent, active, scale);

            DrawChromeLabel(rect, label, active ? new Color(0.95f, 1f, 0.98f, 1f) : new Color(accent.r, accent.g, accent.b, 0.92f), labelStyle);

            return TransparentButton(rect);
        }

        public static bool DrawLauncherButton(Rect rect, string label, Color accent, float scale, GUIStyle labelStyle)
        {
            var tint = new Color(
                Mathf.Lerp(0.78f, accent.r, 0.18f),
                Mathf.Lerp(0.86f, accent.g, 0.18f),
                Mathf.Lerp(1f, accent.b, 0.18f),
                0.96f);
            if (!RuntimeUiArtLibrary.DrawChromeTexture(rect, "ui_panel_button_option_04_v03", tint, ScaleMode.StretchToFill))
            {
                DrawGeneratedPanelButton(rect, accent, scale);
            }
            DrawButtonAccent(rect, accent, scale);

            DrawChromeLabel(rect, label, new Color(accent.r, accent.g, accent.b, 0.96f), labelStyle);

            return TransparentButton(rect);
        }

        public static bool DrawPanelButton(Rect rect, string label, Color accent, float scale, GUIStyle labelStyle)
        {
            if (!RuntimeUiArtLibrary.DrawChromeTexture(rect, "ui_panel_button_option_04_v03", Color.white))
            {
                DrawGeneratedPanelButton(rect, accent, scale);
            }
            DrawButtonAccent(rect, accent, scale);

            DrawChromeLabel(rect, label, new Color(accent.r, accent.g, accent.b, 0.96f), labelStyle);

            return TransparentButton(rect);
        }

        private static void DrawChromeLabel(Rect rect, string label, Color color, GUIStyle sourceStyle)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = sourceStyle.alignment,
                clipping = sourceStyle.clipping,
                font = sourceStyle.font,
                fontSize = sourceStyle.fontSize,
                fontStyle = sourceStyle.fontStyle,
                margin = ZeroOffset(),
                padding = ZeroOffset(),
                wordWrap = sourceStyle.wordWrap
            };
            style.normal.textColor = color;
            GUI.Label(rect, label, style);
        }

        private static bool TransparentButton(Rect rect)
        {
            var currentEvent = Event.current;
            if (currentEvent.type != EventType.MouseUp || !rect.Contains(currentEvent.mousePosition))
            {
                return false;
            }

            currentEvent.Use();
            return true;
        }

        private static void DrawCommandCardChrome(Rect rect, Color accent, CommandCardState state, float scale)
        {
            var stateAccent = StateAccent(accent, state);
            if (RuntimeUiArtLibrary.DrawChromeTexture(rect, CommandCardTextureName(state), state == CommandCardState.Disabled ? new Color(0.78f, 0.82f, 0.9f, 0.72f) : Color.white))
            {
                var referenceIconRect = CommandCardIconRect(rect, scale);
                var referenceIconWell = Shrink(referenceIconRect, -4f * scale);
                Fill(referenceIconWell, new Color(0.006f, 0.01f, 0.016f, 0.42f));

                if (state == CommandCardState.Selected)
                {
                    DrawOutline(Shrink(rect, 2f * scale), new Color(stateAccent.r, stateAccent.g, stateAccent.b, 0.78f), Mathf.Max(2f, 2f * scale));
                }
                else if (state == CommandCardState.Disabled)
                {
                    Fill(Shrink(rect, 5f * scale), new Color(0f, 0f, 0f, 0.32f));
                }

                // Sits below the meta text, not through it: CommandCardMetaRect runs to
                // yMax - 5*scale, so a strip starting at yMax - 9 covered the bottom of the cost
                // digits ("5G +1" read as half-height glyphs behind a coloured bar).
                // The card art has a nameplate behind the label row but nothing behind the cost
                // row, so the cost text landed straight on pale stone and washed out. The
                // procedural path below never had this problem because CardInset is already dark.
                var referenceMetaPlate = Shrink(CommandCardMetaRect(rect, scale), -2f * scale);
                Fill(referenceMetaPlate, new Color(0.02f, 0.03f, 0.045f, state == CommandCardState.Disabled ? 0.42f : 0.62f));

                var referenceCostStrip = new Rect(rect.x + rect.width * 0.2f, rect.yMax - 4f * scale, rect.width * 0.6f, 2f * scale);
                Fill(referenceCostStrip, new Color(stateAccent.r, stateAccent.g, stateAccent.b, state == CommandCardState.Disabled ? 0.34f : 0.78f));
                return;
            }

            var edge = state == CommandCardState.Disabled ? DisabledEdge : SlateEdge;
            var fill = state == CommandCardState.Disabled
                ? new Color(0.07f, 0.078f, 0.094f, 0.9f)
                : Tint(CardBack, stateAccent, state == CommandCardState.Selected ? 0.16f : 0.07f);

            Fill(rect, DeepEdge);
            Fill(Shrink(rect, 2f * scale), edge);
            Fill(Shrink(rect, 4f * scale), fill);

            var inset = new Rect(rect.x + 7f * scale, rect.y + 7f * scale, rect.width - 14f * scale, rect.height - 14f * scale);
            Fill(inset, CardInset);
            DrawMetalRails(rect, edge, scale);
            DrawCornerHardware(rect, stateAccent, scale);

            var iconRect = CommandCardIconRect(rect, scale);
            var iconWell = Shrink(iconRect, -5f * scale);
            Fill(iconWell, new Color(0.012f, 0.018f, 0.026f, 0.82f));
            DrawOutline(iconWell, new Color(stateAccent.r, stateAccent.g, stateAccent.b, 0.58f), Mathf.Max(1f, 1f * scale));

            // Kept clear of CommandCardMetaRect, which runs to yMax - 5*scale; this strip used to
            // start at yMax - 8 and so was drawn across the lower half of the cost/income text.
            var costStrip = new Rect(rect.x + rect.width * 0.18f, rect.yMax - 4f * scale, rect.width * 0.64f, 2f * scale);
            Fill(costStrip, new Color(stateAccent.r, stateAccent.g, stateAccent.b, state == CommandCardState.Disabled ? 0.42f : 0.92f));

            if (state == CommandCardState.Selected)
            {
                DrawOutline(Shrink(rect, 1f * scale), new Color(stateAccent.r, stateAccent.g, stateAccent.b, 0.92f), Mathf.Max(2f, 2f * scale));
                Fill(new Rect(rect.x + 11f * scale, rect.y + 4f * scale, rect.width - 22f * scale, 2f * scale), stateAccent);
                Fill(new Rect(rect.x + rect.width * 0.18f, rect.y + 9f * scale, rect.width * 0.64f, 5f * scale), new Color(stateAccent.r, stateAccent.g, stateAccent.b, 0.9f));
                Fill(new Rect(rect.x + 8f * scale, rect.y + rect.height * 0.38f, 5f * scale, rect.height * 0.24f), stateAccent);
                Fill(new Rect(rect.xMax - 13f * scale, rect.y + rect.height * 0.38f, 5f * scale, rect.height * 0.24f), stateAccent);
                Fill(Shrink(iconWell, 5f * scale), new Color(stateAccent.r, stateAccent.g, stateAccent.b, 0.16f));
            }
            else if (state == CommandCardState.Error)
            {
                Fill(new Rect(rect.x + 10f * scale, rect.yMax - 7f * scale, rect.width - 20f * scale, 3f * scale), ErrorRed);
            }
        }

        private static void DrawMetalRails(Rect rect, Color edge, float scale)
        {
            var rail = Mathf.Max(2f, 2f * scale);
            Fill(new Rect(rect.x + 12f * scale, rect.y + 4f * scale, rect.width - 24f * scale, rail), edge);
            Fill(new Rect(rect.x + 12f * scale, rect.yMax - 6f * scale, rect.width - 24f * scale, rail), edge);
            Fill(new Rect(rect.x + 4f * scale, rect.y + 12f * scale, rail, rect.height - 24f * scale), edge);
            Fill(new Rect(rect.xMax - 6f * scale, rect.y + 12f * scale, rail, rect.height - 24f * scale), edge);
        }

        private static void DrawCornerHardware(Rect rect, Color accent, float scale)
        {
            var corner = 9f * scale;
            var line = Mathf.Max(2f, 2f * scale);
            var hardware = new Color(accent.r, accent.g, accent.b, 0.86f);

            Fill(new Rect(rect.x + 5f * scale, rect.y + 5f * scale, corner, line), hardware);
            Fill(new Rect(rect.x + 5f * scale, rect.y + 5f * scale, line, corner), hardware);
            Fill(new Rect(rect.xMax - 5f * scale - corner, rect.y + 5f * scale, corner, line), hardware);
            Fill(new Rect(rect.xMax - 7f * scale, rect.y + 5f * scale, line, corner), hardware);
            Fill(new Rect(rect.x + 5f * scale, rect.yMax - 7f * scale, corner, line), hardware);
            Fill(new Rect(rect.x + 5f * scale, rect.yMax - 5f * scale - corner, line, corner), hardware);
            Fill(new Rect(rect.xMax - 5f * scale - corner, rect.yMax - 7f * scale, corner, line), hardware);
            Fill(new Rect(rect.xMax - 7f * scale, rect.yMax - 5f * scale - corner, line, corner), hardware);
        }

        private static void DrawControlChrome(Rect rect, Color accent, bool active, float scale)
        {
            var tint = active ? Color.white : new Color(0.78f, 0.86f, 1f, 0.78f);
            if (RuntimeUiArtLibrary.DrawChromeTexture(rect, "ui_round_button_option_01_v03", tint, ScaleMode.ScaleToFit))
            {
                if (active)
                {
                    var pulseRect = Shrink(rect, rect.width * 0.2f);
                    Fill(new Rect(pulseRect.x, pulseRect.y + pulseRect.height * 0.46f, pulseRect.width, Mathf.Max(2f, 2f * scale)), new Color(accent.r, accent.g, accent.b, 0.72f));
                    Fill(new Rect(pulseRect.x + pulseRect.width * 0.46f, pulseRect.y, Mathf.Max(2f, 2f * scale), pulseRect.height), new Color(accent.r, accent.g, accent.b, 0.72f));
                }

                return;
            }

            DrawGeneratedRoundButton(rect, accent, active, scale);
            return;
        }

        private static void DrawGeneratedPanelButton(Rect rect, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, GeneratedPanelButton(), ScaleMode.StretchToFill, true);
            GUI.color = previousColor;

            var inset = Mathf.Max(4f, 4f * scale);
            DrawOutline(Shrink(rect, inset), new Color(accent.r, accent.g, accent.b, 0.42f), Mathf.Max(1f, 1f * scale));
        }

        private static void DrawButtonAccent(Rect rect, Color accent, float scale)
        {
            var height = Mathf.Max(3f, 3f * scale);
            var width = rect.width * 0.78f;
            Fill(new Rect(rect.x + (rect.width - width) * 0.5f, rect.yMax - height - 3f * scale, width, height), new Color(accent.r, accent.g, accent.b, 0.92f));
        }

        private static void DrawGeneratedRoundButton(Rect rect, Color accent, bool active, float scale)
        {
            var drawRect = rect.width > rect.height
                ? new Rect(rect.x + (rect.width - rect.height) * 0.5f, rect.y, rect.height, rect.height)
                : new Rect(rect.x, rect.y + (rect.height - rect.width) * 0.5f, rect.width, rect.width);
            var previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(drawRect, GeneratedRoundButton(), ScaleMode.StretchToFill, true);
            GUI.color = previousColor;

            if (active)
            {
                var pulseRect = Shrink(drawRect, drawRect.width * 0.24f);
                Fill(new Rect(pulseRect.x, pulseRect.y + pulseRect.height * 0.46f, pulseRect.width, Mathf.Max(2f, 2f * scale)), new Color(accent.r, accent.g, accent.b, 0.7f));
                Fill(new Rect(pulseRect.x + pulseRect.width * 0.46f, pulseRect.y, Mathf.Max(2f, 2f * scale), pulseRect.height), new Color(accent.r, accent.g, accent.b, 0.7f));
            }

            DrawOutline(Shrink(drawRect, drawRect.width * 0.14f), new Color(accent.r, accent.g, accent.b, 0.36f), Mathf.Max(1f, scale));
        }

        private static Texture2D GeneratedPanelButton()
        {
            if (generatedPanelButton != null)
            {
                return generatedPanelButton;
            }

            const int width = 128;
            const int height = 48;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cut = 12;
                    var outsideCut =
                        x + y < cut ||
                        width - 1 - x + y < cut ||
                        x + height - 1 - y < cut ||
                        width - 1 - x + height - 1 - y < cut;
                    if (outsideCut || x < 2 || x > width - 3 || y < 2 || y > height - 3)
                    {
                        texture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                        continue;
                    }

                    var edge = x < 8 || x > width - 9 || y < 8 || y > height - 9 || x + y < cut + 9 || width - 1 - x + y < cut + 9 || x + height - 1 - y < cut + 9 || width - 1 - x + height - 1 - y < cut + 9;
                    var inner = new Color(0.018f, 0.028f, 0.043f, 0.94f);
                    var bevel = new Color(0.34f, 0.36f, 0.34f, 0.86f);
                    texture.SetPixel(x, y, edge ? bevel : inner);
                }
            }

            texture.Apply(false, true);
            generatedPanelButton = texture;
            return generatedPanelButton;
        }

        private static Texture2D GeneratedRoundButton()
        {
            if (generatedRoundButton != null)
            {
                return generatedRoundButton;
            }

            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy) / center;
                    if (dist > 1f)
                    {
                        texture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    }
                    else if (dist > 0.78f)
                    {
                        texture.SetPixel(x, y, new Color(0.48f, 0.43f, 0.32f, 0.9f));
                    }
                    else if (dist > 0.66f)
                    {
                        texture.SetPixel(x, y, new Color(0.05f, 0.065f, 0.075f, 0.96f));
                    }
                    else
                    {
                        var glow = Mathf.Clamp01(1f - dist);
                        texture.SetPixel(x, y, new Color(0.012f + glow * 0.04f, 0.023f + glow * 0.05f, 0.035f + glow * 0.06f, 0.95f));
                    }
                }
            }

            texture.Apply(false, true);
            generatedRoundButton = texture;
            return generatedRoundButton;
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void Fill(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static Rect Shrink(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
        }

        private static RectOffset ZeroOffset()
        {
            return new RectOffset(0, 0, 0, 0);
        }

        private static Color StateAccent(Color accent, CommandCardState state)
        {
            return state switch
            {
                CommandCardState.Disabled => new Color(0.42f, 0.45f, 0.5f, 0.88f),
                CommandCardState.Error => ErrorRed,
                _ => accent
            };
        }

        /// <summary>
        /// The card chrome art. One texture for every state, deliberately.
        /// </summary>
        /// <remarks>
        /// There ARE four state textures and they are not used, because they do not agree with each
        /// other about where the card is. Measured on the 192x232 canvas they share, the frame's
        /// left rail sits at x=49 on normal, 38 on selected, 14 on disabled and 15 on error, and the
        /// top rail at y=13 on three of them and 7 on selected.
        ///
        /// Every state change therefore slid the artwork sideways by up to 35 pixels — 18% of the
        /// card's width — while the icon, label and cost text stayed where the code puts them.
        /// Reported from iPad play as the icons shifting and not aligning when a send card is
        /// pressed, which is exactly what it is: the icon does not move, the card does.
        ///
        /// State is still fully legible, because it was never carried by the swap alone.
        /// DrawCommandCardChrome already outlines a selected card in its accent and darkens a
        /// disabled one, and DrawSendButton greys the icon and text besides. Those cues were
        /// written to work on top of this art and are unchanged.
        ///
        /// This is the reversible half of the fix. Re-export the four textures on a common frame
        /// origin and the swap can come straight back — the measurement above is the spec for it,
        /// and a repeat of it is the check.
        /// </remarks>
        private static string CommandCardTextureName(CommandCardState state) =>
            "ui_command_card_normal_option_04";

        private static Color Tint(Color baseColor, Color tint, float amount)
        {
            return new Color(
                baseColor.r + tint.r * amount,
                baseColor.g + tint.g * amount,
                baseColor.b + tint.b * amount,
                baseColor.a);
        }
    }
}
