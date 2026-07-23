using System;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class Tower3DProofSetGenerator
    {
        private const string GenerateMenuPath = "Line Wards/Art/Generate Available Tower 3D Proof Wrappers";
        private const string ValidateMenuPath = "Line Wards/Art/Validate Tower 3D Proof Wrappers";
        private const string PromoteMenuPath = "Line Wards/Art/Promote Complete Tower 3D Set";
        private const string LibraryPath = "Assets/Resources/TowerVisualLibrary.asset";
        private const string RuntimePrefabFolder = "Assets/Prefabs/Towers";
        private const string MaterialFolder = "Assets/Art/Towers/Production/Materials";
        private const string RuntimeMaterialPath = MaterialFolder + "/mat_tower_3d_cohesion_runtime_v01.mat";

        private static readonly Tower3DSpec[] Specs =
        {
            new(
                "Arrow",
                "tower.arrow",
                TowerVisualRole.Arrow,
                "Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d.prefab",
                RuntimePrefabFolder + "/Tower_Arrow_3D.prefab",
                new Vector3(1.36f, 1.36f, 1.36f),
                0.16f,
                new[] { "Muzzle", "Lens", "BowLeft", "BowRight" },
                new Vector3(0f, 0.18f, 0.72f),
                new Vector3(0f, 0.2f, 0.06f)),
            new(
                "Control",
                "tower.control",
                TowerVisualRole.Control,
                "Assets/Art/AIStaging/Models/Towers/Control/tower_control_3d.prefab",
                RuntimePrefabFolder + "/Tower_Control_3D.prefab",
                new Vector3(1.12f, 1.08f, 1.12f),
                0.14f,
                new[] { "Muzzle", "Lens", "ControlCore", "ControlRing", "PulseEmitter" },
                new Vector3(0f, 0.2f, 0.62f),
                new Vector3(0f, 0.24f, 0f)),
            new(
                "Relay",
                "tower.relay",
                TowerVisualRole.Relay,
                "Assets/Art/AIStaging/Models/Towers/Relay/tower_relay_3d.prefab",
                RuntimePrefabFolder + "/Tower_Relay_3D.prefab",
                new Vector3(1.05f, 1.18f, 1.05f),
                0.18f,
                new[] { "Muzzle", "Lens", "RelayMast", "RelaySignal", "RelayCore" },
                new Vector3(0f, 0.28f, 0.48f),
                new Vector3(0f, 0.44f, 0f)),
            new(
                "Pulse",
                "tower.pulse",
                TowerVisualRole.Pulse,
                "Assets/Art/AIStaging/Models/Towers/Pulse/tower_pulse_3d.prefab",
                RuntimePrefabFolder + "/Tower_Pulse_3D.prefab",
                new Vector3(1.16f, 1.08f, 1.16f),
                0.14f,
                new[] { "Muzzle", "Lens", "PulseCore", "PulseRingA", "PulseEmitter" },
                new Vector3(0f, 0.22f, 0.52f),
                new Vector3(0f, 0.24f, 0f)),
            new(
                "Prism",
                "tower.prism",
                TowerVisualRole.Prism,
                "Assets/Art/AIStaging/Models/Towers/Prism/tower_prism_3d.prefab",
                RuntimePrefabFolder + "/Tower_Prism_3D.prefab",
                new Vector3(1.08f, 1.22f, 1.08f),
                0.16f,
                new[] { "Muzzle", "Lens", "PrismSpire", "PrismLens", "BeamAnchor" },
                new Vector3(0f, 0.32f, 0.5f),
                new Vector3(0f, 0.48f, 0f))
        };

        [MenuItem(GenerateMenuPath)]
        public static void GenerateAvailableProofWrappers()
        {
            EnsureFolder(RuntimePrefabFolder);
            EnsureFolder(MaterialFolder);
            var material = CreateRuntimeMaterial();
            var generatedCount = 0;

            for (var index = 0; index < Specs.Length; index++)
            {
                if (GenerateWrapperIfRawExists(Specs[index], material))
                {
                    generatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated {generatedCount} available Tower 3D proof wrapper(s). Missing raw generated prefabs are left untouched.");
        }

        [MenuItem(ValidateMenuPath)]
        public static void ValidateProofWrappers()
        {
            var issueCount = 0;
            for (var index = 0; index < Specs.Length; index++)
            {
                issueCount += ValidateWrapper(Specs[index], requireExists: false);
            }

            if (issueCount == 0)
            {
                Debug.Log("Tower 3D proof wrapper validation passed for available wrappers.");
            }
            else
            {
                Debug.LogWarning($"Tower 3D proof wrapper validation completed with {issueCount} issue(s).");
            }
        }

        [MenuItem(PromoteMenuPath)]
        public static void PromoteCompleteTower3DSet()
        {
            var issueCount = 0;
            for (var index = 0; index < Specs.Length; index++)
            {
                issueCount += ValidateWrapper(Specs[index], requireExists: true);
            }

            if (issueCount > 0)
            {
                Debug.LogError($"Tower 3D set promotion blocked. Resolve {issueCount} missing/invalid wrapper issue(s) first.");
                return;
            }

            var library = AssetDatabase.LoadAssetAtPath<TowerVisualLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"Missing tower visual library at {LibraryPath}. Not promoting Tower 3D set.");
                return;
            }

            var serializedLibrary = new SerializedObject(library);
            var profiles = serializedLibrary.FindProperty("profiles");
            for (var index = 0; index < Specs.Length; index++)
            {
                UpsertProfile(profiles, Specs[index]);
            }

            serializedLibrary.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("Promoted complete Tower 3D set to TowerVisualLibrary.");
        }

        private static bool GenerateWrapperIfRawExists(Tower3DSpec spec, Material material)
        {
            var rawPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RawPrefabPath);
            if (rawPrefab == null)
            {
                Debug.LogWarning($"Skipping {spec.DisplayName}: missing raw 3D prefab at {spec.RawPrefabPath}.");
                return false;
            }

            if (rawPrefab.GetComponentsInChildren<MeshRenderer>(true).Length == 0 ||
                rawPrefab.GetComponentsInChildren<MeshFilter>(true).Length == 0)
            {
                Debug.LogWarning($"Skipping {spec.DisplayName}: raw 3D prefab has no mesh renderers/filters at {spec.RawPrefabPath}.");
                return false;
            }

            var root = new GameObject($"Tower_{spec.DisplayName}_3D");
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);

            var generatedInstance = PrefabUtility.InstantiatePrefab(rawPrefab) as GameObject;
            if (generatedInstance == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                Debug.LogWarning($"Skipping {spec.DisplayName}: failed to instantiate raw 3D prefab.");
                return false;
            }

            generatedInstance.name = "UnityAIGeneratedVisual";
            generatedInstance.transform.SetParent(body.transform, false);
            generatedInstance.transform.localPosition = Vector3.zero;
            generatedInstance.transform.localRotation = Quaternion.identity;
            generatedInstance.transform.localScale = Vector3.one;
            StripColliders(generatedInstance);
            ApplyRuntimeMaterial(generatedInstance, material);

            CreateEmptyChild(root, "BodyTintAnchor", Vector3.zero);
            CreateEmptyChild(root, "RoleMarker", Vector3.zero);
            CreateEmptyChild(root, "OwnerTrim", Vector3.zero);
            CreateRangeHalo(root);
            for (var index = 0; index < spec.AnchorNames.Length; index++)
            {
                var anchorName = spec.AnchorNames[index];
                var anchorPosition = ResolveAnchorPosition(spec, anchorName);
                CreateEmptyChild(root, anchorName, anchorPosition);
            }

            PrefabUtility.SaveAsPrefabAsset(root, spec.RuntimePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return true;
        }

        private static Vector3 ResolveAnchorPosition(Tower3DSpec spec, string anchorName)
        {
            if (anchorName == "Muzzle" || anchorName == "PulseEmitter" || anchorName == "BeamAnchor")
            {
                return spec.MuzzlePosition;
            }

            if (anchorName == "Lens" ||
                anchorName == "ControlCore" ||
                anchorName == "RelayCore" ||
                anchorName == "PulseCore" ||
                anchorName == "PrismLens")
            {
                return spec.CorePosition;
            }

            if (anchorName == "BowLeft")
            {
                return new Vector3(-0.36f, spec.CorePosition.y, 0.08f);
            }

            if (anchorName == "BowRight")
            {
                return new Vector3(0.36f, spec.CorePosition.y, 0.08f);
            }

            if (anchorName == "ControlRing" || anchorName == "PulseRingA")
            {
                return new Vector3(0f, spec.CorePosition.y - 0.02f, 0f);
            }

            if (anchorName == "RelayMast" || anchorName == "RelaySignal" || anchorName == "PrismSpire")
            {
                return new Vector3(0f, spec.CorePosition.y + 0.18f, 0f);
            }

            return Vector3.zero;
        }

        private static int ValidateWrapper(Tower3DSpec spec, bool requireExists)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RuntimePrefabPath);
            if (prefab == null)
            {
                if (requireExists)
                {
                    Debug.LogError($"Missing required Tower 3D wrapper for {spec.DisplayName}: {spec.RuntimePrefabPath}.");
                    return 1;
                }

                Debug.LogWarning($"Tower 3D wrapper not yet generated for {spec.DisplayName}: {spec.RuntimePrefabPath}.");
                return 0;
            }

            var issueCount = 0;
            issueCount += ValidateChild(prefab, "Body", requireRenderer: true);
            issueCount += ValidateChild(prefab, "BodyTintAnchor", requireRenderer: false);
            issueCount += ValidateChild(prefab, "RoleMarker", requireRenderer: false);
            issueCount += ValidateChild(prefab, "OwnerTrim", requireRenderer: false);
            issueCount += ValidateChild(prefab, "RangeHalo", requireRenderer: false);
            for (var index = 0; index < spec.AnchorNames.Length; index++)
            {
                issueCount += ValidateChild(prefab, spec.AnchorNames[index], requireRenderer: false);
            }

            return issueCount;
        }

        private static int ValidateChild(GameObject prefab, string childPath, bool requireRenderer)
        {
            var child = prefab.transform.Find(childPath);
            if (child == null)
            {
                Debug.LogError($"{prefab.name} is missing child '{childPath}'.", prefab);
                return 1;
            }

            if (requireRenderer && child.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                Debug.LogError($"{prefab.name} child '{childPath}' has no renderer.", prefab);
                return 1;
            }

            return 0;
        }

        private static void UpsertProfile(SerializedProperty profiles, Tower3DSpec spec)
        {
            var profileIndex = -1;
            for (var index = 0; index < profiles.arraySize; index++)
            {
                if (profiles.GetArrayElementAtIndex(index).FindPropertyRelative("towerId").stringValue == spec.TowerId)
                {
                    profileIndex = index;
                    break;
                }
            }

            if (profileIndex < 0)
            {
                profileIndex = profiles.arraySize;
                profiles.InsertArrayElementAtIndex(profileIndex);
            }

            var profile = profiles.GetArrayElementAtIndex(profileIndex);
            profile.FindPropertyRelative("towerId").stringValue = spec.TowerId;
            profile.FindPropertyRelative("role").enumValueIndex = (int)spec.Role;
            profile.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RuntimePrefabPath);
            profile.FindPropertyRelative("scale").vector3Value = spec.RuntimeScale;
            profile.FindPropertyRelative("lift").floatValue = spec.RuntimeLift;
            profile.FindPropertyRelative("bodyRendererPath").stringValue = "BodyTintAnchor";
            profile.FindPropertyRelative("roleMarkerRendererPath").stringValue = "RoleMarker";
            profile.FindPropertyRelative("ownerTrimRendererPath").stringValue = "OwnerTrim";
            profile.FindPropertyRelative("rangeHaloRendererPath").stringValue = "RangeHalo";
        }

        private static void StripColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(colliders[index]);
            }
        }

        private static void ApplyRuntimeMaterial(GameObject root, Material material)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                var materials = renderer.sharedMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    materials[materialIndex] = material;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void CreateRangeHalo(GameObject parent)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            child.name = "RangeHalo";
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            child.transform.localScale = new Vector3(1.55f, 0.01f, 1.55f);
            if (child.TryGetComponent<Collider>(out var collider))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            if (child.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = CreateHaloMaterial();
                renderer.enabled = false;
            }
        }

        private static GameObject CreateEmptyChild(GameObject parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static Material CreateRuntimeMaterial()
        {
            EnsureFolder(MaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(RuntimeMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, RuntimeMaterialPath);
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(RuntimeMaterialPath);
            material.color = new Color(0.82f, 0.95f, 1f, 1f);
            material.enableInstancing = true;
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.18f);
            SetFloatIfPresent(material, "_Glossiness", 0.18f);
            SetColorIfPresent(material, "_EmissionColor", new Color(0.02f, 0.035f, 0.05f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateHaloMaterial()
        {
            var path = MaterialFolder + "/mat_tower_3d_range_halo_v01.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            material.color = new Color(0.24f, 0.62f, 1f, 0.18f);
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

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            var folder = System.IO.Path.GetFileName(assetPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        [Serializable]
        private readonly struct Tower3DSpec
        {
            public Tower3DSpec(
                string displayName,
                string towerId,
                TowerVisualRole role,
                string rawPrefabPath,
                string runtimePrefabPath,
                Vector3 runtimeScale,
                float runtimeLift,
                string[] anchorNames,
                Vector3 muzzlePosition,
                Vector3 corePosition)
            {
                DisplayName = displayName;
                TowerId = towerId;
                Role = role;
                RawPrefabPath = rawPrefabPath;
                RuntimePrefabPath = runtimePrefabPath;
                RuntimeScale = runtimeScale;
                RuntimeLift = runtimeLift;
                AnchorNames = anchorNames;
                MuzzlePosition = muzzlePosition;
                CorePosition = corePosition;
            }

            public string DisplayName { get; }

            public string TowerId { get; }

            public TowerVisualRole Role { get; }

            public string RawPrefabPath { get; }

            public string RuntimePrefabPath { get; }

            public Vector3 RuntimeScale { get; }

            public float RuntimeLift { get; }

            public string[] AnchorNames { get; }

            public Vector3 MuzzlePosition { get; }

            public Vector3 CorePosition { get; }
        }
    }
}
