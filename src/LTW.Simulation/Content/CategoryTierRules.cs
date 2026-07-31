using System;
using LTW.Simulation.Commands;
using LTW.Simulation.Economy;

namespace LTW.Simulation.Content;

/// <summary>
/// What a category tier costs and what it multiplies.
/// </summary>
/// <remarks>
/// One table rather than constants spread across combat, the bridge and the UI. Every consumer —
/// the damage seam, the spawn path, the bot's purchase check, the menu cards — reads these, so a
/// balance change is one edit and cannot leave the UI advertising a price the simulation does not
/// charge.
///
/// Percentages are integers applied as <c>value * percent / 100</c>, so the arithmetic stays exact
/// and deterministic. No floats reach a damage or health number.
///
/// The starting numbers come from docs/CATEGORY_UPGRADE_TIERS_PLAN.md. That doc calibrated the
/// creep/tower gap (225 vs 190 at the top) to let attack out-scale defence and break a permanent
/// bot stalemate — which was later found to be a bug in the bots' own pressure check, not a
/// balance property, and fixed. The gap is kept for now because a category you have invested two
/// purchases in should out-trade one you have not, but it is a starting point to be measured, not
/// a derived result. See GD_TUNING_LOG.md.
/// </remarks>
public static class CategoryTierRules
{
    public const int MaxTier = 3;

    // Index by tier: [unused, tier 1, tier 2, tier 3].
    private static readonly int[] TowerDamagePercent = { 0, 100, 115, 130 };
    private static readonly int[] CreepHealthPercent = { 0, 100, 150, 225 };

    // Cost to REACH the tier at that index. Tier 1 is free and default, so it costs nothing.
    // Each step is roughly 2.5x the one before, the rule stated in the design doc so a future
    // fourth tier does not need a fresh judgement call.
    private static readonly int[] TowerLineCost = { 0, 0, 140, 360 };
    private static readonly int[] SendCategoryCost = { 0, 0, 120, 300 };

    /// <summary>
    /// Percent to apply to a tower's authored damage at this tier.
    /// </summary>
    public static int TowerDamagePercentFor(int tier) => PercentAt(TowerDamagePercent, tier);

    /// <summary>
    /// Percent to apply to a creep's authored max health at this tier.
    /// </summary>
    public static int CreepHealthPercentFor(int tier) => PercentAt(CreepHealthPercent, tier);

    /// <summary>
    /// Gold to buy <paramref name="targetTier"/> for one category, or 0 if that tier is not
    /// purchasable (tier 1, or past the top).
    /// </summary>
    public static int CostFor(CategoryKind kind, int targetTier)
    {
        var costs = kind == CategoryKind.TowerLine ? TowerLineCost : SendCategoryCost;
        return targetTier >= 0 && targetTier < costs.Length ? costs[targetTier] : 0;
    }

    /// <summary>
    /// Scales an authored value by an integer percent, rounding to NEAREST, never below 1.
    /// </summary>
    /// <remarks>
    /// The rounding rule is load-bearing here, not a detail, because damage is a small integer and
    /// the multiplier is capped. All three candidates were measured across the 15-tower roster:
    ///
    ///   truncate — tier 2 improved 3 towers of 15, and tier 3 only 9. Five towers sit at 2 damage
    ///              (Arrow, Control, Relay, Gatling, Sapling) and 2 * 130 / 100 truncates back to
    ///              2, so a player could buy ARCANE to its top tier and see no change at all.
    ///   ceiling  — improved all 15 at tier 2, but by giving every 2-damage tower a flat +50%
    ///              regardless of the percent, which was enough to put two bot defences back into
    ///              the stalemate this feature must not cause.
    ///   nearest  — tier 2 improves 9 of 15, tier 3 improves 13 of those again, and every tower on
    ///              the roster is better at tier 3 than at tier 1. Chosen.
    ///
    /// A 2-damage tower still gains nothing at tier 2 and +1 at tier 3. That is the floor of what
    /// integers can express at this multiplier, and raising the multiplier is not available —
    /// tower scaling above 130% is exactly what stops matches ending. See GD_TUNING_LOG.md.
    ///
    /// The floor of 1 additionally stops a future sub-100% tier rounding a 1-damage tower to zero.
    /// </remarks>
    public static int Scale(int value, int percent) => Math.Max(1, (value * percent + 50) / 100);

    /// <summary>
    /// An unknown tier scales by 100% rather than throwing — the same reasoning as
    /// PlayerEconomyState.TierAt, since this runs inside damage and spawn paths.
    /// </summary>
    private static int PercentAt(int[] table, int tier) =>
        tier >= PlayerEconomyState.BaseTier && tier < table.Length ? table[tier] : 100;
}
