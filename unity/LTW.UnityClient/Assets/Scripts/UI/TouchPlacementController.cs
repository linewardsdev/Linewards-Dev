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
    public sealed class TouchPlacementController : MonoBehaviour
    {
        private const int LaneWidth = 7;
        private const int LaneLength = 16;
        private const string BuilderSpriteResourcePath = "Art/Builder/Production/Sprites/builder_candidate_v01_trimmed";
        private const string BuilderModelResourcePath = "Prefabs/Builder/Builder_3D";

        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color WardViolet = new(0.608f, 0.424f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);
        private static readonly Color DisabledInk = new(0.22f, 0.25f, 0.32f, 0.88f);
        private static readonly Color DisabledText = new(0.55f, 0.59f, 0.68f, 1f);

        private static GUIStyle? panelStyle;
        private static GUIStyle? titleStyle;
        private static GUIStyle? bodyStyle;
        private static GUIStyle? buttonStyle;
        private static GUIStyle? metaStyle;

        [SerializeField]
        private Camera inputCamera = null!;

        [SerializeField]
        private UnityCommandAdapter commandAdapter = null!;

        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        [SerializeField]
        private SendDockController sendDockController = null!;

        [SerializeField]
        private PlacementFeedbackView feedbackView = null!;

        [SerializeField]
        private GameObject ghost = null!;
        private readonly System.Collections.Generic.Dictionary<string, GameObject?> ghostModels = new();
        private TowerVisualLibrary? towerVisualLibrary;
        private Material? ghostMaterial;

        private GameObject builderAvatar = null!;
        private SpriteRenderer? builderAvatarSprite;
        private Animator? builderAvatarAnimator;

        private const float BuilderWalkSpeed = 4.5f;
        private const float BuilderWalkBobAmplitude = 0.05f;
        private const float BuilderWalkBobFrequency = 9f;
        private const float BuilderWalkTurnDegreesPerSecond = 720f;

        [SerializeField]
        private bool showPlacementReadout = true;

        private readonly List<GameObject> selectionRings = new();

        private bool isPlacing;
        private bool isPaletteExpanded;
        private int selectedTowerRole;
        private int lastSelectedTowerRole;
        private int highlightedTowerRole = -1;
        private int selectedTowerCategory = -1;
        private Vector2Int selectedCell;
        private TowerCombatState? selectedTower;

        /// <summary>
        /// Towers picked while MULTI is on, in tap order.
        /// </summary>
        /// <remarks>
        /// Held separately from <see cref="selectedTower"/> rather than replacing it. Single select
        /// carries a tower's whole identity — name, purpose, cell, tier — and a batch panel cannot
        /// show any of that meaningfully for five towers at once, so the two panels stay distinct
        /// and so does the state behind them.
        /// </remarks>
        private readonly List<TowerCombatState> multiSelection = new();

        private bool isMultiSelectMode;

        /// <summary>How long after a tap on a tower a second tap on the SAME tower counts as a double.</summary>
        /// <remarks>
        /// 0.35s is the usual mobile double-tap window — long enough not to punish a deliberate,
        /// unhurried second tap, short enough that two separate decisions a third of a second apart
        /// are not silently merged into one.
        /// </remarks>
        private const float DoubleTapSeconds = 0.35f;

        private Vector2Int lastTowerTapCell;

        /// <summary>
        /// When the last tower tap landed, on the UNSCALED clock.
        /// </summary>
        /// <remarks>
        /// Unscaled deliberately. A double tap is a fact about the player's thumb, not about game
        /// time, and <c>Time.time</c> is not a safe proxy here — LocalPlaytestBatchRunner drives the
        /// editor at <c>timeScale</c> 20, which would shrink a 0.35s window to 17ms of real time and
        /// make the gesture impossible to perform.
        ///
        /// Starts at negative infinity so the very first tap of a session cannot pair with the zero
        /// value a plain float would have started at.
        /// </remarks>
        private float lastTowerTapAt = float.NegativeInfinity;

        /// <summary>Set by the first SELL tap, cleared by anything else. See DrawMultiSelectActions.</summary>
        private bool sellArmed;
        private VerticalSliceCommandResult placementPreview = VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);

        public bool IsPlacing => isPlacing;

        public bool IsTowerPaletteExpanded => isPaletteExpanded;

        public void Initialize(Camera camera, UnityCommandAdapter adapter, PlacementFeedbackView feedback, GameObject placementGhost)
        {
            inputCamera = camera;
            commandAdapter = adapter;
            feedbackView = feedback;
            ghost = placementGhost;
            selectedCell = DefaultBuilderCell();
            EnsureBuilderAvatar();
            UpdateBuilderAvatar();
        }

        public void BeginTowerPlacement() => BeginTowerPlacement(lastSelectedTowerRole);

        public void BeginControlTowerPlacement() => BeginTowerPlacement(1);

        public void BeginUtilityTowerPlacement() => BeginTowerPlacement(2);

        public void BeginPulseTowerPlacement() => BeginTowerPlacement(3);

        public void BeginPrismTowerPlacement() => BeginTowerPlacement(4);

        private void BeginTowerPlacement(int towerRole)
        {
            CloseSendDock();
            isPlacing = true;
            isPaletteExpanded = false;
            selectedTowerRole = towerRole;
            lastSelectedTowerRole = towerRole;
            highlightedTowerRole = towerRole;
            ghost.SetActive(true);
            MoveGhost();
            selectedTower = null;
            HideSelectionRing();
            feedbackView.Clear();
        }

        public void CloseBottomPanelsForSend()
        {
            isPaletteExpanded = false;
            selectedTower = null;

            // Multi-select ends here too. Its RAISE and SELL buttons live in the launcher strip the
            // dock covers, so leaving the mode on while sending left it active but unreachable —
            // board taps kept toggling towers into a batch the player could not see or act on.
            SetMultiSelectMode(false);
            HideSelectionRing();

            if (isPlacing)
            {
                CancelPlacement(false);
            }
        }

        public void NudgeUp() => Nudge(Vector2Int.down);

        public void NudgeDown() => Nudge(Vector2Int.up);

        public void NudgeLeft() => Nudge(Vector2Int.left);

        public void NudgeRight() => Nudge(Vector2Int.right);

        public void CancelPlacement() => CancelPlacement(true);

        private void CancelPlacement(bool clearFeedback)
        {
            isPlacing = false;
            ghost.SetActive(false);
            UpdateBuilderAvatar();
            if (clearFeedback)
            {
                feedbackView.Clear();
            }
        }

        public void ConfirmPlacement()
        {
            if (!isPlacing)
            {
                return;
            }

            if (!IsSelectedCellInBounds())
            {
                feedbackView.ShowRejected(CommandRejectionReason.InvalidLane);
                placementPreview = VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);
                UpdateGhostColor();
                return;
            }

            var result = commandAdapter.PlaceTowerByRole(selectedTowerRole, selectedCell.x, selectedCell.y);
            if (result.Accepted)
            {
                feedbackView.ShowAccepted(SelectedTowerName() + " placed");
                lastSelectedTowerRole = selectedTowerRole;
                // Keep placement active with the selected tower so the compact tower-specific panel
                // remains available. Players can tap ALL there to swap tower types.
                MoveGhost();
                return;
            }

            if (result.RejectionReason == CommandRejectionReason.InsufficientGold)
            {
                feedbackView.ShowRejected(result.RejectionReason, SelectedTowerCost(), CurrentPlayerGold());
            }
            else
            {
                feedbackView.ShowRejected(result.RejectionReason);
            }
            RefreshPlacementPreview();
        }

        public void SellLastTower()
        {
            RefreshSelectedTowerFromSnapshot();
            var result = selectedTower is not null
                ? commandAdapter.SellTowerAt(selectedTower.Position.X, selectedTower.Position.Y)
                : commandAdapter.SellLastSampleTower();
            if (result.Accepted)
            {
                selectedTower = null;
                HideSelectionRing();
                feedbackView.ShowEconomy("Tower sold");
                return;
            }

            feedbackView.ShowRejected(result.RejectionReason);
        }

        private void Update()
        {
            TickBuilderWalk();

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (IsPointerOverRuntimeUi(Input.mousePosition))
            {
                return;
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            // No camera tagged MainCamera yet (e.g. a scene still loading) — skip this tap rather
            // than dereference null, matching how UnityVerticalSliceRenderer.ConfigureDefaultCamera
            // handles the same case (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items).
            if (inputCamera == null)
            {
                return;
            }

            var ray = inputCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var distance))
            {
                return;
            }

            var hit = ray.GetPoint(distance);
            var hitCell = ClampToLane(new Vector2Int(Mathf.RoundToInt(hit.x), WorldZToGridY(hit.z)));
            if (isPlacing)
            {
                selectedCell = hitCell;
                MoveGhost();
                return;
            }

            if (SelectTowerAt(hitCell))
            {
                return;
            }

            selectedTower = null;
            if (!isMultiSelectMode)
            {
                HideSelectionRing();
            }

            selectedCell = hitCell;
            UpdateBuilderAvatar();
        }

        private void OnGUI()
        {
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

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            DrawTowerPalette(scale);
            if (IsSendDockExpanded())
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

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            titleStyle!.fontSize = Mathf.RoundToInt(17f * scale);
            titleStyle.normal.textColor = accent;
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            bodyStyle.normal.textColor = Cloud;

            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 9f * scale, rect.width - 24f * scale, 24f * scale), $"{SelectedTowerName().ToUpperInvariant()}  {SelectedTowerCost()}G", titleStyle);

            var placementLine = placementPreview.Accepted ? $"CELL {selectedCell.x}, {selectedCell.y} READY" : PlacementPreviewText();
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 35f * scale, rect.width - 24f * scale, 20f * scale), placementLine, bodyStyle);
            var actionHint = placementPreview.Accepted ? "BUILDER ONLINE  •  TAP BUILD" : PlacementRecoveryText();
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 57f * scale, rect.width - 24f * scale, 20f * scale), actionHint, bodyStyle);

            // The quick-switch strip shows the category the SELECTED tower belongs to, not a fixed
            // list. It used to hardcode the five arcane roles, so choosing a Foundry or Grove tower
            // left the strip offering Arrow/Control/Relay/Pulse/Prism — reported from play as
            // "when you choose a cat 3 or cat 2 tower, the quick switch buttons are still cat 1".
            // Switching to a neighbour within the line you are already building is the point of the
            // strip; jumping you back to arcane was the opposite.
            var switchY = rect.y + 82f * scale;
            var switchHeight = 24f * scale;
            var switchGap = 4f * scale;
            var switchEntries = CategoryEntriesByCost(LTW.UnityClient.Simulation.TowerCatalog.ForRole(selectedTowerRole).Category);
            if (switchEntries.Count > 0)
            {
                var switchWidth = (rect.width - 24f * scale - switchGap * (switchEntries.Count - 1)) / switchEntries.Count;
                var switchX = rect.x + 12f * scale;
                for (var index = 0; index < switchEntries.Count; index++)
                {
                    var entry = switchEntries[index];
                    if (DrawPlacementSwitchButton(new Rect(switchX, switchY, switchWidth, switchHeight), entry.ShortLabel, entry.Role, entry.Accent, scale))
                    {
                        BeginTowerPlacement(entry.Role);
                        return;
                    }

                    switchX += switchWidth + switchGap;
                }
            }

            var buttonHeight = 30f * scale;
            var gap = 6f * scale;
            var allWidth = 64f * scale;
            var buildWidth = 104f * scale;
            var cancelWidth = 86f * scale;
            var totalWidth = allWidth + buildWidth + cancelWidth + gap * 2f;
            var buttonY = rect.yMax - 38f * scale;
            var buttonX = rect.center.x - totalWidth * 0.5f;

            if (DrawLauncherButton(new Rect(buttonX, buttonY, allWidth, buttonHeight), "ALL", SignalGold, scale))
            {
                CancelPlacement(false);
                OpenTowerPalette();
                return;
            }

            buttonX += allWidth + gap;
            if (DrawLauncherButton(new Rect(buttonX, buttonY, buildWidth, buttonHeight), "BUILD", placementPreview.Accepted ? MintSignal : Danger, scale))
            {
                ConfirmPlacement();
                return;
            }

            buttonX += buildWidth + gap;
            if (DrawLauncherButton(new Rect(buttonX, buttonY, cancelWidth, buttonHeight), "CANCEL", Danger, scale))
            {
                CancelPlacement();
                return;
            }
        }

        private bool DrawPlacementSwitchButton(Rect rect, string label, int towerRole, Color accent, float scale)
        {
            buttonStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            return RuntimeUiChrome.DrawControlButton(rect, label, accent, selectedTowerRole == towerRole, scale, buttonStyle);
        }

        private bool SelectTowerAt(Vector2Int cell)
        {
            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }

            selectedTower = null;
            if (!isMultiSelectMode)
            {
                HideSelectionRing();
            }

            var snapshot = simulationDriver?.LatestSnapshot;
            if (snapshot is null)
            {
                return false;
            }

            foreach (var tower in snapshot.Towers)
            {
                if (tower.OwnerId.Equals(simulationDriver.LocalPlayerId) && tower.LaneId.Equals(simulationDriver.LocalPlayerLaneId) && tower.Position.X == cell.x && tower.Position.Y == cell.y)
                {
                    // Checked before the multi-select branch below, or the second tap of the gesture
                    // would be eaten by ToggleInMultiSelection and read as "deselect this one".
                    if (ConsumeDoubleTap(cell))
                    {
                        return SelectEveryTowerOfType(tower);
                    }

                    if (isMultiSelectMode)
                    {
                        return ToggleInMultiSelection(tower);
                    }

                    selectedTower = tower;
                    UpdateSelectionRing(tower);
                    HideBuilderAvatar();
                    ghost.SetActive(false);
                    feedbackView.ShowAccepted(TowerRoleName(tower.TowerId.Value) + " selected");
                    return true;
                }
            }

            return false;
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

        private List<GridPosition> SelectedPositions()
        {
            var positions = new List<GridPosition>(multiSelection.Count);
            foreach (var tower in multiSelection)
            {
                positions.Add(tower.Position);
            }

            return positions;
        }

        private void RaiseSelection(BatchUpgradeQuote quote)
        {
            sellArmed = false;
            if (quote.Affordable <= 0)
            {
                feedbackView.ShowRejected(
                    quote.HasWork ? CommandRejectionReason.InsufficientGold : CommandRejectionReason.InvalidTier,
                    quote.TotalCost,
                    CurrentPlayerGold());
                return;
            }

            var outcome = commandAdapter!.UpgradeSelection(SelectedPositions());
            feedbackView.ShowAccepted(outcome.IsPartial
                ? $"Raised {outcome.Upgraded} of {outcome.Eligible} for {outcome.GoldSpent}G — out of gold"
                : $"Raised {outcome.Upgraded} for {outcome.GoldSpent}G");
            RefreshSelectionFromSnapshot();
        }

        private void SellSelection(BatchSellQuote quote)
        {
            if (!quote.HasWork)
            {
                return;
            }

            if (!sellArmed)
            {
                sellArmed = true;
                // Amber, not red: this is a confirmation prompt, not a refusal, and it has to state
                // the consequence rather than just asking "are you sure".
                feedbackView.ShowEconomy($"Tap SELL again to sell {quote.Towers} for {quote.Refund}G");
                return;
            }

            sellArmed = false;
            var outcome = commandAdapter!.SellSelection(SelectedPositions());
            feedbackView.ShowAccepted($"Sold {outcome.Sold} for {outcome.Refund}G");
            multiSelection.Clear();
            HideSelectionRing();
        }

        /// <summary>
        /// Re-reads the selected towers from the snapshot after a batch changed them.
        /// </summary>
        /// <remarks>
        /// The list holds snapshot values, so towers that were just raised still carry their old
        /// tier. Without this the panel would keep offering to raise towers it had already raised.
        /// </remarks>
        private void RefreshSelectionFromSnapshot()
        {
            if (simulationDriver?.LatestSnapshot is not { } snapshot)
            {
                return;
            }

            for (var index = 0; index < multiSelection.Count; index++)
            {
                var position = multiSelection[index].Position;
                foreach (var tower in snapshot.Towers)
                {
                    if (tower.OwnerId.Equals(simulationDriver.LocalPlayerId)
                        && tower.LaneId.Equals(simulationDriver.LocalPlayerLaneId)
                        && tower.Position.Equals(position))
                    {
                        multiSelection[index] = tower;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Whether this tap is the second of a double tap on the same cell, consuming it either way.
        /// </summary>
        /// <remarks>
        /// The cell has to match, not just the timing. Two quick taps on two DIFFERENT towers are two
        /// deliberate single selections, and treating them as a double would replace the player's
        /// second choice with every tower sharing its type.
        ///
        /// On a hit the timestamp is reset rather than rolled forward, so three fast taps are one
        /// double followed by a fresh single. Rolling it forward would make taps 2-and-3 fire the
        /// gesture a second time, and on an already-complete selection that reads as a dead tap.
        /// </remarks>
        private bool ConsumeDoubleTap(Vector2Int cell)
        {
            var now = Time.unscaledTime;
            var isDouble = cell == lastTowerTapCell && now - lastTowerTapAt <= DoubleTapSeconds;
            lastTowerTapAt = isDouble ? float.NegativeInfinity : now;
            lastTowerTapCell = cell;
            return isDouble;
        }

        /// <summary>
        /// Double tap: select every tower of the tapped tower's type, and turn MULTI on to act on them.
        /// </summary>
        /// <remarks>
        /// The point is bulk upgrades. Raising eight Arrow Wards one at a time means eight taps to
        /// select and eight panels to confirm; this makes it one gesture and one RAISE, which the
        /// batch quote already prices and gold-limits.
        ///
        /// Additive rather than replacing, and that is the one real design choice here. A player who
        /// has already picked towers by hand and then double taps has ASKED for more, not for their
        /// work to be discarded — and because it unions, double tapping two types in turn builds a
        /// mixed selection, which is the natural way to raise a whole defence. It also makes the
        /// gesture idempotent: the first tap of the pair may have toggled this tower out of the
        /// selection, and the union puts it back, so the result does not depend on whether the tower
        /// happened to be selected beforehand.
        ///
        /// Scoped to the local player's own lane, matching <see cref="SelectTowerAt"/> — the batch
        /// commands can only act on towers the player owns, so selecting anything else would build a
        /// selection the RAISE button then silently ignored.
        /// </remarks>
        private bool SelectEveryTowerOfType(TowerCombatState tower)
        {
            if (simulationDriver?.LatestSnapshot is not { } snapshot)
            {
                return false;
            }

            if (!isMultiSelectMode)
            {
                // Clears any single selection and stands down placement, so the two modes never both
                // think they own the next tap.
                SetMultiSelectMode(true);
            }

            sellArmed = false;
            var ofType = 0;
            foreach (var candidate in snapshot.Towers)
            {
                if (!candidate.OwnerId.Equals(simulationDriver.LocalPlayerId)
                    || !candidate.LaneId.Equals(simulationDriver.LocalPlayerLaneId)
                    || !candidate.TowerId.Equals(tower.TowerId))
                {
                    continue;
                }

                ofType++;
                if (!multiSelection.Any(selected => selected.Position.Equals(candidate.Position)))
                {
                    multiSelection.Add(candidate);
                }
            }

            ShowSelectionRings(multiSelection);
            ghost.SetActive(false);
            HideBuilderAvatar();

            // Reports the type count AND the total, because after a second double tap on another type
            // those differ, and a bare "8 selected" would leave the player unsure whether the first
            // batch survived.
            var name = TowerRoleName(tower.TowerId.Value).ToUpperInvariant();
            feedbackView.ShowAccepted(multiSelection.Count == ofType
                ? $"All {ofType} {name} selected"
                : $"All {ofType} {name} — {multiSelection.Count} selected");
            return true;
        }

        /// <summary>
        /// Adds a tower to the multi-selection, or takes it out if it is already in.
        /// </summary>
        /// <remarks>
        /// Always returns true, including when it REMOVES one. The caller treats false as "no tower
        /// here, treat it as a tap on empty board" and would clear the whole selection — so
        /// deselecting one tower would wipe the other four.
        /// </remarks>
        private bool ToggleInMultiSelection(TowerCombatState tower)
        {
            sellArmed = false;
            var existing = multiSelection.FindIndex(candidate => candidate.Position.Equals(tower.Position));
            if (existing >= 0)
            {
                multiSelection.RemoveAt(existing);
            }
            else
            {
                multiSelection.Add(tower);
            }

            ShowSelectionRings(multiSelection);
            ghost.SetActive(false);
            HideBuilderAvatar();
            feedbackView.ShowAccepted(multiSelection.Count == 0 ? "Selection cleared" : $"{multiSelection.Count} selected");
            return true;
        }

        private void SetMultiSelectMode(bool enabled)
        {
            isMultiSelectMode = enabled;
            multiSelection.Clear();
            sellArmed = false;
            selectedTower = null;
            HideSelectionRing();
            if (enabled)
            {
                // A placement in flight would fight the same taps.
                isPlacing = false;
                ghost.SetActive(false);
                HideBuilderAvatar();
            }
        }

        /// <summary>
        /// Drops towers that no longer exist from the selection.
        /// </summary>
        /// <remarks>
        /// A selected tower can leave the board without the player touching it — sold from the
        /// batch itself, or destroyed. The held TowerCombatState is a snapshot value, so a stale
        /// entry would keep drawing a ring over an empty cell and keep being counted in the totals.
        /// </remarks>
        private void PruneMultiSelection()
        {
            if (multiSelection.Count == 0 || simulationDriver?.LatestSnapshot is not { } snapshot)
            {
                return;
            }

            var removed = multiSelection.RemoveAll(selected => !snapshot.Towers.Any(tower =>
                tower.OwnerId.Equals(simulationDriver.LocalPlayerId)
                && tower.LaneId.Equals(simulationDriver.LocalPlayerLaneId)
                && tower.Position.Equals(selected.Position)));
            if (removed > 0)
            {
                ShowSelectionRings(multiSelection);
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
            if (DrawLauncherButton(new Rect(rect.xMax - 142f * scale, rect.y + 7f * scale, 78f * scale, 30f * scale), "BUILD", SignalGold, scale))
            {
                selectedTower = null;
                HideSelectionRing();
                OpenTowerPalette();
                return;
            }
            if (RuntimeUiChrome.DrawPanelButton(new Rect(rect.xMax - 56f * scale, rect.y + 7f * scale, 40f * scale, 30f * scale), "X", accent, scale, buttonStyle ?? GUI.skin.button))
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
            var upgradeRect = new Rect(rect.x + rect.width - 170f * scale, rect.y + 36f * scale, 78f * scale, 42f * scale);
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

        private void Nudge(Vector2Int delta)
        {
            if (!isPlacing)
            {
                return;
            }

            selectedCell += delta;
            selectedCell = ClampToLane(selectedCell);
            MoveGhost();
        }

        private static Vector2Int DefaultBuilderCell() => new(LaneWidth / 2, LaneLength - 3);

        private static Vector2Int ClampToLane(Vector2Int cell) =>
            new(
                Mathf.Clamp(cell.x, 0, LaneWidth - 1),
                Mathf.Clamp(cell.y, 0, LaneLength - 1));

        private static int WorldZToGridY(float z) => LaneLength - 1 - Mathf.RoundToInt(z);

        private static Vector3 GridToWorld(Vector2Int cell, float y) => new(cell.x, y, LaneLength - 1 - cell.y);

        private void MoveGhost()
        {
            // The builder now walks along with tower placement instead of vanishing for it, so
            // the ghost preview shouldn't appear at the destination the instant a new cell is
            // picked either — it stays hidden until the builder actually arrives there
            // (TickBuilderWalk re-enables it once the walk finishes), reading as the builder
            // setting the tower down rather than a preview floating in ahead of them.
            UpdateBuilderAvatar();
            ghost.SetActive(false);
            ghost.transform.position = GridToWorld(selectedCell, 0.6f);
            ConfigurePlacementGhostVisual();
            // A resolved model already carries its profile's own scale, so the root has to stay at
            // one or the two multiply and the preview comes out larger than the placed tower.
            ghost.transform.localScale = HasGhostModel() ? Vector3.one : SelectedTowerGhostScale();
            RefreshPlacementPreview();
        }

        private void RefreshPlacementPreview()
        {
            if (!IsSelectedCellInBounds())
            {
                placementPreview = VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);
                UpdateGhostColor();
                return;
            }

            placementPreview = commandAdapter.PreviewTowerByRole(selectedTowerRole, selectedCell.x, selectedCell.y);

            UpdateGhostColor();
        }

        private void UpdateGhostColor()
        {
            var color = placementPreview.Accepted ? SelectedTowerAccent() : Danger;
            color.a = placementPreview.Accepted ? 0.74f : 0.86f;
            foreach (var ghostRenderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                ghostRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Shows a translucent copy of the actual tower model for the selected role.
        /// </summary>
        /// <remarks>
        /// The preview used to be assembled from tinted cylinders and cubes — a stack of coloured
        /// primitives roughly standing in for each tower's proportions. That was reasonable when
        /// the towers themselves were primitives, but they are 3D models now, so the preview was
        /// showing a shape that no longer matched what you would get. Instantiating the real
        /// prefab means the silhouette under your finger is the silhouette you are about to place.
        /// </remarks>
        private void ConfigurePlacementGhostVisual()
        {
            var roleId = SelectedTowerRoleId();
            foreach (var cached in ghostModels)
            {
                if (cached.Value != null)
                {
                    cached.Value.SetActive(cached.Key == roleId);
                }
            }

            if (ghostModels.TryGetValue(roleId, out var existing) && existing != null)
            {
                return;
            }

            var model = BuildGhostModel(roleId);
            ghostModels[roleId] = model;
            if (model == null)
            {
                // No library or prefab: fall back to the plain ghost root so placement still has
                // something to point at rather than nothing at all.
                EnsureGhostFallback(true);
                return;
            }

            EnsureGhostFallback(false);
        }

        private GameObject? BuildGhostModel(string roleId)
        {
            var library = towerVisualLibrary != null
                ? towerVisualLibrary
                : towerVisualLibrary = Resources.Load<TowerVisualLibrary>("TowerVisualLibrary");
            var profile = library != null ? library.FindProfile($"tower.{roleId}") : null;
            if (profile == null || profile.Prefab == null)
            {
                return null;
            }

            var model = Instantiate(profile.Prefab, ghost.transform);
            model.name = $"GhostModel_{roleId}";
            model.transform.localPosition = Vector3.up * profile.Lift;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = profile.HasScale ? profile.Scale : Vector3.one;

            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            // One shared unlit translucent material across the whole model. Keeping the tower's
            // own materials would make the preview look like a finished tower already standing
            // there, which is exactly the confusion a ghost has to avoid.
            foreach (var modelRenderer in model.GetComponentsInChildren<Renderer>(true))
            {
                modelRenderer.sharedMaterial = GhostMaterial();
                modelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                modelRenderer.receiveShadows = false;
            }

            return model;
        }

        private bool HasGhostModel() =>
            ghostModels.TryGetValue(SelectedTowerRoleId(), out var model) && model != null;

        private Material GhostMaterial()
        {
            if (ghostMaterial != null)
            {
                return ghostMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            ghostMaterial = new Material(shader) { name = "PlacementGhost" };
            return ghostMaterial;
        }

        /// <summary>Shows or hides the ghost root's own renderer, used only when no model resolves.</summary>
        private void EnsureGhostFallback(bool visible)
        {
            if (ghost.TryGetComponent<Renderer>(out var rootRenderer))
            {
                rootRenderer.enabled = visible;
            }
        }

        private void EnsureBuilderAvatar()
        {
            if (builderAvatar != null)
            {
                return;
            }

            builderAvatar = new GameObject("Builder Avatar");
            builderAvatar.transform.SetParent(transform, false);
            builderAvatar.SetActive(false);

            var model = Resources.Load<GameObject>(BuilderModelResourcePath);
            if (model != null)
            {
                // Rigged biped (Meshy) with a real Walk cycle — TickBuilderWalk drives its
                // Animator's "Walking" bool instead of the old bob-only primitive avatar.
                var instance = Instantiate(model, builderAvatar.transform);
                instance.name = "Model";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                // Raw mesh stands ~2.2 units tall. 0.4 put it at ~0.88 world units — under a single
                // board cell, which read as a dropped prop rather than as the unit doing the work,
                // especially next to towers that occupy most of their own cell. 0.6 puts it at
                // ~1.32, so it clears a cell and is legible at the tilted match camera's angle
                // without overtopping the towers it builds. One number to dial if it wants to be
                // larger still.
                instance.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                builderAvatarAnimator = instance.GetComponentInChildren<Animator>(true);
                return;
            }

            // Fallback if the 3D model asset is missing for any reason (e.g. a build stripped
            // Resources content it shouldn't have) — the original primitive-and-sprite avatar.
            builderAvatar.transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);
            CreateBuilderPart("Body", PrimitiveType.Capsule, new Vector3(0f, 0.34f, 0f), new Vector3(0.28f, 0.34f, 0.28f));
            CreateBuilderPart("Pack", PrimitiveType.Cube, new Vector3(0f, 0.38f, -0.2f), new Vector3(0.25f, 0.3f, 0.12f));
            CreateBuilderPart("Visor", PrimitiveType.Cube, new Vector3(0f, 0.53f, 0.18f), new Vector3(0.2f, 0.08f, 0.08f));
            CreateBuilderSpriteVisual();
        }

        private void HideBuilderAvatar()
        {
            if (builderAvatar != null)
            {
                builderAvatar.SetActive(false);
            }
        }

        /// <summary>
        /// Walks the builder avatar toward whatever cell is currently selected instead of
        /// teleporting it there — called every frame (not gated behind input, unlike the rest of
        /// <see cref="Update"/>) so the walk keeps progressing across frames with no clicks.
        /// A simple bob (no leg geometry exists on this primitive-built avatar) plus turning to
        /// face the direction of travel is enough to read as an actual walk rather than a slide.
        /// </summary>
        private void TickBuilderWalk()
        {
            if (builderAvatar == null || !builderAvatar.activeSelf)
            {
                return;
            }

            var target = BuilderGroundPosition(selectedCell);
            var current = builderAvatar.transform.position;
            var flatCurrent = new Vector3(current.x, 0f, current.z);
            var flatTarget = new Vector3(target.x, 0f, target.z);
            var toTarget = flatTarget - flatCurrent;
            var distance = toTarget.magnitude;

            if (distance < 0.02f)
            {
                builderAvatar.transform.position = target;
                builderAvatarAnimator?.SetBool("Walking", false);
                if (isPlacing)
                {
                    // Arrived — only now does the tower ghost actually appear, settled exactly
                    // onto the real cell, reading as the builder having walked over and set it
                    // down rather than a preview that was already floating there.
                    ghost.transform.position = GridToWorld(selectedCell, 0.6f);
                    ghost.SetActive(true);
                }

                return;
            }

            builderAvatarAnimator?.SetBool("Walking", true);
            var direction = toTarget / distance;
            var step = Mathf.Min(distance, BuilderWalkSpeed * Time.deltaTime);
            var moved = flatCurrent + direction * step;

            // The rigged model's own Walk clip already animates a real up-down bounce from its
            // leg motion — layering the old procedural sine bob on top (built for the legless
            // primitive avatar) would double up as an odd extra wobble, so it's skipped whenever
            // a real Animator is driving the character.
            var bob = builderAvatarAnimator == null
                ? Mathf.Abs(Mathf.Sin(Time.time * BuilderWalkBobFrequency)) * BuilderWalkBobAmplitude
                : 0f;
            builderAvatar.transform.position = new Vector3(moved.x, target.y + bob, moved.z);

            var desiredRotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);
            builderAvatar.transform.rotation = Quaternion.RotateTowards(
                builderAvatar.transform.rotation,
                desiredRotation,
                BuilderWalkTurnDegreesPerSecond * Time.deltaTime);
        }

        private void CreateBuilderPart(string partName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(primitiveType);
            if (part == null || builderAvatar == null)
            {
                return;
            }

            part.name = partName;
            part.transform.SetParent(builderAvatar.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
        }

        private static Vector3 BuilderRestOffset => new(-0.48f, 0f, 0.24f);

        private Vector3 BuilderGroundPosition(Vector2Int cell) => GridToWorld(cell, 0.02f) + BuilderRestOffset;

        private void UpdateBuilderAvatar()
        {
            if (builderAvatar == null)
            {
                EnsureBuilderAvatar();
            }

            if (builderAvatar == null)
            {
                return;
            }

            // Only snap instantly the first time the avatar appears (there's no sensible "previous
            // cell" to walk in from yet). Every cell change after that is picked up by
            // TickBuilderWalk instead, which walks the avatar across the board rather than
            // teleporting it — this call just needs to make sure it's visible/coloured.
            var alreadyVisible = builderAvatar.activeSelf;
            if (!alreadyVisible)
            {
                builderAvatar.transform.position = BuilderGroundPosition(selectedCell);
            }

            builderAvatar.SetActive(true);

            // The rigged model carries its own painted PBR material — recolouring it the way the
            // primitive/sprite fallback does below would just wash out its texture with a flat
            // tint, so it's left alone entirely.
            if (builderAvatarAnimator != null)
            {
                return;
            }

            var accent = SelectedTowerAccent();
            accent.a = 1f;
            foreach (var part in builderAvatar.GetComponentsInChildren<Renderer>(true))
            {
                if (part == builderAvatarSprite)
                {
                    part.enabled = true;
                    continue;
                }

                if (builderAvatarSprite != null)
                {
                    part.enabled = false;
                    continue;
                }

                part.material.color = part.gameObject.name switch
                {
                    "Body" => Cloud,
                    "Pack" => PanelInk,
                    _ => accent
                };
            }
        }

        private void CreateBuilderSpriteVisual()
        {
            var sprite = Resources.Load<Sprite>(BuilderSpriteResourcePath);
            if (sprite == null)
            {
                return;
            }

            var plate = new GameObject("AIPlateVisual");
            plate.transform.SetParent(builderAvatar.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.27f, 0.04f);
            plate.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plate.transform.localScale = new Vector3(0.145f, 0.145f, 1f);

            builderAvatarSprite = plate.AddComponent<SpriteRenderer>();
            builderAvatarSprite.sprite = sprite;
            builderAvatarSprite.sortingOrder = 12;

            foreach (var part in builderAvatar.GetComponentsInChildren<Renderer>(true))
            {
                if (part != builderAvatarSprite)
                {
                    part.enabled = false;
                }
            }
        }

        private void UpdateSelectionRing(TowerCombatState tower) => ShowSelectionRings(new[] { tower });

        /// <summary>
        /// Puts a range ring under every selected tower, growing the pool as the selection does.
        /// </summary>
        /// <remarks>
        /// Was a single GameObject, which was right while exactly one tower could be selected. With
        /// a multi-selection the ring has to be per tower or the board shows one highlighted tower
        /// out of five and the player has no way to see what a batch is about to act on.
        /// </remarks>
        private void ShowSelectionRings(IReadOnlyList<TowerCombatState> towers)
        {
            for (var index = 0; index < towers.Count; index++)
            {
                if (index >= selectionRings.Count)
                {
                    var created = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    created.name = "SelectedTowerRangeRing";
                    selectionRings.Add(created);
                }

                var ring = selectionRings[index];
                var tower = towers[index];
                ring.SetActive(true);
                ring.transform.position = GridToWorld(new Vector2Int(tower.Position.X, tower.Position.Y), 0.06f);
                ring.transform.localScale = TowerSelectionRingScale(tower.TowerId.Value);
                ring.GetComponent<Renderer>().material.color = TowerAccent(tower.TowerId.Value);
            }

            for (var index = towers.Count; index < selectionRings.Count; index++)
            {
                selectionRings[index].SetActive(false);
            }
        }

        private void HideSelectionRing()
        {
            for (var index = 0; index < selectionRings.Count; index++)
            {
                if (selectionRings[index] != null)
                {
                    selectionRings[index].SetActive(false);
                }
            }
        }

        private Vector3 SelectedTowerGhostScale()
        {
            return SelectedTowerRoleId() switch
            {
                "control" => new Vector3(0.82f, 0.46f, 0.82f),
                "relay" => new Vector3(0.52f, 0.52f, 0.52f),
                "pulse" => new Vector3(0.86f, 0.44f, 0.86f),
                "prism" => new Vector3(0.48f, 1.0f, 0.48f),
                _ => new Vector3(0.62f, 0.78f, 0.62f)
            };
        }

        private string SelectedTowerRoleId()
        {
            return LTW.UnityClient.Simulation.TowerCatalog.ForRole(selectedTowerRole).RoleId;
        }

        // Unlike name/colour below, ring scale has no per-tower value in TowerCatalog to fall back to
        // — the fallback here is a deliberate shared default size for the 10 towers added since this
        // was written, not a wrong answer borrowed from Arrow's branch.
        private static Vector3 TowerSelectionRingScale(string towerId)
        {
            if (towerId.Contains("control")) return new Vector3(1.42f, 0.03f, 1.42f);
            if (towerId.Contains("relay") || towerId.Contains("economy")) return new Vector3(1.18f, 0.03f, 1.18f);
            if (towerId.Contains("pulse")) return new Vector3(1.62f, 0.03f, 1.62f);
            if (towerId.Contains("prism")) return new Vector3(2.12f, 0.03f, 2.12f);
            return new Vector3(1.28f, 0.03f, 1.28f);
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

            if (!isPaletteExpanded && IsSendDockExpanded())
            {
                return;
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

                // Only offered with the palette closed: with it open the palette covers the board
                // the taps would have to land on.
                if (DrawLauncherButton(MultiSelectLauncherRect(scale, frame), isMultiSelectMode ? "DONE" : "MULTI", isMultiSelectMode ? SignalGold : MintSignal, scale))
                {
                    SetMultiSelectMode(!isMultiSelectMode);
                }

                return;
            }

            var rect = TowerPalettePanelRect(scale, frame);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), MintSignal);
            if (DrawLauncherButton(launcherRect, "CLOSE", MintSignal, scale))
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

            var buttonY = rect.y + 84f * scale;
            var buttonHeight = 84f * scale;
            var gap = 8f * scale;

            if (selectedTowerCategory < 0)
            {
                DrawTowerCategoryPicker(rect, buttonY, buttonHeight, gap, scale);
                return;
            }

            if (RuntimeUiChrome.DrawPanelButton(new Rect(rect.xMax - 72f * scale, rect.y + 8f * scale, 58f * scale, 32f * scale), "BACK", MintSignal, scale, buttonStyle))
            {
                selectedTowerCategory = -1;
                return;
            }

            DrawTowerCategoryGrid(rect, buttonY, buttonHeight, gap, scale);
        }

        /// <summary>
        /// Category chooser, mirroring the send dock. Card height is divided out of the panel's
        /// actual height so adding a category cannot push the last card off the panel.
        /// </summary>
        private void DrawTowerCategoryPicker(Rect rect, float buttonY, float buttonHeight, float gap, float scale)
        {
            var labels = LTW.UnityClient.Simulation.TowerCatalog.CategoryLabels;
            var gold = CurrentPlayerGold();

            for (var category = 0; category < labels.Length; category++)
            {
                var accent = CategoryAccent(category);
                var cardRect = RuntimeUiChrome.CategoryCardRect(rect, buttonY, gap, category, labels.Length, scale);
                // Hit region excludes BOTH action rows, or the card's own button eats their clicks
                // before either is ever delivered.
                var pressed = RuntimeUiChrome.DrawCommandCard(
                    cardRect, accent, CommandCardState.Normal, scale, RuntimeUiChrome.CategoryCardSelectRect(cardRect, scale));

                buttonStyle!.fontSize = Mathf.RoundToInt(13f * scale);
                buttonStyle.normal.textColor = Cloud;
                // Label and meta are positioned proportionally here, matching the send dock's
                // category card. They previously used CommandCardLabelRect/CommandCardMetaRect,
                // which anchor a fixed distance off the card's BOTTOM edge — on a card grown for a
                // tier row that put both lines straight through the new row.
                GUI.Label(new Rect(cardRect.x, cardRect.y + cardRect.height * 0.20f, cardRect.width, 22f * scale), labels[category], buttonStyle);

                metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
                metaStyle.normal.textColor = accent;
                metaStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(cardRect.x, cardRect.y + cardRect.height * 0.44f, cardRect.width, 18f * scale), "5 TOWERS", metaStyle);

                DrawTowerCategoryBatch(cardRect, category, accent, scale);
                DrawTowerCategoryTier(cardRect, category, accent, gold, scale);

                if (pressed)
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
            var pressed = RuntimeUiChrome.DrawCategoryTierRow(
                cardRect, tier, commandAdapter.MaxCategoryTier, cost, gold >= cost, accent, scale, metaStyle!, buttonStyle!);

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

            var iconRect = RuntimeUiChrome.CommandCardIconRect(rect, scale);
            if (!RuntimeUiIconLibrary.DrawIcon(iconRect, $"ui_icon_tower_{entry.RoleId}_v01", isAffordable))
            {
                DrawTowerIcon(iconRect, TowerIconForRole(entry.Role), displayAccent, scale);
            }

            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            buttonStyle.normal.textColor = isAffordable ? Cloud : DisabledText;
            buttonStyle.hover.textColor = buttonStyle.normal.textColor;
            buttonStyle.active.textColor = buttonStyle.normal.textColor;
            GUI.Label(RuntimeUiChrome.CommandCardLabelRect(rect, scale), entry.ShortLabel, buttonStyle);

            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = isAffordable
                ? new Color(
                    Mathf.Lerp(displayAccent.r, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.g, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.b, 1f, 0.55f),
                    1f)
                : displayAccent;
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
            _ => TowerIconKind.Arrow
        };

        private void OpenTowerPalette()
        {
            CloseSendDock();
            // Symmetric with CloseBottomPanelsForSend. Today the BUILD button is not even drawn
            // while multi-select is on — RAISE occupies its slot — so this cannot currently be
            // reached in that state. It is here so that stops being load-bearing: any future route
            // into the palette ends the mode rather than leaving it live under a panel.
            SetMultiSelectMode(false);
            isPaletteExpanded = true;
            selectedTowerCategory = -1;
        }

        private void CloseSendDock() => SendDock?.CloseDock();

        private bool IsSendDockExpanded() => SendDock?.IsExpanded == true;

        private SendDockController? SendDock
        {
            get
            {
                if (sendDockController == null)
                {
                    sendDockController = Object.FindAnyObjectByType<SendDockController>();
                }

                return sendDockController;
            }
        }

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
                border = new RectOffset(6, 6, 6, 6),
                margin = ZeroOffset(),
                padding = ZeroOffset()
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = MintSignal }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Cloud }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                margin = ZeroOffset(),
                padding = ZeroOffset(),
                normal = { textColor = Cloud },
                hover = { textColor = Cloud },
                active = { textColor = Cloud }
            };

            metaStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
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

        private int SelectedTowerCost()
        {
            // Read from the simulation's catalog. These used to be literals here and in the
            // palette buttons, and both had drifted from what the simulation actually charges.
            return commandAdapter != null ? commandAdapter.TowerCost(selectedTowerRole) : 0;
        }

        private int CurrentPlayerGold()
        {
            if (commandAdapter == null)
            {
                commandAdapter = Object.FindAnyObjectByType<UnityCommandAdapter>();
            }

            return commandAdapter?.CurrentPlayerGold() ?? 0;
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

        private bool IsSelectedCellInBounds() =>
            selectedCell.x >= 0 && selectedCell.x < LaneWidth && selectedCell.y >= 0 && selectedCell.y < LaneLength;

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

        private void RefreshSelectedTowerFromSnapshot()
        {
            if (selectedTower is null)
            {
                return;
            }

            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }

            var snapshot = simulationDriver?.LatestSnapshot;
            var current = snapshot?.Towers.FirstOrDefault(tower => tower.EntityId.Equals(selectedTower.EntityId));
            if (current is null)
            {
                selectedTower = null;
                HideSelectionRing();
                return;
            }

            selectedTower = current;
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
            var width = Mathf.Min(frame.width - 16f * scale, 430f * scale);
            // One height for both states. The picker used to need a taller panel because it stacked
            // three full-width cards; laid out as a row sized to the card art's own aspect it fits
            // inside the same 282 the tower grid uses, so the panel no longer grows and shrinks
            // under the player as they step through it.
            var height = 282f * scale;
            var launcherClearance = 136f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale) - launcherClearance, width, height);
        }

        private static Rect PlacementPanelRect(float scale, Rect frame)
        {
            var width = Mathf.Min(frame.width - 16f * scale, 360f * scale);
            var height = 160f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale), width, height);
        }

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
