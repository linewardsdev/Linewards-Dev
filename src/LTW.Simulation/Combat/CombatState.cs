using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CombatState
{
    public CombatState(IEnumerable<CreepCombatState> creeps, IEnumerable<TowerCombatState> towers)
    {
        Creeps = (creeps ?? throw new ArgumentNullException(nameof(creeps))).ToArray();
        Towers = (towers ?? throw new ArgumentNullException(nameof(towers))).ToArray();
    }

    public IReadOnlyList<CreepCombatState> Creeps { get; }

    public IReadOnlyList<TowerCombatState> Towers { get; }

    public CombatState ReplaceCreep(CreepCombatState creep) =>
        new CombatState(Replace(Creeps, creep, existing => existing.EntityId.Equals(creep.EntityId)), Towers);

    public CombatState ReplaceTower(TowerCombatState tower) =>
        new CombatState(Creeps, Replace(Towers, tower, existing => existing.EntityId.Equals(tower.EntityId)));

    public CombatState RemoveCreep(EntityId creepEntityId) =>
        new CombatState(Creeps.Where(creep => !creep.EntityId.Equals(creepEntityId)), Towers);

    public CombatState RemoveTower(EntityId towerEntityId) =>
        new CombatState(Creeps, Towers.Where(tower => !tower.EntityId.Equals(towerEntityId)));

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
            Creeps.Where(creep => !creep.LaneId.Equals(laneId)),
            Towers.Where(tower => !tower.OwnerId.Equals(ownerId)));

    private static IReadOnlyList<T> Replace<T>(IReadOnlyList<T> values, T replacement, Func<T, bool> predicate)
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
