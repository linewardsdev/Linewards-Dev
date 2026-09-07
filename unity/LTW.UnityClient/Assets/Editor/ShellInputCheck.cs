using System.Linq;
using System.Reflection;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Proves that a tap on a UI Toolkit shell button reaches the simulation, in Play Mode.
    /// </summary>
    /// <remarks>
    /// The shell screens are this project's first runtime UI Toolkit surface, and the conditions are
    /// unusual: legacy input (<c>activeInputHandler: 0</c>), and no EventSystem anywhere, because
    /// <c>Assets/Scenes/LocalVerticalSlice.unity</c> contains no GameObjects at all. Neither a
    /// compile nor a screenshot can tell you whether buttons work under those conditions — a
    /// perfectly rendered menu whose buttons are inert looks exactly like a working one.
    ///
    /// So this asserts the two halves separately, and says which is which:
    ///
    /// 1. DISPATCH, by proof. A pointer press and release are sent into the START GAME button and
    ///    the check then asserts on the SIMULATION, not on the button: the opening build countdown
    ///    must have begun. That covers UXML naming, the button wiring, the action interface, and the
    ///    session-flow call, end to end.
    /// 2. THE INPUT SOURCE, by inspection. Which object is feeding pointer input to runtime panels
    ///    is internal to the UIElements module, so it is read (never written) through reflection and
    ///    logged. What that answers is whether UI Toolkit fell back to its own event system — the
    ///    behaviour that makes an EventSystem unnecessary here — or whether nothing is driving the
    ///    panel at all, which would be a silent, ship-breaking configuration error.
    ///
    /// Runs headless and exits non-zero, like SessionModalityCheck:
    ///   Unity -batchmode -executeMethod LTW.UnityClient.Editor.ShellInputCheck.Run
    /// Deliberately NOT -quit: it drives Play Mode and exits itself.
    /// </remarks>
    public static class ShellInputCheck
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const string SessionKey = "LTW.ShellInputCheck.Active";

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

            var host = new GameObject("~LTWShellInputCheck") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Probe>();
        }

        private sealed class Probe : MonoBehaviour
        {
            private int step;
            private int settleFrames = 10;
            private int waited;
            private UnitySimulationDriver driver;
            private Button startButton;

            private void Update()
            {
                if (settleFrames > 0)
                {
                    settleFrames--;
                    return;
                }

                switch (step)
                {
                    case 0:
                        if (!Locate())
                        {
                            if (++waited > 600)
                            {
                                Finish("the title screen never produced a laid-out START GAME button");
                            }

                            return;
                        }

                        ReportInputSource();
                        step++;
                        settleFrames = 2;
                        return;

                    case 1:
                        Click(startButton);
                        step++;
                        settleFrames = 3;
                        return;

                    case 2:
                        if (driver.IsOpeningBuildCountdown)
                        {
                            Debug.Log(
                                "SHELLINPUT OK: a pointer press on START GAME reached the simulation — " +
                                $"IsOpeningBuildCountdown={driver.IsOpeningBuildCountdown}, " +
                                $"remaining={driver.OpeningBuildCountdownRemaining:0.0}s.");
                            Finish(null);
                            return;
                        }

                        Finish(
                            "a pointer press on START GAME did NOT start the build countdown " +
                            $"(HasStarted={driver.HasStarted}, IsPaused={driver.IsPaused})");
                        return;
                }
            }

            private bool Locate()
            {
                driver ??= FindAnyObjectByType<UnitySimulationDriver>();
                if (driver == null)
                {
                    return false;
                }

                var document = FindAnyObjectByType<UIDocument>();
                if (document == null || document.rootVisualElement == null)
                {
                    return false;
                }

                startButton = document.rootVisualElement.Q<Button>("title-start");
                if (startButton == null)
                {
                    return false;
                }

                // The title screen has to actually be up and laid out, or a pointer event aimed at
                // the button would be aimed at a zero-sized rectangle.
                return startButton.worldBound.width > 1f && startButton.worldBound.height > 1f;
            }

            /// <summary>
            /// Sends a press and release at the centre of an element, as a pointer would.
            /// </summary>
            /// <remarks>
            /// <c>Button</c> answers to a pointer sequence through its <c>Clickable</c> manipulator,
            /// so this is the shape of the real thing rather than a direct call to the handler —
            /// which would have proven only that a delegate can be invoked. The submit event is sent
            /// as well because it is the keyboard/gamepad route to the same action, and a shell menu
            /// that is unreachable without a touchscreen would be a defect of its own.
            /// </remarks>
            private static void Click(VisualElement element)
            {
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

                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = element;
                    element.SendEvent(submit);
                }
            }

            /// <summary>
            /// Places a pooled pointer event, whose position setters are not part of the public API.
            /// </summary>
            /// <remarks>
            /// A pooled event initialises to the origin, and <c>Clickable</c> tests the release
            /// against the element's rectangle. The origin happens to be inside every element's own
            /// local rectangle, so the click would register regardless — but a test that passes by
            /// coincidence is not a test, so the real coordinates are written in where the property
            /// allows it and the check carries on unbothered where it does not.
            /// </remarks>
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

            /// <summary>
            /// Logs which object is feeding pointer input to runtime panels. Read-only reflection.
            /// </summary>
            private static void ReportInputSource()
            {
                var uguiComponents = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                    .Select(behaviour => behaviour.GetType().Name)
                    .Where(name => name is "EventSystem" or "StandaloneInputModule" or "PanelEventHandler" or "PanelRaycaster")
                    .Distinct()
                    .ToArray();

                var runtimeUtility = typeof(PanelSettings).Assembly
                    .GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
                const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

                object defaultEventSystem = null;
                object usesDefault = null;
                if (runtimeUtility != null)
                {
                    defaultEventSystem =
                        runtimeUtility.GetProperty("defaultEventSystem", Statics)?.GetValue(null) ??
                        runtimeUtility.GetField("s_DefaultEventSystem", Statics)?.GetValue(null);
                    usesDefault =
                        runtimeUtility.GetProperty("useDefaultEventSystem", Statics)?.GetValue(null) ??
                        runtimeUtility.GetField("s_UseDefaultEventSystem", Statics)?.GetValue(null);
                }

                var inputSource = "unknown";
                if (defaultEventSystem != null)
                {
                    var inputProperty = defaultEventSystem.GetType()
                        .GetProperty("input", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    inputSource = inputProperty?.GetValue(defaultEventSystem)?.GetType().FullName ?? "null";
                }

                Debug.Log(
                    $"SHELLINPUT source: uGUI components in scene=[{string.Join(", ", uguiComponents)}] " +
                    $"defaultEventSystem={(defaultEventSystem == null ? "none" : defaultEventSystem.GetType().Name)} " +
                    $"useDefaultEventSystem={usesDefault?.ToString() ?? "unknown"} " +
                    $"inputSource={inputSource} activeInputHandler=legacy");
            }

            private void Finish(string error)
            {
                if (error != null)
                {
                    Debug.LogError($"SHELLINPUT FAIL: {error}");
                }

                SessionState.SetBool(SessionKey, false);
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                EditorApplication.Exit(error == null ? 0 : 1);
            }
        }
    }
}
