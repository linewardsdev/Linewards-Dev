#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.AuthenticationModels;
using PlayFab.MultiplayerModels;
using UnityEngine;
using UnityEngine.Networking;

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// Gets the local player into an online match and joins it, using the signed-in PlayFab
    /// session (see <see cref="PlayFabSession"/>) to claim the seat — the same
    /// <c>playFabTicket=</c> join path <c>PlayFabJoinTests</c> proves server-side. Under MPS
    /// (<see cref="UseMultiplayerServers"/>), <see cref="QueueForMatchAsync"/> opportunistically
    /// pools whoever else happens to be queuing at the same time (see its own remarks — this is
    /// deliberately not an invite mechanism) and falls back to a direct solo-vs-bots request when
    /// nobody else is around, matching <c>MatchRegistry.CreateMatch</c>'s own bot-fill shape either
    /// way. See docs/MULTIPLAYER_ROLLOUT.md's MP-05.
    /// </summary>
    /// <remarks>
    /// Two ways to get there, chosen by <see cref="UseMultiplayerServers"/>: the direct-connect
    /// path (default) hits a single known <see cref="MatchServerConfig"/> address directly — the
    /// fast local/LAN loop MP-06's whole rollout was tested against, kept alive deliberately so
    /// ordinary iteration never needs a build uploaded to PlayFab or Docker running. The
    /// PlayFab Multiplayer Servers (MPS) path instead asks PlayFab for a dynamically-allocated
    /// server (<see cref="RequestServerAsync"/>) — see docs/MULTIPLAYER_ROLLOUT.md's MP-07.
    /// </remarks>
    public static class OnlineMatchService
    {
        private const int HumanSeat = 1;

        /// <summary>
        /// Selects which of the two paths described in this class's own remarks
        /// <see cref="CreateAndJoinAsync"/> takes. Defaults false (direct-connect) — flips once
        /// MP-07's Phase 5 (a real PlayFab build uploaded and "game client access" enabled) exists
        /// to point at, per that phase's own checklist.
        /// </summary>
        public static bool UseMultiplayerServers = false;

        /// <summary>
        /// <c>PlayerPrefs</c> key for the match a player is currently (or was, at last kill) in —
        /// see <see cref="PendingMatchId"/>. Survives an app kill, unlike everything else about a
        /// live match, which lives only in <see cref="MatchWireClient"/>/<c>UnitySimulationDriver</c>.
        /// </summary>
        private const string PendingMatchIdKey = "LTW.Online.PendingMatchId";

        /// <summary>
        /// The resolved address of <see cref="PendingMatchId"/>'s match — under MPS this is the
        /// specific server PlayFab allocated for it (never re-resolved on rejoin: a match's server
        /// instance does not move during its life, so rejoining is just re-dialing this same
        /// address, no new <see cref="RequestServerAsync"/> call). Persisted alongside the match
        /// id so a fresh process (kill-and-relaunch) still has it — see
        /// docs/MULTIPLAYER_ROLLOUT.md's MP-07.
        /// </summary>
        private const string PendingMatchHostKey = "LTW.Online.PendingMatchHost";

        private const string PendingMatchPortKey = "LTW.Online.PendingMatchPort";

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
        public static void ClearPendingMatch()
        {
            PlayerPrefs.DeleteKey(PendingMatchIdKey);
            PlayerPrefs.DeleteKey(PendingMatchHostKey);
            PlayerPrefs.DeleteKey(PendingMatchPortKey);
        }

        private static void SavePendingMatch(string matchId, string host, int port)
        {
            PlayerPrefs.SetString(PendingMatchIdKey, matchId);
            PlayerPrefs.SetString(PendingMatchHostKey, host);
            PlayerPrefs.SetInt(PendingMatchPortKey, port);
            PlayerPrefs.Save();
        }

        public static async Task<MatchWireClient?> CreateAndJoinAsync(Action<string> onFailure)
        {
            if (!PlayFabSession.IsSignedIn || PlayFabSession.PlayFabId is null || PlayFabSession.SessionTicket is null)
            {
                onFailure("Sign in with Google before starting an online match.");
                return null;
            }

            if (UseMultiplayerServers)
            {
                (string EntityToken, PlayFab.AuthenticationModels.EntityKey Entity) identity;
                try
                {
                    identity = await GetEntityTokenAsync();
                }
                catch (Exception exception)
                {
                    onFailure($"Could not get an entity token: {exception.Message}");
                    return null;
                }

                return await QueueForMatchAsync(PlayFabSession.PlayFabId, identity.EntityToken, identity.Entity, onFailure);
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

            return await JoinAsync(matchId, MatchServerConfig.Host, MatchServerConfig.Port, onFailure);
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

            // Never re-resolves via RequestServerAsync: an MPS-allocated match's server instance
            // does not move during its life, so rejoining is just re-dialing the same address
            // already persisted at the original join — see PendingMatchHostKey's own remarks. The
            // MatchServerConfig fallback covers the direct-connect dev path (whose address never
            // varies) and a PendingMatchId saved before these host/port keys existed.
            var host = PlayerPrefs.GetString(PendingMatchHostKey, "");
            var port = PlayerPrefs.GetInt(PendingMatchPortKey, 0);
            if (string.IsNullOrEmpty(host) || port == 0)
            {
                host = MatchServerConfig.Host;
                port = MatchServerConfig.Port;
            }

            return await JoinAsync(matchId, host, port, onFailure);
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
        private static async Task<MatchWireClient?> JoinAsync(string matchId, string host, int port, Action<string> onFailure)
        {
            // A PlayFab session ticket is base64-shaped and contains '+' and '=' — an unencoded
            // '+' decodes server-side as a space and silently corrupts the ticket. See
            // docs/MULTIPLAYER_ROLLOUT.md's MP-05 "second real bug" for the incident this note
            // exists because of.
            var encodedTicket = UnityWebRequest.EscapeURL(PlayFabSession.SessionTicket);
            var joinUri = new Uri($"ws://{host}:{port}/matches/{matchId}/join?seat={HumanSeat}&playFabTicket={encodedTicket}");

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
                    SavePendingMatch(matchId, host, port);
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
                    // Gated on CloseStatus specifically (not just IsDisconnected): a network drop
                    // or a send timeout during this same window closes the socket for a reason
                    // that says nothing about the ticket's validity, and used to wipe a perfectly
                    // good session anyway. See docs/SECURITY_AUDIT_2026-09-05.md's M-C3.
                    if (client.CloseStatus == WebSocketCloseStatus.PolicyViolation)
                    {
                        PlayFabSession.ForgetOnAuthFailure();
                    }

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

        /// <summary>
        /// Every existing PlayFab call in this codebase (<c>PlayFabLoginService</c>) is
        /// callback-based, never awaited — this is the one place that bridges to <c>Task</c>, so
        /// <see cref="CreateAndJoinAsync"/>'s MPS branch can read the same way its direct-connect
        /// sibling does. Populates the vendored SDK's own entity-token cache as a side effect
        /// (<c>PlayFabHttp.OnPlayFabApiResult</c> stores it on <c>PlayFabSettings.staticPlayer</c>
        /// automatically), which is what lets <see cref="RequestServerAsync"/>'s and
        /// <see cref="QueueForMatchAsync"/>'s <c>AuthType.EntityToken</c> calls authenticate with no
        /// explicit header of our own. Also returns the caller's own <c>EntityKey</c> — needed as
        /// <c>CreateMatchmakingTicketRequest.Creator</c>, which the token alone doesn't supply.
        /// </summary>
        private static Task<(string EntityToken, PlayFab.AuthenticationModels.EntityKey Entity)> GetEntityTokenAsync()
        {
            var completion = new TaskCompletionSource<(string, PlayFab.AuthenticationModels.EntityKey)>();
            PlayFabAuthenticationAPI.GetEntityToken(
                new GetEntityTokenRequest(),
                result => completion.TrySetResult((result.EntityToken, result.Entity)),
                error => completion.TrySetException(new InvalidOperationException(error.GenerateErrorReport())));
            return completion.Task;
        }

        /// <summary>How often to poll PlayFab for allocation state after a RequestMultiplayerServer call that didn't come back Active immediately.</summary>
        private const double ServerAllocationPollIntervalSeconds = 0.5;

        /// <summary>
        /// Generous relative to PlayFab's own documented ~3s target from a warm standby pool —
        /// leaves real margin for a cold allocation rather than assuming the fast case.
        /// </summary>
        private const double ServerAllocationTimeoutSeconds = 10;

        /// <summary>
        /// Asks PlayFab Multiplayer Servers (MP-07) directly for a solo server instead of pooling
        /// through matchmaking — <see cref="QueueForMatchAsync"/>'s fallback when nobody else is
        /// queuing (MP-05's own bounded wait), and, when <see cref="UseMultiplayerServers"/> is
        /// used without matchmaking at all, a direct entry point in its own right. The
        /// <c>SessionCookie</c> carries the exact same <c>{humanSeats, playFabSeats}</c> shape
        /// <see cref="CreateMatchAsync"/>'s HTTP body does — the server-side
        /// <c>CreateMatchRequest</c> DTO deserializes it identically either way. Polls
        /// <c>GetMultiplayerServerDetails</c> until the allocation reaches <c>"Active"</c>, since a
        /// fresh allocation is not necessarily instant even from a warm pool.
        /// </summary>
        private static async Task<(string sessionId, string host, int port)> RequestServerAsync(string playFabId, string entityToken)
        {
            var sessionId = Guid.NewGuid().ToString();
            var sessionCookie = JsonConvert.SerializeObject(new
            {
                humanSeats = new[] { HumanSeat },
                playFabSeats = new Dictionary<int, string> { [HumanSeat] = playFabId },
            });

            var initial = await RequestMultiplayerServerAsync(new RequestMultiplayerServerRequest
            {
                BuildId = MultiplayerServerConfig.BuildId,
                PreferredRegions = MultiplayerServerConfig.PreferredRegions,
                SessionId = sessionId,
                SessionCookie = sessionCookie,
                InitialPlayers = new List<string> { playFabId },
            });

            var state = initial.State;
            var ipv4Address = initial.IPV4Address;
            var ports = initial.Ports;

            var deadline = DateTime.UtcNow.AddSeconds(ServerAllocationTimeoutSeconds);
            while (state != "Active" && DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(ServerAllocationPollIntervalSeconds));
                var details = await GetMultiplayerServerDetailsAsync(sessionId);
                state = details.State;
                ipv4Address = details.IPV4Address;
                ports = details.Ports;
            }

            if (state != "Active")
            {
                throw new TimeoutException($"server allocation for session {sessionId} did not reach Active in time (last state: {state})");
            }

            var port = ports?.FirstOrDefault(candidate => candidate.Name == MultiplayerServerConfig.PortName)?.Num
                ?? throw new InvalidOperationException($"allocated server had no port named '{MultiplayerServerConfig.PortName}'");

            return (sessionId, ipv4Address, port);
        }

        /// <summary>
        /// How long a queued ticket waits for another real player before falling back to
        /// <see cref="RequestServerAsync"/>'s direct solo-vs-bots path — see
        /// docs/MULTIPLAYER_ROLLOUT.md's MP-05 for why this fallback is mandatory, not optional:
        /// PlayFab's matchmaker can never match a ticket by itself (a queue's minimum match size is
        /// always &gt;= 2, confirmed directly from Microsoft's own docs), so a ticket with no
        /// company simply cancels after this — there is no shorter road to "solo, against bots"
        /// through the matchmaker itself.
        /// </summary>
        private const int MatchmakingGiveUpAfterSeconds = 25;

        private const double MatchmakingPollIntervalSeconds = 0.5;

        /// <summary>
        /// Queues for a match. If another real player is also queuing, PlayFab pools them into the
        /// same server (bots filling whatever seats are left) — deliberately opportunistic, not an
        /// invite: nothing here lets a player choose who they land with, per this project's own
        /// explicit direction. If nobody else shows up within
        /// <see cref="MatchmakingGiveUpAfterSeconds"/>, the ticket cancels and this falls back to
        /// <see cref="RequestServerAsync"/>'s existing, already-proven direct solo-vs-bots path —
        /// same as if matchmaking had never been attempted.
        /// </summary>
        private static async Task<MatchWireClient?> QueueForMatchAsync(string playFabId, string entityToken, PlayFab.AuthenticationModels.EntityKey entity, Action<string> onFailure)
        {
            string ticketId;
            try
            {
                ticketId = await CreateMatchmakingTicketAsync(entity);
            }
            catch (Exception exception)
            {
                onFailure($"Could not create a matchmaking ticket: {exception.Message}");
                return null;
            }

            // A small safety margin beyond the ticket's own server-side GiveUpAfterSeconds timer —
            // PlayFab is what actually transitions the ticket to Canceled; this just guards against
            // never observing that transition for some unexpected reason, rather than polling forever.
            var deadline = DateTime.UtcNow.AddSeconds(MatchmakingGiveUpAfterSeconds + 10);
            string status;
            string? matchId;
            do
            {
                await Task.Delay(TimeSpan.FromSeconds(MatchmakingPollIntervalSeconds));
                GetMatchmakingTicketResult ticket;
                try
                {
                    ticket = await GetMatchmakingTicketAsync(ticketId);
                }
                catch (Exception exception)
                {
                    onFailure($"Could not check matchmaking ticket status: {exception.Message}");
                    return null;
                }

                status = ticket.Status;
                matchId = ticket.MatchId;
            }
            while (status != "Matched" && status != "Canceled" && DateTime.UtcNow < deadline);

            if (status == "Matched" && matchId is not null)
            {
                GetMatchResult match;
                try
                {
                    match = await GetMatchAsync(matchId);
                }
                catch (Exception exception)
                {
                    onFailure($"Could not get match details: {exception.Message}");
                    return null;
                }

                var matchedPort = match.ServerDetails.Ports?.FirstOrDefault(candidate => candidate.Name == MultiplayerServerConfig.PortName)?.Num
                    ?? throw new InvalidOperationException($"matched server had no port named '{MultiplayerServerConfig.PortName}'");

                // "current", not a derived id: a queue-auto-allocated server's own internal match id
                // is not guaranteed to equal PlayFab's own MatchId here — HttpMatchHost's "current"
                // join alias exists specifically to sidestep needing that equivalence. See
                // docs/MULTIPLAYER_ROLLOUT.md's MP-05.
                return await JoinAsync("current", match.ServerDetails.IPV4Address, matchedPort, onFailure);
            }

            // Nobody else was queuing within the bounded wait — the common case at this
            // population. Falls back to the same direct-request flow that ran unconditionally
            // before matchmaking existed; a solo player sees no functional difference.
            (string sessionId, string host, int port) allocation;
            try
            {
                allocation = await RequestServerAsync(playFabId, entityToken);
            }
            catch (Exception exception)
            {
                onFailure($"Could not request a server: {exception.Message}");
                return null;
            }

            return await JoinAsync(allocation.sessionId, allocation.host, allocation.port, onFailure);
        }

        private static Task<string> CreateMatchmakingTicketAsync(PlayFab.AuthenticationModels.EntityKey entity)
        {
            var completion = new TaskCompletionSource<string>();
            PlayFabMultiplayerAPI.CreateMatchmakingTicket(
                new CreateMatchmakingTicketRequest
                {
                    QueueName = MultiplayerServerConfig.MatchmakingQueueName,
                    GiveUpAfterSeconds = MatchmakingGiveUpAfterSeconds,
                    Creator = new MatchmakingPlayer
                    {
                        // PlayFab.MultiplayerModels.EntityKey, a distinct type from
                        // PlayFab.AuthenticationModels.EntityKey despite the identical shape — the
                        // vendored SDK duplicates this model per API category rather than sharing one.
                        Entity = new PlayFab.MultiplayerModels.EntityKey { Id = entity.Id, Type = entity.Type },
                    },
                },
                result => completion.TrySetResult(result.TicketId),
                error => completion.TrySetException(new InvalidOperationException(error.GenerateErrorReport())));
            return completion.Task;
        }

        private static Task<GetMatchmakingTicketResult> GetMatchmakingTicketAsync(string ticketId)
        {
            var completion = new TaskCompletionSource<GetMatchmakingTicketResult>();
            PlayFabMultiplayerAPI.GetMatchmakingTicket(
                new GetMatchmakingTicketRequest { TicketId = ticketId, QueueName = MultiplayerServerConfig.MatchmakingQueueName },
                result => completion.TrySetResult(result),
                error => completion.TrySetException(new InvalidOperationException(error.GenerateErrorReport())));
            return completion.Task;
        }

        private static Task<GetMatchResult> GetMatchAsync(string matchId)
        {
            var completion = new TaskCompletionSource<GetMatchResult>();
            PlayFabMultiplayerAPI.GetMatch(
                new GetMatchRequest { MatchId = matchId, QueueName = MultiplayerServerConfig.MatchmakingQueueName },
                result => completion.TrySetResult(result),
                error => completion.TrySetException(new InvalidOperationException(error.GenerateErrorReport())));
            return completion.Task;
        }

        private static Task<RequestMultiplayerServerResponse> RequestMultiplayerServerAsync(RequestMultiplayerServerRequest request)
        {
            var completion = new TaskCompletionSource<RequestMultiplayerServerResponse>();
            PlayFabMultiplayerAPI.RequestMultiplayerServer(
                request,
                result => completion.TrySetResult(result),
                error => completion.TrySetException(new InvalidOperationException(error.GenerateErrorReport())));
            return completion.Task;
        }

        private static Task<GetMultiplayerServerDetailsResponse> GetMultiplayerServerDetailsAsync(string sessionId)
        {
            var completion = new TaskCompletionSource<GetMultiplayerServerDetailsResponse>();
            PlayFabMultiplayerAPI.GetMultiplayerServerDetails(
                new GetMultiplayerServerDetailsRequest { SessionId = sessionId },
                result => completion.TrySetResult(result),
                error => completion.TrySetException(new InvalidOperationException(error.GenerateErrorReport())));
            return completion.Task;
        }

        private sealed class CreateMatchResponse
        {
            public string? matchId;
        }
    }
}
