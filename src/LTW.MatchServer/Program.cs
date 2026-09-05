using LTW.MatchServer;
using LTW.MatchServer.PlayFab;

var port = args.Length > 0 && int.TryParse(args[0], out var parsedPort) ? parsedPort : 5117;
var replayDirectory = Path.Combine(AppContext.BaseDirectory, "replays");

// PLAYFAB_SECRET_KEY is a real credential and is read from the environment only — never from a
// config file or a command-line argument, which would land it in shell history or a process
// list. See docs/PLAYFAB_SETUP.md's "Handling the Secret Key".
var playFabTitleId = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID");
var playFabSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_SECRET_KEY");
PlayFabSessionAuthority? playFabAuthority = null;
if (!string.IsNullOrEmpty(playFabTitleId) && !string.IsNullOrEmpty(playFabSecretKey))
{
    playFabAuthority = new PlayFabSessionAuthority(new HttpClient(), playFabTitleId, playFabSecretKey);
    Console.WriteLine($"PlayFab configured: title {playFabTitleId}");
}
else
{
    Console.WriteLine("PlayFab not configured (PLAYFAB_TITLE_ID / PLAYFAB_SECRET_KEY not set) — PlayFab-identified seats will refuse every join. See docs/PLAYFAB_SETUP.md.");
}

var registry = new MatchRegistry(replayDirectory, playFabAuthority);
// "+" (any host), not "localhost": a device on the same LAN sends a Host header naming the Mac's
// own LAN IP, which HttpListener would otherwise refuse to match — found live testing against a
// real iPad, which cannot reach "localhost" meaning itself. Binding to all interfaces is also
// just what a real deployment (MP-07) needs anyway: a container's own loopback is never reachable
// from outside it, so this was the right default to end up at, not a dev-only special case.
var host = new HttpMatchHost(registry, $"http://+:{port}/");
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
