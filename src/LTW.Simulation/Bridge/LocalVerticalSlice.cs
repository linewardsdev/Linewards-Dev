using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Combat;
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
    private const int StartingLives = 220;

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
        routes[laneId] = route;
        stateRevision++;
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
        economy = new EconomyService(new EconomyRules(incomeIntervalTicks: 50, sendCooldownTicks: 0, sellRefundPercent: 50, leakLifeLoss: 1, incomeCeiling: 600, incomeTaperStart: 300));
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
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount - tower.Cost.Amount)));
        // The owner's line tier is baked in HERE, at build time. A tier bought later raises what
        // new towers are built at and leaves this one where it is until it is paid for
        // individually — see UpgradeTower.
        var builtTier = player.TowerLineTier(tower.CategoryIndex);
        combatState = new CombatState(
            combatState.Creeps,
            combatState.Towers.Concat(new[] { new TowerCombatState(towerEntityId, towerId, playerId, laneId, position, builtTier) }));
        pendingEvents.Add(new TowerPlacedEvent(tick, playerId, laneId, towerEntityId, towerId, position));
        return VerticalSliceCommandResult.Accept();
    }

    public VerticalSliceCommandResult QueueSend(PlayerId playerId, ContentId creepId) => QueueSend(playerId, creepId, 1);

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
        var minimumIncome = CategoryTierRules.MinimumIncomeFor(categoryKind, targetTier);
        if (player.Income.Amount < minimumIncome)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InsufficientIncome);
        }

        var cost = CategoryTierRules.CostFor(categoryKind, targetTier);
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
        var tower = combatState.Towers
            .Where(candidate => candidate.OwnerId.Equals(playerId) && candidate.LaneId.Equals(laneId) && candidate.Position.Equals(position))
            .OrderByDescending(candidate => candidate.EntityId.Value)
            .FirstOrDefault();
        return tower is null ? VerticalSliceCommandResult.Reject(CommandRejectionReason.NotOwner) : SellTower(playerId, tower);
    }

    private VerticalSliceCommandResult SellTower(PlayerId playerId, TowerCombatState tower)
    {
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

        var summary = economy.TryCreateMatchSummary(players, tick);
        if (!matchEnded && summary is not null) { matchEnded = true; MatchSummary = summary; pendingEvents.Add(new MatchEndedEvent(tick, summary.WinnerId)); }
    }

    public VerticalSliceSnapshot GetSnapshot() =>
        new VerticalSliceSnapshot(
            tick,
            players,
            combat.GetCreepSnapshots(combatState, combatContent, routeSet),
            combatState.Towers,
            combat.GetTowerAimSnapshots(combatState, combatContent, routeSet));

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
                    options.PrimaryCreepFor(playerId) ?? content.Creeps[0].Id));
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
        if (player.Gold.Amount < tower.Cost.Amount)
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InsufficientGold);
        }

        return TowerPlacementValidation.Accept(grid, placement, tower, player);
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
