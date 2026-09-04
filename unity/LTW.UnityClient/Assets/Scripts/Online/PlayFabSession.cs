#nullable enable

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// The signed-in player's identity for this process, once a login succeeds. A plain static
    /// holder rather than a MonoBehaviour singleton: nothing here needs a scene lifecycle, and the
    /// eventual online-match join (MP-06, not built yet) reads <see cref="SessionTicket"/> to prove
    /// a seat claim the same way <c>PlayFabSessionAuthority</c> already verifies server-side — see
    /// docs/MULTIPLAYER_ROLLOUT.md's MP-05.
    /// </summary>
    public static class PlayFabSession
    {
        public static bool IsSignedIn { get; private set; }

        public static string? PlayFabId { get; private set; }

        /// <summary>
        /// The PlayFab session ticket. If this is ever placed in a URL (the eventual join query
        /// string), it MUST be percent-encoded first — a real ticket contains '+' and '=' and an
        /// unencoded '+' decodes server-side as a space, silently corrupting it. See
        /// docs/MULTIPLAYER_ROLLOUT.md's MP-05 "second real bug" note.
        /// </summary>
        public static string? SessionTicket { get; private set; }

        public static void SetSignedIn(string playFabId, string sessionTicket)
        {
            PlayFabId = playFabId;
            SessionTicket = sessionTicket;
            IsSignedIn = true;
        }

        public static void Clear()
        {
            PlayFabId = null;
            SessionTicket = null;
            IsSignedIn = false;
        }
    }
}
