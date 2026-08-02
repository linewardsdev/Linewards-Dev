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

        /// <summary>
        /// The simulation revision <see cref="LatestSnapshot"/> was built at, for consumers that do
        /// per-snapshot rather than per-frame work.
        /// </summary>
        /// <remarks>
        /// Published so a component reading this driver every frame can answer "is this the same
        /// board I already handled?" with one comparison and no allocation. It is the same value the
        /// gate below uses, so a consumer cannot disagree with the driver about what is new.
        ///
        /// It is strictly FINER-grained than <c>UnityVerticalSliceRenderer</c>'s snapshot hash — it
        /// moves when a seat's gold moves, which that hash ignores — and that direction is the safe
        /// one. It means this driver can republish a snapshot the renderer then decides is unchanged,
        /// which costs a hash. The dangerous direction cannot happen: the renderer's hash is a pure
        /// function of the snapshot, and the snapshot is a pure function of the state this counts, so
        /// there is no board the renderer would call new that this calls unchanged.
        /// </remarks>
        public long SnapshotRevision { get; private set; } = long.MinValue;

        /// <summary>
        /// Send-only telemetry for the match so far. Built on demand — nothing publishes it.
        /// </summary>
        /// <remarks>
        /// This used to be republished on every frame alongside the snapshot, and it was the single
        /// most expensive thing in that refresh: <c>ReplayRecord</c>'s constructor copies the entire
        /// accepted-command list of the match so far, and seed 1 finishes with 5,754 of them
        /// (OPEN_ITEMS item 36). Nothing on the frame path needs it. Its three readers are
        /// <c>LocalReplayExporter</c> and <c>LocalPlaytestRecorder</c>, which want it when a match
        /// ends or a human asks for an export, and <c>LocalPlaytestBatchRunner</c>, which reads it
        /// once at the end of a batch run.
        ///
        /// So it is a call, not a cache, and deliberately not memoised: the freshest possible answer
        /// costs the same as a stale one at the rate anybody actually asks, and a cache here would
        /// need its own invalidation to avoid exporting a replay that stops one command short.
        /// </remarks>
        public ReplayRecord? LatestReplay => simulation?.GetReplayRecord();

        /// <summary>
        /// Bot profiles and their recent sends. Built on demand — see <see cref="LatestReplay"/>.
        /// </summary>
        /// <remarks>
        /// Same treatment and the same reason: its only frame-path reader is
        /// <c>DiagnosticsOverlay</c>, which is off unless <c>-ltwDiagnostics</c> was passed and which
        /// now rebuilds its text per snapshot rather than per frame, so this is reached at the tick
        /// rate rather than the frame rate even with the overlay on.
        /// </remarks>
        public BotDiagnosticsSnapshot? LatestBotDiagnostics => simulation?.GetBotDiagnostics();

        /// <summary>
        /// The seat this client drives, surfaced so HUD and input code stop assuming player 1.
        /// Falls back to seat 1 only before <see cref="Initialize"/> has run, matching the previous
        /// hardcoded behavior for that window rather than throwing during scene startup.
        /// </summary>
        public PlayerId LocalPlayerId => simulation is null ? new PlayerId(1) : simulation.LocalPlayerId;

        /// <summary>The lane the local seat defends. Derived from the seat, never hardcoded.</summary>
        public LaneId LocalPlayerLaneId => simulation is null ? new LaneId(1) : simulation.LocalPlayerLaneId;

        /// <summary>
        /// Whether the local seat is out of the match, so its command surfaces must stand down.
        /// </summary>
        /// <remarks>
        /// A derived read rather than a published flag, which is where this deliberately differs
        /// from <see cref="UI.RuntimeUiChrome.ModalScreenActive"/>. That one has to be a flag because
        /// it depends on state private to LocalSessionFlowOverlay — a settings panel being open —
        /// that nothing else can see. Elimination is already a fact of the snapshot every HUD
        /// component holds, so publishing a second copy of it would only create something that can
        /// disagree with the simulation, and would stop working if the overlay were ever absent.
        ///
        /// Answers false rather than throwing before <see cref="Initialize"/> has run, matching
        /// <see cref="LocalPlayerId"/>: a HUD that has not been handed a match yet is not eliminated.
        /// </remarks>
        public bool IsLocalSeatEliminated =>
            LatestSnapshot is not null && LatestSnapshot.Players.Get(LocalPlayerId).IsEliminated;

        /// <summary>
        /// Ticks between income payouts, read from the sim rather than a client-side copy
        /// (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items — HudView used to hardcode this separately). Falls back to the
        /// sim's current default only before <see cref="Initialize"/> has run.
        /// </summary>
        public int IncomeIntervalTicks => simulation is null ? 50 : simulation.IncomeIntervalTicks;

        public void Initialize(LocalVerticalSlice localSimulation)
        {
            simulation = localSimulation;
            SnapshotRevision = long.MinValue;
            RefreshSnapshot();
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

        /// <summary>
        /// Republishes <see cref="LatestSnapshot"/> if — and only if — the simulation has moved.
        /// </summary>
        /// <remarks>
        /// This is called from <see cref="Update"/> on every frame, at ~60 fps, against a simulation
        /// ticking at 4 Hz, so fourteen calls in fifteen have nothing new to publish. It used to
        /// rebuild everything anyway — an order of magnitude more than the whole renderer after
        /// OPEN_ITEMS item 24, and the reason that item's win showed up as more frames rather than
        /// less garbage. Measured by <c>RendererAllocationProbe</c> on a paused seed-1 board at tick
        /// 3160 (576 creeps, 432 towers), renderer off so nothing else is running: 619.7 KB/frame
        /// above the same session's idle floor before, 0.0 KB/frame after — the floor itself.
        ///
        /// <b>The snapshot's defensive copy is untouched, and that is the point.</b>
        /// <c>VerticalSliceSnapshot</c> copies the creep, tower and aim-target lists on purpose, so a
        /// caller holding an old snapshot cannot watch it mutate underneath. Making it shallow would
        /// have been the wrong fix and would have broken exactly the callers this driver serves —
        /// the fix is to stop building a snapshot that is going to be identical, not to make the
        /// snapshot cheaper to be wrong with.
        ///
        /// <b>Why the gate is the simulation's revision and not the tick.</b> Commands apply
        /// synchronously, and the opening build countdown is thirty seconds in which the tick never
        /// advances while the player builds — <c>UnityCommandAdapter.PlaceTower</c> is deliberately
        /// not gated on the match having started, unlike every other command there. A tick gate
        /// leaves a tower built during the countdown invisible until the match starts, and that is
        /// measured rather than reasoned: <c>OpeningCountdownFreshnessCheck</c> passes against this
        /// gate and fails against a build with the non-tick half of the revision removed, with the
        /// board still reporting zero towers. <c>LocalVerticalSlice</c> counts its own state changes
        /// on the field writes rather than at call sites, so a synchronous command bumps the revision
        /// in the same breath as it changes the board. Why not the renderer's snapshot hash, which
        /// answers the same question one layer up: computing it needs a snapshot, and building the
        /// snapshot is the cost being avoided.
        ///
        /// <see cref="LatestEvents"/> is still drained every frame it is asked for, unchanged, and
        /// deliberately not put behind the gate. An event is consumed once, so a frame that skipped
        /// the drain would lose it, and there is no revision to hang it on anyway — draining is
        /// itself a mutation. It is also nearly free between ticks, where there is nothing to drain:
        /// what is left of this method after the gate measures inside the probe's noise floor.
        /// </remarks>
        public void RefreshSnapshot(bool drainEvents = false)
        {
            if (simulation is null)
            {
                return;
            }

            var revision = simulation.StateRevision;
            if (revision != SnapshotRevision || LatestSnapshot is null)
            {
                LatestSnapshot = simulation.GetSnapshot();
                SnapshotRevision = revision;
            }

            if (drainEvents)
            {
                LatestEvents = simulation.DrainEvents();
            }

            LatestMatchSummary = simulation.MatchSummary;
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
