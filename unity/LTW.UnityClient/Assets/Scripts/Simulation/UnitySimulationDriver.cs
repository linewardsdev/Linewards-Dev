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

        public bool HasStarted { get; private set; }

        public bool IsPaused { get; private set; } = true;

        public VerticalSliceSnapshot LatestSnapshot { get; private set; }

        public IReadOnlyList<ISimulationEvent> LatestEvents { get; private set; } = new List<ISimulationEvent>();

        public MatchSummary? LatestMatchSummary { get; private set; }

        public ReplayRecord? LatestReplay { get; private set; }

        public BotDiagnosticsSnapshot? LatestBotDiagnostics { get; private set; }

        public void Initialize(LocalVerticalSlice localSimulation)
        {
            simulation = localSimulation;
            LatestSnapshot = simulation.GetSnapshot();
            LatestMatchSummary = simulation.MatchSummary;
            LatestReplay = simulation.GetReplayRecord();
            LatestBotDiagnostics = simulation.GetBotDiagnostics();
        }

        private void Update()
        {
            if (simulation is null)
            {
                return;
            }

            if (HasStarted && !IsPaused && LatestMatchSummary is null)
            {
                accumulator += Time.deltaTime;
                var tickDuration = 1f / ticksPerSecond;
                while (accumulator >= tickDuration)
                {
                    simulation.AdvanceOneTick();
                    accumulator -= tickDuration;
                }
            }

            LatestSnapshot = simulation.GetSnapshot();
            LatestEvents = simulation.DrainEvents();
            LatestMatchSummary = simulation.MatchSummary;
            LatestReplay = simulation.GetReplayRecord();
            LatestBotDiagnostics = simulation.GetBotDiagnostics();
        }

        public void StartMatch()
        {
            HasStarted = true;
            IsPaused = false;
        }

        public void PauseMatch()
        {
            if (HasStarted)
            {
                IsPaused = true;
            }
        }

        public void TogglePause()
        {
            if (!HasStarted)
            {
                StartMatch();
                return;
            }

            IsPaused = !IsPaused;
        }

        public void ResetMatch()
        {
            simulation?.Reset();
            accumulator = 0f;
            HasStarted = false;
            IsPaused = true;
            if (simulation is not null)
            {
                LatestSnapshot = simulation.GetSnapshot();
                LatestMatchSummary = simulation.MatchSummary;
                LatestReplay = simulation.GetReplayRecord();
                LatestBotDiagnostics = simulation.GetBotDiagnostics();
            }
        }
    }
}
