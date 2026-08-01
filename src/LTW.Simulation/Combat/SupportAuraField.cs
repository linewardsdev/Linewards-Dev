using System.Collections.Generic;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

/// <summary>
/// Which creeps and towers are currently inside a support creep's aura, resolved once per tick.
/// </summary>
/// <remarks>
/// Built once and handed to the tick's steps rather than recomputed where it is read, because the
/// places that read it are the hot ones — every damage application asks whether its victim is
/// shielded, and damage is applied several times per tower per tick.
///
/// The scan is deliberately shaped around the fact that SUPPORTS ARE RARE and beneficiaries are
/// not. A naive "for each creep, look at every other creep" is O(n squared), and this build peaks
/// near 750 concurrent creeps, which is half a million comparisons per tick on a phone. Collecting
/// the handful of supports first and testing everything against only those makes it O(n x supports),
/// which at a realistic dozen supports is a few thousand.
///
/// Proximity is measured in ROUTE INDICES, not grid cells. Creeps live on a one-dimensional path,
/// so "within three cells along the lane" is one integer subtraction, and it is also what a player
/// reads off the screen — the creeps around it in the line.
///
/// Auras deliberately DO NOT STACK. Two Menders heal the same creep once, not twice. Grovebond caps
/// its equivalent at three neighbours and is still the most fiddly number on the tower roster; for
/// a first pass at support creeps a flat on/off is bounded by construction, cannot compound, and is
/// explicable in one sentence. Whether stacking is worth the risk is a question for after this has
/// been played.
/// </remarks>
public sealed class SupportAuraField
{
    /// <summary>How far along the route a support reaches, in cells either side.</summary>
    public const int PathRadius = 3;

    /// <summary>Movement cost removed from a paced creep, so 3 becomes 2 — half again as fast.</summary>
    public const int PacesetterMovementBonus = 1;

    /// <summary>Ticks between a Mender's heals.</summary>
    /// <remarks>
    /// Every 5 ticks rather than every tick, and the arithmetic is why. A lane takes roughly 48
    /// ticks to walk, so healing per tick would restore about 48 health over one lane — more than
    /// the maximum health of every creep on the roster bar two. At this interval it is about 9,
    /// which matters against chip damage and does nothing against burst, and that is the trade the
    /// unit is supposed to offer.
    /// </remarks>
    public const int MenderHealIntervalTicks = 5;

    /// <summary>Health restored to each creep in range, per heal.</summary>
    public const int MenderHealAmount = 1;

    /// <summary>Percent of incoming damage a Bulwark's shield removes.</summary>
    public const int BulwarkDamageReductionPercent = 25;

    /// <summary>Extra ticks added to a bound tower's cooldown between shots.</summary>
    /// <remarks>
    /// One tick, against authored cooldowns of 2 to 6, so a Binder costs a fast tower a third of
    /// its rate and a slow one a sixth. It is expressed as a flat tick rather than a percentage so
    /// it hurts the cheap rapid towers most, which is the intent — the counter to a Binder should
    /// be heavy single shots, not more of the same.
    /// </remarks>
    public const int BinderCooldownExtraTicks = 1;

    /// <summary>Cells from the Binder, in Manhattan distance, that towers are slowed within.</summary>
    /// <remarks>
    /// Grid distance here, not route distance, because the thing being affected is a TOWER, and a
    /// tower sits on the grid rather than on the path. Manhattan matches how tower range itself is
    /// measured (<c>IsInRange</c>), so a Binder's reach reads the same way as everything else.
    /// </remarks>
    public const int BinderTowerRangeCells = 2;

    private readonly HashSet<EntityId> paced = new();
    private readonly HashSet<EntityId> shielded = new();
    private readonly HashSet<EntityId> boundTowers = new();

    public bool IsPaced(EntityId creep) => paced.Contains(creep);

    public bool IsShielded(EntityId creep) => shielded.Contains(creep);

    public bool IsBound(EntityId tower) => boundTowers.Contains(tower);

