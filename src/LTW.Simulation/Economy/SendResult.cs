using LTW.Simulation.Commands;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Economy;

public sealed class SendResult
{
    private SendResult(
        bool accepted,
        EconomyPlayerSet players,
        CommandRejectionReason rejectionReason,
        PlayerId? targetPlayerId)
    {
        Accepted = accepted;
        Players = players;
        RejectionReason = rejectionReason;
        TargetPlayerId = targetPlayerId;
    }

    public bool Accepted { get; }

    public EconomyPlayerSet Players { get; }

    public CommandRejectionReason RejectionReason { get; }

    public PlayerId? TargetPlayerId { get; }

    public static SendResult Accept(EconomyPlayerSet players, PlayerId targetPlayerId) =>
        new SendResult(true, players, CommandRejectionReason.None, targetPlayerId);

    public static SendResult Reject(EconomyPlayerSet players, CommandRejectionReason rejectionReason) =>
        new SendResult(false, players, rejectionReason, null);
}
