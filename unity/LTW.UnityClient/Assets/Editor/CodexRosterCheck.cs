using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Asserts every unit in the simulation's content catalog has exactly one client catalog entry,
    /// a visual profile with a real prefab, an icon that loads, and a line of codex copy.
    /// </summary>
    /// <remarks>
    /// The codex is the first screen that walks the WHOLE roster, so it is the first thing that can
    /// be wrong about a unit nobody happens to build. Every failure this checks for is silent at
    /// runtime:
    ///
    ///   - A creep with no catalog entry falls back to Runner, because both catalogs promise never
    ///     to return null. The codex would then show fifteen creeps, one of them twice, and nothing
    ///     would be logged.
    ///   - A visual profile with a null prefab instantiates nothing. The stage renders an empty
    ///     frame that looks exactly like a camera pointed at the wrong place.
    ///   - A missing icon PNG resolves to nothing, and Unity substitutes a placeholder rather than
    ///     erroring — the failure mode ShellBrandMarkCheck exists for, and the reason the send dock
    ///     could name icons before their art was rendered.
    ///
    /// No Play Mode needed; it reads assets and static tables only.
    ///
    ///   Unity -batchmode -quit -projectPath &lt;project&gt; \
    ///     -executeMethod LTW.UnityClient.Editor.CodexRosterCheck.Run
    /// </remarks>
    public static class CodexRosterCheck
    {
        private const string IconRoot = "Art/UI/Icons/";

        [MenuItem("Line Wards/Checks/Codex Roster")]
        public static void Run()
        {
            var failures = new List<string>();
            var content = SampleVerticalSliceContent.Create();

            var towerLibrary = Resources.Load<TowerVisualLibrary>("TowerVisualLibrary");
            var creepLibrary = Resources.Load<CreepVisualLibrary>("CreepVisualLibrary");

            if (towerLibrary == null)
            {
                failures.Add("Resources/TowerVisualLibrary is missing.");
            }

            if (creepLibrary == null)
            {
                failures.Add("Resources/CreepVisualLibrary is missing.");
            }

            // ---------------------------------------------------------------- towers

            CheckPairing(
                failures,
                "tower",
                content.Towers.Select(tower => tower.Id.Value),
                TowerCatalog.Entries.Select(entry => entry.ContentId));

            foreach (var entry in TowerCatalog.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Blurb))
                {
                    failures.Add($"tower '{entry.ContentId}' has no codex blurb.");
                }

                if (towerLibrary != null)
                {
                    var profile = towerLibrary.FindProfile(entry.ContentId);
                    if (profile == null)
                    {
                        failures.Add($"tower '{entry.ContentId}' has no visual profile.");
                    }
                    else if (profile.Prefab == null)
                    {
                        failures.Add($"tower '{entry.ContentId}' has a visual profile with a null prefab.");
                    }
                }

                CheckIcon(failures, "tower", entry.ContentId, entry.IconResource);
            }

            // ---------------------------------------------------------------- creeps

            CheckPairing(
                failures,
                "creep",
                content.Creeps.Select(creep => creep.Id.Value),
                CreepCatalog.Entries.Select(entry => entry.ContentId));

            var roles = new HashSet<int>();
            foreach (var entry in CreepCatalog.Entries)
            {
                // The role index is the send dock's selection identity. Two entries sharing one
                // would make a tap highlight the wrong card, which is subtle enough on a five-card
                // grid to go unnoticed for a long time.
                if (!roles.Add(entry.Role))
                {
                    failures.Add($"creep '{entry.ContentId}' repeats role index {entry.Role}.");
                }

                if (string.IsNullOrWhiteSpace(entry.Blurb))
                {
                    failures.Add($"creep '{entry.ContentId}' has no codex blurb.");
                }

                if (creepLibrary != null)
                {
                    var profile = creepLibrary.FindProfile(entry.ContentId);
                    if (profile == null)
                    {
                        failures.Add($"creep '{entry.ContentId}' has no visual profile.");
                    }
                    else if (profile.Prefab == null)
                    {
                        failures.Add($"creep '{entry.ContentId}' has a visual profile with a null prefab.");
                    }
                }

                CheckIcon(failures, "creep", entry.ContentId, entry.IconResource);
            }

            // The dock and the codex both index CategoryLabels by the catalog's category number.
            if (CreepCatalog.CategoryLabels.Length != 3 || TowerCatalog.CategoryLabels.Length != 3)
            {
                failures.Add("a category label array is no longer three long; the codex eyebrow indexes it directly.");
            }

            if (failures.Count > 0)
            {
                Debug.LogError($"CODEX ROSTER CHECK FAILED ({failures.Count}):\n  {string.Join("\n  ", failures)}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log(
                $"CODEX ROSTER CHECK PASSED: {TowerCatalog.Entries.Length} towers and " +
                $"{CreepCatalog.Entries.Length} creeps all resolve a definition, a prefab, an icon and a blurb.");
        }

        /// <summary>
        /// Asserts two id lists describe the same set, and says which way any mismatch runs.
        /// </summary>
        /// <remarks>
        /// Both directions matter and they fail differently. A simulation id with no catalog entry
        /// is a unit the codex cannot show; a catalog entry with no simulation id is a codex page
        /// whose stat sheet has nothing to read, which surfaces as a null definition rather than a
        /// missing screen.
        /// </remarks>
        private static void CheckPairing(List<string> failures, string kind, IEnumerable<string> simulationIds, IEnumerable<string> catalogIds)
        {
            var simulation = new HashSet<string>(simulationIds);
            var catalog = catalogIds.ToArray();

            foreach (var id in simulation)
            {
                if (!catalog.Contains(id))
                {
                    failures.Add($"{kind} '{id}' is in the content catalog but has no client catalog entry.");
                }
            }

            foreach (var id in catalog)
            {
                if (!simulation.Contains(id))
                {
                    failures.Add($"{kind} '{id}' has a client catalog entry but no content definition.");
                }
            }

            var duplicates = catalog.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key);
            foreach (var id in duplicates)
            {
                failures.Add($"{kind} '{id}' appears more than once in the client catalog.");
            }
        }

        private static void CheckIcon(List<string> failures, string kind, string contentId, string iconResource)
        {
            if (Resources.Load<Texture2D>(IconRoot + iconResource) == null)
            {
                failures.Add($"{kind} '{contentId}' has no icon at Resources/{IconRoot}{iconResource}.");
            }
        }
    }
}
