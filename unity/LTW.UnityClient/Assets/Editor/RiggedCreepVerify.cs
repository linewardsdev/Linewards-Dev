using System.Text;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Proves a rigged creep prefab actually animates, by sampling its clip at several times and
    /// reporting whether bone transforms move. A still screenshot cannot show this, and an Animator
    /// component being present does not mean the rig is bound or the clip is driving anything.
    /// </summary>
    public static class RiggedCreepVerify
    {
        /// <summary>Every rigged creep prefab, kept in step with RiggedCreepSetup.RiggedCreeps.</summary>
        private static readonly string[] RiggedCreepPrefabs =
        {
            "Assets/Prefabs/Creeps/Creep_Brute_3D.prefab",
            "Assets/Prefabs/Creeps/Creep_ObsidianBrute_3D.prefab",
            "Assets/Prefabs/Creeps/Creep_TurretWalker_3D.prefab",
        };

        [MenuItem("Line Wars/Art/Verify Rigged Creep Animation")]
        public static void VerifyRiggedCreeps()
        {
            var allOk = true;
            foreach (var path in RiggedCreepPrefabs)
            {
                Debug.Log($"RIGVERIFY ---- {path} ----");
                allOk &= VerifyPrefab(path);
            }

            Debug.Log(allOk ? "RIGVERIFY ALL=PASS" : "RIGVERIFY ALL=FAIL");
            Exit(allOk ? 0 : 1);
        }

        private static bool VerifyPrefab(string prefabPath)
        {
            var ok = true;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"RIGVERIFY missing prefab {prefabPath}");
                return false;

            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            var skinned = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned == null)
            {
                Debug.LogError("RIGVERIFY no SkinnedMeshRenderer — the mesh is not skinned to a rig.");
                ok = false;
            }
            else
            {
                Debug.Log($"RIGVERIFY skinnedMesh='{skinned.name}' bones={skinned.bones.Length} " +
                          $"rootBone='{(skinned.rootBone != null ? skinned.rootBone.name : "<none>")}'");
                if (skinned.bones.Length == 0)
                {
                    Debug.LogError("RIGVERIFY skinned renderer has zero bones.");
                    ok = false;
                }
            }

            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogError("RIGVERIFY no Animator or no controller bound.");
                ok = false;
                Object.DestroyImmediate(instance);
                return ok;

            }

            var controller = animator.runtimeAnimatorController;
            Debug.Log($"RIGVERIFY controller='{controller.name}' clips={controller.animationClips.Length}");
            if (controller.animationClips.Length == 0)
            {
                Debug.LogError("RIGVERIFY controller has no clips.");
                ok = false;
            }

            // Sample the clip at several normalised times and record bone positions. If the rig is
            // genuinely driven, these differ; if the clip is empty or unbound, they will not.
            if (skinned != null && skinned.bones.Length > 0 && controller.animationClips.Length > 0)
            {
                var clip = controller.animationClips[0];
                Debug.Log($"RIGVERIFY clip='{clip.name}' length={clip.length:F3}s loop={clip.isLooping} frameRate={clip.frameRate}");

                // Sample against the Animator's own GameObject, not the wrapper root: clip curve
                // paths are stored relative to the FBX root that the Animator sits on, so sampling
                // from a parent silently matches nothing and every bone reads as static.
                var sampleRoot = animator.gameObject;
                Debug.Log($"RIGVERIFY sampleRoot='{sampleRoot.name}'");

                var samples = new[] { 0f, 0.25f, 0.5f, 0.75f };
                var positions = new Vector3[samples.Length][];
                for (var s = 0; s < samples.Length; s++)
                {
                    clip.SampleAnimation(sampleRoot, samples[s] * clip.length);
                    positions[s] = new Vector3[skinned.bones.Length];
                    for (var b = 0; b < skinned.bones.Length; b++)
                    {
                        positions[s][b] = skinned.bones[b] != null
                            ? skinned.bones[b].localPosition + skinned.bones[b].localEulerAngles
                            : Vector3.zero;
                    }
                }

                var report = new StringBuilder();
                var movedBones = 0;
                for (var b = 0; b < skinned.bones.Length; b++)
                {
                    var maxDelta = 0f;
                    for (var s = 1; s < samples.Length; s++)
                    {
                        maxDelta = Mathf.Max(maxDelta, Vector3.Distance(positions[0][b], positions[s][b]));
                    }

                    var name = skinned.bones[b] != null ? skinned.bones[b].name : "<null>";
                    report.AppendLine($"RIGVERIFY   bone '{name}' maxDelta={maxDelta:F4}");
                    if (maxDelta > 0.01f)
                    {
                        movedBones++;
                    }
                }

                Debug.Log(report.ToString().TrimEnd());
                Debug.Log($"RIGVERIFY bonesThatMove={movedBones}/{skinned.bones.Length}");
                if (movedBones == 0)
                {
                    Debug.LogError("RIGVERIFY no bone moved across the clip — animation is not driving the rig.");
                    ok = false;
                }
            }

            Object.DestroyImmediate(instance);
            Debug.Log(ok ? "RIGVERIFY RESULT=PASS" : "RIGVERIFY RESULT=FAIL");
            return ok;
        }

        private static void Exit(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
