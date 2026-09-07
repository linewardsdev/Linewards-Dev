using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
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
/// commands from every connection are naturally serialized once <see cref="matchLock"/> is held —
/// <see cref="ConnectionSeatAuthority"/> answers the separate seat-spoofing question. This
/// previously claimed there was "no cross-connection race to resolve here" at all, which was false
/// in practice: <see cref="BindAsync"/>/<see cref="DisconnectAsync"/> mutated the connection tables
/// without taking the lock, racing the tick loop's own broadcast — see
/// docs/SECURITY_AUDIT_2026-09-05.md's H2 for the incident this correction exists because of.
/// </remarks>
public sealed class ServerMatch : IDisposable
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
    internal const double DefaultOpeningBuildWindowSeconds = 30;

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

    /// <summary>
    /// One send at a time per connection. Before this, the tick loop's own broadcast and a
    /// command's reply could both call <c>WebSocket.SendAsync</c> on the same socket
    /// concurrently — the BCL contract only allows one outstanding send per socket at a time; on
    /// some platforms this throws (killing the tick loop, see <see cref="Completion"/>'s own
    /// remarks on why that matters), and even where it doesn't, an interleaved write can reorder
    /// two whole JSON messages into the same frame. See docs/SECURITY_AUDIT_2026-09-05.md's M-S4.
    /// </summary>
    private readonly Dictionary<int, SemaphoreSlim> sendLocksByConnectionId = new();
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

    /// <summary>Captured only for the "match_started" telemetry event — see <see cref="EmitTelemetry"/>.</summary>
    private readonly int humanSeatCount;

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
        humanSeatCount = humanSeats.Count;
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
        EmitTelemetry("match_started", new { humanSeatCount, ticksPerSecond });
        _ = RunLoopAsync(loopCancellation.Token);
    }

    public void Stop() => loopCancellation?.Cancel();

    /// <summary>
    /// One structured JSON line per lifecycle event, to stdout — the same sink this class and
    /// <c>HttpMatchHost</c> already use for every other operational log line (<c>Console.Error.WriteLine</c>
    /// for faults, <c>Console.WriteLine</c> for the MPS bootstrap message in <c>Program.cs</c>),
    /// deliberately not <c>GameserverSDK.LogMessage</c>: that requires <c>GameserverSDK.Start()</c>
    /// to have run first, which standalone mode never calls, and this class has no way to tell
    /// which mode it is running under. stdout works in both — MPS-mode PlayFab log collection and
    /// standalone-mode `docker logs` both already capture it, per <c>Program.cs</c>'s own bootstrap
    /// message doing exactly this today.
    /// </summary>
    /// <remarks>
    /// This is the emission side of MP-07's telemetry deliverable, not the deliverable itself — a
    /// real "cost, crash, desync and abuse figures on one dashboard" needs something ingesting
    /// these lines (Azure Monitor, Application Insights, or similar), which needs real Azure
    /// resources this environment cannot provision. What this DOES give: every match's start/end/
    /// fault is now a structured, greppable, machine-parseable line instead of prose scattered
    /// across the existing free-text log lines. See docs/MULTIPLAYER_ROLLOUT.md's MP-07.
    /// </remarks>
    private static readonly JsonSerializerOptions TelemetryJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Where a telemetry line actually goes — a swappable static rather than a hardcoded
    /// <c>Console.WriteLine</c> call so a test can capture emitted lines without redirecting the
    /// real <see cref="Console"/>, which would risk interleaving with unrelated output from other
    /// tests running concurrently in the same process. Defaults to <see cref="Console.WriteLine(string)"/>
    /// in every real path.
    /// </summary>
    public static Action<string> TelemetrySink { get; set; } = Console.WriteLine;

    private void EmitTelemetry(string eventName, object details)
    {
        var line = JsonSerializer.Serialize(new
        {
            @event = eventName,
            timestamp = DateTimeOffset.UtcNow,
            matchId = MatchId,
            details,
        }, TelemetryJson);
        TelemetrySink(line);
    }

    /// <summary>
    /// Before this fix, the loop had no <c>try/finally</c> at all: <see cref="Stop"/> canceling
    /// mid-wait threw <see cref="OperationCanceledException"/> straight out of the fire-and-forget
    /// task (see <see cref="Start"/>), skipping <see cref="loopCompletion"/>'s own
    /// <c>TrySetResult</c> entirely — contradicting this class's own doc comment that
    /// <see cref="Completion"/> resolves "whether from <see cref="Stop"/> ... or from the match
    /// itself ending." A bad <c>ticksPerSecond</c> producing an invalid <see cref="PeriodicTimer"/>
    /// interval, or <see cref="WriteReplayAsync"/> throwing anything other than
    /// <see cref="IOException"/>, hit the same gap. Under PlayFab Multiplayer Servers this hangs
    /// <c>Program.cs</c>'s <c>Task.WhenAny(exit.Task, currentMatch.Completion)</c> forever, and the
    /// health callback (which checks <c>Completion.IsCompleted</c>) reports a dead match healthy
    /// indefinitely. See docs/SECURITY_AUDIT_2026-09-05.md's H4.
    /// </summary>
    private async Task RunLoopAsync(CancellationToken cancellation)
    {
        try
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
                        // Deliberately no AdvanceOneTick here: the whole point of the window is
                        // that nothing simulation-side happens yet (see openingBuildWindow's
                        // remarks) — bots take their opening turn inside a match's very first
                        // AdvanceOneTick call, so skipping the call skips that too. Placement
                        // commands still reach the slice normally through DispatchAsync, which
                        // never gates on this.
                        message = BuildTickMessage(Array.Empty<LTW.Simulation.Events.ISimulationEvent>(), isOpeningBuildCountdown: true, remaining.TotalSeconds);
                    }
                    else
                    {
                        // No BeginClientRequest before this: every bot decision this tick resolves
                        // its own claimed seat as trusted, per ConnectionSeatAuthority's default
                        // state — see its own remarks for why treating "no request in flight" as
                        // "internal, trusted call" is deliberate rather than a hole.
                        slice.AdvanceOneTick();
                        var events = slice.DrainEvents();
                        message = BuildTickMessage(events, isOpeningBuildCountdown: false, remainingSeconds: 0);

                        if (slice.MatchSummary is not null && !replayWritten)
                        {
                            replayWritten = true;
                            EmitTelemetry("match_ended", new
                            {
                                winnerId = slice.MatchSummary.WinnerId.Value,
                                completedAtTick = slice.MatchSummary.CompletedAtTick.Value,
                                durationSeconds = (DateTimeOffset.UtcNow - matchStartedAtUtc).TotalSeconds,
                                humanSeatCount,
                            });
                            await WriteReplayAsync();

                            // The match itself is over — stop the loop after this tick's message
                            // still goes out below. Before this, nothing ever stopped a finished
                            // match from ticking (and broadcasting) forever; MP-07's GSDK
                            // integration also needs the process to actually exit once its one
                            // hosted match ends, which starts here. Canceling now (rather than
                            // breaking directly) means the loop's own while-condition, not a
                            // second exit path, is what ends it — the next iteration's
                            // `!cancellation.IsCancellationRequested` short-circuits false before
                            // ever calling WaitForNextTickAsync on an already-canceled token.
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
        catch (OperationCanceledException)
        {
            // Stop() canceling while WaitForNextTickAsync or matchLock.WaitAsync was already in
            // flight — both only ever observe THIS match's own cancellation token (never an
            // unrelated timeout), so this is always an expected exit, never a fault.
            loopCompletion.TrySetResult();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ServerMatch {MatchId}: tick loop faulted and stopped: {exception}");
            EmitTelemetry("match_faulted", new
            {
                exceptionType = exception.GetType().Name,
                message = exception.Message,
                durationSeconds = (DateTimeOffset.UtcNow - matchStartedAtUtc).TotalSeconds,
            });
            loopCompletion.TrySetException(exception);
        }
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
            // Path.GetFileName, not the raw MatchId, as the file name: under MPS, MatchId is the
            // client-originated SessionId (see MatchRegistry.CreateMatch's own matchId parameter)
            // — a Path.Combine second argument that happens to be rooted (e.g. "/etc/x.json")
            // replaces the whole directory outright rather than being appended to it. GetFileName
            // strips any directory component from either a relative traversal or an absolute path,
            // so the write can only ever land inside replayDirectory. See
            // docs/SECURITY_AUDIT_2026-09-05.md's L1.
            var path = Path.Combine(replayDirectory, Path.GetFileName($"{MatchId}.json"));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(dto, json));
        }
        catch (Exception exception)
        {
            // Telemetry-only in this pass — a failed replay write must never take the match down
            // for the players still on it. Broadened from IOException: Directory.CreateDirectory
            // and File.WriteAllTextAsync can also throw UnauthorizedAccessException (a read-only
            // or non-writable directory), and JsonSerializer.Serialize can throw
            // NotSupportedException — either previously escaped this catch entirely and faulted
            // the tick loop mid-match-end, before RunLoopAsync's own H4 fix existed to catch it.
            // See docs/SECURITY_AUDIT_2026-09-05.md's H4.
            Console.Error.WriteLine($"ServerMatch {MatchId}: replay write failed: {exception}");
        }
    }

    /// <summary>
    /// Binds a new connection to a seat after checking its join token, and sends the welcome
    /// message. Returns the connection id to route future frames with, or null if the token was
    /// wrong — the caller closes the socket in that case.
    /// </summary>
    public async Task<int?> AcceptAsync(WebSocket socket, int seat, string token)
    {
        // FixedTimeEquals, not `!=` — a 128-bit GUID token makes this impractical to actually
        // exploit, but the fix is one line. See docs/SECURITY_AUDIT_2026-09-05.md's L4.
        if (!joinTokensBySeat.TryGetValue(seat, out var expected)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(token)))
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
    /// <summary>
    /// The dictionary mutations below (and the read in the eviction loop) run under
    /// <see cref="matchLock"/> — before this fix they didn't, contradicting this class's own
    /// remarks that the lock "serializes every touch" of match state. A join racing the tick
    /// loop's own concurrent <c>BroadcastAsync</c> enumeration of <see cref="connectionsById"/>
    /// could throw (crashing the tick loop silently, see <see cref="Completion"/>'s own remarks)
    /// or, on two simultaneous joins landing on the same <see cref="nextConnectionId"/> value,
    /// bind one player's connection over another's. See docs/SECURITY_AUDIT_2026-09-05.md's H2.
    /// I/O (closing a stale socket, sending the welcome) deliberately happens AFTER the lock is
    /// released, the same way <see cref="DispatchAsync"/> already computes its result under the
    /// lock but sends the reply after releasing it — holding a lock across a slow or hung peer's
    /// socket write would stall the tick loop for everyone else (see H3's own fix for that
    /// specific risk on the broadcast path).
    /// </summary>
    private async Task<int> BindAsync(WebSocket socket, int seat)
    {
        var playerId = new PlayerId(seat);
        var staleSockets = new List<WebSocket>();
        var evictedPlayerIds = new List<PlayerId>();
        int connectionId;
        long welcomeTick;

        await matchLock.WaitAsync();
        try
        {
            foreach (var staleConnectionId in seatByConnectionId.Where(entry => entry.Value.Equals(playerId)).Select(entry => entry.Key).ToArray())
            {
                if (connectionsById.TryGetValue(staleConnectionId, out var staleSocket) && staleSocket.State == WebSocketState.Open)
                {
                    staleSockets.Add(staleSocket);
                }

                if (DisconnectLocked(staleConnectionId) is PlayerId evictedPlayerId)
                {
                    evictedPlayerIds.Add(evictedPlayerId);
                }
            }

            connectionId = nextConnectionId++;
            connectionsById[connectionId] = socket;
            seatByConnectionId[connectionId] = playerId;
            sendLocksByConnectionId[connectionId] = new SemaphoreSlim(1, 1);
            authority.BindConnection(connectionId, playerId);
            welcomeTick = slice.GetSnapshot().Tick.Value;
        }
        finally
        {
            matchLock.Release();
        }

        foreach (var staleSocket in staleSockets)
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

        foreach (var evictedPlayerId in evictedPlayerIds)
        {
            onSeatDisconnected?.Invoke(evictedPlayerId.Value);
        }

        onSeatBound?.Invoke(seat, playerId);
        await SendAsync(connectionId, socket, new WelcomeMessage { Seat = seat, Tick = welcomeTick });
        return connectionId;
    }

    /// <summary>
    /// Acquires <see cref="matchLock"/> itself — for a receive loop's own disconnect (its
    /// <c>finally</c>, which holds no lock of its own). <see cref="BindAsync"/> evicting a stale
    /// connection for the same seat uses <see cref="DisconnectLocked"/> directly instead, since it
    /// already holds the lock and <see cref="SemaphoreSlim"/> is not reentrant.
    /// </summary>
    public async Task DisconnectAsync(int connectionId)
    {
        PlayerId? removedPlayerId;
        await matchLock.WaitAsync();
        try
        {
            removedPlayerId = DisconnectLocked(connectionId);
        }
        finally
        {
            matchLock.Release();
        }

        if (removedPlayerId is PlayerId playerId)
        {
            onSeatDisconnected?.Invoke(playerId.Value);
        }
    }

    /// <summary>
    /// Releases <see cref="matchLock"/> and the tick loop's own cancellation source. Only safe to
    /// call once <see cref="Completion"/> has resolved and <see cref="CloseAllConnectionsAsync"/>
    /// has already run (see <see cref="MatchRegistry.CreateMatch"/>'s eviction continuation, the
    /// only caller) — every other method on this class that touches <see cref="matchLock"/> only
    /// ever runs while the match is still findable through the registry, and eviction removes it
    /// from the registry before this is called. See docs/SECURITY_AUDIT_2026-09-05.md's L5.
    /// </summary>
    public void Dispose()
    {
        loopCancellation?.Dispose();
        matchLock.Dispose();
    }

    /// <summary>
    /// Closes every connection this match still holds — called once <see cref="Completion"/>
    /// resolves (see <see cref="MatchRegistry.CreateMatch"/>), so a finished standalone-mode
    /// match does not leave its sockets open forever alongside its now-orphaned dictionary entry.
    /// See docs/SECURITY_AUDIT_2026-09-05.md's M3.
    /// </summary>
    public async Task CloseAllConnectionsAsync()
    {
        KeyValuePair<int, WebSocket>[] connections;
        await matchLock.WaitAsync();
        try
        {
            connections = connectionsById.ToArray();
            connectionsById.Clear();
            seatByConnectionId.Clear();
            sendLocksByConnectionId.Clear();
        }
        finally
        {
            matchLock.Release();
        }

        foreach (var (connectionId, socket) in connections)
        {
            authority.ForgetConnection(connectionId);
            if (socket.State != WebSocketState.Open)
            {
                continue;
            }

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "match ended", timeout.Token);
            }
            catch (Exception)
            {
                // Best-effort: the match is gone regardless, and a peer that never acknowledges
                // (or is already gone itself) must not hold this loop open.
                socket.Abort();
            }
        }
    }

    private PlayerId? DisconnectLocked(int connectionId)
    {
        connectionsById.Remove(connectionId);
        var removedPlayerId = seatByConnectionId.Remove(connectionId, out var playerId) ? (PlayerId?)playerId : null;

        // Deliberately NOT disposed here: a concurrent SendAsync for this same connectionId may
        // already hold a reference to this exact SemaphoreSlim (captured before this disconnect
        // acquired matchLock) and still be using it — disposing out from under that would throw
        // ObjectDisposedException from a completely unrelated send. Removing it from the dictionary
        // is enough to stop any FUTURE send from finding it; the object itself is reclaimed by GC
        // once the in-flight send (if any) finishes and drops its own reference.
        sendLocksByConnectionId.Remove(connectionId);

        authority.ForgetConnection(connectionId);
        return removedPlayerId;
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
            await SendAsync(connectionId, socket, new ErrorMessage { Message = "malformed json" });
            return;
        }

        using (document)
        {
            string? type;
            VerticalSliceCommandResult? result;
            string? commandId;
            try
            {
                if (!document.RootElement.TryGetProperty("type", out var typeProperty) || typeProperty.ValueKind != JsonValueKind.String)
                {
                    await SendAsync(connectionId, socket, new ErrorMessage { Message = "missing or non-string 'type'" });
                    return;
                }

                type = typeProperty.GetString();

                if (!seatByConnectionId.TryGetValue(connectionId, out var claimed))
                {
                    // Previously fell back to `new PlayerId(0)` here — PlayerId's own constructor
                    // rejects non-positive values, so that "sentinel" threw the instant it ran
                    // rather than ever standing in for "no seat claimed". See
                    // docs/SECURITY_AUDIT_2026-09-05.md's M2.
                    await SendAsync(connectionId, socket, new ErrorMessage { Message = "connection is not bound to a seat" });
                    return;
                }

                var raw = document.RootElement.GetRawText();

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
                        case "cancelSend":
                            (result, commandId) = Handle(claimed, JsonSerializer.Deserialize<CancelSendMessage>(raw, json));
                            break;
                        case "clearSendQueue":
                            (result, commandId) = Handle(claimed, JsonSerializer.Deserialize<ClearSendQueueMessage>(raw, json));
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
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
            {
                // A well-formed-JSON-but-invalid frame (wrong-typed fields, an omitted
                // towerId/creepId defaulting to "" -> ContentId's own constructor rejecting it, a
                // non-positive laneId, negative coordinates, ...) used to throw straight out of
                // DispatchAsync uncaught. ReceiveLoopAsync's only catch was WebSocketException, so
                // a buggy-but-legitimate client silently stopped receiving ticks with no error and
                // no close. See docs/SECURITY_AUDIT_2026-09-05.md's M2.
                Console.Error.WriteLine($"ServerMatch: malformed message from connection {connectionId}: {exception.Message}");
                await SendAsync(connectionId, socket, new ErrorMessage { Message = "malformed message" });
                return;
            }

            if (result is null)
            {
                await SendAsync(connectionId, socket, new ErrorMessage { Message = $"unknown or malformed message: '{type}'" });
                return;
            }

            await SendAsync(connectionId, socket, new CommandResultMessage
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

    private (VerticalSliceCommandResult, string?) Handle(PlayerId claimed, CancelSendMessage? message)
    {
        if (message is null)
        {
            return (VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidContentId), null);
        }

        var result = slice.CancelQueuedSend(claimed, new ContentId(message.CreepId));
        return (result, message.Id);
    }

    private (VerticalSliceCommandResult, string?) Handle(PlayerId claimed, ClearSendQueueMessage? message)
    {
        // Deliberately always Accept, matching UnityCommandAdapter.ClearSendQueue's own local-play
        // contract: an already-empty queue is a normal state, not a refusal, so there is no
        // rejection reason to report here regardless of how many entries actually went.
        slice.ClearSendQueue(claimed);
        return (VerticalSliceCommandResult.Accept(), message?.Id);
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
        // Snapshotting under matchLock, not just ToArray()-ing the live dictionary unlocked: this
        // runs after RunLoopAsync has already released the lock (broadcasting must not hold it —
        // see SendAsync's own timeout for why), so without this the snapshot itself would race
        // BindAsync/DisconnectAsync mutating connectionsById concurrently. See
        // docs/SECURITY_AUDIT_2026-09-05.md's H2.
        KeyValuePair<int, WebSocket>[] connections;
        await matchLock.WaitAsync();
        try
        {
            connections = connectionsById.ToArray();
        }
        finally
        {
            matchLock.Release();
        }

        foreach (var (connectionId, socket) in connections)
        {
            await SendAsync(connectionId, socket, message);
        }
    }

    /// <summary>
    /// Bounds a single send. Before this, a player who opened a socket and simply stopped reading
    /// would eventually fill their own TCP receive window; the next send to them then blocked
    /// forever, and since <see cref="BroadcastAsync{T}"/> awaits each send in turn, EVERY other
    /// player's match froze too — a single unresponsive peer, no exploit needed beyond not
    /// reading. See docs/SECURITY_AUDIT_2026-09-05.md's H3.
    /// </summary>
    private const int SendTimeoutSeconds = 2;

    private async Task SendAsync<T>(int connectionId, WebSocket socket, T message)
    {
        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        // One send in flight per connection at a time — see sendLocksByConnectionId's own remarks
        // for why (docs/SECURITY_AUDIT_2026-09-05.md's M-S4). Missing means this connection has
        // already been disconnected (a race between this call being queued and that disconnect
        // landing first); nothing to send to.
        if (!sendLocksByConnectionId.TryGetValue(connectionId, out var sendLock))
        {
            return;
        }

        await sendLock.WaitAsync();
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, json);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(SendTimeoutSeconds));
            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
            }
            catch (WebSocketException)
            {
                // A dropped connection surfaces on its own receive loop, which removes it from
                // connectionsById — nothing else to do about a broadcast that lost the race with a
                // client going away mid-send.
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                // The peer stopped reading — this send would otherwise have blocked every other
                // player's broadcast indefinitely. Abort rather than attempt a graceful close: a
                // peer this unresponsive is not going to answer a close handshake either. The abort
                // makes the peer's own pending ReceiveAsync throw, which its receive loop's
                // existing catch+finally already turns into a normal disconnect — no separate
                // cleanup needed here.
                socket.Abort();
            }
        }
        finally
        {
            sendLock.Release();
        }
    }
}
