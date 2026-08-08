using LTW.Simulation.Primitives;

namespace LTW.Simulation.Authority;

/// <summary>
/// Decides which seat a command is actually allowed to act as.
/// </summary>
/// <remarks>
/// Every command in this project carries a <c>PlayerId</c>. In a local match that field is only
/// ever written by the local adapter and is therefore trustworthy. Over a wire it is the opposite:
/// the seat is the one field an attacker most wants to change, and believing it lets a client queue
/// sends into somebody else's queue, build in their lane, or spend their gold.
///
/// So the rule this interface exists to enforce is: <b>the seat comes from the connection, never
/// from the message.</b> A server implementation resolves the authenticated session and returns
/// that seat, ignoring whatever the command claimed. <see cref="LocalSeatAuthority"/> is the
/// single-process case, where there is exactly one player and the question is trivial.
///
/// Placed before the server exists on purpose. Retrofitting an authority boundary after commands
/// are already flowing means auditing every call site under time pressure; having the boundary
/// here means the server implementation is a class, not a refactor.
/// </remarks>
public interface ISeatAuthority
{
    /// <summary>
    /// The seat <paramref name="claimedBy"/> is permitted to act as, or null if it may not act.
    /// </summary>
    /// <remarks>
    /// Returning null is a refusal, not an error: a spectator, a defeated seat's stale client, or a
    /// forged id all land here and all mean "do nothing", not "throw".
    /// </remarks>
    PlayerId? ResolveSeat(PlayerId claimedBy);
}

/// <summary>
/// The single-process case: one player, and the claim is believed because nothing else can write it.
/// </summary>
/// <remarks>
/// Deliberately still checks rather than waving everything through. A local match has bots acting
/// as their own seats, so "is this seat real" is a question worth answering even here, and a local
/// implementation that accepted anything would let a bug in the client act as a bot and stay
/// invisible until the server arrived.
/// </remarks>
public sealed class LocalSeatAuthority : ISeatAuthority
{
    private readonly System.Collections.Generic.HashSet<int> seats;

    public LocalSeatAuthority(System.Collections.Generic.IEnumerable<PlayerId> seats)
    {
        this.seats = new System.Collections.Generic.HashSet<int>();
        foreach (var seat in seats)
        {
            this.seats.Add(seat.Value);
        }
    }

    public PlayerId? ResolveSeat(PlayerId claimedBy) =>
        seats.Contains(claimedBy.Value) ? claimedBy : null;
}
