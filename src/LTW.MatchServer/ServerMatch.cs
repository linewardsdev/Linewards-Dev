using System.Net.WebSockets;
using System.Text.Json;
using LTW.MatchServer.Wire;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using LTW.Simulation.Replay;

namespace LTW.MatchServer;

/// <summary>
/// One authoritative match instance: a <c>LocalVerticalSlice</c>, the connections attached to its
/// human seats, and the fixed-tick loop that advances it and broadcasts what happened.
/// MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-04 — see docs/MULTIPLAYER_ROLLOUT.md for what this
/// initiative covers in this pass and what still needs a real deployment to prove.
/// </summary>
/// <remarks>
/// Every match owns exactly one <c>LocalVerticalSlice</c>, ticked from exactly one loop, so
/// commands from every connection are naturally serialized — there is no cross-connection race to
/// resolve here, only the seat-spoofing question <see cref="ConnectionSeatAuthority"/> answers.
/// </remarks>
public sealed class ServerMatch
{
    /// <summary>
    /// The default, real-play pace — matches Unity's own shipped local single-player rate
    /// (<c>UnitySimulationDriver</c>'s <c>ticksPerSecond</c> field, 4, never overridden by any
    /// scene or prefab — confirmed by grepping every scene/prefab for its component GUID and
    /// finding none, so the live value really is the script's compiled default).
    /// </summary>
    /// <remarks>
    /// Was 10 originally, picked from `ARCHITECTURE.md`'s aspirational "10 to 20 simulation ticks
    /// per second" prototype target — which the actual shipped client never ended up running at.
    /// Found live, testing a real match on a real iPad: the whole match (creep movement, income,
    /// cooldowns, everything defined "per tick") played at 2.5x local's pace, because running the
    /// identical simulation logic more often per real second makes it run everything faster in
    /// real time, not just "more precisely" — a server tick rate is a pacing choice, not a free
    /// precision upgrade over local play's rate.
    ///
    /// Overridable per match (see the constructor) purely for tests: a match that takes minutes
    /// of wall-clock time to finish at real pace is still the right thing to run at real pace in
    /// production, and the wrong thing to wait out in a test suite — MP-04's own acceptance check
    /// ("runs to a result") is proven at a compressed rate instead, over the same real WebSocket
    /// transport a real client would use.
    /// </remarks>
    internal const double DefaultTicksPerSecond = 4;

    /// <summary>
    /// How long a match sits at tick 0 accepting placement commands before the simulation clock
    /// (and bot opening sends) start — mirrors the local single-player flow's 30 second "tap PLAY
    /// to begin" window (see Unity's <c>UnitySimulationDriver.BeginOpeningBuildCountdown</c>),
    /// which has no server-side equivalent otherwise: without this, <see cref="RunLoopAsync"/>
    /// called <c>AdvanceOneTick</c> on its very first iteration, and bots sent their opening creeps
    /// inside that same call — before a human player could place a single tower.
    /// </summary>
    private const double DefaultOpeningBuildWindowSeconds = 30;

    private readonly double ticksPerSecond;
    private readonly TimeSpan openingBuildWindow;
    private DateTimeOffset matchStartedAtUtc;
    private long tickSequence;

    public string MatchId { get; }

    private readonly LocalVerticalSlice slice;
    private readonly ConnectionSeatAuthority authority;

    /// <summary>
    /// Serializes every touch of <see cref="slice"/> and <see cref="authority"/> — the tick loop's
    /// own <c>AdvanceOneTick</c> (which runs every bot's turn) and every client's dispatched
    /// command both go through this. Two real reasons, found by this class's own first version
    /// failing an integration test rather than assumed up front: <c>LocalVerticalSlice</c> is not
    /// thread-safe, so a client command landing mid-tick would already be a bug; and
    /// <see cref="ConnectionSeatAuthority"/> carries WHICH connection is calling as ambient state
    /// between two calls, which is only a correct design at all if nothing else can interleave
    /// and observe or change it in between.
    /// </summary>
    private readonly SemaphoreSlim matchLock = new(1, 1);
    private readonly ContentCatalog content;
    private readonly LocalMatchOptions options;

