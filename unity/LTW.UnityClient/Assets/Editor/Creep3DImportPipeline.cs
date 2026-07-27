using System.IO;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Builds runtime creep wrappers around the AI-generated 3D meshes and points
    /// CreepVisualLibrary at them. Mirrors <see cref="Tower3DImportPipeline"/>, which does the
    /// same job for towers.
    /// </summary>
    public static class Creep3DImportPipeline
    {
        public const string LibraryPath = "Assets/Resources/CreepVisualLibrary.asset";
        public const string RuntimePrefabFolder = "Assets/Prefabs/Creeps";
        public const string MaterialFolder = "Assets/Art/Creeps/Production/Materials";

        private const string BodyChildName = "Body";
        private const string ImportedVisualName = "Imported3DVisual";
        private const string TintAnchorName = "BodyTintAnchor";
        private const string SenderAccentName = "SenderAccent";

        public static bool GenerateWrapper(Creep3DImportSpec spec)
        {
            var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourceModelPath);
            if (sourceModel == null)
            {
                Debug.LogWarning($"Skipping {spec.DisplayName}: missing source model at {spec.SourceModelPath}.");
                return false;
            }

            EnsureFolder(RuntimePrefabFolder);
            EnsureFolder(MaterialFolder);

            var bodyMaterial = CreateBodyMaterial(spec);
            if (bodyMaterial == null)
            {
                return false;
            }

            var root = new GameObject($"Creep_{spec.DisplayName}_3D");
            var body = CreateEmptyChild(root, BodyChildName, Vector3.zero);

            var instance = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
            if (instance == null)
            {
                Object.DestroyImmediate(root);
                Debug.LogWarning($"Skipping {spec.DisplayName}: failed to instantiate {spec.SourceModelPath}.");
                return false;
            }

            instance.name = ImportedVisualName;
            instance.transform.SetParent(body.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(spec.ImportEulerAngles);
            instance.transform.localScale = Vector3.one * spec.ImportScale;

            StripColliders(instance);
            ApplyMaterial(instance, bodyMaterial);
            SeatOnGround(instance);
            AttachAnimator(instance, spec);

            // The library tints whatever sits under bodyRendererPath. Pointing it at an empty
            // anchor keeps the team colour from multiplying into the baked albedo, which is how
            // the tower wrappers avoid washing their meshes out.
            CreateEmptyChild(root, TintAnchorName, Vector3.zero);
            CreateSenderAccent(root, spec);

            PrefabUtility.SaveAsPrefabAsset(root, spec.RuntimePrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"Generated {spec.DisplayName} 3D creep wrapper: {spec.RuntimePrefabPath}");
            return true;
        }

        public static void UpsertProfile(SerializedProperty profiles, Creep3DImportSpec spec)
        {
            var profileIndex = -1;
            for (var index = 0; index < profiles.arraySize; index++)
            {
                if (profiles.GetArrayElementAtIndex(index).FindPropertyRelative("creepId").stringValue == spec.CreepId)
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

            var profile = profiles.GetArrayElementAtIndex(profileIndex);
            profile.FindPropertyRelative("creepId").stringValue = spec.CreepId;
            profile.FindPropertyRelative("role").enumValueIndex = (int)spec.Role;
            profile.FindPropertyRelative("prefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(spec.RuntimePrefabPath);
            profile.FindPropertyRelative("scale").vector3Value = spec.RuntimeScale;
            profile.FindPropertyRelative("motionStyle").enumValueIndex = (int)spec.MotionStyle;
            profile.FindPropertyRelative("bodyRendererPath").stringValue = TintAnchorName;
            profile.FindPropertyRelative("deathCueStyle").enumValueIndex = (int)spec.DeathCueStyle;

            var accents = profile.FindPropertyRelative("senderAccentRendererPaths");
            accents.arraySize = 1;
            accents.GetArrayElementAtIndex(0).stringValue = SenderAccentName;

            profile.FindPropertyRelative("damageRendererPaths").arraySize = 0;
        }

        public static int ValidateWrapper(Creep3DImportSpec spec)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RuntimePrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing creep 3D wrapper for {spec.DisplayName}: {spec.RuntimePrefabPath}.");
                return 1;
            }

            var issues = 0;
            issues += ValidateChild(prefab, BodyChildName, requireRenderer: true);
            issues += ValidateChild(prefab, TintAnchorName, requireRenderer: false);
            issues += ValidateChild(prefab, SenderAccentName, requireRenderer: true);

            if (prefab.GetComponentInChildren<Collider>(true) != null)
            {
                Debug.LogError($"{spec.DisplayName} wrapper still carries a collider.");
                issues++;
            }

            return issues;
        }

        private static Material CreateBodyMaterial(Creep3DImportSpec spec)
        {
            var materialPathForExistingCheck = $"{MaterialFolder}/mat_creep_{spec.DisplayName.ToLowerInvariant()}_3d_body_v01.mat";
            var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPathForExistingCheck);
            if (existingMaterial != null)
            {
                // Body material is hand-tuned after creation (smoothness, parallax, etc.).
                // Regenerating the wrapper must not reset those tuned values back to this recipe's
                // defaults — see the equivalent fix in Tower3DImportPipeline.CreateBodyMaterial.
                return existingMaterial;
            }

            var baseColor = AssetDatabase.LoadAssetAtPath<Texture>($"{spec.TextureFolder}/Baked_BaseColor.png");
            if (baseColor == null)
            {
                Debug.LogWarning($"Skipping {spec.DisplayName}: missing base colour map in {spec.TextureFolder}.");
                return null;
            }

            var material = new Material(LTW.UnityClient.Simulation.RenderCompat.Lit)
            {
                name = $"mat_creep_{spec.DisplayName.ToLowerInvariant()}_3d_body_v01",
            };

            // Property names differ per pipeline: URP/Lit uses _BaseMap and _BaseColor where
            // Built-in Standard uses _MainTex and _Color. Set whichever the shader exposes so a
            // regenerated creep is textured under either.
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseColor);
            RenderCompat.SetAlbedo(material, Color.white);

            // Unity's metallic-smoothness packing, produced by
            // tools/art_pipeline/repack_metallic_smoothness.py from the glTF source map.
            var metallic = AssetDatabase.LoadAssetAtPath<Texture>($"{spec.TextureFolder}/Baked_MetallicSmoothness.png");
            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.SetFloat("_GlossMapScale", 1f);
                material.EnableKeyword(RenderCompat.UsingScriptablePipeline ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
            }

            var emission = AssetDatabase.LoadAssetAtPath<Texture>($"{spec.TextureFolder}/Baked_Emit.png");
            if (emission != null)
            {
                material.SetTexture("_EmissionMap", emission);
                material.SetColor("_EmissionColor", Color.white);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            var materialPath = $"{MaterialFolder}/{material.name}.mat";
            AssetDatabase.DeleteAsset(materialPath);
            AssetDatabase.CreateAsset(material, materialPath);
            return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }

        /// <summary>
        /// Binds an Animator to a rigged creep's imported visual. Skipped entirely for the static
        /// meshes, which have no rig and keep their procedural motion instead.
        /// </summary>
        private static void AttachAnimator(GameObject instance, Creep3DImportSpec spec)
        {
            if (string.IsNullOrWhiteSpace(spec.AnimatorControllerPath))
            {
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(spec.AnimatorControllerPath);
            if (controller == null)
            {
                Debug.LogWarning($"{spec.DisplayName}: no animator controller at {spec.AnimatorControllerPath}; leaving unanimated.");
                return;
            }

            if (instance.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            {
                Debug.LogWarning($"{spec.DisplayName}: animator controller specified but the model has no SkinnedMeshRenderer, so it carries no rig. Leaving unanimated.");
                return;
            }

            // Not `?? AddComponent`: Unity's overloaded equality makes a missing component compare
            // equal to null without being C# null, so the null-coalescing operator skips right
            // past it and hands back an unusable object.
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // Creeps are driven along the lane by the simulation, and are frequently off-screen or
            // culled behind the HUD; culling updates keeps the skinning cost off those frames.
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            Debug.Log($"{spec.DisplayName}: attached Animator with {controller.name}.");
        }

        private static void CreateSenderAccent(GameObject root, Creep3DImportSpec spec)
        {
            // Carries the sender colour, since the mesh itself is no longer tinted.
            //
            // This was a solid cylinder, which rendered as an opaque hard-edged plate parked on the
            // board under the creep — it read as a coloured coaster rather than as the unit giving
            // off light. It is now a flat quad running the LTW/Contact Shadow shader, whose radial
            // falloff turns the same colour into a soft pool that fades out at its rim.
            var accent = GameObject.CreatePrimitive(PrimitiveType.Quad);
            accent.name = SenderAccentName;
            accent.transform.SetParent(root.transform, false);
            accent.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            accent.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            accent.transform.localScale = new Vector3(spec.AccentRadius * 1.85f, spec.AccentRadius * 1.85f, 1f);
            StripColliders(accent);
            accent.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var materialPath = $"{MaterialFolder}/mat_creep_{spec.DisplayName.ToLowerInvariant()}_3d_sender_v01.mat";
            var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existingMaterial != null)
            {
                // Same reasoning as CreateBodyMaterial: regenerating the wrapper must not reset an
                // already-tuned material back to this recipe's defaults.
                accent.GetComponent<Renderer>().sharedMaterial = existingMaterial;
                return;
            }

            // Soft radial falloff rather than a lit surface: the runtime writes the sender colour
            // straight into _Color, and the shader fades it to nothing at the rim so the accent
            // reads as light pooling under the creep instead of a disc lying on the board.
            var shader = Shader.Find("LTW/Contact Shadow") ?? LTW.UnityClient.Simulation.RenderCompat.Lit;
            var material = new Material(shader)
            {
                name = $"mat_creep_{spec.DisplayName.ToLowerInvariant()}_3d_sender_v01",
            };
            var accentColor = new Color(0.078f, 0.086f, 0.11f, 0.55f);
            material.SetColor("_Color", accentColor);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", accentColor);
            if (material.HasProperty("_Softness")) material.SetFloat("_Softness", 0.85f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            AssetDatabase.CreateAsset(material, materialPath);
            accent.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }

        /// <summary>
        /// Lifts the imported mesh so its lowest point rests on y = 0.
        /// </summary>
        /// <remarks>
        /// The intake exports each model with its base already at the origin, but any import
        /// rotation pivots about that base and swings part of the mesh below the board. Reseating
        /// from the measured bounds keeps units on the surface whatever rotation a spec asks for.
        /// </remarks>
        private static void SeatOnGround(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            instance.transform.localPosition -= new Vector3(0f, bounds.min.y, 0f);
        }

        private static GameObject CreateEmptyChild(GameObject parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        private static void ApplyMaterial(GameObject instance, Material material)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var slots = new Material[renderers[index].sharedMaterials.Length == 0 ? 1 : renderers[index].sharedMaterials.Length];
                for (var slot = 0; slot < slots.Length; slot++)
                {
                    slots[slot] = material;
                }

                renderers[index].sharedMaterials = slots;
                renderers[index].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[index].receiveShadows = true;
            }
        }

        private static void StripColliders(GameObject instance)
        {
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                Object.DestroyImmediate(colliders[index], true);
            }
        }

        private static int ValidateChild(GameObject prefab, string childName, bool requireRenderer)
        {
            var child = prefab.transform.Find(childName);
            if (child == null)
            {
                Debug.LogError($"{prefab.name} is missing child '{childName}'.");
                return 1;
            }

            if (requireRenderer && child.GetComponentInChildren<Renderer>(true) == null)
            {
                Debug.LogError($"{prefab.name} child '{childName}' has no renderer.");
                return 1;
            }

            return 0;
        }

        public static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }
    }

    public readonly struct Creep3DImportSpec
    {
        public Creep3DImportSpec(
            string displayName,
            string creepId,
            CreepVisualRole role,
            CreepVisualMotionStyle motionStyle,
            CreepDeathCueStyle deathCueStyle,
            string sourceModelPath,
            string textureFolder,
            string runtimePrefabPath,
            Vector3 runtimeScale,
            float importScale,
            Vector3 importEulerAngles,
            float accentRadius,
            string animatorControllerPath = null)
        {
            DisplayName = displayName;
            CreepId = creepId;
            Role = role;
            MotionStyle = motionStyle;
            DeathCueStyle = deathCueStyle;
            SourceModelPath = sourceModelPath;
            TextureFolder = textureFolder;
            RuntimePrefabPath = runtimePrefabPath;
            RuntimeScale = runtimeScale;
            ImportScale = importScale;
            ImportEulerAngles = importEulerAngles;
            AccentRadius = accentRadius;
            AnimatorControllerPath = animatorControllerPath;
        }

        /// <summary>
        /// Optional. Set when the source model carries a skeletal rig and animation clips; the
        /// wrapper then gets an Animator bound to this controller. Null for the static meshes,
        /// which stay on the procedural motion in UnityVerticalSliceRenderer.
        /// </summary>
        public string AnimatorControllerPath { get; }

        public string DisplayName { get; }

        public string CreepId { get; }

        public CreepVisualRole Role { get; }

        public CreepVisualMotionStyle MotionStyle { get; }

        public CreepDeathCueStyle DeathCueStyle { get; }

        public string SourceModelPath { get; }

        public string TextureFolder { get; }

        public string RuntimePrefabPath { get; }

        public Vector3 RuntimeScale { get; }

        public float ImportScale { get; }

        public Vector3 ImportEulerAngles { get; }

        public float AccentRadius { get; }
    }
}
