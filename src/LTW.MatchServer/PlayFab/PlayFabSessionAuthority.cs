using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LTW.MatchServer.PlayFab;

/// <summary>
/// Verifies a PlayFab session ticket by calling PlayFab's own Server API, so a connecting client
/// cannot simply claim a PlayFabId — MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-05, built against
/// PlayFab's documented `AuthenticateSessionTicket` endpoint rather than the PlayFab NuGet SDK:
/// this is one REST call, and `MVP_DEPENDENCIES.md` rule 4 ("prefer small, well-supported
/// dependencies over framework stacks") is the same reasoning MP-04 already applied to
/// `System.Net.WebSockets` over a web framework.
/// </summary>
/// <remarks>
/// Request and response shapes are taken directly from PlayFab's own REST reference
/// (learn.microsoft.com/en-us/rest/api/playfab/server/authentication/authenticate-session-ticket),
/// fetched 2026-09-03 rather than assumed from memory, and <see cref="PlayFabSessionAuthorityTests"/>
/// exercises this class against a fake handler returning exactly that shape. What that test
/// CANNOT prove is that a real PlayFab title responds the same way — see
/// docs/MULTIPLAYER_ROLLOUT.md's MP-05 "Before any of this can start" for why nothing here has
/// been run against a live title yet.
/// </remarks>
public sealed class PlayFabSessionAuthority
{
    private readonly HttpClient http;
    private readonly string titleId;
    private readonly string secretKey;
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    public PlayFabSessionAuthority(HttpClient http, string titleId, string secretKey)
    {
        this.http = http;
        this.titleId = titleId;
        this.secretKey = secretKey;
    }

    /// <summary>
    /// Returns the verified PlayFabId for <paramref name="sessionTicket"/>, or null if the ticket
    /// is invalid, expired, or PlayFab could not be reached at all. Deliberately never throws for
    /// any of those cases — a join handler should treat "PlayFab is down" exactly like "bad
    /// ticket": refuse this one join, not crash the match host serving everyone else.
    /// </summary>
    public async Task<string?> AuthenticateAsync(string sessionTicket, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{titleId}.playfabapi.com/Server/AuthenticateSessionTicket");
        request.Headers.Add("X-SecretKey", secretKey);
        request.Content = JsonContent.Create(new AuthenticateSessionTicketRequest(sessionTicket), options: json);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout, not a caller-requested cancellation — PlayFab took too long, treated the
            // same as unreachable.
            return null;
        }

        using (response)
        {
            // PlayFab reports both a real rejection (InvalidSessionTicket, error code 1100) and
            // most transport problems as a 4xx/5xx with an ApiErrorWrapper body — this class does
            // not need to distinguish WHY the ticket did not check out, only that it did not.
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            AuthenticateSessionTicketEnvelope? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<AuthenticateSessionTicketEnvelope>(json, cancellationToken);
            }
            catch (JsonException)
            {
                return null;
            }

            if (body?.Data is null || body.Data.IsSessionTicketExpired)
            {
                return null;
            }

            // Defense-in-depth, not the primary enforcement: PlayFab's own ban system already
            // invalidates a banned player's session tickets outright (Game Manager's Players →
            // Bans → Add Ban flow — "any existing player authentication tokens are invalidated
            // and future authentication attempts... will be rejected", confirmed from Microsoft's
            // own docs), which IsSessionTicketExpired above should already catch. This checks the
            // ban flag PlayFab includes in the SAME response body directly, at zero extra API
            // calls, rather than trusting that invalidation propagates with no gap. See
            // docs/MULTIPLAYER_ROLLOUT.md's MP-07 abuse-handling note.
            if (body.Data.UserInfo?.TitleInfo?.IsBanned == true)
            {
                return null;
            }

            var playFabId = body.Data.UserInfo?.PlayFabId;
            return string.IsNullOrEmpty(playFabId) ? null : playFabId;
        }
    }

    private sealed record AuthenticateSessionTicketRequest([property: JsonPropertyName("SessionTicket")] string SessionTicket);

    private sealed class AuthenticateSessionTicketEnvelope
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("data")]
        public AuthenticateSessionTicketData? Data { get; set; }
    }

    private sealed class AuthenticateSessionTicketData
    {
        [JsonPropertyName("IsSessionTicketExpired")]
        public bool IsSessionTicketExpired { get; set; }

        [JsonPropertyName("UserInfo")]
        public UserInfoDto? UserInfo { get; set; }
    }

    private sealed class UserInfoDto
    {
        [JsonPropertyName("PlayFabId")]
        public string? PlayFabId { get; set; }

        [JsonPropertyName("TitleInfo")]
        public UserTitleInfoDto? TitleInfo { get; set; }
    }

    private sealed class UserTitleInfoDto
    {
        // Lowercase in PlayFab's own documented response shape — every sibling field on this
        // same object (AvatarUrl, Created, DisplayName, ...) is PascalCase; this one specifically
        // is not, per learn.microsoft.com/en-us/rest/api/playfab/server/authentication/authenticate-session-ticket's
        // UserTitleInfo table (fetched 2026-09-07).
        [JsonPropertyName("isBanned")]
        public bool IsBanned { get; set; }
    }
}
