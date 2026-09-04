#nullable enable

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// The two Google OAuth client IDs Sign in with Google on iOS needs — see
    /// docs/PLAYFAB_SETUP.md's Google section and Assets/Editor/iOS/GoogleSignInPostProcessBuild.cs,
    /// which injects these into the exported Xcode project's Info.plist. Neither is a secret — both
    /// are meant to ship inside the built app — and both are filled in as of 2026-09-04.
    /// </summary>
    public static class GoogleSignInIOSConfig
    {
        /// <summary>
        /// The Web-application OAuth client entered into PlayFab's Google add-on (2026-09-04) —
        /// this is GIDServerClientID, what lets PlayFab's LoginWithGoogleAccount verify the server
        /// auth code server-side.
        /// </summary>
        public const string WebClientId = "395374534175-j6n04le6nsr9t9c48hdij2qblt0v34s6.apps.googleusercontent.com";

        /// <summary>
        /// The iOS-type OAuth client (Bundle ID "com.ltwplaceholder.ltw", per ProjectSettings.asset)
        /// created 2026-09-04. This is GIDClientID: what identifies this specific app binary to
        /// Google, distinct from the server-side Web client above.
        /// </summary>
        public const string IosClientId = "395374534175-c6o9df1gvt26ui4s5gsi213eeau6jd80.apps.googleusercontent.com";

        /// <summary>
        /// Both IDs are filled in, but that only proves they were typed in correctly, not that they
        /// are the right way around — see docs/MULTIPLAYER_ROLLOUT.md's MP-05 for why this pair is
        /// easy to transpose (two visually similar strings, no compile-time way to tell them apart)
        /// and confirm the Application type column in Google Cloud Console before trusting this.
        /// </summary>
        public static bool IsConfigured =>
            !WebClientId.StartsWith("PASTE_", System.StringComparison.Ordinal) &&
            !IosClientId.StartsWith("PASTE_", System.StringComparison.Ordinal);
    }
}
