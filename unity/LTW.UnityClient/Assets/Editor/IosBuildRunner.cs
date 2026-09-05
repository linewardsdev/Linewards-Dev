using System;
using System.IO;
using System.Collections.Generic;
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
            ApplyInsecureHttpOverride();

            var sdk = ReadArgument("-ltwSdk") ?? "device";
            PlayerSettings.iOS.sdkVersion = sdk.Equals("simulator", StringComparison.OrdinalIgnoreCase)
                ? iOSSdkVersion.SimulatorSDK
                : iOSSdkVersion.DeviceSDK;

            ApplySimulatorArchitecture();

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

            // MSAA is disabled for the duration of a simulator build and restored afterwards, so
            // the committed assets are never left modified. See SuppressMsaaForSimulator.
            var restoreMsaa = SuppressMsaaForSimulator();

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                RestoreMsaa(restoreMsaa);
            }

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

        /// <summary>
        /// Opts into cleartext HTTP/WS for LAN-testing builds — pass <c>-ltwAllowInsecureHttp 1</c>.
        /// </summary>
        /// <remarks>
        /// iOS's App Transport Security refuses any plain "http://"/"ws://" request by default,
        /// with no exception surfaced anywhere in-app — found live, testing PLAY ONLINE against a
        /// real `LTW.MatchServer` on the same LAN: the request never even reaches the Mac, it is
        /// blocked on-device before it leaves. `PlayerSettings.iOS.allowHTTPDownload` is what
        /// injects the corresponding `NSAllowsArbitraryLoads` exception into the generated
        /// Info.plist; unset by default (never written unless this override is passed), because it
        /// is an App Store review flag and MP-07's real hosting will use HTTPS/WSS anyway — this
        /// exists purely so a LAN-IP dev build (see MatchServerConfig's own remarks) can be tested
        /// on a real device before that infrastructure exists.
        /// </remarks>
        private static void ApplyInsecureHttpOverride()
        {
            var flag = ReadArgument("-ltwAllowInsecureHttp");
            if (string.IsNullOrWhiteSpace(flag) || flag == "0")
            {
                return;
            }

            PlayerSettings.iOS.allowHTTPDownload = true;
            Debug.Log("IOS BUILD: allowHTTPDownload enabled (NSAllowsArbitraryLoads) — dev/LAN testing only, do not ship this.");
        }

        /// <summary>
        /// Forces the simulator SDK to build for the architecture the host's simulators run.
        /// </summary>
        /// <remarks>
        /// Unity defaults `iOSSimulatorArchitecture` to x86_64, which is an Intel-Mac-era choice.
        /// Every simulator on an Apple Silicon Mac is arm64, so an x86_64 build has no valid
        /// destination — Xcode's dropdown comes up empty and Run reports "A build only device
        /// cannot be used to run this target", which points at the device and sends you looking in
        /// entirely the wrong place.
        ///
        /// Set here rather than left to the project asset so a simulator export is correct whoever
        /// runs it, and so the device path is untouched.
        ///
        /// The wrong fix, tried first and recorded so nobody repeats it: overriding Xcode's `ARCHS`
        /// to `$(ARCHS_STANDARD)` in a post-process. That makes the destination list populate, which
        /// looks like success, and then fails at link with "symbol(s) not found for architecture
        /// x86_64" — because it asks Xcode to build a slice Unity never produced libraries for.
        /// The architecture has to be chosen before Unity builds, not patched into the project after.
        /// </remarks>
        private static void ApplySimulatorArchitecture()
        {
            if (PlayerSettings.iOS.sdkVersion != iOSSdkVersion.SimulatorSDK)
            {
                return;
            }

            // Through SerializedObject because Unity exposes no scripting API for this — it is
            // settable in the Player Settings inspector and nowhere else. 0 is x86_64, 1 is arm64.
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var architecture = settings.FindProperty("iOSSimulatorArchitecture");
            if (architecture == null)
            {
                Debug.LogWarning("IOS BUILD: no iOSSimulatorArchitecture field; leaving the simulator architecture as configured.");
                return;
            }

            if (architecture.intValue != SimulatorArm64)
            {
                architecture.intValue = SimulatorArm64;
                settings.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("IOS BUILD: simulator architecture switched from x86_64 to arm64.");
            }
        }

        /// <summary>`iOSSimulatorArchitecture` enum value for arm64. 0 is x86_64.</summary>
        private const int SimulatorArm64 = 1;


        /// <summary>
        /// Turns MSAA off across every pipeline asset for a simulator build, returning what to undo.
        /// </summary>
        /// <remarks>
        /// The iOS simulator's Metal implementation cannot do what the pipeline asks of it. With MSAA
        /// on, every frame fails with "RenderPass: Attachment 0 was created with 4 samples but 2
        /// samples were requested", followed by "EndRenderPass: Not inside a Renderpass" — measured at
        /// 905 such errors in a 25-second run.
        ///
        /// The symptom is confusing, and cost an hour: the board renders as a black screen while the
        /// HUD looks perfect. That is because IMGUI draws straight to the backbuffer and never enters
        /// a URP render pass, so the UI survives the exact failure that erases the scene. It reads as
        /// "the game did not load" when in fact only the scene half of the frame is missing.
        ///
        /// NOT a regression from the quality tiers, checked before assuming: the base pipeline asset
        /// was already MSAA 2 before those existed. It had simply never been exercised, because
        /// nobody had built this project for iOS.
        ///
        /// Suppressed only for the simulator, and restored in a finally, because MSAA is wanted on
        /// real hardware — devices handle it fine and the crystal and spire silhouettes need it. A
        /// simulator build is a preview, and a preview that renders nothing is worth less than one
        /// without antialiasing.
        /// </remarks>
        private static List<(string Path, int Samples)> SuppressMsaaForSimulator()
        {
            var restore = new List<(string, int)>();
            if (PlayerSettings.iOS.sdkVersion != iOSSdkVersion.SimulatorSDK)
            {
                return restore;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                var serialized = new SerializedObject(asset);
                var msaa = serialized.FindProperty("m_MSAA");
                if (msaa == null || msaa.intValue <= 1)
                {
                    continue;
                }

                restore.Add((path, msaa.intValue));
                msaa.intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }

            if (restore.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"IOS BUILD: MSAA suppressed on {restore.Count} pipeline asset(s) for the simulator; the simulator GPU cannot satisfy the render pass and the scene renders black with it on.");
            }

            return restore;
        }

        private static void RestoreMsaa(List<(string Path, int Samples)> restore)
        {
            if (restore.Count == 0)
            {
                return;
            }

            foreach (var (path, samples) in restore)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                var serialized = new SerializedObject(asset);
                serialized.FindProperty("m_MSAA").intValue = samples;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"IOS BUILD: MSAA restored on {restore.Count} pipeline asset(s).");
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
