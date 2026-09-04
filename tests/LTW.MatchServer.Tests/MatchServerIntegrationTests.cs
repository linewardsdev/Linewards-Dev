using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LTW.MatchServer;
using LTW.Simulation.Bridge;

namespace LTW.MatchServer.Tests;

/// <summary>
/// Real end-to-end proof for MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-04: an actual
/// <see cref="HttpListener"/>, actual <see cref="ClientWebSocket"/> connections, over real (if
/// loopback) sockets. See docs/MULTIPLAYER_ROLLOUT.md's MP-04 section for what this proves and
/// what it explicitly does not (two real devices, a regional container, measured bandwidth —
/// none of which this environment can produce).
/// </summary>
public sealed class MatchServerIntegrationTests : IAsyncLifetime
{
    private readonly Xunit.Abstractions.ITestOutputHelper output;
    public MatchServerIntegrationTests(Xunit.Abstractions.ITestOutputHelper output) => this.output = output;

    private string replayDirectory = "";
    private HttpMatchHost host = null!;
    private MatchRegistry registry = null!;
    private int port;

    public Task InitializeAsync()
    {
        port = FindFreePort();
        replayDirectory = Path.Combine(Path.GetTempPath(), "ltw-matchserver-tests", Guid.NewGuid().ToString("N"));
        registry = new MatchRegistry(replayDirectory);
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

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<(string matchId, Dictionary<string, string> tokens)> CreateMatchAsync(int[] humanSeats, double? ticksPerSecond = null)
    {
        using var client = new HttpClient();
        var body = JsonSerializer.Serialize(new { humanSeats, ticksPerSecond }, Json);
        var response = await client.PostAsync(
            $"http://localhost:{port}/matches",
            new StringContent(body, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = payload.GetProperty("matchId").GetString()!;
        var tokens = payload.GetProperty("tokens").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!);
        return (matchId, tokens);
    }

    private async Task<ClientWebSocket> JoinAsync(string matchId, int seat, string token)
    {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://localhost:{port}/matches/{matchId}/join?seat={seat}&token={token}"), CancellationToken.None);
        return socket;
    }

    private static async Task<JsonElement> ReceiveAsync(ClientWebSocket socket, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        var buffer = new byte[16384];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cts.Token);
            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return JsonSerializer.Deserialize<JsonElement>(message.ToArray());
    }

    private static async Task<JsonElement> ReceiveOfTypeAsync(ClientWebSocket socket, string type, TimeSpan? overallTimeout = null)
    {
        using var cts = new CancellationTokenSource(overallTimeout ?? TimeSpan.FromSeconds(10));
        while (!cts.IsCancellationRequested)
        {
            var message = await ReceiveAsync(socket, TimeSpan.FromSeconds(5));
            if (message.TryGetProperty("type", out var typeProperty) && typeProperty.GetString() == type)
            {
                return message;
            }
        }

        throw new TimeoutException($"never received a '{type}' message");
    }

    private static async Task SendAsync(ClientWebSocket socket, string json)
    {
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }

    [Fact]
    public async Task Two_humans_and_six_bots_complete_a_private_match_with_no_matchmaking()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1, 2 });
        Assert.Equal(2, tokens.Count);

        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        using var seat2 = await JoinAsync(matchId, 2, tokens["2"]);

        var welcome1 = await ReceiveOfTypeAsync(seat1, "welcome");
        Assert.Equal(1, welcome1.GetProperty("seat").GetInt32());
        var welcome2 = await ReceiveOfTypeAsync(seat2, "welcome");
        Assert.Equal(2, welcome2.GetProperty("seat").GetInt32());

        await SendAsync(seat1, """{"type":"placeTower","id":"a","laneId":1,"towerId":"tower.arrow","x":2,"y":14}""");
        var result = await ReceiveOfTypeAsync(seat1, "commandResult");
        Assert.Equal("a", result.GetProperty("id").GetString());
        Assert.True(result.GetProperty("accepted").GetBoolean());

        // A wire-level fact, not a business rule: this build's message shape has no field a
        // client could put a claimed seat into at all, so there is nothing here to spoof — see
        // the class remarks and CommandAuthorityTests (LTW.Tests) for the same property proven
        // directly against LocalVerticalSlice with an authority that WOULD notice a mismatch.
        // Not the only tower on the board by this point — six of the eight seats are bot-driven
        // and, now that the seat authority actually lets bots act (see ConnectionSeatAuthority's
        // and ServerMatch.matchLock's own remarks for the bug this test caught), they are already
        // building on their own lanes from tick zero. Filtered to seat 1's own tower specifically.
        var tickMessage = await ReceiveOfTypeAsync(seat1, "tick");
        var placedTower = tickMessage.GetProperty("towers").EnumerateArray()
            .Single(tower => tower.GetProperty("ownerId").GetInt32() == 1);
        Assert.Equal(2, placedTower.GetProperty("x").GetInt32());
        Assert.Equal(14, placedTower.GetProperty("y").GetInt32());
    }

    [Fact]
    public async Task An_invalid_join_token_is_refused()
    {
        var (matchId, _) = await CreateMatchAsync(new[] { 1 });
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://localhost:{port}/matches/{matchId}/join?seat=1&token=not-the-real-token"), CancellationToken.None);

        var buffer = new byte[16];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
    }

    [Fact]
    public async Task Eight_lane_match_of_one_human_and_seven_bots_runs_to_a_result()
    {
        // 200 ticks/second, not the real 10: a probe against the same reference seed
        // (LocalMatchOptions.Default) in LTW.Tests found it concludes around tick 3849, which at
        // real pace is over six minutes of wall clock — correct for a shipped match, useless for
        // a test. This proves the SAME server loop, transport and dispatch reach a real
        // MatchEndedEvent; it says nothing about real-time pacing, which the default (untested
        // here) rate is answerable for on its own.
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 }, ticksPerSecond: 200);
        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        // No commands from the human seat at all — this is checking the bots play the match out
        // on the server's own tick loop, end to end, exactly as they would locally. One "tick"
        // message per tick now (see TickMessage's own remarks for why that replaced one send per
        // event plus a separate snapshot send), so this only has to look inside its events array.
        JsonElement? finalSummary = null;
        long lastSeenTick = -1;
        var messagesSeen = 0;
        // Measured on this machine (see docs/MULTIPLAYER_ROLLOUT.md's MP-04 notes): about 34
        // delivered ticks/second over the real loopback WebSocket transport, regardless of the
        // requested rate — the bottleneck is round-trip send/receive overhead, not the simulation
        // or the timer. 3849 ticks at that rate is ~115s; 150 leaves real margin.
        var deadline = DateTime.UtcNow.AddSeconds(150);
        while (DateTime.UtcNow < deadline)
        {
            var message = await ReceiveAsync(seat1, TimeSpan.FromSeconds(10));
            messagesSeen++;
            if (!message.TryGetProperty("type", out var type) || type.GetString() != "tick")
            {
                continue;
            }

            lastSeenTick = message.GetProperty("tick").GetInt64();
            var matchEnded = message.GetProperty("events").EnumerateArray()
                .FirstOrDefault(evt => evt.GetProperty("kind").GetString() == "MatchEndedEvent");
            if (matchEnded.ValueKind != JsonValueKind.Undefined)
            {
                finalSummary = matchEnded;
                break;
            }
        }

        output.WriteLine($"messagesSeen={messagesSeen} lastSeenTick={lastSeenTick}");
        Assert.NotNull(finalSummary);

        var replayPath = Path.Combine(replayDirectory, $"{matchId}.json");
        var replayDeadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(replayPath) && DateTime.UtcNow < replayDeadline)
        {
            await Task.Delay(100);
        }

        Assert.True(File.Exists(replayPath), "server-side replay capture was never written");
        var replayJson = await File.ReadAllTextAsync(replayPath);
        Assert.Contains("\"commands\"", replayJson, StringComparison.OrdinalIgnoreCase);
    }
}
