using System.IO;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class Tower3DRawCandidateGenerator
    {
        private const string GenerateControlMenuPath = "Line Wards/Art/Rejected/Generate Rejected Control Mesh-Card Proof";
        private const string ControlFolder = "Assets/Art/AIStaging/Models/Towers/Control";
        private const string ControlPrefabPath = ControlFolder + "/tower_control_3d.prefab";
        private const string MaterialFolder = ControlFolder + "/Materials";
        private const string ControlPlatePath = "Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_control_candidate_v01_alpha.png";

        [MenuItem(GenerateControlMenuPath)]
        public static void GenerateControlCandidate()
        {
            Debug.LogWarning("Generating the rejected Control mesh-card proof for evidence only. This is not a production 3D source path and remains non-promotable.");

            EnsureFolder(ControlFolder);
            EnsureFolder(MaterialFolder);

            var controlPlate = AssetDatabase.LoadAssetAtPath<Texture2D>(ControlPlatePath);
            if (controlPlate == null)
            {
                Debug.LogError($"Missing polished Control source plate at {ControlPlatePath}.");
                return;
            }

            var root = new GameObject("tower_control_3d");
            var plateMaterial = CreateTransparentTexturedMaterial("mat_control_raw_polished_plate_v02", controlPlate, Color.white);
            var shadowMaterial = CreateTransparentTexturedMaterial("mat_control_raw_soft_depth_shadow_v02", controlPlate, new Color(0.05f, 0.08f, 0.12f, 0.34f));
            var rimMaterial = CreateMaterial("mat_control_raw_depth_rim_v02", new Color(0.1f, 0.18f, 0.24f, 1f));

            CreatePrimitive(root, "ControlSoftDepthShadow", PrimitiveType.Quad, new Vector3(0f, 0.1f, 0.12f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1.34f, 1.34f, 1f), shadowMaterial);
            CreatePrimitive(root, "ControlPolishedPlate", PrimitiveType.Quad, new Vector3(0f, 0.2f, 0.08f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1.28f, 1.28f, 1f), plateMaterial);

            CreatePrimitive(root, "SubtleDepthBase", PrimitiveType.Cylinder, new Vector3(0f, 0.055f, -0.04f), Quaternion.identity, new Vector3(0.62f, 0.028f, 0.62f), rimMaterial);
            CreatePrimitive(root, "BackPlateOffset", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, -0.1f), Quaternion.identity, new Vector3(0.5f, 0.018f, 0.5f), rimMaterial);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, ControlPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(saved != null
                ? $"Generated rejected raw Control mesh-card proof at {ControlPrefabPath}. Do not promote this proof."
                : $"Failed to generate raw Control 3D candidate at {ControlPrefabPath}.");
        }

        private static GameObject CreatePrimitive(
            GameObject parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material material)
        {
            var child = GameObject.CreatePrimitive(primitiveType);
            child.name = name;
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            child.transform.localScale = scale;

            if (child.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return child;
        }

        private static Material CreateMaterial(string name, Color color, Color? emission = null)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = name;
            material.color = color;
            material.enableInstancing = true;
            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.18f);
            SetFloatIfPresent(material, "_Glossiness", 0.18f);
            SetColorIfPresent(material, "_EmissionColor", emission ?? Color.black);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTransparentTexturedMaterial(string name, Texture texture, Color color)
        {
            var material = CreateMaterial(name, color, new Color(0.02f, 0.035f, 0.05f, 1f));
            material.mainTexture = texture;
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetTextureIfPresent(material, "_BaseMap", texture);
            SetColorIfPresent(material, "_BaseColor", color);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            var folder = Path.GetFileName(assetPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
