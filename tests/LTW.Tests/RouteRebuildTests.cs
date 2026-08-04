using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Covers what happens to creeps already walking a lane when that lane's route is rebuilt.
/// </summary>
/// <remarks>
/// A creep's position is a PathIndex, and an index only means a place while the route it indexes is
/// the same route. Building or selling a tower reshapes the lane, so without a remap every creep in
/// it keeps an index that now points somewhere else entirely — reported from play as creeps jumping
/// around the board whenever a tower is placed.
///
/// This was never cosmetic. The simulation genuinely relocated them, which changes what is in range
/// of which tower, so a player could shuffle an incoming wave by building and selling.
/// </remarks>
public sealed class RouteRebuildTests
{
    private readonly ITestOutputHelper output;

    public RouteRebuildTests(ITestOutputHelper output) => this.output = output;

    private static readonly PlayerId Player = new(1);

    private static LocalVerticalSlice Slice()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        slice.GrantLocalPlaytestGold(Player, new Gold(5000));
        return slice;
    }

    /// <summary>
    /// Sends a creep INTO the local player's own lane, and returns after it has walked a while.
    /// </summary>
    /// <remarks>
    /// A player's sends land in their carousel TARGET's lane, never their own, so having the local
    /// player send produces a lane with nothing in it — which is how the first version of these
    /// tests managed to assert "nothing teleported" against zero creeps. The seat that attacks this
    /// lane is asked for rather than assumed, since which one it is depends on the lane count.
    /// </remarks>
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

    /// <summary>Where every creep in the local lane is standing, keyed by entity.</summary>
    private static Dictionary<long, GridPosition> Positions(LocalVerticalSlice slice)
    {
        var lane = slice.LocalPlayerLaneId;
        return slice.GetSnapshot().Creeps
            .Where(creep => creep.LaneId.Equals(lane))
            .ToDictionary(creep => creep.EntityId.Value, creep => creep.Position);
    }

    /// <summary>
    /// Placing a tower does not move the creeps already in the lane.
    /// </summary>
    /// <remarks>
    /// The bug as reported. A cell of tolerance is allowed because a rebuilt route genuinely may not
    /// contain the exact cell a creep was standing on — the point is that it lands adjacent, not
    /// somewhere across the board.
    /// </remarks>
    [Fact]
    public void Placing_a_tower_does_not_teleport_creeps_already_in_the_lane()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;

        // Spread along the lane rather than bunched at the gate, so the assertion covers creeps at
        // many different indices — the failure scales with distance from the start.
        SendIntoLocalLane(slice, waves: 6);

        var before = Positions(slice);
        Assert.True(before.Count >= 4, $"only {before.Count} creeps in the lane; this proves little");

        var lengthBefore = slice.RouteLength(lane);
        Assert.True(slice.PlaceTower(Player, lane, SampleVerticalSliceContent.TowerId, new GridPosition(3, 6)).Accepted);
        var lengthAfter = slice.RouteLength(lane);

        var after = Positions(slice);
        var moves = before
            .Where(entry => after.ContainsKey(entry.Key))
            .Select(entry => (Entity: entry.Key, From: entry.Value, To: after[entry.Key]))
            .Select(m => (m.Entity, m.From, m.To, Distance: System.Math.Abs(m.To.X - m.From.X) + System.Math.Abs(m.To.Y - m.From.Y)))
            .ToArray();

        output.WriteLine($"route {lengthBefore} -> {lengthAfter}; {moves.Length} creeps tracked");
        foreach (var m in moves.OrderByDescending(m => m.Distance).Take(6))
        {
            output.WriteLine($"  entity {m.Entity}: ({m.From.X},{m.From.Y}) -> ({m.To.X},{m.To.Y})  moved {m.Distance}");
        }

        Assert.NotEmpty(moves);
        Assert.All(moves, m => Assert.True(m.Distance <= 1,
            $"entity {m.Entity} jumped from ({m.From.X},{m.From.Y}) to ({m.To.X},{m.To.Y}) — {m.Distance} cells — when a tower was placed"));
    }

    /// <summary>
    /// Selling a tower does not move them either.
    /// </summary>
    /// <remarks>
    /// The same rebuild runs on the way back down, and a fix that only covered placement would leave
    /// half the bug in place — sell is the cheaper half to trigger repeatedly.
    /// </remarks>
    [Fact]
    public void Selling_a_tower_does_not_teleport_creeps_either()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;
        var cell = new GridPosition(3, 6);
        Assert.True(slice.PlaceTower(Player, lane, SampleVerticalSliceContent.TowerId, cell).Accepted);

        SendIntoLocalLane(slice, waves: 6);

        var before = Positions(slice);
        Assert.True(before.Count >= 4, $"only {before.Count} creeps in the lane; this proves little");

        Assert.True(slice.SellTowerAt(Player, lane, cell).Accepted);

        var after = Positions(slice);
        foreach (var entry in before.Where(entry => after.ContainsKey(entry.Key)))
        {
            var to = after[entry.Key];
            var distance = System.Math.Abs(to.X - entry.Value.X) + System.Math.Abs(to.Y - entry.Value.Y);
            Assert.True(distance <= 1,
                $"entity {entry.Key} jumped {distance} cells when a tower was sold");
        }
    }

    /// <summary>
    /// A creep that ignores the maze is left alone by the remap.
    /// </summary>
    /// <remarks>
    /// Spire Turret Walker walks the direct route, which a tower never reshapes. Remapping it onto
    /// the mazed route would teleport the one unit whose whole point is that mazing does not reach
    /// it — so this asserts the exemption rather than trusting the condition.
    /// </remarks>
    [Fact]
    public void A_maze_ignoring_creep_is_not_remapped()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;

        var attacker = slice.LocalPlaytestSenderForLane(lane);
        Assert.NotNull(attacker);
        slice.GrantLocalPlaytestGold(attacker!.Value, new Gold(5000));
        Assert.True(slice.QueueSend(attacker.Value, SampleVerticalSliceContent.TurretWalkerCreepId).Accepted);
        for (var tick = 0; tick < 15; tick++)
        {
            slice.AdvanceOneTick();
        }

        var walkerBefore = slice.GetSnapshot().Creeps
            .Where(creep => creep.LaneId.Equals(lane) && creep.CreepId.Equals(SampleVerticalSliceContent.TurretWalkerCreepId))
            .ToDictionary(creep => creep.EntityId.Value, creep => creep.Position);
        Assert.NotEmpty(walkerBefore);

        Assert.True(slice.PlaceTower(Player, lane, SampleVerticalSliceContent.TowerId, new GridPosition(3, 6)).Accepted);

        foreach (var creep in slice.GetSnapshot().Creeps.Where(c => walkerBefore.ContainsKey(c.EntityId.Value)))
        {
            var from = walkerBefore[creep.EntityId.Value];
            output.WriteLine($"  walker {creep.EntityId.Value}: ({from.X},{from.Y}) -> ({creep.Position.X},{creep.Position.Y})");
            Assert.Equal(from, creep.Position);
        }
    }
}
