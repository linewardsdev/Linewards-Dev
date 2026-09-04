using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using LTW.Simulation.Replay;
using LTW.Simulation.Seats;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-02: real opponents with no server and no network,
/// built on MP-00's command log and MP-01's seat table. See docs/MULTIPLAYER_ROLLOUT.md.
/// </summary>
public sealed class RecordedSeatTests
{
    private static MatchReplayRecord PlayReferenceMatch(int ticks)
    {
        var reference = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default);
        for (var tick = 0; tick < ticks && reference.MatchSummary is null; tick++)
        {
            reference.AdvanceOneTick();
        }

        return reference.GetMatchReplayRecord();
    }

    [Fact]
    public void A_match_of_seven_recorded_seats_runs_to_a_result_with_no_server_and_no_network()
    {
        // The reference configuration (LocalMatchOptions.Default, seat 1 undriven like every
        // match in this suite) concludes around tick 3849 — probed directly rather than guessed,
        // since OPEN_ITEMS.md itself notes one seed completing at 926 and another past 80,000.
        const int budget = 8000;
        var recording = PlayReferenceMatch(budget);
        Assert.True(recording.Commands.Count(command => !command.PlayerId.Equals(new PlayerId(1))) > 50,
            "the reference match should have given every non-local seat something to record");

        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        for (var seat = 2; seat <= 8; seat++)
        {
            var playerId = new PlayerId(seat);
            Assert.True(slice.AssignRecordedOpponent(playerId, recording, playerId, $"Ghost {seat}"));
            Assert.Equal(SeatRole.Recorded, slice.GetSeatTable().For(playerId).Role);
        }

        for (var tick = 0; tick < budget && slice.MatchSummary is null; tick++)
        {
            slice.AdvanceOneTick();
        }

        Assert.NotNull(slice.MatchSummary);
    }

    [Fact]
    public void A_content_version_mismatch_falls_back_to_a_bot_and_reports_it()
    {
        var recording = PlayReferenceMatch(50);
        var mismatched = new MatchReplayRecord(recording.Seed, "not-the-real-version", recording.MapId, recording.Players, recording.CompletedAtTick, recording.Commands);

        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        var target = new PlayerId(2);

        Assert.False(slice.AssignRecordedOpponent(target, mismatched, target, "Ghost"));

        var seat = slice.GetSeatTable().For(target);
        Assert.Equal(SeatRole.Bot, seat.Role);
        Assert.NotNull(seat.BotProfile);

        var fallback = slice.DrainEvents().OfType<RecordedSeatFallbackEvent>().Single();
        Assert.Equal(target, fallback.PlayerId);
        Assert.Contains("content version mismatch", fallback.Reason);
    }

    [Fact]
    public void A_recording_that_ends_early_is_bot_filled_from_that_tick()
    {
        var full = PlayReferenceMatch(1500);
        var target = new PlayerId(2);

        // Truncated to a tick WINDOW, not a fixed command count: taking the first handful of
        // commands outright risked grabbing several that all landed on tick 0 (the opening-turn
        // seeding), which would have exhausted the recording within its very first tick and left
        // nothing to observe "still Recorded" against — this needs commands spread across several
        // real ticks to test anything.
        const int window = 100;
        var truncated = new MatchReplayRecord(
            full.Seed,
            full.ContentVersion,
            full.MapId,
            full.Players,
            full.CompletedAtTick,
            full.Commands.Where(command => command.PlayerId.Equals(target) && command.Tick.Value <= window).ToArray());
        Assert.True(truncated.Commands.Count >= 2, "need at least two recorded commands for this to test anything");
        var lastRecordedTick = truncated.Commands.Max(command => command.Tick.Value);
        Assert.True(truncated.Commands.Select(command => command.Tick.Value).Distinct().Count() > 1,
            "need commands spread across more than one tick, or there is no 'still Recorded' window to observe");

        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        Assert.True(slice.AssignRecordedOpponent(target, truncated, target, "Ghost"));

        // Strictly before the tick that plays the LAST recorded command: that tick's own
        // processing is what exhausts the driver and retires it, in the same AdvanceOneTick call
        // — there is no separate tick where the table still reads Recorded after the last action
        // played, only up to and not including it.
        for (var tick = 0; tick < lastRecordedTick; tick++)
        {
            slice.AdvanceOneTick();
            Assert.Equal(SeatRole.Recorded, slice.GetSeatTable().For(target).Role);
        }

        // The tick that plays the last recorded command also retires the driver, within the same
        // call — the table reads Bot immediately after, not one tick later.
        slice.AdvanceOneTick();
        var seat = slice.GetSeatTable().For(target);
        Assert.Equal(SeatRole.Bot, seat.Role);
        Assert.NotNull(seat.BotProfile);

        // And the seat keeps playing afterward — this is a hand-off, not the seat going quiet.
        var activityAfter = 0;
        for (var tick = 0; tick < 800 && slice.MatchSummary is null; tick++)
        {
            slice.AdvanceOneTick();
            activityAfter += slice.DrainEvents().Count(evt => evt is TowerPlacedEvent placed && placed.PlayerId.Equals(target));
        }

        Assert.True(activityAfter > 0, "the fallback bot never did anything after taking over");
    }

    [Fact]
    public void Assigning_a_recorded_opponent_to_the_local_seat_is_rejected()
    {
        var recording = PlayReferenceMatch(50);
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);

        Assert.False(slice.AssignRecordedOpponent(slice.LocalPlayerId, recording, new PlayerId(2), "Ghost"));
        Assert.Equal(SeatRole.Human, slice.GetSeatTable().For(slice.LocalPlayerId).Role);
    }

    [Fact]
    public void Reset_restores_a_stand_in_seat_to_its_configured_bot()
    {
        var recording = PlayReferenceMatch(50);
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default);

        // Seat 4 specifically: LocalMatchOptions.DefaultLanes configures it Greedy, not the
        // Balanced every stand-in bot plays as — seat 2's own default happens to BE Balanced,
        // which would make this assertion pass even if Reset installed a fresh stand-in instead
        // of actually restoring the seat's own configuration.
        var target = new PlayerId(4);
        var originalProfile = slice.GetSeatTable().For(target).BotProfile;
        Assert.Equal(LTW.Simulation.Bots.BotDecisionProfile.Greedy, originalProfile);

        var mismatched = new MatchReplayRecord(recording.Seed, "wrong-version", recording.MapId, recording.Players, recording.CompletedAtTick, recording.Commands);
        Assert.False(slice.AssignRecordedOpponent(target, mismatched, target, "Ghost"));
        Assert.NotEqual(originalProfile, slice.GetSeatTable().For(target).BotProfile);

        slice.Reset();

        Assert.Equal(SeatRole.Bot, slice.GetSeatTable().For(target).Role);
        Assert.Equal(originalProfile, slice.GetSeatTable().For(target).BotProfile);
    }
}
