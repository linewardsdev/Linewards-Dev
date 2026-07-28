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
        economy = new EconomyService(new EconomyRules(incomeIntervalTicks: 50, sendCooldownTicks: 30, sellRefundPercent: 50, leakLifeLoss: 1));
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

        foreach (var bot in bots)
        {
            TryPlaceBotTower(bot.Key, bot.Value);
            if (!HasCompletedOpeningDefense(bot.Key, bot.Value))
            {
                continue;
            }

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

        tick = new SimulationTick(tick.Value + 1);
        if (economy.IsIncomeTick(tick))
        {
            foreach (var player in players.Players.Where(player => !player.IsEliminated))
            {
                pendingEvents.Add(new IncomeTickEvent(tick, player.PlayerId, new Gold(player.Income.Amount)));
            }
        }

        players = economy.ApplyIncomeTick(players, tick);
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
        new VerticalSliceSnapshot(tick, players, combat.GetCreepSnapshots(combatState, routes), combatState.Towers);

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
        return playerIds
            .Where(playerId => playerId.Value != 1 && options.IsBotEnabledFor(playerId))
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

    private static int DesiredOpeningTowerCount(BotDecisionProfile profile) => profile switch
    {
        BotDecisionProfile.Greedy => 2,
        BotDecisionProfile.Balanced => 3,
        BotDecisionProfile.Defensive => 4,
        _ => 2
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
    private bool HasCompletedOpeningDefense(PlayerId playerId, BotController bot)
    {
        if (bot.Profile == BotDecisionProfile.Greedy)
        {
            return true;
        }

        var ownedTowerCount = combatState.Towers.Count(tower => tower.OwnerId.Equals(playerId));
        return ownedTowerCount >= DesiredOpeningTowerCount(bot.Profile);
    }

    private void TryPlaceBotTower(PlayerId playerId, BotController bot)
    {
        var ownedTowerCount = combatState.Towers.Count(tower => tower.OwnerId.Equals(playerId));
        if (ownedTowerCount >= DesiredOpeningTowerCount(bot.Profile))
        {
            return;
        }

        var towerId = BotTowerForSlot(bot.Profile, ownedTowerCount);
        var laneId = topology.HomeLaneFor(playerId);
        var candidates = BotPlacementCandidates(bot.Profile, ownedTowerCount);

        foreach (var position in candidates)
        {
            if (PlaceTower(playerId, laneId, towerId, position).Accepted)
            {
                return;
            }
        }
    }

    private static ContentId BotTowerForSlot(BotDecisionProfile profile, int ownedTowerCount)
    {
        if (profile == BotDecisionProfile.Defensive)
        {
            return ownedTowerCount switch
            {
                0 => SampleVerticalSliceContent.ControlTowerId,
                1 => SampleVerticalSliceContent.TowerId,
                2 => SampleVerticalSliceContent.TowerId,
                3 => SampleVerticalSliceContent.PulseTowerId,
                _ => SampleVerticalSliceContent.PrismTowerId
            };
        }

        if (profile == BotDecisionProfile.Balanced)
        {
            return ownedTowerCount switch
            {
                0 => SampleVerticalSliceContent.TowerId,
                1 => SampleVerticalSliceContent.ControlTowerId,
                _ => SampleVerticalSliceContent.PulseTowerId
            };
        }

        return ownedTowerCount == 1 ? SampleVerticalSliceContent.PrismTowerId : SampleVerticalSliceContent.TowerId;
    }

    private static IReadOnlyList<GridPosition> BotPlacementCandidates(BotDecisionProfile profile, int ownedTowerCount)
    {
        if (profile == BotDecisionProfile.Defensive)
        {
            return ownedTowerCount switch
            {
                0 => new[] { new GridPosition(1, 3), new GridPosition(5, 5), new GridPosition(1, 7) },
                1 => new[] { new GridPosition(5, 3), new GridPosition(1, 5), new GridPosition(5, 7) },
                _ => new[] { new GridPosition(5, 9), new GridPosition(1, 11), new GridPosition(5, 13) }
            };
        }

        return ownedTowerCount switch
        {
            0 => new[] { new GridPosition(5, 3), new GridPosition(1, 5), new GridPosition(5, 7) },
            _ => new[] { new GridPosition(1, 7), new GridPosition(5, 9), new GridPosition(1, 11) }
        };
    }

    private TowerPlacementValidation ValidateTowerPlacement(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position)
    {
        var command = new PlaceTowerCommand(playerId, tick, laneId, towerId, position);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return TowerPlacementValidation.Reject(contentResult.RejectionReason);
        }

        if (!grids.TryGetValue(laneId, out var grid))
        {
            return TowerPlacementValidation.Reject(CommandRejectionReason.InvalidLane);
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
