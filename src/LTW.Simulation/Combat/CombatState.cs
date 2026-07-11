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
