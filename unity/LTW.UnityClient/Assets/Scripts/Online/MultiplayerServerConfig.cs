#nullable enable

using System.Collections.Generic;

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// Values that only exist once a build is uploaded in PlayFab Game Manager
    /// (docs/MULTIPLAYER_ROLLOUT.md's MP-07, Phase 5) — placeholders here until then, mirroring
    /// how <see cref="PlayFabConfig.TitleId"/> is itself a portal-sourced constant, not a secret.
    /// </summary>
    public static class MultiplayerServerConfig
    {
        /// <summary>
        /// The uploaded build's guid, from PlayFab Game Manager. Set 2026-09-11 to the build on
        /// image tag <c>mps-20260911f</c>, created by <c>tools/playfab/create_build.py</c> — the
        /// first build that provably carries the title secret (a PlayFab game secret, verified on
        /// the build via GetBuild) it needs to verify a join. Every build before it (<c>faee9e3e</c>,
        /// <c>4dbf4418</c>, <c>36cfe0aa</c>, <c>b1f71d89</c>, <c>6c5906bf</c>, <c>4a31e6b5</c>)
        /// hid a real defect: a case-sensitive port-name lookup, an arm64 image on x86-64 VMs, a
        /// non-root user unable to write PlayFab's log mount, and — for the last four — no secret
        /// reaching the container because Game Manager's form cannot set one. See
        /// MULTIPLAYER_ROLLOUT.md's MP-07 Phase 5.
        /// </summary>
        public const string BuildId = "b922cefb-e900-49fa-84d4-f3d8cf40999a";

        /// <summary>
        /// Tried in order until one has a server available — see
        /// <c>RequestMultiplayerServerRequest.PreferredRegions</c>'s own remarks.
        /// </summary>
        public static readonly List<string> PreferredRegions = new() { "EastUs" };

        /// <summary>
        /// The port NAME (not number — PlayFab assigns the actual number dynamically per
        /// allocation) configured for the uploaded build in Game Manager. Confirmed empirically
        /// against a real build, not guessed — see MP-07's Phase 2/5.
        /// </summary>
        public const string PortName = "game";

        /// <summary>
        /// The matchmaking queue created via the PlayFab API (MP-05 Phase 2,
        /// <c>tools/playfab/create_queue.py</c>, 2026-09-11): <c>MinMatchSize 2</c>,
        /// <c>MaxMatchSize 8</c>, <c>ServerAllocationEnabled</c> pointed at <see cref="BuildId"/>
        /// above. Created through the API, not Game Manager's form, for the same reason
        /// <c>create_build.py</c> exists — <c>SetMatchmakingQueue</c> is not exposed as a portal
        /// form action at all. See docs/MULTIPLAYER_ROLLOUT.md's MP-05.
        /// </summary>
        public const string MatchmakingQueueName = "ltw-quickmatch";

        /// <summary>
        /// The single Azure region this queue's <c>RegionSelectionRule</c> and every matchmaking
        /// ticket's <c>Latencies</c> attribute name — must match <see cref="PreferredRegions"/>
        /// and the build's own region (both <c>EastUs</c> today). A queue with
        /// <c>ServerAllocationEnabled</c> requires a <c>RegionSelectionRule</c>, and PlayFab
        /// rejects a ticket with <c>MatchmakingAttributeInvalid</c> if it carries no matching
        /// latency measurement — see <c>OnlineMatchService.CreateMatchmakingTicketAsync</c>, which
        /// sends this as a synthetic value rather than a real QoS beacon measurement (Party's beacon
        /// SDK is not integrated). Harmless with exactly one region: there is nowhere else a server
        /// could be allocated, so the number only has to clear <see cref="RegionSelectionRuleMaxLatencyMs"/>,
        /// never actually pick between regions. Revisit — a real measurement, not a placeholder —
        /// before a second region is ever added.
        /// </summary>
        public const string RegionSelectionRuleRegion = "EastUs";

        /// <summary>See <see cref="RegionSelectionRuleRegion"/>. Comfortably under the queue's
        /// configured <c>MaxLatency</c> (500ms) with room to spare.</summary>
        public const int SyntheticRegionLatencyMs = 10;
    }
}
