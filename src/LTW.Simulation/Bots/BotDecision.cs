using LTW.Simulation.Commands;

namespace LTW.Simulation.Bots;

public sealed class BotDecision
{
    public BotDecision(ISimulationCommand? command)
    {
        Command = command;
    }

    public ISimulationCommand? Command { get; }

    public static BotDecision None { get; } = new BotDecision(null);
}
