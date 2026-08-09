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
            public Entry(int role, string contentId, string shortLabel, string displayName, int category, Color accent, string blurb = "")
            {
                Role = role;
                ContentId = contentId;
                ShortLabel = shortLabel;
                DisplayName = displayName;
                Category = category;
                Accent = accent;
                Blurb = blurb;
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

            /// <summary>One line of codex copy: what this tower is for, in a player's terms.</summary>
            /// <remarks>
            /// Here rather than on <c>TowerDefinition</c> deliberately. The simulation has no
            /// opinion about prose, and adding a description field to shared content would put copy
            /// edits through the assembly the balance tests run against. This class is already the
            /// place where presentation-only facts about a tower live.
            ///
            /// It states what the numbers cannot: the stat block shows a Barricade's 10 damage, not
            /// that its job is to occupy a cell. Where a number IS the point, the codex reads it
            /// from the simulation instead of repeating it here — a blurb that quotes a stat is a
            /// copy that goes stale at the next rebalance.
            /// </remarks>
            public string Blurb { get; }

            /// <summary>Trailing segment of the content id, e.g. "arrow" — used by visual lookups.</summary>
            public string RoleId => ContentId.StartsWith("tower.") ? ContentId.Substring(6) : ContentId;

            /// <summary>Name of this tower's icon PNG under Resources/Art/UI/Icons.</summary>
            public string IconResource => $"ui_icon_tower_{RoleId}_v01";
        }

        public static readonly Entry[] Entries =
        {
            new(0, "tower.arrow", "ARROW", "Arrow ward", CategoryArcane, TowerRolePalette.Arrow,
                "The opening tower. Cheap, quick to reload, and the only thing most lanes can afford on the first build."),
            new(1, "tower.control", "CTRL", "Control ward", CategoryArcane, TowerRolePalette.Control,
                "An Arrow that reaches a cell further for a slower reload. Bought for coverage, not for damage."),
            new(2, "tower.relay", "RELAY", "Relay ward", CategoryArcane, TowerRolePalette.Relay,
                "Pays its owner every time it lands a hit. It attacks poorly on purpose — the gold is the point, and it needs a lane busy enough to keep firing."),
            new(3, "tower.pulse", "PULSE", "Pulse ward", CategoryArcane, TowerRolePalette.Pulse,
                "Hits everything packed around its target. One cell of range, so it has to be built where the maze turns."),
            new(4, "tower.prism", "PRISM", "Prism ward", CategoryArcane, TowerRolePalette.Prism,
                "The longest reach in Arcane and the hardest single hit. Slow enough that it wants a brake in front of it."),
            // Roster expansion A6; colour lives in TowerRolePalette with the rest of the line.
            new(15, "tower.twin_crescent", "TWIN", "Twin crescent ward", CategoryArcane, TowerRolePalette.TwinCrescent,
                "Two crescents on one mount, firing in turn. Costs more than an Arrow for the same damage and buys steadier uptime rather than a bigger hit."),

            new(5, "tower.gatling", "GATLING", "Gatling turret", CategoryFoundry, new Color(0.87f, 0.62f, 0.28f),
                "The fastest reload on the roster. Shreds anything cheap and does very little to anything armoured."),
            new(6, "tower.tesla", "TESLA", "Tesla coil spire", CategoryFoundry, new Color(0.42f, 0.78f, 1f),
                "Foundry's answer to a crowd — real reach and a hit that carries past its target."),
            new(7, "tower.foundry", "FOUNDRY", "Foundry core", CategoryFoundry, new Color(1f, 0.48f, 0.24f),
                "The heaviest shell in the game, and it brakes what it hits. Twelve ticks between shots, so every one has to land."),
            new(8, "tower.barricade", "BASTION", "Barricade bastion", CategoryFoundry, new Color(0.72f, 0.68f, 0.58f),
                "Cheap enough that its job is occupying a cell. It shoots because it may as well; the maze is what you bought."),
            new(9, "tower.repair_drone", "DRONE", "Repair drone spire", CategoryFoundry, new Color(0.95f, 0.82f, 0.45f),
                "Shortens the reload of the Foundry towers around it. Mediocre alone, and the reason a Foundry cluster outperforms its stat lines."),

            new(10, "tower.elder_canopy", "CANOPY", "Elder canopy", CategoryGrove, new Color(0.45f, 0.78f, 0.36f),
                "Sees five cells — further than anything else on the board. Built at the back to cover the whole approach."),
            new(11, "tower.sapling", "SAPLING", "Sapling sentinel", CategoryGrove, new Color(0.62f, 0.85f, 0.42f),
                "The cheapest tower in the game. Grove's maze block, bought by the handful."),
            new(12, "tower.bloomheart", "BLOOM", "Bloomheart totem", CategoryGrove, new Color(0.96f, 0.78f, 0.36f),
                "Strengthens the Grove towers beside it. The tower that makes a grove worth planting in one place."),
            new(13, "tower.thorn_snare", "THORN", "Thorn snare totem", CategoryGrove, new Color(0.58f, 0.42f, 0.78f),
                "Lays bramble across the cells in its reach and crawls anything walking them. A slow is worth shots, so build it where towers can see."),
            new(14, "tower.spore_cloud", "SPORE", "Spore cloud bloom", CategoryGrove, new Color(0.66f, 0.36f, 0.82f),
                "Drifts a cloud over a wide patch of lane and damages everything under it. Slow, and indifferent to how many creeps arrive.")
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
