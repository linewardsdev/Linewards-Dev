using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Pathing;

public sealed class LaneGrid
{
    private readonly HashSet<GridPosition> blockedCells;
    private readonly HashSet<GridPosition> occupiedCells;

    public LaneGrid(MapDefinition map)
        : this(map, Array.Empty<GridPosition>())
    {
    }

    public LaneGrid(MapDefinition map, IEnumerable<GridPosition> occupiedCells)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        blockedCells = new HashSet<GridPosition>(map.BlockedCells);
        this.occupiedCells = new HashSet<GridPosition>(occupiedCells ?? throw new ArgumentNullException(nameof(occupiedCells)));
    }

    public MapDefinition Map { get; }

    public int Width => Map.Width;

    public int Height => Map.Height;

    public GridPosition Spawn => Map.Spawn;

    public GridPosition Exit => Map.Exit;

    public IReadOnlyCollection<GridPosition> OccupiedCells => occupiedCells.ToArray();

    public bool Contains(GridPosition position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

    public bool IsSpawn(GridPosition position) => Spawn.Equals(position);

    public bool IsExit(GridPosition position) => Exit.Equals(position);

    public bool IsOccupied(GridPosition position) => occupiedCells.Contains(position);

    public bool IsBlocked(GridPosition position) => blockedCells.Contains(position);

    public bool IsWalkable(GridPosition position) =>
        Contains(position) && !IsBlocked(position) && !IsOccupied(position);

    public LaneGrid WithOccupied(GridPosition position) =>
        new LaneGrid(Map, occupiedCells.Concat(new[] { position }));

    public LaneGrid WithoutOccupied(GridPosition position) =>
        new LaneGrid(Map, occupiedCells.Where(cell => !cell.Equals(position)));
}
