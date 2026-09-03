using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Single source of truth for the surface response of the fifteen tower body materials.
    /// </summary>
    /// <remarks>
    /// The bodies had drifted into two clusters that lined up exactly with who authored them and
    /// when: the five original towers at _Smoothness 1.0 with per-role emission peaking at 2.0, and
    /// the ten added later at 0.12 with an identical near-black 0.02/0.035/0.05 on all ten.
    ///
    /// The cause is upstream and already fixed. Tower3DImportPipeline created the later ten through
    /// the TRANSPARENT recipe, which is where 0.12 and that emission constant come from. But
    /// CreateBodyMaterial deliberately returns an existing material untouched — body colour and
    /// emission are hand-tuned after creation, and regeneration must not flatten that — so fixing
    /// the generator could not and did not repair the ten materials it had already written. They
    /// need an explicit, deliberate re-tune, which is what this file is.
    ///
    /// It is a tool rather than fifteen hand-edits because the values want to be reviewable
    /// together. The defect was never one bad number; it was two internally-consistent sets of
    /// numbers that nobody could see side by side.
    ///
    /// Deliberately NOT run from the import pipeline. Applying it is a decision about how the game
    /// looks, and wiring it into generation would recreate exactly the situation this fixes: values
    /// changing underneath whoever tuned them last.
    /// </remarks>
    public static class TowerBodyMaterialTuning
    {
        private const string MaterialFolder = "Assets/Art/Towers/Production/Materials";

        /// <summary>Scalar multiplied onto the metallic-gloss map's smoothness channel.</summary>
        /// <remarks>
        /// Chosen from measurement, not taste. The smoothness channel of the fifteen baked
        /// metallic-gloss maps averages 0.579, so this scalar lands effective smoothness at 0.261 —
        /// a broad, soft specular lobe with no hotspot.
        ///
        /// Both previous values were wrong in opposite directions, which is why this is not simply
        /// "copy cluster A onto cluster B". 1.0 gives effective 0.579, a tight glossy highlight that
        /// reads as wet plastic on what is meant to be stone and cast metal. 0.12 gives 0.070, which
        /// is no specular response at all. The reference look carries its richness through broad
        /// value gradients and rim light rather than through hotspots, and that is what a wide, dim
        /// lobe produces.
        ///
        /// Worth stating plainly: this is a defensible starting point, not a tuned final value. It
        /// wants a human look against a capture before anything treats it as settled.
        /// </remarks>
        public const float BodySmoothness = 0.45f;

        /// <summary>
        /// Per-tower body emission, in HDR, keyed by the material's tower id.
        /// </summary>
        /// <remarks>
        /// The first five are the existing hand-tuned values, reproduced verbatim so applying this
        /// does not disturb them; they are listed rather than skipped so all fifteen can be compared
        /// in one place, which is the thing that was missing.
        ///
        /// The ten new ones peak near 2.0 for a concrete reason: bloom threshold is 1.05, so the old
        /// 0.05 peak could not bloom at any bloom intensity. A value under the threshold is not a
        /// dim glow, it is no glow.
        ///
        /// Hues are spread rather than assigned freely, so towers stay tellable apart by accent.
        /// Spore Cloud is pinned rather than chosen — it is its own fog shader's colour
        /// (0.45, 0.85, 0.35) scaled to the same peak, so the tower and the cloud it emits agree.
        /// The Grove line does crowd the greens; that is its identity and is accepted, with the
        /// gameplay-critical role read carried by the role marker rather than by body emission.
        /// </remarks>
        private static readonly Dictionary<string, Color> BodyEmission = new Dictionary<string, Color>
        {
            // The original five, unchanged.
            ["arrow"] = new Color(0.604f, 1.278f, 2.0f),
            ["control"] = new Color(1.216f, 0.848f, 2.0f),
            ["relay"] = new Color(2.0f, 1.568f, 0.58f),
            ["pulse"] = new Color(0.698f, 1.764f, 1.428f),
            ["prism"] = new Color(2.0f, 0.9f, 0.6f),

            // Foundry line: forge, muzzle, arc, armour, repair.
            ["foundry"] = new Color(2.0f, 0.30f, 0.24f),
            ["gatling"] = new Color(2.0f, 1.06f, 0.24f),
            ["tesla"] = new Color(0.96f, 1.86f, 2.0f),
            // Barricade is deliberately the dimmest and least saturated of the fifteen: it is a wall,
            // and a glowing wall reads as a power source. Still above threshold so it is not inert.
            ["barricade"] = new Color(1.30f, 1.44f, 1.62f),
            // Pulled toward mint rather than left on leaf green: at leaf green it landed 3.8 degrees
            // from Sapling at near-identical saturation, which is not a distinguishable accent.
            ["repair_drone"] = new Color(0.56f, 2.0f, 1.232f),

            // Grove line: canopy, sapling, bloom, thorn, spore.
            ["elder_canopy"] = new Color(1.42f, 2.0f, 0.52f),
            ["sapling"] = new Color(0.72f, 2.0f, 0.90f),
            ["bloomheart"] = new Color(2.0f, 0.62f, 1.44f),
            ["thorn_snare"] = new Color(2.0f, 0.34f, 0.86f),
            ["spore_cloud"] = new Color(1.06f, 2.0f, 0.82f),

            // Roster expansion. Indigo at hue 236: the widest unused arc was 214-259, between
            // Barricade and Control, and its middle is where this wants to sit anyway — kin to
            // Arrow, which it is kitbashed from and shares a category with, without being
            // mistaken for it at 25 degrees of separation. Barricade is only 22 degrees away but
            // sits at saturation 0.20 against this 0.70, so the two never read as the same accent.
            ["twin_crescent"] = new Color(0.60f, 0.693f, 2.0f),
        };

        /// <summary>
        /// Applies the tuned body values to every tower, or to one when the batch-mode run passes
        /// <c>-ltwTowerRole &lt;role&gt;</c>.
        /// </summary>
        /// <remarks>
        /// The filter exists so adding a unit does not mean rewriting fifteen shipped materials.
        /// Those fifteen currently sit at _Smoothness 0.42 against the 0.45 this class asks for,
        /// so an unscoped apply is not a no-op for them — it is an unreviewed change to how the
        /// whole roster reflects light, arriving inside a commit about one new tower.
        /// </remarks>
        [MenuItem("Line Wars/Art/Apply Tower Body Material Tuning")]
        public static void ApplyTuning()
        {
            var changed = 0;
            var missing = new List<string>();
            var onlyRole = ReadArgumentValue("-ltwTowerRole");

            foreach (var pair in BodyEmission)
            {
                if (onlyRole != null && !string.Equals(pair.Key, onlyRole, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = $"{MaterialFolder}/mat_tower_{pair.Key}_3d_body_runtime_v01.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    missing.Add(path);
                    continue;
                }

                SetFloat(material, "_Smoothness", BodySmoothness);
                SetFloat(material, "_Glossiness", BodySmoothness);

                // The split nobody recorded: the later ten also shipped with environment reflections
                // switched off entirely, so the scene's reflection probe reached only a third of the
                // roster no matter what smoothness said.
                SetFloat(material, "_GlossyReflections", 1f);
                SetFloat(material, "_EnvironmentReflections", 1f);
                SetFloat(material, "_SpecularHighlights", 1f);

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", pair.Value);
                    material.EnableKeyword("_EMISSION");
                    // Emission here is a look, not a light source. Leaving it in the GI path would
                    // let a 2.0 accent bleed onto the board it is standing on.
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }

                EditorUtility.SetDirty(material);
                changed++;
            }

            AssetDatabase.SaveAssets();

            foreach (var path in missing)
            {
                Debug.LogError($"TOWER TUNING: no material at {path}.");
            }

            Debug.Log($"TOWER TUNING: applied smoothness {BodySmoothness} and per-role emission to {changed} of {BodyEmission.Count} tower bodies.");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(missing.Count == 0 ? 0 : 1);
            }
        }

        /// <summary>
        /// Fails if any tower body has drifted off the table above.
        /// </summary>
        /// <remarks>
        /// Runs headless and exits non-zero:
        ///   Unity -batchmode -quit -executeMethod LTW.UnityClient.Editor.TowerBodyMaterialTuning.ValidateTuning
        /// </remarks>
        [MenuItem("Line Wars/Art/Validate Tower Body Material Tuning")]
        public static void ValidateTuning()
        {
            var failures = new List<string>();

            foreach (var pair in BodyEmission)
            {
                var path = $"{MaterialFolder}/mat_tower_{pair.Key}_3d_body_runtime_v01.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    failures.Add($"{pair.Key}: no material at {path}.");
                    continue;
                }

                var smoothness = material.GetFloat("_Smoothness");
                if (!Mathf.Approximately(smoothness, BodySmoothness))
                {
                    failures.Add($"{pair.Key}: _Smoothness is {smoothness}, expected {BodySmoothness}.");
                }

                var emission = material.GetColor("_EmissionColor");
                if (!Approximately(emission, pair.Value))
                {
                    failures.Add($"{pair.Key}: _EmissionColor is {emission}, expected {pair.Value}.");
                }

                // Below the bloom threshold, emission is not dim — it is absent. This is the check
                // that would have caught the original defect on its own.
                var peak = Mathf.Max(emission.r, Mathf.Max(emission.g, emission.b));
                if (peak <= BloomThreshold)
                {
                    failures.Add($"{pair.Key}: emission peaks at {peak:F3}, at or below the {BloomThreshold} bloom threshold, so it cannot bloom.");
                }

                if (material.HasProperty("_GlossyReflections") && material.GetFloat("_GlossyReflections") < 0.5f)
                {
                    failures.Add($"{pair.Key}: _GlossyReflections is off, so the scene reflection probe does not reach it.");
                }
            }

            if (failures.Count == 0)
            {
                Debug.Log($"TOWER TUNING OK: {BodyEmission.Count} tower bodies at smoothness {BodySmoothness}, all emissions above the {BloomThreshold} bloom threshold, all receiving reflections.");
            }
            else
            {
                foreach (var failure in failures)
                {
                    Debug.LogError($"TOWER TUNING FAIL: {failure}");
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }

        /// <summary>Mirrors UrpPostProcessingSetup's authored bloom threshold.</summary>
        private const float BloomThreshold = 1.05f;

        private static string ReadArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.001f && Mathf.Abs(a.g - b.g) < 0.001f && Mathf.Abs(a.b - b.b) < 0.001f;
    }
}
