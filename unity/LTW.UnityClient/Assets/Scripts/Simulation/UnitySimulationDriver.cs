#nullable enable

using System.Collections.Generic;
using LTW.Simulation.Bridge;
using LTW.Simulation.Events;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using LTW.Simulation.Replay;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class UnitySimulationDriver : MonoBehaviour
    {
        [SerializeField]
        private float ticksPerSecond = 4f;

        [SerializeField]
        private float openingBuildCountdownSeconds = 30f;

        private LocalVerticalSlice simulation = null!;
        private float accumulator;
        private float openingBuildCountdownEndsAt;

        public bool HasStarted { get; private set; }

        public bool IsOpeningBuildCountdown { get; private set; }

        public float OpeningBuildCountdownRemaining { get; private set; }

        public float OpeningBuildCountdownDuration => openingBuildCountdownSeconds;

        public bool IsPaused { get; private set; } = true;

        public VerticalSliceSnapshot LatestSnapshot { get; private set; } = null!;

        public IReadOnlyList<ISimulationEvent> LatestEvents { get; private set; } = new List<ISimulationEvent>();

        public MatchSummary? LatestMatchSummary { get; private set; }

        public ReplayRecord? LatestReplay { get; private set; }

        public BotDiagnosticsSnapshot? LatestBotDiagnostics { get; private set; }

        /// <summary>
        /// The seat this client drives, surfaced so HUD and input code stop assuming player 1.
        /// Falls back to seat 1 only before <see cref="Initialize"/> has run, matching the previous
        /// hardcoded behavior for that window rather than throwing during scene startup.
        /// </summary>
        public PlayerId LocalPlayerId => simulation is null ? new PlayerId(1) : simulation.LocalPlayerId;

        /// <summary>The lane the local seat defends. Derived from the seat, never hardcoded.</summary>
        public LaneId LocalPlayerLaneId => simulation is null ? new LaneId(1) : simulation.LocalPlayerLaneId;

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

            if (IsOpeningBuildCountdown)
            {
                OpeningBuildCountdownRemaining = Mathf.Max(0f, openingBuildCountdownEndsAt - Time.unscaledTime);
                if (OpeningBuildCountdownRemaining <= 0f)
                {
                    StartMatch();
                }
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

            RefreshSnapshot(drainEvents: true);
        }

        public void RefreshSnapshot(bool drainEvents = false)
        {
            if (simulation is null)
            {
                return;
            }

            LatestSnapshot = simulation.GetSnapshot();
            if (drainEvents)
            {
                LatestEvents = simulation.DrainEvents();
            }

            LatestMatchSummary = simulation.MatchSummary;
            LatestReplay = simulation.GetReplayRecord();
            LatestBotDiagnostics = simulation.GetBotDiagnostics();
        }

        public void BeginOpeningBuildCountdown()
        {
            if (simulation is null)
            {
                return;
            }

            if (LatestMatchSummary is not null)
            {
                ResetMatch();
            }

            accumulator = 0f;
            HasStarted = false;
            IsPaused = true;
            IsOpeningBuildCountdown = true;
            OpeningBuildCountdownRemaining = Mathf.Max(1f, openingBuildCountdownSeconds);
            openingBuildCountdownEndsAt = Time.unscaledTime + OpeningBuildCountdownRemaining;
            RefreshSnapshot(drainEvents: true);
        }

        public void StartMatch()
        {
            IsOpeningBuildCountdown = false;
            OpeningBuildCountdownRemaining = 0f;
            simulation?.StartMatch();
            HasStarted = true;
            IsPaused = false;
            RefreshSnapshot(drainEvents: true);
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
            if (IsOpeningBuildCountdown)
            {
                StartMatch();
                return;
            }

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
            openingBuildCountdownEndsAt = 0f;
            IsOpeningBuildCountdown = false;
            OpeningBuildCountdownRemaining = 0f;
            HasStarted = false;
            IsPaused = true;
            if (simulation is not null)
            {
                RefreshSnapshot();
            }
        }
    }
}
