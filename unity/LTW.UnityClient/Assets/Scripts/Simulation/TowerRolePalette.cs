using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// The five arcane-role colour constants, seeded into <see cref="TowerCatalog"/>'s entries for
    /// those roles.
    /// </summary>
    /// <remarks>
    /// This used to also be the lookup itself (a <c>For(string towerId)</c> substring-matcher), which
    /// was the single source of truth for tower role colour back when arcane was the whole roster.
    /// That lookup only ever recognised the 5 arcane roles, so all 10 Foundry/Grove towers silently
    /// fell through to Arrow's blue once the roster grew — <c>TowerCatalog.ForContentId(...).Accent</c>
    /// replaces it now, covering all 15 towers from one array instead of a hand-maintained switch.
    /// The constants stay here because <see cref="TowerCatalog"/>'s 5 arcane entries and a couple of
    /// UI call sites (the in-placement switch strip) still reference them directly by name.
    ///
    /// Hues are kept apart deliberately: the board mapping used to return the same pale blue for
    /// control and prism, and the card palette gave arrow and prism two shades of blue, so in both
    /// places a pair of roles was effectively indistinguishable. Prism takes the warm coral slot,
    /// which is the only hue neither definition was already using, so the four associations players
    /// have already formed are preserved.
    /// </remarks>
    public static class TowerRolePalette
    {
        public static readonly Color Arrow = new Color(0.302f, 0.639f, 1f);
        public static readonly Color Control = new Color(0.608f, 0.424f, 1f);
        public static readonly Color Relay = new Color(1f, 0.784f, 0.29f);
        public static readonly Color Pulse = new Color(0.349f, 0.882f, 0.714f);
        public static readonly Color Prism = new Color(1f, 0.45f, 0.3f);
    }
}
