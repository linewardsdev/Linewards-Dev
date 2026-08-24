using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Combat;
using LTW.Simulation.Authority;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Events;
using LTW.Simulation.Pathing;
using LTW.Simulation.Primitives;
using LTW.Simulation.Replay;

namespace LTW.Simulation.Bridge;

public sealed class LocalVerticalSlice
{
    /// <summary>Lives every seat opens with.</summary>
    /// <remarks>
    /// 100 as of 2026-08-09, down from 220. Reported from an M4 iPad with 8 GB: matches run too long
    /// and accumulate enough live creeps to slow the device down.
    ///
    /// Lives are the match clock here, not just a fail condition — a seat is eliminated when they
    /// reach zero, so halving them halves how much leaking a match has to absorb before it resolves.
    /// That shortens the tail where the board is busiest, which is the part that costs frames: creep
    /// count grows with match length, and MatchEscalationRules raises sent-creep HEALTH over time,
    /// so late creeps also survive longer and stack up.
    ///
    /// Halving rather than tuning to a target tick count: the escalation rules already close matches
    /// on their own schedule, and picking a lives number to hit a duration would be fitting one
    /// mechanism to another's timing. This changes how much damage a seat can take, and lets
    /// escalation keep doing what it does.
    /// </remarks>
    private const int StartingLives = 40;

    private readonly ContentCatalog content;
    private readonly EconomyService economy;
    private readonly GridPathService pathService;
    private readonly CombatService combat;
    private readonly CommandContentValidator commandValidator;
    private readonly Dictionary<LaneId, LaneGrid> grids;
    private readonly Dictionary<LaneId, IReadOnlyList<GridPosition>> routes;

    /// <summary>
    /// The content catalog, indexed by id.
    /// </summary>
    /// <remarks>
    /// The catalog is a flat list, and resolving a definition out of it with First/FirstOrDefault
    /// is a scan. That is invisible at a 15-tower roster, but these sit in per-tick paths — the bot
    /// build, tier and upgrade passes each resolve definitions for every tower a bot owns, every
    /// tick, and the upgrade-eligibility scan resolves one per candidate. CombatContent already
    /// indexes the same catalog for exactly this reason; this is the bridge doing the same rather
    /// than keeping a scan per lookup.
    /// </remarks>
    private readonly Dictionary<ContentId, TowerDefinition> towersById;
    private readonly Dictionary<ContentId, CreepDefinition> creepsById;

    /// <summary>The unmazed route per lane, for creeps that ignore the maze.</summary>
    /// <remarks>
    /// Holds the route each lane had before a single tower existed. <see cref="routes"/> is
    /// reassigned on every placement, sell and elimination; this is written once and never again,
    /// which is the property that makes flying cheap rather than a second pathfind per build.
    /// </remarks>
    private readonly Dictionary<LaneId, IReadOnlyList<GridPosition>> directRoutes;

    /// <summary>
    /// Both routes, as combat wants them. Holds the live dictionaries rather than copies, so the
    /// mazed side keeps reflecting rebuilds without this needing to be rebuilt with it.
    /// </summary>
    private readonly LaneRouteSet routeSet;
    // Not readonly: creeps, towers and lane ownership are fixed for a match but category tiers are
    // not, and combat reads them through this. Refreshed once per tick in AdvanceOneTick.
    private CombatContent combatContent;
    private readonly LocalMatchOptions options;
    private readonly LocalMatchTopology topology;
    private readonly List<ISimulationEvent> pendingEvents = new();
    private readonly Dictionary<PlayerId, BotController> bots;

    /// <summary>What the bots are allowed to see of this match, and how they act on it.</summary>
    /// <remarks>
    /// One instance for the whole match rather than one per bot per tick: it holds nothing but a
    /// reference back to this slice, and every method takes the player it is asking about.
    /// </remarks>
    private readonly IBotMatchContext botMatch;

    private readonly List<AcceptedCommandRecord> acceptedCommands = new();
    private readonly List<BotDecisionRecord> botDecisionRecords = new();

    private EconomyPlayerSet playersState = null!;
    private CombatState combatStateValue = null!;
    private SimulationTick tickValue;
    private long stateRevision;
    private long nextEntityId = 1;
    private bool matchStarted;
    private bool matchEnded;

    /// <summary>
    /// Changes exactly when something <see cref="GetSnapshot"/> would report differently.
    /// </summary>
    /// <remarks>
    /// Exists so a caller can ask "is there a new snapshot?" without paying for one. That question
    /// has no cheap answer from outside: <see cref="GetSnapshot"/> copies the creep, tower and
    /// aim-target lists on purpose, so that a caller holding an old snapshot cannot watch it change
    /// underneath, and building one only to discover it is identical is exactly the cost this
    /// avoids. <c>UnitySimulationDriver</c> was doing that on every frame at ~60 fps against a
    /// simulation ticking at 4 Hz (OPEN_ITEMS item 36).
    ///
    /// <b>The tick number is NOT a substitute, and that is the whole reason this exists.</b>
    /// Commands apply the moment they are accepted rather than on the next tick — <see
    /// cref="PlaceTower"/>, <see cref="UpgradeTower"/>, <see cref="SellTowerAt"/> and <see
    /// cref="BuyCategoryTier"/> all mutate synchronously — and the client's opening build countdown
    /// is thirty seconds in which the tick does not advance at all while the player builds. Gated on
    /// the tick, a tower built during the countdown stays invisible until the match starts — measured,
    /// not argued: the client's <c>OpeningCountdownFreshnessCheck</c> fails on exactly that with the
    /// non-tick half of this counter removed.
    /// <c>UnityVerticalSliceRenderer</c> found the same thing one layer up and gates on a
    /// hash of the snapshot for the same reason; this is that idea moved upstream of the allocation,
    /// where a hash cannot go because hashing requires the snapshot.
    ///
    /// <b>Comprehensive by construction rather than by discipline.</b> It is not incremented at
    /// call sites — it is incremented by the setters of the only four pieces of mutable state <see
    /// cref="GetSnapshot"/> reads (<see cref="players"/>, <see cref="combatState"/>, <see
    /// cref="tick"/>, and lane routes through <see cref="SetRoute"/>), so a mutator added later
    /// cannot forget it without also failing to change anything. The two remaining pieces of mutable
    /// state are deliberately uncounted, because neither can move on its own: <c>combatContent</c> is
    /// reassigned only in <see cref="AdvanceOneTick"/>, three lines after the tick it rides along
    /// with, and <c>grids</c> is read by pathing rather than by the snapshot and only ever changes in
    /// the same breath as a tower and a route.
    ///
    /// Monotonic and never reset, including across <see cref="Reset"/> — a match reset is a change
    /// like any other, and a counter that went backwards there would let a caller mistake the new
    /// match's opening board for the old match's last one.
    /// </remarks>
    public long StateRevision => stateRevision;

    /// <summary>
    /// The seats, and the one write path that records that they moved.
    /// </summary>
    /// <remarks>
    /// A property over a field purely so the revision cannot be bypassed. Every existing
    /// <c>players = ...</c> assignment keeps working unchanged and now counts itself.
    /// </remarks>
    private EconomyPlayerSet players
    {
        get => playersState;
        set
        {
            playersState = value;
            stateRevision++;
        }
    }

    /// <summary>Creeps and towers. See <see cref="players"/> for why this is a property.</summary>
    private CombatState combatState
    {
        get => combatStateValue;
        set
        {
            combatStateValue = value;
            stateRevision++;
        }
    }

    /// <summary>The current tick. See <see cref="players"/> for why this is a property.</summary>
    private SimulationTick tick
    {
        get => tickValue;
        set
        {
            tickValue = value;
            stateRevision++;
        }
    }

    /// <summary>
    /// Rebuilds one lane's route, counting it as a state change.
    /// </summary>
    /// <remarks>
    /// The routes dictionary is the fourth thing the snapshot depends on — creep positions are
    /// interpolated along it — and a dictionary indexer write cannot be intercepted the way the
    /// three fields above can, so it gets a named door instead. Every existing site outside the
    /// constructor happens to sit beside a tower or seat change that would have counted anyway;
    /// routing them all through here means that stays true by construction rather than by
    /// coincidence.
    /// </remarks>
    private void SetRoute(LaneId laneId, IReadOnlyList<GridPosition> route)
    {
        var previous = routes.TryGetValue(laneId, out var existing) ? existing : null;
        routes[laneId] = route;
        RemapCreepsOntoNewRoute(laneId, previous, route);
        stateRevision++;
    }

    /// <summary>
    /// Keeps creeps standing where they were when their lane's route is rebuilt.
    /// </summary>
    /// <remarks>
    /// A creep's position is a PathIndex, and an index only means a place while the route it indexes
    /// is the same route. Building or selling a tower reshapes the lane, and until this existed every
    /// creep in it kept the index it had — so index 12 stopped being the cell it was standing on and
    /// became whatever index 12 happened to be on the new path. Reported from play as creeps jumping
    /// around the board whenever a tower goes down, and it was never cosmetic: the simulation really
    /// did move them, which changes what is in range of what.
    ///
    /// The remap is by POSITION: take the cell the creep was actually standing on, and give it the
    /// index of the nearest cell on the new route. Nearest rather than "same distance from the end"
    /// because a rebuilt route is usually a different LENGTH, so preserving progress-along-the-route
    /// would slide everything backwards the moment a maze got longer — which is a defence buff nobody
    /// asked for and would make building mid-wave the strongest move in the game.
    ///
    /// Ties break to the LOWER index, which is the conservative direction: a creep may be nudged
    /// slightly back along a re-routed path but is never handed free progress for standing still.
    ///
    /// Creeps that ignore the maze are skipped, and must be: they walk the direct route, which this
    /// never touches, so remapping them onto the mazed one would teleport the one unit that is
    /// supposed to be immune to mazing.
    ///
    /// "Nearest" is BOUNDED, and the bound is the whole difference between a nudge and a teleport.
    /// Reported from an iPad: build across the lane until the corridor is walled off, and "the
    /// creeps started going to the right" — they did not walk right, they arrived there in zero
    /// ticks. While a reroute merely bulges around a tower the nearest cell is a step away and this
    /// is invisible; wall the corridor off and the path moves to the far side of the board, so the
    /// nearest cell to a creep standing in the old corridor is several cells away, laterally. Worse
    /// than jarring: the measured case moved a creep from (1,14) onto the exit at (3,15), handing
    /// the attacker a leak that the DEFENDER paid to cause.
    ///
    /// Past the bound the creep keeps its remaining STEPS instead. That still relocates it — the
    /// corridor genuinely moved and a creep's position is only ever an index into the route, so
    /// there is nowhere else for it to be — but it arrives with exactly as much lane left to walk
    /// as it had, rather than being handed the end of it.
    /// </remarks>
    /// <summary>
    /// How far a creep may be snapped to the rebuilt route before the snap is judged a teleport.
    /// </summary>
    /// <remarks>
    /// Two, because a reroute around a single tower puts the replacement cell one step to the side
    /// and a reroute around two adjacent ones puts it two. Beyond that the path has not bulged, it
    /// has moved, and snapping to it stops being the "keep the creep where it is" this method
    /// promises. Deliberately not 1: at 1 an ordinary two-wide bulge would fall through to the
    /// remaining-steps branch, which relocates far more creeps than it needs to.
    /// </remarks>
    private const int MaxSnapDistance = 2;

