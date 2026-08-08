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
    InvalidTier,

    /// <summary>
    /// A category tier purchase refused because the player's INCOME is below the threshold for
    /// that tier, regardless of how much gold they are holding.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="InsufficientGold"/> on purpose: the two are fixed by opposite
    /// actions. Insufficient gold means wait and bank; insufficient income means send creeps,
    /// which costs the gold you were banking. Collapsing them into one reason would tell a player
    /// to do the very thing that cannot help.
    ///
    /// Appended rather than inserted, for the reason given on InvalidTier above.
    /// </remarks>
    InsufficientIncome,

    /// <summary>
    /// This creep already has the most copies a seat may have waiting in its send queue.
    /// </summary>
    /// <remarks>
    /// Appended, for the reason given on InvalidTier above: this project replays accepted commands
    /// from a seed, so the numeric value of an existing member must not move. Inserting this next
    /// to InvalidTier, where it reads better, would have shifted InsufficientIncome by one and
    /// silently changed what older replays mean.
    /// </remarks>
    SendQueueFull
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
