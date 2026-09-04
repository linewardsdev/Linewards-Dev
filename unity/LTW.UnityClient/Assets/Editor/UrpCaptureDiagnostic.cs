using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Isolates why a scriptable-pipeline capture reads back blank, by rendering one camera into a
    /// render texture and reporting what actually landed in it.
    /// </summary>
    public static class UrpCaptureDiagnostic
    {
        [MenuItem("Line Wards/Migration/Diagnose Capture Path")]
        public static void Diagnose()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            Debug.Log($"DIAG pipeline={(pipeline == null ? "Built-in" : pipeline.GetType().Name)}");

            var probe = new GameObject("DiagCamera");
            probe.transform.position = new Vector3(0f, 5f, -10f);
            probe.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            var camera = probe.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            // Orthographic with a known size so pixels-per-world-unit can be measured directly.
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            probe.transform.position = new Vector3(0f, 0f, -10f);
            probe.transform.rotation = Quaternion.identity;

            var hasData = camera.GetComponent<UniversalAdditionalCameraData>() != null;
            Debug.Log($"DIAG additionalCameraData present before render: {hasData}");

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = Vector3.zero;
            cube.transform.localScale = new Vector3(2f, 2f, 1f);   // exactly 2 world units wide

            var rt = new RenderTexture(128, 256, 24, RenderTextureFormat.ARGB32);  // portrait 0.5 aspect
            var tex = new Texture2D(128, 256, TextureFormat.RGBA32, false);

            try
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                var supported = RenderPipeline.SupportsRenderRequest(camera, request);
                Debug.Log($"DIAG SupportsRenderRequest={supported}");

                if (supported)
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 128, 256), 0, 0);
                tex.Apply();
                var p = tex.GetPixels();
                var mean = new Vector3();
                for (var i = 0; i < p.Length; i++)
                {
                    mean += new Vector3(p[i].r, p[i].g, p[i].b);
                }

                mean /= p.Length;
                Debug.Log($"DIAG readback mean RGB=({mean.x:F3}, {mean.y:F3}, {mean.z:F3})");

                // Count columns containing the cube (not background) to get pixels per world unit.
                var cols = 0;
                for (var x = 0; x < 128; x++)
                {
                    var isCube = false;
                    for (var y = 0; y < 256; y++)
                    {
                        var c = tex.GetPixel(x, y);
                        if (Mathf.Abs(c.b - 0.9f) > 0.25f) { isCube = true; break; }
                    }
                    if (isCube) cols++;
                }

                var aspect = camera.aspect;
                var rtAspect = 128f / 256f;
                var expectedRt = 128f / (2f * camera.orthographicSize * rtAspect) * 2f;
                var expectedScreen = 128f / (2f * camera.orthographicSize * ((float)Screen.width / Screen.height)) * 2f;
                Debug.Log($"DIAG cube width: measured={cols}px | if RT-aspect({rtAspect:F3})={expectedRt:F1}px | if screen-aspect={expectedScreen:F1}px | camera.aspect={aspect:F4}");

                Debug.Log($"DIAG additionalCameraData present after render: " +
                          $"{camera.GetComponent<UniversalAdditionalCameraData>() != null}");
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(probe);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
