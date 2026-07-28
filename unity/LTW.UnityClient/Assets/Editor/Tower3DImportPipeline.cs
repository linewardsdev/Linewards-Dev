using System;
using System.IO;
using LTW.UnityClient.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LTW.UnityClient.Editor
{
    public static class Tower3DImportPipeline
    {
        public const string LibraryPath = "Assets/Resources/TowerVisualLibrary.asset";
        public const string RuntimePrefabFolder = "Assets/Prefabs/Towers";
        public const string MaterialFolder = "Assets/Art/Towers/Production/Materials";
        public const string SourceNotesFolder = "Assets/Art/Towers/Production/SourceNotes";
        private static readonly Color BodyColor = new(0.82f, 0.95f, 1f, 1f);
        private static readonly Color TrimColor = new(0.92f, 0.68f, 0.22f, 1f);
        private static readonly Color EnergyColor = new(0.28f, 0.82f, 1f, 1f);
        private static readonly Color OwnerColor = new(0.36f, 1f, 0.78f, 1f);
        private static readonly Color RangeColor = new(0.24f, 0.62f, 1f, 0.18f);

        public static bool GenerateWrapperIfRawExists(Tower3DImportSpec spec, Material material = null, bool writeSourceNotes = true)
        {
            var rawPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RawPrefabPath);
            if (rawPrefab == null)
            {
                Debug.LogWarning($"Skipping {spec.DisplayName}: missing raw 3D prefab at {spec.RawPrefabPath}.");
                return false;
            }

            if (!HasRenderableMesh(rawPrefab))
            {
                Debug.LogWarning($"Skipping {spec.DisplayName}: raw 3D prefab has no mesh renderers/filters at {spec.RawPrefabPath}.");
                return false;
            }

            EnsureFolder(RuntimePrefabFolder);
            EnsureFolder(MaterialFolder);
            var recipe = CreateMaterialRecipe(spec, rawPrefab, material);

            var root = new GameObject($"Tower_{spec.DisplayName}_3D");
            var body = CreateEmptyChild(root, "Body", Vector3.zero);

            var generatedInstance = PrefabUtility.InstantiatePrefab(rawPrefab) as GameObject;
            if (generatedInstance == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                Debug.LogWarning($"Skipping {spec.DisplayName}: failed to instantiate raw 3D prefab.");
                return false;
            }

            // Fully disconnect from the source FBX prefab before any structural editing. Unity
            // silently reverts an attempt to reparent a NESTED child of a still-connected prefab
            // instance to somewhere outside it (e.g. moving a split Head mesh onto a fresh
            // HeadPivot below) — reparenting the instance's own root elsewhere works fine, which
            // is why that always worked, but a deeper child does not without unpacking first. This
            // whole hierarchy is disposable scratch used only to assemble the new runtime prefab,
            // so unpacking has no downside here.
            PrefabUtility.UnpackPrefabInstance(generatedInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            generatedInstance.name = "Imported3DVisual";
            generatedInstance.transform.SetParent(body.transform, false);
            generatedInstance.transform.localPosition = spec.ImportPosition;
            generatedInstance.transform.localRotation = spec.ImportRotation;
            generatedInstance.transform.localScale = spec.ImportScale;

            StripColliders(generatedInstance);
            if (spec.PreserveSourceMaterials)
            {
                NormalizeRendererPolicy(generatedInstance);
            }
            else
            {
                ApplyRuntimeMaterial(generatedInstance, recipe.BodyMaterial);
            }

            // Towers with an independently-aimed/recoiling part (a turret Head, or — for
            // Prism/Relay — the whole crystal cluster / dish assembly, split via
            // split_tower_rigid_part.py) still sit deep in the imported hierarchy
            // (Imported3DVisual/.../Head, .../Spire, or .../Dish) and carry whatever non-identity
            // rest transform that hierarchy's own axis/scale correction bakes in — unlike Body,
            // which is a purpose-built empty with an identity rest transform by construction.
            // Rather than have runtime code reason about that opaque rest frame, give it the same
            // clean-empty treatment as Body: a fresh "HeadPivot" child of Body (identity rest
            // transform), with the actual mesh reparented under it using worldPositionStays so its
            // visual position/orientation doesn't move, only its point of reference does. "Spire"
            // and "Dish" are also UpdateTowerMotion's continuous-spin part names — nesting either
            // under HeadPivot doesn't interfere with that, since the spin is applied to the part's
            // own local rotation on top of whatever HeadPivot is doing.
            var headMesh = FindDeepChild(generatedInstance.transform, "Head")
                ?? FindDeepChild(generatedInstance.transform, "Spire")
                ?? FindDeepChild(generatedInstance.transform, "Dish");
            if (headMesh != null)
            {
                var headPivot = CreateEmptyChild(body, "HeadPivot", Vector3.zero);
                headMesh.SetParent(headPivot.transform, worldPositionStays: true);
            }

            CreateEmptyChild(root, "BodyTintAnchor", Vector3.zero);
            CreateRoleMarker(root, spec, recipe.EnergyMaterial);
            CreateOwnerTrim(root, recipe.OwnerMaterial);
            CreateRangeHalo(root, recipe.RangeMaterial);

            // Anchors are parented under Body, not root: UnityVerticalSliceRenderer.UpdateTowerMotion
            // rotates Body to aim at the tower's current target, and an anchor parented under the
            // never-rotating root would then point at a stale direction the moment the tower turned
            // — visually detaching the muzzle flash from an already-rotated model. Body sits at
            // root's local origin with identity rotation at rest, so this reparenting does not move
            // any anchor's resting position; it only makes them inherit Body's rotation going forward.
            for (var index = 0; index < spec.AnchorNames.Length; index++)
            {
                var anchorName = spec.AnchorNames[index];
                CreateEmptyChild(body, anchorName, ResolveAnchorPosition(spec, anchorName));
            }

            PrefabUtility.SaveAsPrefabAsset(root, spec.RuntimePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            if (writeSourceNotes)
            {
                WriteSourceNotes(spec);
            }

            Debug.Log($"Generated {spec.DisplayName} 3D runtime wrapper: {spec.RuntimePrefabPath}");
            return true;
        }

        public static int ValidateWrapper(Tower3DImportSpec spec, bool requireExists, bool requireBoardMaterial = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RuntimePrefabPath);
            if (prefab == null)
            {
                if (requireExists)
                {
                    Debug.LogError($"Missing required Tower 3D wrapper for {spec.DisplayName}: {spec.RuntimePrefabPath}.");
                    return 1;
                }

                Debug.LogWarning($"Tower 3D wrapper not yet generated for {spec.DisplayName}: {spec.RuntimePrefabPath}.");
                return 0;
            }

            var issueCount = 0;
            issueCount += ValidateChild(prefab, "Body", requireRenderer: true);
            issueCount += ValidateChild(prefab, "BodyTintAnchor", requireRenderer: false);
            issueCount += ValidateChild(prefab, "RoleMarker", requireRenderer: false);
            issueCount += ValidateChild(prefab, "OwnerTrim", requireRenderer: false);
            issueCount += ValidateChild(prefab, "RangeHalo", requireRenderer: false);

            for (var index = 0; index < spec.AnchorNames.Length; index++)
            {
                // Anchors live under Body now, not at the prefab root — see the comment where
                // they're created in GenerateWrapperIfRawExists.
                issueCount += ValidateChild(prefab, $"Body/{spec.AnchorNames[index]}", requireRenderer: false);
            }

            issueCount += ValidateNoRuntimeColliders(prefab);
            issueCount += ValidateRendererPolicy(prefab, spec, requireBoardMaterial);
            issueCount += ValidateBounds(prefab, spec);
            return issueCount;
        }

        public static void UpsertProfile(SerializedProperty profiles, Tower3DImportSpec spec)
        {
            var profileIndex = -1;
            for (var index = 0; index < profiles.arraySize; index++)
            {
                if (profiles.GetArrayElementAtIndex(index).FindPropertyRelative("towerId").stringValue == spec.TowerId)
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
            profile.FindPropertyRelative("towerId").stringValue = spec.TowerId;
            profile.FindPropertyRelative("role").enumValueIndex = (int)spec.Role;
            profile.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RuntimePrefabPath);
            profile.FindPropertyRelative("scale").vector3Value = spec.RuntimeScale;
            profile.FindPropertyRelative("lift").floatValue = spec.RuntimeLift;
            profile.FindPropertyRelative("bodyRendererPath").stringValue = "BodyTintAnchor";
            profile.FindPropertyRelative("roleMarkerRendererPath").stringValue = "RoleMarker";
            profile.FindPropertyRelative("ownerTrimRendererPath").stringValue = "OwnerTrim";
            profile.FindPropertyRelative("rangeHaloRendererPath").stringValue = "RangeHalo";
        }

        public static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            var folder = Path.GetFileName(assetPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static bool HasRenderableMesh(GameObject prefab) =>
            prefab.GetComponentsInChildren<MeshRenderer>(true).Length > 0 &&
            prefab.GetComponentsInChildren<MeshFilter>(true).Length > 0;

        private static void StripColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(colliders[index]);
            }
        }

        private static void ApplyRuntimeMaterial(GameObject root, Material material)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                var materials = renderer.sharedMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    materials[materialIndex] = material;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void NormalizeRendererPolicy(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var materials = renderer.sharedMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (materials[materialIndex] != null)
                    {
                        ConfigurePreservedSourceMaterial(materials[materialIndex]);
                    }
                }
            }
        }

        private static void ConfigurePreservedSourceMaterial(Material material)
        {
            material.enableInstancing = true;
            material.renderQueue = -1;
            material.SetOverrideTag("RenderType", string.Empty);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(material);
        }

        private static void CreateRoleMarker(GameObject parent, Tower3DImportSpec spec, Material material)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            child.name = "RoleMarker";
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = spec.CorePosition + new Vector3(0f, 0.08f, 0f);
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = new Vector3(0.16f, 0.08f, 0.16f);
            RemoveCollider(child);

            if (child.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>
        /// Owner colour pooled under the tower. Was a solid cylinder, which read as an opaque
        /// coloured plate sitting on the board; it is now a flat quad so the soft-falloff material
        /// can fade it out at the rim. See <see cref="CreateSenderAccent"/>'s counterpart in the
        /// creep pipeline, which had the same problem.
        /// </summary>
        private static void CreateOwnerTrim(GameObject parent, Material material)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Quad);
            child.name = "OwnerTrim";
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(0f, -0.045f, 0f);
            child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            child.transform.localScale = new Vector3(1.05f, 1.05f, 1f);
            RemoveCollider(child);

            if (child.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void CreateRangeHalo(GameObject parent, Material material)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            child.name = "RangeHalo";
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = new Vector3(0.92f, 0.008f, 0.92f);
            RemoveCollider(child);

            if (child.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                renderer.enabled = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void RemoveCollider(GameObject child)
        {
            if (child.TryGetComponent<Collider>(out var collider))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static GameObject CreateEmptyChild(GameObject parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Vector3 ResolveAnchorPosition(Tower3DImportSpec spec, string anchorName)
        {
            if (anchorName == "Muzzle" || anchorName == "PulseEmitter" || anchorName == "BeamAnchor")
            {
                return spec.MuzzlePosition;
            }

            if (anchorName == "Lens" ||
                anchorName == "ControlCore" ||
                anchorName == "RelayCore" ||
                anchorName == "PulseCore" ||
                anchorName == "PrismLens")
            {
                return spec.CorePosition;
            }

            if (anchorName == "BowLeft")
            {
                return new Vector3(-0.36f, spec.CorePosition.y, 0.08f);
            }

            if (anchorName == "BowRight")
            {
                return new Vector3(0.36f, spec.CorePosition.y, 0.08f);
            }

            if (anchorName == "ControlRing" || anchorName == "PulseRingA")
            {
                return new Vector3(0f, spec.CorePosition.y - 0.02f, 0f);
            }

            if (anchorName == "RelayMast" || anchorName == "RelaySignal" || anchorName == "PrismSpire")
            {
                return new Vector3(0f, spec.CorePosition.y + 0.18f, 0f);
            }

            return Vector3.zero;
        }

        private static int ValidateChild(GameObject prefab, string childPath, bool requireRenderer)
        {
            var child = prefab.transform.Find(childPath);
            if (child == null)
            {
                Debug.LogError($"{prefab.name} is missing child '{childPath}'.", prefab);
                return 1;
            }

            if (requireRenderer && child.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                Debug.LogError($"{prefab.name} child '{childPath}' has no renderer.", prefab);
                return 1;
            }

            return 0;
        }

        private static int ValidateNoRuntimeColliders(GameObject prefab)
        {
            var issueCount = 0;
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                if (colliders[index].transform.name == "RangeHalo")
                {
                    continue;
                }

                Debug.LogError($"{prefab.name} has visual-wrapper collider '{colliders[index].name}'. Runtime 3D tower visuals should not carry colliders.", prefab);
                issueCount++;
            }

            return issueCount;
        }

        private static int ValidateRendererPolicy(GameObject prefab, Tower3DImportSpec spec, bool requireBoardMaterial)
        {
            var issueCount = 0;
            var expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath(spec));
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);

            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer.name == "RangeHalo" || renderer.name == "RoleMarker" || renderer.name == "OwnerTrim")
                {
                    continue;
                }

                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    Debug.LogError($"{spec.DisplayName} renderer '{renderer.name}' casts shadows. Runtime tower 3D wrappers should disable shadows.", prefab);
                    issueCount++;
                }

                if (renderer.receiveShadows)
                {
                    Debug.LogError($"{spec.DisplayName} renderer '{renderer.name}' receives shadows. Runtime tower 3D wrappers should disable shadow receive.", prefab);
                    issueCount++;
                }

                if (spec.PreserveSourceMaterials || !requireBoardMaterial || expectedMaterial == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (materials[materialIndex] != expectedMaterial)
                    {
                        Debug.LogError($"{spec.DisplayName} renderer '{renderer.name}' does not use the expected board-safe body material.", prefab);
                        issueCount++;
                        break;
                    }
                }
            }

            return issueCount;
        }

        private static int ValidateBounds(GameObject prefab, Tower3DImportSpec spec)
        {
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            var found = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].name == "RangeHalo")
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderers[index].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            if (!found)
            {
                Debug.LogError($"{prefab.name} has no mesh renderer bounds to validate.", prefab);
                return 1;
            }

            var scaledSize = Vector3.Scale(bounds.size, spec.RuntimeScale);
            if (scaledSize.x > 2.25f || scaledSize.z > 2.25f || scaledSize.y > 3.25f)
            {
                Debug.LogWarning($"{spec.DisplayName} 3D wrapper bounds may be too large for one-cell mobile read after profile scale: {scaledSize}.", prefab);
            }

            return 0;
        }

        private static Tower3DMaterialRecipe CreateMaterialRecipe(Tower3DImportSpec spec, GameObject rawPrefab, Material bodyMaterialOverride)
        {
            var sourceTexture = FindSourceAlbedo(rawPrefab);
            return new Tower3DMaterialRecipe(
                bodyMaterialOverride != null
                    ? ConfigureBodyMaterial(bodyMaterialOverride, BodyColor, sourceTexture, spec.PreserveSourceAlpha)
                    : CreateBodyMaterial(BodyMaterialPath(spec), BodyColor, sourceTexture, spec.PreserveSourceAlpha),
                CreateOpaqueMaterial(BucketMaterialPath(spec, "trim"), TrimColor, null),
                CreateOpaqueMaterial(BucketMaterialPath(spec, "energy"), EnergyColor, null, emission: new Color(0.08f, 0.18f, 0.24f, 1f)),
                CreateSoftPoolMaterial(BucketMaterialPath(spec, "owner"), OwnerColor),
                CreateTransparentMaterial(BucketMaterialPath(spec, "range_halo"), RangeColor));
        }

        private static Texture FindSourceAlbedo(GameObject rawPrefab)
        {
            var renderers = rawPrefab.GetComponentsInChildren<MeshRenderer>(true);
            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var materials = renderers[rendererIndex].sharedMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    var material = materials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    if (material.mainTexture != null)
                    {
                        return material.mainTexture;
                    }

                    if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
                    {
                        return material.GetTexture("_BaseMap");
                    }
                }
            }

            return null;
        }

        private static string BodyMaterialPath(Tower3DImportSpec spec) =>
            BucketMaterialPath(spec, "body_runtime");

        private static string BucketMaterialPath(Tower3DImportSpec spec, string bucket)
        {
            var safeTowerId = spec.TowerId.Replace(".", "_").Replace("/", "_").ToLowerInvariant();
            return $"{MaterialFolder}/mat_{safeTowerId}_3d_{bucket}_v01.mat";
        }

        private static Material CreateBodyMaterial(string path, Color color, Texture mainTexture, bool preserveAlpha)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace("\\", "/") ?? MaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                // Body material is hand-tuned per role after creation (color, emission, smoothness).
                // Regenerating the wrapper must not reset those tuned values back to the generic recipe defaults.
                return material;
            }

            material = new Material(LTW.UnityClient.Simulation.RenderCompat.Lit);
            AssetDatabase.CreateAsset(material, path);
            return ConfigureBodyMaterial(material, color, mainTexture, preserveAlpha);
        }

        private static Material ConfigureBodyMaterial(Material material, Color color, Texture mainTexture, bool preserveAlpha) =>
            preserveAlpha
                ? ConfigureTransparentMaterial(material, color, mainTexture)
                : ConfigureOpaqueMaterial(material, color, mainTexture);

        /// <summary>
        /// Material for the owner pool under a tower: soft radial falloff so the colour fades out
        /// at its rim instead of ending on a hard circular edge. Preserves an existing asset, since
        /// these get hand-tuned after creation.
        /// </summary>
        private static Material CreateSoftPoolMaterial(string path, Color color)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace("\\", "/") ?? MaterialFolder);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("LTW/Contact Shadow") ?? LTW.UnityClient.Simulation.RenderCompat.Lit;
            var material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            var pooled = new Color(color.r, color.g, color.b, 0.5f);
            material.SetColor("_Color", pooled);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", pooled);
            if (material.HasProperty("_Softness")) material.SetFloat("_Softness", 0.85f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static Material CreateOpaqueMaterial(string path, Color color, Texture mainTexture, Color? emission = null)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace("\\", "/") ?? MaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(LTW.UnityClient.Simulation.RenderCompat.Lit);
                AssetDatabase.CreateAsset(material, path);
            }

            return ConfigureOpaqueMaterial(material, color, mainTexture, emission);
        }

        private static Material ConfigureOpaqueMaterial(Material material, Color color, Texture mainTexture, Color? emission = null)
        {
            material.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(material));
            material.color = color;
            material.mainTexture = mainTexture;
            material.enableInstancing = true;
            material.renderQueue = -1;
            material.SetOverrideTag("RenderType", string.Empty);
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.18f);
            SetFloatIfPresent(material, "_Glossiness", 0.18f);
            SetFloatIfPresent(material, "_GlossyReflections", 0f);
            SetFloatIfPresent(material, "_SpecularHighlights", 0f);
            SetTextureIfPresent(material, "_BaseMap", mainTexture);
            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);
            SetColorIfPresent(material, "_EmissionColor", emission ?? new Color(0.02f, 0.035f, 0.05f, 1f));
            material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTransparentMaterial(string path, Color color)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace("\\", "/") ?? MaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(LTW.UnityClient.Simulation.RenderCompat.Lit);
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = Path.GetFileNameWithoutExtension(path);
            material.color = color;
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.08f);
            SetFloatIfPresent(material, "_Glossiness", 0.08f);
            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material ConfigureTransparentMaterial(Material material, Color color, Texture mainTexture)
        {
            material.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(material));
            material.color = color;
            material.mainTexture = mainTexture;
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.12f);
            SetFloatIfPresent(material, "_Glossiness", 0.12f);
            SetTextureIfPresent(material, "_BaseMap", mainTexture);
            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);
            SetColorIfPresent(material, "_EmissionColor", new Color(0.02f, 0.035f, 0.05f, 1f));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void WriteSourceNotes(Tower3DImportSpec spec)
        {
            EnsureFolder(SourceNotesFolder);
            var path = $"{SourceNotesFolder}/{spec.TowerId.Replace(".", "_")}_3d_pipeline.md";
            var notes =
                $"# {spec.DisplayName} Tower 3D Pipeline Notes\n\n" +
                $"Date: 2026-07-23\n" +
                $"Status: runtime wrapper generated by shared Arrow-derived 3D tower pipeline\n\n" +
                $"## Source\n\n" +
                $"- Tower id: `{spec.TowerId}`\n" +
                $"- Raw generated/imported prefab: `{spec.RawPrefabPath}`\n" +
                $"- Runtime wrapper: `{spec.RuntimePrefabPath}`\n\n" +
                $"## Runtime Contract\n\n" +
                $"- Required children: `Body`, `BodyTintAnchor`, `RoleMarker`, `OwnerTrim`, `RangeHalo`\n" +
                $"- Role anchors: `{string.Join("`, `", spec.AnchorNames)}`\n" +
                $"- Body material: `{BodyMaterialPath(spec)}`\n" +
                $"- Trim material: `{BucketMaterialPath(spec, "trim")}`\n" +
                $"- Energy material: `{BucketMaterialPath(spec, "energy")}`\n" +
                $"- Owner material: `{BucketMaterialPath(spec, "owner")}`\n" +
                $"- Range material: `{BucketMaterialPath(spec, "range_halo")}`\n" +
                $"- Import position: `{spec.ImportPosition}`\n" +
                $"- Import scale: `{spec.ImportScale}`\n" +
                $"- Preserve source alpha: `{spec.PreserveSourceAlpha}`\n" +
                $"- Preserve source materials: `{spec.PreserveSourceMaterials}`\n" +
                $"- Promotion remains separate from wrapper generation.\n";

            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), path), notes);
            AssetDatabase.ImportAsset(path);
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture value)
        {
            if (value != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, value);
            }
        }

        private readonly struct Tower3DMaterialRecipe
        {
            public Tower3DMaterialRecipe(Material bodyMaterial, Material trimMaterial, Material energyMaterial, Material ownerMaterial, Material rangeMaterial)
            {
                BodyMaterial = bodyMaterial;
                TrimMaterial = trimMaterial;
                EnergyMaterial = energyMaterial;
                OwnerMaterial = ownerMaterial;
                RangeMaterial = rangeMaterial;
            }

            public Material BodyMaterial { get; }

            public Material TrimMaterial { get; }

            public Material EnergyMaterial { get; }

            public Material OwnerMaterial { get; }

            public Material RangeMaterial { get; }
        }
    }

    [Serializable]
    public readonly struct Tower3DImportSpec
    {
        public Tower3DImportSpec(
            string displayName,
            string towerId,
            TowerVisualRole role,
            string rawPrefabPath,
            string runtimePrefabPath,
            Vector3 runtimeScale,
            float runtimeLift,
            string[] anchorNames,
            Vector3 muzzlePosition,
            Vector3 corePosition)
            : this(
                displayName,
                towerId,
                role,
                rawPrefabPath,
                runtimePrefabPath,
                runtimeScale,
                runtimeLift,
                anchorNames,
                muzzlePosition,
                corePosition,
                Quaternion.identity,
                Vector3.zero,
                Vector3.one,
                false)
        {
        }

        public Tower3DImportSpec(
            string displayName,
            string towerId,
            TowerVisualRole role,
            string rawPrefabPath,
            string runtimePrefabPath,
            Vector3 runtimeScale,
            float runtimeLift,
            string[] anchorNames,
            Vector3 muzzlePosition,
            Vector3 corePosition,
            Quaternion importRotation)
            : this(
                displayName,
                towerId,
                role,
                rawPrefabPath,
                runtimePrefabPath,
                runtimeScale,
                runtimeLift,
                anchorNames,
                muzzlePosition,
                corePosition,
                importRotation,
                Vector3.zero,
                Vector3.one,
                false)
        {
        }

        public Tower3DImportSpec(
            string displayName,
            string towerId,
            TowerVisualRole role,
            string rawPrefabPath,
            string runtimePrefabPath,
            Vector3 runtimeScale,
            float runtimeLift,
            string[] anchorNames,
            Vector3 muzzlePosition,
            Vector3 corePosition,
            Quaternion importRotation,
            Vector3 importPosition,
            Vector3 importScale)
            : this(
                displayName,
                towerId,
                role,
                rawPrefabPath,
                runtimePrefabPath,
                runtimeScale,
                runtimeLift,
                anchorNames,
                muzzlePosition,
                corePosition,
                importRotation,
                importPosition,
                importScale,
                false)
        {
        }

        public Tower3DImportSpec(
            string displayName,
            string towerId,
            TowerVisualRole role,
            string rawPrefabPath,
            string runtimePrefabPath,
            Vector3 runtimeScale,
            float runtimeLift,
            string[] anchorNames,
            Vector3 muzzlePosition,
            Vector3 corePosition,
            Quaternion importRotation,
            Vector3 importPosition,
            Vector3 importScale,
            bool preserveSourceAlpha,
            bool reviewPromotable = true,
            bool preserveSourceMaterials = false)
        {
            DisplayName = displayName;
            TowerId = towerId;
            Role = role;
            RawPrefabPath = rawPrefabPath;
            RuntimePrefabPath = runtimePrefabPath;
            RuntimeScale = runtimeScale;
            RuntimeLift = runtimeLift;
            AnchorNames = anchorNames ?? Array.Empty<string>();
            MuzzlePosition = muzzlePosition;
            CorePosition = corePosition;
            ImportRotation = importRotation;
            ImportPosition = importPosition;
            ImportScale = importScale == Vector3.zero ? Vector3.one : importScale;
            PreserveSourceAlpha = preserveSourceAlpha;
            ReviewPromotable = reviewPromotable;
            PreserveSourceMaterials = preserveSourceMaterials;
        }

        public string DisplayName { get; }

        public string TowerId { get; }

        public TowerVisualRole Role { get; }

        public string RawPrefabPath { get; }

        public string RuntimePrefabPath { get; }

        public Vector3 RuntimeScale { get; }

        public float RuntimeLift { get; }

        public string[] AnchorNames { get; }

        public Vector3 MuzzlePosition { get; }

        public Vector3 CorePosition { get; }

        public Quaternion ImportRotation { get; }

        public Vector3 ImportPosition { get; }

        public Vector3 ImportScale { get; }

        public bool PreserveSourceAlpha { get; }

        public bool ReviewPromotable { get; }

        public bool PreserveSourceMaterials { get; }
    }
}
