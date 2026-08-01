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
/// The numbers are the design doc's original 140/190 and 150/225. They were cut to 115/130 while
/// a line tier applied to every tower at once, because at 190% two bot defences could no longer
/// finish a match. Making the tier per-tower — bought once per tower rather than once per line —
/// removed that cliff: at 140/190 every lane count now completes, and the 3-lane match moves only
/// from 3290 to 3406 ticks. Paying for each tower is what makes the designed numbers safe, because
/// the upgrade arrives gradually instead of transforming a whole line the moment it is bought.
/// See GD_TUNING_LOG.md.
/// </remarks>
public static class CategoryTierRules
{
    public const int MaxTier = 3;

    // Index by tier: [unused, tier 1, tier 2, tier 3].
    private static readonly int[] TowerDamagePercent = { 0, 100, 140, 190 };
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
    /// Income a player must already be earning before a tier can be bought, as a share of its price.
    /// </summary>
    /// <remarks>
    /// Half, so a tier has to pay for itself in about two income ticks before you are allowed to buy
    /// it. Expressed as a share rather than a second hand-authored table for the same reason
    /// <see cref="TowerUpgradePercentOfCost"/> is: the requirement then tracks the price
    /// automatically, and a future retune of TowerLineCost or SendCategoryCost cannot leave a
    /// threshold behind pointing at a number that no longer exists.
    /// </remarks>
    private const int MinimumIncomePercentOfCost = 50;

    /// <summary>
    /// Income required before <paramref name="targetTier"/> may be bought for this category.
    /// </summary>
    /// <remarks>
    /// Upgrades were gated on gold alone, and gold and income are not the same claim. Gold arrives
    /// from kills, leaks and the opening bank, so a player who never sends can sit on a lane, bank
    /// bounties and buy a tier at the starting income of 10 — buying power without ever building
    /// the economy that is supposed to pay for it. Income can only be raised by sending, so gating
    /// on it makes a tier something earned by playing the game's own economic loop.
    ///
    /// Deliberately BELOW where bots already sit, so this changes what a human can rush and nothing
    /// about the balance already measured. Sampled across a three- and an eight-lane match, bots buy
    /// tier 2 at income 198-321 and tier 3 at 407-600, against requirements of 60-70 and 150-180.
    /// Not one bot purchase in either match would have been refused.
    ///
    /// Returns 0 for tier 1 and for anything past the top, matching <see cref="CostFor"/> — a tier
    /// nobody can buy needs no threshold.
    /// </remarks>
    public static int MinimumIncomeFor(CategoryKind kind, int targetTier) =>
        CostFor(kind, targetTier) * MinimumIncomePercentOfCost / 100;

    /// <summary>
    /// Share of a tower's build cost charged to raise that one tower by a tier.
    /// </summary>
    /// <remarks>
    /// A share rather than a flat number so it scales across a roster spanning 10 to 52 gold
    /// without a second per-tower table: upgrading a Sapling stays cheap and upgrading a Foundry
    /// Core stays a commitment. Below 100% on purpose — at full price, selling and rebuilding at
    /// the higher tier would always match it and the button would have no reason to exist.
    /// </remarks>
    private const int TowerUpgradePercentOfCost = 60;

    /// <summary>
    /// Gold to raise one placed tower by a tier.
    /// </summary>
    public static int TowerUpgradeCost(int towerBuildCost) =>
        Math.Max(1, towerBuildCost * TowerUpgradePercentOfCost / 100);

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
    /// The measurements above were taken at 115/130, when the multiplier was capped by the
    /// stalemate. At the restored 140/190 truncation would hurt less, but nearest is kept: it is
    /// the rule that guarantees every tower on the roster gains at every tier rather than only the
    /// ones whose authored damage happens to divide well.
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
