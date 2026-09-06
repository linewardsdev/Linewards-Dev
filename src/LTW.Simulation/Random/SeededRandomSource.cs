using System;

namespace LTW.Simulation.Random;

/// <summary>
/// Backed by <see cref="System.Random"/> — NOT an owned algorithm. Its seeded sequence is not
/// part of .NET's documented compatibility contract, which matters here because MP-04's "server
/// replay matches what shipped" property, and every balance number this project has recorded as
/// "measured on seed N", both rest on the same seed always producing the same stream (see
/// <c>BotController.random</c>'s own remarks). Latent today only because the server (.NET 10) and
/// the client (the same DLL under Unity Mono/IL2CPP) happen to use the same generator family.
/// </summary>
/// <remarks>
/// An owned PRNG (PCG32, xorshift) was prototyped as the fix and reverted: it changes
/// <c>BotController</c>'s seeded line-preference draw for the SAME seed, which several tests tune
/// against (<c>BotMazingTests</c>, <c>VerticalSliceBridgeTests</c>) — three broke, not because
/// they pin an exact random value (none do), but because the specific line a bot commits to for a
/// given seed shifted along with the algorithm, and those tests assert on the resulting gameplay
/// (which line got built/upgraded), not on repeatability alone. Swapping the algorithm is safe by
/// itself; making it land cleanly needs a coordinated re-tuning of those seeds/assertions, which
/// is out of scope for a security-fix pass. See docs/SECURITY_AUDIT_2026-09-05.md's M-DET1.
/// </remarks>
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
