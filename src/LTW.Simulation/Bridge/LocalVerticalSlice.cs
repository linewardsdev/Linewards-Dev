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
    private readonly ContentCatalog content;
    private readonly EconomyService economy;
    private readonly GridPathService pathService;
    private readonly CombatService combat;
    private readonly CommandContentValidator commandValidator;
    private readonly Dictionary<LaneId, LaneGrid> grids;
    private readonly Dictionary<LaneId, IReadOnlyList<GridPosition>> routes;
    private readonly CombatContent combatContent;
    private readonly List<ISimulationEvent> pendingEvents = new();
    private readonly Dictionary<PlayerId, BotController> bots;
    private readonly List<AcceptedCommandRecord> acceptedCommands = new();
    private readonly List<BotDecisionRecord> botDecisionRecords = new();

    private EconomyPlayerSet players;
    private CombatState combatState;
    private SimulationTick tick;
    private long nextEntityId = 1;
    private bool matchEnded;

    public MatchSummary? MatchSummary { get; private set; }

    public ReplayRecord GetReplayRecord() => new ReplayRecord(1, content.Version, content.Maps[0].Id, players.Players.Select(player => player.PlayerId).ToArray(), tick, acceptedCommands);

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

    public LocalVerticalSlice(ContentCatalog content)
    {
        this.content = content;
        economy = new EconomyService(new EconomyRules(incomeIntervalTicks: 50, sendCooldownTicks: 30, sellRefundPercent: 50, leakLifeLoss: 1));
        pathService = new GridPathService();
        combat = new CombatService();
        commandValidator = new CommandContentValidator();

        var map = content.Maps[0];
        grids = new Dictionary<LaneId, LaneGrid>();
        routes = new Dictionary<LaneId, IReadOnlyList<GridPosition>>();
        for (var lane = 1; lane <= 3; lane++)
        {
            var laneId = new LaneId(lane);
            grids[laneId] = new LaneGrid(map);
            routes[laneId] = pathService.FindRoute(grids[laneId]).Route;
        }
        combatContent = new CombatContent(
            content.Creeps,
            content.Towers,
            new Dictionary<LaneId, PlayerId> { [new LaneId(1)] = new PlayerId(1), [new LaneId(2)] = new PlayerId(2), [new LaneId(3)] = new PlayerId(3) });
        players = new EconomyPlayerSet(new[]
        {
            new PlayerEconomyState(new PlayerId(1), new Gold(100), new Income(10), new Lives(140)),
            new PlayerEconomyState(new PlayerId(2), new Gold(100), new Income(10), new Lives(140)),
            new PlayerEconomyState(new PlayerId(3), new Gold(100), new Income(10), new Lives(140))
        });
        combatState = new CombatState(Enumerable.Empty<CreepCombatState>(), Enumerable.Empty<TowerCombatState>());
        bots = new Dictionary<PlayerId, BotController>
        {
            [new PlayerId(2)] = new BotController(BotDecisionProfile.Balanced, content.Creeps[0].Id),
            [new PlayerId(3)] = new BotController(BotDecisionProfile.Defensive, content.Creeps[0].Id)
        };
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

    public VerticalSliceCommandResult QueueSend(PlayerId playerId, ContentId creepId, int quantity)
    {
        var command = new QueueSendCommand(playerId, tick, creepId, quantity);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return VerticalSliceCommandResult.Reject(contentResult.RejectionReason);
        }

        var creep = content.Creeps.First(definition => definition.Id.Equals(creepId));
        var send = economy.QueueSend(players, playerId, creep, quantity, tick);
        if (!send.Accepted)
        {
            return VerticalSliceCommandResult.Reject(send.RejectionReason);
        }

        players = send.Players;
        var laneId = new LaneId(send.TargetPlayerId!.Value.Value);
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
        if (tower is null)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.NotOwner);
        }

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
        foreach (var bot in bots)
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
            if (simulationEvent is CreepKilledEvent killed && creepsBeforeCombat.TryGetValue(killed.CreepEntityId, out var killedCreep))
                players = economy.ApplyKillBounty(players, killed.DefenderId, content.Creeps.First(creep => creep.Id.Equals(killedCreep.CreepId))).Players;
            if (simulationEvent is LeakEvent leak && creepsBeforeCombat.TryGetValue(leak.CreepEntityId, out var leakedCreep))
            {
                var creep = content.Creeps.First(definition => definition.Id.Equals(leakedCreep.CreepId));
                var defenderLivesBefore = players.Get(leak.DefenderId).Lives.Amount;
                players = economy.ApplyLeak(players, leak.SenderId, leak.DefenderId, creep).Players;
                if (defenderLivesBefore > 0 && players.Get(leak.DefenderId).Lives.Amount == 0)
                {
                    pendingEvents.Add(new PlayerEliminatedEvent(tick, leak.DefenderId));
                }

                var nextLaneId = NextLaneId(leakedCreep.LaneId);
                var transferred = combat.SpawnCreep(NextEntityId(), creep, leakedCreep.SenderId, nextLaneId);
                combatState = new CombatState(combatState.Creeps.Concat(new[] { transferred }), combatState.Towers);
                pendingEvents.Add(new CreepSpawnedEvent(tick, transferred.EntityId, transferred.CreepId, transferred.SenderId, new PlayerId(nextLaneId.Value)));
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
        players = new EconomyPlayerSet(new[]
        {
            new PlayerEconomyState(new PlayerId(1), new Gold(100), new Income(10), new Lives(140)),
            new PlayerEconomyState(new PlayerId(2), new Gold(100), new Income(10), new Lives(140)),
            new PlayerEconomyState(new PlayerId(3), new Gold(100), new Income(10), new Lives(140))
        });
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
        matchEnded = false;
        MatchSummary = null;
        acceptedCommands.Clear();
        botDecisionRecords.Clear();
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

    private static LaneId NextLaneId(LaneId laneId) => new(laneId.Value % 3 + 1);

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
}
