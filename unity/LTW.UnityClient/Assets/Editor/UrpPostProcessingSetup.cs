using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Creates the post-processing volume profile that carries tonemapping and bloom, the capability
    /// the URP migration exists to obtain.
    /// </summary>
    /// <remarks>
    /// Tower emission maps cover only 2.5 to 15 percent of each texture, so a colour tint alone
    /// barely reads. Bloom is what turns those small emissive areas into something visible at phone
    /// scale, and it is unavailable on the Built-in pipeline for this editor version.
    ///
    /// Scripted rather than authored by hand so the settings are reviewable in the diff and can be
    /// re-applied headlessly.
    /// </remarks>
    public static class UrpPostProcessingSetup
    {
        // Lives under Resources so the runtime launcher can load it without a scene
        // reference, matching how the visual libraries are already loaded.
        private const string SettingsFolder = "Assets/Resources";
        private const string ProfilePath = SettingsFolder + "/LTW_PostProcessing.asset";

        [MenuItem("Line Wards/Migration/Create Post Processing Profile")]
        public static void CreateProfile()
        {
            var exitCode = 0;
            try
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }

                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, ProfilePath);
                }

                ConfigureTonemapping(profile);
                ConfigureBloom(profile);
                ConfigureColorAdjustments(profile);

                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Post processing profile ready at {ProfilePath} with {profile.components.Count} override(s).");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet<T>(out var existing) ? existing : profile.Add<T>(true);
        }

        private static void ConfigureTonemapping(VolumeProfile profile)
        {
            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.overrideState = true;
            // Neutral rather than ACES: ACES shifts the whole palette warm and would undo the board
            // and role colour work rather than sitting on top of it.
            tonemapping.mode.value = TonemappingMode.Neutral;
        }

        private static void ConfigureBloom(VolumeProfile profile)
        {
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;

            // Threshold sits above the lit board so stonework does not glow, but below the emissive
            // tower detail, whose colours are authored at twice intensity.
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;

            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.9f;

            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.62f;

            // High quality filtering is the expensive option; the cheaper path is chosen because the
            // target is mobile and the emissive areas are small.
            bloom.highQualityFiltering.overrideState = true;
            bloom.highQualityFiltering.value = false;

            bloom.downscale.overrideState = true;
            bloom.downscale.value = BloomDownscaleMode.Half;
        }

        private static void ConfigureColorAdjustments(VolumeProfile profile)
        {
            var adjustments = GetOrAdd<ColorAdjustments>(profile);
            adjustments.active = true;

            // Left neutral deliberately. The palette was tuned against the baseline and this exists
            // as the single dial to reach for if the tonemapper shifts it, rather than re-tuning
            // colours across the renderer again.
            adjustments.postExposure.overrideState = true;
            adjustments.postExposure.value = 0f;
            adjustments.saturation.overrideState = true;
            adjustments.saturation.value = 0f;
        }
    }
}
