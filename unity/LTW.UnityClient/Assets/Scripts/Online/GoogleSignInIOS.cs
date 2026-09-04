#nullable enable

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// <see cref="IGoogleSignInProvider"/> for iOS, via a small native bridge
    /// (Assets/Plugins/iOS/LTWGoogleSignInBridge.mm) wrapping Google's GoogleSignIn-iOS SDK
    /// directly — there is no maintained Unity plugin for this any more (Google archived
    /// google-signin-unity in April 2026); see docs/MULTIPLAYER_ROLLOUT.md's MP-05 for why this is
    /// hand-written rather than a dependency.
    /// </summary>
    /// <remarks>
    /// UNTESTED ON DEVICE as of writing: this repo's Xcode export lives in a different location
    /// than this Unity project, so this class compiles and its wiring is real, but the actual
    /// on-device sign-in flow has not been exercised. See docs/PLAYFAB_SETUP.md's Google section.
    /// </remarks>
    public sealed class GoogleSignInIOS : IGoogleSignInProvider
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _LTWGoogleSignIn_SignIn();
#endif

        // UnitySendMessage can only target one outstanding call at a time in this simple design —
        // there is exactly one sign-in button, and it disables itself while a sign-in is pending
        // (see ShellScreenView), so a second concurrent SignIn() call cannot happen today.
        private static Action<string>? pendingSuccess;
        private static Action<string>? pendingFailure;

        public void SignIn(Action<string> onServerAuthCode, Action<string> onFailure)
        {
#if UNITY_IOS && !UNITY_EDITOR
            GoogleSignInReceiver.EnsureExists();
            pendingSuccess = onServerAuthCode;
            pendingFailure = onFailure;
            _LTWGoogleSignIn_SignIn();
#else
            onFailure("Google Sign-In is only implemented for iOS in this build.");
#endif
        }

        internal static void DeliverSuccess(string serverAuthCode)
        {
            var callback = pendingSuccess;
            pendingSuccess = null;
            pendingFailure = null;
            if (callback == null)
            {
                Debug.LogWarning("GoogleSignInIOS received a success callback with no pending request.");
                return;
            }

            callback(serverAuthCode);
        }

        internal static void DeliverFailure(string message)
        {
            var callback = pendingFailure;
            pendingSuccess = null;
            pendingFailure = null;
            if (callback == null)
            {
                Debug.LogWarning($"GoogleSignInIOS received a failure callback with no pending request: {message}");
                return;
            }

            callback(message);
        }
    }
}
