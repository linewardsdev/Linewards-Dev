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
                    return tower.Cost.Amount;
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
                    return creep.Cost.Amount;
                }
            }

            return 0;
        }

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

        public VerticalSliceCommandResult SendSwarmCreep() => SendCreep(SampleVerticalSliceContent.SwarmCreepId, 3);

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

            return RefreshAfterAccepted(simulation.QueueSend(simulation.LocalPlayerId, creepId, quantity));
        }

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
