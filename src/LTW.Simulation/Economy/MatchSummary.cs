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
    {
        PlayerId = playerId;
        Gold = gold;
        Income = income;
        Lives = lives;
        IsEliminated = isEliminated;
    }

    public PlayerId PlayerId { get; }

    public Gold Gold { get; }

    public Income Income { get; }

    public Lives Lives { get; }

    public bool IsEliminated { get; }
}
