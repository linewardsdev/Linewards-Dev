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
        private const string MusicVolumeKey = "ltw.presentation.music-volume";
        private const string HealthBarsVisibleKey = "ltw.presentation.health-bars-visible";

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

        /// <summary>
        /// Music bed level, separate from feedback cues because they serve different needs: a
        /// player who wants quiet music but loud leak alarms is the normal case, not the edge.
        /// Defaults modest — the bed is a floor for the mix, not a voice in it.
        /// </summary>
        public static float MusicVolume
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(MusicVolumeKey, 0.4f), 0f, 1f);
            set
            {
                PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Whether a damaged creep's fill-bar quad is drawn at all. Defaults on — the redesign
        /// (flat unlit fill-bar quads through the shared LTW/Fill Bar shader, replacing the old lit
        /// cube pair) is meant to answer the "little value" half of the iPad round 2 report; this
        /// toggle exists for whichever players still want the board bare, without deciding that
        /// question for everyone by removing the feature outright.
        /// </summary>
        public static bool HealthBarsVisible
        {
            get => PlayerPrefs.GetInt(HealthBarsVisibleKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(HealthBarsVisibleKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void ToggleAudioMuted() => AudioMuted = !AudioMuted;

        public static void AdjustFeedbackVolume(float delta) => FeedbackVolume = FeedbackVolume + delta;
    }
}
