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

            return client;
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
