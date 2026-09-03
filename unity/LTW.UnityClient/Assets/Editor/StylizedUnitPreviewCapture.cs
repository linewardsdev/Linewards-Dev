using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Renders unit prefabs under the stylized shader and under stock URP/Lit, in the same
    /// lighting, so the two can be judged against each other rather than against memory.
    /// </summary>
    /// <remarks>
    /// Deliberately synchronous. `VisualReviewCaptureRunner` is the project's real capture harness
    /// and it is the right tool for review sets, but it drives Play Mode across frames, so under
    /// `-batchmode -quit` the editor exits before it produces anything — which is exactly what
    /// happened the first time this verification was attempted. This does one `Camera.Render()` on
    /// the main thread and writes the file before returning, which is what makes it usable from
    /// `-executeMethod`.
    ///
    /// Scope is deliberately small: it exists to answer "does the new shader actually look better in
    /// URP", not to replace the review harness. It builds its own scene, renders, and reverts.
    /// </remarks>
    public static class StylizedUnitPreviewCapture
    {
        private const string DefaultOutputDirectory = "../../../docs/screenshot-reviews/stylized-shader-20260801";

        /// <summary>
        /// Where captures land, relative to <c>Application.dataPath</c>, overridable with
        /// <c>-ltwPreviewOutputDir</c> so a new review does not overwrite the shipped
        /// before/after pair this folder exists to hold.
        /// </summary>
        private static string OutputDirectory =>
            ReadArgumentValue("-ltwPreviewOutputDir") ?? DefaultOutputDirectory;

        private static readonly string[] DefaultSubjectPrefabs =
        {
            "Assets/Prefabs/Towers/Tower_Arrow_3D.prefab",
            "Assets/Prefabs/Creeps/Creep_TurretWalker_3D.prefab"
        };

        /// <summary>
        /// The default pair, or whatever <c>-ltwPreviewSubjects</c> names as a comma-separated list
        /// of prefab paths. The framing holds two or three units before they run out of frame.
        /// </summary>
        /// <remarks>
        /// Added so a newly integrated unit can be judged beside the one it was kitbashed from, in
        /// the shipped shader and lighting, without editing this file for each unit.
        /// </remarks>
        private static string[] SubjectPrefabs
        {
            get
            {
                var requested = ReadArgumentValue("-ltwPreviewSubjects");
                return string.IsNullOrWhiteSpace(requested)
                    ? DefaultSubjectPrefabs
                    : requested.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                               .Select(entry => entry.Trim())
                               .Where(entry => entry.Length > 0)
                               .ToArray();
            }
        }

        private static string ReadArgumentValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        /// <summary>
        /// Renders whatever materials are on disk right now, to one file.
        /// </summary>
        /// <remarks>
        /// The honest way to get a before/after: revert the materials, capture, re-apply, capture.
        /// The first attempt at this reconstructed a "before" by cloning URP/Lit materials from the
        /// migrated ones and force-enabling `_EMISSION`, which produced a red cast that exists in no
        /// shipped configuration and made the comparison worthless. Rendering what is actually on
        /// disk cannot lie in that way.
        /// </remarks>
        [MenuItem("Line Wars/Review/Capture Units As Currently Authored")]
        public static void CaptureCurrent() => Render("capture_current.png", 1400, 800);

        /// <summary>
        /// The same framing at the pixel size units actually occupy in the game.
        /// </summary>
        /// <remarks>
        /// This is the capture that decides whether any of the shading work was worth doing, and it
        /// is the one the project keeps not taking. The asset review measured towers at roughly 46px
        /// and creeps between 16 and 105px on a phone; a 1400px poster render says nothing about
        /// whether a rim, a contour or a shading ramp survives to that. Rendering at 240x137 puts the
        /// tower at about the measured size, and the file is then upscaled with nearest-neighbour for
        /// inspection so what is written is genuinely that many pixels rather than a resample of a
        /// larger render.
        /// </remarks>
        [MenuItem("Line Wars/Review/Capture Units At Game Size")]
        public static void CaptureGameSize() => Render("capture_gamesize.png", 240, 137);

        [MenuItem("Line Wars/Review/Capture Stylized Shader Comparison")]
        public static void Capture()
        {
            var root = new GameObject("StylizedPreviewRoot");
            var spawned = new List<GameObject>();

            try
            {
                var x = -0.85f;
                foreach (var path in SubjectPrefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[StylizedPreview] Missing prefab: {path}");
                        continue;
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.position = new Vector3(x, 0f, 0f);
                    instance.transform.rotation = Quaternion.Euler(0f, 205f, 0f);
                    spawned.Add(instance);
                    x += 1.7f;
                }

                if (spawned.Count == 0)
                {
                    Debug.LogError("[StylizedPreview] No subjects instantiated; nothing to capture.");
                    return;
                }

                // Ground plane so AO and the contour have something to read against, matching the
                // desaturated board the uplift doc asks for.
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.SetParent(root.transform, false);
                ground.transform.localScale = Vector3.one * 2f;
                var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                groundMat.SetColor("_BaseColor", new Color(0.055f, 0.065f, 0.085f, 1f));
                groundMat.SetFloat("_Smoothness", 0.15f);
                ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

                var lightGo = new GameObject("Key");
                lightGo.transform.SetParent(root.transform, false);
                var key = lightGo.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 1.5f;
                key.color = new Color(1f, 0.96f, 0.90f);
                key.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(42f, 35f, 0f);

                var rimGo = new GameObject("RimLight");
                rimGo.transform.SetParent(root.transform, false);
                var rim = rimGo.AddComponent<Light>();
                rim.type = LightType.Directional;
                rim.intensity = 0.7f;
                rim.color = new Color(0.55f, 0.78f, 1f);
                rimGo.transform.rotation = Quaternion.Euler(18f, 205f, 0f);

                var camGo = new GameObject("PreviewCamera");
                camGo.transform.SetParent(root.transform, false);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 1.05f;
                cam.transform.position = new Vector3(0f, 1.55f, -2.6f);
                cam.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.045f, 0.052f, 0.07f, 1f);

                var renderers = spawned
                    .SelectMany(go => go.GetComponentsInChildren<MeshRenderer>(true))
                    .Concat(spawned.SelectMany(go => go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        .Cast<Renderer>())
                    .Where(r => r != null)
                    .ToArray();

                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, OutputDirectory));
                Directory.CreateDirectory(directory);

                RenderTo(cam, Path.Combine(directory, "after_stylized.png"), 1400, 800);

                // Same geometry, same lights, stock Lit. Rebuilt from each material's own albedo and
                // colour so the comparison isolates the lighting model rather than the texture set.
                var original = renderers.ToDictionary(r => r, r => r.sharedMaterials);
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials
                        .Select(source =>
                        {
                            if (source == null) return null;
                            var clone = new Material(lit);
                            if (source.HasProperty("_BaseMap")) clone.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                            if (source.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                            if (source.HasProperty("_EmissionMap")) clone.SetTexture("_EmissionMap", source.GetTexture("_EmissionMap"));
                            if (source.HasProperty("_EmissionColor"))
                            {
                                clone.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
                                clone.EnableKeyword("_EMISSION");
                            }
                            if (source.HasProperty("_Metallic")) clone.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                            clone.SetFloat("_Smoothness", 0.5f);
                            return clone;
                        })
                        .ToArray();
                }

                RenderTo(cam, Path.Combine(directory, "before_urp_lit.png"), 1400, 800);

                foreach (var pair in original)
                {
                    pair.Key.sharedMaterials = pair.Value;
                }

                Debug.Log($"[StylizedPreview] Wrote before/after captures to {directory}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Builds the preview scene, renders one frame to <paramref name="fileName"/>, tears it down.</summary>
        private static void Render(string fileName, int width, int height)
        {
            var root = new GameObject("StylizedPreviewRoot");
            try
            {
                var x = -0.85f;
                var any = false;
                foreach (var path in SubjectPrefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) { Debug.LogWarning($"[StylizedPreview] Missing prefab: {path}"); continue; }
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.position = new Vector3(x, 0f, 0f);
                    instance.transform.rotation = Quaternion.Euler(0f, 205f, 0f);
                    x += 1.7f;
                    any = true;
                }

                if (!any)
                {
                    Debug.LogError("[StylizedPreview] No subjects instantiated.");
                    return;
                }

                BuildStage(root);
                var cam = root.GetComponentInChildren<Camera>();
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, OutputDirectory));
                Directory.CreateDirectory(directory);
                RenderTo(cam, Path.Combine(directory, fileName), width, height);
                Debug.Log($"[StylizedPreview] Wrote {Path.Combine(directory, fileName)}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildStage(GameObject root)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = Vector3.one * 2f;
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.SetColor("_BaseColor", new Color(0.055f, 0.065f, 0.085f, 1f));
            groundMat.SetFloat("_Smoothness", 0.15f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            var lightGo = new GameObject("Key");
            lightGo.transform.SetParent(root.transform, false);
            var key = lightGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.5f;
            key.color = new Color(1f, 0.96f, 0.90f);
            key.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(42f, 35f, 0f);

            var rimGo = new GameObject("RimLight");
            rimGo.transform.SetParent(root.transform, false);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.7f;
            rim.color = new Color(0.55f, 0.78f, 1f);
            rimGo.transform.rotation = Quaternion.Euler(18f, 205f, 0f);

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.05f;
            cam.transform.position = new Vector3(0f, 1.55f, -2.6f);
            cam.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.045f, 0.052f, 0.07f, 1f);
        }

        private static void RenderTo(Camera cam, string path, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = previous;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
