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
        int[]? sendCategoryTiers,
        int chosenTowerLine = UnchosenTowerLine)
    {
        ChosenTowerLine = chosenTowerLine;
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

    /// <summary>No line committed to yet.</summary>
    public const int UnchosenTowerLine = -1;

    /// <summary>
    /// The one tower line this seat has committed to, or <see cref="UnchosenTowerLine"/>.
    /// </summary>
    /// <remarks>
    /// Reported from play 2026-08-07: with every line buildable, a scattered set of wards blends
    /// DPS, AOE and slow into something that cannot lose, so there is no decision to make. The
    /// original Line Tower Wars answered this by making you pick a race and live with it, and this
    /// is that.
    ///
    /// Committing is one-way. A seat that could re-pick mid-match would just be the old
    /// build-everything with extra steps, and the whole value of the choice is that it closes doors.
    ///
    /// This only became shippable once every line had its own brake — Bramble Hold was the roster's
    /// only slow and it is Grove's, so locking before that would have made Grove mandatory rather
    /// than making the choice interesting. See <c>TowerDefinition.SlowsCreeps</c>.
    /// </remarks>
    public int ChosenTowerLine { get; }

    /// <summary>Whether this seat may build from <paramref name="lineIndex"/>.</summary>
    /// <remarks>
    /// An uncommitted seat may build anything: the commitment happens on the first tower placed,
    /// so the choice is made by playing rather than by a modal before the match starts.
    /// </remarks>
    public bool CanBuildFromLine(int lineIndex) =>
        ChosenTowerLine == UnchosenTowerLine || ChosenTowerLine == lineIndex;

    /// <summary>Commits this seat to a line. Ignored once committed — the choice is one-way.</summary>
    public PlayerEconomyState WithChosenTowerLine(int lineIndex) =>
        ChosenTowerLine != UnchosenTowerLine
            ? this
            : new PlayerEconomyState(PlayerId, Gold, Income, Lives, NextSendAvailableTick, IsEliminated, towerLineTiers, sendCategoryTiers, lineIndex);

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
        new PlayerEconomyState(PlayerId, gold, Income, Lives, NextSendAvailableTick, IsEliminated, towerLineTiers, sendCategoryTiers, ChosenTowerLine);

    public PlayerEconomyState WithIncome(Income income) =>
        new PlayerEconomyState(PlayerId, Gold, income, Lives, NextSendAvailableTick, IsEliminated, towerLineTiers, sendCategoryTiers, ChosenTowerLine);

    public PlayerEconomyState WithLives(Lives lives) =>
        new PlayerEconomyState(PlayerId, Gold, Income, lives, NextSendAvailableTick, lives.Amount == 0 || IsEliminated, towerLineTiers, sendCategoryTiers, ChosenTowerLine);

    public PlayerEconomyState WithNextSendAvailableTick(SimulationTick nextSendAvailableTick) =>
        new PlayerEconomyState(PlayerId, Gold, Income, Lives, nextSendAvailableTick, IsEliminated, towerLineTiers, sendCategoryTiers, ChosenTowerLine);

    public PlayerEconomyState WithTowerLineTier(int lineIndex, int tier) =>
        new PlayerEconomyState(PlayerId, Gold, Income, Lives, NextSendAvailableTick, IsEliminated, Replaced(towerLineTiers, lineIndex, tier), sendCategoryTiers, ChosenTowerLine);

    public PlayerEconomyState WithSendCategoryTier(int categoryIndex, int tier) =>
        new PlayerEconomyState(PlayerId, Gold, Income, Lives, NextSendAvailableTick, IsEliminated, towerLineTiers, Replaced(sendCategoryTiers, categoryIndex, tier), ChosenTowerLine);

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
