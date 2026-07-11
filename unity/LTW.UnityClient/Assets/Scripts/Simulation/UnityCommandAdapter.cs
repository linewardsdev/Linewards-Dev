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

        public VerticalSliceCommandResult PlaceSampleTower(int x, int y)
        {
            return simulation is null
                ? VerticalSliceCommandResult.Reject(LTW.Simulation.Commands.CommandRejectionReason.MatchPaused)
                : simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(x, y));
        }

        public VerticalSliceCommandResult SendSampleCreep()
        {
            return simulation is null
                ? VerticalSliceCommandResult.Reject(LTW.Simulation.Commands.CommandRejectionReason.MatchPaused)
                : simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);
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
