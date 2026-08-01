using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Covers the SUPPORT category: the four auras, the trailing pace that protects them, and the one
/// creep that ignores the maze.
/// </summary>
/// <remarks>
/// Before this category existed a creep was six numbers and an inert flag, and the three send
/// categories were separated by nothing a player could observe — measured across the roster, CORE
/// spanned cost 6-40 and health 5-48 while category 1 spanned 5-38 and 4-48, with three pairs
/// sharing both a health-per-gold ratio and a speed. These tests pin the behaviour that replaced
/// that overlap, because none of it is visible in a stat line.
/// </remarks>
public sealed class SupportCreepTests
{
    private readonly ITestOutputHelper output;

    public SupportCreepTests(ITestOutputHelper output) => this.output = output;

    private static readonly ContentCatalog Catalog = SampleVerticalSliceContent.Create();
    private static readonly LaneId Lane = new(1);
    private static readonly PlayerId Attacker = new(2);
    private static readonly PlayerId Defender = new(1);

    private static IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> Routes() =>
        new Dictionary<LaneId, IReadOnlyList<GridPosition>>
        {
            [Lane] = Enumerable.Range(0, 18).Select(y => new GridPosition(3, y)).ToArray()
        };

    private static CombatContent Content() => new(
        Catalog.Creeps,
        Catalog.Towers,
        new Dictionary<LaneId, PlayerId> { [Lane] = Defender });

    private static CreepDefinition Creep(string id) => Catalog.Creeps.First(creep => creep.Id.Value == id);

    private static CreepCombatState At(CombatService service, int entityId, string creepId, int pathIndex, PlayerId? sender = null) =>
        service.SpawnCreep(new EntityId(entityId), Creep(creepId), sender ?? Attacker, Lane).WithMovement(pathIndex, 0);

    // ---- The category itself ------------------------------------------------------------------

    /// <summary>
    /// The category has one support role per creep, and no two share one.
    /// </summary>
    /// <remarks>
    /// The point of the rework. Five creeps that all did the same thing is what it replaced, so a
    /// duplicate role here would quietly restore exactly the problem.
    /// </remarks>
    [Fact]
    public void Every_support_creep_has_a_distinct_role()
    {
        var category = Catalog.Creeps.Where(creep => creep.CategoryIndex == 1).ToArray();
        var roles = category.Select(creep => creep.Support).Where(role => role != CreepSupportRole.None).ToArray();

        output.WriteLine(string.Join("\n", category.Select(c => $"  {c.Name,-20} {c.Support,-11} cost {c.Cost.Amount,2} hp {c.MaxHealth,2} moveCost {c.MovementCost} flies {c.IgnoresMaze}")));

        Assert.Equal(5, category.Length);
        Assert.Equal(roles.Length, roles.Distinct().Count());
        Assert.Single(category, creep => creep.IgnoresMaze);
    }

    /// <summary>
    /// No two creeps anywhere on the roster cost the same.
    /// </summary>
    /// <remarks>
    /// Structural, not cosmetic. BotController sorts its preference list by descending cost and
    /// sends the first id it can afford, so two creeps at one price make the later one
    /// mathematically unreachable — affording it always implies affording the other. A first pass
    /// at the SUPPORT costs tied three pairs and silently deleted three creeps from the bots'
    /// repertoire; only VerticalSliceBridgeTests caught it, and only for one of the three.
    /// </remarks>
    [Fact]
    public void No_two_creeps_share_a_cost()
    {
        var byCost = Catalog.Creeps.GroupBy(creep => creep.Cost.Amount).Where(group => group.Count() > 1).ToArray();
        Assert.True(byCost.Length == 0,
            "these creeps share a cost, which makes the dearer one unreachable to bots: " +
            string.Join("; ", byCost.Select(g => $"{g.Key}G = {string.Join(", ", g.Select(c => c.Name))}")));
    }

    /// <summary>The content default and the combat constant are the same number.</summary>
    /// <remarks>
    /// CreepDefinition cannot reference CombatService without pointing Content at Combat, so the
    /// default is restated there. This is the assertion that keeps the copy honest.
    /// </remarks>
    [Fact]
    public void The_default_movement_cost_matches_the_combat_base()
    {
        Assert.Equal(CombatService.BaseMovementCost, CreepDefinition.DefaultMovementCost);
    }

