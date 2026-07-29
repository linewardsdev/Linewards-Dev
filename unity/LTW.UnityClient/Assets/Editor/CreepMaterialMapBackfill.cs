#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Forces metallic-smoothness maps to import as linear data, and binds them into creep body
    /// materials that were created before their map existed.
    /// </summary>
    /// <remarks>
    /// Two separate problems, both invisible until you look at the metal response:
    ///
    /// 1. A packed metallic-smoothness map is data, not colour — metallic in R, smoothness in A.
    ///    Imported as sRGB, both channels get a transfer curve applied and the material reads
    ///    wrong. Unity defaults new PNGs to sRGB, so every such map needs sRGBTexture off. The
    ///    postprocessor below enforces that on import so it cannot be forgotten again; the
    ///    Category 1 maps have it, and the Category 3 maps were imported without it.
    ///
    /// 2. <see cref="Creep3DImportPipeline"/>'s CreateBodyMaterial deliberately returns an
    ///    existing material untouched, so hand-tuned values survive regeneration. That also means
    ///    a map added *after* the material was made never gets bound. Every Category 2 creep was
    ///    in this state: they shipped with no _MetallicGlossMap at all, because Meshy gave them
    ///    separate metallic and roughness files that the repack tool did not understand until it
    ///    was taught the second input shape.
    ///
    /// The menu item is a one-time backfill. New creeps get the map bound at creation and the
    /// colour space set by the postprocessor, so neither path should be needed again.
    /// </remarks>
    public static class CreepMaterialMapBackfill
    {
        private const string MapName = "Baked_MetallicSmoothness.png";
        private const string CreepModelRoot = "Assets/Art/AIStaging/Models/Creeps";
        private const string MaterialFolder = "Assets/Art/Creeps/Production/Materials";

        [MenuItem("Line Wards/Art/Backfill Creep Metallic Maps")]
        public static void BackfillCreepMetallicMaps()
        {
            var maps = AssetDatabase.FindAssets("t:Texture2D", new[] { CreepModelRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path) == MapName)
                .OrderBy(path => path)
                .ToArray();

            var recoloured = 0;
            foreach (var path in maps)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer || !importer.sRGBTexture)
                {
                    continue;
                }

                importer.sRGBTexture = false;
                importer.SaveAndReimport();
                recoloured++;
                Debug.Log($"Metallic map set to linear: {path}");
            }

            var bound = 0;
            foreach (var path in maps)
            {
                // "<root>/<Role>/AIDrop/<stem>_Textures/Baked_MetallicSmoothness.png" -> Role
                var role = path[(CreepModelRoot.Length + 1)..];
                role = role[..role.IndexOf('/')];

                var materialPath = $"{MaterialFolder}/mat_creep_{role.ToLowerInvariant()}_3d_body_v01.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    Debug.LogWarning($"No body material at {materialPath} for map {path}.");
                    continue;
                }

                if (material.GetTexture("_MetallicGlossMap") != null)
                {
                    continue;
                }

                var map = AssetDatabase.LoadAssetAtPath<Texture>(path);
                material.SetTexture("_MetallicGlossMap", map);
                material.SetFloat("_GlossMapScale", 1f);
                material.EnableKeyword(RenderCompat.UsingScriptablePipeline ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
                EditorUtility.SetDirty(material);
                bound++;
                Debug.Log($"Bound metallic map into {Path.GetFileName(materialPath)}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"BACKFILL: {maps.Length} map(s) found, {recoloured} set to linear, {bound} bound into materials.");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// Keeps future metallic-smoothness maps out of the sRGB transfer path automatically.
        /// </summary>
        private sealed class LinearMetallicMapPostprocessor : AssetPostprocessor
        {
            private void OnPreprocessTexture()
            {
                if (Path.GetFileName(assetPath) != MapName)
                {
                    return;
                }

                var importer = (TextureImporter)assetImporter;
                importer.sRGBTexture = false;
            }
        }
    }
}
