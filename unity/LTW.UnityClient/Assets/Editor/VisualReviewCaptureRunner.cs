#nullable enable

using System;
using System.IO;
using System.Reflection;
using LTW.UnityClient.Simulation;
using LTW.UnityClient.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class VisualReviewCaptureRunner
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private static readonly string OutputDirectory = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "..",
            "docs",
            "screenshot-reviews",
            "art-creep-starter-set-kickoff",
            "captures"));

        private static CaptureState state;
        private static double nextActionAt;
        private static string? pendingCapturePath;
        private static string? pendingCaptureLabel;
        private static string? delayedCaptureLabel;
        private static int captureIndex;
        private static double startedAt;
        private static bool exitAfterRun;
        private static bool previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousEnterPlayModeOptions;

        [MenuItem("Line Wards/Review/Capture Visual Review Set")]
        public static void CaptureVisualReviewSet()
        {
            Directory.CreateDirectory(OutputDirectory);
            ClearPriorGeneratedCaptures();
            captureIndex = 1;
            pendingCapturePath = null;
            pendingCaptureLabel = null;
            delayedCaptureLabel = null;
            exitAfterRun = ShouldExitAfterRun();
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            state = CaptureState.WaitForPlayMode;
            nextActionAt = EditorApplication.timeSinceStartup + 0.5d;
            startedAt = EditorApplication.timeSinceStartup;

            EditorApplication.update -= Update;
            EditorApplication.update += Update;

            EditorSceneManager.OpenScene(ScenePath);
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup - startedAt > 90d)
            {
                Finish("Timed out while capturing visual review screenshots.");
                return;
            }

            if (pendingCapturePath != null)
            {
                if (!File.Exists(pendingCapturePath))
                {
                    return;
                }

                pendingCapturePath = null;
                var completedLabel = pendingCaptureLabel;
                pendingCaptureLabel = null;
                nextActionAt = EditorApplication.timeSinceStartup + 0.5d;
                AdvanceState(completedLabel);
                return;
            }

            if (EditorApplication.timeSinceStartup < nextActionAt)
            {
                return;
            }

            if (delayedCaptureLabel != null)
            {
                var label = delayedCaptureLabel;
                delayedCaptureLabel = null;
                QueueCapture(label);
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            var driver = UnityEngine.Object.FindAnyObjectByType<UnitySimulationDriver>();
            var commands = UnityEngine.Object.FindAnyObjectByType<UnityCommandAdapter>();
            var placement = UnityEngine.Object.FindAnyObjectByType<TouchPlacementController>();
            var sendDock = UnityEngine.Object.FindAnyObjectByType<SendDockController>();
            var laneToggle = UnityEngine.Object.FindAnyObjectByType<LaneViewToggleController>();
            var stress = UnityEngine.Object.FindAnyObjectByType<HeavySendStressHarness>();

            if (driver == null || commands == null || placement == null || sendDock == null || laneToggle == null || stress == null)
            {
                return;
            }

            switch (state)
            {
                case CaptureState.WaitForPlayMode:
                    QueueCapture("default-hud");
                    break;

                case CaptureState.OpenBuildMenu:
                    SetPrivateBool(placement, "isPaletteExpanded", true);
                    ScheduleCaptureThenAdvance("build-menu-open");
                    break;

                case CaptureState.OpenSendMenu:
                    SetPrivateBool(placement, "isPaletteExpanded", false);
                    SetPrivateBool(sendDock, "isExpanded", true);
                    ScheduleCaptureThenAdvance("send-menu-open");
                    break;

                case CaptureState.OpenLaneSelector:
                    SetPrivateBool(sendDock, "isExpanded", false);
                    laneToggle.ToggleView();
                    ScheduleCaptureThenAdvance("lane-selector-open");
                    break;

                case CaptureState.ActiveCombat:
                    laneToggle.ShowLaneView();
                    StartCombat(driver, commands);
                    ScheduleCaptureThenAdvance("active-combat");
                    break;

                case CaptureState.HeavyPressure:
                    stress.StartRun();
                    ScheduleCaptureThenAdvance("heavy-pressure", 4d);
                    break;

                case CaptureState.ReducedEffects:
                    PresentationPreferences.ReducedEffects = true;
                    ScheduleCaptureThenAdvance("reduced-effects-heavy", 2d);
                    break;

                case CaptureState.Results:
                    TryAccelerateMatch(driver);
                    if (driver.LatestMatchSummary == null && EditorApplication.timeSinceStartup - startedAt < 80d)
                    {
                        nextActionAt = EditorApplication.timeSinceStartup + 0.5d;
                        return;
                    }

                    QueueCapture("results-or-late-match");
                    break;

                case CaptureState.Done:
                    Finish(null);
                    break;
            }
        }

        private static void StartCombat(UnitySimulationDriver driver, UnityCommandAdapter commands)
        {
            driver.StartMatch();
            commands.PlaceSampleTower(2, 13);
            commands.PlaceControlTower(4, 12);
            commands.SendSampleCreep();
            commands.SendBruteCreep();
            commands.SendSwarmCreep();
        }

        private static void TryAccelerateMatch(UnitySimulationDriver driver)
        {
            var field = typeof(UnitySimulationDriver).GetField("ticksPerSecond", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(driver, 1200f);
            Time.timeScale = 20f;
            driver.StartMatch();
        }

        private static void ScheduleCaptureThenAdvance(string label, double delaySeconds = 0.75d)
        {
            nextActionAt = EditorApplication.timeSinceStartup + delaySeconds;
            state = (CaptureState)((int)state + 1);
            delayedCaptureLabel = label;
        }

        private static void QueueCapture(string label)
        {
            var path = Path.Combine(OutputDirectory, $"{captureIndex:00}-{label}.png");
            captureIndex++;
            pendingCapturePath = path;
            pendingCaptureLabel = label;
            ScreenCapture.CaptureScreenshot(path);
        }

        private static void AdvanceState(string? completedLabel)
        {
            if (state == CaptureState.WaitForPlayMode)
            {
                state = CaptureState.OpenBuildMenu;
                return;
            }

            if (string.Equals(completedLabel, "results-or-late-match", StringComparison.OrdinalIgnoreCase))
            {
                state = CaptureState.Done;
            }
        }

        private static void Finish(string? error)
        {
            EditorApplication.update -= Update;
            Time.timeScale = 1f;
            PresentationPreferences.ReducedEffects = false;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            if (error == null)
            {
                Debug.Log($"LTW visual review screenshots captured in {OutputDirectory}");
            }
            else
            {
                Debug.LogWarning(error);
            }

            if (exitAfterRun || InternalEditorUtility.inBatchMode)
            {
                EditorApplication.Exit(error == null ? 0 : 1);
            }
        }

        private static void SetPrivateBool(object target, string fieldName, bool value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        private static void ClearPriorGeneratedCaptures()
        {
            foreach (var file in Directory.GetFiles(OutputDirectory, "*.png"))
            {
                File.Delete(file);
            }
        }

        private static bool ShouldExitAfterRun()
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "-ltwExitAfterCapture", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private enum CaptureState
        {
            WaitForPlayMode,
            OpenBuildMenu,
            OpenSendMenu,
            OpenLaneSelector,
            ActiveCombat,
            HeavyPressure,
            ReducedEffects,
            Results,
            Done
        }
    }
}
