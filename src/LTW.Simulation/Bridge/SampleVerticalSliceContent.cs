using System;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

public static class SampleVerticalSliceContent
{
    public static readonly ContentId TowerId = new("tower.arrow");

    public static readonly ContentId ControlTowerId = new("tower.slow-control");

    public static readonly ContentId UtilityTowerId = new("tower.economy-relay");

    public static readonly ContentId CreepId = new("creep.runner");

    public static readonly ContentId BruteCreepId = new("creep.brute");

    public static readonly ContentId SwarmCreepId = new("creep.swarm");

    public static ContentCatalog Create()
    {
        return new ContentCatalog(
            "mvp-06-sample",
            new[]
            {
                new TowerDefinition(TowerId, "Arrow Tower", new Gold(25), rangeCells: 2, damage: 5, attackCooldownTicks: 2),
                new TowerDefinition(ControlTowerId, "Slow Control Ward", new Gold(35), rangeCells: 2, damage: 3, attackCooldownTicks: 3),
                new TowerDefinition(UtilityTowerId, "Economy Relay Ward", new Gold(40), rangeCells: 1, damage: 1, attackCooldownTicks: 5)
            },
            new[]
            {
                new CreepDefinition(CreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 10, speedPerSecond: 1),
                new CreepDefinition(BruteCreepId, "Brute", new Gold(18), new Income(2), new Gold(2), new Gold(3), maxHealth: 24, speedPerSecond: 1),
                new CreepDefinition(SwarmCreepId, "Swarm", new Gold(6), new Income(1), new Gold(1), new Gold(1), maxHealth: 5, speedPerSecond: 2)
            },
            Array.Empty<TechDefinition>(),
            new[] { new MapDefinition(new ContentId("map.vertical-slice"), "Vertical Slice", width: 7, height: 18, new GridPosition(3, 0), new GridPosition(3, 17), Array.Empty<GridPosition>()) },
            Array.Empty<BotProfileDefinition>());
    }
}
