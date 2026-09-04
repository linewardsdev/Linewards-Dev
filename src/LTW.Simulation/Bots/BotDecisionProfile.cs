namespace LTW.Simulation.Bots;

public enum BotDecisionProfile
{
    Greedy = 0,
    Balanced = 1,
    Defensive = 2,

    /// <summary>
    /// Builds and defends, never attacks. For the guided practice match: the lanes around the
    /// player look alive — a maze goes up at the normal opening cadence and line tiers are bought
    /// at the defensive cadence — but nothing is ever sent and no send tier is ever bought, so the
    /// only creeps the player meets are their own.
    /// </summary>
    Passive = 3
}
