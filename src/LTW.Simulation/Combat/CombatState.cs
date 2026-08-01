using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CombatState
{
    private readonly CreepCombatState[] creeps;
    private readonly TowerCombatState[] towers;

    public CombatState(IEnumerable<CreepCombatState> creeps, IEnumerable<TowerCombatState> towers)
    {
        this.creeps = (creeps ?? throw new ArgumentNullException(nameof(creeps))).ToArray();
        this.towers = (towers ?? throw new ArgumentNullException(nameof(towers))).ToArray();
    }

    /// <summary>
    /// Adopts arrays this class already owns, rather than copying them again.
    /// </summary>
    /// <remarks>
    /// Every mutation below produces exactly one fresh array and passes the other side straight
    /// through untouched. The public constructor cannot do that — it takes IEnumerable and has to
    /// copy defensively — so routing internal mutations through it copied the new array a SECOND
    /// time and copied the collection that had not changed at all. At the 266 creeps this game has
    /// been measured carrying, that second pair of copies was most of the cost of a tick.
    ///
    /// Private, and only ever handed arrays constructed here, so the immutability the public API
    /// promises is unchanged.
    /// </remarks>
    private CombatState(CreepCombatState[] creeps, TowerCombatState[] towers)
    {
        this.creeps = creeps;
        this.towers = towers;
    }

    public IReadOnlyList<CreepCombatState> Creeps => creeps;

    public IReadOnlyList<TowerCombatState> Towers => towers;

    public CombatState ReplaceCreep(CreepCombatState creep) =>
        new CombatState(Replace(creeps, creep, existing => existing.EntityId.Equals(creep.EntityId)), towers);

    public CombatState ReplaceTower(TowerCombatState tower) =>
        new CombatState(creeps, Replace(towers, tower, existing => existing.EntityId.Equals(tower.EntityId)));

    public CombatState RemoveCreep(EntityId creepEntityId) =>
        new CombatState(creeps.Where(creep => !creep.EntityId.Equals(creepEntityId)).ToArray(), towers);

    public CombatState RemoveTower(EntityId towerEntityId) =>
        new CombatState(creeps, towers.Where(tower => !tower.EntityId.Equals(towerEntityId)).ToArray());

    /// <summary>
    /// Clears an eliminated seat's lane: every tower it built, and every creep still walking it.
    /// </summary>
    /// <remarks>
    /// Creeps are matched by LANE and towers by OWNER, which are deliberately different keys. A
    /// tower only ever stands in its owner's own lane, so either key finds the same set. A creep in
    /// this lane belongs to whoever SENT it, which is somebody else — filtering creeps by owner
    /// would clear the dead player's creeps out of everyone else's lanes and leave the attackers
    /// standing in theirs, which is precisely backwards.
    /// </remarks>
    public CombatState WipeLane(LaneId laneId, PlayerId ownerId) =>
        new CombatState(
            creeps.Where(creep => !creep.LaneId.Equals(laneId)).ToArray(),
            towers.Where(tower => !tower.OwnerId.Equals(ownerId)).ToArray());

    /// <summary>
    /// A copy of <paramref name="values"/> with one entry swapped, ready to be adopted as-is.
    /// </summary>
    private static T[] Replace<T>(T[] values, T replacement, Func<T, bool> predicate)
    {
        var next = values.ToArray();
        for (var index = 0; index < next.Length; index++)
        {
            if (predicate(next[index]))
            {
                next[index] = replacement;
                return next;
            }
        }

        throw new KeyNotFoundException("State item was not found.");
    }
}
