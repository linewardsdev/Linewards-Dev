using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// The single source of truth for tower role colour.
    /// </summary>
    /// <remarks>
    /// Role colour was previously defined twice: <c>UnityVerticalSliceRenderer.TowerMarkerColor</c>
    /// for board markers, halos and the derived base tint, and <c>TouchPlacementController</c> for
    /// the build palette cards and selection ring. The two disagreed on three of the five roles, so
    /// the colour a player learned from a card was not the colour the placed tower carried.
    ///
    /// Hues are also kept apart. The board mapping used to return the same pale blue for control
    /// and prism, and the card palette gave arrow and prism two shades of blue, so in both places a
    /// pair of roles was effectively indistinguishable. Prism takes the warm coral slot, which is
    /// the only hue neither definition was already using, so the four associations players have
    /// already formed are preserved.
    /// </remarks>
    public static class TowerRolePalette
    {
        public static readonly Color Arrow = new Color(0.302f, 0.639f, 1f);
        public static readonly Color Control = new Color(0.608f, 0.424f, 1f);
        public static readonly Color Relay = new Color(1f, 0.784f, 0.29f);
        public static readonly Color Pulse = new Color(0.349f, 0.882f, 0.714f);
        public static readonly Color Prism = new Color(1f, 0.45f, 0.3f);

        /// <summary>
        /// Resolves a tower id to its role colour. Matching is ordered most specific first so a
        /// role cannot fall through into another's branch, which is how prism previously inherited
        /// control's colour.
        /// </summary>
        public static Color For(string towerId)
        {
            if (string.IsNullOrEmpty(towerId))
            {
                return Arrow;
            }

            if (towerId.Contains("prism"))
            {
                return Prism;
            }

            if (towerId.Contains("pulse") || towerId.Contains("splash") || towerId.Contains("fire") || towerId.Contains("area"))
            {
                return Pulse;
            }

            if (towerId.Contains("relay") || towerId.Contains("economy") || towerId.Contains("utility"))
            {
                return Relay;
            }

            if (towerId.Contains("control") || towerId.Contains("slow") || towerId.Contains("ice"))
            {
                return Control;
            }

            return Arrow;
        }
    }
}
