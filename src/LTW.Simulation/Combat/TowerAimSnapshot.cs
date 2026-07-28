using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class TowerAimSnapshot
{
    public TowerAimSnapshot(EntityId towerEntityId, GridPosition targetPosition)
    {
        TowerEntityId = towerEntityId;
        TargetPosition = targetPosition;
    }

    public EntityId TowerEntityId { get; }

    public GridPosition TargetPosition { get; }
}
