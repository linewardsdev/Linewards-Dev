#nullable enable

using UnityEngine;

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
        private const string PlayFabIdKey = "LTW.Online.PlayFabId";
        private const string SessionTicketKey = "LTW.Online.SessionTicket";

        public static bool IsSignedIn { get; private set; }

        public static string? PlayFabId { get; private set; }

        /// <summary>
        /// The PlayFab session ticket. If this is ever placed in a URL (the eventual join query
        /// string), it MUST be percent-encoded first — a real ticket contains '+' and '=' and an
        /// unencoded '+' decodes server-side as a space, silently corrupting it. See
        /// docs/MULTIPLAYER_ROLLOUT.md's MP-05 "second real bug" note.
        /// </summary>
        public static string? SessionTicket { get; private set; }

        /// <summary>
        /// Signs in, and persists to <c>PlayerPrefs</c> so <see cref="TryRestore"/> can bring this
        /// same identity back after a kill — see its own remarks for why a real session ticket
        /// rather than Google's own native session is what actually needs to survive one.
        /// </summary>
        public static void SetSignedIn(string playFabId, string sessionTicket)
        {
            PlayFabId = playFabId;
            SessionTicket = sessionTicket;
            IsSignedIn = true;

            PlayerPrefs.SetString(PlayFabIdKey, playFabId);
            PlayerPrefs.SetString(SessionTicketKey, sessionTicket);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Restores a previously persisted identity with no network call and no Google
        /// interaction — called once at Title startup (<c>ShellScreenView</c>'s own remarks).
        /// </summary>
        /// <remarks>
        /// Found needed live testing kill-and-relaunch reconnect: Google Sign-In's native bridge
        /// has no session of its own restored here — what actually matters to this game is the
        /// PlayFab session ticket, the one thing every online operation (create/join/rejoin a
        /// match) already authenticates with, so persisting THAT directly is both simpler and more
        /// direct than restoring Google's own SDK session and re-deriving a ticket from it every
        /// launch. Deliberately optimistic: a restored ticket may have expired since it was saved,
        /// and this makes no server call to check — the same PlayFab ticket verification every
        /// online operation already does server-side is what discovers that, the first time one is
        /// actually attempted, exactly like a network failure would surface for any other reason.
        /// <see cref="ForgetOnAuthFailure"/> is the other half: whatever discovers the rejection is
        /// what clears this back out, since nothing here can tell "expired" from "never tried" on
        /// its own.
        /// </remarks>
        public static bool TryRestore()
        {
            if (IsSignedIn)
            {
                return true;
            }

            var playFabId = PlayerPrefs.GetString(PlayFabIdKey, "");
            var sessionTicket = PlayerPrefs.GetString(SessionTicketKey, "");
            if (string.IsNullOrEmpty(playFabId) || string.IsNullOrEmpty(sessionTicket))
            {
                return false;
            }

            PlayFabId = playFabId;
            SessionTicket = sessionTicket;
            IsSignedIn = true;
            return true;
        }

        /// <summary>
        /// Clears the in-memory AND persisted identity — a real sign-out, unlike a match ending or
        /// being left, which have nothing to do with who is signed in.
        /// </summary>
        public static void Clear()
        {
            PlayFabId = null;
            SessionTicket = null;
            IsSignedIn = false;

            PlayerPrefs.DeleteKey(PlayFabIdKey);
            PlayerPrefs.DeleteKey(SessionTicketKey);
        }

        /// <summary>
        /// Call when a PlayFab-ticket-authenticated operation (create/join/rejoin a match) fails in
        /// a way that could mean the restored ticket has expired — there is no cheaper way to tell
        /// "expired" from "never valid to begin with" apart from a real attempt failing, so this is
        /// the reset half of the optimistic restore <see cref="TryRestore"/> does. Resets to the
        /// normal "SIGN IN WITH GOOGLE" state rather than leaving the player stuck behind a session
        /// that looks signed in but can never successfully do anything.
        /// </summary>
        public static void ForgetOnAuthFailure() => Clear();
    }
}
