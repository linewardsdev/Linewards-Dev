using System;

namespace LTW.Simulation.Primitives;

public readonly struct EntityId : IEquatable<EntityId>
{
    public EntityId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Entity IDs must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public bool IsValid => Value > 0;

    public bool Equals(EntityId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();
}
