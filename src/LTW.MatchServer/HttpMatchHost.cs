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
public sealed class HttpMatchHost
{
    private readonly HttpListener listener = new();
    private readonly MatchRegistry registry;
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private CancellationTokenSource? cancellation;

    public HttpMatchHost(MatchRegistry registry, string prefix)
    {
        this.registry = registry;
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

            if (context.Request.HttpMethod == "POST" && context.Request.Url?.AbsolutePath == "/matches")
            {
                await HandleCreateMatchAsync(context);
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.Close();
        }
        catch (Exception)
        {
            // A single request's failure must not take the listener down. Real telemetry belongs
            // here before this leaves a single local process — see MP-07 in the rollout plan.
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

    private async Task HandleCreateMatchAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        var request = string.IsNullOrWhiteSpace(body)
            ? new CreateMatchRequest()
            : JsonSerializer.Deserialize<CreateMatchRequest>(body, json) ?? new CreateMatchRequest();

        var match = registry.CreateMatch(request.HumanSeats ?? new List<int> { 1 }, request.TicksPerSecond, request.PlayFabSeats, request.OpeningBuildWindowSeconds);
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

        var match = matchId is not null ? registry.Find(matchId) : null;
        if (match is null || !int.TryParse(seatText, out var seat) || (token is null && playFabTicket is null))
        {
            context.Response.StatusCode = 400;
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
            await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "join failed", CancellationToken.None);
            return;
        }

        if (connectionId is null)
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "bad token", CancellationToken.None);
            return;
        }

        await ReceiveLoopAsync(match, connectionId.Value, socket);
    }

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
                }
                while (!result.EndOfMessage);

                await match.DispatchAsync(connectionId, Encoding.UTF8.GetString(message.ToArray()));
            }
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            match.Disconnect(connectionId);
        }
    }

    private sealed class CreateMatchRequest
    {
        public List<int>? HumanSeats { get; set; }

        /// <summary>Real clients never set this — it exists so an integration test can play a
        /// match out in seconds instead of real-time minutes. See ServerMatch's own remarks.</summary>
        public double? TicksPerSecond { get; set; }

        /// <summary>Seat number -> the PlayFabId that alone may claim it. See
        /// MatchRegistry.CreateMatch and docs/MULTIPLAYER_ROLLOUT.md's MP-05.</summary>
        public Dictionary<int, string>? PlayFabSeats { get; set; }

        /// <summary>Real clients never set this either — same reasoning as <see cref="TicksPerSecond"/>,
        /// so a test can prove the opening build window's behavior without a real 30 second wait.</summary>
        public double? OpeningBuildWindowSeconds { get; set; }
    }

    private sealed class CreateMatchResponse
    {
        public string MatchId { get; set; } = "";
        public Dictionary<string, string> Tokens { get; set; } = new();
    }
}
