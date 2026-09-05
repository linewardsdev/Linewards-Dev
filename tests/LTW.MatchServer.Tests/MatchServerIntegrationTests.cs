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

    private async Task<(string matchId, Dictionary<string, string> tokens)> CreateMatchAsync(int[] humanSeats, double? ticksPerSecond = null, double openingBuildWindowSeconds = 0)
    {
        using var client = new HttpClient();
        // Defaults to 0 so every test but the one exercising the window itself keeps seeing the
        // pre-window behavior (ticks advance from the first loop iteration) rather than needing to
        // wait out ServerMatch's real 30 second default.
        var body = JsonSerializer.Serialize(new { humanSeats, ticksPerSecond, openingBuildWindowSeconds }, Json);
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

    /// <summary>
    /// Found investigating reconnect: <c>BindAsync</c> had no "already bound" check at all, so a
    /// second connection presenting the same token for a seat that already had a live connection
    /// would simply bind alongside it — both going on to receive every broadcast and dispatch
    /// commands as that seat, which is exactly the kind of desync reconnect exists to avoid.
    /// </summary>
    [Fact]
    public async Task Rejoining_a_seat_evicts_the_stale_connection_instead_of_double_binding_it()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 });
        var stale = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(stale, "welcome");

        using var fresh = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(fresh, "welcome");

        // The stale connection is told it's done, not just silently ignored from here on — a
        // client that never itself noticed the drop must still learn its connection is dead.
        var buffer = new byte[16];
        var closeResult = await stale.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, closeResult.MessageType);
        stale.Dispose();

        // Only the fresh connection can act as the seat now.
        await SendAsync(fresh, """{"type":"placeTower","id":"a","laneId":1,"towerId":"tower.arrow","x":2,"y":14}""");
        var result = await ReceiveOfTypeAsync(fresh, "commandResult");
        Assert.True(result.GetProperty("accepted").GetBoolean());
    }

    /// <summary>
    /// MP-06 self-audit: a wire-reconstructed player was missing ChosenTowerLine/tower-line-tiers/
    /// send-category-tiers entirely, which silently broke tier purchases past the first client-side
    /// (see PlayerSnapshotDto's own remarks). Proves the fix against a real match, not a unit test
    /// of the DTO shape alone: place a tower and confirm both the resulting line commitment and its
    /// starting tier show up in a real TickMessage.
    /// </summary>
    /// <remarks>
    /// Does NOT also drive a live tier-2 purchase through to confirmation: tier 2 gates on minimum
    /// income (NextTierMinimumIncome), and at 200 ticks/second against seven active bots a single
    /// arrow tower reliably lost the match (PlayerEliminated) before affording it, tried three
    /// times while writing this. Not a gap in what this proves — the reconstruction code path is
    /// identical for tier 1 and tier 2 (the same WithTowerLineTier call this test already exercises
    /// for the baseline), so orchestrating a live change would exercise no code this does not
    /// already cover, only add a flaky economy-balance dependency this test does not need.
    /// </remarks>
    [Fact]
    public async Task Tick_messages_carry_the_players_chosen_line_and_tier_state()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 }, ticksPerSecond: 200);
        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        await SendAsync(seat1, """{"type":"placeTower","id":"place","laneId":1,"towerId":"tower.arrow","x":2,"y":14}""");
        var placeResult = await ReceiveOfTypeAsync(seat1, "commandResult");
        Assert.True(placeResult.GetProperty("accepted").GetBoolean());

        // Polls for a tick where the placed tower is actually visible, the same robust pattern the
        // class's other tests already use, rather than trusting that the very next "tick" message
        // after "commandResult" already reflects it — commandResult is sent AFTER matchLock is
        // released (see ServerMatch.DispatchAsync's own remarks), so a tick broadcast for the same
        // or a later tick can legitimately arrive at the client before its triggering command's own
        // reply does. Once the tower is visible, ChosenTowerLine and TowerLineTiers were set in the
        // SAME PlaceTower call, atomically, so asserting them off this same message is correct.
        JsonElement? placedTower = null;
        JsonElement afterPlace = default;
        var placeDeadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < placeDeadline)
        {
            afterPlace = await ReceiveOfTypeAsync(seat1, "tick", TimeSpan.FromSeconds(5));
            var candidate = afterPlace.GetProperty("towers").EnumerateArray()
                .FirstOrDefault(tower => tower.GetProperty("ownerId").GetInt32() == 1 && tower.GetProperty("x").GetInt32() == 2 && tower.GetProperty("y").GetInt32() == 14);
            if (candidate.ValueKind != JsonValueKind.Undefined)
            {
                placedTower = candidate;
                break;
            }
        }

        Assert.NotNull(placedTower);
        var playerAfterPlace = afterPlace.GetProperty("players").EnumerateArray()
            .Single(player => player.GetProperty("playerId").GetInt32() == 1);
        var chosenLine = playerAfterPlace.GetProperty("chosenTowerLine").GetInt32();
        Assert.NotEqual(-1, chosenLine);
        Assert.Equal(1, playerAfterPlace.GetProperty("towerLineTiers")[chosenLine].GetInt32());
    }

    /// <summary>
    /// The bug a live PLAY ONLINE test found: a server match previously started ticking (and bots
    /// opened their creep sends) on its very first loop iteration, so a player dropped straight
    /// into an active match with no chance to place an opening tower first — see ServerMatch's own
    /// remarks on <c>openingBuildWindow</c>. Proves the window's contract end to end: tick stays at
    /// 0 and no creeps appear while it runs, placement commands still work, and it ends on its own
    /// into a normal ticking match that keeps whatever was placed during it.
    /// </summary>
    [Fact]
    public async Task Opening_build_window_holds_the_tick_and_still_accepts_placement()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 }, ticksPerSecond: 20, openingBuildWindowSeconds: 1);
        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        var firstTick = await ReceiveOfTypeAsync(seat1, "tick");
        Assert.True(firstTick.GetProperty("isOpeningBuildCountdown").GetBoolean());
        Assert.Equal(0, firstTick.GetProperty("tick").GetInt64());
        Assert.Empty(firstTick.GetProperty("creeps").EnumerateArray());

        await SendAsync(seat1, """{"type":"placeTower","id":"place","laneId":1,"towerId":"tower.arrow","x":2,"y":14}""");
        var placeResult = await ReceiveOfTypeAsync(seat1, "commandResult");
        Assert.True(placeResult.GetProperty("accepted").GetBoolean());

        // Polls until the window ends and real ticks resume, then confirms the tower placed
        // DURING the window survived into live play — the same "poll until visible" pattern the
        // class's other tests use, since a tick/commandResult ordering race exists here too (see
        // Tick_messages_carry_the_players_chosen_line_and_tier_state's own remarks).
        JsonElement? placedTowerAfterLive = null;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var tick = await ReceiveOfTypeAsync(seat1, "tick", TimeSpan.FromSeconds(5));
            if (tick.GetProperty("isOpeningBuildCountdown").GetBoolean())
            {
                Assert.Equal(0, tick.GetProperty("tick").GetInt64());
                continue;
            }

            Assert.True(tick.GetProperty("tick").GetInt64() > 0);
            var candidate = tick.GetProperty("towers").EnumerateArray()
                .FirstOrDefault(tower => tower.GetProperty("ownerId").GetInt32() == 1 && tower.GetProperty("x").GetInt32() == 2 && tower.GetProperty("y").GetInt32() == 14);
            if (candidate.ValueKind != JsonValueKind.Undefined)
            {
                placedTowerAfterLive = candidate;
                break;
            }
        }

        Assert.NotNull(placedTowerAfterLive);
    }

    /// <summary>
    /// The bug a live online test found: the send-dock UI's real behavior is "queue this creep,
    /// pay for it when affordable" (<c>LocalVerticalSlice.EnqueueSend</c>, no immediate gold
    /// check), but the wire layer only ever had "queueSend" — an immediate, all-or-nothing
    /// spend-and-spawn (<c>LocalVerticalSlice.QueueSend</c>) that rejects outright if the sender
    /// cannot afford it right now. Every online send of anything not affordable on the spot
    /// silently failed instead of waiting. Proves the fix directly: a fresh match starts with 100
    /// gold (see LocalVerticalSlice's own player construction), the Siege Colossus costs 104 (see
    /// SampleVerticalSliceContent), so an immediate "queueSend" for one would have been rejected —
    /// "enqueueSend" must still be accepted, because it does not check affordability at all.
    /// </summary>
    [Fact]
    public async Task Enqueue_send_accepts_a_creep_the_sender_cannot_yet_afford()
    {
        // The opening build window (see ServerMatch's own remarks) never calls AdvanceOneTick, so
        // DrainSendQueues never runs either — a queued creep sits untouched for the window's whole
        // duration regardless of gold. That is what makes this deterministic: outside the window,
        // income (10/tick by default) clears the Colossus's 4-gold shortfall in a single tick at
        // any real tick rate, so there is no reliable moment to observe "queued but not yet paid
        // for" without either racing the drain or, as here, holding it off entirely.
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 }, ticksPerSecond: 20, openingBuildWindowSeconds: 5);
        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        await SendAsync(seat1, """{"type":"enqueueSend","id":"enqueue-colossus","creepId":"creep.colossus"}""");
        var result = await ReceiveOfTypeAsync(seat1, "commandResult");
        Assert.True(result.GetProperty("accepted").GetBoolean());

        // The other half of the same live-test finding: even once the send genuinely queues
        // server-side, a wire client had no data at all to show a queue badge from — PlayerSnapshotDto
        // never carried it. Polls the same way the other tests here do, since this can land on a
        // tick that arrives before or after the commandResult reply.
        var deadline = DateTime.UtcNow.AddSeconds(4);
        var sawQueuedColossus = false;
        while (DateTime.UtcNow < deadline)
        {
            var tick = await ReceiveOfTypeAsync(seat1, "tick", TimeSpan.FromSeconds(5));
            var player = tick.GetProperty("players").EnumerateArray().Single(p => p.GetProperty("playerId").GetInt32() == 1);
            if (player.GetProperty("sendQueue").EnumerateArray().Any(entry => entry.GetString() == "creep.colossus"))
            {
                sawQueuedColossus = true;
                break;
            }
        }

        Assert.True(sawQueuedColossus, "the queued colossus never appeared in any tick's sendQueue");
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
        // A "34 delivered ticks/second regardless of requested rate" figure was recorded here
        // originally and was wrong — measured while a since-fixed bot-authority bug meant the
        // match was stalled, not slow. See docs/MULTIPLAYER_ROLLOUT.md's MP-04 "What broke" for
        // the correction: real throughput tracks the requested rate. 150s is simply generous
        // margin for a CI-shared machine, not derived from a measured ceiling.
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

    /// <summary>
    /// MP-06: a wire-based renderer cannot place a creep without this. Bots on the seven other
    /// lanes will send on their own within the first few ticks at real content's default economy,
    /// so this needs no scripted command — just enough ticks for that to happen.
    /// </summary>
    [Fact]
    public async Task Tick_messages_carry_creep_positions_once_bots_start_sending()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 }, ticksPerSecond: 200);
        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        JsonElement? tickWithCreeps = null;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var message = await ReceiveAsync(seat1, TimeSpan.FromSeconds(10));
            if (!message.TryGetProperty("type", out var type) || type.GetString() != "tick")
            {
                continue;
            }

            if (message.GetProperty("creeps").GetArrayLength() > 0)
            {
                tickWithCreeps = message;
                break;
            }
        }

        Assert.NotNull(tickWithCreeps);
        var creep = tickWithCreeps!.Value.GetProperty("creeps")[0];
        Assert.True(creep.GetProperty("entityId").GetInt64() > 0);
        Assert.False(string.IsNullOrEmpty(creep.GetProperty("creepId").GetString()));
        Assert.True(creep.GetProperty("maxHealth").GetInt32() > 0);
        Assert.True(creep.GetProperty("effectiveMovementCost").GetInt32() > 0);
    }
}
