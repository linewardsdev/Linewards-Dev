using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Shared shaders, materials and meshes for the generated board surface and the unit contact
    /// shadows.
    /// </summary>
    /// <remarks>
    /// Every board object used to call <c>renderer.material.color = ...</c>, which instantiates a
    /// private material per object and defeats both static batching and GPU instancing. The board
    /// geometry that survives as separate GameObjects goes through <see cref="SharedOpaque"/>
    /// instead, so objects that share a colour share a material and can be batched.
    /// </remarks>
    internal static class BoardRenderResources
    {
        private const string BoardVertexColorShaderResourcePath = "Shaders/LTWBoardVertexColor";
        private const string ContactShadowShaderResourcePath = "Shaders/LTWContactShadow";

        private static readonly Dictionary<uint, Material> SharedOpaqueMaterials = new Dictionary<uint, Material>();

        private static Shader boardVertexColorShader;
        private static Shader contactShadowShader;
        private static Material boardSurfaceMaterial;
        private static Mesh contactShadowMesh;

        public static Shader BoardVertexColorShader
        {
            get
            {
                if (boardVertexColorShader == null)
                {
                    boardVertexColorShader = Resources.Load<Shader>(BoardVertexColorShaderResourcePath)
                        ?? Shader.Find("LTW/Board Vertex Color");
                }

                return boardVertexColorShader;
            }
        }

        public static Shader ContactShadowShader
        {
            get
            {
                if (contactShadowShader == null)
                {
                    contactShadowShader = Resources.Load<Shader>(ContactShadowShaderResourcePath)
                        ?? Shader.Find("LTW/Contact Shadow");
                }

                return contactShadowShader;
            }
        }

        /// <summary>
        /// The single material every baked lane mesh renders with. Colour comes from the vertex
        /// stream, so one material covers the whole board.
        /// </summary>
        public static Material BoardSurfaceMaterial
        {
            get
            {
                if (boardSurfaceMaterial == null)
                {
                    var shader = BoardVertexColorShader ?? Shader.Find("Standard");
                    boardSurfaceMaterial = new Material(shader)
                    {
                        name = "LTW Board Surface",
                        enableInstancing = true
                    };
                }

                return boardSurfaceMaterial;
            }
        }

        /// <summary>
        /// Unit quad lying in the XZ plane, facing up, used for the contact shadow decals. A shared
        /// mesh plus a shared material means every blob on screen instances into one draw call.
        /// </summary>
        public static Mesh ContactShadowMesh
        {
            get
            {
                if (contactShadowMesh == null)
                {
                    contactShadowMesh = new Mesh { name = "LTW Contact Shadow Quad" };
                    contactShadowMesh.SetVertices(new List<Vector3>
                    {
                        new Vector3(-0.5f, 0f, -0.5f),
                        new Vector3(0.5f, 0f, -0.5f),
                        new Vector3(0.5f, 0f, 0.5f),
                        new Vector3(-0.5f, 0f, 0.5f)
                    });
                    contactShadowMesh.SetNormals(new List<Vector3>
                    {
                        Vector3.up,
                        Vector3.up,
                        Vector3.up,
                        Vector3.up
                    });
                    contactShadowMesh.SetUVs(0, new List<Vector2>
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f)
                    });
                    contactShadowMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                    contactShadowMesh.RecalculateBounds();
                }

                return contactShadowMesh;
            }
        }

        public static Material CreateContactShadowMaterial(string name, Color color, float softness)
        {
            var shader = ContactShadowShader;
            if (shader == null)
            {
                // Sprites/Default is the only alpha-blended fallback guaranteed to exist without the
                // custom shader; the blob degrades to a hard-edged square rather than disappearing.
                shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            }

            var material = new Material(shader)
            {
                name = name,
                enableInstancing = true,
                color = color
            };

            if (material.HasProperty("_Softness"))
            {
                material.SetFloat("_Softness", softness);
            }

            return material;
        }

        /// <summary>
        /// Returns a shared opaque material for a colour, creating it on first use. Callers must
        /// assign it through <c>sharedMaterial</c>; touching <c>renderer.material</c> would clone
        /// it again and undo the batching this exists for.
        /// </summary>
        public static Material SharedOpaque(Color color)
        {
            var key = ColorKey(color);
            if (SharedOpaqueMaterials.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            // Deliberately the stock Standard shader, not the vertex-colour one: these materials go
            // on built-in primitive meshes, which carry no COLOR stream to multiply against.
            var material = new Material(Shader.Find("Standard"))
            {
                name = $"LTW Board Shared {key:X8}",
                enableInstancing = true,
                color = color
            };

            SharedOpaqueMaterials[key] = material;
            return material;
        }

        private static uint ColorKey(Color color)
        {
            var packed = (Color32)color;
            return (uint)(packed.r << 24 | packed.g << 16 | packed.b << 8 | packed.a);
        }
    }
}
