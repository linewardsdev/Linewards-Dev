using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.State;

public sealed class MatchSnapshot
{
    public MatchSnapshot(SimulationTick tick, string contentVersion, IReadOnlyList<PlayerSnapshot> players, IReadOnlyList<LaneSnapshot> lanes)
    {
        Tick = tick;
        ContentVersion = contentVersion;
        Players = CopyRequired(players, nameof(players));
        Lanes = CopyRequired(lanes, nameof(lanes));
    }

    public SimulationTick Tick { get; }

    public string ContentVersion { get; }

    public IReadOnlyList<PlayerSnapshot> Players { get; }

    public IReadOnlyList<LaneSnapshot> Lanes { get; }

    private static IReadOnlyList<T> CopyRequired<T>(IEnumerable<T>? values, string parameterName) =>
        values?.ToArray() ?? throw new System.ArgumentNullException(parameterName);
}

public sealed class PlayerSnapshot
{
    public PlayerSnapshot(PlayerId playerId, Gold gold, Income income, Lives lives, bool isEliminated)
    {
        PlayerId = playerId;
        Gold = gold;
        Income = income;
        Lives = lives;
        IsEliminated = isEliminated;
    }

    public PlayerId PlayerId { get; }

    public Gold Gold { get; }

    public Income Income { get; }

    public Lives Lives { get; }

    public bool IsEliminated { get; }
}

public sealed class LaneSnapshot
{
    public LaneSnapshot(LaneId laneId, PlayerId ownerId, IReadOnlyList<TowerSnapshot> towers, IReadOnlyList<CreepSnapshot> creeps)
    {
        LaneId = laneId;
        OwnerId = ownerId;
        Towers = CopyRequired(towers, nameof(towers));
        Creeps = CopyRequired(creeps, nameof(creeps));
    }

    public LaneId LaneId { get; }

    public PlayerId OwnerId { get; }

    public IReadOnlyList<TowerSnapshot> Towers { get; }

    public IReadOnlyList<CreepSnapshot> Creeps { get; }

    private static IReadOnlyList<T> CopyRequired<T>(IEnumerable<T>? values, string parameterName) =>
        values?.ToArray() ?? throw new System.ArgumentNullException(parameterName);
}

public sealed class TowerSnapshot
{
    public TowerSnapshot(EntityId entityId, ContentId towerId, GridPosition position)
    {
        EntityId = entityId;
        TowerId = towerId;
        Position = position;
    }

    public EntityId EntityId { get; }

    public ContentId TowerId { get; }

    public GridPosition Position { get; }
}

public sealed class CreepSnapshot
{
    public CreepSnapshot(EntityId entityId, ContentId creepId, PlayerId senderId, GridPosition gridPosition, int health)
    {
        EntityId = entityId;
        CreepId = creepId;
        SenderId = senderId;
        GridPosition = gridPosition;
        Health = health;
    }

    public EntityId EntityId { get; }

    public ContentId CreepId { get; }

    public PlayerId SenderId { get; }

    public GridPosition GridPosition { get; }

    public int Health { get; }
}
