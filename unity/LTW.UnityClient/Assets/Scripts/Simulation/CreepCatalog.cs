#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Which shape <see cref="UI.SendDockController"/> draws when a creep's icon PNG cannot be
    /// loaded. Public because the roster that names it now lives in <see cref="CreepCatalog"/>.
    /// </summary>
    /// <remarks>
    /// Kept even though every creep's PNG does now exist, because it is the fallback that made it
    /// safe to name an icon before its art was rendered — see the comment on
    /// <c>RuntimeUiIconLibrary.DrawIcon</c>'s return value. The RESOURCE NAME is no longer switched
    /// on this; it is derived from the content id.
    /// </remarks>
    public enum CreepIconKind
    {
        Runner,
        Brute,
        Swarm,
        Shade,
        Siege,
        Wisp,
        Revenant,
        ObsidianBrute,
        Serpent,
        TurretWalker,
        Zephyr,
        Burrower,
        Stalker,
        Warden,
        Colossus
    }

    /// <summary>
    /// The client's single description of every sendable creep: role index, content id, labels,
    /// category, accent colour and codex copy.
    /// </summary>
    /// <remarks>
    /// The missing half of <see cref="TowerCatalog"/>. Towers have had a catalog since the palette
    /// drifted into pricing Arrow three different ways; creeps never got one, so their presentation
    /// data lived in three private <c>SendCard[]</c> builders inside the send dock — fine while the
    /// dock was the only thing that drew a creep, and not fine the moment the codex needed the same
    /// labels, accents and ordering. A second copy is exactly what went stale last time.
    ///
    /// Cost, health and every other number are deliberately absent for the same reason they are
    /// absent from TowerCatalog: they are read from the simulation's ContentCatalog at display time.
    /// Everything here is presentation only.
    ///
    /// Role index is the dock's selection identity — it is what <c>highlightedCreepRole</c> is
    /// compared against, and it travels with the entry so sorting the cards by price cannot
    /// renumber it. Entries may be reordered freely; the indices may not change.
    /// </remarks>
    public static class CreepCatalog
    {
        public const int CategoryCore = 0;
        public const int CategorySupport = 1;
        public const int CategoryElite = 2;

        // Names each category by what it actually does, replacing the "CATEGORY 1/2" placeholders
        // that were waiting on this content:
        //   CORE  — the founding five, all send-cooldown gated.
        //   SUPPORT — force multipliers rather than bodies. Four buff the creeps around them or
        //           slow the towers shooting at them; the fifth walks over the maze entirely. The
        //           category was called RAPID for an exemption from a send cooldown that has been
        //           set to 0 for a long time, so the name described nothing a player could observe.
        //   ELITE — the Meshy-rigged bipeds: costlier, heavier, and back on the normal cooldown,
        //           because price is what paces them.
        public static readonly string[] CategoryLabels = { "CORE", "SUPPORT", "ELITE" };

        public sealed class Entry
        {
            public Entry(
                int role,
                string contentId,
                string shortLabel,
                string displayName,
                int category,
                Color accent,
                CreepIconKind icon,
                string blurb,
                bool ignoresCooldown = false)
            {
                Role = role;
                ContentId = contentId;
                ShortLabel = shortLabel;
                DisplayName = displayName;
                Category = category;
                Accent = accent;
                Icon = icon;
                Blurb = blurb;
                IgnoresCooldown = ignoresCooldown;
            }

            /// <summary>Dock identity, and what highlightedCreepRole is compared against.</summary>
            public int Role { get; }

            /// <summary>Matches the simulation's ContentId, e.g. "creep.runner".</summary>
            public string ContentId { get; }

            /// <summary>Card label. Kept short enough to fit the send card.</summary>
            public string ShortLabel { get; }

            public string DisplayName { get; }

            public int Category { get; }

            public Color Accent { get; }

            /// <summary>Shape drawn when the icon PNG is missing.</summary>
            public CreepIconKind Icon { get; }

            /// <summary>One line of codex copy: what this creep is for, in a player's terms.</summary>
            public string Blurb { get; }

            public bool IgnoresCooldown { get; }

            /// <summary>Trailing segment of the content id, e.g. "obsidian_brute" — used by icon and visual lookups.</summary>
            public string CreepId => ContentId.StartsWith("creep.") ? ContentId.Substring(6) : ContentId;

            /// <summary>Name of this creep's icon PNG under Resources/Art/UI/Icons.</summary>
            /// <remarks>
            /// Derived rather than switched on. This was a fifteen-arm switch in the send dock that
            /// produced exactly this string for every arm — a second copy of the roster whose only
            /// job was to restate the naming convention. Towers already derive theirs the same way
            /// (<c>TouchPlacementController.Gui</c>), and coverage is complete on both sides.
            /// </remarks>
            public string IconResource => $"ui_icon_send_{CreepId}_v01";
        }

        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color WardViolet = new(0.608f, 0.424f, 1f, 1f);

        public static readonly Entry[] Entries =
        {
            new(0, "creep.runner", "RUNNER", "Runner", CategoryCore, ArcaneBlue, CreepIconKind.Runner,
                "The baseline body. Cheap enough to send in numbers and slow enough that everything gets a shot at it."),
            new(1, "creep.brute", "BRUTE", "Brute", CategoryCore, WardViolet, CreepIconKind.Brute,
                "Twice a Runner's health at under twice the price. The wall you put in front of anything you actually care about."),
            new(2, "creep.swarm", "SWARM", "Swarm", CategoryCore, SignalGold, CreepIconKind.Swarm,
                "The cheapest creep on the roster, and the fastest of the core five. Sent in bulk to bury single-target towers in targets."),
            new(3, "creep.shade", "SHADE", "Shade", CategoryCore, MintSignal, CreepIconKind.Shade,
                "Quick and worth real income. Priced for a player who is winning the economy rather than the lane."),
            new(4, "creep.siege", "SIEGE", "Siege", CategoryCore, new Color(1f, 0.62f, 0.26f), CreepIconKind.Siege,
                "The heaviest core body and the largest core income. Slow, expensive, and very hard to clear with chip damage."),

            new(5, "creep.wisp", "WISP", "Crystal Wisp", CategorySupport, ArcaneBlue, CreepIconKind.Wisp,
                "Paces the creeps around it, so the pack ahead moves faster than it does. Fragile, and worth almost nothing dead.",
                ignoresCooldown: true),
            new(6, "creep.revenant", "REVENANT", "Ash Revenant", CategorySupport, WardViolet, CreepIconKind.Revenant,
                "Trickles health back into the creeps beside it. Answers chip damage; does nothing at all against burst.",
                ignoresCooldown: true),
            new(7, "creep.obsidian_brute", "OBSIDIAN", "Obsidian Brute", CategorySupport, new Color(0.92f, 0.32f, 0.28f), CreepIconKind.ObsidianBrute,
                "Shields the creeps around it and carries the health to stay alive while it does. The most expensive support.",
                ignoresCooldown: true),
            new(8, "creep.serpent", "SERPENT", "Serpent Coil", CategorySupport, MintSignal, CreepIconKind.Serpent,
                "Drags out the reload of every tower near it. Hurts fast towers most, so the counter is heavy single shots.",
                ignoresCooldown: true),
            new(9, "creep.turret_walker", "WALKER", "Spire Turret Walker", CategorySupport, new Color(0.42f, 0.82f, 0.86f), CreepIconKind.TurretWalker,
                "Walks straight over the maze and every tower in it. Dies to almost anything alone — it survives only when the defence is already busy.",
                ignoresCooldown: true),

            new(10, "creep.zephyr", "WRAITH", "Zephyr Wraith", CategoryElite, ArcaneBlue, CreepIconKind.Zephyr,
                "The fastest creep in the game. Crosses a lane before a slow tower has finished its second shot."),
            new(11, "creep.burrower", "BURROW", "Fracture Burrower", CategoryElite, new Color(0.85f, 0.55f, 0.25f), CreepIconKind.Burrower,
                "Heavy and cheap for its health. The elite answer to a lane built out of chip damage."),
            new(12, "creep.stalker", "STALKER", "Umbral Stalker", CategoryElite, WardViolet, CreepIconKind.Stalker,
                "Quick, moderately tough, and the best income per gold in the elite bracket."),
            new(13, "creep.warden", "WARDEN", "Aegis Warden", CategoryElite, MintSignal, CreepIconKind.Warden,
                "A wall that walks. Second only to the Colossus in health, and considerably cheaper."),
            new(14, "creep.colossus", "COLOSSUS", "Siege Colossus", CategoryElite, new Color(1f, 0.45f, 0.30f), CreepIconKind.Colossus,
                "The heaviest body and the largest income on the roster. One is a statement; a wave of them ends a lane.")
        };

        private static readonly Dictionary<int, Entry> ByRole = BuildByRole();
        private static readonly Dictionary<string, Entry> ByContentId = BuildByContentId();

        private static Dictionary<int, Entry> BuildByRole()
        {
            var map = new Dictionary<int, Entry>(Entries.Length);
            foreach (var entry in Entries)
            {
                map[entry.Role] = entry;
            }

            return map;
        }

        private static Dictionary<string, Entry> BuildByContentId()
        {
            var map = new Dictionary<string, Entry>(Entries.Length);
            foreach (var entry in Entries)
            {
                map[entry.ContentId] = entry;
            }

            return map;
        }

        /// <summary>
        /// Never returns null: an unknown role falls back to the first entry, matching
        /// <see cref="TowerCatalog.ForRole"/>.
        /// </summary>
        public static Entry ForRole(int role) => ByRole.TryGetValue(role, out var entry) ? entry : Entries[0];

        /// <summary>
        /// Resolves a creep's simulation ContentId (e.g. "creep.wisp") to its catalog entry. Never
        /// returns null, same Runner fallback as <see cref="ForRole"/>.
        /// </summary>
        public static Entry ForContentId(string contentId) =>
            contentId is not null && ByContentId.TryGetValue(contentId, out var entry) ? entry : Entries[0];

        public static IEnumerable<Entry> InCategory(int category)
        {
            foreach (var entry in Entries)
            {
                if (entry.Category == category)
                {
                    yield return entry;
                }
            }
        }
    }
}
