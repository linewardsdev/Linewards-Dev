using System.Collections.Generic;
using LTW.Simulation.Bridge;
using LTW.Simulation.Events;
using LTW.Simulation.Economy;
using LTW.Simulation.Replay;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class UnitySimulationDriver : MonoBehaviour
    {
        [SerializeField]
        private float ticksPerSecond = 4f;

        private LocalVerticalSlice simulation;
        private float accumulator;

        public VerticalSliceSnapshot LatestSnapshot { get; private set; }

        public IReadOnlyList<ISimulationEvent> LatestEvents { get; private set; } = new List<ISimulationEvent>();

        public MatchSummary? LatestMatchSummary { get; private set; }

        public ReplayRecord? LatestReplay { get; private set; }

        public void Initialize(LocalVerticalSlice localSimulation)
        {
            simulation = localSimulation;
            LatestSnapshot = simulation.GetSnapshot();
            LatestMatchSummary = simulation.MatchSummary;
            LatestReplay = simulation.GetReplayRecord();
        }

        private void Update()
        {
            if (simulation is null)
            {
                return;
            }

            accumulator += Time.deltaTime;
            var tickDuration = 1f / ticksPerSecond;
            while (accumulator >= tickDuration)
            {
                simulation.AdvanceOneTick();
                accumulator -= tickDuration;
            }

            LatestSnapshot = simulation.GetSnapshot();
            LatestEvents = simulation.DrainEvents();
            LatestMatchSummary = simulation.MatchSummary;
            LatestReplay = simulation.GetReplayRecord();
        }

        public void ResetMatch()
        {
            simulation?.Reset();
            if (simulation is not null)
            {
                LatestSnapshot = simulation.GetSnapshot();
                LatestMatchSummary = simulation.MatchSummary;
                LatestReplay = simulation.GetReplayRecord();
            }
        }
    }
}
