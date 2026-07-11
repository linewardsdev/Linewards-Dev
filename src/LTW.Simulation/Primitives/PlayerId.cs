using System;

namespace LTW.Simulation.Primitives;

public readonly struct PlayerId : IEquatable<PlayerId>
{
    public PlayerId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Player IDs must be positive.");
        }

        Value = value;
    }

    public int Value { get; }

    public bool IsValid => Value > 0;

    public bool Equals(PlayerId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is PlayerId other && Equals(other);

    public override int GetHashCode() => Value;

    public override string ToString() => Value.ToString();
}
