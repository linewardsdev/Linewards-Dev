using LTW.MatchServer;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.MatchServer.Tests;

/// <summary>
/// Direct unit coverage for <see cref="ConnectionSeatAuthority"/> — before this, its only coverage
/// was indirect, through <see cref="MatchServerIntegrationTests"/>'s real WebSocket matches. That
/// proves the class works as USED, but not its own stated contract (trust <c>claimedBy</c> between
/// requests; ignore it entirely, resolving only the bound connection, during one) directly and in
/// isolation. See docs/SECURITY_AUDIT_2026-09-05.md's M-T1.
/// </summary>
public sealed class ConnectionSeatAuthorityTests
{
    [Fact]
    public void Between_requests_the_claimed_seat_is_trusted_outright()
    {
        var authority = new ConnectionSeatAuthority();

        // No BeginClientRequest has ever been called — this is the state a bot or a recorded seat
        // always sees, per the class's own remarks.
        Assert.Equal(new PlayerId(3), authority.ResolveSeat(new PlayerId(3)));
    }

    [Fact]
    public void During_a_request_the_bound_connection_wins_regardless_of_the_claimed_seat()
    {
        var authority = new ConnectionSeatAuthority();
        authority.BindConnection(connectionId: 1, seat: new PlayerId(1));

        authority.BeginClientRequest(1);
        var resolved = authority.ResolveSeat(claimedBy: new PlayerId(99));

        Assert.Equal(new PlayerId(1), resolved);
    }

    [Fact]
    public void During_a_request_from_an_unbound_connection_resolution_fails_closed()
    {
        var authority = new ConnectionSeatAuthority();

        // No BindConnection for connection 7 at all — a message dispatched for a connection that
        // was never bound to a seat (see ServerMatch.DispatchAsync's own M2-era guard) must not
        // fall back to trusting the claim, or an unbound connection could act as any seat it liked.
        authority.BeginClientRequest(7);

        Assert.Null(authority.ResolveSeat(claimedBy: new PlayerId(1)));
    }

    [Fact]
    public void EndRequest_returns_to_trusting_the_claimed_seat()
    {
        var authority = new ConnectionSeatAuthority();
        authority.BindConnection(connectionId: 1, seat: new PlayerId(1));

        authority.BeginClientRequest(1);
        authority.EndRequest();

        // Leaving currentConnectionId set past EndRequest would make the NEXT call (a bot's own
        // turn, per the class's own remarks) wrongly resolve against a stale client connection.
        Assert.Equal(new PlayerId(5), authority.ResolveSeat(new PlayerId(5)));
    }

    [Fact]
    public void ForgetConnection_makes_a_later_request_from_it_fail_closed()
    {
        var authority = new ConnectionSeatAuthority();
        authority.BindConnection(connectionId: 1, seat: new PlayerId(1));
        authority.ForgetConnection(1);

        authority.BeginClientRequest(1);

        Assert.Null(authority.ResolveSeat(claimedBy: new PlayerId(1)));
    }
}
