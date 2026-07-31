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
        private const string FillBarShaderResourcePath = "Shaders/LTWFillBar";
        private const string WeaponBeamShaderResourcePath = "Shaders/LTWWeaponBeam";

        private static readonly Dictionary<uint, Material> SharedOpaqueMaterials = new Dictionary<uint, Material>();

        private static Shader boardVertexColorShader;
        private static Shader contactShadowShader;
        private static Shader fillBarShader;
        private static Shader weaponBeamShader;
        private static Material boardSurfaceMaterial;
        private static Material fillBarMaterial;
        private static Mesh contactShadowMesh;
        private static Mesh fillBarMesh;

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

        public static Shader WeaponBeamShader
        {
            get
            {
                if (weaponBeamShader == null)
                {
                    weaponBeamShader = Resources.Load<Shader>(WeaponBeamShaderResourcePath)
                        ?? Shader.Find("LTW/Weapon Beam");
                }

                return weaponBeamShader;
            }
        }

        /// <summary>
        /// Material for a tower's weapon beam: hot core, soft radial falloff, tapered to the target.
        /// </summary>
        /// <remarks>
        /// Falls back to an additive particle shader rather than an opaque one if the custom shader
        /// is missing, so a beam degrades to a plain glowing box instead of an opaque black brick
        /// across the lane.
        /// </remarks>
        public static Material CreateWeaponBeamMaterial(string name)
        {
            var shader = WeaponBeamShader
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");

            return new Material(shader)
            {
                name = name,
                enableInstancing = true
            };
        }

        public static Shader FillBarShader
        {
            get
            {
                if (fillBarShader == null)
                {
                    fillBarShader = Resources.Load<Shader>(FillBarShaderResourcePath)
                        ?? Shader.Find("LTW/Fill Bar");
                }

                return fillBarShader;
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
                    var shader = BoardVertexColorShader ?? RenderCompat.Lit;
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

        /// <summary>
        /// Unit quad in the XZ plane whose U coordinate runs 0 to 1 along local X, used by
        /// <see cref="FillBarMaterial"/> to read off how full a gauge is. A fixed-size mesh plus a
        /// shared material read through a <c>MaterialPropertyBlock</c> means a lane's pressure
        /// gauge never has to rescale its transform to show fill level.
        /// </summary>
        public static Mesh FillBarMesh
        {
            get
            {
                if (fillBarMesh == null)
                {
                    fillBarMesh = new Mesh { name = "LTW Fill Bar Quad" };
                    fillBarMesh.SetVertices(new List<Vector3>
                    {
                        new Vector3(-0.5f, 0f, -0.5f),
                        new Vector3(0.5f, 0f, -0.5f),
                        new Vector3(0.5f, 0f, 0.5f),
                        new Vector3(-0.5f, 0f, 0.5f)
                    });
                    fillBarMesh.SetNormals(new List<Vector3>
                    {
                        Vector3.up,
                        Vector3.up,
                        Vector3.up,
                        Vector3.up
                    });
                    fillBarMesh.SetUVs(0, new List<Vector2>
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f)
                    });
                    fillBarMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                    fillBarMesh.RecalculateBounds();
                }

                return fillBarMesh;
            }
        }

        /// <summary>
        /// The single material every lane's pressure gauge renders with. Per-lane fill level and
        /// colour are set through a <c>MaterialPropertyBlock</c> on each renderer, not by cloning
        /// this material, so every gauge stays on one shared material.
        /// </summary>
        public static Material FillBarMaterial
        {
            get
            {
                if (fillBarMaterial == null)
                {
                    var shader = FillBarShader ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                    fillBarMaterial = new Material(shader)
                    {
                        name = "LTW Fill Bar",
                        enableInstancing = true
                    };
                }

                return fillBarMaterial;
            }
        }

        /// <summary>
        /// Material for the Spore Cloud's drifting fog. Shares the contact-shadow quad and, like it,
        /// needs no texture — the falloff and the churn are both computed in the fragment shader.
        /// </summary>
        /// <remarks>
        /// Falls back the same way CreateContactShadowMaterial does: if the custom shader is missing
        /// the fog degrades to a flat translucent square rather than vanishing, which is visible and
        /// therefore reportable instead of failing silently.
        /// </remarks>
        public static Material CreateSporeFogMaterial(string name, Color color, float softness, float churn, float speed)
        {
            var shader = Shader.Find("LTW/Spore Fog")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent");

            var material = new Material(shader)
            {
                name = name,
                enableInstancing = true,
                color = color
            };

            if (material.HasProperty("_Softness")) material.SetFloat("_Softness", softness);
            if (material.HasProperty("_Churn")) material.SetFloat("_Churn", churn);
            if (material.HasProperty("_Speed")) material.SetFloat("_Speed", speed);
            return material;
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
            var material = new Material(RenderCompat.Lit)
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
