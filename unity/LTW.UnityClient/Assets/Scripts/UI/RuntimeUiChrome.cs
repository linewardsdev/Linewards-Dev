#nullable enable

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
        /// Dims everything behind a modal panel.
        /// </summary>
        /// <remarks>
        /// Call immediately BEFORE drawing a panel that owns the screen. This is the visual half of
        /// modality; <see cref="ModalScreenActive"/> is the input half, and both are needed.
        ///
        /// Captured on 2026-07-31: the pre-match title panel rendered over a fully drawn, fully
        /// interactive build palette — two panels overlapping, neither dimmed, and the one
        /// underneath still taking input. A modal that was modal in neither sense.
        /// </remarks>
        public static void DrawModalScrim()
        {
            var priorColor = GUI.color;
            GUI.color = new Color(0.004f, 0.008f, 0.016f, 0.72f);
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

        // Tier row geometry, in unscaled units, measured up from the card's bottom edge. Shared so
        // the row and the card's hit region derive from the same numbers — if they drift, the card
        // either steals the upgrade button's clicks or leaves a dead strip that selects nothing.
        private const float CategoryTierRowBottomInset = 26f;
        private const float CategoryTierRowHeight = 20f;

        /// <summary>Where the tier row sits on a category card.</summary>
        public static Rect CategoryTierRowRect(Rect card, float scale) => new(
            card.x + 7f * scale,
            card.yMax - (CategoryTierRowBottomInset + CategoryTierRowHeight) * scale,
            card.width - 14f * scale,
            CategoryTierRowHeight * scale);

        /// <summary>
        /// The part of a category card that selects the category: everything above its tier row.
        /// </summary>
        public static Rect CategoryCardSelectRect(Rect card, float scale)
        {
            var row = CategoryTierRowRect(card, scale);
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
        /// Preferred height for a category card that carries a tier row, so both pickers ask for
        /// the same thing.
        /// </summary>
        /// <remarks>
        /// Passed as the preferred height to <see cref="CategoryCardHeight"/> rather than used
        /// directly, exactly as that method's remarks require — it still clamps against the panel,
        /// so a panel that has not been grown to match simply gets shorter cards instead of cards
        /// hanging over the board.
        /// </remarks>
        public const float CategoryCardWithTierHeight = 104f;

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
        public static bool DrawCategoryTierRow(
            Rect card,
            int tier,
            int maxTier,
            int upgradeCost,
            bool canAfford,
            Color accent,
            float scale,
            GUIStyle tierStyle,
            GUIStyle buttonStyle)
        {
            var row = CategoryTierRowRect(card, scale);

            tierStyle.fontSize = Mathf.RoundToInt(9f * scale);
            tierStyle.alignment = TextAnchor.MiddleLeft;
            tierStyle.normal.textColor = accent;
            GUI.Label(new Rect(row.x, row.y, row.width * 0.42f, row.height), $"TIER {tier}", tierStyle);

            var buttonRect = new Rect(row.x + row.width * 0.44f, row.y, row.width * 0.56f, row.height);
            if (tier >= maxTier)
            {
                tierStyle.alignment = TextAnchor.MiddleRight;
                tierStyle.normal.textColor = new Color(accent.r, accent.g, accent.b, 0.65f);
                GUI.Label(buttonRect, "MAX", tierStyle);
                tierStyle.alignment = TextAnchor.MiddleLeft;
                return false;
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = canAfford;
            buttonStyle.fontSize = Mathf.RoundToInt(9f * scale);
            var pressed = DrawPanelButton(buttonRect, $"UP {upgradeCost}G", canAfford ? accent : DisabledEdge, scale, buttonStyle);
            GUI.enabled = previousEnabled;
            return pressed;
        }

        /// <summary>
        /// Height for one card in a vertical category picker, derived from the panel it sits in.
        /// </summary>
        /// <remarks>
        /// Shared by the send dock and the build palette because this has now been got wrong twice, in
        /// both of them, the same way: a FIXED card height. At two categories the cards fitted; a third
        /// pushed the last one's bottom edge to 352 inside a 282-tall panel, so it hung outside the dock
        /// and over the board with its lower half off-screen.
        ///
        /// Deriving the height means adding a category — or a fourth, or a tier row inside each card —
        /// cannot bring that back. Anything that wants MORE space per card should raise
        /// <paramref name="preferredHeight"/> and let this clamp it, never bypass it.
        /// </remarks>
        public static float CategoryCardHeight(
            Rect panel,
            float contentTop,
            float gap,
            int cardCount,
            float preferredHeight,
            float scale)
        {
            if (cardCount <= 0)
            {
                return 0f;
            }

            var top = contentTop - panel.y;
            var available = panel.height - top - 12f * scale - gap * (cardCount - 1);
            return Mathf.Min(preferredHeight, Mathf.Max(1f, available / cardCount));
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

        private static string CommandCardTextureName(CommandCardState state)
        {
            return state switch
            {
                CommandCardState.Selected => "ui_command_card_selected_option_04",
                CommandCardState.Disabled => "ui_command_card_disabled_option_04",
                CommandCardState.Error => "ui_command_card_error_option_04",
                _ => "ui_command_card_normal_option_04"
            };
        }

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
