using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Binds the AO maps produced by <c>tools/art/bake_ao.py</c> to the unit body materials.
    /// </summary>
    /// <remarks>
    /// Open item 3: no asset in this project has ambient occlusion. Resolved item 6 established
    /// that the existing packed ORM maps cannot supply it — their red channel is exactly 0.000
    /// everywhere — so it has to be baked, and `bake_ao.py` bakes it. This is the other half:
    /// getting those files onto the materials and turning the shader's AO path on.
    ///
    /// Matching is by role token rather than an explicit table, so baking a new role and re-running
    /// this picks it up with no code change. A body material for role `arrow` takes
    /// `tower_arrow_3d_ao_v01`; `turretwalker` takes `creep_turretwalker_3d_ao_v01`.
    ///
    /// Only BODY materials are considered. Trim, energy and accent materials share the unit's UV
    /// layout but are small, mostly-unoccluded details, and the migration already excludes the
    /// overlay ones for a different reason.
    /// </remarks>
    public static class AssignBakedOcclusion
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/Art/Towers",
            "Assets/Art/Creeps"
        };

        /// <summary>Name with separators removed, so `elder_canopy` and `eldercanopy` compare equal.</summary>
        private static string Flatten(string value) =>
            value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);

        [MenuItem("LTW/Art/Stylized Units/3. Bind Baked Occlusion Maps")]
        public static void Bind()
        {
            var aoTextures = AssetDatabase
                .FindAssets("t:Texture2D", SearchFolders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).Contains("_ao_"))
                .ToDictionary(
                    path => Path.GetFileNameWithoutExtension(path),
                    path => AssetDatabase.LoadAssetAtPath<Texture2D>(path));

            if (aoTextures.Count == 0)
            {
                Debug.LogWarning("[BakedAO] No '*_ao_*' textures found. Run tools/art/bake_ao.py first.");
                return;
            }

            var report = new StringBuilder("=== binding baked AO ===\n");
            foreach (var pair in aoTextures)
            {
                report.AppendLine($"  found {pair.Key}");
            }

            var bound = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", SearchFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!name.Contains("body"))
                {
                    continue;
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty("_OcclusionMap"))
                {
                    continue;
                }

                // The role token is the part both names share: mat_tower_arrow_3d_body_runtime_v01
                // and tower_arrow_3d_ao_v01 meet at "tower_arrow_3d".
                //
                // Compared with separators stripped, because the two sides disagree about them for
                // four roles: the bake writes `tower_eldercanopy_3d_ao_v01` while the material is
                // `mat_tower_elder_canopy_3d_body_runtime_v01`, and likewise for repair_drone,
                // thorn_snare and spore_cloud. Matched literally, those four silently bind nothing —
                // which is exactly what happened on the first run: 26 of 30, with no error, because
                // "no AO for this role" and "AO whose name is punctuated differently" are
                // indistinguishable to a Contains() check.
                var flatName = Flatten(name);
                var match = aoTextures.FirstOrDefault(candidate =>
                {
                    var stem = Flatten(candidate.Key.ToLowerInvariant().Replace("_ao_v01", string.Empty));
                    return flatName.Contains(stem);
                });

                if (match.Value == null)
                {
                    continue;
                }

                material.SetTexture("_OcclusionMap", match.Value);
                material.SetFloat("_OcclusionStrength", 1f);
                EditorUtility.SetDirty(material);
                report.AppendLine($"  bound {match.Key} -> {Path.GetFileNameWithoutExtension(path)}");
                bound++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.AppendLine($"  bound {bound} material(s)");
            Debug.Log(report.ToString());
        }
    }
}
