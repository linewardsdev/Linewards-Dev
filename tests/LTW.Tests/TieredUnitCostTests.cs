using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// That a category tier raises what its own units cost, not just what the tier cost.
/// </summary>
/// <remarks>
/// The tier was originally a one-off purchase that made everything in its category permanently
/// better at no change in price: a tier-3 send category put 225% health on the board for the gold
/// that bought 100%, and a tier-3 tower line built 190% damage for the price of 100%. Every send
/// and every build after the tier was free power, which made the tier's own price the entire
/// balancing lever and a strictly correct purchase the moment it was affordable.
///
/// Charged at 65% of the power increase, so the tier is still worth buying — a tier-3 creep carries
/// 225% health for 181% price, which is 24% more health per gold — while the free ride ends.
/// </remarks>
public sealed class TieredUnitCostTests
{
    private readonly ITestOutputHelper output;

    public TieredUnitCostTests(ITestOutputHelper output) => this.output = output;

    private static readonly PlayerId Seat = new(1);

    private static LocalVerticalSlice Slice()
    {
        var slice = new LocalVerticalSlice(
            SampleVerticalSliceContent.Create(),
            new LocalMatchOptions(seed: 1, laneCount: LocalMatchOptions.MaxLaneCount),
            enableBots: false);
        slice.GrantLocalPlaytestGold(Seat, new Gold(200_000));
        slice.GrantLocalPlaytestIncome(Seat, new Income(900));
        return slice;
    }

    private static int Gold(LocalVerticalSlice slice) =>
        slice.GetSnapshot().Players.Get(Seat).Gold.Amount;

    [Fact]
    public void The_cost_table_charges_part_of_the_power_increase_and_never_all_of_it()
    {
        for (var tier = 1; tier <= CategoryTierRules.MaxTier; tier++)
        {
            var health = CategoryTierRules.CreepHealthPercentFor(tier);
            var sendCost = CategoryTierRules.SendCostPercentFor(tier);
            var damage = CategoryTierRules.TowerDamagePercentFor(tier);
            var buildCost = CategoryTierRules.TowerBuildCostPercentFor(tier);
            output.WriteLine($"tier {tier}: creep health {health}% at {sendCost}% cost, tower damage {damage}% at {buildCost}% cost");

            // Under the power curve, or the tier buys nothing worth its own price.
            Assert.True(sendCost < health || tier == 1, $"tier {tier} send cost {sendCost}% is not under health {health}%");
            Assert.True(buildCost < damage || tier == 1, $"tier {tier} build cost {buildCost}% is not under damage {damage}%");

            // Above 100 from tier 2 on, or the free ride is still there.
            Assert.True(tier == 1 ? sendCost == 100 : sendCost > 100);
            Assert.True(tier == 1 ? buildCost == 100 : buildCost > 100);
        }
    }

    [Fact]
    public void Upgrading_a_send_category_makes_its_creeps_cost_more_to_send()
    {
        var slice = Slice();
        var creep = SampleVerticalSliceContent.CreepId;

        var before = Gold(slice);
        Assert.True(slice.QueueSend(Seat, creep).Accepted);
        var atTier1 = before - Gold(slice);

        Assert.True(slice.BuyCategoryTier(Seat, CategoryKind.SendCategory, 0, 2).Accepted);

        var afterTier = Gold(slice);
        Assert.True(slice.QueueSend(Seat, creep).Accepted);
        var atTier2 = afterTier - Gold(slice);

        output.WriteLine($"send cost: tier 1 {atTier1}G, tier 2 {atTier2}G");
        Assert.True(atTier2 > atTier1, $"tier 2 send cost {atTier2} did not exceed tier 1's {atTier1}");
    }

    [Fact]
    public void Building_in_an_upgraded_line_costs_more_than_building_before_the_upgrade()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;
        var towerId = SampleVerticalSliceContent.TowerId;

        var before = Gold(slice);
        Assert.True(slice.PlaceTower(Seat, lane, towerId, new GridPosition(1, 3)).Accepted);
        var atTier1 = before - Gold(slice);

