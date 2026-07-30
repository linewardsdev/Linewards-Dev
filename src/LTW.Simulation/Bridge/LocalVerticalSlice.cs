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
    private const int RelaySignalGoldPerHit = 1;

    private readonly ContentCatalog content;
    private readonly EconomyService economy;
    private readonly GridPathService pathService;
    private readonly CombatService combat;
    private readonly CommandContentValidator commandValidator;
    private readonly Dictionary<LaneId, LaneGrid> grids;
    private readonly Dictionary<LaneId, IReadOnlyList<GridPosition>> routes;
    private readonly CombatContent combatContent;
    private readonly LocalMatchOptions options;
    private readonly LocalMatchTopology topology;
    private readonly List<ISimulationEvent> pendingEvents = new();
    private readonly Dictionary<PlayerId, BotController> bots;
    private readonly List<AcceptedCommandRecord> acceptedCommands = new();
    private readonly List<BotDecisionRecord> botDecisionRecords = new();

    private EconomyPlayerSet players;
    private CombatState combatState;
    private SimulationTick tick;
    private long nextEntityId = 1;
    private bool matchStarted;
    private bool matchEnded;

    public MatchSummary? MatchSummary { get; private set; }

    public ReplayRecord GetReplayRecord() => new ReplayRecord(options.Seed, content.Version, content.Maps[0].Id, players.Players.Select(player => player.PlayerId).ToArray(), tick, acceptedCommands);

    /// <summary>
    /// Total creep entities held in combat state, unfiltered by HasLeaked. GetSnapshot's creep list goes
    /// through GetCreepSnapshots, which already excludes spent lane-transfer entities, so it cannot show
    /// whether one is still sitting in CombatState. Exists to verify the count stays bounded to live
    /// creeps instead of accumulating a tombstone per lane hop — see OPEN_ITEMS.md item 11.
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
        economy = new EconomyService(new EconomyRules(incomeIntervalTicks: 50, sendCooldownTicks: 0, sellRefundPercent: 50, leakLifeLoss: 1));
        pathService = new GridPathService();
        combat = new CombatService();
        commandValidator = new CommandContentValidator();

        var map = content.Maps[0];
        grids = new Dictionary<LaneId, LaneGrid>();
        routes = new Dictionary<LaneId, IReadOnlyList<GridPosition>>();
        foreach (var laneId in topology.Lanes)
        {
            grids[laneId] = new LaneGrid(map);
            routes[laneId] = pathService.FindRoute(grids[laneId]).Route;
        }
        combatContent = new CombatContent(
            content.Creeps,
            content.Towers,
            topology.LaneOwners);
        players = CreateStartingPlayers(topology.Players);
        combatState = new CombatState(Enumerable.Empty<CreepCombatState>(), Enumerable.Empty<TowerCombatState>());
        bots = enableBots ? CreateBots(topology.Players) : new Dictionary<PlayerId, BotController>();
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
        routes[laneId] = placement.Route;
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount - tower.Cost.Amount)));
        combatState = new CombatState(
            combatState.Creeps,
            combatState.Towers.Concat(new[] { new TowerCombatState(towerEntityId, towerId, playerId, laneId, position) }));
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
        var creep = content.Creeps.FirstOrDefault(definition => definition.Id.Equals(creepId));
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

        var creep = content.Creeps.First(definition => definition.Id.Equals(creepId));
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
        var spawned = Enumerable.Range(0, quantity).Select(_ => combat.SpawnCreep(NextEntityId(), creep, playerId, laneId)).ToArray();
        combatState = new CombatState(combatState.Creeps.Concat(spawned), combatState.Towers);
        acceptedCommands.Add(new AcceptedCommandRecord(tick, playerId, creepId, quantity));
        pendingEvents.Add(new CreepQueuedEvent(tick, playerId, send.TargetPlayerId.Value, creepId, quantity));
        foreach (var spawnedCreep in spawned) pendingEvents.Add(new CreepSpawnedEvent(tick, spawnedCreep.EntityId, creepId, playerId, send.TargetPlayerId.Value));
        return VerticalSliceCommandResult.Accept();
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
        var towerDefinition = content.Towers.First(definition => definition.Id.Equals(tower.TowerId));
        var refund = economy.CalculateSellRefund(towerDefinition);
        var player = players.Get(playerId);
        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount + refund.Amount)));
        combatState = combatState.RemoveTower(tower.EntityId);
        grids[tower.LaneId] = grids[tower.LaneId].WithoutOccupied(tower.Position);
        routes[tower.LaneId] = pathService.FindRoute(grids[tower.LaneId]).Route;
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

        // Send decision runs before tower building (reordered 2026-07-28 — see
        // GD_TUNING_LOG.md). TryPlaceBotTower has no cap and previously ran first, so it
        // would absorb a bot's surplus gold into "one more tower" before a pricier preferred
        // creep (e.g. creep.serpent at 20g, creep.obsidian_brute at 30g) ever had a chance to
        // become affordable — confirmed via replay analysis showing those creeps at 0 uses
        // even though they were reachable on paper. Giving the send its claim on gold first,
        // with towers only spending what's left, fixes the starvation without adding a new
        // tunable cap.
        //
        // OrderBy(bot.Key.Value): Dictionary<PlayerId, BotController> enumeration order is
        // documented-unspecified, and it feeds NextEntityId() assignment (via QueueSend/PlaceTower)
        // here, which is the final tie-breaker in SelectTarget, Pulse's splash Take(2) and ChainArc.
        // It happens to be insertion order today (no removals from this dictionary), but a sim that
        // records and replays should not rely on that — SeedExpandedLaneBotOpeners already sorts for
        // the same reason (OPEN_ITEMS.md item 23).
        foreach (var bot in bots.OrderBy(bot => bot.Key.Value))
        {
            if (HasMinimumDefenseCoverage(bot.Key, bot.Value) && !IsLaneUnderPressure(bot.Key, bot.Value))
            {
                var decision = bot.Value.Decide(players.Get(bot.Key), content, tick);
                if (decision.Command is QueueSendCommand send)
                {
                    var sendResult = QueueSend(send.PlayerId, send.CreepId, send.Quantity);
                    if (sendResult.Accepted)
                    {
                        botDecisionRecords.Add(new BotDecisionRecord(tick, send.PlayerId, bot.Value.Profile, send.CreepId, send.Quantity));
                    }
                }
            }

            TryPlaceBotTower(bot.Key, bot.Value);
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
        var result = combat.Advance(combatState, combatContent, routes, tick);
        combatState = result.State;
        foreach (var simulationEvent in result.Events)
        {
            if (simulationEvent is CreepDamagedEvent damaged)
            {
                ApplyRelaySignalGold(damaged);
            }

            if (simulationEvent is CreepKilledEvent killed && creepsBeforeCombat.TryGetValue(killed.CreepEntityId, out var killedCreep))
                players = economy.ApplyKillBounty(players, killed.DefenderId, content.Creeps.First(creep => creep.Id.Equals(killedCreep.CreepId))).Players;
            if (simulationEvent is LeakEvent leak && creepsBeforeCombat.TryGetValue(leak.CreepEntityId, out var leakedCreep))
            {
                var creep = content.Creeps.First(definition => definition.Id.Equals(leakedCreep.CreepId));
                var defenderLivesBefore = players.Get(leak.DefenderId).Lives.Amount;
                players = economy.ApplyLeak(players, leak.SenderId, leak.DefenderId, creep, leak.LivesLost).Players;
                if (defenderLivesBefore > 0 && players.Get(leak.DefenderId).Lives.Amount == 0)
                {
                    pendingEvents.Add(new PlayerEliminatedEvent(tick, leak.DefenderId));
                }

                // The spent entity is removed unconditionally, not just when it transfers. Reaching a lane
                // end is not death (health carries forward per the design note in OPEN_ITEMS.md item 11),
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
            combat.GetCreepSnapshots(combatState, combatContent, routes),
            combatState.Towers,
            combat.GetTowerAimSnapshots(combatState, combatContent, routes));

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
            routes[laneId] = pathService.FindRoute(grids[laneId]).Route;
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
            TryPlaceBotTower(bot.Key, bot.Value);
            var sendResult = QueueSend(bot.Key, bot.Value.PrimaryCreepId, quantity: 1);
            if (sendResult.Accepted)
            {
                botDecisionRecords.Add(new BotDecisionRecord(tick, bot.Key, bot.Value.Profile, bot.Value.PrimaryCreepId, quantity: 1));
            }
        }
    }

    /// <summary>
    /// Minimum tower count before a non-Greedy bot is allowed to send at all — a floor gate
    /// checked by <see cref="HasMinimumDefenseCoverage"/>, never a ceiling on how much a bot can
    /// build (see <see cref="TryPlaceBotTower"/>, which keeps building past this number as long as
    /// gold and candidate positions allow).
    /// </summary>
    private static int MinimumTowerCoverage(BotDecisionProfile profile) => profile switch
    {
        BotDecisionProfile.Balanced => 3,
        BotDecisionProfile.Defensive => 4,
        _ => 0
    };

    /// <summary>
    /// Greedy bots are designed to send from the start (they prioritize income, not a defensive
    /// package); Balanced and Defensive are designed to finish their opening tower package before
    /// creating any send pressure. That intent was previously enforced only indirectly, through
    /// gold-reserve thresholds tuned against specific tower costs — cheap enough towers could
    /// leave just enough spare gold to opportunistically afford a cheap creep mid-build-out. This
    /// checks the actual intent directly instead, so it holds regardless of the current cost
    /// balance.
    /// </summary>
    private bool HasMinimumDefenseCoverage(PlayerId playerId, BotController bot)
    {
        if (bot.Profile == BotDecisionProfile.Greedy)
        {
            return true;
        }

        var ownedTowerCount = combatState.Towers.Count(tower => tower.OwnerId.Equals(playerId));
        return ownedTowerCount >= MinimumTowerCoverage(bot.Profile);
    }

    /// <summary>
    /// True once a non-Greedy bot's own lane is carrying enough incoming creep health that it
    /// should hold/build instead of spending gold on sends — the reactive replacement for the old
    /// fixed opening-tower-count gate, driven by actual lane threat rather than a tick schedule.
    /// Greedy is exempt: sending is its primary lever (see docs/ARCHITECTURE.md's Bot Controller
    /// section), not something pressure should suppress.
    /// </summary>
    private bool IsLaneUnderPressure(PlayerId playerId, BotController bot)
    {
        if (bot.Profile == BotDecisionProfile.Greedy)
        {
            return false;
        }

        var myLane = topology.HomeLaneFor(playerId);

        // !HasLeaked is load-bearing, not defensive tidiness. A creep that finishes a lane is not
        // despawned — it transfers to the next opponent's lane as a NEW entity, and the spent entity stays
        // in CombatState tagged with the lane it exited, still holding the health it left with. So without
        // this filter, incomingHealth counts every creep that has ever finished walking this lane and grows
        // monotonically for the whole match. Once it crosses PressureThreshold the bot stops sending and
        // never sends again.
        //
        // Every other creep filter in the codebase already excludes HasLeaked (eight sites in
        // CombatService, plus GetCreepSnapshots), which is why nothing looked wrong on screen — this was
        // the only consumer that saw the spent entities.
        //
        // Measured consequence of the fix, and it corrects an earlier diagnosis of mine: the "two mazing
        // bots stalemate forever" finding was attributed to defence out-scaling attack. It was not. With
        // this filter the same seed completes at tick 926 instead of running past 80,000. It is also the
        // real cause of the bot that sat on 3,700 gold with a frozen tower count, which I previously
        // blamed on the placement ceiling alone.
        var incomingHealth = combatState.Creeps
            .Where(creep => creep.LaneId.Equals(myLane) && !creep.IsDead && !creep.HasLeaked)
            .Sum(creep => creep.Health);
        var ownedTowerCount = combatState.Towers.Count(tower => tower.OwnerId.Equals(playerId));
        return incomingHealth >= bot.PressureThreshold(content, ownedTowerCount);
    }

    private void TryPlaceBotTower(PlayerId playerId, BotController bot)
    {
        var ownedTowerCount = combatState.Towers.Count(tower => tower.OwnerId.Equals(playerId));
        var towerId = BotTowerForSlot(bot.Profile, ownedTowerCount);
        var towerCost = content.Towers.First(tower => tower.Id.Equals(towerId)).Cost.Amount;
        var player = players.Get(playerId);
        if (player.Gold.Amount - bot.GoldReserveFloor(content) < towerCost)
        {
            return;
        }

        var laneId = topology.HomeLaneFor(playerId);
        var position = BestMazingPlacement(playerId, laneId, towerId, content.Towers.First(t => t.Id.Equals(towerId)).RangeCells);
        if (position is not null)
        {
            PlaceTower(playerId, laneId, towerId, position.Value);
        }
    }

    /// <summary>
    /// Picks the cell that best lengthens the creep route while still covering it — mazing.
    /// </summary>
    /// <remarks>
    /// This replaced a hardcoded list of nine positions in columns 1 and 5, chosen with no reference to
    /// the route at all. That arrangement had two consequences worth stating, because both distorted
    /// every balance measurement taken against these bots. It never mazed, so bots defended a straight
    /// lane no human would leave straight; and once those nine cells were occupied the bot could never
    /// build again, which is why a bot in an earlier probe sat on 3,700 gold with its tower count frozen
    /// at nine.
    ///
    /// Scoring is deliberately simple and explainable rather than clever:
    ///   route length gained x MazeLengthWeight   — how much longer the creeps' walk becomes
    ///   + route cells this tower covers          — how much of that walk it can actually shoot
    /// Length dominates, because a cell that adds ten steps of walking helps every tower already built,
    /// while coverage only helps this one. Cells that would block the route entirely are rejected by
    /// GridPathService before they are ever scored.
    ///
    /// Cost: one BFS per candidate cell per placement. The grid is 7x16 and a bot places a tower at most
    /// once per tick, so this is bounded and small, but it is the reason the search is a single pass over
    /// empty cells rather than a lookahead.
    /// </remarks>
    private GridPosition? BestMazingPlacement(PlayerId playerId, LaneId laneId, ContentId towerId, int rangeCells)
    {
        if (!grids.TryGetValue(laneId, out var grid) || !routes.TryGetValue(laneId, out var currentRoute))
        {
            return null;
        }

        var map = content.Maps[0];
        GridPosition? best = null;
        var bestScore = int.MinValue;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var candidate = new GridPosition(x, y);
                var validation = ValidateTowerPlacement(playerId, laneId, towerId, candidate);
                if (!validation.Result.Accepted)
                {
                    continue;
                }

                var route = validation.Placement!.Route;
                var lengthGain = route.Count - currentRoute.Count;
                var covered = 0;
                for (var index = 0; index < route.Count; index++)
                {
                    var cell = route[index];
                    if (System.Math.Abs(cell.X - candidate.X) + System.Math.Abs(cell.Y - candidate.Y) <= rangeCells)
                    {
                        covered++;
                    }
                }

                var score = lengthGain * MazeLengthWeight + covered;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// How many route cells of coverage one extra step of creep walking is worth.
    /// </summary>
    /// <remarks>
    /// Above 1 so lengthening wins ties against merely covering more. Extra route length multiplies
    /// across every tower the bot owns and every one it will build later, whereas coverage from a single
    /// placement only ever helps that placement.
    /// </remarks>
    private const int MazeLengthWeight = 4;

    // Cycled by ownedTowerCount rather than switched on a few slots with a repeating tail arm, for two
    // reasons (OPEN_ITEMS.md item 15): the old shape could only ever reach 5 of the 15 towers (Arrow,
    // Control, Pulse, Prism plus whatever the tail arm was), so every mechanic added since the 15-tower
    // expansion was measured against a bot that never builds it; and its tail arm repeated a single
    // tower forever once reached (Defensive -> endless Prism, Greedy -> endless Arrow), which is why
    // BotMazingTests' "keeps building past nine towers" assertion passed on a bot spamming one tower.
    // Each profile's array is a flavour (Defensive leans control/area/support, Greedy leans cheap, both
    // fully reachable but not the only towers that profile builds), and the three arrays' union covers
    // all 15 towers, not just each profile's own list.
    // Each array's first few entries deliberately match the old hardcoded switch's early slots
    // exactly (same tower, same cost, same order) rather than reshuffling from slot 0. Income only
    // ticks every 50 simulation ticks (IncomeIntervalTicks), so gold is flat between jumps and a
    // bot's opening tower-count gate (HasMinimumDefenseCoverage) clears on whichever jump first
    // covers the cumulative cost — even a few gold of difference in an early slot can push that
    // past a 50-tick boundary and shift the observable timing by up to a full income cycle. Keeping
    // the opening identical avoids re-tuning every test that depends on early bot timing; the fix
    // for item 15 only needs the array to stop repeating forever once it reaches its old tail.
    private static readonly ContentId[] DefensiveBuildOrder =
    {
        SampleVerticalSliceContent.ControlTowerId,
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.PulseTowerId,
        SampleVerticalSliceContent.PrismTowerId,
        SampleVerticalSliceContent.RepairDroneTowerId,
        SampleVerticalSliceContent.ElderCanopyTowerId,
        SampleVerticalSliceContent.ThornSnareTowerId,
        SampleVerticalSliceContent.BarricadeTowerId
    };

    private static readonly ContentId[] BalancedBuildOrder =
    {
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.ControlTowerId,
        SampleVerticalSliceContent.PulseTowerId,
        SampleVerticalSliceContent.PulseTowerId,
        SampleVerticalSliceContent.GatlingTowerId,
        SampleVerticalSliceContent.SaplingTowerId,
        SampleVerticalSliceContent.TeslaTowerId,
        SampleVerticalSliceContent.BloomheartTowerId,
        SampleVerticalSliceContent.PrismTowerId,
        SampleVerticalSliceContent.FoundryTowerId,
        SampleVerticalSliceContent.SporeCloudTowerId
    };

    private static readonly ContentId[] GreedyBuildOrder =
    {
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.PrismTowerId,
        SampleVerticalSliceContent.TowerId,
        SampleVerticalSliceContent.SaplingTowerId,
        SampleVerticalSliceContent.GatlingTowerId,
        SampleVerticalSliceContent.UtilityTowerId,
        SampleVerticalSliceContent.SporeCloudTowerId
    };

    private static ContentId BotTowerForSlot(BotDecisionProfile profile, int ownedTowerCount)
    {
        var buildOrder = profile switch
        {
            BotDecisionProfile.Defensive => DefensiveBuildOrder,
            BotDecisionProfile.Balanced => BalancedBuildOrder,
            _ => GreedyBuildOrder
        };

        return buildOrder[ownedTowerCount % buildOrder.Length];
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

        var tower = content.Towers.First(definition => definition.Id.Equals(towerId));
        var player = players.Get(playerId);
        if (player.Gold.Amount < tower.Cost.Amount)
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InsufficientGold);
        }

        return TowerPlacementValidation.Accept(grid, placement, tower, player);
    }

    private EntityId NextEntityId() => new EntityId(nextEntityId++);

    private void ApplyRelaySignalGold(CreepDamagedEvent damaged)
    {
        var tower = combatState.Towers.FirstOrDefault(candidate => candidate.EntityId.Equals(damaged.TowerEntityId));
        if (tower is null || !IsRelayTower(tower.TowerId))
        {
            return;
        }

        var player = players.Get(tower.OwnerId);
        if (player.IsEliminated)
        {
            return;
        }

        players = players.Replace(player.WithGold(new Gold(player.Gold.Amount + RelaySignalGoldPerHit)));
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

    private static bool IsRelayTower(ContentId towerId) =>
        towerId.Value.IndexOf("relay", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
        towerId.Value.IndexOf("utility", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
        towerId.Value.IndexOf("economy", System.StringComparison.OrdinalIgnoreCase) >= 0;

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
}
