#nullable enable

using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Combat;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    /// <summary>
    /// Every IMGUI panel this controller draws — the tower palette, the selected-tower panel,
    /// the multi-select actions and their shared styles and rects. This is the surface the
    /// item-10 HUD migration replaces, kept in one file so it can be lifted whole.
    /// </summary>
    public sealed partial class TouchPlacementController
    {
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color WardViolet = new(0.608f, 0.424f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);
        private static readonly Color DisabledInk = new(0.22f, 0.25f, 0.32f, 0.88f);
        private static readonly Color DisabledText = new(0.55f, 0.59f, 0.68f, 1f);

        // Dimmer than the meta row: context, not a number the player is pricing a decision against.
        private static readonly Color MutedTraitText = new(0.62f, 0.68f, 0.78f, 0.85f);

        private static GUIStyle? panelStyle;
        private static GUIStyle? titleStyle;
        private static GUIStyle? bodyStyle;
        private static GUIStyle? buttonStyle;
        private static GUIStyle? metaStyle;
        private static GUIStyle? rowNameStyle;

        private int highlightedTowerRole = -1;
        private int selectedTowerCategory = -1;

        private void OnGUI()
        {
            // Cleared FIRST, above every early return, and set again below only if the panel is
            // actually drawn. Four returns sit between here and that draw — no readout, a modal
            // screen, an eliminated seat, and placing a tower — and the last of those is reached by
            // opening BUILD and picking anything, which stranded a non-zero inset and left the board
            // short for the rest of the match.
            RuntimeUiChrome.BuildDockInset = 0f;

            if (!showPlacementReadout)
            {
                return;
            }

            // A session-flow screen owns the display. Standing down is what makes it modal: a
            // scrim can dim this component but cannot stop it taking the click, because IMGUI
            // dispatches events in draw order and the HUD draws first.
            if (RuntimeUiChrome.ModalScreenActive)
            {
                return;
            }

            // The local seat is out — no BUILD, no MULTI, no placement panel, no selected-tower
            // panel. Update has already closed all of them (OPEN_ITEMS.md item 31); this only has to
            // stop drawing, because the launchers are drawn from the closed state and would put BUILD
            // and MULTI back on screen for a seat that cannot use either.
            if (IsLocalSeatEliminated)
            {
                return;
            }

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            DrawTowerPalette(scale);
            // Phone-only: SEND and BUILD share one drawer slot there, so an expanded SEND really
            // does cover this panel and the placement controls beneath it. On a tablet rail SEND
            // is permanently expanded in its own column (see DrawTowerPalette and SendDockController
            // OnGUI, 2026-08-30), so this used to make the selected-tower panel and the placement
            // stack permanently undrawable the moment the rail existed — not just while SEND was
            // in use, for the entire match.
            if (!MobileViewportLayout.HasSideRails && IsSendDockExpanded())
            {
                return;
            }

            PruneMultiSelection();
            DrawSelectedTowerPanel(scale);

            if (!isPlacing)
            {
                return;
            }

            var rect = PlacementPanelRect(scale, frame);
            var accent = SelectedTowerAccent();

            if (MobileViewportLayout.HasSideRails)
            {
                // No inset from the rail variant. It sits beside the board, not over it, so asking
                // the camera to lift for it squeezed the board to a fraction of its height for a
                // panel that was covering nothing — the inset is measured from the screen bottom,
                // and a rail block starts a long way up it.
                DrawPlacementStack(rect, accent, scale);
                return;
            }

            // The bar DOES sit over the board, so it contributes the same inset the build drawer
            // does, and can share that field because the two are mutually exclusive — the palette
            // closes the moment placement begins. The board lifts clear rather than hiding the very
            // cells the player is aiming at.
            RuntimeUiChrome.BuildDockInset = MobileViewportLayout.ViewportHeight - rect.yMin;
            DrawPlacementBar(rect, accent, scale);
        }

        /// <summary>
        /// Placement controls as a single slim bar, for a screen with no rail to put them in.
        /// </summary>
        /// <remarks>
        /// This was a 160-unit panel carrying five stacked things — name and cost, a placement
        /// status line, a recovery hint, the quick-switch strip and three buttons — anchored to the
        /// bottom of the board. It covered roughly four rows of the lane at the exact moment the
        /// player is aiming at a cell, which is the worst possible time to hide them.
        ///
        /// Everything survives; only the stacking goes. The hint line is the one casualty and it is
        /// folded into the status text rather than dropped, because "BUILDER ONLINE • TAP BUILD"
        /// restates what a lit BUILD button already says, while a REJECTION reason does not and is
        /// kept.
        /// </remarks>
        private void DrawPlacementBar(Rect rect, Color accent, float scale)
        {
            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 3f * scale, rect.width, 3f * scale), accent);

            var pad = 7f * scale;
            var gap = 5f * scale;
            var rowHeight = (rect.height - pad * 3f) * 0.5f;

            // Row one: the quick-switch strip, full width. Tried as one row with everything on it
            // and it does not fit — six labels, a name, a status and three buttons crammed into 412
            // units clipped the labels to "RROW"/"ULSE" and pushed BUILD underneath the SEND
            // launcher. Two rows is still less than half the 160-unit panel this replaces, and the
            // point was never to be one row; it was to stop covering the board.
            var switchEntries = CategoryEntriesByCost(LTW.UnityClient.Simulation.TowerCatalog.ForRole(selectedTowerRole).Category);
            if (switchEntries.Count > 0)
            {
                var switchWidth = (rect.width - pad * 2f - gap * (switchEntries.Count - 1)) / switchEntries.Count;
                var switchX = rect.x + pad;
                for (var index = 0; index < switchEntries.Count; index++)
                {
                    var entry = switchEntries[index];
                    if (DrawPlacementSwitchButton(new Rect(switchX, rect.y + pad, switchWidth, rowHeight), entry.ShortLabel, entry.Role, entry.Accent, scale))
                    {
                        BeginTowerPlacement(entry.Role);
                        return;
                    }

                    switchX += switchWidth + gap;
                }
            }

            // Row two: what is being placed, where, and the three actions.
            var y = rect.y + pad * 2f + rowHeight;
            var allWidth = 52f * scale;
            var cancelWidth = 46f * scale;
            var buildWidth = 92f * scale;

            var x = rect.x + pad;
            if (DrawLauncherButton(new Rect(x, y, allWidth, rowHeight), "ALL", SignalGold, scale))
            {
                CancelPlacement(false);
                OpenTowerPalette();
                return;
            }

            x += allWidth + gap;

            // The selected tower is already named by the highlighted button in the strip above, so
            // this carries cost and cell rather than repeating it — which is what let the row fit.
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            bodyStyle.normal.textColor = placementPreview.Accepted ? Cloud : Danger;
            var textRight = rect.xMax - pad - buildWidth - gap - cancelWidth - gap;
            GUI.Label(new Rect(x, y, Mathf.Max(0f, textRight - x), rowHeight), $"{SelectedTowerCost()}G   {PlacementStatusShort()}", bodyStyle);

            if (DrawLauncherButton(new Rect(textRight + gap, y, cancelWidth, rowHeight), "\u00d7", Danger, scale))
            {
                CancelPlacement();
                return;
            }

            if (DrawLauncherButton(new Rect(rect.xMax - pad - buildWidth, y, buildWidth, rowHeight), "BUILD", placementPreview.Accepted ? MintSignal : Danger, scale))
            {
                ConfirmPlacement();
            }
        }

        /// <summary>
        /// The same controls stacked in the side rail, where a wide screen has room and the board
        /// keeps every row.
        /// </summary>
        private void DrawPlacementStack(Rect rect, Color accent, float scale)
        {
            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 3f * scale, rect.width, 3f * scale), accent);

            var pad = 8f * scale;
            var y = rect.y + pad;
            var innerWidth = rect.width - pad * 2f;

            titleStyle!.fontSize = Mathf.RoundToInt(13f * scale);
            titleStyle.normal.textColor = accent;
            GUI.Label(new Rect(rect.x + pad, y, innerWidth, 20f * scale), $"{SelectedTowerName().ToUpperInvariant()}", titleStyle);
            y += 20f * scale;

            bodyStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            bodyStyle.normal.textColor = Cloud;
            GUI.Label(new Rect(rect.x + pad, y, innerWidth, 18f * scale), $"{SelectedTowerCost()}G", bodyStyle);
            y += 18f * scale;

            bodyStyle.normal.textColor = placementPreview.Accepted ? Cloud : Danger;
            GUI.Label(new Rect(rect.x + pad, y, innerWidth, 18f * scale), PlacementStatusShort(), bodyStyle);
            y += 24f * scale;

            var switchEntries = CategoryEntriesByCost(LTW.UnityClient.Simulation.TowerCatalog.ForRole(selectedTowerRole).Category);
            var perRow = Mathf.Max(1, Mathf.FloorToInt(innerWidth / (46f * scale)));
            var switchWidth = (innerWidth - 4f * scale * (perRow - 1)) / perRow;
            var switchHeight = 24f * scale;
            for (var index = 0; index < switchEntries.Count; index++)
            {
                var entry = switchEntries[index];
                var column = index % perRow;
                var cell = new Rect(rect.x + pad + column * (switchWidth + 4f * scale), y, switchWidth, switchHeight);
                if (DrawPlacementSwitchButton(cell, entry.ShortLabel, entry.Role, entry.Accent, scale))
                {
                    BeginTowerPlacement(entry.Role);
                    return;
                }

                if (column == perRow - 1)
                {
                    y += switchHeight + 4f * scale;
                }
            }

            if (switchEntries.Count % perRow != 0)
            {
                y += switchHeight + 4f * scale;
            }

            y += 4f * scale;
            var buttonHeight = 28f * scale;
            var half = (innerWidth - 5f * scale) * 0.5f;
            if (DrawLauncherButton(new Rect(rect.x + pad, y, half, buttonHeight), "ALL", SignalGold, scale))
            {
                CancelPlacement(false);
                OpenTowerPalette();
                return;
            }

            if (DrawLauncherButton(new Rect(rect.x + pad + half + 5f * scale, y, half, buttonHeight), "\u00d7", Danger, scale))
            {
                CancelPlacement();
                return;
            }

            y += buttonHeight + 5f * scale;
            if (DrawLauncherButton(new Rect(rect.x + pad, y, innerWidth, buttonHeight), "BUILD", placementPreview.Accepted ? MintSignal : Danger, scale))
            {
                ConfirmPlacement();
            }
        }

        /// <summary>
        /// The placement state in as few words as a slim bar can carry.
        /// </summary>
        /// <remarks>
        /// A rejection reason is kept in full because it is the only thing on screen that explains a
        /// refusal. The accepted case loses its "BUILDER ONLINE • TAP BUILD" hint, which restated
        /// what a lit BUILD button already communicates.
        /// </remarks>
        private string PlacementStatusShort() =>
            placementPreview.Accepted
                ? $"CELL {selectedCell.x}, {selectedCell.y}"
                : PlacementPreviewText();

        private bool DrawPlacementSwitchButton(Rect rect, string label, int towerRole, Color accent, float scale)
        {
            buttonStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            return RuntimeUiChrome.DrawControlButton(rect, label, accent, selectedTowerRole == towerRole, scale, buttonStyle);
        }

        /// <summary>
        /// The batch actions, drawn into the launcher strip rather than into a panel.
        /// </summary>
        /// <remarks>
        /// This WAS a card, and the card was the wrong shape for the job. Multi-select exists to tap
        /// towers on the board, and the card sat over the board taking those taps — the one mode
        /// where an overlay costs the most is the one mode that had one. It was redundant as well as
        /// harmful: the rings already show which towers are selected and the toast already reports
        /// the count, so the card was re-stating both while covering the thing they described.
        ///
        /// The actions now live in the launcher strip, which is HUD space already spent. RAISE takes
        /// the BUILD slot, since BUILD would only exit this mode anyway, and the counts and prices
        /// ride on the buttons themselves. Nothing is drawn over the board that was not drawn over it
        /// before turning MULTI on.
        /// </remarks>
        private void DrawMultiSelectActions(float scale, Rect frame)
        {
            if (multiSelection.Count == 0 || commandAdapter == null)
            {
                return;
            }

            var positions = SelectedPositions();
            var upgrade = commandAdapter.QuoteSelectionUpgrade(positions);
            var sale = commandAdapter.QuoteSelectionSale(positions);

            var raiseLabel = !upgrade.HasWork
                ? "RAISE 0"
                : upgrade.IsGoldLimited
                    ? $"RAISE {upgrade.Affordable}/{upgrade.Eligible}  {upgrade.AffordableCost}G"
                    : $"RAISE {upgrade.Eligible}  {upgrade.TotalCost}G";
            if (DrawLauncherButton(MultiSelectRaiseRect(scale, frame), raiseLabel, upgrade.HasWork ? MintSignal : DisabledInk, scale))
            {
                RaiseSelection(upgrade);
            }

            var sellLabel = sellArmed ? $"CONFIRM {sale.Towers}" : $"SELL {sale.Towers}  +{sale.Refund}G";
            if (DrawLauncherButton(MultiSelectSellRect(scale, frame), sellLabel, Danger, scale))
            {
                SellSelection(sale);
            }
        }

        /// <summary>
        /// Multi-select's RAISE/SELL/DONE, laid out inside the always-open BUILD panel instead of
        /// the launcher strip <see cref="DrawMultiSelectActions"/> used when the panel could
        /// collapse. Same quotes, same actions — just full-width rows instead of two pills sized to
        /// fit next to a launcher button that no longer exists on rail.
        /// </summary>
        private void DrawMultiSelectPanelRail(Rect rect, float scale)
        {
            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            if (RuntimeUiChrome.DrawPanelButton(new Rect(rect.xMax - 72f * scale, rect.y + 8f * scale, 58f * scale, 32f * scale), "DONE", SignalGold, scale, buttonStyle))
            {
                SetMultiSelectMode(false);
                return;
            }

            if (multiSelection.Count == 0 || commandAdapter == null)
            {
                return;
            }

            var positions = SelectedPositions();
            var upgrade = commandAdapter.QuoteSelectionUpgrade(positions);
            var sale = commandAdapter.QuoteSelectionSale(positions);

            var pad = 12f * scale;
            var actionWidth = rect.width - pad * 2f;
            var actionHeight = 44f * scale;
            var actionY = rect.y + 52f * scale;

            var raiseLabel = !upgrade.HasWork
                ? "RAISE 0"
                : upgrade.IsGoldLimited
                    ? $"RAISE {upgrade.Affordable}/{upgrade.Eligible}  {upgrade.AffordableCost}G"
                    : $"RAISE {upgrade.Eligible}  {upgrade.TotalCost}G";
            if (DrawLauncherButton(new Rect(rect.x + pad, actionY, actionWidth, actionHeight), raiseLabel, upgrade.HasWork ? MintSignal : DisabledInk, scale))
            {
                RaiseSelection(upgrade);
            }

            actionY += actionHeight + 8f * scale;
            var sellLabel = sellArmed ? $"CONFIRM {sale.Towers}" : $"SELL {sale.Towers}  +{sale.Refund}G";
            if (DrawLauncherButton(new Rect(rect.x + pad, actionY, actionWidth, actionHeight), sellLabel, Danger, scale))
            {
                SellSelection(sale);
            }
        }

        private void DrawSelectedTowerPanel(float scale)
        {
            // commandAdapter is checked here rather than only at the two `!= null` sites further down.
            // Below those, the panel dereferences it with `!` for MaxCategoryTier and TowerLineIndexAt,
            // so a null adapter with a live selection threw a NullReferenceException out of OnGUI every
            // frame the panel was up — observed in the editor log at TouchPlacementController.cs:688.
            // Guarding at the top is the honest fix: none of this panel can be drawn without an
            // adapter, so it should not start.
            if (isPlacing || selectedTower is null || commandAdapter == null)
            {
                return;
            }

            var frame = MobileViewportLayout.ScreenRect();
            var rect = SelectedTowerPanelRect(scale, frame);
            var accent = TowerAccent(selectedTower.TowerId.Value);
            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            titleStyle!.fontSize = Mathf.RoundToInt(16f * scale);
            titleStyle.normal.textColor = accent;
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            bodyStyle.normal.textColor = Cloud;

            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 8f * scale, rect.width - 156f * scale, 22f * scale), TowerRoleName(selectedTower.TowerId.Value).ToUpperInvariant(), titleStyle);
            // BUILD/upgrade share a left column and X/SELL share a right column, both columns the
            // same width and x-offset top-to-bottom. Found by capture comparison (2026-08-29): X used
            // to be 40 wide against SELL's 70 directly below it, and BUILD's left edge sat 28 units
            // right of the upgrade button's — same-looking pills that didn't actually line up.
            if (DrawLauncherButton(new Rect(rect.xMax - 172f * scale, rect.y + 7f * scale, 78f * scale, 30f * scale), "BUILD", SignalGold, scale))
            {
                selectedTower = null;
                HideSelectionRing();
                OpenTowerPalette();
                return;
            }
            if (RuntimeUiChrome.DrawPanelButton(new Rect(rect.xMax - 86f * scale, rect.y + 7f * scale, 70f * scale, 30f * scale), "X", accent, scale, buttonStyle ?? GUI.skin.button))
            {
                selectedTower = null;
                HideSelectionRing();
                return;
            }
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 32f * scale, rect.width - 24f * scale, 18f * scale), TowerPurpose(selectedTower.TowerId.Value), bodyStyle);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 51f * scale, rect.width - 24f * scale, 18f * scale), $"CELL {selectedTower.Position.X}, {selectedTower.Position.Y}  OWNER P{selectedTower.OwnerId.Value}", bodyStyle);
            // Tier of THIS tower, which is not the same as its line's tier: a line tier only
            // applies to towers built after it, so a veteran tower can sit below its own line.
            // Showing it here is what makes "why is that one weaker" answerable.
            var upgradeCost = commandAdapter != null ? commandAdapter.TowerUpgradeCostAt(selectedTower.Position.X, selectedTower.Position.Y) : 0;
            var canUpgrade = commandAdapter != null && commandAdapter.CanUpgradeTowerAt(selectedTower.Position.X, selectedTower.Position.Y);
            var affordable = canUpgrade && CurrentPlayerGold() >= upgradeCost;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 70f * scale, rect.width - 190f * scale, 18f * scale), $"TIER {selectedTower.Tier}", bodyStyle);

            buttonStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            if (RuntimeUiChrome.DrawPanelButton(new Rect(rect.x + rect.width - 86f * scale, rect.y + 36f * scale, 70f * scale, 42f * scale), "SELL", Danger, scale, buttonStyle))
            {
                SellLastTower();
            }

            // The button is ALWAYS drawn, even when it cannot be pressed, and its label says what is
            // missing. An earlier version replaced it with a bare "LINE CAPPED" whenever the tower
            // had caught up to its line — which is the state every tower is in at the start of a
            // match, so the whole feature looked like it did not exist, and the label named no way
            // out of it. A disabled control that says NEED ARCANE 2 is discoverable; an absent one
            // teaches nothing.
            var upgradeRect = new Rect(rect.x + rect.width - 172f * scale, rect.y + 36f * scale, 78f * scale, 42f * scale);
            var atMaxTier = selectedTower.Tier >= commandAdapter!.MaxCategoryTier;
            var lineIndex = commandAdapter.TowerLineIndexAt(selectedTower.Position.X, selectedTower.Position.Y);
            var lineLabel = lineIndex >= 0 && lineIndex < LTW.UnityClient.Simulation.TowerCatalog.CategoryLabels.Length
                ? LTW.UnityClient.Simulation.TowerCatalog.CategoryLabels[lineIndex]
                : "LINE";

            var upgradeLabel = atMaxTier ? "MAX"
                : !canUpgrade ? $"NEED\n{lineLabel} {selectedTower.Tier + 1}"
                : $"UP {upgradeCost}G";

            // Pressed even when it cannot succeed, on purpose. RuntimeUiChrome.DrawPanelButton is a
            // hand-rolled MouseUp check that ignores GUI.enabled entirely, so setting that flag
            // only greys the colour — the tap lands either way. Rather than swallow it silently,
            // the command runs and its rejection explains itself ("Upgrade the line first", "Need
            // more gold"), which is what the send and build cards already do. A tap that appears to
            // do nothing is the worst of the three options.
            buttonStyle.fontSize = Mathf.RoundToInt((canUpgrade ? 11f : 9f) * scale);
            if (RuntimeUiChrome.DrawPanelButton(upgradeRect, upgradeLabel, affordable ? MintSignal : DisabledText, scale, buttonStyle))
            {
                var result = commandAdapter.UpgradeTowerAt(selectedTower.Position.X, selectedTower.Position.Y);
                if (result.Accepted)
                {
                    feedbackView.ShowEconomy($"{TowerRoleName(selectedTower.TowerId.Value)} upgraded");
                    RefreshSelectedTowerFromSnapshot();
                }
                else
                {
                    feedbackView.ShowRejected(result.RejectionReason, upgradeCost, CurrentPlayerGold());
                }
            }

            buttonStyle.fontSize = Mathf.RoundToInt(11f * scale);
        }

        private string SelectedTowerName()
        {
            return LTW.UnityClient.Simulation.TowerCatalog.ForRole(selectedTowerRole).DisplayName;
        }

        private static string TowerRoleName(string towerId) =>
            LTW.UnityClient.Simulation.TowerCatalog.ForContentId(towerId).DisplayName;

        private static Color TowerAccent(string towerId) =>
            LTW.UnityClient.Simulation.TowerCatalog.ForContentId(towerId).Accent;

        private Color SelectedTowerAccent()
        {
            return LTW.UnityClient.Simulation.TowerCatalog.ForRole(selectedTowerRole).Accent;
        }

        private void DrawTowerPalette(float scale)
        {
            if (isPlacing)
            {
                return;
            }

            // Phone-only: a bottom drawer is one physical slot, so SEND being expanded there
            // really does cover BUILD's launcher underneath it. On a tablet rail the two live in
            // separate, non-overlapping columns (left/right), so this used to make BUILD's
            // launcher un-drawn and un-tappable — not just hidden, ABSENT from OnGUI entirely —
            // for the entire time SEND was open on the other side of the screen. Reported live
            // 2026-08-30 as "the buttons have no function... tapping them does nothing."
            if (!MobileViewportLayout.HasSideRails && !isPaletteExpanded && IsSendDockExpanded())
            {
                return;
            }

            // No launcher, no collapse: on a tablet rail there is room for the panel to just stay
            // open (owner's call, 2026-08-30). This alone makes the collapsed branch below dead
            // code for rail, which is what removes the BUILD launcher, the old DONE-in-launcher-
            // strip and the RAISE/SELL launcher-strip buttons from it — see DrawMultiSelectPanelRail
            // for where those three moved instead.
            if (MobileViewportLayout.HasSideRails)
            {
                isPaletteExpanded = true;
            }

            var frame = MobileViewportLayout.ScreenRect();
            var launcherRect = TowerPaletteLauncherRect(scale, frame);
            if (!isPaletteExpanded)
            {

                // BUILD yields its slot to RAISE while multi-select is on. It is not lost: BUILD
                // would only have exited the mode, which is what DONE directly above it does.
                if (isMultiSelectMode)
                {
                    DrawMultiSelectActions(scale, frame);
                }
                else if (DrawLauncherButton(launcherRect, "BUILD", MintSignal, scale))
                {
                    OpenTowerPalette();
                }

                // MULTI's launcher was removed 2026-08-09 ("the multi button can be removed"), but
                // DONE is deliberately still drawn while the mode is on, because the button was
                // never the only way in: double-tapping a tower selects every tower of its type and
                // turns the mode on (see SelectEveryTowerOfType). Dropping this line entirely would
                // leave that gesture with no way out.
                //
                // The mode itself and its batch upgrade/sell paths are untouched. They are reached
                // from the category cards as well, and TowerSelectionBatchTests pins their
                // behaviour — removing an entry point is not the same as removing the feature, and
                // only the first was asked for.
                if (isMultiSelectMode && DrawLauncherButton(MultiSelectLauncherRect(scale, frame), "DONE", SignalGold, scale))
                {
                    SetMultiSelectMode(false);
                }

                return;
            }

            var rect = TowerPalettePanelRect(scale, frame);

            // Tell the board camera how much of the bottom this is taking, so the lane is lifted
            // above it instead of hidden under it. Measured from the screen bottom rather than the
            // frame's, because the camera viewport is in screen space.
            //
            // This panel is the single owner of the value, and sets it back to zero itself when it
            // closes. Clearing it from another component's Update instead was the first attempt and
            // is a race: Unity does not order Update between components, so the renderer could read
            // the inset either side of the clear and the board lifted only on some frames.
            //
            // Rail mode is excluded, the same way DrawPlacementStack's rail branch already was: the
            // rail sits beside the board, not over it, so asking the camera to lift for it squeezed
            // the board to a sliver for a panel covering nothing. Missing this guard when the rail
            // branch was added is exactly the bug that comment already describes — confirmed by
            // capture, where the board collapsed to a strip in the middle of a mostly-black screen.
            if (!MobileViewportLayout.HasSideRails)
            {
                RuntimeUiChrome.BuildDockInset = MobileViewportLayout.ViewportHeight - rect.yMin;
            }

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), MintSignal);

            // No CLOSE on rail: the panel cannot collapse there, so a button that used to do it
            // would either do nothing or reopen the launcher this branch no longer draws.
            if (!MobileViewportLayout.HasSideRails && DrawLauncherButton(launcherRect, "CLOSE", MintSignal, scale))
            {
                isPaletteExpanded = false;
                return;
            }

            titleStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            titleStyle.normal.textColor = MintSignal;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 10f * scale, 120f * scale, 20f * scale), "BUILD", titleStyle);
            // The launcher slot above is already CLOSE while the palette is expanded; a header
            // CLOSE duplicated it. See the matching note in SendDockController.
            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);

            // Multi-select took over the launcher strip's BUILD slot for RAISE/SELL/DONE while the
            // panel was collapsible (see the removed launcher-strip branch above). With the panel
            // always open on rail that strip no longer exists, so multi-select gets a page of the
            // panel itself instead of fighting the category picker for the same space.
            if (MobileViewportLayout.HasSideRails && isMultiSelectMode)
            {
                DrawMultiSelectPanelRail(rect, scale);
                return;
            }

            // Hoisted above the rail/drawer split, not drawn once per branch: this used to sit
            // after that split reached a shared fall-through, which the rail branch's early
            // return (below) would otherwise skip entirely — a category selected on a rail had no
            // way back to the picker at all. Matches where SendDockController already draws BACK,
            // for the same reason.
            if (selectedTowerCategory >= 0
                && RuntimeUiChrome.DrawPanelButton(new Rect(rect.xMax - 72f * scale, rect.y + 8f * scale, 58f * scale, 32f * scale), "BACK", MintSignal, scale, buttonStyle))
            {
                selectedTowerCategory = -1;
                return;
            }

            var gap = 8f * scale;

            if (MobileViewportLayout.HasSideRails)
            {
                // A full-width LIST, not the drawer's grid of narrow cards — mirrors
                // SendDockController's identical redesign (owner's call, 2026-08-30) for the same
                // reason: a rail card only ever got a third or half of the rail's width shared
                // with siblings, and every round of "make more fit in the same card" (icons,
                // specialty text, row height) just found a new way to overflow that fixed
                // portrait shape. See RuntimeUiChrome.DrawListRow for why this cannot reuse the
                // card art itself.
                var contentTop = rect.y + 40f * scale;
                if (selectedTowerCategory < 0)
                {
                    DrawTowerCategoryPickerRail(rect, contentTop, scale);
                }
                else
                {
                    DrawTowerGridRail(CategoryEntriesByCost(selectedTowerCategory), rect, contentTop, scale);
                }

                return;
            }

            var buttonY = rect.y + 84f * scale;
            var buttonHeight = 84f * scale;

            if (selectedTowerCategory < 0)
            {
                DrawTowerCategoryPicker(rect, buttonY, buttonHeight, gap, scale, LTW.UnityClient.Simulation.TowerCatalog.CategoryLabels.Length);
                return;
            }

            DrawTowerCategoryGrid(rect, buttonY, buttonHeight, gap, scale);
        }

        /// <summary>
        /// Category chooser, mirroring the send dock. Card height is divided out of the panel's
        /// actual height so adding a category cannot push the last card off the panel.
        /// </summary>
        /// <remarks>
        /// <paramref name="columns"/> wraps into more than one row when it is fewer than the
        /// category count, all cards sized as if every row were full — the trailing partial row
        /// (three categories, two columns: GROVE alone) sits in the first slot rather than
        /// stretching to fill the row, so it stays the same size as the pair above it.
        ///
        /// Rail mode passes 2, not 3, because this card carries TWO bottom-anchored rows (a
        /// whole-line batch upgrade above the tier row) that the send dock's equivalent does not —
        /// at the width three columns leaves in a rail, the "5 TOWERS" hint slid down far enough to
        /// print through both of them (reported from play 2026-08-29, alongside "NEED +70" clipping
        /// to "EED +7" from the same undersized card). Measured, not guessed: a rail's card at three
        /// columns is 264 units wide against phone drawer's own 321 at a comparable scale, because
        /// the drawer temporarily claims nearly the whole screen width where the rail is a
        /// permanent, narrower column — two columns clears 404 units, comfortably past what both
        /// rows need.
        /// </remarks>
        private void DrawTowerCategoryPicker(Rect rect, float buttonY, float buttonHeight, float gap, float scale, int columns)
        {
            var labels = LTW.UnityClient.Simulation.TowerCatalog.CategoryLabels;
            var gold = CurrentPlayerGold();
            // -1 until the first tower goes down, after which every other line is locked out for the
            // rest of the match. Read once per frame rather than per card so all three agree.
            var chosenLine = CurrentPlayerTowerLine();
            var rowHeight = 0f;
            var rowCount = Mathf.CeilToInt((float)labels.Length / columns);
            var maxCardHeight = RuntimeUiChrome.CategoryPickerRowMaxHeight(rect, buttonY, gap, rowCount, scale);

            for (var category = 0; category < labels.Length; category++)
            {
                var row = category / columns;
                var column = category % columns;
                var top = buttonY + row * (rowHeight + gap);
                var locked = chosenLine >= 0 && category != chosenLine;
                var accent = CategoryAccent(category);
                var cardRect = RuntimeUiChrome.CategoryCardRect(rect, top, gap, column, columns, scale, maxCardHeight);
                if (row == 0 && column == 0)
                {
                    // Captured from the first card so row 1's Y offset (above) has something to
                    // add — every card is the same size (fixed columns, not the row's own item
                    // count), so the first card's height speaks for all of them.
                    rowHeight = cardRect.height;
                }
                // Hit region excludes BOTH action rows, or the card's own button eats their clicks
                // before either is ever delivered. hasIcon: false — this card has no icon, so the
                // chrome's own icon-well backdrop is skipped rather than drawn over nothing
                // (OPEN_ITEMS.md item 48's "unexplained translucent square").
                var pressed = RuntimeUiChrome.DrawCommandCard(
                    cardRect, accent, locked ? CommandCardState.Disabled : CommandCardState.Normal, scale, RuntimeUiChrome.CategoryCardSelectRect(cardRect, scale), hasIcon: false);

                // metaStyle, not buttonStyle: buttonStyle is GUI.skin.button underneath, and only its
                // text color was ever overridden here, so its solid grey button-skin background box
                // drew behind the line name on every card — wide enough to sit on top of the art's
                // own rounded corners on both sides. Confirmed via a real-UI capture (item 48).
                // metaStyle is GUI.skin.label, background-free.
                metaStyle!.fontSize = Mathf.RoundToInt(13f * scale);
                metaStyle.normal.textColor = locked ? DisabledText : Cloud;
                metaStyle.alignment = TextAnchor.MiddleCenter;
                // Label and meta are positioned proportionally here, matching the send dock's
                // category card. They previously used CommandCardLabelRect/CommandCardMetaRect,
                // which anchor a fixed distance off the card's BOTTOM edge — on a card grown for a
                // tier row that put both lines straight through the new row.
                GUI.Label(new Rect(cardRect.x, cardRect.y + cardRect.height * 0.20f, cardRect.width, 22f * scale), labels[category], metaStyle);

                metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
                metaStyle.normal.textColor = locked ? DisabledText : accent;
                metaStyle.alignment = TextAnchor.MiddleCenter;
                // "LOCKED" rather than the tower count, because the count is an invitation and this
                // card is not one. The chosen line's own card keeps saying what it holds.
                GUI.Label(new Rect(cardRect.x, cardRect.y + cardRect.height * 0.44f, cardRect.width, 18f * scale), locked ? "LOCKED" : "5 TOWERS", metaStyle);

                // Both rows act on the line they sit under — a batch upgrade of towers this seat can
                // never own, and a tier for a line it can never build. Drawn only for a line still
                // in play, so a locked card is a flat statement rather than three dead buttons.
                if (!locked)
                {
                    DrawTowerCategoryBatch(cardRect, category, accent, scale);
                    DrawTowerCategoryTier(cardRect, category, accent, gold, scale);
                }

                // DrawCommandCard already refuses the press through GUI.enabled for a Disabled card;
                // this is the second lock, so a future change to that state handling cannot quietly
                // reopen a line the simulation would then reject.
                if (pressed && !locked)
                {
                    selectedTowerCategory = category;
                }
            }
        }

        /// <summary>
        /// Raise every already-placed tower in one line to the tier that line has reached.
        /// </summary>
        /// <remarks>
        /// Buying a line tier only raises what you build NEXT; towers already standing keep the tier
        /// they were built at and have to be paid up individually. That is the original game's rule
        /// and it is being kept, but it left the player tapping the same tower-by-tower upgrade over
        /// and over across a whole lane. This is that same sequence of taps behind one button, at
        /// the same total price — a convenience, not a discount.
        ///
        /// The spend is best-effort by design: it raises as many as the gold reaches, cheapest
        /// first, rather than refusing a batch it cannot finish. The button says up front how many
        /// that will be, so a partial result is the advertised outcome rather than a surprise.
        /// </remarks>
        private void DrawTowerCategoryBatch(Rect cardRect, int category, Color accent, float scale)
        {
            if (commandAdapter == null)
            {
                return;
            }

            var quote = commandAdapter.QuoteLineUpgrade(category);
            if (!RuntimeUiChrome.DrawCategoryBatchRow(cardRect, quote, accent, scale, metaStyle!, buttonStyle!))
            {
                return;
            }

            if (quote.Affordable <= 0)
            {
                // The typed overload, so this reads the same as every other "cannot afford it" in
                // the HUD and quotes the shortfall in the same words.
                feedbackView.ShowRejected(CommandRejectionReason.InsufficientGold, quote.TotalCost, CurrentPlayerGold());
                return;
            }

            var outcome = commandAdapter.UpgradeLine(category);
            if (outcome.Upgraded <= 0)
            {
                feedbackView.ShowRejected(CommandRejectionReason.InvalidTier);
                return;
            }

            // Says what it did, and — when it could not finish — what stopped it. "Raised 3" alone
            // would leave the player counting towers to work out whether the other two failed or
            // were never eligible.
            feedbackView.ShowAccepted(outcome.IsPartial
                ? $"Raised {outcome.Upgraded} of {outcome.Eligible} for {outcome.GoldSpent}G — out of gold"
                : $"Raised {outcome.Upgraded} for {outcome.GoldSpent}G");
        }

        /// <summary>
        /// Tier readout and upgrade button for one tower line.
        /// </summary>
        private void DrawTowerCategoryTier(Rect cardRect, int category, Color accent, int gold, float scale)
        {
            if (commandAdapter == null)
            {
                return;
            }

            var tier = commandAdapter.TowerLineTier(category);
            var cost = commandAdapter.NextTierCost(LTW.Simulation.Commands.CategoryKind.TowerLine, category);
            var requiredIncome = commandAdapter.NextTierMinimumIncome(LTW.Simulation.Commands.CategoryKind.TowerLine, category);
            var pressed = RuntimeUiChrome.DrawCategoryTierRow(
                cardRect, tier, commandAdapter.MaxCategoryTier, cost, gold >= cost, accent, scale, metaStyle!, buttonStyle!,
                requiredIncome, commandAdapter.CurrentPlayerIncome());

            if (pressed)
            {
                var result = commandAdapter.BuyTowerLineTier(category);
                var label = LTW.UnityClient.Simulation.TowerCatalog.CategoryLabels[category];
                if (result.Accepted)
                {
                    feedbackView.ShowEconomy($"{label} TIER {tier + 1}");
                }
                else
                {
                    feedbackView.ShowRejected(result.RejectionReason, cost, gold);
                }
            }
        }

        /// <summary>
        /// Rail-only category list: one full-width row per tower line, replacing the grid of
        /// narrow cards. Taller than SendDockController's equivalent rows — a tower line carries
        /// both the whole-line batch upgrade and its own tier row stacked at the card's bottom,
        /// where a send category only has the tier row — so this reserves the same
        /// CategoryCardSelectRect footprint (batch + tier) the drawer's cards already did rather
        /// than the tier-row-only boundary SendDockController computes for itself.
        /// </summary>
        private void DrawTowerCategoryPickerRail(Rect rect, float contentTop, float scale)
        {
            var labels = LTW.UnityClient.Simulation.TowerCatalog.CategoryLabels;
            var gold = CurrentPlayerGold();
            var chosenLine = CurrentPlayerTowerLine();
            const float rowHeight = 148f;
            const float gap = 10f;

            for (var category = 0; category < labels.Length; category++)
            {
                var row = RuntimeUiChrome.ListRowRect(rect, contentTop, rowHeight * scale, gap * scale, category);
                if (row.yMax > rect.yMax + 1f)
                {
                    break;
                }

                var locked = chosenLine >= 0 && category != chosenLine;
                var accent = CategoryAccent(category);

                // Excludes both action rows (batch + tier), same reasoning as
                // RuntimeUiChrome.CategoryCardSelectRect's own remark: the row's GUI.Button
                // consumes the click for its whole hit area, so either button drawn afterward
                // inside it would never see a press.
                var selectRect = RuntimeUiChrome.CategoryCardSelectRect(row, scale);
                var pressed = RuntimeUiChrome.DrawListRow(row, accent, locked ? CommandCardState.Disabled : CommandCardState.Normal, scale, selectRect);

                var textX = row.x + 14f * scale;
                var textWidth = row.width - 28f * scale;

                rowNameStyle!.fontSize = Mathf.RoundToInt(16f * scale);
                rowNameStyle.normal.textColor = locked ? DisabledText : Cloud;
                GUI.Label(new Rect(textX, row.y + 8f * scale, textWidth, 24f * scale), labels[category], rowNameStyle);

                metaStyle!.fontSize = Mathf.RoundToInt(10f * scale);
                metaStyle.alignment = TextAnchor.MiddleLeft;
                metaStyle.wordWrap = false;
                metaStyle.clipping = TextClipping.Overflow;
                metaStyle.normal.textColor = locked ? DisabledText : accent;
                // "LOCKED" rather than the tower count, because the count is an invitation and
                // this row is not one. The chosen line's own row keeps saying what it holds.
                GUI.Label(new Rect(textX, row.y + 34f * scale, textWidth, 18f * scale), locked ? "LOCKED" : "5 TOWERS", metaStyle);

                if (!locked)
                {
                    DrawTowerCategoryBatch(row, category, accent, scale);
                    DrawTowerCategoryTier(row, category, accent, gold, scale);
                }

                if (pressed && !locked)
                {
                    selectedTowerCategory = category;
                }
            }
        }

        /// <summary>Scroll offset for the rail's tower list, one per controller instance.</summary>
        private Vector2 towerListScroll;

        /// <summary>Drag-scroll state for <see cref="towerListScroll"/>. See HandleListDragScroll.</summary>
        private readonly RuntimeUiChrome.DragScrollTracker towerListDragTracker = new();

        /// <summary>
        /// Rail-only tower list: one full-width row per tower, scrolling rather than the grid's
        /// silent clip. See SendDockController.DrawSendCardsRail's own remark — the same
        /// measured 2026-08-30 shortfall applies here (ARCANE alone already has six towers
        /// against five per send category).
        /// </summary>
        private void DrawTowerGridRail(System.Collections.Generic.List<LTW.UnityClient.Simulation.TowerCatalog.Entry> entries, Rect rect, float contentTop, float scale)
        {
            if (entries.Count == 0)
            {
                return;
            }

            var gold = CurrentPlayerGold();
            const float rowHeight = 88f;
            const float gap = 8f;

            var viewRect = new Rect(rect.x, contentTop, rect.width, Mathf.Max(1f, rect.yMax - contentTop));
            var rowStride = rowHeight * scale + gap * scale;
            var contentHeight = Mathf.Max(viewRect.height, entries.Count * rowStride - gap * scale);
            var scrollbarAllowance = contentHeight > viewRect.height ? 18f * scale : 0f;
            var contentRect = new Rect(0f, 0f, viewRect.width - scrollbarAllowance, contentHeight);
            var listPanel = new Rect(0f, 0f, contentRect.width, contentRect.height);

            // The scrollbar column is excluded from the touch rect on purpose — see
            // RuntimeUiChrome.HandleListDragScroll's remarks on why a press on the thumb itself is
            // left to Unity's own scrollbar handling instead of also being read as a content drag.
            var dragTouchRect = new Rect(viewRect.x, viewRect.y, viewRect.width - scrollbarAllowance, viewRect.height);
            towerListScroll = RuntimeUiChrome.HandleListDragScroll(towerListDragTracker, dragTouchRect, towerListScroll, viewRect.height, contentHeight, scale);

            towerListScroll = GUI.BeginScrollView(viewRect, towerListScroll, contentRect);

            // try/finally, not just tidiness — see SendDockController.DrawSendCardsRail's own
            // remark: BeginScrollView/EndScrollView must always pair, or anything thrown between
            // them leaves IMGUI's clip/group stack unbalanced for every draw call afterward.
            try
            {
                for (var slot = 0; slot < entries.Count; slot++)
                {
                    var entry = entries[slot];
                    var row = RuntimeUiChrome.ListRowRect(listPanel, 0f, rowHeight * scale, gap * scale, slot);
                    var cost = TowerCostFor(entry);

                    if (DrawTowerRow(row, entry, cost, gold >= cost, highlightedTowerRole == entry.Role, scale))
                    {
                        selectedTower = null;
                        BeginTowerPlacement(entry.Role);
                    }
                }
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        /// <summary>One tower, as a full-width rail row rather than a card.</summary>
        private bool DrawTowerRow(Rect row, LTW.UnityClient.Simulation.TowerCatalog.Entry entry, int cost, bool isAffordable, bool isSelected, float scale)
        {
            var displayAccent = isAffordable ? entry.Accent : DisabledText;
            var state = isSelected ? CommandCardState.Selected : CommandCardState.Normal;
            var pressed = RuntimeUiChrome.DrawListRow(row, entry.Accent, state, scale);

            var iconRect = RuntimeUiChrome.ListRowIconRect(row, scale);
            RuntimeUiChrome.DrawListRowIconWell(iconRect, displayAccent, scale);
            if (!RuntimeUiIconLibrary.DrawIcon(iconRect, $"ui_icon_tower_{entry.RoleId}_v01", isAffordable))
            {
                DrawTowerIcon(iconRect, TowerIconForRole(entry.Role), displayAccent, scale);
            }

            var costColumnWidth = 96f * scale;
            var textX = iconRect.xMax + 12f * scale;
            var textWidth = Mathf.Max(1f, row.xMax - costColumnWidth - textX);

            rowNameStyle!.fontSize = Mathf.RoundToInt(14f * scale);
            rowNameStyle.normal.textColor = isAffordable ? Cloud : DisabledText;
            GUI.Label(new Rect(textX, row.y + 8f * scale, textWidth, 22f * scale), entry.ShortLabel, rowNameStyle);

            if (CodexScreenView.FindTower(entry.ContentId) is { } towerDefinition)
            {
                metaStyle!.fontSize = Mathf.RoundToInt(10f * scale);
                metaStyle.alignment = TextAnchor.MiddleLeft;
                metaStyle.wordWrap = false;
                metaStyle.clipping = TextClipping.Overflow;
                var fitted = RuntimeUiChrome.FitSpecialtyText(CodexScreenView.TowerTraits(towerDefinition), metaStyle, textWidth);
                if (fitted != null)
                {
                    metaStyle.normal.textColor = isAffordable ? MutedTraitText : DisabledText;
                    GUI.Label(new Rect(textX, row.yMax - 26f * scale, textWidth, 18f * scale), fitted, metaStyle);
                }
            }

            metaStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            metaStyle.alignment = TextAnchor.MiddleRight;
            metaStyle.wordWrap = false;
            metaStyle.clipping = TextClipping.Overflow;
            metaStyle.normal.textColor = isAffordable
                ? new Color(
                    Mathf.Lerp(displayAccent.r, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.g, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.b, 1f, 0.55f),
                    1f)
                : displayAccent;
            GUI.Label(new Rect(row.xMax - costColumnWidth - 8f * scale, row.y, costColumnWidth, row.height), $"{cost}G", metaStyle);

            return pressed;
        }

        /// <summary>
        /// The five towers of the selected category, laid out 3 over 2 like the send grids.
        /// </summary>
        /// <remarks>
        /// Driven off TowerCatalog rather than a hardcoded button per tower. The previous version
        /// spelled out each button with its own literal price, which is how the palette came to
        /// advertise 20 gold for a tower the simulation charged 14 for.
        /// </remarks>
        private void DrawTowerCategoryGrid(Rect rect, float buttonY, float buttonHeight, float gap, float scale)
        {
            var gold = CurrentPlayerGold();
            var entries = CategoryEntriesByCost(selectedTowerCategory);
            if (entries.Count == 0)
            {
                return;
            }

            // Cheapest first, left to right. Sorted at draw time against the SIMULATION's cost rather
            // than by reordering TowerCatalog.Entries, for two reasons: the catalog deliberately holds
            // no cost (it is read from ContentCatalog at display time, because a copy in the client is
            // exactly what went stale before), and Entry.Role is the palette's identity — reordering
            // the array would renumber roles that saved captures and review tooling refer to.
            // Sorting here means a cost rebalance reorders the palette on its own.


            var firstRow = Mathf.Min(3, entries.Count);
            var firstRowWidth = (rect.width - 24f * scale - gap * (firstRow - 1)) / firstRow;
            var x = rect.x + 12f * scale;

            for (var index = 0; index < firstRow; index++)
            {
                DrawCatalogPaletteButton(new Rect(x, buttonY, firstRowWidth, buttonHeight), entries[index], gold, scale);
                x += firstRowWidth + gap;
            }

            var remaining = entries.Count - firstRow;
            if (remaining <= 0)
            {
                return;
            }

            var secondRowY = buttonY + buttonHeight + gap;
            var secondRowWidth = (rect.width - 24f * scale - gap * (remaining - 1)) / remaining;
            x = rect.x + 12f * scale;
            for (var index = firstRow; index < entries.Count; index++)
            {
                DrawCatalogPaletteButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), entries[index], gold, scale);
                x += secondRowWidth + gap;
            }
        }

        /// <summary>
        /// One category's towers, cheapest first. Shared by the build palette and the in-placement
        /// quick-switch strip so the two can never disagree about order.
        /// </summary>
        /// <remarks>
        /// Sorted against the SIMULATION's cost rather than by reordering TowerCatalog.Entries: the
        /// catalog deliberately holds no cost (it is read from ContentCatalog at display time,
        /// because a client-side copy is exactly what went stale before), and Entry.Role is the
        /// palette's identity, so reordering that array would renumber roles saved captures and
        /// review tooling refer to. Ties break by role for a stable order — Thorn Snare and Spore
        /// Cloud are both 34 gold.
        /// </remarks>
        private System.Collections.Generic.List<LTW.UnityClient.Simulation.TowerCatalog.Entry> CategoryEntriesByCost(int category)
        {
            var entries = new System.Collections.Generic.List<LTW.UnityClient.Simulation.TowerCatalog.Entry>(
                LTW.UnityClient.Simulation.TowerCatalog.InCategory(category));
            entries.Sort((left, right) =>
            {
                var byCost = TowerCostFor(left).CompareTo(TowerCostFor(right));
                return byCost != 0 ? byCost : left.Role.CompareTo(right.Role);
            });
            return entries;
        }

        /// <summary>Tower cost from the simulation, or 0 before the adapter is wired.</summary>
        private int TowerCostFor(LTW.UnityClient.Simulation.TowerCatalog.Entry entry) =>
            commandAdapter != null ? commandAdapter.TowerCost(entry.Role) : 0;

        private void DrawCatalogPaletteButton(Rect buttonRect, LTW.UnityClient.Simulation.TowerCatalog.Entry entry, int gold, float scale)
        {
            var cost = commandAdapter != null ? commandAdapter.TowerCost(entry.Role) : 0;
            if (DrawCatalogCard(buttonRect, entry, cost, gold >= cost, highlightedTowerRole == entry.Role, scale))
            {
                selectedTower = null;
                BeginTowerPlacement(entry.Role);
            }
        }

        /// <summary>
        /// Palette card whose icon is resolved from the tower's content id.
        /// </summary>
        /// <remarks>
        /// DrawPaletteButton takes a TowerIconKind, a five-value enum from when there were five
        /// towers, so every new tower had to borrow one of the original pictures. All fifteen now
        /// have a rendered icon named after their content id, so the id is what we look up. The
        /// procedural DrawTowerIcon fallback still covers a missing file.
        /// </remarks>
        private bool DrawCatalogCard(Rect rect, LTW.UnityClient.Simulation.TowerCatalog.Entry entry, int cost, bool isAffordable, bool isSelected, float scale)
        {
            var displayAccent = isAffordable ? entry.Accent : DisabledText;
            var state = isSelected ? CommandCardState.Selected : CommandCardState.Normal;
            var pressed = RuntimeUiChrome.DrawCommandCard(rect, entry.Accent, state, scale);

            RuntimeUiChrome.DrawCommandCardUnitIconWell(rect, displayAccent, scale);
            var iconRect = RuntimeUiChrome.CommandCardUnitIconRect(rect, scale);
            if (!RuntimeUiIconLibrary.DrawIcon(iconRect, $"ui_icon_tower_{entry.RoleId}_v01", isAffordable))
            {
                DrawTowerIcon(iconRect, TowerIconForRole(entry.Role), displayAccent, scale);
            }

            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            buttonStyle.normal.textColor = isAffordable ? Cloud : DisabledText;
            buttonStyle.hover.textColor = buttonStyle.normal.textColor;
            buttonStyle.active.textColor = buttonStyle.normal.textColor;
            GUI.Label(RuntimeUiChrome.CommandCardLabelRect(rect, scale), entry.ShortLabel, buttonStyle);

            // "If there is space left, add details or stats" (2026-08-30), same rule and same
            // shared rect helper as SendDockController.DrawSendButton's creep cards — one line of
            // the codex's own trait text, skipped rather than wrapped when the rect comes back too
            // short to hold it.
            var specialtyRect = RuntimeUiChrome.CommandCardSpecialtyRect(rect, scale);
            if (specialtyRect.height >= 14f * scale
                && CodexScreenView.FindTower(entry.ContentId) is { } towerDefinition)
            {
                metaStyle!.fontSize = Mathf.RoundToInt(8f * scale);
                metaStyle.alignment = TextAnchor.MiddleCenter;
                metaStyle.wordWrap = false;
                metaStyle.clipping = TextClipping.Clip;
                var fitted = RuntimeUiChrome.FitSpecialtyText(CodexScreenView.TowerTraits(towerDefinition), metaStyle, specialtyRect.width);
                if (fitted != null)
                {
                    metaStyle.normal.textColor = isAffordable ? MutedTraitText : DisabledText;
                    GUI.Label(specialtyRect, fitted, metaStyle);
                }
            }

            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = isAffordable
                ? new Color(
                    Mathf.Lerp(displayAccent.r, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.g, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.b, 1f, 0.55f),
                    1f)
                : displayAccent;
            // Explicit rather than assumed: the specialty line above (when drawn) and the rail
            // category rows elsewhere both mutate this shared style's alignment/wrap/clipping.
            metaStyle.alignment = TextAnchor.MiddleCenter;
            metaStyle.wordWrap = false;
            metaStyle.clipping = TextClipping.Overflow;
            GUI.Label(RuntimeUiChrome.CommandCardMetaRect(rect, scale), $"{cost}G", metaStyle);

            return pressed;
        }

        private static Color CategoryAccent(int category) => category switch
        {
            1 => new Color(0.87f, 0.62f, 0.28f),
            2 => new Color(0.45f, 0.78f, 0.36f),
            _ => LTW.UnityClient.Simulation.TowerRolePalette.Arrow
        };

        /// <summary>
        /// Icon for a role. Only the original five have authored icons; the new towers fall back to
        /// the procedural shape closest to their silhouette until real icons are rendered.
        /// </summary>
        private static TowerIconKind TowerIconForRole(int role) => role switch
        {
            1 => TowerIconKind.Control,
            2 => TowerIconKind.Relay,
            3 => TowerIconKind.Pulse,
            4 => TowerIconKind.Prism,
            5 => TowerIconKind.Arrow,
            6 => TowerIconKind.Prism,
            7 => TowerIconKind.Pulse,
            8 => TowerIconKind.Control,
            9 => TowerIconKind.Relay,
            10 => TowerIconKind.Prism,
            11 => TowerIconKind.Arrow,
            12 => TowerIconKind.Relay,
            13 => TowerIconKind.Pulse,
            14 => TowerIconKind.Control,
            15 => TowerIconKind.Arrow,
            _ => TowerIconKind.Arrow
        };

        private static void DrawTowerIcon(Rect rect, TowerIconKind iconKind, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = accent;

            var cx = rect.x + rect.width * 0.5f;
            var cy = rect.y + rect.height * 0.52f;
            var line = Mathf.Max(2f * scale, 1f);

            switch (iconKind)
            {
                case TowerIconKind.Control:
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy - 5f * scale, 14f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy + 5f * scale, 14f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy - 5f * scale, line, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 5f * scale, cy - 5f * scale, line, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 3f * scale, cy - 1f * scale, 6f * scale, 6f * scale), Texture2D.whiteTexture);
                    break;
                case TowerIconKind.Relay:
                    GUI.DrawTexture(new Rect(cx - line * 0.5f, cy - 11f * scale, line, 22f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 6f * scale, cy + 8f * scale, 12f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 4f * scale, cy - 11f * scale, 8f * scale, 8f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 9f * scale, cy - 1f * scale, 4f * scale, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 5f * scale, cy - 1f * scale, 4f * scale, 12f * scale), Texture2D.whiteTexture);
                    break;
                case TowerIconKind.Pulse:
                    GUI.DrawTexture(new Rect(cx - 10f * scale, cy - 7f * scale, 20f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy + 2f * scale, 14f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 4f * scale, cy - 3f * scale, 8f * scale, 8f * scale), Texture2D.whiteTexture);
                    break;
                case TowerIconKind.Prism:
                    GUI.DrawTexture(new Rect(cx - 3f * scale, cy - 13f * scale, 6f * scale, 24f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 8f * scale, cy - 4f * scale, 16f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 5f * scale, cy + 8f * scale, 10f * scale, line), Texture2D.whiteTexture);
                    break;
                default:
                    GUI.DrawTexture(new Rect(cx - line * 0.5f, cy - 12f * scale, line, 24f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 9f * scale, cy - 6f * scale, line, 15f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 7f * scale, cy - 6f * scale, line, 15f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 5f * scale, cy - 11f * scale, 10f * scale, line), Texture2D.whiteTexture);
                    break;
            }

            GUI.color = previousColor;
        }

        private static bool DrawLauncherButton(Rect rect, string label, Color accent, float scale)
        {
            var style = buttonStyle ?? GUI.skin.button;
            buttonStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            return rect.width > rect.height * 1.35f
                ? RuntimeUiChrome.DrawPanelButton(rect, label, accent, scale, style)
                : RuntimeUiChrome.DrawLauncherButton(rect, label, accent, scale, style);
        }

        private string PlacementPreviewText()
        {
            return placementPreview.RejectionReason switch
            {
                CommandRejectionReason.InsufficientGold => "NEED GOLD",
                CommandRejectionReason.CellOccupied => "CELL OCCUPIED",
                CommandRejectionReason.PathBlocked => "PATH BLOCKED",
                CommandRejectionReason.InvalidLane => "OUTSIDE YOUR LINE",
                _ => "CANNOT PLACE"
            };
        }

        private string PlacementRecoveryText()
        {
            return placementPreview.RejectionReason switch
            {
                CommandRejectionReason.InsufficientGold => "Send less or wait for income",
                CommandRejectionReason.CellOccupied => "Pick an empty grid cell",
                CommandRejectionReason.PathBlocked => "Leave a route from spawn to exit",
                CommandRejectionReason.InvalidLane => "Tap inside the highlighted lane",
                _ => "Try a different cell"
            };
        }

        private static void EnsureStyles()
        {
            if (panelStyle is not null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                font = RuntimeUiChrome.SharedFont,
                border = new RectOffset(6, 6, 6, 6),
                margin = ZeroOffset(),
                padding = ZeroOffset()
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = MintSignal }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Cloud }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                margin = ZeroOffset(),
                padding = ZeroOffset(),
                // Background cleared on all three: this style is used via GUI.Label for plain
                // bold text over hand-drawn chrome, and GUI.skin.button's own gray box was
                // rendering behind it — invisible against the old card art's similarly gray
                // nameplate, stark against a flat row background.
                normal = { textColor = Cloud, background = null },
                hover = { textColor = Cloud, background = null },
                active = { textColor = Cloud, background = null }
            };

            metaStyle = new GUIStyle(GUI.skin.label)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };

            // Rail list rows' name text (category/tower name). Deliberately GUI.skin.label, not
            // GUI.skin.button like buttonStyle — see SendDockController's identical field for why
            // a GUI.skin.button copy's background does not actually clear.
            rowNameStyle = new GUIStyle(GUI.skin.label)
            {
                font = RuntimeUiChrome.SharedFont,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };
        }

        /// <summary>
        /// Panel background, delegated to the shared chrome.
        /// </summary>
        /// <remarks>
        /// This used to build its own style from `GUI.skin.box`, which meant the background was
        /// Unity's built-in editor-skin box with a navy tint over it. Three files had an identical
        /// copy of that, and a fourth drew a flat rect with hairline edges instead — so the HUD's
        /// panels disagreed with each other and none of them matched the chamfered buttons.
        /// </remarks>
        private static void DrawPanel(Rect rect, Color color) =>
            RuntimeUiChrome.DrawPanel(rect, color, MobileViewportLayout.UiScale());

        private static void DrawAccent(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static string TowerPurpose(string towerId)
        {
            if (towerId.Contains("control")) return "Area control and clustered pressure";
            if (towerId.Contains("relay") || towerId.Contains("economy")) return "Utility pressure and income support";
            if (towerId.Contains("pulse")) return "Short-range burst against dense pressure";
            if (towerId.Contains("prism")) return "Long-range focus against priority pressure";
            return "Focused single-target defense";
        }

        private static RectOffset ZeroOffset() => new RectOffset(0, 0, 0, 0);

        private enum TowerIconKind
        {
            Arrow,
            Control,
            Relay,
            Pulse,
            Prism
        }

        private bool IsPointerOverRuntimeUi(Vector2 screenPosition)
        {
            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var guiPoint = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            // The send dock is drawn by another component, so this gate has to ask it rather than
            // assume. Without this, every tap on an open dock also landed on the board.
            if (SendDock?.ContainsPoint(guiPoint) == true)
            {
                return true;
            }

            // These three all answer for the launcher strip, which only exists on a phone: on a
            // rail the panel is always open and RAISE/SELL/DONE draw inside it instead (see
            // DrawMultiSelectPanelRail), so the strip's old screen positions are empty on rail and
            // must not swallow board taps that land on them.
            if (!MobileViewportLayout.HasSideRails)
            {
                if (isMultiSelectMode && multiSelection.Count > 0
                    && (MultiSelectRaiseRect(scale, frame).Contains(guiPoint) || MultiSelectSellRect(scale, frame).Contains(guiPoint)))
                {
                    return true;
                }

                if (!isPaletteExpanded && MultiSelectLauncherRect(scale, frame).Contains(guiPoint))
                {
                    return true;
                }

                if (!isMultiSelectMode && TowerPaletteLauncherRect(scale, frame).Contains(guiPoint))
                {
                    return true;
                }
            }

            if (isPaletteExpanded && TowerPalettePanelRect(scale, frame).Contains(guiPoint))
            {
                return true;
            }

            if (isPlacing && PlacementPanelRect(scale, frame).Contains(guiPoint))
            {
                return true;
            }

            return selectedTower is not null && SelectedTowerPanelRect(scale, frame).Contains(guiPoint);
        }

        private static Rect TowerPaletteLauncherRect(float scale, Rect frame)
        {
            var launcherWidth = 76f * scale;
            var launcherHeight = 44f * scale;
            return new Rect(frame.x + 12f * scale, frame.yMax - launcherHeight - MobileViewportLayout.BottomMargin(scale), launcherWidth, launcherHeight);
        }

        /// <summary>
        /// The MULTI toggle, stacked directly above the BUILD launcher.
        /// </summary>
        /// <remarks>
        /// An explicit mode button rather than a long-press or a drag box. Both of those overload a
        /// gesture the board already uses — long-press competes with nothing today but is invisible
        /// until discovered, and a drag would fight board panning. A button costs permanent HUD
        /// space and buys an unambiguous mode the player can see the state of.
        /// </remarks>
        private static Rect MultiSelectLauncherRect(float scale, Rect frame)
        {
            var launcher = TowerPaletteLauncherRect(scale, frame);
            return new Rect(launcher.x, launcher.y - launcher.height - 8f * scale, launcher.width, launcher.height);
        }

        private Rect TowerPalettePanelRect(float scale, Rect frame)
        {
            // On a tablet the palette lives in the left rail, below the HUD stack — the same
            // treatment the send dock got in the right rail (item 14) and for the same reason: the
            // rail sits beside the board, so the panel stops covering lane rows and stops asking the
            // camera to lift. Shares PlacementRailTopInset with DrawPlacementStack's rail rect rather
            // than defining its own, because the two are mutually exclusive in time (DrawTowerPalette
            // returns immediately if isPlacing) and so can safely share the same region.
            if (MobileViewportLayout.HasSideRails)
            {
                var rail = MobileViewportLayout.SideRailRect(rightSide: false);
                var railMargin = MobileViewportLayout.EdgeMargin(scale);
                var top = rail.y + PlacementRailTopInset * scale;
                return new Rect(
                    rail.x + railMargin * 0.5f,
                    top,
                    Mathf.Max(1f, rail.width - railMargin),
                    Mathf.Max(1f, rail.yMax - top - railMargin));
            }

            var width = Mathf.Min(frame.width - 16f * scale, 520f * scale);
            // One height for both states. The picker used to need a taller panel because it stacked
            // three full-width cards; laid out as a row sized to the card art's own aspect it fits
            // inside the same 282 the tower grid uses, so the panel no longer grows and shrinks
            // under the player as they step through it.
            var height = 330f * scale;
            var launcherClearance = 136f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale) - launcherClearance, width, height);
        }

        /// <summary>
        /// Where the placement controls live: a slim bar on a phone, a rail block on a tablet.
        /// </summary>
        /// <remarks>
        /// Was a fixed 360x160 panel at the bottom of the board on every screen, which covered about
        /// four rows of the lane while the player was aiming at a cell. The bar is 46 units tall
        /// instead of 160, and on a screen wide enough for a rail the controls leave the board
        /// entirely.
        ///
        /// The rail block starts below the HUD stack rather than at the rail's top. That stack is
        /// the state cell, four stat cells and four counter lines — about 330 units — and the two
        /// would otherwise draw over each other.
        /// </remarks>
        private static Rect PlacementPanelRect(float scale, Rect frame)
        {
            var margin = MobileViewportLayout.EdgeMargin(scale);

            if (MobileViewportLayout.HasSideRails)
            {
                var rail = MobileViewportLayout.SideRailRect(rightSide: false);
                var top = rail.y + PlacementRailTopInset * scale;
                return new Rect(
                    rail.x + margin * 0.5f,
                    top,
                    Mathf.Max(1f, rail.width - margin),
                    Mathf.Max(1f, Mathf.Min(250f * scale, rail.yMax - top - margin)));
            }

            // Stops short of the SEND launcher rather than running under it. The launcher is
            // 76 units wide against the frame's right edge, and a bar centred on the frame put
            // BUILD directly beneath it — a button drawn first and then covered, which IMGUI
            // resolves by giving the click to whichever drew last.
            var launcherClearance = 96f * scale;
            var height = 76f * scale;
            var left = frame.x + 8f * scale;
            var width = Mathf.Max(1f, frame.width - 8f * scale - launcherClearance - (left - frame.x));
            return new Rect(left, frame.yMax - height - MobileViewportLayout.BottomMargin(scale), width, height);
        }

        /// <summary>Clearance for the HUD readout that occupies the top of the left rail.</summary>
        private const float PlacementRailTopInset = 360f;

        /// <summary>RAISE, in the slot BUILD occupies when multi-select is off.</summary>
        private static Rect MultiSelectRaiseRect(float scale, Rect frame)
        {
            var launcher = TowerPaletteLauncherRect(scale, frame);
            return new Rect(launcher.x, launcher.y, 148f * scale, launcher.height);
        }

        /// <summary>SELL, immediately right of RAISE and clear of the SEND dock's launcher.</summary>
        private static Rect MultiSelectSellRect(float scale, Rect frame)
        {
            var raise = MultiSelectRaiseRect(scale, frame);
            return new Rect(raise.xMax + 8f * scale, raise.y, 148f * scale, raise.height);
        }

        private static Rect SelectedTowerPanelRect(float scale, Rect frame)
        {
            var width = Mathf.Min(frame.width - 16f * scale, 360f * scale);
            var height = 112f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - 112f * scale, width, height);
        }
    }
}
