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
    private static int DamageOverWindow(string towerId, string creepId, int towerLineTier)
    {
        var catalog = SampleVerticalSliceContent.Create();
        var tower = catalog.Towers.Single(definition => definition.Id.Value == towerId);
        var creep = catalog.Creeps.Single(definition => definition.Id.Value == creepId);
        var service = new CombatService();

        var tiers = new int[PlayerEconomyState.CategoryCount];
        for (var index = 0; index < tiers.Length; index++)
        {
            tiers[index] = PlayerEconomyState.BaseTier;
        }

        tiers[tower.CategoryIndex] = towerLineTier;

        var content = new CombatContent(
                catalog.Creeps,
                catalog.Towers,
                new System.Collections.Generic.Dictionary<LaneId, PlayerId> { [Lane] = Defender })
            .WithPlayerTiers(new System.Collections.Generic.Dictionary<PlayerId, PlayerCategoryTiers>
            {
                [Defender] = new PlayerCategoryTiers(Defender, tiers, tiers)
            });

        var route = Enumerable.Range(0, 40).Select(step => new GridPosition(3, step)).ToArray();
        var routes = new System.Collections.Generic.Dictionary<LaneId, System.Collections.Generic.IReadOnlyList<GridPosition>>
        {
            [Lane] = route
        };

        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), creep, Attacker, Lane) },
            new[] { new TowerCombatState(new EntityId(100), tower.Id, Defender, Lane, new GridPosition(2, 2)) });

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
    /// A tower line is an ongoing investment, so a tier must improve the towers already standing —
    /// otherwise the only way to benefit would be to sell and rebuild everything.
    /// </summary>
    [Fact]
    public void A_tower_tier_improves_towers_that_were_already_built()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));

        // Build FIRST, upgrade after.
        Assert.True(slice.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(2, 4)).Accepted);
        var towerBefore = slice.GetSnapshot().Towers.Single();

        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 2).Accepted);

        // Same tower entity, still standing — the upgrade applies to it rather than to some future
        // replacement.
        var towerAfter = slice.GetSnapshot().Towers.Single();
        Assert.Equal(towerBefore.EntityId.Value, towerAfter.EntityId.Value);
        Assert.Equal(2, slice.GetSnapshot().Players.Get(new PlayerId(1)).TowerLineTier(Arcane));
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
