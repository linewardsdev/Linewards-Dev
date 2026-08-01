using System.Collections.Generic;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Asserts that a tower built during the opening build countdown reaches the board, by running
    /// it in Play Mode.
    /// </summary>
    /// <remarks>
    /// Sibling of <see cref="SessionModalityCheck"/> and <see cref="EliminatedSeatCheck"/>, and built
    /// for the same reason: the client has no test framework, so a rule that only holds across a
    /// driver, a renderer and a frame boundary has nothing watching it.
    ///
    /// It exists because of what OPEN_ITEMS item 36 changed. <c>UnitySimulationDriver</c> no longer
    /// rebuilds its snapshot on every frame — it rebuilds when the simulation says its state moved —
    /// and the whole risk of that change is a board that lags an input. The opening countdown is the
    /// sharpest case in the game: it is thirty seconds in which the simulation does not tick at all
    /// while the player builds, and <c>UnityCommandAdapter.PlaceTower</c> is deliberately NOT gated
    /// on the match having started, so a build lands with the tick frozen at zero. Any change signal
    /// derived from the tick passes every other check in this repo and fails here — the tower would
    /// simply not appear until the match began.
    ///
    /// The tick assertions are not decoration. Without them a regression that quietly started the
    /// match would make this check pass while testing nothing, which is the failure mode of every
    /// test written around a precondition it does not verify.
    ///
    /// Runs headless and exits non-zero:
    ///   Unity -batchmode -executeMethod LTW.UnityClient.Editor.OpeningCountdownFreshnessCheck.Run
    ///
    /// Deliberately NOT -quit: it drives Play Mode and exits itself when done.
    /// </remarks>
    public static class OpeningCountdownFreshnessCheck
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const string SessionKey = "LTW.OpeningCountdownFreshnessCheck.Active";

        private static bool previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousEnterPlayModeOptions;

        public static void Run()
        {
            SessionState.SetBool(SessionKey, true);
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;

            EditorSceneManager.OpenScene(ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Installs the probe, because EditorApplication.update is not pumped in batchmode Play Mode.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            var host = new GameObject("~LTWOpeningCountdownFreshnessCheck") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Probe>();
        }

        private sealed class Probe : MonoBehaviour
        {
            /// <summary>Cells picked well clear of the lane's spawn and exit rows.</summary>
            private static readonly Vector2Int[] BuildCells =
            {
                new Vector2Int(1, 4),
                new Vector2Int(3, 6),
                new Vector2Int(2, 9)
            };

            private readonly List<string> failures = new List<string>();
            private int step;
            private int settleFrames;
            private int towersBefore;
            private int towerObjectsBefore;
            private long countdownTick;
            private UnitySimulationDriver driver;
            private UnityCommandAdapter commands;
            private UnityVerticalSliceRenderer renderer;

            private void Update()
            {
                driver ??= FindAnyObjectByType<UnitySimulationDriver>();
                commands ??= FindAnyObjectByType<UnityCommandAdapter>();
                renderer ??= FindAnyObjectByType<UnityVerticalSliceRenderer>();
                if (driver == null || commands == null || renderer == null)
                {
                    return;
                }

                // Every assertion waits a frame after changing state: the driver republishes at the
                // end of its own Update and the renderer projects that snapshot in its own.
                if (settleFrames > 0)
                {
                    settleFrames--;
                    return;
                }

                // Two builds, so the check covers a tower arriving on a board that already gained one
                // this countdown as well as the first one. Each build is one step of the switch.
                var buildStep = (step - 2) / 2;

                switch (step)
                {
                    case 0:
                        driver.BeginOpeningBuildCountdown();
                        settleFrames = 3;
                        break;

                    case 1:
                        // Controls. If any of these are wrong the later assertions prove nothing:
                        // a match that is already running would refresh for reasons that have
                        // nothing to do with the case being tested.
                        Require(driver.IsOpeningBuildCountdown, "control: the driver must be in the opening build countdown");
                        Require(!driver.HasStarted, "control: the match must not have started");
                        Require(driver.LatestSnapshot != null, "control: the driver must have published a snapshot");
                        countdownTick = driver.LatestSnapshot?.Tick.Value ?? -1L;
                        Require(countdownTick == 0L, $"control: the countdown must be frozen at tick 0, saw {countdownTick}");
                        Require(driver.LatestSnapshot?.Towers.Count == 0, "control: the board must start empty");
                        step++;
                        return;

                    case 2:
                    case 4:
                        towersBefore = driver.LatestSnapshot?.Towers.Count ?? 0;
                        towerObjectsBefore = renderer.ActiveTowerPresentationCount;
                        var cell = BuildCells[buildStep];
                        var placed = commands.PlaceSampleTower(cell.x, cell.y);
                        Require(placed.Accepted, $"build {buildStep + 1} at ({cell.x},{cell.y}) was rejected ({placed.RejectionReason}), so this check is not measuring what it claims");
                        settleFrames = 3;
                        break;

                    case 3:
                    case 5:
                        // The assertion the whole change rests on. A tick-based gate reaches here
                        // with the old count still published and fails on this line.
                        Require(
                            driver.LatestSnapshot?.Towers.Count == towersBefore + 1,
                            $"build {buildStep + 1}: the driver must publish the tower built during the countdown — snapshot still shows {driver.LatestSnapshot?.Towers.Count} towers, not {towersBefore + 1}");
                        // Tower objects specifically, not the aggregate: a timed presentation
                        // appearing in the same frame moves the aggregate by the same amount a tower
                        // does, so the aggregate version of this line passed against a deliberately
                        // broken change signal that never drew the tower at all.
                        Require(
                            renderer.ActiveTowerPresentationCount == towerObjectsBefore + 1,
                            $"build {buildStep + 1}: the renderer must draw the tower built during the countdown — active towers stayed at {renderer.ActiveTowerPresentationCount}, not {towerObjectsBefore + 1}");
                        Require(
                            (driver.LatestSnapshot?.Tick.Value ?? -1L) == countdownTick,
                            "the tick must not have advanced, or this check has stopped testing the between-ticks case");
                        break;

                    case 6:
                        Require(driver.IsOpeningBuildCountdown, "the countdown must still be running at the end, or the builds above were not countdown builds");
                        Finish();
                        return;
                }

                step++;
            }

            private void Require(bool condition, string what)
            {
                if (!condition)
                {
                    failures.Add(what);
                }
            }

            private void Finish()
            {
                foreach (var failure in failures)
                {
                    Debug.LogError($"COUNTDOWN FRESHNESS FAIL: {failure}");
                }

                if (failures.Count == 0)
                {
                    Debug.Log("COUNTDOWN FRESHNESS OK: towers built with the tick frozen at 0 reach both the published snapshot and the renderer within a frame.");
                }

                SessionState.SetBool(SessionKey, false);
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }
    }
}
