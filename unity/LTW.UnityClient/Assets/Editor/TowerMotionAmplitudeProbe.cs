using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using LTW.UnityClient.UI;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Measures how far each tower actually MOVES on screen, in pixels, at the real board camera.
    /// </summary>
    /// <remarks>
    /// TOWER_ANIMATION_ALIGNMENT.md closes by naming exactly one thing it did not verify: "how the
    /// fixed towers look in motion ... the Grove line in particular now leans entirely on its sway,
    /// and that sway has never been watched at the current board scale." That is the gap this fills.
    ///
    /// It is a measurement rather than a screenshot because the question is quantitative. Idle
    /// motion is authored in world units — breathe is a scale fraction, drift a lateral offset — and
    /// whether 0.03 of either survives projection to a phone screen is not a matter of taste. A
    /// still frame cannot answer it and a motion capture sampled every 0.45s only answers "did
    /// anything move at all", which is the question the last pass already answered.
    ///
    /// Sampling covers 20 seconds. The window has to clear a full cycle of the slowest authored
    /// motion, and that is far longer than the profile fields suggest: breatheHz and driftHz are
    /// fed straight into Mathf.Sin as an ANGULAR RATE, not a frequency, so Spore Cloud's "0.35"
    /// drift has a period of 2*pi/0.35 = 18s, not 2.9s. A 4s window — which looks generous against
    /// the field names — catches only a fifth of that arc, and reports the slow drifters as barely
    /// moving when they are simply mid-stroke. Sapling, the fastest drifter, measured 100% of its
    /// authored amplitude in the same 4s window, which is what exposed the error.
    /// </remarks>
    public static class TowerMotionAmplitudeProbe
    {
        private const string ScenePath = "Assets/Scenes/LocalVerticalSlice.unity";
        private const int Width = 1080;
        private const int Height = 1920;
        private const float SampleSeconds = 20f;

        private sealed class Track
        {
            public string Role = string.Empty;
            public GameObject Root = null!;
            public float MinX = float.MaxValue;
            public float MaxX = float.MinValue;
            public float MinY = float.MaxValue;
            public float MaxY = float.MinValue;
            public float MinHeight = float.MaxValue;
            public float MaxHeight = float.MinValue;
            public float MinWorldX = float.MaxValue;
            public float MaxWorldX = float.MinValue;
            public int Samples;
            public float RootScale;
        }

        private static readonly List<Track> Tracks = new();
        private static bool started;
        private static bool placed;
        private static bool bound;
        private static int bindAtFrame;
        private static readonly List<GridPosition> PlacedCells = new();
        private static readonly List<string> PlacedRoles = new();
        private static double startedAt;
        private static float samplingUntil;
        private static int frames;
        private static string outputPath = string.Empty;

        public static void Run()
        {
            started = false;
            placed = false;
            bound = false;
            frames = 0;
            PlacedCells.Clear();
            PlacedRoles.Clear();
            Tracks.Clear();
            outputPath = ReadArg("-ltwProbeOutput") ?? Path.Combine(Path.GetTempPath(), "tower-motion-amplitude.md");

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            MobileViewportLayout.SetCaptureViewportOverride(Width, Height, new Rect(0f, 0f, Width, Height));
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying = true;
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - startedAt > 180d) Finish("timed out");
                return;
            }

            var driver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            var commands = Object.FindAnyObjectByType<UnityCommandAdapter>();
            if (driver == null || commands == null)
            {
                if (EditorApplication.timeSinceStartup - startedAt > 180d) Finish("no driver");
                return;
            }

            if (!started)
            {
                driver.StartMatch();
                started = true;
                return;
            }

            frames++;
            if (frames < 6) return;

            if (!placed)
            {
                if (!PlaceOneOfEachTower(commands, driver)) return;
                placed = true;
                // The renderer builds tower GameObjects during ITS next update, not when the command
                // is accepted. Binding on the placing frame finds nothing at all — the first attempt
                // at this reported "no scene object" for every one of the fifteen.
                bindAtFrame = frames + 4;
                return;
            }

            if (!bound)
            {
                if (frames < bindAtFrame) return;
                if (!BindTracks(driver, PlacedCells, PlacedRoles))
                {
                    Finish("could not bind any tower to a scene object");
                    return;
                }

                bound = true;
                samplingUntil = Time.time + SampleSeconds;
                return;
            }

            Sample();

            if (Time.time >= samplingUntil)
            {
                Report();
                Finish(null);
            }
        }

        /// <summary>
        /// Places one tower of every role down the lane and binds each to its scene object.
        /// </summary>
        private static bool PlaceOneOfEachTower(UnityCommandAdapter commands, UnitySimulationDriver driver)
        {
            var field = typeof(UnityCommandAdapter).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(commands) is not LocalVerticalSlice sim)
            {
                return false;
            }

            sim.GrantLocalPlaytestGold(sim.LocalPlayerId, new Gold(100000));

            // Only cells where placement is ACCEPTED are used. The match has already built towers
            // of its own by this point, so a fixed layout collides with them — the first run bound
            // six roles to whatever happened to be standing on the cell it wanted and reported that
            // object's motion under the wrong tower's name. Walking until each role lands is the
            // difference between measuring the roster and measuring coincidence.
            var placedCells = new List<GridPosition>();
            var placedRoles = new List<string>();
            var x = 0;
            var y = 1;
            foreach (var entry in TowerCatalog.Entries)
            {
                var landed = false;
                while (!landed && y < 15)
                {
                    var cell = new GridPosition(x, y);
                    if (sim.PlaceTower(sim.LocalPlayerId, sim.LocalPlayerLaneId, new LTW.Simulation.Content.ContentId(entry.ContentId), cell).Accepted)
                    {
                        placedCells.Add(cell);
                        placedRoles.Add(entry.ShortLabel);
                        landed = true;
                    }

                    x++;
                    if (x > 6)
                    {
                        x = 0;
                        y++;
                    }
                }

                if (!landed)
                {
                    Debug.LogWarning($"MOTIONAMP ran out of free cells before placing {entry.ShortLabel}");
                }
            }

            driver.RefreshSnapshot();
            PlacedCells.AddRange(placedCells);
            PlacedRoles.AddRange(placedRoles);
            return true;
        }

        /// <summary>
        /// Binds each placed tower to its scene object through the renderer's own activeTowers map.
        /// </summary>
        /// <remarks>
        /// By entity id, NOT by proximity. Proximity looks right and is wrong: a contact-shadow
        /// decal, a range ring, a mechanic marker and a spore-fog quad all sit on the same cell as
        /// the tower, and picking the topmost object standing there selects one of those about as
        /// often as it selects the tower. That produced a table of exact 0.00 readings — the decals
        /// genuinely do not move — which reads as "this tower is dead" rather than "this probe bound
        /// to a decal". Two towers happened to bind correctly and moved, which is what gave it away.
        /// </remarks>
        private static bool BindTracks(UnitySimulationDriver driver, IReadOnlyList<GridPosition> cells, IReadOnlyList<string> roles)
        {
            var renderer = Object.FindAnyObjectByType<UnityVerticalSliceRenderer>();
            var snapshot = driver.LatestSnapshot;
            if (renderer == null || snapshot == null) return false;

            var field = typeof(UnityVerticalSliceRenderer).GetField("activeTowers", BindingFlags.Instance | BindingFlags.NonPublic);
            // Keyed by entity id as a long. It was a string when this probe was written, and the
            // change silently disarmed the whole thing: the cast failed, BindTracks bailed, and the
            // probe reported "could not reflect activeTowers" rather than any measurement.
            if (field?.GetValue(renderer) is not Dictionary<long, GameObject> activeTowers)
            {
                Debug.LogError("MOTIONAMP could not reflect activeTowers");
                return false;
            }

            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                var tower = snapshot.Towers.FirstOrDefault(candidate =>
                    candidate.LaneId.Equals(driver.LocalPlayerLaneId) && candidate.Position.Equals(cell));
                if (tower == null)
                {
                    Debug.LogWarning($"MOTIONAMP {roles[index]} is not in the snapshot at {cell.X},{cell.Y}");
                    continue;
                }

                if (activeTowers.TryGetValue(tower.EntityId.Value, out var instance) && instance != null)
                {
                    Tracks.Add(new Track { Role = roles[index], Root = instance, RootScale = instance.transform.localScale.x });
                }
                else
                {
                    Debug.LogWarning($"MOTIONAMP {roles[index]} has no active tower object");
                }
            }

            return Tracks.Count > 0;
        }

        private static void Sample()
        {
            var cam = Camera.main ?? Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)[0];
            foreach (var track in Tracks)
            {
                if (track.Root == null) continue;

                // Body only, not the whole tower object. RoleMarker, OwnerTrim and RangeHalo are
                // STATIC SIBLINGS of Body, so aggregate bounds mix moving geometry with fixed
                // geometry and the centre travels less than the tower actually does — by a different
                // fraction per tower, depending how large its halo is relative to its mesh. That
                // reported Spore Cloud at 23% of its authored drift while Elder Canopy measured
                // 100%, which is an artefact of halo size, not a difference in motion.
                var body = track.Root.transform.Find("Body") ?? track.Root.transform;
                var renderers = body.GetComponentsInChildren<Renderer>(false);
                if (renderers.Length == 0) continue;

                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);

                // Viewport, then pixels against the declared portrait surface rather than the editor
                // game view, so the numbers are what a phone would show.
                var centre = cam.WorldToViewportPoint(bounds.center);
                var top = cam.WorldToViewportPoint(bounds.center + Vector3.up * bounds.extents.y);
                var px = centre.x * Width;
                var py = centre.y * Height;
                var heightPx = Mathf.Abs(top.y - centre.y) * Height * 2f;

                track.MinX = Mathf.Min(track.MinX, px);
                track.MaxX = Mathf.Max(track.MaxX, px);
                track.MinY = Mathf.Min(track.MinY, py);
                track.MaxY = Mathf.Max(track.MaxY, py);
                track.MinHeight = Mathf.Min(track.MinHeight, heightPx);
                track.MaxHeight = Mathf.Max(track.MaxHeight, heightPx);

                // World-space travel too, as a check on the pixel numbers: lateral drift is authored
                // in world units, so measured peak-to-peak should land near 2x the authored driftAmp.
                // If it comes in far under, the sampling missed the extremes and the pixel figures
                // understate the motion rather than the motion being absent.
                track.MinWorldX = Mathf.Min(track.MinWorldX, bounds.center.x);
                track.MaxWorldX = Mathf.Max(track.MaxWorldX, bounds.center.x);
                track.Samples++;
            }
        }

        private static void Report()
        {
            var report = new StringBuilder();
            report.AppendLine("# Tower Idle Motion Amplitude At Board Scale");
            report.AppendLine();
            report.AppendLine($"Measured over {SampleSeconds}s at {Width}x{Height}, the declared portrait surface.");
            report.AppendLine("Sway is peak-to-peak lateral travel of the tower's rendered bounds centre.");
            report.AppendLine("Pulse is peak-to-peak change in its rendered height. Both in screen pixels.");
            report.AppendLine();
            report.AppendLine("| Tower | Sway px | Pulse px | World sway | Root scale | Samples | Reads |");
            report.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

            foreach (var track in Tracks)
            {
                var sway = track.MaxX - track.MinX;
                var pulse = track.MaxHeight - track.MinHeight;
                var strongest = Mathf.Max(sway, pulse);
                // A pixel of travel over four seconds is not motion anyone can see on a phone; three
                // is the point where a slow drift starts to register against a static board.
                var reads = strongest >= 3f ? "yes" : strongest >= 1.5f ? "marginal" : "NO";
                report.AppendLine($"| {track.Role} | {sway:F2} | {pulse:F2} | {track.MaxWorldX - track.MinWorldX:F4} | {track.RootScale:F3} | {track.Samples} | {reads} |");
                Debug.Log($"MOTIONAMP {track.Role} sway={sway:F2}px pulse={pulse:F2}px");
            }

            File.WriteAllText(outputPath, report.ToString());
            Debug.Log($"MOTIONAMP wrote {outputPath}");
        }

        private static void Finish(string? error)
        {
            EditorApplication.update -= Tick;
            if (error != null) Debug.LogError($"MOTIONAMP FAILED: {error}");
            MobileViewportLayout.ClearCaptureViewportOverride();
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }

        private static string? ReadArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == name) return args[index + 1];
            }

            return null;
        }
    }
}
