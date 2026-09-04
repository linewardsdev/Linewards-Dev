using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-00 exit signal: a recorded match replays bit-for-bit.
/// See docs/MULTIPLAYER_ROLLOUT.md for where this sits in the rollout.
/// </summary>
public sealed class MatchReplayTests
{
    [Fact]
    public void Full_eight_lane_bot_match_replays_to_an_identical_fingerprint()
    {
        var options = LocalMatchOptions.Default;
        var original = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        for (var tick = 0; tick < 1500 && original.MatchSummary is null; tick++)
        {
            original.AdvanceOneTick();
        }

        var record = original.GetMatchReplayRecord();

        // A weak reference match (no bots, one tower) would pass this test trivially. Eight bot
        // lanes over a real span of ticks should produce dozens of placements, sends and tier
        // purchases — if it does not, the fixture stopped meaning anything before it started
        // proving something.
        Assert.True(record.Commands.Count > 50, $"expected a substantial command log, got {record.Commands.Count}");

        var replayed = LocalVerticalSlice.Replay(record, SampleVerticalSliceContent.Create(), options);

        Assert.Equal(original.CurrentTick, replayed.CurrentTick);
        Assert.Equal(original.GetSnapshot().Fingerprint(), replayed.GetSnapshot().Fingerprint());

        // The replayed match's OWN command log should be the same log played back to it — the
        // property that lets a replay be replayed again, and the check that would catch a replay
        // silently dropping or reordering a command that happened not to move the fingerprint.
        var replayedLog = replayed.GetMatchReplayRecord().Commands;
        Assert.Equal(record.Commands.Count, replayedLog.Count);
        for (var index = 0; index < record.Commands.Count; index++)
        {
            Assert.Equal(record.Commands[index].Kind, replayedLog[index].Kind);
            Assert.Equal(record.Commands[index].Tick, replayedLog[index].Tick);
            Assert.Equal(record.Commands[index].PlayerId, replayedLog[index].PlayerId);
            Assert.Equal(record.Commands[index].Position, replayedLog[index].Position);
            Assert.Equal(record.Commands[index].Quantity, replayedLog[index].Quantity);
        }
    }

    [Fact]
    public void Commands_in_the_same_tick_are_ordered_by_true_call_order()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        var localId = slice.LocalPlayerId;
        var laneId = slice.LocalPlayerLaneId;

        // Two placements back to back, with no tick advanced between them — both land at
        // CurrentTick 0, and Sequence is the only record of which happened first.
        Assert.True(slice.PlaceTower(localId, laneId, SampleVerticalSliceContent.TowerId, new GridPosition(2, 14)).Accepted);
        Assert.True(slice.PlaceTower(localId, laneId, SampleVerticalSliceContent.ControlTowerId, new GridPosition(4, 13)).Accepted);

        var log = slice.GetMatchReplayRecord().Commands;
        Assert.True(log.Count >= 2);
        var arrow = log[^2];
        var control = log[^1];

        Assert.Equal(slice.CurrentTick, arrow.Tick);
        Assert.Equal(slice.CurrentTick, control.Tick);
        Assert.True(arrow.Sequence < control.Sequence);
        Assert.Equal(SampleVerticalSliceContent.TowerId, arrow.ContentId);
        Assert.Equal(SampleVerticalSliceContent.ControlTowerId, control.ContentId);
    }

    [Fact]
    public void Reset_clears_the_command_log_and_its_sequence_counter()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        Assert.True(slice.PlaceTower(slice.LocalPlayerId, slice.LocalPlayerLaneId, SampleVerticalSliceContent.TowerId, new GridPosition(2, 14)).Accepted);
        Assert.NotEmpty(slice.GetMatchReplayRecord().Commands);

        slice.Reset();
        Assert.Empty(slice.GetMatchReplayRecord().Commands);

        Assert.True(slice.PlaceTower(slice.LocalPlayerId, slice.LocalPlayerLaneId, SampleVerticalSliceContent.TowerId, new GridPosition(2, 14)).Accepted);
        Assert.Equal(0, slice.GetMatchReplayRecord().Commands[0].Sequence);
    }
}
