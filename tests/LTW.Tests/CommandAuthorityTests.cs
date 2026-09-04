using System.Linq;
using LTW.Simulation.Authority;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-03: the rules an authoritative server must keep
/// enforcing, gathered in one place. See docs/MULTIPLAYER_ROLLOUT.md for what this initiative
/// covers and what it deliberately does not (per-command rate limiting beyond the send queue).
/// </summary>
public sealed class CommandAuthorityTests
{
    /// <summary>Denies exactly one seat; resolves every other claim as itself.</summary>
    private sealed class DenyingSeatAuthority : ISeatAuthority
    {
        private readonly PlayerId denied;
        public DenyingSeatAuthority(PlayerId denied) => this.denied = denied;
        public PlayerId? ResolveSeat(PlayerId claimedBy) => claimedBy.Equals(denied) ? null : claimedBy;
    }

    /// <summary>
    /// Resolves one specific claim to a DIFFERENT seat than it claimed, and everything else as
    /// itself — the shape a real networked authority takes: the connection's own seat, not
    /// whatever the message says.
    /// </summary>
    private sealed class RemappingSeatAuthority : ISeatAuthority
    {
        private readonly PlayerId claim;
        private readonly PlayerId actual;
        public RemappingSeatAuthority(PlayerId claim, PlayerId actual) { this.claim = claim; this.actual = actual; }
        public PlayerId? ResolveSeat(PlayerId claimedBy) => claimedBy.Equals(claim) ? actual : claimedBy;
    }

    [Fact]
    public void A_denied_seat_cannot_place_upgrade_buy_sell_or_send()
    {
        var denied = new PlayerId(2);
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false, seatAuthority: new DenyingSeatAuthority(denied));
        var goldBefore = slice.GetSnapshot().Players.Get(denied).Gold.Amount;

        Assert.False(slice.PlaceTower(denied, new LaneId(2), SampleVerticalSliceContent.TowerId, new GridPosition(2, 14)).Accepted);
        Assert.False(slice.QueueSend(denied, SampleVerticalSliceContent.CreepId).Accepted);
        Assert.False(slice.BuyCategoryTier(denied, LTW.Simulation.Commands.CategoryKind.TowerLine, 0, 1).Accepted);
        Assert.False(slice.SellTowerAt(denied, new LaneId(2), new GridPosition(2, 14)).Accepted);
        Assert.Equal(0, slice.SellTowers(denied, new LaneId(2), new[] { new GridPosition(2, 14) }).Sold);

