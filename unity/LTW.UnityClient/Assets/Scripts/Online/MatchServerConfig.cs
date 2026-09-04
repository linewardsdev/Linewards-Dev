#nullable enable

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// Where <c>LTW.MatchServer</c> lives. Defaults to loopback because MP-07 ("Operations" — real
    /// hosting) has not started; there is nowhere else to point this yet. See
    /// docs/MULTIPLAYER_ROLLOUT.md's MP-07.
    /// </summary>
    public static class MatchServerConfig
    {
        public static string Host = "localhost";
        public static int Port = 5117;

        public static string HttpBaseUrl => $"http://{Host}:{Port}";
        public static string WebSocketBaseUrl => $"ws://{Host}:{Port}";
    }
}
