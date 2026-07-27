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
    /// The Meshy source models ship unrigged, so rigs are authored separately (see
    /// tools/art_pipeline/rig_quadruped_creep.py) and re-exported alongside the prepared mesh.
    /// A freshly imported rigged FBX defaults to no rig and non-looping clips, which reads as a
    /// creature that animates once and then freezes, so both have to be set explicitly here
    /// rather than left to the importer defaults.
    /// </remarks>
    public static class RiggedCreepSetup
    {
        public const string BruteRiggedModelPath =
            "Assets/Art/AIStaging/Models/Creeps/Brute/AIDrop/brute_meshy_rock_golem_blend_0725021355_prepared_rigged.fbx";

        public const string ControllerFolder = "Assets/Animation/Creeps";
        public const string BruteControllerPath = ControllerFolder + "/Creep_Brute_3D.controller";

        [MenuItem("Line Wards/Art/Configure Rigged Creep Animation")]
        public static void ConfigureRiggedCreeps()
        {
            var ok = ConfigureModel(BruteRiggedModelPath) && BuildController(BruteRiggedModelPath, BruteControllerPath);
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
