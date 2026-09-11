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
        /// The matchmaking queue created in PlayFab Game Manager (MP-05's Phase 2) — must have
        /// <c>ServerAllocationEnabled</c> pointed at the same <see cref="BuildId"/> above. Not set
        /// until that portal step is done. See docs/MULTIPLAYER_ROLLOUT.md's MP-05.
        /// </summary>
        public const string MatchmakingQueueName = "TODO-set-after-queue-creation";
    }
}
