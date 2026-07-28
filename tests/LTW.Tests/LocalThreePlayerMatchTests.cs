using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Bridge;
using Xunit;

namespace LTW.Tests;

public sealed class LocalThreePlayerMatchTests
{
    /// <summary>
    /// Replaces an earlier version of this test that asserted "no bot decisions in the first 30
    /// ticks" — that was calibrated against the old tick-scheduled gold-reserve heuristics.
    /// Reactive, gold-gated tower building can legitimately finish a profile's opening package
    /// within a couple of ticks when starting gold covers it, so an early send is correct
    /// behavior now, not a bug. What must still hold, and what this checks directly instead: a
    /// bot never sends before its own profile's minimum tower coverage is actually met.
    /// </summary>
    [Fact]
    public void Bots_never_send_before_meeting_their_own_profiles_minimum_tower_coverage()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions());
        var minimumCoverage = new Dictionary<int, int> { [2] = 3, [3] = 4 };
        var validated = new HashSet<(long Tick, int PlayerId)>();

        for (var tick = 0; tick < 60; tick++)
        {
            slice.AdvanceOneTick();
            var snapshot = slice.GetSnapshot();

            foreach (var decision in slice.GetBotDiagnostics().RecentDecisions)
            {
                if (!validated.Add((decision.Tick.Value, decision.PlayerId.Value)))
                {
                    continue;
                }

                var ownedTowers = snapshot.Towers.Count(t => t.OwnerId.Value == decision.PlayerId.Value);
                Assert.True(ownedTowers >= minimumCoverage[decision.PlayerId.Value],
                    $"Player {decision.PlayerId.Value} sent at tick {decision.Tick.Value} with only {ownedTowers} towers (needs {minimumCoverage[decision.PlayerId.Value]}).");
            }
        }

        var finalSnapshot = slice.GetSnapshot();
        Assert.True(finalSnapshot.Towers.Count(t => t.OwnerId.Value == 2) >= 3);
        Assert.True(finalSnapshot.Towers.Count(t => t.OwnerId.Value == 3) >= 4);
    }

    [Fact]
    public void Two_bots_complete_a_local_carousel_match()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions());

        for (var tick = 0; tick < 6_000 && slice.MatchSummary is null; tick++) slice.AdvanceOneTick();

        Assert.NotNull(slice.MatchSummary);
        // Lower bound dropped again with the reactive-spending bot rework: bots now both build
        // proportionally more towers AND start sending as soon as their own coverage is met
        // (rather than waiting on a tick-scheduled gold reserve until tick 120-180+), so two bots
        // fighting each other resolves faster than either the original tick-scheduled bots or the
        // interim fixed-tower-count bots did. This is an accepted consequence of reactive bots,
        // not a regression — see docs/GD_TUNING_LOG.md's bot-rework entry.
        Assert.InRange(slice.MatchSummary!.CompletedAtTick.Value, 150, 900);
        Assert.NotEmpty(slice.GetReplayRecord().AcceptedCommands);
    }

    [Fact]
    public void Completed_local_match_does_not_advance_after_results()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions());

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

    [Fact]
    public void Local_match_options_control_replay_seed_and_bot_profiles()
    {
        var options = new LocalMatchOptions(seed: 202, laneCount: 3, botLanes: new[]
        {
            new BotLaneOptions(2, profile: BotDecisionProfile.Greedy, primaryCreepId: SampleVerticalSliceContent.BruteCreepId),
            new BotLaneOptions(3, profile: BotDecisionProfile.Balanced, primaryCreepId: SampleVerticalSliceContent.SwarmCreepId)
        });
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        var replay = slice.GetReplayRecord();
        var diagnostics = slice.GetBotDiagnostics();

        Assert.Equal(202, replay.Seed);
        Assert.Contains(diagnostics.Profiles, profile =>
            profile.PlayerId.Value == 2 &&
            profile.Profile == BotDecisionProfile.Greedy &&
            profile.PrimaryCreepId.Equals(SampleVerticalSliceContent.BruteCreepId));
        Assert.Contains(diagnostics.Profiles, profile =>
            profile.PlayerId.Value == 3 &&
            profile.Profile == BotDecisionProfile.Balanced &&
            profile.PrimaryCreepId.Equals(SampleVerticalSliceContent.SwarmCreepId));
    }

    private static LocalMatchOptions ThreeLaneOptions() => new(laneCount: 3);
}
