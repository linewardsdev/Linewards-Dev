using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

public sealed class LocalVerticalSliceTests
{
    [Fact]
    public void Player_one_send_spawns_in_player_twos_lane_and_is_recorded_for_replay()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

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
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);

        slice.Reset();

        Assert.Empty(slice.GetReplayRecord().AcceptedCommands);
    }
}
