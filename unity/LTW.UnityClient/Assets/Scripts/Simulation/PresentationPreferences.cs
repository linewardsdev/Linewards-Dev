using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>Small local accessibility preferences used only by the presentation layer.</summary>
    public static class PresentationPreferences
    {
        private const string ReducedEffectsKey = "ltw.presentation.reduced-effects";
        private const string TextScaleKey = "ltw.presentation.text-scale";
        private const string AudioMutedKey = "ltw.presentation.audio-muted";
        private const string FeedbackVolumeKey = "ltw.presentation.feedback-volume";

        public static bool ReducedEffects
        {
            get => PlayerPrefs.GetInt(ReducedEffectsKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ReducedEffectsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float TextScale
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(TextScaleKey, 1f), 0.8f, 1.5f);
            set
            {
                PlayerPrefs.SetFloat(TextScaleKey, Mathf.Clamp(value, 0.8f, 1.5f));
                PlayerPrefs.Save();
            }
        }

        public static bool AudioMuted
        {
            get => PlayerPrefs.GetInt(AudioMutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(AudioMutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float FeedbackVolume
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(FeedbackVolumeKey, 0.25f), 0f, 1f);
            set
            {
                PlayerPrefs.SetFloat(FeedbackVolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static void ToggleAudioMuted() => AudioMuted = !AudioMuted;

        public static void AdjustFeedbackVolume(float delta) => FeedbackVolume = FeedbackVolume + delta;
    }
}
