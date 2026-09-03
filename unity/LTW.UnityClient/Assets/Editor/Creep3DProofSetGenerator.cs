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
        private const string GenerateMenuPath = "Line Wars/Art/Generate Creep 3D Wrappers";
        private const string PromoteMenuPath = "Line Wars/Art/Promote Creep 3D Set";
        private const string ValidateMenuPath = "Line Wars/Art/Validate Creep 3D Wrappers";

        private const string ModelRoot = "Assets/Art/AIStaging/Models/Creeps";

        // Runtime scales below are solved, not hand-picked (2026-07-28 silhouette pass). They are
        // not comparable to each other on their own: every source FBX has a different intrinsic
        // mesh size, and the renderer applies a further 1.18/1.12/1.18 on top, so equal scales do
        // not mean equal on-screen size. Before this pass they were effectively arbitrary, and
        // silhouette carried no threat information — Siege (48 health) rendered *shorter* than
        // Shade (14 health), and Serpent Coil (32 health) was the smallest creep on the board.
        //
        // The rule: perceived size, taken as sqrt(effectiveHeight * effectiveWidth), tracks the
        // creep's health along 0.62 + 0.52*sqrt((hp-4)/56). Each scale is then solved from the
        // creep's *measured* prefab bounds to hit that target, subject to two clamps — effective
        // width <= 1.45 world units (one grid cell is 1 unit; beyond this creeps foul neighbouring
        // lane content) and a health-tracking height ceiling so slender meshes cannot tower over
        // heavier creeps.
        //
        // Consequence worth knowing before editing: changing a number here by hand will usually be
        // wrong. Re-measure prefab bounds and re-solve. Two residual inversions are accepted and
        // understood — Shade and Revenant are slender meshes that hit the height ceiling before
        // reaching their size target, so both sit slightly below where health alone would put
        // them. See docs/GD_TUNING_LOG.md for the measured before/after table.
        private static readonly Creep3DImportSpec[] Specs =
        {
            new(
                "Runner",
                "creep.runner",
                CreepVisualRole.Runner,
                CreepVisualMotionStyle.RunnerDart,
                CreepDeathCueStyle.SparkBurst,
                // Rigged variant: same prepared mesh with a blade/spine skeleton and a beat cycle
                // added (tools/art_pipeline/rig_bladed_runner.py). Built against the prepared FBX
                // so the scale/rotation values below stay valid.
                ModelRoot + "/Runner/AIDrop/runner_meshy_blade_claw_blend_0725021347_prepared_rigged.fbx",
                ModelRoot + "/Runner/AIDrop/runner_meshy_blade_claw_blend_0725021347_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Runner_3D.prefab",
                new Vector3(1.08f, 1.08f, 1.08f),
                1f,
                // The blade-claw mesh is 0.90 wide but only 0.23 tall, so at board scale it reads
                // as a sliver no scale can rescue without overflowing the lane. The 35 buys back
                // silhouette, roughly doubling apparent height — though note it is a ROLL about the
                // creep's own long axis, not the nose-up pitch the original comment called it: the
                // long axis is Unity X before the yaw, and a rotation about X leaves it alone and
                // turns the flat blade plane edge-up instead. That is what takes the prefab from
                // 0.230 to the 0.448 UnitBoundsReport measures. Set back to Vector3.zero to
                // return it flat.
                //
                // Yaw was 90 and its own comment asked for a capture before the sign was trusted.
                // Captured (docs/screenshot-reviews/creep-rigs-wave-2-3/) and the sign was wrong:
                // the lance sits at Blender +X, which maps to Unity -X, and yaw 90 sends that to
                // +Z — up the lane, away from travel. The creep was flying backwards. 270 points
                // it down the lane. Bounds are unchanged either way, so the solved runtime scale
                // above still holds.
                new Vector3(35f, 270f, 0f),
                0.42f,
                RiggedCreepSetup.RunnerControllerPath),
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
                new Vector3(1.07f, 1.07f, 1.07f),
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
                new Vector3(0.82f, 0.82f, 0.82f),
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
                new Vector3(0.99f, 0.99f, 0.99f),
                1f,
                Vector3.zero,
                0.38f),
            new(
                "Siege",
                "creep.siege",
                CreepVisualRole.Siege,
                CreepVisualMotionStyle.SiegeWindup,
                CreepDeathCueStyle.HeavyShatter,
                // Rigged variant: same prepared mesh with four rolling wheels, a sprung chassis and
                // a ram head added (tools/art_pipeline/rig_wheeled_ram.py). Built against the
                // prepared FBX so the scale/rotation values below stay valid.
                ModelRoot + "/Siege/AIDrop/siege_meshy_beast_hybrid_blend_0725021332_prepared_rigged.fbx",
                ModelRoot + "/Siege/AIDrop/siege_meshy_beast_hybrid_blend_0725021332_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Siege_3D.prefab",
                new Vector3(1.36f, 1.36f, 1.36f),
                1f,
                // Raw mesh bounds are long on X, short on Y, same profile as Runner — yaw 90 to
                // face down the lane. Verified with a capture, unlike the Runner's, and this one
                // was right: the plow nose sits at Blender -X, which maps to Unity +X, and yaw 90
                // sends that to -Z, the travel direction.
                new Vector3(0f, 90f, 0f),
                0.46f,
                RiggedCreepSetup.SiegeControllerPath),

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
                new Vector3(0.68f, 0.68f, 0.68f),
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
                // The one deliberate exception to the health-driven rule above (see the Specs
                // header). By health alone (8, second-lowest) the Revenant would be near the
                // bottom, but its threat is economic rather than durability — it carries the
                // roster's best income-per-cost, so ignoring it feeds the sender. Its design
                // question is "does the defender finish off a fragile, high-value target, or let
                // it feed the enemy economy?", and a player can only prioritise what they can
                // pick out of a wave. Held at 1.05 so it stays visible. Its slender mesh keeps
                // the footprint honest: tall, but second-narrowest, so it does not read as a tank.
                new Vector3(1.05f, 1.05f, 1.05f),
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
                new Vector3(1.23f, 1.23f, 1.23f),
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
                // Rigged variant: same prepared mesh with an eight-sector coil ring and a head
                // added (tools/art_pipeline/rig_coiled_serpent.py). Built against the prepared FBX
                // so the scale/rotation values below stay valid.
                ModelRoot + "/Serpent/AIDrop/serpent_meshy_serpent_v01_prepared_rigged.fbx",
                ModelRoot + "/Serpent/AIDrop/serpent_meshy_serpent_v01_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Serpent_3D.prefab",
                new Vector3(1.23f, 1.23f, 1.23f),
                1f,
                // Was Vector3.zero, uncaptured. Captured while rigging
                // (docs/screenshot-reviews/creep-rigs-wave-2-3/) and it was backwards, the same
                // way the Brute's was before it got yaw 180: the head faces Blender -Y, which maps
                // to Unity +Z, so at yaw 0 the creep presented its tail coil to the camera and its
                // face to the leak gate. Bounds are symmetric about the coil axis so the solved
                // runtime scale above is unaffected.
                new Vector3(0f, 180f, 0f),
                0.42f,
                RiggedCreepSetup.SerpentControllerPath),
            new(
                "TurretWalker",
                "creep.turret_walker",
                CreepVisualRole.Walker,
                CreepVisualMotionStyle.SiegeWindup,
                CreepDeathCueStyle.HeavyShatter,
                ModelRoot + "/Turretwalker/AIDrop/turretwalker_meshy_turretwalker_v01_prepared_rigged.fbx",
                ModelRoot + "/Turretwalker/AIDrop/turretwalker_meshy_turretwalker_v01_prepared_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_TurretWalker_3D.prefab",
                new Vector3(0.96f, 0.96f, 0.96f),
                1f,
                // Cannon barrel measured pointing off-axis in a Blender-space check; yaw 135 there
                // aligned it with the travel direction, used here as the first guess.
                new Vector3(0f, 135f, 0f),
                0.44f,
                RiggedCreepSetup.TurretWalkerControllerPath),

            // Category 3 — Meshy auto-rigged bipeds, used as delivered (24-bone humanoid rig,
            // walk and run clips already authored). Unlike every creep above, these never go
            // through ai_asset_intake.py: its normalize step exports MESH+EMPTY with no
            // bake_anim and would strip the armature and clips outright. Scale and orientation
            // are handled here instead, which is what importScale/importEulerAngles are for.
            //
            // importEulerAngles is Vector3.zero for all five, and unusually that is *verified*
            // rather than a first guess: a render from the real game camera (30 degrees off
            // vertical, creeps travelling toward it) shows them already facing the camera, which
            // is the facing the game wants.
            //
            // motionStyle is Auto throughout. The renderer layers procedural motion on top of
            // skeletal animation, and these carry real biped walk/run cycles — stacking a bob or
            // windup on top risks double-animating. Start minimal, add only if a capture shows
            // they need it.
            //
            // runtimeScale values are solved, not authored, using the rule in GD_TUNING_LOG.md:
            // perceived size sqrt(effH*effW) tracks health along 0.62 + 0.52*sqrt((hp-4)/56),
            // measured against each model's animated-pose bounds AS IMPORTED INTO UNITY (not the raw
            // Blender bounds — FBX unit-scale conversion makes the imported mesh ~1.8x larger, and
            // solving against Blender figures put every one of these creeps roughly double size)
            // and clamped to width <= 1.45
            // world units. The curve's denominator is deliberately left at 56 so none of the
            // existing ten shift; creeps above 60 health simply extrapolate past Obsidian Brute.
            new(
                "Zephyr",
                "creep.zephyr",
                CreepVisualRole.Air,
                CreepVisualMotionStyle.Auto,
                CreepDeathCueStyle.SoftDissolve,
                ModelRoot + "/Zephyr/AIDrop/zephyr_meshy_zephyr_v01_rigged.fbx",
                ModelRoot + "/Zephyr/AIDrop/zephyr_meshy_zephyr_v01_rigged_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Zephyr_3D.prefab",
                new Vector3(0.278f, 0.278f, 0.278f),
                1f,
                Vector3.zero,
                0.38f,
                RiggedCreepSetup.ZephyrControllerPath),
            new(
                "Stalker",
                "creep.stalker",
                CreepVisualRole.Stealth,
                CreepVisualMotionStyle.Auto,
                CreepDeathCueStyle.SoftDissolve,
                ModelRoot + "/Stalker/AIDrop/stalker_meshy_stalker_v01_rigged.fbx",
                ModelRoot + "/Stalker/AIDrop/stalker_meshy_stalker_v01_rigged_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Stalker_3D.prefab",
                new Vector3(0.208f, 0.208f, 0.208f),
                1f,
                Vector3.zero,
                0.42f,
                RiggedCreepSetup.StalkerControllerPath),
            new(
                "Burrower",
                "creep.burrower",
                CreepVisualRole.Burrower,
                CreepVisualMotionStyle.Auto,
                CreepDeathCueStyle.HeavyShatter,
                ModelRoot + "/Burrower/AIDrop/burrower_meshy_burrower_v01_rigged.fbx",
                ModelRoot + "/Burrower/AIDrop/burrower_meshy_burrower_v01_rigged_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Burrower_3D.prefab",
                new Vector3(0.245f, 0.245f, 0.245f),
                1f,
                Vector3.zero,
                0.46f,
                RiggedCreepSetup.BurrowerControllerPath),
            new(
                "Warden",
                "creep.warden",
                CreepVisualRole.Warden,
                CreepVisualMotionStyle.Auto,
                CreepDeathCueStyle.HeavyShatter,
                ModelRoot + "/Warden/AIDrop/warden_meshy_warden_v01_rigged.fbx",
                ModelRoot + "/Warden/AIDrop/warden_meshy_warden_v01_rigged_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Warden_3D.prefab",
                new Vector3(0.428f, 0.428f, 0.428f),
                1f,
                Vector3.zero,
                0.44f,
                RiggedCreepSetup.WardenControllerPath),
            new(
                "Colossus",
                "creep.colossus",
                CreepVisualRole.Boss,
                CreepVisualMotionStyle.Auto,
                CreepDeathCueStyle.HeavyShatter,
                ModelRoot + "/Colossus/AIDrop/colossus_meshy_colossus_v01_rigged.fbx",
                ModelRoot + "/Colossus/AIDrop/colossus_meshy_colossus_v01_rigged_Textures",
                Creep3DImportPipeline.RuntimePrefabFolder + "/Creep_Colossus_3D.prefab",
                new Vector3(0.329f, 0.329f, 0.329f),
                1f,
                Vector3.zero,
                0.50f,
                RiggedCreepSetup.ColossusControllerPath),
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
