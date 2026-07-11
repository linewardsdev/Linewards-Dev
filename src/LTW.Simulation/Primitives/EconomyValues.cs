using System;

namespace LTW.Simulation.Primitives;

public readonly struct Gold : IEquatable<Gold>
{
    public Gold(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Gold cannot be negative.");
        }

        Amount = amount;
    }

    public int Amount { get; }

    public bool Equals(Gold other) => Amount == other.Amount;

    public override bool Equals(object? obj) => obj is Gold other && Equals(other);

    public override int GetHashCode() => Amount;

    public override string ToString() => Amount.ToString();
}

public readonly struct Income : IEquatable<Income>
{
    public Income(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Income cannot be negative.");
        }

        Amount = amount;
    }

    public int Amount { get; }

    public bool Equals(Income other) => Amount == other.Amount;

    public override bool Equals(object? obj) => obj is Income other && Equals(other);

    public override int GetHashCode() => Amount;

    public override string ToString() => Amount.ToString();
}

public readonly struct Lives : IEquatable<Lives>
{
    public Lives(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Lives cannot be negative.");
        }

        Amount = amount;
    }

    public int Amount { get; }

    public bool Equals(Lives other) => Amount == other.Amount;

    public override bool Equals(object? obj) => obj is Lives other && Equals(other);

    public override int GetHashCode() => Amount;

    public override string ToString() => Amount.ToString();
}
