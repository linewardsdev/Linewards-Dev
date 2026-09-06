using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace LTW.MatchServer;

/// <summary>
/// The whole transport: <see cref="HttpListener"/> plus the BCL's own
/// <see cref="System.Net.WebSockets.WebSocket"/> upgrade support, deliberately in place of a web
/// framework. `MVP_DEPENDENCIES.md` rule 4 is "prefer small, well-supported dependencies over
/// framework stacks" — this whole transport is base class library, not one added package, for a
/// job (accept a WebSocket, route a handful of JSON message types) that does not need one.
/// </summary>
/// <remarks>
/// Two routes, both intentionally minimal — there is no matchmaking, no accounts, and no
/// production hardening (TLS termination, auth beyond the join token, rate limiting at the
/// transport level) here. See docs/MULTIPLAYER_ROLLOUT.md's MP-04 for what is and is not proven
/// by this pass:
///
///   POST /matches                         body: {"humanSeats":[1,2]} -> {"matchId","tokens"}
///   GET  /matches/{id}/join?seat=N&amp;token=T -> upgrades to a WebSocket bound to seat N
/// </remarks>
public sealed class HttpMatchHost : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly MatchRegistry registry;
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private readonly bool allowMatchCreation;
    private readonly string? createSecret;
    private CancellationTokenSource? cancellation;

    /// <summary>
    /// <paramref name="allowMatchCreation"/> is false under PlayFab Multiplayer Servers (MP-07):
    /// that process is reachable at a real address for the life of exactly one PlayFab-allocated
    /// match, and leaving <c>POST /matches</c> live would let anything that can reach it create a
    /// second, PlayFab-invisible match sharing the process — see
    /// docs/MULTIPLAYER_ROLLOUT.md's MP-07.
    /// </summary>
    /// <param name="createSecret">
    /// When set, <c>POST /matches</c> requires a matching <c>X-Match-Create-Secret</c> header —
    /// standalone mode otherwise has no auth at all in front of unbounded match creation. Null
    /// (the default, and every caller today) preserves the existing open behavior for local/dev
    /// use and the test suite. See docs/SECURITY_AUDIT_2026-09-05.md's M3.
    /// </param>
    public HttpMatchHost(MatchRegistry registry, string prefix, bool allowMatchCreation = true, string? createSecret = null)
    {
        this.registry = registry;
        this.allowMatchCreation = allowMatchCreation;
        this.createSecret = createSecret;
        listener.Prefixes.Add(prefix);
    }

    public void Start()
    {
        cancellation = new CancellationTokenSource();
        listener.Start();
        _ = AcceptLoopAsync(cancellation.Token);
    }

    public void Stop()
    {
        cancellation?.Cancel();
        listener.Stop();
    }

    /// <summary>
    /// Releases the listener and its cancellation source outright — <see cref="Stop"/> alone
    /// leaves both allocated. Safe to call after <see cref="Stop"/> (or instead of it, since this
    /// stops the accept loop too). See docs/SECURITY_AUDIT_2026-09-05.md's L5.
    /// </summary>
    public void Dispose()
    {
        Stop();
        cancellation?.Dispose();
        listener.Close();
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (token.IsCancellationRequested || !listener.IsListening)
            {
                return;
            }
            catch (Exception exception)
            {
                // Previously this exact shape of exception (one HttpListenerException.GetContextAsync
                // can genuinely raise for an aborted/bad handshake while still listening, per its own
                // documented behavior) had no catch clause at all — it faulted this fire-and-forget
                // loop silently (see Start()), and the server never accepted another connection
                // again with zero indication anything had gone wrong. Log and keep accepting instead
                // of tearing down the whole listener over one bad handshake. See
                // docs/SECURITY_AUDIT_2026-09-05.md's H4.
                Console.Error.WriteLine($"HttpMatchHost: accept failed, continuing: {exception}");
                continue;
            }

            _ = HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            if (context.Request.IsWebSocketRequest)
            {
                await HandleJoinAsync(context);
                return;
            }

            if (allowMatchCreation && context.Request.HttpMethod == "POST" && context.Request.Url?.AbsolutePath == "/matches")
            {
                await HandleCreateMatchAsync(context);
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.Close();
        }
        catch (Exception exception)
        {
            // A single request's failure must not take the listener down. Logged, not silent —
            // see docs/SECURITY_AUDIT_2026-09-05.md's H4; full telemetry is still MP-07 scope.
            Console.Error.WriteLine($"HttpMatchHost: request failed: {exception}");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>
    /// A real create-match body is a handful of small seat/PlayFabId entries — generous, not
    /// tight, matching the inbound WebSocket cap. Without this an unauthenticated caller could
    /// send an arbitrarily large body and OOM the process the same way as
    /// docs/SECURITY_AUDIT_2026-09-05.md's H1.
    /// </summary>
    private const long MaxCreateMatchBodyBytes = 64 * 1024;

    private async Task HandleCreateMatchAsync(HttpListenerContext context)
    {
        if (createSecret is not null && context.Request.Headers["X-Match-Create-Secret"] != createSecret)
        {
            context.Response.StatusCode = 401;
            context.Response.Close();
            return;
        }

        if (context.Request.ContentLength64 > MaxCreateMatchBodyBytes)
        {
            context.Response.StatusCode = 413;
            context.Response.Close();
            return;
        }

        string body;
        using (var reader = new StreamReader(context.Request.InputStream))
        {
            var buffer = new char[8192];
            var builder = new StringBuilder();
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                builder.Append(buffer, 0, read);
                if (builder.Length > MaxCreateMatchBodyBytes)
                {
                    context.Response.StatusCode = 413;
                    context.Response.Close();
                    return;
                }
            }

            body = builder.ToString();
        }

        var request = string.IsNullOrWhiteSpace(body)
            ? new CreateMatchRequest()
            : JsonSerializer.Deserialize<CreateMatchRequest>(body, json) ?? new CreateMatchRequest();

        var humanSeats = request.HumanSeats ?? new List<int> { 1 };
        if (!CreateMatchRequestValidator.TryValidate(humanSeats, request.PlayFabSeats, request.TicksPerSecond, request.OpeningBuildWindowSeconds, out var validationError))
        {
            context.Response.StatusCode = 400;
            var errorBody = Encoding.UTF8.GetBytes(validationError!);
            await context.Response.OutputStream.WriteAsync(errorBody);
            context.Response.Close();
            return;
        }

        ServerMatch match;
        try
        {
            match = registry.CreateMatch(humanSeats, request.TicksPerSecond, request.PlayFabSeats, request.OpeningBuildWindowSeconds);
        }
        catch (InvalidOperationException)
        {
            // MatchRegistry's own concurrent-match cap — see docs/SECURITY_AUDIT_2026-09-05.md's M3.
            context.Response.StatusCode = 503;
            context.Response.Close();
            return;
        }

        var responseBody = JsonSerializer.SerializeToUtf8Bytes(new CreateMatchResponse
        {
            MatchId = match.MatchId,
            Tokens = match.JoinTokens.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value),
        }, json);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;
        await context.Response.OutputStream.WriteAsync(responseBody);
        context.Response.Close();
    }

    /// <summary>
    /// Two ways to prove a seat claim, either a bare query string away: MP-04's own join token
    /// (<c>token=</c>), or MP-05's PlayFab session ticket (<c>playFabTicket=</c>), verified against
    /// PlayFab before anything is bound — see <see cref="ServerMatch.AcceptWithPlayFabAsync"/>.
    /// Neither is trusted here; both resolve inside <see cref="ServerMatch"/> itself.
    /// </summary>
    private async Task HandleJoinAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "";
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var matchId = segments.Length >= 2 && segments[0] == "matches" ? segments[1] : null;
        var seatText = context.Request.QueryString["seat"];
        var token = context.Request.QueryString["token"];
        var playFabTicket = context.Request.QueryString["playFabTicket"];

        // "current" resolves to "the only match this registry holds" instead of requiring an exact
        // id — only meaningful (and only offered) when match creation is disallowed, i.e. PlayFab
        // Multiplayer Servers mode, which already guarantees exactly one match per process. A
        // queue-matched client only ever learns PlayFab's own MatchId, not necessarily this
        // registry's internal id — see MatchRegistry.FindOnly's own remarks for why this sidesteps
        // that question entirely rather than assuming the two happen to be equal.
        var match = matchId == "current" && !allowMatchCreation
            ? registry.FindOnly()
            : matchId is not null ? registry.Find(matchId) : null;
        if (match is null || !int.TryParse(seatText, out var seat) || (token is null && playFabTicket is null))
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        // Before the WebSocket upgrade, specifically: a playFabTicket join otherwise triggers a
        // real, quota-limited PlayFab AuthenticateSessionTicket call regardless of whether the
        // ticket is garbage, and the upgrade itself has a cost (a live socket, a receive loop)
        // even for a token join. See docs/SECURITY_AUDIT_2026-09-05.md's M5.
        var remoteAddress = context.Request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
        if (!TryAcquireJoinSlot(remoteAddress))
        {
            context.Response.StatusCode = 429;
            context.Response.Close();
            return;
        }

        var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
        var socket = wsContext.WebSocket;
        int? connectionId;
        try
        {
            connectionId = playFabTicket is not null
                ? await match.AcceptWithPlayFabAsync(socket, seat, playFabTicket)
                : await match.AcceptAsync(socket, seat, token!);
        }
        catch (Exception)
        {
            // The WebSocket upgrade already happened by this point, so a thrown exception here
            // cannot become an HTTP error response — it must close the socket instead, or the
            // client is left waiting on a connection nothing will ever answer.
            await CloseFailedJoinAsync(socket, WebSocketCloseStatus.InternalServerError, "join failed");
            return;
        }

        if (connectionId is null)
        {
            await CloseFailedJoinAsync(socket, WebSocketCloseStatus.PolicyViolation, "bad token");
            return;
        }

        await ReceiveLoopAsync(match, connectionId.Value, socket);
    }

    /// <summary>
    /// Bounds join attempts per remote address — a fixed window, not exact under concurrent hits
    /// on the same address (the read-modify-write inside the lock is correct; what's approximate
    /// is only which requests land in which window at the boundary), which is fine for a coarse
    /// abuse guard. See docs/SECURITY_AUDIT_2026-09-05.md's M5.
    /// </summary>
    private const int MaxJoinAttemptsPerWindow = 20;

    private static readonly TimeSpan JoinRateLimitWindow = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, JoinWindow> joinAttemptsByAddress = new();

    private sealed class JoinWindow
    {
        public int Count;
        public DateTime WindowStart;
    }

    private bool TryAcquireJoinSlot(string address)
    {
        var now = DateTime.UtcNow;
        var window = joinAttemptsByAddress.GetOrAdd(address, _ => new JoinWindow { WindowStart = now });
        lock (window)
        {
            if (now - window.WindowStart >= JoinRateLimitWindow)
            {
                window.WindowStart = now;
                window.Count = 0;
            }

            if (window.Count >= MaxJoinAttemptsPerWindow)
            {
                return false;
            }

            window.Count++;
            return true;
        }
    }

    /// <summary>
    /// Sends a close frame without waiting for the peer's own close handshake reply, aborting
    /// instead if even that hangs — <see cref="WebSocket.CloseAsync"/>'s full handshake would
    /// otherwise let an unresponsive or hostile peer hold this join's resources open indefinitely.
    /// See docs/SECURITY_AUDIT_2026-09-05.md's M5.
    /// </summary>
    private static async Task CloseFailedJoinAsync(WebSocket socket, WebSocketCloseStatus status, string reason)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await socket.CloseOutputAsync(status, reason, timeout.Token);
        }
        catch (Exception)
        {
            socket.Abort();
        }
        finally
        {
            // See docs/SECURITY_AUDIT_2026-09-05.md's L5 — a rejected join never reached
            // ReceiveLoopAsync's own disposal, so nothing released this socket at all.
            socket.Dispose();
        }
    }

    /// <summary>
    /// Caps a single inbound message — without this, any client holding a valid token could send
    /// one multi-gigabyte message and OOM the whole process, since the receive loop below
    /// previously accumulated frames into an unbounded MemoryStream. Real client messages are well
    /// under 200 bytes; this is generous, not tight. See docs/SECURITY_AUDIT_2026-09-05.md's H1.
    /// </summary>
    private const int MaxInboundMessageBytes = 16 * 1024;

    private static async Task ReceiveLoopAsync(ServerMatch match, int connectionId, WebSocket socket)
    {
        var buffer = new byte[8192];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                    if (message.Length > MaxInboundMessageBytes)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too large", CancellationToken.None);
                        return;
                    }
                }
                while (!result.EndOfMessage);

                await match.DispatchAsync(connectionId, Encoding.UTF8.GetString(message.ToArray()));
            }
        }
        catch (WebSocketException)
        {
        }
        catch (Exception exception)
        {
            // A malformed-but-otherwise-legitimate frame can throw out of DispatchAsync (see
            // docs/SECURITY_AUDIT_2026-09-05.md's M2) — closing here rather than leaving the socket
            // silently orphaned. DispatchAsync's own guarding is the real fix; this is the backstop.
            Console.Error.WriteLine($"HttpMatchHost: receive loop failed, closing connection: {exception}");
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "malformed message", CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
        }
        finally
        {
            await match.DisconnectAsync(connectionId);

            // Every path above (a graceful close, a caught exception, or the loop simply exiting
            // because the socket stopped being Open on its own) left the WebSocket itself
            // undisposed — CloseAsync/CloseOutputAsync send a close frame but do not release the
            // underlying object. See docs/SECURITY_AUDIT_2026-09-05.md's L5.
            socket.Dispose();
        }
    }

    private sealed class CreateMatchResponse
    {
        public string MatchId { get; set; } = "";
        public Dictionary<string, string> Tokens { get; set; } = new();
    }
}
