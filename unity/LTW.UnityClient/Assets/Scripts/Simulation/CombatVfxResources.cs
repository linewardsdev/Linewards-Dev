using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Shared mesh and material factory for the combat streak quads: the moving projectile
    /// dart, its trail, and the impact sparks (finding #8 / R4, render review 2026-09-01).
    /// </summary>
    /// <remarks>
    /// Its own class rather than more members on <see cref="BoardRenderResources"/> because that
    /// file is being edited concurrently for the tier-silhouette work; same shape as that class
    /// (a Resources-loaded shader with a <c>Shader.Find</c> fallback, one shared mesh, a material
    /// factory) so a later merge is a move rather than a rewrite.
    /// </remarks>
    internal static class CombatVfxResources
    {
        private const string CombatVfxShaderResourcePath = "Shaders/LTWCombatVfx";

        private static Shader combatVfxShader;
        private static Mesh taperedQuadMesh;

        public static Shader CombatVfxShader
        {
            get
            {
                if (combatVfxShader == null)
                {
                    combatVfxShader = Resources.Load<Shader>(CombatVfxShaderResourcePath)
                        ?? Shader.Find("LTW/Combat VFX");
                }

                return combatVfxShader;
            }
        }

        /// <summary>
        /// A unit wedge in the XZ plane, facing +Y: a full unit wide at z = -0.5 (uv v = 0) and
        /// a single point at z = +0.5 (v = 1), with u running across the wide end. Scaled per
        /// use to (width, 1, length); the C# side turns local +Z along travel and tips +Y
        /// toward the camera.
        /// </summary>
        /// <remarks>
        /// A triangle, not a four-vertex quad with a narrow tip: the point vertex carries u = 0.5,
        /// so the across-profile in LTWCombatVfx converges on the core value at the tip and the
        /// head of a dart draws hot rather than pinching to nothing. The same mesh turned 180
        /// degrees is the trail — wide where it meets the dart, a point where it fades out.
        /// </remarks>
        public static Mesh TaperedQuadMesh
        {
            get
            {
                if (taperedQuadMesh == null)
                {
                    taperedQuadMesh = new Mesh { name = "LTW Combat Streak Wedge" };
                    taperedQuadMesh.SetVertices(new List<Vector3>
                    {
                        new Vector3(-0.5f, 0f, -0.5f),
                        new Vector3(0.5f, 0f, -0.5f),
                        new Vector3(0f, 0f, 0.5f)
                    });
                    taperedQuadMesh.SetNormals(new List<Vector3>
                    {
                        Vector3.up,
                        Vector3.up,
                        Vector3.up
                    });
                    taperedQuadMesh.SetUVs(0, new List<Vector2>
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(0.5f, 1f)
                    });
                    taperedQuadMesh.SetTriangles(new[] { 0, 2, 1 }, 0);
                    taperedQuadMesh.RecalculateBounds();
                }

                return taperedQuadMesh;
            }
        }

        /// <summary>
        /// A material for one pooled streak quad. One per pooled object, made once when the
        /// object is created: colour and fade are written per shot and per frame, so the quads
        /// cannot share a material, and cloning at creation is cheaper than letting
        /// <c>renderer.material</c> clone lazily on first use.
        /// </summary>
        /// <remarks>
        /// Falls back through the same additive shaders <see cref="BoardRenderResources.CreateWeaponBeamMaterial"/>
        /// does, so a missing custom shader degrades to a plain glowing wedge rather than a
        /// black one.
        /// </remarks>
        public static Material CreateStreakMaterial(string name)
        {
            var shader = CombatVfxShader
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");

            return new Material(shader)
            {
                name = name,
                enableInstancing = true
            };
        }
    }
}
