using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class TowerCombatState
{
    public TowerCombatState(EntityId entityId, ContentId towerId, PlayerId ownerId, LaneId laneId, GridPosition position, int tier = PlayerEconomyState.BaseTier)
        : this(entityId, towerId, ownerId, laneId, position, new SimulationTick(0), null, null, tier)
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
        GridPosition? shellImpactCell,
        int tier)
    {
        EntityId = entityId;
        TowerId = towerId;
        OwnerId = ownerId;
        LaneId = laneId;
        Position = position;
        NextAttackTick = nextAttackTick;
        ShellImpactTick = shellImpactTick;
        ShellImpactCell = shellImpactCell;
        Tier = tier;
    }

    public EntityId EntityId { get; }

    public ContentId TowerId { get; }

    public PlayerId OwnerId { get; }

    public LaneId LaneId { get; }

    public GridPosition Position { get; }

    public SimulationTick NextAttackTick { get; }

    /// <summary>
    /// THIS tower's tier, fixed when it was built and raised only by paying to upgrade it.
    /// </summary>
    /// <remarks>
    /// Per-tower rather than read from the owner's category tier, and that is the whole design.
    /// Buying a line tier raises the tier NEW towers are built at; it does nothing for the ones
    /// already standing. Bringing an existing tower up costs gold, one tower at a time, which is
    /// what makes "upgrade the twelve Arrows I already have, or spend the same gold on new ones"
    /// a real decision rather than an automatic win.
    ///
    /// An earlier version applied the line tier at shot time, so every placed tower improved for
    /// free the moment the line was bought. That is the opposite of the intended model and left the
    /// category purchase doing all the work by itself.
    /// </remarks>
    public int Tier { get; }

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
    /// Copies the shell fields AND the tier through.
    /// </summary>
    /// <remarks>
    /// Forgetting that copy is the single most likely bug in the mortar: arming a shell and setting
    /// the cooldown in the same tick would disarm the shell instantly, the Foundry would never
    /// damage anything, and it would compile. The tier rides along for the same reason — a wither
    /// that dropped it would silently reset a tower the player paid to upgrade back to tier 1 the
    /// next time it fired.
    /// </remarks>
    public TowerCombatState WithNextAttackTick(SimulationTick nextAttackTick) =>
        new TowerCombatState(EntityId, TowerId, OwnerId, LaneId, Position, nextAttackTick, ShellImpactTick, ShellImpactCell, Tier);

    public TowerCombatState WithShellInFlight(SimulationTick impactTick, GridPosition impactCell) =>
        new TowerCombatState(EntityId, TowerId, OwnerId, LaneId, Position, NextAttackTick, impactTick, impactCell, Tier);

    public TowerCombatState WithoutShellInFlight() =>
        new TowerCombatState(EntityId, TowerId, OwnerId, LaneId, Position, NextAttackTick, null, null, Tier);

    public TowerCombatState WithTier(int tier) =>
        new TowerCombatState(EntityId, TowerId, OwnerId, LaneId, Position, NextAttackTick, ShellImpactTick, ShellImpactCell, tier);
}
