using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// Upgrading one placed tower: the payment that brings an existing tower up to the line tier its
/// owner has already bought.
/// </summary>
public sealed class TowerUpgradeTests
{
    private const int Arcane = 0;
    private static readonly PlayerId Player = new(1);
    private static readonly LaneId Lane = new(1);
    private static readonly GridPosition Cell = new(2, 4);

    private static LocalVerticalSlice Slice()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        slice.GrantLocalPlaytestGold(Player, new Gold(5000));
        return slice;
    }

    private static LocalVerticalSlice SliceWithArrow()
    {
        var slice = Slice();
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, Cell).Accepted);
        return slice;
    }

    private static int ArrowCost() => SampleVerticalSliceContent.Create().Towers
        .Single(tower => tower.Id.Equals(SampleVerticalSliceContent.TowerId)).Cost.Amount;

    [Fact]
    public void Upgrading_raises_that_towers_tier_and_charges_a_share_of_its_build_cost()
    {
        var slice = SliceWithArrow();
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Arcane, 2).Accepted);
        var goldBefore = slice.GetSnapshot().Players.Get(Player).Gold.Amount;
        var expected = CategoryTierRules.TowerUpgradeCost(ArrowCost());

        var result = slice.UpgradeTower(Player, Lane, Cell);

        Assert.True(result.Accepted);
        Assert.Equal(2, slice.GetSnapshot().Towers.Single().Tier);
        Assert.Equal(goldBefore - expected, slice.GetSnapshot().Players.Get(Player).Gold.Amount);
        Assert.True(expected > 0 && expected < ArrowCost(),
            $"upgrade should cost something but less than a rebuild: {expected} against a build price of {ArrowCost()}");
    }

    /// <summary>
    /// The line tier is the ceiling: the category purchase unlocks progression, the per-tower gold
    /// realises it.
    /// </summary>
    [Fact]
    public void A_tower_cannot_be_upgraded_past_its_owners_line_tier()
    {
        var slice = SliceWithArrow();

        // Line still at tier 1, so there is nothing to catch up to.
        var tooEarly = slice.UpgradeTower(Player, Lane, Cell);
        Assert.False(tooEarly.Accepted);
        Assert.Equal(CommandRejectionReason.InvalidTier, tooEarly.RejectionReason);
        Assert.Equal(1, slice.GetSnapshot().Towers.Single().Tier);

        // Buy tier 2: exactly one upgrade becomes available, and only one.
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Arcane, 2).Accepted);
        Assert.True(slice.UpgradeTower(Player, Lane, Cell).Accepted);
        Assert.Equal(2, slice.GetSnapshot().Towers.Single().Tier);

        var atCeiling = slice.UpgradeTower(Player, Lane, Cell);
        Assert.False(atCeiling.Accepted);
        Assert.Equal(CommandRejectionReason.InvalidTier, atCeiling.RejectionReason);
        Assert.Equal(2, slice.GetSnapshot().Towers.Single().Tier);
    }

    [Fact]
    public void Upgrading_stops_at_the_top_tier()
    {
        var slice = SliceWithArrow();
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Arcane, 2).Accepted);
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Arcane, 3).Accepted);

        Assert.True(slice.UpgradeTower(Player, Lane, Cell).Accepted);
        Assert.True(slice.UpgradeTower(Player, Lane, Cell).Accepted);
        Assert.Equal(CategoryTierRules.MaxTier, slice.GetSnapshot().Towers.Single().Tier);

        var past = slice.UpgradeTower(Player, Lane, Cell);
        Assert.False(past.Accepted);
        Assert.Equal(CommandRejectionReason.InvalidTier, past.RejectionReason);
    }

    [Fact]
    public void An_unaffordable_upgrade_is_refused_and_charges_nothing()
    {
        // Granted so that after building and buying the line tier the player is left just short of
        // the upgrade, computed from the rules rather than hardcoded so a balance change cannot
        // quietly turn this into a test of nothing.
        const int Foundry = 1;
        var catalog = SampleVerticalSliceContent.Create();
        var foundry = catalog.Towers.Single(tower => tower.Id.Value == "tower.foundry");
        var tierCost = CategoryTierRules.CostFor(CategoryKind.TowerLine, 2);
        var upgradeCost = CategoryTierRules.TowerUpgradeCost(foundry.Cost.Amount);

        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        var startingGold = slice.GetSnapshot().Players.Get(Player).Gold.Amount;
        slice.GrantLocalPlaytestGold(Player, new Gold(foundry.Cost.Amount + tierCost + upgradeCost - 1 - startingGold));

        Assert.True(slice.PlaceTower(Player, Lane, foundry.Id, Cell).Accepted);
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Foundry, 2).Accepted);

        var goldBefore = slice.GetSnapshot().Players.Get(Player).Gold.Amount;
        Assert.True(goldBefore < upgradeCost, $"setup should leave the player short: {goldBefore} against {upgradeCost}");

        var result = slice.UpgradeTower(Player, Lane, Cell);

        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.InsufficientGold, result.RejectionReason);
        Assert.Equal(goldBefore, slice.GetSnapshot().Players.Get(Player).Gold.Amount);
        Assert.Equal(1, slice.GetSnapshot().Towers.Single().Tier);
    }

    [Fact]
    public void Upgrading_an_empty_cell_or_another_players_tower_is_refused()
    {
        var slice = SliceWithArrow();

        var emptyCell = slice.UpgradeTower(Player, Lane, new GridPosition(5, 9));
        Assert.False(emptyCell.Accepted);
        Assert.Equal(CommandRejectionReason.NotOwner, emptyCell.RejectionReason);

        // Someone else's lane, where this player owns nothing.
        var foreign = slice.UpgradeTower(new PlayerId(2), Lane, Cell);
        Assert.False(foreign.Accepted);
        Assert.Equal(CommandRejectionReason.NotOwner, foreign.RejectionReason);
    }

    /// <summary>
    /// Upgrading one tower leaves its neighbours alone — that is the whole point of paying per
    /// tower rather than per line.
    /// </summary>
    [Fact]
    public void Upgrading_one_tower_does_not_touch_the_others()
    {
        var slice = Slice();
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, Cell).Accepted);
        Assert.True(slice.PlaceTower(Player, Lane, SampleVerticalSliceContent.TowerId, new GridPosition(4, 6)).Accepted);
        Assert.True(slice.BuyCategoryTier(Player, CategoryKind.TowerLine, Arcane, 2).Accepted);

        Assert.True(slice.UpgradeTower(Player, Lane, Cell).Accepted);

        var towers = slice.GetSnapshot().Towers.OrderBy(tower => tower.EntityId.Value).ToArray();
        Assert.Equal(2, towers.Length);
        Assert.Equal(2, towers.Single(tower => tower.Position.Equals(Cell)).Tier);
        Assert.Equal(1, towers.Single(tower => !tower.Position.Equals(Cell)).Tier);
    }

    /// <summary>
    /// Upgrading must beat selling and rebuilding, or the button has no reason to exist.
    /// </summary>
    [Fact]
    public void Upgrading_costs_less_than_rebuilding_at_the_higher_tier()
    {
        var catalog = SampleVerticalSliceContent.Create();
        foreach (var tower in catalog.Towers)
        {
            var upgrade = CategoryTierRules.TowerUpgradeCost(tower.Cost.Amount);
            Assert.True(
                upgrade < tower.Cost.Amount,
                $"{tower.Id.Value}: upgrading costs {upgrade} against a build price of {tower.Cost.Amount}, so rebuilding would be at least as good");
        }
    }
}
