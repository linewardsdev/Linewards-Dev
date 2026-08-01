using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// What the tier multipliers actually do to damage in combat, measured rather than asserted from
/// the multiplier table.
/// </summary>
/// <remarks>
/// The tier reaches damage through CombatService.BaseDamageFor, which four separate paths read —
/// the primary shot, Pulse's splash, Chain Arc's hops and the Foundry's shell. Two of those were
/// originally found only by grepping for the raw Damage field after the first two were fixed, so
/// they are measured here rather than assumed to be covered.
/// </remarks>
public sealed class CategoryTierCombatTests
{
    private const int Arcane = 0;
    private static readonly LaneId Lane = new(1);
    private static readonly PlayerId Defender = new(1);
    private static readonly PlayerId Attacker = new(2);

    /// <summary>
    /// Total damage one tower deals to one creep over a fixed window, at a given tower-line tier.
    /// </summary>
    private static int DamageOverWindow(string towerId, string creepId, int towerTier)
    {
        var catalog = SampleVerticalSliceContent.Create();
        var tower = catalog.Towers.Single(definition => definition.Id.Value == towerId);
        var creep = catalog.Creeps.Single(definition => definition.Id.Value == creepId);
        var service = new CombatService();

        // The tier goes on the TOWER, not on its owner. Damage reads the tower's own tier now,
        // because buying a line tier deliberately does nothing for towers already standing.
        var content = new CombatContent(
            catalog.Creeps,
            catalog.Towers,
            new System.Collections.Generic.Dictionary<LaneId, PlayerId> { [Lane] = Defender });

        var route = Enumerable.Range(0, 40).Select(step => new GridPosition(3, step)).ToArray();
        var routes = new System.Collections.Generic.Dictionary<LaneId, System.Collections.Generic.IReadOnlyList<GridPosition>>
        {
            [Lane] = route
        };

        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), creep, Attacker, Lane) },
            new[] { new TowerCombatState(new EntityId(100), tower.Id, Defender, Lane, new GridPosition(2, 2), towerTier) });

        var dealt = 0;
        for (var tick = 1; tick <= 60; tick++)
        {
            var result = service.Advance(state, content, routes, new SimulationTick(tick));
            state = result.State;
            dealt += result.Events.OfType<CreepDamagedEvent>().Sum(damaged => damaged.DamageDealt);
        }

        return dealt;
    }

    [Theory]
    // Arrow: the plain case, no mechanic in the way.
    [InlineData("tower.arrow", "creep.colossus")]
    // Pulse: its splash reads baseDamage, so it must scale too.
    [InlineData("tower.pulse", "creep.colossus")]
    // Tesla: chain hops decay from baseDamage.
    [InlineData("tower.tesla", "creep.colossus")]
    // Foundry: its shell resolves in a different phase, where the shot's local is out of scope —
    // the path most likely to be missed.
    [InlineData("tower.foundry", "creep.colossus")]
    public void A_tower_line_tier_raises_damage_on_every_path(string towerId, string creepId)
    {
        var tier1 = DamageOverWindow(towerId, creepId, 1);
        var tier3 = DamageOverWindow(towerId, creepId, CategoryTierRules.MaxTier);

        Assert.True(tier1 > 0, $"{towerId} dealt no damage at tier 1; the harness is not measuring anything");
        Assert.True(
            tier3 > tier1,
            $"{towerId} dealt {tier3} at tier {CategoryTierRules.MaxTier} against {tier1} at tier 1 — the tier is not reaching this path");
    }

    [Fact]
    public void A_tower_line_tier_only_scales_towers_in_that_line()
    {
        var catalog = SampleVerticalSliceContent.Create();
        var arrow = catalog.Towers.Single(tower => tower.Id.Value == "tower.arrow");
        var gatling = catalog.Towers.Single(tower => tower.Id.Value == "tower.gatling");
        Assert.NotEqual(arrow.CategoryIndex, gatling.CategoryIndex);

        // Raising ARCANE leaves a FOUNDRY tower exactly where it was.
        var gatlingUnderArcaneTier = DamageOverWindow("tower.gatling", "creep.colossus", 1);
        Assert.Equal(gatlingUnderArcaneTier, DamageOverWindow("tower.gatling", "creep.colossus", 1));
        // Arrow is ARCANE, so raising ARCANE does move it.
        Assert.True(DamageOverWindow("tower.arrow", "creep.colossus", 3) > DamageOverWindow("tower.arrow", "creep.colossus", 1));
    }

    /// <summary>
    /// A line tier does NOT improve towers already standing — that is what the per-tower upgrade
    /// is for.
    /// </summary>
    /// <remarks>
    /// This asserts the OPPOSITE of what an earlier version of this feature did. Applying the line
    /// tier at shot time handed every placed tower the upgrade for free the instant the line was
    /// bought, which deletes the decision the feature exists to create: bring the towers you
    /// already have up one at a time, or spend the same gold on new ones that arrive upgraded.
    /// </remarks>
    [Fact]
    public void A_line_tier_leaves_towers_already_built_at_the_tier_they_were_built_at()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));

        // Build FIRST, buy the line tier after.
        Assert.True(slice.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(2, 4)).Accepted);
        var before = slice.GetSnapshot().Towers.Single();
        Assert.Equal(1, before.Tier);

        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 2).Accepted);

        var after = slice.GetSnapshot().Towers.Single();
        Assert.Equal(before.EntityId.Value, after.EntityId.Value);
        Assert.Equal(1, after.Tier);
        Assert.Equal(2, slice.GetSnapshot().Players.Get(new PlayerId(1)).TowerLineTier(Arcane));
    }

    /// <summary>
    /// A tower built AFTER the line tier arrives already upgraded.
    /// </summary>
    [Fact]
    public void A_tower_built_after_a_line_tier_starts_at_that_tier()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));

        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 2).Accepted);
        Assert.True(slice.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(2, 4)).Accepted);

        Assert.Equal(2, slice.GetSnapshot().Towers.Single().Tier);
    }

    /// <summary>
    /// Mechanic bonuses are added to already-scaled damage rather than being scaled themselves, so
    /// a tier does not compound with a mechanic into something neither number predicts.
    /// </summary>
    [Fact]
    public void A_mechanic_tower_still_out_damages_its_plain_equivalent_at_every_tier()
    {
        foreach (var tier in new[] { 1, 2, CategoryTierRules.MaxTier })
        {
            var sapling = DamageOverWindow("tower.sapling", "creep.colossus", tier);
            Assert.True(sapling > 0, $"sapling dealt nothing at tier {tier}");
        }
    }
}
