#nullable enable

using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using LTW.Simulation.Replay;
using LTW.UnityClient.Online;
using LTW.UnityClient.Online.Wire;
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
        /// Set instead of <see cref="simulation"/> for a networked match — see
        /// <see cref="Initialize(MatchWireClient)"/>. The two are mutually exclusive; every branch
        /// below that reads <c>simulation</c> has a wire-mode counterpart reading this instead.
        /// </summary>
        private MatchWireClient? wireClient;

        private ContentCatalog? wireContent;
        private int wireLocalSeat = 1;
        private long wireLastAppliedTick = long.MinValue;
        private int nextPredictedEntityId = -1;
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
        public LTW.Simulation.Content.ContentCatalog? Content => simulation is not null ? simulation.Content : wireContent;

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
        public PlayerId LocalPlayerId => simulation is not null ? simulation.LocalPlayerId : new PlayerId(wireClient is not null ? wireLocalSeat : 1);

        /// <summary>The lane the local seat defends. Derived from the seat, never hardcoded.</summary>
        public LaneId LocalPlayerLaneId => simulation is not null ? simulation.LocalPlayerLaneId : new LaneId(wireClient is not null ? wireLocalSeat : 1);

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
        /// <remarks>
        /// The 50-tick fallback matches <c>LocalVerticalSlice</c>'s own hardcoded
        /// <c>incomeIntervalTicks: 50</c> — a fixed simulation rule, not something a match ever
        /// varies, so a wire-backed match (which never constructs a local
        /// <c>LocalVerticalSlice</c> to read this from) is safe to assume the same constant rather
        /// than needing the server to say so on the wire.
        /// </remarks>
        public int IncomeIntervalTicks => simulation is null ? 50 : simulation.IncomeIntervalTicks;

        /// <summary>
        /// Cells a creep walks in <paramref name="laneId"/> right now — the measure of its maze.
        /// </summary>
        /// <remarks>
        /// A pass-through, because the slice is private to this driver and the tutorial director
        /// needs to ask "has the player bent the path yet?" without being handed the simulation.
        /// Zero before <see cref="Initialize"/>, matching how the slice answers an unknown lane.
        /// Also zero for a wire-backed match, which has no local slice to ask at all — safe because
        /// the tutorial never runs online (docs/MULTIPLAYER_ROLLOUT.md's MP-06: "Practice and the
        /// tutorial stay local, never touch the server"), so this driver's only caller for this
        /// method is never live at the same time <see cref="wireClient"/> is set.
        /// </remarks>
        public int RouteLength(LaneId laneId) => simulation is null ? 0 : simulation.RouteLength(laneId);

        /// <summary>Cells of the unmazed route in <paramref name="laneId"/>. See <see cref="RouteLength"/>.</summary>
        public int DirectRouteLength(LaneId laneId) => simulation is null ? 0 : simulation.DirectRouteLength(laneId);

        public void Initialize(LocalVerticalSlice localSimulation)
        {
            simulation = localSimulation;
            wireClient = null;
            SnapshotRevision = long.MinValue;
            RefreshSnapshot();
        }

        /// <summary>
        /// The wire-backed equivalent of <see cref="Initialize(LocalVerticalSlice)"/> — see
        /// docs/MULTIPLAYER_ROLLOUT.md's MP-06. <paramref name="client"/> must already be connected
        /// (see <see cref="Online.OnlineMatchService.CreateAndJoinAsync"/>); this only starts
        /// pumping it every frame from <see cref="Update"/>.
        /// </summary>
        public void Initialize(MatchWireClient client)
        {
            simulation = null!;
            wireClient = client;
            wireContent = SampleVerticalSliceContent.Create();
            wireLocalSeat = 1;
            wireLastAppliedTick = long.MinValue;
            SnapshotRevision = long.MinValue;
            HasStarted = false;
            IsPaused = true;
            IsOpeningBuildCountdown = false;
            LatestMatchSummary = null;
            LatestEvents = new List<ISimulationEvent>();
        }

        /// <summary>
        /// Replaces the match with a fresh one built from <see cref="LocalMatchRuntimeOptions.PendingOptions"/>.
        /// </summary>
        /// <remarks>
        /// Needed because bot profiles are fixed at construction: <c>LocalVerticalSlice</c> builds
        /// its <c>BotController</c>s in its constructor from the options it was given, and
        /// <c>Reset()</c> keeps both the options and the bots. <c>PendingOptions</c> was, until
        /// practice, read exactly once — by <c>UnityMatchBootstrapper</c> at scene load — so nothing
        /// set after that could ever reach a match. This is the one path that can.
        ///
        /// The command adapter is re-pointed here rather than by whoever calls this, because it is
        /// the only other component holding the slice (everything else reads through this driver),
        /// and it lives on the same GameObject by the launcher's construction. A caller that had to
        /// remember to re-initialise it separately would, one day, not — and every tap would then
        /// go to a board nobody is looking at.
        ///
        /// Ends in the same state as <see cref="ResetMatch"/> — not started, paused, no countdown —
        /// so callers sequence it exactly where they used to sequence a reset. Draining the fresh
        /// slice's (empty) events here keeps a consumer that runs before this driver's next Update
        /// from replaying the old match's last frame of events over the new board.
        /// </remarks>
        public void RebuildMatchFromPendingOptions()
        {
            var rebuilt = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchRuntimeOptions.PendingOptions);
            Initialize(rebuilt);
            if (TryGetComponent<UnityCommandAdapter>(out var commandAdapter))
            {
                commandAdapter.Initialize(rebuilt, this);
            }

            ResetMatch();
            RefreshSnapshot(drainEvents: true);
        }

        private void Update()
        {
            if (wireClient is not null)
            {
                UpdateWire(wireClient);
                return;
            }

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
        /// The wire-mode equivalent of the local branch above: pump the socket, and if a genuinely
        /// new tick arrived, rebuild <see cref="LatestSnapshot"/> from it. There is deliberately no
        /// local tick-advancing loop here — the server is the only clock for a networked match.
        /// </summary>
        /// <remarks>
        /// <see cref="LatestEvents"/> stays empty and <see cref="LatestMatchSummary"/> stays null
        /// for a wire-backed match — see docs/MULTIPLAYER_ROLLOUT.md's MP-06 "Landed" for why: the
        /// wire has no fixed per-event-kind DTO catalog (<c>EventDto.Data</c> is the sender's own
        /// concrete type serialized generically), so a client would need a mirror of every
        /// <c>LTW.Simulation.Events.*</c> shape to consume it losslessly. Event-driven VFX/audio and
        /// the results screen's automatic match-end detection are consequently a scoped-out gap for
        /// this pass, not an oversight — the board, HUD and commands all work regardless, since
        /// those read <see cref="LatestSnapshot"/>, not the event stream.
        /// </remarks>
        private void UpdateWire(MatchWireClient client)
        {
            client.Pump();

            if (client.Welcome is { } welcome)
            {
                wireLocalSeat = welcome.Seat;
                HasStarted = true;
                IsPaused = false;
            }

            if (client.LatestTick is { } tick && tick.Tick != wireLastAppliedTick)
            {
                wireLastAppliedTick = tick.Tick;
                LatestSnapshot = BuildSnapshotFromWire(tick, client.PendingPredictions);
                SnapshotRevision = tick.Tick;
            }
        }

        /// <summary>
        /// Reconstructs one <see cref="PlayerEconomyState"/> from wire data, including its
        /// committed tower line and per-category tiers — a gap the first MP-06 pass left, found in
        /// a self-audit immediately after rather than by a live test. Missing this meant
        /// <c>ChosenTowerLine</c> always read as uncommitted and every tier always read as base,
        /// which silently broke tier purchases past the first:
        /// <c>UnityCommandAdapter.BuyCategoryTier</c>'s wire-sent <c>TargetTier</c> is
        /// <c>current + 1</c>, computed from exactly these fields.
        /// </summary>
        /// <remarks>
        /// The simple public <c>PlayerEconomyState</c> constructor cannot set these — only the
        /// private, fuller constructor can, and this project does not have access to it. Built up
        /// instead via the class's own <c>With*</c> methods, the same way any other code in
        /// <c>LTW.Simulation</c> would have to.
        /// </remarks>
        private static PlayerEconomyState BuildPlayerFromWire(PlayerSnapshotDto player)
        {
            var state = new PlayerEconomyState(
                new PlayerId(player.PlayerId),
                new LTW.Simulation.Primitives.Gold(player.Gold),
                new LTW.Simulation.Primitives.Income(player.Income),
                // A player's own invariant (WithLives sets isEliminated when lives hit zero) is
                // relied on here rather than duplicated: an eliminated player's wire Lives is
                // always 0, so the simple public constructor alone already gets IsEliminated
                // right without needing the private full constructor this class does not expose.
                new LTW.Simulation.Primitives.Lives(player.Eliminated ? 0 : player.Lives));

            if (player.ChosenTowerLine != PlayerEconomyState.UnchosenTowerLine)
            {
                state = state.WithChosenTowerLine(player.ChosenTowerLine);
            }

            for (var i = 0; i < player.TowerLineTiers.Length; i++)
            {
                state = state.WithTowerLineTier(i, player.TowerLineTiers[i]);
            }

            for (var i = 0; i < player.SendCategoryTiers.Length; i++)
            {
                state = state.WithSendCategoryTier(i, player.SendCategoryTiers[i]);
            }

            return state;
        }

        /// <summary>
        /// Reconstructs a real <see cref="VerticalSliceSnapshot"/> from wire data — not a
        /// wire-shaped substitute — so every existing reader of this driver's
        /// <see cref="LatestSnapshot"/> (the renderer, the HUD, six other scripts — see
        /// docs/MULTIPLAYER_ROLLOUT.md's MP-06 investigation) works completely unchanged. Verified
        /// against <c>PlayerEconomyState</c>/<c>TowerCombatState</c>/<c>CreepPresentationSnapshot</c>'s
        /// own public constructors rather than assumed.
        /// </summary>
        /// <remarks>
        /// <see cref="VerticalSliceSnapshot"/>'s tower-aim-target, bramble-cell, send-queue and
        /// seat-table fields are not on the wire yet (only players/towers/creeps are — MP-04's own
        /// scoping note, extended for creeps this pass but not the rest) and are passed as empty
        /// here. Safe: every reader treats an empty collection as "nothing to show" rather than
        /// asserting non-empty, so this degrades to no aim-turn animation / no bramble decals / no
        /// send-queue badges / no seat leaderboard for a networked match rather than crashing.
        /// </remarks>
        private VerticalSliceSnapshot BuildSnapshotFromWire(TickMessage tick, IReadOnlyCollection<PendingPrediction> predictions)
        {
            var players = tick.Players.Select(BuildPlayerFromWire);

            var towers = tick.Towers.Select(tower => new TowerCombatState(
                new LTW.Simulation.Primitives.EntityId(tower.EntityId),
                new LTW.Simulation.Content.ContentId(tower.TowerId),
                new PlayerId(tower.OwnerId),
                new LaneId(tower.LaneId),
                new GridPosition(tower.X, tower.Y),
                tower.Tier)).ToList();

            var creeps = tick.Creeps.Select(creep => new CreepPresentationSnapshot(
                new LTW.Simulation.Primitives.EntityId(creep.EntityId),
                new LTW.Simulation.Content.ContentId(creep.CreepId),
                new PlayerId(creep.SenderId),
                new LaneId(creep.LaneId),
                new GridPosition(creep.X, creep.Y),
                creep.Health,
                creep.MaxHealth,
                creep.SpeedPerSecond,
                new GridPosition(creep.NextX, creep.NextY),
                creep.MovementProgress,
                // EffectiveMovementCost already has the bramble penalty folded in server-side (see
                // CreepSnapshotDto's own remarks) — passed as MovementCost with IsBraked forced
                // false so this snapshot's OWN EffectiveMovementCost getter (MovementCost +
                // penalty-if-braked) does not apply that penalty a second time.
                creep.EffectiveMovementCost,
                isBraked: false)).ToList();

            foreach (var prediction in predictions)
            {
                switch (prediction.Kind)
                {
                    case PendingPredictionKind.PlaceTower:
                        towers.Add(new TowerCombatState(
                            new LTW.Simulation.Primitives.EntityId(nextPredictedEntityId--),
                            new LTW.Simulation.Content.ContentId(prediction.TowerId),
                            LocalPlayerId,
                            new LaneId(prediction.LaneId),
                            new GridPosition(prediction.X, prediction.Y)));
                        break;
                    case PendingPredictionKind.SellTower:
                        towers.RemoveAll(t => t.OwnerId.Equals(LocalPlayerId) && t.LaneId.Value == prediction.LaneId && t.Position.X == prediction.X && t.Position.Y == prediction.Y);
                        break;
                    case PendingPredictionKind.UpgradeTower:
                        var index = towers.FindIndex(t => t.OwnerId.Equals(LocalPlayerId) && t.LaneId.Value == prediction.LaneId && t.Position.X == prediction.X && t.Position.Y == prediction.Y);
                        if (index >= 0)
                        {
                            var current = towers[index];
                            towers[index] = new TowerCombatState(current.EntityId, current.TowerId, current.OwnerId, current.LaneId, current.Position, current.Tier + 1);
                        }

                        break;
                }
            }

            return new VerticalSliceSnapshot(
                new SimulationTick(tick.Tick),
                new EconomyPlayerSet(players),
                creeps,
                towers,
                towerAimTargets: System.Array.Empty<TowerAimSnapshot>(),
                brambleCells: new Dictionary<LaneId, IReadOnlyList<GridPosition>>());
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
