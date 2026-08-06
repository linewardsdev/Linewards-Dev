using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Puts a <see cref="LODGroup"/> on every unit prefab, wired to the decimated meshes
    /// <c>tools/art/make_all_lods.py</c> produces.
    /// </summary>
    /// <remarks>
    /// Open item 15's last sub-item. Every unit is ~15,000 triangles at LOD0, and the capture
    /// harness has measured 266 creeps on camera — about four million triangles of units in one
    /// frame, on a mobile target. The decimation stage has existed for a while and the meshes
    /// were generated for all 30 roles; until this ran, none of them were referenced by anything,
    /// so they were inert bytes in the repo rather than a performance win.
    ///
    /// The LOD0 renderers are the prefab's existing ones, untouched — this adds levels below
    /// them rather than replacing what ships today, so the worst case if a threshold is wrong is
    /// a unit that swaps too early, not a unit that disappears.
    ///
    /// Thresholds are screen-relative HEIGHT, which is what makes them safe to state as
    /// constants: these units occupy a measured 46-105px on a 1206px-tall device screen, so
    /// LOD0 covers everything from full-screen down to roughly a tenth of the height, and the
    /// decimated levels take over below that. A tower inspected up close in a store screen still
    /// gets LOD0.
    ///
    /// Cross-fade is deliberately NOT enabled here. `m_EnableLODCrossFade` is a per-quality-tier
    /// project setting, resolved item 15 already fixed it once, and turning it on from a prefab
    /// script would silently disagree with the tier assets. If popping is visible in play, that
    /// is the knob — in QualitySettings, not here.
    /// </remarks>
    public static class AuthorLodGroups
    {
        private static readonly (string Prefabs, string Lods, string Prefix)[] Roster =
        {
            ("Assets/Prefabs/Towers", "Assets/Art/Towers/Production/LODs", "tower"),
            ("Assets/Prefabs/Creeps", "Assets/Art/Creeps/Production/LODs", "creep")
        };

        /// <summary>Screen-relative height below which each level takes over. See the remarks.</summary>
        private const float Lod1Threshold = 0.10f;
        private const float Lod2Threshold = 0.045f;
        private const float CullThreshold = 0.012f;

        [MenuItem("LTW/Art/Author LOD Groups")]
        public static void Author() => Run(apply: true);

        [MenuItem("LTW/Art/Author LOD Groups (Dry Run)")]
        public static void DryRun() => Run(apply: false);

        private static void Run(bool apply)
        {
            var report = new StringBuilder(apply ? "=== authoring LOD groups ===\n" : "=== LOD groups DRY RUN ===\n");
            var authored = 0;
            var missing = 0;

            foreach (var (prefabFolder, lodFolder, prefix) in Roster)
            {
                if (!AssetDatabase.IsValidFolder(prefabFolder))
                {
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = Path.GetFileNameWithoutExtension(path);

                    // Only the 3D prefabs. The older primitive-built ones share a naming stem and
                    // have no imported mesh to pair a decimated copy with.
                    if (!name.EndsWith("_3D"))
                    {
                        continue;
                    }

                    var role = Flatten(name.Replace("_3D", string.Empty)
                        .Replace("Tower_", string.Empty)
                        .Replace("Creep_", string.Empty)
                        .ToLowerInvariant());
                    var lod1 = FindLod(lodFolder, prefix, role, 1);
                    var lod2 = FindLod(lodFolder, prefix, role, 2);
                    if (lod1 == null || lod2 == null)
                    {
                        report.AppendLine($"  no LOD meshes for {name} (role '{role}')");
                        missing++;
                        continue;
                    }

                    report.AppendLine($"  {name}  <- {Path.GetFileNameWithoutExtension(lod1)}, "
                                      + Path.GetFileNameWithoutExtension(lod2));
                    if (apply && !Attach(path, lod1, lod2, report))
                    {
                        continue;
                    }

                    authored++;
                }
            }

            if (apply)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            report.AppendLine($"\n  {authored} prefab(s) {(apply ? "authored" : "would be authored")}, "
                              + $"{missing} without meshes");
            Debug.Log(report.ToString());
        }

        private static bool Attach(string prefabPath, string lod1Path, string lod2Path, StringBuilder report)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // Existing renderers are LOD0 and are never touched. Any levels a previous run
                // added are removed first so this is idempotent — re-running after a re-bake
                // must not stack a second copy of every decimated mesh under the prefab.
                foreach (var stale in root.transform.Cast<Transform>()
                             .Where(child => child.name.StartsWith("LOD_"))
                             .ToArray())
                {
                    Object.DestroyImmediate(stale.gameObject);
                }

                var lod0 = root.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.GetComponent<ParticleSystem>() == null)
                    .ToArray();
                if (lod0.Length == 0)
                {
                    report.AppendLine($"      skipped: no renderers on {Path.GetFileNameWithoutExtension(prefabPath)}");
                    return false;
                }

                var lod1 = InstantiateLevel(root, lod1Path, "LOD_1", lod0[0].sharedMaterial);
                var lod2 = InstantiateLevel(root, lod2Path, "LOD_2", lod0[0].sharedMaterial);
                if (lod1.Length == 0 || lod2.Length == 0)
                {
                    report.AppendLine($"      skipped: decimated mesh had no renderer");
                    return false;
                }

                // Explicit == null, NOT ??. GetComponent returns a fake-null UnityEngine.Object
                // when the component is absent: Unity overloads == to report that as null, but
                // ?? tests real CLR null and so takes the fake object, which then throws
                // MissingComponentException on the first call. That is exactly what the first
                // run did — 30 prefabs matched, 0 were written, and the only evidence was one
                // exception line in a log that otherwise reported no errors.
                var group = root.GetComponent<LODGroup>();
                if (group == null)
                {
                    group = root.AddComponent<LODGroup>();
                }
                group.SetLODs(new[]
                {
                    new LOD(Lod1Threshold, lod0),
                    new LOD(Lod2Threshold, lod1),
                    new LOD(CullThreshold, lod2)
                });
                group.RecalculateBounds();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Adds one decimated mesh under the prefab root, inheriting LOD0's material.</summary>
        private static Renderer[] InstantiateLevel(GameObject root, string meshPath, string name, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
            if (source == null)
            {
                return System.Array.Empty<Renderer>();
            }

            var level = Object.Instantiate(source, root.transform);
            level.name = name;
            level.transform.localPosition = Vector3.zero;
            level.transform.localRotation = Quaternion.identity;
            level.transform.localScale = Vector3.one;

            var renderers = level.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                // The decimated FBX arrives with whatever material Blender wrote. It shares LOD0's
                // UVs by construction, so it takes LOD0's material — otherwise a unit would change
                // colour at the swap distance, which reads as a bug rather than a LOD.
                renderer.sharedMaterial = material;
            }

            return renderers;
        }

        private static string FindLod(string folder, string prefix, string role, int level)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return null;
            }

            return AssetDatabase.FindAssets("t:Model", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path =>
                {
                    var stem = Flatten(Path.GetFileNameWithoutExtension(path).ToLowerInvariant());
                    return stem.Contains(Flatten($"{prefix}_{role}_3d")) && stem.EndsWith($"lod{level}");
                });
        }

        /// <summary>Separators stripped. Prefab `Tower_ElderCanopy_3D` must meet mesh
        /// `tower_eldercanopy_3d_LOD1` — the same mismatch that silently cost the AO bind four
        /// roles before it compared this way.</summary>
        private static string Flatten(string value) =>
            value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
    }
}
