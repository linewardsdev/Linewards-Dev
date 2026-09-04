#nullable enable

using PlayFab;
using PlayFab.ClientModels;

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// Ties a platform's <see cref="IGoogleSignInProvider"/> to PlayFab's
    /// <c>LoginWithGoogleAccount</c> and, on success, records the result in
    /// <see cref="PlayFabSession"/>. See docs/MULTIPLAYER_ROLLOUT.md's MP-05 — this is the client
    /// half of the identity flow the server side (<c>PlayFabSessionAuthority</c>) already verifies.
    /// </summary>
    public static class PlayFabLoginService
    {
        public static void SignInWithGoogle(IGoogleSignInProvider provider, System.Action<string> onSuccess, System.Action<string> onFailure)
        {
            PlayFabConfig.EnsureConfigured();

            provider.SignIn(
                onServerAuthCode: serverAuthCode =>
                {
                    var request = new LoginWithGoogleAccountRequest
                    {
                        TitleId = PlayFabConfig.TitleId,
                        ServerAuthCode = serverAuthCode,
                        CreateAccount = true,
                    };

                    PlayFabClientAPI.LoginWithGoogleAccount(
                        request,
                        result =>
                        {
                            PlayFabSession.SetSignedIn(result.PlayFabId, result.SessionTicket);
                            onSuccess(result.PlayFabId);
                        },
                        error => onFailure(error.GenerateErrorReport()));
                },
                onFailure: onFailure);
        }
    }
}
