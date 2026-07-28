using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Generates the runtime creep wrappers around the AI-generated meshes and promotes them into
    /// CreepVisualLibrary, replacing the 2D plate prefabs the library previously loaded.
    /// </summary>
    public static class Creep3DProofSetGenerator
    {
        private const string GenerateMenuPath = "Line Wards/Art/Generate Creep 3D Wrappers";
        private const string PromoteMenuPath = "Line Wards/Art/Promote Creep 3D Set";
        private const string ValidateMenuPath = "Line Wards/Art/Validate Creep 3D Wrappers";

        private const string ModelRoot = "Assets/Art/AIStaging/Models/Creeps";

        private static readonly Creep3DImportSpec[] Specs =
        {
            new(
                "Runner",
                "creep.runner",
                CreepVisualRole.Runner,
                CreepVisualMotionStyle.RunnerDart,
                CreepDeathCueStyle.SparkBurst,
                ModelRoot + "/Runner/AIDrop/runner_meshy_blade_claw_blend_0725021347_prepared.fbx",
                ModelRoot + "/Runner/AIDrop/runner_meshy_blade_claw_blend_0725021347_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Runner_3D.prefab",
                new Vector3(1.13f, 1.13f, 1.13f),
                1f,
                // The blade-claw mesh is 0.90 wide but only 0.23 tall, so at board scale it reads
                // as a sliver no scale can rescue without overflowing the lane. Pitching it nose-up
                // trades unseen depth for silhouette, roughly doubling apparent height, and suits a
                // darting creep. Set back to Vector3.zero to return it flat.
                // Yaw 90 is a first guess to test whether it turns the blade to face down the lane
                // (creeps travel toward -Z); verifying with a capture before trusting the sign.
                new Vector3(35f, 90f, 0f),
                0.42f),
            new(
                "Brute",
                "creep.brute",
                CreepVisualRole.Brute,
                CreepVisualMotionStyle.HeavyBob,
                CreepDeathCueStyle.HeavyShatter,
                // Rigged variant: same prepared mesh with a quadruped skeleton and walk clip added
                // (tools/art_pipeline/rig_quadruped_creep.py). Built against the prepared FBX so
                // the scale/rotation values below stay valid.
                ModelRoot + "/Brute/AIDrop/brute_meshy_rock_golem_blend_0725021355_prepared_rigged.fbx",
                ModelRoot + "/Brute/AIDrop/brute_meshy_rock_golem_blend_0725021355_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Brute_3D.prefab",
                new Vector3(1.18f, 1.18f, 1.18f),
                1f,
                // Unlike Runner/Siege, Brute's long axis was already aligned with the lane (no 90
                // degree fix needed) but faced the wrong end of it — reported backwards, not
                // sideways. Yaw 180 turns it to face travel direction (toward -Z).
                new Vector3(0f, 180f, 0f),
                0.46f,
                RiggedCreepSetup.BruteControllerPath),
            new(
                "Swarm",
                "creep.swarm",
                CreepVisualRole.Swarm,
                CreepVisualMotionStyle.ClusterJitter,
                CreepDeathCueStyle.ShardScatter,
                ModelRoot + "/Swarm/AIDrop/swarm_meshy_crystal_swarm_blend_0725021324_prepared.fbx",
                ModelRoot + "/Swarm/AIDrop/swarm_meshy_crystal_swarm_blend_0725021324_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Swarm_3D.prefab",
                new Vector3(0.80f, 0.80f, 0.80f),
                1f,
                Vector3.zero,
                0.40f),
            new(
                "Shade",
                "creep.shade",
                CreepVisualRole.Stealth,
                CreepVisualMotionStyle.Shimmer,
                CreepDeathCueStyle.SoftDissolve,
                ModelRoot + "/Shade/AIDrop/shade_meshy_shard_wraith_blend_0725021339_prepared.fbx",
                ModelRoot + "/Shade/AIDrop/shade_meshy_shard_wraith_blend_0725021339_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Shade_3D.prefab",
                new Vector3(0.82f, 0.82f, 0.82f),
                1f,
                Vector3.zero,
                0.38f),
            new(
                "Siege",
                "creep.siege",
                CreepVisualRole.Siege,
                CreepVisualMotionStyle.SiegeWindup,
                CreepDeathCueStyle.HeavyShatter,
                ModelRoot + "/Siege/AIDrop/siege_meshy_beast_hybrid_blend_0725021332_prepared.fbx",
                ModelRoot + "/Siege/AIDrop/siege_meshy_beast_hybrid_blend_0725021332_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Siege_3D.prefab",
                new Vector3(1.13f, 1.13f, 1.13f),
                1f,
                // Raw mesh bounds are long on X, short on Y, same profile as Runner (which needed
                // yaw 90 to face down the lane) — same fix, verify with a capture.
                new Vector3(0f, 90f, 0f),
                0.46f),

            // Category 2 additions. Orientation/scale below are first guesses from a Blender-space
            // facing check (docs/GD_TUNING_LOG.md has the render-based reasoning per creep) — the
            // FBX Blender(Z-up)->Unity(Y-up) axis conversion means that check doesn't map 1:1 onto
            // these Unity-space import angles, so treat these as a starting point pending a real
            // capture, same as every other creep's "first guess... verify with a capture" comments
            // above.
            new(
                "Wisp",
                "creep.wisp",
                CreepVisualRole.Wisp,
                CreepVisualMotionStyle.Hover,
                CreepDeathCueStyle.ShardScatter,
                ModelRoot + "/Wisp/AIDrop/wisp_meshy_wisp_v01_prepared.fbx",
                ModelRoot + "/Wisp/AIDrop/wisp_meshy_wisp_v01_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Wisp_3D.prefab",
                new Vector3(0.75f, 0.75f, 0.75f),
                1f,
                // Small orbital-ring silhouette with no obvious front, same as Swarm — no yaw fix.
                Vector3.zero,
                0.34f),
            new(
                "Revenant",
                "creep.revenant",
                CreepVisualRole.Stealth,
                CreepVisualMotionStyle.Shimmer,
                CreepDeathCueStyle.SoftDissolve,
                ModelRoot + "/Revenant/AIDrop/revenant_meshy_revenant_v01_prepared.fbx",
                ModelRoot + "/Revenant/AIDrop/revenant_meshy_revenant_v01_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Revenant_3D.prefab",
                new Vector3(0.85f, 0.85f, 0.85f),
                1f,
                Vector3.zero,
                0.40f),
            new(
                "ObsidianBrute",
                "creep.obsidian_brute",
                CreepVisualRole.Brute,
                CreepVisualMotionStyle.HeavyBob,
                CreepDeathCueStyle.HeavyShatter,
                ModelRoot + "/Obsidianbrute/AIDrop/obsidianbrute_meshy_obsidianbrute_v01_prepared_rigged.fbx",
                ModelRoot + "/Obsidianbrute/AIDrop/obsidianbrute_meshy_obsidianbrute_v01_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_ObsidianBrute_3D.prefab",
                new Vector3(1.20f, 1.20f, 1.20f),
                1f,
                // Gorilla-stance golem, same general build as the original Brute, which needed
                // yaw 180 despite looking front-facing before correction — using the same fix as
                // a first guess rather than trusting an uncorrected look.
                new Vector3(0f, 180f, 0f),
                0.48f,
                RiggedCreepSetup.ObsidianBruteControllerPath),
            new(
                "Serpent",
                "creep.serpent",
                CreepVisualRole.Coil,
                // Was HeavyBob, borrowed from the golems, but that is a leg-driven lumber and this
                // creep has no limbs to step on. Coil carries the writhe through yaw and a scale
                // pulse instead.
                CreepVisualMotionStyle.Coil,
                CreepDeathCueStyle.ShardScatter,
                ModelRoot + "/Serpent/AIDrop/serpent_meshy_serpent_v01_prepared.fbx",
                ModelRoot + "/Serpent/AIDrop/serpent_meshy_serpent_v01_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Serpent_3D.prefab",
                new Vector3(0.85f, 0.85f, 0.85f),
                1f,
                Vector3.zero,
                0.42f),
            new(
                "TurretWalker",
                "creep.turret_walker",
                CreepVisualRole.Walker,
                CreepVisualMotionStyle.SiegeWindup,
                CreepDeathCueStyle.HeavyShatter,
                ModelRoot + "/Turretwalker/AIDrop/turretwalker_meshy_turretwalker_v01_prepared_rigged.fbx",
                ModelRoot + "/Turretwalker/AIDrop/turretwalker_meshy_turretwalker_v01_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_TurretWalker_3D.prefab",
                new Vector3(1.00f, 1.00f, 1.00f),
                1f,
                // Cannon barrel measured pointing off-axis in a Blender-space check; yaw 135 there
                // aligned it with the travel direction, used here as the first guess.
                new Vector3(0f, 135f, 0f),
                0.44f,
                RiggedCreepSetup.TurretWalkerControllerPath),
        };

        [MenuItem(GenerateMenuPath)]
        public static void GenerateCreep3DWrappers()
        {
            var generated = 0;
            for (var index = 0; index < Specs.Length; index++)
            {
                if (Creep3DImportPipeline.GenerateWrapper(Specs[index]))
                {
                    generated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated {generated} creep 3D wrapper(s).");
        }

        [MenuItem(PromoteMenuPath)]
        public static void PromoteCreep3DSet()
        {
            GenerateCreep3DWrappers();

            var issues = 0;
            for (var index = 0; index < Specs.Length; index++)
            {
                issues += Creep3DImportPipeline.ValidateWrapper(Specs[index]);
            }

            if (issues > 0)
            {
                Debug.LogError($"Creep 3D promotion aborted: {issues} wrapper issue(s).");
                ExitIfBatch(1);
                return;
            }

            var library = AssetDatabase.LoadAssetAtPath<CreepVisualLibrary>(Creep3DImportPipeline.LibraryPath);
            if (library == null)
            {
                Debug.LogError($"Missing creep visual library at {Creep3DImportPipeline.LibraryPath}.");
                ExitIfBatch(1);
                return;
            }

            var serialized = new SerializedObject(library);
            var profiles = serialized.FindProperty("profiles");
            for (var index = 0; index < Specs.Length; index++)
            {
                Creep3DImportPipeline.UpsertProfile(profiles, Specs[index]);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Promoted {Specs.Length} creep 3D wrapper(s) into {Creep3DImportPipeline.LibraryPath}.");
            ExitIfBatch(0);
        }

        [MenuItem(ValidateMenuPath)]
        public static void ValidateCreep3DWrappers()
        {
            var issues = 0;
            for (var index = 0; index < Specs.Length; index++)
            {
                issues += Creep3DImportPipeline.ValidateWrapper(Specs[index]);
            }

            if (issues > 0)
            {
                Debug.LogError($"Creep 3D validation found {issues} issue(s).");
                ExitIfBatch(1);
                return;
            }

            Debug.Log($"Validated {Specs.Length} creep 3D wrapper(s) with no issues.");
            ExitIfBatch(0);
        }

        private static void ExitIfBatch(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
