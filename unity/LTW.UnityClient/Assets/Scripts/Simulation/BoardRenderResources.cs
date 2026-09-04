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
        private static Mesh mechanicRingMesh;
        private static Mesh sporeFogMesh;
        private static Mesh fillBarMesh;
        private static Mesh tierCrownStudMesh;
        private static readonly Dictionary<uint, Material> TierCrownMaterials = new Dictionary<uint, Material>();

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
        /// Radial segments on the procedural disc/ring decal meshes below. 48 keeps the outline
        /// visibly round at the largest scale any of them is drawn (the 6-cell spore fog) while
        /// staying well under the vertex count of a single primitive cylinder cap.
        /// </summary>
        private const int RadialDecalSegments = 48;

        /// <summary>
        /// Inner radius of <see cref="MechanicRingMesh"/> as a fraction of its outer radius. The
        /// cell centre inside it stays clear, so a braked cell reads as a bordered cell rather
        /// than a filled one.
        /// </summary>
        public const float MechanicRingInnerFraction = 0.65f;

        /// <summary>
        /// Unit-diameter annulus in the XZ plane, facing up, for STANDING per-cell mechanic
        /// markers that should read as a ring rather than a filled disc. Shares
        /// <see cref="CreateContactShadowMaterial"/>'s shader unchanged.
        /// </summary>
        /// <remarks>
        /// R8a (re-audit 2026-09-02, OPEN_ITEMS item 53): Thorn Snare / Foundry Core's braked
        /// cells drew as saturated filled discs and were the loudest shapes on the board. The
        /// contact-shadow shader has no ring parameter — its falloff is a single function of the
        /// UV radius — but it never looks at the mesh's actual position, only its UVs. So a ring
        /// is a MESH problem, not a shader one: every vertex here carries the UV of the radius it
        /// actually sits at (uv = 0.5 + 0.5 * radius * direction), and the shader then does what
        /// it already does — the outer rim gets the material's soft edge, and the inner rim,
        /// sitting at uv radius <see cref="MechanicRingInnerFraction"/> which is well inside the
        /// falloff band for any softness under 0.35, is a clean hard cut. No new shader, no new
        /// material, one extra shared mesh.
        /// </remarks>
        public static Mesh MechanicRingMesh
        {
            get
            {
                if (mechanicRingMesh == null)
                {
                    mechanicRingMesh = BuildRadialDecalMesh(
                        "LTW Mechanic Ring",
                        new[]
                        {
                            new Vector2(0.5f * MechanicRingInnerFraction, MechanicRingInnerFraction),
                            new Vector2(0.5f, 1f)
                        });
                }

                return mechanicRingMesh;
            }
        }

        /// <summary>
        /// Fraction of the spore fog's radius held at full density before the fade begins. At Spore
        /// Cloud Bloom's authored range of 3 cells (the only tower that draws fog) this is exactly
        /// the first cell out from the tower. <see cref="CreateSporeFogMaterial"/>'s softness must
        /// be 1 minus this value for the plateau to be flat; see <see cref="SporeFogMesh"/>.
        /// </summary>
        public const float SporeFogPlateauRadius = 1f / 3f;

        /// <summary>
        /// Unit-diameter disc in the XZ plane, facing up, for the Spore Cloud fog. Same shader and
        /// material as before; the mesh re-shapes the falloff.
        /// </summary>
        /// <remarks>
        /// R8c (re-audit 2026-09-02, OPEN_ITEMS item 53): the fog covered its nine nearest cells at
        /// what read as one uniform density. The falloff was already radial, and its numbers were
        /// checked before touching the shape — but the shader's single smoothstep from
        /// (1 - _Softness) to the rim can only move WHERE the fade starts, and the shipped 0.95
        /// was already the steepest relative fade it can produce; no softness value holds the
        /// first cell full and then drops to a fifth by the next. Same trick as
        /// <see cref="MechanicRingMesh"/>: the shader reads radius from UV, so a disc whose UV
        /// radius is a piecewise-linear remap of its position radius gives any monotone profile
        /// with the shader untouched. With softness 1 - <see cref="SporeFogPlateauRadius"/>:
        ///   position 0 .. 1/3 of the radius (0 .. 1 cell)   -> uv = position, falloff 1.0
        ///   position 1/2 (1.5 cells)                          -> uv 0.809, falloff 0.20
        ///   position 0.9 (2.7 cells)                          -> uv 0.869, falloff 0.10
        ///   position 1 (the rim, 3 cells)                     -> uv 1.0,   falloff 0
        /// so the fog is full over the first cell, a fifth of that by the diagonal neighbours, and
        /// still a faint haze out to the rim, which stays exactly at the range. The plateau keeps
        /// uv = position rather than a constant so the shader's drifting churn bands still move
        /// across it. Between the outer rings the UV radius is nearly constant, which flattens the
        /// churn's radial variation there; at a tenth of the authored alpha that is invisible.
        /// </remarks>
        public static Mesh SporeFogMesh
        {
            get
            {
                if (sporeFogMesh == null)
                {
                    sporeFogMesh = BuildRadialDecalMesh(
                        "LTW Spore Fog Disc",
                        new[]
                        {
                            new Vector2(0f, 0f),
                            new Vector2(0.5f * SporeFogPlateauRadius, SporeFogPlateauRadius),
                            new Vector2(0.25f, 0.809f),
                            new Vector2(0.45f, 0.869f),
                            new Vector2(0.5f, 1f)
                        });
                }

                return sporeFogMesh;
            }
        }

        /// <summary>
        /// Builds a flat, upward-facing disc or annulus from concentric rings of vertices.
        /// </summary>
        /// <param name="rings">
        /// Inner to outer. <c>x</c> is the ring's position radius in mesh units (0.5 = the unit
        /// quad's edge), <c>y</c> the UV radius the contact-shadow / spore-fog shaders read there
        /// (1 = the fade's end). A first ring at radius 0 becomes a single centre vertex.
        /// </param>
        /// <remarks>
        /// Winding matches <see cref="ContactShadowMesh"/> (cross product up); both shaders are
        /// Cull Off anyway. UVs are (0.5, 0.5) + 0.5 * uvRadius * direction, so the shaders'
        /// <c>uv * 2 - 1</c> recovers exactly <c>uvRadius * direction</c>.
        /// </remarks>
        private static Mesh BuildRadialDecalMesh(string name, Vector2[] rings)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            var firstRingIsCentre = rings[0].x <= 0f;
            var ringStart = new int[rings.Length];
            for (var ring = 0; ring < rings.Length; ring++)
            {
                ringStart[ring] = vertices.Count;
                if (ring == 0 && firstRingIsCentre)
                {
                    vertices.Add(Vector3.zero);
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    continue;
                }

                for (var segment = 0; segment < RadialDecalSegments; segment++)
                {
                    var angle = segment * (Mathf.PI * 2f / RadialDecalSegments);
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    vertices.Add(new Vector3(direction.x * rings[ring].x, 0f, direction.y * rings[ring].x));
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(0.5f, 0.5f) + 0.5f * rings[ring].y * direction);
                }
            }

            for (var ring = 1; ring < rings.Length; ring++)
            {
                var inner = ringStart[ring - 1];
                var outer = ringStart[ring];
                var innerIsCentre = ring == 1 && firstRingIsCentre;
                for (var segment = 0; segment < RadialDecalSegments; segment++)
                {
                    var next = (segment + 1) % RadialDecalSegments;
                    var innerA = innerIsCentre ? inner : inner + segment;
                    var innerB = innerIsCentre ? inner : inner + next;
                    var outerA = outer + segment;
                    var outerB = outer + next;

                    triangles.Add(innerA);
                    triangles.Add(outerB);
                    triangles.Add(outerA);
                    if (!innerIsCentre)
                    {
                        triangles.Add(innerA);
                        triangles.Add(innerB);
                        triangles.Add(outerB);
                    }
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
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
        /// Radial segments on <see cref="TierCrownStudMesh"/>. Six: a stud is drawn at roughly a
        /// dozen pixels across at the shipped framing, where a hexagon and a circle are the same
        /// shape and further vertices would buy nothing.
        /// </summary>
        private const int TierCrownStudSegments = 6;

        /// <summary>
        /// Unit stud for the tier crown (UnityVerticalSliceRenderer.ApplyTierCrown): a bevelled
        /// hexagonal boss, base radius 0.5 on the y = 0 plane, shoulder at radius 0.3 / y 0.72,
        /// apex at y 1. Nineteen vertices, eighteen triangles, no underside (it sits on the
        /// board). The caller scales it uniformly to the stud's world size, so one shared mesh
        /// serves every tower whatever its profile scale.
        /// </summary>
        /// <remarks>
        /// Finding #12 (render review 2026-09-01, Wave 5): the `36-tier-pair-closeup` step put a
        /// tier-3 Arrow beside a tier-1 Arrow at one framing and they were indistinguishable —
        /// every tier cue so far lived on accessories that are disabled or too small to read at
        /// 60–90 px. The read has to be a silhouette change, and a ring of raised studs around
        /// the base is the shape that (a) alters the outline from every angle the tilted board
        /// camera can take, (b) is COUNTABLE, so tier 2 and tier 3 differ by more studs rather
        /// than by the hue shift TierMarkerBoost's own remarks call "a hint, not a readout", and
        /// (c) needs no per-tower authoring, since it is sized from the tower's measured footprint
        /// at runtime. Flank normals lean outward-and-up and the cap's inward-and-up so the mesh
        /// still shades as a boss if its material ever falls back to the lit recipe; the shipped
        /// <see cref="TierCrownMaterial"/> is unlit and ignores them.
        /// </remarks>
        public static Mesh TierCrownStudMesh
        {
            get
            {
                if (tierCrownStudMesh == null)
                {
                    const float shoulderRadius = 0.3f;
                    const float shoulderHeight = 0.72f;

                    var vertices = new List<Vector3>(TierCrownStudSegments * 3 + 1);
                    var normals = new List<Vector3>(TierCrownStudSegments * 3 + 1);
                    var triangles = new List<int>(TierCrownStudSegments * 9);

                    // Per segment: base vertex, shoulder vertex sharing the flank's normal, and the
                    // shoulder again carrying the cap's normal — a hard crease between flank and cap.
                    for (var segment = 0; segment < TierCrownStudSegments; segment++)
                    {
                        var angle = segment * (Mathf.PI * 2f / TierCrownStudSegments);
                        var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        // Perpendicular to each face's slope: flank runs (-0.2 radial, +0.72 up),
                        // cap runs (-0.3 radial, +0.28 up).
                        var flankNormal = (direction * shoulderHeight + Vector3.up * (0.5f - shoulderRadius)).normalized;
                        var capNormal = (direction * (1f - shoulderHeight) + Vector3.up * shoulderRadius).normalized;

                        vertices.Add(direction * 0.5f);
                        normals.Add(flankNormal);
                        vertices.Add(direction * shoulderRadius + Vector3.up * shoulderHeight);
                        normals.Add(flankNormal);
                        vertices.Add(direction * shoulderRadius + Vector3.up * shoulderHeight);
                        normals.Add(capNormal);
                    }

                    var apex = vertices.Count;
                    vertices.Add(Vector3.up);
                    normals.Add(Vector3.up);

                    // Winding as ContactShadowMesh / BuildRadialDecalMesh: (b - a) x (c - a) along
                    // the outward normal.
                    for (var segment = 0; segment < TierCrownStudSegments; segment++)
                    {
                        var next = (segment + 1) % TierCrownStudSegments;
                        var baseA = segment * 3;
                        var shoulderA = baseA + 1;
                        var capA = baseA + 2;
                        var baseB = next * 3;
                        var shoulderB = baseB + 1;
                        var capB = baseB + 2;

                        triangles.Add(baseA);
                        triangles.Add(shoulderA);
                        triangles.Add(baseB);

                        triangles.Add(baseB);
                        triangles.Add(shoulderA);
                        triangles.Add(shoulderB);

                        triangles.Add(capA);
                        triangles.Add(apex);
                        triangles.Add(capB);
                    }

                    tierCrownStudMesh = new Mesh { name = "LTW Tier Crown Stud" };
                    tierCrownStudMesh.SetVertices(vertices);
                    tierCrownStudMesh.SetNormals(normals);
                    tierCrownStudMesh.SetTriangles(triangles, 0);
                    tierCrownStudMesh.RecalculateBounds();
                }

                return tierCrownStudMesh;
            }
        }

        /// <summary>
        /// Shared material for one owner's tier-crown studs, one per accent colour. Callers assign
        /// it through <c>sharedMaterial</c>, same rule as <see cref="SharedOpaque"/>.
        /// </summary>
        /// <remarks>
        /// Unlit, deliberately, rather than the lit recipe with an emission term. The stud has
        /// one job — read as a lit accent dot at distance — and URP/Lit's emission is gated by
        /// the <c>_EMISSION</c> shader_feature keyword, which no first-party material in this
        /// project enables (only the unreferenced ThirdParty StylizedWeaponKit does). Enabling it
        /// on a material created at runtime works in the Editor and is stripped from a player
        /// build, where the crown would then silently render as a plain dim boss — the same
        /// editor-passes/device-fails trap RenderCompat.CreatePrimitive documents.
        /// <see cref="RenderCompat.Unlit"/> is already shipped at runtime by BoardBackdrop and
        /// LTWParticleBurst, so it is known to survive the build; an unlit accent at full value is
        /// the "emissive lift" here, with no lighting to dim it. Falls back to the lit shader
        /// only if the unlit one is missing entirely, so the crown degrades to a shaded boss
        /// instead of disappearing.
        /// </remarks>
        public static Material TierCrownMaterial(Color color)
        {
            var key = ColorKey(color);
            if (TierCrownMaterials.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var shader = RenderCompat.Unlit ?? RenderCompat.Lit;
            var material = new Material(shader)
            {
                name = $"LTW Tier Crown {key:X8}",
                enableInstancing = true
            };
            RenderCompat.SetAlbedo(material, color);

            TierCrownMaterials[key] = material;
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
