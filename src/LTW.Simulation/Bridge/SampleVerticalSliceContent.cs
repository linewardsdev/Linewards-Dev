using System;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

public static class SampleVerticalSliceContent
{
    public static readonly ContentId TowerId = new("tower.arrow");

    public static readonly ContentId CreepId = new("creep.runner");

    public static ContentCatalog Create()
    {
        return new ContentCatalog(
            "mvp-06-sample",
            new[] { new TowerDefinition(TowerId, "Arrow Tower", new Gold(25), rangeCells: 2, damage: 5, attackCooldownTicks: 2) },
            new[] { new CreepDefinition(CreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 10, speedPerSecond: 1) },
            Array.Empty<TechDefinition>(),
            new[] { new MapDefinition(new ContentId("map.vertical-slice"), "Vertical Slice", width: 12, height: 9, new GridPosition(0, 4), new GridPosition(11, 4), Array.Empty<GridPosition>()) },
            Array.Empty<BotProfileDefinition>());
    }
}
