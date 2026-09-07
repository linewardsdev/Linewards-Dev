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
        //
        // Polls across several "tick" messages rather than trusting the very first one received
        // after "commandResult" already reflects the placement: commandResult is sent AFTER
        // matchLock is released (see ServerMatch.DispatchAsync's own remarks), so a tick broadcast
        // for an earlier tick can legitimately arrive at the client before its triggering command's
        // own reply does. A bare `.Single(...)` on the first tick received was flaky for exactly
        // this reason — see docs/SECURITY_AUDIT_2026-09-05.md's M-T1 and the identical, already-
        // robust pattern in Tick_messages_carry_the_players_chosen_line_and_tier_state below.
        JsonElement? placedTower = null;
        var placeDeadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < placeDeadline)
        {
            var tickMessage = await ReceiveOfTypeAsync(seat1, "tick");
            placedTower = tickMessage.GetProperty("towers").EnumerateArray()
                .Select(tower => (JsonElement?)tower)
                .SingleOrDefault(tower => tower!.Value.GetProperty("ownerId").GetInt32() == 1);
            if (placedTower is not null)
            {
                break;
            }
        }

        Assert.NotNull(placedTower);
        Assert.Equal(2, placedTower!.Value.GetProperty("x").GetInt32());
        Assert.Equal(14, placedTower.Value.GetProperty("y").GetInt32());
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

    /// <summary>
    /// OPEN_ITEMS.md item 55: found the day item 47 finally gave <c>CancelQueuedSend</c> a real UI
    /// control — every other command already had a wire message, this one never did, so canceling
    /// a queued send silently did nothing during an online match. Proves the fix directly: queue a
    /// creep the sender cannot afford yet (same opening-build-window trick as the enqueue test
    /// above, so the queue entry sits still instead of draining), cancel it over the wire, and
    /// confirm it actually leaves the tick's own sendQueue.
    /// </summary>
    [Fact]
    public async Task Cancel_send_removes_a_queued_creep_from_the_wire_snapshot()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 }, ticksPerSecond: 20, openingBuildWindowSeconds: 5);
        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        await SendAsync(seat1, """{"type":"enqueueSend","id":"enqueue-colossus","creepId":"creep.colossus"}""");
        Assert.True((await ReceiveOfTypeAsync(seat1, "commandResult")).GetProperty("accepted").GetBoolean());

        var sawQueuedColossus = await PollTicksAsync(seat1, TimeSpan.FromSeconds(4), player =>
            player.GetProperty("sendQueue").EnumerateArray().Any(entry => entry.GetString() == "creep.colossus"));
        Assert.True(sawQueuedColossus, "the queued colossus never appeared in any tick's sendQueue");

        await SendAsync(seat1, """{"type":"cancelSend","id":"cancel-colossus","creepId":"creep.colossus"}""");
        Assert.True((await ReceiveOfTypeAsync(seat1, "commandResult")).GetProperty("accepted").GetBoolean());

        var stillQueued = await PollTicksAsync(seat1, TimeSpan.FromSeconds(4), player =>
            player.GetProperty("sendQueue").EnumerateArray().Any(entry => entry.GetString() == "creep.colossus"));
        Assert.False(stillQueued, "the colossus was still in the sendQueue after canceling it");
    }

    /// <summary>Same finding as above, for the whole-queue clear. See item 55.</summary>
    [Fact]
    public async Task Clear_send_queue_empties_the_wire_snapshots_sendQueue()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 }, ticksPerSecond: 20, openingBuildWindowSeconds: 5);
        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        await SendAsync(seat1, """{"type":"enqueueSend","id":"enqueue-colossus-1","creepId":"creep.colossus"}""");
        Assert.True((await ReceiveOfTypeAsync(seat1, "commandResult")).GetProperty("accepted").GetBoolean());
        await SendAsync(seat1, """{"type":"enqueueSend","id":"enqueue-colossus-2","creepId":"creep.colossus"}""");
        Assert.True((await ReceiveOfTypeAsync(seat1, "commandResult")).GetProperty("accepted").GetBoolean());

        var sawQueuedColossus = await PollTicksAsync(seat1, TimeSpan.FromSeconds(4), player =>
            player.GetProperty("sendQueue").EnumerateArray().Any(entry => entry.GetString() == "creep.colossus"));
        Assert.True(sawQueuedColossus, "neither queued colossus ever appeared in any tick's sendQueue");

        await SendAsync(seat1, """{"type":"clearSendQueue","id":"clear-queue"}""");
        Assert.True((await ReceiveOfTypeAsync(seat1, "commandResult")).GetProperty("accepted").GetBoolean());

        var stillQueued = await PollTicksAsync(seat1, TimeSpan.FromSeconds(4), player =>
            player.GetProperty("sendQueue").EnumerateArray().Any(entry => entry.GetString() == "creep.colossus"));
        Assert.False(stillQueued, "the send queue was not empty after clearSendQueue");
    }

    /// <summary>
    /// Shared polling loop for the two tests above — same pattern
    /// <see cref="Enqueue_send_accepts_a_creep_the_sender_cannot_yet_afford"/> already used inline,
    /// pulled out since both new tests need it twice each (once to see the queue populated, once
    /// to see the effect of canceling/clearing it).
    /// </summary>
    private static async Task<bool> PollTicksAsync(ClientWebSocket socket, TimeSpan timeout, Func<JsonElement, bool> matchesSeat1Player)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            var tick = await ReceiveOfTypeAsync(socket, "tick", TimeSpan.FromSeconds(5));
            var player = tick.GetProperty("players").EnumerateArray().Single(p => p.GetProperty("playerId").GetInt32() == 1);
            if (matchesSeat1Player(player))
            {
                return true;
            }
        }

        return false;
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

        // Before ReplayFileReader existed, nothing in `src` could turn this file back into
        // anything — MP07_RUNBOOK.md's "investigate a desync from its replay" step had no actual
        // mechanism behind it. `humanSeats: [1]` means MatchRegistry.CreateMatch's own seat loop
        // never disables a lane (seat 1 already IS LocalMatchOptions.Default's LocalPlayerId), so
        // the options the live match actually ran under are exactly LocalMatchOptions.Default —
        // the same instance this replay needs. See docs/SECURITY_AUDIT_2026-09-05.md's M-T1.
        var record = await ReplayFileReader.ReadAsync(replayPath);
        Assert.True(record.Commands.Count > 0, "expected a non-empty command log from a real completed match");

        var replayedOnce = LocalVerticalSlice.Replay(record, SampleVerticalSliceContent.Create(), LocalMatchOptions.Default);
        Assert.NotNull(replayedOnce.MatchSummary);
        Assert.Equal(record.CompletedAtTick.Value, replayedOnce.CurrentTick.Value);

        // Replaying the SAME parsed record twice must reach the same state both times — proves
        // the file round-trips into something deterministic, not just something parseable.
        var replayedTwice = LocalVerticalSlice.Replay(record, SampleVerticalSliceContent.Create(), LocalMatchOptions.Default);
        Assert.Equal(replayedOnce.GetSnapshot().Fingerprint(), replayedTwice.GetSnapshot().Fingerprint());
    }

    /// <summary>
    /// Before this, a match that ended never actually stopped: the tick loop's own while
    /// condition never checked <c>slice.MatchSummary</c>, so it kept ticking and broadcasting
    /// forever once a match was over. MP-07's GSDK integration also needs the process to exit
    /// once its one hosted match ends, which starts with the loop itself actually stopping — see
    /// docs/MULTIPLAYER_ROLLOUT.md's MP-07.
    /// </summary>
    [Fact]
    public async Task ServerMatch_completion_resolves_and_ticking_stops_once_the_match_ends()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 }, ticksPerSecond: 200);
        var match = registry.Find(matchId);
        Assert.NotNull(match);

        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        var deadline = DateTime.UtcNow.AddSeconds(150);
        var matchEnded = false;
        while (DateTime.UtcNow < deadline)
        {
            var message = await ReceiveAsync(seat1, TimeSpan.FromSeconds(10));
            if (!message.TryGetProperty("type", out var type) || type.GetString() != "tick")
            {
                continue;
            }

            if (message.GetProperty("events").EnumerateArray().Any(evt => evt.GetProperty("kind").GetString() == "MatchEndedEvent"))
            {
                matchEnded = true;
                break;
            }
        }

        Assert.True(matchEnded, "match never reached MatchEndedEvent");

        var completed = await Task.WhenAny(match!.Completion, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(match.Completion, completed);
        Assert.True(match.Completion.IsCompletedSuccessfully);
    }

    /// <summary>
    /// docs/SECURITY_AUDIT_2026-09-05.md's H4: before RunLoopAsync's own try/finally existed,
    /// Stop() canceling while the loop was mid-await threw OperationCanceledException straight out
    /// of the fire-and-forget tick-loop task, skipping Completion's TrySetResult entirely and
    /// contradicting Stop's own doc comment that Completion resolves either way. This is exactly
    /// the coverage gap the audit itself flagged as missing.
    /// </summary>
    [Fact]
    public async Task ServerMatch_completion_resolves_when_Stop_is_called_directly()
    {
        var match = registry.CreateMatch(new[] { 1 }, ticksPerSecond: 4);

        match.Stop();

        var completed = await Task.WhenAny(match.Completion, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(match.Completion, completed);
        Assert.True(match.Completion.IsCompletedSuccessfully);
    }

    /// <summary>
    /// MP-07's telemetry deliverable, emission side: every match's start and end should now be a
    /// structured, machine-parseable line rather than only free-text logging. Captures
    /// <see cref="ServerMatch.TelemetrySink"/> for the duration of one real match instead of
    /// redirecting the process's own <see cref="Console"/>, which would risk interleaving with
    /// unrelated output from other tests. See docs/MULTIPLAYER_ROLLOUT.md's MP-07.
    /// </summary>
    [Fact]
    public async Task Match_lifecycle_emits_structured_telemetry_for_start_and_end()
    {
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var originalSink = ServerMatch.TelemetrySink;
        ServerMatch.TelemetrySink = lines.Enqueue;
        try
        {
            var match = registry.CreateMatch(new[] { 1 }, ticksPerSecond: 200);

            // Same generous 150s budget as Eight_lane_match_of_one_human_and_seven_bots_runs_to_a_result
            // for the same reason — this is a real bot-vs-bot match run to a real conclusion, not a
            // fixed number of ticks, and CI-shared-machine margin matters more than a tight bound.
            var deadline = DateTime.UtcNow.AddSeconds(150);
            while (!match.Completion.IsCompleted && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(match.Completion.IsCompletedSuccessfully, "match never completed naturally within the deadline");

            var events = lines.Select(line => JsonSerializer.Deserialize<JsonElement>(line))
                .Where(element => element.GetProperty("matchId").GetString() == match.MatchId)
                .ToArray();

            var started = Assert.Single(events, element => element.GetProperty("event").GetString() == "match_started");
            Assert.Equal(1, started.GetProperty("details").GetProperty("humanSeatCount").GetInt32());

            var ended = Assert.Single(events, element => element.GetProperty("event").GetString() == "match_ended");
            Assert.True(ended.GetProperty("details").GetProperty("completedAtTick").GetInt64() > 0);
            Assert.True(ended.GetProperty("details").GetProperty("durationSeconds").GetDouble() > 0);
        }
        finally
        {
            ServerMatch.TelemetrySink = originalSink;
        }
    }

    /// <summary>
    /// The "crash report" half of MP-07's telemetry deliverable — a faulted tick loop (H4's own
    /// concern) must be visible as a structured event, not just a free-text
    /// <c>Console.Error.WriteLine</c>. <c>ticksPerSecond: 0</c> reaches <c>PeriodicTimer</c>'s own
    /// constructor and throws <c>OverflowException</c> before the loop ever ticks — see
    /// docs/SECURITY_AUDIT_2026-09-05.md's M1 (the reason `HttpMatchHost`'s own HTTP path now
    /// validates this upfront; calling <c>MatchRegistry.CreateMatch</c> directly, as this test
    /// does, bypasses that validator on purpose, the same way H4's own coverage does).
    /// </summary>
    [Fact]
    public async Task A_faulted_match_emits_structured_telemetry()
    {
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var originalSink = ServerMatch.TelemetrySink;
        ServerMatch.TelemetrySink = lines.Enqueue;
        try
        {
            var match = registry.CreateMatch(new[] { 1 }, ticksPerSecond: 0);

            var completed = await Task.WhenAny(match.Completion, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(match.Completion, completed);
            Assert.True(match.Completion.IsFaulted);

            var faulted = lines.Select(line => JsonSerializer.Deserialize<JsonElement>(line))
                .Single(element => element.GetProperty("matchId").GetString() == match.MatchId
                    && element.GetProperty("event").GetString() == "match_faulted");
            Assert.Equal("OverflowException", faulted.GetProperty("details").GetProperty("exceptionType").GetString());
        }
        finally
        {
            ServerMatch.TelemetrySink = originalSink;
        }
    }

    [Fact]
    public void CreateMatch_reuses_an_explicit_matchId_when_given_one_and_mints_a_fresh_one_otherwise()
    {
        var explicitMatch = registry.CreateMatch(new[] { 1 }, matchId: "explicit-id");
        Assert.Equal("explicit-id", explicitMatch.MatchId);
        Assert.Same(explicitMatch, registry.Find("explicit-id"));

        var mintedMatch = registry.CreateMatch(new[] { 1 });
        Assert.NotEqual("explicit-id", mintedMatch.MatchId);
        Assert.True(Guid.TryParseExact(mintedMatch.MatchId, "N", out _), $"expected a GUID-'N'-shaped id, got '{mintedMatch.MatchId}'");
    }

    [Fact]
    public async Task HttpMatchHost_with_match_creation_disabled_rejects_create_but_still_allows_join()
    {
        // A separate host on its own port, sharing this test's registry — allowMatchCreation is a
        // constructor-time choice on the host, not the registry, so the shared fixture's own
        // (creation-enabled) host can't be reused for this.
        var restrictedPort = FindFreePort();
        var restrictedHost = new HttpMatchHost(registry, $"http://localhost:{restrictedPort}/", allowMatchCreation: false);
        restrictedHost.Start();
        try
        {
            using var client = new HttpClient();
            var response = await client.PostAsync(
                $"http://localhost:{restrictedPort}/matches",
                new StringContent("{}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // Creation is disabled on the HOST, not the registry itself — pre-register a match
            // directly (as MP-07's GSDK bootstrap will) and confirm the join route still works.
            var match = registry.CreateMatch(new[] { 1 });
            var token = match.TokenFor(1)!;
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri($"ws://localhost:{restrictedPort}/matches/{match.MatchId}/join?seat=1&token={token}"), CancellationToken.None);
            await ReceiveOfTypeAsync(socket, "welcome");
        }
        finally
        {
            restrictedHost.Stop();
        }
    }

    /// <summary>
    /// docs/SECURITY_AUDIT_2026-09-05.md's M3: standalone mode's create-secret gate. Null (every
    /// other test's host) preserves today's open behavior; a host actually configured with a
    /// secret must reject a request that omits or gets it wrong, and accept one that matches.
    /// </summary>
    [Fact]
    public async Task HttpMatchHost_with_a_create_secret_configured_requires_a_matching_header()
    {
        const string secret = "test-secret";
        var restrictedPort = FindFreePort();
        var restrictedHost = new HttpMatchHost(registry, $"http://localhost:{restrictedPort}/", createSecret: secret);
        restrictedHost.Start();
        try
        {
            using var client = new HttpClient();
            var body = new StringContent("{\"humanSeats\":[1]}", Encoding.UTF8, "application/json");
            var withoutSecret = await client.PostAsync($"http://localhost:{restrictedPort}/matches", body);
            Assert.Equal(HttpStatusCode.Unauthorized, withoutSecret.StatusCode);

            using var wrongRequest = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{restrictedPort}/matches")
            {
                Content = new StringContent("{\"humanSeats\":[1]}", Encoding.UTF8, "application/json"),
            };
            wrongRequest.Headers.Add("X-Match-Create-Secret", "wrong");
            var withWrongSecret = await client.SendAsync(wrongRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, withWrongSecret.StatusCode);

            using var rightRequest = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{restrictedPort}/matches")
            {
                Content = new StringContent("{\"humanSeats\":[1]}", Encoding.UTF8, "application/json"),
            };
            rightRequest.Headers.Add("X-Match-Create-Secret", secret);
            var withRightSecret = await client.SendAsync(rightRequest);
            Assert.Equal(HttpStatusCode.OK, withRightSecret.StatusCode);
        }
        finally
        {
            restrictedHost.Stop();
        }
    }

    /// <summary>
    /// docs/SECURITY_AUDIT_2026-09-05.md's M3: before this cap existed, unauthenticated
    /// <c>POST /matches</c> calls could grow a standalone process's match count (each with its own
    /// bot-vs-bot tick loop) without limit.
    /// </summary>
    [Fact]
    public void MatchRegistry_refuses_to_exceed_its_configured_concurrent_match_cap()
    {
        var boundedRegistry = new MatchRegistry(replayDirectory, maxConcurrentMatches: 2);
        boundedRegistry.CreateMatch(new[] { 1 });
        boundedRegistry.CreateMatch(new[] { 1 });

        Assert.Throws<InvalidOperationException>(() => boundedRegistry.CreateMatch(new[] { 1 }));
    }

    /// <summary>
    /// docs/SECURITY_AUDIT_2026-09-05.md's M3: MatchRegistry never removed a finished match before
    /// this fix — <see cref="MatchRegistry.Find"/> would keep resolving it, and its sockets stayed
    /// open, forever.
    /// </summary>
    [Fact]
    public async Task Match_is_evicted_from_the_registry_once_it_ends()
    {
        var match = registry.CreateMatch(new[] { 1 }, ticksPerSecond: 200);
        var token = match.TokenFor(1)!;
        using var seat1 = await JoinAsync(match.MatchId, 1, token);
        await ReceiveOfTypeAsync(seat1, "welcome");

        match.Stop();
        await match.Completion;

        // Eviction runs as a continuation of Completion, not synchronously with it resolving —
        // give it a moment, same as any other fire-and-forget cleanup in this codebase.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (registry.Find(match.MatchId) is not null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.Null(registry.Find(match.MatchId));

        var result = await seat1.ReceiveAsync(new byte[16], CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
    }

    /// <summary>
    /// docs/SECURITY_AUDIT_2026-09-05.md's M5: without a join rate limit, every join attempt with
    /// any <c>playFabTicket</c> — garbage or not — triggers a real, quota-limited PlayFab
    /// <c>AuthenticateSessionTicket</c> call. This proves the limit actually engages per address
    /// rather than merely existing in code.
    /// </summary>
    [Fact]
    public async Task Excess_join_attempts_from_one_address_are_rejected_with_429()
    {
        var restrictedPort = FindFreePort();
        var restrictedHost = new HttpMatchHost(registry, $"http://localhost:{restrictedPort}/");
        restrictedHost.Start();
        try
        {
            var match = registry.CreateMatch(new[] { 1 });
            var token = match.TokenFor(1)!;
            var sawTooManyRequests = false;
            for (var i = 0; i < 25 && !sawTooManyRequests; i++)
            {
                // Each successful connect simply rebinds seat 1 (evicting the previous connection
                // — already-proven behavior, see Rejoining_a_seat_evicts... above), so this loop
                // never runs out of legitimate ways to reach the join handler.
                using var socket = new ClientWebSocket();
                try
                {
                    await socket.ConnectAsync(new Uri($"ws://localhost:{restrictedPort}/matches/{match.MatchId}/join?seat=1&token={token}"), CancellationToken.None);
                }
                catch (WebSocketException exception) when (exception.Message.Contains("429"))
                {
                    sawTooManyRequests = true;
                }
            }

            Assert.True(sawTooManyRequests, "expected at least one join attempt to be rate-limited");
        }
        finally
        {
            restrictedHost.Stop();
        }
    }

    /// <summary>
    /// docs/SECURITY_AUDIT_2026-09-05.md's H1: the receive loop used to accumulate frames into an
    /// unbounded MemoryStream — any client holding a valid token could send one multi-gigabyte
    /// message and OOM the process. This proves the cap actually closes the connection rather than
    /// buffering it, and that doing so doesn't take the rest of the match down with it.
    /// </summary>
    [Fact]
    public async Task Oversized_inbound_message_closes_the_connection_instead_of_being_buffered()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1, 2 });
        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        // Comfortably past HttpMatchHost's 16KB cap, wrapped as a syntactically plausible frame so
        // this is really testing the size cap, not just malformed JSON.
        var oversized = $"{{\"type\":\"placeTower\",\"towerId\":\"{new string('a', 20 * 1024)}\"}}";
        await SendAsync(seat1, oversized);

        var buffer = new byte[16];
        var result = await seat1.ReceiveAsync(buffer, CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, seat1.CloseStatus);

        // The rest of the match (and the process) must still be alive — an unrelated seat can
        // still join the same match normally right after.
        using var seat2 = await JoinAsync(matchId, 2, tokens["2"]);
        await ReceiveOfTypeAsync(seat2, "welcome");
    }

    /// <summary>
    /// MP-07's GSDK bootstrap (<c>Program.cs</c>) deserializes a PlayFab <c>SessionCookie</c>
    /// string into the exact same <see cref="CreateMatchRequest"/> shape
    /// <see cref="HttpMatchHost.HandleCreateMatchAsync"/> deserializes its HTTP body into — a
    /// plain xUnit test, no GSDK or PlayFab involved, pinning that shared contract.
    /// </summary>
    [Fact]
    public void CreateMatchRequest_deserializes_from_a_SessionCookie_shaped_json_string()
    {
        var sessionCookie = "{\"humanSeats\":[1],\"ticksPerSecond\":200,\"playFabSeats\":{\"1\":\"some-playfab-id\"},\"openingBuildWindowSeconds\":5}";

        var request = JsonSerializer.Deserialize<CreateMatchRequest>(sessionCookie, Json);

        Assert.NotNull(request);
        Assert.Equal(new List<int> { 1 }, request!.HumanSeats);
        Assert.Equal(200, request.TicksPerSecond);
        Assert.Equal("some-playfab-id", request.PlayFabSeats?[1]);
        Assert.Equal(5, request.OpeningBuildWindowSeconds);
    }

    /// <summary>
    /// Before CreateMatchRequestValidator existed, this seat bound fine and only failed the
    /// moment it sent an economy command (a `KeyNotFoundException` deep inside the slice). See
    /// docs/SECURITY_AUDIT_2026-09-05.md's M1.
    /// </summary>
    [Fact]
    public async Task POST_matches_rejects_an_out_of_range_seat()
    {
        using var client = new HttpClient();
        var body = JsonSerializer.Serialize(new { humanSeats = new[] { 999 } }, Json);
        var response = await client.PostAsync(
            $"http://localhost:{port}/matches",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Before CreateMatchRequestValidator existed, `ticksPerSecond: 0` reached
    /// `PeriodicTimer`'s constructor and threw `OverflowException` inside the tick loop's own
    /// background task — after the caller already had a 200 response with real join tokens for a
    /// match that could never actually run. See docs/SECURITY_AUDIT_2026-09-05.md's M1.
    /// </summary>
    [Fact]
    public async Task POST_matches_rejects_a_ticksPerSecond_of_zero()
    {
        using var client = new HttpClient();
        var body = JsonSerializer.Serialize(new { humanSeats = new[] { 1 }, ticksPerSecond = 0 }, Json);
        var response = await client.PostAsync(
            $"http://localhost:{port}/matches",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Before this fix, an omitted `towerId` (defaulting to `""`) reached `ContentId`'s own
    /// constructor, which throws `ArgumentException` — uncaught inside `DispatchAsync`, which
    /// ReceiveLoopAsync only guarded against `WebSocketException`, silently orphaning the
    /// connection (no error, no close, no more ticks). See docs/SECURITY_AUDIT_2026-09-05.md's M2.
    /// </summary>
    [Fact]
    public async Task Malformed_command_message_receives_an_error_instead_of_orphaning_the_connection()
    {
        var (matchId, tokens) = await CreateMatchAsync(new[] { 1 });
        using var seat1 = await JoinAsync(matchId, 1, tokens["1"]);
        await ReceiveOfTypeAsync(seat1, "welcome");

        await SendAsync(seat1, """{"type":"placeTower","id":"bad","laneId":1,"x":2,"y":14}""");
        var error = await ReceiveOfTypeAsync(seat1, "error");
        Assert.False(string.IsNullOrEmpty(error.GetProperty("message").GetString()));

        // The connection must still be alive and dispatching normally afterward.
        await SendAsync(seat1, """{"type":"placeTower","id":"good","laneId":1,"towerId":"tower.arrow","x":2,"y":14}""");
        var result = await ReceiveOfTypeAsync(seat1, "commandResult");
        Assert.Equal("good", result.GetProperty("id").GetString());
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

    /// <summary>
    /// MP-05: a queue-auto-allocated server has no SessionCookie of its own — the matched players
    /// come from GSDK's GetInitialPlayers() instead. See QueuedMatchBootstrap's own remarks.
    /// </summary>
    [Fact]
    public void QueuedMatchBootstrap_assigns_sequential_seats_to_each_matched_playFabId()
    {
        var (humanSeats, playFabIdBySeat) = QueuedMatchBootstrap.AssignSeatsFromInitialPlayers(new[] { "playfab-a", "playfab-b", "playfab-c" });

        Assert.Equal(new List<int> { 1, 2, 3 }, humanSeats);
        Assert.Equal("playfab-a", playFabIdBySeat[1]);
        Assert.Equal("playfab-b", playFabIdBySeat[2]);
        Assert.Equal("playfab-c", playFabIdBySeat[3]);
    }

    [Fact]
    public void QueuedMatchBootstrap_returns_empty_for_no_matched_players()
    {
        var (humanSeats, playFabIdBySeat) = QueuedMatchBootstrap.AssignSeatsFromInitialPlayers(Array.Empty<string>());

        Assert.Empty(humanSeats);
        Assert.Empty(playFabIdBySeat);
    }

    [Fact]
    public async Task HttpMatchHost_current_alias_resolves_to_the_only_match_when_creation_is_disallowed()
    {
        var restrictedPort = FindFreePort();
        var restrictedHost = new HttpMatchHost(registry, $"http://localhost:{restrictedPort}/", allowMatchCreation: false);
        restrictedHost.Start();
        try
        {
            var match = registry.CreateMatch(new[] { 1 });
            var token = match.TokenFor(1)!;
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri($"ws://localhost:{restrictedPort}/matches/current/join?seat=1&token={token}"), CancellationToken.None);
            var welcome = await ReceiveOfTypeAsync(socket, "welcome");
            Assert.Equal(1, welcome.GetProperty("seat").GetInt32());
        }
        finally
        {
            restrictedHost.Stop();
        }
    }

    [Fact]
    public async Task HttpMatchHost_current_alias_is_not_offered_when_match_creation_is_allowed()
    {
        // Standalone mode (allowMatchCreation: true, the shared fixture's own host) never treats
        // "current" as special — an exact match id is always required there, so this looks up a
        // match literally named "current" (none exists) and never even reaches a WebSocket
        // upgrade, unlike a rejected-but-upgraded join (see AcceptAsync's own token-mismatch path).
        var match = registry.CreateMatch(new[] { 1 });
        _ = match.TokenFor(1)!;
        using var socket = new ClientWebSocket();
        await Assert.ThrowsAsync<WebSocketException>(() =>
            socket.ConnectAsync(new Uri($"ws://localhost:{port}/matches/current/join?seat=1&token=irrelevant"), CancellationToken.None));
    }

    /// <summary>
    /// docs/SECURITY_AUDIT_2026-09-05.md's L5: <see cref="HttpMatchHost"/> did not implement
    /// <see cref="IDisposable"/> before this fix. <see cref="ServerMatch"/>'s own disposal is
    /// exercised, unobserved, as part of <see cref="Match_is_evicted_from_the_registry_once_it_ends"/>'s
    /// eviction — this covers <see cref="HttpMatchHost"/> directly, including with a connection
    /// still live when Dispose runs.
    /// </summary>
    [Fact]
    public async Task HttpMatchHost_disposes_without_throwing()
    {
        var restrictedPort = FindFreePort();
        var restrictedHost = new HttpMatchHost(registry, $"http://localhost:{restrictedPort}/");
        restrictedHost.Start();

        var match = registry.CreateMatch(new[] { 1 });
        var token = match.TokenFor(1)!;
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://localhost:{restrictedPort}/matches/{match.MatchId}/join?seat=1&token={token}"), CancellationToken.None);
        await ReceiveOfTypeAsync(socket, "welcome");

        restrictedHost.Dispose();
    }
}
