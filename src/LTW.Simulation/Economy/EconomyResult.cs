using LTW.Simulation.Commands;

namespace LTW.Simulation.Economy;

public sealed class EconomyResult
{
    private EconomyResult(bool accepted, EconomyPlayerSet players, CommandRejectionReason rejectionReason)
    {
        Accepted = accepted;
        Players = players;
        RejectionReason = rejectionReason;
    }

    public bool Accepted { get; }

    public EconomyPlayerSet Players { get; }

    public CommandRejectionReason RejectionReason { get; }

    public static EconomyResult Accept(EconomyPlayerSet players) =>
        new EconomyResult(true, players, CommandRejectionReason.None);

    public static EconomyResult Reject(EconomyPlayerSet players, CommandRejectionReason rejectionReason) =>
        new EconomyResult(false, players, rejectionReason);
}
