using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;

namespace LTW.MatchServer;

/// <summary>
/// Every live match this process hosts, by id — MP-04's "N isolated authoritative match
/// instances". No matchmaking and no accounts: a match is created explicitly, by whoever is
/// starting a private game, and its id and per-seat join tokens are handed back for the seats'
/// players to use directly. See docs/MULTIPLAYER_ROLLOUT.md's MP-05 for where matchmaking and
/// real identity belong instead.
/// </summary>
public sealed class MatchRegistry
{
    private readonly Dictionary<string, ServerMatch> matches = new();
    private readonly ContentCatalog content = SampleVerticalSliceContent.Create();
    private readonly string replayDirectory;

    /// <summary>
    /// Shared across every match this process hosts — stateless besides its own HttpClient and
    /// title config, so one instance is correct rather than one per match. Null when
    /// <c>PLAYFAB_TITLE_ID</c>/<c>PLAYFAB_SECRET_KEY</c> are not set (see <c>Program.cs</c>):
    /// nothing in this process requires PlayFab to run, only PlayFab-identified seats to work —
    /// see docs/PLAYFAB_SETUP.md.
    /// </summary>
    private readonly LTW.MatchServer.PlayFab.PlayFabSessionAuthority? playFabAuthority;

    public MatchRegistry(string replayDirectory, LTW.MatchServer.PlayFab.PlayFabSessionAuthority? playFabAuthority = null)
    {
        this.replayDirectory = replayDirectory;
        this.playFabAuthority = playFabAuthority;
    }

    /// <summary>
    /// Creates and starts a private, always-eight-lane match. Seats in
    /// <paramref name="humanSeats"/> get a join token and no bot, UNLESS they also appear in
    /// <paramref name="playFabIdBySeat"/>, in which case that specific PlayFabId (verified against
    /// PlayFab, not trusted from the join request — see <see cref="ServerMatch.AcceptWithPlayFabAsync"/>)
    /// is the only thing that can claim the seat. Every other seat is bot-driven, using the same
    /// default profiles a local match would (<see cref="LocalMatchOptions.Default"/>).
    /// </summary>
    /// <remarks>
    /// <paramref name="playFabIdBySeat"/> is deliberately the same shape a future matchmaking
    /// result handler would produce (a seat number to PlayFabId map) — this method does not care
    /// whether a human or a matchmaker decided that mapping, per MP-05's own architecture note
    /// that private matches and matchmade ones differ only in who calls this.
    /// </remarks>
    public ServerMatch CreateMatch(
        IReadOnlyCollection<int> humanSeats,
        double? ticksPerSecond = null,
        IReadOnlyDictionary<int, string>? playFabIdBySeat = null,
        double? openingBuildWindowSeconds = null,
        string? matchId = null,
        Action<int, LTW.Simulation.Primitives.PlayerId>? onSeatBound = null,
        Action<int>? onSeatDisconnected = null)
    {
        // Null means "mint a fresh id" (every caller today). MP-07's GSDK path passes PlayFab's
        // own SessionId in instead, so a process hosting exactly one match under PlayFab
        // Multiplayer Servers reuses the id the client already generated — see
        // docs/MULTIPLAYER_ROLLOUT.md's MP-07.
        var resolvedMatchId = matchId ?? Guid.NewGuid().ToString("N");
        var options = LocalMatchOptions.Default;
        foreach (var seat in humanSeats)
        {
            // LocalMatchOptions.LocalPlayerId only ever names ONE seat as "the human" (see
            // docs/MULTIPLAYER_ROLLOUT.md's MP-01 "only one human seat can ever exist" gap) —
            // every OTHER human seat here is instead configured bot-DISABLED, which is enough for
            // the seat authority and the command methods to work correctly for it. Only the seat
            // TABLE's own Human/Bot/Empty label is wrong for seats 2+ until MP-01 grows real
            // multi-seat identity; nothing gameplay-relevant gates on that label.
            if (seat != options.LocalPlayerId.Value)
            {
                options = options.WithLane(seat, enabled: false);
            }
        }

        var match = new ServerMatch(
            resolvedMatchId,
            content,
            options,
            humanSeats,
            replayDirectory,
            ticksPerSecond ?? ServerMatch.DefaultTicksPerSecond,
            playFabIdBySeat,
            playFabAuthority,
            openingBuildWindowSeconds ?? 30,
            onSeatBound,
            onSeatDisconnected);
        matches[resolvedMatchId] = match;
        match.Start();
        return match;
    }

    public ServerMatch? Find(string matchId) => matches.TryGetValue(matchId, out var match) ? match : null;

    /// <summary>
    /// The one match this registry holds, or null if there is zero or more than one — used only by
    /// <see cref="HttpMatchHost"/>'s <c>current</c> join alias, itself only offered when
    /// <c>allowMatchCreation</c> is false (PlayFab Multiplayer Servers mode), which already
    /// guarantees exactly one match per process. See docs/MULTIPLAYER_ROLLOUT.md's MP-05: a
    /// queue-matched client learns PlayFab's own <c>MatchId</c> from <c>GetMatch</c>, which is not
    /// guaranteed to equal this registry's internal match id — "current" sidesteps needing that
    /// equivalence at all rather than assuming it.
    /// </summary>
    public ServerMatch? FindOnly() => matches.Count == 1 ? matches.Values.Single() : null;
}
