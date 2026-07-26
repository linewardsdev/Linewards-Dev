using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Converts project materials from the Built-in Standard shader to URP/Lit, for the migration
    /// tracked in docs/URP_MIGRATION.md.
    /// </summary>
    /// <remarks>
    /// Written explicitly rather than driven through the package's interactive converter window so
    /// it runs headless and so every property mapping is visible and reviewable. The mappings match
    /// what URP's own StandardUpgrader performs: albedo moves from _MainTex to _BaseMap, tint from
    /// _Color to _BaseColor, and gloss from _Glossiness to _Smoothness.
    ///
    /// Third-party materials are converted too. Leaving the weapon kit on a Built-in shader would
    /// render it magenta, and it is referenced by the creep and tower generator scripts.
    /// </remarks>
    public static class UrpMaterialConverter
    {
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        [MenuItem("Line Wards/Migration/Convert Materials To URP")]
        public static void ConvertMaterials()
        {
            var lit = Shader.Find(LitShaderName);
            var unlit = Shader.Find(UnlitShaderName);
            if (lit == null || unlit == null)
            {
                Debug.LogError($"URP shaders not found. Is the package installed? Lit={lit != null}, Unlit={unlit != null}");
                ExitIfBatch(1);
                return;
            }

            var converted = 0;
            var skipped = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null)
                {
                    continue;
                }

                var shaderName = material.shader.name;
                if (shaderName.StartsWith("Universal Render Pipeline/") || shaderName.StartsWith("LTW/"))
                {
                    continue;
                }

                if (shaderName == "Standard" || shaderName == "Standard (Specular setup)")
                {
                    ConvertStandardToLit(material, lit);
                    converted++;
                    continue;
                }

                if (shaderName == "Unlit/Color" || shaderName == "Unlit/Texture" || shaderName == "Unlit/Transparent")
                {
                    ConvertUnlit(material, unlit, shaderName);
                    converted++;
                    continue;
                }

                if (shaderName.StartsWith("Sprites/") || shaderName.StartsWith("UI/") || shaderName.StartsWith("GUI/"))
                {
                    // Sprite and UI shaders render correctly under URP; leaving them alone avoids
                    // disturbing the HUD and the endpoint gate plates.
                    continue;
                }

                skipped.Add($"{shaderName}  ({path})");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"URP material conversion: {converted} converted, {skipped.Count} left unconverted.");
            foreach (var entry in skipped)
            {
                Debug.LogWarning($"UNCONVERTED {entry}");
            }

            ExitIfBatch(0);
        }

        private static void ConvertStandardToLit(Material material, Shader lit)
        {
            var mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            var metallicMap = material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null;
            var bumpMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
            var occlusionMap = material.HasProperty("_OcclusionMap") ? material.GetTexture("_OcclusionMap") : null;
            var emissionMap = material.HasProperty("_EmissionMap") ? material.GetTexture("_EmissionMap") : null;
            var emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
            var metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            var glossiness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
            var glossMapScale = material.HasProperty("_GlossMapScale") ? material.GetFloat("_GlossMapScale") : 1f;
            var hadEmission = material.IsKeywordEnabled("_EMISSION");

            material.shader = lit;

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", mainTex);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

            // URP drives smoothness from the map's alpha scaled by _Smoothness when a
            // metallic-gloss map is bound, mirroring Built-in's _GlossMapScale.
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", metallicMap != null ? glossMapScale : glossiness);
            }

            if (metallicMap != null && material.HasProperty("_MetallicGlossMap"))
            {
                material.SetTexture("_MetallicGlossMap", metallicMap);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (bumpMap != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", bumpMap);
                material.EnableKeyword("_NORMALMAP");
            }

            if (occlusionMap != null && material.HasProperty("_OcclusionMap"))
            {
                material.SetTexture("_OcclusionMap", occlusionMap);
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            if (emissionMap != null || hadEmission)
            {
                if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", emissionMap);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emissionColor);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(material);
        }

        private static void ConvertUnlit(Material material, Shader unlit, string previousShaderName)
        {
            var mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            var wasTransparent = previousShaderName == "Unlit/Transparent";

            material.shader = unlit;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", mainTex);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);

            if (wasTransparent && material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            EditorUtility.SetDirty(material);
        }

        private static void ExitIfBatch(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
