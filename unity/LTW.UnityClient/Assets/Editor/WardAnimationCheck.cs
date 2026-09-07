using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Asserts every ward on the board is actually MOVING, by watching the scene graph in Play Mode.
    /// </summary>
    /// <remarks>
    /// Reported from an iPad build: none of the ward animations appear to run. The question that
    /// needs answering first is whether that is an iOS/Xcode problem or a problem everywhere, and
    /// the only way to settle it is to measure the same thing in the editor.
    ///
    /// So this measures the scene graph rather than the renderer's internals. <c>UpdateTowerMotion</c>
    /// drives a tower's Body child (idle breathe, drift, aim yaw, recoil) and, on the towers that
    /// have one, a Ring/Dish/Spire spin part. "Animating" therefore means those transforms differ
    /// across frames — which is observable from outside without reflecting into private renderer
    /// state. <c>TowerMotionAmplitudeProbe</c> does reflect, and is currently broken because the
    /// field it reached for has since been renamed; a check that reads only public scene structure
    /// cannot rot that way.
    ///
    /// Amplitude is deliberately NOT judged here — that is TowerMotionAmplitudeProbe's job, in
    /// screen pixels at the board camera. This answers the cruder and more urgent question: is the
    /// motion running at all.
    ///
    ///   Unity -batchmode -executeMethod LTW.UnityClient.Editor.WardAnimationCheck.Run
    ///
    /// Deliberately NOT -quit: it drives Play Mode and exits itself.
    /// </remarks>
    public static class WardAnimationCheck
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const string SessionKey = "LTW.WardAnimationCheck.Active";

        /// <summary>Roles chosen to cover every shape of tower motion the renderer can drive.</summary>
        private static readonly (string Id, int X, int Y)[] Subjects =
        {
            ("tower.arrow", 2, 4),        // split Head: aim yaw and recoil
            ("tower.relay", 4, 5),        // Dish: a spin part, plus the rest tilt
            ("tower.prism", 2, 7),        // Spire: a spin part
            ("tower.control", 4, 8),      // StemCore + Ring under one pivot
            ("tower.sapling", 2, 10),     // Grove, which leans entirely on idle sway
        };

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

            var host = new GameObject("~LTWWardAnimationCheck") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Probe>();
        }

        private sealed class Track
        {
            public string Label = string.Empty;
            public Transform Node = null!;
            public Quaternion FirstRotation;
            public Vector3 FirstPosition;
            public Vector3 FirstScale;
            public float MaxRotationDelta;
            public float MaxPositionDelta;
            public float MaxScaleDelta;

            public bool Moved => MaxRotationDelta > 0.05f || MaxPositionDelta > 0.0005f || MaxScaleDelta > 0.0005f;
        }

        private sealed class Probe : MonoBehaviour
        {
            private readonly List<string> failures = new List<string>();
            private readonly List<Track> tracks = new List<Track>();
            private int settleFrames = 30;
            private int step;
            private int sampled;
            private float sampledSeconds;

            private void Update()
            {
                if (settleFrames-- > 0)
                {
                    return;
                }

                switch (step)
                {
                    case 0:
                        if (!PlaceTowers())
                        {
                            Finish("could not place towers; no simulation reachable");
                            return;
                        }

                        step++;
                        settleFrames = 20;
                        return;

                    case 1:
                        if (!BindTracks())
                        {
                            Finish("no ward Body transforms found in the scene after placing towers");
                            return;
                        }

                        step++;
                        return;

                    case 2:
                        Sample();
                        sampled++;
                        sampledSeconds += Time.unscaledDeltaTime;

                        // Counted in SECONDS, not frames, and that distinction is the whole
                        // measurement. Every authored motion here is a function of Time, so a frame
                        // count measures nothing on its own — batchmode runs unthrottled at roughly
                        // 2800fps, where 120 frames is 0.04 seconds of game time and a 32 deg/sec
                        // spin registers as 1.4 degrees. The first version of this probe reported
                        // exactly that and it reads like a stopped animation.
                        //
                        // Twelve seconds, because the slowest authored drift is an ANGULAR rate fed
                        // to Mathf.Sin rather than a frequency: Spore Cloud's 0.35 has a period of
                        // 2*pi/0.35, about 18 seconds, so a short window catches a fraction of the
                        // stroke and reports a slow drifter as barely moving.
                        if (sampledSeconds >= 12f)
                        {
                            step++;
                        }

                        return;

                    default:
                        Report();
                        return;
                }
            }

            private static bool PlaceTowers()
            {
                var commands = FindAnyObjectByType<UnityCommandAdapter>();
                if (commands == null)
                {
                    return false;
                }

                var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(commands) is not LocalVerticalSlice sim)
                {
                    return false;
                }

                sim.GrantLocalPlaytestGold(sim.LocalPlayerId, new Gold(100000));

                // Towers only turn and recoil when they have something to shoot at, so the lane has
                // to be live and populated. Measuring an empty board answers a question nobody
                // asked: of course a turret with no target holds its heading.
                var driver = FindAnyObjectByType<UnitySimulationDriver>();
                driver?.StartMatch();

                var attacker = sim.LocalPlaytestSenderForLane(sim.LocalPlayerLaneId);
                if (attacker.HasValue)
                {
                    sim.GrantLocalPlaytestGold(attacker.Value, new Gold(100000));
                    for (var wave = 0; wave < 12; wave++)
                    {
                        sim.QueueSend(attacker.Value, SampleVerticalSliceContent.BruteCreepId);
                    }
                }

                foreach (var (id, x, y) in Subjects)
                {
                    var result = sim.PlaceTower(sim.LocalPlayerId, sim.LocalPlayerLaneId, new ContentId(id), new GridPosition(x, y));
                    if (!result.Accepted)
                    {
                        Debug.LogWarning($"WARDANIM could not place {id} at ({x},{y}): {result.RejectionReason}");
                    }
                }

                return true;
            }

            private bool BindTracks()
            {
                // Pooled tower instances are parented under the renderer's own object and named for
                // their prefab. Both the Body child and any spin part are tracked, because they are
                // driven separately and either one alone stopping is a real defect.
                foreach (var root in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                {
                    if (!root.name.StartsWith("Tower_"))
                    {
                        continue;
                    }

                    Add(root, "Body");

                    // HeadPivot, not Body, is where aim yaw and recoil land — Tower3DImportPipeline
                    // gives every split-turret tower one so the barrel can turn without dragging the
                    // base with it. Tracking only Body measures the idle breathe and misses the two
                    // motions a player is most likely to mean by "the wards aren't animating".
                    Add(root, "HeadPivot");
                    Add(root, "Barrel");
                    foreach (var spin in TowerVisualTuning.SpinPartNames)
                    {
                        Add(root, spin);
                    }
                }

                return tracks.Count > 0;
            }

            private void Add(Transform root, string childName)
            {
                var node = FindDeep(root, childName);
                if (node == null)
                {
                    return;
                }

                tracks.Add(new Track
                {
                    Label = $"{root.name}/{childName}",
                    Node = node,
                    FirstRotation = node.localRotation,
                    FirstPosition = node.localPosition,
                    FirstScale = node.localScale,
                });
            }

            private void Sample()
            {
                foreach (var track in tracks)
                {
                    if (track.Node == null)
                    {
                        continue;
                    }

                    track.MaxRotationDelta = Mathf.Max(track.MaxRotationDelta, Quaternion.Angle(track.FirstRotation, track.Node.localRotation));
                    track.MaxPositionDelta = Mathf.Max(track.MaxPositionDelta, Vector3.Distance(track.FirstPosition, track.Node.localPosition));
                    track.MaxScaleDelta = Mathf.Max(track.MaxScaleDelta, Vector3.Distance(track.FirstScale, track.Node.localScale));
                }
            }

            private void Report()
            {
                foreach (var track in tracks.OrderBy(t => t.Label))
                {
                    Debug.Log(
                        $"WARDANIM {(track.Moved ? "MOVING" : "STATIC")} {track.Label,-46} " +
                        $"rot={track.MaxRotationDelta:0.000} pos={track.MaxPositionDelta:0.0000} scale={track.MaxScaleDelta:0.0000}");
                }

                var heads = tracks.Where(t => t.Label.EndsWith("/HeadPivot")).ToArray();
                Debug.Log($"WARDANIM HEADS: {heads.Count(h => h.Moved)}/{heads.Length} turret heads turned or recoiled");

                var bodies = tracks.Where(t => t.Label.EndsWith("/Body")).ToArray();
                var stillBodies = bodies.Where(t => !t.Moved).ToArray();
                foreach (var track in stillBodies)
                {
                    failures.Add($"{track.Label} never moved across {sampled} frames");
                }

                Debug.Log($"WARDANIM SUMMARY: {bodies.Length - stillBodies.Length}/{bodies.Length} ward bodies moving, {tracks.Count} transforms tracked, over {sampledSeconds:0.0}s / {sampled} frames");
                Finish(null);
            }

            private static Transform FindDeep(Transform parent, string name)
            {
                if (parent.name == name)
                {
                    return parent;
                }

                for (var index = 0; index < parent.childCount; index++)
                {
                    var found = FindDeep(parent.GetChild(index), name);
                    if (found != null)
                    {
                        return found;
                    }
                }

                return null;
            }

            private void Finish(string fatal)
            {
                if (fatal != null)
                {
                    failures.Add(fatal);
                }

                foreach (var failure in failures)
                {
                    Debug.LogError($"WARDANIM FAIL: {failure}");
                }

                if (failures.Count == 0)
                {
                    Debug.Log("WARDANIM OK: every ward body is animating in the editor.");
                }

                SessionState.SetBool(SessionKey, false);
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }
    }
}
