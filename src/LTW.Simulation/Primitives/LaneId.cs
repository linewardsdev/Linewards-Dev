using System;

namespace LTW.Simulation.Primitives;

public readonly struct LaneId : IEquatable<LaneId>
{
    public LaneId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Lane IDs must be positive.");
        }

        Value = value;
    }

    public int Value { get; }

    public bool IsValid => Value > 0;

    public bool Equals(LaneId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is LaneId other && Equals(other);

    public override int GetHashCode() => Value;

    public override string ToString() => Value.ToString();
}
