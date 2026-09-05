#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// Creates a private match on <c>LTW.MatchServer</c> and joins it, using the signed-in PlayFab
    /// session (see <see cref="PlayFabSession"/>) to claim the seat — the same
    /// <c>playFabTicket=</c> join path <c>PlayFabJoinTests</c> proves server-side. There is no
    /// matchmaking yet (MP-05's remaining scope), so this always creates a fresh private match for
    /// exactly one human seat plus seven bots, matching <c>MatchRegistry.CreateMatch</c>'s own
    /// default shape.
    /// </summary>
    public static class OnlineMatchService
    {
        private const int HumanSeat = 1;

        /// <summary>
        /// <c>PlayerPrefs</c> key for the match a player is currently (or was, at last kill) in —
        /// see <see cref="PendingMatchId"/>. Survives an app kill, unlike everything else about a
        /// live match, which lives only in <see cref="MatchWireClient"/>/<c>UnitySimulationDriver</c>.
        /// </summary>
        private const string PendingMatchIdKey = "LTW.Online.PendingMatchId";

        /// <summary>
        /// The match a reconnect should target — set the moment a join succeeds (create or
        /// rejoin), cleared only by <see cref="ClearPendingMatch"/>. Deliberately survives a lost
        /// connection: the whole point is for it to still be there after an app kill, so a relaunch
        /// (or a live reconnect attempt, which never restarts the process at all) can find it.
        /// </summary>
        public static string? PendingMatchId
        {
            get
            {
                var stored = PlayerPrefs.GetString(PendingMatchIdKey, "");
                return string.IsNullOrEmpty(stored) ? null : stored;
            }
        }

        /// <summary>
        /// Forgets the pending match — called once a match is left deliberately
        /// (<c>UnitySimulationDriver.LeaveOnlineMatch</c>) or ends in a result, neither of which
        /// should leave behind something a later reconnect would try to rejoin.
        /// </summary>
        public static void ClearPendingMatch() => PlayerPrefs.DeleteKey(PendingMatchIdKey);

        private static void SavePendingMatch(string matchId)
        {
            PlayerPrefs.SetString(PendingMatchIdKey, matchId);
            PlayerPrefs.Save();
        }

        public static async Task<MatchWireClient?> CreateAndJoinAsync(Action<string> onFailure)
        {
            if (!PlayFabSession.IsSignedIn || PlayFabSession.PlayFabId is null || PlayFabSession.SessionTicket is null)
            {
                onFailure("Sign in with Google before starting an online match.");
                return null;
            }

            string matchId;
            try
            {
                matchId = await CreateMatchAsync(PlayFabSession.PlayFabId);
            }
            catch (Exception exception)
            {
                onFailure($"Could not create a match: {exception.Message}");
                return null;
            }

            return await JoinAsync(matchId, onFailure);
        }

        /// <summary>
        /// Rejoins a match this session already knows the id of — a network hole mid-match
        /// (<c>UnitySimulationDriver</c>'s own reconnect loop) or a fresh app launch that found
        /// <see cref="PendingMatchId"/> still set (<c>ShellScreenView</c>'s post-sign-in check).
        /// Skips match creation entirely: <c>ServerMatch.BindAsync</c> already accepts a verified
        /// PlayFab ticket for a seat in an EXISTING match with no special-casing needed there
        /// (found true rather than assumed, investigating this feature — it evicts whatever
        /// connection was already bound to the seat first, so a stale half-dead socket from before
        /// the drop cannot end up double-bound alongside the new one).
        /// </summary>
        public static async Task<MatchWireClient?> RejoinAsync(string matchId, Action<string> onFailure)
        {
            if (!PlayFabSession.IsSignedIn || PlayFabSession.PlayFabId is null || PlayFabSession.SessionTicket is null)
            {
                onFailure("Sign in with Google before rejoining an online match.");
                return null;
            }

            return await JoinAsync(matchId, onFailure);
        }

        /// <summary>
        /// How long to wait for the server's own acceptance (a <c>WelcomeMessage</c>) after the
        /// WebSocket handshake itself succeeds — see <see cref="JoinAsync"/>'s own remarks for why
        /// that handshake succeeding is not the same thing.
        /// </summary>
        private const double JoinAcceptanceTimeoutSeconds = 5;

        /// <summary>
        /// Found investigating a failed rejoin: <c>ConnectAsync</c> succeeding only means the
        /// WebSocket UPGRADE succeeded — <c>HttpMatchHost.HandleJoinAsync</c> accepts that upgrade
        /// FIRST and only afterward checks the token/PlayFab ticket
        /// (<c>ServerMatch.AcceptAsync</c>/<c>AcceptWithPlayFabAsync</c>), closing the socket with a
        /// policy violation if it does not match. A caller trusting <c>ConnectAsync</c> alone would
        /// treat that as a successful join and hand the caller a client that is already dying, only
        /// to have it surface a frame or two later as an ordinary disconnect — indistinguishable
        /// from a real network drop, and (for a rejoin) potentially retried against a match/seat
        /// that was never actually rejoined in the first place. Waits for the server's own
        /// <c>WelcomeMessage</c> — genuine acceptance — or for the connection to die first
        /// (rejection), rather than trusting the handshake alone.
        /// </summary>
        private static async Task<MatchWireClient?> JoinAsync(string matchId, Action<string> onFailure)
        {
            // A PlayFab session ticket is base64-shaped and contains '+' and '=' — an unencoded
            // '+' decodes server-side as a space and silently corrupts the ticket. See
            // docs/MULTIPLAYER_ROLLOUT.md's MP-05 "second real bug" for the incident this note
            // exists because of.
            var encodedTicket = UnityWebRequest.EscapeURL(PlayFabSession.SessionTicket);
            var joinUri = new Uri($"{MatchServerConfig.WebSocketBaseUrl}/matches/{matchId}/join?seat={HumanSeat}&playFabTicket={encodedTicket}");

            var client = new MatchWireClient(joinUri);
            try
            {
                await client.ConnectAsync();
            }
            catch (Exception exception)
            {
                onFailure($"Could not join the match: {exception.Message}");
                client.Dispose();
                return null;
            }

            var deadline = DateTime.UtcNow.AddSeconds(JoinAcceptanceTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                client.Pump();
                if (client.Welcome is not null)
                {
                    SavePendingMatch(matchId);
                    return client;
                }

                if (client.IsDisconnected)
                {
                    // Specifically this rejection, not the generic exception/timeout branches
                    // below: a closed-during-join is what a bad or expired PlayFab ticket looks
                    // like from here (ServerMatch.AcceptWithPlayFabAsync closing the socket with a
                    // policy violation), as opposed to a network blip or a genuinely gone match —
                    // see PlayFabSession.ForgetOnAuthFailure's own remarks for why only this
                    // specific case resets the restored session rather than every join failure.
                    PlayFabSession.ForgetOnAuthFailure();
                    onFailure("The server closed the connection while joining — the seat or match may no longer be valid.");
                    client.Dispose();
                    return null;
                }

                await Task.Delay(50);
            }

            onFailure("Timed out waiting for the server to accept the join.");
            client.Dispose();
            return null;
        }

        private static async Task<string> CreateMatchAsync(string playFabId)
        {
            var body = JsonConvert.SerializeObject(new
            {
                humanSeats = new[] { HumanSeat },
                playFabSeats = new Dictionary<int, string> { [HumanSeat] = playFabId },
            });

            using var request = new UnityWebRequest($"{MatchServerConfig.HttpBaseUrl}/matches", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"{request.responseCode}: {request.error}");
            }

            var response = JsonConvert.DeserializeObject<CreateMatchResponse>(request.downloadHandler.text);
            if (response?.matchId is null)
            {
                throw new InvalidOperationException("server response had no matchId");
            }

            return response.matchId;
        }

        private sealed class CreateMatchResponse
        {
            public string? matchId;
        }
    }
}
