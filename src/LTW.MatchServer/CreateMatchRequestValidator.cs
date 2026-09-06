using LTW.Simulation.Bridge;

namespace LTW.MatchServer;

/// <summary>
/// One validator shared by <c>HttpMatchHost.HandleCreateMatchAsync</c> (an unauthenticated HTTP
/// body, standalone mode) and <c>Program.cs</c>'s MPS bootstrap (a client-authored PlayFab
/// <c>SessionCookie</c>, deserialized into the exact same <see cref="CreateMatchRequest"/> shape)
/// — both need these same bounds checked before <see cref="MatchRegistry.CreateMatch"/>/
/// <see cref="ServerMatch"/> ever sees these numbers. Without this: <c>ticksPerSecond = 0</c>
/// throws <c>OverflowException</c> building the tick <c>PeriodicTimer</c>; <c>-1000</c> exactly
/// hits <c>Timeout.Infinite</c>, hanging the tick loop forever; <c>openingBuildWindowSeconds =
/// 1e308</c> throws inside the <c>TimeSpan</c> constructor, crashing the process during MPS
/// allocation; an out-of-range seat binds but then throws <c>KeyNotFoundException</c> the moment
/// that seat sends any economy command. See docs/SECURITY_AUDIT_2026-09-05.md's M1.
/// </summary>
public static class CreateMatchRequestValidator
{
    // 1000 is not an arbitrary product choice — it is PeriodicTimer's own natural ceiling
    // (ServerMatch.cs's tick loop already gets ArgumentOutOfRangeException from the BCL above
    // this), so rejecting anything past it here just moves that same rejection to a clear 400
    // instead of a crash three layers down. The test suite's own fast-forwarded matches run up to
    // 200 ticks/second — a tighter cap such as 60 would reject legitimate test/tooling use, not
    // just abuse.
    public const double MinTicksPerSecond = 1;
    public const double MaxTicksPerSecond = 1000;
    public const double MinOpeningBuildWindowSeconds = 0;
    public const double MaxOpeningBuildWindowSeconds = 300;

    public static bool TryValidate(
        IReadOnlyCollection<int> humanSeats,
        IReadOnlyDictionary<int, string>? playFabSeats,
        double? ticksPerSecond,
        double? openingBuildWindowSeconds,
        out string? error)
    {
        if (humanSeats.Count == 0
            || humanSeats.Distinct().Count() != humanSeats.Count
            || humanSeats.Any(seat => seat < 1 || seat > LocalMatchOptions.MaxLaneCount))
        {
            error = $"humanSeats must be distinct values in [1, {LocalMatchOptions.MaxLaneCount}].";
            return false;
        }

        if (playFabSeats is not null && playFabSeats.Keys.Any(seat => !humanSeats.Contains(seat)))
        {
            error = "playFabSeats keys must be a subset of humanSeats.";
            return false;
        }

        if (ticksPerSecond is { } resolvedTicksPerSecond
            && (resolvedTicksPerSecond < MinTicksPerSecond || resolvedTicksPerSecond > MaxTicksPerSecond))
        {
            error = $"ticksPerSecond must be in [{MinTicksPerSecond}, {MaxTicksPerSecond}].";
            return false;
        }

        if (openingBuildWindowSeconds is { } resolvedOpeningBuildWindowSeconds
            && (resolvedOpeningBuildWindowSeconds < MinOpeningBuildWindowSeconds || resolvedOpeningBuildWindowSeconds > MaxOpeningBuildWindowSeconds))
        {
            error = $"openingBuildWindowSeconds must be in [{MinOpeningBuildWindowSeconds}, {MaxOpeningBuildWindowSeconds}].";
            return false;
        }

        error = null;
        return true;
    }
}
