#nullable enable

using System;
using UnityEngine;

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// The landing spot for the native iOS bridge's <c>UnitySendMessage</c> calls
    /// (Assets/Plugins/iOS/LTWGoogleSignInBridge.mm). The GameObject name here
    /// ("LTWGoogleSignInReceiver") is hardcoded on the native side too — the two must match
    /// exactly, since <c>UnitySendMessage</c> addresses a GameObject by name, not by type.
    /// </summary>
    /// <remarks>
    /// A native callback cannot call back into an arbitrary C# closure directly, so this receiver
    /// exists purely to convert "a message arrived on the main thread" into "invoke whichever
    /// callback <see cref="GoogleSignInIOS"/> is currently waiting on" — see
    /// <see cref="GoogleSignInIOS.DeliverSuccess"/>/<see cref="GoogleSignInIOS.DeliverFailure"/>.
    /// </remarks>
    internal sealed class GoogleSignInReceiver : MonoBehaviour
    {
        private const string GameObjectName = "LTWGoogleSignInReceiver";

        private static GoogleSignInReceiver? instance;

        internal static void EnsureExists()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject(GameObjectName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<GoogleSignInReceiver>();
        }

        // Invoked by native code via UnitySendMessage("LTWGoogleSignInReceiver", "OnGoogleSignInSuccess", serverAuthCode).
        private void OnGoogleSignInSuccess(string serverAuthCode)
        {
            GoogleSignInIOS.DeliverSuccess(serverAuthCode);
        }

        // Invoked by native code via UnitySendMessage("LTWGoogleSignInReceiver", "OnGoogleSignInFailure", message).
        private void OnGoogleSignInFailure(string message)
        {
            GoogleSignInIOS.DeliverFailure(message);
        }
    }
}
