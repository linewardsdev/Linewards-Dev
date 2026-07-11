using System.Collections.Generic;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Pathing;

public sealed class PathSearchResult
{
    private PathSearchResult(bool found, IReadOnlyList<GridPosition> route)
    {
        Found = found;
        Route = route;
    }

    public bool Found { get; }

    public IReadOnlyList<GridPosition> Route { get; }

    public static PathSearchResult Success(IReadOnlyList<GridPosition> route) => new PathSearchResult(true, route);

    public static PathSearchResult NoPath { get; } = new PathSearchResult(false, System.Array.Empty<GridPosition>());
}
