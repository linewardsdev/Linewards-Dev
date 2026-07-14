using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class TowerVisualPrefabGenerator
    {
        private const string MenuPath = "Line Wards/Art/Generate Placeholder Tower Prefabs";
        private const string ValidateMenuPath = "Line Wards/Art/Validate Tower Placeholder Prefabs";
        private const string LibraryPath = "Assets/Resources/TowerVisualLibrary.asset";
        private const string PrefabFolder = "Assets/Prefabs/Towers";
        private const string MaterialFolder = "Assets/Art/Towers/GeneratedMaterials";
        private const string ReportPath = "Assets/Art/Towers/GeneratedPlaceholderReport.md";

        private static readonly TowerSpec[] TowerSpecs =
        {
            new("Arrow", "Tower_Arrow", new Color(0.24f, 0.78f, 1f), new Color(0.95f, 0.82f, 0.34f), TowerShape.Crossbow),
            new("Control", "Tower_Control", new Color(0.55f, 0.5f, 1f), new Color(0.32f, 0.94f, 0.88f), TowerShape.ContainmentDish),
            new("Relay", "Tower_Relay", new Color(0.25f, 0.9f, 0.58f), new Color(1f, 0.72f, 0.3f), TowerShape.SignalMast),
            new("Pulse", "Tower_Pulse", new Color(0.95f, 0.38f, 0.55f), new Color(1f, 0.95f, 0.44f), TowerShape.PulseDrum),
            new("Prism", "Tower_Prism", new Color(0.74f, 0.54f, 1f), new Color(0.4f, 0.94f, 1f), TowerShape.LensSpire),
        };

        [MenuItem(MenuPath)]
        public static void GeneratePlaceholderTowerPrefabs()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            var trimMaterial = CreateOrUpdateMaterial(MaterialFolder + "/Tower_OwnerTrim.mat", new Color(0.98f, 0.76f, 0.24f));
            var haloMaterial = CreateOrUpdateMaterial(MaterialFolder + "/Tower_RangeHalo.mat", new Color(0.36f, 0.72f, 1f, 0.26f));
            var prefabs = new GameObject[TowerSpecs.Length];

            for (var index = 0; index < TowerSpecs.Length; index++)
            {
                var spec = TowerSpecs[index];
                var bodyMaterial = CreateOrUpdateMaterial(MaterialFolder + "/" + spec.PrefabName + "_Body.mat", spec.BodyColor);
                var roleMaterial = CreateOrUpdateMaterial(MaterialFolder + "/" + spec.PrefabName + "_RoleMarker.mat", spec.MarkerColor);
                prefabs[index] = SaveTowerPrefab(spec, bodyMaterial, roleMaterial, trimMaterial, haloMaterial);
            }

            UpdateVisualLibrary(prefabs);
            WriteGenerationReport();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ReportPath);
            AssetDatabase.Refresh();
            Debug.Log($"Generated placeholder tower prefabs, updated TowerVisualLibrary, and wrote {ReportPath}.");
        }

        [MenuItem(ValidateMenuPath)]
        public static void ValidateTowerPlaceholderPrefabs()
        {
            var issueCount = 0;
            foreach (var spec in TowerSpecs)
            {
                var prefabPath = PrefabFolder + "/" + spec.PrefabName + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"Missing tower placeholder prefab at {prefabPath}.");
                    issueCount++;
                    continue;
                }

                issueCount += ValidateRendererPath(prefab, "Body");
                issueCount += ValidateRendererPath(prefab, "RoleMarker");
                issueCount += ValidateRendererPath(prefab, "OwnerTrim");
                issueCount += ValidateRendererPath(prefab, "RangeHalo");
            }

            var library = AssetDatabase.LoadAssetAtPath<TowerVisualLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"Missing tower visual library at {LibraryPath}.");
                issueCount++;
            }
            else
            {
                foreach (var profile in library.Profiles)
                {
                    if (profile == null)
                    {
                        Debug.LogError("Tower visual library contains a null profile entry.", library);
                        issueCount++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(profile.TowerId))
                    {
                        Debug.LogError("Tower visual profile is missing a tower id.", library);
                        issueCount++;
                    }

                    if (profile.Prefab == null)
                    {
                        Debug.LogError($"Tower visual profile '{profile.TowerId}' has no prefab assigned.", library);
                        issueCount++;
                    }
                }
            }

            if (issueCount == 0)
            {
                Debug.Log("Tower placeholder prefab validation passed.");
            }
            else
            {
                Debug.LogWarning($"Tower placeholder prefab validation completed with {issueCount} issue(s).");
            }
        }

        private static GameObject SaveTowerPrefab(
            TowerSpec spec,
            Material bodyMaterial,
            Material roleMaterial,
            Material trimMaterial,
            Material haloMaterial)
        {
            var root = new GameObject(spec.PrefabName);

            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0f), new Vector3(1.6f, 0.012f, 1.6f), haloMaterial);
            CreateChild(root, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), spec.BaseScale, bodyMaterial);
            CreateChild(root, "Body", spec.BodyPrimitive, spec.BodyPosition, spec.BodyScale, bodyMaterial);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, spec.TrimPosition, spec.TrimScale, trimMaterial);

            switch (spec.Shape)
            {
                case TowerShape.Crossbow:
                    CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.66f, 0.12f), new Vector3(0.18f, 0.09f, 0.82f), roleMaterial);
                    CreateChild(root, "BoltRail", PrimitiveType.Cube, new Vector3(0f, 0.58f, 0.18f), new Vector3(0.22f, 0.08f, 1.02f), bodyMaterial);
                    CreateChild(root, "BowLeft", PrimitiveType.Cube, new Vector3(-0.36f, 0.66f, 0.03f), new Vector3(0.48f, 0.08f, 0.12f), roleMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
                    CreateChild(root, "BowRight", PrimitiveType.Cube, new Vector3(0.36f, 0.66f, 0.03f), new Vector3(0.48f, 0.08f, 0.12f), roleMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, 16f);
                    CreateChild(root, "BowTipLeft", PrimitiveType.Cube, new Vector3(-0.67f, 0.62f, -0.03f), new Vector3(0.12f, 0.08f, 0.2f), roleMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
                    CreateChild(root, "BowTipRight", PrimitiveType.Cube, new Vector3(0.67f, 0.62f, -0.03f), new Vector3(0.12f, 0.08f, 0.2f), roleMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, -24f);
                    CreateChild(root, "BowString", PrimitiveType.Cube, new Vector3(0f, 0.64f, -0.14f), new Vector3(1.22f, 0.028f, 0.032f), trimMaterial);
                    CreateChild(root, "DrawCord", PrimitiveType.Cube, new Vector3(0f, 0.64f, 0.1f), new Vector3(0.04f, 0.032f, 0.48f), trimMaterial);
                    CreateChild(root, "ArrowShaft", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0.34f), new Vector3(0.055f, 0.055f, 0.62f), roleMaterial);
                    CreateChild(root, "ArrowHead", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0.72f), new Vector3(0.2f, 0.12f, 0.2f), roleMaterial).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    break;
                case TowerShape.ContainmentDish:
                    CreateChild(root, "RoleMarker", PrimitiveType.Cylinder, new Vector3(0f, 0.58f, 0f), new Vector3(1.08f, 0.038f, 1.08f), roleMaterial);
                    CreateChild(root, "ControlRing", PrimitiveType.Cylinder, new Vector3(0f, 0.73f, 0f), new Vector3(0.88f, 0.034f, 0.88f), roleMaterial);
                    CreateChild(root, "ControlHalo", PrimitiveType.Cylinder, new Vector3(0f, 0.86f, 0f), new Vector3(0.62f, 0.026f, 0.62f), roleMaterial);
                    CreateChild(root, "ControlCore", PrimitiveType.Sphere, new Vector3(0f, 0.73f, 0f), new Vector3(0.28f, 0.28f, 0.28f), roleMaterial);
                    CreateChild(root, "ContainmentBarNorth", PrimitiveType.Cube, new Vector3(0f, 0.66f, 0.54f), new Vector3(0.78f, 0.07f, 0.08f), trimMaterial);
                    CreateChild(root, "ContainmentBarSouth", PrimitiveType.Cube, new Vector3(0f, 0.66f, -0.54f), new Vector3(0.78f, 0.07f, 0.08f), trimMaterial);
                    CreateChild(root, "ClampNorth", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0.62f), new Vector3(0.22f, 0.16f, 0.24f), trimMaterial);
                    CreateChild(root, "ClampSouth", PrimitiveType.Cube, new Vector3(0f, 0.72f, -0.62f), new Vector3(0.22f, 0.16f, 0.24f), trimMaterial);
                    CreateChild(root, "ClampEast", PrimitiveType.Cube, new Vector3(0.62f, 0.72f, 0f), new Vector3(0.24f, 0.16f, 0.22f), trimMaterial);
                    CreateChild(root, "ClampWest", PrimitiveType.Cube, new Vector3(-0.62f, 0.72f, 0f), new Vector3(0.24f, 0.16f, 0.22f), trimMaterial);
                    break;
                case TowerShape.SignalMast:
                    CreateChild(root, "RoleMarker", PrimitiveType.Sphere, new Vector3(0f, 1.16f, 0f), new Vector3(0.26f, 0.26f, 0.26f), roleMaterial);
                    CreateChild(root, "RelayMast", PrimitiveType.Cube, new Vector3(0f, 0.78f, 0f), new Vector3(0.1f, 0.82f, 0.1f), bodyMaterial);
                    CreateChild(root, "RelayCore", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.42f, 0.11f, 0.42f), roleMaterial);
                    CreateChild(root, "RelaySignal", PrimitiveType.Cylinder, new Vector3(0f, 1.0f, -0.14f), new Vector3(0.54f, 0.03f, 0.54f), roleMaterial).transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
                    CreateChild(root, "BeaconDish", PrimitiveType.Cylinder, new Vector3(0f, 1.0f, 0.16f), new Vector3(0.34f, 0.028f, 0.34f), trimMaterial).transform.localRotation = Quaternion.Euler(-58f, 0f, 0f);
                    CreateChild(root, "SignalBridge", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0f), new Vector3(0.56f, 0.06f, 0.08f), trimMaterial);
                    CreateChild(root, "CapacitorLeft", PrimitiveType.Cube, new Vector3(-0.32f, 0.62f, 0f), new Vector3(0.12f, 0.48f, 0.12f), trimMaterial);
                    CreateChild(root, "CapacitorRight", PrimitiveType.Cube, new Vector3(0.32f, 0.62f, 0f), new Vector3(0.12f, 0.48f, 0.12f), trimMaterial);
                    CreateChild(root, "SignalRodLeft", PrimitiveType.Cube, new Vector3(-0.44f, 0.88f, 0f), new Vector3(0.065f, 0.56f, 0.065f), roleMaterial);
                    CreateChild(root, "SignalRodRight", PrimitiveType.Cube, new Vector3(0.44f, 0.88f, 0f), new Vector3(0.065f, 0.56f, 0.065f), roleMaterial);
                    break;
                case TowerShape.PulseDrum:
                    CreateChild(root, "RoleMarker", PrimitiveType.Cylinder, new Vector3(0f, 0.58f, 0f), new Vector3(0.82f, 0.075f, 0.82f), roleMaterial);
                    CreateChild(root, "PulseCore", PrimitiveType.Sphere, new Vector3(0f, 0.62f, 0f), new Vector3(0.34f, 0.34f, 0.34f), roleMaterial);
                    CreateChild(root, "PulseRingA", PrimitiveType.Cylinder, new Vector3(0f, 0.76f, 0f), new Vector3(0.94f, 0.045f, 0.94f), roleMaterial);
                    CreateChild(root, "PulseRingB", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(0.72f, 0.045f, 0.72f), trimMaterial);
                    CreateChild(root, "PulseRingC", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.56f, 0.035f, 0.56f), trimMaterial);
                    CreateChild(root, "PulseEmitter", PrimitiveType.Cube, new Vector3(0f, 0.62f, 0.42f), new Vector3(0.26f, 0.18f, 0.22f), roleMaterial);
                    CreateChild(root, "PulseDiaphragm", PrimitiveType.Cylinder, new Vector3(0f, 0.62f, 0.58f), new Vector3(0.34f, 0.035f, 0.34f), roleMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    CreateChild(root, "VentLeft", PrimitiveType.Cube, new Vector3(-0.48f, 0.54f, 0f), new Vector3(0.12f, 0.2f, 0.42f), trimMaterial);
                    CreateChild(root, "VentRight", PrimitiveType.Cube, new Vector3(0.48f, 0.54f, 0f), new Vector3(0.12f, 0.2f, 0.42f), trimMaterial);
                    break;
                case TowerShape.LensSpire:
                    CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(0.34f, 0.98f, 0.34f), roleMaterial).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    CreateChild(root, "PrismSpire", PrimitiveType.Cube, new Vector3(0f, 0.84f, 0f), new Vector3(0.22f, 0.86f, 0.22f), bodyMaterial).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    CreateChild(root, "PrismTip", PrimitiveType.Cube, new Vector3(0f, 1.48f, 0f), new Vector3(0.18f, 0.26f, 0.18f), roleMaterial).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    CreateChild(root, "PrismLens", PrimitiveType.Sphere, new Vector3(0f, 0.9f, 0.38f), new Vector3(0.26f, 0.26f, 0.16f), roleMaterial);
                    CreateChild(root, "PrismAperture", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0.54f), new Vector3(0.3f, 0.035f, 0.3f), trimMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    CreateChild(root, "PrismBeamHint", PrimitiveType.Cube, new Vector3(0f, 0.9f, 0.74f), new Vector3(0.07f, 0.07f, 0.62f), roleMaterial);
                    CreateChild(root, "FocusForkLeft", PrimitiveType.Cube, new Vector3(-0.22f, 0.94f, 0.22f), new Vector3(0.08f, 0.48f, 0.12f), trimMaterial).transform.localRotation = Quaternion.Euler(0f, 24f, -18f);
                    CreateChild(root, "FocusForkRight", PrimitiveType.Cube, new Vector3(0.22f, 0.94f, 0.22f), new Vector3(0.08f, 0.48f, 0.12f), trimMaterial).transform.localRotation = Quaternion.Euler(0f, -24f, 18f);
                    CreateChild(root, "FacetLeft", PrimitiveType.Cube, new Vector3(-0.2f, 0.62f, -0.02f), new Vector3(0.08f, 0.46f, 0.08f), trimMaterial).transform.localRotation = Quaternion.Euler(0f, 45f, -14f);
                    CreateChild(root, "FacetRight", PrimitiveType.Cube, new Vector3(0.2f, 0.62f, -0.02f), new Vector3(0.08f, 0.46f, 0.08f), trimMaterial).transform.localRotation = Quaternion.Euler(0f, 45f, 14f);
                    break;
            }

            var prefabPath = PrefabFolder + "/" + spec.PrefabName + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void UpdateVisualLibrary(GameObject[] prefabs)
        {
            var library = AssetDatabase.LoadAssetAtPath<TowerVisualLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<TowerVisualLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var serializedLibrary = new SerializedObject(library);
            var profiles = serializedLibrary.FindProperty("profiles");
            profiles.arraySize = TowerSpecs.Length;

            for (var index = 0; index < TowerSpecs.Length; index++)
            {
                ConfigureProfile(profiles.GetArrayElementAtIndex(index), TowerSpecs[index], prefabs[index]);
            }

            serializedLibrary.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
        }

        private static void ConfigureProfile(SerializedProperty profile, TowerSpec spec, GameObject prefab)
        {
            profile.FindPropertyRelative("towerId").stringValue = spec.TowerId;
            profile.FindPropertyRelative("role").enumValueIndex = (int)spec.Role;
            profile.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            profile.FindPropertyRelative("scale").vector3Value = spec.RuntimeScale;
            profile.FindPropertyRelative("lift").floatValue = spec.RuntimeLift;
            profile.FindPropertyRelative("bodyRendererPath").stringValue = "Body";
            profile.FindPropertyRelative("roleMarkerRendererPath").stringValue = "RoleMarker";
            profile.FindPropertyRelative("ownerTrimRendererPath").stringValue = "OwnerTrim";
            profile.FindPropertyRelative("rangeHaloRendererPath").stringValue = "RangeHalo";
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

        private static int ValidateRendererPath(GameObject prefab, string path)
        {
            var target = prefab.transform.Find(path);
            if (target == null)
            {
                Debug.LogError($"Tower placeholder prefab '{prefab.name}' is missing required child '{path}'.", prefab);
                return 1;
            }

            if (target.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                Debug.LogError($"Tower placeholder prefab '{prefab.name}' child '{path}' has no renderer.", prefab);
                return 1;
            }

            return 0;
        }

        private static void WriteGenerationReport()
        {
            var report = @"# Generated Tower Placeholder Prefabs

This report is generated by `Line Wards > Art > Generate Placeholder Tower Prefabs`.

## Generated Prefabs

| Tower | Runtime ID | Prefab | Required Children | Readability Target |
| --- | --- | --- | --- | --- |
| Arrow | `tower.arrow` | `Assets/Prefabs/Towers/Tower_Arrow.prefab` | `Body`, `RoleMarker`, `OwnerTrim`, `RangeHalo` | Low horizontal crossbow: wide limbs, string, rail, shaft, and forward arrowhead |
| Control | `tower.control` | `Assets/Prefabs/Towers/Tower_Control.prefab` | `Body`, `RoleMarker`, `OwnerTrim`, `RangeHalo` | Wide flat containment dish, double control halo, restraint bands, clamps, and suspended core |
| Relay | `tower.relay` | `Assets/Prefabs/Towers/Tower_Relay.prefab` | `Body`, `RoleMarker`, `OwnerTrim`, `RangeHalo` | Support beacon mast with raised signal node, upward dish, twin rods, and side capacitors |
| Pulse | `tower.pulse` | `Assets/Prefabs/Towers/Tower_Pulse.prefab` | `Body`, `RoleMarker`, `OwnerTrim`, `RangeHalo` | Heavy pressure-reactor drum with thick nested shock rings, side vents, and front burst diaphragm |
| Prism | `tower.prism` | `Assets/Prefabs/Towers/Tower_Prism.prefab` | `Body`, `RoleMarker`, `OwnerTrim`, `RangeHalo` | Tall faceted lens-spire with crystal tip, split focus forks, forward aperture, and beam hint |

## Generated Library

- `Assets/Resources/TowerVisualLibrary.asset`

## Generated Materials

- `Assets/Art/Towers/GeneratedMaterials/Tower_OwnerTrim.mat`
- `Assets/Art/Towers/GeneratedMaterials/Tower_RangeHalo.mat`
- `Assets/Art/Towers/GeneratedMaterials/Tower_Arrow_Body.mat`
- `Assets/Art/Towers/GeneratedMaterials/Tower_Control_Body.mat`
- `Assets/Art/Towers/GeneratedMaterials/Tower_Relay_Body.mat`
- `Assets/Art/Towers/GeneratedMaterials/Tower_Pulse_Body.mat`
- `Assets/Art/Towers/GeneratedMaterials/Tower_Prism_Body.mat`
- one role marker material per tower role

## Follow-Up

1. Run `Line Wards > Art > Validate Tower Placeholder Prefabs`.
2. Place each tower in a phone-size capture and check silhouette readability at gameplay zoom.
3. Keep the required child names stable when replacing placeholder primitives with polished art.
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
            AssetDatabase.CreateFolder(parent, folder);
        }

        private readonly struct TowerSpec
        {
            public TowerSpec(string displayName, string prefabName, Color bodyColor, Color markerColor, TowerShape shape)
            {
                DisplayName = displayName;
                PrefabName = prefabName;
                BodyColor = bodyColor;
                MarkerColor = markerColor;
                Shape = shape;
            }

            public string DisplayName { get; }

            public string PrefabName { get; }

            public string TowerId => DisplayName switch
            {
                "Arrow" => "tower.arrow",
                "Control" => "tower.control",
                "Relay" => "tower.relay",
                "Pulse" => "tower.pulse",
                "Prism" => "tower.prism",
                _ => string.Empty
            };

            public TowerVisualRole Role => DisplayName switch
            {
                "Control" => TowerVisualRole.Control,
                "Relay" => TowerVisualRole.Relay,
                "Pulse" => TowerVisualRole.Pulse,
                "Prism" => TowerVisualRole.Prism,
                _ => TowerVisualRole.Arrow
            };

            public Color BodyColor { get; }

            public Color MarkerColor { get; }

            public TowerShape Shape { get; }

            public PrimitiveType BodyPrimitive => DisplayName == "Prism" ? PrimitiveType.Cube : PrimitiveType.Cylinder;

            public Vector3 BaseScale => DisplayName switch
            {
                "Arrow" => new Vector3(0.66f, 0.1f, 0.58f),
                "Control" => new Vector3(0.78f, 0.1f, 0.78f),
                "Pulse" => new Vector3(0.76f, 0.11f, 0.76f),
                "Relay" => new Vector3(0.56f, 0.11f, 0.56f),
                "Prism" => new Vector3(0.46f, 0.1f, 0.46f),
                _ => new Vector3(0.56f, 0.12f, 0.56f)
            };

            public Vector3 BodyPosition => DisplayName switch
            {
                "Arrow" => new Vector3(0f, 0.26f, -0.05f),
                "Control" => new Vector3(0f, 0.24f, 0f),
                "Relay" => new Vector3(0f, 0.28f, 0f),
                "Pulse" => new Vector3(0f, 0.25f, 0f),
                "Prism" => new Vector3(0f, 0.36f, 0f),
                _ => new Vector3(0f, 0.26f, 0f)
            };

            public Vector3 BodyScale => DisplayName switch
            {
                "Arrow" => new Vector3(0.42f, 0.32f, 0.34f),
                "Control" => new Vector3(0.58f, 0.24f, 0.58f),
                "Relay" => new Vector3(0.36f, 0.42f, 0.36f),
                "Pulse" => new Vector3(0.62f, 0.32f, 0.62f),
                "Prism" => new Vector3(0.3f, 0.62f, 0.3f),
                _ => new Vector3(0.38f, 0.44f, 0.38f)
            };

            public Vector3 TrimPosition => DisplayName switch
            {
                "Arrow" => new Vector3(0f, 0.42f, -0.05f),
                "Control" => new Vector3(0f, 0.4f, 0f),
                "Relay" => new Vector3(0f, 0.48f, 0f),
                "Pulse" => new Vector3(0f, 0.41f, 0f),
                "Prism" => new Vector3(0f, 0.62f, 0f),
                _ => new Vector3(0f, 0.5f, 0f)
            };

            public Vector3 TrimScale => DisplayName switch
            {
                "Control" => new Vector3(0.68f, 0.032f, 0.68f),
                "Pulse" => new Vector3(0.72f, 0.034f, 0.72f),
                "Relay" => new Vector3(0.48f, 0.032f, 0.48f),
                "Prism" => new Vector3(0.42f, 0.032f, 0.42f),
                _ => new Vector3(0.46f, 0.035f, 0.46f)
            };

            public Vector3 RuntimeScale => DisplayName switch
            {
                "Control" => new Vector3(1.05f, 1.0f, 1.05f),
                "Relay" => new Vector3(1.0f, 1.16f, 1.0f),
                "Pulse" => new Vector3(1.08f, 1.02f, 1.08f),
                "Prism" => new Vector3(1.0f, 1.22f, 1.0f),
                _ => new Vector3(1.02f, 1.16f, 1.02f)
            };

            public float RuntimeLift => DisplayName == "Relay" ? 0.16f : 0.12f;
        }

        private enum TowerShape
        {
            Crossbow,
            ContainmentDish,
            SignalMast,
            PulseDrum,
            LensSpire
        }
    }
}
