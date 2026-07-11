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
    PathBlocked
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
