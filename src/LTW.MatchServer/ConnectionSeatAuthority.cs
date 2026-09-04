using LTW.Simulation.Authority;
using LTW.Simulation.Primitives;

namespace LTW.MatchServer;

/// <summary>
/// The server-side <see cref="ISeatAuthority"/> MULTIPLAYER_SEATS_AND_AUTHORITY.md's own remarks
/// describe: "answers is this the seat on THIS CONNECTION", not "is this seat in the match" (that
/// question is <c>LocalSeatAuthority</c>'s, and it is the wrong one on a server).
/// </summary>
/// <remarks>
/// <see cref="ISeatAuthority.ResolveSeat"/> takes only the CLAIMED <see cref="PlayerId"/> — it has
/// no connection parameter, because the interface was written for a <c>LocalVerticalSlice</c> that
/// is one shared instance per match, called from whatever the current call stack is, not once per
/// connection. This class carries the connection identity as ambient state instead:
/// <see cref="ServerMatch.DispatchAsync"/> calls <see cref="BeginClientRequest"/> with the
/// connection about to be served, immediately before the one <c>LocalVerticalSlice</c> method that
/// connection's message maps to, and calls <see cref="EndRequest"/> immediately after — every
/// <see cref="ResolveSeat"/> in between resolves against THAT connection, ignoring
/// <paramref name="claimedBy"/> entirely, which is the whole property this class exists for.
///
/// Between requests (the default state, and the state a bot or <c>RecordedSeatDriver</c> call
/// always sees, since neither goes through <see cref="BeginClientRequest"/> at all) this trusts
/// <paramref name="claimedBy"/> outright — the same trust <c>LocalSeatAuthority</c> gives every
/// caller locally. That is deliberate, not a gap: a bot deciding its own seat's move, or a
/// recorded seat replaying one, never arrived over a wire, so there is no message to distrust.
/// Getting this wrong the first time (rejecting every bot call because it had no connection to
/// resolve against) is exactly why <see cref="ServerMatch"/> now serializes every call through
/// this authority with one lock — see its own remarks.
/// </remarks>
public sealed class ConnectionSeatAuthority : ISeatAuthority
{
    private readonly Dictionary<int, PlayerId> seatByConnection = new();
    private int? currentConnectionId;

    /// <summary>Binds a connection to the seat it authenticated as when it joined. Once only.</summary>
    public void BindConnection(int connectionId, PlayerId seat) => seatByConnection[connectionId] = seat;

    public void ForgetConnection(int connectionId) => seatByConnection.Remove(connectionId);

    /// <summary>Call immediately before dispatching one message from <paramref name="connectionId"/> into the match.</summary>
    public void BeginClientRequest(int connectionId) => currentConnectionId = connectionId;

    /// <summary>
    /// Call immediately after a dispatched command returns, whether accepted or rejected — leaving
    /// <see cref="currentConnectionId"/> set would make the NEXT call (a bot's own turn, on the
    /// same serialized sequence) wrongly resolve against a client connection it has nothing to do
    /// with.
    /// </summary>
    public void EndRequest() => currentConnectionId = null;

    public PlayerId? ResolveSeat(PlayerId claimedBy)
    {
        if (currentConnectionId is not int id)
        {
            // No client request is in flight — a bot or a recorded seat deciding its own move,
            // trusted the same way LocalSeatAuthority trusts everything in a local match.
            return claimedBy;
        }

        return seatByConnection.TryGetValue(id, out var seat) ? seat : null;
    }
}
