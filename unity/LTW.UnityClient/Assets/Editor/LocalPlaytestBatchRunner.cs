#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LTW.Simulation.Bots;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class LocalPlaytestBatchRunner
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        /// <summary>
        /// Wall-clock budget for a whole batch run, not sim time.
        /// </summary>
        /// <remarks>
        /// Raised from 60s alongside CombatService.BaseMovementCost. Creeps now take three ticks per
        /// route cell instead of one, so the same seed runs ~3,500 ticks rather than ~900, and the
        /// old budget left very little headroom on a cold Library.
        /// </remarks>
        private const double TimeoutSeconds = 180d;

        /// <summary>
        /// Marks a batch run as in-flight, in storage that outlives a domain reload.
        /// </summary>
        private const string SessionKeyActive = "LTW.LocalPlaytestBatchRunner.Active";

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
        private static LocalMatchOptions matchOptions = LocalMatchOptions.Default;
        private static string evidenceLabel = "default";

        /// <summary>Simulation ticks per second of scaled time. Overridden with -ltwTickRate.</summary>
        /// <remarks>
        /// The default of 1200 is 300x the shipped 4, which is what makes a batch run finish in
        /// seconds — but it makes the presentation-object counts in the evidence report MEANINGLESS,
        /// and they were reported as if they were not.
        ///
        /// Transient effects (beams, flashes, floating text) are spawned per simulation TICK and
        /// expire on Time.time. Running 300x the ticks per unit of time therefore banks 300x the
        /// effects against an unchanged expiry rate, and they pile up. Measured on the same seed and
        /// the same completed tick: peak active presentation objects reads 41,911 at 1200 ticks/s
        /// and 1,721 at the shipped 4 — a 24x difference in a number that was being read as a
        /// mobile budget, with identical creep and tower peaks in both runs.
        ///
        /// So: leave the default for a fast pass/fail run, and pass -ltwTickRate 4 whenever the
        /// pooling numbers are the point. This is the "normal-speed stress capture" MVP_STATUS.md
        /// asks for; it costs about 45s instead of 12s.
        /// </remarks>
        private static float tickRate = 1200f;

        /// <summary>
        /// Unity time scale. Overridden with -ltwTimeScale.
        /// </summary>
        /// <remarks>
        /// Scales Time.time and Time.deltaTime TOGETHER, so unlike tickRate it does not distort the
        /// effects-per-tick ratio — it just makes the wall clock shorter. That is why a realistic
        /// capture lowers tickRate and leaves this alone.
        /// </remarks>
        private static float timeScale = 20f;

        public static void Run()
        {
            SessionState.SetBool(SessionKeyActive, true);
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
            matchOptions = ReadOptionsFromCommandLine();
            evidenceLabel = ReadStringArgument("-ltwEvidenceLabel") ?? $"seed-{matchOptions.Seed}";
            tickRate = Mathf.Max(1f, ReadFloatArgument("-ltwTickRate") ?? 1200f);
            timeScale = Mathf.Clamp(ReadFloatArgument("-ltwTimeScale") ?? 20f, 0.1f, 100f);
            LocalMatchRuntimeOptions.PendingOptions = matchOptions;

            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.update += Update;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Drives <see cref="Update"/> from inside Play Mode, where the editor's own update loop does
        /// not reach.
        /// </summary>
        /// <remarks>
        /// This is the whole reason batch runs used to hang. The runner is driven by
        /// EditorApplication.update, and in BATCHMODE that callback is not pumped once Play Mode
        /// starts. So the match ran with nothing watching it — and the timeout could not fire either,
        /// because the timeout lives inside the same unpumped Update. The symptom was a process
        /// sitting at 80% CPU with a completely healthy log and no progress, forever.
        ///
        /// It was diagnosable from one line in that log: UnityVerticalSliceRenderer.Update() WAS
        /// running. MonoBehaviour ticks are fine in batchmode Play Mode; only the editor callback is
        /// not. So the runner borrows a MonoBehaviour tick instead of inventing one.
        ///
        /// RuntimeInitializeOnLoadMethod is used rather than an EditorApplication play-mode callback
        /// for the same reason — it is a runtime hook, so it does not depend on the mechanism that is
        /// broken here.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallPlayModePump()
        {
            if (!SessionState.GetBool(SessionKeyActive, false))
            {
                return;
            }

            // A domain reload would zero the statics below and make the first timeout check fire
            // instantly against startedAt = 0. Command-line arguments outlive a reload, so the run is
            // re-derived from them rather than persisted — one source of truth that cannot drift.
            if (startedAt <= 0d)
            {
                matchOptions = ReadOptionsFromCommandLine();
                evidenceLabel = ReadStringArgument("-ltwEvidenceLabel") ?? $"seed-{matchOptions.Seed}";
                tickRate = Mathf.Max(1f, ReadFloatArgument("-ltwTickRate") ?? 1200f);
                timeScale = Mathf.Clamp(ReadFloatArgument("-ltwTimeScale") ?? 20f, 0.1f, 100f);
                state = BatchState.WaitingForPlayMode;
                startedAt = EditorApplication.timeSinceStartup;
            }

            var pump = new GameObject("~LTWBatchPump");
            pump.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(pump);
            pump.AddComponent<BatchPump>();
        }

        private sealed class BatchPump : MonoBehaviour
        {
            private void Update() => LocalPlaytestBatchRunner.Update();
        }

        private static int lastPumpedFrame = -1;

        private static void Update()
        {
            // The editor loop and the play-mode pump can both reach this in the same frame. The state
            // machine counts FRAMES (VerifyingReset waits 40 of them), so ticking twice per frame
            // would quietly halve every such window.
            if (Application.isPlaying)
            {
                if (Time.frameCount == lastPumpedFrame)
                {
                    return;
                }

                lastPumpedFrame = Time.frameCount;
            }

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
                    TicksPerSecondField?.SetValue(driver, tickRate);
                    Time.timeScale = timeScale;
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
            var path = Path.Combine(directory, $"local-unity-batch-{SafeFilePart(evidenceLabel)}-{DateTime.Now:yyyyMMdd-HHmmss}.md");

            using var writer = new StreamWriter(path);
            writer.WriteLine("# Local Unity Batch Playtest Evidence");
            writer.WriteLine();
            writer.WriteLine($"- Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"- Scene: `{ScenePath}`");
            writer.WriteLine($"- Unity Version: `{Application.unityVersion}`");
            writer.WriteLine($"- Evidence Label: `{evidenceLabel}`");
            writer.WriteLine($"- Configured Seed: {matchOptions.Seed}");
            writer.WriteLine($"- Local Player Seat: P{matchOptions.LocalPlayerId.Value} (lane {matchOptions.LocalPlayerId.Value})");
            // Starts at 1, not 2: lane 1 is only guaranteed human when the local seat is P1.
            for (var playerId = 1; playerId <= matchOptions.LaneCount; playerId++)
            {
                var id = new PlayerId(playerId);
                var enabled = matchOptions.IsBotEnabledFor(id);
                writer.WriteLine($"- P{playerId} Bot: {(enabled ? "enabled" : "disabled")}, Profile: {matchOptions.BotProfileFor(id)}, Primary Creep: `{matchOptions.PrimaryCreepFor(id)?.Value ?? "default"}`");
            }
            writer.WriteLine($"- Result: {(failure is null && resetClean ? "pass" : "fail")}");
            writer.WriteLine($"- Wall Time Seconds: {(completedAt > 0d ? completedAt - startedAt : EditorApplication.timeSinceStartup - startedAt):F2}");
            writer.WriteLine($"- Completed Tick: {completedTick}");
            writer.WriteLine($"- Winner: {(winnerId == 0 ? "unknown" : "P" + winnerId)}");
            writer.WriteLine($"- Accepted Replay Commands: {acceptedReplayCommands}");
            writer.WriteLine($"- Playtest Report: `{exportedPlaytestReport ?? "not exported"}`");
            writer.WriteLine($"- Peak Creeps: {peakCreeps}");
            writer.WriteLine($"- Peak Towers: {peakTowers}");
            writer.WriteLine($"- Simulation Tick Rate: {tickRate:F0}/s (shipped is 4/s){(tickRate > 4f ? " — ACCELERATED, see note below" : "")}");
            writer.WriteLine($"- Unity Time Scale: {timeScale:F1}x");
            writer.WriteLine($"- Peak Active Presentation Objects: {peakActivePresentationObjects}");
            writer.WriteLine($"- Peak Pooled Presentation Objects: {peakPooledPresentationObjects}");
            if (tickRate > 4f)
            {
                writer.WriteLine();
                writer.WriteLine($"> **The presentation-object peaks above are inflated by the {tickRate / 4f:F0}x tick rate and are NOT a mobile budget.** Transient effects are spawned per simulation tick and expire on `Time.time`, so running more ticks per unit of time banks proportionally more of them against an unchanged expiry rate. Measured on seed 1: 41,911 peak active at 1200/s versus 1,721 at the shipped 4/s, with identical creep and tower peaks. Re-run with `-ltwTickRate 4` when the pooling numbers are the point.");
            }
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
            SessionState.SetBool(SessionKeyActive, false);
            EditorApplication.update -= Update;
            Time.timeScale = 1f;
            LocalMatchRuntimeOptions.PendingOptions = LocalMatchOptions.Default;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            EditorApplication.Exit(error is null ? 0 : 1);
        }

        private static LocalMatchOptions ReadOptionsFromCommandLine()
        {
            var seed = ReadIntArgument("-ltwSeed") ?? 1;
            // -ltwLocalPlayer seats the local (human) player somewhere other than lane 1. Whichever
            // lane the human occupies is automatically not bot-driven, and the lane they vacate
            // becomes bot-driven, so a batch run with no human input still plays out fully.
            var options = new LocalMatchOptions(seed: seed, localPlayerId: ReadIntArgument("-ltwLocalPlayer") ?? 1);

            for (var playerId = 2; playerId <= LocalMatchOptions.MaxLaneCount; playerId++)
            {
                var argSuffix = $"-ltwP{playerId}";
                var enabled = ReadBoolArgument($"{argSuffix}Enabled", fallback: true);
                var profile = ReadEnumArgument(argSuffix, options.BotProfileFor(new PlayerId(playerId)));
                var creep = ReadContentIdArgument($"{argSuffix}Creep");
                options = options.WithLane(playerId, enabled: enabled, profile: profile, primaryCreepId: creep);
            }

            return options;
        }

        private static string? ReadStringArgument(string name)
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

        private static float? ReadFloatArgument(string name) =>
            float.TryParse(ReadStringArgument(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                : null;

        private static int? ReadIntArgument(string name) =>
            int.TryParse(ReadStringArgument(name), out var value) ? value : null;

        private static bool ReadBoolArgument(string name, bool fallback) =>
            bool.TryParse(ReadStringArgument(name), out var value) ? value : fallback;

        private static BotDecisionProfile ReadEnumArgument(string name, BotDecisionProfile fallback) =>
            Enum.TryParse(ReadStringArgument(name), ignoreCase: true, out BotDecisionProfile value) ? value : fallback;

        private static ContentId? ReadContentIdArgument(string name)
        {
            var value = ReadStringArgument(name);
            return string.IsNullOrWhiteSpace(value) ? null : new ContentId(value);
        }

        private static string SafeFilePart(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var characters = value
                .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : char.ToLowerInvariant(character))
                .ToArray();
            return new string(characters);
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