    /// <summary>Creeps a Mender reached this tick, empty on ticks that are not heal ticks.</summary>
    public List<EntityId> Mended { get; } = new();

    /// <summary>
    /// Resolves every aura for this tick.
    /// </summary>
    /// <remarks>
    /// A support never buffs itself, and only ever buffs creeps from the SAME SENDER. Both matter:
    /// a lane can hold creeps from two players at once — the seat attacking it directly, plus
    /// anything that leaked in from the previous lane — and shielding the creeps being sent AT the
    /// support's owner would be exactly backwards.
    ///
    /// Auras also do not cross between the mazed and direct routes. A path index means a different
    /// place on each, so the comparison would be meaningless, and it is right anyway: the flyer is
    /// over the towers, not walking beside the pack. That isolation is the point of the unit.
    /// </remarks>
    public static SupportAuraField Resolve(CombatState state, CombatContent content, SimulationTick tick)
    {
        var field = new SupportAuraField();
        var supports = new List<(CreepCombatState Creep, CreepDefinition Definition)>();

        foreach (var creep in state.Creeps)
        {
            if (creep.IsDead || creep.HasLeaked)
            {
                continue;
            }

            var definition = content.GetCreep(creep.CreepId);
            if (definition.Support != CreepSupportRole.None)
            {
                supports.Add((creep, definition));
            }
        }

        if (supports.Count == 0)
        {
            return field;
        }

        var isHealTick = tick.Value % MenderHealIntervalTicks == 0;

        foreach (var creep in state.Creeps)
        {
            if (creep.IsDead || creep.HasLeaked)
            {
                continue;
            }

            foreach (var (support, definition) in supports)
            {
                if (support.EntityId.Equals(creep.EntityId)
                    || !support.LaneId.Equals(creep.LaneId)
                    || !support.SenderId.Equals(creep.SenderId)
                    || support.IgnoresMaze != creep.IgnoresMaze)
                {
                    continue;
                }

                var distance = support.PathIndex - creep.PathIndex;
                if (distance < 0)
                {
                    distance = -distance;
                }

                if (distance > PathRadius)
                {
                    continue;
                }

                switch (definition.Support)
                {
                    case CreepSupportRole.Pacesetter:
                        field.paced.Add(creep.EntityId);
                        break;
                    case CreepSupportRole.Bulwark:
                        field.shielded.Add(creep.EntityId);
                        break;
                    case CreepSupportRole.Mender when isHealTick && creep.Health < creep.MaxHealth:
                        field.Mended.Add(creep.EntityId);
                        break;
                }
            }
        }

        // Binders are handled by BindTowersInRange, which needs route positions this method has no
        // access to. Nothing is added here on purpose: an unbound field means no tower is slowed,
        // which is the correct reading for a caller that never resolves positions.
        return field;
    }

    /// <summary>
    /// Marks the towers a Binder is currently close enough to slow.
    /// </summary>
    /// <remarks>
    /// Split from <see cref="Resolve"/> because a Binder's reach is measured from where it is
    /// STANDING, and that needs the route to turn a path index into a cell — which Resolve does not
    /// have. CombatService calls this straight after resolving, once it can supply positions.
    /// </remarks>
    public void BindTowersInRange(CombatState state, CombatContent content, System.Func<CreepCombatState, GridPosition> positionOf)
    {
        boundTowers.Clear();

        foreach (var creep in state.Creeps)
        {
            if (creep.IsDead || creep.HasLeaked || content.GetCreep(creep.CreepId).Support != CreepSupportRole.Binder)
            {
                continue;
            }

            var from = positionOf(creep);
            foreach (var tower in state.Towers)
            {
                if (!tower.LaneId.Equals(creep.LaneId))
                {
                    continue;
                }

                var distance = System.Math.Abs(tower.Position.X - from.X) + System.Math.Abs(tower.Position.Y - from.Y);
                if (distance <= BinderTowerRangeCells)
                {
                    boundTowers.Add(tower.EntityId);
                }
            }
        }
    }
}
