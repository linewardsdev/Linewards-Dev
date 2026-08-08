using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// Guards the tower roster the build palette is built against.
/// </summary>
/// <remarks>
/// These exist because the client used to carry its own copy of every tower price and the copies
/// drifted: the palette buttons advertised 20 gold for the Arrow Tower, the selection readout said
/// 25, and the simulation charged 14. The client now reads costs from this catalog, so the catalog
/// is the only place a price lives — and the content ids below are the contract the client's
/// TowerCatalog keys off, so a renamed or dropped id must fail here rather than silently produce a
/// palette button that cannot place anything.
/// </remarks>
public class TowerRosterTests
{
    private static readonly string[] ExpectedTowerIds =
    {
        "tower.arrow", "tower.control", "tower.relay", "tower.pulse", "tower.prism",
        "tower.gatling", "tower.tesla", "tower.foundry", "tower.barricade", "tower.repair_drone",
        "tower.elder_canopy", "tower.sapling", "tower.bloomheart", "tower.thorn_snare", "tower.spore_cloud",
        // Roster expansion, ARCANE 6 of 8 (ROSTER_EXPANSION_PLAN.md A6).
        "tower.twin_crescent"
    };

    [Fact]
    public void Roster_contains_every_tower_the_build_palette_offers()
    {
        var catalog = SampleVerticalSliceContent.Create();
        var ids = catalog.Towers.Select(tower => tower.Id.Value).ToArray();

        Assert.Equal(ExpectedTowerIds.Length, catalog.Towers.Count);
        foreach (var expected in ExpectedTowerIds)
        {
            Assert.Contains(expected, ids);
        }
    }

    [Fact]
    public void Tower_ids_are_unique()
    {
        var catalog = SampleVerticalSliceContent.Create();
        var ids = catalog.Towers.Select(tower => tower.Id.Value).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    /// <summary>
    /// Every tower must be buildable and able to shoot. A zero cooldown would divide by zero in
    /// any damage-per-second reasoning, and a zero-range tower can never acquire a target.
    /// </summary>
    [Fact]
    public void Every_tower_has_usable_stats()
    {
        var catalog = SampleVerticalSliceContent.Create();

        foreach (var tower in catalog.Towers)
        {
            Assert.True(tower.Cost.Amount > 0, $"{tower.Id.Value} costs nothing");
            Assert.True(tower.RangeCells >= 1, $"{tower.Id.Value} has no range");
            Assert.True(tower.Damage > 0, $"{tower.Id.Value} deals no damage");
            Assert.True(tower.AttackCooldownTicks >= 1, $"{tower.Id.Value} has no cooldown");
        }
    }

    /// <summary>
    /// A player must be able to afford something on the opening hand, or the first turn is dead.
    /// </summary>
    [Fact]
    public void Cheapest_tower_is_affordable_from_starting_gold()
    {
        var catalog = SampleVerticalSliceContent.Create();
        var simulation = new LocalVerticalSlice(catalog, enableBots: false);
        var startingGold = simulation.GetSnapshot().Players.Get(simulation.LocalPlayerId).Gold.Amount;
        var cheapest = catalog.Towers.Min(tower => tower.Cost.Amount);

        Assert.True(cheapest <= startingGold, $"cheapest tower is {cheapest}g against {startingGold}g starting gold");
    }

    /// <summary>
    /// No tower may be strictly dominated on all four axes — same or better cost, range, damage and
    /// cooldown than another. A dominated tower is a palette slot nobody would ever press.
    /// </summary>
    [Fact]
    public void No_tower_is_strictly_dominated_by_another()
    {
        var catalog = SampleVerticalSliceContent.Create();

        // Two exemptions, both for the same reason: a payoff these four numbers cannot express.
        //
        // Relay Ward's stats are deliberately weak because it earns gold on every hit — authored
        // as TowerDefinition.SignalGoldPerHit, paid by LocalVerticalSlice.ApplySignalGold.
        //
        // Twin Crescent Ward IS dominated by Arrow on these axes, and that is the design rather
        // than an oversight: same range and damage, slower and dearer, because it fires TWICE per
        // cooldown at two different creeps (CombatService.TwinVolley). Against one creep Arrow
        // genuinely beats it — the intended trade — and against two it is 2.0 damage/tick to
        // Arrow's 1.5. The rule here exists to catch "a palette slot nobody would ever press",
        // and this is a slot pressed whenever chaff appears; only the four-axis proxy cannot see
        // it. Twin_crescent_beats_arrow_only_when_a_second_target_exists pins both directions.
        //
        // Worth watching: at two entries this list is still the exception. If it reaches four or
        // five, the check is measuring the wrong thing and should compare effective output rather
        // than authored stats.
        var exempt = new[] { "tower.relay", "tower.twin_crescent" };

        foreach (var tower in catalog.Towers)
        {
            if (exempt.Contains(tower.Id.Value))
            {
                continue;
            }

            foreach (var other in catalog.Towers)
            {
                if (ReferenceEquals(tower, other))
                {
                    continue;
                }

                var dominated =
                    other.Cost.Amount <= tower.Cost.Amount &&
                    other.RangeCells >= tower.RangeCells &&
                    other.Damage >= tower.Damage &&
                    other.AttackCooldownTicks <= tower.AttackCooldownTicks;

                var strictly = dominated && (
                    other.Cost.Amount < tower.Cost.Amount ||
                    other.RangeCells > tower.RangeCells ||
                    other.Damage > tower.Damage ||
                    other.AttackCooldownTicks < tower.AttackCooldownTicks);

                Assert.False(strictly, $"{tower.Id.Value} is strictly dominated by {other.Id.Value}");
            }
        }
    }
}
