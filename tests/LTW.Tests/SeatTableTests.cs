using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using LTW.Simulation.Seats;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-01: a match described as a seat table rather than one
/// hardcoded human and a pile of bot flags. See docs/MULTIPLAYER_ROLLOUT.md for scope — today
/// there is still exactly one human seat; what these tests prove is the TABLE and the drop/
/// reconnect machinery, not multiple simultaneous humans.
/// </summary>
public sealed class SeatTableTests
{
    [Fact]
    public void Seat_roles_match_what_the_options_actually_configured()
    {
        var options = LocalMatchOptions.Default.WithLane(3, enabled: false);
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        var table = slice.GetSeatTable();
        Assert.Equal(8, table.Seats.Count);

        var localSeat = table.For(new PlayerId(1));
        Assert.Equal(SeatRole.Human, localSeat.Role);
        Assert.True(localSeat.IsConnected);
        Assert.Null(localSeat.BotProfile);

        var emptySeat = table.For(new PlayerId(3));
        Assert.Equal(SeatRole.Empty, emptySeat.Role);
        Assert.True(emptySeat.IsConnected);
        Assert.Null(emptySeat.BotProfile);

        var botSeat = table.For(new PlayerId(2));
        Assert.Equal(SeatRole.Bot, botSeat.Role);
        Assert.True(botSeat.IsConnected);
        Assert.NotNull(botSeat.BotProfile);
    }

    [Fact]
    public void All_bots_passive_needs_no_special_case_in_the_seat_table()
    {
        var options = LocalMatchOptions.Default.WithAllBotsPassive();
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        var table = slice.GetSeatTable();
        Assert.Equal(SeatRole.Human, table.For(slice.LocalPlayerId).Role);
        foreach (var seat in table.Seats.Where(seat => !seat.PlayerId.Equals(slice.LocalPlayerId)))
        {
            Assert.Equal(SeatRole.Bot, seat.Role);
            Assert.Equal(BotDecisionProfile.Passive, seat.BotProfile);
        }
    }

    [Fact]
    public void Dropping_the_local_seat_bot_fills_it_and_reconnecting_returns_it()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default);
        var localId = slice.LocalPlayerId;

        Assert.True(slice.GetSeatTable().For(localId).IsConnected);

        Assert.True(slice.DropSeat(localId));
        Assert.False(slice.DropSeat(localId), "a seat that is already dropped has nothing left to drop");

        var dropped = slice.GetSeatTable().For(localId);
        Assert.Equal(SeatRole.Human, dropped.Role);
        Assert.False(dropped.IsConnected);
        Assert.NotNull(dropped.BotProfile);

        Assert.True(slice.ReconnectSeat(localId));
        Assert.False(slice.ReconnectSeat(localId), "a seat that is already connected has nothing to reconnect");

        var reconnected = slice.GetSeatTable().For(localId);
        Assert.Equal(SeatRole.Human, reconnected.Role);
        Assert.True(reconnected.IsConnected);
        Assert.Null(reconnected.BotProfile);
    }

    [Fact]
    public void A_dropped_and_reconnected_seat_leaves_no_gap_a_replay_cannot_reproduce()
    {
        var options = LocalMatchOptions.Default;
        var original = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);
        var localId = original.LocalPlayerId;

        for (var tick = 0; tick < 200; tick++)
        {
            original.AdvanceOneTick();
        }

        Assert.True(original.DropSeat(localId));
        for (var tick = 0; tick < 200; tick++)
        {
            original.AdvanceOneTick();
        }

        Assert.True(original.ReconnectSeat(localId));
        for (var tick = 0; tick < 200 && original.MatchSummary is null; tick++)
        {
            original.AdvanceOneTick();
        }

        // The stand-in bot must actually have played, or this proves nothing about a drop.
        var duringDrop = original.GetMatchReplayRecord().Commands.Count(command => command.PlayerId.Equals(localId));
        Assert.True(duringDrop > 0, "the dropped seat's stand-in never issued a single command");

        var record = original.GetMatchReplayRecord();
        var replayed = LocalVerticalSlice.Replay(record, SampleVerticalSliceContent.Create(), options);

        Assert.Equal(original.GetSnapshot().Fingerprint(), replayed.GetSnapshot().Fingerprint());
    }
}
