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
    /// Tower selection: single tap, double-tap select-all-of-type, and the multi-selection
    /// set that the batch raise and sell actions operate on.
    /// </summary>
    public sealed partial class TouchPlacementController
    {
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
    }
}
