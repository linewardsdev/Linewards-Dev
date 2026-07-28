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
