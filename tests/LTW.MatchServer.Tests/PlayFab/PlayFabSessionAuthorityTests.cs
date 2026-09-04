using System.Linq;
using System.Net;
using System.Text;
using LTW.MatchServer.PlayFab;

namespace LTW.MatchServer.Tests.PlayFab;

/// <summary>
/// Exercises <see cref="PlayFabSessionAuthority"/> against PlayFab's own documented request and
/// response shapes (learn.microsoft.com/en-us/rest/api/playfab/server/authentication/authenticate-session-ticket,
/// fetched 2026-09-03) via a fake handler — the piece of MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-05
/// buildable without a real PlayFab Title ID and Secret Key. See
/// docs/MULTIPLAYER_ROLLOUT.md's MP-05 for what this does and does not prove.
/// </summary>
public sealed class PlayFabSessionAuthorityTests
{
    /// <summary>Captures the outgoing request and replays a canned response, exactly like the real endpoint would for the same call.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public Func<HttpResponseMessage> Respond { get; set; } = () => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return Respond();
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task Builds_the_documented_request_shape()
    {
        var handler = new FakeHandler
        {
            Respond = () => JsonResponse(HttpStatusCode.OK, """{"code":200,"status":"OK","data":{"IsSessionTicketExpired":false,"UserInfo":{"PlayFabId":"ABC123"}}}"""),
        };
        var authority = new PlayFabSessionAuthority(new HttpClient(handler), "ABCDE", "the-secret-key");

        await authority.AuthenticateAsync("a-session-ticket");

        // Uri.ToString() lowercases the host per RFC 3986 (hostnames are case-insensitive) — that
        // is Uri behaving correctly, not this class getting the title ID wrong.
        Assert.Equal("https://ABCDE.playfabapi.com/Server/AuthenticateSessionTicket", handler.LastRequest!.RequestUri!.ToString(), ignoreCase: true);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("the-secret-key", handler.LastRequest.Headers.GetValues("X-SecretKey").Single());
        // Case-sensitive: PlayFab's real API expects the exact key "SessionTicket" (see the class's
        // own remarks) — a case-insensitive check here previously let a camelCase-serialized body
        // ("sessionTicket") pass silently, which would have been rejected by a real PlayFab title.
        Assert.Contains("\"SessionTicket\":\"a-session-ticket\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_valid_ticket_returns_the_playfab_id()
    {
        var handler = new FakeHandler
        {
            Respond = () => JsonResponse(HttpStatusCode.OK, """{"code":200,"status":"OK","data":{"IsSessionTicketExpired":false,"UserInfo":{"PlayFabId":"ABC123","Username":"whoever"}}}"""),
        };
        var authority = new PlayFabSessionAuthority(new HttpClient(handler), "ABCDE", "secret");

        var result = await authority.AuthenticateAsync("a-session-ticket");

        Assert.Equal("ABC123", result);
    }

    [Fact]
    public async Task An_expired_ticket_is_rejected_even_though_playfab_returned_200()
    {
        // PlayFab's own documented behaviour: an expired ticket is still a 200 with
        // IsSessionTicketExpired: true, not a 4xx — status-code-only checking would wrongly accept it.
        var handler = new FakeHandler
        {
            Respond = () => JsonResponse(HttpStatusCode.OK, """{"code":200,"status":"OK","data":{"IsSessionTicketExpired":true,"UserInfo":{"PlayFabId":"ABC123"}}}"""),
        };
        var authority = new PlayFabSessionAuthority(new HttpClient(handler), "ABCDE", "secret");

        var result = await authority.AuthenticateAsync("a-session-ticket");

        Assert.Null(result);
    }

    [Fact]
    public async Task An_invalid_ticket_reported_as_an_error_wrapper_is_rejected()
    {
        // PlayFab's documented ApiErrorWrapper shape for a 400, error code 1100 (InvalidSessionTicket).
        var handler = new FakeHandler
        {
            Respond = () => JsonResponse(HttpStatusCode.BadRequest, """{"code":400,"status":"BadRequest","error":"InvalidSessionTicket","errorCode":1100,"errorMessage":"Invalid session ticket"}"""),
        };
        var authority = new PlayFabSessionAuthority(new HttpClient(handler), "ABCDE", "secret");

        var result = await authority.AuthenticateAsync("not-a-real-ticket");

        Assert.Null(result);
    }

    [Fact]
    public async Task An_unreachable_playfab_is_rejected_not_thrown()
    {
        var handler = new FakeHandler { Respond = () => throw new HttpRequestException("simulated network failure") };
        var authority = new PlayFabSessionAuthority(new HttpClient(handler), "ABCDE", "secret");

        var result = await authority.AuthenticateAsync("a-session-ticket");

        Assert.Null(result);
    }

    [Fact]
    public async Task Malformed_json_is_rejected_not_thrown()
    {
        var handler = new FakeHandler { Respond = () => JsonResponse(HttpStatusCode.OK, "{ this is not json") };
        var authority = new PlayFabSessionAuthority(new HttpClient(handler), "ABCDE", "secret");

        var result = await authority.AuthenticateAsync("a-session-ticket");

        Assert.Null(result);
    }
}
