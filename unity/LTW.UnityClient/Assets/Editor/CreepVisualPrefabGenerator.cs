using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class CreepVisualPrefabGenerator
    {
        private const string MenuPath = "Line Wards/Art/Generate Placeholder Creep Prefabs";
        private const string ValidateMenuPath = "Line Wards/Art/Validate Creep Visual Library";
        private const string LibraryPath = "Assets/Resources/CreepVisualLibrary.asset";
        private const string PrefabFolder = "Assets/Prefabs/Creeps";
        private const string MaterialFolder = "Assets/Art/Creeps/GeneratedMaterials";

        private const string RunnerPrefabPath = PrefabFolder + "/Creep_Runner.prefab";
        private const string BrutePrefabPath = PrefabFolder + "/Creep_Brute.prefab";
        private const string SwarmPrefabPath = PrefabFolder + "/Creep_Swarm.prefab";

        [MenuItem(MenuPath)]
        public static void GeneratePlaceholderCreepPrefabs()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            var bodyMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/Creep_Body.mat",
                new Color(0.26f, 0.82f, 1f));
            var accentMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/Creep_SenderAccent.mat",
                new Color(0.95f, 0.82f, 0.34f));
            var damageMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/Creep_Damage.mat",
                new Color(0.95f, 0.22f, 0.18f));
            var shadowMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/Creep_Shadow.mat",
                new Color(0.04f, 0.05f, 0.07f, 0.42f));

            var runnerPrefab = SaveRunnerPrefab(bodyMaterial, accentMaterial, damageMaterial, shadowMaterial);
            var brutePrefab = SaveBrutePrefab(bodyMaterial, accentMaterial, damageMaterial, shadowMaterial);
            var swarmPrefab = SaveSwarmPrefab(bodyMaterial, accentMaterial, damageMaterial, shadowMaterial);

            UpdateVisualLibrary(runnerPrefab, brutePrefab, swarmPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated placeholder creep prefabs and updated CreepVisualLibrary.");
        }

        [MenuItem(ValidateMenuPath)]
        public static void ValidateCreepVisualLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<CreepVisualLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"Missing creep visual library at {LibraryPath}.");
                return;
            }

            var issueCount = 0;
            foreach (var profile in library.Profiles)
            {
                if (profile == null)
                {
                    issueCount++;
                    Debug.LogError("Creep visual library contains a null profile entry.", library);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(profile.CreepId))
                {
                    issueCount++;
                    Debug.LogError("Creep visual profile is missing a creep id.", library);
                }

                if (profile.Prefab == null)
                {
                    issueCount++;
                    Debug.LogWarning($"Creep visual profile '{profile.CreepId}' has no prefab assigned yet.", library);
                    continue;
                }

                issueCount += ValidateRendererPath(profile, profile.BodyRendererPath, "body");
                issueCount += ValidateRendererPaths(profile, profile.SenderAccentRendererPaths, "sender accent");
                issueCount += ValidateRendererPaths(profile, profile.DamageRendererPaths, "damage");
            }

            if (issueCount == 0)
            {
                Debug.Log("Creep visual library validation passed.");
            }
            else
            {
                Debug.LogWarning($"Creep visual library validation completed with {issueCount} issue(s).");
            }
        }

        private static GameObject SaveRunnerPrefab(
            Material bodyMaterial,
            Material accentMaterial,
            Material damageMaterial,
            Material shadowMaterial)
        {
            var root = new GameObject("Creep_Runner");
            CreateChild(root, "Shadow", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.9f, 0.025f, 1.2f), shadowMaterial);
            CreateChild(root, "Body", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(0.58f, 0.34f, 1.05f), bodyMaterial);
            CreateChild(root, "Accent", PrimitiveType.Cube, new Vector3(0f, 0.14f, -0.48f), new Vector3(0.18f, 0.12f, 0.24f), accentMaterial);
            CreateChild(root, "Damage", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0.34f), new Vector3(0.34f, 0.08f, 0.16f), damageMaterial);
            CreateChild(root, "SpeedLine", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.72f), new Vector3(0.08f, 0.05f, 0.64f), accentMaterial);

            return SavePrefab(root, RunnerPrefabPath);
        }

        private static GameObject SaveBrutePrefab(
            Material bodyMaterial,
            Material accentMaterial,
            Material damageMaterial,
            Material shadowMaterial)
        {
            var root = new GameObject("Creep_Brute");
            CreateChild(root, "Shadow", PrimitiveType.Cylinder, new Vector3(0f, -0.1f, 0f), new Vector3(1.25f, 0.03f, 1.25f), shadowMaterial);
            CreateChild(root, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0f), new Vector3(1.05f, 0.72f, 1.05f), bodyMaterial);

            var accent = new GameObject("Accent");
            accent.transform.SetParent(root.transform, false);
            CreateChild(accent, "Core", PrimitiveType.Sphere, new Vector3(0f, 0.2f, -0.43f), new Vector3(0.34f, 0.24f, 0.16f), accentMaterial);

            var damage = new GameObject("Damage");
            damage.transform.SetParent(root.transform, false);
            CreateChild(damage, "PlateLeft", PrimitiveType.Cube, new Vector3(-0.42f, 0.2f, 0.05f), new Vector3(0.2f, 0.12f, 0.58f), damageMaterial);
            CreateChild(damage, "PlateRight", PrimitiveType.Cube, new Vector3(0.42f, 0.2f, 0.05f), new Vector3(0.2f, 0.12f, 0.58f), damageMaterial);

            return SavePrefab(root, BrutePrefabPath);
        }

        private static GameObject SaveSwarmPrefab(
            Material bodyMaterial,
            Material accentMaterial,
            Material damageMaterial,
            Material shadowMaterial)
        {
            var root = new GameObject("Creep_Swarm");
            CreateChild(root, "Shadow", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(1.15f, 0.02f, 1.15f), shadowMaterial);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            CreateChild(body, "Shard0", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0f), new Vector3(0.36f, 0.24f, 0.36f), bodyMaterial);
            CreateChild(body, "Shard1", PrimitiveType.Sphere, new Vector3(-0.34f, 0.08f, 0.2f), new Vector3(0.28f, 0.2f, 0.28f), bodyMaterial);
            CreateChild(body, "Shard2", PrimitiveType.Sphere, new Vector3(0.36f, 0.09f, 0.18f), new Vector3(0.26f, 0.18f, 0.26f), bodyMaterial);
            CreateChild(body, "Shard3", PrimitiveType.Sphere, new Vector3(0.04f, 0.08f, -0.36f), new Vector3(0.24f, 0.18f, 0.24f), bodyMaterial);

            var accent = new GameObject("Accent");
            accent.transform.SetParent(root.transform, false);
            CreateChild(accent, "Signal0", PrimitiveType.Sphere, new Vector3(-0.18f, 0.23f, -0.12f), new Vector3(0.14f, 0.1f, 0.14f), accentMaterial);
            CreateChild(accent, "Signal1", PrimitiveType.Sphere, new Vector3(0.22f, 0.21f, 0.18f), new Vector3(0.12f, 0.09f, 0.12f), accentMaterial);

            var damage = new GameObject("Damage");
            damage.transform.SetParent(root.transform, false);
            CreateChild(damage, "CrackedShard", PrimitiveType.Sphere, new Vector3(0.34f, 0.16f, -0.18f), new Vector3(0.18f, 0.12f, 0.18f), damageMaterial);

            return SavePrefab(root, SwarmPrefabPath);
        }

        private static GameObject CreateChild(
            GameObject parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var child = GameObject.CreatePrimitive(primitiveType);
            child.name = name;
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;

            if (child.TryGetComponent<Collider>(out var collider))
            {
                Object.DestroyImmediate(collider);
            }

            if (child.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }

            return child;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindDefaultShader());
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader FindDefaultShader() =>
            Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Sprites/Default");

        private static void UpdateVisualLibrary(
            GameObject runnerPrefab,
            GameObject brutePrefab,
            GameObject swarmPrefab)
        {
            var library = AssetDatabase.LoadAssetAtPath<CreepVisualLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CreepVisualLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var serializedLibrary = new SerializedObject(library);
            var profiles = serializedLibrary.FindProperty("profiles");
            profiles.arraySize = 3;

            ConfigureProfile(
                profiles.GetArrayElementAtIndex(0),
                "creep.runner",
                CreepVisualRole.Runner,
                runnerPrefab,
                new Vector3(0.42f, 0.24f, 0.72f),
                CreepVisualMotionStyle.RunnerDart,
                "Body",
                new[] { "Accent", "SpeedLine" },
                new[] { "Damage" });
            ConfigureProfile(
                profiles.GetArrayElementAtIndex(1),
                "creep.brute",
                CreepVisualRole.Brute,
                brutePrefab,
                new Vector3(0.78f, 0.48f, 0.96f),
                CreepVisualMotionStyle.HeavyBob,
                "Body",
                new[] { "Accent/Core" },
                new[] { "Damage/PlateLeft", "Damage/PlateRight" });
            ConfigureProfile(
                profiles.GetArrayElementAtIndex(2),
                "creep.swarm",
                CreepVisualRole.Swarm,
                swarmPrefab,
                new Vector3(0.18f, 0.12f, 0.18f),
                CreepVisualMotionStyle.ClusterJitter,
                "Body",
                new[] { "Accent/Signal0", "Accent/Signal1" },
                new[] { "Damage/CrackedShard" });

            serializedLibrary.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
        }

        private static void ConfigureProfile(
            SerializedProperty profile,
            string creepId,
            CreepVisualRole role,
            GameObject prefab,
            Vector3 scale,
            CreepVisualMotionStyle motionStyle,
            string bodyRendererPath,
            string[] senderAccentRendererPaths,
            string[] damageRendererPaths)
        {
            profile.FindPropertyRelative("creepId").stringValue = creepId;
            profile.FindPropertyRelative("role").enumValueIndex = (int)role;
            profile.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            profile.FindPropertyRelative("scale").vector3Value = scale;
            profile.FindPropertyRelative("motionStyle").enumValueIndex = (int)motionStyle;
            profile.FindPropertyRelative("bodyRendererPath").stringValue = bodyRendererPath;
            SetStringArray(profile.FindPropertyRelative("senderAccentRendererPaths"), senderAccentRendererPaths);
            SetStringArray(profile.FindPropertyRelative("damageRendererPaths"), damageRendererPaths);
        }

        private static void SetStringArray(SerializedProperty property, string[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }
        }

        private static int ValidateRendererPaths(
            CreepVisualProfile profile,
            System.Collections.Generic.IReadOnlyList<string> paths,
            string slotName)
        {
            var issueCount = 0;
            for (var index = 0; index < paths.Count; index++)
            {
                issueCount += ValidateRendererPath(profile, paths[index], slotName);
            }

            return issueCount;
        }

        private static int ValidateRendererPath(CreepVisualProfile profile, string path, string slotName)
        {
            if (profile.Prefab == null || string.IsNullOrWhiteSpace(path))
            {
                return 0;
            }

            var target = profile.Prefab.transform.Find(path);
            if (target == null)
            {
                Debug.LogError($"Creep visual profile '{profile.CreepId}' has missing {slotName} path '{path}'.", profile.Prefab);
                return 1;
            }

            if (target.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                Debug.LogError($"Creep visual profile '{profile.CreepId}' has {slotName} path '{path}' with no renderers.", profile.Prefab);
                return 1;
            }

            return 0;
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
            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
