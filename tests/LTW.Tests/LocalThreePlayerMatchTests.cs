using System.Linq;
using LTW.Simulation.Bridge;
using Xunit;

namespace LTW.Tests;

public sealed class LocalThreePlayerMatchTests
{
    [Fact]
    public void Bots_build_opening_defense_before_first_send_pressure()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        for (var tick = 0; tick < 30; tick++) slice.AdvanceOneTick();

        var snapshot = slice.GetSnapshot();
        Assert.True(snapshot.Towers.Count(tower => tower.OwnerId.Value == 2) >= 2);
        Assert.True(snapshot.Towers.Count(tower => tower.OwnerId.Value == 3) >= 3);
        Assert.Empty(slice.GetBotDiagnostics().RecentDecisions);
    }

    [Fact]
    public void Two_bots_complete_a_local_carousel_match()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        for (var tick = 0; tick < 6_000 && slice.MatchSummary is null; tick++) slice.AdvanceOneTick();

        Assert.NotNull(slice.MatchSummary);
        Assert.InRange(slice.MatchSummary!.CompletedAtTick.Value, 900, 1_800);
        Assert.NotEmpty(slice.GetReplayRecord().AcceptedCommands);
    }

    [Fact]
    public void Completed_local_match_does_not_advance_after_results()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        for (var tick = 0; tick < 6_000 && slice.MatchSummary is null; tick++) slice.AdvanceOneTick();

        Assert.NotNull(slice.MatchSummary);
        var completedAt = slice.MatchSummary!.CompletedAtTick;
        var replayCommandCount = slice.GetReplayRecord().AcceptedCommands.Count;
        var snapshot = slice.GetSnapshot();

        for (var tick = 0; tick < 500; tick++) slice.AdvanceOneTick();

        Assert.Equal(completedAt, slice.MatchSummary.CompletedAtTick);
        Assert.Equal(snapshot.Tick, slice.GetSnapshot().Tick);
        Assert.Equal(replayCommandCount, slice.GetReplayRecord().AcceptedCommands.Count);
    }
}
