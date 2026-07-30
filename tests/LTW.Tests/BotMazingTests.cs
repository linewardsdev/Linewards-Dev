using System.Linq;
using LTW.Simulation.Bridge;
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

        for (var tick = 0; tick < 1200; tick++)
        {
            slice.AdvanceOneTick();
        }

        var mazed = slice.RouteLength(new LaneId(2));
        output.WriteLine($"lane 2 route: {straight} cells straight, {mazed} after mazing");

        Assert.True(mazed > straight * 2, $"route only went from {straight} to {mazed} cells — the bot is not mazing");
    }

    [Fact]
    public void Bots_keep_building_past_the_old_nine_cell_ceiling()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLanes());

        for (var tick = 0; tick < 1200; tick++)
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
}
