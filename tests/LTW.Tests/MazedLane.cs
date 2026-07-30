using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using LTW.Simulation.Pathing;
using LTW.Simulation.Primitives;

namespace LTW.Tests;

/// <summary>
/// A lane with a real maze in it, for harnesses that need to measure a tower against the route players
/// actually create.
/// </summary>
/// <remarks>
/// Every measurement harness in this project used to build its own route as a straight line —
/// <c>Enumerable.Range(0, 18).Select(y =&gt; new GridPosition(3, y))</c> — which was wrong twice over. The
/// real map is 7x16 with spawn (3,0) and exit (3,15), so the straight route is 16 cells, not 18. And more
/// importantly this game is about MAZING: once the bots learned to do it their lane route went from 16
/// cells to 40. A tower beside a 16-cell straight lane sees roughly a third of the exposure it sees beside
/// a real maze, so every damage-per-gold figure, mechanic contribution and mandatory-buy verdict measured
/// against a straight line was calibrated against a defence no player would ever build.
///
/// The maze here is built with the REAL <see cref="GridPathService"/> against the REAL map, adding cells
/// one at a time and keeping only those that leave a path — the same rule placement enforces in play. It
/// is not a hand-drawn shape that happens to be long.
///
/// The maze is GEOMETRY ONLY: the cells are occupied for pathing but are not given combat state, so they
/// do not shoot. That is deliberate for contribution measurements, where the point is to isolate one
/// tower's output. A harness that wants a real bundle of shooters should add them explicitly.
/// </remarks>
public sealed class MazedLane
{
    public static readonly LaneId Lane = new(1);

    private MazedLane(IReadOnlyList<GridPosition> route, IReadOnlyList<GridPosition> mazeCells, MapDefinition map)
    {
        Route = route;
        MazeCells = mazeCells;
        Map = map;
    }

    public IReadOnlyList<GridPosition> Route { get; }

    /// <summary>Cells the maze occupies. A subject tower must not be placed on one of these.</summary>
    public IReadOnlyList<GridPosition> MazeCells { get; }

    public MapDefinition Map { get; }

    public IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> Routes() =>
        new Dictionary<LaneId, IReadOnlyList<GridPosition>> { [Lane] = Route };

    /// <summary>
    /// The straight route the same map produces with nothing built, for before/after comparisons.
    /// </summary>
    public static IReadOnlyList<GridPosition> StraightRoute()
    {
        var map = SampleVerticalSliceContent.Create().Maps[0];
        return new GridPathService().FindRoute(new LaneGrid(map)).Route;
    }

    /// <summary>
    /// Builds a serpentine maze by walling alternate rows from alternate ends, which is the shape a player
    /// mazing for length converges on.
    /// </summary>
    /// <remarks>
    /// Each cell is offered to <see cref="GridPathService.ValidatePlacement"/> and dropped if it would seal
    /// the lane, so the result is always walkable. Rows are spaced two apart to leave a corridor between
    /// walls, and the alternating open end is what forces the path back and forth.
    /// </remarks>
    public static MazedLane Build()
    {
        var map = SampleVerticalSliceContent.Create().Maps[0];
        var pathService = new GridPathService();
        var occupied = new List<GridPosition>();

        for (var row = 2; row < map.Height - 2; row += 2)
        {
            // Alternate which end stays open so the route has to cross the full width each time.
            var openColumn = row / 2 % 2 == 0 ? map.Width - 1 : 0;
            for (var column = 0; column < map.Width; column++)
            {
                if (column == openColumn)
                {
                    continue;
                }

                var candidate = new GridPosition(column, row);
                var grid = new LaneGrid(map, occupied);
                if (pathService.ValidatePlacement(grid, candidate).IsValid)
                {
                    occupied.Add(candidate);
                }
            }
        }

        var finalGrid = new LaneGrid(map, occupied);
        var route = pathService.FindRoute(finalGrid).Route;
        return new MazedLane(route, occupied, map);
    }

    /// <summary>
    /// An empty cell adjacent to the route at roughly <paramref name="fractionAlongRoute"/> of its length,
    /// for placing the tower under test.
    /// </summary>
    /// <remarks>
    /// Picking by fraction rather than by coordinate is what lets a harness say "mid-lane" without knowing
    /// the maze's shape, so the maze can change without every caller needing new numbers. Returns the first
    /// orthogonally adjacent free cell, searched outward from the chosen route index, so the tower always
    /// has something to shoot at.
    /// </remarks>
    public GridPosition CellBesideRoute(double fractionAlongRoute)
    {
        var target = (int)(Route.Count * fractionAlongRoute);
        for (var offset = 0; offset < Route.Count; offset++)
        {
            foreach (var index in new[] { target + offset, target - offset })
            {
                if (index < 0 || index >= Route.Count)
                {
                    continue;
                }

                var cell = Route[index];
                // Bounds are checked on the raw coordinates BEFORE constructing a GridPosition, because
                // its constructor throws on a negative axis rather than returning something invalid.
                foreach (var (x, y) in new[] { (cell.X - 1, cell.Y), (cell.X + 1, cell.Y), (cell.X, cell.Y - 1), (cell.X, cell.Y + 1) })
                {
                    if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height)
                    {
                        continue;
                    }

                    var neighbour = new GridPosition(x, y);
                    if (IsFree(neighbour))
                    {
                        return neighbour;
                    }
                }
            }
        }

        throw new System.InvalidOperationException("no free cell adjacent to the mazed route");
    }

    /// <summary>
    /// Free cells orthogonally adjacent to <paramref name="cell"/>, for harnesses that need to place a
    /// support tower touching the one under test — Grovebond's bonded neighbours and the Repair Drone's
    /// serviced neighbour both depend on adjacency, so they cannot use arbitrary spare cells.
    /// </summary>
    public IReadOnlyList<GridPosition> FreeNeighboursOf(GridPosition cell)
    {
        var found = new List<GridPosition>();
        foreach (var (x, y) in new[] { (cell.X - 1, cell.Y), (cell.X + 1, cell.Y), (cell.X, cell.Y - 1), (cell.X, cell.Y + 1) })
        {
            if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height)
            {
                continue;
            }

            var neighbour = new GridPosition(x, y);
            if (IsFree(neighbour))
            {
                found.Add(neighbour);
            }
        }

        return found;
    }

    private bool IsFree(GridPosition position) =>
        !Route.Any(cell => cell.Equals(position)) &&
        !MazeCells.Any(cell => cell.Equals(position));
}
