using System;

namespace LTW.Simulation.Random;

public sealed class SeededRandomSource : IRandomSource
{
    private readonly System.Random random;

    public SeededRandomSource(int seed)
    {
        random = new System.Random(seed);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Maximum must be greater than minimum.");
        }

        return random.Next(minInclusive, maxExclusive);
    }

    public double NextDouble() => random.NextDouble();
}
