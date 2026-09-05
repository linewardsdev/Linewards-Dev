namespace LTW.MatchServer;

/// <summary>
/// The shape of a match's initial configuration — today, the body of <c>HttpMatchHost</c>'s
/// <c>POST /matches</c>. Its own file (not private to <c>HttpMatchHost</c>) because MP-07's GSDK
/// integration deserializes this same JSON shape out of PlayFab's <c>SessionCookie</c> instead of
/// an HTTP body, once a process is allocated to a match by PlayFab Multiplayer Servers rather than
/// asked to create one directly — see docs/MULTIPLAYER_ROLLOUT.md's MP-07.
/// </summary>
public sealed class CreateMatchRequest
{
    public List<int>? HumanSeats { get; set; }

    /// <summary>Real clients never set this — it exists so an integration test can play a
    /// match out in seconds instead of real-time minutes. See ServerMatch's own remarks.</summary>
    public double? TicksPerSecond { get; set; }

    /// <summary>Seat number -> the PlayFabId that alone may claim it. See
    /// MatchRegistry.CreateMatch and docs/MULTIPLAYER_ROLLOUT.md's MP-05.</summary>
    public Dictionary<int, string>? PlayFabSeats { get; set; }

    /// <summary>Real clients never set this either — same reasoning as <see cref="TicksPerSecond"/>,
    /// so a test can prove the opening build window's behavior without a real 30 second wait.</summary>
    public double? OpeningBuildWindowSeconds { get; set; }
}
