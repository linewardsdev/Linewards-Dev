using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using LTW.UnityClient.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Asserts the double-tap gesture selects every tower of one type, by running it in Play Mode.
    /// </summary>
    /// <remarks>
    /// The gesture is worth checking headlessly because none of it is visible in a screenshot: what a
    /// double tap selects is a set, and a capture only shows rings. The three ways it can be wrong
    /// are all silent — picking up a neighbouring tower of a different type, missing one of its own,
    /// or failing to turn MULTI on so the RAISE button never appears.
    ///
    /// Drives the controller's private members by reflection rather than widening its API, matching
    /// what <see cref="RealUiCaptureRunner"/> already does to pose the same component for captures.
    ///
    /// Runs headless and exits non-zero:
    ///   Unity -batchmode -executeMethod LTW.UnityClient.Editor.TowerTypeSelectCheck.Run
    ///
    /// Deliberately NOT -quit: it drives Play Mode and exits itself when done.
    /// </remarks>
    public static class TowerTypeSelectCheck
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const string SessionKey = "LTW.TowerTypeSelectCheck.Active";

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

        /// <remarks>
        /// Same reason <see cref="LocalPlaytestBatchRunner.InstallPlayModePump"/> exists:
        /// EditorApplication.update is not pumped in batchmode Play Mode, so a check that watches a
        /// running match has to borrow a MonoBehaviour tick.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            var host = new GameObject("~LTWTowerTypeSelectCheck") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Probe>();
        }

        private sealed class Probe : MonoBehaviour
        {
            /// <summary>Arrow Ward, the cheapest tower, so three of them are affordable at match start.</summary>
            private const int ArrowRole = 0;

            /// <summary>Three of one type and two of another.</summary>
            /// <remarks>
            /// The mix is the point. A filter that ignored the tower type would return five, and one
            /// that returned only the tapped tower would return one — three is only correct if type,
            /// owner and lane are all being honoured.
            /// </remarks>
            private static readonly GridPosition[] ArrowCells = { new(1, 4), new(1, 6), new(1, 8) };
            private static readonly GridPosition[] SaplingCells = { new(5, 4), new(5, 6) };

            private static readonly ContentId ArrowId = SampleVerticalSliceContent.TowerId;
            private static readonly ContentId SaplingId = new("tower.sapling");

            private readonly List<string> failures = new List<string>();
            private int step;
            private int settleFrames = 4;
            private UnitySimulationDriver driver;
            private TouchPlacementController touch;

            private void Update()
            {
                driver ??= FindAnyObjectByType<UnitySimulationDriver>();
                touch ??= FindAnyObjectByType<TouchPlacementController>();
                if (driver == null || touch == null)
                {
                    return;
                }

                if (settleFrames > 0)
                {
                    settleFrames--;
                    return;
                }

                var simulation = Simulation();
                if (simulation is null)
                {
                    failures.Add("no LocalVerticalSlice on the driver");
                    Finish();
                    return;
                }

                switch (step)
                {
                    case 0:
                        PlaceTowers(simulation);

                        // The controller reads towers out of driver.LatestSnapshot, which the driver
                        // only republishes on its own tick. Asserting in this same frame would test
                        // against a snapshot taken BEFORE these placements and fail for a reason that
                        // has nothing to do with the gesture.
                        settleFrames = 4;
                        break;

                    case 1:
                        RunChecks(simulation);
                        Finish();
                        return;
                }

                step++;
            }

            private void PlaceTowers(LocalVerticalSlice simulation)
            {
                var lane = simulation.LocalPlayerLaneId;
                var me = simulation.LocalPlayerId;

                // 3 arrows at 14G and 2 saplings at 10G is 62G against a 100G opening bank.
                foreach (var cell in ArrowCells)
                {
                    Accept(simulation.PlaceTower(me, lane, ArrowId, cell), $"place arrow at {cell.X},{cell.Y}");
                }

                foreach (var cell in SaplingCells)
                {
                    Accept(simulation.PlaceTower(me, lane, SaplingId, cell), $"place sapling at {cell.X},{cell.Y}");
                }
            }

            private void RunChecks(LocalVerticalSlice simulation)
            {
                var lane = simulation.LocalPlayerLaneId;
                var me = simulation.LocalPlayerId;
                var arrowId = ArrowId;
                var saplingId = SaplingId;
                var arrowCells = ArrowCells;
                var saplingCells = SaplingCells;

                var placed = simulation.GetSnapshot().Towers
                    .Where(tower => tower.OwnerId.Equals(me) && tower.LaneId.Equals(lane))
                    .ToArray();
                var arrow = placed.FirstOrDefault(tower => tower.TowerId.Equals(arrowId));
                if (arrow is null)
                {
                    failures.Add("no arrow tower was placed, so the gesture cannot be exercised");
                    return;
                }

                // The renderer publishes LatestSnapshot on its own tick; the controller reads the
                // selection out of it, so an unsettled snapshot would fail for the wrong reason.
                if (driver.LatestSnapshot is null)
                {
                    failures.Add("driver has no LatestSnapshot yet");
                    return;
                }

                // 1. Single tap on an arrow selects only that one and does NOT enter multi mode.
                SetPrivate(touch, "isMultiSelectMode", false);
                SetPrivate(touch, "lastTowerTapAt", float.NegativeInfinity);
                Invoke(touch, "SelectTowerAt", new Vector2Int(arrow.Position.X, arrow.Position.Y));
                Expect(!MultiSelectMode, "a single tap must not turn MULTI on");

                // 2. A second tap on the SAME cell inside the window is the gesture: MULTI on, and
                //    exactly the three arrows selected — not the saplings beside them.
                Invoke(touch, "SelectTowerAt", new Vector2Int(arrow.Position.X, arrow.Position.Y));
                Expect(MultiSelectMode, "the double tap must turn MULTI on so RAISE is reachable");

                var selected = Selection();
                Expect(selected.Count == arrowCells.Length,
                    $"the double tap should select all {arrowCells.Length} arrows, got {selected.Count}");
                Expect(selected.All(tower => tower.TowerId.Equals(arrowId)),
                    "the selection picked up a tower of another type");

                // 3. Idempotent. Repeating the gesture on the same type must not double-count, which is
                //    what a plain Add without the contains-check would do.
                SetPrivate(touch, "lastTowerTapAt", float.NegativeInfinity);
                Invoke(touch, "SelectTowerAt", new Vector2Int(arrow.Position.X, arrow.Position.Y));
                Invoke(touch, "SelectTowerAt", new Vector2Int(arrow.Position.X, arrow.Position.Y));
                Expect(Selection().Count == arrowCells.Length,
                    $"repeating the gesture changed the count to {Selection().Count}; it should stay at {arrowCells.Length}");

                // 4. Additive across types: double tapping a sapling keeps the arrows.
                var sapling = placed.FirstOrDefault(tower => tower.TowerId.Equals(saplingId));
                if (sapling is not null)
                {
                    SetPrivate(touch, "lastTowerTapAt", float.NegativeInfinity);
                    Invoke(touch, "SelectTowerAt", new Vector2Int(sapling.Position.X, sapling.Position.Y));
                    Invoke(touch, "SelectTowerAt", new Vector2Int(sapling.Position.X, sapling.Position.Y));
                    var mixed = Selection();
                    Expect(mixed.Count == arrowCells.Length + saplingCells.Length,
                        $"double tapping a second type should union to {arrowCells.Length + saplingCells.Length}, got {mixed.Count}");
                    Expect(mixed.Any(tower => tower.TowerId.Equals(arrowId)),
                        "the second gesture discarded the first type instead of adding to it");
                }

                // 5. Two slow taps are NOT a double tap. Rewinding the clock past the window is the
                //    only part of the gesture a Play Mode probe can express, since it cannot wait.
                SetPrivate(touch, "isMultiSelectMode", false);
                Invoke(touch, "SetMultiSelectMode", false);
                SetPrivate(touch, "lastTowerTapAt", Time.unscaledTime - 5f);
                SetPrivate(touch, "lastTowerTapCell", new Vector2Int(arrow.Position.X, arrow.Position.Y));
                Invoke(touch, "SelectTowerAt", new Vector2Int(arrow.Position.X, arrow.Position.Y));
                Expect(!MultiSelectMode, "two taps outside the double-tap window must stay a single selection");
            }

            private bool MultiSelectMode => (bool)GetPrivate(touch, "isMultiSelectMode");

            private List<TowerCombatState> Selection() =>
                (List<TowerCombatState>)GetPrivate(touch, "multiSelection");

            private LocalVerticalSlice Simulation()
            {
                foreach (var name in new[] { "Simulation", "simulation", "Slice", "slice" })
                {
                    var property = driver.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property?.GetValue(driver) is LocalVerticalSlice fromProperty)
                    {
                        return fromProperty;
                    }

                    var field = driver.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field?.GetValue(driver) is LocalVerticalSlice fromField)
                    {
                        return fromField;
                    }
                }

                return null;
            }

            private void Accept(VerticalSliceCommandResult result, string what)
            {
                if (!result.Accepted)
                {
                    failures.Add($"{what} was rejected ({result.RejectionReason})");
                }
            }

            private void Expect(bool condition, string what)
            {
                if (!condition)
                {
                    failures.Add(what);
                }
            }

            private static void SetPrivate(object target, string field, object value) =>
                target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);

            private static object GetPrivate(object target, string field) =>
                target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

            private static void Invoke(object target, string method, params object[] args) =>
                target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, args);

            private void Finish()
            {
                foreach (var failure in failures)
                {
                    Debug.LogError($"TOWERTYPESELECT FAIL: {failure}");
                }

                if (failures.Count == 0)
                {
                    Debug.Log("TOWERTYPESELECT OK: double tap selects every tower of one type, turns MULTI on, unions across types, and ignores slow taps.");
                }

                SessionState.SetBool(SessionKey, false);
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }
    }
}
