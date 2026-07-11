using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CreepCombatState
{
    public CreepCombatState(
        EntityId entityId,
        ContentId creepId,
        PlayerId senderId,
        LaneId laneId,
        int health,
        int pathIndex,
        int movementProgress,
        bool hasLeaked)
    {
        EntityId = entityId;
        CreepId = creepId;
        SenderId = senderId;
        LaneId = laneId;
        Health = health;
        PathIndex = pathIndex;
        MovementProgress = movementProgress;
        HasLeaked = hasLeaked;
    }

    public EntityId EntityId { get; }

    public ContentId CreepId { get; }

    public PlayerId SenderId { get; }

    public LaneId LaneId { get; }

    public int Health { get; }

    public int PathIndex { get; }

    public int MovementProgress { get; }

    public bool HasLeaked { get; }

    public bool IsDead => Health <= 0;

    public CreepCombatState WithHealth(int health) =>
        new CreepCombatState(EntityId, CreepId, SenderId, LaneId, health, PathIndex, MovementProgress, HasLeaked);

    public CreepCombatState WithMovement(int pathIndex, int movementProgress) =>
        new CreepCombatState(EntityId, CreepId, SenderId, LaneId, Health, pathIndex, movementProgress, HasLeaked);

    public CreepCombatState MarkLeaked() =>
        new CreepCombatState(EntityId, CreepId, SenderId, LaneId, Health, PathIndex, MovementProgress, hasLeaked: true);
}
