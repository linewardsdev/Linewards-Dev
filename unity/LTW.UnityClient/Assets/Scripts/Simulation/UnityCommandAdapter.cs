using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class UnityCommandAdapter : MonoBehaviour
    {
        private const int LaneWidth = 7;
        private const int LaneLength = 16;

        private LocalVerticalSlice simulation;
        private UnitySimulationDriver simulationDriver;

        public void Initialize(LocalVerticalSlice localSimulation, UnitySimulationDriver driver)
        {
            simulation = localSimulation;
            simulationDriver = driver;
        }

        /// <summary>
        /// Reads economy state directly from the simulation command source. UI code should use this
        /// for immediate affordability/readback after a command instead of relying on a rendered snapshot.
        /// </summary>
        public int CurrentPlayerGold()
        {
            return simulation is null ? 0 : simulation.GetSnapshot().Players.Get(simulation.LocalPlayerId).Gold.Amount;
        }

        /// <summary>
        /// The tower line the local player has committed to, or -1 while they are still free to pick.
        /// </summary>
        /// <remarks>
        /// A seat commits to one line with its first tower and cannot build outside it afterwards.
        /// The palette needs to know because the rule is otherwise invisible: every category card
        /// looked buildable, and tapping one from a line the seat had locked out of produced a bare
        /// rejection with nothing to explain it.
        /// </remarks>
        public int CurrentPlayerTowerLine()
        {
            return simulation is null
                ? PlayerEconomyState.UnchosenTowerLine
                : simulation.GetSnapshot().Players.Get(simulation.LocalPlayerId).ChosenTowerLine;
        }

        /// <summary>
        /// Ticks remaining before the local player may send again, or 0 if a send is available.
        /// </summary>
        /// <remarks>
        /// The send cooldown is enforced but was invisible: a card the player could afford looked
        /// perfectly sendable, and tapping it produced a bare rejection. Surfacing the remaining
        /// time lets the dock show why.
        /// </remarks>
        public int CurrentPlayerSendCooldownTicks()
        {
            if (simulation is null)
            {
                return 0;
            }

            var snapshot = simulation.GetSnapshot();
            // SimulationTick.Value is a long, so this subtraction is a long and needs an explicit
            // narrowing cast to match this method's int return (CS0266 without it — main did not
            // compile). Safe: remaining is bounded above by the send cooldown itself, which is
            // currently 0 ticks (74b8519 removed it) — this reads the live value rather than
            // assuming that, so it stays correct if the cooldown is ever restored.
            var remaining = snapshot.Players.Get(simulation.LocalPlayerId).NextSendAvailableTick.Value - snapshot.Tick.Value;
            return remaining > 0 ? (int)remaining : 0;
        }

        public VerticalSliceCommandResult PreviewSampleTower(int x, int y) => PreviewTower(SampleVerticalSliceContent.TowerId, x, y);

        public VerticalSliceCommandResult PreviewControlTower(int x, int y) => PreviewTower(SampleVerticalSliceContent.ControlTowerId, x, y);

        public VerticalSliceCommandResult PreviewUtilityTower(int x, int y) => PreviewTower(SampleVerticalSliceContent.UtilityTowerId, x, y);

        public VerticalSliceCommandResult PreviewPulseTower(int x, int y) => PreviewTower(SampleVerticalSliceContent.PulseTowerId, x, y);

        public VerticalSliceCommandResult PreviewPrismTower(int x, int y) => PreviewTower(SampleVerticalSliceContent.PrismTowerId, x, y);

        private VerticalSliceCommandResult PreviewTower(LTW.Simulation.Content.ContentId towerId, int x, int y)
        {
            if (!IsValidCell(x, y))
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);
            }

            return simulation is null
                ? VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused)
                : simulation.PreviewPlaceTower(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, towerId, new GridPosition(x, y));
        }

        /// <summary>
        /// Places the tower a palette role refers to. The per-tower PlaceXTower helpers below stay
        /// for the review runners, which name towers explicitly; gameplay goes through here so a
        /// new tower needs a catalog entry and nothing else.
        /// </summary>
        public VerticalSliceCommandResult PlaceTowerByRole(int role, int x, int y) =>
            PlaceTower(new LTW.Simulation.Content.ContentId(TowerCatalog.ForRole(role).ContentId), x, y);

        public VerticalSliceCommandResult PreviewTowerByRole(int role, int x, int y) =>
            PreviewTower(new LTW.Simulation.Content.ContentId(TowerCatalog.ForRole(role).ContentId), x, y);

        /// <summary>
        /// Gold cost of a tower, read from the simulation's catalog.
        /// </summary>
        /// <remarks>
        /// The client used to hold its own copy of every price and it went stale in two different
        /// places at once — the palette showed 20 gold for Arrow, the selection readout said 25,
        /// and the simulation charged 14. Asking the catalog means the displayed price and the
        /// affordability gate cannot disagree with the charge.
        /// </remarks>
        /// <summary>
        /// The local player's current tier for one tower line (0 ARCANE, 1 FOUNDRY, 2 GROVE).
        /// </summary>
        public int TowerLineTier(int lineIndex) =>
            simulation is null ? 1 : simulation.GetSnapshot().Players.Get(simulation.LocalPlayerId).TowerLineTier(lineIndex);

        /// <summary>
        /// The local player's current tier for one send category (0 CORE, 1 RAPID, 2 ELITE).
        /// </summary>
        public int SendCategoryTier(int categoryIndex) =>
            simulation is null ? 1 : simulation.GetSnapshot().Players.Get(simulation.LocalPlayerId).SendCategoryTier(categoryIndex);

        /// <summary>
        /// Gold to take a category from its current tier to the next, or 0 when already at the top.
        /// </summary>
        /// <remarks>
        /// Read from CategoryTierRules rather than held as a UI constant, so a card can never
        /// advertise a price the simulation would not charge.
        /// </remarks>
        public int NextTierCost(LTW.Simulation.Commands.CategoryKind kind, int categoryIndex)
        {
            var current = kind == LTW.Simulation.Commands.CategoryKind.TowerLine
                ? TowerLineTier(categoryIndex)
                : SendCategoryTier(categoryIndex);
            return current >= LTW.Simulation.Content.CategoryTierRules.MaxTier
                ? 0
                : LTW.Simulation.Content.CategoryTierRules.CostFor(kind, current + 1, UpgradesOwned());
        }

        /// <summary>
        /// Tiers the local seat already holds, which is what the next one is priced against.
        /// </summary>
        /// <remarks>
        /// Both prices below escalate with this, so the card has to read it too. Showing the list
        /// price while the bridge charges the escalated one is exactly the mismatch the remarks on
        /// <see cref="NextTierCost"/> already warn about — a card that advertises a price the
        /// simulation would not charge — and it would widen with every tier the player bought.
        /// </remarks>
        private int UpgradesOwned() =>
            simulation is null
                ? 0
                : LTW.Simulation.Content.CategoryTierRules.UpgradesOwned(LocalSeat());

        /// <summary>The local seat's economy state, which every tiered price is read against.</summary>
        private LTW.Simulation.Economy.PlayerEconomyState LocalSeat() =>
            simulation!.GetSnapshot().Players.Get(simulation.LocalPlayerId);

        /// <summary>Income the player must already be earning to buy the next tier, or 0 at the top.</summary>
        /// <remarks>
        /// Exposed alongside <see cref="NextTierCost"/> because the two together are what the card
        /// has to say. A tier now has two prices and only one of them is gold; showing the gold and
        /// silently refusing on the other leaves a button that looks affordable and does nothing.
        /// </remarks>
        public int NextTierMinimumIncome(LTW.Simulation.Commands.CategoryKind kind, int categoryIndex)
        {
            var current = kind == LTW.Simulation.Commands.CategoryKind.TowerLine
                ? TowerLineTier(categoryIndex)
                : SendCategoryTier(categoryIndex);
            return current >= LTW.Simulation.Content.CategoryTierRules.MaxTier
                ? 0
                : LTW.Simulation.Content.CategoryTierRules.MinimumIncomeFor(kind, current + 1, UpgradesOwned());
        }

        /// <summary>The local player's income right now.</summary>
        public int CurrentPlayerIncome() =>
            simulation is null ? 0 : simulation.GetSnapshot().Players.Get(simulation.LocalPlayerId).Income.Amount;

        public int MaxCategoryTier => LTW.Simulation.Content.CategoryTierRules.MaxTier;

        public VerticalSliceCommandResult BuyTowerLineTier(int lineIndex) =>
            BuyCategoryTier(LTW.Simulation.Commands.CategoryKind.TowerLine, lineIndex);

        public VerticalSliceCommandResult BuySendCategoryTier(int categoryIndex) =>
            BuyCategoryTier(LTW.Simulation.Commands.CategoryKind.SendCategory, categoryIndex);

        private VerticalSliceCommandResult BuyCategoryTier(LTW.Simulation.Commands.CategoryKind kind, int categoryIndex)
        {
            if (simulationDriver == null || !simulationDriver.HasStarted || simulationDriver.IsPaused)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            var current = kind == LTW.Simulation.Commands.CategoryKind.TowerLine
                ? TowerLineTier(categoryIndex)
                : SendCategoryTier(categoryIndex);
            return RefreshAfterAccepted(simulation.BuyCategoryTier(simulation.LocalPlayerId, kind, categoryIndex, current + 1));
        }

        /// <summary>
        /// Gold to raise the local player's tower at this cell by a tier, or 0 if there is none.
        /// </summary>
        public int TowerUpgradeCostAt(int x, int y) =>
            simulation is null ? 0 : simulation.TowerUpgradeCostAt(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, new GridPosition(x, y));

        /// <summary>
        /// Whether that tower can actually be raised right now: below the top tier, and below the
        /// line tier its owner has bought.
        /// </summary>
        /// <remarks>
        /// Asked separately from the cost so a card can show WHY the button is dead — at the line's
        /// ceiling reads differently from cannot afford it, and answering both with one number
        /// would collapse them.
        /// </remarks>
        public bool CanUpgradeTowerAt(int x, int y)
        {
            if (simulation is null)
            {
                return false;
            }

            var snapshot = simulation.GetSnapshot();
            var tower = snapshot.Towers.FirstOrDefault(candidate =>
                candidate.OwnerId.Equals(simulation.LocalPlayerId)
                && candidate.LaneId.Equals(simulation.LocalPlayerLaneId)
                && candidate.Position.X == x
                && candidate.Position.Y == y);
            if (tower is null)
            {
                return false;
            }

            var definition = simulation.Content.Towers.FirstOrDefault(candidate => candidate.Id.Equals(tower.TowerId));
            if (definition is null)
            {
                return false;
            }

            var ceiling = snapshot.Players.Get(simulation.LocalPlayerId).TowerLineTier(definition.CategoryIndex);
            return tower.Tier < ceiling && tower.Tier < LTW.Simulation.Content.CategoryTierRules.MaxTier;
        }

        /// <summary>
        /// Which tower line the tower at this cell belongs to, or -1 if there is none.
        /// </summary>
        /// <remarks>
        /// Exists so the selected-tower panel can NAME the line a tower is waiting on rather than
        /// saying it is capped and leaving the player to work out by what.
        /// </remarks>
        public int TowerLineIndexAt(int x, int y)
        {
            if (simulation is null)
            {
                return -1;
            }

            var tower = simulation.GetSnapshot().Towers.FirstOrDefault(candidate =>
                candidate.OwnerId.Equals(simulation.LocalPlayerId)
                && candidate.LaneId.Equals(simulation.LocalPlayerLaneId)
                && candidate.Position.X == x
                && candidate.Position.Y == y);
            if (tower is null)
            {
                return -1;
            }

            var definition = simulation.Content.Towers.FirstOrDefault(candidate => candidate.Id.Equals(tower.TowerId));
            return definition?.CategoryIndex ?? -1;
        }

        /// <summary>
        /// What raising every tower in one line would cost, and how much of that the player can
        /// currently afford. Asked every frame the card is on screen, so it must not spend anything.
        /// </summary>
        public BatchUpgradeQuote QuoteLineUpgrade(int lineIndex) =>
            simulation is null
                ? default
                : simulation.QuoteTowerLineUpgrade(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, lineIndex);

        /// <summary>
        /// Raises every tower in one line, spending as far as the player's gold reaches.
        /// </summary>
        /// <remarks>
        /// Returns the outcome rather than a VerticalSliceCommandResult because a batch has no
        /// single accepted/rejected answer: raising three of five towers is neither. The caller has
        /// to say what actually happened, so it is given the numbers to say it with.
        /// </remarks>
        public BatchUpgradeOutcome UpgradeLine(int lineIndex)
        {
            if (simulationDriver == null || !simulationDriver.HasStarted || simulationDriver.IsPaused || simulation is null)
            {
                return default;
            }

            var outcome = simulation.UpgradeTowerLine(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, lineIndex);
            if (outcome.Upgraded > 0)
            {
                simulationDriver.RefreshSnapshot();
            }

            return outcome;
        }

        /// <summary>What raising a hand-picked selection would cost, and how much of it is affordable.</summary>
        public BatchUpgradeQuote QuoteSelectionUpgrade(IReadOnlyCollection<GridPosition> positions) =>
            simulation is null
                ? default
                : simulation.QuoteTowerUpgrades(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, positions);

        /// <summary>What selling a hand-picked selection would return.</summary>
        public BatchSellQuote QuoteSelectionSale(IReadOnlyCollection<GridPosition> positions) =>
            simulation is null
                ? default
                : simulation.QuoteTowerSales(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, positions);

        /// <summary>Raises a hand-picked selection, spending as far as the player's gold reaches.</summary>
        public BatchUpgradeOutcome UpgradeSelection(IReadOnlyCollection<GridPosition> positions)
        {
            if (simulationDriver == null || !simulationDriver.HasStarted || simulationDriver.IsPaused || simulation is null)
            {
                return default;
            }

            var outcome = simulation.UpgradeTowers(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, positions);
            if (outcome.Upgraded > 0)
            {
                simulationDriver.RefreshSnapshot();
            }

            return outcome;
        }

        /// <summary>Sells a hand-picked selection.</summary>
        public BatchSellOutcome SellSelection(IReadOnlyCollection<GridPosition> positions)
        {
            if (simulationDriver == null || !simulationDriver.HasStarted || simulationDriver.IsPaused || simulation is null)
            {
                return default;
            }

            var outcome = simulation.SellTowers(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, positions);
            if (outcome.Sold > 0)
            {
                simulationDriver.RefreshSnapshot();
            }

            return outcome;
        }

        public VerticalSliceCommandResult UpgradeTowerAt(int x, int y)
        {
            if (simulationDriver == null || !simulationDriver.HasStarted || simulationDriver.IsPaused)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            return RefreshAfterAccepted(simulation.UpgradeTower(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, new GridPosition(x, y)));
        }

        public int TowerCost(int role)
        {
            if (simulation is null)
            {
                return 0;
            }

            var contentId = TowerCatalog.ForRole(role).ContentId;
            foreach (var tower in simulation.Content.Towers)
            {
                if (tower.Id.Value == contentId)
                {
                    // At the local seat's line tier, which is what the bridge charges. A palette
                    // quoting the authored cost after the line is upgraded is a card that says one
                    // number and bills another, and the gap grows with the tier.
                    var tier = LocalSeat().TowerLineTier(tower.CategoryIndex);
                    return tower.Cost.Amount * LTW.Simulation.Content.CategoryTierRules.TowerBuildCostPercentFor(tier) / 100;
                }
            }

            return 0;
        }

        /// <summary>
        /// Gold cost of a creep, read from the simulation's catalog.
        /// </summary>
        /// <remarks>
        /// Mirrors <see cref="TowerCost"/> for the same reason: <c>SendDockController</c> used to hold
        /// its own copy of every creep price (in the Send* cost argument, the affordability gate, and
        /// the card meta string — three copies per creep), and Serpent Coil already drifted once (22 vs
        /// the simulation's 20) across all three before this existed.
        /// </remarks>
        /// <summary>Gold for ONE creep of this type. See <see cref="SendCost"/> for what a send costs.</summary>
        public int CreepCost(LTW.Simulation.Content.ContentId creepId)
        {
            if (simulation is null)
            {
                return 0;
            }

            foreach (var creep in simulation.Content.Creeps)
            {
                if (creep.Id.Equals(creepId))
                {
                    // At the local seat's send-category tier and its current income's opening
                    // discount, matching EconomyService.SendCostFor exactly — a card quoting a
                    // different number than what QueueSend actually charges is a button that lies.
                    var tier = LocalSeat().SendCategoryTier(creep.CategoryIndex);
                    var tick = simulation.GetSnapshot().Tick.Value;
                    return creep.Cost.Amount * LTW.Simulation.Content.CategoryTierRules.SendCostPercentFor(tier) / 100
                        * LTW.Simulation.Content.OpeningEconomyRules.CreepCostPercentFor(tick) / 100;
                }
            }

            return 0;
        }

        /// <summary>
        /// How many creeps one press of a send button queues.
        /// </summary>
        /// <remarks>
        /// Swarm is the only creep sent in a batch — it is a cheap, fast, low-health swarm and one
        /// of them is not a threat. Every other creep sends singly.
        ///
        /// This exists so the batch size has ONE definition. It used to be a literal in
        /// SendSwarmCreep and nowhere else, which meant the HUD could not know about it: the send
        /// dock priced the card at the unit cost, enabled it whenever the player could afford one,
        /// and then EconomyService charged `cost * quantity` and rejected. With 15 gold and Swarm
        /// at 6, the card read "6G", looked affordable, failed, and reported "Need 6G (have 15G)" —
        /// a message that is self-contradictory on its face because the 6 and the 15 came from
        /// different calculations.
        /// </remarks>
        public int SendQuantity(LTW.Simulation.Content.ContentId creepId) =>
            creepId.Equals(SampleVerticalSliceContent.SwarmCreepId) ? SwarmSendQuantity : 1;

        /// <summary>Gold one press of a send button actually costs, batch included.</summary>
        /// <remarks>
        /// This is what the HUD must display and gate on. It mirrors EconomyService.SendCostFor,
        /// which prices the creep at the sender's category tier and then multiplies by quantity —
        /// in that order, so a batch costs exactly what the same number of single sends would.
        /// </remarks>
        public int SendCost(LTW.Simulation.Content.ContentId creepId) =>
            CreepCost(creepId) * SendQuantity(creepId);

        /// <summary>Income one press of this send button actually grants right now.</summary>
        /// <remarks>
        /// Asks the simulation rather than printing the creep's authored IncomeGain, because the two
        /// stop agreeing once the player passes the income taper's knee — at that point a card
        /// advertising "+5" while granting +2 is worse than showing no number at all. Same reasoning
        /// as <see cref="SendCost"/>, and it includes the send quantity for the same reason.
        /// </remarks>
        public int SendIncomeGain(LTW.Simulation.Content.ContentId creepId) =>
            simulation is null
                ? 0
                : simulation.IncomeGainForSend(simulation.LocalPlayerId, creepId, SendQuantity(creepId));

        private const int SwarmSendQuantity = 3;

        public VerticalSliceCommandResult PlaceSampleTower(int x, int y) => PlaceTower(SampleVerticalSliceContent.TowerId, x, y);

        public VerticalSliceCommandResult PlaceControlTower(int x, int y) => PlaceTower(SampleVerticalSliceContent.ControlTowerId, x, y);

        public VerticalSliceCommandResult PlaceUtilityTower(int x, int y) => PlaceTower(SampleVerticalSliceContent.UtilityTowerId, x, y);

        public VerticalSliceCommandResult PlacePulseTower(int x, int y) => PlaceTower(SampleVerticalSliceContent.PulseTowerId, x, y);

        public VerticalSliceCommandResult PlacePrismTower(int x, int y) => PlaceTower(SampleVerticalSliceContent.PrismTowerId, x, y);

        private VerticalSliceCommandResult PlaceTower(LTW.Simulation.Content.ContentId towerId, int x, int y)
        {
            if (!IsValidCell(x, y))
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);
            }

            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            return RefreshAfterAccepted(simulation.PlaceTower(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, towerId, new GridPosition(x, y)));
        }

        public VerticalSliceCommandResult SendSampleCreep()
        {
            return SendSampleCreep(1);
        }

        public VerticalSliceCommandResult SendSampleCreep(int quantity) => SendCreep(SampleVerticalSliceContent.CreepId, quantity);

        public VerticalSliceCommandResult SendBruteCreep() => SendCreep(SampleVerticalSliceContent.BruteCreepId, 1);

        public VerticalSliceCommandResult SendSwarmCreep() =>
            SendCreep(SampleVerticalSliceContent.SwarmCreepId, SendQuantity(SampleVerticalSliceContent.SwarmCreepId));

        public VerticalSliceCommandResult SendShadeCreep() => SendCreep(SampleVerticalSliceContent.ShadeCreepId, 1);

        public VerticalSliceCommandResult SendSiegeCreep() => SendCreep(SampleVerticalSliceContent.SiegeCreepId, 1);

        public VerticalSliceCommandResult SendWispCreep() => SendCreep(SampleVerticalSliceContent.WispCreepId, 1);

        public VerticalSliceCommandResult SendRevenantCreep() => SendCreep(SampleVerticalSliceContent.RevenantCreepId, 1);

        public VerticalSliceCommandResult SendObsidianBruteCreep() => SendCreep(SampleVerticalSliceContent.ObsidianBruteCreepId, 1);

        public VerticalSliceCommandResult SendSerpentCreep() => SendCreep(SampleVerticalSliceContent.SerpentCreepId, 1);

        public VerticalSliceCommandResult SendTurretWalkerCreep() => SendCreep(SampleVerticalSliceContent.TurretWalkerCreepId, 1);

        public VerticalSliceCommandResult SendZephyrCreep() => SendCreep(SampleVerticalSliceContent.ZephyrCreepId, 1);

        public VerticalSliceCommandResult SendBurrowerCreep() => SendCreep(SampleVerticalSliceContent.BurrowerCreepId, 1);

        public VerticalSliceCommandResult SendStalkerCreep() => SendCreep(SampleVerticalSliceContent.StalkerCreepId, 1);

        public VerticalSliceCommandResult SendWardenCreep() => SendCreep(SampleVerticalSliceContent.WardenCreepId, 1);

        public VerticalSliceCommandResult SendColossusCreep() => SendCreep(SampleVerticalSliceContent.ColossusCreepId, 1);

        public VerticalSliceCommandResult CreateDamagedTransferReviewCreep()
        {
            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            var result = simulation.CreateLocalPlaytestDamagedTransferCreep(SampleVerticalSliceContent.BruteCreepId, health: 8);
            return RefreshAfterAccepted(result);
        }

        public VerticalSliceCommandResult SendStressReviewWave(int burstIndex, int senderPlayerId = 3)
        {
            if (simulationDriver == null || !simulationDriver.HasStarted || simulationDriver.IsPaused)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            var stressSender = new PlayerId(senderPlayerId);
            simulation.GrantLocalPlaytestGold(stressSender, new Gold(5000));
            // Each burst is one QueueSend, but they arrive every 1.25s against a send cooldown, so
            // without this only the first burst of a run would land.
            simulation.ClearLocalPlaytestSendCooldown(stressSender);
            return (burstIndex % 3) switch
            {
                1 => simulation.QueueSend(stressSender, SampleVerticalSliceContent.BruteCreepId, 10),
                2 => simulation.QueueSend(stressSender, SampleVerticalSliceContent.SwarmCreepId, 30),
                _ => simulation.QueueSend(stressSender, SampleVerticalSliceContent.CreepId, 24)
            };
        }

        private VerticalSliceCommandResult SendCreep(LTW.Simulation.Content.ContentId creepId, int quantity)
        {
            if (simulationDriver == null || !simulationDriver.HasStarted || simulationDriver.IsPaused)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            // Queued, not sent. On a phone the player has to open the dock and find the card, and
            // asking them to do that at the instant income lands is not reasonable — so a tap states
            // intent and the simulation pays for it when it can. A send with no gold behind it now
            // waits instead of being refused.
            //
            // One enqueue per creep, because the queue holds creeps rather than batches: a swarm
            // send of six is six entries, which is also what makes the per-creep cap of ten mean
            // the same thing for every card.
            var result = VerticalSliceCommandResult.Accept();
            for (var i = 0; i < quantity; i++)
            {
                result = simulation.EnqueueSend(simulation.LocalPlayerId, creepId);
                if (!result.Accepted)
                {
                    // Stops at the first refusal rather than pressing on. The only refusal a player
                    // will see here is a full queue, and queueing four of a requested six would be
                    // a partial success reported as a failure.
                    break;
                }
            }

            return RefreshAfterAccepted(result);
        }

        /// <summary>How many of this creep the local seat has waiting in its send queue.</summary>
        /// <remarks>
        /// The send card needs it: a tap no longer produces a creep straight away, so without a
        /// count on the card the player has no way to tell a queued tap from one that did nothing.
        /// </remarks>
        /// <remarks>
        /// Read off the SNAPSHOT, not the simulation. Under a server the queue is authoritative
        /// state that arrives over the wire like gold and lives; a client that reached into the
        /// simulation would be reading a local guess, and this badge would drift the first time a
        /// message was dropped — silently, and only for the player who queued.
        /// </remarks>
        public int QueuedSendCount(LTW.Simulation.Content.ContentId creepId) =>
            simulation is null
                ? 0
                : simulation.GetSnapshot().QueuedSendCountFor(simulation.LocalPlayerId, creepId);

        /// <summary>Everything the local seat has waiting, across all creeps.</summary>
        /// <remarks>
        /// For the queue readout (iPad round 2, item 15): per-creep counts live on the cards, but a
        /// closed dock showed nothing at all, so a player with sends waiting had no way to know
        /// without reopening it. Read off the snapshot for the same authority reason as
        /// <see cref="QueuedSendCount"/> directly above.
        /// </remarks>
        public int TotalQueuedSends() =>
            simulation is null
                ? 0
                : simulation.GetSnapshot().SendQueueFor(simulation.LocalPlayerId).Count;

        /// <summary>Takes back the local seat's most recent queued send of one creep.</summary>
        /// <remarks>
        /// The undo half of <c>SendCreep</c>. A queued tap is a statement of intent that has not
        /// been paid for yet, so it can still be withdrawn — and on a touch screen the mis-tap is
        /// the mistake worth being able to take back.
        ///
        /// Cancels the most recent rather than the next to go out; see
        /// <c>LocalVerticalSlice.CancelQueuedSend</c> for why the other end would be the wrong one.
        /// </remarks>
        public VerticalSliceCommandResult CancelQueuedSend(LTW.Simulation.Content.ContentId creepId)
        {
            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            return RefreshAfterAccepted(simulation.CancelQueuedSend(simulation.LocalPlayerId, creepId));
        }

        /// <summary>Empties the local seat's send queue, returning how many entries went.</summary>
        /// <remarks>
        /// For "I queued the wrong thing ten times". Returns a count rather than a result because an
        /// already-empty queue is a normal state rather than a refusal — the caller uses the number
        /// to decide whether anything is worth animating.
        /// </remarks>
        public int ClearSendQueue()
        {
            if (simulation is null)
            {
                return 0;
            }

            var removed = simulation.ClearSendQueue(simulation.LocalPlayerId);
            if (removed > 0)
            {
                RefreshAfterAccepted(VerticalSliceCommandResult.Accept());
            }

            return removed;
        }

        /// <summary>Total creeps waiting in the local seat's send queue.</summary>
        public int QueuedSendTotal() =>
            simulation is null ? 0 : simulation.GetSnapshot().SendQueueFor(simulation.LocalPlayerId).Count;

        public VerticalSliceCommandResult SellLastSampleTower()
        {
            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            return RefreshAfterAccepted(simulation.SellLastTower(simulation.LocalPlayerId));
        }

        public VerticalSliceCommandResult SellTowerAt(int x, int y)
        {
            if (!IsValidCell(x, y))
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);
            }

            if (simulation is null)
            {
                return VerticalSliceCommandResult.Reject(CommandRejectionReason.MatchPaused);
            }

            return RefreshAfterAccepted(simulation.SellTowerAt(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, new GridPosition(x, y)));
        }

        public void ResetMatch()
        {
            simulation?.Reset();
            simulationDriver?.RefreshSnapshot();
        }

        private VerticalSliceCommandResult RefreshAfterAccepted(VerticalSliceCommandResult result)
        {
            if (result.Accepted)
            {
                simulationDriver?.RefreshSnapshot();
            }

            return result;
        }

        private static bool IsValidCell(int x, int y) => x >= 0 && x < LaneWidth && y >= 0 && y < LaneLength;
    }
}
