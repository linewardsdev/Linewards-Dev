namespace LTW.Simulation.Random;

/// <summary>
/// Reaches actual gameplay through <c>BotController.random</c> — a bot's opening line preference
/// is drawn from it (see that field's own remarks) — not merely scaffolding for a future need.
/// This comment previously claimed the opposite ("not used by any production code path today");
/// that was true only until <c>BotController</c> started consuming it, and was left uncorrected.
/// See docs/SECURITY_AUDIT_2026-09-05.md's M-DET1 and OPEN_ITEMS.md's retired 2026-07-29 review,
/// "replay records cannot reproduce a match", for why the seeded-repeatability contract this
/// interface exists to preserve matters at all.
/// </summary>
public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);

    double NextDouble();
}