    /// <summary>Seat -> join token, for every seat a human may occupy. Bot and empty seats have none.</summary>
    private readonly Dictionary<int, string> joinTokensBySeat;

    /// <summary>Seat -> the specific PlayFabId that alone may claim it, for matchmade seats. See <see cref="AcceptWithPlayFabAsync"/>.</summary>
    private readonly IReadOnlyDictionary<int, string> playFabIdBySeat;

    private readonly LTW.MatchServer.PlayFab.PlayFabSessionAuthority? playFabAuthority;

    private readonly Dictionary<int, WebSocket> connectionsById = new();
    private readonly Dictionary<int, PlayerId> seatByConnectionId = new();
    private int nextConnectionId;

    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private readonly string replayDirectory;
    private bool replayWritten;

    private CancellationTokenSource? loopCancellation;

    /// <summary>
    /// Resolves once <see cref="RunLoopAsync"/> actually stops — whether from <see cref="Stop"/>
    /// (manual/Ctrl+C, in standalone mode) or from the match itself ending (see the
    /// <c>slice.MatchSummary</c> check inside the loop). PlayFab MPS's GSDK integration (MP-07)
    /// awaits this to know when its one hosted match is over and the process should exit — see
    /// docs/MULTIPLAYER_ROLLOUT.md's MP-07.
    /// </summary>
    public Task Completion => loopCompletion.Task;

    private readonly TaskCompletionSource loopCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Told about every seat bind/disconnect purely for MP-07's GSDK integration
    /// (<c>GameserverSDK.UpdateConnectedPlayers</c> is informational-only reporting to PlayFab —
    /// <see cref="AcceptWithPlayFabAsync"/>'s ticket check remains the only real enforcement).
    /// Plain BCL-typed delegates, not a GSDK reference, so this class and its tests stay
    /// independent of whether GSDK is even referenced by the process hosting it.
    /// </summary>
    private readonly Action<int, PlayerId>? onSeatBound;

    private readonly Action<int>? onSeatDisconnected;

    public ServerMatch(
        string matchId,
        ContentCatalog content,
        LocalMatchOptions options,
        IReadOnlyCollection<int> humanSeats,
        string replayDirectory,
        double ticksPerSecond = DefaultTicksPerSecond,
        IReadOnlyDictionary<int, string>? playFabIdBySeat = null,
        LTW.MatchServer.PlayFab.PlayFabSessionAuthority? playFabAuthority = null,
        double openingBuildWindowSeconds = DefaultOpeningBuildWindowSeconds,
        Action<int, PlayerId>? onSeatBound = null,
        Action<int>? onSeatDisconnected = null)
    {
        MatchId = matchId;
        this.content = content;
        this.options = options;
        this.replayDirectory = replayDirectory;
        this.ticksPerSecond = ticksPerSecond;
        openingBuildWindow = TimeSpan.FromSeconds(openingBuildWindowSeconds);
        this.playFabIdBySeat = playFabIdBySeat ?? new Dictionary<int, string>();
        this.playFabAuthority = playFabAuthority;
        this.onSeatBound = onSeatBound;
        this.onSeatDisconnected = onSeatDisconnected;
        authority = new ConnectionSeatAuthority();
        slice = new LocalVerticalSlice(content, options, enableBots: true, seatAuthority: authority);

        // A seat in playFabIdBySeat is claimed by PlayFab identity, not a token — see
        // AcceptWithPlayFabAsync. Everything else in humanSeats keeps MP-04's token scheme.
        joinTokensBySeat = humanSeats
            .Where(seat => !this.playFabIdBySeat.ContainsKey(seat))
            .ToDictionary(seat => seat, _ => Guid.NewGuid().ToString("N"));
    }

    /// <summary>The token a human seat must present to join — null for a bot, empty, or PlayFab-identified seat.</summary>
    public string? TokenFor(int seat) => joinTokensBySeat.TryGetValue(seat, out var token) ? token : null;

    public IReadOnlyDictionary<int, string> JoinTokens => joinTokensBySeat;

