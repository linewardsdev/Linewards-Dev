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

            var hasData = camera.GetComponent<UniversalAdditionalCameraData>() != null;
            Debug.Log($"DIAG additionalCameraData present before render: {hasData}");

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(0f, 0f, 0f);
            cube.transform.localScale = Vector3.one * 3f;

            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);

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
                tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                tex.Apply();
                var p = tex.GetPixels();
                var mean = new Vector3();
                for (var i = 0; i < p.Length; i++)
                {
                    mean += new Vector3(p[i].r, p[i].g, p[i].b);
                }

                mean /= p.Length;
                Debug.Log($"DIAG readback mean RGB=({mean.x:F3}, {mean.y:F3}, {mean.z:F3}) " +
                          $"[expected background ~ (0.20, 0.60, 0.90) if the render landed]");

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
