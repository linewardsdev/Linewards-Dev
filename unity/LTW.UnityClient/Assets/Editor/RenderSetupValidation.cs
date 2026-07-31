using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Asserts the render pipeline asset still carries the settings the look depends on.
    /// </summary>
    /// <remarks>
    /// Every flag checked here was, at some point, set to a value that silently degraded the image
    /// rather than breaking anything. That is the pattern worth guarding: soft shadows were requested
    /// in code and disabled in the asset, so URP stripped the variant and rendered hard shadows with
    /// no warning; colour grading sat in LDR mode, so HDR values could not be graded even once the
    /// grading existed. Neither produces an error, a warning, or a visibly broken frame — they
    /// produce a slightly worse image that is indistinguishable from an art problem.
    ///
    /// Flags are compared against the value the look was tuned at, and a mismatch is an error rather
    /// than a warning, for the same reason the post-processing profile check is an error: nobody
    /// reads a warning about a render setting until they are already hunting one.
    ///
    /// Runs headless and exits non-zero, so it can gate a build:
    ///   Unity -batchmode -quit -executeMethod LTW.UnityClient.Editor.RenderSetupValidation.ValidateRenderSetup
    /// </remarks>
    public static class RenderSetupValidation
    {
        private const string PipelineAssetPath = "Assets/Settings/LTW_UniversalRenderPipeline.asset";

        [MenuItem("Line Wards/Migration/Validate Render Setup")]
        public static void ValidateRenderSetup()
        {
            var failures = new List<string>();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);

            if (pipeline == null)
            {
                failures.Add($"No UniversalRenderPipelineAsset at {PipelineAssetPath}.");
            }
            else
            {
                // The key light asks for LightShadows.Soft in LocalVerticalSliceLauncher. If the
                // pipeline does not support them the request is not an error — it is dropped, and the
                // shadow renders hard-edged off a 1024 map.
                Require(failures, pipeline.supportsSoftShadows, "supportsSoftShadows is off; the key light requests soft shadows and silently receives hard ones.");

                // LDR grading quantises to an 8-bit LUT before the tonemapper, which throws away the
                // headroom the emissive-heavy palette is authored in.
                Require(failures, pipeline.colorGradingMode == ColorGradingMode.HighDynamicRange, $"colorGradingMode is {pipeline.colorGradingMode}, expected HighDynamicRange.");

                // Bloom and tonemapping both need somewhere above 1.0 to read from.
                Require(failures, pipeline.supportsHDR, "supportsHDR is off; bloom has no values above the threshold to pick up.");

                Require(failures, pipeline.msaaSampleCount >= 2, $"msaaSampleCount is {pipeline.msaaSampleCount}; geometric edges are unfiltered.");

                Require(failures, pipeline.supportsMainLightShadows, "supportsMainLightShadows is off; the board renders with no contact between units and ground.");
            }

            if (failures.Count == 0)
            {
                Debug.Log($"RENDER SETUP OK: {PipelineAssetPath} — soft shadows on, HDR grading, HDR colour buffer, MSAA {(pipeline == null ? 0 : pipeline.msaaSampleCount)}x, main light shadows on.");
            }
            else
            {
                foreach (var failure in failures)
                {
                    Debug.LogError($"RENDER SETUP FAIL: {failure}");
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }

        private static void Require(ICollection<string> failures, bool condition, string message)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }
    }
}
