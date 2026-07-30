using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Checks whether the Repair Drone Spire's +1 range to neighbours does anything, and whether it does
/// so much that buying one is never wrong.
/// </summary>
/// <remarks>
/// Two separate questions, and they need different measurements.
///
/// 1. IS IT DECORATION? Same method as BloomheartDivergenceTests: run the scenario twice against
///    bit-identical state, once with the real drone and once with a stat-identical baseline under an id
///    the mechanic does not match, and compare what the NEIGHBOUR achieves. A strictly-additive buff
///    should pass this easily — that is not the interesting part.
///
/// 2. IS IT A MANDATORY BUY? This is the real risk, and no domination test catches it: a tower everyone
///    builds looks fine on every stat axis. The honest test is opportunity cost — does spending gold on a
///    drone beat spending the same gold on more damage? If the drone always wins, it is mandatory. If
///    plain damage wins at equal gold, buying one is a genuine choice.
/// </remarks>
public sealed class RepairDroneValueTests
{
    private static readonly LaneId Lane = new(1);
    private static readonly PlayerId Defender = new(1);
    private static readonly PlayerId Attacker = new(2);
    private static readonly ContentId DroneId = new("tower.repair_drone");

    /// <summary>The drone's stats under an id the range buff does not match.</summary>
    private static readonly ContentId BaselineId = new("tower.baseline_spire");

    private readonly ITestOutputHelper output;

    public RepairDroneValueTests(ITestOutputHelper output) => this.output = output;

    private static ContentCatalog Catalog()
    {
        var real = SampleVerticalSliceContent.Create();
        var drone = real.Towers.Single(tower => tower.Id.Equals(DroneId));
        var baseline = new TowerDefinition(
            BaselineId,
            "Baseline Spire",
            drone.Cost,
            drone.RangeCells,
            drone.Damage,
            drone.AttackCooldownTicks);

        return new ContentCatalog(
            real.Version,
            real.Towers.Concat(new[] { baseline }).ToArray(),
            real.Creeps,
            real.Techs,
            real.Maps,
            real.BotProfiles);
    }

    private static IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> Routes() =>
        new Dictionary<LaneId, IReadOnlyList<GridPosition>>
        {
            [Lane] = Enumerable.Range(0, 18).Select(y => new GridPosition(3, y)).ToArray()
        };

    private sealed record Placement(string TowerId, int X, int Y);

    /// <summary>
    /// Runs a fixed creep stream against a set of towers and returns total damage dealt and total gold
    /// spent, so bundles of different composition can be compared per gold.
    /// </summary>
    private static (int Damage, int Gold, int DamageByFirst) Run(IReadOnlyList<Placement> placements, string creepId, int creepCount)
    {
        var catalog = Catalog();
        var service = new CombatService();
        var content = new CombatContent(
            catalog.Creeps,
            catalog.Towers,
            new Dictionary<LaneId, PlayerId> { [Lane] = Defender });
        var routes = Routes();
        var definition = catalog.Creeps.Single(creep => creep.Id.Value == creepId);

        var towers = placements
            .Select((placement, index) => new TowerCombatState(
                new EntityId(100 + index),
                new ContentId(placement.TowerId),
                Defender,
                Lane,
                new GridPosition(placement.X, placement.Y)))
            .ToArray();

        var gold = placements.Sum(placement =>
            catalog.Towers.Single(tower => tower.Id.Value == placement.TowerId).Cost.Amount);

        var state = new CombatState(System.Array.Empty<CreepCombatState>(), towers);
        var damage = 0;
        var damageByFirst = 0;
        var nextCreep = 1;

        for (var tick = 0; tick < 200; tick++)
        {
            // Trickle so creeps pass the towers one at a time rather than arriving as one stack, which
            // is the case a range buff most plausibly helps with.
            if (tick % 4 == 0 && nextCreep <= creepCount)
            {
                state = new CombatState(
                    state.Creeps.Concat(new[] { service.SpawnCreep(new EntityId(nextCreep++), definition, Attacker, Lane) }),
                    state.Towers);
            }

            var result = service.Advance(state, content, routes, new SimulationTick(tick));
            state = result.State;

            foreach (var damaged in result.Events.OfType<CreepDamagedEvent>())
            {
                damage += damaged.DamageDealt;
                if (damaged.TowerEntityId.Equals(new EntityId(100)))
                {
                    damageByFirst += damaged.DamageDealt;
                }
            }
        }

        return (damage, gold, damageByFirst);
    }

