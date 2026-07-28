using System;
using LTW.Simulation.Bots;
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
                new TowerDefinition(TowerId, "Arrow Tower", new Gold(14), rangeCells: 2, damage: 2, attackCooldownTicks: 2),
                new TowerDefinition(ControlTowerId, "Control Ward", new Gold(24), rangeCells: 2, damage: 2, attackCooldownTicks: 3),
                new TowerDefinition(UtilityTowerId, "Relay Ward", new Gold(28), rangeCells: 2, damage: 2, attackCooldownTicks: 4),
                new TowerDefinition(PulseTowerId, "Pulse Ward", new Gold(32), rangeCells: 1, damage: 6, attackCooldownTicks: 4),
                new TowerDefinition(PrismTowerId, "Prism Ward", new Gold(42), rangeCells: 4, damage: 9, attackCooldownTicks: 6)
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
            new[]
            {
                // Aggression/defenseBias/minimumGoldReserve drive BotController's reactive spending
                // (gold-reserve floor, minimum tower coverage before sending, lane-pressure
                // tolerance) instead of the tick-scheduled constants they replace.
                // Reserve values stay modest: sending is already gated by MinimumTowerCoverage
                // in LocalVerticalSlice until a profile's opening package is built, so the reserve
                // here only needs to stop a bot spending down to zero gold, not also cover the
                // whole build-out phase (a high reserve just stalls building against the new
                // cheaper tower costs).
                new BotProfileDefinition(BotProfileIds.Greedy, "Greedy", aggression: 90, defenseBias: 10, minimumGoldReserve: 0),
                new BotProfileDefinition(BotProfileIds.Balanced, "Balanced", aggression: 50, defenseBias: 50, minimumGoldReserve: 20),
                new BotProfileDefinition(BotProfileIds.Defensive, "Defensive", aggression: 20, defenseBias: 80, minimumGoldReserve: 20)
            });
    }
}
