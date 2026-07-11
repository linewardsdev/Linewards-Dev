using System;

namespace LTW.Simulation.Content;

public readonly struct ContentId : IEquatable<ContentId>
{
    public ContentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Content IDs cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public bool Equals(ContentId other) => StringComparer.Ordinal.Equals(Value, other.Value);

    public override bool Equals(object? obj) => obj is ContentId other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
