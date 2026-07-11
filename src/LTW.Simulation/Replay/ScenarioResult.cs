using LTW.Simulation.Economy;

namespace LTW.Simulation.Replay;

public sealed class ScenarioResult
{
    public ScenarioResult(ReplayRecord replay, EconomyPlayerSet players, string finalStateHash)
    {
        Replay = replay;
        Players = players;
        FinalStateHash = finalStateHash;
    }

    public ReplayRecord Replay { get; }

    public EconomyPlayerSet Players { get; }

    public string FinalStateHash { get; }
}
