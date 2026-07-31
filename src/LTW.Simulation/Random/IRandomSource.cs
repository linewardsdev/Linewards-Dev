namespace LTW.Simulation.Random;

/// <summary>
/// Not used by any production code path today — the simulation is all-integer and deterministic
/// without it, and this (with <see cref="SeededRandomSource"/>) is referenced only by its own
/// contract test. Kept as scaffolding for a future non-deterministic need (e.g. cosmetic variance)
/// rather than deleted, since deleting it would mean re-authoring the same seeded-repeatability
/// contract from scratch if one comes up. See OPEN_ITEMS.md's retired 2026-07-29 review, "replay records cannot reproduce a match".
/// </summary>
public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);

    double NextDouble();
}
