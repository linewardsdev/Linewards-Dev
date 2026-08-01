using System;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Economy;

public sealed class PlayerEconomyState
{
    /// <summary>
    /// Upgradeable categories per side: three tower lines, three send categories.
    /// </summary>
    public const int CategoryCount = 3;

    /// <summary>
    /// The tier every category starts at. Tier 1 is free and default; 2 and 3 are purchased.
    /// </summary>
    public const int BaseTier = 1;

    private readonly int[] towerLineTiers;
    private readonly int[] sendCategoryTiers;

    public PlayerEconomyState(PlayerId playerId, Gold gold, Income income, Lives lives)
        : this(playerId, gold, income, lives, new SimulationTick(0), isEliminated: false, towerLineTiers: null, sendCategoryTiers: null)
    {
    }

    private PlayerEconomyState(
        PlayerId playerId,
        Gold gold,
        Income income,
        Lives lives,
        SimulationTick nextSendAvailableTick,
        bool isEliminated,
        int[]? towerLineTiers,
        int[]? sendCategoryTiers)
    {
        PlayerId = playerId;
        Gold = gold;
        Income = income;
        Lives = lives;
        NextSendAvailableTick = nextSendAvailableTick;
        IsEliminated = isEliminated;
        this.towerLineTiers = towerLineTiers ?? DefaultTiers();
        this.sendCategoryTiers = sendCategoryTiers ?? DefaultTiers();
    }

    public PlayerId PlayerId { get; }

    public Gold Gold { get; }

    public Income Income { get; }

    public Lives Lives { get; }

    public SimulationTick NextSendAvailableTick { get; }

    public bool IsEliminated { get; }

    /// <summary>
    /// The gold this seat will actually be paid at the next income tick — zero once eliminated.
    /// </summary>
    /// <remarks>
    /// <see cref="Income"/> is the economy the player built and keeps: it is what the match summary
    /// reports and what an elimination does NOT undo. This is the different question a presentation
    /// layer is really asking, and until now it could not be asked at all.
    ///
    /// Elimination is enforced as a filter inside <c>EconomyService.ApplyIncomeTick</c>, which is
    /// correct and is not changing. But it meant "does this seat earn anything" lived only in that
    /// loop, so a HUD holding a snapshot had no way to derive it — the Unity client read
    /// <see cref="Income"/>, faithfully, and went on showing a defeated seat the +10 it started the
    /// match with for the rest of the match (OPEN_ITEMS.md item 31). The value was live; there was
    /// simply no live value that meant what the HUD was trying to say.
    ///
    /// <c>EliminatedSeatEarnsNothingAndReportsThat</c> in PlayerEliminationTests pins this to what
    /// the income tick actually pays, so the two cannot drift apart.
    /// </remarks>
    public Income EffectiveIncome => IsEliminated ? new Income(0) : Income;

    /// <summary>
    /// This player's tier for one tower line (0 ARCANE, 1 FOUNDRY, 2 GROVE), scaling its damage.
    /// </summary>
    public int TowerLineTier(int lineIndex) => TierAt(towerLineTiers, lineIndex);

    /// <summary>
    /// This player's tier for one send category (0 CORE, 1 RAPID, 2 ELITE), scaling spawn health.
    /// </summary>
    public int SendCategoryTier(int categoryIndex) => TierAt(sendCategoryTiers, categoryIndex);

    // Every wither below carries BOTH tier arrays through. Dropping one would compile and would
    // silently reset that side to tier 1 the next time a player earned income or took a leak —
    // the same failure shape TowerCombatState.WithNextAttackTick's remark warns about for the
    // mortar's shell fields.
    public PlayerEconomyState WithGold(Gold gold) =>
        new PlayerEconomyState(PlayerId, gold, Income, Lives, NextSendAvailableTick, IsEliminated, towerLineTiers, sendCategoryTiers);

    public PlayerEconomyState WithIncome(Income income) =>
        new PlayerEconomyState(PlayerId, Gold, income, Lives, NextSendAvailableTick, IsEliminated, towerLineTiers, sendCategoryTiers);

    public PlayerEconomyState WithLives(Lives lives) =>
        new PlayerEconomyState(PlayerId, Gold, Income, lives, NextSendAvailableTick, lives.Amount == 0 || IsEliminated, towerLineTiers, sendCategoryTiers);

    public PlayerEconomyState WithNextSendAvailableTick(SimulationTick nextSendAvailableTick) =>
        new PlayerEconomyState(PlayerId, Gold, Income, Lives, nextSendAvailableTick, IsEliminated, towerLineTiers, sendCategoryTiers);

    public PlayerEconomyState WithTowerLineTier(int lineIndex, int tier) =>
        new PlayerEconomyState(PlayerId, Gold, Income, Lives, NextSendAvailableTick, IsEliminated, Replaced(towerLineTiers, lineIndex, tier), sendCategoryTiers);

    public PlayerEconomyState WithSendCategoryTier(int categoryIndex, int tier) =>
        new PlayerEconomyState(PlayerId, Gold, Income, Lives, NextSendAvailableTick, IsEliminated, towerLineTiers, Replaced(sendCategoryTiers, categoryIndex, tier));

    /// <summary>
    /// Copies this player's tiers out for combat to read. A copy rather than the array itself, so
    /// nothing downstream can write back into a state that is supposed to be immutable.
    /// </summary>
    public int[] CopyTowerLineTiers() => (int[])towerLineTiers.Clone();

    public int[] CopySendCategoryTiers() => (int[])sendCategoryTiers.Clone();

    private static int[] DefaultTiers()
    {
        var tiers = new int[CategoryCount];
        for (var index = 0; index < tiers.Length; index++)
        {
            tiers[index] = BaseTier;
        }

        return tiers;
    }

    /// <summary>
    /// An out-of-range read answers <see cref="BaseTier"/> rather than throwing: an index that has
    /// drifted should scale nothing, not end the match with an exception from inside combat.
    /// </summary>
    private static int TierAt(int[] tiers, int index) =>
        index >= 0 && index < tiers.Length ? tiers[index] : BaseTier;

    /// <summary>
    /// Copy-on-write, so a snapshot taken earlier in the tick keeps the tiers it was taken with.
    /// </summary>
    /// <remarks>
    /// This one DOES throw on a bad index, unlike <see cref="TierAt"/>. Reading a nonexistent
    /// category is survivable; being asked to buy one means a command was built wrong, and
    /// silently writing nothing would bill the player for an upgrade they never received.
    /// </remarks>
    private static int[] Replaced(int[] tiers, int index, int tier)
    {
        if (index < 0 || index >= tiers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Category index must be 0..{tiers.Length - 1}.");
        }

        var next = (int[])tiers.Clone();
        next[index] = tier;
        return next;
    }
}
