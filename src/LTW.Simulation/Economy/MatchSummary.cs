using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Economy;

public sealed class MatchSummary
{
    public MatchSummary(PlayerId winnerId, SimulationTick completedAtTick, IReadOnlyList<PlayerEconomySummary> players)
    {
        WinnerId = winnerId;
        CompletedAtTick = completedAtTick;
        Players = players.ToArray();
    }

    public PlayerId WinnerId { get; }

    public SimulationTick CompletedAtTick { get; }

    public IReadOnlyList<PlayerEconomySummary> Players { get; }
}

public sealed class PlayerEconomySummary
{
    public PlayerEconomySummary(PlayerId playerId, Gold gold, Income income, Lives lives, bool isEliminated)
        : this(playerId, gold, income, lives, isEliminated, placement: 0)
    {
    }

    public PlayerEconomySummary(PlayerId playerId, Gold gold, Income income, Lives lives, bool isEliminated, int placement)
    {
        PlayerId = playerId;
        Gold = gold;
        Income = income;
        Lives = lives;
        IsEliminated = isEliminated;
        Placement = placement;
    }

    /// <summary>
    /// Where this seat finished: 1 for the winner, then 2 upward in reverse elimination order.
    /// </summary>
    /// <remarks>
    /// 0 when unknown, which is what the older five-argument constructor produces — a caller that
    /// does not track elimination order gets an honest "no placement" rather than a fabricated one.
    ///
    /// Carried on the summary rather than derived by each consumer. The results screen rendered
    /// every defeated seat as `OUT` with no ordering, so a seven-way loss read as a seven-way tie,
    /// and the information to rank them was known but thrown away at the end of the match. The
    /// replay report and any future post-match screen want the same number.
    ///
    /// Reverse elimination order is the ranking that matches how the mode is played: surviving
    /// longer is the whole objective, so the last seat to fall placed second.
    /// </remarks>
    public int Placement { get; }

    public PlayerId PlayerId { get; }

    public Gold Gold { get; }

    public Income Income { get; }

    public Lives Lives { get; }

    public bool IsEliminated { get; }
}
