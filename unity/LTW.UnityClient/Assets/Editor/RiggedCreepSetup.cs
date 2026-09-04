using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Configures a rigged creep FBX for skeletal animation and builds the animator controller
    /// that drives it.
    /// </summary>
    /// <remarks>
    /// Two sources of rig feed this. Meshy auto-rigs bipeds and ships them with walk and run
    /// clips already authored, so the Category 3 creeps below are used as delivered. It cannot
    /// rig quadrupeds, so those carry hand-authored rigs instead (see
    /// tools/art_pipeline/rig_quadruped_creep.py for the golems and rig_turret_walker.py for the
    /// walker) re-exported alongside the prepared mesh.
    ///
    /// Either way a freshly imported rigged FBX defaults to no rig and non-looping clips, which
    /// reads as a creature that animates once and then freezes, so both have to be set explicitly
    /// here rather than left to the importer defaults.
    ///
    /// Note the Category 3 models deliberately skip tools/art_pipeline/ai_asset_intake.py: its
    /// normalize step exports object_types={"MESH","EMPTY"} with no bake_anim, which would
    /// silently strip the armature and every clip. Their scale and orientation are handled by
    /// Creep3DImportSpec's importScale/importEulerAngles instead.
    /// </remarks>
    public static class RiggedCreepSetup
    {
        public const string BruteRiggedModelPath =
            "Assets/Art/AIStaging/Models/Creeps/Brute/AIDrop/brute_meshy_rock_golem_blend_0725021355_prepared_rigged.fbx";

        public const string ObsidianBruteRiggedModelPath =
            "Assets/Art/AIStaging/Models/Creeps/Obsidianbrute/AIDrop/obsidianbrute_meshy_obsidianbrute_v01_prepared_rigged.fbx";

        public const string TurretWalkerRiggedModelPath =
            "Assets/Art/AIStaging/Models/Creeps/Turretwalker/AIDrop/turretwalker_meshy_turretwalker_v01_prepared_rigged.fbx";

        // Wave 2.3 (OPEN_ITEMS item 11). None of these three is a walker, which is why each has
        // its own script rather than another entry in rig_quadruped_creep.py's PROFILES table:
        // the Siege is a wheeled ram, the Serpent is a coil, and the Runner floats. Item 4's
        // render-check-first rule is what surfaced that, ahead of any rig work.
        public const string SiegeRiggedModelPath =
            "Assets/Art/AIStaging/Models/Creeps/Siege/AIDrop/siege_meshy_beast_hybrid_blend_0725021332_prepared_rigged.fbx";

        public const string SerpentRiggedModelPath =
            "Assets/Art/AIStaging/Models/Creeps/Serpent/AIDrop/serpent_meshy_serpent_v01_prepared_rigged.fbx";

        public const string RunnerRiggedModelPath =
            "Assets/Art/AIStaging/Models/Creeps/Runner/AIDrop/runner_meshy_blade_claw_blend_0725021347_prepared_rigged.fbx";

        // Category 3: Meshy-rigged bipeds, used as delivered. The clip each carries follows the
        // creep's speed — the running clip for the fast ones (Zephyr, Stalker), walking for the
        // slow ones — because a run cycle's longer stride and faster cadence measurably reduces
        // the foot skate that comes from creeps translating faster than any gait can carry them.
        private const string Category3Root = "Assets/Art/AIStaging/Models/Creeps";

        public const string ZephyrRiggedModelPath =
            Category3Root + "/Zephyr/AIDrop/zephyr_meshy_zephyr_v01_rigged.fbx";

        public const string StalkerRiggedModelPath =
            Category3Root + "/Stalker/AIDrop/stalker_meshy_stalker_v01_rigged.fbx";

        public const string BurrowerRiggedModelPath =
            Category3Root + "/Burrower/AIDrop/burrower_meshy_burrower_v01_rigged.fbx";

        public const string WardenRiggedModelPath =
            Category3Root + "/Warden/AIDrop/warden_meshy_warden_v01_rigged.fbx";

        public const string ColossusRiggedModelPath =
            Category3Root + "/Colossus/AIDrop/colossus_meshy_colossus_v01_rigged.fbx";

        public const string ControllerFolder = "Assets/Animation/Creeps";
        public const string BruteControllerPath = ControllerFolder + "/Creep_Brute_3D.controller";
        public const string ObsidianBruteControllerPath = ControllerFolder + "/Creep_ObsidianBrute_3D.controller";
        public const string TurretWalkerControllerPath = ControllerFolder + "/Creep_TurretWalker_3D.controller";
        public const string ZephyrControllerPath = ControllerFolder + "/Creep_Zephyr_3D.controller";
        public const string StalkerControllerPath = ControllerFolder + "/Creep_Stalker_3D.controller";
        public const string BurrowerControllerPath = ControllerFolder + "/Creep_Burrower_3D.controller";
        public const string WardenControllerPath = ControllerFolder + "/Creep_Warden_3D.controller";
        public const string ColossusControllerPath = ControllerFolder + "/Creep_Colossus_3D.controller";
        public const string SiegeControllerPath = ControllerFolder + "/Creep_Siege_3D.controller";
        public const string SerpentControllerPath = ControllerFolder + "/Creep_Serpent_3D.controller";
        public const string RunnerControllerPath = ControllerFolder + "/Creep_Runner_3D.controller";

        /// <summary>
        /// Every creep carrying a hand-authored rig, paired with the controller built for it.
        /// </summary>
        /// <remarks>
        /// The quadruped rigs are produced by tools/art_pipeline/rig_quadruped_creep.py, which
        /// holds the measured per-creep bone geometry in its PROFILES table. Meshy only auto-rigs
        /// bipeds, so every quadruped here is rigged by that script instead — and conversely the
        /// Category 3 bipeds need no script at all, arriving with a 24-bone humanoid rig and
        /// clips already authored.
        ///
        /// The last three are not quadrupeds and do not share a script, because they do not share
        /// a body plan: rig_wheeled_ram.py rolls the Siege's four wheels, rig_coiled_serpent.py
        /// runs a wave around the Serpent's coil, and rig_bladed_runner.py beats the Runner's
        /// blades. "Rig the remaining creeps as quadrupeds" was the reading item 11 invited, and
        /// the render check item 4 mandates is what showed none of the three has legs at all.
        /// </remarks>
        private static readonly (string ModelPath, string ControllerPath)[] RiggedCreeps =
        {
            (BruteRiggedModelPath, BruteControllerPath),
            (ObsidianBruteRiggedModelPath, ObsidianBruteControllerPath),
            (TurretWalkerRiggedModelPath, TurretWalkerControllerPath),
            (ZephyrRiggedModelPath, ZephyrControllerPath),
            (StalkerRiggedModelPath, StalkerControllerPath),
            (BurrowerRiggedModelPath, BurrowerControllerPath),
            (WardenRiggedModelPath, WardenControllerPath),
            (ColossusRiggedModelPath, ColossusControllerPath),
            (SiegeRiggedModelPath, SiegeControllerPath),
            (SerpentRiggedModelPath, SerpentControllerPath),
            (RunnerRiggedModelPath, RunnerControllerPath),
        };

        [MenuItem("Line Wards/Art/Configure Rigged Creep Animation")]
        public static void ConfigureRiggedCreeps()
        {
            var ok = true;
            foreach (var (modelPath, controllerPath) in RiggedCreeps)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
                {
                    Debug.LogError($"Missing rigged model at {modelPath}.");
                    ok = false;
                    continue;
                }

                ok &= ConfigureModel(modelPath) && BuildController(modelPath, controllerPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        private static bool ConfigureModel(string modelPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"No model importer at {modelPath}.");
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            // The wrapper pipeline overrides materials with the tuned production material anyway
            // (Creep3DImportPipeline.ApplyMaterial), so importing the FBX's own is just noise.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                Debug.LogError($"{modelPath} carries no animation clips; the rig export did not include the action.");
                return false;
            }

            for (var index = 0; index < clips.Length; index++)
            {
                clips[index].name = "Walk";
                clips[index].loopTime = true;
                clips[index].loopPose = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log($"Configured rigged model {Path.GetFileName(modelPath)}: Generic rig, {clips.Length} looping clip(s).");
            return true;
        }

        private static bool BuildController(string modelPath, string controllerPath)
        {
            Creep3DImportPipeline.EnsureFolder(ControllerFolder);

            AnimationClip walk = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    walk = clip;
                    break;
                }
            }

            if (walk == null)
            {
                Debug.LogError($"No AnimationClip sub-asset found in {modelPath}.");
                return false;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var state = controller.layers[0].stateMachine.AddState("Walk");
            state.motion = walk;
            controller.layers[0].stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            Debug.Log($"Built animator controller {controllerPath} with clip '{walk.name}'.");
            return true;
        }
    }
}
