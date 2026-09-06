using System.Collections.Generic;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Pathing;

/// <summary>
/// Not wired into production today — every call site that needs a lane's route
/// (<c>LocalVerticalSlice</c>'s own <c>SetRoute</c>/<c>pathService.FindRoute</c> call sites) calls
/// <see cref="GridPathService.FindRoute"/> directly, uncached, on every maze rebuild rather than
/// through this. Kept rather than deleted (see docs/SECURITY_AUDIT_2026-09-05.md's L6): it has its
/// own real behavioral contract (get-or-calculate plus per-lane invalidation) and its own test
/// (<c>PathingTests.Path_cache_invalidates_only_requested_lane</c>), so it is ready the moment
/// per-tick route recalculation becomes a measured cost rather than a re-authored one.
/// </summary>
public sealed class LanePathCache
{
    private readonly GridPathService pathService;
    private readonly Dictionary<LaneId, IReadOnlyList<GridPosition>> routes = new();

    public LanePathCache(GridPathService pathService)
    {
        this.pathService = pathService;
    }

    public IReadOnlyList<GridPosition> GetOrCalculate(LaneId laneId, LaneGrid grid)
    {
        if (routes.TryGetValue(laneId, out var route))
        {
            return route;
        }

        var result = pathService.FindRoute(grid);
        route = result.Found ? result.Route : System.Array.Empty<GridPosition>();
        routes[laneId] = route;
        return route;
    }

    public void Invalidate(LaneId laneId)
    {
        routes.Remove(laneId);
    }
}
