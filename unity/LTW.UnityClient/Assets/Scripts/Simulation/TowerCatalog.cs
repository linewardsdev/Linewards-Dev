#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// The client's single description of every buildable tower: role index, content id, labels,
    /// category and accent colour.
    /// </summary>
    /// <remarks>
    /// This replaces a set of parallel switch statements on the palette's role index, each of
    /// which had to be extended by hand for every new tower. That arrangement had already drifted
    /// badly: the palette buttons priced Arrow at 20 gold, SelectedTowerCost said 25, and the
    /// simulation charged 14 — three different numbers for one tower, so the affordability check
    /// and the displayed price disagreed with what the player was actually charged.
    ///
    /// Cost deliberately does NOT appear here. It is read from the simulation's ContentCatalog at
    /// display time (UnityCommandAdapter.TowerCost), because a copy in the client is exactly what
    /// went stale. Everything here is presentation only.
    ///
    /// Role index is the palette's identity for a tower and is persisted in nothing, so entries
    /// may be reordered freely — but the first five keep their historical indices, since existing
    /// review tooling and saved captures refer to them.
    /// </remarks>
    public static class TowerCatalog
    {
        public const int CategoryArcane = 0;
        public const int CategoryFoundry = 1;
        public const int CategoryGrove = 2;

        public static readonly string[] CategoryLabels = { "ARCANE", "FOUNDRY", "GROVE" };

        public sealed class Entry
        {
            public Entry(int role, string contentId, string shortLabel, string displayName, int category, Color accent)
            {
                Role = role;
                ContentId = contentId;
                ShortLabel = shortLabel;
                DisplayName = displayName;
                Category = category;
                Accent = accent;
            }

            /// <summary>Palette identity, and the value carried in selectedTowerRole.</summary>
            public int Role { get; }

            /// <summary>Matches the simulation's ContentId, e.g. "tower.arrow".</summary>
            public string ContentId { get; }

            /// <summary>Card label. Kept short enough to fit the palette button.</summary>
            public string ShortLabel { get; }

            public string DisplayName { get; }

            public int Category { get; }

            public Color Accent { get; }

            /// <summary>Trailing segment of the content id, e.g. "arrow" — used by visual lookups.</summary>
            public string RoleId => ContentId.StartsWith("tower.") ? ContentId.Substring(6) : ContentId;
        }

        public static readonly Entry[] Entries =
        {
            new(0, "tower.arrow", "ARROW", "Arrow ward", CategoryArcane, TowerRolePalette.Arrow),
            new(1, "tower.control", "CTRL", "Control ward", CategoryArcane, TowerRolePalette.Control),
            new(2, "tower.relay", "RELAY", "Relay ward", CategoryArcane, TowerRolePalette.Relay),
            new(3, "tower.pulse", "PULSE", "Pulse ward", CategoryArcane, TowerRolePalette.Pulse),
            new(4, "tower.prism", "PRISM", "Prism ward", CategoryArcane, TowerRolePalette.Prism),
            // Roster expansion A6; colour lives in TowerRolePalette with the rest of the line.
            new(15, "tower.twin_crescent", "TWIN", "Twin crescent ward", CategoryArcane, TowerRolePalette.TwinCrescent),

            new(5, "tower.gatling", "GATLING", "Gatling turret", CategoryFoundry, new Color(0.87f, 0.62f, 0.28f)),
            new(6, "tower.tesla", "TESLA", "Tesla coil spire", CategoryFoundry, new Color(0.42f, 0.78f, 1f)),
            new(7, "tower.foundry", "FOUNDRY", "Foundry core", CategoryFoundry, new Color(1f, 0.48f, 0.24f)),
            new(8, "tower.barricade", "BULWARK", "Barricade bastion", CategoryFoundry, new Color(0.72f, 0.68f, 0.58f)),
            new(9, "tower.repair_drone", "DRONE", "Repair drone spire", CategoryFoundry, new Color(0.95f, 0.82f, 0.45f)),

            new(10, "tower.elder_canopy", "CANOPY", "Elder canopy", CategoryGrove, new Color(0.45f, 0.78f, 0.36f)),
            new(11, "tower.sapling", "SAPLING", "Sapling sentinel", CategoryGrove, new Color(0.62f, 0.85f, 0.42f)),
            new(12, "tower.bloomheart", "BLOOM", "Bloomheart totem", CategoryGrove, new Color(0.96f, 0.78f, 0.36f)),
            new(13, "tower.thorn_snare", "THORN", "Thorn snare totem", CategoryGrove, new Color(0.58f, 0.42f, 0.78f)),
            new(14, "tower.spore_cloud", "SPORE", "Spore cloud bloom", CategoryGrove, new Color(0.66f, 0.36f, 0.82f))
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
        /// Never returns null: an unknown role falls back to the first entry, matching the old
        /// switch statements, which all used Arrow as their default arm.
        /// </summary>
        public static Entry ForRole(int role) => ByRole.TryGetValue(role, out var entry) ? entry : Entries[0];

        /// <summary>
        /// Resolves a tower's simulation ContentId (e.g. "tower.arrow") to its catalog entry. For
        /// board-placed towers, where only the content id string is known, not the palette role index.
        /// Never returns null, same Arrow fallback as <see cref="ForRole"/>.
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
