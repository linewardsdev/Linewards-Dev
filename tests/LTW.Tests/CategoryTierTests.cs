using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// The category upgrade tiers: what they cost, what they scale, and when.
/// </summary>
/// <remarks>
/// The two sides apply at deliberately different moments, and most of what can go wrong with this
/// feature is a timing question rather than an arithmetic one — so the "already on the board" cases
/// are tested as carefully as the multipliers.
/// </remarks>
public sealed class CategoryTierTests
{
    private const int Arcane = 0;
    private const int Core = 0;

    private static LocalVerticalSlice Slice() =>
        new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);

    private static PlayerEconomyState Player(LocalVerticalSlice slice, int playerId) =>
        slice.GetSnapshot().Players.Get(new PlayerId(playerId));

    [Fact]
    public void Every_category_starts_at_tier_one_and_costs_nothing()
    {
        var player = Player(Slice(), 1);

        for (var category = 0; category < PlayerEconomyState.CategoryCount; category++)
        {
            Assert.Equal(1, player.TowerLineTier(category));
            Assert.Equal(1, player.SendCategoryTier(category));
        }

        Assert.Equal(0, CategoryTierRules.CostFor(CategoryKind.TowerLine, 1));
        Assert.Equal(0, CategoryTierRules.CostFor(CategoryKind.SendCategory, 1));
    }

    [Fact]
    public void Buying_a_tier_deducts_exactly_its_authored_cost()
    {
        var slice = Slice();
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(1000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));
        var goldBefore = Player(slice, 1).Gold.Amount;

        var result = slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 2);

        Assert.True(result.Accepted);
        Assert.Equal(2, Player(slice, 1).TowerLineTier(Arcane));
        Assert.Equal(goldBefore - CategoryTierRules.CostFor(CategoryKind.TowerLine, 2), Player(slice, 1).Gold.Amount);
    }

    [Fact]
    public void Tiers_must_be_bought_in_order()
    {
        var slice = Slice();
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));

        // Skipping straight to 3 is refused even though the gold is plainly there, so the
        // escalating cost is actually paid rather than stepped over.
        var skipped = slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 3);
        Assert.False(skipped.Accepted);
        Assert.Equal(CommandRejectionReason.InvalidTier, skipped.RejectionReason);
        Assert.Equal(1, Player(slice, 1).TowerLineTier(Arcane));

        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 2).Accepted);
        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 3).Accepted);

        // And there is no tier 4.
        var past = slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 4);
        Assert.False(past.Accepted);
        Assert.Equal(CommandRejectionReason.InvalidTier, past.RejectionReason);
    }

    [Fact]
    public void Rebuying_the_tier_already_held_is_refused()
    {
        var slice = Slice();
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));
        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.SendCategory, Core, 2).Accepted);
        var goldAfterFirst = Player(slice, 1).Gold.Amount;

        var again = slice.BuyCategoryTier(new PlayerId(1), CategoryKind.SendCategory, Core, 2);

        Assert.False(again.Accepted);
        Assert.Equal(CommandRejectionReason.InvalidTier, again.RejectionReason);
        Assert.Equal(goldAfterFirst, Player(slice, 1).Gold.Amount);
    }

    [Fact]
    public void An_unaffordable_tier_is_refused_and_charges_nothing()
    {
        var slice = Slice();

        // Income is granted so that GOLD is the thing being tested. A starting player has income 10
        // against a tier-2 requirement of 60, so without this the purchase is refused for
        // InsufficientIncome and the gold path below is never reached — the test would still pass
        // its "refused and charges nothing" claim while asserting the wrong reason for it.
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));

        var goldBefore = Player(slice, 1).Gold.Amount;
        Assert.True(goldBefore < CategoryTierRules.CostFor(CategoryKind.SendCategory, 2));

        var result = slice.BuyCategoryTier(new PlayerId(1), CategoryKind.SendCategory, Core, 2);

        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.InsufficientGold, result.RejectionReason);
        Assert.Equal(goldBefore, Player(slice, 1).Gold.Amount);
        Assert.Equal(1, Player(slice, 1).SendCategoryTier(Core));
    }

    [Fact]
    public void A_category_index_outside_the_roster_is_refused()
    {
        var slice = Slice();
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));

        Assert.Equal(
            CommandRejectionReason.InvalidContentId,
            slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, 7, 2).RejectionReason);
        Assert.Equal(
            CommandRejectionReason.InvalidContentId,
            slice.BuyCategoryTier(new PlayerId(1), CategoryKind.SendCategory, -1, 2).RejectionReason);
    }

    [Fact]
    public void Each_category_upgrades_on_its_own()
    {
        var slice = Slice();
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));

        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 2).Accepted);

        var player = Player(slice, 1);
        Assert.Equal(2, player.TowerLineTier(Arcane));
        // Nothing else moved: not the other two lines, and not the send side at all.
        Assert.Equal(1, player.TowerLineTier(1));
        Assert.Equal(1, player.TowerLineTier(2));
        Assert.Equal(1, player.SendCategoryTier(Arcane));
    }

    [Fact]
    public void One_players_tiers_do_not_reach_another()
    {
        var slice = Slice();
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));

        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.TowerLine, Arcane, 2).Accepted);

        Assert.Equal(2, Player(slice, 1).TowerLineTier(Arcane));
        Assert.Equal(1, Player(slice, 2).TowerLineTier(Arcane));
    }

    [Fact]
    public void A_send_tier_scales_the_health_of_creeps_sent_after_it()
    {
        var slice = Slice();
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));

        Assert.True(slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.BruteCreepId, 1).Accepted);
        var baseline = slice.GetSnapshot().Creeps.Single().Health;

        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.SendCategory, Core, 2).Accepted);
        Assert.True(slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.BruteCreepId, 1).Accepted);

        var upgraded = slice.GetSnapshot().Creeps.OrderBy(creep => creep.EntityId.Value).Last().Health;
        var expected = CategoryTierRules.Scale(baseline, CategoryTierRules.CreepHealthPercentFor(2));
        Assert.Equal(expected, upgraded);
        Assert.True(upgraded > baseline);
    }

    /// <summary>
    /// The rule that stops a losing wave being rescued: a creep already walking keeps the health it
    /// was paid for.
    /// </summary>
    [Fact]
    public void A_send_tier_does_not_retroactively_heal_creeps_already_on_the_board()
    {
        var slice = Slice();
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));
        Assert.True(slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.BruteCreepId, 3).Accepted);

        var before = slice.GetSnapshot().Creeps
            .OrderBy(creep => creep.EntityId.Value)
            .Select(creep => (creep.EntityId.Value, creep.Health, creep.MaxHealth))
            .ToArray();
        Assert.NotEmpty(before);

        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.SendCategory, Core, 2).Accepted);
        slice.AdvanceOneTick();

        var after = slice.GetSnapshot().Creeps
            .Where(creep => before.Any(earlier => earlier.Value == creep.EntityId.Value))
            .OrderBy(creep => creep.EntityId.Value)
            .Select(creep => (creep.EntityId.Value, creep.Health, creep.MaxHealth))
            .ToArray();

        Assert.Equal(before.Length, after.Length);
        for (var index = 0; index < before.Length; index++)
        {
            // Health can only have gone DOWN (a tower may have hit it); it must never rise, and the
            // bar it is measured against must not move at all.
            Assert.True(after[index].Health <= before[index].Health);
            Assert.Equal(before[index].MaxHealth, after[index].MaxHealth);
        }
    }

    /// <summary>
    /// A creep's health bar is a fraction of what THAT creep spawned with, not of what its type is
    /// worth — otherwise a tier-3 creep renders as a 225%-full bar.
    /// </summary>
    [Fact]
    public void A_scaled_creep_reports_a_max_health_that_matches_what_it_spawned_with()
    {
        var slice = Slice();
        slice.GrantLocalPlaytestGold(new PlayerId(1), new Gold(5000));
        slice.GrantLocalPlaytestIncome(new PlayerId(1), new Income(1000));
        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.SendCategory, Core, 2).Accepted);
        Assert.True(slice.BuyCategoryTier(new PlayerId(1), CategoryKind.SendCategory, Core, 3).Accepted);
        Assert.True(slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.BruteCreepId, 1).Accepted);

        var creep = slice.GetSnapshot().Creeps.Single();
        var authored = SampleVerticalSliceContent.Create().Creeps
            .Single(definition => definition.Id.Equals(SampleVerticalSliceContent.BruteCreepId)).MaxHealth;

        Assert.Equal(creep.MaxHealth, creep.Health);
        Assert.True(creep.MaxHealth > authored);
        Assert.Equal(CategoryTierRules.Scale(authored, CategoryTierRules.CreepHealthPercentFor(3)), creep.MaxHealth);
    }

    [Fact]
    public void Tier_percentages_escalate_and_tier_one_is_neutral()
    {
        Assert.Equal(100, CategoryTierRules.TowerDamagePercentFor(1));
        Assert.Equal(100, CategoryTierRules.CreepHealthPercentFor(1));

        Assert.True(CategoryTierRules.TowerDamagePercentFor(2) > CategoryTierRules.TowerDamagePercentFor(1));
        Assert.True(CategoryTierRules.TowerDamagePercentFor(3) > CategoryTierRules.TowerDamagePercentFor(2));
        Assert.True(CategoryTierRules.CreepHealthPercentFor(2) > CategoryTierRules.CreepHealthPercentFor(1));
        Assert.True(CategoryTierRules.CreepHealthPercentFor(3) > CategoryTierRules.CreepHealthPercentFor(2));
    }

    /// <summary>
    /// A tower line multiplies every tower in it, forever; a send category only helps creeps bought
    /// afterwards. The cheaper of the two would therefore be the stronger one, so tower tiers are
    /// deliberately the dearer side — the design doc had this the other way round.
    /// </summary>
    [Fact]
    public void A_tower_tier_costs_more_than_the_send_tier_at_the_same_level()
    {
        for (var tier = 2; tier <= CategoryTierRules.MaxTier; tier++)
        {
            Assert.True(
                CategoryTierRules.CostFor(CategoryKind.TowerLine, tier) > CategoryTierRules.CostFor(CategoryKind.SendCategory, tier),
                $"tier {tier}: tower {CategoryTierRules.CostFor(CategoryKind.TowerLine, tier)} should exceed send {CategoryTierRules.CostFor(CategoryKind.SendCategory, tier)}");
        }
    }

    /// <summary>
    /// Each step costs roughly 2.5x the one before, so a fourth tier would not need a fresh
    /// judgement call about pricing.
    /// </summary>
    [Fact]
    public void Each_tier_costs_roughly_two_and_a_half_times_the_last()
    {
        foreach (var kind in new[] { CategoryKind.TowerLine, CategoryKind.SendCategory })
        {
            var second = CategoryTierRules.CostFor(kind, 2);
            var third = CategoryTierRules.CostFor(kind, 3);
            var ratio = third / (double)second;
            Assert.InRange(ratio, 2.2d, 2.8d);
        }
    }

    /// <summary>
    /// Attack scales harder than defence at full investment.
    /// </summary>
    /// <remarks>
    /// The design doc asks for this so a fully-invested attacker can get ahead of a fully-invested
    /// defender, which is what lets a match end. It is worth stating as a test because the
    /// consequence of getting it backwards is not "slightly off balance" — it is a game that
    /// cannot finish, which is how the first calibration of this feature failed.
    ///
    /// Note the magnitudes below are NOT the thing that decides whether matches close: measurement
    /// showed which side the BOTS spend their upgrade gold on dominates the multiplier entirely
    /// (see LocalVerticalSlice.BotTierPreference). This assertion guards the design intent; the
    /// bot preference guards the pacing.
    /// </remarks>
    [Fact]
    public void Attack_scales_harder_than_defence_at_full_investment()
    {
        Assert.True(
            CategoryTierRules.CreepHealthPercentFor(CategoryTierRules.MaxTier)
                > CategoryTierRules.TowerDamagePercentFor(CategoryTierRules.MaxTier),
            "attack must out-scale defence at full investment or a match cannot close");
    }

    /// <summary>
    /// Every tower on the roster is measurably better at max tier than at tier 1.
    /// </summary>
    /// <remarks>
    /// The reason this needs a test: damage is a small integer and the multiplier is capped, so
    /// truncation silently made a tier purchase worth NOTHING to the five 2-damage towers — a
    /// player could take ARCANE to the top and see their Arrow towers hit for exactly what they
    /// did before. Pins the rounding rule that fixed it.
    /// </remarks>
    [Fact]
    public void Every_tower_gains_damage_by_the_top_tier()
    {
        var catalog = SampleVerticalSliceContent.Create();
        var topPercent = CategoryTierRules.TowerDamagePercentFor(CategoryTierRules.MaxTier);

        foreach (var tower in catalog.Towers)
        {
            Assert.True(
                CategoryTierRules.Scale(tower.Damage, topPercent) > tower.Damage,
                $"{tower.Id.Value} deals {tower.Damage} and still {CategoryTierRules.Scale(tower.Damage, topPercent)} at the top tier — the upgrade does nothing for it");
        }
    }
}
