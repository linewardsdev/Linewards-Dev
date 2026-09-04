#nullable enable

#if UNITY_IOS

using System.IO;
using System.Linq;
using LTW.UnityClient.Online;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace LTW.UnityClient.EditorTools.iOS
{
    /// <summary>
    /// Unity regenerates Info.plist from scratch on every Xcode export, so the Google Sign-In keys
    /// it needs (GIDClientID, GIDServerClientID, and a URL scheme for the OAuth redirect) cannot
    /// just be hand-edited once in the exported project — they would vanish on the next export.
    /// This runs after every iOS build and injects them, the same way Firebase/AdMob's own Unity
    /// plugins do it. See Assets/Plugins/iOS/LTWGoogleSignInBridge.mm for what consumes these.
    /// </summary>
    /// <remarks>
    /// UNTESTED as of writing — this repo's actual Xcode exports happen from a different worktree
    /// (see project memory on Xcode export staleness), so this has not run against a real export.
    /// It only touches Info.plist keys; GoogleSignInDependencies.xml (EDM4U) is what links the
    /// actual CocoaPod, independently of this script.
    /// </remarks>
    internal static class GoogleSignInPostProcessBuild
    {
        [PostProcessBuild(1)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            if (!GoogleSignInIOSConfig.IsConfigured)
            {
                Debug.LogWarning(
                    "GoogleSignInPostProcessBuild: GoogleSignInIOSConfig still has placeholder client IDs — " +
                    "Sign in with Google will not work in this build until real values are filled in. " +
                    "See docs/PLAYFAB_SETUP.md's Google section.");
                return;
            }

            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var root = plist.root;
            root.SetString("GIDClientID", GoogleSignInIOSConfig.IosClientId);
            root.SetString("GIDServerClientID", GoogleSignInIOSConfig.WebClientId);

            var reversedClientId = ReverseClientId(GoogleSignInIOSConfig.IosClientId);
            var urlTypes = root.CreateArray("CFBundleURLTypes");
            var urlTypeDict = urlTypes.AddDict();
            urlTypeDict.SetString("CFBundleURLName", "GoogleSignIn");
            var schemes = urlTypeDict.CreateArray("CFBundleURLSchemes");
            schemes.AddString(reversedClientId);

            plist.WriteToFile(plistPath);
        }

        /// <summary>
        /// "1234-abc.apps.googleusercontent.com" -&gt; "com.googleusercontent.apps.1234-abc" — the
        /// URL scheme Google's iOS SDK expects for the OAuth redirect, per Google's own
        /// documented convention (reverse the client ID's dot-separated segments).
        /// </summary>
        private static string ReverseClientId(string clientId)
        {
            return string.Join(".", clientId.Split('.').Reverse());
        }
    }
}

#endif
