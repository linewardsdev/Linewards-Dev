using System.Text.Json;
using LTW.MatchServer;
using LTW.MatchServer.PlayFab;
using Microsoft.Playfab.Gaming.GSDK.CSharp;

var port = args.Length > 0 && int.TryParse(args[0], out var parsedPort) ? parsedPort : 5117;

// PLAYFAB_SECRET_KEY is a real credential and is read from the environment only — never from a
// config file or a command-line argument, which would land it in shell history or a process
// list. See docs/PLAYFAB_SETUP.md's "Handling the Secret Key". Under MPS there is no
// environment to set (PlayFab's build form has none), so RunUnderPlayFabMultiplayerServersAsync
// has a second source — see its own remarks; the environment still wins if both are present.
var playFabAuthority = CreatePlayFabAuthority(
    Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID"),
    Environment.GetEnvironmentVariable("PLAYFAB_SECRET_KEY"),
    source: "environment");

// Selects this PROCESS's own lifecycle, not anything about a match's rules. "standalone" (default,
// unset) is today's exact behavior — one process, always running, hosts however many matches
// POST /matches asks for. "mps" is Azure PlayFab Multiplayer Servers — the opposite shape, a
// process PlayFab allocates to exactly one match via its GSDK, that exits once that match ends.
// See docs/MULTIPLAYER_ROLLOUT.md's MP-07.
var mode = Environment.GetEnvironmentVariable("LTW_MATCHSERVER_MODE") ?? "standalone";
if (mode == "mps")
{
    await RunUnderPlayFabMultiplayerServersAsync(playFabAuthority);
    return;
}

if (playFabAuthority is null)
{
    Console.WriteLine("PlayFab not configured (PLAYFAB_TITLE_ID / PLAYFAB_SECRET_KEY not set) — PlayFab-identified seats will refuse every join. See docs/PLAYFAB_SETUP.md.");
}

var registry = new MatchRegistry(Path.Combine(AppContext.BaseDirectory, "replays"), playFabAuthority);
await RunStandaloneAsync(registry, port);

/// <summary>Null when either value is missing — the caller decides what that means for its mode.</summary>
static PlayFabSessionAuthority? CreatePlayFabAuthority(string? titleId, string? secretKey, string source)
{
    if (string.IsNullOrEmpty(titleId) || string.IsNullOrEmpty(secretKey))
    {
        return null;
    }

    // A short, explicit timeout — the BCL default (100s) would otherwise pin a handler (and the
    // join it's servicing) for a client's entire join attempt if PlayFab is slow or unreachable,
    // compounding M5's amplification risk rather than failing it fast. See
    // docs/SECURITY_AUDIT_2026-09-05.md's M5.
    var playFabHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    Console.WriteLine($"PlayFab configured: title {titleId} (from {source})");
    return new PlayFabSessionAuthority(playFabHttpClient, titleId, secretKey);
}

static async Task RunStandaloneAsync(MatchRegistry registry, int port)
{
    // "+" (any host), not "localhost": a device on the same LAN sends a Host header naming the
    // Mac's own LAN IP, which HttpListener would otherwise refuse to match — found live testing
    // against a real iPad, which cannot reach "localhost" meaning itself. Binding to all
    // interfaces is also just what a real deployment needs anyway: a container's own loopback is
    // never reachable from outside it, so this was the right default to end up at, not a dev-only
    // special case.
    // Optional, unset by default (local/dev use and the test suite): standalone mode otherwise has
    // no auth at all in front of unbounded, unauthenticated match creation. See
    // docs/SECURITY_AUDIT_2026-09-05.md's M3.
    var createSecret = Environment.GetEnvironmentVariable("LTW_MATCH_CREATE_SECRET");
    var host = new HttpMatchHost(registry, $"http://+:{port}/", createSecret: createSecret);
    host.Start();

    Console.WriteLine($"LTW.MatchServer listening on http://localhost:{port}/ (and any other interface)");
    Console.WriteLine("POST /matches  {\"humanSeats\":[1,2],\"playFabSeats\":{\"1\":\"<PlayFabId>\"}}");
    Console.WriteLine("GET  /matches/{id}/join?seat=N&token=T              (MP-04 join token)");
    Console.WriteLine("GET  /matches/{id}/join?seat=N&playFabTicket=T      (MP-05 PlayFab session ticket)");
    Console.WriteLine("Press Ctrl+C to stop.");

    var exit = new TaskCompletionSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        exit.SetResult();
    };
    await exit.Task;
    host.Stop();
}

