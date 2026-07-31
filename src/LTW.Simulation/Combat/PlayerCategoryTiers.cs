using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

/// <summary>
/// One player's purchased category tiers, in the shape combat needs to read them.
/// </summary>
/// <remarks>
/// Combat needs tiers but has no business knowing about gold, income, lives or elimination, so
/// this carries across only what scales damage and health. Threading
/// <see cref="EconomyPlayerSet"/> into <c>CombatService.Advance</c> instead would have dragged the
/// whole economy model into combat for two integers.
///
/// A snapshot, not a live view: it is rebuilt from the economy once per tick. That is deliberate —
/// a tier bought this tick applies from the next combat pass, and a creep already walking keeps
/// the health it spawned with.
/// </remarks>
public sealed class PlayerCategoryTiers
{
    private readonly int[] towerLineTiers;
    private readonly int[] sendCategoryTiers;

    public PlayerCategoryTiers(PlayerId playerId, int[] towerLineTiers, int[] sendCategoryTiers)
    {
        PlayerId = playerId;
        this.towerLineTiers = towerLineTiers;
        this.sendCategoryTiers = sendCategoryTiers;
    }

    public static PlayerCategoryTiers From(PlayerEconomyState player) =>
        new PlayerCategoryTiers(player.PlayerId, player.CopyTowerLineTiers(), player.CopySendCategoryTiers());

    public PlayerId PlayerId { get; }

    public int TowerLineTier(int lineIndex) => TierAt(towerLineTiers, lineIndex);

    public int SendCategoryTier(int categoryIndex) => TierAt(sendCategoryTiers, categoryIndex);

    private static int TierAt(int[] tiers, int index) =>
        index >= 0 && index < tiers.Length ? tiers[index] : PlayerEconomyState.BaseTier;
}
