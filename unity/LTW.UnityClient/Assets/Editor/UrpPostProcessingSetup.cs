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

        /// <summary>Tonemapping, Bloom, ColorAdjustments.</summary>
        private const int ExpectedOverrides = 3;

        /// <summary>
        /// Fails if the committed profile is missing, short, or holds a null override.
        /// </summary>
        /// <remarks>
        /// This is the CI half of the guard; LocalVerticalSliceLauncher.IsPostProcessingProfileUsable
        /// is the runtime half. Both exist because the failure this catches was silent from every
        /// direction: the generator reported success, the launcher's old null-check passed, and the
        /// only symptom was that the game looked flat — which is indistinguishable from an art
        /// problem unless you know to look at the asset.
        ///
        /// Runs headless and exits non-zero, so it can gate a build:
        ///   Unity -batchmode -quit -executeMethod LTW.UnityClient.Editor.UrpPostProcessingSetup.ValidateProfile
        /// </remarks>
        [MenuItem("Line Wars/Migration/Validate Post Processing Profile")]
        public static void ValidateProfile()
        {
            var failures = new System.Collections.Generic.List<string>();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

            if (profile == null)
            {
                failures.Add($"No VolumeProfile at {ProfilePath}.");
            }
            else if (profile.components == null || profile.components.Count != ExpectedOverrides)
            {
                failures.Add($"Expected {ExpectedOverrides} overrides, found {(profile.components == null ? 0 : profile.components.Count)}.");
            }
            else
            {
                for (var index = 0; index < profile.components.Count; index++)
                {
                    if (profile.components[index] == null)
                    {
                        failures.Add($"Override {index} is null — sub-assets were not written to disk.");
                    }
                }

                // Presence is not enough: the point of the profile is these three specific effects.
                if (!profile.TryGet<Tonemapping>(out _)) failures.Add("Missing Tonemapping override.");
                if (!profile.TryGet<Bloom>(out _)) failures.Add("Missing Bloom override.");
                if (!profile.TryGet<ColorAdjustments>(out _)) failures.Add("Missing ColorAdjustments override.");
            }

            if (failures.Count == 0)
            {
                Debug.Log($"POSTFX OK: {ProfilePath} has {ExpectedOverrides} non-null overrides (Tonemapping, Bloom, ColorAdjustments).");
            }
            else
            {
                foreach (var failure in failures)
                {
                    Debug.LogError($"POSTFX FAIL: {failure}");
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }

        [MenuItem("Line Wars/Migration/Create Post Processing Profile")]
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

        /// <summary>
        /// Gets or creates a volume override AND makes it a persisted sub-asset of the profile.
        /// </summary>
        /// <remarks>
        /// The AddObjectToAsset call is the entire fix for the profile shipping empty.
        ///
        /// VolumeProfile.Add&lt;T&gt; creates the component and puts it in profile.components, so
        /// everything downstream — including this file's own success log, which printed
        /// "3 override(s)" — looked correct in the editor session that ran it. But a VolumeComponent
        /// is a ScriptableObject, and a ScriptableObject that is never added to an asset file has
        /// nowhere to serialize to. On the next domain reload the three entries deserialized as
        /// {fileID: 0}, which is exactly what was committed in 746403b and has been shipping ever
        /// since: a profile with three null components, a Volume built from it at runtime, no
        /// tonemapper and no bloom.
        ///
        /// It failed silently in both directions. The generator reported success, and
        /// LocalVerticalSliceLauncher's guard only tested `profile == null` — the asset exists, so
        /// the guard passed and never warned. ValidateProfile below now checks the contents.
        /// </remarks>
        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing))
            {
                // An override from a previous run can still be an orphan if it was created before
                // this fix, so adopt it into the asset rather than assuming it is already persisted.
                if (existing != null && !AssetDatabase.Contains(existing))
                {
                    AssetDatabase.AddObjectToAsset(existing, profile);
                }

                return existing;
            }

            var created = profile.Add<T>(true);
            created.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(created, profile);
            return created;
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
