using System.Collections.Generic;
using LTW.Simulation.Combat;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bots;

/// <summary>
/// The slice of a live match a bot can see and act on.
/// </summary>
/// <remarks>
/// This is the seam that got the bot AI out of the match bridge (OPEN_ITEMS.md item 26). About a
/// third of <c>LocalVerticalSlice</c> was bot decision logic — where to build, what to build, which
/// tier to buy, which tower to raise — sitting there for one reason only: that is where the board
/// state and the command methods happened to be. Naming what the bot actually needs turns out to be
/// a short list, and everything else about a match stays private to the bridge.
///
/// The division of labour is deliberate and is the thing to keep honest when adding to this:
/// <b>the context answers questions of fact, the bot decides what to do about them.</b> So the
/// bridge is asked "how much live creep health is in this lane" (a fact, and one with a real trap
/// in it — see <see cref="LiveCreepHealthIn"/>) and the bot decides what counts as too much. The
/// bridge is asked "what has this seat sent" and the bot decides which category that makes worth
/// deepening. A method here that returns a judgement rather than a measurement is the bot leaking
/// back into the bridge.
///
/// Every mutating method returns whether the command was accepted rather than a rejection reason:
/// a bot's response to any rejection is the same — do nothing this tick and re-read the board next
/// tick — so the reason would be a detail no caller could act on.
///
/// Determinism: nothing here is order-dependent except through the commands it issues, which the
/// bridge already sequences in player-id order. Implementations must not iterate an unordered
/// collection to produce a result that a decision then depends on.
/// </remarks>
public interface IBotMatchContext
{
    /// <summary>The catalog this match is being played with — tower and creep stats, bot profiles.</summary>
    ContentCatalog Content { get; }

    /// <summary>The tick currently being decided.</summary>
    SimulationTick Tick { get; }

    /// <summary>This seat's gold, income, lives, elimination state and category tiers.</summary>
    PlayerEconomyState PlayerState(PlayerId playerId);

    /// <summary>The lane this seat defends, and the only one it may build in.</summary>
    LaneId HomeLaneFor(PlayerId playerId);

    /// <summary>Every tower this seat owns, in any lane.</summary>
    /// <remarks>
    /// Not filtered to the home lane, because a bot's tower count is a fact about the seat rather
    /// than about a lane — an eliminated seat has had its lane wiped and owns nothing anywhere.
    /// Callers that need one lane filter it themselves.
    /// </remarks>
    IReadOnlyList<TowerCombatState> TowersOwnedBy(PlayerId playerId);

    /// <summary>Total health of the creeps currently walking a lane.</summary>
    /// <remarks>
    /// Excludes creeps that have already reached the end of this lane. That is load-bearing rather
    /// than tidiness, and it is the reason this is a bridge query rather than a filter the bot
    /// writes for itself: a creep that finishes a lane is not despawned, it transfers onward as a
    /// NEW entity, and the spent entity stays in combat state tagged with the lane it left, still
    /// holding the health it left with. A caller that forgets the filter sees a number that grows
    /// monotonically for the whole match. Bot pressure was the one consumer in the codebase that
    /// did forget it, which locked bots out of sending for the rest of a match once it crossed
    /// their threshold — with it, the same seed completes at tick 926 instead of running past
    /// 80,000.
    /// </remarks>
    int LiveCreepHealthIn(LaneId laneId);

    /// <summary>The route creeps currently walk in a lane, or an empty list for an unknown lane.</summary>
    IReadOnlyList<GridPosition> RouteFor(LaneId laneId);

    /// <summary>A tower definition by id, or null when the catalog does not contain one.</summary>
    /// <remarks>
    /// Resolved by the bridge rather than scanned out of <see cref="Content"/> by the caller: the
    /// bridge indexes the catalog by id, and these lookups sit in per-tick paths where a linear
    /// scan per lookup was measurable enough to be worth removing once already (OPEN_ITEMS.md item
    /// 27).
    /// </remarks>
    TowerDefinition? FindTower(ContentId towerId);

    /// <summary>
    /// What would happen if this seat placed this tower here, without placing anything.
    /// </summary>
    /// <remarks>
    /// The whole of the mazing search runs through this — one probe per legal cell per placement —
    /// so it returns the route the placement WOULD produce rather than just a yes/no. Working that
    /// out without the bridge would mean the bot owning a copy of the lane grid and the pathfinder,
    /// which is exactly the coupling this interface exists to avoid.
    /// </remarks>
    BotPlacementProbe ProbePlacement(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position);

    /// <summary>
    /// How many creeps this seat has sent, totalled per send category.
    /// </summary>
    /// <remarks>
    /// A fact about the match, indexed by <c>CreepDefinition.CategoryIndex</c> and always
    /// <c>PlayerEconomyState.CategoryCount</c> long. What to make of it — that the most-sent
    /// category is the one worth buying a tier in — is the bot's business, in
    /// <c>BotController.MostCommittedCategory</c>.
    /// </remarks>
    IReadOnlyList<int> SendsByCategory(PlayerId playerId);

    /// <summary>Queues a send, and returns whether the economy accepted it.</summary>
    bool TrySend(PlayerId playerId, ContentId creepId, int quantity);

    /// <summary>Places a tower, and returns whether the placement was accepted.</summary>
    bool TryPlaceTower(PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position);

    /// <summary>Buys the next tier of one category, and returns whether the purchase was accepted.</summary>
    bool TryBuyCategoryTier(PlayerId playerId, CategoryKind categoryKind, int categoryIndex, int targetTier);

    /// <summary>Raises one placed tower by a tier, and returns whether the upgrade was accepted.</summary>
    bool TryUpgradeTower(PlayerId playerId, LaneId laneId, GridPosition position);
}

/// <summary>The outcome of asking what a placement would do, without doing it.</summary>
public readonly struct BotPlacementProbe
{
    private BotPlacementProbe(bool accepted, IReadOnlyList<GridPosition> route)
    {
        Accepted = accepted;
        Route = route;
    }

    /// <summary>Whether the placement would be legal — affordable, in the seat's own lane, not sealing the route.</summary>
    public bool Accepted { get; }

    /// <summary>The route creeps would walk afterwards. Empty when <see cref="Accepted"/> is false.</summary>
    public IReadOnlyList<GridPosition> Route { get; }

    public static BotPlacementProbe Allowed(IReadOnlyList<GridPosition> route) => new BotPlacementProbe(true, route);

    public static BotPlacementProbe Rejected { get; } = new BotPlacementProbe(false, System.Array.Empty<GridPosition>());
}
