using UnityEngine;
using UnityEngine.Rendering;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Resolves shaders for whichever render pipeline is active.
    /// </summary>
    /// <remarks>
    /// Code that creates materials at runtime cannot hard-code a shader name across a pipeline
    /// migration. <c>Shader.Find("Standard")</c> returns null under URP, and a material built from
    /// a null shader renders magenta, which is how the spawn gate bars and lane chevrons survived
    /// the material conversion as bright pink geometry: they are created in code, not as assets, so
    /// the asset converter never saw them.
    ///
    /// Both pipelines stay supported because `main` is Built-in while the migration runs on a
    /// branch.
    /// </remarks>
    public static class RenderCompat
    {
        private static Shader litShader;
        private static Shader unlitShader;

        public static bool UsingScriptablePipeline => GraphicsSettings.currentRenderPipeline != null;

        /// <summary>Opaque lit shader: URP/Lit when a scriptable pipeline is active, else Standard.</summary>
        public static Shader Lit
        {
            get
            {
                if (litShader != null)
                {
                    return litShader;
                }

                litShader = UsingScriptablePipeline
                    ? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")
                    : Shader.Find("Standard");
                return litShader;
            }
        }

        /// <summary>Unlit shader, used for flat cues that should not take lighting.</summary>
        public static Shader Unlit
        {
            get
            {
                if (unlitShader != null)
                {
                    return unlitShader;
                }

                unlitShader = UsingScriptablePipeline
                    ? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")
                    : Shader.Find("Unlit/Color");
                return unlitShader;
            }
        }

        /// <summary>
        /// Sets a material's albedo tint through whichever property the active pipeline uses.
        /// URP/Lit exposes `_BaseColor`; Built-in Standard exposes `_Color`.
        /// </summary>
        public static void SetAlbedo(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            // Set both when present: Material.color maps to _Color, and several call sites and the
            // renderer's tinting helpers still read it.
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        /// <summary>
        /// <see cref="GameObject.CreatePrimitive"/> that is guaranteed to arrive with a material.
        /// </summary>
        /// <remarks>
        /// This is open item 30's actual root cause, and it only bites in a PLAYER.
        /// <c>GameObject.CreatePrimitive</c> takes its material from
        /// <c>GraphicsSettings.currentRenderPipeline.defaultMaterial</c>, and URP's implementation of
        /// that property (<c>UniversalRenderPipelineAsset.DefaultResources.cs</c>) is wrapped in
        /// <c>#if UNITY_EDITOR</c> with a bare <c>return null</c> for players. So every primitive
        /// created at runtime has a working mesh, a working renderer, and NO material in a build, and
        /// a renderer with no material draws with the magenta error shader.
        ///
        /// In the Editor the same call returns URP's Lit.mat and everything looks correct, which is
        /// exactly why the defect reached a device build and was blamed on a shader.
        ///
        /// The repair is deliberately conditional on the material being missing, so the Editor path
        /// is untouched and the two environments are not silently given different materials.
        /// </remarks>
        public static GameObject CreatePrimitive(PrimitiveType primitiveType)
        {
            var instance = GameObject.CreatePrimitive(primitiveType);
            EnsureMaterial(instance);
            return instance;
        }

        /// <summary>
        /// Gives a renderer a material if it has none. See <see cref="CreatePrimitive"/> for why an
        /// object can reach here without one.
        /// </summary>
        /// <remarks>
        /// One shared fallback rather than one per object: callers that tint a primitive all go
        /// through <c>renderer.material</c>, which clones before writing, so nobody mutates this
        /// instance. Callers that assign <c>sharedMaterial</c> replace it outright.
        /// </remarks>
        public static void EnsureMaterial(GameObject instance)
        {
            if (instance == null || !instance.TryGetComponent<Renderer>(out var renderer))
            {
                return;
            }

            if (renderer.sharedMaterial != null)
            {
                return;
            }

            renderer.sharedMaterial = DefaultPrimitiveMaterial;
        }

        private static Material defaultPrimitiveMaterial;

        private static Material DefaultPrimitiveMaterial
        {
            get
            {
                if (defaultPrimitiveMaterial == null)
                {
                    defaultPrimitiveMaterial = new Material(Lit) { name = "LTW Default Primitive" };
                }

                return defaultPrimitiveMaterial;
            }
        }

        /// <summary>Clears cached lookups, for when the active pipeline changes in-editor.</summary>
        public static void ResetCache()
        {
            litShader = null;
            unlitShader = null;
            defaultPrimitiveMaterial = null;
        }
    }
}
