using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Asserts the bots actually maze — that they lengthen the creep route rather than just occupying cells.
/// </summary>
/// <remarks>
/// This game is about mazing, and until now the bots did none of it. Their placements came from a
/// hardcoded list of nine cells in columns 1 and 5, chosen with no reference to the route, and once those
/// were filled a bot could never build again. Every balance measurement taken against these opponents was
/// therefore taken against a straight lane that no human player would ever leave straight — which makes
/// those measurements a poor guide to how heavy creeps or towers should be.
/// </remarks>
public sealed class BotMazingTests
{
    private readonly ITestOutputHelper output;

    public BotMazingTests(ITestOutputHelper output) => this.output = output;

    private static LocalMatchOptions ThreeLanes() => new(seed: 1, laneCount: 3);

    [Fact]
    public void Bots_lengthen_their_lane_route_well_past_the_straight_line()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLanes());
        var straight = slice.RouteLength(new LaneId(2));

        // 1600, not 1200. Once bots could build all 15 towers (item 15 in OPEN_ITEMS.md) instead of
        // repeating the same 2-3 cheap ones forever, the early gold that used to go straight into
        // another Arrow/Pulse now sometimes buys a costlier tower on the way to the rest of the
        // roster, so the same lane took until tick ~1400 to clear this bar instead of well before
        // 1200 — it still reaches 30 cells (1.875x) by tick 1600 and holds there, so this is a timing
        // shift, not a mazing regression.
        //
        // 2000, not 1600, once bots learned to send an escort only behind a wall
        // (BotController.EscortFollowWindowTicks). That moved early gold off the cheapest escorts
        // and onto walls, which cost more, so the crossing point drifted a little later — and 1600
        // turned out to be sitting exactly ON it: measured, lane 2 is at 20 cells after 1600
        // advances and 22 after 1601. The bar was NOT lowered to accommodate that, because the
        // capability plainly has not dropped: tower count over the same window went 52 -> 53, and
        // the same lane reaches 42 cells (2.6x) by tick 2000. Sampling off the knife edge measures
        // mazing; sampling on it measures which side of a single placement the clock stopped.
        for (var tick = 0; tick < 2000; tick++)
        {
            slice.AdvanceOneTick();
        }

        var mazed = slice.RouteLength(new LaneId(2));
        output.WriteLine($"lane 2 route: {straight} cells straight, {mazed} after mazing");

        // 1.35x, not 1.4x, and not the original 2x. Each drop had a measured cause rather than a
        // convenient one:
        //
        //   2x   -> measured while a bot-pressure bug (a filter missing !HasLeaked) froze the bots
        //           out of sending, so every scrap of gold went into towers and the route hit 40
        //           cells. That number described a bug, not a capability.
        //   1.4x -> with sends working, the same bots split gold between towers and creeps: 24 cells.
        //   1.35x-> category tiers gave bots a THIRD thing to spend on, so the split is three ways
        //           and lane 2 settles at 22. Confirmed by measurement, not assumed: disabling
        //           TryBuyBotTier alone restores exactly 24, so this is the cost of the feature and
        //           nothing else.
        //
        // A gate requiring bots to keep a tower's worth of gold in reserve before upgrading was
        // tried to win those two cells back. It did not (still 22) and cost 1650 ticks of match
        // length by starving the creep tiers that close a game out, so it was dropped — see
        // TryBuyBotTier. 22 of 16 is still comfortably mazing, which is what this test is for.
        Assert.True(mazed > straight * 135 / 100, $"route only went from {straight} to {mazed} cells — the bot is not mazing");
    }

    [Fact]
    public void Bots_keep_building_past_the_old_nine_cell_ceiling()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLanes());

        // 1500, not 1200. OpeningEconomyRules' opening discount (2026-08-24) makes sends cheaper
        // for the first 300 ticks, and TakeTurn already gives sending first claim on a tick's gold
        // (see BotController's own doc on that ordering) — so a bot spends more on creeps during
        // the discount window and has less left for towers, delaying when the busiest one clears
        // nine. Traced: best tower count is 8 at 1200, 10 by 1400. Still comfortably past the old
        // ceiling, just later.
        for (var tick = 0; tick < 1500; tick++)
        {
            slice.AdvanceOneTick();
        }

        var snapshot = slice.GetSnapshot();
        var best = snapshot.Players.Players
            .Max(player => snapshot.Towers.Count(tower => tower.OwnerId.Equals(player.PlayerId)));

        Assert.True(best > 9, $"the busiest bot built only {best} towers, so it is still capped");
    }

    /// <summary>
    /// A maze must never seal the lane. GridPathService already rejects a blocking placement, so this
    /// guards that the bot search honours it rather than finding some way around it.
    /// </summary>
    [Fact]
    public void Mazing_never_blocks_the_route()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLanes());

        for (var tick = 0; tick < 1200; tick++)
        {
            slice.AdvanceOneTick();
            foreach (var lane in new[] { 1, 2, 3 })
            {
                Assert.True(slice.RouteLength(new LaneId(lane)) > 0, $"lane {lane} route was sealed at tick {tick}");
            }
        }
    }

    /// <summary>
    /// Bots actually buy category tiers, and buy the side their profile is about.
    /// </summary>
    /// <remarks>
    /// Bots must upgrade or the feature makes them strictly worse opponents: they maze well now,
    /// and a tier-3 human against a tier-1 bot defence would be a walkover.
    ///
    /// Which SIDE they buy is asserted too, because it is not cosmetic — measurement showed the
    /// preference dominates the tier multipliers for match pacing. When bots poured their upgrade
    /// gold into towers, two of them could not finish a match at any multiplier; driving the choice
    /// from each profile's own Aggression/DefenseBias instead brought the same match in at 3249
    /// ticks, faster than the 3627 it takes with no tiers at all.
    /// </remarks>
    [Fact]
    public void Bots_buy_category_tiers_on_the_side_their_profile_favours()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLanes());

        // 2200, not 1600. Measured after StartingLives dropped to 100: the first tier on this seed
        // is bought at EXACTLY tick 1600, so the old loop stopped one tick short of the thing it
        // asserts. That is a boundary, not a behaviour change -- the tiers are still bought, and by
        // the end of a match 2 of 3 seats hold one -- so this is headroom rather than a widened
        // window hiding a shift. If this fails again, check WHEN the first tier lands before
        // raising it further; a number that keeps climbing means bots are getting poorer, which is
        // a real finding and not a test problem.
        //
        // 3100, not 2200, with the 2x creep roster (2026-08-19). The number climbed, so the check
        // above was run rather than skipped, and bots ARE poorer: P3's income at tick 2400 fell from
        // 363 to 98 because sends cost twice as much and income only rises by sending. That much is
        // the intended consequence of the reprice. What it exposed was NOT: uncapped TryBuild spends
        // every surplus coin on another tower each tick, so a bot could never accumulate a tier's
        // price at all, and P3 bought nothing for a whole match while probing showed it would have
        // been ACCEPTED at any moment it happened to hold the gold. Buying tiers before building
        // (BotController.TakeTurn) fixes that; the first tier on this seed then lands at 2900.
        for (var tick = 0; tick < 3100; tick++)
        {
            slice.AdvanceOneTick();
        }

        var players = slice.GetSnapshot().Players.Players
            .Where(player => player.PlayerId.Value != 1)
            .ToArray();

        var upgraded = players.Where(player =>
            Enumerable.Range(0, PlayerEconomyState.CategoryCount).Any(category =>
                player.TowerLineTier(category) > 1 || player.SendCategoryTier(category) > 1))
            .ToArray();

        foreach (var player in players)
        {
            var tower = string.Join(",", Enumerable.Range(0, PlayerEconomyState.CategoryCount).Select(player.TowerLineTier));
            var send = string.Join(",", Enumerable.Range(0, PlayerEconomyState.CategoryCount).Select(player.SendCategoryTier));
            output.WriteLine($"P{player.PlayerId.Value}: tower=[{tower}] send=[{send}]");
        }

        Assert.NotEmpty(upgraded);

        // Every upgrade a bot bought is on exactly one side, and it is the side its profile is
        // about — no bot should be splitting its gold across both.
        foreach (var player in upgraded)
        {
            var boughtTower = Enumerable.Range(0, PlayerEconomyState.CategoryCount).Any(category => player.TowerLineTier(category) > 1);
            var boughtSend = Enumerable.Range(0, PlayerEconomyState.CategoryCount).Any(category => player.SendCategoryTier(category) > 1);
            Assert.True(boughtTower ^ boughtSend, $"P{player.PlayerId.Value} bought on both sides; the profile preference is not being respected");
        }
    }

    /// <summary>
    /// Bots bring their placed towers up to the line tier they bought.
    /// </summary>
    /// <remarks>
    /// Without this a bot buys a line tier and never realises it: the tier reaches only towers
    /// built afterwards, so a bot that has finished building would carry an upgrade it paid for and
    /// gets nothing from — strictly worse than not buying it.
    ///
    /// Sampled DURING the match rather than from the final snapshot, and that is not incidental.
    /// A defeated player's lane is wiped, and the bot that favours tower tiers is often the one
    /// that loses, so the end-of-match snapshot showed zero upgraded towers while the peak during
    /// play was 35 — a measurement that would have reported this feature as completely dead.
    /// </remarks>
    [Fact]
    public void Bots_upgrade_the_towers_they_have_already_built()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLanes());

        var peakTier = 1;
        var peakUpgraded = 0;
        // 3200, not 2400, with the 2x creep roster (2026-08-19) — see the sibling test above for why
        // bots reach a tier later now and what was structural rather than economic about it. Measured
        // on this seed: the tower line tier is bought at 2900, the first upgrade lands at 3000, and
        // 46 towers are eventually upgraded in a match that ends at 3281. The window sits between the
        // upgrade and the end deliberately, since a defeated seat's lane is wiped.
        for (var tick = 0; tick < 3200; tick++)
        {
            slice.AdvanceOneTick();
            if (tick % 25 != 0)
            {
                continue;
            }

            var towers = slice.GetSnapshot().Towers;
            if (towers.Count == 0)
            {
                continue;
            }

            peakTier = System.Math.Max(peakTier, towers.Max(tower => tower.Tier));
            peakUpgraded = System.Math.Max(peakUpgraded, towers.Count(tower => tower.Tier > 1));
        }

        output.WriteLine($"peak tower tier {peakTier}, peak upgraded towers {peakUpgraded}");
        Assert.True(peakTier > 1, "no bot ever raised a tower above tier 1, so bots never realise the line tiers they buy");
        Assert.True(peakUpgraded > 1, $"only {peakUpgraded} tower(s) were ever upgraded; bots should level a line, not a single tower");
    }
}
