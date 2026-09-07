#nullable enable

using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace LTW.UnityClient.Online
{
    /// <summary>
    /// Persists small secret strings — today, the PlayFab session ticket and id — via each
    /// platform's real secure storage instead of <c>PlayerPrefs</c>' plaintext files. See
    /// <see cref="PlayFabSession"/>'s own remarks for what this replaces and
    /// docs/SECURITY_AUDIT_2026-09-05.md's M-C2 for the finding: a bearer credential for the
    /// entire PlayFab Client API, sitting in an unencrypted iOS plist backup or Android
    /// <c>shared_prefs</c> XML, with no sign-out UI to ever clear it except a failed auth check.
    /// </summary>
    /// <remarks>
    /// iOS only for now, via Assets/Plugins/iOS/LTWKeychainBridge.mm — Android has no online
    /// sign-in provider at all yet (see every <see cref="IGoogleSignInProvider"/> implementation:
    /// only <see cref="GoogleSignInIOS"/> exists), so there is no Android secret to protect yet
    /// either. Falls back to <c>PlayerPrefs</c> on every other platform (Android included, until
    /// it gets its own <c>EncryptedSharedPreferences</c> bridge) and in the Editor, where a native
    /// Keychain call cannot run at all — the same degrade shape <see cref="GoogleSignInIOS"/>
    /// already uses for "this feature is iOS-only so far".
    ///
    /// UNTESTED ON DEVICE, same caveat as <c>LTWKeychainBridge.mm</c> itself: this repo's Xcode
    /// export lives in a different location than this Unity project, so the actual on-device
    /// Keychain read/write has not been exercised. Falls back to <c>PlayerPrefs</c> AND logs a
    /// warning if a Keychain write ever fails, rather than silently losing the value — a session
    /// that fails to persist securely should still persist, not vanish.
    /// </remarks>
    public static class SecureSessionStore
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int _LTWKeychain_Set(string account, string value);

        [DllImport("__Internal")]
        private static extern int _LTWKeychain_Get(string account, StringBuilder buffer, int bufferSize);

        [DllImport("__Internal")]
        private static extern int _LTWKeychain_Delete(string account);

        // A PlayFab session ticket is a signed token with no published fixed length — generous
        // rather than tight, matching this project's other "real values are far smaller than this
        // cap" caps (HttpMatchHost's own inbound-message and create-body size limits).
        private const int MaxValueLength = 8192;
#endif

        public static void Set(string key, string value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_LTWKeychain_Set(key, value) != 0)
            {
                return;
            }

            Debug.LogWarning($"SecureSessionStore: Keychain write failed for '{key}', falling back to PlayerPrefs.");
#endif
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public static string Get(string key, string defaultValue = "")
        {
#if UNITY_IOS && !UNITY_EDITOR
            var buffer = new StringBuilder(MaxValueLength);
            var length = _LTWKeychain_Get(key, buffer, MaxValueLength);
            if (length >= 0)
            {
                return buffer.ToString(0, length);
            }

            // Not found in the Keychain — could be a value written by an older build, before this
            // store existed, still sitting in PlayerPrefs. Falling back to reading THAT (rather
            // than returning defaultValue outright) is what makes this change transparent to an
            // already-signed-in player, instead of silently signing every existing install out.
#endif
            return PlayerPrefs.GetString(key, defaultValue);
        }

        public static void Delete(string key)
        {
#if UNITY_IOS && !UNITY_EDITOR
            _LTWKeychain_Delete(key);
#endif
            // Deliberately ALSO clears PlayerPrefs on every platform, iOS included: a value could
            // still be sitting there from before this store existed or from a fallback write above
            // — ForgetOnAuthFailure/Clear's whole contract is "nothing restorable is left behind",
            // see PlayFabSession.Clear's own remarks.
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
