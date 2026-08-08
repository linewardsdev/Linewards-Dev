using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// The send queue: tap now, pay when the gold arrives.
/// </summary>
/// <remarks>
/// This exists because the game is played on a phone. Sending means opening the dock, finding a
/// card and tapping it, and a player cannot be asked to do that at the exact moment income lands.
/// Queueing lets them state intent once and have it resolve as the economy allows.
///
/// Four properties, and they are the whole contract: it holds up to ten of any one creep, it pays
/// oldest first, an entry it cannot afford waits rather than failing, and nothing is charged while
/// it waits.
/// </remarks>
public sealed class SendQueueTests
{
    private readonly ITestOutputHelper output;

    public SendQueueTests(ITestOutputHelper output) => this.output = output;

    private static readonly PlayerId Seat = new(1);

    private static LocalVerticalSlice Slice(int gold)
    {
        var slice = new LocalVerticalSlice(
            SampleVerticalSliceContent.Create(),
            new LocalMatchOptions(seed: 1, laneCount: LocalMatchOptions.MaxLaneCount),
            enableBots: false);
        slice.StartMatch();
        slice.GrantLocalPlaytestGold(Seat, new Gold(gold));
        return slice;
    }

    private static int Gold(LocalVerticalSlice slice) => slice.GetSnapshot().Players.Get(Seat).Gold.Amount;

    [Fact]
    public void Ten_of_one_creep_may_wait_and_an_eleventh_is_refused()
    {
        var slice = Slice(0);
        var creep = SampleVerticalSliceContent.CreepId;

        for (var i = 0; i < LocalVerticalSlice.MaxQueuedSendsPerCreep; i++)
        {
            Assert.True(slice.EnqueueSend(Seat, creep).Accepted, $"queueing copy {i + 1} was refused");
        }

        var eleventh = slice.EnqueueSend(Seat, creep);
        Assert.False(eleventh.Accepted);
        Assert.Equal(CommandRejectionReason.SendQueueFull, eleventh.RejectionReason);
        Assert.Equal(10, slice.QueuedSendCountFor(Seat, creep));

        // The cap is per creep, not per queue: a different creep still queues on top of a full one.
        Assert.True(slice.EnqueueSend(Seat, SampleVerticalSliceContent.BruteCreepId).Accepted);
        Assert.Equal(11, slice.SendQueueFor(Seat).Count);
    }

    [Fact]
    public void A_queue_longer_than_the_bank_stops_where_the_gold_runs_out()
    {
        var slice = Slice(0);
        var creep = SampleVerticalSliceContent.BruteCreepId;

        // Queued past what the opening bank can pay for, rather than starting from zero: gold can
        // only be added through the bridge, so "broke" has to be reached by spending.
        for (var i = 0; i < LocalVerticalSlice.MaxQueuedSendsPerCreep; i++)
        {
            Assert.True(slice.EnqueueSend(Seat, creep).Accepted);
        }

        for (var tick = 0; tick < 5; tick++)
        {
            slice.AdvanceOneTick();
        }

        var cost = SampleVerticalSliceContent.Create().Creeps.First(c => c.Id.Equals(creep)).Cost.Amount;
        var remaining = slice.SendQueueFor(Seat).Count;
        output.WriteLine($"  {cost}g each, bank ran down to {Gold(slice)}g, {remaining} still waiting");

        // Some went, some are still waiting, and what is waiting is waiting because the seat cannot
        // pay for it — not because it was dropped.
        Assert.InRange(remaining, 1, LocalVerticalSlice.MaxQueuedSendsPerCreep - 1);
        Assert.True(Gold(slice) < cost, $"queue stopped at {remaining} while still holding {Gold(slice)}g for a {cost}g creep");
    }

    [Fact]
    public void Gold_arriving_releases_the_queue_without_another_tap()
    {
        var slice = Slice(0);
        var creep = SampleVerticalSliceContent.BruteCreepId;

        // Past what the opening bank covers, so there is something left waiting to be released.
        for (var i = 0; i < LocalVerticalSlice.MaxQueuedSendsPerCreep; i++)
        {
            Assert.True(slice.EnqueueSend(Seat, creep).Accepted);
        }

        slice.AdvanceOneTick();
        var stranded = slice.SendQueueFor(Seat).Count;
        Assert.True(stranded > 0, "the bank paid for the whole queue, so nothing was left to release");

        // The whole point of the feature: the player does not come back and tap again.
        slice.GrantLocalPlaytestGold(Seat, new Gold(500));
        slice.AdvanceOneTick();

        output.WriteLine($"  {stranded} were stranded, then gold arrived: queue {slice.SendQueueFor(Seat).Count}");
        Assert.Empty(slice.SendQueueFor(Seat));
    }

    [Fact]
    public void The_queue_pays_oldest_first_and_does_not_skip_ahead()
    {
        var slice = Slice(0);
        var cheap = SampleVerticalSliceContent.CreepId;
        var dear = SampleVerticalSliceContent.BruteCreepId;

        // Enough dear ones to outrun the bank, then one cheap one behind them. A queue that looked
        // past the entry it could not afford would find the cheap one and send it, reordering what
        // the player asked for.
        for (var i = 0; i < LocalVerticalSlice.MaxQueuedSendsPerCreep; i++)
        {
            Assert.True(slice.EnqueueSend(Seat, dear).Accepted);
        }

        Assert.True(slice.EnqueueSend(Seat, cheap).Accepted);

        var content = SampleVerticalSliceContent.Create();
        var cheapCost = content.Creeps.First(c => c.Id.Equals(cheap)).Cost.Amount;
        var dearCost = content.Creeps.First(c => c.Id.Equals(dear)).Cost.Amount;
        Assert.True(dearCost > cheapCost, "this test needs the blocked entry to be the dearer one");

        for (var tick = 0; tick < 5; tick++)
        {
            slice.AdvanceOneTick();
        }

        var queue = slice.SendQueueFor(Seat);
        output.WriteLine($"  cheap {cheapCost}g, dear {dearCost}g, {Gold(slice)}g left, {queue.Count} waiting, head is {queue[0].Value}");

        // Blocked on a dear entry it cannot pay for, with the affordable cheap one still behind it.
        Assert.Equal(dear, queue[0]);
        Assert.Equal(cheap, queue[queue.Count - 1]);
        Assert.True(Gold(slice) < dearCost);
    }

    [Fact]
    public void An_eliminated_seat_cannot_queue()
    {
        var slice = Slice(10_000);
        slice.EliminateForLocalPlaytest(Seat);

        var result = slice.EnqueueSend(Seat, SampleVerticalSliceContent.CreepId);
        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.PlayerEliminated, result.RejectionReason);
    }
}
