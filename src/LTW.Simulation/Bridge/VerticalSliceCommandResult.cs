using LTW.Simulation.Commands;

namespace LTW.Simulation.Bridge;

public sealed class VerticalSliceCommandResult
{
    private VerticalSliceCommandResult(bool accepted, CommandRejectionReason rejectionReason)
    {
        Accepted = accepted;
        RejectionReason = rejectionReason;
    }

    public bool Accepted { get; }

    public CommandRejectionReason RejectionReason { get; }

    public static VerticalSliceCommandResult Accept() => new VerticalSliceCommandResult(true, CommandRejectionReason.None);

    public static VerticalSliceCommandResult Reject(CommandRejectionReason rejectionReason) =>
        new VerticalSliceCommandResult(false, rejectionReason);
}
