using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// What a seat may still do after it is out.
/// </summary>
/// <remarks>
/// Elimination is a state the seat cannot come back from, so every command that changes the board
/// has to refuse it. The half that was already enforced is the half that reads oddly in isolation:
/// <c>UpgradeTower</c>, <c>SellTowers</c> and <c>BuyCategoryTier</c> all check, while
/// <c>PlaceTower</c> and <c>SellTowerAt</c> did not, and <c>QueueSend</c> checked only that the
/// *target* was alive rather than the sender.
///
/// The gap is not theoretical for bots. <c>AdvanceOneTick</c> calls <c>TakeTurn</c> for every bot in
/// the dictionary with no elimination filter, so a defeated bot keeps acting; what stopped it was
/// entirely whatever the individual commands happened to check. A defeated seat rebuilding its lane
/// is the visible symptom, and it is the reason these are tested at the bridge rather than trusted
/// to the caller.
/// </remarks>
public sealed class EliminatedSeatTests
{
    /// <summary>
    /// A full eight-lane table, deliberately, not the two-seat default.
    /// </summary>
    /// <remarks>
    /// <c>QueueSend</c> returns <see cref="CommandRejectionReason.PlayerEliminated"/> for two
    /// different situations: the sender being out, and there being no live opponent left to send
    /// to. On a small table those are impossible to tell apart — eliminate the only opponent and
    /// the send is refused for the second reason while the first is untested. Seven other seats are
    /// still alive here, so a refusal can only be about the sender.
    /// </remarks>
    private static LocalVerticalSlice Slice()
    {
        var slice = new LocalVerticalSlice(
            SampleVerticalSliceContent.Create(),
            new LocalMatchOptions(laneCount: LocalMatchOptions.MaxLaneCount),
            enableBots: false);
        slice.GrantLocalPlaytestGold(slice.LocalPlayerId, new Gold(100_000));
        return slice;
    }

    [Fact]
    public void An_eliminated_seat_cannot_place_a_tower()
    {
        var slice = Slice();
        var seat = slice.LocalPlayerId;
        var lane = slice.LocalPlayerLaneId;

        // Placing before elimination establishes that the position and funds are otherwise fine, so
        // a rejection afterwards can only be the elimination.
        Assert.True(slice.PlaceTower(seat, lane, SampleVerticalSliceContent.TowerId, new GridPosition(1, 3)).Accepted);

        slice.EliminateForLocalPlaytest(seat);

        var rebuilt = slice.PlaceTower(seat, lane, SampleVerticalSliceContent.TowerId, new GridPosition(1, 5));
        Assert.False(rebuilt.Accepted);
        Assert.Equal(CommandRejectionReason.PlayerEliminated, rebuilt.RejectionReason);
    }

    [Fact]
    public void An_eliminated_seat_cannot_send()
    {
        var slice = Slice();
        var seat = slice.LocalPlayerId;

        Assert.True(slice.QueueSend(seat, SampleVerticalSliceContent.CreepId).Accepted);

        slice.EliminateForLocalPlaytest(seat);

        var sent = slice.QueueSend(seat, SampleVerticalSliceContent.CreepId);
        Assert.False(sent.Accepted);
        Assert.Equal(CommandRejectionReason.PlayerEliminated, sent.RejectionReason);
    }

    [Fact]
    public void An_eliminated_seat_cannot_sell_a_tower()
    {
        var slice = Slice();
        var seat = slice.LocalPlayerId;
        var lane = slice.LocalPlayerLaneId;
        var cell = new GridPosition(1, 3);

        Assert.True(slice.PlaceTower(seat, lane, SampleVerticalSliceContent.TowerId, cell).Accepted);

        // Eliminating wipes the lane, so the tower this would have sold is already gone and the
        // command has to refuse on the seat's state rather than on finding nothing there. Asserting
        // the reason rather than just the rejection is what separates the two.
        slice.EliminateForLocalPlaytest(seat);

        var sold = slice.SellTowerAt(seat, lane, cell);
        Assert.False(sold.Accepted);
        Assert.Equal(CommandRejectionReason.PlayerEliminated, sold.RejectionReason);
    }
}