/// <summary>
/// One process, allocated by PlayFab to exactly one match, that exits when it ends — see
/// docs/MULTIPLAYER_ROLLOUT.md's MP-07 for the design this follows and what it does not yet
/// handle (Azure VM maintenance recycling an already-allocated server mid-match).
/// </summary>
static async Task RunUnderPlayFabMultiplayerServersAsync(PlayFabSessionAuthority? playFabAuthority)
{
    // Must match (case-insensitively — see the lookup below) both the port NAME configured for
    // this build in PlayFab Game Manager (MP-07's Phase 5) and the client's
    // MultiplayerServerConfig.PortName.
    const string gamePortName = "game";
    // PlayFab delivers a game secret named X as environment variable PF_MPS_SECRET_X — the
    // secret is uploaded under the name PLAYFAB_SECRET_KEY, so this is what arrives.
    const string GameSecretEnvironmentVariable = "PF_MPS_SECRET_PLAYFAB_SECRET_KEY";
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    var exit = new TaskCompletionSource();
    ServerMatch? currentMatch = null;
    var connectedSeats = new Dictionary<int, LTW.Simulation.Primitives.PlayerId>();

    void ReportConnectedPlayers()
    {
        var players = connectedSeats.Values.Select(playerId => new ConnectedPlayer(playerId.Value.ToString())).ToList();
        GameserverSDK.UpdateConnectedPlayers(players);
    }

    // Both callbacks are informational to PlayFab only — PlayFabSessionAuthority/
    // ServerMatch.AcceptWithPlayFabAsync remains the sole real join enforcement, unchanged.
    var firstHumanBound = new TaskCompletionSource();
    void OnSeatBound(int seat, LTW.Simulation.Primitives.PlayerId playerId)
    {
        connectedSeats[seat] = playerId;
        firstHumanBound.TrySetResult();
        ReportConnectedPlayers();
    }

    void OnSeatDisconnected(int seat)
    {
        connectedSeats.Remove(seat);
        ReportConnectedPlayers();
    }

    // Registered BEFORE Start(): PlayFab can begin heartbeating — and deciding to reclaim standby
    // capacity — the moment Start() is called.
    GameserverSDK.RegisterShutdownCallback(() =>
    {
        // Fires only for a server PlayFab reclaims BEFORE it is ever allocated (surplus standby
        // capacity) — PlayFab's own docs are explicit that an already-ACTIVE server is never shut
        // down this way. Nothing has been created yet at this point in that case, so there is
        // nothing to clean up beyond letting the process exit.
        GameserverSDK.LogMessage("GSDK shutdown callback fired — exiting.");
        exit.TrySetResult();
    });
    // Correct only because ServerMatch.RunLoopAsync now always resolves Completion (normal exit,
    // Stop(), or a genuine crash — see its own H4 fix) instead of sometimes never resolving it at
    // all. Before that fix, a faulted tick loop left Completion permanently unresolved, so
    // IsCompleted stayed false forever and this reported a dead match healthy indefinitely. See
    // docs/SECURITY_AUDIT_2026-09-05.md's H4.
    GameserverSDK.RegisterHealthCallback(() => currentMatch is null || !currentMatch.Completion.IsCompleted);
    GameserverSDK.RegisterMaintenanceCallback(scheduledTime =>
        GameserverSDK.LogMessage($"Azure VM maintenance scheduled at {scheduledTime:O} — no in-match mitigation exists yet, see docs/MULTIPLAYER_ROLLOUT.md's MP-07."));

    GameserverSDK.Start();

    // Deliberately NOT a fixed path under AppContext.BaseDirectory (standalone mode's own
    // choice): that path lives only inside this match's own ephemeral container and is gone the
    // moment PlayFab deletes it after the match ends — silently losing every replay MP-07's
    // runbook needs to investigate a desync. GSDK's own log folder is what the VM agent zips up
    // and makes available after the server ends (see GSDK's "Logging" docs: any file placed in
    // this directory, not just ones written through GameserverSDK.LogMessage, gets included) —
    // writing replays there is what actually makes them retrievable. Available immediately after
    // Start(), unlike SessionCookieKey/SessionIdKey below, which need allocation first.
    var replayDirectory = Path.Combine(GameserverSDK.GetLogsDirectory(), "replays");

    // Under MPS there is no PLAYFAB_TITLE_ID/PLAYFAB_SECRET_KEY environment: the title id comes
    // from the GSDK config, and the secret from PlayFab's own "game secrets" feature — uploaded
    // once to the title (UploadSecret), referenced by name on the build (GameSecretReferences),
    // and delivered to every server as an environment variable named PF_MPS_SECRET_<name>
    // (learn.microsoft.com/gaming/playfab/multiplayer/servers/manage-secrets). GetBuild never
    // shows its value, and Game Manager's New Build form can't set it — builds using it are
    // created through the API (docs/MP07_RUNBOOK.md). Build METADATA, merged verbatim into
    // getConfigSettings() (confirmed in InternalSdk.cs), stays as a fallback for a build created
    // that way. Found live 2026-09-11: two builds created through the form carried no metadata at
    // all, and every server refused every join with no authority to verify a ticket against
    // (ServerMatch.AcceptWithPlayFabAsync) — the archived log said exactly that.
    if (playFabAuthority is null)
    {
        var startupConfig = GameserverSDK.getConfigSettings();
        startupConfig.TryGetValue(GameserverSDK.TitleIdKey, out var gsdkTitleId);
        var gameSecret = Environment.GetEnvironmentVariable(GameSecretEnvironmentVariable);
        if (!string.IsNullOrEmpty(gameSecret))
        {
            playFabAuthority = CreatePlayFabAuthority(gsdkTitleId, gameSecret, source: "GSDK config + PlayFab game secret");
        }
        else
        {
            startupConfig.TryGetValue("PLAYFAB_SECRET_KEY", out var metadataSecretKey);
            playFabAuthority = CreatePlayFabAuthority(gsdkTitleId, metadataSecretKey, source: "GSDK config + build metadata");
        }

        if (playFabAuthority is null)
        {
            var warning = $"PlayFab not configured: this build references no game secret (env {GameSecretEnvironmentVariable}) and carries no PLAYFAB_SECRET_KEY metadata — every PlayFab-identified join will be refused. See docs/MP07_RUNBOOK.md's deploy steps.";
            GameserverSDK.LogMessage(warning);
            Console.WriteLine(warning);
        }
    }

    var registry = new MatchRegistry(replayDirectory, playFabAuthority);

    // Case-insensitive on purpose: found live (2026-09-09) that Game Manager's own build form
    // capitalizes a typed port name back on display ("Game" for an entry typed "game"), and this
    // comparison was a plain ordinal `==` — every real deployment attempt threw this exception
    // before GameserverSDK.Start()'s heartbeat could ever begin, which PlayFab then reports as
    // "Unhealthy" (no heartbeat within its own ~10-minute window) with no clearer signal anywhere
    // in Game Manager pointing at the actual cause. A human retyping a name into a portal field is
    // exactly the kind of case drift worth being lenient about rather than a real configuration
    // difference worth failing loudly over.
    var connectionInfo = GameserverSDK.GetGameServerConnectionInfo();
    var gamePort = connectionInfo.GamePortsConfiguration.FirstOrDefault(candidate => string.Equals(candidate.Name, gamePortName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"no GamePortsConfiguration entry named '{gamePortName}' (case-insensitive) — check this build's port configuration in PlayFab Game Manager.");

    var host = new HttpMatchHost(registry, $"http://+:{gamePort.ServerListeningPort}/", allowMatchCreation: false);
    host.Start();

    // Blocking, per GSDK's own documented contract — must not run on this async entry point's own
    // thread, or nothing else (the heartbeat included) could make progress while it waits.
    var allocated = await Task.Run(() => GameserverSDK.ReadyForPlayers());
    if (!allocated)
    {
        // Terminated before ever being allocated a match — nothing was created, nothing to clean up.
        GameserverSDK.LogMessage("Server was terminated before ever being allocated a match.");
        host.Stop();
        return;
    }

    // SessionCookieKey/SessionIdKey only become available once ReadyForPlayers() has returned
    // true — reading them any earlier would find nothing.
    var config = GameserverSDK.getConfigSettings();
    var sessionId = config.TryGetValue(GameserverSDK.SessionIdKey, out var id) ? id : Guid.NewGuid().ToString("N");
    var sessionCookie = config.TryGetValue(GameserverSDK.SessionCookieKey, out var cookie) ? cookie : "{}";

    // Same JSON shape as HttpMatchHost's own POST /matches body — CreateMatchRequest is shared
    // between both so the contract can't drift.
    var request = JsonSerializer.Deserialize<CreateMatchRequest>(sessionCookie, json) ?? new CreateMatchRequest();

    List<int> humanSeats;
    Dictionary<int, string>? playFabIdBySeat;
    if (request.HumanSeats is { Count: > 0 })
    {
        // A direct RequestMultiplayerServer call (today's solo-vs-bots path) — SessionCookie
        // carries the real seat assignment, unchanged.
        humanSeats = request.HumanSeats;
        playFabIdBySeat = request.PlayFabSeats;
    }
    else
    {
        // No SessionCookie content — this server was auto-allocated by a matchmaking queue's
        // ServerAllocationEnabled, not a direct request. See docs/MULTIPLAYER_ROLLOUT.md's MP-05.
        (humanSeats, playFabIdBySeat) = QueuedMatchBootstrap.AssignSeatsFromInitialPlayers(GameserverSDK.GetInitialPlayers());
    }

    // The SessionCookie branch above is client-authored (a direct RequestMultiplayerServer call
    // carries whatever the caller put in it) — validated here for the same reason
    // HttpMatchHost.HandleCreateMatchAsync validates its own HTTP body. Nothing can send a 400
    // back at this point (there is no HTTP response, the server is already allocated), so an
    // invalid request exits the process instead of letting ServerMatch construct itself from bad
    // numbers — see docs/SECURITY_AUDIT_2026-09-05.md's M1.
    if (!CreateMatchRequestValidator.TryValidate(humanSeats, playFabIdBySeat, request.TicksPerSecond, request.OpeningBuildWindowSeconds, out var validationError))
    {
        GameserverSDK.LogMessage($"Rejecting malformed match request: {validationError}");
        Console.WriteLine($"Rejecting malformed match request: {validationError}");
        host.Stop();
        return;
    }

    currentMatch = registry.CreateMatch(
        humanSeats,
        request.TicksPerSecond,
        playFabIdBySeat,
        request.OpeningBuildWindowSeconds,
        matchId: sessionId,
        onSeatBound: OnSeatBound,
        onSeatDisconnected: OnSeatDisconnected);

    // A non-PlayFab-identified seat's join token is otherwise never observable from outside this
    // process (there is no HTTP create response to read it from under MPS, unlike standalone mode)
    // — genuinely useful for a runbook grepping container logs during a live join issue, not just
    // local LocalMultiplayerAgent testing. Logged both to GSDK's own log file (what a real
    // deployment's zipped logs would contain) and stdout (what `docker logs` shows locally).
    var bootstrapMessage = $"Match {currentMatch.MatchId} bootstrapped. Join tokens: {JsonSerializer.Serialize(currentMatch.JoinTokens, json)}";
    GameserverSDK.LogMessage(bootstrapMessage);
    Console.WriteLine(bootstrapMessage);

    // A match nobody ever joins must not run to its natural end: ServerMatch happily plays a
    // bot-vs-bot match for a human who was refused at the door or never dialed, which under MPS
    // keeps this VM allocated (billed) for the whole match AND keeps PlayFab from archiving this
    // server's log — the log that says why the join was refused. Found 2026-09-11: the first
    // refused real join left an Active server with "no logs to download" for exactly this
    // reason. Ends the match if no human binds a seat within a bounded window; a match that
    // had a human and lost them is MP-06 reconnect's concern, not this one's.
    const int NoHumanJoinedTimeoutSeconds = 60;
    var noHumanJoined = Task.Delay(TimeSpan.FromSeconds(NoHumanJoinedTimeoutSeconds));
    var first = await Task.WhenAny(exit.Task, currentMatch.Completion, firstHumanBound.Task, noHumanJoined);
    if (first == noHumanJoined)
    {
        var message = $"Match {currentMatch.MatchId}: no human joined within {NoHumanJoinedTimeoutSeconds}s of allocation — ending it so this server can exit.";
        GameserverSDK.LogMessage(message);
        Console.WriteLine(message);
        currentMatch.Stop();
        host.Stop();
        return;
    }

    if (first == firstHumanBound.Task)
    {
        // Process exit itself is the "I'm done" signal to PlayFab's agent — there is no separate
        // GSDK call to make beyond simply returning from Main.
        await Task.WhenAny(exit.Task, currentMatch.Completion);
    }

    host.Stop();
}
