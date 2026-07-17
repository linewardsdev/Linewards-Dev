using System;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

public static class SampleVerticalSliceContent
{
    public static readonly ContentId TowerId = new("tower.arrow");

    public static readonly ContentId ControlTowerId = new("tower.control");

    public static readonly ContentId UtilityTowerId = new("tower.relay");

    public static readonly ContentId PulseTowerId = new("tower.pulse");

    public static readonly ContentId PrismTowerId = new("tower.prism");

    public static readonly ContentId CreepId = new("creep.runner");

    public static readonly ContentId BruteCreepId = new("creep.brute");

    public static readonly ContentId SwarmCreepId = new("creep.swarm");

    public static readonly ContentId ShadeCreepId = new("creep.shade");

    public static readonly ContentId SiegeCreepId = new("creep.siege");

    public static ContentCatalog Create()
    {
        return new ContentCatalog(
            "mvp-07-5x2-roster",
            new[]
            {
                new TowerDefinition(TowerId, "Arrow Tower", new Gold(25), rangeCells: 2, damage: 5, attackCooldownTicks: 2),
                new TowerDefinition(ControlTowerId, "Control Ward", new Gold(35), rangeCells: 2, damage: 3, attackCooldownTicks: 3),
                new TowerDefinition(UtilityTowerId, "Relay Ward", new Gold(40), rangeCells: 1, damage: 1, attackCooldownTicks: 5),
                new TowerDefinition(PulseTowerId, "Pulse Ward", new Gold(45), rangeCells: 1, damage: 8, attackCooldownTicks: 4),
                new TowerDefinition(PrismTowerId, "Prism Ward", new Gold(60), rangeCells: 4, damage: 12, attackCooldownTicks: 6)
            },
            new[]
            {
                new CreepDefinition(CreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 10, speedPerSecond: 1),
                new CreepDefinition(BruteCreepId, "Brute", new Gold(18), new Income(2), new Gold(2), new Gold(3), maxHealth: 24, speedPerSecond: 1),
                new CreepDefinition(SwarmCreepId, "Swarm", new Gold(6), new Income(1), new Gold(1), new Gold(1), maxHealth: 5, speedPerSecond: 2),
                new CreepDefinition(ShadeCreepId, "Shade", new Gold(24), new Income(3), new Gold(2), new Gold(4), maxHealth: 14, speedPerSecond: 2),
                new CreepDefinition(SiegeCreepId, "Siege", new Gold(40), new Income(4), new Gold(4), new Gold(6), maxHealth: 48, speedPerSecond: 1)
            },
            Array.Empty<TechDefinition>(),
            new[] { new MapDefinition(new ContentId("map.vertical-slice"), "Vertical Slice", width: 7, height: 16, new GridPosition(3, 0), new GridPosition(3, 15), Array.Empty<GridPosition>()) },
            Array.Empty<BotProfileDefinition>());
    }
}
