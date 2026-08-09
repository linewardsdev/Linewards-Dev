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
    /// Every creep still walking keeps walking after a tower reshapes its lane.
    /// </summary>
    /// <remarks>
    /// Reported from iPad play as "creeps pause for a second or two when they have to turn". The
    /// turn itself was ruled out — the model rotates at 540 deg/sec, so a corner takes 0.17s — which
    /// left the stall in movement, and re-mazing is what puts corners in a lane in the first place.
    ///
    /// The presentation makes any such stall unusually visible: CreepTravelPosition stops
    /// interpolating and CreepFacingYaw holds its previous yaw, both on NextPosition == Position, so
    /// a stalled creep is frozen AND not turning and reads as a deliberate pause rather than a hitch.
    /// This asserts the simulation half, which is the half that can actually be wrong.
    ///
    /// A creep that leaks during the window leaves the snapshot and is not counted — the assertion is
    /// about creeps that are still in the lane at the end and never moved while they were there.
    /// </remarks>
    [Fact]
    public void Creeps_keep_walking_after_a_tower_reshapes_the_lane()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;
        SendIntoLocalLane(slice, waves: 6);

        Assert.True(slice.PlaceTower(Player, lane, SampleVerticalSliceContent.TowerId, new GridPosition(3, 6)).Accepted);

        var start = Positions(slice);
        Assert.True(start.Count >= 4, $"only {start.Count} creeps in the lane; this proves little");

        // 20 ticks. A creep banks MovementProgress each tick and steps a whole cell when it reaches
        // MovementCost, which is 3 for an unbraked creep, so anything that is walking at all has had
        // room for several steps. Long enough that a one-off hitch cannot pass and short enough that
        // a healthy lane has not emptied itself into the gate.
        for (var tick = 0; tick < 20; tick++)
        {
            slice.AdvanceOneTick();
        }

        var end = Positions(slice);
        var stalled = start
            .Where(entry => end.ContainsKey(entry.Key) && end[entry.Key].Equals(entry.Value))
            .Select(entry => entry.Key)
            .ToArray();

        output.WriteLine($"{start.Count} creeps before, {end.Count} after, {stalled.Length} never moved");
        foreach (var creep in slice.GetSnapshot().Creeps.Where(c => stalled.Contains(c.EntityId.Value)))
        {
            output.WriteLine($"  entity {creep.EntityId.Value}: at ({creep.Position.X},{creep.Position.Y}) next ({creep.NextPosition.X},{creep.NextPosition.Y}) progress {creep.MovementProgress}/{creep.MovementCost}");
        }

        Assert.Empty(stalled);
    }

    /// <summary>
    /// Building steadily does not starve creeps of movement.
    /// </summary>
    /// <remarks>
    /// The route rebuild deliberately drops MovementProgress — it is a fraction of a step into a cell
    /// that has generally moved, so carrying it would advance a creep along a step it never started.
    /// Correct in itself, but a creep needs MovementCost ticks of banked progress to step one cell,
    /// and a player mid-build rebuilds the lane far more often than that. Each rebuild returns every
    /// creep in the lane to zero, so the question is whether a normal building pace can hold a lane
    /// at a standstill.
    ///
    /// One placement does not do it — Creeps_keep_walking_after_a_tower_reshapes_the_lane covers that
    /// and passes. This places on a cadence instead.
    /// </remarks>
    [Fact]
    public void Building_steadily_does_not_hold_creeps_still()
    {
        var slice = Slice();
        var lane = slice.LocalPlayerLaneId;
        SendIntoLocalLane(slice, waves: 6);

        var start = Positions(slice);
        Assert.True(start.Count >= 4, $"only {start.Count} creeps in the lane; this proves little");

        // A tower every 2 ticks, which is faster than MovementCost (3) and is the pace a player
        // taps at while laying out a maze.
        var cells = new[]
        {
            new GridPosition(3, 6), new GridPosition(3, 8), new GridPosition(3, 10),
            new GridPosition(1, 7), new GridPosition(5, 7), new GridPosition(1, 11),
        };
        foreach (var cell in cells)
        {
            slice.PlaceTower(Player, lane, SampleVerticalSliceContent.TowerId, cell);
            slice.AdvanceOneTick();
            slice.AdvanceOneTick();
        }

        var end = Positions(slice);
        var stalled = start
            .Where(entry => end.ContainsKey(entry.Key) && end[entry.Key].Equals(entry.Value))
            .Select(entry => entry.Key)
            .ToArray();

        output.WriteLine($"{start.Count} creeps before, {end.Count} after, {stalled.Length} never moved across {cells.Length} placements");
        foreach (var creep in slice.GetSnapshot().Creeps.Where(c => stalled.Contains(c.EntityId.Value)))
        {
            output.WriteLine($"  entity {creep.EntityId.Value}: at ({creep.Position.X},{creep.Position.Y}) next ({creep.NextPosition.X},{creep.NextPosition.Y}) progress {creep.MovementProgress}/{creep.MovementCost}");
        }

        Assert.Empty(stalled);
    }

    /// <summary>
    /// A braked creep's reported cost matches the step it actually banks against.
    /// </summary>
    /// <remarks>
    /// This is the bug behind "creeps pause for a second or two when they have to turn". Brakes sit
    /// in the maze, which is where the corners are, so the pause and the turn arrive together and the
    /// turn looked like the cause. It was not: the model rotates at 540 deg/sec.
    ///
    /// StepCreep makes a braked creep bank against MovementCost + BrambleMovementPenalty, but the
    /// snapshot reported MovementCost alone. Presentation divides progress by what it is given, so
    /// the fraction reached 1 after 3 of the 9 ticks and clamped there — the creep crossed its cell
    /// in a third of the time and then stood perfectly still for the remaining 6 ticks. At 4 ticks a
    /// second that is 1.5 seconds of a 2.25 second cell, motionless, which is the report almost
    /// exactly.
    ///
    /// Asserted on the numbers rather than through the renderer, because the renderer is where the
    /// symptom showed and the snapshot is where the fault was.
    /// </remarks>
    [Fact]
    public void A_braked_creeps_effective_cost_covers_the_whole_cell()
    {
        var unbraked = new LTW.Simulation.Combat.CreepPresentationSnapshot(
            new EntityId(1), SampleVerticalSliceContent.BruteCreepId, Player, new LaneId(1),
            new GridPosition(3, 4), health: 10, maxHealth: 10, speedPerSecond: 1,
            nextPosition: new GridPosition(3, 5), movementProgress: 0,
            movementCost: LTW.Simulation.Combat.CombatService.BaseMovementCost, isBraked: false);

        var braked = new LTW.Simulation.Combat.CreepPresentationSnapshot(
            new EntityId(2), SampleVerticalSliceContent.BruteCreepId, Player, new LaneId(1),
            new GridPosition(3, 4), health: 10, maxHealth: 10, speedPerSecond: 1,
            nextPosition: new GridPosition(3, 5), movementProgress: 0,
            movementCost: LTW.Simulation.Combat.CombatService.BaseMovementCost, isBraked: true);

        // Unbraked: nothing added, so the two agree and the old renderer maths was already right.
        Assert.Equal(unbraked.MovementCost, unbraked.EffectiveMovementCost);

        // Braked: the extra ticks are included, so an interpolation over EffectiveMovementCost
        // spans the whole crossing instead of saturating a third of the way through it.
        Assert.Equal(
            braked.MovementCost + LTW.Simulation.Combat.CombatService.BrambleMovementPenalty,
            braked.EffectiveMovementCost);
        Assert.True(
            braked.EffectiveMovementCost > braked.MovementCost,
            "a braked creep must report a larger cost than an unbraked one, or presentation clamps early");

        output.WriteLine($"  unbraked {unbraked.MovementCost} -> {unbraked.EffectiveMovementCost}; braked {braked.MovementCost} -> {braked.EffectiveMovementCost}");
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
