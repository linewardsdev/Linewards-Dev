using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// Raising a whole tower line in one action: the batch behind the category card's "upgrade all",
/// which spends as far as the player's gold reaches rather than refusing what it cannot complete.
/// </summary>
public sealed class TowerLineUpgradeTests
{
    private const int Arcane = 0;
    private const int Foundry = 1;
    private const int TierTwoCost = 140;
    private static readonly PlayerId Player = new(1);
    private static readonly LaneId Lane = new(1);
    private static readonly GridPosition ArrowCell = new(2, 4);
    private static readonly GridPosition PrismCell = new(4, 6);
    private static readonly GridPosition GatlingCell = new(2, 8);

    private static int CostOf(ContentId id) => SampleVerticalSliceContent.Create().Towers
        .Single(tower => tower.Id.Equals(id)).Cost.Amount;

    private static int UpgradeCostOf(ContentId id) => CategoryTierRules.TowerUpgradeCost(CostOf(id));

    private static int Gold(LocalVerticalSlice slice) => slice.GetSnapshot().Players.Get(Player).Gold.Amount;

    private static int TierOf(LocalVerticalSlice slice, GridPosition cell) =>
        slice.GetSnapshot().Towers.Single(tower => tower.Position.Equals(cell)).Tier;

    /// <summary>
    /// An Arcane Arrow and Prism plus a Foundry Gatling, Arcane raised to tier 2, and the player
    /// left holding EXACTLY <paramref name="goldAfterSetup"/>.
    /// </summary>
    /// <remarks>
    /// Gold is dialled in by computing the grant rather than by spending down to it, because
    /// GrantLocalPlaytestGold only ever adds. Setting the post-setup balance precisely is the whole
    /// point: the affordability behaviour under test is invisible unless the budget is exact.
    /// </remarks>
    private static LocalVerticalSlice Slice(int goldAfterSetup)
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);

        var spend = CostOf(SampleVerticalSliceContent.TowerId)
            + CostOf(SampleVerticalSliceContent.PrismTowerId)
            + CostOf(SampleVerticalSliceContent.GatlingTowerId)
            + TierTwoCost;
        var grant = goldAfterSetup + spend - Gold(slice);
        Assert.True(grant > 0, $"setup needs a positive grant, got {grant}");
        slice.GrantLocalPlaytestGold(Player, new Gold(grant));
        slice.GrantLocalPlaytestIncome(Player, new Income(1000));

        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, ArrowCell).Accepted);
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.PrismTowerId, PrismCell).Accepted);
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.GatlingTowerId, GatlingCell).Accepted);
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Arcane, 2).Accepted);

        Assert.Equal(goldAfterSetup, Gold(slice));
        return slice;
    }

    [Fact]
    public void Raising_a_line_upgrades_every_eligible_tower_and_charges_the_sum()
    {
        var expected = UpgradeCostOf(SampleVerticalSliceContent.TowerId) + UpgradeCostOf(SampleVerticalSliceContent.PrismTowerId);
        var slice = Slice(goldAfterSetup: 5000);

        var outcome = slice.UpgradeTowerLine(Player, Lane, Arcane);

        Assert.Equal(2, outcome.Upgraded);
        Assert.Equal(2, outcome.Eligible);
        Assert.Equal(expected, outcome.GoldSpent);
        Assert.Equal(5000 - expected, Gold(slice));
        Assert.Equal(2, TierOf(slice, ArrowCell));
        Assert.Equal(2, TierOf(slice, PrismCell));
    }

    [Fact]
    public void Raising_a_line_leaves_the_other_lines_alone()
    {
        var slice = Slice(goldAfterSetup: 5000);

        slice.UpgradeTowerLine(Player, Lane, Arcane);

        Assert.Equal(1, TierOf(slice, GatlingCell));
    }

    /// <summary>
    /// The behaviour chosen over refusing the batch: spend what there is, report what it bought.
    /// </summary>
    [Fact]
    public void Raising_a_line_spends_as_far_as_the_gold_reaches_taking_the_cheapest_first()
    {
        var arrow = UpgradeCostOf(SampleVerticalSliceContent.TowerId);
        var prism = UpgradeCostOf(SampleVerticalSliceContent.PrismTowerId);
        Assert.True(arrow < prism, $"this test needs Arrow cheaper than Prism, got {arrow} and {prism}");

        // Enough for the cheaper one only.
        var slice = Slice(goldAfterSetup: prism - 1);

        var outcome = slice.UpgradeTowerLine(Player, Lane, Arcane);

        Assert.Equal(1, outcome.Upgraded);
        Assert.Equal(2, outcome.Eligible);
        Assert.True(outcome.IsPartial);
        Assert.Equal(arrow, outcome.GoldSpent);
        Assert.Equal(2, TierOf(slice, ArrowCell));
        Assert.Equal(1, TierOf(slice, PrismCell));
    }

    [Fact]
    public void Nothing_is_charged_for_towers_already_at_the_lines_ceiling()
    {
        var slice = Slice(goldAfterSetup: 5000);
        slice.UpgradeTowerLine(Player, Lane, Arcane);
        var goldAfterFirst = Gold(slice);

        var outcome = slice.UpgradeTowerLine(Player, Lane, Arcane);

        Assert.Equal(0, outcome.Upgraded);
        Assert.Equal(0, outcome.Eligible);
        Assert.Equal(0, outcome.GoldSpent);
        Assert.Equal(goldAfterFirst, Gold(slice));
    }

    /// <summary>
    /// The card prices the batch from the quote and the batch charges from its own walk, so a
    /// disagreement between them would be a price that changes at the moment of paying.
    /// </summary>
    [Fact]
    public void The_quote_matches_what_the_batch_then_charges()
    {
        var slice = Slice(goldAfterSetup: 5000);

        var quote = slice.QuoteTowerLineUpgrade(Player, Lane, Arcane);
        var outcome = slice.UpgradeTowerLine(Player, Lane, Arcane);

        Assert.Equal(quote.Eligible, outcome.Eligible);
        Assert.Equal(quote.TotalCost, outcome.GoldSpent);
        Assert.Equal(quote.Affordable, outcome.Upgraded);
        Assert.Equal(quote.AffordableCost, outcome.GoldSpent);
        Assert.False(quote.IsGoldLimited);
    }

    [Fact]
    public void A_gold_limited_quote_predicts_the_partial_result_exactly()
    {
        var prism = UpgradeCostOf(SampleVerticalSliceContent.PrismTowerId);
        var slice = Slice(goldAfterSetup: prism - 1);

        var quote = slice.QuoteTowerLineUpgrade(Player, Lane, Arcane);
        Assert.True(quote.IsGoldLimited);

        var outcome = slice.UpgradeTowerLine(Player, Lane, Arcane);

        Assert.Equal(quote.Affordable, outcome.Upgraded);
        Assert.Equal(quote.AffordableCost, outcome.GoldSpent);
    }

    /// <summary>
    /// A batch is a shorthand for the taps it stands in for, so it must land the player in exactly
    /// the state that tapping each tower would have.
    /// </summary>
    [Fact]
    public void A_batch_lands_the_same_state_as_upgrading_each_tower_by_hand()
    {
        var batched = Slice(goldAfterSetup: 5000);
        var byHand = Slice(goldAfterSetup: 5000);

        batched.UpgradeTowerLine(Player, Lane, Arcane);
        Assert.True(byHand.UpgradeTower(Player, Lane, ArrowCell).Accepted);
        Assert.True(byHand.UpgradeTower(Player, Lane, PrismCell).Accepted);

        Assert.Equal(Gold(byHand), Gold(batched));
        Assert.Equal(TierOf(byHand, ArrowCell), TierOf(batched, ArrowCell));
        Assert.Equal(TierOf(byHand, PrismCell), TierOf(batched, PrismCell));
        Assert.Equal(TierOf(byHand, GatlingCell), TierOf(batched, GatlingCell));
    }
}
