using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class TowerCombatState
{
    public TowerCombatState(EntityId entityId, ContentId towerId, PlayerId ownerId, LaneId laneId, GridPosition position)
        : this(entityId, towerId, ownerId, laneId, position, new SimulationTick(0))
    {
    }

    private TowerCombatState(
        EntityId entityId,
        ContentId towerId,
        PlayerId ownerId,
        LaneId laneId,
        GridPosition position,
        SimulationTick nextAttackTick)
    {
        EntityId = entityId;
        TowerId = towerId;
        OwnerId = ownerId;
        LaneId = laneId;
        Position = position;
        NextAttackTick = nextAttackTick;
    }

    public EntityId EntityId { get; }

    public ContentId TowerId { get; }

    public PlayerId OwnerId { get; }

    public LaneId LaneId { get; }

    public GridPosition Position { get; }

    public SimulationTick NextAttackTick { get; }

    public TowerCombatState WithNextAttackTick(SimulationTick nextAttackTick) =>
        new TowerCombatState(EntityId, TowerId, OwnerId, LaneId, Position, nextAttackTick);
}
