namespace LTW.MatchServer;

/// <summary>
/// Turns the player list a PlayFab matchmaking queue's `ServerAllocationEnabled` auto-allocation
/// hands this process (via <c>GameserverSDK.GetInitialPlayers()</c>) into the same
/// <c>humanSeats</c>/<c>playFabIdBySeat</c> shape a direct <c>RequestMultiplayerServer</c> call's
/// <c>SessionCookie</c> already provides — see docs/MULTIPLAYER_ROLLOUT.md's MP-05. A
/// queue-allocated server has no meaningful <c>SessionCookie</c> of its own (nothing ever called
/// <c>RequestMultiplayerServer</c> directly to set one), so this is the only source of truth for
/// who's actually in the match in that case.
/// </summary>
/// <remarks>
/// Pure and GSDK-free on purpose: <c>Program.cs</c>'s own GSDK calls aren't unit-testable without a
/// real or simulated agent (this project's own established limit — see MULTIPLAYER_ROLLOUT.md's
/// MP-07), but the actual seat-assignment logic has no reason to share that limitation.
/// </remarks>
public static class QueuedMatchBootstrap
{
    /// <summary>
    /// Assigns each matched PlayFabId to sequential seats starting at 1 — the same seats a direct
    /// request's <c>humanSeats: [1]</c> (and, eventually, higher seat numbers) would use. Seats
    /// beyond the returned list are left for <c>MatchRegistry.CreateMatch</c>'s own bot fill,
    /// exactly as an under-capacity direct request already leaves them today.
    /// </summary>
    public static (List<int> HumanSeats, Dictionary<int, string> PlayFabIdBySeat) AssignSeatsFromInitialPlayers(IEnumerable<string> initialPlayers)
    {
        var humanSeats = new List<int>();
        var playFabIdBySeat = new Dictionary<int, string>();
        var seat = 1;
        foreach (var playFabId in initialPlayers)
        {
            humanSeats.Add(seat);
            playFabIdBySeat[seat] = playFabId;
            seat++;
        }

        return (humanSeats, playFabIdBySeat);
    }
}
