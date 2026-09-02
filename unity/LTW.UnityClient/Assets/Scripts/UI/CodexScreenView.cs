#nullable enable

using System.Collections.Generic;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Content;
using LTW.UnityClient.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace LTW.UnityClient.UI
{
    /// <summary>
    /// The codex: every ward and creep, one at a time, with its model turning and its stat sheet.
    /// </summary>
    /// <remarks>
    /// A plain class rather than a second MonoBehaviour. <see cref="ShellScreenView"/> owns the
    /// UIDocument and the screen switching for all four shell screens; this owns what one of them
    /// contains, which is a stage, a roster and a stat sheet. Merging the two would have put 3D
    /// staging inside the class whose job is showing and hiding elements.
    ///
    /// EVERY NUMBER HERE IS READ FROM THE SIMULATION, none are written down. That is not tidiness:
    /// the roster has been rebalanced repeatedly, and a codex that restated 46 authored numbers
    /// would be wrong within a release and wrong in the most damaging possible place — a screen
    /// whose entire purpose is telling the player what a unit does. The catalogs supply labels,
    /// colours and one line of prose each; <see cref="ContentCatalog"/> supplies the rest.
    /// </remarks>
    internal sealed class CodexScreenView
    {
        /// <summary>
        /// The authored roster, built once.
        /// </summary>
        /// <remarks>
        /// <c>SampleVerticalSliceContent.Create()</c> is a pure static factory with no dependencies,
        /// which is what makes a stat sheet possible on a screen with no match running. It does
        /// allocate a fresh catalog per call, hence the cache — the codex reads it on every
        /// selection change.
        ///
        /// Internal rather than private: the tablet-rail send/build cards (SendDockController,
        /// TouchPlacementController.Gui) reuse this cache and the trait strings below it, so the
        /// specialty line a card shows agrees with what the codex says for the same unit rather
        /// than restating the same rule a second way.
        /// </remarks>
        internal static ContentCatalog? catalog;

        internal static ContentCatalog Catalog => catalog ??= SampleVerticalSliceContent.Create();

        /// <summary>Fallback tick rate, matching the one the send dock uses when no driver is present.</summary>
        private const float FallbackTicksPerSecond = 4f;

        private readonly VisualElement root;
        private readonly Transform stageParent;
        private readonly UnitySimulationDriver? simulationDriver;

        private readonly Label? eyebrow;
        private readonly Label? unitName;
        private readonly Label? position;
        private readonly Label? blurb;
        private readonly Label? traits;
        private readonly VisualElement? preview;
        private readonly Label? previewPending;
        private readonly VisualElement? stats;
        private readonly VisualElement? rail;
        private readonly Button? tabWards;
        private readonly Button? tabCreeps;

        private UnitPreviewStage? stage;
        private VisualElement[] chips = System.Array.Empty<VisualElement>();

        private bool showingCreeps;
        private int index;

        /// <summary>What the sheet was last built for, so a per-frame Refresh is nearly free.</summary>
        private string renderedKey = string.Empty;

        internal CodexScreenView(VisualElement codexRoot, Transform stageOwner, UnitySimulationDriver? driver)
        {
            root = codexRoot;
            stageParent = stageOwner;
            simulationDriver = driver;

            eyebrow = root.Q<Label>("codex-eyebrow");
            unitName = root.Q<Label>("codex-name");
            position = root.Q<Label>("codex-position");
            blurb = root.Q<Label>("codex-blurb");
            traits = root.Q<Label>("codex-traits");
            preview = root.Q<VisualElement>("codex-preview");
            previewPending = root.Q<Label>("codex-preview-pending");
            stats = root.Q<VisualElement>("codex-stats");
            rail = root.Q<VisualElement>("codex-rail");
            tabWards = root.Q<Button>("codex-tab-wards");
            tabCreeps = root.Q<Button>("codex-tab-creeps");

            Wire("codex-prev", () => Step(-1));
            Wire("codex-next", () => Step(1));
            Wire("codex-tab-wards", () => SelectHalf(creeps: false));
            Wire("codex-tab-creeps", () => SelectHalf(creeps: true));

            BuildRail();
        }

        /// <summary>How many units are in the half currently shown.</summary>
        /// <remarks>
        /// Read from the catalog every time rather than cached or written down. The halves were the
        /// same size when this was written and are not any more — the Twin Crescent Ward made the
        /// wards sixteen against fifteen creeps — and a roster that grows on one side is the normal
        /// case, not the exception.
        /// </remarks>
        private int Count => showingCreeps ? CreepCatalog.Entries.Length : TowerCatalog.Entries.Length;

        /// <summary>Called every frame the codex is up; rebuilds only when the selection moved.</summary>
        internal void Refresh()
        {
            var key = $"{(showingCreeps ? "creep" : "tower")}:{index}";
            if (key == renderedKey)
            {
                return;
            }

            renderedKey = key;
            Render();
        }

        /// <summary>
        /// Releases the stage when the codex is left.
        /// </summary>
        /// <remarks>
        /// Called from <see cref="ShellScreenView"/> when it switches away, not from a destructor.
        /// The stage keeps a camera pointed at a render texture; left running it would cost a full
        /// render pass every frame for the remainder of the session, invisibly.
        /// </remarks>
        internal void Close()
        {
            stage?.Clear();
            renderedKey = string.Empty;
        }

        private void Wire(string name, System.Action handler)
        {
            var button = root.Q<Button>(name);
            if (button == null)
            {
                Debug.LogError($"CODEX missing button '{name}' in the shell document.");
                return;
            }

            button.clicked += handler;
        }

        private void Step(int delta)
        {
            var count = Count;
            index = ((index + delta) % count + count) % count;
        }

        private void SelectHalf(bool creeps)
        {
            if (showingCreeps == creeps)
            {
                return;
            }

            showingCreeps = creeps;
            index = 0;
            BuildRail();
        }

        // ------------------------------------------------------------------------------ rail

        /// <summary>
        /// Builds the icon strip: every unit in the current half, one row per category.
        /// </summary>
        /// <remarks>
        /// The whole half at once rather than the current category only, so any unit is one tap
        /// away instead of up to fifteen presses of NEXT.
        ///
        /// One row PER CATEGORY, explicitly, rather than a wrap. The first version let fifteen
        /// chips wrap five to a row, which landed on the category boundaries exactly because every
        /// category held five — and then the Twin Crescent Ward made Arcane six, so the wrap put
        /// one Arcane tower at the head of a row of Foundry ones and the grouping stopped meaning
        /// anything. A layout whose correctness depends on every category being the same size is a
        /// layout that breaks on the next unit added; this one does not care.
        /// </remarks>
        private void BuildRail()
        {
            if (rail == null)
            {
                return;
            }

            rail.Clear();

            var count = Count;
            chips = new VisualElement[count];

            var categories = showingCreeps ? CreepCatalog.CategoryLabels.Length : TowerCatalog.CategoryLabels.Length;
            for (var category = 0; category < categories; category++)
            {
                var row = new VisualElement { name = $"codex-rail-row-{category}" };
                row.AddToClassList("ltw-codex-rail-row");
                row.pickingMode = PickingMode.Ignore;

                for (var slot = 0; slot < count; slot++)
                {
                    if (CategoryOf(slot) != category)
                    {
                        continue;
                    }

                    row.Add(BuildChip(slot));
                }

                rail.Add(row);
            }
        }

        private int CategoryOf(int slot) =>
            showingCreeps ? CreepCatalog.Entries[slot].Category : TowerCatalog.Entries[slot].Category;

        private VisualElement BuildChip(int slot)
        {
            {
                var target = slot;
                var chip = new Button(() =>
                {
                    index = target;
                })
                {
                    name = $"codex-chip-{slot}"
                };
                chip.AddToClassList("ltw-codex-chip");

                var iconName = showingCreeps
                    ? CreepCatalog.Entries[slot].IconResource
                    : TowerCatalog.Entries[slot].IconResource;
                var icon = Resources.Load<Texture2D>($"Art/UI/Icons/{iconName}");
                if (icon != null)
                {
                    chip.style.backgroundImage = new StyleBackground(icon);
                }
                else
                {
                    // Falls back to the unit's short label rather than an empty square, because a
                    // roster entry whose icon has not been drawn yet is a real state here — an
                    // unlabelled blank chip is indistinguishable from a layout bug, and it is not
                    // tappable-looking, so the unit becomes unreachable from the rail.
                    chip.text = showingCreeps
                        ? CreepCatalog.Entries[slot].ShortLabel
                        : TowerCatalog.Entries[slot].ShortLabel;
                    chip.AddToClassList("ltw-codex-chip--textual");
                    Debug.LogWarning($"CODEX no icon at Resources/Art/UI/Icons/{iconName}; the rail is showing its label instead.");
                }

                chips[slot] = chip;
                return chip;
            }
        }

        // ---------------------------------------------------------------------------- render

        private void Render()
        {
            EnsureStage();

            for (var slot = 0; slot < chips.Length; slot++)
            {
                chips[slot]?.EnableInClassList("ltw-codex-chip--current", slot == index);
            }

            if (tabWards != null)
            {
                tabWards.EnableInClassList("ltw-tab--current", !showingCreeps);
            }

            if (tabCreeps != null)
            {
                tabCreeps.EnableInClassList("ltw-tab--current", showingCreeps);
            }

            if (position != null)
            {
                position.text = $"{index + 1} / {Count}";
            }

            if (showingCreeps)
            {
                RenderCreep(CreepCatalog.Entries[index]);
            }
            else
            {
                RenderTower(TowerCatalog.Entries[index]);
            }
        }

        private void EnsureStage()
        {
            if (stage != null)
            {
                return;
            }

            var stageObject = new GameObject("LTW Codex Preview Stage");
            stageObject.transform.SetParent(stageParent, false);
            stage = stageObject.AddComponent<UnitPreviewStage>();
        }

        private void RenderTower(TowerCatalog.Entry entry)
        {
            var definition = FindTower(entry.ContentId);
            BindPreview(stage != null && stage.ShowTower(entry.ContentId));

            if (eyebrow != null)
            {
                eyebrow.text = $"WARD · {TowerCatalog.CategoryLabels[entry.Category]}";
            }

            if (unitName != null)
            {
                unitName.text = definition?.Name.ToUpperInvariant() ?? entry.DisplayName.ToUpperInvariant();
                unitName.style.color = entry.Accent;
            }

            if (blurb != null)
            {
                blurb.text = entry.Blurb;
            }

            if (definition == null)
            {
                Debug.LogError($"CODEX no tower definition for '{entry.ContentId}'.");
                return;
            }

            var seconds = definition.AttackCooldownTicks / TicksPerSecond;
            var dps = seconds > 0f ? definition.Damage / seconds : 0f;

            BuildStats(new[]
            {
                (definition.Cost.Amount.ToString(), "GOLD"),
                (definition.Damage.ToString(), "DAMAGE"),
                (definition.RangeCells.ToString(), "RANGE"),
                ($"{seconds:0.0}s", "RELOAD"),
                ($"{dps:0.#}", "DPS"),
                (CategoryTierRules.Scale(definition.Damage, CategoryTierRules.TowerDamagePercentFor(CategoryTierRules.MaxTier)).ToString(), $"DMG T{CategoryTierRules.MaxTier}")
            });

            if (traits != null)
            {
                traits.text = TowerTraits(definition);
            }
        }

        private void RenderCreep(CreepCatalog.Entry entry)
        {
            var definition = FindCreep(entry.ContentId);
            BindPreview(stage != null && stage.ShowCreep(entry.ContentId));

            if (eyebrow != null)
            {
                eyebrow.text = $"CREEP · {CreepCatalog.CategoryLabels[entry.Category]}";
            }

            if (unitName != null)
            {
                unitName.text = definition?.Name.ToUpperInvariant() ?? entry.DisplayName.ToUpperInvariant();
                unitName.style.color = entry.Accent;
            }

            if (blurb != null)
            {
                blurb.text = entry.Blurb;
            }

            if (definition == null)
            {
                Debug.LogError($"CODEX no creep definition for '{entry.ContentId}'.");
                return;
            }

            // Cells per second, not the authored SpeedPerSecond, which is only half the fraction —
            // a creep advances SpeedPerSecond / MovementCost cells per TICK (CombatService). The
            // support creeps exist precisely because raising MovementCost makes them trail the pack
            // they follow, so a codex quoting SpeedPerSecond alone would show four of them as the
            // same speed as the creeps they are supposed to fall behind.
            var cellsPerSecond = definition.SpeedPerSecond * TicksPerSecond / definition.MovementCost;

            BuildStats(new[]
            {
                (definition.Cost.Amount.ToString(), "GOLD"),
                (definition.MaxHealth.ToString(), "HEALTH"),
                ($"{cellsPerSecond:0.0}", "CELLS/S"),
                ($"+{definition.IncomeGain.Amount}", "INCOME"),
                (definition.LeakBounty.Amount.ToString(), "LEAK PAY"),
                (CategoryTierRules.Scale(definition.MaxHealth, CategoryTierRules.CreepHealthPercentFor(CategoryTierRules.MaxTier)).ToString(), $"HP T{CategoryTierRules.MaxTier}")
            });

            if (traits != null)
            {
                traits.text = CreepTraits(definition);
            }
        }

        /// <summary>
        /// Points the preview element at the stage's render texture, or at the no-model state.
        /// </summary>
        /// <remarks>
        /// Re-assigned on every render rather than once, because the texture does not exist until
        /// the stage has mounted its first subject — binding it in the constructor would bind null
        /// and never recover.
        ///
        /// The no-model branch is load-bearing rather than defensive. Stats land before art on this
        /// roster, so a unit with no prefab is a state the codex will genuinely be asked to show;
        /// without this it kept whatever texture the PREVIOUS unit left bound, which is worse than
        /// an empty card because it silently attributes one tower's model to another.
        /// </remarks>
        private void BindPreview(bool hasModel)
        {
            if (preview == null)
            {
                return;
            }

            if (hasModel && stage?.Texture != null)
            {
                preview.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(stage.Texture));
            }
            else
            {
                preview.style.backgroundImage = StyleKeyword.None;
            }

            previewPending?.EnableInClassList("is-shown", !hasModel);
        }

        private float TicksPerSecond =>
            simulationDriver != null ? simulationDriver.TicksPerSecond : FallbackTicksPerSecond;

        private void BuildStats((string Value, string Label)[] tiles)
        {
            if (stats == null)
            {
                return;
            }

            stats.Clear();

            // Two rows of three. One row of six puts 42px numerals into 170px of width at the panel's
            // reference resolution, which is where a stat value stops being glanceable.
            for (var start = 0; start < tiles.Length; start += 3)
            {
                var row = new VisualElement();
                row.AddToClassList("ltw-stats");
                row.pickingMode = PickingMode.Ignore;

                for (var offset = 0; offset < 3 && start + offset < tiles.Length; offset++)
                {
                    var (value, label) = tiles[start + offset];
                    var tile = new VisualElement();
                    tile.AddToClassList("ltw-stat");
                    tile.pickingMode = PickingMode.Ignore;

                    var valueLabel = new Label(value);
                    valueLabel.AddToClassList("ltw-stat__value");
                    valueLabel.AddToClassList("ltw-stat__value--codex");
                    valueLabel.pickingMode = PickingMode.Ignore;

                    var nameLabel = new Label(label);
                    nameLabel.AddToClassList("ltw-stat__label");
                    nameLabel.pickingMode = PickingMode.Ignore;

                    tile.Add(valueLabel);
                    tile.Add(nameLabel);
                    row.Add(tile);
                }

                stats.Add(row);
            }
        }

        // ---------------------------------------------------------------------------- traits

        /// <summary>
        /// The behaviours a stat block cannot show, with their numbers read from the rules that
        /// implement them.
        /// </summary>
        /// <remarks>
        /// Quoting <see cref="SupportAuraField"/>'s own constants rather than writing "25%" into a
        /// string is the whole discipline of this screen in one method. Those numbers have already
        /// moved once — the Binder's extra ticks and the Repair Drone's relief both doubled when
        /// every cooldown on the roster did — and a hardcoded codex would have gone quietly wrong
        /// on that change while continuing to look authoritative.
        /// </remarks>
        internal static string TowerTraits(TowerDefinition definition)
        {
            var parts = new List<string> { RoleLabel(definition.Role) };

            if (definition.SignalGoldPerHit > 0)
            {
                parts.Add($"earns {definition.SignalGoldPerHit} gold on every hit");
            }

            if (definition.SlowsCreeps)
            {
                parts.Add("brakes creeps walking its range");
            }

            if (definition.CountersFlyers)
            {
                parts.Add($"+{CombatService.AntiAirDamageBonusPercent}% damage against flying creeps");
            }

            return string.Join("  ·  ", parts);
        }

        internal static string RoleLabel(TowerRole role) => role switch
        {
            TowerRole.Aoe => "Hits more than one creep",
            TowerRole.Brake => "Slows what it shoots",
            TowerRole.Economy => "Earns from its own fire",
            TowerRole.Support => "Helps the towers around it",
            TowerRole.Wall => "Cheap enough that its job is the cell",
            _ => "Single-target damage"
        };

        internal static string CreepTraits(CreepDefinition definition)
        {
            var parts = new List<string>();

            switch (definition.Support)
            {
                case CreepSupportRole.Pacesetter:
                    parts.Add($"Speeds friendly creeps within {SupportAuraField.PathRadius} cells");
                    break;
                case CreepSupportRole.Mender:
                    parts.Add($"Heals nearby friendly creeps every {SupportAuraField.MenderHealIntervalTicks} ticks");
                    break;
                case CreepSupportRole.Bulwark:
                    parts.Add($"Removes {SupportAuraField.BulwarkDamageReductionPercent}% of damage taken nearby");
                    break;
                case CreepSupportRole.Binder:
                    parts.Add($"Adds {SupportAuraField.BinderCooldownExtraTicks} ticks to the reload of towers within {SupportAuraField.BinderTowerRangeCells} cells");
                    break;
            }

            if (definition.IgnoresMaze)
            {
                parts.Add("Walks over towers and ignores the maze");
            }

            if (definition.MovementCost > CreepDefinition.DefaultMovementCost)
            {
                parts.Add("Trails behind the pack it follows");
            }

            if (definition.IgnoresSendCooldown)
            {
                parts.Add("Exempt from the send cooldown");
            }

            if (parts.Count == 0)
            {
                parts.Add($"Pays {definition.KillBounty.Amount} gold to whoever kills it");
            }

            return string.Join("  ·  ", parts);
        }

        // ---------------------------------------------------------------------------- lookups

        internal static TowerDefinition? FindTower(string contentId)
        {
            foreach (var tower in Catalog.Towers)
            {
                if (tower.Id.Value == contentId)
                {
                    return tower;
                }
            }

            return null;
        }

        internal static CreepDefinition? FindCreep(string contentId)
        {
            foreach (var creep in Catalog.Creeps)
            {
                if (creep.Id.Value == contentId)
                {
                    return creep;
                }
            }

            return null;
        }
    }
}
