using LTW.Simulation.Bridge;
using Xunit;

namespace LTW.Tests;

public sealed class LocalThreePlayerMatchTests
{
    [Fact]
    public void Two_bots_complete_a_local_carousel_match()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        for (var tick = 0; tick < 6_000 && slice.MatchSummary is null; tick++) slice.AdvanceOneTick();

        Assert.NotNull(slice.MatchSummary);
        Assert.InRange(slice.MatchSummary!.CompletedAtTick.Value, 300, 2_500);
        Assert.NotEmpty(slice.GetReplayRecord().AcceptedCommands);
    }
}
