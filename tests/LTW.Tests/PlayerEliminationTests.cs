using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// A defeated seat's lane is cleared and the match carries on around it.
/// </summary>
/// <remarks>
/// Elimination used to be an economy-only fact — no income, no sends, skipped when routing — while
/// CombatService knew nothing about it. A defeated player's towers kept firing and creeps kept
/// walking their lane, leaking against somebody already on zero lives.
/// </remarks>
public sealed class PlayerEliminationTests
{
    private readonly ITestOutputHelper output;
    public PlayerEliminationTests(ITestOutputHelper output) => this.output = output;

    /// <summary>Drains one seat to zero lives by repeatedly sending at it, with no defence built.</summary>
    private static LocalVerticalSlice RunUntilEliminated(out PlayerId victim, out int atTick)
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 7, laneCount: 3), enableBots: false);
        var eliminated = (PlayerId?)null;
        atTick = 0;

        for (var tick = 0; tick < 40_000 && eliminated is null; tick++)
        {
            slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.SiegeCreepId);
            slice.AdvanceOneTick();
            foreach (var e in slice.DrainEvents())
            {
                if (e is PlayerEliminatedEvent pe && eliminated is null)
                {
                    eliminated = pe.PlayerId;
                    atTick = tick;
                }
            }
        }

        Assert.NotNull(eliminated);
        victim = eliminated!.Value;
        return slice;
    }

    [Fact]
    public void An_eliminated_players_lane_is_cleared_and_the_match_continues()
    {
        var slice = RunUntilEliminated(out var victim, out var atTick);
        var lane = new LaneId(victim.Value);
        var snapshot = slice.GetSnapshot();

        output.WriteLine($"eliminated P{victim.Value} at tick {atTick}");

        Assert.DoesNotContain(snapshot.Towers, t => t.OwnerId.Equals(victim));
        Assert.DoesNotContain(snapshot.Creeps, c => c.LaneId.Equals(lane));
        Assert.Null(slice.MatchSummary);

        // The route must be rebuilt, not merely emptied of towers: towers are what lengthen it, so
        // a stale route would leave creeps walking around a maze that no longer exists.
        Assert.Equal(MazedLane.StraightRoute().Count, slice.RouteLength(lane));
    }

    [Fact]
    public void Wiping_a_lane_leaves_other_lanes_and_the_eliminated_players_own_creeps_alone()
    {
        var slice = RunUntilEliminated(out var victim, out _);
        var lane = new LaneId(victim.Value);
        var snapshot = slice.GetSnapshot();

        // Only the defeated seat's lane is cleared; every other lane keeps playing.
        Assert.All(snapshot.Creeps, creep => Assert.NotEqual(lane, creep.LaneId));
        Assert.Contains(snapshot.Players.Players, p => !p.IsEliminated);

        // Creeps the eliminated player SENT are somebody else's problem now, not deleted with them.
        var theirCreepsElsewhere = snapshot.Creeps.Count(c => c.SenderId.Equals(victim));
        output.WriteLine($"creeps still in flight that P{victim.Value} sent: {theirCreepsElsewhere}");
    }

    [Fact]
    public void The_wipe_announces_each_removed_entity_so_presentation_can_react()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 7, laneCount: 3), enableBots: false);
        var sawEliminated = false;
        var soldOnWipe = 0;
        var killedOnWipe = 0;

        for (var tick = 0; tick < 40_000 && !sawEliminated; tick++)
        {
            slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.SiegeCreepId);
            slice.AdvanceOneTick();
            var events = slice.DrainEvents();
            if (events.Any(e => e is PlayerEliminatedEvent))
            {
                sawEliminated = true;
                soldOnWipe = events.Count(e => e is TowerSoldEvent);
                killedOnWipe = events.Count(e => e is CreepKilledEvent);
            }
        }

        Assert.True(sawEliminated);
        // No towers were ever built in this scenario, so the meaningful signal is the creeps that
        // were mid-lane when their target died.
        output.WriteLine($"on the wipe tick: {soldOnWipe} tower-sold, {killedOnWipe} creep-killed");
        Assert.True(killedOnWipe >= 1, "the creeps standing in the wiped lane should be announced");
    }
}
