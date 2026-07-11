using LTW.Simulation.Primitives;

namespace LTW.Simulation.Economy;

public sealed class PlayerEconomyState
{
    public PlayerEconomyState(PlayerId playerId, Gold gold, Income income, Lives lives)
        : this(playerId, gold, income, lives, new SimulationTick(0), isEliminated: false)
    {
    }

    private PlayerEconomyState(
        PlayerId playerId,
        Gold gold,
        Income income,
        Lives lives,
        SimulationTick nextSendAvailableTick,
        bool isEliminated)
    {
        PlayerId = playerId;
        Gold = gold;
        Income = income;
        Lives = lives;
        NextSendAvailableTick = nextSendAvailableTick;
        IsEliminated = isEliminated;
    }

    public PlayerId PlayerId { get; }

    public Gold Gold { get; }

    public Income Income { get; }

    public Lives Lives { get; }

    public SimulationTick NextSendAvailableTick { get; }

    public bool IsEliminated { get; }

    public PlayerEconomyState WithGold(Gold gold) =>
        new PlayerEconomyState(PlayerId, gold, Income, Lives, NextSendAvailableTick, IsEliminated);

    public PlayerEconomyState WithIncome(Income income) =>
        new PlayerEconomyState(PlayerId, Gold, income, Lives, NextSendAvailableTick, IsEliminated);

    public PlayerEconomyState WithLives(Lives lives) =>
        new PlayerEconomyState(PlayerId, Gold, Income, lives, NextSendAvailableTick, lives.Amount == 0 || IsEliminated);

    public PlayerEconomyState WithNextSendAvailableTick(SimulationTick nextSendAvailableTick) =>
        new PlayerEconomyState(PlayerId, Gold, Income, Lives, nextSendAvailableTick, IsEliminated);
}