        var line = slice.GetSnapshot().Towers.First(t => t.OwnerId.Equals(Seat)).Tier;
        Assert.Equal(1, line);

        Assert.True(slice.BuyCategoryTier(Seat, CategoryKind.TowerLine, 0, 2).Accepted);

        var afterTier = Gold(slice);
        Assert.True(slice.PlaceTower(Seat, lane, towerId, new GridPosition(1, 5)).Accepted);
        var atTier2 = afterTier - Gold(slice);

        output.WriteLine($"build cost: tier 1 {atTier1}G, tier 2 {atTier2}G");
        Assert.True(atTier2 > atTier1, $"tier 2 build cost {atTier2} did not exceed tier 1's {atTier1}");
    }

    /// <summary>
    /// The seat is charged exactly the price the rules quote.
    /// </summary>
    /// <remarks>
    /// The affordability check lives in ValidateTowerPlacement and the deduction in PlaceTower.
    /// They read the price through one function now, and this is what would catch them drifting
    /// apart again — a seat told it can afford a tower and then billed something else is the
    /// specific failure, and it only shows at the boundary or against the exact figure.
    /// </remarks>
    [Fact]
    public void The_charge_is_exactly_the_quoted_tier_price()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;
        var towerId = SampleVerticalSliceContent.TowerId;
        var authored = SampleVerticalSliceContent.Create().Towers.First(t => t.Id.Equals(towerId)).Cost.Amount;

        Assert.True(slice.BuyCategoryTier(Seat, CategoryKind.TowerLine, 0, 2).Accepted);

        var quoted = authored * CategoryTierRules.TowerBuildCostPercentFor(2) / 100;
        output.WriteLine($"authored {authored}G, quoted at tier 2 {quoted}G");
        Assert.True(quoted > authored);

        var before = Gold(slice);
        Assert.True(slice.PlaceTower(Seat, lane, towerId, new GridPosition(1, 3)).Accepted);
        Assert.Equal(quoted, before - Gold(slice));
    }

    /// <summary>A seat that could afford the authored price but not the tiered one is refused.</summary>
    /// <remarks>
    /// The other side of the same drift: a check still reading the authored cost would accept this
    /// build and then overdraw the seat. Built by granting only up to the gap rather than by
    /// spending down, since the bridge exposes no way to take gold away.
    /// </remarks>
    [Fact]
    public void The_authored_price_is_not_enough_once_the_line_is_upgraded()
    {
        var slice = new LocalVerticalSlice(
            SampleVerticalSliceContent.Create(),
            new LocalMatchOptions(seed: 1, laneCount: LocalMatchOptions.MaxLaneCount),
            enableBots: false);
        var towerId = SampleVerticalSliceContent.TowerId;
        var authored = SampleVerticalSliceContent.Create().Towers.First(t => t.Id.Equals(towerId)).Cost.Amount;

        // Granted once, up front, to land on exactly the authored cost after the tier is paid for.
        // Gold can only be added through the bridge, so the opening bank has to be counted into the
        // grant rather than spent down afterwards.
        slice.GrantLocalPlaytestIncome(Seat, new Income(900));
        var tierPrice = CategoryTierRules.CostFor(CategoryKind.TowerLine, 2, 0);
        slice.GrantLocalPlaytestGold(Seat, new Gold(tierPrice + authored - Gold(slice)));
        Assert.True(slice.BuyCategoryTier(Seat, CategoryKind.TowerLine, 0, 2).Accepted);
        Assert.Equal(authored, Gold(slice));

        var refused = slice.PlaceTower(Seat, slice.LocalPlayerLaneId, towerId, new GridPosition(1, 3));
        output.WriteLine($"holding the authored {authored}G against a tier-2 price of {authored * CategoryTierRules.TowerBuildCostPercentFor(2) / 100}G: {refused.RejectionReason}");
        Assert.False(refused.Accepted);
        Assert.Equal(CommandRejectionReason.InsufficientGold, refused.RejectionReason);
        Assert.Equal(authored, Gold(slice));
    }
}
