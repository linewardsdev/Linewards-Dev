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
        private const string MeshFolder = "Assets/Art/Creeps/GeneratedMeshes";
        private const string ReportPath = "Assets/Art/Creeps/GeneratedPlaceholderReport.md";

        private const string RunnerPrefabPath = PrefabFolder + "/Creep_Runner.prefab";
        private const string BrutePrefabPath = PrefabFolder + "/Creep_Brute.prefab";
        private const string SwarmPrefabPath = PrefabFolder + "/Creep_Swarm.prefab";

        [MenuItem(MenuPath)]
        public static void GeneratePlaceholderCreepPrefabs()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(PrefabFolder);

            var runnerDartMesh = CreateOrUpdateMesh(MeshFolder + "/Runner_Dart.asset", CreateRunnerDartMesh());
            var runnerFinMesh = CreateOrUpdateMesh(MeshFolder + "/Runner_Fin.asset", CreateWedgePlateMesh());
            var bruteCoreMesh = CreateOrUpdateMesh(MeshFolder + "/Brute_ArmoredCore.asset", CreateOctagonalCoreMesh());
            var brutePlateMesh = CreateOrUpdateMesh(MeshFolder + "/Brute_ArmorPlate.asset", CreateWedgePlateMesh());
            var swarmShardMesh = CreateOrUpdateMesh(MeshFolder + "/Swarm_Shard.asset", CreateShardMesh());

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

            var runnerPrefab = SaveRunnerPrefab(runnerDartMesh, runnerFinMesh, bodyMaterial, accentMaterial, damageMaterial, shadowMaterial);
            var brutePrefab = SaveBrutePrefab(bruteCoreMesh, brutePlateMesh, bodyMaterial, accentMaterial, damageMaterial, shadowMaterial);
            var swarmPrefab = SaveSwarmPrefab(swarmShardMesh, bodyMaterial, accentMaterial, damageMaterial, shadowMaterial);

            UpdateVisualLibrary(runnerPrefab, brutePrefab, swarmPrefab);
            WriteGenerationReport();

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ReportPath);
            AssetDatabase.Refresh();
            Debug.Log($"Generated placeholder creep prefabs, updated CreepVisualLibrary, and wrote {ReportPath}.");
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
            Mesh runnerDartMesh,
            Mesh runnerFinMesh,
            Material bodyMaterial,
            Material accentMaterial,
            Material damageMaterial,
            Material shadowMaterial)
        {
            var root = new GameObject("Creep_Runner");
            CreateChild(root, "Shadow", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.9f, 0.025f, 1.2f), shadowMaterial);
            CreateMeshChild(root, "Body", runnerDartMesh, new Vector3(0f, 0.11f, 0f), new Vector3(0.68f, 0.34f, 1.16f), bodyMaterial);
            CreateMeshChild(root, "Accent", runnerFinMesh, new Vector3(0f, 0.17f, -0.52f), new Vector3(0.22f, 0.14f, 0.28f), accentMaterial);
            CreateMeshChild(root, "Damage", runnerFinMesh, new Vector3(0f, 0.22f, 0.35f), new Vector3(0.4f, 0.1f, 0.18f), damageMaterial);
            CreateChild(root, "SpeedLine", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.72f), new Vector3(0.08f, 0.05f, 0.64f), accentMaterial);

            return SavePrefab(root, RunnerPrefabPath);
        }

        private static GameObject SaveBrutePrefab(
            Mesh bruteCoreMesh,
            Mesh brutePlateMesh,
            Material bodyMaterial,
            Material accentMaterial,
            Material damageMaterial,
            Material shadowMaterial)
        {
            var root = new GameObject("Creep_Brute");
            CreateChild(root, "Shadow", PrimitiveType.Cylinder, new Vector3(0f, -0.1f, 0f), new Vector3(1.25f, 0.03f, 1.25f), shadowMaterial);
            CreateMeshChild(root, "Body", bruteCoreMesh, new Vector3(0f, 0.2f, 0f), new Vector3(1.05f, 0.72f, 1.05f), bodyMaterial);

            var accent = new GameObject("Accent");
            accent.transform.SetParent(root.transform, false);
            CreateMeshChild(accent, "Core", bruteCoreMesh, new Vector3(0f, 0.22f, -0.45f), new Vector3(0.34f, 0.24f, 0.18f), accentMaterial);

            var damage = new GameObject("Damage");
            damage.transform.SetParent(root.transform, false);
            CreateMeshChild(damage, "PlateLeft", brutePlateMesh, new Vector3(-0.42f, 0.22f, 0.05f), new Vector3(0.24f, 0.12f, 0.6f), damageMaterial);
            CreateMeshChild(damage, "PlateRight", brutePlateMesh, new Vector3(0.42f, 0.22f, 0.05f), new Vector3(0.24f, 0.12f, 0.6f), damageMaterial);

            return SavePrefab(root, BrutePrefabPath);
        }

        private static GameObject SaveSwarmPrefab(
            Mesh swarmShardMesh,
            Material bodyMaterial,
            Material accentMaterial,
            Material damageMaterial,
            Material shadowMaterial)
        {
            var root = new GameObject("Creep_Swarm");
            CreateChild(root, "Shadow", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(1.15f, 0.02f, 1.15f), shadowMaterial);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            CreateMeshChild(body, "Shard0", swarmShardMesh, new Vector3(0f, 0.12f, 0f), new Vector3(0.36f, 0.24f, 0.36f), bodyMaterial);
            CreateMeshChild(body, "Shard1", swarmShardMesh, new Vector3(-0.34f, 0.08f, 0.2f), new Vector3(0.28f, 0.2f, 0.28f), bodyMaterial);
            CreateMeshChild(body, "Shard2", swarmShardMesh, new Vector3(0.36f, 0.09f, 0.18f), new Vector3(0.26f, 0.18f, 0.26f), bodyMaterial);
            CreateMeshChild(body, "Shard3", swarmShardMesh, new Vector3(0.04f, 0.08f, -0.36f), new Vector3(0.24f, 0.18f, 0.24f), bodyMaterial);

            var accent = new GameObject("Accent");
            accent.transform.SetParent(root.transform, false);
            CreateMeshChild(accent, "Signal0", swarmShardMesh, new Vector3(-0.18f, 0.23f, -0.12f), new Vector3(0.14f, 0.1f, 0.14f), accentMaterial);
            CreateMeshChild(accent, "Signal1", swarmShardMesh, new Vector3(0.22f, 0.21f, 0.18f), new Vector3(0.12f, 0.09f, 0.12f), accentMaterial);

            var damage = new GameObject("Damage");
            damage.transform.SetParent(root.transform, false);
            CreateMeshChild(damage, "CrackedShard", swarmShardMesh, new Vector3(0.34f, 0.16f, -0.18f), new Vector3(0.18f, 0.12f, 0.18f), damageMaterial);

            return SavePrefab(root, SwarmPrefabPath);
        }

        private static GameObject CreateMeshChild(
            GameObject parent,
            string name,
            Mesh mesh,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            return child;
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

        private static Mesh CreateOrUpdateMesh(string path, Mesh source)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            mesh.Clear();
            mesh.vertices = source.vertices;
            mesh.triangles = source.triangles;
            mesh.uv = source.uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
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
                new[] { "Damage" },
                CreepDeathCueStyle.SparkBurst);
            ConfigureProfile(
                profiles.GetArrayElementAtIndex(1),
                "creep.brute",
                CreepVisualRole.Brute,
                brutePrefab,
                new Vector3(0.78f, 0.48f, 0.96f),
                CreepVisualMotionStyle.HeavyBob,
                "Body",
                new[] { "Accent/Core" },
                new[] { "Damage/PlateLeft", "Damage/PlateRight" },
                CreepDeathCueStyle.HeavyShatter);
            ConfigureProfile(
                profiles.GetArrayElementAtIndex(2),
                "creep.swarm",
                CreepVisualRole.Swarm,
                swarmPrefab,
                new Vector3(0.18f, 0.12f, 0.18f),
                CreepVisualMotionStyle.ClusterJitter,
                "Body",
                new[] { "Accent/Signal0", "Accent/Signal1" },
                new[] { "Damage/CrackedShard" },
                CreepDeathCueStyle.ShardScatter);

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
            string[] damageRendererPaths,
            CreepDeathCueStyle deathCueStyle)
        {
            profile.FindPropertyRelative("creepId").stringValue = creepId;
            profile.FindPropertyRelative("role").enumValueIndex = (int)role;
            profile.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            profile.FindPropertyRelative("scale").vector3Value = scale;
            profile.FindPropertyRelative("motionStyle").enumValueIndex = (int)motionStyle;
            profile.FindPropertyRelative("bodyRendererPath").stringValue = bodyRendererPath;
            SetStringArray(profile.FindPropertyRelative("senderAccentRendererPaths"), senderAccentRendererPaths);
            SetStringArray(profile.FindPropertyRelative("damageRendererPaths"), damageRendererPaths);
            profile.FindPropertyRelative("deathCueStyle").enumValueIndex = (int)deathCueStyle;
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

        private static void WriteGenerationReport()
        {
            var report = @"# Generated Creep Placeholder Prefabs

This report is generated by `Line Wards > Art > Generate Placeholder Creep Prefabs`.

## Generated Prefabs

| Creep | Prefab | Body Path | Sender Accent Paths | Damage Paths | Death Cue |
| --- | --- | --- | --- | --- | --- |
| Runner | `Assets/Prefabs/Creeps/Creep_Runner.prefab` | `Body` | `Accent`, `SpeedLine` | `Damage` | Spark burst |
| Brute | `Assets/Prefabs/Creeps/Creep_Brute.prefab` | `Body` | `Accent/Core` | `Damage/PlateLeft`, `Damage/PlateRight` | Heavy shatter |
| Swarm | `Assets/Prefabs/Creeps/Creep_Swarm.prefab` | `Body` | `Accent/Signal0`, `Accent/Signal1` | `Damage/CrackedShard` | Shard scatter |

## Generated Materials

- `Assets/Art/Creeps/GeneratedMaterials/Creep_Body.mat`
- `Assets/Art/Creeps/GeneratedMaterials/Creep_SenderAccent.mat`
- `Assets/Art/Creeps/GeneratedMaterials/Creep_Damage.mat`
- `Assets/Art/Creeps/GeneratedMaterials/Creep_Shadow.mat`

## Generated Low-Poly Meshes

- `Assets/Art/Creeps/GeneratedMeshes/Runner_Dart.asset`
- `Assets/Art/Creeps/GeneratedMeshes/Runner_Fin.asset`
- `Assets/Art/Creeps/GeneratedMeshes/Brute_ArmoredCore.asset`
- `Assets/Art/Creeps/GeneratedMeshes/Brute_ArmorPlate.asset`
- `Assets/Art/Creeps/GeneratedMeshes/Swarm_Shard.asset`

## Follow-Up

1. Run `Line Wards > Art > Validate Creep Visual Library`.
2. Run the local vertical slice.
3. Send runner, brute, and swarm creeps with `S`, `V`, and `W`.
4. Check phone-size silhouette readability and heavy-send readability.
5. Replace generated placeholder meshes/materials with polished production art after the silhouettes are approved.
";

            var fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ReportPath);
            var directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            System.IO.File.WriteAllText(fullPath, report);
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

        private static Mesh CreateRunnerDartMesh()
        {
            var vertices = new[]
            {
                new Vector3(0f, 0f, -0.62f),
                new Vector3(-0.44f, 0f, 0.28f),
                new Vector3(0.44f, 0f, 0.28f),
                new Vector3(0f, 0.36f, -0.12f),
                new Vector3(0f, 0.08f, 0.62f)
            };
            var triangles = new[]
            {
                0, 3, 1,
                0, 2, 3,
                1, 3, 4,
                3, 2, 4,
                0, 1, 4,
                0, 4, 2,
                1, 2, 4
            };

            return BuildMesh(vertices, triangles);
        }

        private static Mesh CreateWedgePlateMesh()
        {
            var vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.36f, 0f, 0.5f),
                new Vector3(0.36f, 0f, 0.5f),
                new Vector3(-0.28f, 0.22f, -0.32f),
                new Vector3(0.28f, 0.22f, -0.32f),
                new Vector3(-0.18f, 0.14f, 0.38f),
                new Vector3(0.18f, 0.14f, 0.38f)
            };
            var triangles = new[]
            {
                0, 4, 1,
                1, 4, 5,
                2, 3, 6,
                3, 7, 6,
                0, 2, 4,
                2, 6, 4,
                1, 5, 3,
                3, 5, 7,
                4, 6, 5,
                5, 6, 7,
                0, 1, 2,
                1, 3, 2
            };

            return BuildMesh(vertices, triangles);
        }

        private static Mesh CreateOctagonalCoreMesh()
        {
            var vertices = new[]
            {
                new Vector3(0f, 0.5f, 0f),
                new Vector3(0.42f, 0.22f, 0.42f),
                new Vector3(0f, 0.22f, 0.58f),
                new Vector3(-0.42f, 0.22f, 0.42f),
                new Vector3(-0.58f, 0.22f, 0f),
                new Vector3(-0.42f, 0.22f, -0.42f),
                new Vector3(0f, 0.22f, -0.58f),
                new Vector3(0.42f, 0.22f, -0.42f),
                new Vector3(0.58f, 0.22f, 0f),
                new Vector3(0f, -0.38f, 0f)
            };
            var triangles = new[]
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,
                0, 4, 5,
                0, 5, 6,
                0, 6, 7,
                0, 7, 8,
                0, 8, 1,
                9, 2, 1,
                9, 3, 2,
                9, 4, 3,
                9, 5, 4,
                9, 6, 5,
                9, 7, 6,
                9, 8, 7,
                9, 1, 8
            };

            return BuildMesh(vertices, triangles);
        }

        private static Mesh CreateShardMesh()
        {
            var vertices = new[]
            {
                new Vector3(0f, 0.42f, 0f),
                new Vector3(-0.38f, 0f, 0f),
                new Vector3(0f, 0f, 0.42f),
                new Vector3(0.38f, 0f, 0f),
                new Vector3(0f, 0f, -0.42f),
                new Vector3(0f, -0.34f, 0f)
            };
            var triangles = new[]
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,
                0, 4, 1,
                5, 2, 1,
                5, 3, 2,
                5, 4, 3,
                5, 1, 4
            };

            return BuildMesh(vertices, triangles);
        }

        private static Mesh BuildMesh(Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles,
                uv = new Vector2[vertices.Length]
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
