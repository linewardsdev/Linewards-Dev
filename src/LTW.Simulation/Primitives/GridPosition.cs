using System;

namespace LTW.Simulation.Primitives;

public readonly struct GridPosition : IEquatable<GridPosition>
{
    public GridPosition(int x, int y)
    {
        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Grid X cannot be negative.");
        }

        if (y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Grid Y cannot be negative.");
        }

        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    public bool Equals(GridPosition other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is GridPosition other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"{X},{Y}";
}
