using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LTW.MatchServer;
using LTW.MatchServer.PlayFab;

namespace LTW.MatchServer.Tests.PlayFab;

/// <summary>
/// The join-time half of MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-05: a seat reserved for a
/// specific PlayFabId can only be claimed by a session ticket PlayFab itself verifies as that
/// PlayFabId — not by any valid ticket, and not by a claim in the join request. Uses a fake
/// PlayFab response the same way <see cref="PlayFabSessionAuthorityTests"/> does; see that class
/// and docs/MULTIPLAYER_ROLLOUT.md's MP-05 for what a fake response can and cannot prove.
/// </summary>
public sealed class PlayFabJoinTests : IAsyncLifetime
{
    /// <summary>Verifies any ticket of the form "ticket-for-{playFabId}" as that PlayFabId; anything else is invalid.</summary>
    private sealed class FakePlayFabHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            var ticket = document.RootElement.GetProperty("SessionTicket").GetString() ?? "";

            if (!ticket.StartsWith("ticket-for-", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"code":400,"status":"BadRequest","error":"InvalidSessionTicket","errorCode":1100,"errorMessage":"Invalid session ticket"}""", Encoding.UTF8, "application/json"),
                };
            }

            var playFabId = ticket["ticket-for-".Length..];
            var responseJson = "{\"code\":200,\"status\":\"OK\",\"data\":{\"IsSessionTicketExpired\":false,\"UserInfo\":{\"PlayFabId\":\"" + playFabId + "\"}}}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private string replayDirectory = "";
    private HttpMatchHost host = null!;
    private MatchRegistry registry = null!;
    private int port;

    public Task InitializeAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        replayDirectory = Path.Combine(Path.GetTempPath(), "ltw-playfab-join-tests-" + Guid.NewGuid().ToString("N"));
        var authority = new PlayFabSessionAuthority(new HttpClient(new FakePlayFabHandler()), "TESTID", "fake-secret");
        registry = new MatchRegistry(replayDirectory, authority);
        host = new HttpMatchHost(registry, $"http://localhost:{port}/");
        host.Start();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        host.Stop();
        try
        {
            if (Directory.Exists(replayDirectory))
            {
                Directory.Delete(replayDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }

        return Task.CompletedTask;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<string> CreateMatchWithPlayFabSeatAsync(int seat, string playFabId)
    {
        using var client = new HttpClient();
        var body = JsonSerializer.Serialize(new { humanSeats = new[] { seat }, playFabSeats = new Dictionary<int, string> { [seat] = playFabId } }, Json);
        var response = await client.PostAsync($"http://localhost:{port}/matches", new StringContent(body, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("matchId").GetString()!;
    }

    [Fact]
    public async Task A_matching_verified_playfab_id_claims_the_reserved_seat()
    {
        var matchId = await CreateMatchWithPlayFabSeatAsync(3, "PLAYER_ABC");

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://localhost:{port}/matches/{matchId}/join?seat=3&playFabTicket=ticket-for-PLAYER_ABC"), CancellationToken.None);

        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        var message = JsonSerializer.Deserialize<JsonElement>(buffer.AsSpan(0, result.Count));

        Assert.Equal("welcome", message.GetProperty("type").GetString());
        Assert.Equal(3, message.GetProperty("seat").GetInt32());
    }

    [Fact]
    public async Task A_verified_but_different_playfab_id_cannot_claim_the_seat()
    {
        var matchId = await CreateMatchWithPlayFabSeatAsync(3, "PLAYER_ABC");

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://localhost:{port}/matches/{matchId}/join?seat=3&playFabTicket=ticket-for-PLAYER_XYZ"), CancellationToken.None);

        var buffer = new byte[16];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
    }

    [Fact]
    public async Task A_ticket_playfab_itself_rejects_cannot_claim_the_seat()
    {
        var matchId = await CreateMatchWithPlayFabSeatAsync(3, "PLAYER_ABC");

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://localhost:{port}/matches/{matchId}/join?seat=3&playFabTicket=not-a-real-ticket"), CancellationToken.None);

        var buffer = new byte[16];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
    }

    [Fact]
    public async Task A_seat_not_reserved_for_playfab_refuses_a_playfab_ticket_even_a_valid_one()
    {
        // Seat 1 is the human seat here, reserved by nothing (no playFabSeats entry) — it still
        // has an ordinary join token. A PlayFab ticket, even a genuinely valid one, is simply the
        // wrong kind of proof for a seat that was never offered that way.
        using var client = new HttpClient();
        var body = JsonSerializer.Serialize(new { humanSeats = new[] { 1 } }, Json);
        var response = await client.PostAsync($"http://localhost:{port}/matches", new StringContent(body, Encoding.UTF8, "application/json"));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = payload.GetProperty("matchId").GetString();

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://localhost:{port}/matches/{matchId}/join?seat=1&playFabTicket=ticket-for-PLAYER_ABC"), CancellationToken.None);

        var buffer = new byte[16];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
    }
}
