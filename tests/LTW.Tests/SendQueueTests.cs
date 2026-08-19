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

    /// <summary>
    /// Cancelling takes back the send the player just added, not the one about to go out.
    /// </summary>
    /// <remarks>
    /// The queue drains front-first, so the front entry is already paid for in intent — it is the
    /// next thing to leave. Cancelling that would take back a DIFFERENT send than the one just
    /// tapped, which is the opposite of an undo. Asserted through the surviving order rather than
    /// through a count, because a count passes whichever end is removed.
    /// </remarks>
    [Fact]
    public void Cancelling_removes_the_most_recent_of_that_creep()
    {
        var slice = Slice(0);
        var brute = SampleVerticalSliceContent.BruteCreepId;
        var runner = SampleVerticalSliceContent.SwarmCreepId;

        Assert.True(slice.EnqueueSend(Seat, brute).Accepted);
        Assert.True(slice.EnqueueSend(Seat, runner).Accepted);
        Assert.True(slice.EnqueueSend(Seat, brute).Accepted);

        Assert.True(slice.CancelQueuedSend(Seat, brute).Accepted);

        // The FIRST brute must survive and keep its place ahead of the runner.
        var queue = slice.SendQueueFor(Seat);
        Assert.Equal(2, queue.Count);
        Assert.Equal(brute, queue[0]);
        Assert.Equal(runner, queue[1]);
    }

    [Fact]
    public void Cancelling_something_that_is_not_queued_says_so()
    {
        var slice = Slice(0);
        Assert.True(slice.EnqueueSend(Seat, SampleVerticalSliceContent.BruteCreepId).Accepted);

        var result = slice.CancelQueuedSend(Seat, SampleVerticalSliceContent.SwarmCreepId);

        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.NothingQueued, result.RejectionReason);
        // And the queue it did not own is untouched.
        Assert.Single(slice.SendQueueFor(Seat));
    }

    /// <summary>
    /// A cancel cannot reach another seat's queue.
    /// </summary>
    /// <remarks>
    /// The mirror of the enqueue authority test. Emptying someone else's queue is a cheaper attack
    /// than filling it, so the seat has to come from the authority here too — in-process the
    /// argument and the resolved seat are the same value, which is exactly why this must be pinned
    /// now rather than when a client starts supplying the id over a wire.
    /// </remarks>
    [Fact]
    public void Clearing_only_empties_the_callers_own_queue()
    {
        var slice = Slice(0);
        var other = new PlayerId(2);
        Assert.True(slice.EnqueueSend(Seat, SampleVerticalSliceContent.BruteCreepId).Accepted);
        Assert.True(slice.EnqueueSend(other, SampleVerticalSliceContent.BruteCreepId).Accepted);

        var removed = slice.ClearSendQueue(Seat);

        Assert.Equal(1, removed);
        Assert.Empty(slice.SendQueueFor(Seat));
        Assert.Single(slice.SendQueueFor(other));
    }

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

    /// <summary>One seat's queue is its own.</summary>
    /// <remarks>
    /// The property an authoritative server depends on. A queue is per-seat private intent, so
    /// filling one seat's must not consume another's capacity or reorder what it asked for.
    /// </remarks>
    [Fact]
    public void Queues_are_per_seat()
    {
        var slice = Slice(0);
        var other = new PlayerId(2);
        var creep = SampleVerticalSliceContent.CreepId;

        for (var i = 0; i < LocalVerticalSlice.MaxQueuedSendsPerCreep; i++)
        {
            Assert.True(slice.EnqueueSend(Seat, creep).Accepted);
        }

        Assert.Equal(CommandRejectionReason.SendQueueFull, slice.EnqueueSend(Seat, creep).RejectionReason);

        // Seat 1 being full says nothing about seat 2.
        Assert.True(slice.EnqueueSend(other, creep).Accepted);
        Assert.Equal(10, slice.QueuedSendCountFor(Seat, creep));
        Assert.Equal(1, slice.QueuedSendCountFor(other, creep));
    }

    /// <summary>An unknown creep is refused by the shared content validator, not by the bridge.</summary>
    /// <remarks>
    /// Matters because a server has to refuse a malformed enqueue with the same reason code it uses
    /// for a malformed send. A bridge-local check would be a second opinion about what a valid creep
    /// is, free to drift from the one every other command is held to.
    /// </remarks>
    [Fact]
    public void An_unknown_creep_is_refused()
    {
        var slice = Slice(0);
        var result = slice.EnqueueSend(Seat, new LTW.Simulation.Content.ContentId("creep.does_not_exist"));

        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.UnknownCreep, result.RejectionReason);
        Assert.Empty(slice.SendQueueFor(Seat));
    }

    /// <summary>
    /// The replay stream records the send the queue produced, once, at the tick it happened.
    /// </summary>
    /// <remarks>
    /// This is the claim the design rests on: the queue is intent and the send is the fact, so only
    /// the send is recorded. If enqueues were recorded too, a replay would apply the intent AND its
    /// effect and double every queued send. Asserted rather than assumed, because nothing else in
    /// the suite would notice.
    /// </remarks>
    [Fact]
    public void A_queued_send_is_recorded_once_when_it_is_paid_for()
    {
        var slice = Slice(0);
        var creep = SampleVerticalSliceContent.CreepId;

        Assert.True(slice.EnqueueSend(Seat, creep).Accepted);
        var beforeDrain = slice.GetReplayRecord().AcceptedCommands.Count(c => c.PlayerId.Equals(Seat));

        slice.AdvanceOneTick();

        var afterDrain = slice.GetReplayRecord().AcceptedCommands.Count(c => c.PlayerId.Equals(Seat));
        output.WriteLine($"  accepted commands for the seat: {beforeDrain} before the drain, {afterDrain} after");

        // Exactly one new record: the send. The enqueue itself left no trace, which is what stops a
        // replay double-sending.
        Assert.Equal(beforeDrain + 1, afterDrain);
        Assert.Empty(slice.SendQueueFor(Seat));
    }

    /// <summary>A seat that is not in this match cannot queue into it.</summary>
    /// <remarks>
    /// The seat-spoofing guard, exercised through the only door a client has. In-process the
    /// claimed id is always the local seat, so this is the test that keeps
    /// <see cref="LTW.Simulation.Authority.ISeatAuthority"/> honest before a server exists to
    /// exercise it properly — without it the boundary is a comment.
    /// </remarks>
    [Fact]
    public void A_seat_outside_the_match_is_refused()
    {
        var slice = Slice(0);
        var notInThisMatch = new PlayerId(99);

        var result = slice.EnqueueSend(notInThisMatch, SampleVerticalSliceContent.CreepId);

        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.InvalidPlayer, result.RejectionReason);
        Assert.Empty(slice.SendQueueFor(notInThisMatch));
    }

    /// <summary>Asking faster than the limiter allows is refused, without consuming queue depth.</summary>
    /// <remarks>
    /// Request rate, not game rule: the refusals here are for ASKING too often, and they land while
    /// the queue still has room. A limiter that only bit once the queue was full would be the cap
    /// wearing a different hat.
    /// </remarks>
    [Fact]
    public void Asking_far_faster_than_a_human_is_throttled()
    {
        var slice = Slice(0);
        var creep = SampleVerticalSliceContent.CreepId;
        var accepted = 0;
        var throttled = 0;

        // Far past the largest honest burst the game can produce. A player filling every card
        // queues about 150 entries across the roster, so the limiter has to sit well above that
        // and still stop this.
        for (var i = 0; i < 2_000; i++)
        {
            var result = slice.EnqueueSend(Seat, creep);
            if (result.Accepted)
            {
                accepted++;
            }
            else if (result.RejectionReason == CommandRejectionReason.CooldownActive)
            {
                throttled++;
            }
        }

        output.WriteLine($"  2000 requests on one tick: {accepted} accepted, {throttled} throttled");
        Assert.True(throttled > 0, "the limiter never bit across 2000 requests on a single tick");

        // The queue cap, not the limiter, is what stopped the accepted ones: a seat may hold ten of
        // this creep and did. If the limiter were doing the stopping it would bite below that, and
        // a real player filling a card would feel it.
        Assert.Equal(LocalVerticalSlice.MaxQueuedSendsPerCreep, accepted);
    }

    /// <summary>The client-facing queue comes off the snapshot, not out of the simulation.</summary>
    /// <remarks>
    /// Under a server the snapshot is what arrives over the wire, so anything the UI shows has to
    /// be reachable from it. This asserts the two views agree, which is what makes reading the
    /// snapshot a safe substitute rather than a second source of truth.
    /// </remarks>
    [Fact]
    public void The_snapshot_carries_the_queue()
    {
        var slice = Slice(0);
        var creep = SampleVerticalSliceContent.BruteCreepId;

        for (var i = 0; i < 3; i++)
        {
            Assert.True(slice.EnqueueSend(Seat, creep).Accepted);
        }

        var snapshot = slice.GetSnapshot();
        Assert.Equal(slice.SendQueueFor(Seat).Count, snapshot.SendQueueFor(Seat).Count);
        Assert.Equal(slice.QueuedSendCountFor(Seat, creep), snapshot.QueuedSendCountFor(Seat, creep));
        Assert.Equal(3, snapshot.QueuedSendCountFor(Seat, creep));
    }
}