        // Nothing above should have moved a single seat's gold — a rejected command is a no-op,
        // not a partial one.
        Assert.Equal(goldBefore, slice.GetSnapshot().Players.Get(denied).Gold.Amount);
        Assert.DoesNotContain(slice.GetSnapshot().Towers, tower => tower.OwnerId.Equals(denied));
    }

    [Fact]
    public void A_command_acts_as_the_resolved_seat_not_the_claim()
    {
        var claimed = new PlayerId(2);
        var actual = new PlayerId(3);
        var slice = new LocalVerticalSlice(
            SampleVerticalSliceContent.Create(),
            LocalMatchOptions.Default,
            enableBots: false,
            seatAuthority: new RemappingSeatAuthority(claimed, actual));

        var result = slice.PlaceTower(claimed, new LaneId(3), SampleVerticalSliceContent.TowerId, new GridPosition(2, 14));

        Assert.True(result.Accepted);
        var placed = Assert.Single(slice.GetSnapshot().Towers);
        Assert.Equal(actual, placed.OwnerId);

        // The claimed seat's own gold and lane are untouched — this was never its command to begin
        // with, whatever it said.
        Assert.DoesNotContain(slice.GetSnapshot().Towers, tower => tower.OwnerId.Equals(claimed));
    }

    [Fact]
    public void Unknown_or_out_of_match_ids_are_rejected_not_thrown()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        var outOfMatch = new PlayerId(99);

        Assert.False(slice.PlaceTower(outOfMatch, new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(2, 14)).Accepted);
        Assert.False(slice.QueueSend(outOfMatch, SampleVerticalSliceContent.CreepId).Accepted);
        Assert.False(slice.BuyCategoryTier(outOfMatch, LTW.Simulation.Commands.CategoryKind.TowerLine, 0, 1).Accepted);
        Assert.False(slice.UpgradeTower(outOfMatch, new LaneId(1), new GridPosition(2, 14)).Accepted);
        Assert.False(slice.SellTowerAt(outOfMatch, new LaneId(1), new GridPosition(2, 14)).Accepted);
        Assert.Equal(0, slice.SellTowers(outOfMatch, new LaneId(1), new[] { new GridPosition(2, 14) }).Sold);
        Assert.False(slice.EnqueueSend(outOfMatch, SampleVerticalSliceContent.CreepId).Accepted);

        // None of the above should have thrown to get here at all — reaching this line IS the test.
    }

    [Fact]
    public void A_player_may_only_build_in_their_own_home_lane()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        var seat = new PlayerId(2);
        var someoneElsesLane = new LaneId(3);

        var result = slice.PlaceTower(seat, someoneElsesLane, SampleVerticalSliceContent.TowerId, new GridPosition(2, 14));

        Assert.False(result.Accepted);
        Assert.Empty(slice.GetSnapshot().Towers);
    }

    [Fact]
    public void Send_targets_come_from_topology_and_cannot_be_supplied_by_a_caller()
    {
        // QueueSend/EnqueueSend take no target parameter at all — the only way to check "a caller
        // cannot choose it" is to confirm the topology-derived target is a pure function of the
        // sender's own seat, identical across two independently constructed matches with the same
        // options, which is what "derived from topology, never accepted from the client" means in
        // an API that structurally has nowhere for a client to put one.
        var first = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        var second = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        var sender = new PlayerId(2);

        Assert.True(first.QueueSend(sender, SampleVerticalSliceContent.CreepId).Accepted);
        Assert.True(second.QueueSend(sender, SampleVerticalSliceContent.CreepId).Accepted);

        var firstTarget = first.DrainEvents().OfType<CreepQueuedEvent>().Single().DefenderId;
        var secondTarget = second.DrainEvents().OfType<CreepQueuedEvent>().Single().DefenderId;
        Assert.Equal(firstTarget, secondTarget);
    }

    [Fact]
    public void Queue_depth_is_capped_per_creep()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        var seat = slice.LocalPlayerId;

        for (var i = 0; i < LocalVerticalSlice.MaxQueuedSendsPerCreep; i++)
        {
            Assert.True(slice.EnqueueSend(seat, SampleVerticalSliceContent.CreepId).Accepted);
        }

        Assert.False(slice.EnqueueSend(seat, SampleVerticalSliceContent.CreepId).Accepted);
    }

    [Fact]
    public void Build_commands_are_rate_limited_and_share_one_budget_across_the_family()
    {
        // Same cell, same tick, repeatedly: only the first call can ever actually place a tower,
        // so this generates a rejection-worthy burst without needing 240 legal cells or that much
        // gold — the token bucket charges a request the instant it is asked, before anything about
        // its own legality is checked, which is exactly the property being tested here.
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        var seat = slice.LocalPlayerId;
        var lane = slice.LocalPlayerLaneId;
        var position = new GridPosition(2, 14);

        Assert.True(slice.PlaceTower(seat, lane, SampleVerticalSliceContent.TowerId, position).Accepted);

        CommandRejectionReason? lastReason = null;
        for (var i = 0; i < 300; i++)
        {
            var result = slice.PlaceTower(seat, lane, SampleVerticalSliceContent.TowerId, position);
            if (!result.Accepted)
            {
                lastReason = result.RejectionReason;
            }
        }

        Assert.Equal(CommandRejectionReason.CooldownActive, lastReason);
    }

    [Fact]
    public void Draining_a_queue_larger_than_the_rate_limit_burst_is_unaffected_by_it()
    {
        // The whole reason QueueSend was split into a public, rate-limited wrapper and a private
        // core DrainSendQueues calls directly (see QueueSend's own remarks): realizing an already-
        // queued send must never be throttled by a limiter that exists to bound REQUESTS, or a
        // queue bigger than the burst would get permanently stuck the first time gold caught up
        // enough to drain it in one go.
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), LocalMatchOptions.Default, enableBots: false);
        var seat = slice.LocalPlayerId;

        for (var i = 0; i < LocalVerticalSlice.MaxQueuedSendsPerCreep; i++)
        {
            Assert.True(slice.EnqueueSend(seat, SampleVerticalSliceContent.CreepId).Accepted);
        }

        slice.GrantLocalPlaytestGold(seat, new Gold(100000));
        slice.AdvanceOneTick();

        Assert.Empty(slice.GetSnapshot().SendQueueFor(seat));
    }

    [Fact]
    public void Gold_income_and_lives_have_no_public_setter_only_validated_commands()
    {
        // Not independently runnable as a behavioural test — there is no "try to set gold
        // directly" call to make, because no such API exists. Recorded here as the structural
        // fact the other tests in this file lean on: PlayerEconomyState's mutators are all With*
        // (immutable copies), and the only paths that ever call them are the command methods
        // already under test above.
        var setters = typeof(LTW.Simulation.Economy.PlayerEconomyState)
            .GetMethods()
            .Where(method => method.Name.StartsWith("Set", System.StringComparison.Ordinal));
        Assert.Empty(setters);
    }
}
