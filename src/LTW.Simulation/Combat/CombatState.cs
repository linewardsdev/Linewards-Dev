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
    /// Private, and only ever handed arrays constructed here or by <see cref="Adopt"/>, so the
    /// immutability the public API promises is unchanged.
    /// </remarks>
    private CombatState(CreepCombatState[] creeps, TowerCombatState[] towers)
    {
        this.creeps = creeps;
        this.towers = towers;
    }

    /// <summary>
    /// Builds a state from arrays the caller has just constructed and will not touch again.
    /// </summary>
    /// <remarks>
    /// The one door onto the adopting constructor above, opened for <see cref="CombatDamageBuffer"/>
    /// — which spends a whole combat phase editing its own arrays in place and then has to hand them
    /// over exactly once. Routing that through the public constructor would copy both arrays a second
    /// time for no reason, which is the cost this whole seam exists to avoid.
    ///
    /// Internal, and the caller must own the arrays outright: whatever is passed here becomes this
    /// state's backing store, so a caller that keeps writing to it would be mutating a value the rest
    /// of the simulation treats as frozen.
    /// </remarks>
    internal static CombatState Adopt(CreepCombatState[] creeps, TowerCombatState[] towers) =>
        new CombatState(creeps, towers);

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
    /// Clears an eliminated seat's lane: every tower it built, every creep still walking that lane,
    /// and every creep it SENT that is still walking somebody else's.
    /// </summary>
    /// <remarks>
    /// Two different reasons a creep leaves the board here, matched by two different keys. A creep
    /// in this LANE belongs to whoever sent it — filtering those by owner would clear the dead
    /// player's own creeps out of everyone else's lanes and leave the attackers standing in theirs,
    /// which is precisely backwards, so lane-owned creeps are matched by lane. A creep this player
    /// SENT is matched by sender instead, wherever it currently is, for the opposite reason: it
    /// belongs to nobody's lane but its own history.
    ///
    /// Originally left the sent-elsewhere creeps standing ("they were paid for and are somebody
    /// else's problem now" — see this method's prior remarks), on the reasoning that a paid-for
    /// unit shouldn't evaporate just because its owner lost. Reported from play 2026-08-29: those
    /// creeps do not just stand there, they keep MARCHING, and a leak from a sender who no longer
    /// exists cannot be credited to anyone (see EconomyService.ApplyLeak's IsEliminated branch) —
    /// so every one of them that reaches a gate destroys a life outright, out of a mechanic whose
    /// entire design is that a life is STOLEN, never destroyed. A ghost sender's creep breaks the
    /// conservation invariant the whole steal mechanic depends on, is un-recoverable by any player,
    /// and keeps counting toward the peak-concurrent-creep number this project fought hard to
    /// control. All three are worse than the "free continued pressure" this design was trying to
    /// avoid rewarding — so now they go with the rest of the seat.
    /// </remarks>
    public CombatState WipeLane(LaneId laneId, PlayerId ownerId) =>
        new CombatState(
            creeps.Where(creep => !creep.LaneId.Equals(laneId) && !creep.SenderId.Equals(ownerId)).ToArray(),
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
