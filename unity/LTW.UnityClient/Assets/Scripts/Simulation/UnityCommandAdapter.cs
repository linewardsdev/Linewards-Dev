using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class UnityCommandAdapter : MonoBehaviour
    {
        private LocalVerticalSlice simulation;

        public void Initialize(LocalVerticalSlice localSimulation)
        {
            simulation = localSimulation;
        }

        public VerticalSliceCommandResult PreviewSampleTower(int x, int y) => PreviewTower(SampleVerticalSliceContent.TowerId, x, y);

        public VerticalSliceCommandResult PreviewControlTower(int x, int y) => PreviewTower(SampleVerticalSliceContent.ControlTowerId, x, y);

        public VerticalSliceCommandResult PreviewUtilityTower(int x, int y) => PreviewTower(SampleVerticalSliceContent.UtilityTowerId, x, y);

        private VerticalSliceCommandResult PreviewTower(LTW.Simulation.Content.ContentId towerId, int x, int y)
        {
            return simulation is null
                ? VerticalSliceCommandResult.Reject(LTW.Simulation.Commands.CommandRejectionReason.MatchPaused)
                : simulation.PreviewPlaceTower(new PlayerId(1), new LaneId(1), towerId, new GridPosition(x, y));
        }

        public VerticalSliceCommandResult PlaceSampleTower(int x, int y) => PlaceTower(SampleVerticalSliceContent.TowerId, x, y);

        public VerticalSliceCommandResult PlaceControlTower(int x, int y) => PlaceTower(SampleVerticalSliceContent.ControlTowerId, x, y);

        public VerticalSliceCommandResult PlaceUtilityTower(int x, int y) => PlaceTower(SampleVerticalSliceContent.UtilityTowerId, x, y);

        private VerticalSliceCommandResult PlaceTower(LTW.Simulation.Content.ContentId towerId, int x, int y)
        {
            return simulation is null
                ? VerticalSliceCommandResult.Reject(LTW.Simulation.Commands.CommandRejectionReason.MatchPaused)
                : simulation.PlaceTower(new PlayerId(1), new LaneId(1), towerId, new GridPosition(x, y));
        }

        public VerticalSliceCommandResult SendSampleCreep()
        {
            return SendSampleCreep(1);
        }

        public VerticalSliceCommandResult SendSampleCreep(int quantity) => SendCreep(SampleVerticalSliceContent.CreepId, quantity);

        public VerticalSliceCommandResult SendBruteCreep() => SendCreep(SampleVerticalSliceContent.BruteCreepId, 1);

        public VerticalSliceCommandResult SendSwarmCreep() => SendCreep(SampleVerticalSliceContent.SwarmCreepId, 3);

        private VerticalSliceCommandResult SendCreep(LTW.Simulation.Content.ContentId creepId, int quantity)
        {
            return simulation is null
                ? VerticalSliceCommandResult.Reject(LTW.Simulation.Commands.CommandRejectionReason.MatchPaused)
                : simulation.QueueSend(new PlayerId(1), creepId, quantity);
        }

        public VerticalSliceCommandResult SellLastSampleTower()
        {
            return simulation is null
                ? VerticalSliceCommandResult.Reject(LTW.Simulation.Commands.CommandRejectionReason.MatchPaused)
                : simulation.SellLastTower(new PlayerId(1));
        }

        public void ResetMatch()
        {
            simulation?.Reset();
        }
    }
}
