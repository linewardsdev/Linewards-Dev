using System;
using System.Collections.Generic;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

/// <summary>
/// The damage phases' mutable working copy of a <see cref="CombatState"/>: one creep list and one
/// tower list, edited in place, materialised back into an immutable state exactly once at the end.
/// </summary>
/// <remarks>
/// This exists because <see cref="CombatState.ReplaceCreep"/> rebuilds the whole creep array, and
/// the attack phase called it once per DAMAGING HIT — plus <see cref="CombatState.RemoveCreep"/>
/// again on a kill, and <see cref="CombatState.ReplaceTower"/> once per firing tower. A tick
/// therefore cost O(hits x creeps), which on a board where every tower has something in range is
/// quadratic in board size; the capture harness has measured 266 concurrent creeps. Measured on a
/// saturated 266-creep, 120-tower board: 153 damaging hits per tick, so 40,600 creep-element copies
/// and 14,400 tower-element copies every tick, all of them to change one entry.
///
/// It is a buffer rather than a smarter <see cref="CombatState"/> because the phase genuinely is
/// mutable — a tower shoots, the target's health drops, the next tower reads it — and the previous
/// code expressed that by threading a fresh immutable state through every call. Threading a mutable
/// one instead keeps the exact same data flow and pays for one array pair per tick rather than per
/// hit. The immutability the rest of the simulation relies on is unchanged: nothing outside
/// <see cref="CombatService"/> ever sees this type, and the state it hands back is frozen.
///
/// ORDER IS LOAD-BEARING and this class is built around preserving it exactly. Entity order feeds
/// SelectTarget, Pulse's splash Take(2) and ChainArc, all of which tie-break on entity id, and
/// event order is asserted on by several tests. So:
///
///   - Replace writes back into the SAME slot, exactly as CombatState.Replace did.
///   - Remove compacts the list, keeping every survivor's position relative to the others — the
///     same order <c>Where(...).ToArray()</c> produced.
///   - Nothing is ever appended, so no creep can change position relative to another.
///
/// Failure modes are preserved too: a removed creep leaves the id index, so a second
/// <see cref="ReplaceCreep"/> against it still throws <see cref="KeyNotFoundException"/> rather
/// than quietly damaging a corpse twice.
/// </remarks>
internal sealed class CombatDamageBuffer
{
    private readonly CombatState source;
    private readonly List<CreepCombatState> creeps;
    private readonly Dictionary<EntityId, int> creepIndexById;
    private readonly List<TowerCombatState> towers;
    private readonly Dictionary<EntityId, int> towerIndexById;

    /// <summary>
    /// Creeps removed since the last compaction. They are still sitting in <see cref="creeps"/>
    /// until someone reads it.
    /// </summary>
    /// <remarks>
    /// Deferred because compacting eagerly is the O(N) shuffle this class exists to avoid, and
    /// removals arrive in runs — a shell that lands on a stack, a chain that finishes two creeps —
    /// which a deferred compaction collapses into one pass. It cannot be deferred past a read: a
    /// caller asking for the creeps must see the same set <see cref="CombatState.RemoveCreep"/>
    /// would have left, so the compaction happens there.
    /// </remarks>
    private readonly HashSet<EntityId> pendingRemovals = new HashSet<EntityId>();

    private readonly Predicate<CreepCombatState> isPendingRemoval;

    private bool changed;

    internal CombatDamageBuffer(CombatState state)
    {
        source = state;
        isPendingRemoval = creep => pendingRemovals.Contains(creep.EntityId);

        var stateCreeps = state.Creeps;
        creeps = new List<CreepCombatState>(stateCreeps.Count);
        creepIndexById = new Dictionary<EntityId, int>(stateCreeps.Count);
        for (var index = 0; index < stateCreeps.Count; index++)
        {
            var creep = stateCreeps[index];
            creeps.Add(creep);
            // First slot wins, matching CombatState.Replace, which stopped at the first match.
            // Entity ids are unique by construction, so this is a statement of intent rather than a
            // case that can arise.
            if (!creepIndexById.ContainsKey(creep.EntityId))
            {
                creepIndexById.Add(creep.EntityId, index);
            }
        }

        var stateTowers = state.Towers;
        towers = new List<TowerCombatState>(stateTowers.Count);
        towerIndexById = new Dictionary<EntityId, int>(stateTowers.Count);
        for (var index = 0; index < stateTowers.Count; index++)
        {
            var tower = stateTowers[index];
            towers.Add(tower);
            if (!towerIndexById.ContainsKey(tower.EntityId))
            {
                towerIndexById.Add(tower.EntityId, index);
            }
        }
    }

