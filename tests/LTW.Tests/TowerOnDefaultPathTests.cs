using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Covers creeps continuing to WALK after a tower is dropped on the lane they are walking down.
/// </summary>
/// <remarks>
/// Reported from play on an iPad: "if you build a ward in the cells that creeps walk through by
/// default, the creeps just stop."
///
/// The map is 7 wide and 16 tall with spawn (3,0) and exit (3,15) and no authored blockers, so the
/// default route is a straight line down column 3 — which means the most natural place a player
/// puts their first tower is directly on it.
///
/// RouteRebuildTests already covers this same placement and passes, because it asks whether creeps
/// TELEPORT. Standing still is the one failure that a "did anything move more than a cell?" check
/// reads as a perfect pass, so nothing in the suite could see this.
/// </remarks>
public sealed class TowerOnDefaultPathTests
{
    private readonly ITestOutputHelper output;

    public TowerOnDefaultPathTests(ITestOutputHelper output) => this.output = output;

    private static readonly PlayerId Player = new(1);

    private static LocalVerticalSlice Slice()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        slice.GrantLocalPlaytestGold(Player, new Gold(5000));
        return slice;
    }

    private static void SendIntoLocalLane(LocalVerticalSlice slice, int waves)
    {
        var attacker = slice.LocalPlaytestSenderForLane(slice.LocalPlayerLaneId);
        Assert.NotNull(attacker);
        slice.GrantLocalPlaytestGold(attacker!.Value, new Gold(5000));

        for (var wave = 0; wave < waves; wave++)
        {
            Assert.True(slice.QueueSend(attacker.Value, SampleVerticalSliceContent.BruteCreepId).Accepted);
            for (var tick = 0; tick < 8; tick++)
            {
                slice.AdvanceOneTick();
            }
        }
    }

    private static Dictionary<long, GridPosition> Positions(LocalVerticalSlice slice)
    {
        var lane = slice.LocalPlayerLaneId;
        return slice.GetSnapshot().Creeps
            .Where(creep => creep.LaneId.Equals(lane))
            .ToDictionary(creep => creep.EntityId.Value, creep => creep.Position);
    }

    /// <summary>
    /// Creeps keep advancing after a tower lands on the cells they were walking through.
    /// </summary>
    /// <remarks>
    /// Asserted on the creeps that were ALREADY in the lane when the tower landed, tracked by
    /// entity id, because those are the ones the bug is about — anything spawned afterwards walks
    /// the rebuilt route from its start and would mask a stall.
    ///
    /// The bar is deliberately low: over 40 ticks a Brute at speed 1 and movement cost 3 covers
    /// roughly 13 cells, so requiring a single cell of progress is not a pace assertion. It is the
    /// difference between walking and standing still.
    /// </remarks>
    [Fact]
    public void Creeps_keep_walking_when_a_tower_is_built_on_their_path()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;

        SendIntoLocalLane(slice, waves: 4);

        var before = Positions(slice);
        Assert.True(before.Count >= 3, $"only {before.Count} creeps in the lane; this proves little");

        // Column 3 is the default route. This is the cell a player naturally builds on.
        Assert.True(slice.PlaceTower(Player, lane, SampleVerticalSliceContent.TowerId, new GridPosition(3, 6)).Accepted);

        var atPlacement = Positions(slice);
        for (var tick = 0; tick < 40; tick++)
        {
            slice.AdvanceOneTick();
        }

        var after = Positions(slice);

        var stalled = new List<string>();
        var walked = 0;
        foreach (var entry in atPlacement)
        {
            // Gone from the lane means killed or leaked, both of which are progress, not a stall.
            if (!after.TryGetValue(entry.Key, out var end))
            {
                walked++;
                continue;
            }

            var advanced = end.Y - entry.Value.Y;
            if (advanced <= 0)
            {
                stalled.Add($"entity {entry.Key} sat at ({entry.Value.X},{entry.Value.Y}) -> ({end.X},{end.Y})");
            }
            else
            {
                walked++;
            }
        }

        output.WriteLine($"{atPlacement.Count} creeps at placement: {walked} advanced, {stalled.Count} stalled");
        foreach (var line in stalled)
        {
            output.WriteLine("  " + line);
        }

        Assert.True(stalled.Count == 0,
            $"{stalled.Count} of {atPlacement.Count} creeps stopped dead after a tower was built on their path:\n  " +
            string.Join("\n  ", stalled));
    }

    /// <summary>
    /// Placing a tower never relocates a creep further than it could have walked.
    /// </summary>
    /// <remarks>
    /// The bug as reported from the iPad: "I placed one ward in the middle cell. I then placed one
    /// to the right and left, so three wide. That's when the creeps got stuck. Then I built another
    /// ward on the left side of the 3, and the creeps started going to the right."
    ///
    /// They did not walk right. They were TELEPORTED right. A creep's position is derived from its
    /// index into the lane route, so rebuilding that route forces every creep to be re-indexed, and
    /// RemapCreepsOntoNewRoute picks the spatially nearest cell on the new route with no bound on
    /// how far that is. While a reroute only bulges around a tower the nearest cell is a step away
    /// and nobody notices. Wall the corridor off completely and the path moves to the other side of
    /// the board — so the nearest cell to a creep standing in the old corridor is four cells away,
    /// across the map, and it arrives there in zero ticks.
    ///
    /// One cell of tolerance, matching RouteRebuildTests: a rebuilt route genuinely may not contain
    /// the exact cell a creep stood on, and landing adjacent is the intended behaviour.
    /// </remarks>
    [Fact]
    public void Walling_off_a_corridor_does_not_teleport_creeps_across_the_board()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;
        SendIntoLocalLane(slice, waves: 5);

        const int Row = 8;
        var worst = 0;
        var worstDescription = string.Empty;

        // The reported sequence: middle, right, left — then the fourth that flips the corridor.
        foreach (var x in new[] { 3, 4, 2, 1, 5 })
        {
            var before = Positions(slice);
            var stepsBefore = slice.DiagnosticRemainingSteps(lane);
            if (!slice.PlaceTower(Player, lane, SampleVerticalSliceContent.TowerId, new GridPosition(x, Row)).Accepted)
            {
                continue;
            }

            var after = Positions(slice);
            var stepsAfter = slice.DiagnosticRemainingSteps(lane);
            foreach (var entry in before.Where(entry => after.ContainsKey(entry.Key)))
            {
                if (!stepsBefore.TryGetValue(entry.Key, out var owedBefore) ||
                    !stepsAfter.TryGetValue(entry.Key, out var owedAfter))
                {
                    continue;
                }

                // Steps GAINED, which is the half of a relocation that can never be legitimate. Some
                // lateral movement is unavoidable — a creep's position is an index into the route,
                // so if the corridor moves the creep must move with it — but no rebuild may hand it
                // progress it did not walk for.
                var gained = owedBefore - owedAfter;
                if (gained > worst)
                {
                    worst = gained;
                    var to = after[entry.Key];
                    worstDescription =
                        $"placing ({x},{Row}) moved entity {entry.Key} from ({entry.Value.X},{entry.Value.Y}) " +
                        $"to ({to.X},{to.Y}), cutting its remaining walk from {owedBefore} steps to {owedAfter} — " +
                        $"{gained} steps of free progress, in zero ticks";
                }
            }

            for (var tick = 0; tick < 10; tick++)
            {
                slice.AdvanceOneTick();
            }
        }

        output.WriteLine($"worst free progress across the sequence: {worst} step(s)");
        if (worst > 0)
        {
            output.WriteLine("  " + worstDescription);
        }

        Assert.True(worst <= 0, worstDescription);
    }

    /// <summary>
    /// The lane still ends: creeps built over eventually reach the exit and leak.
    /// </summary>
    /// <remarks>
    /// The stronger property, and the one a player actually feels. A creep that advances one cell
    /// and then jams satisfies the test above; this one only passes if the whole lane still drains.
    /// </remarks>
    [Fact]
    public void A_lane_with_a_tower_on_its_default_path_still_drains()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;

        SendIntoLocalLane(slice, waves: 4);
        Assert.True(slice.PlaceTower(Player, lane, SampleVerticalSliceContent.TowerId, new GridPosition(3, 6)).Accepted);

        var tracked = Positions(slice).Keys.ToHashSet();
        Assert.NotEmpty(tracked);

        var remaining = tracked.Count;
        for (var tick = 0; tick < 400 && remaining > 0; tick++)
        {
            slice.AdvanceOneTick();
            remaining = Positions(slice).Keys.Count(id => tracked.Contains(id));
        }

        output.WriteLine($"{tracked.Count} creeps at placement, {remaining} still in the lane after 400 ticks");
        Assert.True(remaining == 0,
            $"{remaining} of {tracked.Count} creeps never left the lane in 400 ticks — they are stuck behind the tower");
    }
}
