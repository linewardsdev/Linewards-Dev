using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Events;

public interface ISimulationEvent
{
    SimulationTick Tick { get; }
}

public sealed class CommandRejectedEvent : ISimulationEvent
{
    public CommandRejectedEvent(SimulationTick tick, PlayerId playerId, string reason)
    {
        Tick = tick;
        PlayerId = playerId;
        Reason = reason;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }

    public string Reason { get; }
}

public sealed class TowerPlacedEvent : ISimulationEvent
{
    public TowerPlacedEvent(SimulationTick tick, PlayerId playerId, LaneId laneId, EntityId towerEntityId, ContentId towerId, GridPosition position)
    {
        Tick = tick;
        PlayerId = playerId;
        LaneId = laneId;
        TowerEntityId = towerEntityId;
        TowerId = towerId;
        Position = position;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }

    public LaneId LaneId { get; }

    public EntityId TowerEntityId { get; }

    public ContentId TowerId { get; }

    public GridPosition Position { get; }
}

public sealed class TowerSoldEvent : ISimulationEvent
{
    public TowerSoldEvent(SimulationTick tick, PlayerId playerId, LaneId laneId, EntityId towerEntityId, Gold refund)
    {
        Tick = tick;
        PlayerId = playerId;
        LaneId = laneId;
        TowerEntityId = towerEntityId;
        Refund = refund;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }

    public LaneId LaneId { get; }

    public EntityId TowerEntityId { get; }

    public Gold Refund { get; }
}

public sealed class TechPurchasedEvent : ISimulationEvent
{
    public TechPurchasedEvent(SimulationTick tick, PlayerId playerId, ContentId techId)
    {
        Tick = tick;
        PlayerId = playerId;
        TechId = techId;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }

    public ContentId TechId { get; }
}

public sealed class CreepQueuedEvent : ISimulationEvent
{
    public CreepQueuedEvent(SimulationTick tick, PlayerId senderId, PlayerId defenderId, ContentId creepId, int quantity)
    {
        Tick = tick;
        SenderId = senderId;
        DefenderId = defenderId;
        CreepId = creepId;
        Quantity = quantity;
    }

    public SimulationTick Tick { get; }

    public PlayerId SenderId { get; }

    public PlayerId DefenderId { get; }

    public ContentId CreepId { get; }

    public int Quantity { get; }
}

public sealed class CreepSpawnedEvent : ISimulationEvent
{
    public CreepSpawnedEvent(SimulationTick tick, EntityId creepEntityId, ContentId creepId, PlayerId senderId, PlayerId defenderId)
    {
        Tick = tick;
        CreepEntityId = creepEntityId;
        CreepId = creepId;
        SenderId = senderId;
        DefenderId = defenderId;
    }

    public SimulationTick Tick { get; }

    public EntityId CreepEntityId { get; }

    public ContentId CreepId { get; }

    public PlayerId SenderId { get; }

    public PlayerId DefenderId { get; }
}


public sealed class CreepDamagedEvent : ISimulationEvent
{
    public CreepDamagedEvent(SimulationTick tick, PlayerId defenderId, LaneId laneId, EntityId towerEntityId, GridPosition towerPosition, EntityId creepEntityId, Gold damageDealt)
    {
        Tick = tick;
        DefenderId = defenderId;
        LaneId = laneId;
        TowerEntityId = towerEntityId;
        TowerPosition = towerPosition;
        CreepEntityId = creepEntityId;
        DamageDealt = damageDealt;
    }

    public SimulationTick Tick { get; }

    public PlayerId DefenderId { get; }

    public LaneId LaneId { get; }

    public EntityId TowerEntityId { get; }

    public GridPosition TowerPosition { get; }

    public EntityId CreepEntityId { get; }

    public Gold DamageDealt { get; }
}

public sealed class CreepKilledEvent : ISimulationEvent
{
    public CreepKilledEvent(SimulationTick tick, EntityId creepEntityId, PlayerId defenderId, Gold bountyAwarded)
    {
        Tick = tick;
        CreepEntityId = creepEntityId;
        DefenderId = defenderId;
        BountyAwarded = bountyAwarded;
    }

    public SimulationTick Tick { get; }

    public EntityId CreepEntityId { get; }

    public PlayerId DefenderId { get; }

    public Gold BountyAwarded { get; }
}

public sealed class IncomeTickEvent : ISimulationEvent
{
    public IncomeTickEvent(SimulationTick tick, PlayerId playerId, Gold goldAwarded)
    {
        Tick = tick;
        PlayerId = playerId;
        GoldAwarded = goldAwarded;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }

    public Gold GoldAwarded { get; }
}

public sealed class LeakEvent : ISimulationEvent
{
    public LeakEvent(SimulationTick tick, PlayerId senderId, PlayerId defenderId, EntityId creepEntityId, Lives livesLost, Gold bountyAwarded)
    {
        Tick = tick;
        SenderId = senderId;
        DefenderId = defenderId;
        CreepEntityId = creepEntityId;
        LivesLost = livesLost;
        BountyAwarded = bountyAwarded;
    }

    public SimulationTick Tick { get; }

    public PlayerId SenderId { get; }

    public PlayerId DefenderId { get; }

    public EntityId CreepEntityId { get; }

    public Lives LivesLost { get; }

    public Gold BountyAwarded { get; }
}

public sealed class PlayerEliminatedEvent : ISimulationEvent
{
    public PlayerEliminatedEvent(SimulationTick tick, PlayerId playerId)
    {
        Tick = tick;
        PlayerId = playerId;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }
}

public sealed class MatchEndedEvent : ISimulationEvent
{
    public MatchEndedEvent(SimulationTick tick, PlayerId winnerId)
    {
        Tick = tick;
        WinnerId = winnerId;
    }

    public SimulationTick Tick { get; }

    public PlayerId WinnerId { get; }
}
