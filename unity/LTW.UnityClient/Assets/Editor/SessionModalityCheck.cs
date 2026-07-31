using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LTW.UnityClient.Simulation;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Asserts which session phases suppress the HUD, by running them in Play Mode.
    /// </summary>
    /// <remarks>
    /// This exists because the same defect shipped twice. The HUD stands down while a session panel
    /// owns the display, and the opening build countdown was wrongly counted as one — so the panel
    /// that says "Place opening towers" hid the build palette.
    ///
    /// The first fix removed the countdown from the modal condition and changed nothing, because
    /// `BeginOpeningBuildCountdown` also sets `HasStarted = false` and `IsPaused = true`, and the
    /// condition tested those too. It was reported fixed, was not, and nothing caught that but a
    /// human opening the app — which is the gap this closes.
    ///
    /// Runs headless and exits non-zero:
    ///   Unity -batchmode -executeMethod LTW.UnityClient.Editor.SessionModalityCheck.Run
    ///
    /// Deliberately NOT -quit: it drives Play Mode and exits itself when done.
    /// </remarks>
    public static class SessionModalityCheck
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const string SessionKey = "LTW.SessionModalityCheck.Active";

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
        /// Installs the driver, because EditorApplication.update is not pumped in batchmode Play Mode.
        /// </summary>
        /// <remarks>
        /// Same reason <see cref="LocalPlaytestBatchRunner.InstallPlayModePump"/> exists: the editor
        /// callback stops firing once Play Mode starts in batch, so a check that watches a running
        /// match has to borrow a MonoBehaviour tick.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            var host = new GameObject("~LTWModalityCheck") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Probe>();
        }

        private sealed class Probe : MonoBehaviour
        {
            private readonly List<string> failures = new List<string>();
            private int step;
            private int settleFrames;
            private UnitySimulationDriver driver;

            private void Update()
            {
                driver ??= FindAnyObjectByType<UnitySimulationDriver>();
                if (driver == null)
                {
                    return;
                }

                // The overlay publishes ModalScreenActive from its own Update, so every assertion
                // waits a frame after changing phase rather than reading a stale value.
                if (settleFrames > 0)
                {
                    settleFrames--;
                    return;
                }

                switch (step)
                {
                    case 0:
                        Expect(true, "pre-match title screen should own the display");
                        driver.BeginOpeningBuildCountdown();
                        settleFrames = 2;
                        break;

                    case 1:
                        // The one that matters. HasStarted is false and IsPaused is true here, so a
                        // naive condition reports modal and the build palette disappears.
                        Expect(false, $"opening build countdown must NOT own the display (HasStarted={driver.HasStarted}, IsPaused={driver.IsPaused}, IsOpeningBuildCountdown={driver.IsOpeningBuildCountdown})");
                        driver.StartMatch();
                        settleFrames = 2;
                        break;

                    case 2:
                        Expect(false, "a live match should not own the display");
                        Finish();
                        return;
                }

                step++;
            }

            private void Expect(bool modal, string what)
            {
                if (LocalSessionFlowOverlay.ModalScreenActive != modal)
                {
                    failures.Add($"{what} — expected ModalScreenActive={modal}, got {LocalSessionFlowOverlay.ModalScreenActive}");
                }
            }

            private void Finish()
            {
                foreach (var failure in failures)
                {
                    Debug.LogError($"MODALITY FAIL: {failure}");
                }

                if (failures.Count == 0)
                {
                    Debug.Log("MODALITY OK: title owns the display; the opening build countdown and a live match do not.");
                }

                SessionState.SetBool(SessionKey, false);
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }
    }
}
