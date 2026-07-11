using System.Collections.Generic;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Pathing;

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
