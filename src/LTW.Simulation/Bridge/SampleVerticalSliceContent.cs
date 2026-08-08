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

    // Bot build orders. These are content, and they used to be code: three private ContentId[]
    // arrays inside LocalVerticalSlice naming the constants above (OPEN_ITEMS.md item 26), which
    // made the bots' build-out a property of the simulation assembly rather than of the catalog.
    // They now travel on BotProfileDefinition.BuildOrder, so a different roster ships different bot
    // openings without a code change — and the ids are declared a few lines up, in the same file
    // that authors the towers they name, rather than reached into from another namespace.
    //
    // Cycled by ownedTowerCount rather than switched on a few slots with a repeating tail arm, for
    // two reasons (OPEN_ITEMS.md's retired 2026-07-29 review, "bots can only build 5 of the 15
    // towers"): the old shape could only ever reach 5 of the 15 towers (Arrow, Control, Pulse, Prism
    // plus whatever the tail arm was), so every mechanic added since the 15-tower expansion was
    // measured against a bot that never builds it; and its tail arm repeated a single tower forever
    // once reached (Defensive -> endless Prism, Greedy -> endless Arrow), which is why BotMazingTests'
    // "keeps building past nine towers" assertion passed on a bot spamming one tower. Each profile's
    // list is a flavour (Defensive leans control/area/support, Greedy leans cheap, both fully
    // reachable but not the only towers that profile builds), and the three lists' union covers all
    // 15 towers, not just each profile's own list.
    //
    // Each list's first few entries deliberately match the old hardcoded switch's early slots exactly
    // (same tower, same cost, same order) rather than reshuffling from slot 0. Income only ticks every
    // 50 simulation ticks (IncomeIntervalTicks), so gold is flat between jumps and a bot's opening
    // tower-count gate (BotProfileDefinition.MinimumTowerCoverage) clears on whichever jump first
    // covers the cumulative cost — even a few gold of difference in an early slot can push that past a
    // 50-tick boundary and shift the observable timing by up to a full income cycle.
    private static readonly TowerRole[] DefensiveBuildOrder =
    {
        // Holds first: a brake, then bodies, then something that hits a group. Defensive wants the
        // lane slow before it wants it dead.
        TowerRole.Brake,
        TowerRole.Dps,
        TowerRole.Dps,
        TowerRole.Aoe,
        TowerRole.Brake,
        TowerRole.Support,
        TowerRole.Dps,
        TowerRole.Aoe,
    };

    private static readonly TowerRole[] BalancedBuildOrder =
    {
        TowerRole.Dps,
        TowerRole.Brake,
        TowerRole.Aoe,
        TowerRole.Dps,
        TowerRole.Aoe,
        TowerRole.Economy,
        TowerRole.Dps,
        TowerRole.Support,
    };

    private static readonly TowerRole[] GreedyBuildOrder =
    {
        // Economy early and often: Greedy's lever is sending, and it wants the lane paying for it.
        TowerRole.Dps,
        TowerRole.Economy,
        TowerRole.Dps,
        TowerRole.Aoe,
        TowerRole.Economy,
        TowerRole.Dps,
    };

    public static ContentCatalog Create()
    {
        return new ContentCatalog(
            "mvp-07-15-tower-3x5-roster",
            new[]
            {
                new TowerDefinition(TowerId, "Arrow Tower", new Gold(14), rangeCells: 2, damage: 6, attackCooldownTicks: 4, categoryIndex: 0, role: TowerRole.Dps),
                // Range 3 rather than 2. At range 2 this was strictly worse than the Arrow Tower on
                // every axis at once — dearer, same reach, same damage, slower — with no
                // compensating mechanic, so there was never a reason to build one. Reach is what a
                // shrine that projects influence should be selling, and it is the only tower under
                // 30 gold that has it.
                // Control's damage 2 -> 3 is main's cost-curve compression; Relay's signalGoldPerHit
                // is this branch making its income authored rather than inferred from its name. The
                // two touch adjacent lines and are independent.
                new TowerDefinition(ControlTowerId, "Control Ward", new Gold(24), rangeCells: 3, damage: 6, attackCooldownTicks: 6, categoryIndex: 0, role: TowerRole.Dps),
                new TowerDefinition(UtilityTowerId, "Relay Ward", new Gold(28), rangeCells: 2, damage: 4, attackCooldownTicks: 8, categoryIndex: 0, signalGoldPerHit: 3, role: TowerRole.Economy),
                new TowerDefinition(PulseTowerId, "Pulse Ward", new Gold(32), rangeCells: 1, damage: 12, attackCooldownTicks: 8, categoryIndex: 0, role: TowerRole.Aoe),
                new TowerDefinition(PrismTowerId, "Prism Ward", new Gold(42), rangeCells: 4, damage: 18, attackCooldownTicks: 12, categoryIndex: 0, role: TowerRole.Dps),

                // Foundry line. Costs sit above the arcane equivalents and the payoff is raw
                // output: Gatling fires every tick for less damage per shot than Arrow but far
                // more over time, Foundry trades Prism's reach for a much harder single hit,
                // and Barricade is the cheapest way to hold a cell at all.
                new TowerDefinition(GatlingTowerId, "Gatling Turret", new Gold(30), rangeCells: 2, damage: 4, attackCooldownTicks: 2, categoryIndex: 1, role: TowerRole.Dps),
                // 38 to 44. Chain Arc turned out to be the strongest mechanic on the roster relative to
                // its own baseline (+60% against a stack, +49% in a trickle), and at 38 the
                // opportunity-cost test failed: a Tesla returned 1.37 damage per gold against 1.24 for
                // equal gold spent on plain Arrow Towers, so taking one was strictly correct.
                new TowerDefinition(TeslaTowerId, "Tesla Coil Spire", new Gold(44), rangeCells: 3, damage: 10, attackCooldownTicks: 6, categoryIndex: 1, role: TowerRole.Aoe),
                new TowerDefinition(FoundryTowerId, "Foundry Core", new Gold(52), rangeCells: 2, damage: 24, attackCooldownTicks: 12, categoryIndex: 1, slowsCreeps: true, role: TowerRole.Brake),
                // Range 1 to 2 and damage 3 to 5: the price of the fixed up-lane arc, and also a repair.
                // At range 1 with a full diamond, 66 of 110 legal placements could hit nothing at
                // all; at range 2 with the half-plane that falls to 34, all of them columns 0 and 6
                // which no range-2 tower reaches the lane from anyway. Damage 5 also crosses the
                // renderer's damage >= 5 threshold, so the shot changes colour and starts printing
                // numbers — the bonus is literally visible.
                new TowerDefinition(BarricadeTowerId, "Barricade Bastion", new Gold(18), rangeCells: 2, damage: 10, attackCooldownTicks: 8, categoryIndex: 1, role: TowerRole.Wall),
                // 34 to 40. At 34 the mandatory-buy test failed outright: a drone bundle returned
                // 3.00 damage per gold against 2.86 for the best plain-damage bundle at comparable
                // gold, so taking one was strictly correct and the choice was fake. 40 brings both
                // drone bundles just under plain damage, which is what a support tower should be -
                // a lateral option, not a free upgrade.
                new TowerDefinition(RepairDroneTowerId, "Repair Drone Spire", new Gold(40), rangeCells: 3, damage: 6, attackCooldownTicks: 4, categoryIndex: 1, role: TowerRole.Support),

                // Grove line. Cheap and individually weak — the line you spam early and outgrow,
                // except Elder Canopy, which is the roster's long-range anchor and priced for it.
                new TowerDefinition(ElderCanopyTowerId, "Elder Canopy", new Gold(46), rangeCells: 5, damage: 14, attackCooldownTicks: 12, categoryIndex: 2, role: TowerRole.Dps),
                new TowerDefinition(SaplingTowerId, "Sapling Sentinel", new Gold(10), rangeCells: 2, damage: 6, attackCooldownTicks: 6, categoryIndex: 2, role: TowerRole.Wall),
                new TowerDefinition(BloomheartTowerId, "Bloomheart Totem", new Gold(22), rangeCells: 2, damage: 8, attackCooldownTicks: 6, categoryIndex: 2, role: TowerRole.Support),
                // Range 1 to 2 is required by the mechanic, not a buff: at range 1 the bramble zone
                // could not reliably cover 3 route cells, and a speed-3 creep would step over the
                // whole thing in one tick. Cost 26 to 30 pays for slowing every creep that crosses
                // it, which roughly doubles the shot opportunities of every tower covering those
                // cells.
                new TowerDefinition(ThornSnareTowerId, "Thorn Snare Totem", new Gold(34), rangeCells: 2, damage: 10, attackCooldownTicks: 6, categoryIndex: 2, slowsCreeps: true, role: TowerRole.Brake),
                // Authored damage is now only the FLOOR: Rot scales the real number off the target's max
                // health (CombatService.RotDamage), so 4 is what it does to chaff and 15 is what it
                // does to a Colossus. Cooldown slows to 6 because the payoff is per-shot magnitude
                // against fat targets, not rate.
                new TowerDefinition(SporeCloudTowerId, "Spore Cloud Bloom", new Gold(34), rangeCells: 3, damage: 8, attackCooldownTicks: 12, categoryIndex: 2, role: TowerRole.Aoe)
            },
            new[]
            {
                new CreepDefinition(CreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 13, speedPerSecond: 1, categoryIndex: 0),
                new CreepDefinition(BruteCreepId, "Brute", new Gold(18), new Income(2), new Gold(2), new Gold(3), maxHealth: 24, speedPerSecond: 1, categoryIndex: 0),
                new CreepDefinition(SwarmCreepId, "Swarm", new Gold(6), new Income(1), new Gold(1), new Gold(1), maxHealth: 5, speedPerSecond: 2, categoryIndex: 0),
                new CreepDefinition(ShadeCreepId, "Shade", new Gold(24), new Income(3), new Gold(2), new Gold(4), maxHealth: 14, speedPerSecond: 2, categoryIndex: 0),
                new CreepDefinition(SiegeCreepId, "Siege", new Gold(40), new Income(4), new Gold(4), new Gold(6), maxHealth: 48, speedPerSecond: 1, categoryIndex: 0),

                // Category 1, "SUPPORT". These five stopped being a stat tier and became a role.
                //
                // Every cost here is DISTINCT from every other creep's, and that is a constraint
                // rather than a preference. BotController sorts its preference list by descending
                // cost and sends the first id it can afford, so two creeps at the same price make
                // the later one mathematically unreachable — affording it always means affording
                // the other. A first pass at these numbers tied walker with shade at 24, revenant
                // with brute at 18 and serpent with burrower at 26, which silently removed three
                // creeps from the bots' repertoire. VerticalSliceBridgeTests catches this.
                //
                // The category needed an identity rather than a rebalance. Its one mechanical trait,
                // ignoresSendCooldown, had been inert since the send cooldown was set to 0, so it was
                // separated from CORE by nothing but numbers — and the numbers overlapped almost
                // exactly (CORE cost 6-40 / health 5-48, these cost 5-38 / health 4-48). Three pairs
                // across the roster shared a hp-per-gold ratio AND a speed, so a player picking
                // between them was picking between reskins. Now: CORE is bodies, SUPPORT is force
                // multipliers, ELITE is big threats.
                //
                // Four of the five are SLOWER than the pack (movementCost 4 against the default 3).
                // That is the whole reason MovementCost exists — speed is a whole number whose floor
                // is 1, and the heavies these follow are already at 1, so nothing below them could be
                // expressed. Drifting back about a cell every twelve ticks does two things at once:
                // it keeps them behind the wall, where leader-first targeting cannot reach them, and
                // it gives every aura a natural expiry as the pack pulls away.
                //
                // Their counter is composition, not better sniping. Elder Canopy targets the creep
                // FURTHEST BACK, so a wave that is all support and no wall feeds it. Stats are
                // deliberately poor: none of these five is worth sending alone, and the roster
                // domination check exempts them for the same reason it exempts Relay Ward — their
                // value is not in their stat line.
                new CreepDefinition(WispCreepId, "Crystal Wisp", new Gold(12), new Income(1), new Gold(1), new Gold(1), maxHealth: 6, speedPerSecond: 1, categoryIndex: 1, ignoresSendCooldown: true, movementCost: 4, support: CreepSupportRole.Pacesetter),
                new CreepDefinition(RevenantCreepId, "Ash Revenant", new Gold(19), new Income(2), new Gold(1), new Gold(2), maxHealth: 14, speedPerSecond: 1, categoryIndex: 1, ignoresSendCooldown: true, movementCost: 4, support: CreepSupportRole.Mender),
                new CreepDefinition(ObsidianBruteCreepId, "Obsidian Brute", new Gold(30), new Income(3), new Gold(3), new Gold(4), maxHealth: 40, speedPerSecond: 1, categoryIndex: 1, ignoresSendCooldown: true, movementCost: 4, support: CreepSupportRole.Bulwark),
                new CreepDefinition(SerpentCreepId, "Serpent Coil", new Gold(27), new Income(2), new Gold(2), new Gold(3), maxHealth: 26, speedPerSecond: 1, categoryIndex: 1, ignoresSendCooldown: true, movementCost: 4, support: CreepSupportRole.Binder),

                // The exception, and the category's payoff. It walks the direct route straight over
                // the maze — the most disruptive thing a creep can do, which is why it is costed to
                // die to almost anything. It is not slowed and carries no aura: it is not travelling
                // with the pack at all, it is racing a shorter route on its own.
                //
                // What keeps it alive is that a tower fires at ONE target per tick and then sits on
                // cooldown, so a defence busy with the wave cannot spare a shot for it. Sent alone it
                // simply dies. It is the only unit on the roster whose value is entirely a function
                // of what else was sent with it.
                new CreepDefinition(TurretWalkerCreepId, "Spire Turret Walker", new Gold(23), new Income(2), new Gold(4), new Gold(5), maxHealth: 10, speedPerSecond: 2, categoryIndex: 1, ignoresSendCooldown: true, ignoresMaze: true),

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
                new CreepDefinition(ColossusCreepId, "Siege Colossus", new Gold(52), new Income(5), new Gold(5), new Gold(8), maxHealth: 78, speedPerSecond: 1, categoryIndex: 2)
            },
            Array.Empty<TechDefinition>(),
            new[] { new MapDefinition(new ContentId("map.vertical-slice"), "Vertical Slice", width: 7, height: 16, new GridPosition(3, 0), new GridPosition(3, 15), Array.Empty<GridPosition>()) },
            new[]
            {
                // Aggression/defenseBias/minimumGoldReserve drive BotController's reactive spending
                // (gold-reserve floor, minimum tower coverage before sending, lane-pressure
                // tolerance) instead of the tick-scheduled constants they replace.
                // Reserve values stay modest: sending is already gated by minimumTowerCoverage
                // until a profile's opening package is built, so the reserve here only needs to stop
                // a bot spending down to zero gold, not also cover the whole build-out phase (a high
                // reserve just stalls building against the new cheaper tower costs).
                //
                // minimumTowerCoverage: Greedy is designed to send from the first tick (it prioritises
                // income, not a defensive package); Balanced and Defensive finish an opening package
                // first. That intent used to be enforced indirectly, through gold-reserve thresholds
                // tuned against specific tower costs, so cheap enough towers left just enough spare
                // gold to opportunistically afford a creep mid-build-out. Stating the tower count
                // directly means it holds regardless of the current cost balance.
                new BotProfileDefinition(BotProfileIds.Greedy, "Greedy", aggression: 90, defenseBias: 10, minimumGoldReserve: 0, buildOrder: GreedyBuildOrder, minimumTowerCoverage: 0),
                new BotProfileDefinition(BotProfileIds.Balanced, "Balanced", aggression: 50, defenseBias: 50, minimumGoldReserve: 20, buildOrder: BalancedBuildOrder, minimumTowerCoverage: 3),
                new BotProfileDefinition(BotProfileIds.Defensive, "Defensive", aggression: 20, defenseBias: 80, minimumGoldReserve: 20, buildOrder: DefensiveBuildOrder, minimumTowerCoverage: 4)
            });
    }
}
