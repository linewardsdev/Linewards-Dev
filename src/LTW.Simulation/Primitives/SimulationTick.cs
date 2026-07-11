using System;

namespace LTW.Simulation.Primitives;

public readonly struct SimulationTick : IEquatable<SimulationTick>, IComparable<SimulationTick>
{
    public SimulationTick(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Ticks cannot be negative.");
        }

        Value = value;
    }

    public long Value { get; }

    public bool Equals(SimulationTick other) => Value == other.Value;

    public int CompareTo(SimulationTick other) => Value.CompareTo(other.Value);

    public override bool Equals(object? obj) => obj is SimulationTick other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();
}
