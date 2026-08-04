using System.Collections.Generic;
using System.Linq;
using LTW.UnityClient.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Asserts the title screen's brand mark actually resolves and lays out, by running it in Play Mode.
    /// </summary>
    /// <remarks>
    /// A USS <c>background-image: resource("UI/brand_mark")</c> that cannot be resolved does not
    /// throw and does not warn — the element simply draws nothing, at the correct size, in the
    /// correct place. A typo in that path, a texture moved out of a Resources folder, or an import
    /// that produced no readable texture all look identical to a working mark from the console.
    ///
    /// That failure mode has already cost this session twice in other forms: PlayerSettings.SetIcons
    /// reporting success while setting no iOS icons, and every icon slot silently taking the 1024
    /// master because non-power-of-two import had rescaled the rest. Both were found only by reading
    /// the result back. This reads it back.
    ///
    ///   Unity -batchmode -executeMethod LTW.UnityClient.Editor.ShellBrandMarkCheck.Run
    ///
    /// Deliberately NOT -quit: it drives Play Mode and exits itself when done.
    /// </remarks>
    public static class ShellBrandMarkCheck
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const string SessionKey = "LTW.ShellBrandMarkCheck.Active";
        private const string MarkClass = "ltw-brand-mark";

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            var host = new GameObject("~LTWShellBrandMarkCheck") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Probe>();
        }

        private sealed class Probe : MonoBehaviour
        {
            private readonly List<string> failures = new List<string>();
            private int settleFrames = 8;

            private void Update()
            {
                // The shell builds its document over a few frames, and a layout queried too early
                // reports zero for everything — which would fail this check for the wrong reason.
                if (settleFrames-- > 0)
                {
                    return;
                }

                var shell = FindAnyObjectByType<ShellScreenView>();
                if (shell == null)
                {
                    Finish("no ShellScreenView in the scene");
                    return;
                }

                var document = shell.GetComponent<UIDocument>();
                var root = document != null ? document.rootVisualElement : null;
                if (root == null)
                {
                    Finish("the shell UIDocument produced no root");
                    return;
                }

                var mark = root.Query<VisualElement>(className: MarkClass).ToList().FirstOrDefault();
                if (mark == null)
                {
                    Finish($"no element with class '{MarkClass}' — the UXML change is not live");
                    return;
                }

                // The three ways this silently draws nothing, checked separately so the failure says
                // which one it was.
                var image = mark.resolvedStyle.backgroundImage;
                if (image.texture == null && image.sprite == null && image.renderTexture == null)
                {
                    failures.Add("the mark resolves to NO background image — check the USS resource() path");
                }

                if (mark.resolvedStyle.width <= 1f || mark.resolvedStyle.height <= 1f)
                {
                    failures.Add($"the mark laid out at {mark.resolvedStyle.width}x{mark.resolvedStyle.height}");
                }

                if (mark.resolvedStyle.display == DisplayStyle.None || mark.resolvedStyle.opacity <= 0.01f)
                {
                    failures.Add("the mark is present but not displayed");
                }

                if (image.texture != null)
                {
                    var ratio = image.texture.width / (float)image.texture.height;
                    Debug.Log($"SHELLMARK: texture {image.texture.width}x{image.texture.height} (ratio {ratio:F3}), " +
                        $"laid out {mark.resolvedStyle.width}x{mark.resolvedStyle.height}");

                    // An unresolved resource() does NOT leave the background empty — UI Toolkit
                    // substitutes a small built-in placeholder, so the null check above passes and
                    // the mark draws as a tiny grey square. Separated from the squash case because
                    // the two want completely different fixes, and a mutation test that broke the
                    // USS path was reported as an NPOT problem until this told them apart.
                    if (image.texture.width <= 64)
                    {
                        failures.Add($"the mark resolved to a {image.texture.width}px placeholder, not the 512x440 art — the USS resource() path does not resolve");
                    }
                    else if (Mathf.Abs(ratio - 1f) < 0.01f)
                    {
                        failures.Add($"the mark texture is square ({image.texture.width}px) — it was authored 512x440, so NPOT rescale has squashed it");
                    }
                }

                Finish(null);
            }

            private void Finish(string fatal)
            {
                if (fatal != null)
                {
                    failures.Add(fatal);
                }

                foreach (var failure in failures)
                {
                    Debug.LogError($"SHELLMARK FAIL: {failure}");
                }

                if (failures.Count == 0)
                {
                    Debug.Log("SHELLMARK OK: the title screen's brand mark resolves, lays out and is displayed.");
                }

                SessionState.SetBool(SessionKey, false);
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }
    }
}
