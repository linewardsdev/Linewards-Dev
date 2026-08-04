using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// That all eight seats actually play, not just the first few.
/// </summary>
/// <remarks>
/// Nearly every balance and bot test in this suite runs on two or three lanes, because that is
/// cheaper and because the mechanic under test does not usually care how many seats exist. The risk
/// that leaves is a failure that only appears at the full table — a bot that never acts, a lane that
/// never receives, a send routed somewhere it should not go — passing everything while the shipped
/// configuration is eight.
///
/// These run the real eight-lane match with real bots and assert per lane rather than in aggregate,
/// because an aggregate is exactly what hides one dead seat among seven live ones.
/// </remarks>
public sealed class FullTableLaneTests
{
    private readonly ITestOutputHelper output;

    public FullTableLaneTests(ITestOutputHelper output) => this.output = output;

    private const int Lanes = LocalMatchOptions.MaxLaneCount;

    private static LocalVerticalSlice FullTable() =>
        new(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: Lanes));

    private static void Advance(LocalVerticalSlice slice, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            slice.AdvanceOneTick();
        }
    }

    [Fact]
    public void Every_seat_builds_in_its_own_lane()
    {
        var slice = FullTable();
        Advance(slice, 1200);

        var towers = slice.GetSnapshot().Towers;
        var built = new Dictionary<int, int>();
        for (var lane = 1; lane <= Lanes; lane++)
        {
            built[lane] = towers.Count(t => t.LaneId.Value == lane);
            output.WriteLine($"lane {lane}: {built[lane]} towers, owner ids {string.Join(",", towers.Where(t => t.LaneId.Value == lane).Select(t => t.OwnerId.Value).Distinct().OrderBy(v => v))}");
        }

        // Every lane except the local seat's. Lane 1 belongs to the human player and has no bot
        // driving it, so it is legitimately empty in a headless run — asserting over all eight would
        // fail on the one lane that is behaving correctly.
        var localLane = slice.LocalPlayerLaneId.Value;
        var silent = built.Where(pair => pair.Key != localLane && pair.Value == 0).Select(pair => pair.Key).ToList();
        Assert.True(silent.Count == 0, $"bot lanes with no towers after 1200 ticks: {string.Join(", ", silent)}");
        Assert.Equal(0, built[localLane]);

        // A lane holds only its own owner's towers. Placement into someone else's lane is rejected,
        // so a foreign owner id here would mean the routing or the ownership check had drifted.
        foreach (var tower in towers)
        {
            Assert.Equal(tower.LaneId.Value, tower.OwnerId.Value);
        }
    }

    [Fact]
    public void Every_bot_seat_sends_and_its_creeps_arrive_in_the_next_lane()
    {
        var slice = FullTable();

        // Sampled every tick rather than read once at the end. A single snapshot only shows what
        // happens to be alive at that instant, and creeps are killed and leaked constantly — the
        // first version of this read one frame at tick 1200 and saw four of the eight seats, which
        // says nothing about whether the other four ever sent.
        var routes = new HashSet<(int Sender, int Lane)>();
        for (var i = 0; i < 1200; i++)
        {
            slice.AdvanceOneTick();
            foreach (var creep in slice.GetSnapshot().Creeps)
            {
                routes.Add((creep.SenderId.Value, creep.LaneId.Value));
            }
        }

        foreach (var route in routes.OrderBy(r => r.Sender).ThenBy(r => r.Lane))
        {
            output.WriteLine($"seat {route.Sender} -> lane {route.Lane}");
        }

        var localSeat = slice.LocalPlayerId.Value;
        for (var seat = 1; seat <= Lanes; seat++)
        {
            if (seat == localSeat)
            {
                continue;
            }

            Assert.True(routes.Any(r => r.Sender == seat), $"seat {seat} never put a creep in anyone's lane");
        }

        // A creep never walks the lane of whoever sent it — the invariant that makes a send an
        // attack rather than a self-inflicted wound, and the one most likely to break quietly when
        // the seat count changes.
        Assert.DoesNotContain(routes, r => r.Sender == r.Lane);

        // Every lane is somebody's target, including lane 1: seat 8's target wraps around to it, so
        // this also covers the wrap that a straight n+1 rule would get wrong at the end of the table.
        for (var lane = 1; lane <= Lanes; lane++)
        {
            Assert.True(routes.Any(r => r.Lane == lane), $"lane {lane} never received a creep from anyone");
        }
    }

    [Fact]
    public void A_defeated_seat_stops_building_for_the_rest_of_the_match()
    {
        var slice = FullTable();
        Advance(slice, 400);

        // Seat 4 rather than 1: the local seat is 1 by default, and a bug that only ever showed on
        // the seat the harness treats specially would be the easiest kind to miss.
        var beaten = new PlayerId(4);
        slice.EliminateForLocalPlaytest(beaten);

        var atElimination = slice.GetSnapshot().Towers.Count(t => t.OwnerId.Equals(beaten));
        Advance(slice, 800);
        var later = slice.GetSnapshot().Towers.Count(t => t.OwnerId.Equals(beaten));

        output.WriteLine($"seat 4 towers at elimination: {atElimination}, 800 ticks later: {later}");
        Assert.Equal(0, atElimination);
        Assert.Equal(0, later);

        // The other seats have to be unaffected — a fix that quietened the whole table rather than
        // the defeated seat would satisfy the assertion above and break the match.
        var others = slice.GetSnapshot().Towers.Count(t => !t.OwnerId.Equals(beaten));
        output.WriteLine($"towers owned by the other seven seats: {others}");
        Assert.True(others > 0, "eliminating one seat silenced every seat");
    }
}