    private void RemapCreepsOntoNewRoute(LaneId laneId, IReadOnlyList<GridPosition>? previous, IReadOnlyList<GridPosition> route)
    {
        if (previous is null || previous.Count == 0 || route.Count == 0 || ReferenceEquals(previous, route))
        {
            return;
        }

        var moved = new List<CreepCombatState>();
        foreach (var creep in combatState.Creeps)
        {
            if (creep.IsDead || creep.HasLeaked || creep.IgnoresMaze || !creep.LaneId.Equals(laneId))
            {
                continue;
            }

            var standingOn = previous[Math.Min(creep.PathIndex, previous.Count - 1)];
            var remaining = Math.Max(0, previous.Count - 1 - creep.PathIndex);

            // Candidates are restricted to those that leave the creep at least as far from the exit
            // as it already was. Distance alone is not a sufficient guard: the cell two steps to the
            // side can be the LAST cell of the rebuilt route, so a short spatial hop hands over
            // every step the creep still owed. That is exactly the measured failure — a creep at
            // (1,15) snapped to the exit at (3,15), two cells away and zero steps from leaking.
            //
            // The allowance is what keeps SELLING honest. Removing a tower shortens the lane, and
            // then every creep in it legitimately owes fewer steps than before — so a flat "never
            // owe less" would have nothing to pick and would fall through to the branch below, which
            // clamps to zero and fires the whole lane back to the spawn. A creep may give up exactly
            // as many steps as the lane itself lost, and no more.
            var allowance = Math.Max(0, previous.Count - route.Count);
            var nearest = -1;
            var bestDistance = int.MaxValue;
            for (var index = 0; index < route.Count; index++)
            {
                if (route.Count - 1 - index < remaining - allowance)
                {
                    continue;
                }

                var candidate = route[index];
                var distance = Math.Abs(candidate.X - standingOn.X) + Math.Abs(candidate.Y - standingOn.Y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = index;
                }
            }

            if (nearest < 0 || bestDistance > MaxSnapDistance)
            {
                // Either the corridor moved out from under this creep entirely — the nearest cell is
                // somewhere across the board — or the rebuilt route is too short to preserve what it
                // still owed. Keep the number of steps it has left to walk and let it land wherever
                // that many steps from the end happens to be.
                nearest = Math.Max(0, Math.Min(route.Count - 1, route.Count - 1 - remaining));
            }

            if (nearest != creep.PathIndex)
            {
                // Movement progress is dropped rather than carried. It is a fraction of the step INTO
                // the next cell of the old route, and that next cell is generally somewhere else now,
                // so carrying it would advance the creep along a step it never started.
                moved.Add(creep.WithMovement(nearest, 0));
            }
        }

        foreach (var creep in moved)
        {
            combatState = combatState.ReplaceCreep(creep);
        }
    }

    public MatchSummary? MatchSummary { get; private set; }

    public int IncomeIntervalTicks => economy.IncomeIntervalTicks;

    /// <summary>Send-only telemetry, not a reproducible replay — see ReplayRecord's remarks.</summary>
    public ReplayRecord GetReplayRecord() => new ReplayRecord(options.Seed, content.Version, content.Maps[0].Id, players.Players.Select(player => player.PlayerId).ToArray(), tick, acceptedCommands);

    /// <summary>
    /// Total creep entities held in combat state, unfiltered by HasLeaked. GetSnapshot's creep list goes
    /// through GetCreepSnapshots, which already excludes spent lane-transfer entities, so it cannot show
    /// whether one is still sitting in CombatState. Exists to verify the count stays bounded to live
    /// creeps instead of accumulating a tombstone per lane hop — see OPEN_ITEMS.md's retired 2026-07-29 review, "every lane hop leaves a permanent spent entity".
    /// </summary>
    public int DiagnosticCombatEntityCount() => combatState.Creeps.Count;

    /// <summary>
    /// Steps each live creep in <paramref name="laneId"/> still owes before it leaks, by entity id.
    /// </summary>
    /// <remarks>
    /// Exists because the property that matters when a lane is rebuilt cannot be observed from
    /// outside. A creep's position is an index into the route, and a rebuilt route winds differently
    /// — so a creep can end up spatially nearer the exit while owing exactly as many steps, and it
    /// can end up spatially further while owing fewer. Grid distance is therefore not a usable proxy
    /// for progress, and asserting on it produced a test that failed on correct behaviour and would
    /// have passed on some incorrect behaviour.
    ///
    /// Same reasoning as <see cref="DiagnosticCombatEntityCount"/>: a diagnostic accessor rather
    /// than widening the snapshot, because this is internal bookkeeping no presentation layer has
    /// any business rendering.
    /// </remarks>
    public IReadOnlyDictionary<long, int> DiagnosticRemainingSteps(LaneId laneId)
    {
        var route = routes.TryGetValue(laneId, out var mazed) ? mazed : Array.Empty<GridPosition>();
        var direct = directRoutes.TryGetValue(laneId, out var straight) ? straight : route;
        var remaining = new Dictionary<long, int>();

        foreach (var creep in combatState.Creeps)
        {
            if (creep.IsDead || creep.HasLeaked || !creep.LaneId.Equals(laneId))
            {
                continue;
            }

            var walking = creep.IgnoresMaze ? direct : route;
            remaining[creep.EntityId.Value] = Math.Max(0, walking.Count - 1 - creep.PathIndex);
        }

        return remaining;
    }

    public BotDiagnosticsSnapshot GetBotDiagnostics()
    {
        var profiles = bots
            .Select(bot => new BotProfileSnapshot(bot.Key, bot.Value.Profile, bot.Value.PrimaryCreepId))
            .OrderBy(profile => profile.PlayerId.Value)
            .ToArray();
        var recentDecisions = botDecisionRecords
            .Skip(System.Math.Max(0, botDecisionRecords.Count - 12))
            .ToArray();
        return new BotDiagnosticsSnapshot(profiles, recentDecisions);
    }

    public LocalVerticalSlice(ContentCatalog content, bool enableBots = true)
        : this(content, LocalMatchOptions.Default, enableBots)
    {
    }

    public LocalVerticalSlice(ContentCatalog content, LocalMatchOptions options, bool enableBots = true)
    {
        this.content = content;
        this.options = options;
        towersById = content.Towers.ToDictionary(tower => tower.Id);
        creepsById = content.Creeps.ToDictionary(creep => creep.Id);
        topology = new LocalMatchTopology(options.LaneCount);
        // sendCooldownTicks: 0 — gold is the only thing that gates a send.
        //
        // This was 30 ticks (7.5 seconds at 4 ticks/second). The rule had been described in the design
        // docs for a long time but never actually enforced until the seats/authority pass switched it
        // on, and once it ran it was clearly wrong for the game: 7.5s between any two sends made cheap
        // chaff like the 5-gold Crystal Wisp impossible to use as chaff, and the dock had to grow a
        // countdown just to explain why a card you could plainly afford refused to work.
        //
        // The enforcement in EconomyService is left intact and still tested with explicit values, so
        // the rule can be turned back on by changing this one number. CreepDefinition.IgnoresSendCooldown
        // also stays: it is inert at 0, but if a cooldown ever returns, Category 2 remains exempt.
        economy = new EconomyService(new EconomyRules(incomeIntervalTicks: 50, sendCooldownTicks: 0, sellRefundPercent: 50, leakLifeLoss: 1, incomeCeiling: EconomyRules.DefaultIncomeCeiling, incomeTaperStart: EconomyRules.DefaultIncomeTaperStart));
        pathService = new GridPathService();
        combat = new CombatService();
        commandValidator = new CommandContentValidator();

        var map = content.Maps[0];
        grids = new Dictionary<LaneId, LaneGrid>();
        routes = new Dictionary<LaneId, IReadOnlyList<GridPosition>>();
        directRoutes = new Dictionary<LaneId, IReadOnlyList<GridPosition>>();
        foreach (var laneId in topology.Lanes)
        {
            grids[laneId] = new LaneGrid(map);
            SetRoute(laneId, pathService.FindRoute(grids[laneId]).Route);

            // The fly-over route, captured for free: no tower has been placed yet, so the route just
            // computed IS the unmazed one. It is never recomputed — lane geometry is fixed for the
            // match, and the whole point of it is that towers do not shape it. Terrain still does,
            // which is correct: Spire Turret Walker steps over TOWERS, not over the map.
            directRoutes[laneId] = routes[laneId];
        }

        routeSet = new LaneRouteSet(routes, directRoutes);
        combatContent = new CombatContent(
            content.Creeps,
            content.Towers,
            topology.LaneOwners);
        players = CreateStartingPlayers(topology.Players);
        combatState = new CombatState(Enumerable.Empty<CreepCombatState>(), Enumerable.Empty<TowerCombatState>());
        bots = enableBots ? CreateBots(topology.Players) : new Dictionary<PlayerId, BotController>();

        // Both are the local stand-ins for what a server will own. Constructed here rather than
        // injected because nothing yet has anywhere to inject from; the point is that the bridge
        // already asks the questions, so the server implementation replaces two objects rather than
        // rewriting every call site. See ISeatAuthority and ICommandRateLimiter.
        seatAuthority = new LocalSeatAuthority(topology.Players);
        enqueueRateLimiter = new TokenBucketRateLimiter();
        botMatch = new BotMatchContext(this);
        tick = new SimulationTick(0);
    }

    /// <summary>
    /// The active content catalog, so the client can read authored values (tower costs and the
    /// like) rather than keeping its own copy of them.
    /// </summary>
    public ContentCatalog Content => content;

    /// <summary>
    /// Number of cells creeps must walk in a lane, which is the measure of how well it has been mazed.
    /// </summary>
    /// <remarks>
    /// Exposed because mazing is the core skill this game is about, and it was previously impossible to
    /// assert on from outside — so nothing noticed that the bots never did it.
    /// </remarks>
    public int RouteLength(LaneId laneId) => routes.TryGetValue(laneId, out var route) ? route.Count : 0;

