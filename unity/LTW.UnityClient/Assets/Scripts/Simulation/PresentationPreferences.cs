using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>Small local accessibility preferences used only by the presentation layer.</summary>
    public static class PresentationPreferences
    {
        private const string ReducedEffectsKey = "ltw.presentation.reduced-effects";
        private const string TextScaleKey = "ltw.presentation.text-scale";

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
    }
}
