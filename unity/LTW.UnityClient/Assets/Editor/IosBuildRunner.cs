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
    /// Exports the Xcode project for an iOS build, headlessly.
    /// </summary>
    /// <remarks>
    /// Unity never produces a runnable .app for iOS — it always emits an Xcode PROJECT, which Xcode
    /// then compiles and signs. So this is step one of two, and the second step needs an Apple ID
    /// that only a human has.
    ///
    /// Written because no build script existed at all: every iOS build would otherwise be someone
    /// clicking through Build Settings and choosing options by hand, which is exactly how a build
    /// ends up differing from the last one for reasons nobody recorded.
    ///
    /// Nothing here is hardcoded to a guess. Bundle identifier, team ID and SDK all come from the
    /// command line, and anything not passed keeps whatever the project already has, so running this
    /// with no arguments changes no project setting.
    ///
    ///   Unity -batchmode -quit -projectPath unity/LTW.UnityClient \
    ///     -executeMethod LTW.UnityClient.Editor.IosBuildRunner.Build \
    ///     -ltwBundleId com.yourname.linewards \
    ///     -ltwTeamId ABCDE12345 \
    ///     -ltwSdk device|simulator \
    ///     -ltwBuildPath build/ios
    /// </remarks>
    public static class IosBuildRunner
    {
        private const string DefaultBuildPath = "build/ios";

        [MenuItem("Line Wards/Build/Export iOS Xcode Project")]
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
                Debug.LogError("IOS BUILD FAIL: no enabled scenes in Build Settings; the player would launch to nothing.");
                return 1;
            }

            ApplyIdentityOverrides();

            var sdk = ReadArgument("-ltwSdk") ?? "device";
            PlayerSettings.iOS.sdkVersion = sdk.Equals("simulator", StringComparison.OrdinalIgnoreCase)
                ? iOSSdkVersion.SimulatorSDK
                : iOSSdkVersion.DeviceSDK;

            var buildPath = ReadArgument("-ltwBuildPath") ?? DefaultBuildPath;
            var fullPath = Path.IsPathRooted(buildPath)
                ? buildPath
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), buildPath));
            Directory.CreateDirectory(fullPath);

            Debug.Log(
                $"IOS BUILD: {scenes.Length} scene(s), sdk={PlayerSettings.iOS.sdkVersion}, " +
                $"bundleId={PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)}, " +
                $"team='{PlayerSettings.iOS.appleDeveloperTeamID}', " +
                $"automaticSigning={PlayerSettings.iOS.appleEnableAutomaticSigning} -> {fullPath}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"IOS BUILD FAIL: {summary.result}, {summary.totalErrors} error(s).");
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages.Where(m => m.type is LogType.Error or LogType.Exception))
                    {
                        Debug.LogError($"IOS BUILD FAIL: [{step.name}] {message.content}");
                    }
                }

                return 1;
            }

            Debug.Log(
                $"IOS BUILD OK: Xcode project at {fullPath} " +
                $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s). " +
                "Open Unity-iPhone.xcodeproj, set a signing team, then run on a device.");
            return 0;
        }

        /// <summary>
        /// Applies bundle id / team / signing from the command line, leaving unpassed values alone.
        /// </summary>
        /// <remarks>
        /// Team ID and bundle identifier belong to a person and an Apple account, so they are never
        /// defaulted here. A build script that invents a bundle identifier produces an app that
        /// installs over, or fails to install alongside, whatever else used that identifier.
        /// </remarks>
        private static void ApplyIdentityOverrides()
        {
            var bundleId = ReadArgument("-ltwBundleId");
            if (!string.IsNullOrWhiteSpace(bundleId))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleId);
            }

            var teamId = ReadArgument("-ltwTeamId");
            if (!string.IsNullOrWhiteSpace(teamId))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = teamId;
                // Automatic signing is what makes a free personal Apple ID work without anyone
                // hand-managing certificates and profiles. It is only switched on when a team is
                // actually supplied, so passing nothing leaves the project's manual setup intact.
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            }
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
