using LTW.Simulation.Primitives;

namespace LTW.Simulation.Economy;

public sealed class LeakResult
{
    public LeakResult(EconomyPlayerSet players, Lives livesLost, Gold bountyAwarded)
    {
        Players = players;
        LivesLost = livesLost;
        BountyAwarded = bountyAwarded;
    }

    public EconomyPlayerSet Players { get; }

    public Lives LivesLost { get; }

    public Gold BountyAwarded { get; }
}
