using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Configures the rigged Builder Bot FBX pair (a mesh+skeleton file and a separate
    /// mesh+skeleton+clip file, Meshy's per-animation export convention) and assembles the
    /// runtime prefab: skinned mesh, PBR material, and an Animator with Idle/Walk states.
    /// </summary>
    /// <remarks>
    /// Meshy split this rig across two FBX files that each carry their own copy of the same mesh
    /// and 24-bone skeleton — CharacterModelPath is the rest-pose copy (used for the mesh/material),
    /// WalkModelPath's only useful contents are its AnimationClip sub-asset. Reusing a clip from a
    /// different FBX than the one supplying the mesh works here because Generic-rig Animators don't
    /// care which file a clip came from, only that the bone names/hierarchy match — which they do,
    /// since both exports came from the same Meshy rigging job.
    /// </remarks>
    public static class BuilderImportPipeline
    {
        public const string CharacterModelPath = "Assets/Art/AIStaging/Models/Builder/AIDrop/builder_bot_character_0728.fbx";
        public const string WalkModelPath = "Assets/Art/AIStaging/Models/Builder/AIDrop/builder_bot_walk_0728.fbx";
        public const string AlbedoTexturePath = "Assets/Art/AIStaging/Models/Builder/AIDrop/builder_bot_albedo_1k_0728.png";
        public const string MetallicSmoothnessTexturePath = "Assets/Art/AIStaging/Models/Builder/AIDrop/builder_bot_metallicsmoothness_0728.png";

        // Under Assets/Resources so the runtime UI script can Resources.Load it directly (there's
        // only one Builder model, unlike towers/creeps which go through a visual-library
        // ScriptableObject to pick between several roles) — resource path is relative to this,
        // i.e. "Prefabs/Builder/Builder_3D".
        public const string RuntimePrefabFolder = "Assets/Resources/Prefabs/Builder";
        public const string RuntimePrefabPath = RuntimePrefabFolder + "/Builder_3D.prefab";
        public const string RuntimePrefabResourcePath = "Prefabs/Builder/Builder_3D";
        public const string MaterialFolder = "Assets/Art/Builder/Production/Materials";
        public const string MaterialPath = MaterialFolder + "/mat_builder_3d_body_v01.mat";
        public const string ControllerFolder = "Assets/Animation/Builder";
        public const string ControllerPath = ControllerFolder + "/Builder_3D.controller";

        [MenuItem("Line Wards/Art/Build Builder 3D")]
        public static void BuildBuilder3D()
        {
            var ok = ConfigureModel(CharacterModelPath, renameClipTo: null)
                && ConfigureModel(WalkModelPath, renameClipTo: "Walk")
                && GenerateWrapper();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        private static bool ConfigureModel(string modelPath, string renameClipTo)
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
            // The wrapper applies its own PBR material built from the separately-repacked
            // textures (see repack step in tools output), so importing the FBX's own is noise.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            if (renameClipTo != null)
            {
                var clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    Debug.LogError($"{modelPath} carries no animation clips; expected a walk cycle.");
                    return false;
                }

                for (var index = 0; index < clips.Length; index++)
                {
                    clips[index].name = renameClipTo;
                    clips[index].loopTime = true;
                    clips[index].loopPose = true;
                }

                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
            return true;
        }

        private static bool GenerateWrapper()
        {
            EnsureFolder(RuntimePrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ControllerFolder);

            var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (characterPrefab == null)
            {
                Debug.LogError($"Missing imported character model at {CharacterModelPath}.");
                return false;
            }

            var walkClip = FindClip(WalkModelPath, "Walk");
            if (walkClip == null)
            {
                Debug.LogError($"No 'Walk' AnimationClip sub-asset found in {WalkModelPath}.");
                return false;
            }

            var material = CreateMaterial();
            var controller = BuildController(walkClip);

            var root = new GameObject("Builder_3D");
            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
            PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modelInstance.name = "Model";
            modelInstance.transform.SetParent(root.transform, false);

            foreach (var renderer in modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterial = material;
            }

            var animator = modelInstance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = modelInstance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            PrefabUtility.SaveAsPrefabAsset(root, RuntimePrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"Generated Builder 3D runtime wrapper: {RuntimePrefabPath}");
            return true;
        }

        private static AnimationClip FindClip(string modelPath, string clipName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is AnimationClip clip && clip.name == clipName)
                {
                    return clip;
                }
            }

            return null;
        }

        private static AnimatorController BuildController(AnimationClip walkClip)
        {
            if (File.Exists(ControllerPath))
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var stateMachine = controller.layers[0].stateMachine;

            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);

            // Idle has no motion at all — the rig's own rest pose (from the character FBX, the
            // same pose Animator falls back to with a null motion) reads fine as a stationary
            // construction bot; there's no separate idle clip to reach for.
            var idleState = stateMachine.AddState("Idle");
            idleState.motion = null;
            stateMachine.defaultState = idleState;

            var walkState = stateMachine.AddState("Walk");
            walkState.motion = walkClip;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.1f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0, "Walking");

            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.1f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Material CreateMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                // Hand-tuned after creation (see the CreateBodyMaterial guard this mirrors in
                // Tower3DImportPipeline/Creep3DImportPipeline) — regenerating the wrapper must
                // never clobber it back to recipe defaults.
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "mat_builder_3d_body_v01" };

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoTexturePath);
            var metallicSmoothness = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicSmoothnessTexturePath);

            if (albedo != null && material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", albedo);
            }

            if (metallicSmoothness != null)
            {
                if (material.HasProperty("_MetallicGlossMap"))
                {
                    material.SetTexture("_MetallicGlossMap", metallicSmoothness);
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                if (material.HasProperty("_SmoothnessTextureChannel"))
                {
                    // 0 = smoothness read from the metallic map's alpha channel, matching how
                    // builder_bot_metallicsmoothness_0728.png was packed.
                    material.SetFloat("_SmoothnessTextureChannel", 0f);
                }
            }

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            var parentFolder = string.IsNullOrEmpty(parent) ? "Assets" : parent;
            var newFolderName = Path.GetFileName(assetPath);
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }
    }
}
