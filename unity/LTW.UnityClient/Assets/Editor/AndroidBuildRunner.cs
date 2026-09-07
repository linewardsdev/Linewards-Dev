using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Builds an Android package headlessly — the same gap <see cref="IosBuildRunner"/> closed
    /// for iOS, mirrored here: this project had no Android build script at all, so every Android
    /// build would otherwise be someone clicking through Build Settings and choosing options by
    /// hand. See docs/PROJECT_TRACKER.md's §9 for why this exists now (Android marketplace
    /// deployment tooling) and what still gates a real store upload — this script produces a
    /// build, it does not clear those gates.
    /// </summary>
    /// <remarks>
    /// Nothing here is hardcoded to a guess, same discipline as <see cref="IosBuildRunner"/>:
    /// package name and keystore all come from the command line (or environment, for secrets),
    /// and anything not passed keeps whatever the project already has.
    ///
    ///   Unity -batchmode -quit -projectPath unity/LTW.UnityClient \
    ///     -executeMethod LTW.UnityClient.Editor.AndroidBuildRunner.Build \
    ///     -ltwPackageName com.yourname.linewards \
    ///     -ltwOutputFormat apk|aab \
    ///     -ltwTargetSdk 36 \
    ///     -ltwBuildPath build/android
    ///
    /// Signing a release build additionally needs (path/alias on the command line, passwords
    /// ONLY from the environment — see <see cref="ApplyKeystoreOverrides"/>):
    ///
    ///     -ltwKeystorePath /path/to/release.keystore -ltwKeyaliasName upload
    ///     LTW_ANDROID_KEYSTORE_PASSWORD=... LTW_ANDROID_KEY_ALIAS_PASSWORD=...
    ///
    /// Passing none of the keystore arguments leaves Unity's own default debug keystore in
    /// place, which is exactly right for a local sideload test build and wrong for a Play
    /// Console upload — see docs/STORE_SIGNING_PREREQUISITES.md's Google section for the real
    /// signing decision (Play App Signing), which this script does not make for you.
    /// </remarks>
    public static class AndroidBuildRunner
    {
        private const string DefaultBuildPath = "build/android";

        [MenuItem("Line Wards/Build/Export Android Build")]
        public static void Build()
        {
            var exitCode = 0;
            try
            {
                exitCode = Export();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static int Export()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("ANDROID BUILD FAIL: no enabled scenes in Build Settings; the player would launch to nothing.");
                return 1;
            }

            ApplyPackageNameOverride();
            ApplyKeystoreOverrides();

            // IL2CPP and ARM64 are not defaults left to chance here — both are the explicit
            // pre-upload requirement STORE_SIGNING_PREREQUISITES.md and
            // ANDROID_DEVICE_VALIDATION.md already record, and Play Console has required 64-bit
            // native libraries for years. Set unconditionally, the same way this project treats
            // idempotent identity settings elsewhere.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // AndroidApiLevel36 (Android 16) by default, not merely "whatever the project has":
            // Google requires new submissions to target API 36 as of 31 August 2026, a deadline
            // already passed as of this writing — see docs/PROJECT_TRACKER.md's §9. Overridable
            // because a local sideload test build has no such requirement.
            var targetSdkArg = ReadArgument("-ltwTargetSdk");
            PlayerSettings.Android.targetSdkVersion = targetSdkArg is not null
                ? ParseSdkVersion(targetSdkArg)
                : AndroidSdkVersions.AndroidApiLevel36;

            var minSdkArg = ReadArgument("-ltwMinSdk");
            if (minSdkArg is not null)
            {
                PlayerSettings.Android.minSdkVersion = ParseSdkVersion(minSdkArg);
            }

            // "apk" by default: a local sideload test build is the common case this script
            // exists for today (see PROJECT_TRACKER.md's §9 — nothing else is unblocked yet).
            // ".aab" is what Play Console actually requires at upload time and produces no
            // directly-installable file, which is why it is opt-in rather than the default.
            var outputFormat = (ReadArgument("-ltwOutputFormat") ?? "apk").ToLowerInvariant();
            if (outputFormat != "apk" && outputFormat != "aab")
            {
                Debug.LogError($"ANDROID BUILD FAIL: unrecognized -ltwOutputFormat '{outputFormat}' — expected 'apk' or 'aab'.");
                return 1;
            }

            EditorUserBuildSettings.buildAppBundle = outputFormat == "aab";
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            var buildPath = ReadArgument("-ltwBuildPath") ?? DefaultBuildPath;
            var fullDirectory = Path.IsPathRooted(buildPath)
                ? buildPath
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), buildPath));
            Directory.CreateDirectory(fullDirectory);

            var productName = PlayerSettings.productName.Replace(" ", "");
            var fileName = $"{productName}.{outputFormat}";
            var fullPath = Path.Combine(fullDirectory, fileName);

            Debug.Log(
                $"ANDROID BUILD: {scenes.Length} scene(s), format={outputFormat}, " +
                $"package={PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}, " +
                $"targetSdk={PlayerSettings.Android.targetSdkVersion}, minSdk={PlayerSettings.Android.minSdkVersion}, " +
                $"scriptingBackend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}, " +
                $"arch={PlayerSettings.Android.targetArchitectures}, " +
                $"customKeystore={PlayerSettings.Android.useCustomKeystore} -> {fullPath}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"ANDROID BUILD FAIL: {summary.result}, {summary.totalErrors} error(s).");
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages.Where(m => m.type is LogType.Error or LogType.Exception))
                    {
                        Debug.LogError($"ANDROID BUILD FAIL: [{step.name}] {message.content}");
                    }
                }

                return 1;
            }

            Debug.Log(
                $"ANDROID BUILD OK: {fullPath} " +
                $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s). " +
                (outputFormat == "apk"
                    ? "Install on a connected device with: adb install -r " + fullPath
                    : "Upload to Play Console — an .aab is not directly installable."));
            return 0;
        }

        /// <summary>
        /// The Android equivalent of <see cref="IosBuildRunner"/>'s bundle-id override — never
        /// defaulted here for the same reason: it is effectively permanent once a build is
        /// uploaded, and belongs to whoever owns the Play Console account, not to a build script.
        /// </summary>
        private static void ApplyPackageNameOverride()
        {
            var packageName = ReadArgument("-ltwPackageName");
            if (!string.IsNullOrWhiteSpace(packageName))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, packageName);
            }
        }

        /// <summary>
        /// Configures a real release keystore for signing, if one is passed — leaves Unity's own
        /// default debug keystore in place otherwise, which is correct for a local sideload test
        /// build. Passwords are read ONLY from the environment, never from a command-line
        /// argument, matching this project's existing PLAYFAB_SECRET_KEY handling
        /// (Program.cs's own remarks) — a command-line argument lands in shell history and the
        /// process list, an environment variable set for one invocation does not.
        /// </summary>
        private static void ApplyKeystoreOverrides()
        {
            var keystorePath = ReadArgument("-ltwKeystorePath");
            if (string.IsNullOrWhiteSpace(keystorePath))
            {
                return;
            }

            var keyaliasName = ReadArgument("-ltwKeyaliasName");
            var keystorePassword = Environment.GetEnvironmentVariable("LTW_ANDROID_KEYSTORE_PASSWORD");
            var keyaliasPassword = Environment.GetEnvironmentVariable("LTW_ANDROID_KEY_ALIAS_PASSWORD");

            if (string.IsNullOrEmpty(keystorePassword) || string.IsNullOrEmpty(keyaliasPassword))
            {
                Debug.LogError(
                    "ANDROID BUILD FAIL: -ltwKeystorePath was passed but LTW_ANDROID_KEYSTORE_PASSWORD " +
                    "and/or LTW_ANDROID_KEY_ALIAS_PASSWORD is not set in the environment. Refusing to " +
                    "build with a real keystore path and no password, rather than silently falling back " +
                    "to the debug keystore for what looks like a release build.");
                throw new InvalidOperationException("Android keystore path given without keystore/key-alias passwords in the environment.");
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePassword;
            if (!string.IsNullOrWhiteSpace(keyaliasName))
            {
                PlayerSettings.Android.keyaliasName = keyaliasName;
            }

            PlayerSettings.Android.keyaliasPass = keyaliasPassword;
            Debug.Log($"ANDROID BUILD: signing with custom keystore '{keystorePath}', alias '{PlayerSettings.Android.keyaliasName}'.");
        }

        private static AndroidSdkVersions ParseSdkVersion(string value)
        {
            if (int.TryParse(value, out var level))
            {
                var name = "AndroidApiLevel" + level;
                if (Enum.TryParse<AndroidSdkVersions>(name, out var parsed))
                {
                    return parsed;
                }
            }

            throw new ArgumentException($"'{value}' is not a recognized Android API level for this Editor's AndroidSdkVersions enum.");
        }

        private static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
