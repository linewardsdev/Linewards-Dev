#nullable enable

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// The one place the PlayFab Title ID lives. Not a secret — see
    /// docs/PLAYFAB_SETUP.md's "Handling the Secret Key" for what IS: the Secret Key is a
    /// server-only credential and must never appear in client code, this project, or this file.
    /// </summary>
    public static class PlayFabConfig
    {
        public const string TitleId = "FBC34";

        private static bool configured;

        /// <summary>Idempotent: safe to call from every entry point that might run first.</summary>
        public static void EnsureConfigured()
        {
            if (configured)
            {
                return;
            }

            PlayFab.PlayFabSettings.TitleId = TitleId;
            configured = true;
        }
    }
}
