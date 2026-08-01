using System.Collections.Generic;
using System.Reflection;
using LTW.Simulation.Bridge;
using LTW.UnityClient.Simulation;
using LTW.UnityClient.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Asserts that a defeated seat's command surfaces stand down, by running it in Play Mode.
    /// </summary>
    /// <remarks>
    /// Sibling of <see cref="SessionModalityCheck"/> and built for the same reason: the client has no
    /// test framework, so a rule that lives only in an <c>OnGUI</c> has nothing watching it. This one
    /// covers OPEN_ITEMS.md item 31 — after `PLAYER 1 OUT` the send dock stayed open and browsable
    /// over the elimination banner, BUILD and SEND stayed live, and the stats bar went on advertising
    /// the +10 income the seat had stopped being paid.
    ///
    /// The load-bearing step is the LAST one, not the elimination. Closing the panels once is easy
    /// and is what a naive fix does; the defect is that nothing stopped them being opened again. So
    /// this forces both open from outside — exactly what a stray tap or any future code path would
    /// do — and requires them to be shut again a frame later.
    ///
    /// Runs headless and exits non-zero:
    ///   Unity -batchmode -executeMethod LTW.UnityClient.Editor.EliminatedSeatCheck.Run
    ///
    /// Deliberately NOT -quit: it drives Play Mode and exits itself when done.
    /// </remarks>
    public static class EliminatedSeatCheck
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const string SessionKey = "LTW.EliminatedSeatCheck.Active";

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

            var host = new GameObject("~LTWEliminatedSeatCheck") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Probe>();
        }

        private sealed class Probe : MonoBehaviour
        {
            private readonly List<string> failures = new List<string>();
            private int step;
            private int settleFrames;
            private UnitySimulationDriver driver;
            private SendDockController dock;
            private TouchPlacementController touch;
            private HudView hud;

            private void Update()
            {
                driver ??= FindAnyObjectByType<UnitySimulationDriver>();
                dock ??= FindAnyObjectByType<SendDockController>();
                touch ??= FindAnyObjectByType<TouchPlacementController>();
                hud ??= FindAnyObjectByType<HudView>();
                if (driver == null || dock == null || touch == null || hud == null)
                {
                    return;
                }

                // Every assertion waits a frame after changing state: the panels are closed by their
                // own OnGUI, which runs after this Update, and the HUD reads a snapshot the driver
                // republishes at the end of its own Update.
                if (settleFrames > 0)
                {
                    settleFrames--;
                    return;
                }

                switch (step)
                {
                    case 0:
                        driver.StartMatch();
                        settleFrames = 2;
                        break;

                    case 1:
                        // Open both panels while still alive. This is the control: if they cannot be
                        // opened here, every later assertion that they are closed proves nothing.
                        ForcePanelsOpen();
                        settleFrames = 2;
                        break;

                    case 2:
                        Require(dock.IsExpanded, "control: the send dock must be open before elimination");
                        Require(touch.IsTowerPaletteExpanded, "control: the build palette must be open before elimination");
                        Require(!driver.IsLocalSeatEliminated, "control: the seat must be alive before elimination");
                        Eliminate();
                        settleFrames = 3;
                        break;

                    case 3:
                        Require(driver.IsLocalSeatEliminated, "the driver must report the local seat as out");
                        Require(!dock.IsExpanded, "the send dock must close on elimination");
                        Require(!touch.IsTowerPaletteExpanded, "the build palette must close on elimination");
                        Require(hud.IsLocalSeatEliminated, "the HUD must know the seat is out");
                        Require(
                            hud.IncomeText == "0",
                            $"the HUD must stop advertising income a dead seat does not earn — showed +{hud.IncomeText}");

                        // The half that matters. Anything that gets these flags set again — a stray
                        // tap, a future entry point — must not survive a frame.
                        ForcePanelsOpen();
                        settleFrames = 3;
                        break;

                    case 4:
                        Require(!dock.IsExpanded, "the send dock must not stay open once reopened while eliminated");
                        Require(!touch.IsTowerPaletteExpanded, "the build palette must not stay open once reopened while eliminated");
                        Finish();
                        return;
                }

                step++;
            }

            private void ForcePanelsOpen()
            {
                SetPrivate(dock, "isExpanded", true);
                SetPrivate(dock, "selectedCategory", -1);
                SetPrivate(touch, "isPaletteExpanded", true);
                SetPrivate(touch, "selectedTowerCategory", -1);
            }

            private void Eliminate()
            {
                var commands = FindAnyObjectByType<UnityCommandAdapter>();
                var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (commands == null || field?.GetValue(commands) is not LocalVerticalSlice sim)
                {
                    failures.Add("could not reach the simulation behind UnityCommandAdapter");
                    return;
                }

                sim.EliminateForLocalPlaytest(sim.LocalPlayerId);
                driver.RefreshSnapshot();
            }

            private static void SetPrivate(object target, string field, object value)
            {
                var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                f?.SetValue(target, value);
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
                    Debug.LogError($"ELIMINATED SEAT FAIL: {failure}");
                }

                if (failures.Count == 0)
                {
                    Debug.Log("ELIMINATED SEAT OK: dock and palette close on elimination and cannot be reopened; the HUD reports +0 income.");
                }

                SessionState.SetBool(SessionKey, false);
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }
    }
}
