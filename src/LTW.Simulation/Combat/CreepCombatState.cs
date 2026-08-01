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
        bool hasLeaked,
        int maxHealth = 0,
        bool ignoresMaze = false)
    {
        EntityId = entityId;
        CreepId = creepId;
        SenderId = senderId;
        LaneId = laneId;
        Health = health;
        PathIndex = pathIndex;
        MovementProgress = movementProgress;
        HasLeaked = hasLeaked;
        // Defaults to the health it was created with, so a caller that does not care about tiers
        // gets a full bar rather than a divide-by-zero or an empty one.
        MaxHealth = maxHealth > 0 ? maxHealth : health;
        IgnoresMaze = ignoresMaze;
    }

    public EntityId EntityId { get; }

    public ContentId CreepId { get; }

    public PlayerId SenderId { get; }

    public LaneId LaneId { get; }

    public int Health { get; }

    /// <summary>
    /// The health this creep SPAWNED with, including its sender's send-category tier.
    /// </summary>
    /// <remarks>
    /// Carried per-creep rather than read back from CreepDefinition, because with tiers the
    /// authored MaxHealth is no longer what any particular creep started at — a tier-3 creep
    /// spawns at 225% of it. The presentation snapshot reports this, so a health bar is a
    /// fraction of what that creep actually had rather than of what its type is worth. Reading
    /// the definition instead renders a tier-3 creep as a 225%-full bar.
    ///
    /// Every wither carries it through, and it never changes after spawn: damage lowers Health,
    /// and a lane transfer keeps both, so the bar stays honest across the whole life of a creep.
    /// </remarks>
    public int MaxHealth { get; }

    /// <summary>Whether this creep walks the direct route rather than the mazed one.</summary>
    /// <remarks>
    /// Copied from the definition at spawn rather than read back from content on demand, and the
    /// reason is the hot path: ResolvePosition has to pick a route, and it runs once per creep per
    /// tower per tick. At the peaks this build actually reaches — hundreds of towers against
    /// hundreds of creeps — that is a content dictionary lookup in the innermost loop of the tick,
    /// to answer a question whose answer never changes for the life of the creep.
    ///
    /// Unlike MaxHealth this is not per-creep data, it is per-TYPE data held per creep. That
    /// duplication is deliberate and safe only because it is immutable: nothing may write it after
    /// spawn, and every wither below passes it straight through.
    /// </remarks>
    public bool IgnoresMaze { get; }

    public int PathIndex { get; }

    public int MovementProgress { get; }

    public bool HasLeaked { get; }

    public bool IsDead => Health <= 0;

    public CreepCombatState WithHealth(int health) =>
        new CreepCombatState(EntityId, CreepId, SenderId, LaneId, health, PathIndex, MovementProgress, HasLeaked, MaxHealth, IgnoresMaze);

    public CreepCombatState WithMovement(int pathIndex, int movementProgress) =>
        new CreepCombatState(EntityId, CreepId, SenderId, LaneId, Health, pathIndex, movementProgress, HasLeaked, MaxHealth, IgnoresMaze);

    public CreepCombatState MarkLeaked() =>
        new CreepCombatState(EntityId, CreepId, SenderId, LaneId, Health, PathIndex, MovementProgress, hasLeaked: true, MaxHealth, IgnoresMaze);
}
