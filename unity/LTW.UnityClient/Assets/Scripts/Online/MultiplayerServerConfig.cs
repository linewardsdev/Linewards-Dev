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
        /// <summary>The uploaded build's guid, from PlayFab Game Manager. Not set until MP-07's Phase 5.</summary>
        public const string BuildId = "TODO-set-after-build-upload";

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
