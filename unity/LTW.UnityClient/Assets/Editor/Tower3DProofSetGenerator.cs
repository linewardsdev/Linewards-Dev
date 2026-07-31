using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class Tower3DProofSetGenerator
    {
        private const string GenerateMenuPath = "Line Wards/Art/Generate Available Tower 3D Proof Wrappers";
        private const string ValidateMenuPath = "Line Wards/Art/Validate Tower 3D Proof Wrappers";
        private const string PromoteAvailableForReviewMenuPath = "Line Wards/Art/Promote Available Tower 3D Proofs For Review";
        private const string PromoteMenuPath = "Line Wards/Art/Promote Complete Tower 3D Set";

        private static readonly Tower3DImportSpec[] Specs =
        {
            new(
                "Arrow",
                "tower.arrow",
                TowerVisualRole.Arrow,
                "Assets/Art/AIStaging/Models/Towers/Arrow/AIDrop/arrow_split_base_head_0727.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Arrow_3D.prefab",
                new Vector3(0.78f, 0.78f, 0.78f),
                0f,
                new[] { "Muzzle", "Lens", "BowLeft", "BowRight" },
                new Vector3(0f, 0.14f, 0.48f),
                new Vector3(0f, 0.16f, 0.02f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),
            new(
                "Control",
                "tower.control",
                TowerVisualRole.Control,
                "Assets/Art/AIStaging/Models/Towers/Control/AIDrop/control_split_base_stemcore_ring_0727.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Control_3D.prefab",
                new Vector3(0.74f, 0.74f, 0.74f),
                0f,
                new[] { "Muzzle", "Lens", "ControlCore", "ControlRing", "PulseEmitter" },
                new Vector3(0f, 0.14f, 0.44f),
                new Vector3(0f, 0.16f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),
            new(
                "Relay",
                "tower.relay",
                TowerVisualRole.Relay,
                "Assets/Art/AIStaging/Models/Towers/Relay/AIDrop/relay_split_base_dish_0727.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Relay_3D.prefab",
                new Vector3(0.66f, 0.66f, 0.66f),
                0f,
                new[] { "Muzzle", "Lens", "RelayMast", "RelaySignal", "RelayCore" },
                new Vector3(0f, 0.18f, 0.38f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),
            new(
                "Pulse",
                "tower.pulse",
                TowerVisualRole.Pulse,
                "Assets/Art/AIStaging/Models/Towers/Pulse/AIDrop/pulse_split_base_ring_0727.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Pulse_3D.prefab",
                new Vector3(0.76f, 0.76f, 0.76f),
                0f,
                new[] { "Muzzle", "Lens", "PulseCore", "PulseRingA", "PulseEmitter" },
                new Vector3(0f, 0.14f, 0.40f),
                new Vector3(0f, 0.16f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),
            new(
                "Prism",
                "tower.prism",
                TowerVisualRole.Prism,
                "Assets/Art/AIStaging/Models/Towers/Prism/AIDrop/prism_split_base_core_0727b.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Prism_3D.prefab",
                new Vector3(0.62f, 0.62f, 0.62f),
                0f,
                new[] { "Muzzle", "Lens", "PrismSpire", "PrismLens", "BeamAnchor" },
                new Vector3(0f, 0.20f, 0.38f),
                new Vector3(0f, 0.24f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            // --- Foundry and Grove lines ---------------------------------------------------
            // Runtime scale is derived, not eyeballed: the prep pipeline normalises every tower to
            // exactly 1.25 height with its base at Z=0, so scale = 0.85 / measured footprint puts
            // each tower at ~0.85 of a board cell (cells are 1 world unit) and keeps it inside its
            // square. Clamped to [0.55, 0.90] so the widest (Sapling, 1.43) does not shrink to
            // nothing and the narrowest (Repair Drone, 0.50) does not balloon.
            //
            // These reference the UNSPLIT prepared meshes, so the whole body is the motion target
            // and there is no independently-aimed head yet. That is deliberate: which part aims
            // depends on each tower's mechanic, and splitting before that is decided is how the
            // Arrow barrel ended up pointing backwards twice.
            new(
                "Gatling",
                "tower.gatling",
                TowerVisualRole.Gatling,
                "Assets/Art/AIStaging/Models/Towers/Gatling/AIDrop/gatling_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Gatling_3D.prefab",
                new Vector3(0.72f, 0.72f, 0.72f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            new(
                "Tesla",
                "tower.tesla",
                TowerVisualRole.Tesla,
                "Assets/Art/AIStaging/Models/Towers/Tesla/AIDrop/tesla_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Tesla_3D.prefab",
                new Vector3(0.9f, 0.9f, 0.9f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            new(
                "Foundry",
                "tower.foundry",
                TowerVisualRole.Foundry,
                "Assets/Art/AIStaging/Models/Towers/Foundry/AIDrop/foundry_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Foundry_3D.prefab",
                new Vector3(0.83f, 0.83f, 0.83f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            new(
                "Barricade",
                "tower.barricade",
                TowerVisualRole.Barricade,
                "Assets/Art/AIStaging/Models/Towers/Barricade/AIDrop/barricade_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Barricade_3D.prefab",
                new Vector3(0.71f, 0.71f, 0.71f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            new(
                "RepairDrone",
                "tower.repair_drone",
                TowerVisualRole.RepairDrone,
                "Assets/Art/AIStaging/Models/Towers/Repairdrone/AIDrop/repairdrone_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_RepairDrone_3D.prefab",
                new Vector3(0.9f, 0.9f, 0.9f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            new(
                "ElderCanopy",
                "tower.elder_canopy",
                TowerVisualRole.ElderCanopy,
                "Assets/Art/AIStaging/Models/Towers/Eldercanopy/AIDrop/eldercanopy_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_ElderCanopy_3D.prefab",
                new Vector3(0.66f, 0.66f, 0.66f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            new(
                "Sapling",
                "tower.sapling",
                TowerVisualRole.Sapling,
                "Assets/Art/AIStaging/Models/Towers/Sapling/AIDrop/sapling_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Sapling_3D.prefab",
                new Vector3(0.59f, 0.59f, 0.59f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            new(
                "Bloomheart",
                "tower.bloomheart",
                TowerVisualRole.Bloomheart,
                "Assets/Art/AIStaging/Models/Towers/Bloomheart/AIDrop/bloomheart_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_Bloomheart_3D.prefab",
                new Vector3(0.67f, 0.67f, 0.67f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            new(
                "ThornSnare",
                "tower.thorn_snare",
                TowerVisualRole.ThornSnare,
                "Assets/Art/AIStaging/Models/Towers/Thornsnare/AIDrop/thornsnare_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_ThornSnare_3D.prefab",
                new Vector3(0.68f, 0.68f, 0.68f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false),

            new(
                "SporeCloud",
                "tower.spore_cloud",
                TowerVisualRole.SporeCloud,
                "Assets/Art/AIStaging/Models/Towers/Sporecloud/AIDrop/sporecloud_meshy_v01_prepared.fbx",
                Tower3DImportPipeline.RuntimePrefabFolder + "/Tower_SporeCloud_3D.prefab",
                new Vector3(0.85f, 0.85f, 0.85f),
                0f,
                new[] { "Muzzle", "Lens" },
                new Vector3(0f, 0.18f, 0.42f),
                new Vector3(0f, 0.22f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false,
                true,
                false)
        };

        [MenuItem(GenerateMenuPath)]
        public static void GenerateAvailableProofWrappers()
        {
            Tower3DImportPipeline.EnsureFolder(Tower3DImportPipeline.RuntimePrefabFolder);
            Tower3DImportPipeline.EnsureFolder(Tower3DImportPipeline.MaterialFolder);
            var generatedCount = 0;

            for (var index = 0; index < Specs.Length; index++)
            {
                if (Tower3DImportPipeline.GenerateWrapperIfRawExists(Specs[index]))
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
                issueCount += Tower3DImportPipeline.ValidateWrapper(Specs[index], requireExists: false);
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
                issueCount += Tower3DImportPipeline.ValidateWrapper(Specs[index], requireExists: true);
            }

            if (issueCount > 0)
            {
                Debug.LogError($"Tower 3D set promotion blocked. Resolve {issueCount} missing/invalid wrapper issue(s) first.");
                return;
            }

            var library = AssetDatabase.LoadAssetAtPath<TowerVisualLibrary>(Tower3DImportPipeline.LibraryPath);
            if (library == null)
            {
                Debug.LogError($"Missing tower visual library at {Tower3DImportPipeline.LibraryPath}. Not promoting Tower 3D set.");
                return;
            }

            var serializedLibrary = new SerializedObject(library);
            var profiles = serializedLibrary.FindProperty("profiles");
            for (var index = 0; index < Specs.Length; index++)
            {
                if (!Specs[index].ReviewPromotable)
                {
                    Debug.LogError($"{Specs[index].DisplayName} 3D set promotion blocked. This proof is marked non-promotable after visual rejection.");
                    return;
                }

                Tower3DImportPipeline.UpsertProfile(profiles, Specs[index]);
            }

            serializedLibrary.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("Promoted complete Tower 3D set to TowerVisualLibrary.");
        }

        [MenuItem(PromoteAvailableForReviewMenuPath)]
        public static void PromoteAvailableTower3DProofsForReview()
        {
            var library = AssetDatabase.LoadAssetAtPath<TowerVisualLibrary>(Tower3DImportPipeline.LibraryPath);
            if (library == null)
            {
                Debug.LogError($"Missing tower visual library at {Tower3DImportPipeline.LibraryPath}. Not promoting available Tower 3D proofs.");
                return;
            }

            var promotedCount = 0;
            var serializedLibrary = new SerializedObject(library);
            var profiles = serializedLibrary.FindProperty("profiles");
            for (var index = 0; index < Specs.Length; index++)
            {
                var spec = Specs[index];
                if (!spec.ReviewPromotable)
                {
                    Debug.LogWarning($"Skipping {spec.DisplayName} review promotion: proof is marked non-promotable after visual rejection.");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.RuntimePrefabPath) == null)
                {
                    Debug.LogWarning($"Skipping {spec.DisplayName} review promotion: missing wrapper at {spec.RuntimePrefabPath}.");
                    continue;
                }

                var issueCount = Tower3DImportPipeline.ValidateWrapper(spec, requireExists: true);
                if (issueCount > 0)
                {
                    Debug.LogWarning($"Skipping {spec.DisplayName} review promotion: wrapper has {issueCount} validation issue(s).");
                    continue;
                }

                Tower3DImportPipeline.UpsertProfile(profiles, spec);
                promotedCount++;
            }

            serializedLibrary.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"Promoted {promotedCount} available Tower 3D proof(s) to TowerVisualLibrary for review only. Complete-set promotion remains separate.");
        }
    }
}
