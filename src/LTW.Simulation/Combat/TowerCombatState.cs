using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class TowerCombatState
{
    public TowerCombatState(EntityId entityId, ContentId towerId, PlayerId ownerId, LaneId laneId, GridPosition position)
        : this(entityId, towerId, ownerId, laneId, position, new SimulationTick(0), null, null)
    {
    }

    private TowerCombatState(
        EntityId entityId,
        ContentId towerId,
        PlayerId ownerId,
        LaneId laneId,
        GridPosition position,
        SimulationTick nextAttackTick,
        SimulationTick? shellImpactTick,
        GridPosition? shellImpactCell)
    {
        EntityId = entityId;
        TowerId = towerId;
        OwnerId = ownerId;
        LaneId = laneId;
        Position = position;
        NextAttackTick = nextAttackTick;
        ShellImpactTick = shellImpactTick;
        ShellImpactCell = shellImpactCell;
    }

    public EntityId EntityId { get; }

    public ContentId TowerId { get; }

    public PlayerId OwnerId { get; }

    public LaneId LaneId { get; }

    public GridPosition Position { get; }

    public SimulationTick NextAttackTick { get; }

    /// <summary>
    /// Tick a Foundry Core's in-flight shell lands, or null when nothing is in the air.
    /// </summary>
    /// <remarks>
    /// Null for every tower except a Foundry Core mid-flight. This lives on the tower rather than in
    /// a third CombatState list deliberately: ReplaceCreep/ReplaceTower/RemoveCreep all rebuild
    /// CombatState from Creeps + Towers across 20 construction sites, so a defaulted third parameter
    /// would compile while silently dropping every shell in flight on every damage application.
    /// </remarks>
    public SimulationTick? ShellImpactTick { get; }

    /// <summary>
    /// Cell a Foundry Core's shell will land on. A cell, not an entity — artillery is aimed at
    /// ground, so the shell can miss if the creep it led died or walked on.
    /// </summary>
    public GridPosition? ShellImpactCell { get; }

    public bool HasShellInFlight => ShellImpactTick is not null;

    /// <summary>
    /// Copies the shell fields through.
    /// </summary>
    /// <remarks>
    /// Forgetting that copy is the single most likely bug in the mortar: arming a shell and setting
    /// the cooldown in the same tick would disarm the shell instantly, the Foundry would never
    /// damage anything, and it would compile.
    /// </remarks>
    public TowerCombatState WithNextAttackTick(SimulationTick nextAttackTick) =>
        new TowerCombatState(EntityId, TowerId, OwnerId, LaneId, Position, nextAttackTick, ShellImpactTick, ShellImpactCell);

    public TowerCombatState WithShellInFlight(SimulationTick impactTick, GridPosition impactCell) =>
        new TowerCombatState(EntityId, TowerId, OwnerId, LaneId, Position, NextAttackTick, impactTick, impactCell);

    public TowerCombatState WithoutShellInFlight() =>
        new TowerCombatState(EntityId, TowerId, OwnerId, LaneId, Position, NextAttackTick, null, null);
}
