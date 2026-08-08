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
    /// How much of a tier's power increase is charged back on the unit's own price.
    /// </summary>
    /// <remarks>
    /// A tier used to make its units strictly better at no change in price: a tier-3 send category
    /// put 225% health on the board for the gold that bought 100%, and a tier-3 tower line built
    /// 190% damage for the cost of 100%. Buying the tier was the whole transaction, and everything
    /// after it was free. That is the half of this mechanic that was missing.
    ///
    /// Charged as a share of the increase rather than a second hand-authored table, so it tracks
    /// <see cref="CreepHealthPercent"/> and <see cref="TowerDamagePercent"/> automatically and a
    /// retune of either cannot leave a price behind pointing at power that no longer exists — the
    /// same reasoning <see cref="MinimumIncomePercentOfCost"/> is written with.
    ///
    /// 65%, deliberately under 100. At 100 the tier would be economically neutral — you would pay
    /// exactly what the extra power is worth, and the only thing left to buy would be fewer units
    /// carrying the same total, which is not worth what a tier costs. At 65 a tier-3 creep carries
    /// 225% health for 181% price, so 24% more health per gold than tier 1, and the tier stays an
    /// upgrade while the free ride ends.
    /// </remarks>
    private const int PowerChargedBackPercent = 65;

    /// <summary>
    /// Percent to apply to a creep's authored send cost at this tier.
    /// </summary>
    /// <remarks>
    /// Tier 1 is exactly the authored cost, so an unupgraded roster is priced as written.
    /// </remarks>
    public static int SendCostPercentFor(int tier) => ChargedPercent(CreepHealthPercentFor(tier));

    /// <summary>
    /// Percent to apply to a tower's authored build cost at this tier.
    /// </summary>
    /// <remarks>
    /// Charged at build time, matching where the line tier is baked into the tower itself. A tower
    /// standing before the tier was bought keeps both its old damage and the price already paid;
    /// raising that one costs <see cref="TowerUpgradePercentOfCost"/> separately.
    /// </remarks>
    public static int TowerBuildCostPercentFor(int tier) => ChargedPercent(TowerDamagePercentFor(tier));

    private static int ChargedPercent(int powerPercent) =>
        100 + ((powerPercent - 100) * PowerChargedBackPercent / 100);

    /// <summary>
    /// How much each tier already bought adds to the price of the next one, as a percent of the
    /// next one's list price.
    /// </summary>
    /// <remarks>
    /// The tables above escalate <em>within</em> a track — tier 3 costs about 2.5x tier 2 — but
    /// said nothing about breadth. A player could take tier 2 in all six tracks for 780 gold flat,
    /// paying the same for the sixth as for the first, which made spreading across every category
    /// strictly better than committing to one. This is the sink that was missing.
    ///
    /// 25% of list, counted against every tier the player already holds anywhere. The first
    /// purchase is at list, the twelfth and last is at 3.75x it. Integer percent, applied as
    /// <c>value * percent / 100</c>, so the arithmetic stays exact and deterministic — the same
    /// rule the damage and health tables follow, and for the same reason.
    ///
    /// Counted across ALL tracks rather than per track, which is the literal shape of "each upgrade
    /// makes the next cost more". It does mean going deep costs more too: a second tier in the line
    /// you have already invested in is the same price as a first tier in a fresh one. That is a
    /// real tension with the design doc's stated goal of rewarding specialisation, and it is the
    /// obvious thing to change if the measured behaviour disappoints — count distinct tracks
    /// touched instead of total tiers held, and depth becomes free while breadth still pays.
    /// </remarks>
    public const int EscalationPercentPerUpgrade = 25;

    /// <summary>
    /// Tiers this player has already bought, across every tower line and send category.
    /// </summary>
    /// <remarks>
    /// Tier 1 is the free default, so a track contributes <c>tier - 1</c>. Reads the player's own
    /// state rather than a running counter, so it cannot drift out of step with what was actually
    /// purchased and needs nothing added to the replay record.
    /// </remarks>
    public static int UpgradesOwned(PlayerEconomyState player)
    {
        var tracksTouched = 0;
        for (var index = 0; index < PlayerEconomyState.CategoryCount; index++)
        {
            if (player.TowerLineTier(index) > 1)
            {
                tracksTouched++;
            }

            if (player.SendCategoryTier(index) > 1)
            {
                tracksTouched++;
            }
        }

        // Distinct tracks touched, not tiers held, and the difference is the whole point of the
        // sink. Counting tiers priced depth and breadth identically — a second tier in the line you
        // had already committed to cost exactly what a first tier in a fresh one did — so nothing
        // pushed a player to commit to anything. The design doc asked for a sink that "rewards
        // specializing in one tower line or send category over spreading thin", and counting tiers
        // did the opposite of that while looking like it was doing something.
        //
        // Counting tracks makes going deep nearly free and opening a third front expensive, which
        // is the pressure that was intended. Reported from play 2026-08-07: any spread of wards
        // across categories blends DPS, AOE and slow into something that cannot lose.
        return tracksTouched;
    }

    /// <summary>
    /// List price of <paramref name="targetTier"/> for one category, before escalation, or 0 if
    /// that tier is not purchasable (tier 1, or past the top).
    /// </summary>
    public static int CostFor(CategoryKind kind, int targetTier)
    {
        var costs = kind == CategoryKind.TowerLine ? TowerLineCost : SendCategoryCost;
        return targetTier >= 0 && targetTier < costs.Length ? costs[targetTier] : 0;
    }

    /// <summary>
    /// What <paramref name="targetTier"/> actually costs a player holding
    /// <paramref name="upgradesOwned"/> tiers already.
    /// </summary>
    /// <remarks>
    /// A tier nobody can buy stays free rather than escalating from zero to zero, so callers can
    /// use this for display without special-casing tier 1.
    /// </remarks>
    public static int CostFor(CategoryKind kind, int targetTier, int upgradesOwned)
    {
        var listPrice = CostFor(kind, targetTier);
        if (listPrice <= 0)
        {
            return 0;
        }

        var owned = upgradesOwned > 0 ? upgradesOwned : 0;
        return listPrice * (100 + (EscalationPercentPerUpgrade * owned)) / 100;
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
    /// Income required for <paramref name="targetTier"/> at the price this player actually pays.
    /// </summary>
    /// <remarks>
    /// The requirement is a share of the price, so escalating the price escalates this with it —
    /// which is the intended coupling, but it has a hard edge worth stating. The dearest purchase
    /// on the board is a tower line's tier 3 at list 360; held to last it is the twelfth upgrade,
    /// costs 1350, and demands 675 income. Against the old ceiling of 600 that tier was not
    /// expensive, it was <em>unreachable</em> — the gate would have refused it at any bank.
    ///
    /// That is why <see cref="EconomyRules.DefaultIncomeCeiling"/> moved to 900 in the same change.
    /// Anyone retuning either the escalator or the tables should re-check this: the highest
    /// requirement the board can produce has to stay under the ceiling, or the last upgrades quietly
    /// become impossible rather than costly.
    /// </remarks>
    public static int MinimumIncomeFor(CategoryKind kind, int targetTier, int upgradesOwned) =>
        CostFor(kind, targetTier, upgradesOwned) * MinimumIncomePercentOfCost / 100;

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
