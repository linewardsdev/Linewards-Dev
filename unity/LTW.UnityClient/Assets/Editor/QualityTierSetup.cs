using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Gives the quality tiers a URP asset each, so choosing a tier actually changes rendering.
    /// </summary>
    /// <remarks>
    /// All six tiers carried `customRenderPipeline: {fileID: 0}`, which under a scriptable pipeline
    /// means every tier falls back to the single project-wide URP asset. A budget phone rendered
    /// exactly what a flagship did.
    ///
    /// Worse than merely decorative, which is why this was not left as a documentation note: the
    /// tiers' own fields ARE varied — Very Low has shadows off and a 15m shadow distance, High has
    /// them on at 40m, the pixel light counts differ. Someone tuned them. But under URP every one of
    /// those fields is dead: shadows, shadowResolution, shadowDistance, antiAliasing and
    /// pixelLightCount are all owned by the URP asset and ignored on QualitySettings. So the
    /// inspector shows a carefully graded set of tiers that do nothing, which is harder to catch
    /// than six tiers that obviously all say the same thing.
    ///
    /// Three assets rather than six. The tiers map many-to-one because the meaningful axis is what
    /// the device can afford, and there are three of those, not six.
    /// </remarks>
    public static class QualityTierSetup
    {
        private const string BaseAssetPath = "Assets/Settings/LTW_UniversalRenderPipeline.asset";
        private const string TierFolder = "Assets/Settings";

        /// <summary>What each tier gives up, and why that particular thing.</summary>
        private readonly struct TierSpec
        {
            public TierSpec(string name, int msaa, bool softShadows, int shadowmap, float shadowDistance, float renderScale, SoftShadowQuality softQuality)
            {
                Name = name;
                Msaa = msaa;
                SoftShadows = softShadows;
                Shadowmap = shadowmap;
                ShadowDistance = shadowDistance;
                RenderScale = renderScale;
                SoftQuality = softQuality;
            }

            public string Name { get; }
            public int Msaa { get; }
            public bool SoftShadows { get; }
            public int Shadowmap { get; }
            public float ShadowDistance { get; }
            public float RenderScale { get; }
            public SoftShadowQuality SoftQuality { get; }
        }

        private static readonly TierSpec[] Tiers =
        {
            // Budget. Gives up antialiasing, shadow softness and a sixth of the resolution — the
            // three most expensive per-pixel costs — but KEEPS HDR and the post stack, because
            // bloom is what the tower emissives are authored for and a build without it does not
            // look like a cheaper version of this game, it looks like a different one.
            new TierSpec("Low", msaa: 1, softShadows: false, shadowmap: 512, shadowDistance: 35f, renderScale: 0.85f, SoftShadowQuality.Low),

            // The tuned baseline: identical to the asset every tier used to share.
            new TierSpec("Medium", msaa: 2, softShadows: true, shadowmap: 1024, shadowDistance: 50f, renderScale: 1f, SoftShadowQuality.Medium),

            // Flagship. Spends on edges and shadow detail, the two things that read at phone DPI.
            new TierSpec("High", msaa: 4, softShadows: true, shadowmap: 2048, shadowDistance: 60f, renderScale: 1f, SoftShadowQuality.High),
        };

        /// <summary>
        /// Unity's six tiers mapped onto the three that differ. Index matches QualitySettings order:
        /// Very Low, Low, Medium, High, Very High, Ultra.
        /// </summary>
        private static readonly int[] TierForQualityLevel = { 0, 0, 1, 1, 2, 2 };

        [MenuItem("Line Wards/Migration/Create Quality Tier Pipeline Assets")]
        public static void CreateTierAssets()
        {
            var exitCode = 0;
            try
            {
                var baseAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(BaseAssetPath);
                if (baseAsset == null)
                {
                    Debug.LogError($"QUALITY TIERS: no base pipeline asset at {BaseAssetPath}.");
                    exitCode = 1;
                }
                else
                {
                    var created = new List<UniversalRenderPipelineAsset>();
                    foreach (var tier in Tiers)
                    {
                        created.Add(CreateOrUpdate(baseAsset, tier));
                    }

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    AssignToQualityLevels(created);

                    Debug.Log($"QUALITY TIERS: {created.Count} pipeline asset(s) ready and assigned across {TierForQualityLevel.Length} quality level(s).");
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static UniversalRenderPipelineAsset CreateOrUpdate(UniversalRenderPipelineAsset baseAsset, TierSpec tier)
        {
            var path = $"{TierFolder}/LTW_URP_{tier.Name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                // Instantiated from the base rather than created blank, so every field this spec does
                // NOT name — renderer list, HDR, colour grading mode, batching, light settings —
                // stays identical to the tuned baseline. A blank asset would silently reintroduce
                // Unity defaults for all of them, which is how the post-processing profile and the
                // soft-shadow flag went wrong in the first place.
                asset = Object.Instantiate(baseAsset);
                asset.name = $"LTW_URP_{tier.Name}";
                AssetDatabase.CreateAsset(asset, path);
            }

            // Through SerializedObject rather than the public properties: supportsSoftShadows is
            // read-only on UniversalRenderPipelineAsset and softShadowQuality is not exposed at all,
            // so half these fields are unreachable from the scripting API. Doing all six the same
            // way keeps one mechanism instead of two, and matches how the tier assignment below has
            // to work anyway.
            var serialized = new SerializedObject(asset);
            SetInt(serialized, "m_MSAA", tier.Msaa);
            SetBool(serialized, "m_SoftShadowsSupported", tier.SoftShadows);
            SetInt(serialized, "m_MainLightShadowmapResolution", tier.Shadowmap);
            SetFloat(serialized, "m_ShadowDistance", tier.ShadowDistance);
            SetFloat(serialized, "m_RenderScale", tier.RenderScale);
            SetInt(serialized, "m_SoftShadowQuality", (int)tier.SoftQuality);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void SetInt(SerializedObject serialized, string path, int value)
        {
            var property = serialized.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"QUALITY TIERS: {serialized.targetObject.name} has no field {path}; left unchanged.");
                return;
            }

            property.intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string path, float value)
        {
            var property = serialized.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"QUALITY TIERS: {serialized.targetObject.name} has no field {path}; left unchanged.");
                return;
            }

            property.floatValue = value;
        }

        private static void SetBool(SerializedObject serialized, string path, bool value)
        {
            var property = serialized.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"QUALITY TIERS: {serialized.targetObject.name} has no field {path}; left unchanged.");
                return;
            }

            property.boolValue = value;
        }

        /// <summary>
        /// Writes each tier's pipeline asset into QualitySettings.
        /// </summary>
        /// <remarks>
        /// Through SerializedObject because QualitySettings exposes no scripting API for
        /// customRenderPipeline — it is settable in the inspector and nowhere else.
        /// </remarks>
        private static void AssignToQualityLevels(IReadOnlyList<UniversalRenderPipelineAsset> assets)
        {
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("QualitySettings"));
            var levels = settings.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray)
            {
                Debug.LogError("QUALITY TIERS: could not read m_QualitySettings.");
                return;
            }

            var assigned = 0;
            for (var index = 0; index < levels.arraySize && index < TierForQualityLevel.Length; index++)
            {
                var property = levels.GetArrayElementAtIndex(index).FindPropertyRelative("customRenderPipeline");
                if (property == null)
                {
                    continue;
                }

                property.objectReferenceValue = assets[TierForQualityLevel[index]];
                assigned++;
            }

            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"QUALITY TIERS: assigned a pipeline asset to {assigned} quality level(s).");
        }

        /// <summary>
        /// Fails if any quality level still resolves to the shared asset.
        /// </summary>
        /// <remarks>
        /// Runs headless and exits non-zero:
        ///   Unity -batchmode -quit -executeMethod LTW.UnityClient.Editor.QualityTierSetup.ValidateTierAssets
        /// </remarks>
        [MenuItem("Line Wards/Migration/Validate Quality Tier Pipeline Assets")]
        public static void ValidateTierAssets()
        {
            var failures = new List<string>();
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("QualitySettings"));
            var levels = settings.FindProperty("m_QualitySettings");

            if (levels == null || !levels.isArray)
            {
                failures.Add("could not read m_QualitySettings.");
            }
            else
            {
                var distinct = new HashSet<string>();
                for (var index = 0; index < levels.arraySize; index++)
                {
                    var element = levels.GetArrayElementAtIndex(index);
                    var name = element.FindPropertyRelative("name")?.stringValue ?? $"level {index}";
                    var pipeline = element.FindPropertyRelative("customRenderPipeline")?.objectReferenceValue;

                    if (pipeline == null)
                    {
                        failures.Add($"'{name}' has no custom render pipeline, so it renders identically to every other tier.");
                    }
                    else
                    {
                        distinct.Add(pipeline.name);
                    }
                }

                // One asset across every tier is the defect this file exists to fix, so it fails
                // even when each level technically has an assignment.
                if (failures.Count == 0 && distinct.Count < 2)
                {
                    failures.Add($"all quality levels resolve to the same pipeline asset ({string.Join(", ", distinct)}); the tiers do not differ.");
                }
            }

            if (failures.Count == 0)
            {
                Debug.Log("QUALITY TIERS OK: every quality level has a pipeline asset and the tiers differ.");
            }
            else
            {
                foreach (var failure in failures)
                {
                    Debug.LogError($"QUALITY TIERS FAIL: {failure}");
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }
    }
}
