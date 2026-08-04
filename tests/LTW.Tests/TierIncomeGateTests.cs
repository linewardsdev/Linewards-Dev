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
/// Covers the income a player must already be earning before a category tier can be bought.
/// </summary>
/// <remarks>
/// Tiers used to be gated on gold alone, and gold and income are not the same claim. Gold arrives
/// from kill bounties, leak bounties and the opening bank, so a player who never sends a creep can
/// sit on a lane, collect, and buy a tier at the starting income of 10 — buying power without ever
/// building the economy meant to pay for it. Income only rises by sending, so requiring it makes a
/// tier something earned through the game's own economic loop.
/// </remarks>
public sealed class TierIncomeGateTests
{
    private readonly ITestOutputHelper output;

    public TierIncomeGateTests(ITestOutputHelper output) => this.output = output;

    private static readonly PlayerId Player = new(1);
    private const int Core = 0;

    private static LocalVerticalSlice Slice()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        slice.GrantLocalPlaytestGold(Player, new Gold(5000));
        return slice;
    }

    /// <summary>
    /// All the gold in the world does not buy a tier at starting income.
    /// </summary>
    /// <remarks>
    /// The exploit this exists to close, stated directly: 5,000 gold and income 10.
    /// </remarks>
    [Theory]
    [InlineData(CategoryKind.SendCategory)]
    [InlineData(CategoryKind.TowerLine)]
    public void Gold_alone_does_not_buy_a_tier(CategoryKind kind)
    {
        var slice = Slice();
        var result = slice.BuyCategoryTier(Player, kind, Core, 2);

        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.InsufficientIncome, result.RejectionReason);
        Assert.Equal(1, Tier(slice, kind));
    }

    /// <summary>Nothing is charged for a purchase refused on income.</summary>
    [Fact]
    public void A_tier_refused_on_income_charges_nothing()
    {
        var slice = Slice();
        var goldBefore = slice.GetSnapshot().Players.Get(Player).Gold.Amount;

        Assert.False(slice.BuyCategoryTier(Player, CategoryKind.SendCategory, Core, 2).Accepted);

        Assert.Equal(goldBefore, slice.GetSnapshot().Players.Get(Player).Gold.Amount);
    }

    /// <summary>
    /// The requirement is exactly half the tier's price, and the boundary is inclusive.
    /// </summary>
    /// <remarks>
    /// Asserted at the boundary rather than somewhere comfortably above it, because an off-by-one
    /// in either direction is invisible anywhere else: one income short must fail, and exactly the
    /// requirement must succeed.
    /// </remarks>
    [Theory]
    [InlineData(CategoryKind.SendCategory, 2)]
    [InlineData(CategoryKind.SendCategory, 3)]
    [InlineData(CategoryKind.TowerLine, 2)]
    [InlineData(CategoryKind.TowerLine, 3)]
    public void The_threshold_is_half_the_price_and_inclusive(CategoryKind kind, int tier)
    {
        // Measured after the climb, not before it. Reaching tier 3 means buying tier 2 first, and a
        // tier already held escalates the price of the next one — so the threshold for tier 3 is
        // half of the ESCALATED price, and computing it from the list price tests a number the
        // bridge will never charge.
        var justUnder = Slice();
        RaiseTo(justUnder, kind, tier - 1);
        var owned = CategoryTierRules.UpgradesOwned(justUnder.GetSnapshot().Players.Get(Player));
        var required = CategoryTierRules.MinimumIncomeFor(kind, tier, owned);
        var price = CategoryTierRules.CostFor(kind, tier, owned);
        Assert.Equal(price / 2, required);
        output.WriteLine($"{kind} tier {tier}: costs {price}G at this point in the climb, requires income {required}");

        // One short of the requirement is refused...
        justUnder.GrantLocalPlaytestIncome(Player, new Income(required - 1 - CurrentIncome(justUnder)));
        Assert.Equal(CommandRejectionReason.InsufficientIncome,
            justUnder.BuyCategoryTier(Player, kind, Core, tier).RejectionReason);

        // ...and exactly the requirement is enough.
        var exactly = Slice();
        RaiseTo(exactly, kind, tier - 1);
        exactly.GrantLocalPlaytestIncome(Player, new Income(required - CurrentIncome(exactly)));
        Assert.True(exactly.BuyCategoryTier(Player, kind, Core, tier).Accepted);
    }

    /// <summary>
    /// Tier 3 asks for more income than tier 2, so the gate keeps biting as tiers climb.
    /// </summary>
    /// <remarks>
    /// A requirement that did not rise would let a player who cleared tier 2 buy tier 3 the moment
    /// they had the gold, which is the same rush one step later.
    /// </remarks>
    [Fact]
    public void A_higher_tier_demands_more_income()
    {
        foreach (var kind in new[] { CategoryKind.SendCategory, CategoryKind.TowerLine })
        {
            Assert.True(CategoryTierRules.MinimumIncomeFor(kind, 3) > CategoryTierRules.MinimumIncomeFor(kind, 2));
        }
    }

    /// <summary>
    /// The gate sits below where bots already buy, so it changes nothing already measured.
    /// </summary>
    /// <remarks>
    /// Measured before the gate existed, across a three- and an eight-lane match on seed 1: bots
    /// bought tier 2 at income 198-321 and tier 3 at 407-600. This asserts the requirements stay
    /// under the weakest of those, so the gate targets a human rush and leaves bot-driven balance
    /// measurements alone. If a future retune of the tier costs pushes a requirement past this, the
    /// balance numbers in GD_TUNING_LOG need re-measuring rather than the bound relaxing.
    /// </remarks>
    [Fact]
    public void The_gate_does_not_reach_where_bots_already_buy()
    {
        const int weakestObservedTier2 = 198;
        const int weakestObservedTier3 = 407;

        foreach (var kind in new[] { CategoryKind.SendCategory, CategoryKind.TowerLine })
        {
            Assert.True(CategoryTierRules.MinimumIncomeFor(kind, 2) < weakestObservedTier2,
                $"{kind} tier 2 now requires {CategoryTierRules.MinimumIncomeFor(kind, 2)} income, at or past the {weakestObservedTier2} bots were measured buying at");
            Assert.True(CategoryTierRules.MinimumIncomeFor(kind, 3) < weakestObservedTier3,
                $"{kind} tier 3 now requires {CategoryTierRules.MinimumIncomeFor(kind, 3)} income, at or past the {weakestObservedTier3} bots were measured buying at");
        }
    }

    /// <summary>
    /// A real match still reaches tier 3 on both sides of the roster.
    /// </summary>
    /// <remarks>
    /// The guard against gating the feature out of existence. A threshold nobody reaches is
    /// indistinguishable from deleting tiers, and no unit test above would notice.
    /// </remarks>
    [Fact]
    public void Bots_still_reach_the_top_tier_in_a_real_match()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 8));
        slice.StartMatch();

        var peakTier = 1;
        for (var tick = 0; tick < 6_000 && slice.MatchSummary is null; tick++)
        {
            slice.AdvanceOneTick();
            foreach (var player in slice.GetSnapshot().Players.Players)
            {
                for (var category = 0; category < PlayerEconomyState.CategoryCount; category++)
                {
                    peakTier = System.Math.Max(peakTier, System.Math.Max(player.TowerLineTier(category), player.SendCategoryTier(category)));
                }
            }
        }

        output.WriteLine($"peak tier reached in an eight-lane match: {peakTier}");
        Assert.Equal(CategoryTierRules.MaxTier, peakTier);
    }

    private static int CurrentIncome(LocalVerticalSlice slice) =>
        slice.GetSnapshot().Players.Get(Player).Income.Amount;

    private static int Tier(LocalVerticalSlice slice, CategoryKind kind)
    {
        var player = slice.GetSnapshot().Players.Get(Player);
        return kind == CategoryKind.TowerLine ? player.TowerLineTier(Core) : player.SendCategoryTier(Core);
    }

    /// <summary>Buys every tier up to <paramref name="tier"/>, so the next one is the one under test.</summary>
    private static void RaiseTo(LocalVerticalSlice slice, CategoryKind kind, int tier)
    {
        for (var step = 2; step <= tier; step++)
        {
            // Priced against what is already held. Each step raises the price of the next, so
            // granting the list requirement leaves the climb one purchase short of itself from the
            // second step on, and RaiseTo would fail on its own assertion rather than set up the
            // state the caller asked for.
            var owned = CategoryTierRules.UpgradesOwned(slice.GetSnapshot().Players.Get(Player));
            slice.GrantLocalPlaytestIncome(Player, new Income(CategoryTierRules.MinimumIncomeFor(kind, step, owned)));
            Assert.True(slice.BuyCategoryTier(Player, kind, Core, step).Accepted);
        }
    }
}
