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
    private static readonly GridPosition ControlCell = new(2, 8);

    private static LocalVerticalSlice Slice()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        slice.GrantLocalPlaytestGold(Player, new Gold(5000));
        slice.GrantLocalPlaytestIncome(Player, new Income(1000));
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, ArrowCell).Accepted);
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.PrismTowerId, PrismCell).Accepted);
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.ControlTowerId, ControlCell).Accepted);
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
    /// A selection cannot outrun its line's ceiling.
    /// </summary>
    /// <remarks>
    /// This used to span two lines — Arcane raised, Foundry not, so a Gatling in the selection had
    /// nowhere to go. The category lock (2026-08-08) made that board impossible: a seat commits to
    /// one line with its first tower, so no selection can ever contain two. The per-line ceiling
    /// logic it was defending is unchanged and still worth pinning — what changed is that a blocked
    /// tower can no longer be produced by mixing lines, so the ceiling is shown by running the batch
    /// twice: the second pass is the one that must find nothing left to buy.
    /// </remarks>
    [Fact]
    public void A_selection_obeys_its_own_lines_ceiling()
    {
        var slice = Slice();
        // Arcane raised to 2, so towers may reach tier 2 and no further.
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Arcane, 2).Accepted);
        var selection = new[] { ArrowCell, PrismCell, ControlCell };

        var first = slice.UpgradeTowers(Player, Lane, selection);
        var goldAtCeiling = Gold(slice);
        var second = slice.UpgradeTowers(Player, Lane, selection);

        Assert.Equal(3, first.Upgraded);
        Assert.Equal(2, TierOf(slice, ArrowCell));
        Assert.Equal(2, TierOf(slice, PrismCell));
        Assert.Equal(2, TierOf(slice, ControlCell));

        // The ceiling, which is the whole point: a second pass over a selection already sitting at
        // the line's tier finds nothing eligible, charges nothing, and moves nothing.
        Assert.Equal(0, second.Upgraded);
        Assert.Equal(0, second.Eligible);
        Assert.Equal(0, second.GoldSpent);
        Assert.Equal(goldAtCeiling, Gold(slice));
    }

    [Fact]
    public void Selling_a_selection_removes_them_all_and_refunds_the_quoted_total()
    {
        var slice = Slice();
        var selection = new[] { ArrowCell, ControlCell };
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

        slice.SellTowers(Player, Lane, new[] { ArrowCell, ControlCell });

        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, ArrowCell).Accepted);
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, ControlCell).Accepted);
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

        batched.SellTowers(Player, Lane, new[] { ArrowCell, ControlCell });
        Assert.True(byHand.SellTowerAt(Player, Lane, ArrowCell).Accepted);
        Assert.True(byHand.SellTowerAt(Player, Lane, ControlCell).Accepted);

        Assert.Equal(Gold(byHand), Gold(batched));
        Assert.Equal(byHand.GetSnapshot().Towers.Count, batched.GetSnapshot().Towers.Count);
    }
}
