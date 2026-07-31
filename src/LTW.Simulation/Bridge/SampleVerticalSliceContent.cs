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

    // Foundry line — industrial towers. Same five defensive questions as the arcane line
    // (single-target, chip, utility, close burst, long sniper) answered with heavier metal:
    // each trades a little efficiency for reach or burst relative to its arcane counterpart.
    public static readonly ContentId GatlingTowerId = new("tower.gatling");

    public static readonly ContentId TeslaTowerId = new("tower.tesla");

    public static readonly ContentId FoundryTowerId = new("tower.foundry");

    public static readonly ContentId BarricadeTowerId = new("tower.barricade");

    public static readonly ContentId RepairDroneTowerId = new("tower.repair_drone");

    // Grove line — growth towers. The cheap end of the roster: individually weaker per gold
    // than either other line, but the entry costs are low enough to build wide early.
    public static readonly ContentId ElderCanopyTowerId = new("tower.elder_canopy");

    public static readonly ContentId SaplingTowerId = new("tower.sapling");

    public static readonly ContentId BloomheartTowerId = new("tower.bloomheart");

    public static readonly ContentId ThornSnareTowerId = new("tower.thorn_snare");

    public static readonly ContentId SporeCloudTowerId = new("tower.spore_cloud");

    public static readonly ContentId CreepId = new("creep.runner");

    public static readonly ContentId BruteCreepId = new("creep.brute");

    public static readonly ContentId SwarmCreepId = new("creep.swarm");

    public static readonly ContentId ShadeCreepId = new("creep.shade");

    public static readonly ContentId SiegeCreepId = new("creep.siege");

    // Category 2 roster (send-menu split landed first; this is the content it was built for).
    public static readonly ContentId WispCreepId = new("creep.wisp");

    public static readonly ContentId RevenantCreepId = new("creep.revenant");

    public static readonly ContentId ObsidianBruteCreepId = new("creep.obsidian_brute");

    public static readonly ContentId SerpentCreepId = new("creep.serpent");

    public static readonly ContentId TurretWalkerCreepId = new("creep.turret_walker");

    // Category 3 roster ("ELITE") — Meshy auto-rigged bipeds. Costlier and heavier than the
    // first ten, and unlike Category 2 these are NOT send-cooldown exempt: cost is what paces
    // them, so they stay on the normal cooldown like Category 1.
    public static readonly ContentId ZephyrCreepId = new("creep.zephyr");

    public static readonly ContentId BurrowerCreepId = new("creep.burrower");

    public static readonly ContentId StalkerCreepId = new("creep.stalker");

    public static readonly ContentId WardenCreepId = new("creep.warden");

    public static readonly ContentId ColossusCreepId = new("creep.colossus");

    public static ContentCatalog Create()
    {
        return new ContentCatalog(
            "mvp-07-15-tower-3x5-roster",
            new[]
            {
                new TowerDefinition(TowerId, "Arrow Tower", new Gold(14), rangeCells: 2, damage: 2, attackCooldownTicks: 2, categoryIndex: 0),
                // Range 3 rather than 2. At range 2 this was strictly worse than the Arrow Tower on
                // every axis at once — dearer, same reach, same damage, slower — with no
                // compensating mechanic, so there was never a reason to build one. Reach is what a
                // shrine that projects influence should be selling, and it is the only tower under
                // 30 gold that has it.
                new TowerDefinition(ControlTowerId, "Control Ward", new Gold(24), rangeCells: 3, damage: 2, attackCooldownTicks: 3, categoryIndex: 0),
                new TowerDefinition(UtilityTowerId, "Relay Ward", new Gold(28), rangeCells: 2, damage: 2, attackCooldownTicks: 4, categoryIndex: 0),
                new TowerDefinition(PulseTowerId, "Pulse Ward", new Gold(32), rangeCells: 1, damage: 6, attackCooldownTicks: 4, categoryIndex: 0),
                new TowerDefinition(PrismTowerId, "Prism Ward", new Gold(42), rangeCells: 4, damage: 9, attackCooldownTicks: 6, categoryIndex: 0),

                // Foundry line. Costs sit above the arcane equivalents and the payoff is raw
                // output: Gatling fires every tick for less damage per shot than Arrow but far
                // more over time, Foundry trades Prism's reach for a much harder single hit,
                // and Barricade is the cheapest way to hold a cell at all.
                new TowerDefinition(GatlingTowerId, "Gatling Turret", new Gold(30), rangeCells: 2, damage: 2, attackCooldownTicks: 1, categoryIndex: 1),
                // 38 to 44. Chain Arc turned out to be the strongest mechanic on the roster relative to
                // its own baseline (+60% against a stack, +49% in a trickle), and at 38 the
                // opportunity-cost test failed: a Tesla returned 1.37 damage per gold against 1.24 for
                // equal gold spent on plain Arrow Towers, so taking one was strictly correct.
                new TowerDefinition(TeslaTowerId, "Tesla Coil Spire", new Gold(44), rangeCells: 3, damage: 5, attackCooldownTicks: 3, categoryIndex: 1),
                new TowerDefinition(FoundryTowerId, "Foundry Core", new Gold(52), rangeCells: 2, damage: 14, attackCooldownTicks: 6, categoryIndex: 1),
                // Range 1 to 2 and damage 3 to 5: the price of the fixed up-lane arc, and also a repair.
                // At range 1 with a full diamond, 66 of 110 legal placements could hit nothing at
                // all; at range 2 with the half-plane that falls to 34, all of them columns 0 and 6
                // which no range-2 tower reaches the lane from anyway. Damage 5 also crosses the
                // renderer's damage >= 5 threshold, so the shot changes colour and starts printing
                // numbers — the bonus is literally visible.
                new TowerDefinition(BarricadeTowerId, "Barricade Bastion", new Gold(18), rangeCells: 2, damage: 5, attackCooldownTicks: 4, categoryIndex: 1),
                // 34 to 40. At 34 the mandatory-buy test failed outright: a drone bundle returned
                // 3.00 damage per gold against 2.86 for the best plain-damage bundle at comparable
                // gold, so taking one was strictly correct and the choice was fake. 40 brings both
                // drone bundles just under plain damage, which is what a support tower should be -
                // a lateral option, not a free upgrade.
                new TowerDefinition(RepairDroneTowerId, "Repair Drone Spire", new Gold(40), rangeCells: 3, damage: 3, attackCooldownTicks: 2, categoryIndex: 1),

                // Grove line. Cheap and individually weak — the line you spam early and outgrow,
                // except Elder Canopy, which is the roster's long-range anchor and priced for it.
                new TowerDefinition(ElderCanopyTowerId, "Elder Canopy", new Gold(46), rangeCells: 5, damage: 8, attackCooldownTicks: 6, categoryIndex: 2),
                new TowerDefinition(SaplingTowerId, "Sapling Sentinel", new Gold(10), rangeCells: 2, damage: 2, attackCooldownTicks: 3, categoryIndex: 2),
                new TowerDefinition(BloomheartTowerId, "Bloomheart Totem", new Gold(22), rangeCells: 2, damage: 4, attackCooldownTicks: 3, categoryIndex: 2),
                // Range 1 to 2 is required by the mechanic, not a buff: at range 1 the bramble zone
                // could not reliably cover 3 route cells, and a speed-3 creep would step over the
                // whole thing in one tick. Cost 26 to 30 pays for slowing every creep that crosses
                // it, which roughly doubles the shot opportunities of every tower covering those
                // cells.
                new TowerDefinition(ThornSnareTowerId, "Thorn Snare Totem", new Gold(34), rangeCells: 2, damage: 5, attackCooldownTicks: 3, categoryIndex: 2),
                // Authored damage is now only the FLOOR: Rot scales the real number off the target's max
                // health (CombatService.RotDamage), so 4 is what it does to chaff and 15 is what it
                // does to a Colossus. Cooldown slows to 6 because the payoff is per-shot magnitude
                // against fat targets, not rate.
                new TowerDefinition(SporeCloudTowerId, "Spore Cloud Bloom", new Gold(34), rangeCells: 3, damage: 4, attackCooldownTicks: 6, categoryIndex: 2)
            },
            new[]
            {
                new CreepDefinition(CreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 10, speedPerSecond: 1, categoryIndex: 0),
                new CreepDefinition(BruteCreepId, "Brute", new Gold(18), new Income(2), new Gold(2), new Gold(3), maxHealth: 24, speedPerSecond: 1, categoryIndex: 0),
                new CreepDefinition(SwarmCreepId, "Swarm", new Gold(6), new Income(1), new Gold(1), new Gold(1), maxHealth: 5, speedPerSecond: 2, categoryIndex: 0),
                new CreepDefinition(ShadeCreepId, "Shade", new Gold(24), new Income(3), new Gold(2), new Gold(4), maxHealth: 14, speedPerSecond: 2, categoryIndex: 0),
                new CreepDefinition(SiegeCreepId, "Siege", new Gold(40), new Income(4), new Gold(4), new Gold(6), maxHealth: 48, speedPerSecond: 1, categoryIndex: 0),

                // Category 2 — first pass, stats chosen to each ask a different defensive
                // question from the existing 5 (and from each other). See docs/GD_TUNING_LOG.md
                // for the full rationale; treat these as tunable starting points, not final.
                new CreepDefinition(WispCreepId, "Crystal Wisp", new Gold(5), new Income(1), new Gold(1), new Gold(1), maxHealth: 4, speedPerSecond: 3, categoryIndex: 1, ignoresSendCooldown: true),
                new CreepDefinition(RevenantCreepId, "Ash Revenant", new Gold(16), new Income(4), new Gold(1), new Gold(2), maxHealth: 8, speedPerSecond: 2, categoryIndex: 1, ignoresSendCooldown: true),
                new CreepDefinition(ObsidianBruteCreepId, "Obsidian Brute", new Gold(30), new Income(3), new Gold(3), new Gold(4), maxHealth: 60, speedPerSecond: 1, categoryIndex: 1, ignoresSendCooldown: true),
                // Cost cut 22->20 (2026-07-28 rebalance): at 22 this was strictly dominated by
                // Obsidian Brute (1.45 HP/gold and 0.091 income/gold vs Obsidian Brute's 2.00 and
                // 0.100 for only 8 more gold) — see docs/GD_TUNING_LOG.md for the full comparison.
                new CreepDefinition(SerpentCreepId, "Serpent Coil", new Gold(20), new Income(2), new Gold(2), new Gold(3), maxHealth: 32, speedPerSecond: 1, categoryIndex: 1, ignoresSendCooldown: true),
                new CreepDefinition(TurretWalkerCreepId, "Spire Turret Walker", new Gold(38), new Income(4), new Gold(4), new Gold(5), maxHealth: 40, speedPerSecond: 2, categoryIndex: 1, ignoresSendCooldown: true),

                // Category 3 ("ELITE") — first pass, tunable. A deliberately later tier: costs
                // and health run past the first ten, which is self-limiting because cost is the
                // gate. Left on the normal send cooldown (no ignoresSendCooldown) unlike
                // Category 2, since price already paces them.
                new CreepDefinition(ZephyrCreepId, "Zephyr Wraith", new Gold(22), new Income(2), new Gold(2), new Gold(3), maxHealth: 12, speedPerSecond: 3, categoryIndex: 2),
                new CreepDefinition(BurrowerCreepId, "Fracture Burrower", new Gold(26), new Income(2), new Gold(3), new Gold(4), maxHealth: 44, speedPerSecond: 1, categoryIndex: 2),
                new CreepDefinition(StalkerCreepId, "Umbral Stalker", new Gold(28), new Income(3), new Gold(2), new Gold(4), maxHealth: 20, speedPerSecond: 2, categoryIndex: 2),
                new CreepDefinition(WardenCreepId, "Aegis Warden", new Gold(34), new Income(3), new Gold(3), new Gold(4), maxHealth: 55, speedPerSecond: 1, categoryIndex: 2),
                // Named Colossus rather than Siege to keep it distinct from creep.siege, which it
                // deliberately outclasses (90 health / 52 gold vs 48 / 40) rather than duplicates
                // — same call made for Obsidian Brute against Brute.
                new CreepDefinition(ColossusCreepId, "Siege Colossus", new Gold(52), new Income(5), new Gold(5), new Gold(8), maxHealth: 90, speedPerSecond: 1, categoryIndex: 2)
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
