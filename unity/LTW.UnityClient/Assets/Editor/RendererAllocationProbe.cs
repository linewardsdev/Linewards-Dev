using System;
using System.Globalization;
using System.Reflection;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Measures how many managed bytes the client allocates per frame on a deterministic mid-match
    /// board, isolating <see cref="UnityVerticalSliceRenderer"/> and <see cref="UnitySimulationDriver"/>
    /// one at a time by enabling each against the same frozen board in the same play session.
    /// </summary>
    /// <remarks>
    /// Written for OPEN_ITEMS item 24 and kept because that item's ledger row cites its numbers: a
    /// figure nobody can re-run is a claim, not a measurement. Item 36 then reused it and added the
    /// driver phase. It is not a pass/fail gate and nothing runs it automatically — it prints and
    /// exits 0 either way, like the motion capture next to it.
    ///
    /// Four phases, all on the same board:
    ///
    /// - PAUSED, renderer on, then PAUSED, renderer off. The match is paused AND the driver component
    ///   is disabled, so every frame sees the identical snapshot object. This is item 24's premise
    ///   stated as a measurement — whatever a frame allocates here is work repeated against an input
    ///   that did not change — and the renderer-off reading is the noise floor to judge it against.
    /// - PAUSED, renderer still off, driver back ON. Same frozen board, same paused match, so the
    ///   driver has nothing new to publish and every byte it allocates is a rebuild of something that
    ///   did not change. Subtracting the renderer-off/driver-off floor above gives the driver's own
    ///   share, which is item 36's measurement. The renderer stays off for it deliberately: at the
    ///   figures item 24 left behind the driver is the larger term, and measuring it underneath the
    ///   renderer would put the smaller number's noise on top of the larger one.
    /// - RUNNING, renderer on, driver on, over a fixed number of ticks at the shipped 4/s, for the
    ///   figure a real match sees. Its frame count is reported, since the frames-per-tick ratio moves
    ///   with how fast each frame is.
    ///
    /// The driver phase measures the driver and everything downstream of it that only runs because it
    /// republished — <see cref="UI.HudView"/> and the diagnostics overlay read what it publishes every
    /// frame. That is the right boundary for item 36, whose claim is about what a frame costs when the
    /// simulation had nothing new to say, not about one method's allocation in isolation.
    ///
    /// Run with:
    ///     Unity -batchmode -nographics -projectPath &lt;project&gt; \
    ///           -executeMethod LTW.UnityClient.Editor.RendererAllocationProbe.Run -logFile &lt;log&gt;
    /// then read the ALLOCPROBE lines out of the log. Takes about four minutes, most of it filling
    /// the board.
    /// </remarks>
    public static class RendererAllocationProbe
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const string SessionKeyActive = "LTW.RendererAllocationProbe.Active";

        /// <summary>Frames sampled per phase, after a warm-up.</summary>
        private const int PhaseFrames = 300;

        private const int WarmupFrames = 30;

        /// <summary>Tick rate/time scale used to build a busy board before measuring.</summary>
        private const float FillTickRate = 400f;
        private const float FillTimeScale = 20f;

        /// <summary>
        /// Simulation tick the fill phase runs to before measuring.
        /// </summary>
        /// <remarks>
        /// A tick rather than a wall-clock duration, because the board has to be the SAME board in
        /// the run being compared against. Filling for a fixed number of seconds does not do that:
        /// two runs of this probe against the same build produced 110 creeps/432 towers and 53
        /// creeps/283 towers, which is a bigger difference than anything being measured. All-bot
        /// matches are deterministic, so the same tick is the same board. Mid-match, where the
        /// eight-lane board is busiest.
        /// </remarks>
        private const long FillTargetTick = 3000L;

        /// <summary>
        /// Simulation ticks the free-running phase is measured over.
        /// </summary>
        /// <remarks>
        /// Ticks, not frames: bytes per FRAME depends on how many frames fall between two ticks, and
        /// that ratio is not a constant across the runs being compared — measured at 4.2, 18.8 and
        /// 7.1 frames/tick in three phases of one run, because the frame rate itself moves with how
        /// much work each frame does. Fixing the tick count instead makes the phase a fixed amount
        /// of GAMEPLAY, and the frames that fell inside it are reported alongside.
        /// </remarks>
        private const int MeasureTicks = 40;

        private static readonly FieldInfo TicksPerSecondField = typeof(UnitySimulationDriver).GetField(
            "ticksPerSecond",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private enum Phase
        {
            WaitingForPlayMode,
            Filling,
            Settling,
            PausedOn,
            PausedOff,
            PausedDriverOn,
            RunningOn,
            Done
        }

        private static Phase phase;
        private static double startedAt;
        private static int phaseFrame;
        private static long phaseStartBytes;
        private static long phaseStartTick;
        private static int phaseStartFrame;
        private static int lastPumpedFrame = -1;

        private static double onA;
        private static double off;
        private static double driverOn;
        private static double onB;
        private static int measuredCreeps;
        private static int measuredTowers;
        private static int measuredActiveObjects;

        public static void Run()
        {
            SessionState.SetBool(SessionKeyActive, true);
            phase = Phase.WaitingForPlayMode;
            startedAt = EditorApplication.timeSinceStartup;
            phaseFrame = 0;
            onA = off = driverOn = onB = 0d;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.update += Update;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallPump()
        {
            if (!SessionState.GetBool(SessionKeyActive, false))
            {
                return;
            }

            if (startedAt <= 0d)
            {
                phase = Phase.WaitingForPlayMode;
                startedAt = EditorApplication.timeSinceStartup;
            }

            var pump = new GameObject("~LTWAllocProbePump");
            pump.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(pump);
            pump.AddComponent<Pump>();
        }

        private sealed class Pump : MonoBehaviour
        {
            private void Update() => RendererAllocationProbe.Update();
        }

        private static void Update()
        {
            if (Application.isPlaying)
            {
                if (Time.frameCount == lastPumpedFrame)
                {
                    return;
                }

                lastPumpedFrame = Time.frameCount;
            }

            // Raised from 600s with the fourth phase: the fill dominates the run, but a 330-frame
            // phase measuring a driver that allocates most of a megabyte a frame is not free either.
            if (EditorApplication.timeSinceStartup - startedAt > 900d)
            {
                Finish("Timed out.");
                return;
            }

            if (!EditorApplication.isPlaying || phase == Phase.Done)
            {
                return;
            }

            var driver = UnityEngine.Object.FindAnyObjectByType<UnitySimulationDriver>();
            var renderer = UnityEngine.Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            if (driver == null || renderer == null)
            {
                return;
            }

            switch (phase)
            {
                case Phase.WaitingForPlayMode:
                    TicksPerSecondField?.SetValue(driver, FillTickRate);
                    Time.timeScale = FillTimeScale;
                    driver.StartMatch();
                    phase = Phase.Filling;
                    break;

                case Phase.Filling:
                    if (driver.LatestMatchSummary is not null || (driver.LatestSnapshot?.Tick.Value ?? 0L) >= FillTargetTick)
                    {
                        // Back to shipped rates for the measurement itself: the whole point is
                        // frames-per-snapshot, and an accelerated tick rate collapses that ratio.
                        TicksPerSecondField?.SetValue(driver, 4f);
                        Time.timeScale = 1f;

                        // Paused, so every frame of the next two phases sees the SAME snapshot.
                        // That is item 24's premise stated exactly: whatever a frame allocates here
                        // is work repeated against an input that did not change.
                        driver.PauseMatch();
                        phase = Phase.Settling;
                        phaseFrame = 0;
                    }

                    break;

                case Phase.Settling:
                    // A few paused frames before the driver is switched off, so its last drained
                    // event list is empty. Freezing it with events still in hand would have the
                    // renderer re-fire the same cues on every frame of the measurement.
                    if (++phaseFrame >= WarmupFrames)
                    {
                        // The driver allocates a fresh snapshot every frame of its own accord
                        // (GetSnapshot copies the creep, tower and aim arrays; GetReplayRecord and
                        // GetBotDiagnostics run alongside it), which measured around 800 KB/frame
                        // at this board size — an order of magnitude more than what is left of the
                        // renderer, and enough to bury it. Frozen, LatestSnapshot keeps returning
                        // the same object and the renderer keeps rendering it, which is exactly the
                        // condition being measured.
                        driver.enabled = false;
                        BeginPhase(Phase.PausedOn, renderer, enabled: true);
                    }

                    break;

                case Phase.PausedOn:
                    if (StepFrames(driver, renderer, out var pausedOnBytes))
                    {
                        onA = pausedOnBytes;
                        BeginPhase(Phase.PausedOff, renderer, enabled: false);
                    }

                    break;

                case Phase.PausedOff:
                    if (StepFrames(driver, renderer, out var pausedOffBytes))
                    {
                        off = pausedOffBytes;
                        // Driver back on, renderer still off, match still paused. Nothing about the
                        // board can change in this phase, so everything the driver allocates here is
                        // a republish of state that did not move.
                        driver.enabled = true;
                        BeginPhase(Phase.PausedDriverOn, renderer, enabled: false);
                    }

                    break;

                case Phase.PausedDriverOn:
                    if (StepFrames(driver, renderer, out var driverOnBytes))
                    {
                        driverOn = driverOnBytes;
                        // Free-running for the realistic figure: the renderer back on, the driver
                        // still on, the match ticking at the shipped 4/s.
                        driver.TogglePause();
                        BeginPhase(Phase.RunningOn, renderer, enabled: true);
                    }

                    break;

                case Phase.RunningOn:
                    if (StepTicks(driver, renderer, out var runningBytes))
                    {
                        onB = runningBytes;
                        Finish(null);
                    }

                    break;
            }
        }

        private static void BeginPhase(Phase next, UnityVerticalSliceRenderer renderer, bool enabled)
        {
            renderer.enabled = enabled;
            phase = next;
            phaseFrame = 0;
        }

        private static long lastMonoUsed = -1;
        private static long accumulatedMonoGrowth;

        /// <summary>
        /// Accumulates managed heap GROWTH, which is the only allocation signal this runtime gives.
        /// </summary>
        /// <remarks>
        /// The three obvious calls were each tried and each fails here, measured rather than assumed:
        /// <c>GC.GetTotalAllocatedBytes</c> is not in Unity's API surface at all and does not
        /// compile; <c>GC.GetAllocatedBytesForCurrentThread</c> compiles and returns a flat zero, so
        /// the first version of this probe reported 0 B/frame in every phase; and the
        /// <c>ProfilerRecorder</c> counter "GC Allocated In Frame" reports Valid but returns the same
        /// constant on every frame in batchmode, which showed up as three different phases producing
        /// byte-identical results.
        ///
        /// So: sample the heap each frame and sum only the frames where it grew. A collection shows
        /// as a drop and is skipped rather than subtracted, which makes this a FLOOR on what was
        /// allocated rather than an exact count. That is enough for its one job, comparing two phases
        /// of one session or the same phase across two builds, and it is stable enough to do it — the
        /// renderer-off phase has read within 30 KB/frame across separate runs.
        /// </remarks>
        private static void SampleAllocations()
        {
            var monoUsed = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            if (lastMonoUsed >= 0 && monoUsed > lastMonoUsed)
            {
                accumulatedMonoGrowth += monoUsed - lastMonoUsed;
            }

            lastMonoUsed = monoUsed;
        }

        /// <summary>Warms up, then records the phase's starting counters. True once warmed.</summary>
        private static bool Warmed(UnitySimulationDriver driver, UnityVerticalSliceRenderer renderer)
        {
            SampleAllocations();
            phaseFrame++;
            if (phaseFrame < WarmupFrames)
            {
                return false;
            }

            if (phaseFrame == WarmupFrames)
            {
                phaseStartTick = driver.LatestSnapshot?.Tick.Value ?? 0L;
                phaseStartBytes = accumulatedMonoGrowth;
                phaseStartFrame = phaseFrame;
                measuredCreeps = Math.Max(measuredCreeps, driver.LatestSnapshot?.Creeps.Count ?? 0);
                measuredTowers = Math.Max(measuredTowers, driver.LatestSnapshot?.Towers.Count ?? 0);
                measuredActiveObjects = Math.Max(measuredActiveObjects, renderer.ActivePresentationObjectCount);
                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "ALLOCPROBE phase {0} starts at tick {1}: creeps {2} towers {3}",
                    phase,
                    phaseStartTick,
                    driver.LatestSnapshot?.Creeps.Count ?? 0,
                    driver.LatestSnapshot?.Towers.Count ?? 0));
                return false;
            }

            return true;
        }

        private static void ReportPhase(long frames, long ticks, double bytesPerFrame)
        {
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "ALLOCPROBE phase {0}: {1:F0} B/frame over {2} frames and {3} ticks ({4:F1} frames/tick)",
                phase,
                bytesPerFrame,
                frames,
                ticks,
                ticks > 0L ? frames / (double)ticks : 0d));
        }

        /// <summary>Measures over a fixed number of FRAMES. Used while the match is paused.</summary>
        private static bool StepFrames(UnitySimulationDriver driver, UnityVerticalSliceRenderer renderer, out double bytesPerFrame)
        {
            bytesPerFrame = 0d;
            if (!Warmed(driver, renderer) || phaseFrame < phaseStartFrame + PhaseFrames)
            {
                return false;
            }

            var frames = phaseFrame - phaseStartFrame;
            bytesPerFrame = (accumulatedMonoGrowth - phaseStartBytes) / (double)frames;
            ReportPhase(frames, (driver.LatestSnapshot?.Tick.Value ?? 0L) - phaseStartTick, bytesPerFrame);
            return true;
        }

        /// <summary>Measures over a fixed number of TICKS. Used while the match is running.</summary>
        private static bool StepTicks(UnitySimulationDriver driver, UnityVerticalSliceRenderer renderer, out double bytesPerFrame)
        {
            bytesPerFrame = 0d;
            if (!Warmed(driver, renderer))
            {
                return false;
            }

            var ticks = (driver.LatestSnapshot?.Tick.Value ?? 0L) - phaseStartTick;
            if (ticks < MeasureTicks)
            {
                return false;
            }

            var frames = phaseFrame - phaseStartFrame;
            bytesPerFrame = (accumulatedMonoGrowth - phaseStartBytes) / (double)frames;
            ReportPhase(frames, ticks, bytesPerFrame);
            return true;
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Update;
            SessionState.SetBool(SessionKeyActive, false);
            phase = Phase.Done;

            if (error != null)
            {
                Debug.LogError("ALLOCPROBE " + error);
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "ALLOCPROBE RESULT creeps {0} towers {1} activeObjects {2} | PAUSED renderer-on {3:F0} B/frame, " +
                "renderer-off {4:F0} B/frame, renderer share {5:F0} B/frame ({6:F1} KB/frame) | " +
                "PAUSED driver-on {7:F0} B/frame, driver share {8:F0} B/frame ({9:F1} KB/frame) | " +
                "RUNNING both-on {10:F0} B/frame",
                measuredCreeps, measuredTowers, measuredActiveObjects,
                onA, off, onA - off, (onA - off) / 1024d,
                driverOn, driverOn - off, (driverOn - off) / 1024d,
                onB));

            EditorApplication.isPlaying = false;
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(error == null ? 0 : 1);
            }
        }
    }
}
