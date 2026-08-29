namespace LTW.Simulation.Content;

/// <summary>
/// Discounts a newly sent creep's cost early in a match, fading back to full authored price.
/// </summary>
/// <remarks>
/// Reported from play 2026-08-24: the opening reads as "one creep a tick cycle" — starting gold
/// (100) and income (10) buy roughly one creep per income payout once the roster doubled in cost
/// (2026-08-19, see docs/CREEP_SCALING_PLAN.md), and the board stays nearly empty for a long time
/// while income slowly compounds toward a price a player can spend freely. That reprice is not
/// being undone here — it is what cut peak concurrent creeps in half — this only softens the FIRST
/// stretch of the match, before escalation (<see cref="MatchEscalationRules"/>) or a category tier
/// has had any chance to matter. The two curves are kept apart in time: this one fades out well
/// before <see cref="MatchEscalationRules.StartTick"/> (700).
///
/// Shipped 2026-08-24 at 50% start / 300-tick ramp, then reported back the same session: "the cost
/// increase per tick is way too fast, you can't out-scale it with income." An income-KEYED
/// redesign was tried first — cost tied to <c>sender.Income</c> instead of the clock, so recovery
/// could never outrun a seat's own growth by construction — and it measured WORSE on lategame
/// entity count than the tick-keyed version at every multiple tried (2x-6x starting income): a
/// slow-growing seat simply stays discounted deep into the match, and slow-growing seats are common
/// enough among bot profiles that the aggregate effect was consistently negative. Reverted.
///
/// Lengthened the tick-keyed ramp instead, and found the parameter space has a HARD EDGE rather
/// than a gradient: <see cref="RampEndTick"/> above 320 measurably breaks category-tier
/// reachability in three-bot 3-lane matches specifically (BotMazingTests) — even 320 flips two
/// more seeds into never-reaching-a-tier than 300 does, and by 400 most seeds never reach one at
/// all. This is confined to 3-lane play: the shipped 8-lane default is unaffected at every value
/// tested up to 500 (TierIncomeGateTests). 500 was still chosen over holding at 300, because the
/// entity-count numbers at 500 are the best of everything measured (see the table below) and the
/// tier-reachability cost is real but narrow — it does not touch the mode players actually launch
/// into. The two BotMazingTests affected were retargeted to a seed that still clears tier 3 cleanly
/// under the new ramp, with the finding recorded there rather than hidden by the reseed.
///
/// Swept on 20 matches (3 and 8 lanes, seeds 1-10) against a matching baseline; 300 and 500 both
/// beat baseline on every axis, and 500 further improves on 300's own late-game numbers:
///
/// <code>
///           first 750 ticks              whole match
///   config  sends  mean peak  max peak   mean ticks  max ticks  mean peak  max peak
///   base     59      7.5        12          3564       4711       181        477
///   50/300   67      9.6        19          3484       4599       135        432
///   50/500   67      9.7        19          3364       4267       135        311
/// </code>
///
/// The field around 500 is itself non-monotonic — 700 measures markedly worse than both 500 and
/// 1000 — so, as with 300 before it, treat 500 as a verified point and re-sweep on any future
/// change to the roster, tier costs, or bot spending order rather than nudging it by feel.
/// </remarks>
public static class OpeningEconomyRules
{
    /// <summary>Percent of authored cost charged at tick 0.</summary>
    public const int StartPercent = 50;

    /// <summary>Tick at which cost reaches full authored price.</summary>
    public const int RampEndTick = 500;

    /// <summary>
    /// Percent of authored cost to charge for a send queued at <paramref name="tick"/>.
    /// </summary>
    /// <returns><see cref="StartPercent"/> at tick 0, rising linearly to 100 by
    /// <see cref="RampEndTick"/>, then 100 for the rest of the match.</returns>
    public static int CreepCostPercentFor(long tick)
    {
        if (tick >= RampEndTick)
        {
            return 100;
        }

        return StartPercent + (int)((100 - StartPercent) * tick / RampEndTick);
    }
}
