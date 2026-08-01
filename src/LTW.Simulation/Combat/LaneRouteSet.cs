using System.Collections.Generic;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

/// <summary>
/// The routes through a lane: the mazed one everything walks, and the direct one flyers take.
/// </summary>
/// <remarks>
/// Two routes exist because Spire Turret Walker ignores the maze. Everything else walks the route
/// the towers have shaped; it walks the one they have not.
///
/// The direct route is the same BFS run against a grid with no towers on it. Lane geometry never
/// changes during a match, so unlike the mazed route — recomputed on every placement, sell and
/// elimination — this one is computed once at match start and then never again.
///
/// Held as a pair rather than threaded as two dictionaries because a dozen call sites take routes
/// and every one of them would otherwise have to decide which to pass. Here the decision is made
/// once, in <see cref="For"/>, from the creep's own definition.
/// </remarks>
public sealed class LaneRouteSet
{
    private readonly IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> mazed;
    private readonly IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> direct;

    public LaneRouteSet(
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> mazed,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> direct)
    {
        this.mazed = mazed;
        this.direct = direct;
    }

    /// <summary>
    /// A set with no separate direct route, so every creep walks the mazed one.
    /// </summary>
    /// <remarks>
    /// What the compatibility overload of <c>CombatService.Advance</c> builds. Roughly fifty call
    /// sites — almost all of them tests and scenario harnesses — pass a single dictionary and have
    /// no flyers in them, so this keeps them meaning exactly what they meant before rather than
    /// making fifty edits to express "unchanged".
    /// </remarks>
    public static LaneRouteSet Single(IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes) =>
        new(routes, routes);

    /// <summary>The mazed route, which is what lane length and bramble zones are measured against.</summary>
    public IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> Mazed => mazed;

    /// <summary>The route this creep actually walks.</summary>
    public IReadOnlyList<GridPosition> For(LaneId laneId, bool ignoresMaze)
    {
        if (ignoresMaze && direct.TryGetValue(laneId, out var directRoute) && directRoute.Count > 0)
        {
            return directRoute;
        }

        return mazed[laneId];
    }
}
