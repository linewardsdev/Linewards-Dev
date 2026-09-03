using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class ArrowTower3DProofGenerator
    {
        private const string RawGeneratedPrefabPath = "Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d.prefab";
        private const string RuntimePrefabPath = "Assets/Prefabs/Towers/Tower_Arrow_3D.prefab";
        private const string LibraryPath = "Assets/Resources/TowerVisualLibrary.asset";
        private const string MaterialFolder = "Assets/Art/Towers/Arrow/Materials";
        private const string GeneratedMaterialPath = "Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d_Assets/Materials/Color_c880fcfa-bfa6-467d-9d72-569a70753096.mat";
        private const string RuntimeMaterialPath = MaterialFolder + "/mat_tower_arrow_3d_runtime_board_v01.mat";
        private const string SourceNotesPath = "Assets/Art/Towers/Arrow/arrow_3d_unity_ai_source_notes.md";
        private static readonly Quaternion RawGeneratedImportRotation = new(-0.5f, 0.5f, 0.5f, 0.5f);

        [MenuItem("Line Wars/Art/Generate Arrow 3D Proof Wrapper")]
        public static void GenerateAndPromote()
        {
            var rawPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RawGeneratedPrefabPath);
            if (rawPrefab == null)
            {
                Debug.LogError($"Missing generated Arrow 3D prefab at {RawGeneratedPrefabPath}.");
                return;
            }

            if (rawPrefab.GetComponentsInChildren<MeshRenderer>(true).Length == 0 ||
                rawPrefab.GetComponentsInChildren<MeshFilter>(true).Length == 0)
            {
                Debug.LogError($"Generated Arrow 3D prefab has no resolvable mesh renderers: {RawGeneratedPrefabPath}. Not promoting.");
                return;
            }

            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/Prefabs/Towers");
            EnsureFolder("Assets/Art/Towers/Arrow");

            var root = new GameObject("Tower_Arrow_3D");
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            CreateEmptyChild(root, "BodyTintAnchor");

            var generatedInstance = (GameObject)PrefabUtility.InstantiatePrefab(rawPrefab);
            generatedInstance.name = "UnityAIGeneratedArrow";
            generatedInstance.transform.SetParent(body.transform, false);
            generatedInstance.transform.localPosition = rawPrefab.transform.localPosition;
            generatedInstance.transform.localRotation = RawGeneratedImportRotation;
            generatedInstance.transform.localScale = rawPrefab.transform.localScale;
            StripColliders(generatedInstance);
            ApplyRuntimeBoardMaterial(generatedInstance);

            CreateEmptyChild(root, "RoleMarker");
            CreateEmptyChild(root, "OwnerTrim");
            CreateEmptyChild(root, "RangeHalo");
            CreateEmptyChild(root, "Lens");
            CreateEmptyChild(root, "Muzzle");
            CreateEmptyChild(root, "BowLeft");
            CreateEmptyChild(root, "BowRight");

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RuntimePrefabPath);
            Object.DestroyImmediate(root);

            UpdateArrowProfile(prefab);
            WriteSourceNotes();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(SourceNotesPath);
            AssetDatabase.Refresh();
            Debug.Log($"Generated Arrow 3D runtime wrapper at {RuntimePrefabPath} and promoted tower.arrow for gameplay review.");
        }

        private static void UpdateArrowProfile(GameObject prefab)
        {
            var library = AssetDatabase.LoadAssetAtPath<TowerVisualLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"Missing tower visual library at {LibraryPath}. Not promoting Arrow 3D wrapper.");
                return;
            }

            var serializedLibrary = new SerializedObject(library);
            var profiles = serializedLibrary.FindProperty("profiles");
            for (var index = 0; index < profiles.arraySize; index++)
            {
                var profile = profiles.GetArrayElementAtIndex(index);
                if (profile.FindPropertyRelative("towerId").stringValue != "tower.arrow")
                {
                    continue;
                }

                profile.FindPropertyRelative("role").enumValueIndex = (int)TowerVisualRole.Arrow;
                profile.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                profile.FindPropertyRelative("scale").vector3Value = new Vector3(1.36f, 1.36f, 1.36f);
                profile.FindPropertyRelative("lift").floatValue = 0.16f;
                profile.FindPropertyRelative("bodyRendererPath").stringValue = "BodyTintAnchor";
                profile.FindPropertyRelative("roleMarkerRendererPath").stringValue = "RoleMarker";
                profile.FindPropertyRelative("ownerTrimRendererPath").stringValue = "OwnerTrim";
                profile.FindPropertyRelative("rangeHaloRendererPath").stringValue = "RangeHalo";
                serializedLibrary.ApplyModifiedProperties();
                EditorUtility.SetDirty(library);
                return;
            }

            Debug.LogError("Tower visual library does not contain a tower.arrow profile. Arrow 3D wrapper was created but not promoted.");
        }

        private static void StripColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                Object.DestroyImmediate(colliders[index]);
            }
        }

        private static void ApplyRuntimeBoardMaterial(GameObject root)
        {
            var material = CreateRuntimeBoardMaterial();
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

        private static Material CreateRuntimeBoardMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(GeneratedMaterialPath);
            var shader = LTW.UnityClient.Simulation.RenderCompat.Lit;
            var material = AssetDatabase.LoadAssetAtPath<Material>(RuntimeMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, RuntimeMaterialPath);
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(RuntimeMaterialPath);
            material.shader = shader;
            material.color = new Color(1.12f, 1.12f, 1.12f, 1f);
            material.mainTexture = source != null ? source.mainTexture : material.mainTexture;
            material.enableInstancing = true;
            material.DisableKeyword("_NORMALMAP");
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_BumpMap", null);
            material.SetFloat("_BumpScale", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.12f);
            material.SetFloat("_GlossyReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetColor("_EmissionColor", new Color(0.035f, 0.045f, 0.055f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateChild(GameObject parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Material material)
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

        private static GameObject CreateEmptyChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(LTW.UnityClient.Simulation.RenderCompat.Lit);
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

        private static void WriteSourceNotes()
        {
            const string notes = @"# Arrow Tower 3D Unity AI Source Notes

Date: 2026-07-18
Status: runtime proof wrapper generated for review

## Source

- Unity AI generated prefab: `Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d.prefab`
- Source drawing: `Assets/Art/AIStaging/SourcePlates/tower_arrow_source_plate_v03.png`
- Runtime proof wrapper: `Assets/Prefabs/Towers/Tower_Arrow_3D.prefab`

## Integration Notes

- The raw Unity AI prefab is preserved as staging evidence.
- The runtime wrapper preserves Line Wars tower contract children: `Body`, `RoleMarker`, `OwnerTrim`, `RangeHalo`.
- `RoleMarker`, `OwnerTrim`, and `RangeHalo` are non-rendering anchors because the generated Arrow model already contains its own readable glow and trim details.
- Arrow-specific anchors are present for review: `Lens`, `Muzzle`, `BowLeft`, `BowRight`.
- `TowerVisualLibrary.asset` points `tower.arrow` at `Tower_Arrow_3D.prefab`.
- The runtime wrapper uses the generated albedo texture with a board-view material: no normal map, no specular highlights, and no shadows.
- `Tower_Arrow_AIPlate.prefab` remains available as rollback if the 3D proof does not beat the sprite in-game.
";

            var fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), SourceNotesPath);
            var directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            System.IO.File.WriteAllText(fullPath, notes);
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
    }
}
