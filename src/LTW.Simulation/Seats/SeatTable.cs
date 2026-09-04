using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Seats;

/// <summary>
/// Every seat in a match, as of the tick it was read from <c>LocalVerticalSlice.GetSeatTable</c>.
/// </summary>
/// <remarks>
/// A read model, not a source of truth: <c>LocalVerticalSlice</c> still owns which seats are
/// human, bot or empty (via <c>LocalMatchOptions</c>) and which are currently dropped (via its own
/// drop/reconnect state) — this is built fresh from both every time it is asked for, the same way
/// <c>GetSnapshot()</c> is. See docs/MULTIPLAYER_ROLLOUT.md's MP-01 for why a table exists at all:
/// a match description that is a list of seats, not an assumption that seat 1 is a person.
/// </remarks>
public sealed class SeatTable
{
    public SeatTable(IReadOnlyList<Seat> seats) => Seats = seats.ToArray();

    public IReadOnlyList<Seat> Seats { get; }

    public Seat For(PlayerId playerId) => Seats.First(seat => seat.PlayerId.Equals(playerId));
}
