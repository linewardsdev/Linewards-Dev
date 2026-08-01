using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// Acting on a hand-picked set of towers: the batch behind multi-select's upgrade and sell.
/// </summary>
public sealed class TowerSelectionBatchTests
{
    private const int Arcane = 0;
    private const int Foundry = 1;
    private static readonly PlayerId Player = new(1);
    private static readonly LaneId Lane = new(1);
    private static readonly GridPosition ArrowCell = new(2, 4);
    private static readonly GridPosition PrismCell = new(4, 6);
    private static readonly GridPosition GatlingCell = new(2, 8);

    private static LocalVerticalSlice Slice()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        slice.GrantLocalPlaytestGold(Player, new Gold(5000));
        slice.GrantLocalPlaytestIncome(Player, new Income(1000));
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, ArrowCell).Accepted);
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.PrismTowerId, PrismCell).Accepted);
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.GatlingTowerId, GatlingCell).Accepted);
        return slice;
    }

    private static int Gold(LocalVerticalSlice slice) => slice.GetSnapshot().Players.Get(Player).Gold.Amount;

    private static int TierOf(LocalVerticalSlice slice, GridPosition cell) =>
        slice.GetSnapshot().Towers.Single(tower => tower.Position.Equals(cell)).Tier;

    [Fact]
    public void Raising_a_selection_touches_only_the_selected_towers()
    {
        var slice = Slice();
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Arcane, 2).Accepted);

        var outcome = slice.UpgradeTowers(Player, Lane, new[] { ArrowCell });

        Assert.Equal(1, outcome.Upgraded);
        Assert.Equal(2, TierOf(slice, ArrowCell));
        Assert.Equal(1, TierOf(slice, PrismCell));
    }

    /// <summary>
    /// The case the whole-line version could never produce: one selection, two ceilings.
    /// </summary>
    [Fact]
    public void A_selection_spanning_two_lines_obeys_each_lines_own_ceiling()
    {
        var slice = Slice();
        // Arcane is raised, Foundry is not — so the Gatling has nowhere to go.
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Arcane, 2).Accepted);

        var outcome = slice.UpgradeTowers(Player, Lane, new[] { ArrowCell, PrismCell, GatlingCell });

        Assert.Equal(2, outcome.Upgraded);
        Assert.Equal(2, outcome.Eligible);
        Assert.Equal(2, TierOf(slice, ArrowCell));
        Assert.Equal(2, TierOf(slice, PrismCell));
        Assert.Equal(1, TierOf(slice, GatlingCell));
    }

    [Fact]
    public void Selling_a_selection_removes_them_all_and_refunds_the_quoted_total()
    {
        var slice = Slice();
        var selection = new[] { ArrowCell, GatlingCell };
        var quote = slice.QuoteTowerSales(Player, Lane, selection);
        var goldBefore = Gold(slice);

        var outcome = slice.SellTowers(Player, Lane, selection);

        Assert.Equal(2, quote.Towers);
        Assert.Equal(2, outcome.Sold);
        Assert.Equal(quote.Refund, outcome.Refund);
        Assert.Equal(goldBefore + quote.Refund, Gold(slice));
        Assert.Single(slice.GetSnapshot().Towers);
        Assert.Equal(PrismCell, slice.GetSnapshot().Towers.Single().Position);
    }

    /// <summary>
    /// Each sale reopens its cell, so a sold cell must be buildable again immediately.
    /// </summary>
    [Fact]
    public void Selling_a_selection_frees_the_cells_it_cleared()
    {
        var slice = Slice();

        slice.SellTowers(Player, Lane, new[] { ArrowCell, GatlingCell });

        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, ArrowCell).Accepted);
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, GatlingCell).Accepted);
    }

    [Fact]
    public void A_selection_ignores_cells_with_nothing_on_them()
    {
        var slice = Slice();
        var empty = new GridPosition(5, 10);

        var quote = slice.QuoteTowerSales(Player, Lane, new[] { ArrowCell, empty });
        var outcome = slice.SellTowers(Player, Lane, new[] { ArrowCell, empty });

        Assert.Equal(1, quote.Towers);
        Assert.Equal(1, outcome.Sold);
    }

    /// <summary>
    /// A batch is shorthand for the taps it replaces, so it has to land the same state.
    /// </summary>
    [Fact]
    public void Selling_a_selection_matches_selling_each_by_hand()
    {
        var batched = Slice();
        var byHand = Slice();

        batched.SellTowers(Player, Lane, new[] { ArrowCell, GatlingCell });
        Assert.True(byHand.SellTowerAt(Player, Lane, ArrowCell).Accepted);
        Assert.True(byHand.SellTowerAt(Player, Lane, GatlingCell).Accepted);

        Assert.Equal(Gold(byHand), Gold(batched));
        Assert.Equal(byHand.GetSnapshot().Towers.Count, batched.GetSnapshot().Towers.Count);
    }
}
