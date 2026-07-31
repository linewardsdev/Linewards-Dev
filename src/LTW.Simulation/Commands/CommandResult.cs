namespace LTW.Simulation.Commands;

public enum CommandRejectionReason
{
    None = 0,
    UnknownTower,
    UnknownCreep,
    UnknownTech,
    InvalidQuantity,
    InvalidContentId,
    InvalidLane,
    InvalidPlayer,
    InvalidEntity,
    NotOwner,
    PlayerEliminated,
    MatchPaused,
    InsufficientGold,
    CooldownActive,
    CellOccupied,
    PathBlocked,

    /// <summary>
    /// A category tier purchase that does not follow tier 1 -> 2 -> 3 in order: already at the
    /// top, or skipping a tier to reach a higher one without paying for the step between.
    /// </summary>
    /// <remarks>
    /// Appended rather than inserted. Accepted commands are recorded and replayed from a seed, so
    /// renumbering an existing member would change the meaning of an already-recorded replay.
    /// </remarks>
    InvalidTier
}

public sealed class CommandResult
{
    private CommandResult(bool accepted, CommandRejectionReason rejectionReason)
    {
        Accepted = accepted;
        RejectionReason = rejectionReason;
    }

    public bool Accepted { get; }

    public CommandRejectionReason RejectionReason { get; }

    public static CommandResult Accept() => new CommandResult(true, CommandRejectionReason.None);

    public static CommandResult Reject(CommandRejectionReason rejectionReason) => new CommandResult(false, rejectionReason);
}
