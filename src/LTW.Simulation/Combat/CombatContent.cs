using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CombatContent
{
    private readonly IReadOnlyDictionary<ContentId, CreepDefinition> creeps;
    private readonly IReadOnlyDictionary<ContentId, TowerDefinition> towers;
    private readonly IReadOnlyDictionary<LaneId, PlayerId> laneOwners;

    public CombatContent(
        IEnumerable<CreepDefinition> creeps,
        IEnumerable<TowerDefinition> towers,
        IReadOnlyDictionary<LaneId, PlayerId> laneOwners)
    {
        this.creeps = (creeps ?? throw new ArgumentNullException(nameof(creeps))).ToDictionary(creep => creep.Id);
        this.towers = (towers ?? throw new ArgumentNullException(nameof(towers))).ToDictionary(tower => tower.Id);
        this.laneOwners = new Dictionary<LaneId, PlayerId>(laneOwners ?? throw new ArgumentNullException(nameof(laneOwners)));
    }

    public CreepDefinition GetCreep(ContentId creepId) => creeps[creepId];

    public TowerDefinition GetTower(ContentId towerId) => towers[towerId];

    public PlayerId GetLaneOwner(LaneId laneId) => laneOwners[laneId];
}