    /// <summary>Length of the unmazed route — what a creep that ignores the maze actually walks.</summary>
    /// <remarks>
    /// Exposed so the difference between the two routes is measurable. It is the whole value of the
    /// mechanic: a maze exists to make the walk long, and this is the number that does not grow when
    /// one is built.
    /// </remarks>
    public int DirectRouteLength(LaneId laneId) => directRoutes.TryGetValue(laneId, out var route) ? route.Count : 0;

    /// <summary>
    /// The seat the local client drives. Presentation and input code should ask for this rather
    /// than assuming player 1, so the same client can be seated anywhere in the match — the
    /// prerequisite for remote players each driving their own seat.
    /// </summary>
    public PlayerId LocalPlayerId => options.LocalPlayerId;

    /// <summary>
    /// The lane the local seat defends. Always derived from the seat rather than hardcoded.
    /// </summary>
    public LaneId LocalPlayerLaneId => topology.HomeLaneFor(options.LocalPlayerId);

    public VerticalSliceCommandResult PreviewPlaceTower(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position)
    {
        return ValidateTowerPlacement(playerId, laneId, towerId, position).Result;
    }

    public VerticalSliceCommandResult PlaceTower(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position)
    {
        var validation = ValidateTowerPlacement(playerId, laneId, towerId, position);
        if (!validation.Result.Accepted)
        {
            return validation.Result;
        }

        var grid = validation.Grid!;
        var placement = validation.Placement!;
        var tower = validation.Tower!;
        var player = validation.Player!;

        var towerEntityId = NextEntityId();
        grids[laneId] = grid.WithOccupied(position);
        SetRoute(laneId, placement.Route);
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount - TowerBuildCostFor(player, tower))));
        // The owner's line tier is baked in HERE, at build time. A tier bought later raises what
        // new towers are built at and leaves this one where it is until it is paid for
        // individually — see UpgradeTower.
        // Committed here, on the first tower that actually lands, so the choice is made by playing
        // rather than by a modal before the match starts. WithChosenTowerLine ignores every call
        // after the first, which is what makes it one-way — a seat that could re-pick mid-match
        // would be the old build-everything with extra steps.
        players = players.Replace(players.Get(playerId).WithChosenTowerLine(tower.CategoryIndex));

        var builtTier = player.TowerLineTier(tower.CategoryIndex);
        combatState = new CombatState(
            combatState.Creeps,
            combatState.Towers.Concat(new[] { new TowerCombatState(towerEntityId, towerId, playerId, laneId, position, builtTier) }));
        pendingEvents.Add(new TowerPlacedEvent(tick, playerId, laneId, towerEntityId, towerId, position));
        return VerticalSliceCommandResult.Accept();
    }

    public VerticalSliceCommandResult QueueSend(PlayerId playerId, ContentId creepId) => QueueSend(playerId, creepId, 1);

    /// <summary>Most copies of one creep a seat may have waiting in its queue at once.</summary>
    public const int MaxQueuedSendsPerCreep = 10;

    /// <summary>
    /// Sends waiting to be paid for, per seat, oldest first.
    /// </summary>
    /// <remarks>
    /// The queue exists because this is a phone. Sending means opening the dock, finding a card and
    /// tapping it, and doing that at the exact moment gold arrives is not something a player can be
    /// asked to do repeatedly. Queueing lets them say what they want once and have it happen as the
    /// economy allows.
    ///
    /// Held on the bridge rather than on PlayerEconomyState because it is match state rather than
    /// economy state, and because it rebuilds from the accepted-command log on replay exactly like
    /// the board does — nothing here needs recording separately.
    ///
    /// One entry per creep, not a count, so the ORDER survives. A dictionary of quantities would
    /// lose which of two different creeps was asked for first, and first-in-first-out is the whole
    /// contract.
    /// </remarks>
    private readonly Dictionary<PlayerId, List<ContentId>> sendQueues = new Dictionary<PlayerId, List<ContentId>>();

    /// <summary>What this seat has waiting, oldest first.</summary>
    public IReadOnlyList<ContentId> SendQueueFor(PlayerId playerId) =>
        sendQueues.TryGetValue(playerId, out var queue) ? queue : Array.Empty<ContentId>();

    /// <summary>How many copies of one creep this seat has waiting.</summary>
    /// <remarks>Exposed for the send card's badge: a player queueing needs to see what they queued.</remarks>
    public int QueuedSendCountFor(PlayerId playerId, ContentId creepId) =>
        sendQueues.TryGetValue(playerId, out var queue) ? queue.Count(queued => queued.Equals(creepId)) : 0;

    /// <summary>
    /// Adds one creep to this seat's send queue, to be paid for when it can be.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT check gold. That is the point of the queue — a send with no gold behind
    /// it waits rather than being refused, and the player does not have to come back and re-tap when
    /// income lands.
    /// </remarks>
    private readonly ISeatAuthority seatAuthority;

    private readonly ICommandRateLimiter enqueueRateLimiter;

    public VerticalSliceCommandResult EnqueueSend(PlayerId playerId, ContentId creepId)
    {
        // The seat comes from the authority, never from the argument. In-process those are the same
        // value, which is exactly why this has to be written now: the day a client supplies the id
        // over a wire, the only thing standing between it and queueing into another player's queue
        // is that this line already exists and already ignores what it was told.
        var seat = seatAuthority.ResolveSeat(playerId);
        if (seat is null)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidPlayer);
        }

        playerId = seat.Value;

        // Request rate, not game rule. The ten-per-creep cap bounds how deep a queue gets; this
        // bounds how often a client may ask, including asking for things it will be refused.
        if (!enqueueRateLimiter.TryConsume(playerId, tick))
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.CooldownActive);
        }
        // Through the validator, like every other command. This is what lets an authoritative
        // server accept or refuse an enqueue with the same reason codes it uses for a send, rather
        // than the bridge having a second private opinion about what a valid creep id is.
        var command = new EnqueueSendCommand(playerId, tick, creepId);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return VerticalSliceCommandResult.Reject(contentResult.RejectionReason);
        }

        if (!topology.HasPlayer(playerId))
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidPlayer);
        }

        if (players.Get(playerId).IsEliminated)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.PlayerEliminated);
        }

        if (!sendQueues.TryGetValue(playerId, out var queue))
        {
            queue = new List<ContentId>();
            sendQueues[playerId] = queue;
        }

        if (queue.Count(queued => queued.Equals(creepId)) >= MaxQueuedSendsPerCreep)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.SendQueueFull);
        }

        queue.Add(creepId);
        return VerticalSliceCommandResult.Accept();
    }

    /// <summary>
    /// Takes back the most recently queued send of one creep, before it has been paid for.
    /// </summary>
    /// <remarks>
    /// Cancels the LAST matching entry rather than the first. The queue drains front-first, so the
    /// front entry is the one about to be paid for and dispatched — cancelling that would take back
    /// a different send than the one the player just added, which is the opposite of an undo. Last
    /// matching is "un-tap", and on a touch screen a mis-tap is the mistake this exists for.
    ///
    /// Same seat authority and rate limiter as <see cref="EnqueueSend"/>, for the same reason: a
    /// cancel that trusted its argument would let a client empty another player's queue, which is a
    /// cheaper attack than filling one.
    /// </remarks>
    public VerticalSliceCommandResult CancelQueuedSend(PlayerId playerId, ContentId creepId)
    {
        var seat = seatAuthority.ResolveSeat(playerId);
        if (seat is null)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidPlayer);
        }

        playerId = seat.Value;

        if (!enqueueRateLimiter.TryConsume(playerId, tick))
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.CooldownActive);
        }

        var command = new CancelQueuedSendCommand(playerId, tick, creepId);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return VerticalSliceCommandResult.Reject(contentResult.RejectionReason);
        }

        if (!topology.HasPlayer(playerId))
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidPlayer);
        }

        // Deliberately NOT gated on elimination. An eliminated seat cannot enqueue, so anything left
        // in its queue is stranded, and refusing to let it be cleared would be refusing to tidy up
        // after a rule this class already enforces elsewhere.
        if (!sendQueues.TryGetValue(playerId, out var queue))
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.NothingQueued);
        }

        var index = queue.FindLastIndex(queued => queued.Equals(creepId));
        if (index < 0)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.NothingQueued);
        }

        queue.RemoveAt(index);
        return VerticalSliceCommandResult.Accept();
    }

    /// <summary>
    /// Empties one seat's whole send queue.
    /// </summary>
    /// <remarks>
    /// The bulk form of <see cref="CancelQueuedSend"/>, for "I queued the wrong thing ten times".
    /// Reports how many entries went rather than a bare accept, because a UI that has just emptied
    /// a queue needs to know whether to animate anything, and an empty queue is a legitimate state
    /// rather than a failure — so this cannot reject the way the single cancel does.
    /// </remarks>
    public int ClearSendQueue(PlayerId playerId)
    {
        var seat = seatAuthority.ResolveSeat(playerId);
        if (seat is null || !enqueueRateLimiter.TryConsume(seat.Value, tick))
        {
            return 0;
        }

        if (!sendQueues.TryGetValue(seat.Value, out var queue))
        {
            return 0;
        }

        var removed = queue.Count;
        queue.Clear();
        return removed;
    }

    /// <summary>
    /// Pays for as much of each seat's queue as it can afford this tick, oldest first.
    /// </summary>
    /// <remarks>
    /// Strictly first-in-first-out: if the oldest entry cannot be paid for, the queue stops there
    /// rather than looking past it for something cheaper. Skipping ahead would quietly reorder what
    /// the player asked for, and a queue that reorders itself is worse than no queue — the player
    /// would have no way to predict what their taps did.
    ///
    /// Drains in a loop rather than one per tick, because gold arrives in lumps at the income tick
    /// and a queue that released one creep per tick would take a hundred ticks to spend a bank the
    /// player already has.
    ///
    /// A CONSECUTIVE run of the same creep drains as one QueueSend call at that quantity, not one
    /// call per unit. Cost is linear in quantity (SendCostFor's own doc: "a bulk send is priced
    /// exactly as the same number of single sends"), so this changes nothing about what a run costs
    /// — but IncomeGainFor is not linear above the taper's knee, and it floors at a minimum of +1 so
    /// a single gain-1 creep is never zeroed out. Evaluated once per unit instead of once per run,
    /// that floor stops being a floor and starts being a per-unit bonus: ten queued Runners at
    /// income 600 (taper band 300-900) granted +10 through this loop against +5 for the SAME ten
    /// sent as one batch — a real income-accounting bug this reordering closes, found from a report
    /// that queued sends were "not counting income correctly." This is also most of what read as
    /// "sending multiples at a time": several ticks' worth of taps, all affordable at once, used to
    /// spawn as a visible burst of individual accept/spawn events; now it is one event at the true
    /// quantity, matching what a direct multi-send already looked like.
    ///
    /// Seats in id order, for the same determinism reason the bot loop is ordered: this spends gold
    /// and assigns entity ids.
    /// </remarks>
    private void DrainSendQueues()
    {
        foreach (var playerId in sendQueues.Keys.OrderBy(id => id.Value).ToArray())
        {
            var queue = sendQueues[playerId];
            while (queue.Count > 0)
            {
                var creepId = queue[0];
                var runLength = 1;
                while (runLength < queue.Count && queue[runLength].Equals(creepId))
                {
                    runLength++;
                }

                // Capped at what gold actually covers, computed BEFORE the call. Asking QueueSend
                // for the full run and letting it reject would turn a queue that can afford HALF a
                // run into one that drains none of it — the exact partial-fill behaviour the
                // original per-unit loop had for free, which batching must not give up to fix the
                // income accounting below.
                var affordable = AffordableRunQuantity(playerId, creepId, runLength);
                if (affordable <= 0 || !QueueSend(playerId, creepId, affordable).Accepted)
                {
                    break;
                }

                queue.RemoveRange(0, affordable);
            }
        }
    }

    /// <summary>
    /// How many of a consecutive same-creep run this seat can pay for right now, capped at the
    /// run's own length.
    /// </summary>
    /// <remarks>
    /// Cost is linear in quantity (unit price times count, computed once — see
    /// <see cref="EconomyService.SendCostFor"/>), so this is arithmetic rather than a search, and
    /// the unit price it reads is the exact one <c>QueueSend</c> will charge for each of them.
    /// </remarks>
    private int AffordableRunQuantity(PlayerId playerId, ContentId creepId, int runLength)
    {
        var unitCost = economy.SendCostFor(players.Get(playerId), CreepFor(creepId), 1);
        return unitCost <= 0 ? runLength : Math.Min(runLength, players.Get(playerId).Gold.Amount / unitCost);
    }

    /// <summary>
    /// Local editor/playtest helper for stress and screenshot scenarios. This deliberately sits on the
    /// vertical-slice bridge rather than in EconomyService so production economy rules stay unchanged.
    /// </summary>
    public void GrantLocalPlaytestGold(PlayerId playerId, Gold amount)
    {
        if (amount.Amount <= 0)
        {
            return;
        }

        var player = players.Get(playerId);
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount + amount.Amount)));
    }

    /// <summary>
    /// Grants income directly, so a scenario can reach the tier gate without playing an economy out.
    /// </summary>
    /// <remarks>
    /// Added with the income requirement on category tiers. Before it, every test and editor
    /// scenario that wanted a tier simply granted gold — which stopped being sufficient the moment
    /// tiers also required income, and left a set of tests failing for a reason that had nothing to
    /// do with what they were testing.
    ///
    /// Sits on the bridge next to <see cref="GrantLocalPlaytestGold"/> and for the same reason:
    /// production economy rules in EconomyService stay untouched, and the only way to raise income
    /// in a real match remains sending creeps.
    /// </remarks>
    public void GrantLocalPlaytestIncome(PlayerId playerId, Income amount)
    {
        if (amount.Amount <= 0)
        {
            return;
        }

        var player = players.Get(playerId);
        players = players.Replace(player.WithIncome(new Income(player.Income.Amount + amount.Amount)));
    }

    /// <summary>
    /// Clears a player's send cooldown so editor/playtest scenarios can queue several sends in one
    /// tick. Sits alongside <see cref="GrantLocalPlaytestGold"/> for the same reason: the
    /// production rule in EconomyService stays untouched.
    /// </summary>
    /// <remarks>
    /// The review capture states each queue a batch of sends back to back to build up load for a
    /// screenshot. Once the send cooldown started actually being enforced, everything after the
    /// first send in a state was rejected with CooldownActive and the captured board went nearly
    /// empty — the rule is right, but a capture scenario is not a player and should not be rate
    /// limited into producing nothing.
    /// </remarks>
    public void ClearLocalPlaytestSendCooldown(PlayerId playerId)
    {
        var player = players.Get(playerId);
        players = players.Replace(player.WithNextSendAvailableTick(tick));
    }

    /// <summary>
    /// Takes a seat's last life so editor/playtest scenarios can show the eliminated state without
    /// playing a match out to it. Third of the trio alongside <see cref="GrantLocalPlaytestGold"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately routed through the same <see cref="PlayerEliminatedEvent"/> and
    /// <c>WipeEliminatedLane</c> the real defeat path uses, rather than just setting the flag. A
    /// screenshot of a seat that is "eliminated" but still has its towers standing and its creeps
    /// walking would be a picture of a state the game cannot actually be in, and the whole point of
    /// RealUiCaptureRunner is that its captures are the real UI rather than a mock of it.
    ///
    /// It does NOT check for a match end afterwards: that is a per-tick concern and the next
    /// AdvanceOneTick resolves it normally, so eliminating the last-but-one seat still produces a
    /// match summary on the following tick exactly as a leak would.
    /// </remarks>
    public void EliminateForLocalPlaytest(PlayerId playerId)
    {
        var player = players.Get(playerId);
        if (player.IsEliminated)
        {
            return;
        }

        players = players.Replace(player.WithLives(new Lives(0)));
        RecordElimination(playerId);
        pendingEvents.Add(new PlayerEliminatedEvent(tick, playerId));
        WipeEliminatedLane(playerId);
    }

    /// <summary>
    /// Which player must send in order for the creeps to arrive in <paramref name="laneId"/>,
    /// or null if no active player currently routes there.
    /// </summary>
    /// <remarks>
    /// Review captures frame one lane and need to put creeps in it. Working the sender out from
    /// outside means duplicating the routing rule — a send goes to the home lane of the sender's
    /// next active opponent — along with the lane count, and it silently stops being true once a
    /// player is eliminated, because NextActiveOpponent then skips past them. The capture harness
    /// did exactly that and quietly filled the wrong lane. Asking the live topology instead keeps
    /// the answer correct as the match state changes.
    /// </remarks>
    public PlayerId? LocalPlaytestSenderForLane(LaneId laneId)
    {
        foreach (var candidate in topology.Players)
        {
            if (players.Get(candidate).IsEliminated)
            {
                continue;
            }

            var target = topology.NextActiveOpponent(candidate, id => !players.Get(id).IsEliminated);
            if (target is not null && topology.HomeLaneFor(target.Value).Equals(laneId))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Local editor/playtest helper for screenshot review. Creates a wounded creep that has just
    /// transferred into the next opponent lane, and emits the same leak/spawn event pairing the
    /// Unity renderer uses to display TRANSFER arrival cues.
    /// </summary>
    public VerticalSliceCommandResult CreateLocalPlaytestDamagedTransferCreep(ContentId creepId, int health)
    {
        var creep = FindCreep(creepId);
        if (creep is null)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidContentId);
        }

        var senderId = new PlayerId(1);
        var sourceLaneId = new LaneId(1);
        var targetLaneId = new LaneId(2);
        var sourceDefenderId = combatContent.GetLaneOwner(sourceLaneId);
        var targetDefenderId = combatContent.GetLaneOwner(targetLaneId);
        var clampedHealth = System.Math.Clamp(health, 1, System.Math.Max(1, creep.MaxHealth - 1));
        var leakedEntityId = NextEntityId();
        var sourceCreep = combat.SpawnCreep(leakedEntityId, creep, senderId, sourceLaneId).WithHealth(clampedHealth);
        var transferred = combat.TransferCreep(NextEntityId(), sourceCreep, targetLaneId).WithMovement(pathIndex: 3, movementProgress: 0);

        combatState = new CombatState(combatState.Creeps.Concat(new[] { transferred }), combatState.Towers);
        pendingEvents.Add(new LeakEvent(tick, senderId, sourceDefenderId, leakedEntityId, new Lives(1), creep.LeakBounty));
        pendingEvents.Add(new CreepSpawnedEvent(tick, transferred.EntityId, transferred.CreepId, transferred.SenderId, targetDefenderId));
        return VerticalSliceCommandResult.Accept();
    }

    public VerticalSliceCommandResult QueueSend(PlayerId playerId, ContentId creepId, int quantity)
    {
        var command = new QueueSendCommand(playerId, tick, creepId, quantity);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return VerticalSliceCommandResult.Reject(contentResult.RejectionReason);
        }

        var creep = CreepFor(creepId);
        var targetPlayerId = topology.NextActiveOpponent(playerId, candidate => !players.Get(candidate).IsEliminated);
        if (targetPlayerId is null)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.PlayerEliminated);
        }

        var send = economy.QueueSend(players, playerId, creep, quantity, tick, targetPlayerId.Value);
        if (!send.Accepted)
        {
            return VerticalSliceCommandResult.Reject(send.RejectionReason);
        }

        players = send.Players;
        var laneId = topology.HomeLaneFor(send.TargetPlayerId!.Value);
        // The sender's send-category tier is baked into health here, at purchase time. A tier
        // bought later does not reach these creeps.
        //
        // Match escalation is folded in the same way and for the same reason. The two are combined
        // multiplicatively rather than added, so a tier-3 send keeps being worth 225% of whatever
        // the escalated baseline is instead of the two bonuses diluting each other late in a match.
        var healthPercent = CategoryTierRules.CreepHealthPercentFor(players.Get(playerId).SendCategoryTier(creep.CategoryIndex))
            * MatchEscalationRules.CreepHealthPercentFor(tick.Value) / 100;
        var spawned = Enumerable.Range(0, quantity).Select(_ => combat.SpawnCreep(NextEntityId(), creep, playerId, laneId, healthPercent)).ToArray();
        combatState = new CombatState(combatState.Creeps.Concat(spawned), combatState.Towers);
        acceptedCommands.Add(new AcceptedCommandRecord(tick, playerId, creepId, quantity));
        pendingEvents.Add(new CreepQueuedEvent(tick, playerId, send.TargetPlayerId.Value, creepId, quantity));
        foreach (var spawnedCreep in spawned) pendingEvents.Add(new CreepSpawnedEvent(tick, spawnedCreep.EntityId, creepId, playerId, send.TargetPlayerId.Value));
        return VerticalSliceCommandResult.Accept();
    }

    /// <summary>
    /// Buys the next tier for one of a player's six categories.
    /// </summary>
    /// <remarks>
    /// Tiers must be bought in order, so the escalating cost is actually paid rather than skipped:
    /// a request for tier 3 from tier 1 is rejected even when the player could afford it outright.
    ///
    /// Gold is deducted and the tier written in the same step, against the same state read, so a
    /// purchase cannot half-apply. The new tier reaches combat on the next tick, when
    /// AdvanceOneTick refreshes CombatContent — towers already standing start hitting harder, and
    /// creeps already walking keep the health they spawned with.
    /// </remarks>
    public VerticalSliceCommandResult BuyCategoryTier(PlayerId playerId, CategoryKind categoryKind, int categoryIndex, int targetTier)
    {
        var command = new BuyCategoryTierCommand(playerId, tick, categoryKind, categoryIndex, targetTier);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return VerticalSliceCommandResult.Reject(contentResult.RejectionReason);
        }

        var player = players.Get(playerId);
        if (player.IsEliminated)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.PlayerEliminated);
        }

        var currentTier = categoryKind == CategoryKind.TowerLine
            ? player.TowerLineTier(categoryIndex)
            : player.SendCategoryTier(categoryIndex);
        if (targetTier != currentTier + 1)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidTier);
        }

        // Income before gold, and the order matters. A player short on both should be told to build
        // their economy rather than to keep banking, because banking is exactly what will not fix
        // the income requirement.
        // Priced against what this player already holds, not the list price. Both the gate and the
        // charge below read the same number, so a seat is never refused for an income it would have
        // met at the price it was actually about to pay.
        var upgradesOwned = CategoryTierRules.UpgradesOwned(player);
        var minimumIncome = CategoryTierRules.MinimumIncomeFor(categoryKind, targetTier, upgradesOwned);
        if (player.Income.Amount < minimumIncome)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InsufficientIncome);
        }

        var cost = CategoryTierRules.CostFor(categoryKind, targetTier, upgradesOwned);
        if (player.Gold.Amount < cost)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InsufficientGold);
        }

        var purchased = player.WithGold(new Gold(player.Gold.Amount - cost));
        purchased = categoryKind == CategoryKind.TowerLine
            ? purchased.WithTowerLineTier(categoryIndex, targetTier)
            : purchased.WithSendCategoryTier(categoryIndex, targetTier);
        players = players.Replace(purchased);

        pendingEvents.Add(new CategoryTierPurchasedEvent(tick, playerId, categoryKind, categoryIndex, targetTier, new Gold(cost)));
        return VerticalSliceCommandResult.Accept();
    }

    /// <summary>
    /// Raises one placed tower by a tier, for gold.
    /// </summary>
    /// <remarks>
    /// The counterpart to a line tier. Buying ARCANE tier 2 raises what new Arrows are built at;
    /// this is how the Arrows already standing catch up, one at a time and one payment each. That
    /// is the decision the feature exists to create — upgrade the twelve towers already on the
    /// board, or spend the same gold building new ones that arrive at the higher tier already.
    ///
    /// The owner's line tier is the CEILING, so the category purchase stays the thing that unlocks
    /// progression and this stays the thing that realises it. A tower at the ceiling reports
    /// InvalidTier rather than silently taking the gold.
    /// </remarks>
    public VerticalSliceCommandResult UpgradeTower(PlayerId playerId, LaneId laneId, GridPosition position)
    {
        var tower = combatState.Towers.FirstOrDefault(candidate =>
            candidate.OwnerId.Equals(playerId) && candidate.LaneId.Equals(laneId) && candidate.Position.Equals(position));
        if (tower is null)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.NotOwner);
        }

        var player = players.Get(playerId);
        if (player.IsEliminated)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.PlayerEliminated);
        }

        var definition = TowerFor(tower.TowerId);
        var ceiling = player.TowerLineTier(definition.CategoryIndex);
        if (tower.Tier >= ceiling || tower.Tier >= CategoryTierRules.MaxTier)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidTier);
        }

        var cost = CategoryTierRules.TowerUpgradeCost(definition.Cost.Amount);
        if (player.Gold.Amount < cost)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InsufficientGold);
        }

        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount - cost)));
        var upgraded = tower.WithTier(tower.Tier + 1);
        combatState = combatState.ReplaceTower(upgraded);
        pendingEvents.Add(new TowerUpgradedEvent(tick, playerId, laneId, tower.EntityId, tower.TowerId, position, upgraded.Tier, new Gold(cost)));
        return VerticalSliceCommandResult.Accept();
    }

    /// <summary>
    /// Raises every tower this player owns in one line, spending as far as their gold reaches.
    /// </summary>
    /// <remarks>
    /// Deliberately a loop over <see cref="UpgradeTower"/> rather than a command of its own, so
    /// every rejection rule — ownership, elimination, the line's tier ceiling, affordability — is
    /// enforced by the code path a single upgrade already uses. A batch is therefore indistinguishable
    /// from the taps it stands in for, and there is no second copy of those rules to drift.
    ///
    /// Note this adds nothing to acceptedCommands, because UpgradeTower does not: that list is
    /// send-only telemetry rather than a reproducible command log (see ReplayRecord's remarks), so
    /// upgrades have never appeared in it, single or batched.
    ///
    /// It lives in the simulation rather than in the Unity adapter because this is real logic —
    /// which towers qualify, in what order, and how far the gold goes — and the client has no test
    /// framework, so logic placed there cannot be regression-tested at all.
    /// </remarks>
    public BatchUpgradeOutcome UpgradeTowerLine(PlayerId playerId, LaneId laneId, int lineIndex) =>
        RunUpgrades(playerId, laneId, candidate => candidate.LineIndex == lineIndex);

    /// <summary>
    /// Raises a hand-picked set of towers, spending as far as the player's gold reaches.
    /// </summary>
    /// <remarks>
    /// Shares everything with the whole-line version except which towers it considers, so a
    /// selection spanning two lines obeys each line's own tier ceiling without special-casing.
    /// </remarks>
    public BatchUpgradeOutcome UpgradeTowers(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions) =>
        RunUpgrades(playerId, laneId, candidate => positions.Contains(candidate.Position));

    private BatchUpgradeOutcome RunUpgrades(PlayerId playerId, LaneId laneId, Func<BatchUpgradeCandidate, bool> include)
    {
        if (players.Get(playerId).IsEliminated)
        {
            return new BatchUpgradeOutcome(0, 0, 0);
        }

        var eligible = EligibleUpgrades(playerId, laneId, include);

        var upgraded = 0;
        var spent = 0;
        foreach (var candidate in eligible)
        {
            var result = UpgradeTower(playerId, laneId, candidate.Position);
            if (result.Accepted)
            {
                upgraded++;
                spent += candidate.Cost;
                continue;
            }

            if (result.RejectionReason == CommandRejectionReason.InsufficientGold)
            {
                // Cheapest first means nothing later in the list is affordable either.
                break;
            }
        }

        return new BatchUpgradeOutcome(upgraded, eligible.Count, spent);
    }

    /// <summary>
    /// What a whole-line upgrade would cost right now, without spending anything.
    /// </summary>
    /// <remarks>
    /// Shares its eligibility rule with <see cref="UpgradeTowerLine"/> through
    /// <see cref="EligibleLineUpgradeCosts"/>, so the price a card shows and the price the batch
    /// charges cannot disagree — the exact drift that had Arrow priced at three different numbers
    /// across the palette, the panel and the simulation.
    /// </remarks>
    public BatchUpgradeQuote QuoteTowerLineUpgrade(PlayerId playerId, LaneId laneId, int lineIndex) =>
        QuoteUpgrades(playerId, laneId, candidate => candidate.LineIndex == lineIndex);

    /// <summary>What raising a hand-picked set of towers would cost right now.</summary>
    public BatchUpgradeQuote QuoteTowerUpgrades(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions) =>
        QuoteUpgrades(playerId, laneId, candidate => positions.Contains(candidate.Position));

    private BatchUpgradeQuote QuoteUpgrades(PlayerId playerId, LaneId laneId, Func<BatchUpgradeCandidate, bool> include)
    {
        var eligible = EligibleUpgrades(playerId, laneId, include);
        var gold = players.Get(playerId).Gold.Amount;

        var affordable = 0;
        var running = 0;
        foreach (var candidate in eligible)
        {
            if (running + candidate.Cost > gold)
            {
                // Cheapest first, so nothing later is affordable either.
                break;
            }

            running += candidate.Cost;
            affordable++;
        }

        return new BatchUpgradeQuote(eligible.Count, eligible.Sum(candidate => candidate.Cost), affordable, running);
    }

    /// <summary>
    /// What selling a hand-picked set of towers would return, without removing anything.
    /// </summary>
    public BatchSellQuote QuoteTowerSales(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions)
    {
        var owned = OwnedTowersAt(playerId, laneId, positions);
        return new BatchSellQuote(
            owned.Count,
            owned.Sum(tower => economy.CalculateSellRefund(
                TowerFor(tower.TowerId)).Amount));
    }

    /// <summary>
    /// Sells a hand-picked set of towers.
    /// </summary>
    /// <remarks>
    /// Every sale goes through <see cref="SellTower"/>, which is what keeps the lane's route in
    /// step: removing a tower reopens its cell and the route is recomputed from the grid. Batching
    /// the removals and repathing once at the end would be faster and wrong — creeps would be
    /// walking a route computed against a maze that no longer exists for the duration.
    ///
    /// Unlike the upgrade batch there is nothing to run out of, so this always completes.
    /// </remarks>
    public BatchSellOutcome SellTowers(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions)
    {
        if (players.Get(playerId).IsEliminated)
        {
            return new BatchSellOutcome(0, 0);
        }

        var goldBefore = players.Get(playerId).Gold.Amount;
        var sold = 0;
        foreach (var tower in OwnedTowersAt(playerId, laneId, positions))
        {
            if (SellTower(playerId, tower).Accepted)
            {
                sold++;
            }
        }

        return new BatchSellOutcome(sold, players.Get(playerId).Gold.Amount - goldBefore);
    }

    /// <summary>
    /// The towers this player actually owns at those cells, newest first per cell.
    /// </summary>
    /// <remarks>
    /// Resolved to a list before anything acts on it, because selling mutates combatState as it
    /// goes. Ordering matches <see cref="SellTowerAt"/>'s so a batch of one sells the same tower a
    /// single tap would.
    /// </remarks>
    private List<TowerCombatState> OwnedTowersAt(PlayerId playerId, LaneId laneId, IReadOnlyCollection<GridPosition> positions) =>
        combatState.Towers
            .Where(candidate => candidate.OwnerId.Equals(playerId)
                && candidate.LaneId.Equals(laneId)
                && positions.Contains(candidate.Position))
            .OrderByDescending(candidate => candidate.EntityId.Value)
            .ToList();

    /// <summary>
    /// The towers in one line this player could raise right now, cheapest first.
    /// </summary>
    /// <remarks>
    /// The single definition of "eligible" for every quote and every spend, whether the batch came
    /// from a whole line or from a hand-picked selection. Splitting them was the
    /// obvious shape and the wrong one: a card that prices a batch by one rule and a batch that
    /// charges by another is precisely how Arrow ended up costing 20, 25 and 14 gold in three
    /// different places in this client.
    /// </remarks>
    private List<BatchUpgradeCandidate> EligibleUpgrades(PlayerId playerId, LaneId laneId, Func<BatchUpgradeCandidate, bool> include)
    {

        // Resolved to a list up front. UpgradeTower replaces towers in combatState as it goes, so
        // enumerating this lazily while spending would walk entries whose tier has already moved.
        return combatState.Towers
            .Where(candidate => candidate.OwnerId.Equals(playerId) && candidate.LaneId.Equals(laneId))
            .Select(candidate =>
            {
                var definition = TowerFor(candidate.TowerId);
                return new BatchUpgradeCandidate(
                    candidate.Position,
                    candidate.Tier,
                    definition.CategoryIndex,
                    CategoryTierRules.TowerUpgradeCost(definition.Cost.Amount));
            })
            // The ceiling is read per candidate because a selection can span lines, and each line
            // has its own tier. Reading one ceiling up front only worked while every candidate was
            // guaranteed to share a line.
            .Where(candidate => candidate.Tier < players.Get(playerId).TowerLineTier(candidate.LineIndex)
                && candidate.Tier < CategoryTierRules.MaxTier
                && include(candidate))
            // Cheapest first, so "as many as you can afford" actually maximises how many go up.
            // Position breaks ties, so the order is stable rather than dependent on tower iteration.
            .OrderBy(candidate => candidate.Cost)
            .ThenBy(candidate => candidate.Position.Y)
            .ThenBy(candidate => candidate.Position.X)
            .ToList();
    }

    /// <summary>
    /// Income this player would actually gain by sending now, taper included.
    /// </summary>
    /// <remarks>
    /// A creep's authored <c>IncomeGain</c> stops being the true number once a player crosses
    /// <see cref="EconomyRules.IncomeTaperStart"/>, so the send dock has to ask rather than print the
    /// authored value. The dock already reads COST from here for the same reason — a hardcoded 6G on
    /// the Swarm card is what let it enable at 6 gold and then be rejected for needing 18.
    /// </remarks>
    public int IncomeGainForSend(PlayerId playerId, ContentId creepId, int quantity)
    {
        var creep = FindCreep(creepId);
        return creep is null ? 0 : economy.IncomeGainFor(players.Get(playerId).Income, creep, quantity);
    }

    /// <summary>
    /// Gold to raise the tower at this cell, or 0 when there is nothing there to raise.
    /// </summary>
    public int TowerUpgradeCostAt(PlayerId playerId, LaneId laneId, GridPosition position)
    {
        var tower = combatState.Towers.FirstOrDefault(candidate =>
            candidate.OwnerId.Equals(playerId) && candidate.LaneId.Equals(laneId) && candidate.Position.Equals(position));
        return tower is null
            ? 0
            : CategoryTierRules.TowerUpgradeCost(TowerFor(tower.TowerId).Cost.Amount);
    }

    /// <summary>
    /// Every player's category tiers, in the form combat reads them.
    /// </summary>
    private IReadOnlyDictionary<PlayerId, PlayerCategoryTiers> CurrentPlayerTiers()
    {
        var tiers = new Dictionary<PlayerId, PlayerCategoryTiers>();
        foreach (var player in players.Players)
        {
            tiers[player.PlayerId] = PlayerCategoryTiers.From(player);
        }

        return tiers;
    }

    public VerticalSliceCommandResult SellLastTower(PlayerId playerId)
    {
        var tower = combatState.Towers
            .Where(candidate => candidate.OwnerId.Equals(playerId))
            .OrderByDescending(candidate => candidate.EntityId.Value)
            .FirstOrDefault();
        return tower is null ? VerticalSliceCommandResult.Reject(CommandRejectionReason.NotOwner) : SellTower(playerId, tower);
    }

    public VerticalSliceCommandResult SellTowerAt(PlayerId playerId, LaneId laneId, GridPosition position)
    {
        // Before the lookup, not after. Elimination wipes the lane, so by the time this searches
        // there is no tower to find and the honest "you are out" would come back as NotOwner — a
        // rejection that happens to be right while saying something false about why.
        if (players.Get(playerId).IsEliminated)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.PlayerEliminated);
        }

        var tower = combatState.Towers
            .Where(candidate => candidate.OwnerId.Equals(playerId) && candidate.LaneId.Equals(laneId) && candidate.Position.Equals(position))
            .OrderByDescending(candidate => candidate.EntityId.Value)
            .FirstOrDefault();
        return tower is null ? VerticalSliceCommandResult.Reject(CommandRejectionReason.NotOwner) : SellTower(playerId, tower);
    }

    private VerticalSliceCommandResult SellTower(PlayerId playerId, TowerCombatState tower)
    {
        // The single-tower path. SellTowers, the batch one, already refused an eliminated seat; this
        // one did not, so the same act was allowed or refused depending on which entry point the
        // caller happened to use. Eliminating also wipes the lane, so in practice this mostly
        // refused for NotOwner instead — the right answer for the wrong reason, and only by
        // accident of ordering.
        if (players.Get(playerId).IsEliminated)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.PlayerEliminated);
        }

        var towerDefinition = TowerFor(tower.TowerId);
        var refund = economy.CalculateSellRefund(towerDefinition);
        var player = players.Get(playerId);
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount + refund.Amount)));
        combatState = combatState.RemoveTower(tower.EntityId);
        grids[tower.LaneId] = grids[tower.LaneId].WithoutOccupied(tower.Position);
        SetRoute(tower.LaneId, pathService.FindRoute(grids[tower.LaneId]).Route);
        pendingEvents.Add(new TowerSoldEvent(tick, playerId, tower.LaneId, tower.EntityId, refund));
        return VerticalSliceCommandResult.Accept();
    }

    public void AdvanceOneTick()
    {
        if (matchEnded)
        {
            return;
        }

        StartMatch();

        // Before the bots act, so a queued send lands on the tick the player's gold reaches it
        // rather than a tick later.
        DrainSendQueues();

        // What each bot does with its turn, and in what order, is BotController.TakeTurn's business
        // (OPEN_ITEMS.md item 26 — that used to be about 350 lines of this class). What stays here is
        // the one part that is genuinely the bridge's: the order the seats decide in.
        //
        // OrderBy(bot.Key.Value): Dictionary<PlayerId, BotController> enumeration order is
        // documented-unspecified, and it feeds NextEntityId() assignment (via QueueSend/PlaceTower)
        // here, which is the final tie-breaker in SelectTarget, Pulse's splash Take(2) and ChainArc.
        // It happens to be insertion order today (no removals from this dictionary), but a sim that
        // records and replays should not rely on that — SeedExpandedLaneBotOpeners already sorts for
        // the same reason (OPEN_ITEMS.md's retired 2026-07-29 review, "determinism: one real hazard").
        foreach (var bot in bots.OrderBy(bot => bot.Key.Value))
        {
            bot.Value.TakeTurn(bot.Key, botMatch);
        }

        tick = new SimulationTick(tick.Value + 1);
        if (economy.IsIncomeTick(tick))
        {
            foreach (var player in players.Players.Where(player => !player.IsEliminated))
            {
                pendingEvents.Add(new IncomeTickEvent(tick, player.PlayerId, new Gold(player.Income.Amount)));
            }
        }

        players = economy.ApplyIncomeTick(players, tick);
        // Read pre-combat rather than post-combat (result.State below) deliberately: a leaked creep is read
        // out of this snapshot when building its transfer below, and that is only safe because MoveCreeps
        // runs first inside combat.Advance and the two damage phases after it both skip HasLeaked creeps —
        // so a creep's health cannot change between here and its leak this same tick. If that phase order
        // ever changes, a transferred creep would silently carry its start-of-tick health instead of what
        // it actually had when it left the lane.
        var creepsBeforeCombat = combatState.Creeps.ToDictionary(creep => creep.EntityId, creep => creep);
        // Tiers bought since the last tick reach combat here. Rebuilt rather than mutated so a
        // snapshot handed out earlier in the tick keeps the tiers it was taken with; the creep,
        // tower and lane dictionaries are carried across by reference, so this is one small
        // allocation, not a re-index of the catalog.
        combatContent = combatContent.WithPlayerTiers(CurrentPlayerTiers());
        var result = combat.Advance(combatState, combatContent, routeSet, tick);
        combatState = result.State;
        foreach (var simulationEvent in result.Events)
        {
            if (simulationEvent is CreepDamagedEvent damaged)
            {
                ApplySignalGold(damaged);
            }

            if (simulationEvent is CreepKilledEvent killed && creepsBeforeCombat.TryGetValue(killed.CreepEntityId, out var killedCreep))
                players = economy.ApplyKillBounty(players, killed.DefenderId, CreepFor(killedCreep.CreepId)).Players;
            if (simulationEvent is LeakEvent leak && creepsBeforeCombat.TryGetValue(leak.CreepEntityId, out var leakedCreep))
            {
                var creep = CreepFor(leakedCreep.CreepId);
                var defenderLivesBefore = players.Get(leak.DefenderId).Lives.Amount;
                players = economy.ApplyLeak(players, leak.SenderId, leak.DefenderId, creep, leak.LivesLost).Players;
                if (defenderLivesBefore > 0 && players.Get(leak.DefenderId).Lives.Amount == 0)
                {
                    RecordElimination(leak.DefenderId);
                    pendingEvents.Add(new PlayerEliminatedEvent(tick, leak.DefenderId));
                    WipeEliminatedLane(leak.DefenderId);
                }

                // The spent entity is removed unconditionally, not just when it transfers. Reaching a lane
                // end is not death (health carries forward per the design note in OPEN_ITEMS.md's retired 2026-07-29 review, "every lane hop leaves a permanent spent entity"),
                // but the entity that just left this lane is done regardless of whether a next lane exists
                // for it: on transfer its successor is the new entity below, and if every other seat is
                // already eliminated (nextLaneId is null) it simply has nowhere left to go. Leaving it in
                // CombatState either way makes it a tombstone: still HasLeaked, still holding the health it
                // exited with, invisible to every filter except one (the bot pressure check, item 10) that
                // forgot to exclude HasLeaked — which is what let these accumulate for a whole match.
                combatState = combatState.RemoveCreep(leak.CreepEntityId);

                var nextLaneId = NextActiveOpponentLaneId(leakedCreep.LaneId, leakedCreep.SenderId);
                if (nextLaneId is not null)
                {
                    var laneId = nextLaneId.Value;
                    var nextDefenderId = combatContent.GetLaneOwner(laneId);
                    var transferred = combat.TransferCreep(NextEntityId(), leakedCreep, laneId);
                    combatState = new CombatState(combatState.Creeps.Concat(new[] { transferred }), combatState.Towers);
                    pendingEvents.Add(new CreepSpawnedEvent(tick, transferred.EntityId, transferred.CreepId, transferred.SenderId, nextDefenderId));
                }
            }

            pendingEvents.Add(simulationEvent);
        }

        var summary = economy.TryCreateMatchSummary(players, tick, eliminatedAtTick);
        if (!matchEnded && summary is not null) { matchEnded = true; MatchSummary = summary; pendingEvents.Add(new MatchEndedEvent(tick, summary.WinnerId)); }
    }

    public VerticalSliceSnapshot GetSnapshot() =>
        new VerticalSliceSnapshot(
            tick,
            players,
            combat.GetCreepSnapshots(combatState, combatContent, routeSet),
            combatState.Towers,
            combat.GetTowerAimSnapshots(combatState, combatContent, routeSet),
            combat.GetBrambleCells(combatState, combatContent, routeSet),
            sendQueues.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<ContentId>)entry.Value.ToArray()));

    public IReadOnlyList<ISimulationEvent> DrainEvents()
    {
        var drained = pendingEvents.ToArray();
        pendingEvents.Clear();
        return drained;
    }

    public void Reset()
    {
        players = CreateStartingPlayers(topology.Players);
        combatState = new CombatState(Enumerable.Empty<CreepCombatState>(), Enumerable.Empty<TowerCombatState>());
        var map = content.Maps[0];
        foreach (var laneId in grids.Keys.ToArray())
        {
            grids[laneId] = new LaneGrid(map);
            SetRoute(laneId, pathService.FindRoute(grids[laneId]).Route);
        }
        pendingEvents.Clear();
        tick = new SimulationTick(0);
        nextEntityId = 1;
        matchStarted = false;
        matchEnded = false;
        MatchSummary = null;
        acceptedCommands.Clear();
        botDecisionRecords.Clear();

        // Two more per-match dictionaries this used to leave standing, both found from a "reset
        // doesn't reset correctly" report. Neither is emptied by resetting `players` above, because
        // both key off PlayerId rather than living inside the player set.
        //
        // sendQueues: an unpaid send queued right before Reset survived it and drained on the FIRST
        // tick of the new match against the fresh starting gold — a creep the player never asked
        // for in this match, spawned as if they had.
        //
        // eliminatedAtTick: RecordElimination writes each seat once and never again ("a seat cannot
        // come back" — true within a match, false across a Reset). A seat eliminated in the
        // previous match kept that tick; if the same seat was eliminated again in the new match,
        // ContainsKey was already true, so the new tick was silently dropped and the results screen
        // ranked that seat using an elimination time from a match that no longer exists.
        sendQueues.Clear();
        eliminatedAtTick.Clear();
    }

    public void StartMatch()
    {
        if (matchStarted)
        {
            return;
        }

        matchStarted = true;
        SeedExpandedLaneBotOpeners();
    }

    private static EconomyPlayerSet CreateStartingPlayers(IReadOnlyList<PlayerId> playerIds)
    {
        return new EconomyPlayerSet(playerIds
            .Select(playerId => new PlayerEconomyState(playerId, new Gold(100), new Income(10), new Lives(StartingLives))));
    }

    private Dictionary<PlayerId, BotController> CreateBots(IReadOnlyList<PlayerId> playerIds)
    {
        // The "never bot lane 1" rule this used to hardcode is now expressed by the seat: whichever
        // lane the local human occupies is excluded by IsBotEnabledFor, so lane 1 correctly becomes
        // bot-driven when the human is seated elsewhere.
        return playerIds
            .Where(playerId => options.IsBotEnabledFor(playerId))
            .ToDictionary(
                playerId => playerId,
                playerId => new BotController(
                    options.BotProfileFor(playerId),
                    options.PrimaryCreepFor(playerId) ?? content.Creeps[0].Id,
                    // Seed and seat together. The seed alone would hand every bot in a match the
                    // same stream, so all eight would jitter identically and the table would be as
                    // uniform as it was before; the seat alone would leave the seed doing nothing,
                    // which is the defect being fixed. 397 is an odd multiplier so seats do not
                    // collide across neighbouring seeds.
                    new LTW.Simulation.Random.SeededRandomSource((options.Seed * 397) ^ playerId.Value)));
    }

    private void SeedExpandedLaneBotOpeners()
    {
        if (options.LaneCount <= 3 || bots.Count == 0)
        {
            return;
        }

        foreach (var bot in bots.OrderBy(bot => bot.Key.Value))
        {
            bot.Value.TakeOpeningTurn(bot.Key, botMatch);
        }
    }

    private TowerPlacementValidation ValidateTowerPlacement(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position)
    {
        var command = new PlaceTowerCommand(playerId, tick, laneId, towerId, position);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return TowerPlacementValidation.Reject(contentResult.RejectionReason);
        }

        // PlayerId.IsValid only means "positive", so a player id outside this match still reaches
        // here and used to throw KeyNotFoundException from the economy lookup further down.
        // Reject instead: over the wire this is just a bad command, not a program error.
        if (!topology.HasPlayer(playerId))
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InvalidPlayer);
        }

        // A seat that is out cannot build. This sits in the validator rather than in PlaceTower so
        // that CanPlaceTower answers the same way — a UI that greys the cell and a command that
        // refuses it have to agree, or the board offers a build it will not honour.
        //
        // It matters most for bots, which no caller filters: AdvanceOneTick runs TakeTurn for every
        // bot in the dictionary with no elimination check, so a defeated bot goes on taking turns
        // and only the individual commands stop it. UpgradeTower, SellTowers and BuyCategoryTier
        // each checked; this one did not, so a defeated bot rebuilt its wiped lane.
        if (players.Get(playerId).IsEliminated)
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.PlayerEliminated);
        }

        // The category lock. A seat commits to one tower line with its first tower and builds only
        // from that line for the rest of the match. Reported from play 2026-08-07: with every line
        // buildable, a scattered set of wards blends DPS, AOE and slow into a defence that cannot
        // lose, so there is no decision to make.
        //
        // Checked in the validator rather than in PlaceTower so CanPlaceTower answers the same way
        // and the palette can grey what it cannot build — a board that offers a build it will not
        // honour is worse than one that offers nothing.
        if (!players.Get(playerId).CanBuildFromLine(TowerFor(towerId).CategoryIndex))
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InvalidContentId);
        }

        if (!grids.TryGetValue(laneId, out var grid))
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InvalidLane);
        }

        // A player may only build in their own home lane. Until now nothing enforced this: the
        // lane id was taken on trust from the caller, so PlaceTower(P1, lane 5, ...) succeeded,
        // charged P1's gold, and left a P1-owned tower defending P5's lane. That was invisible
        // locally only because the Unity client hardcodes lane 1 for the single human seat, but
        // it becomes a live exploit the moment remote clients submit their own commands (a
        // player could reshape an opponent's maze, or spend into their lane to grief the route).
        // Selling already checked ownership (see SellTowerAt's OwnerId filter); building didn't.
        if (!laneId.Equals(topology.HomeLaneFor(playerId)))
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.NotOwner);
        }

        var placement = pathService.ValidatePlacement(grid, position);
        if (!placement.IsValid)
        {
            return TowerPlacementValidation.Reject(ToCommandRejection(placement.RejectionReason));
        }

        var tower = TowerFor(towerId);
        var player = players.Get(playerId);
        if (player.Gold.Amount < TowerBuildCostFor(player, tower))
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InsufficientGold);
        }

        return TowerPlacementValidation.Accept(grid, placement, tower, player);
    }

    /// <summary>
    /// What this player pays to build <paramref name="tower"/>, at their line's tier.
    /// </summary>
    /// <remarks>
    /// One function because the affordability check and the charge live in different methods —
    /// ValidateTowerPlacement decides, PlaceTower deducts — and the two reading the price
    /// separately is how a seat gets told it can afford a tower and then billed something else.
    ///
    /// The tier is the owner's at the moment of building, which is the same instant the tier is
    /// baked into the tower's damage. A line upgraded afterwards changes neither this tower's
    /// damage nor the price already paid for it.
    /// </remarks>
    private static int TowerBuildCostFor(PlayerEconomyState player, TowerDefinition tower) =>
        tower.Cost.Amount * CategoryTierRules.TowerBuildCostPercentFor(player.TowerLineTier(tower.CategoryIndex)) / 100;

    /// <summary>Tick each seat was eliminated, in the order it happened.</summary>
    /// <remarks>
    /// Kept here because this is the only place that sees eliminations as they occur. The final
    /// player set cannot answer it — every defeated seat looks identical there, which is why the
    /// results screen ranked nobody. Written once per seat: a seat cannot come back.
    /// </remarks>
    private readonly Dictionary<PlayerId, long> eliminatedAtTick = new Dictionary<PlayerId, long>();

    private void RecordElimination(PlayerId playerId)
    {
        if (!eliminatedAtTick.ContainsKey(playerId))
        {
            eliminatedAtTick[playerId] = tick.Value;
        }
    }

    private EntityId NextEntityId() => new EntityId(nextEntityId++);

    /// <summary>
    /// The definition for an id the caller already knows is in the catalog.
    /// </summary>
    /// <remarks>
    /// Throws on an unknown id, exactly as the <c>First(...)</c> scans these replaced did. Every
    /// caller either comes through <see cref="CommandContentValidator"/> or reads the id back off
    /// an entity the simulation itself created, so a miss here is a program error rather than a
    /// bad command — the <c>Find</c> pair below is for the callers that genuinely can be handed an
    /// id that does not exist.
    /// </remarks>
    private TowerDefinition TowerFor(ContentId towerId) => towersById[towerId];

    private CreepDefinition CreepFor(ContentId creepId) => creepsById[creepId];

    private TowerDefinition? FindTower(ContentId towerId) =>
        towersById.TryGetValue(towerId, out var tower) ? tower : null;

    private CreepDefinition? FindCreep(ContentId creepId) =>
        creepsById.TryGetValue(creepId, out var creep) ? creep : null;

    /// <summary>
    /// Pays a tower's owner for landing a hit, where the tower's content says it earns.
    /// </summary>
    /// <remarks>
    /// Lives here rather than in CombatService so economy types stay out of combat, which is the
    /// separation the architecture keeps everywhere else.
    ///
    /// The amount is read from the tower's definition. It used to be a constant applied to any
    /// tower whose content id contained "relay", "utility" or "economy" — correct for the one tower
    /// that has ever earned, and a trap for any future tower whose id happened to contain one of
    /// those words.
    /// </remarks>
    private void ApplySignalGold(CreepDamagedEvent damaged)
    {
        var tower = combatState.Towers.FirstOrDefault(candidate => candidate.EntityId.Equals(damaged.TowerEntityId));
        if (tower is null)
        {
            return;
        }

        var definition = FindTower(tower.TowerId);
        if (definition is null || definition.SignalGoldPerHit <= 0)
        {
            return;
        }

        var player = players.Get(tower.OwnerId);
        if (player.IsEliminated)
        {
            return;
        }

        var amount = new Gold(definition.SignalGoldPerHit);
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount + amount.Amount)));
        pendingEvents.Add(new TowerEarnedGoldEvent(tick, tower.OwnerId, tower.LaneId, tower.EntityId, tower.Position, amount));
    }

    /// <summary>
    /// Clears a defeated seat's lane so the match can carry on around it.
    /// </summary>
    /// <remarks>
    /// Elimination was only ever an ECONOMY fact: an eliminated player earns no income, cannot
    /// send, and is skipped when choosing a target or the next lane after a leak. CombatService
    /// knows nothing about it, so a defeated seat's towers kept firing forever and creeps kept
    /// walking a lane whose owner had already lost — still leaking, still deducting lives from
    /// somebody on zero.
    ///
    /// Three things have to happen together, and the third is the one that is easy to miss:
    ///
    /// 1. The towers go. They belong to a player who is out.
    /// 2. The creeps in the lane go. They were attacking a seat that no longer exists.
    /// 3. The lane's GRID and ROUTE are rebuilt empty. Towers occupy cells and are what lengthens
    ///    the route, so removing them without rebuilding leaves the maze standing as an invisible
    ///    wall — creeps would keep walking the long way round obstacles that are no longer there.
    ///
    /// Creeps the eliminated player SENT are deliberately left alone. They are in other people's
    /// lanes, they were paid for, and they are somebody else's problem now.
    ///
    /// The in-flight creeps are removed rather than pushed on to the next lane. Both readings are
    /// defensible — the carousel exists precisely to move creeps onward — but "wiped" is the
    /// literal ask, and forwarding them would hand the attacker free continued pressure as a reward
    /// for the kill. Worth revisiting once it can be seen in play.
    /// </remarks>
    private void WipeEliminatedLane(PlayerId playerId)
    {
        var laneId = topology.HomeLaneFor(playerId);

        // Announced per entity, not as one bulk event, so the presentation layer can play the sell
        // and death cues it already has rather than having everything blink out in a single frame.
        // These go straight onto pendingEvents: the economy hooks that pay kill bounty and refunds
        // read combat's OWN event list, so nothing here pays out for a wipe.
        foreach (var tower in combatState.Towers.Where(tower => tower.OwnerId.Equals(playerId)).ToArray())
        {
            pendingEvents.Add(new TowerSoldEvent(tick, playerId, laneId, tower.EntityId, new Gold(0)));
        }

        foreach (var creep in combatState.Creeps.Where(creep => creep.LaneId.Equals(laneId)).ToArray())
        {
            pendingEvents.Add(new CreepKilledEvent(tick, creep.EntityId, playerId, new Gold(0)));
        }

        combatState = combatState.WipeLane(laneId, playerId);

        var map = content.Maps[0];
        grids[laneId] = new LaneGrid(map);
        SetRoute(laneId, pathService.FindRoute(grids[laneId]).Route);
    }

    private LaneId? NextActiveOpponentLaneId(LaneId currentLaneId, PlayerId senderId)
    {
        return topology.NextActiveOpponentLaneAfterLeak(
            currentLaneId,
            senderId,
            defenderId => !players.Get(defenderId).IsEliminated);
    }

    private static CommandRejectionReason ToCommandRejection(PlacementRejectionReason reason)
    {
        return reason switch
        {
            PlacementRejectionReason.OutsideGrid => CommandRejectionReason.InvalidLane,
            PlacementRejectionReason.AlreadyOccupied => CommandRejectionReason.CellOccupied,
            PlacementRejectionReason.PathBlocked => CommandRejectionReason.PathBlocked,
            _ => CommandRejectionReason.PathBlocked
        };
    }


    private sealed class TowerPlacementValidation
    {
        private TowerPlacementValidation(
            VerticalSliceCommandResult result,
            LaneGrid? grid,
            PlacementValidationResult? placement,
            TowerDefinition? tower,
            PlayerEconomyState? player)
        {
            Result = result;
            Grid = grid;
            Placement = placement;
            Tower = tower;
            Player = player;
        }

        public VerticalSliceCommandResult Result { get; }

        public LaneGrid? Grid { get; }

        public PlacementValidationResult? Placement { get; }

        public TowerDefinition? Tower { get; }

        public PlayerEconomyState? Player { get; }

        public static TowerPlacementValidation Accept(LaneGrid grid, PlacementValidationResult placement, TowerDefinition tower, PlayerEconomyState player) =>
            new TowerPlacementValidation(VerticalSliceCommandResult.Accept(), grid, placement, tower, player);

        public static TowerPlacementValidation Reject(CommandRejectionReason reason) =>
            new TowerPlacementValidation(VerticalSliceCommandResult.Reject(reason), null, null, null, null);
    }

    /// <summary>
    /// Presents this match to the bots as <see cref="IBotMatchContext"/>, and nothing more.
    /// </summary>
    /// <remarks>
    /// Nested and private so the bots' view of a match is exactly this list of questions and
    /// commands, rather than the whole bridge — which is what it was before OPEN_ITEMS.md item 26,
    /// when the decisions themselves lived in this class and could reach any field they liked.
    ///
    /// Every method here forwards to the same code a human's tap goes through. That is the point:
    /// a bot cannot place a tower in someone else's lane, spend gold it does not have, or upgrade
    /// past a line's tier ceiling, because it is not a special case anywhere — it submits the same
    /// commands and is refused by the same rules.
    /// </remarks>
    private sealed class BotMatchContext : IBotMatchContext
    {
        private readonly LocalVerticalSlice slice;

        public BotMatchContext(LocalVerticalSlice slice) => this.slice = slice;

        public ContentCatalog Content => slice.content;

        public SimulationTick Tick => slice.tick;

        public PlayerEconomyState PlayerState(PlayerId playerId) => slice.players.Get(playerId);

        public LaneId HomeLaneFor(PlayerId playerId) => slice.topology.HomeLaneFor(playerId);

        public IReadOnlyList<TowerCombatState> TowersOwnedBy(PlayerId playerId) =>
            slice.combatState.Towers.Where(tower => tower.OwnerId.Equals(playerId)).ToArray();

        /// <inheritdoc />
        /// <remarks>
        /// !HasLeaked is load-bearing, not defensive tidiness. A creep that finishes a lane is not
        /// despawned — it transfers to the next opponent's lane as a NEW entity, and the spent entity
        /// stays in CombatState tagged with the lane it exited, still holding the health it left with.
        /// So without this filter, the total counts every creep that has ever finished walking this
        /// lane and grows monotonically for the whole match. Once it crossed the bot's pressure
        /// threshold the bot stopped sending and never sent again.
        ///
        /// Every other creep filter in the codebase already excludes HasLeaked (eight sites in
        /// CombatService, plus GetCreepSnapshots), which is why nothing looked wrong on screen — bot
        /// pressure was the only consumer that saw the spent entities.
        ///
        /// Measured consequence of the fix, and it corrects an earlier diagnosis: the "two mazing bots
        /// stalemate forever" finding was attributed to defence out-scaling attack. It was not. With
        /// this filter the same seed completes at tick 926 instead of running past 80,000. It is also
        /// the real cause of the bot that sat on 3,700 gold with a frozen tower count, previously
        /// blamed on the placement ceiling alone.
        /// </remarks>
        public int LiveCreepHealthIn(LaneId laneId) =>
            slice.combatState.Creeps
                .Where(creep => creep.LaneId.Equals(laneId) && !creep.IsDead && !creep.HasLeaked)
                .Sum(creep => creep.Health);

        public IReadOnlyList<GridPosition> RouteFor(LaneId laneId) =>
            slice.routes.TryGetValue(laneId, out var route) ? route : System.Array.Empty<GridPosition>();

        public TowerDefinition? FindTower(ContentId towerId) => slice.FindTower(towerId);

        public BotPlacementProbe ProbePlacement(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position)
        {
            var validation = slice.ValidateTowerPlacement(playerId, laneId, towerId, position);
            return validation.Result.Accepted
                ? BotPlacementProbe.Allowed(validation.Placement!.Route)
                : BotPlacementProbe.Rejected;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Read off the same decision log <see cref="GetBotDiagnostics"/> reports, so what a bot
        /// believes it has sent and what the diagnostics overlay shows it sent cannot disagree — and
        /// so <see cref="Reset"/> clears both by clearing one.
        /// </remarks>
        public IReadOnlyList<int> SendsByCategory(PlayerId playerId)
        {
            var counts = new int[PlayerEconomyState.CategoryCount];
            foreach (var record in slice.botDecisionRecords)
            {
                if (!record.PlayerId.Equals(playerId))
                {
                    continue;
                }

                var creep = slice.FindCreep(record.ContentId);
                if (creep is not null && creep.CategoryIndex >= 0 && creep.CategoryIndex < counts.Length)
                {
                    counts[creep.CategoryIndex] += record.Quantity;
                }
            }

            return counts;
        }

        public bool TrySend(PlayerId playerId, ContentId creepId, int quantity)
        {
            if (!slice.QueueSend(playerId, creepId, quantity).Accepted)
            {
                return false;
            }

            // Recorded here rather than by the bot, because it is diagnostics rather than a decision:
            // the profile on the record is what the overlay labels the row with, and the bridge is
            // what knows which controller this seat is driven by.
            slice.botDecisionRecords.Add(new BotDecisionRecord(
                slice.tick,
                playerId,
                slice.bots.TryGetValue(playerId, out var bot) ? bot.Profile : BotDecisionProfile.Balanced,
                creepId,
                quantity));
            return true;
        }

        public bool TryPlaceTower(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position) =>
            slice.PlaceTower(playerId, laneId, towerId, position).Accepted;

        public bool TryBuyCategoryTier(PlayerId playerId, CategoryKind categoryKind, int categoryIndex, int targetTier) =>
            slice.BuyCategoryTier(playerId, categoryKind, categoryIndex, targetTier).Accepted;

        public bool TryUpgradeTower(PlayerId playerId, LaneId laneId, GridPosition position) =>
            slice.UpgradeTower(playerId, laneId, position).Accepted;
    }
}
