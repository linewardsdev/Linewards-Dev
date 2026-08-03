using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.EditorTools
{
    /// <summary>
    /// Flips every unit material between the stylized shader and the stock URP/Lit it replaced,
    /// so before and after can be compared in one editor session.
    /// </summary>
    /// <remarks>
    /// Built because the obvious ways to see "before" are both bad. Checking out the old commit and
    /// reopening means a full asset reimport each way, and opening a second project at the old
    /// revision means waiting on a Library rebuild for a 1.58 GiB repo. Neither is something to do
    /// twice, let alone repeatedly, and comparing shading is exactly the task that needs repeated
    /// back-and-forth rather than one look.
    ///
    /// This only ever rewrites the shader reference and the occlusion strength, so it is reversible
    /// and idempotent in both directions. It is a comparison tool, not a migration: whichever state
    /// is left on disk is the one that gets committed, so finish on the side you actually want.
    ///
    /// One honest caveat about fidelity. Going to stock restores the pipeline the units shipped with
    /// and unbinds AO, which is the true before. It does NOT restore the exact per-material
    /// `_Smoothness` values from before the migration — those were 0.45 and 0.5 depending on the
    /// cluster, and are now 0.42 everywhere. The difference is small and in the direction the uplift
    /// doc argues for anyway, but it means "stock" here is the old SHADER, not a byte-exact old look.
    /// For that, use git.
    /// </remarks>
    public static class StylizedShaderABToggle
    {
        private const string StylizedShaderName = "LTW/Stylized Unit";
        private const string StockShaderName = "Universal Render Pipeline/Lit";

        // Scope comes from the migration rather than a second copy here. The first version of this
        // file kept its own folder list and no exclusions at all, so "show after" restyled the role
        // markers and energy accents the migration deliberately protects — the same defect that made
        // the arrow tower render as a solid cyan blob — and the switched counts drifted 62 -> 118 -> 111
        // across one round trip instead of staying put.

        [MenuItem("LTW/Art/Stylized Units/A-B Compare - Show BEFORE (stock URP Lit)")]
        public static void ShowBefore() => Switch(toStylized: false);

        [MenuItem("LTW/Art/Stylized Units/A-B Compare - Show AFTER (stylized)")]
        public static void ShowAfter() => Switch(toStylized: true);

        private static void Switch(bool toStylized)
        {
            var target = Shader.Find(toStylized ? StylizedShaderName : StockShaderName);
            var source = Shader.Find(toStylized ? StockShaderName : StylizedShaderName);
            if (target == null || source == null)
            {
                Debug.LogError("[A-B] Could not resolve both shaders.");
                return;
            }

            var folders = StylizedUnitMaterialMigration.MaterialFolders
                .Where(AssetDatabase.IsValidFolder)
                .ToArray();
            var switched = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", folders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                // Only materials currently on the other side of the pair, and only ones the
                // migration itself would touch. Board surfaces, range halos, role markers and
                // sender accents are left alone in both directions, which is what keeps the
                // switched count identical every time instead of growing on each flip.
                if (material == null
                    || material.shader != source
                    || StylizedUnitMaterialMigration.IsOutOfScope(path))
                {
                    continue;
                }

                var occlusion = material.HasProperty("_OcclusionMap") ? material.GetTexture("_OcclusionMap") : null;
                material.shader = target;

                // Seed the current authored values on the way in, so this is a tuning loop and not
                // just a shader swap: edit a default, recompile, flip to AFTER, look. Towers and
                // creeps take different sets, which is the whole reason re-seeding matters here.
                if (toStylized)
                {
                    StylizedUnitMaterialMigration.ReseedDefaults(material, path);
                }

                if (occlusion != null)
                {
                    material.SetTexture("_OcclusionMap", occlusion);
                    // AO is part of the after, not the before: item 3 established there was no
                    // occlusion bound to anything until this work baked it.
                    material.SetFloat("_OcclusionStrength", toStylized ? 1f : 0f);
                    if (toStylized) material.DisableKeyword("_OCCLUSIONMAP");
                    else material.DisableKeyword("_OCCLUSIONMAP");
                }

                EditorUtility.SetDirty(material);
                switched++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[A-B] Now showing {(toStylized ? "AFTER (stylized)" : "BEFORE (stock URP/Lit)")} " +
                      $"— {switched} materials switched.");
        }
    }
}
