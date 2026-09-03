using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Single source of truth for the surface response of the fifteen creep body materials.
    /// </summary>
    /// <remarks>
    /// Creep bodies had split three ways along the same authorship seam as the towers: the original
    /// five at _Smoothness 1.0 with reflections on, five later ones at 0.5 with reflections off, and
    /// five more at 0.5 with reflections off, no emission map and black emission.
    ///
    /// The rule here differs from the tower one in a way that matters. Tower emission colour IS the
    /// colour — each tower carries a per-role hue. Creep emission colour is a MULTIPLIER on a baked
    /// emission map that already carries the hue, so the only thing to set is how far above the bloom
    /// threshold the map is allowed to reach. Giving a creep a coloured emission would fight its own
    /// map rather than tune it.
    /// </remarks>
    public static class CreepBodyMaterialTuning
    {
        private const string MaterialFolder = "Assets/Art/Creeps/Production/Materials";

        /// <summary>Mirrors UrpPostProcessingSetup's authored bloom threshold.</summary>
        private const float BloomThreshold = 1.05f;

        /// <summary>Matches TowerBodyMaterialTuning.BodySmoothness, deliberately.</summary>
        /// <remarks>
        /// Creeps and towers share a frame and a light rig, so a different specular width on each
        /// would read as two art styles rather than two unit types. The measured effect is the same
        /// as on towers: the creep metallic-gloss maps average 0.652 in their smoothness channel, so
        /// this lands effective smoothness at 0.294 — a broad lobe, no hotspot.
        /// </remarks>
        public const float BodySmoothness = 0.45f;

        /// <summary>
        /// Emission multiplier for creeps whose emission map carries something to multiply.
        /// </summary>
        /// <remarks>
        /// 1.0 was the old value, and 1.0 is exactly the problem: the maps are LDR, so the product
        /// could never exceed 1.0 and the bloom threshold is 1.05. Every creep in the game was
        /// therefore incapable of blooming, not dimly blooming.
        ///
        /// 1.8 rather than the towers' 2.0 because creep emission is applied through the map's own
        /// peak rather than authored per unit, so the brightest creep lands at 1.80 and the rest fall
        /// below it naturally. Measured risk is low: the emissive area is at most 1.74 percent of any
        /// creep's texture above half brightness, which is precisely the small-bright-detail case
        /// bloom exists to serve.
        /// </remarks>
        public const float EmissionMultiplier = 1.8f;

        private static readonly string[] Creeps =
        {
            "brute", "runner", "shade", "siege", "swarm",
            "burrower", "colossus", "obsidianbrute", "revenant", "serpent",
            "stalker", "turretwalker", "warden", "wisp", "zephyr",
        };

        [MenuItem("Line Wars/Art/Apply Creep Body Material Tuning")]
        public static void ApplyTuning()
        {
            var tuned = 0;
            var withoutMap = new List<string>();
            var missing = new List<string>();

            foreach (var creep in Creeps)
            {
                var material = LoadMaterial(creep);
                if (material == null)
                {
                    missing.Add(creep);
                    continue;
                }

                SetFloat(material, "_Smoothness", BodySmoothness);
                SetFloat(material, "_Glossiness", BodySmoothness);
                SetFloat(material, "_GlossyReflections", 1f);
                SetFloat(material, "_EnvironmentReflections", 1f);
                SetFloat(material, "_SpecularHighlights", 1f);

                if (HasEmissionMap(material))
                {
                    material.SetColor("_EmissionColor", new Color(EmissionMultiplier, EmissionMultiplier, EmissionMultiplier));
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else
                {
                    // Emission with no map multiplies against 1, so it would light the ENTIRE body
                    // uniformly rather than picking out detail. For a creature that is a lantern, not
                    // a highlight. These stay dark until someone authors them a map.
                    material.SetColor("_EmissionColor", Color.black);
                    material.DisableKeyword("_EMISSION");
                    withoutMap.Add(creep);
                }

                EditorUtility.SetDirty(material);
                tuned++;
            }

            AssetDatabase.SaveAssets();

            foreach (var creep in missing)
            {
                Debug.LogError($"CREEP TUNING: no body material for {creep}.");
            }

            Debug.Log(
                $"CREEP TUNING: applied smoothness {BodySmoothness} and reflections to {tuned} of {Creeps.Length} creep bodies; " +
                $"emission multiplier {EmissionMultiplier} on {tuned - withoutMap.Count} with maps. " +
                $"NOT emissive, no map authored: {string.Join(", ", withoutMap)}.");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(missing.Count == 0 ? 0 : 1);
            }
        }

        /// <summary>
        /// Fails if any creep body has drifted, and separately reports maps too dim to bloom.
        /// </summary>
        /// <remarks>
        /// Runs headless and exits non-zero:
        ///   Unity -batchmode -quit -executeMethod LTW.UnityClient.Editor.CreepBodyMaterialTuning.ValidateTuning
        /// </remarks>
        [MenuItem("Line Wars/Art/Validate Creep Body Material Tuning")]
        public static void ValidateTuning()
        {
            var failures = new List<string>();
            var unbloomable = new List<string>();
            var unmapped = new List<string>();

            foreach (var creep in Creeps)
            {
                var material = LoadMaterial(creep);
                if (material == null)
                {
                    failures.Add($"{creep}: no body material.");
                    continue;
                }

                var smoothness = material.GetFloat("_Smoothness");
                if (!Mathf.Approximately(smoothness, BodySmoothness))
                {
                    failures.Add($"{creep}: _Smoothness is {smoothness}, expected {BodySmoothness}.");
                }

                if (material.HasProperty("_GlossyReflections") && material.GetFloat("_GlossyReflections") < 0.5f)
                {
                    failures.Add($"{creep}: _GlossyReflections is off, so the scene reflection probe does not reach it.");
                }

                var emission = material.GetColor("_EmissionColor");
                var peak = Mathf.Max(emission.r, Mathf.Max(emission.g, emission.b));

                if (HasEmissionMap(material))
                {
                    if (peak <= BloomThreshold)
                    {
                        failures.Add($"{creep}: emission multiplier {peak:F3} is at or below the {BloomThreshold} bloom threshold, so its emissive detail cannot bloom.");
                    }
                }
                else
                {
                    unmapped.Add(creep);
                    if (peak > 0f)
                    {
                        failures.Add($"{creep}: has emission colour {peak:F3} but no emission map, which would light the whole body uniformly.");
                    }
                }
            }

            if (unmapped.Count > 0)
            {
                Debug.LogWarning($"CREEP TUNING: {unmapped.Count} creep(s) have no emission map authored and render with no emissive detail: {string.Join(", ", unmapped)}.");
            }

            if (failures.Count == 0)
            {
                Debug.Log($"CREEP TUNING OK: {Creeps.Length} creep bodies at smoothness {BodySmoothness}, all receiving reflections, every bound emission map above the {BloomThreshold} bloom threshold.");
            }
            else
            {
                foreach (var failure in failures)
                {
                    Debug.LogError($"CREEP TUNING FAIL: {failure}");
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }

        private static Material LoadMaterial(string creep)
        {
            // Two naming shapes exist in this folder; both are load-tested rather than assumed.
            var candidates = new[]
            {
                $"{MaterialFolder}/mat_creep_{creep}_3d_body_runtime_v01.mat",
                $"{MaterialFolder}/mat_creep_{creep}_body_runtime_v01.mat",
                $"{MaterialFolder}/mat_creep_{creep}_3d_body_v01.mat",
                $"{MaterialFolder}/mat_creep_{creep}_body_v01.mat",
            };

            for (var index = 0; index < candidates.Length; index++)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(candidates[index]);
                if (material != null)
                {
                    return material;
                }
            }

            return null;
        }

        private static bool HasEmissionMap(Material material) =>
            material.HasProperty("_EmissionMap") && material.GetTexture("_EmissionMap") != null;

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }
    }
}
