namespace LTW.Simulation.Content;

/// <summary>
/// Makes a long match close, by raising the health of newly sent creeps as it drags on.
/// </summary>
/// <remarks>
/// Two competent defences produce a stalemate. That is not a hypothetical: with mazing bots the
/// three-lane match was verified out to 80,000 ticks — about five and a half hours of game time —
/// with both survivors above 180 lives, sending into defences that held indefinitely. It is
/// recorded as a P1 in GAMEPLAY_REVIEW_FINDINGS.md, and the fix list there is exactly the three
/// options this file picks from: escalating creep strength, an income cap, or a sudden-death phase.
///
/// Until now the game did have a closing mechanism, but only by accident: income compounded without
/// limit, so eventually one side could buy through anything. Capping income (EconomyRules.IncomeCeiling)
/// removes that accident and the stalemate comes straight back, which is why the cap and this
/// escalation land together — one is not correct without the other.
///
/// Escalation is chosen over sudden death because it is symmetric and gradual. Both players get
/// tankier creeps at the same rate, so it does not hand the win to whoever happens to be ahead when
/// a timer expires; it just keeps raising the pressure until the better defence is the one that
/// holds a little longer, and the difference between the two sides compounds into a result.
///
/// Applied at PURCHASE time to newly sent creeps only, the same way a send-category tier is. Creeps
/// already walking a lane are never retroactively buffed — a creep's health is fixed the moment it
/// is bought, which keeps <see cref="CombatService.TransferCreep"/> honest across lane hops.
/// </remarks>
public static class MatchEscalationRules
{
    /// <summary>Tick before which creeps are exactly as authored.</summary>
    /// <remarks>
    /// Both this and <see cref="PercentPerInterval"/> were picked by sweeping them against seed 1 at
    /// three and eight lanes, not by feel. Holding the slope at 20, the start tick trades match
    /// length against how many creeps are alive at once:
    ///
    /// <code>
    ///   start   8-lane close / peak creeps   3-lane close / peak creeps
    ///    2400        4472 / 804                   4561 / 303
    ///    2000        4069 / 797                   4179 / 306
    ///    1600        3873 / 1017                  3814 / 312
    ///    1200        3676 / 1307                  3727 / 78
    /// </code>
    ///
    /// 2,000 is where peak creeps bottom out while still pulling ~400 ticks out of the match, so it
    /// is the best of the four rather than merely an acceptable one. Starting earlier does keep
    /// shortening matches, but only by putting more creeps on the board at once — which is the
    /// mobile performance problem this whole change exists to fix, so it is the wrong currency to
    /// pay in.
    /// </remarks>
    public const int StartTick = 2_000;

    /// <summary>How often the bonus steps up once escalation has started.</summary>
    public const int IntervalTicks = 100;

    /// <summary>Extra percent of authored health added per elapsed interval.</summary>
    /// <remarks>
    /// Also swept, at <see cref="StartTick"/> 2,400. 12 was too gentle — the three-lane seed closed
    /// at 5,229, outside the 5,000-tick bound this suite asserts. Above 20 the curve turns back on
    /// itself: at 25 the eight-lane match closes LATER (4,626 vs 4,472) with half again as many
    /// creeps alive (1,242 vs 804), because health that makes a creep harder to kill also makes it
    /// live longer and pile up. 20 is the floor of that curve, not a midpoint between extremes.
    ///
    /// <code>
    ///   slope   8-lane close / peak creeps   3-lane close / peak creeps
    ///     12        4665 / 556                   5229 / 265   (misses the bound)
    ///     16        4568 / 687                   4829 / 287
    ///     20        4472 / 804                   4561 / 303
    ///     25        4626 / 1242                  4341 / 308
    /// </code>
    /// </remarks>
    public const int PercentPerInterval = 20;

    /// <summary>
    /// Percent to apply to a newly sent creep's authored health at this tick.
    /// </summary>
    /// <returns>100 before <see cref="StartTick"/>, rising by <see cref="PercentPerInterval"/> per
    /// completed <see cref="IntervalTicks"/> after it.</returns>
    public static int CreepHealthPercentFor(long tick)
    {
        if (tick <= StartTick)
        {
            return 100;
        }

        return 100 + (int)((tick - StartTick) / IntervalTicks * PercentPerInterval);
    }
}
