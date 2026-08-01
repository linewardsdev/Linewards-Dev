using LTW.Simulation.Commands;
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

/// <summary>
/// A player bought a new tier for one of their categories.
/// </summary>
/// <remarks>
/// Carries the gold spent as well as the tier reached so the presentation layer can show what the
/// purchase cost without re-deriving it from the rules table, and so a replay reads as a record of
/// what happened rather than something that has to be recomputed to be understood.
/// </remarks>
public sealed class CategoryTierPurchasedEvent : ISimulationEvent
{
    public CategoryTierPurchasedEvent(SimulationTick tick, PlayerId playerId, CategoryKind categoryKind, int categoryIndex, int tier, Gold cost)
    {
        Tick = tick;
        PlayerId = playerId;
        CategoryKind = categoryKind;
        CategoryIndex = categoryIndex;
        Tier = tier;
        Cost = cost;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }

    public CategoryKind CategoryKind { get; }

    public int CategoryIndex { get; }

    public int Tier { get; }

    public Gold Cost { get; }
}

/// <summary>
/// One placed tower was raised a tier.
/// </summary>
public sealed class TowerUpgradedEvent : ISimulationEvent
{
    public TowerUpgradedEvent(SimulationTick tick, PlayerId playerId, LaneId laneId, EntityId towerEntityId, ContentId towerId, GridPosition position, int tier, Gold cost)
    {
        Tick = tick;
        PlayerId = playerId;
        LaneId = laneId;
        TowerEntityId = towerEntityId;
        TowerId = towerId;
        Position = position;
        Tier = tier;
        Cost = cost;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }

    public LaneId LaneId { get; }

    public EntityId TowerEntityId { get; }

    public ContentId TowerId { get; }

    public GridPosition Position { get; }

    public int Tier { get; }

    public Gold Cost { get; }
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


public sealed class TowerFiredEvent : ISimulationEvent
{
    public TowerFiredEvent(
        SimulationTick tick,
        LaneId laneId,
        EntityId towerEntityId,
        GridPosition towerPosition,
        EntityId targetCreepEntityId,
        GridPosition targetPosition,
        SimulationTick impactTick,
        GridPosition impactPosition)
    {
        Tick = tick;
        LaneId = laneId;
        TowerEntityId = towerEntityId;
        TowerPosition = towerPosition;
        TargetCreepEntityId = targetCreepEntityId;
        TargetPosition = targetPosition;
        ImpactTick = impactTick;
        ImpactPosition = impactPosition;
    }

    public SimulationTick Tick { get; }

    public LaneId LaneId { get; }

    public EntityId TowerEntityId { get; }

    public GridPosition TowerPosition { get; }

    public EntityId TargetCreepEntityId { get; }

    public GridPosition TargetPosition { get; }

    /// <summary>
    /// Tick this shot actually lands. Equal to <see cref="Tick"/> for every direct-fire tower, and
    /// that is honest rather than a fudge: an instant hit lands this tick.
    /// </summary>
    /// <remarks>
    /// Only the Foundry Core's mortar sets a future tick. The launch event fully describing the
    /// shell's destination and arrival is what lets the renderer animate the whole flight locally,
    /// including a whiff — where the simulation emits no damage event at all and an event-driven
    /// impact would make the shell visually evaporate in mid-air.
    /// </remarks>
    public SimulationTick ImpactTick { get; }

    /// <summary>
    /// Cell this shot lands on. Equal to <see cref="TargetPosition"/> for direct fire; for the
    /// mortar it is the LED cell, which is where the target is predicted to be on impact.
    /// </summary>
    public GridPosition ImpactPosition { get; }
}

public sealed class CreepDamagedEvent : ISimulationEvent
{
    public CreepDamagedEvent(SimulationTick tick, PlayerId defenderId, LaneId laneId, EntityId towerEntityId, GridPosition towerPosition, EntityId creepEntityId, int damageDealt)
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

    public int DamageDealt { get; }
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

/// <summary>
/// A Mender restored health to a friendly creep.
/// </summary>
/// <remarks>
/// Raised so presentation can show healing at all. Without it a Mender is invisible: the creep's
/// health bar creeps back up with nothing on screen explaining why, which reads as a bug rather
/// than as a unit doing its job. Mirrors <see cref="CreepDamagedEvent"/> so the renderer can treat
/// the two as one family — a number floating off a creep, differing only in sign and colour.
///
/// Carries the resulting health as well as the amount, because the amount alone cannot be turned
/// into a bar: clamping at MaxHealth means a heal of 1 sometimes restores less than 1.
/// </remarks>
public sealed class CreepHealedEvent : ISimulationEvent
{
    public CreepHealedEvent(SimulationTick tick, EntityId creepEntityId, int healed, int health)
    {
        Tick = tick;
        CreepEntityId = creepEntityId;
        Healed = healed;
        Health = health;
    }

    public SimulationTick Tick { get; }

    public EntityId CreepEntityId { get; }

    public int Healed { get; }

    public int Health { get; }
}
