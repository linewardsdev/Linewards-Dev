using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class AiSourcePlateProofGenerator
    {
        private const string GenerateMenuPath = "Line Wards/Art/Generate AI Source Plate Proof Prefabs";
        private const string ValidateMenuPath = "Line Wards/Art/Validate AI Source Plate Proof Prefabs";

        private const string TowerLibraryPath = "Assets/Resources/TowerVisualLibrary.asset";
        private const string CreepLibraryPath = "Assets/Resources/CreepVisualLibrary.asset";
        private const string TowerPrefabFolder = "Assets/Prefabs/Towers";
        private const string CreepPrefabFolder = "Assets/Prefabs/Creeps";
        private const string MaterialFolder = "Assets/Art/AIStaging/SourcePlates/Materials";
        private const string ReportPath = "Assets/Art/AIStaging/SourcePlates/AiSourcePlateProofReport.md";

        private const string ArrowSpritePath = "Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed.png";
        private const string ControlSpritePath = "Assets/Art/Towers/Production/Sprites/tower_control_candidate_v01_trimmed.png";
        private const string RelaySpritePath = "Assets/Art/Towers/Production/Sprites/tower_relay_candidate_v01_trimmed.png";
        private const string PulseSpritePath = "Assets/Art/Towers/Production/Sprites/tower_pulse_candidate_v01_trimmed.png";
        private const string PrismSpritePath = "Assets/Art/Towers/Production/Sprites/tower_prism_candidate_v01_trimmed.png";
        private const string RunnerSpritePath = "Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v07_trimmed.png";
        private const string BruteSpritePath = "Assets/Art/Creeps/Production/Sprites/creep_brute_candidate_v02b_trimmed.png";
        private const string SwarmSpritePath = "Assets/Art/Creeps/Production/Sprites/creep_swarm_candidate_v01_trimmed.png";
        private const string ShadeSpritePath = "Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v02_trimmed.png";
        private const string SiegeSpritePath = "Assets/Art/Creeps/Production/Sprites/creep_siege_candidate_v01_trimmed.png";
        private const string ArrowPrefabPath = TowerPrefabFolder + "/Tower_Arrow_AIPlate.prefab";
        private const string ControlPrefabPath = TowerPrefabFolder + "/Tower_Control_AIPlate.prefab";
        private const string RelayPrefabPath = TowerPrefabFolder + "/Tower_Relay_AIPlate.prefab";
        private const string PulsePrefabPath = TowerPrefabFolder + "/Tower_Pulse_AIPlate.prefab";
        private const string PrismPrefabPath = TowerPrefabFolder + "/Tower_Prism_AIPlate.prefab";
        private const string RunnerPrefabPath = CreepPrefabFolder + "/Creep_Runner_AIPlate.prefab";
        private const string BrutePrefabPath = CreepPrefabFolder + "/Creep_Brute_AIPlate.prefab";
        private const string SwarmPrefabPath = CreepPrefabFolder + "/Creep_Swarm_AIPlate.prefab";
        private const string ShadePrefabPath = CreepPrefabFolder + "/Creep_Shade_AIPlate.prefab";
        private const string SiegePrefabPath = CreepPrefabFolder + "/Creep_Siege_AIPlate.prefab";

        [MenuItem(GenerateMenuPath)]
        public static void GenerateAiSourcePlateProofPrefabs()
        {
            EnsureFolder(TowerPrefabFolder);
            EnsureFolder(CreepPrefabFolder);
            EnsureFolder(MaterialFolder);

            ConfigureSpriteImport(ArrowSpritePath);
            ConfigureSpriteImport(ControlSpritePath);
            ConfigureSpriteImport(RelaySpritePath);
            ConfigureSpriteImport(PulseSpritePath);
            ConfigureSpriteImport(PrismSpritePath);
            ConfigureSpriteImport(RunnerSpritePath);
            ConfigureSpriteImport(BruteSpritePath);
            ConfigureSpriteImport(SwarmSpritePath);
            ConfigureSpriteImport(ShadeSpritePath);
            ConfigureSpriteImport(SiegeSpritePath);

            var plateMaterial = CreateOrUpdateMaterial(MaterialFolder + "/mat_ai_source_plate_sprite_v01.mat", Color.white);
            var supportMaterial = CreateOrUpdateMaterial(MaterialFolder + "/mat_ai_source_plate_support_v01.mat", new Color(0.05f, 0.1f, 0.14f, 0.68f));
            var accentMaterial = CreateOrUpdateMaterial(MaterialFolder + "/mat_ai_source_plate_accent_v01.mat", new Color(0.38f, 0.94f, 1f, 0.84f));
            var trimMaterial = CreateOrUpdateMaterial(MaterialFolder + "/mat_ai_source_plate_trim_v01.mat", new Color(0.95f, 0.78f, 0.28f, 0.84f));
            var haloMaterial = CreateOrUpdateMaterial(MaterialFolder + "/mat_ai_source_plate_halo_v01.mat", new Color(0.24f, 0.62f, 1f, 0.18f));
            var shadowMaterial = CreateOrUpdateMaterial(MaterialFolder + "/mat_ai_source_plate_shadow_v01.mat", new Color(0.015f, 0.02f, 0.03f, 0.36f));
            var damageMaterial = CreateOrUpdateMaterial(MaterialFolder + "/mat_ai_source_plate_damage_v01.mat", new Color(0.95f, 0.22f, 0.18f, 0.18f));

            SaveArrowProofPrefab(plateMaterial, supportMaterial, accentMaterial, trimMaterial, haloMaterial);
            SaveControlProofPrefab(plateMaterial, supportMaterial, accentMaterial, trimMaterial, haloMaterial);
            SaveRelayProofPrefab(plateMaterial, supportMaterial, accentMaterial, trimMaterial, haloMaterial);
            SavePulseProofPrefab(plateMaterial, supportMaterial, accentMaterial, trimMaterial, haloMaterial);
            SavePrismProofPrefab(plateMaterial, supportMaterial, accentMaterial, trimMaterial, haloMaterial);
            SaveRunnerProofPrefab(plateMaterial, supportMaterial, accentMaterial, shadowMaterial, damageMaterial);
            SaveBruteProofPrefab(plateMaterial, supportMaterial, accentMaterial, shadowMaterial, damageMaterial);
            SaveSwarmProofPrefab(plateMaterial, supportMaterial, accentMaterial, shadowMaterial, damageMaterial);
            SaveShadeProofPrefab(plateMaterial, supportMaterial, accentMaterial, shadowMaterial, damageMaterial);
            SaveSiegeProofPrefab(plateMaterial, supportMaterial, accentMaterial, shadowMaterial, damageMaterial);

            WriteReport();

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ReportPath);
            AssetDatabase.Refresh();
            Debug.Log("Generated AI source plate proof prefabs. Runtime visual libraries were not changed.");
        }

        [MenuItem(ValidateMenuPath)]
        public static void ValidateAiSourcePlateProofPrefabs()
        {
            var issueCount = 0;
            issueCount += ValidatePrefab(ArrowPrefabPath, new[] { "Body", "RoleMarker", "OwnerTrim", "RangeHalo", "Muzzle", "Lens", "AIPlateVisual" });
            issueCount += ValidatePrefab(ControlPrefabPath, new[] { "Body", "RoleMarker", "OwnerTrim", "RangeHalo", "Muzzle", "Lens", "AIPlateVisual" });
            issueCount += ValidatePrefab(RelayPrefabPath, new[] { "Body", "RoleMarker", "OwnerTrim", "RangeHalo", "Muzzle", "Lens", "AIPlateVisual" });
            issueCount += ValidatePrefab(PulsePrefabPath, new[] { "Body", "RoleMarker", "OwnerTrim", "RangeHalo", "Muzzle", "Lens", "AIPlateVisual" });
            issueCount += ValidatePrefab(PrismPrefabPath, new[] { "Body", "RoleMarker", "OwnerTrim", "RangeHalo", "Muzzle", "Lens", "AIPlateVisual" });
            issueCount += ValidatePrefab(RunnerPrefabPath, new[] { "Body", "GroundShadow", "RoleMarker", "Damage", "AIPlateVisual" });
            issueCount += ValidatePrefab(BrutePrefabPath, new[] { "Body", "GroundShadow", "RoleMarker", "Damage", "AIPlateVisual" });
            issueCount += ValidatePrefab(SwarmPrefabPath, new[] { "Body", "GroundShadow", "RoleMarker", "Damage", "AIPlateVisual" });
            issueCount += ValidatePrefab(ShadePrefabPath, new[] { "Body", "GroundShadow", "RoleMarker", "Damage", "AIPlateVisual" });
            issueCount += ValidatePrefab(SiegePrefabPath, new[] { "Body", "GroundShadow", "RoleMarker", "Damage", "AIPlateVisual" });

            if (issueCount == 0)
            {
                Debug.Log("AI source plate proof validation passed.");
            }
            else
            {
                Debug.LogWarning($"AI source plate proof validation completed with {issueCount} issue(s).");
            }
        }

        private static GameObject SaveArrowProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material trimMaterial,
            Material haloMaterial)
        {
            var root = new GameObject("Tower_Arrow_AIPlate");
            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0f), new Vector3(1.55f, 0.01f, 1.55f), haloMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, -0.02f), new Vector3(0.44f, 0.06f, 0.44f), supportMaterial, renderEnabled: false);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, -0.02f), new Vector3(0.48f, 0.018f, 0.48f), trimMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.42f), new Vector3(0.22f, 0.035f, 0.68f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0.82f), new Vector3(0.09f, 0.09f, 0.09f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Lens", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0.04f), new Vector3(0.16f, 0.16f, 0.16f), accentMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", ArrowSpritePath, plateMaterial, new Vector3(0f, 0.18f, 0.08f), new Vector2(0.155f, 0.155f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ArrowPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveRunnerProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material shadowMaterial,
            Material damageMaterial)
        {
            var root = new GameObject("Creep_Runner_AIPlate");
            CreateChild(root, "GroundShadow", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.46f, 0.012f, 0.9f), shadowMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.02f), new Vector3(0.018f, 0.018f, 0.018f), supportMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.58f), new Vector3(0.018f, 0.018f, 0.018f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Damage", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.32f), new Vector3(0.018f, 0.018f, 0.018f), damageMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", RunnerSpritePath, plateMaterial, new Vector3(0f, 0.22f, 0.06f), new Vector2(0.235f, 0.235f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RunnerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveControlProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material trimMaterial,
            Material haloMaterial)
        {
            var root = new GameObject("Tower_Control_AIPlate");
            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0f), new Vector3(1.55f, 0.01f, 1.55f), haloMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, -0.02f), new Vector3(0.44f, 0.06f, 0.44f), supportMaterial, renderEnabled: false);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, -0.02f), new Vector3(0.48f, 0.018f, 0.48f), trimMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.42f), new Vector3(0.22f, 0.035f, 0.68f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0.82f), new Vector3(0.09f, 0.09f, 0.09f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Lens", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0.04f), new Vector3(0.16f, 0.16f, 0.16f), accentMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", ControlSpritePath, plateMaterial, new Vector3(0f, 0.18f, 0.08f), new Vector2(0.135f, 0.135f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ControlPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveRelayProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material trimMaterial,
            Material haloMaterial)
        {
            var root = new GameObject("Tower_Relay_AIPlate");
            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0f), new Vector3(1.55f, 0.01f, 1.55f), haloMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, -0.02f), new Vector3(0.44f, 0.06f, 0.44f), supportMaterial, renderEnabled: false);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, -0.02f), new Vector3(0.48f, 0.018f, 0.48f), trimMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.42f), new Vector3(0.22f, 0.035f, 0.68f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0.82f), new Vector3(0.09f, 0.09f, 0.09f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Lens", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0.04f), new Vector3(0.16f, 0.16f, 0.16f), accentMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", RelaySpritePath, plateMaterial, new Vector3(0f, 0.18f, 0.08f), new Vector2(0.17f, 0.17f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RelayPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SavePulseProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material trimMaterial,
            Material haloMaterial)
        {
            var root = new GameObject("Tower_Pulse_AIPlate");
            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0f), new Vector3(1.55f, 0.01f, 1.55f), haloMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, -0.02f), new Vector3(0.44f, 0.06f, 0.44f), supportMaterial, renderEnabled: false);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, -0.02f), new Vector3(0.48f, 0.018f, 0.48f), trimMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.42f), new Vector3(0.22f, 0.035f, 0.68f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0.82f), new Vector3(0.09f, 0.09f, 0.09f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Lens", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0.04f), new Vector3(0.16f, 0.16f, 0.16f), accentMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", PulseSpritePath, plateMaterial, new Vector3(0f, 0.18f, 0.08f), new Vector2(0.145f, 0.145f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PulsePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SavePrismProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material trimMaterial,
            Material haloMaterial)
        {
            var root = new GameObject("Tower_Prism_AIPlate");
            CreateChild(root, "RangeHalo", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0f), new Vector3(1.55f, 0.01f, 1.55f), haloMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, -0.02f), new Vector3(0.44f, 0.06f, 0.44f), supportMaterial, renderEnabled: false);
            CreateChild(root, "OwnerTrim", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, -0.02f), new Vector3(0.48f, 0.018f, 0.48f), trimMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.42f), new Vector3(0.22f, 0.035f, 0.68f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Muzzle", PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0.82f), new Vector3(0.09f, 0.09f, 0.09f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Lens", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0.04f), new Vector3(0.16f, 0.16f, 0.16f), accentMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", PrismSpritePath, plateMaterial, new Vector3(0f, 0.18f, 0.08f), new Vector2(0.16f, 0.16f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrismPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveBruteProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material shadowMaterial,
            Material damageMaterial)
        {
            var root = new GameObject("Creep_Brute_AIPlate");
            CreateChild(root, "GroundShadow", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.46f, 0.012f, 0.9f), shadowMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.02f), new Vector3(0.018f, 0.018f, 0.018f), supportMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.58f), new Vector3(0.018f, 0.018f, 0.018f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Damage", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.32f), new Vector3(0.018f, 0.018f, 0.018f), damageMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", BruteSpritePath, plateMaterial, new Vector3(0f, 0.22f, 0.06f), new Vector2(0.175f, 0.175f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BrutePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveSiegeProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material shadowMaterial,
            Material damageMaterial)
        {
            var root = new GameObject("Creep_Siege_AIPlate");
            CreateChild(root, "GroundShadow", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.46f, 0.012f, 0.9f), shadowMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.02f), new Vector3(0.018f, 0.018f, 0.018f), supportMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.58f), new Vector3(0.018f, 0.018f, 0.018f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Damage", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.32f), new Vector3(0.018f, 0.018f, 0.018f), damageMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", SiegeSpritePath, plateMaterial, new Vector3(0f, 0.22f, 0.06f), new Vector2(0.24f, 0.24f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, SiegePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveSwarmProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material shadowMaterial,
            Material damageMaterial)
        {
            var root = new GameObject("Creep_Swarm_AIPlate");
            CreateChild(root, "GroundShadow", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.46f, 0.012f, 0.9f), shadowMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.02f), new Vector3(0.018f, 0.018f, 0.018f), supportMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.58f), new Vector3(0.018f, 0.018f, 0.018f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Damage", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.32f), new Vector3(0.018f, 0.018f, 0.018f), damageMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", SwarmSpritePath, plateMaterial, new Vector3(0f, 0.22f, 0.06f), new Vector2(0.16f, 0.16f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, SwarmPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject SaveShadeProofPrefab(
            Material plateMaterial,
            Material supportMaterial,
            Material accentMaterial,
            Material shadowMaterial,
            Material damageMaterial)
        {
            var root = new GameObject("Creep_Shade_AIPlate");
            CreateChild(root, "GroundShadow", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.46f, 0.012f, 0.9f), shadowMaterial, renderEnabled: false);
            CreateChild(root, "Body", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.02f), new Vector3(0.018f, 0.018f, 0.018f), supportMaterial, renderEnabled: false);
            CreateChild(root, "RoleMarker", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.58f), new Vector3(0.018f, 0.018f, 0.018f), accentMaterial, renderEnabled: false);
            CreateChild(root, "Damage", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.32f), new Vector3(0.018f, 0.018f, 0.018f), damageMaterial, renderEnabled: false);
            CreateSpritePlate(root, "AIPlateVisual", ShadeSpritePath, plateMaterial, new Vector3(0f, 0.22f, 0.06f), new Vector2(0.185f, 0.185f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ShadePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateSpritePlate(GameObject parent, string name, string spritePath, Material material, Vector3 localPosition, Vector2 size)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogError($"Missing AI source plate sprite at {spritePath}.");
                return new GameObject(name);
            }

            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            child.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 10;
            return child;
        }

        private static void ConfigureSpriteImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static GameObject CreateChild(
            GameObject parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool renderEnabled = true)
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
                renderer.enabled = renderEnabled;
            }

            return child;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindSpriteShader());
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            material.color = color;
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader FindSpriteShader() =>
            Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? LTW.UnityClient.Simulation.RenderCompat.Lit;

        private static int ValidatePrefab(string path, string[] childPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"Missing AI source plate proof prefab at {path}.");
                return 1;
            }

            var issueCount = 0;
            for (var index = 0; index < childPaths.Length; index++)
            {
                var childPath = childPaths[index];
                var target = prefab.transform.Find(childPath);
                if (target == null)
                {
                    Debug.LogError($"AI proof prefab '{prefab.name}' is missing child '{childPath}'.", prefab);
                    issueCount++;
                    continue;
                }

                if (target.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    Debug.LogError($"AI proof prefab '{prefab.name}' child '{childPath}' has no renderer.", prefab);
                    issueCount++;
                }
            }

            return issueCount;
        }

        private static void WriteReport()
        {
            var report = $@"# AI Source Plate Proof Report

This report is generated by `Line Wards > Art > Generate AI Source Plate Proof Prefabs`.

## Generated Proof Prefabs

| Role | Runtime ID | Runtime Prefab | Source Plate | Notes |
| --- | --- | --- | --- | --- |
| Arrow tower | `tower.arrow` | `{ArrowPrefabPath}` | `{ArrowSpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |
| Control tower | `tower.control` | `{ControlPrefabPath}` | `{ControlSpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |
| Relay tower | `tower.relay` | `{RelayPrefabPath}` | `{RelaySpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |
| Pulse tower | `tower.pulse` | `{PulsePrefabPath}` | `{PulseSpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |
| Prism tower | `tower.prism` | `{PrismPrefabPath}` | `{PrismSpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |
| Runner creep | `creep.runner` | `{RunnerPrefabPath}` | `{RunnerSpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |
| Brute creep | `creep.brute` | `{BrutePrefabPath}` | `{BruteSpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |
| Swarm creep | `creep.swarm` | `{SwarmPrefabPath}` | `{SwarmSpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |
| Shade creep | `creep.shade` | `{ShadePrefabPath}` | `{ShadeSpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |
| Siege creep | `creep.siege` | `{SiegePrefabPath}` | `{SiegeSpritePath}` | Painted plate is kept on `AIPlateVisual`; required contract children remain present but render-disabled so primitive scaffolding does not cover the source plate. |

## Runtime Promotion

- Proof generation itself does not edit runtime visual libraries.
- V1 roles may be promoted intentionally by updating `TowerVisualLibrary.asset` and `CreepVisualLibrary.asset` after review approval.
- As of 2026-07-15, `tower.arrow`, `tower.control`, `tower.relay`, `tower.pulse`, `tower.prism`, `creep.runner`, `creep.brute`, `creep.swarm`, `creep.shade`, and `creep.siege` are wired as active runtime proof entries pending hands-on review for the newest batch.
- The Builder avatar is code-integrated from `Assets/Resources/Art/Builder/Production/Sprites/builder_candidate_v01_trimmed.png` because it is procedural rather than driven by a visual library profile.

## Review Notes

- This is a proof integration slice, not a final art pass.
- `AIPlateVisual` is intentionally not referenced by profile color paths so runtime tinting does not flatten the painted plate.
- Proof plate sizes are conservative so wide generated silhouettes do not spill across neighboring cells in phone-scale gameplay.
- If the proof plates fail phone-scale gameplay readability, restore the previous source-kit prefabs before rolling this pipeline across other roles.
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
    }
}
