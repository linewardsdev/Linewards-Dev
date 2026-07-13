#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class LocalPlaytestBatchRunner
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const double TimeoutSeconds = 60d;

        private static readonly FieldInfo? TicksPerSecondField = typeof(UnitySimulationDriver).GetField(
            "ticksPerSecond",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static BatchState state;
        private static double startedAt;
        private static double completedAt;
        private static int resetFrames;
        private static int peakCreeps;
        private static int peakTowers;
        private static int peakActivePresentationObjects;
        private static int peakPooledPresentationObjects;
        private static string? failure;
        private static bool previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousEnterPlayModeOptions;
        private static long completedTick;
        private static int winnerId;
        private static int acceptedReplayCommands;
        private static string? exportedPlaytestReport;
        private static string[] finalPlayerLines = Array.Empty<string>();

        public static void Run()
        {
            state = BatchState.WaitingForPlayMode;
            startedAt = EditorApplication.timeSinceStartup;
            completedAt = 0d;
            resetFrames = 0;
            peakCreeps = 0;
            peakTowers = 0;
            peakActivePresentationObjects = 0;
            peakPooledPresentationObjects = 0;
            failure = null;
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            completedTick = 0;
            winnerId = 0;
            acceptedReplayCommands = 0;
            exportedPlaytestReport = null;
            finalPlayerLines = Array.Empty<string>();

            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.update += Update;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.EnterPlaymode();
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
            {
                Finish("Timed out before the local Unity playtest completed.");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            var driver = UnityEngine.Object.FindAnyObjectByType<UnitySimulationDriver>();
            var renderer = UnityEngine.Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            var recorder = UnityEngine.Object.FindAnyObjectByType<LocalPlaytestRecorder>();

            if (driver is null || renderer is null || recorder is null)
            {
                return;
            }

            switch (state)
            {
                case BatchState.WaitingForPlayMode:
                    TicksPerSecondField?.SetValue(driver, 1200f);
                    Time.timeScale = 20f;
                    driver.StartMatch();
                    state = BatchState.RunningMatch;
                    break;

                case BatchState.RunningMatch:
                    RecordPeaks(driver, renderer);
                    if (driver.LatestMatchSummary is not null)
                    {
                        CaptureCompletedMatch(driver);
                        completedAt = EditorApplication.timeSinceStartup;
                        state = BatchState.ExportingReport;
                    }

                    break;

                case BatchState.ExportingReport:
                    RecordPeaks(driver, renderer);
                    var reportPath = recorder.LatestReportPath ?? recorder.ExportNow();
                    if (reportPath is null || !File.Exists(reportPath))
                    {
                        Finish("Match completed, but the playtest report was not exported.");
                        return;
                    }

                    exportedPlaytestReport = reportPath;
                    driver.ResetMatch();
                    recorder.ResetRecorder();
                    state = BatchState.VerifyingReset;
                    break;

                case BatchState.VerifyingReset:
                    resetFrames++;
                    if (resetFrames < 40)
                    {
                        return;
                    }

                    var snapshot = driver.LatestSnapshot;
                    var resetClean =
                        snapshot.Tick.Value == 0 &&
                        snapshot.Creeps.Count == 0 &&
                        snapshot.Towers.Count == 0 &&
                        driver.LatestMatchSummary is null &&
                        renderer.ActivePresentationObjectCount == 0;

                    WriteEvidence(driver, renderer, resetClean);
                    Finish(resetClean ? null : "Reset left stale simulation or presentation state.");
                    break;
            }
        }

        private static void RecordPeaks(UnitySimulationDriver driver, UnityVerticalSliceRenderer renderer)
        {
            var snapshot = driver.LatestSnapshot;
            peakCreeps = Math.Max(peakCreeps, snapshot?.Creeps.Count ?? 0);
            peakTowers = Math.Max(peakTowers, snapshot?.Towers.Count ?? 0);
            peakActivePresentationObjects = Math.Max(peakActivePresentationObjects, renderer.ActivePresentationObjectCount);
            peakPooledPresentationObjects = Math.Max(peakPooledPresentationObjects, renderer.PooledPresentationObjectCount);
        }

        private static void CaptureCompletedMatch(UnitySimulationDriver driver)
        {
            var summary = driver.LatestMatchSummary;
            var replay = driver.LatestReplay;
            completedTick = replay?.CompletedAtTick.Value ?? summary?.CompletedAtTick.Value ?? 0;
            winnerId = summary?.WinnerId.Value ?? 0;
            acceptedReplayCommands = replay?.AcceptedCommands.Count ?? 0;
            finalPlayerLines = summary is null
                ? Array.Empty<string>()
                : Array.ConvertAll(
                    summary.Players.ToArray(),
                    player => $"- P{player.PlayerId.Value}: lives {player.Lives.Amount}, income {player.Income.Amount}, gold {player.Gold.Amount}, eliminated {player.IsEliminated}");
        }

        private static void WriteEvidence(UnitySimulationDriver driver, UnityVerticalSliceRenderer renderer, bool resetClean)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var directory = Path.Combine(root, "docs", "playtest-evidence");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"local-unity-batch-{DateTime.Now:yyyyMMdd-HHmmss}.md");

            using var writer = new StreamWriter(path);
            writer.WriteLine("# Local Unity Batch Playtest Evidence");
            writer.WriteLine();
            writer.WriteLine($"- Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"- Scene: `{ScenePath}`");
            writer.WriteLine($"- Unity Version: `{Application.unityVersion}`");
            writer.WriteLine($"- Result: {(failure is null && resetClean ? "pass" : "fail")}");
            writer.WriteLine($"- Wall Time Seconds: {(completedAt > 0d ? completedAt - startedAt : EditorApplication.timeSinceStartup - startedAt):F2}");
            writer.WriteLine($"- Completed Tick: {completedTick}");
            writer.WriteLine($"- Winner: {(winnerId == 0 ? "unknown" : "P" + winnerId)}");
            writer.WriteLine($"- Accepted Replay Commands: {acceptedReplayCommands}");
            writer.WriteLine($"- Playtest Report: `{exportedPlaytestReport ?? "not exported"}`");
            writer.WriteLine($"- Peak Creeps: {peakCreeps}");
            writer.WriteLine($"- Peak Towers: {peakTowers}");
            writer.WriteLine($"- Peak Active Presentation Objects: {peakActivePresentationObjects}");
            writer.WriteLine($"- Peak Pooled Presentation Objects: {peakPooledPresentationObjects}");
            writer.WriteLine($"- Reset Clean: {resetClean}");
            writer.WriteLine($"- Active Presentation Objects After Reset: {renderer.ActivePresentationObjectCount}");
            writer.WriteLine($"- Pooled Presentation Objects After Reset: {renderer.PooledPresentationObjectCount}");

            if (finalPlayerLines.Length > 0)
            {
                writer.WriteLine();
                writer.WriteLine("## Final Players");
                foreach (var line in finalPlayerLines)
                {
                    writer.WriteLine(line);
                }
            }
        }

        private static void Finish(string? error)
        {
            failure = error;
            EditorApplication.update -= Update;
            Time.timeScale = 1f;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            EditorApplication.Exit(error is null ? 0 : 1);
        }

        private enum BatchState
        {
            WaitingForPlayMode,
            RunningMatch,
            ExportingReport,
            VerifyingReset
        }
    }
}
