using LTW.Simulation.Bots;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Seats;

/// <summary>
/// One seat's identity and current driver, as of the tick a <see cref="SeatTable"/> was read.
/// </summary>
/// <remarks>
/// <see cref="Role"/> and <see cref="IsConnected"/> are deliberately separate axes rather than a
/// single richer enum (<c>Human</c>, <c>HumanDropped</c>, <c>Bot</c>, <c>Empty</c>...): a dropped
/// human's IDENTITY does not change, only who is currently acting for it, and
/// docs/MULTIPLAYER_ROLLOUT.md's MP-01 deliverable says the reconnect has to be "as data" — a
/// seat that remembers whose it is rather than one that becomes a bot and back. Collapsing the
/// two into one enum would make "reconnect" a role CHANGE instead of a flag flip, which is exactly
/// the modelling this exists to avoid.
/// </remarks>
public sealed class Seat
{
    public Seat(PlayerId playerId, SeatRole role, bool isConnected, BotDecisionProfile? botProfile, string? displayName = null)
    {
        PlayerId = playerId;
        Role = role;
        IsConnected = isConnected;
        BotProfile = botProfile;
        DisplayName = displayName;
    }

    public PlayerId PlayerId { get; }

    /// <summary>Who this seat belongs to. See the class remarks for why this never changes on a drop.</summary>
    public SeatRole Role { get; }

    /// <summary>
    /// True for every <see cref="SeatRole.Bot"/> and <see cref="SeatRole.Empty"/> seat, always —
    /// neither has a connection to lose. Meaningful only for <see cref="SeatRole.Human"/>: false
    /// means a bot is standing in for this seat right now, per <c>LocalVerticalSlice.DropSeat</c>.
    /// </summary>
    public bool IsConnected { get; }

    /// <summary>
    /// The profile actually acting for this seat right now: the seat's own, for a
    /// <see cref="SeatRole.Bot"/> seat, or the stand-in's, for a disconnected
    /// <see cref="SeatRole.Human"/> one. Null for a connected human or an empty seat, since
    /// neither has a bot decision loop running.
    /// </summary>
    public BotDecisionProfile? BotProfile { get; }

    /// <summary>
    /// Set only for <see cref="SeatRole.Recorded"/> — the name a client shows for the ghost, so a
    /// recorded opponent reads as a person, not a slot. Null for every other role.
    /// </summary>
    public string? DisplayName { get; }
}
