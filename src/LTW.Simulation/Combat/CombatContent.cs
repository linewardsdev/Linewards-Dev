using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CombatContent
{
    private static readonly IReadOnlyDictionary<PlayerId, PlayerCategoryTiers> NoTiers =
        new Dictionary<PlayerId, PlayerCategoryTiers>();

    private readonly IReadOnlyDictionary<ContentId, CreepDefinition> creeps;
    private readonly IReadOnlyDictionary<ContentId, TowerDefinition> towers;
    private readonly IReadOnlyDictionary<LaneId, PlayerId> laneOwners;
    private readonly IReadOnlyDictionary<PlayerId, PlayerCategoryTiers> playerTiers;

    public CombatContent(
        IEnumerable<CreepDefinition> creeps,
        IEnumerable<TowerDefinition> towers,
        IReadOnlyDictionary<LaneId, PlayerId> laneOwners)
    {
        this.creeps = (creeps ?? throw new ArgumentNullException(nameof(creeps))).ToDictionary(creep => creep.Id);
        this.towers = (towers ?? throw new ArgumentNullException(nameof(towers))).ToDictionary(tower => tower.Id);
        this.laneOwners = new Dictionary<LaneId, PlayerId>(laneOwners ?? throw new ArgumentNullException(nameof(laneOwners)));
        playerTiers = NoTiers;
    }

    private CombatContent(
        IReadOnlyDictionary<ContentId, CreepDefinition> creeps,
        IReadOnlyDictionary<ContentId, TowerDefinition> towers,
        IReadOnlyDictionary<LaneId, PlayerId> laneOwners,
        IReadOnlyDictionary<PlayerId, PlayerCategoryTiers> playerTiers)
    {
        this.creeps = creeps;
        this.towers = towers;
        this.laneOwners = laneOwners;
        this.playerTiers = playerTiers;
    }

    public CreepDefinition GetCreep(ContentId creepId) => creeps[creepId];

    public TowerDefinition GetTower(ContentId towerId) => towers[towerId];

    public PlayerId GetLaneOwner(LaneId laneId) => laneOwners[laneId];

    /// <summary>
    /// Rebuilds this content carrying a fresh set of per-player category tiers.
    /// </summary>
    /// <remarks>
    /// Creeps, towers and lane ownership are fixed for a match; tiers are not — a player can buy
    /// one at any point — so tiers cannot be baked in at construction like everything else here.
    /// A wither keeps this type immutable, matching PlayerEconomyState and TowerCombatState, and
    /// reuses the three existing dictionaries by reference rather than rebuilding them, so calling
    /// this once per tick costs one small allocation instead of re-indexing the whole catalog.
    /// </remarks>
    public CombatContent WithPlayerTiers(IReadOnlyDictionary<PlayerId, PlayerCategoryTiers> tiers) =>
        new CombatContent(creeps, towers, laneOwners, tiers ?? NoTiers);

    /// <summary>
    /// A player's tower-line tier (0 ARCANE, 1 FOUNDRY, 2 GROVE).
    /// </summary>
    /// <remarks>
    /// Answers <see cref="PlayerEconomyState.BaseTier"/> for a player with no recorded tiers rather
    /// than throwing. Several test and scenario harnesses build a CombatContent directly with no
    /// economy behind it; they should get baseline damage, not a KeyNotFoundException raised from
    /// the middle of a damage calculation.
    /// </remarks>
    public int TowerLineTierFor(PlayerId playerId, int lineIndex) =>
        playerTiers.TryGetValue(playerId, out var tiers) ? tiers.TowerLineTier(lineIndex) : PlayerEconomyState.BaseTier;

    /// <summary>
    /// A player's send-category tier (0 CORE, 1 RAPID, 2 ELITE).
    /// </summary>
    public int SendCategoryTierFor(PlayerId playerId, int categoryIndex) =>
        playerTiers.TryGetValue(playerId, out var tiers) ? tiers.SendCategoryTier(categoryIndex) : PlayerEconomyState.BaseTier;
}
