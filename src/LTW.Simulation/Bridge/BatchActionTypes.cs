using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

/// <summary>
/// One tower that could be raised as part of a batch, whether that batch is a whole line
/// or a hand-picked selection.
/// </summary>
internal readonly struct BatchUpgradeCandidate
{
    public BatchUpgradeCandidate(GridPosition position, int tier, int lineIndex, int cost)
    {
        Position = position;
        Tier = tier;
        LineIndex = lineIndex;
        Cost = cost;
    }

    public GridPosition Position { get; }

    public int Tier { get; }

    public int LineIndex { get; }

    public int Cost { get; }
}

/// <summary>
/// What a whole-line upgrade would cost, asked before any gold moves.
/// </summary>
/// <remarks>
/// Carries both the full price and the affordable slice because the card has to say two different
/// things: what raising the line actually costs, and what this tap is going to do right now. A
/// button offering "UPGRADE 5" that silently raises 3 is the kind of partial result that reads as a
/// bug rather than as a budget.
/// </remarks>
public readonly struct BatchUpgradeQuote
{
    public BatchUpgradeQuote(int eligible, int totalCost, int affordable, int affordableCost)
    {
        Eligible = eligible;
        TotalCost = totalCost;
        Affordable = affordable;
        AffordableCost = affordableCost;
    }

    /// <summary>Towers below the line's tier ceiling, and so raisable at all.</summary>
    public int Eligible { get; }

    /// <summary>Gold to raise every one of them.</summary>
    public int TotalCost { get; }

    /// <summary>How many of them the player's current gold actually reaches.</summary>
    public int Affordable { get; }

    /// <summary>Gold that raising exactly <see cref="Affordable"/> of them would spend.</summary>
    public int AffordableCost { get; }

    public bool HasWork => Eligible > 0;

    /// <summary>True when gold is the binding constraint rather than the tier ceiling.</summary>
    public bool IsGoldLimited => Affordable < Eligible;
}

/// <summary>
/// What a whole-line upgrade actually did.
/// </summary>
public readonly struct BatchUpgradeOutcome
{
    public BatchUpgradeOutcome(int upgraded, int eligible, int goldSpent)
    {
        Upgraded = upgraded;
        Eligible = eligible;
        GoldSpent = goldSpent;
    }

    public int Upgraded { get; }

    public int Eligible { get; }

    public int GoldSpent { get; }

    public bool IsPartial => Upgraded > 0 && Upgraded < Eligible;
}

/// <summary>
/// What selling a set of towers would return, asked before anything is removed.
/// </summary>
/// <remarks>
/// Selling has no affordability limit — everything you own in the selection goes — so unlike
/// <see cref="BatchUpgradeQuote"/> there is no affordable slice to distinguish. What it does need
/// is to be shown BEFORE the tap: a batch sell is irreversible and destroys the maze the player
/// spent the match building.
/// </remarks>
public readonly struct BatchSellQuote
{
    public BatchSellQuote(int towers, int refund)
    {
        Towers = towers;
        Refund = refund;
    }

    public int Towers { get; }

    public int Refund { get; }

    public bool HasWork => Towers > 0;
}

/// <summary>What a batch sell actually did.</summary>
public readonly struct BatchSellOutcome
{
    public BatchSellOutcome(int sold, int refund)
    {
        Sold = sold;
        Refund = refund;
    }

    public int Sold { get; }

    public int Refund { get; }
}