    // ---- Trailing -----------------------------------------------------------------------------

    /// <summary>
    /// A support creep sent with the pack falls behind it, which is what keeps it alive.
    /// </summary>
    /// <remarks>
    /// The whole reason MovementCost exists. Speed is a whole number whose floor is 1 and the
    /// heavies a support follows are already at 1, so without a per-creep cost a support would move
    /// in lockstep with the pack forever and be unkillable by leader-first targeting rather than
    /// merely hard to reach.
    /// </remarks>
    [Fact]
    public void A_support_creep_drifts_behind_the_pack_it_was_sent_with()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { At(service, 1, "creep.brute", 0), At(service, 2, "creep.obsidian_brute", 0) },
            System.Array.Empty<TowerCombatState>());

        for (var tick = 0; tick < 48; tick++)
        {
            state = service.Advance(state, Content(), Routes(), new SimulationTick(tick)).State;
        }

        var brute = state.Creeps.Single(creep => creep.EntityId.Value == 1);
        var support = state.Creeps.Single(creep => creep.EntityId.Value == 2);
        output.WriteLine($"after 48 ticks: brute at {brute.PathIndex}, support at {support.PathIndex}");

        Assert.True(support.PathIndex < brute.PathIndex,
            $"the support kept pace with the pack ({support.PathIndex} vs {brute.PathIndex}), so nothing shields it");
    }

    /// <summary>
    /// With the pack in front, towers shoot the pack — the support is reached last.
    /// </summary>
    /// <remarks>
    /// This is the payoff of trailing, asserted through real target selection rather than by
    /// inspecting the ordering. Both creeps are in range of the tower; only their position along the
    /// lane differs.
    /// </remarks>
    [Fact]
    public void Towers_shoot_the_leading_pack_before_a_trailing_support()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { At(service, 1, "creep.obsidian_brute", 7), At(service, 2, "creep.brute", 9) },
            new[] { new TowerCombatState(new EntityId(50), new ContentId("tower.arrow"), Defender, Lane, new GridPosition(3, 8)) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        var damaged = result.Events.OfType<CreepDamagedEvent>().ToArray();

        Assert.NotEmpty(damaged);
        Assert.All(damaged, hit => Assert.Equal(2, hit.CreepEntityId.Value));
    }

    // ---- The four auras -----------------------------------------------------------------------

    [Fact]
    public void A_pacesetter_speeds_up_a_creep_beside_it_but_not_a_distant_one()
    {
        var service = new CombatService();
        var content = Content();
        var near = At(service, 1, "creep.brute", 4);
        var far = At(service, 2, "creep.brute", 12);
        var wisp = At(service, 3, "creep.wisp", 4);

        var withAura = new CombatState(new[] { near, far, wisp }, System.Array.Empty<TowerCombatState>());
        var without = new CombatState(new[] { At(service, 1, "creep.brute", 4) }, System.Array.Empty<TowerCombatState>());

        for (var tick = 0; tick < 12; tick++)
        {
            withAura = service.Advance(withAura, content, Routes(), new SimulationTick(tick)).State;
            without = service.Advance(without, content, Routes(), new SimulationTick(tick)).State;
        }

        var paced = withAura.Creeps.Single(creep => creep.EntityId.Value == 1).PathIndex;
        var unpaced = without.Creeps.Single(creep => creep.EntityId.Value == 1).PathIndex;
        var distant = withAura.Creeps.Single(creep => creep.EntityId.Value == 2);

        output.WriteLine($"paced {paced}, unpaced {unpaced}, distant advanced {distant.PathIndex - 12}");

        Assert.True(paced > unpaced, $"the Pacesetter did nothing ({paced} vs {unpaced})");
        Assert.Equal(unpaced - 4, distant.PathIndex - 12);
    }

    [Fact]
    public void A_mender_heals_a_wounded_neighbour_up_to_its_maximum()
    {
        var service = new CombatService();
        var wounded = At(service, 1, "creep.brute", 5).WithHealth(4);
        var state = new CombatState(
            new[] { wounded, At(service, 2, "creep.revenant", 5) },
            System.Array.Empty<TowerCombatState>());

        var healEvents = 0;
        for (var tick = 1; tick <= 40; tick++)
        {
            var result = service.Advance(state, Content(), Routes(), new SimulationTick(tick));
            state = result.State;
            healEvents += result.Events.OfType<CreepHealedEvent>().Count();
        }

        var healed = state.Creeps.Single(creep => creep.EntityId.Value == 1);
        output.WriteLine($"health 4 -> {healed.Health} of {healed.MaxHealth} across {healEvents} heal events");

        Assert.True(healed.Health > 4, "the Mender healed nothing");
        Assert.True(healed.Health <= healed.MaxHealth, "the Mender healed past maximum health");
    }

    [Fact]
    public void A_bulwark_reduces_damage_taken_by_a_neighbour()
    {
        var service = new CombatService();
        var tower = new TowerCombatState(new EntityId(50), new ContentId("tower.prism"), Defender, Lane, new GridPosition(3, 8));

        // Prism at damage 9, deliberately. A 25% cut of a small number truncates to nothing —
        // Gatling's damage of 2 would reduce to 2 and the test would report no shield where the
        // mechanic is in fact working exactly as specified.
        var shielded = Damage(service, new[] { At(service, 1, "creep.siege", 8), At(service, 2, "creep.obsidian_brute", 8) }, tower, 1);
        var bare = Damage(service, new[] { At(service, 1, "creep.siege", 8) }, tower, 1);

        output.WriteLine($"damage taken: {bare} bare, {shielded} shielded");
        Assert.True(shielded < bare, $"the Bulwark did not reduce damage ({shielded} vs {bare})");
        Assert.True(shielded >= 1, "a shielded creep must still take at least 1, or weak towers cannot kill it");
    }

    private static int Damage(CombatService service, CreepCombatState[] creeps, TowerCombatState tower, int targetEntityId)
    {
        var state = new CombatState(creeps, new[] { tower });
        var total = 0;
        for (var tick = 0; tick < 12; tick++)
        {
            var result = service.Advance(state, Content(), Routes(), new SimulationTick(tick));
            state = result.State;
            total += result.Events.OfType<CreepDamagedEvent>()
                .Where(hit => hit.CreepEntityId.Value == targetEntityId)
                .Sum(hit => hit.DamageDealt);
        }

        return total;
    }

    /// <summary>
    /// A tower inside a Binder's reach waits longer between shots.
    /// </summary>
    /// <remarks>
    /// Asserted on the armed cooldown rather than by counting shots over time, and that is not a
    /// convenience. Counting was tried first and reported 4 against 4 — not because the Binder did
    /// nothing, but because an Arrow's range of 2 lets a creep through in about nine ticks either
    /// way, so the window closed before the slower cadence could cost it a shot. The shot count was
    /// measuring the range window, not the brake.
    /// </remarks>
    [Fact]
    public void A_binder_slows_the_towers_near_it()
    {
        Assert.Equal(1, ArmedCooldown(new[] { "creep.siege", "creep.serpent" }) - ArmedCooldown(new[] { "creep.siege" }));
    }

    /// <summary>Ticks the tower waits after firing once, with these creeps in the lane.</summary>
    private static int ArmedCooldown(string[] creepIds)
    {
        var service = new CombatService();
        var creeps = creepIds.Select((id, index) => At(service, index + 1, id, 8)).ToArray();
        var tower = new TowerCombatState(new EntityId(50), new ContentId("tower.arrow"), Defender, Lane, new GridPosition(3, 8));

        var result = service.Advance(new CombatState(creeps, new[] { tower }), Content(), Routes(), new SimulationTick(0));
        Assert.NotEmpty(result.Events.OfType<TowerFiredEvent>());
        return (int)result.State.Towers.Single().NextAttackTick.Value;
    }

    /// <summary>
    /// An aura never reaches creeps belonging to another sender.
    /// </summary>
    /// <remarks>
    /// Not hypothetical: a lane holds creeps from two players at once — the seat attacking it
    /// directly, plus whatever leaked in from the previous lane — so shielding by proximity alone
    /// would have a support buffing the creeps being sent AT its own owner.
    /// </remarks>
    [Fact]
    public void An_aura_does_not_reach_another_senders_creeps()
    {
        var service = new CombatService();
        var other = new PlayerId(3);
        var state = new CombatState(
            new[] { At(service, 1, "creep.brute", 4, other), At(service, 2, "creep.wisp", 4) },
            System.Array.Empty<TowerCombatState>());
        var alone = new CombatState(new[] { At(service, 1, "creep.brute", 4, other) }, System.Array.Empty<TowerCombatState>());

        for (var tick = 0; tick < 12; tick++)
        {
            state = service.Advance(state, Content(), Routes(), new SimulationTick(tick)).State;
            alone = service.Advance(alone, Content(), Routes(), new SimulationTick(tick)).State;
        }

        Assert.Equal(
            alone.Creeps.Single().PathIndex,
            state.Creeps.Single(creep => creep.EntityId.Value == 1).PathIndex);
    }

    // ---- Flying -------------------------------------------------------------------------------

    /// <summary>
    /// The Walker takes the unmazed route while everything else takes the maze.
    /// </summary>
    /// <remarks>
    /// Asserted through a real match rather than by reading the flag, so it covers the wiring —
    /// LaneRouteSet, the direct route captured at match start, and the position resolution that
    /// picks between them — rather than just the content.
    /// </remarks>
    [Fact]
    public void The_walker_ignores_the_maze_and_the_others_do_not()
    {
        var walker = Creep("creep.turret_walker");
        Assert.True(walker.IgnoresMaze);
        Assert.Equal(CreepSupportRole.None, walker.Support);

        foreach (var creep in Catalog.Creeps.Where(c => c.Id.Value != "creep.turret_walker"))
        {
            Assert.False(creep.IgnoresMaze, $"{creep.Name} also ignores the maze; only the Walker should");
        }
    }

    /// <summary>
    /// A mazed lane lengthens the walkers' route and leaves the flyer's alone.
    /// </summary>
    /// <remarks>
    /// The mechanic's whole value, measured end to end rather than by reading the flag: a maze
    /// exists to make the walk long, and this is the number that does not grow when one is built.
    ///
    /// Mazed by letting the bots do it, which is a deliberate second attempt. Hand-placing towers
    /// was tried first and proved nothing — six towers went down in columns 1 and 5 while the route
    /// runs up the middle, so the route length never moved and the test passed its real assertion
    /// for the wrong reason. Bots build against the route itself, so the maze is genuine.
    /// </remarks>
    [Fact]
    public void A_mazed_lane_lengthens_the_walkers_route_but_not_the_flyers()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3));
        var lane = new LaneId(2);
        var straight = slice.RouteLength(lane);
        var directBefore = slice.DirectRouteLength(lane);

        slice.StartMatch();
        for (var tick = 0; tick < 1200; tick++)
        {
            slice.AdvanceOneTick();
        }

        var mazed = slice.RouteLength(lane);
        output.WriteLine($"lane 2: {straight} straight -> {mazed} mazed; direct route {directBefore} -> {slice.DirectRouteLength(lane)}");

        Assert.True(mazed > straight, $"the bots did not maze ({straight} -> {mazed}), so this proves nothing");
        Assert.Equal(straight, slice.DirectRouteLength(lane));
        Assert.Equal(directBefore, slice.DirectRouteLength(lane));
    }

    // ---- Bots understand composition -----------------------------------------------------------

    /// <summary>
    /// A bot never opens with an escort — the first thing it sends is always a wall.
    /// </summary>
    /// <remarks>
    /// The bot cannot see the lane, so "is there a wall in front of this support" is answered from
    /// what it just bought. This is the assertion that the gate is actually shut at the start.
    ///
    /// It is also the regression guard for a bug that made the whole rule inert. The gate was first
    /// written against a <c>long.MinValue</c> sentinel, and <c>tick.Value - long.MinValue</c>
    /// OVERFLOWS: at tick 300 it evaluates to -9223372036854775508, which is less than the nine-tick
    /// window, so escorts were permitted on the very first decision of the match. Every test in the
    /// suite passed with the rule doing nothing.
    /// </remarks>
    [Fact]
    public void A_bot_opens_with_a_wall_never_with_an_escort()
    {
        var content = SampleVerticalSliceContent.Create();

        foreach (var profile in new[] { BotDecisionProfile.Greedy, BotDecisionProfile.Balanced, BotDecisionProfile.Defensive })
        {
            for (var gold = 0; gold <= 600; gold += 7)
            {
                var bot = new BotController(profile, SampleVerticalSliceContent.CreepId);
                var state = new PlayerEconomyState(new PlayerId(2), new Gold(gold), new Income(60), new Lives(220));
                if (bot.Decide(state, content, new SimulationTick(300)).Command is not QueueSendCommand send)
                {
                    continue;
                }

                var creep = content.Creeps.First(c => c.Id.Equals(send.CreepId));
                Assert.True(creep.Support == CreepSupportRole.None && !creep.IgnoresMaze,
                    $"{profile} opened with {creep.Name} at {gold} gold, which needs a wall in front of it");
            }
        }
    }

    /// <summary>
    /// Having sent a wall, a bot will follow it with an escort — and only inside the window.
    /// </summary>
    /// <remarks>
    /// The other half. A gate that never opens would pass the test above while quietly removing
    /// five creeps from the game, which is worse than the behaviour it replaced.
    /// </remarks>
    [Fact]
    public void A_bot_follows_a_wall_with_an_escort_and_stops_once_the_window_closes()
    {
        var content = SampleVerticalSliceContent.Create();
        var inWindow = FollowUp(content, BotController.EscortFollowWindowTicks);
        var pastWindow = FollowUp(content, BotController.EscortFollowWindowTicks + 1);

        output.WriteLine($"follow-up inside the window: {inWindow}; past it: {pastWindow}");

        Assert.False(inWindow is null, "a bot that just sent a wall never followed it with anything");
        Assert.True(inWindow!.Support != CreepSupportRole.None || inWindow.IgnoresMaze,
            $"the follow-up was {inWindow.Name}, a wall — the escort was never reachable");
        Assert.True(pastWindow is null || pastWindow.Support == CreepSupportRole.None,
            $"{pastWindow?.Name} was sent {BotController.EscortFollowWindowTicks + 1} ticks after the wall, outside its own aura reach");
    }

    /// <summary>What a Greedy bot sends `delay` ticks after its first send, at ample gold.</summary>
    private static CreepDefinition? FollowUp(ContentCatalog content, int delay)
    {
        var bot = new BotController(BotDecisionProfile.Greedy, SampleVerticalSliceContent.CreepId);
        var state = new PlayerEconomyState(new PlayerId(2), new Gold(600), new Income(60), new Lives(220));

        bot.Decide(state, content, new SimulationTick(300));
        return bot.Decide(state, content, new SimulationTick(300 + delay)).Command is QueueSendCommand send
            ? content.Creeps.First(creep => creep.Id.Equals(send.CreepId))
            : null;
    }

    /// <summary>
    /// A bot never buys more than one of an aura creep at a time.
    /// </summary>
    /// <remarks>
    /// Auras do not stack, so a second Mender in the same send is the same effect at twice the
    /// price. The profiles otherwise batch up to three.
    /// </remarks>
    [Fact]
    public void A_bot_sends_aura_creeps_one_at_a_time()
    {
        var content = SampleVerticalSliceContent.Create();
        var bot = new BotController(BotDecisionProfile.Greedy, SampleVerticalSliceContent.CreepId);
        var state = new PlayerEconomyState(new PlayerId(2), new Gold(600), new Income(60), new Lives(220));

        for (var tick = 300; tick < 340; tick++)
        {
            if (bot.Decide(state, content, new SimulationTick(tick)).Command is not QueueSendCommand send)
            {
                continue;
            }

            var creep = content.Creeps.First(c => c.Id.Equals(send.CreepId));
            if (creep.Support != CreepSupportRole.None)
            {
                Assert.Equal(1, send.Quantity);
            }
        }
    }
}
