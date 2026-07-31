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

        /// <summary>
        /// Simulation ticks per second, so presentation can convert a tick count from an event into
        /// a duration in seconds. The Foundry mortar needs this to animate a shell's flight for
        /// exactly as long as the simulation says it takes.
        /// </summary>
        public float TicksPerSecond => ticksPerSecond;

        [SerializeField]
        private float openingBuildCountdownSeconds = 30f;

        private LocalVerticalSlice simulation = null!;
        /// <summary>
        /// Ceiling on simulation ticks advanced in a single frame.
        /// </summary>
        /// <remarks>
        /// The catch-up loop below was unbounded, which is the classic fixed-timestep spiral of
        /// death: a frame that runs long banks a large deltaTime, which runs even longer, and the
        /// game never recovers. At the shipped 4 ticks/second it stayed hidden, because a frame would
        /// have to stall for seconds to bank a meaningful backlog.
        ///
        /// The batch playtest runner is where it actually bit. It sets ticksPerSecond to 1200 and
        /// timeScale to 20 to run a match quickly, so a single 0.33s frame (Unity's default
        /// maximumDeltaTime) banks 0.33 x 20 x 1200 = roughly 8,000 ticks — an entire match, attempted
        /// inside one frame, while every creep and tower is simulated. Runs stalled on frame 3 and
        /// looked like a hang.
        ///
        /// 250 is chosen to be far above any real frame's need at 4 ticks/second (that is 62 seconds
        /// of simulation in one frame) while still bounding the worst case to something that finishes.
        /// </remarks>
        private const int MaxTicksPerFrame = 250;

        private float accumulator;
        private float openingBuildCountdownEndsAt;

        public bool HasStarted { get; private set; }

        public bool IsOpeningBuildCountdown { get; private set; }

        public float OpeningBuildCountdownRemaining { get; private set; }

        public float OpeningBuildCountdownDuration => openingBuildCountdownSeconds;

        public bool IsPaused { get; private set; } = true;

        public VerticalSliceSnapshot LatestSnapshot { get; private set; } = null!;

        /// <summary>
        /// The authored content catalog, so presentation can read a tower's real stats — range and
        /// the like — instead of keeping a client-side copy that goes stale. Same reasoning as
        /// UnityCommandAdapter reading cost from here rather than hardcoding it.
        /// </summary>
        public LTW.Simulation.Content.ContentCatalog? Content => simulation?.Content;

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

        /// <summary>
        /// Ticks between income payouts, read from the sim rather than a client-side copy
        /// (OPEN_ITEMS.md item 24 — HudView used to hardcode this separately). Falls back to the
        /// sim's current default only before <see cref="Initialize"/> has run.
        /// </summary>
        public int IncomeIntervalTicks => simulation is null ? 50 : simulation.IncomeIntervalTicks;

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
                var ticksThisFrame = 0;
                while (accumulator >= tickDuration && ticksThisFrame < MaxTicksPerFrame)
                {
                    simulation.AdvanceOneTick();
                    accumulator -= tickDuration;
                    ticksThisFrame++;
                }

                // Drop the backlog rather than carry it. Keeping it is what turns one slow frame into
                // a permanent one: the catch-up work makes the next frame slower still, which banks
                // more catch-up, and the loop never gets back to real time.
                if (ticksThisFrame >= MaxTicksPerFrame)
                {
                    accumulator = 0f;
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
