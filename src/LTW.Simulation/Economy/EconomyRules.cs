namespace LTW.Simulation.Economy;

public sealed class EconomyRules
{
    /// <summary>Income at which sends start granting less than their nominal gain.</summary>
    /// <remarks>
    /// Measured, not guessed. In a shipped eight-lane match the leading bot's income runs 35 at tick
    /// 200, 230 at tick 800 and 1,099 at tick 1,600 — so a knee at 300 leaves the whole opening and
    /// midgame byte-identical to the uncapped curve and only engages once income is already climbing
    /// faster than the match can end.
    /// </remarks>
    public const int DefaultIncomeTaperStart = 300;

    /// <summary>Income ceiling applied when a caller does not specify one.</summary>
    /// <remarks>Twice the taper start, giving a wide band to fall off across rather than a cliff.</remarks>
    public const int DefaultIncomeCeiling = 600;

    public EconomyRules(
        int incomeIntervalTicks,
        int sendCooldownTicks,
        int sellRefundPercent,
        int leakLifeLoss,
        int incomeCeiling = DefaultIncomeCeiling,
        int incomeTaperStart = DefaultIncomeTaperStart)
    {
        if (incomeIntervalTicks <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(incomeIntervalTicks), "Income interval must be positive.");
        }

        if (sendCooldownTicks < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(sendCooldownTicks), "Send cooldown cannot be negative.");
        }

        if (sellRefundPercent < 0 || sellRefundPercent > 100)
        {
            throw new System.ArgumentOutOfRangeException(nameof(sellRefundPercent), "Sell refund percent must be between 0 and 100.");
        }

        if (leakLifeLoss <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(leakLifeLoss), "Leak life loss must be positive.");
        }

        if (incomeCeiling <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(incomeCeiling), "Income ceiling must be positive.");
        }

        if (incomeTaperStart < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(incomeTaperStart), "Income taper start cannot be negative.");
        }

        if (incomeTaperStart >= incomeCeiling)
        {
            throw new System.ArgumentOutOfRangeException(nameof(incomeTaperStart), "Income taper must start below the ceiling.");
        }

        IncomeIntervalTicks = incomeIntervalTicks;
        SendCooldownTicks = sendCooldownTicks;
        SellRefundPercent = sellRefundPercent;
        LeakLifeLoss = leakLifeLoss;
        IncomeCeiling = incomeCeiling;
        IncomeTaperStart = incomeTaperStart;
    }

    public int IncomeIntervalTicks { get; }

    public int SendCooldownTicks { get; }

    public int SellRefundPercent { get; }

    public int LeakLifeLoss { get; }

    /// <summary>The income level at which sending stops raising income any further.</summary>
    /// <remarks>
    /// Income and gold are a coupled loop: a send permanently raises income, income pays gold every
    /// <see cref="IncomeIntervalTicks"/> ticks, and gold buys more sends. That is a positive feedback
    /// loop with no natural brake, so income grows super-linearly for as long as a match runs — a
    /// modelled 8,000-tick match reaches income 36,136 on 1.8M banked gold, and a measured batch run
    /// peaked at 4,111 concurrent creeps, which is a mobile performance problem before it is a balance
    /// one.
    ///
    /// A floor on the per-send gain does NOT fix this. Any strictly positive gain keeps the loop
    /// compounding because the number of sends per interval grows with gold; flooring the gain at 1
    /// only delays the runaway (modelled: income 423 at tick 3,200, but 2,374 by tick 4,800). The gain
    /// has to actually reach zero, which is what a ceiling gives.
    /// </remarks>
    public int IncomeCeiling { get; }

    /// <summary>Income below which sends grant their full nominal gain, untouched.</summary>
    /// <remarks>
    /// The knee exists so the brake is a late-game one rather than a permanent tax. Tapering
    /// proportionally from zero income was tried first and is wrong: it halves a gain-2 creep at
    /// income 100, which is inside the opening, and measurably weakened the bots — their maze in
    /// <c>BotMazingTests</c> fell from 22 cells to 20 because they simply had less gold to build
    /// with. Below this threshold the economy behaves exactly as it did before the ceiling existed.
    /// </remarks>
    public int IncomeTaperStart { get; }
}
