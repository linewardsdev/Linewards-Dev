namespace LTW.Simulation.Seats;

/// <summary>
/// What a seat's IDENTITY is, not what is currently driving it. A dropped human seat is still
/// <see cref="Human"/> — see <see cref="Seat.IsConnected"/> for whether a bot is standing in.
/// </summary>
public enum SeatRole
{
    /// <summary>Driven by a person. May or may not be connected right now — see <see cref="Seat.IsConnected"/>.</summary>
    Human,

    /// <summary>Driven by <c>BotController</c> for the whole match. Never a stand-in for a human.</summary>
    Bot,

    /// <summary>
    /// Driven by another player's recorded command stream — MULTIPLAYER_SEATS_AND_AUTHORITY.md's
    /// MP-02. A client should show this as a named ghost, not as <see cref="Bot"/>: it is a real
    /// person's play, just not live.
    /// </summary>
    Recorded,

    /// <summary>Nobody and nothing drives it. It still holds an economy record and a lane; it simply never acts.</summary>
    Empty,
}
