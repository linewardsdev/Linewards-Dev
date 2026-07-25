using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Accumulates board geometry into a single vertex-coloured <see cref="Mesh"/>.
    /// </summary>
    /// <remarks>
    /// The board used to be built from one <see cref="GameObject.CreatePrimitive"/> per cell and
    /// per decoration strip. Measured on the eight-lane board, that was 1,730 renderers, each with
    /// its own material instance, so nothing batched and every tile cost a draw call; baking folds
    /// 1,586 of them into eight meshes and leaves 152 renderers. Everything on the board is
    /// static and flat-shaded, so the colour can live in the vertex stream instead of a material,
    /// which lets an entire lane collapse into one mesh and one draw call.
    ///
    /// Geometry is emitted to match Unity's primitives exactly (unit cube spanning +/-0.5, shared
    /// built-in meshes for the rounded shapes) so the merged board is positionally identical to
    /// the primitives it replaces.
    /// </remarks>
    internal sealed class BoardMeshBuilder
    {
        /// <summary>
        /// Per-face basis for the unit cube: outward normal, plus the screen-right and screen-up
        /// axes seen by a viewer outside that face. Winding follows Unity's clockwise-is-front
        /// convention.
        /// </summary>
        private static readonly Vector3[] BoxFaceNormals =
        {
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, -1f, 0f),
        };

        private static readonly Vector3[] BoxFaceRight =
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
        };

        private static readonly Vector3[] BoxFaceUp =
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, -1f),
        };

        private static readonly Dictionary<PrimitiveType, Mesh> PrimitiveMeshes = new Dictionary<PrimitiveType, Mesh>();

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Vector3> sourceVertices = new List<Vector3>();
        private readonly List<Vector3> sourceNormals = new List<Vector3>();
        private readonly List<int> sourceTriangles = new List<int>();

        public int VertexCount => vertices.Count;

        public bool IsEmpty => vertices.Count == 0;

        /// <summary>
        /// Converts an authored board colour into the value the shader must receive.
        /// </summary>
        /// <remarks>
        /// Applied inside <see cref="AddBox(Matrix4x4, Color, float)"/> and
        /// <see cref="AddMesh"/> rather than at the call sites, so no future caller can bake an
        /// undecoded colour and silently brighten the board.
        ///
        /// This project renders in linear colour space. A colour assigned through
        /// <c>Material.color</c> is decoded from sRGB on its way to the GPU, but a colour written
        /// into a mesh's vertex stream is not - it arrives raw and is treated as linear. Baking the
        /// authored value unchanged therefore roughly doubles the board's brightness. Decoding here
        /// makes the baked albedo identical to what the per-object materials produced, so the
        /// palette that <c>BoardSurface()</c> defines survives the move to vertex colours.
        /// </remarks>
        public static Color ToRenderSpace(Color color) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;

        /// <summary>
        /// Returns the built-in mesh behind a <see cref="PrimitiveType"/>. The temporary primitive
        /// is destroyed straight away; the mesh it points at is a shared built-in asset and stays
        /// alive.
        /// </summary>
        public static Mesh PrimitiveMesh(PrimitiveType primitiveType)
        {
            if (PrimitiveMeshes.TryGetValue(primitiveType, out var cached) && cached != null)
            {
                return cached;
            }

            var probe = GameObject.CreatePrimitive(primitiveType);
            probe.SetActive(false);
            var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(probe);
            PrimitiveMeshes[primitiveType] = mesh;
            return mesh;
        }

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            colors.Clear();
            triangles.Clear();
        }

        /// <summary>
        /// Appends an axis-aligned box matching a scaled primitive cube.
        /// </summary>
        /// <param name="groundShade">
        /// Multiplier applied to the colour at the bottom of the box, blending back to the full
        /// colour at the top. This deepens the seams between board tiles the way the gaps between
        /// the old cubes did under the key light, and reads as contact occlusion along every
        /// raised strip. Pass 1 for a perfectly flat box.
        /// </param>
        public void AddBox(Vector3 center, Vector3 size, Color color, float groundShade = 1f)
        {
            AddBox(Matrix4x4.TRS(center, Quaternion.identity, size), color, groundShade);
        }

        public void AddBox(Matrix4x4 transform, Color color, float groundShade = 1f)
        {
            color = ToRenderSpace(color);
            var normalTransform = transform.inverse.transpose;
            for (var face = 0; face < BoxFaceNormals.Length; face++)
            {
                var normal = BoxFaceNormals[face];
                var right = BoxFaceRight[face];
                var up = BoxFaceUp[face];
                var faceCenter = normal * 0.5f;
                var worldNormal = normalTransform.MultiplyVector(normal).normalized;
                var baseIndex = vertices.Count;

                AddBoxCorner(transform, worldNormal, faceCenter - right * 0.5f - up * 0.5f, color, groundShade);
                AddBoxCorner(transform, worldNormal, faceCenter + right * 0.5f - up * 0.5f, color, groundShade);
                AddBoxCorner(transform, worldNormal, faceCenter + right * 0.5f + up * 0.5f, color, groundShade);
                AddBoxCorner(transform, worldNormal, faceCenter - right * 0.5f + up * 0.5f, color, groundShade);

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }
        }

        /// <summary>
        /// Appends an arbitrary source mesh (cylinders, spheres, quads) flat-shaded in one colour.
        /// </summary>
        public void AddMesh(Mesh source, Matrix4x4 transform, Color color)
        {
            if (source == null)
            {
                return;
            }

            color = ToRenderSpace(color);
            source.GetVertices(sourceVertices);
            source.GetNormals(sourceNormals);
            var hasNormals = sourceNormals.Count == sourceVertices.Count;
            var normalTransform = transform.inverse.transpose;
            var baseIndex = vertices.Count;

            for (var index = 0; index < sourceVertices.Count; index++)
            {
                vertices.Add(transform.MultiplyPoint3x4(sourceVertices[index]));
                normals.Add(hasNormals ? normalTransform.MultiplyVector(sourceNormals[index]).normalized : Vector3.up);
                colors.Add(color);
            }

            for (var subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                source.GetTriangles(sourceTriangles, subMesh);
                for (var index = 0; index < sourceTriangles.Count; index++)
                {
                    triangles.Add(baseIndex + sourceTriangles[index]);
                }
            }
        }

        public void AddPrimitive(PrimitiveType primitiveType, Matrix4x4 transform, Color color)
        {
            if (primitiveType == PrimitiveType.Cube)
            {
                AddBox(transform, color);
                return;
            }

            AddMesh(PrimitiveMesh(primitiveType), transform, color);
        }

        public Mesh CreateMesh(string name)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertices.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddBoxCorner(Matrix4x4 transform, Vector3 worldNormal, Vector3 localPosition, Color color, float groundShade)
        {
            vertices.Add(transform.MultiplyPoint3x4(localPosition));
            normals.Add(worldNormal);
            if (groundShade >= 1f)
            {
                colors.Add(color);
                return;
            }

            var shade = Mathf.Lerp(groundShade, 1f, localPosition.y + 0.5f);
            colors.Add(new Color(color.r * shade, color.g * shade, color.b * shade, color.a));
        }
    }
}