    /// <summary>
    /// Every creep still in the buffer, in state order.
    /// </summary>
    /// <remarks>
    /// Deliberately a <see cref="List{T}"/> behind an <see cref="IReadOnlyList{T}"/>, not an
    /// iterator. Every caller runs a LINQ filter over this, and LINQ picks a struct-enumerator fast
    /// path for a list or an array and a fully interface-dispatched one for anything else. An
    /// earlier version of this class exposed a hole-skipping <c>yield return</c> instead, so that
    /// removals never had to compact; through those filters — 120 towers each scanning every creep —
    /// the extra dispatch cost more than the array rebuilds it had just removed, measuring 25%
    /// SLOWER per tick at 1,064 creeps while allocating 2.5x less. Keeping the concrete list is what
    /// makes the win show up in time as well as in bytes.
    /// </remarks>
    internal IReadOnlyList<CreepCombatState> Creeps
    {
        get
        {
            if (pendingRemovals.Count > 0)
            {
                Compact();
            }

            return creeps;
        }
    }

    /// <summary>
    /// Towers in state order. Never compacted, because towers are only ever replaced in these
    /// phases, never removed.
    /// </summary>
    internal IReadOnlyList<TowerCombatState> Towers => towers;

    /// <summary>Swaps one creep for its updated self, in place. Throws if it is not here, as CombatState did.</summary>
    /// <remarks>
    /// Correct while a removal is pending, and that is worth stating: a pending removal has not
    /// shifted anything yet, so every surviving creep's recorded index is still the right one.
    /// </remarks>
    internal void ReplaceCreep(CreepCombatState creep)
    {
        if (!creepIndexById.TryGetValue(creep.EntityId, out var index))
        {
            throw new KeyNotFoundException("State item was not found.");
        }

        creeps[index] = creep;
        changed = true;
    }

    /// <summary>
    /// Drops a creep out of the buffer, keeping every survivor's position relative to the others.
    /// </summary>
    /// <remarks>
    /// Tolerates a creep that is not here, because <see cref="CombatState.RemoveCreep"/> was a
    /// filter and a filter that matches nothing is not an error.
    /// </remarks>
    internal void RemoveCreep(EntityId creepEntityId)
    {
        if (!creepIndexById.Remove(creepEntityId))
        {
            return;
        }

        pendingRemovals.Add(creepEntityId);
        changed = true;
    }

    /// <summary>Swaps one tower for its updated self, in place. Throws if it is not here, as CombatState did.</summary>
    internal void ReplaceTower(TowerCombatState tower)
    {
        if (!towerIndexById.TryGetValue(tower.EntityId, out var index))
        {
            throw new KeyNotFoundException("State item was not found.");
        }

        towers[index] = tower;
        changed = true;
    }

    /// <summary>
    /// Freezes the buffer back into an immutable state — the phase's one and only array rebuild.
    /// </summary>
    /// <remarks>
    /// Hands back the state it was built from when nothing happened, so a quiet tick — no tower in
    /// range of anything — costs no allocation at all rather than two pointless array copies.
    /// </remarks>
    internal CombatState ToState()
    {
        if (!changed)
        {
            return source;
        }

        if (pendingRemovals.Count > 0)
        {
            Compact();
        }

        return CombatState.Adopt(creeps.ToArray(), towers.ToArray());
    }

    /// <summary>
    /// Closes the gaps left by removals in one in-place pass and re-points the id index at the
    /// result. Order is preserved: RemoveAll keeps the survivors in the order it found them.
    /// </summary>
    private void Compact()
    {
        creeps.RemoveAll(isPendingRemoval);
        pendingRemovals.Clear();

        // Cleared rather than replaced, so the dictionary keeps the capacity it was sized to and
        // this costs no allocation.
        creepIndexById.Clear();
        for (var index = 0; index < creeps.Count; index++)
        {
            creepIndexById[creeps[index].EntityId] = index;
        }
    }
}
