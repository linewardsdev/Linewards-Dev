using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Combat;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Events;
using LTW.Simulation.Pathing;
using LTW.Simulation.Primitives;

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

    private EconomyPlayerSet players;
    private CombatState combatState;
    private SimulationTick tick;
    private long nextEntityId = 1;

    public LocalVerticalSlice(ContentCatalog content)
    {
        this.content = content;
        economy = new EconomyService(new EconomyRules(incomeIntervalTicks: 5, sendCooldownTicks: 3, sellRefundPercent: 50, leakLifeLoss: 1));
        pathService = new GridPathService();
        combat = new CombatService();
        commandValidator = new CommandContentValidator();

        var map = content.Maps[0];
        var laneOne = new LaneId(1);
        grids = new Dictionary<LaneId, LaneGrid> { [laneOne] = new LaneGrid(map) };
        routes = new Dictionary<LaneId, IReadOnlyList<GridPosition>>
        {
            [laneOne] = pathService.FindRoute(grids[laneOne]).Route
        };
        combatContent = new CombatContent(
            content.Creeps,
            content.Towers,
            new Dictionary<LaneId, PlayerId> { [laneOne] = new PlayerId(1) });
        players = new EconomyPlayerSet(new[]
        {
            new PlayerEconomyState(new PlayerId(1), new Gold(100), new Income(10), new Lives(20)),
            new PlayerEconomyState(new PlayerId(2), new Gold(100), new Income(10), new Lives(20)),
            new PlayerEconomyState(new PlayerId(3), new Gold(100), new Income(10), new Lives(20))
        });
        combatState = new CombatState(Enumerable.Empty<CreepCombatState>(), Enumerable.Empty<TowerCombatState>());
        tick = new SimulationTick(0);
    }

    public VerticalSliceCommandResult PlaceTower(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position)
    {
        var command = new PlaceTowerCommand(playerId, tick, laneId, towerId, position);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return VerticalSliceCommandResult.Reject(contentResult.RejectionReason);
        }

        if (!grids.TryGetValue(laneId, out var grid))
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);
        }

        var placement = pathService.ValidatePlacement(grid, position);
        if (!placement.IsValid)
        {
            return VerticalSliceCommandResult.Reject(ToCommandRejection(placement.RejectionReason));
        }

        var tower = content.Towers.First(definition => definition.Id.Equals(towerId));
        var player = players.Get(playerId);
        if (player.Gold.Amount < tower.Cost.Amount)
        {
            return VerticalSliceCommandResult.Reject(CommandRejectionReason.InsufficientGold);
        }

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

    public VerticalSliceCommandResult QueueSend(PlayerId playerId, ContentId creepId)
    {
        var command = new QueueSendCommand(playerId, tick, creepId, quantity: 1);
        var contentResult = commandValidator.Validate(command, content);
        if (!contentResult.Accepted)
        {
            return VerticalSliceCommandResult.Reject(contentResult.RejectionReason);
        }

        var creep = content.Creeps.First(definition => definition.Id.Equals(creepId));
        var send = economy.QueueSend(players, playerId, creep, quantity: 1, tick);
        if (!send.Accepted)
        {
            return VerticalSliceCommandResult.Reject(send.RejectionReason);
        }

        players = send.Players;
        var laneId = new LaneId(1);
        var creepEntityId = NextEntityId();
        combatState = new CombatState(
            combatState.Creeps.Concat(new[] { combat.SpawnCreep(creepEntityId, creep, playerId, laneId) }),
            combatState.Towers);
        pendingEvents.Add(new CreepQueuedEvent(tick, playerId, send.TargetPlayerId!.Value, creepId, quantity: 1));
        pendingEvents.Add(new CreepSpawnedEvent(tick, creepEntityId, creepId, playerId, send.TargetPlayerId.Value));
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
        tick = new SimulationTick(tick.Value + 1);
        players = economy.ApplyIncomeTick(players, tick);
        var result = combat.Advance(combatState, combatContent, routes, tick);
        combatState = result.State;
        foreach (var simulationEvent in result.Events)
        {
            pendingEvents.Add(simulationEvent);
        }
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
            new PlayerEconomyState(new PlayerId(1), new Gold(100), new Income(10), new Lives(20)),
            new PlayerEconomyState(new PlayerId(2), new Gold(100), new Income(10), new Lives(20)),
            new PlayerEconomyState(new PlayerId(3), new Gold(100), new Income(10), new Lives(20))
        });
        combatState = new CombatState(Enumerable.Empty<CreepCombatState>(), Enumerable.Empty<TowerCombatState>());
        pendingEvents.Clear();
        tick = new SimulationTick(0);
        nextEntityId = 1;
    }

    private EntityId NextEntityId() => new EntityId(nextEntityId++);

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
}