    /// <summary>Starts the fixed-tick loop. Safe to call once; a match with no humans yet still plays out bot-vs-bot.</summary>
    public void Start()
    {
        if (loopCancellation is not null)
        {
            return;
        }

        matchStartedAtUtc = DateTimeOffset.UtcNow;
        loopCancellation = new CancellationTokenSource();
        _ = RunLoopAsync(loopCancellation.Token);
    }

    public void Stop() => loopCancellation?.Cancel();

    private async Task RunLoopAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / ticksPerSecond));
        while (!cancellation.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellation))
        {
            TickMessage message;
            await matchLock.WaitAsync(cancellation);
            try
            {
                var remaining = openingBuildWindow - (DateTimeOffset.UtcNow - matchStartedAtUtc);
                if (remaining > TimeSpan.Zero)
                {
                    // Deliberately no AdvanceOneTick here: the whole point of the window is that
                    // nothing simulation-side happens yet (see openingBuildWindow's remarks) — bots
                    // take their opening turn inside a match's very first AdvanceOneTick call, so
                    // skipping the call skips that too. Placement commands still reach the slice
                    // normally through DispatchAsync, which never gates on this.
                    message = BuildTickMessage(Array.Empty<LTW.Simulation.Events.ISimulationEvent>(), isOpeningBuildCountdown: true, remaining.TotalSeconds);
                }
                else
                {
                    // No BeginClientRequest before this: every bot decision this tick resolves its
                    // own claimed seat as trusted, per ConnectionSeatAuthority's default state — see
                    // its own remarks for why treating "no request in flight" as "internal, trusted
                    // call" is deliberate rather than a hole.
                    slice.AdvanceOneTick();
                    var events = slice.DrainEvents();
                    message = BuildTickMessage(events, isOpeningBuildCountdown: false, remainingSeconds: 0);

                    if (slice.MatchSummary is not null && !replayWritten)
                    {
                        replayWritten = true;
                        await WriteReplayAsync();

                        // The match itself is over — stop the loop after this tick's message still
                        // goes out below. Before this, nothing ever stopped a finished match from
                        // ticking (and broadcasting) forever; MP-07's GSDK integration also needs
                        // the process to actually exit once its one hosted match ends, which starts
                        // here. Canceling now (rather than breaking directly) means the loop's own
                        // while-condition, not a second exit path, is what ends it — the next
                        // iteration's `!cancellation.IsCancellationRequested` short-circuits false
                        // before ever calling WaitForNextTickAsync on an already-canceled token.
                        loopCancellation?.Cancel();
                    }
                }
            }
            finally
            {
                matchLock.Release();
            }

            await BroadcastAsync(message);
        }

        loopCompletion.TrySetResult();
    }

    private TickMessage BuildTickMessage(IReadOnlyList<LTW.Simulation.Events.ISimulationEvent> events, bool isOpeningBuildCountdown, double remainingSeconds)
    {
        var snapshot = slice.GetSnapshot();
        return new TickMessage
        {
            Tick = snapshot.Tick.Value,
            Sequence = ++tickSequence,
            IsOpeningBuildCountdown = isOpeningBuildCountdown,
            OpeningBuildCountdownRemainingSeconds = remainingSeconds,
            Events = events.Select(simulationEvent => new EventDto
            {
                Kind = simulationEvent.GetType().Name,
                Data = simulationEvent,
            }).ToList(),
            Players = snapshot.Players.Players.Select(player => new PlayerSnapshotDto
            {
                PlayerId = player.PlayerId.Value,
                Gold = player.Gold.Amount,
                Income = player.Income.Amount,
                Lives = player.Lives.Amount,
                Eliminated = player.IsEliminated,
                ChosenTowerLine = player.ChosenTowerLine,
                TowerLineTiers = player.CopyTowerLineTiers(),
                SendCategoryTiers = player.CopySendCategoryTiers(),
                SendQueue = snapshot.SendQueueFor(player.PlayerId).Select(id => id.Value).ToArray(),
            }).ToList(),
            Towers = snapshot.Towers.Select(tower => new TowerSnapshotDto
            {
                EntityId = tower.EntityId.Value,
                TowerId = tower.TowerId.Value,
                OwnerId = tower.OwnerId.Value,
                LaneId = tower.LaneId.Value,
                X = tower.Position.X,
                Y = tower.Position.Y,
                Tier = tower.Tier,
            }).ToList(),
            Creeps = snapshot.Creeps.Select(creep => new CreepSnapshotDto
            {
                EntityId = creep.EntityId.Value,
                CreepId = creep.CreepId.Value,
                SenderId = creep.SenderId.Value,
                LaneId = creep.LaneId.Value,
                X = creep.Position.X,
                Y = creep.Position.Y,
                Health = creep.Health,
                MaxHealth = creep.MaxHealth,
                SpeedPerSecond = creep.SpeedPerSecond,
                NextX = creep.NextPosition.X,
                NextY = creep.NextPosition.Y,
                MovementProgress = creep.MovementProgress,
                EffectiveMovementCost = creep.EffectiveMovementCost,
                IsBraked = creep.IsBraked,
            }).ToList(),
        };
    }

    /// <summary>
    /// Server-side replay capture — MP-04's own deliverable. Written next to the match id so a
    /// desync report or a curated MP-02 recording can be pulled off disk by hand; a real service
    /// would ship this to durable storage instead of the local filesystem, which is out of scope
    /// for proving the mechanism works.
    /// </summary>
    private async Task WriteReplayAsync()
    {
        try
        {
            Directory.CreateDirectory(replayDirectory);
            var record = slice.GetMatchReplayRecord();
            var dto = new
            {
                record.Seed,
                record.ContentVersion,
                MapId = record.MapId.Value,
                Players = record.Players.Select(player => player.Value).ToArray(),
                CompletedAtTick = record.CompletedAtTick.Value,
                Commands = record.Commands.Select(command => new
                {
                    Tick = command.Tick.Value,
                    command.Sequence,
                    PlayerId = command.PlayerId.Value,
                    Kind = command.Kind.ToString(),
                    LaneId = command.LaneId?.Value,
                    ContentId = command.ContentId?.Value,
                    X = command.Position?.X,
                    Y = command.Position?.Y,
                    Category = command.Category?.ToString(),
                    command.CategoryIndex,
                    command.TargetTier,
                    command.Quantity,
                }).ToArray(),
            };
            var path = Path.Combine(replayDirectory, $"{MatchId}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(dto, json));
        }
        catch (IOException)
        {
            // Telemetry-only in this pass — a failed replay write must never take the match down
            // for the players still on it.
        }
    }

    /// <summary>
    /// Binds a new connection to a seat after checking its join token, and sends the welcome
    /// message. Returns the connection id to route future frames with, or null if the token was
    /// wrong — the caller closes the socket in that case.
    /// </summary>
    public async Task<int?> AcceptAsync(WebSocket socket, int seat, string token)
    {
        if (!joinTokensBySeat.TryGetValue(seat, out var expected) || expected != token)
        {
            return null;
        }

        return await BindAsync(socket, seat);
    }

    /// <summary>
    /// Joins a seat reserved by PlayFab identity (see <see cref="playFabIdBySeat"/>) rather than a
    /// join token — MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-05. Verifies the session ticket with
    /// PlayFab's own Server API before binding anything, so this can only ever succeed for the
    /// SPECIFIC PlayFabId a matchmaking result (or, today, whoever created the match) reserved
    /// that seat for — a valid ticket for a DIFFERENT PlayFab player must not be enough to claim it.
    /// </summary>
    public async Task<int?> AcceptWithPlayFabAsync(WebSocket socket, int seat, string sessionTicket)
    {
        if (playFabAuthority is null || !playFabIdBySeat.TryGetValue(seat, out var expectedPlayFabId))
        {
            return null;
        }

        var verifiedPlayFabId = await playFabAuthority.AuthenticateAsync(sessionTicket);
        if (verifiedPlayFabId is null || verifiedPlayFabId != expectedPlayFabId)
        {
            return null;
        }

        return await BindAsync(socket, seat);
    }

    /// <summary>
    /// Binds a socket to a seat, first evicting whatever connection already held it.
    /// </summary>
    /// <remarks>
    /// Found investigating reconnect: this had no "already bound" check at all — a second
    /// connection presenting a valid token/ticket for a seat that already had a live connection
    /// would simply bind alongside it, and both would go on receiving every broadcast and
    /// dispatching commands as that seat. Harmless by accident until reconnect made rebinding an
    /// already-bound seat a real, expected path (a network hole's stale socket has often not yet
    /// noticed it is dead when the replacement connection arrives) — at which point two live
    /// connections both acting as one player is exactly the desync reconnect is supposed to avoid.
    /// The evicted socket's own receive loop discovers the close and calls <see cref="Disconnect"/>
    /// on its own connection id, same as any other disconnect — this only forces that along.
    /// </remarks>
    private async Task<int> BindAsync(WebSocket socket, int seat)
    {
        var playerId = new PlayerId(seat);
        foreach (var staleConnectionId in seatByConnectionId.Where(entry => entry.Value.Equals(playerId)).Select(entry => entry.Key).ToArray())
        {
            if (connectionsById.TryGetValue(staleConnectionId, out var staleSocket) && staleSocket.State == WebSocketState.Open)
            {
                try
                {
                    // CloseOutputAsync, not CloseAsync: the whole scenario this exists for is a
                    // stale, half-dead socket whose OWN receive loop has not noticed it is dead yet
                    // — exactly the case where nothing is left reading on that end to answer a full
                    // close handshake. CloseAsync waits for that answering close frame before
                    // returning; against a truly stale peer that wait never resolves, which would
                    // hang THIS bind (and so the reconnecting client's own welcome) indefinitely.
                    // Found by a test using a real but idle peer socket, not live traffic — same
                    // shape of bug either way.
                    await staleSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "reconnected from elsewhere", CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    // Already on its way down — the point was to make sure it stops being live for
                    // this seat, not that this specific close frame lands.
                }
            }

            Disconnect(staleConnectionId);
        }

        var connectionId = nextConnectionId++;
        connectionsById[connectionId] = socket;
        seatByConnectionId[connectionId] = playerId;
        authority.BindConnection(connectionId, playerId);
        onSeatBound?.Invoke(seat, playerId);

        await SendAsync(socket, new WelcomeMessage { Seat = seat, Tick = slice.GetSnapshot().Tick.Value });
        return connectionId;
    }

    public void Disconnect(int connectionId)
    {
        connectionsById.Remove(connectionId);
        if (seatByConnectionId.Remove(connectionId, out var playerId))
        {
            onSeatDisconnected?.Invoke(playerId.Value);
        }

        authority.ForgetConnection(connectionId);
    }

    /// <summary>
    /// Routes one frame from an already-accepted connection into the match. The claimed seat in
    /// the message is never trusted for anything:
    /// <see cref="ConnectionSeatAuthority.BeginClientRequest"/> fixes which seat this connection
    /// is actually allowed to act as before the real <c>LocalVerticalSlice</c> call happens, and
    /// every command method resolves through it — see <see cref="ConnectionSeatAuthority"/>'s own
    /// remarks. Holds <see cref="matchLock"/> for the same reason the tick loop does: this and a
    /// tick's own bot turns must never interleave.
    /// </summary>
    public async Task DispatchAsync(int connectionId, string frame)
    {
        if (!connectionsById.TryGetValue(connectionId, out var socket))
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(frame);
        }
        catch (JsonException)
        {
            await SendAsync(socket, new ErrorMessage { Message = "malformed json" });
            return;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("type", out var typeProperty))
            {
                await SendAsync(socket, new ErrorMessage { Message = "missing 'type'" });
                return;
            }

            var type = typeProperty.GetString();
            var claimed = seatByConnectionId.TryGetValue(connectionId, out var seat) ? seat : new PlayerId(0);
            var raw = document.RootElement.GetRawText();

            VerticalSliceCommandResult? result;
            string? commandId;
            await matchLock.WaitAsync();
            try
            {
                authority.BeginClientRequest(connectionId);
                switch (type)
                {
                    case "placeTower":
                        (result, commandId) = Handle(claimed, JsonSerializer.Deserialize<PlaceTowerMessage>(raw, json));
                        break;
                    case "queueSend":
                        (result, commandId) = Handle(claimed, JsonSerializer.Deserialize<QueueSendMessage>(raw, json));
                        break;
                    case "enqueueSend":
                        (result, commandId) = Handle(claimed, JsonSerializer.Deserialize<EnqueueSendMessage>(raw, json));
                        break;
                    case "buyCategoryTier":
                        (result, commandId) = Handle(claimed, JsonSerializer.Deserialize<BuyCategoryTierMessage>(raw, json));
                        break;
                    case "upgradeTower":
                        (result, commandId) = Handle(claimed, JsonSerializer.Deserialize<UpgradeTowerMessage>(raw, json));
                        break;
                    case "sellTower":
                        (result, commandId) = Handle(claimed, JsonSerializer.Deserialize<SellTowerMessage>(raw, json));
                        break;
                    default:
                        result = null;
                        commandId = null;
                        break;
                }
            }
            finally
            {
                authority.EndRequest();
                matchLock.Release();
            }

            if (result is null)
            {
                await SendAsync(socket, new ErrorMessage { Message = $"unknown or malformed message: '{type}'" });
                return;
            }

            await SendAsync(socket, new CommandResultMessage
            {
                Id = commandId,
                Accepted = result.Accepted,
                RejectionReason = result.Accepted ? null : result.RejectionReason.ToString(),
            });
        }
    }

    private (VerticalSliceCommandResult, string?) Handle(PlayerId claimed, PlaceTowerMessage? message)
    {
        if (message is null)
        {
            return (VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidContentId), null);
        }

        var result = slice.PlaceTower(claimed, new LaneId(message.LaneId), new ContentId(message.TowerId), new GridPosition(message.X, message.Y));
        return (result, message.Id);
    }

    private (VerticalSliceCommandResult, string?) Handle(PlayerId claimed, QueueSendMessage? message)
    {
        if (message is null)
        {
            return (VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidContentId), null);
        }

        var result = slice.QueueSend(claimed, new ContentId(message.CreepId), message.Quantity);
        return (result, message.Id);
    }

    private (VerticalSliceCommandResult, string?) Handle(PlayerId claimed, EnqueueSendMessage? message)
    {
        if (message is null)
        {
            return (VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidContentId), null);
        }

        var result = slice.EnqueueSend(claimed, new ContentId(message.CreepId));
        return (result, message.Id);
    }

    private (VerticalSliceCommandResult, string?) Handle(PlayerId claimed, BuyCategoryTierMessage? message)
    {
        if (message is null)
        {
            return (VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidContentId), null);
        }

        var result = slice.BuyCategoryTier(claimed, (CategoryKind)message.CategoryKind, message.CategoryIndex, message.TargetTier);
        return (result, message.Id);
    }

    private (VerticalSliceCommandResult, string?) Handle(PlayerId claimed, UpgradeTowerMessage? message)
    {
        if (message is null)
        {
            return (VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidContentId), null);
        }

        var result = slice.UpgradeTower(claimed, new LaneId(message.LaneId), new GridPosition(message.X, message.Y));
        return (result, message.Id);
    }

    private (VerticalSliceCommandResult, string?) Handle(PlayerId claimed, SellTowerMessage? message)
    {
        if (message is null)
        {
            return (VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidContentId), null);
        }

        var result = slice.SellTowerAt(claimed, new LaneId(message.LaneId), new GridPosition(message.X, message.Y));
        return (result, message.Id);
    }

    private async Task BroadcastAsync<T>(T message)
    {
        foreach (var socket in connectionsById.Values.ToArray())
        {
            await SendAsync(socket, message);
        }
    }

    private async Task SendAsync<T>(WebSocket socket, T message)
    {
        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, json);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // A dropped connection surfaces on its own receive loop, which removes it from
            // connectionsById — nothing else to do about a broadcast that lost the race with a
            // client going away mid-send.
        }
    }
}
