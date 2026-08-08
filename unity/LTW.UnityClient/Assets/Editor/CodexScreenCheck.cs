using System.Collections.Generic;
using System.Reflection;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Opens the codex from the title screen in Play Mode, walks every unit in both halves, and
    /// asserts the preview stage is actually drawing something.
    /// </summary>
    /// <remarks>
    /// The thing this exists to catch is a BLANK STAGE. A camera that renders nothing produces a
    /// uniform texture, and a uniform texture bound to a styled element looks exactly like a
    /// deliberately empty panel — there is no exception, no warning and no missing asset. This
    /// project has already shipped that failure twice on the capture path, where <c>-nographics</c>
    /// wrote a flat grey PNG and exited 0.
    ///
    /// So the assertion is on PIXELS, not on wiring: read the render texture back and require that
    /// it is not all one colour. Everything else it checks — the button reaching the screen, each
    /// unit resolving a name and six stat tiles — would already be visible in a screenshot. The
    /// blankness would not.
    ///
    ///   Unity -batchmode -executeMethod LTW.UnityClient.Editor.CodexScreenCheck.Run
    ///
    /// Deliberately NOT -quit (it drives Play Mode and exits itself) and deliberately NOT
    /// -nographics, which would make the very thing under test unmeasurable.
    /// </remarks>
    public static class CodexScreenCheck
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const string SessionKey = "LTW.CodexScreenCheck.Active";

        private static bool previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions previousEnterPlayModeOptions;

        public static void Run()
        {
            SessionState.SetBool(SessionKey, true);
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
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

            var host = new GameObject("~LTWCodexScreenCheck") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Probe>();
        }

        private sealed class Probe : MonoBehaviour
        {
            /// <summary>Frames left for the stage to mount, animate and render before it is sampled.</summary>
            /// <remarks>
            /// A camera with a target texture renders on its own schedule, so the frame in which a
            /// subject is instantiated is not the frame in which it appears. Sampling too early
            /// reads the clear colour and reports a blank stage that is merely a late one.
            /// </remarks>
            private const int SettleFramesPerUnit = 4;

            private readonly List<string> failures = new List<string>();
            private VisualElement root;
            private int settleFrames = 10;
            private int step;
            private int locateAttempts;
            private int visited;
            private int variedTextures;
            private int modelless;

            private void Update()
            {
                if (settleFrames-- > 0)
                {
                    return;
                }

                // Counted separately from `step`, which is the state machine's index. Reusing it
                // here would leave the machine starting at whatever the retry count reached.
                if (root == null && !Locate())
                {
                    if (++locateAttempts > 200)
                    {
                        Finish("never found a laid-out title screen with a CODEX button");
                    }

                    return;
                }

                switch (step)
                {
                    case 0:
                        Click(root.Q<Button>("title-codex"));
                        step++;
                        settleFrames = 6;
                        return;

                    case 1:
                        if (!IsShown(root.Q<VisualElement>("screen-codex")))
                        {
                            Finish("pressing CODEX did not bring up screen-codex");
                            return;
                        }

                        if (IsShown(root.Q<VisualElement>("screen-title")))
                        {
                            Finish("screen-title is still shown behind the codex");
                            return;
                        }

                        step++;
                        settleFrames = SettleFramesPerUnit;
                        return;

                    case 2:
                        InspectCurrentUnit();
                        visited++;

                        // Counts come from the catalogs, never from a literal. They were both 15
                        // when this was written and the wards are 16 now, so a hardcoded 15/30 did
                        // not fail — it quietly walked fifteen of the sixteen wards and reported
                        // success. A coverage check that silently under-covers as the roster grows
                        // is worse than no check, because it reads as a passing one.
                        if (visited == TowerCatalog.Entries.Length)
                        {
                            Click(root.Q<Button>("codex-tab-creeps"));
                            settleFrames = SettleFramesPerUnit;
                            return;
                        }

                        if (visited >= TowerCatalog.Entries.Length + CreepCatalog.Entries.Length)
                        {
                            step++;
                            settleFrames = 2;
                            return;
                        }

                        Click(root.Q<Button>("codex-next"));
                        settleFrames = SettleFramesPerUnit;
                        return;

                    case 3:
                        // BACK is a session action, so this asserts the whole round trip: the codex
                        // must let go of the screen, not merely draw a button that says it will.
                        Click(root.Q<Button>("codex-back"));
                        step++;
                        settleFrames = 6;
                        return;

                    default:
                        if (!IsShown(root.Q<VisualElement>("screen-title")))
                        {
                            failures.Add("BACK from the codex did not return to the title screen");
                        }

                        var drawable = visited - modelless;
                        if (drawable > 0 && variedTextures == 0)
                        {
                            failures.Add(
                                "the preview stage produced a uniform texture for every one of the " +
                                $"{drawable} units that have a model — the stage is rendering nothing");
                        }
                        else if (variedTextures < drawable)
                        {
                            failures.Add(
                                $"only {variedTextures} of {drawable} units with a model rendered anything; " +
                                "the rest produced a uniform texture");
                        }

                        Finish(null);
                        return;
                }
            }

            /// <summary>
            /// Checks one codex page: it must name a unit, show six stat tiles, and draw pixels.
            /// </summary>
            private void InspectCurrentUnit()
            {
                var label = root.Q<Label>("codex-name");
                var position = root.Q<Label>("codex-position");
                var where = position != null ? position.text : $"#{visited}";

                if (label == null || string.IsNullOrWhiteSpace(label.text) || label.text == "-")
                {
                    failures.Add($"codex entry {where} has no unit name");
                }

                var tiles = root.Q<VisualElement>("codex-stats")?.Query<VisualElement>(className: "ltw-stat").ToList();
                if (tiles == null || tiles.Count != 6)
                {
                    failures.Add($"codex entry {where} ({label?.text}) built {tiles?.Count ?? 0} stat tiles, expected 6");
                }

                var traits = root.Q<Label>("codex-traits");
                if (traits == null || string.IsNullOrWhiteSpace(traits.text) || traits.text == "-")
                {
                    failures.Add($"codex entry {where} ({label?.text}) has no trait line");
                }

                // A unit whose art has not been drawn yet is an expected state, not a failure — the
                // Twin Crescent Ward shipped playable with no prefab at all. It is counted
                // separately rather than skipped, so "the roster has gaps" and "the stage is
                // broken" stay distinguishable in the result line.
                var pending = root.Q<Label>("codex-preview-pending");
                if (pending != null && pending.ClassListContains("is-shown"))
                {
                    modelless++;
                    return;
                }

                var stage = FindAnyObjectByType<LTW.UnityClient.UI.UnitPreviewStage>();
                if (stage == null || stage.Texture == null)
                {
                    failures.Add($"codex entry {where} ({label?.text}) has no preview stage texture");
                    return;
                }

                if (HasVariedPixels(stage.Texture))
                {
                    variedTextures++;
                }
                else
                {
                    Debug.LogWarning($"CODEX: entry {where} ({label?.text}) rendered a uniform texture.");
                }
            }

            /// <summary>
            /// Whether a render texture contains more than one colour.
            /// </summary>
            /// <remarks>
            /// Uniform means "the camera cleared and drew nothing", which is the failure. Comparing
            /// against an expected colour instead would be worse on both sides: it would need the
            /// clear colour written down in a second place, and it would pass for a texture that is
            /// uniformly the WRONG colour, which is equally broken.
            /// </remarks>
            private static bool HasVariedPixels(RenderTexture texture)
            {
                var previous = RenderTexture.active;
                var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                try
                {
                    RenderTexture.active = texture;
                    readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                    readback.Apply();

                    var pixels = readback.GetPixels32();
                    if (pixels.Length == 0)
                    {
                        return false;
                    }

                    var first = pixels[0];
                    foreach (var pixel in pixels)
                    {
                        if (pixel.r != first.r || pixel.g != first.g || pixel.b != first.b || pixel.a != first.a)
                        {
                            return true;
                        }
                    }

                    return false;
                }
                finally
                {
                    RenderTexture.active = previous;
                    DestroyImmediate(readback);
                }
            }

            private static bool IsShown(VisualElement screen) =>
                screen != null && screen.ClassListContains("is-shown");

            private bool Locate()
            {
                var document = FindAnyObjectByType<UIDocument>();
                if (document == null || document.rootVisualElement == null)
                {
                    return false;
                }

                var button = document.rootVisualElement.Q<Button>("title-codex");
                if (button == null || button.worldBound.width <= 1f)
                {
                    return false;
                }

                root = document.rootVisualElement;
                return true;
            }

            private void Click(VisualElement element)
            {
                if (element == null)
                {
                    failures.Add("a codex control was missing when the check tried to press it");
                    return;
                }

                var centre = element.worldBound.center;
                var local = element.WorldToLocal(centre);

                using (var down = PointerDownEvent.GetPooled())
                {
                    down.target = element;
                    Position(down, centre, local);
                    element.SendEvent(down);
                }

                using (var up = PointerUpEvent.GetPooled())
                {
                    up.target = element;
                    Position(up, centre, local);
                    element.SendEvent(up);
                }
            }

            /// <summary>Places a pooled pointer event, whose position setters are not public API.</summary>
            private static void Position(EventBase evt, Vector2 world, Vector2 local)
            {
                const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var type = evt.GetType();
                type.GetProperty("position", Flags)?.SetValue(evt, (Vector3)world);
                type.GetProperty("localPosition", Flags)?.SetValue(evt, (Vector3)local);
                type.GetProperty("button", Flags)?.SetValue(evt, 0);
                type.GetProperty("pointerId", Flags)?.SetValue(evt, PointerId.mousePointerId);
                type.GetProperty("clickCount", Flags)?.SetValue(evt, 1);
            }

            private void Finish(string fatal)
            {
                if (fatal != null)
                {
                    failures.Add(fatal);
                }

                foreach (var failure in failures)
                {
                    Debug.LogError($"CODEX FAIL: {failure}");
                }

                if (failures.Count == 0)
                {
                    Debug.Log(
                        $"CODEX OK: opened from the title, walked all {visited} units " +
                        $"({TowerCatalog.Entries.Length} wards + {CreepCatalog.Entries.Length} creeps), " +
                        $"{variedTextures} rendered a non-uniform preview, {modelless} " +
                        $"{(modelless == 1 ? "has" : "have")} no model yet, and BACK returned to the title.");
                }

                SessionState.SetBool(SessionKey, false);
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }
    }
}
