#nullable enable

using System;

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// Obtains a Google OAuth 2.0 server auth code the caller can hand to PlayFab's
    /// <c>LoginWithGoogleAccount</c> — the credential itself, not a PlayFab login. Kept as an
    /// interface so <see cref="PlayFabLoginService"/> does not care which platform-specific SDK
    /// produced the code.
    /// </summary>
    public interface IGoogleSignInProvider
    {
        void SignIn(Action<string> onServerAuthCode, Action<string> onFailure);
    }
}
