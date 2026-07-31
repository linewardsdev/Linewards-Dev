using LTW.Simulation.Primitives;

namespace LTW.Simulation.Economy;

public sealed class LeakResult
{
    public LeakResult(EconomyPlayerSet players, Lives livesLost, Gold bountyAwarded, Lives livesStolen)
    {
        Players = players;
        LivesLost = livesLost;
        BountyAwarded = bountyAwarded;
        LivesStolen = livesStolen;
    }

    public EconomyPlayerSet Players { get; }

    public Lives LivesLost { get; }

    public Gold BountyAwarded { get; }

    /// <summary>
    /// Lives the sender took from the defender, which is normally <see cref="LivesLost"/>.
    /// </summary>
    /// <remarks>
    /// Reported separately rather than inferred from <see cref="LivesLost"/> because the two come
    /// apart in the cases that matter: an eliminated sender steals nothing while the defender still
    /// loses the life, and a leak into the sender's own lane steals nothing at all. Presentation
    /// needs the number actually credited, not the number deducted.
    /// </remarks>
    public Lives LivesStolen { get; }
}
