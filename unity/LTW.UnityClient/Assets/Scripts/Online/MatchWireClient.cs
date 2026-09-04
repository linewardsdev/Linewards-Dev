#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LTW.UnityClient.Online.Wire;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// The Unity-side half of MP-06's wire protocol: owns one <see cref="ClientWebSocket"/>
    /// connection to <c>LTW.MatchServer</c>, matching the shapes in
    /// <c>LTW.MatchServer/Wire/ClientMessages.cs</c>/<c>ServerMessages.cs</c> field for field (see
    /// this project's own <c>Wire/</c> folder).
    /// </summary>
    /// <remarks>
    /// The actual socket I/O runs on background tasks (`ReceiveLoopAsync`), because
    /// <see cref="ClientWebSocket"/> has no synchronous API. Nothing from those tasks touches
    /// Unity state directly — every received frame is parsed just enough to route it, then queued,
    /// and <see cref="Pump"/> (called from <c>UnitySimulationDriver.Update</c>, i.e. the main
    /// thread) is the only place that updates <see cref="Welcome"/>/<see cref="LatestTick"/> or
    /// invokes the public events. This is the standard, safe pattern for a background-socket/
    /// main-thread-game-loop split — it avoids needing a SynchronizationContext dispatch trick, at
    /// the cost of up to one frame of latency between a frame arriving and it being acted on, which
    /// is immaterial next to real network latency.
    /// </remarks>
    /// <summary>What a command is predicted to do to the board, before the server confirms it — see
    /// <see cref="MatchWireClient.AddPendingPrediction"/>.</summary>
    public enum PendingPredictionKind
    {
        PlaceTower,
        SellTower,
        UpgradeTower,
    }

    /// <summary>
    /// One in-flight, unconfirmed local action. See docs/MULTIPLAYER_ROLLOUT.md's MP-06 for why
    /// this predicts only the LOCAL player's own actions rather than the whole match (bots,
    /// opponents, and combat always render from the server's own state, never predicted).
    /// </summary>
    public sealed class PendingPrediction
    {
        public string Id { get; set; } = "";
        public PendingPredictionKind Kind { get; set; }
        public int LaneId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>Only meaningful for <see cref="PendingPredictionKind.PlaceTower"/>.</summary>
        public string TowerId { get; set; } = "";
    }

    public sealed class MatchWireClient : IDisposable
    {
        private readonly ClientWebSocket socket = new();
        private readonly Uri joinUri;
        private readonly SemaphoreSlim sendLock = new(1, 1);
        private readonly ConcurrentQueue<string> inbox = new();
        private readonly CancellationTokenSource lifetime = new();
        private readonly JsonSerializerSettings jsonSettings = new() { NullValueHandling = NullValueHandling.Ignore };
        private readonly Dictionary<string, PendingPrediction> pendingPredictions = new();

        public WelcomeMessage? Welcome { get; private set; }

        public TickMessage? LatestTick { get; private set; }

        public bool IsConnected => socket.State == WebSocketState.Open;

        public event Action<CommandResultMessage>? OnCommandResult;

        /// <summary>Protocol-level errors AND disconnects — see <c>ErrorMessage</c>'s own remarks server-side.</summary>
        public event Action<string>? OnError;

        public MatchWireClient(Uri joinUri)
        {
            this.joinUri = joinUri;
        }

        public async Task ConnectAsync()
        {
            await socket.ConnectAsync(joinUri, lifetime.Token);
            _ = ReceiveLoopAsync();
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[16384];
            try
            {
                while (socket.State == WebSocketState.Open && !lifetime.IsCancellationRequested)
                {
                    using var message = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, lifetime.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            inbox.Enqueue(BuildDisconnectSentinel("server closed the connection"));
                            return;
                        }

                        message.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    inbox.Enqueue(Encoding.UTF8.GetString(message.ToArray()));
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown via Dispose — nothing to report.
            }
            catch (WebSocketException exception)
            {
                inbox.Enqueue(BuildDisconnectSentinel(exception.Message));
            }
        }

        private const string DisconnectSentinelType = "__disconnected";

        private static string BuildDisconnectSentinel(string reason) =>
            JsonConvert.SerializeObject(new { type = DisconnectSentinelType, message = reason });

        /// <summary>
        /// Drains whatever arrived since the last call and applies it. Call once per frame from the
        /// main thread — see this class's own remarks for why the split exists.
        /// </summary>
        public void Pump()
        {
            while (inbox.TryDequeue(out var frame))
            {
                JObject root;
                try
                {
                    root = JObject.Parse(frame);
                }
                catch (JsonException exception)
                {
                    Debug.LogWarning($"MatchWireClient: malformed frame from server: {exception.Message}");
                    continue;
                }

                var type = root["type"]?.Value<string>();
                switch (type)
                {
                    case ServerMessageType.Welcome:
                        Welcome = root.ToObject<WelcomeMessage>(JsonSerializer.Create(jsonSettings));
                        break;
                    case ServerMessageType.Tick:
                        LatestTick = root.ToObject<TickMessage>(JsonSerializer.Create(jsonSettings));
                        break;
                    case ServerMessageType.CommandResult:
                        var result = root.ToObject<CommandResultMessage>(JsonSerializer.Create(jsonSettings));
                        if (result is not null)
                        {
                            // Resolved either way: an accepted command's real effect will already
                            // be in the very next TickMessage (ServerMatch applies commands
                            // synchronously, under the same lock the tick loop uses), and a
                            // rejected one should simply stop showing its optimistic ghost.
                            if (result.Id is not null)
                            {
                                pendingPredictions.Remove(result.Id);
                            }

                            OnCommandResult?.Invoke(result);
                        }

                        break;
                    case ServerMessageType.Error:
                        var error = root.ToObject<ErrorMessage>(JsonSerializer.Create(jsonSettings));
                        OnError?.Invoke(error?.Message ?? "unknown protocol error");
                        break;
                    case DisconnectSentinelType:
                        OnError?.Invoke(root["message"]?.Value<string>() ?? "disconnected");
                        break;
                    default:
                        Debug.LogWarning($"MatchWireClient: unrecognized message type '{type}'.");
                        break;
                }
            }
        }

        public IReadOnlyCollection<PendingPrediction> PendingPredictions => pendingPredictions.Values;

        /// <summary>
        /// Records a prediction so <see cref="UnitySimulationDriver"/> can merge it into the next
        /// snapshot it builds, ahead of the server's confirmation. Callers pass the SAME
        /// <see cref="Wire.ClientCommandMessage.Id"/> they are about to send via
        /// <see cref="SendCommand"/>, so the matching <c>CommandResultMessage</c> can resolve it.
        /// </summary>
        public void AddPendingPrediction(PendingPrediction prediction)
        {
            pendingPredictions[prediction.Id] = prediction;
        }

        /// <summary>
        /// Fire-and-forget from the caller's perspective — <see cref="UnityCommandAdapter"/>'s
        /// command methods are synchronous today (see its own remarks), so this does not return a
        /// result. Failure is reported via <see cref="OnError"/>, same as any other send failure.
        /// </summary>
        public async void SendCommand(Wire.ClientCommandMessage message)
        {
            try
            {
                var json = JsonConvert.SerializeObject(message, jsonSettings);
                var bytes = Encoding.UTF8.GetBytes(json);
                await sendLock.WaitAsync(lifetime.Token);
                try
                {
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, lifetime.Token);
                }
                finally
                {
                    sendLock.Release();
                }
            }
            catch (Exception exception) when (exception is WebSocketException or ObjectDisposedException or OperationCanceledException)
            {
                OnError?.Invoke($"send failed: {exception.Message}");
            }
        }

        public void Dispose()
        {
            lifetime.Cancel();
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client disposed", CancellationToken.None).Wait(TimeSpan.FromSeconds(1));
                }
            }
            catch (Exception)
            {
                // Best-effort close — the connection is going away regardless.
            }

            socket.Dispose();
            lifetime.Dispose();
            sendLock.Dispose();
        }
    }
}
