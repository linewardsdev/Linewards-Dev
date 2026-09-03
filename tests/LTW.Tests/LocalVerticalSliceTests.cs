using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

public sealed class LocalVerticalSliceTests
{
    [Fact]
    public void Player_one_send_spawns_in_player_twos_lane_and_is_recorded_for_replay()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

        var result = slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);

        Assert.True(result.Accepted);
        var creep = Assert.Single(slice.GetSnapshot().Creeps);
        Assert.Equal(new LaneId(2), creep.LaneId);
        var replay = slice.GetReplayRecord();
        var command = Assert.Single(replay.AcceptedCommands);
        Assert.Equal(new PlayerId(1), command.PlayerId);
        Assert.Equal(SampleVerticalSliceContent.CreepId, command.ContentId);
    }

    [Fact]
    public void Reset_clears_recorded_commands()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);

        slice.Reset();

        Assert.Empty(slice.GetReplayRecord().AcceptedCommands);
    }

    /// <summary>
    /// A send queued right before Reset does not survive it and spawn against the new match's
    /// starting gold.
    /// </summary>
    /// <remarks>
    /// Found from a "reset does not reset correctly" report. Reset rebuilt the player set but left
    /// the queue dictionary standing, keyed off PlayerId rather than living inside it — so an unpaid
    /// send from the abandoned match drained on the first tick of the new one, spawning a creep the
    /// player never asked for in the match they were actually playing.
    /// </remarks>
    [Fact]
    public void Reset_clears_a_queued_send_rather_than_letting_it_drain_into_the_next_match()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        var seat = new PlayerId(1);
        Assert.True(slice.EnqueueSend(seat, SampleVerticalSliceContent.CreepId).Accepted);
        Assert.NotEmpty(slice.SendQueueFor(seat));

        slice.Reset();

        Assert.Empty(slice.SendQueueFor(seat));

        slice.StartMatch();
        slice.AdvanceOneTick();
        Assert.Empty(slice.GetSnapshot().Creeps);
    }

    /// <summary>
    /// A seat eliminated in one match can be ranked correctly if the same seat is eliminated again,
    /// at a different tick, after a Reset.
    /// </summary>
    /// <remarks>
    /// RecordElimination writes a seat's elimination tick once and never again — true within a
    /// match, since a seat cannot come back, but Reset starts a NEW match where it is not true.
    /// Before this was fixed, the dictionary survived Reset, so a seat's second elimination found
    /// the key already present and silently kept the FIRST match's tick — the results screen would
    /// rank a seat by when it fell in a match that no longer exists.
    ///
    /// Three lanes, no bots, so eliminations are driven entirely by
    /// <see cref="LocalVerticalSlice.EliminateForLocalPlaytest"/> and nothing else can end the
    /// match early. Each match eliminates seats 2 and 3 in the OPPOSITE order from the other, so a
    /// stale tick for either one flips that seat's placement — a fix produces the reversed ranking
    /// on the second match; the bug reproduces the first match's ranking again.
    /// </remarks>
    [Fact]
    public void Reset_lets_a_seat_be_ranked_by_when_it_falls_in_the_new_match_not_the_old_one()
    {
        var options = new LocalMatchOptions(seed: 1, laneCount: 3);
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options, enableBots: false);
        slice.StartMatch();

        for (var tick = 0; tick < 5; tick++) slice.AdvanceOneTick();
        slice.EliminateForLocalPlaytest(new PlayerId(2));
        for (var tick = 0; tick < 5; tick++) slice.AdvanceOneTick();
        slice.EliminateForLocalPlaytest(new PlayerId(3));
        slice.AdvanceOneTick();

        var firstSummary = slice.MatchSummary;
        Assert.NotNull(firstSummary);
        var firstPlacements = firstSummary!.Players.ToDictionary(player => player.PlayerId.Value, player => player.Placement);
        Assert.Equal(2, firstPlacements[3]);
        Assert.Equal(3, firstPlacements[2]);

        slice.Reset();
        slice.StartMatch();

        for (var tick = 0; tick < 5; tick++) slice.AdvanceOneTick();
        slice.EliminateForLocalPlaytest(new PlayerId(3));
        for (var tick = 0; tick < 5; tick++) slice.AdvanceOneTick();
        slice.EliminateForLocalPlaytest(new PlayerId(2));
        slice.AdvanceOneTick();

        var secondSummary = slice.MatchSummary;
        Assert.NotNull(secondSummary);
        var secondPlacements = secondSummary!.Players.ToDictionary(player => player.PlayerId.Value, player => player.Placement);
        Assert.Equal(2, secondPlacements[2]);
        Assert.Equal(3, secondPlacements[3]);
    }
}
