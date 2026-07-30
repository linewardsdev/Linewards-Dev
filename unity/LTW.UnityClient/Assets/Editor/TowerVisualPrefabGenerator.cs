using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class TowerVisualPrefabGenerator
    {
        private const string MenuPath = "Line Wards/Art/Generate Placeholder Tower Prefabs";
        private const string ValidateMenuPath = "Line Wards/Art/Validate Tower Placeholder Prefabs";
        private const string GenerateAuthoredArrowMenuPath = "Line Wards/Art/Generate Authored Arrow Tower";
        private const string GenerateWardPrototypeMenuPath = "Line Wards/Art/Generate Ward Prototype Tower Wrappers";
        private const string LibraryPath = "Assets/Resources/TowerVisualLibrary.asset";
        private const string PrefabFolder = "Assets/Prefabs/Towers";
        private const string MaterialFolder = "Assets/Art/Towers/GeneratedMaterials";
        private const string AuthoredArrowMaterialFolder = "Assets/Art/Towers/Arrow/Materials";
        private const string WardPrototypeMaterialFolder = "Assets/Art/Towers/WardPrototype/Materials";
        private const string ReportPath = "Assets/Art/Towers/GeneratedPlaceholderReport.md";
        private const string WardPrototypeReportPath = "Assets/Art/Towers/WardPrototype/Agent1WardWrapperReport.md";

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

        [MenuItem(GenerateAuthoredArrowMenuPath)]
        public static void GenerateAuthoredArrowTower()
        {
            EnsureFolder(AuthoredArrowMaterialFolder);
            EnsureFolder(PrefabFolder);

            var bodyMaterial = CreateOrUpdateMaterial(AuthoredArrowMaterialFolder + "/mat_role_tower_arrow_body_v01.mat", new Color(0.12f, 0.32f, 0.42f));
            var energyMaterial = CreateOrUpdateMaterial(AuthoredArrowMaterialFolder + "/mat_role_tower_arrow_energy_v01.mat", new Color(0.42f, 0.96f, 1f));
            var trimMaterial = CreateOrUpdateMaterial(AuthoredArrowMaterialFolder + "/mat_role_tower_arrow_trim_v01.mat", new Color(0.96f, 0.78f, 0.24f));
            var darkMaterial = CreateOrUpdateMaterial(AuthoredArrowMaterialFolder + "/mat_role_tower_arrow_dark_v01.mat", new Color(0.04f, 0.09f, 0.12f));
            var haloMaterial = CreateOrUpdateMaterial(AuthoredArrowMaterialFolder + "/mat_role_tower_arrow_range_v01.mat", new Color(0.24f, 0.62f, 1f, 0.22f));

            var prefab = SaveAuthoredArrowPrefab(bodyMaterial, energyMaterial, trimMaterial, darkMaterial, haloMaterial);
            UpdateSingleVisualProfile(new TowerSpec("Arrow", "Tower_Arrow", new Color(0.24f, 0.78f, 1f), new Color(0.95f, 0.82f, 0.34f), TowerShape.Crossbow), prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated authored Arrow tower prefab and updated TowerVisualLibrary profile.");
        }

        [MenuItem(GenerateWardPrototypeMenuPath)]
        public static void GenerateWardPrototypeTowerWrappers()
        {
            EnsureFolder(WardPrototypeMaterialFolder);
            EnsureFolder(PrefabFolder);

            var baseMaterial = CreateOrUpdateMaterial(WardPrototypeMaterialFolder + "/mat_ltw_ward_body_v01.mat", new Color(0.1f, 0.25f, 0.32f));
            var darkMaterial = CreateOrUpdateMaterial(WardPrototypeMaterialFolder + "/mat_ltw_ward_dark_v01.mat", new Color(0.035f, 0.07f, 0.095f));
            var energyMaterial = CreateOrUpdateMaterial(WardPrototypeMaterialFolder + "/mat_ltw_ward_energy_v01.mat", new Color(0.38f, 0.94f, 1f));
            var controlMaterial = CreateOrUpdateMaterial(WardPrototypeMaterialFolder + "/mat_ltw_ward_control_v01.mat", new Color(0.58f, 0.52f, 1f));
            var relayMaterial = CreateOrUpdateMaterial(WardPrototypeMaterialFolder + "/mat_ltw_ward_relay_v01.mat", new Color(0.36f, 0.96f, 0.64f));
            var trimMaterial = CreateOrUpdateMaterial(WardPrototypeMaterialFolder + "/mat_ltw_ward_owner_trim_v01.mat", new Color(0.96f, 0.78f, 0.24f));
            var haloMaterial = CreateOrUpdateMaterial(WardPrototypeMaterialFolder + "/mat_ltw_ward_range_v01.mat", new Color(0.24f, 0.62f, 1f, 0.22f));

            var arrowPrefab = SaveWardArrowWrapper(baseMaterial, energyMaterial, trimMaterial, darkMaterial, haloMaterial);
            var relayPrefab = SaveWardRelayWrapper(baseMaterial, relayMaterial, trimMaterial, darkMaterial, haloMaterial);
            var controlPrefab = SaveWardControlWrapper(baseMaterial, controlMaterial, trimMaterial, darkMaterial, haloMaterial);

            UpdateSingleVisualProfile(new TowerSpec("Arrow", "Tower_Arrow", new Color(0.24f, 0.78f, 1f), new Color(0.95f, 0.82f, 0.34f), TowerShape.Crossbow), arrowPrefab);
            UpdateSingleVisualProfile(new TowerSpec("Relay", "Tower_Relay", new Color(0.25f, 0.9f, 0.58f), new Color(1f, 0.72f, 0.3f), TowerShape.SignalMast), relayPrefab);
            UpdateSingleVisualProfile(new TowerSpec("Control", "Tower_Control", new Color(0.55f, 0.5f, 1f), new Color(0.32f, 0.94f, 0.88f), TowerShape.ContainmentDish), controlPrefab);
            WriteWardWrapperReport();

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(WardPrototypeReportPath);
            AssetDatabase.Refresh();
            Debug.Log("Generated Ward Prototype tower wrappers for Arrow, Relay, and Control.");
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

                if (spec.DisplayName == "Arrow")
                {
                    issueCount += ValidateRendererPath(prefab, "BowLeft");
                    issueCount += ValidateRendererPath(prefab, "BowRight");
                    issueCount += ValidateRendererPath(prefab, "Lens");
                    issueCount += ValidateRendererPath(prefab, "Muzzle");
                }
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

        private static GameObject SaveAuthoredArrowPrefab(
            Material bodyMaterial,
            Material energyMaterial,
            Material trimMaterial,
            Material darkMaterial,
            Material haloMaterial)
        {
            var root = new GameObject("Tower_Arrow");

            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.045f, 0f), new Vector3(1.72f, 0.01f, 1.72f), haloMaterial);
            CreateChild(root, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, -0.02f), new Vector3(0.72f, 0.085f, 0.6f), darkMaterial);
            CreateChild(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, -0.04f), new Vector3(0.52f, 0.24f, 0.42f), bodyMaterial);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.35f, -0.04f), new Vector3(0.54f, 0.028f, 0.44f), trimMaterial);

            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.16f), new Vector3(0.14f, 0.08f, 1.12f), energyMaterial);
            CreateChild(root, "BoltRail", PrimitiveType.Cube, new Vector3(0f, 0.47f, 0.14f), new Vector3(0.24f, 0.08f, 1.16f), darkMaterial);
            CreateChild(root, "RailGlow", PrimitiveType.Cube, new Vector3(0f, 0.575f, 0.32f), new Vector3(0.07f, 0.035f, 0.72f), energyMaterial);
            CreateChild(root, "ArrowShaft", PrimitiveType.Cube, new Vector3(0f, 0.63f, 0.4f), new Vector3(0.045f, 0.045f, 0.76f), energyMaterial);
            CreateChild(root, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.64f, 0.86f), new Vector3(0.16f, 0.16f, 0.16f), energyMaterial);
            CreateChild(root, "ArrowHead", PrimitiveType.Cube, new Vector3(0f, 0.64f, 0.91f), new Vector3(0.2f, 0.12f, 0.2f), energyMaterial).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            CreateChild(root, "BowLeft", PrimitiveType.Cube, new Vector3(-0.5f, 0.55f, 0.05f), new Vector3(0.72f, 0.08f, 0.14f), energyMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            CreateChild(root, "BowRight", PrimitiveType.Cube, new Vector3(0.5f, 0.55f, 0.05f), new Vector3(0.72f, 0.08f, 0.14f), energyMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            CreateChild(root, "BowTipLeft", PrimitiveType.Cube, new Vector3(-0.88f, 0.48f, -0.08f), new Vector3(0.16f, 0.08f, 0.28f), trimMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            CreateChild(root, "BowTipRight", PrimitiveType.Cube, new Vector3(0.88f, 0.48f, -0.08f), new Vector3(0.16f, 0.08f, 0.28f), trimMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            CreateChild(root, "BowString", PrimitiveType.Cube, new Vector3(0f, 0.5f, -0.22f), new Vector3(1.58f, 0.022f, 0.028f), trimMaterial);
            CreateChild(root, "DrawCord", PrimitiveType.Cube, new Vector3(0f, 0.53f, 0.02f), new Vector3(0.035f, 0.028f, 0.52f), trimMaterial);

            CreateChild(root, "Lens", PrimitiveType.Sphere, new Vector3(0f, 0.7f, 0.02f), new Vector3(0.24f, 0.2f, 0.24f), energyMaterial);
            CreateChild(root, "LensFrame", PrimitiveType.Cylinder, new Vector3(0f, 0.7f, 0.02f), new Vector3(0.34f, 0.032f, 0.34f), trimMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateChild(root, "LeftAnchor", PrimitiveType.Cube, new Vector3(-0.34f, 0.42f, -0.04f), new Vector3(0.12f, 0.2f, 0.18f), darkMaterial);
            CreateChild(root, "RightAnchor", PrimitiveType.Cube, new Vector3(0.34f, 0.42f, -0.04f), new Vector3(0.12f, 0.2f, 0.18f), darkMaterial);

            var prefabPath = PrefabFolder + "/Tower_Arrow.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveWardArrowWrapper(
            Material bodyMaterial,
            Material energyMaterial,
            Material trimMaterial,
            Material darkMaterial,
            Material haloMaterial)
        {
            var root = new GameObject("Tower_Arrow");

            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.045f, 0f), new Vector3(1.72f, 0.01f, 1.72f), haloMaterial);
            CreateChild(root, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.018f, -0.05f), new Vector3(0.48f, 0.045f, 0.38f), darkMaterial);
            CreateChild(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.085f, -0.1f), new Vector3(0.22f, 0.08f, 0.18f), bodyMaterial);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.155f, -0.1f), new Vector3(0.26f, 0.016f, 0.2f), trimMaterial);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.62f, 0.28f), new Vector3(0.09f, 0.055f, 1.32f), energyMaterial);
            CreateChild(root, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.75f, 1.1f), new Vector3(0.22f, 0.22f, 0.22f), energyMaterial);
            CreateChild(root, "Lens", PrimitiveType.Sphere, new Vector3(0f, 0.76f, -0.1f), new Vector3(0.3f, 0.24f, 0.3f), energyMaterial);
            CreateChild(root, "BowLeft", PrimitiveType.Cube, new Vector3(-0.62f, 0.62f, 0.08f), new Vector3(0.74f, 0.065f, 0.11f), energyMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            CreateChild(root, "BowRight", PrimitiveType.Cube, new Vector3(0.62f, 0.62f, 0.08f), new Vector3(0.74f, 0.065f, 0.11f), energyMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
            AddSourceKitPrefabChild(
                root,
                "ArrowRailVisual",
                "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Musket/_Prefabs_Musket/Musket1_2_1.prefab",
                new Vector3(0f, 0.72f, 0.22f),
                new Vector3(0.72f, 0.72f, 0.72f),
                Quaternion.Euler(0f, 90f, 0f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/Tower_Arrow.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveWardRelayWrapper(
            Material bodyMaterial,
            Material relayMaterial,
            Material trimMaterial,
            Material darkMaterial,
            Material haloMaterial)
        {
            var root = new GameObject("Tower_Relay");

            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.045f, 0f), new Vector3(1.58f, 0.01f, 1.58f), haloMaterial);
            CreateChild(root, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.56f, 0.11f, 0.56f), darkMaterial);
            CreateChild(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.24f, 0f), new Vector3(0.42f, 0.28f, 0.42f), bodyMaterial);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.43f, 0f), new Vector3(0.48f, 0.026f, 0.48f), trimMaterial);
            CreateChild(root, "RoleMarker", PrimitiveType.Sphere, new Vector3(0f, 1.2f, 0f), new Vector3(0.24f, 0.24f, 0.24f), relayMaterial);
            CreateChild(root, "RelayMast", PrimitiveType.Cube, new Vector3(0f, 0.78f, 0f), new Vector3(0.08f, 0.86f, 0.08f), bodyMaterial);
            CreateChild(root, "RelayCore", PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), new Vector3(0.4f, 0.09f, 0.4f), relayMaterial);
            CreateChild(root, "RelaySignal", PrimitiveType.Cylinder, new Vector3(0f, 1.02f, -0.14f), new Vector3(0.5f, 0.026f, 0.5f), relayMaterial).transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
            CreateChild(root, "CapacitorLeft", PrimitiveType.Cube, new Vector3(-0.34f, 0.62f, 0f), new Vector3(0.11f, 0.48f, 0.11f), trimMaterial);
            CreateChild(root, "CapacitorRight", PrimitiveType.Cube, new Vector3(0.34f, 0.62f, 0f), new Vector3(0.11f, 0.48f, 0.11f), trimMaterial);
            AddSourceKitPrefabChild(
                root,
                "RelayMastVisual",
                "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Staves/_PrefabsStaves/Staff5_1_1.prefab",
                new Vector3(0f, 0.85f, 0.02f),
                new Vector3(0.9f, 0.9f, 0.9f),
                Quaternion.Euler(0f, 0f, 0f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/Tower_Relay.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveWardControlWrapper(
            Material bodyMaterial,
            Material controlMaterial,
            Material trimMaterial,
            Material darkMaterial,
            Material haloMaterial)
        {
            var root = new GameObject("Tower_Control");

            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.045f, 0f), new Vector3(1.78f, 0.01f, 1.78f), haloMaterial);
            CreateChild(root, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.78f, 0.1f, 0.78f), darkMaterial);
            CreateChild(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.58f, 0.24f, 0.58f), bodyMaterial);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.64f, 0.026f, 0.64f), trimMaterial);
            CreateChild(root, "RoleMarker", PrimitiveType.Cylinder, new Vector3(0f, 0.62f, 0f), new Vector3(1.02f, 0.034f, 1.02f), controlMaterial);
            CreateChild(root, "ControlRing", PrimitiveType.Cylinder, new Vector3(0f, 0.77f, 0f), new Vector3(0.84f, 0.032f, 0.84f), controlMaterial);
            CreateChild(root, "ControlCore", PrimitiveType.Sphere, new Vector3(0f, 0.77f, 0f), new Vector3(0.26f, 0.26f, 0.26f), controlMaterial);
            CreateChild(root, "PulseEmitter", PrimitiveType.Cube, new Vector3(0f, 0.7f, 0.54f), new Vector3(0.22f, 0.14f, 0.18f), controlMaterial);
            AddSourceKitPrefabChild(
                root,
                "ControlDishVisual",
                "Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Shields/_PrefabsShields/Shield2_1_2.prefab",
                new Vector3(0f, 0.66f, 0f),
                new Vector3(1.08f, 1.08f, 1.08f),
                Quaternion.Euler(70f, 0f, 0f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/Tower_Control.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AddSourceKitPrefabChild(
            GameObject parent,
            string childName,
            string assetPath,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (source == null)
            {
                Debug.LogWarning($"Missing source kit prefab at {assetPath}; skipping '{childName}'.");
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(source);
            }

            instance.name = childName;
            instance.transform.SetParent(parent.transform, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;
            RemoveColliders(instance);
        }

        private static void RemoveColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                Object.DestroyImmediate(colliders[index]);
            }
        }

        private static void UpdateVisualLibrary(GameObject[] prefabs)
        {
            // Delegates to the non-destructive find-or-append below instead of truncating the array to
            // TowerSpecs.Length. The shipped library carries 15 profiles (10 authored via
            // Tower3DImportPipeline since this generator predates them), and this method only knows how
            // to build the original 5 — truncating here used to wipe the other 10 on a single menu click.
            for (var index = 0; index < TowerSpecs.Length; index++)
            {
                UpdateSingleVisualProfile(TowerSpecs[index], prefabs[index]);
            }
        }

        private static void UpdateSingleVisualProfile(TowerSpec spec, GameObject prefab)
        {
            var library = AssetDatabase.LoadAssetAtPath<TowerVisualLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<TowerVisualLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var serializedLibrary = new SerializedObject(library);
            var profiles = serializedLibrary.FindProperty("profiles");
            var profileIndex = -1;
            for (var index = 0; index < profiles.arraySize; index++)
            {
                var profile = profiles.GetArrayElementAtIndex(index);
                if (profile.FindPropertyRelative("towerId").stringValue == spec.TowerId)
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

            ConfigureProfile(profiles.GetArrayElementAtIndex(profileIndex), spec, prefab);
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
            ?? LTW.UnityClient.Simulation.RenderCompat.Lit
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

        private static void WriteWardWrapperReport()
        {
            var report = @"# Ward Prototype Tower Wrapper Report

This report is generated by `Line Wards > Art > Generate Ward Prototype Tower Wrappers`.

## Generated Runtime Wrappers

| Tower | Runtime ID | Runtime Prefab | Source Kit Child | Source Kit Asset | Intent |
| --- | --- | --- | --- | --- | --- |
| Arrow | `tower.arrow` | `Assets/Prefabs/Towers/Tower_Arrow.prefab` | `ArrowRailVisual` | `Musket1_2_1.prefab` | Strong long-axis rail for focused single-target read. |
| Relay | `tower.relay` | `Assets/Prefabs/Towers/Tower_Relay.prefab` | `RelayMastVisual` | `Staff5_1_1.prefab` | Tall mast/capacitor support profile. |
| Control | `tower.control` | `Assets/Prefabs/Towers/Tower_Control.prefab` | `ControlDishVisual` | `Shield2_1_2.prefab` | Wide dish/containment silhouette. |

## Contract

Each wrapper keeps the required runtime children:

- `Body`
- `RoleMarker`
- `OwnerTrim`
- `RangeHalo`

Optional role anchors were also kept where useful:

- Arrow: `Muzzle`, `BowLeft`, `BowRight`, `Lens`
- Relay: `RelayMast`, `RelayCore`, `RelaySignal`, `CapacitorLeft`, `CapacitorRight`
- Control: `ControlRing`, `ControlCore`, `PulseEmitter`

## Notes

- Source-kit prefabs are nested as visual children; runtime profiles still point to Line Wards wrapper prefabs.
- Third-party source folder paths are not used by play-mode renderer code.
- Source-kit child colliders are stripped during wrapper generation.
- Pulse and Prism are intentionally deferred until the first three wrappers pass screenshot review.
";

            var fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), WardPrototypeReportPath);
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
                _ => new Vector3(1.18f, 1.28f, 1.18f)
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