    // ---- Question 1: does the buff do anything at all? ----------------------------------------

    /// <summary>
    /// The buff must move the neighbour's output by a MEANINGFUL amount, not merely a measurable one.
    /// </summary>
    /// <remarks>
    /// The bar is deliberately a ratio rather than "greater than zero". The previous +1 range version
    /// passed a greater-than-zero check while contributing 2 damage out of 48 — one extra shot across
    /// twelve creeps — which is decoration that happens to be non-zero. A support tower that cannot move
    /// its neighbour by a fifth is not doing anything a player would notice.
    /// </remarks>
    [Fact]
    public void Repair_drone_meaningfully_increases_what_an_adjacent_tower_achieves()
    {
        // Entity 100 is the Arrow, so DamageByFirst isolates the NEIGHBOUR's output — the drone's own
        // damage is excluded and cannot flatter the result.
        var withDrone = Run(
            new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.repair_drone", 1, 8) },
            "creep.colossus",
            creepCount: 12);
        var withBaseline = Run(
            new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.baseline_spire", 1, 8) },
            "creep.colossus",
            creepCount: 12);

        Assert.True(withDrone.DamageByFirst > 0, "the neighbour never fired, so nothing was measured");
        var gain = (withDrone.DamageByFirst - withBaseline.DamageByFirst) / (double)withBaseline.DamageByFirst;
        Assert.True(
            gain >= 0.2d,
            $"drone gave the neighbour {withDrone.DamageByFirst} against the baseline's {withBaseline.DamageByFirst} "
            + $"({gain:P0}) — too small for a player to notice");
    }

    // ---- Question 2: is it a mandatory buy? --------------------------------------------------

    [Fact]
    public void Report_repair_drone_value_against_equal_gold_spent_on_damage()
    {
        var bundles = new (string Name, Placement[] Placements)[]
        {
            ("arrow alone", new[] { new Placement("tower.arrow", 2, 8) }),
            ("arrow + drone", new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.repair_drone", 1, 8) }),
            ("arrow + baseline spire", new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.baseline_spire", 1, 8) }),
            ("arrow x2", new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.arrow", 1, 8) }),
            ("arrow x3", new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.arrow", 1, 8), new Placement("tower.arrow", 2, 10) }),
            ("arrow + gatling", new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.gatling", 1, 8) }),
            ("drone + two arrows", new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.arrow", 2, 10), new Placement("tower.repair_drone", 1, 8) }),
        };

        output.WriteLine($"{"bundle",-24} {"gold",5} {"damage",7} {"dmg/gold",9} {"neighbour",10}");
        foreach (var (name, placements) in bundles)
        {
            var result = Run(placements, "creep.colossus", creepCount: 12);
            output.WriteLine($"{name,-24} {result.Gold,5} {result.Damage,7} {result.Damage / (double)result.Gold,9:F2} {result.DamageByFirst,10}");
        }
    }

    /// <summary>
    /// The mandatory-buy check. Spending gold on a drone must not beat spending comparable gold on
    /// plain damage, or every build takes one and the choice is fake.
    /// </summary>
    /// <remarks>
    /// Compared per gold rather than in absolute damage, because the drone costs 34 and an Arrow costs
    /// 14 — an absolute comparison would just reward whichever bundle spent more.
    /// </remarks>
    [Fact]
    public void Buying_a_drone_does_not_beat_spending_the_same_gold_on_damage()
    {
        var drone = Run(
            new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.repair_drone", 1, 8) },
            "creep.colossus",
            creepCount: 12);
        var moreDamage = Run(
            new[] { new Placement("tower.arrow", 2, 8), new Placement("tower.arrow", 1, 8), new Placement("tower.arrow", 2, 10) },
            "creep.colossus",
            creepCount: 12);

        var dronePerGold = drone.Damage / (double)drone.Gold;
        var damagePerGold = moreDamage.Damage / (double)moreDamage.Gold;

        Assert.True(
            damagePerGold >= dronePerGold,
            $"a drone bundle returns {dronePerGold:F2} damage per gold against {damagePerGold:F2} for plain damage, "
            + "so buying one is strictly correct and the tower is a mandatory pick");
    }
}
