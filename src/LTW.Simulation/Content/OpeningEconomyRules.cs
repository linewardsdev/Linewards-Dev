namespace LTW.Simulation.Content;

/// <summary>
/// Discounts a newly sent creep's cost early in a match, fading back to full authored price.
/// </summary>
/// <remarks>
/// Reported from play 2026-08-24: the opening reads as "one creep a tick cycle" — starting gold
/// (100) and income (10) buy roughly one creep per income payout once the roster doubled in cost
/// (2026-08-19, see docs/CREEP_SCALING_PLAN.md), and the board stays nearly empty for a long time
/// while income slowly compounds toward a price a player can spend freely. That reprice was
/// deliberate and is not being undone here — it is what cut peak concurrent creeps in half — this
/// only softens the FIRST stretch of the match, before escalation (<see cref="MatchEscalationRules"/>)
/// or a category tier has had any chance to matter.
///
/// The two curves are deliberately kept apart in time. This one is fully faded out well before
/// <see cref="MatchEscalationRules.StartTick"/>, so a discount that makes the opening feel normal
/// again never overlaps the escalation that makes the LATE game close — the entity-count work stays
/// exactly as effective as it was measured to be.
///
/// 50/300 was swept against a wide field, not picked by feel — and the field turned out NOT to be
/// smooth. A milder discount (55% start, same ramp) and shorter ramps at the same 50% depth (100,
/// 150 ticks) all measured WORSE on the late game than 50/300 itself, which only makes sense as a
/// bot-AI threshold effect: a few extra ticks of cheap gold shift a bot's tower-coverage or
/// pressure-response timing by enough to change which regime it settles into, and that shift is not
/// monotonic in either parameter. Practical conclusion: 50/300 is a verified point, not the centre
/// of a safe region — retune by re-sweeping, not by nudging.
///
/// Swept on 20 matches (3 and 8 lanes, seeds 1-10), against a matching 20-run baseline:
///
/// <code>
///           first 750 ticks              whole match
///   config  sends  mean peak  max peak   mean ticks  max ticks  mean peak  max peak
///   base     59      7.5        12          3564       4711       181        477
///   50/300   67      9.6        19          3484       4599       135        432
/// </code>
///
/// Early send throughput and on-board creep presence both rise (+14%, +28% mean / +58% max) while
/// match length is within noise of the 20% bar this project treats as meaningful (-2.2%) — and the
/// late-game entity count this discount was measured against actually IMPROVES rather than costs
/// anything, both mean (-25%) and max (-9%). No tradeoff was found; the field around this point was
/// swept specifically to look for one.
/// </remarks>
public static class OpeningEconomyRules
{
    /// <summary>Percent of authored cost charged at tick 0.</summary>
    public const int StartPercent = 50;

    /// <summary>Tick at which cost reaches full authored price.</summary>
    public const int RampEndTick = 300;

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
