using System.Collections.Generic;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Pathing;

public sealed class GridPathService
{
    private static readonly (int X, int Y)[] NeighborOffsets =
    {
        (0, 1),
        (-1, 0),
        (1, 0),
        (0, -1)
    };

    public PathSearchResult FindRoute(LaneGrid grid)
    {
        var visited = new HashSet<GridPosition>();
        var previous = new Dictionary<GridPosition, GridPosition>();
        var queue = new Queue<GridPosition>();

        if (!grid.IsWalkable(grid.Spawn) || !grid.IsWalkable(grid.Exit))
        {
            return PathSearchResult.NoPath;
        }

        visited.Add(grid.Spawn);
        queue.Enqueue(grid.Spawn);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Equals(grid.Exit))
            {
                return PathSearchResult.Success(ReconstructRoute(previous, grid.Spawn, grid.Exit));
            }

            foreach (var neighbor in GetNeighbors(current))
            {
                if (!grid.IsWalkable(neighbor) || !visited.Add(neighbor))
                {
                    continue;
                }

                previous[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        return PathSearchResult.NoPath;
    }

    public PlacementValidationResult ValidatePlacement(LaneGrid grid, GridPosition position)
    {
        var basicRejection = GetBasicPlacementRejection(grid, position);
        if (basicRejection != PlacementRejectionReason.None)
        {
            return PlacementValidationResult.Reject(basicRejection);
        }

        var route = FindRoute(grid.WithOccupied(position));
        return route.Found
            ? PlacementValidationResult.Valid(route.Route)
            : PlacementValidationResult.Reject(PlacementRejectionReason.PathBlocked);
    }

    private static PlacementRejectionReason GetBasicPlacementRejection(LaneGrid grid, GridPosition position)
    {
        if (!grid.Contains(position))
        {
            return PlacementRejectionReason.OutsideGrid;
        }

        if (grid.IsSpawn(position))
        {
            return PlacementRejectionReason.SpawnCell;
        }

        if (grid.IsExit(position))
        {
            return PlacementRejectionReason.ExitCell;
        }

        if (grid.IsBlocked(position))
        {
            return PlacementRejectionReason.NotWalkable;
        }

        if (grid.IsOccupied(position))
        {
            return PlacementRejectionReason.AlreadyOccupied;
        }

        return PlacementRejectionReason.None;
    }

    private static IEnumerable<GridPosition> GetNeighbors(GridPosition position)
    {
        foreach (var offset in NeighborOffsets)
        {
            var x = position.X + offset.X;
            var y = position.Y + offset.Y;
            if (x >= 0 && y >= 0)
            {
                yield return new GridPosition(x, y);
            }
        }
    }

    private static IReadOnlyList<GridPosition> ReconstructRoute(
        IReadOnlyDictionary<GridPosition, GridPosition> previous,
        GridPosition spawn,
        GridPosition exit)
    {
        var route = new List<GridPosition>();
        var current = exit;
        route.Add(current);

        while (!current.Equals(spawn))
        {
            current = previous[current];
            route.Add(current);
        }

        route.Reverse();
        return route.ToArray();
    }
}
