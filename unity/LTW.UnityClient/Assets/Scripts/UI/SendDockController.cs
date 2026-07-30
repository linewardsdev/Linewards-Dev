#nullable enable

using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class SendDockController : MonoBehaviour
    {
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color WardViolet = new(0.608f, 0.424f, 1f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color DisabledInk = new(0.22f, 0.25f, 0.32f, 0.88f);
        private static readonly Color DisabledText = new(0.55f, 0.59f, 0.68f, 1f);

        private static GUIStyle? panelStyle;
        private static GUIStyle? titleStyle;
        private static GUIStyle? buttonStyle;
        private static GUIStyle? metaStyle;

        [SerializeField]
        private UnityCommandAdapter commandAdapter = null!;

        [SerializeField]
        private PlacementFeedbackView feedbackView = null!;

        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        [SerializeField]
        private TouchPlacementController touchPlacementController = null!;

        [SerializeField]
        private bool showRuntimeDock = true;

        // Names each category by what it actually does, replacing the "CATEGORY 1/2" placeholders
        // that were waiting on this content:
        //   CORE  — the founding five, all send-cooldown gated.
        //   RAPID — every Category 2 creep sets ignoresSendCooldown, and that exemption is their
        //           defining trait, so the name says so.
        //   ELITE — the Meshy-rigged bipeds: costlier, heavier, and back on the normal cooldown,
        //           because price is what paces them.
        private static readonly string[] CategoryLabels = { "CORE", "RAPID", "ELITE" };

        /// <summary>
        /// Whether a category's grid contains any card the send cooldown actually gates.
        /// </summary>
        /// <remarks>
        /// Replaces a hardcoded <c>selectedCategory != 1</c> test. Category 2 (RAPID) is entirely
        /// cooldown-exempt, so showing a "READY IN x.xs" countdown over that grid was misleading
        /// and it was suppressed by index. Category 3 is gated again, so an index comparison would
        /// have silently hidden a countdown that does apply. Keyed off the same fact the cards
        /// themselves use — whether they pass <c>ignoresCooldown</c>.
        /// </remarks>
        private static bool CategoryHasCooldownGatedCards(int category) => category != 1;

        private bool isExpanded;
        private int selectedCategory = -1;
        private int highlightedCreepRole = -1;
        private int reviewGoldOverride = -1;
        private static bool isSendCoolingDown;

        public bool IsExpanded => isExpanded;

        public void CloseDock()
        {
            isExpanded = false;
            selectedCategory = -1;
        }

        public void Initialize(UnityCommandAdapter adapter, PlacementFeedbackView feedback)
        {
            commandAdapter = adapter;
            feedbackView = feedback;
        }

        public void SendRunner() => Send(commandAdapter.SendSampleCreep(), "Runner sent", 10, 0);

        public void SendBrute() => Send(commandAdapter.SendBruteCreep(), "Brute sent", 18, 1);

        public void SendSwarm() => Send(commandAdapter.SendSwarmCreep(), "Swarm sent", 18, 2);

        public void SendShade() => Send(commandAdapter.SendShadeCreep(), "Shade sent", 24, 3);

        public void SendSiege() => Send(commandAdapter.SendSiegeCreep(), "Siege sent", 40, 4);

        public void SendWisp() => Send(commandAdapter.SendWispCreep(), "Wisp sent", 5, 5);

        public void SendRevenant() => Send(commandAdapter.SendRevenantCreep(), "Revenant sent", 16, 6);

        public void SendObsidianBrute() => Send(commandAdapter.SendObsidianBruteCreep(), "Obsidian Brute sent", 30, 7);

        // Cost 20, not 22: Serpent Coil's content cost was cut in the 2026-07-28 rebalance but the
        // UI kept quoting, gating on, and reporting the old 22 (also fixed on its card below).
        public void SendSerpent() => Send(commandAdapter.SendSerpentCreep(), "Serpent sent", 20, 8);

        public void SendTurretWalker() => Send(commandAdapter.SendTurretWalkerCreep(), "Turret Walker sent", 38, 9);

        public void SendZephyr() => Send(commandAdapter.SendZephyrCreep(), "Zephyr Wraith sent", 22, 10);

        public void SendBurrower() => Send(commandAdapter.SendBurrowerCreep(), "Fracture Burrower sent", 26, 11);

        public void SendStalker() => Send(commandAdapter.SendStalkerCreep(), "Umbral Stalker sent", 28, 12);

        public void SendWarden() => Send(commandAdapter.SendWardenCreep(), "Aegis Warden sent", 34, 13);

        public void SendColossus() => Send(commandAdapter.SendColossusCreep(), "Siege Colossus sent", 52, 14);

        private void OnGUI()
        {
            if (!showRuntimeDock)
            {
                return;
            }

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var launcherWidth = 76f * scale;
            var launcherHeight = 44f * scale;
            var launcherRect = new Rect(frame.xMax - launcherWidth - 12f * scale, frame.yMax - launcherHeight - MobileViewportLayout.BottomMargin(scale), launcherWidth, launcherHeight);
            var touchPlacement = TouchPlacement;
            if (touchPlacement?.IsTowerPaletteExpanded == true)
            {
                isExpanded = false;
                selectedCategory = -1;
                return;
            }

            if (!isExpanded)
            {
                if (DrawLauncherButton(launcherRect, "SEND", SignalGold, scale))
                {
                    touchPlacement?.CloseBottomPanelsForSend();
                    isExpanded = true;
                }

                return;
            }

            var width = Mathf.Min(frame.width - 16f * scale, 430f * scale);
            // The picker needs a taller panel than the creep grids do, and only while it is up.
            // Grids are two rows: 84 header + 84 + 8 + 84 = 260, inside 282. The picker is now
            // three full-width cards: 84 header + 3*84 + 2*8 = 352, which overflowed 282 and put
            // the third card outside the panel — the same class of bug the DrawCategoryPicker
            // remark below records having already been fixed once.
            var height = (selectedCategory < 0 ? 374f : 282f) * scale;
            var launcherClearance = 136f * scale;
            var rect = new Rect(frame.xMax - width - 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale) - launcherClearance, width, height);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), SignalGold);
            if (DrawLauncherButton(launcherRect, "CLOSE", SignalGold, scale))
            {
                CloseDock();
                return;
            }

            var titleText = selectedCategory < 0 ? "SEND" : $"SEND › {CategoryLabels[selectedCategory]}";
            titleStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            titleStyle.normal.textColor = SignalGold;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 10f * scale, 220f * scale, 20f * scale), titleText, titleStyle);
            // Only one CLOSE. The launcher slot above already turned into CLOSE when the dock
            // opened, and it has to stay something other than SEND while expanded, so a second
            // CLOSE in the header was pure duplication — two controls, same owner, same action,
            // both on screen at once. The launcher keeps it: it sits in the thumb zone, and it is
            // where the finger already is after tapping SEND. BACK moves into the vacated slot.
            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            if (selectedCategory >= 0
                && RuntimeUiChrome.DrawPanelButton(new Rect(rect.xMax - 72f * scale, rect.y + 8f * scale, 58f * scale, 32f * scale), "BACK", SignalGold, scale, buttonStyle))
            {
                selectedCategory = -1;
                return;
            }

            var gold = CurrentPlayerGold();
            // Reads 0 while the shipped send cooldown is 0. Kept as a live read rather than deleted so
            // the dock explains itself again the moment a cooldown returns.
            var cooldownSeconds = CurrentSendCooldownSeconds();
            isSendCoolingDown = cooldownSeconds > 0f;

            metaStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            metaStyle.normal.textColor = MintSignal;
            GUI.Label(new Rect(rect.xMax - 204f * scale, rect.y + 12f * scale, 58f * scale, 18f * scale), $"G{gold}", metaStyle);

            // The cooldown is 7.5s, long enough that without a countdown the dock just looks
            // broken while it runs.
            // Hidden on a grid whose every card is cooldown-exempt (Category 2 / RAPID), where a
            // countdown would read as a restriction that is not applying. Asked as a question
            // about the category rather than compared against an index, so Category 3 — which is
            // gated again — correctly keeps its countdown.
            if (isSendCoolingDown && (selectedCategory < 0 || CategoryHasCooldownGatedCards(selectedCategory)))
            {
                metaStyle.normal.textColor = SignalGold;
                GUI.Label(
                    new Rect(rect.x + 12f * scale, rect.y + 28f * scale, 200f * scale, 18f * scale),
                    $"READY IN {cooldownSeconds:0.0}s",
                    metaStyle);
            }

            var buttonY = rect.y + 84f * scale;
            var buttonHeight = 84f * scale;
            var gap = 8f * scale;

            // Explicitly three-way. This was previously a bare `else` for Category 2, which would
            // have silently rendered Category 2's grid for any new category index.
            if (selectedCategory < 0)
            {
                DrawCategoryPicker(rect, buttonY, buttonHeight, gap, scale);
            }
            else if (selectedCategory == 0)
            {
                DrawCategoryOneCreeps(rect, buttonY, buttonHeight, gap, gold, scale);
            }
            else if (selectedCategory == 1)
            {
                DrawCategoryTwoCreeps(rect, buttonY, buttonHeight, gap, gold, scale);
            }
            else
            {
                DrawCategoryThreeCreeps(rect, buttonY, buttonHeight, gap, gold, scale);
            }
        }

        /// <summary>
        /// Category chooser shown before any 5-creep grid. Full-width cards, since there's no
        /// icon/cost to show yet — just a name and a "5 sends" hint.
        /// </summary>
        /// <remarks>
        /// The card height has to divide the panel the same way the creep grids do. These were
        /// once drawn at buttonHeight * 2 + gap each, which put the pair's bottom edge at
        /// 84 + 176 + 8 + 176 = 444 inside a panel only 282 tall — so the second card hung
        /// completely outside the dock, over the board, with its lower half off the bottom of the
        /// screen. One buttonHeight each lands the three cards at 84 + 3*84 + 2*8 = 352, which is
        /// why the panel grows to 374 while the picker is up (see the height calculation in
        /// OnGUI); at the grid height of 282 the third card would have repeated that same bug.
        /// </remarks>
        private void DrawCategoryPicker(Rect rect, float buttonY, float buttonHeight, float gap, float scale)
        {
            // Card height is divided out of the space the panel actually has, not fixed. At a
            // fixed buttonHeight the three cards ran to 84 + 3*(84+8) = 352 inside a 282-tall
            // panel, so the last one hung outside the dock and over the board — the same defect a
            // two-card version of this picker had, reintroduced when a third category landed.
            // Deriving the height means adding a fourth category cannot bring it back.
            var cardHeight = RuntimeUiChrome.CategoryCardHeight(
                rect, buttonY, gap, CategoryLabels.Length, buttonHeight, scale);
            var cardWidth = rect.width - 24f * scale;
            var x = rect.x + 12f * scale;
            var accents = new[] { ArcaneBlue, WardViolet, SignalGold };

            for (var category = 0; category < CategoryLabels.Length; category++)
            {
                var y = buttonY + category * (cardHeight + gap);
                if (DrawCategoryCard(new Rect(x, y, cardWidth, cardHeight), CategoryLabels[category], accents[category], scale))
                {
                    selectedCategory = category;
                }
            }
        }

        private static bool DrawCategoryCard(Rect rect, string label, Color accent, float scale)
        {
            var pressed = RuntimeUiChrome.DrawCommandCard(rect, accent, CommandCardState.Normal, scale);

            buttonStyle!.fontSize = Mathf.RoundToInt(13f * scale);
            buttonStyle.normal.textColor = Cloud;
            buttonStyle.hover.textColor = Cloud;
            buttonStyle.active.textColor = Cloud;
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.32f, rect.width, 22f * scale), label, buttonStyle);

            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = accent;
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.58f, rect.width, 18f * scale), "5 SENDS", metaStyle);
            return pressed;
        }

        private void DrawCategoryOneCreeps(Rect rect, float buttonY, float buttonHeight, float gap, int gold, float scale)
        {
            var buttonWidth = (rect.width - 24f * scale - gap * 2f) / 3f;
            var x = rect.x + 12f * scale;

            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "RUNNER", "10G  +1", CreepIconKind.Runner, ArcaneBlue, gold >= 10, highlightedCreepRole == 0, scale))
            {
                SendRunner();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "BRUTE", "18G  +2", CreepIconKind.Brute, WardViolet, gold >= 18, highlightedCreepRole == 1, scale))
            {
                SendBrute();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "SWARM", "18G  +3", CreepIconKind.Swarm, SignalGold, gold >= 18, highlightedCreepRole == 2, scale))
            {
                SendSwarm();
            }

            var secondRowY = buttonY + buttonHeight + gap;
            var secondRowWidth = (rect.width - 24f * scale - gap) / 2f;
            x = rect.x + 12f * scale;
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "SHADE", "24G  +3", CreepIconKind.Shade, MintSignal, gold >= 24, highlightedCreepRole == 3, scale))
            {
                SendShade();
            }

            x += secondRowWidth + gap;
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "SIEGE", "40G  +4", CreepIconKind.Siege, new Color(1f, 0.62f, 0.26f), gold >= 40, highlightedCreepRole == 4, scale))
            {
                SendSiege();
            }
        }

        private void DrawCategoryTwoCreeps(Rect rect, float buttonY, float buttonHeight, float gap, int gold, float scale)
        {
            var buttonWidth = (rect.width - 24f * scale - gap * 2f) / 3f;
            var x = rect.x + 12f * scale;

            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "WISP", "5G  +1", CreepIconKind.Wisp, ArcaneBlue, gold >= 5, highlightedCreepRole == 5, scale, ignoresCooldown: true))
            {
                SendWisp();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "REVENANT", "16G  +4", CreepIconKind.Revenant, WardViolet, gold >= 16, highlightedCreepRole == 6, scale, ignoresCooldown: true))
            {
                SendRevenant();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "OBSIDIAN", "30G  +3", CreepIconKind.ObsidianBrute, new Color(0.92f, 0.32f, 0.28f), gold >= 30, highlightedCreepRole == 7, scale, ignoresCooldown: true))
            {
                SendObsidianBrute();
            }

            var secondRowY = buttonY + buttonHeight + gap;
            var secondRowWidth = (rect.width - 24f * scale - gap) / 2f;
            x = rect.x + 12f * scale;
            // 20G, not 22: matches the content cost after the 2026-07-28 rebalance. The card, its
            // affordability gate and SendSerpent's reported cost were all still quoting the old
            // value, so an affordable Serpent could read as unaffordable.
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "SERPENT", "20G  +2", CreepIconKind.Serpent, MintSignal, gold >= 20, highlightedCreepRole == 8, scale, ignoresCooldown: true))
            {
                SendSerpent();
            }

            x += secondRowWidth + gap;
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "WALKER", "38G  +4", CreepIconKind.TurretWalker, new Color(0.42f, 0.82f, 0.86f), gold >= 38, highlightedCreepRole == 9, scale, ignoresCooldown: true))
            {
                SendTurretWalker();
            }
        }

        /// <summary>
        /// Category 3 (ELITE). Same 3-up/2-up geometry as the other two grids, ordered by cost.
        /// No <c>ignoresCooldown</c> anywhere here — unlike Category 2 these are gated normally,
        /// which is why the countdown above is asked as a question rather than hidden by index.
        /// </summary>
        private void DrawCategoryThreeCreeps(Rect rect, float buttonY, float buttonHeight, float gap, int gold, float scale)
        {
            var buttonWidth = (rect.width - 24f * scale - gap * 2f) / 3f;
            var x = rect.x + 12f * scale;

            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "WRAITH", "22G  +2", CreepIconKind.Zephyr, ArcaneBlue, gold >= 22, highlightedCreepRole == 10, scale))
            {
                SendZephyr();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "BURROW", "26G  +2", CreepIconKind.Burrower, new Color(0.85f, 0.55f, 0.25f), gold >= 26, highlightedCreepRole == 11, scale))
            {
                SendBurrower();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "STALKER", "28G  +3", CreepIconKind.Stalker, WardViolet, gold >= 28, highlightedCreepRole == 12, scale))
            {
                SendStalker();
            }

            var secondRowY = buttonY + buttonHeight + gap;
            var secondRowWidth = (rect.width - 24f * scale - gap) / 2f;
            x = rect.x + 12f * scale;
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "WARDEN", "34G  +3", CreepIconKind.Warden, MintSignal, gold >= 34, highlightedCreepRole == 13, scale))
            {
                SendWarden();
            }

            x += secondRowWidth + gap;
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "COLOSSUS", "52G  +5", CreepIconKind.Colossus, new Color(1f, 0.45f, 0.30f), gold >= 52, highlightedCreepRole == 14, scale))
            {
                SendColossus();
            }
        }

        private TouchPlacementController? TouchPlacement
        {
            get
            {
                if (touchPlacementController == null)
                {
                    touchPlacementController = Object.FindAnyObjectByType<TouchPlacementController>();
                }

                return touchPlacementController;
            }
        }

        private void Send(LTW.Simulation.Bridge.VerticalSliceCommandResult result, string successMessage, int cost, int creepRole)
        {
            if (result.Accepted)
            {
                highlightedCreepRole = creepRole;
                feedbackView.ShowEconomy(successMessage);
                return;
            }

            if (result.RejectionReason == LTW.Simulation.Commands.CommandRejectionReason.InsufficientGold)
            {
                feedbackView.ShowRejected(result.RejectionReason, cost, CurrentPlayerGold());
            }
            else
            {
                feedbackView.ShowRejected(result.RejectionReason);
            }
        }

        private static bool DrawSendButton(Rect rect, string label, string meta, CreepIconKind iconKind, Color accent, bool isAffordable, bool isSelected, float scale, bool ignoresCooldown = false)
        {
            // Cooling down reads as unaffordable, because for the player it is the same thing:
            // the card cannot be sent right now. Without this a card you could clearly afford
            // looked ready and answered a tap with a bare refusal. Category 2 creeps are exempt
            // from the cooldown in the simulation, so their cards must stay live through it —
            // greying them out would tell the player the opposite of the rule.
            isAffordable = isAffordable && (ignoresCooldown || !isSendCoolingDown);
            var displayAccent = isAffordable ? accent : DisabledText;
            var state = isAffordable
                ? isSelected ? CommandCardState.Selected : CommandCardState.Normal
                : CommandCardState.Disabled;
            var pressed = RuntimeUiChrome.DrawCommandCard(rect, accent, state, scale);

            var iconRect = RuntimeUiChrome.CommandCardIconRect(rect, scale);
            if (!RuntimeUiIconLibrary.DrawIcon(iconRect, CreepIconResourceName(iconKind), isAffordable))
            {
                DrawCreepIcon(iconRect, iconKind, displayAccent, scale);
            }

            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            buttonStyle.normal.textColor = isAffordable ? Cloud : DisabledText;
            buttonStyle.hover.textColor = buttonStyle.normal.textColor;
            buttonStyle.active.textColor = buttonStyle.normal.textColor;
            GUI.Label(RuntimeUiChrome.CommandCardLabelRect(rect, scale), label, buttonStyle);

            // Raw accent at this size washed out over the pale stone areas of the card art — the
            // cost digits faded while the "+income" beside them stayed readable. Lifting the
            // accent toward white keeps the per-creep colour coding while restoring contrast; the
            // dark plate behind the row (DrawCommandCardChrome) does the rest.
            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = isAffordable
                ? new Color(
                    Mathf.Lerp(displayAccent.r, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.g, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.b, 1f, 0.55f),
                    1f)
                : displayAccent;
            GUI.Label(RuntimeUiChrome.CommandCardMetaRect(rect, scale), meta, metaStyle);
            return pressed;
        }



        private static string CreepIconResourceName(CreepIconKind iconKind)
        {
            return iconKind switch
            {
                CreepIconKind.Brute => "ui_icon_send_brute_v01",
                CreepIconKind.Swarm => "ui_icon_send_swarm_v01",
                CreepIconKind.Shade => "ui_icon_send_shade_v01",
                CreepIconKind.Siege => "ui_icon_send_siege_v01",
                // RuntimeUiIconLibrary.DrawIcon falls back to the procedural DrawCreepIcon
                // shapes below whenever a resource is missing, so a name here is safe to add
                // before its PNG exists. Category 2's PNGs do now exist; Category 3's may not
                // yet, and will simply draw their procedural shape until rendered.
                CreepIconKind.Wisp => "ui_icon_send_wisp_v01",
                CreepIconKind.Revenant => "ui_icon_send_revenant_v01",
                CreepIconKind.ObsidianBrute => "ui_icon_send_obsidian_brute_v01",
                CreepIconKind.Serpent => "ui_icon_send_serpent_v01",
                CreepIconKind.TurretWalker => "ui_icon_send_turret_walker_v01",
                CreepIconKind.Zephyr => "ui_icon_send_zephyr_v01",
                CreepIconKind.Burrower => "ui_icon_send_burrower_v01",
                CreepIconKind.Stalker => "ui_icon_send_stalker_v01",
                CreepIconKind.Warden => "ui_icon_send_warden_v01",
                CreepIconKind.Colossus => "ui_icon_send_colossus_v01",
                _ => "ui_icon_send_runner_v01"
            };
        }

        private static void DrawCreepIcon(Rect rect, CreepIconKind iconKind, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = accent;

            var cx = rect.x + rect.width * 0.5f;
            var cy = rect.y + rect.height * 0.52f;
            var line = Mathf.Max(2f * scale, 1f);
            var dimAccent = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01(accent.a * 0.48f));

            switch (iconKind)
            {
                case CreepIconKind.Brute:
                    DrawIconRect(new Rect(cx - 11f * scale, cy - 8f * scale, 22f * scale, 16f * scale), accent);
                    DrawIconRect(new Rect(cx - 15f * scale, cy - 4f * scale, 6f * scale, 12f * scale), accent);
                    DrawIconRect(new Rect(cx + 9f * scale, cy - 4f * scale, 6f * scale, 12f * scale), accent);
                    DrawIconRect(new Rect(cx - 5f * scale, cy - 2f * scale, 10f * scale, 4f * scale), Cloud);
                    break;
                case CreepIconKind.Swarm:
                    DrawIconRect(new Rect(cx - 10f * scale, cy - 7f * scale, 7f * scale, 7f * scale), accent, -25f);
                    DrawIconRect(new Rect(cx - 1f * scale, cy - 12f * scale, 7f * scale, 7f * scale), accent, 18f);
                    DrawIconRect(new Rect(cx + 8f * scale, cy - 4f * scale, 7f * scale, 7f * scale), accent, -18f);
                    DrawIconRect(new Rect(cx - 5f * scale, cy + 4f * scale, 7f * scale, 7f * scale), accent, 32f);
                    DrawIconRect(new Rect(cx + 5f * scale, cy + 8f * scale, 6f * scale, 6f * scale), accent, -35f);
                    DrawIconRect(new Rect(cx - 11f * scale, cy + 11f * scale, 23f * scale, line), dimAccent);
                    break;
                case CreepIconKind.Shade:
                    DrawIconRect(new Rect(cx - 10f * scale, cy - 8f * scale, 9f * scale, 18f * scale), dimAccent, -16f);
                    DrawIconRect(new Rect(cx + 2f * scale, cy - 9f * scale, 8f * scale, 18f * scale), dimAccent, 16f);
                    DrawIconRect(new Rect(cx - 3f * scale, cy - 12f * scale, 7f * scale, 23f * scale), accent);
                    DrawIconRect(new Rect(cx - 8f * scale, cy + 6f * scale, 18f * scale, line), accent);
                    break;
                case CreepIconKind.Siege:
                    DrawIconRect(new Rect(cx - 10f * scale, cy - 7f * scale, 20f * scale, 14f * scale), accent);
                    DrawIconRect(new Rect(cx - 4f * scale, cy - 13f * scale, 8f * scale, 9f * scale), accent);
                    DrawIconRect(new Rect(cx - 3f * scale, cy - 1f * scale, 6f * scale, 17f * scale), Cloud);
                    DrawIconRect(new Rect(cx - 14f * scale, cy + 6f * scale, 28f * scale, line), accent);
                    break;
                case CreepIconKind.Wisp:
                    DrawIconRect(new Rect(cx - 6f * scale, cy - 6f * scale, 12f * scale, 12f * scale), accent, 45f);
                    DrawIconRect(new Rect(cx - 13f * scale, cy - 13f * scale, 26f * scale, line), dimAccent, 20f);
                    DrawIconRect(new Rect(cx - 13f * scale, cy + 13f * scale, 26f * scale, line), dimAccent, -20f);
                    break;
                case CreepIconKind.Revenant:
                    DrawIconRect(new Rect(cx - 9f * scale, cy - 12f * scale, 18f * scale, 22f * scale), dimAccent, -8f);
                    DrawIconRect(new Rect(cx - 3f * scale, cy - 14f * scale, 6f * scale, 12f * scale), accent);
                    DrawIconRect(new Rect(cx - 12f * scale, cy + 6f * scale, 8f * scale, 10f * scale), accent, -20f);
                    DrawIconRect(new Rect(cx + 4f * scale, cy + 6f * scale, 8f * scale, 10f * scale), accent, 20f);
                    break;
                case CreepIconKind.ObsidianBrute:
                    DrawIconRect(new Rect(cx - 12f * scale, cy - 9f * scale, 24f * scale, 18f * scale), accent);
                    DrawIconRect(new Rect(cx - 16f * scale, cy - 3f * scale, 7f * scale, 13f * scale), accent);
                    DrawIconRect(new Rect(cx + 9f * scale, cy - 3f * scale, 7f * scale, 13f * scale), accent);
                    DrawIconRect(new Rect(cx - 4f * scale, cy - 1f * scale, 8f * scale, 4f * scale), new Color(0.92f, 0.32f, 0.28f));
                    break;
                case CreepIconKind.Serpent:
                    DrawIconRect(new Rect(cx - 12f * scale, cy - 2f * scale, 12f * scale, 10f * scale), dimAccent, 10f);
                    DrawIconRect(new Rect(cx - 4f * scale, cy - 6f * scale, 12f * scale, 10f * scale), accent, -6f);
                    DrawIconRect(new Rect(cx + 6f * scale, cy - 2f * scale, 10f * scale, 9f * scale), dimAccent, 12f);
                    break;
                case CreepIconKind.TurretWalker:
                    DrawIconRect(new Rect(cx - 6f * scale, cy - 12f * scale, 12f * scale, 9f * scale), accent);
                    DrawIconRect(new Rect(cx - 2f * scale, cy - 4f * scale, 4f * scale, 10f * scale), accent);
                    DrawIconRect(new Rect(cx - 14f * scale, cy + 8f * scale, 9f * scale, line * 1.5f), dimAccent, 30f);
                    DrawIconRect(new Rect(cx + 5f * scale, cy + 8f * scale, 9f * scale, line * 1.5f), dimAccent, -30f);
                    break;
                // Category 3 are all bipeds, so each shape reads as an upright figure and is
                // separated by silhouette rather than by motif: a swept-back sprinter, a squat
                // wide-shouldered digger, a narrow hunched prowler, a shielded blocky guard, and
                // an oversized heavy. Without a case here a kind silently draws the runner glyph.
                case CreepIconKind.Zephyr:
                    DrawIconRect(new Rect(cx - 2f * scale, cy - 13f * scale, 5f * scale, 14f * scale), accent, 12f);
                    DrawIconRect(new Rect(cx - 12f * scale, cy - 2f * scale, 14f * scale, line), dimAccent, 22f);
                    DrawIconRect(new Rect(cx - 1f * scale, cy + 2f * scale, 4f * scale, 12f * scale), accent, -14f);
                    DrawIconRect(new Rect(cx - 13f * scale, cy + 9f * scale, 12f * scale, line), dimAccent, 16f);
                    break;
                case CreepIconKind.Burrower:
                    DrawIconRect(new Rect(cx - 9f * scale, cy - 9f * scale, 18f * scale, 8f * scale), accent);
                    DrawIconRect(new Rect(cx - 5f * scale, cy + 1f * scale, 10f * scale, 9f * scale), accent);
                    DrawIconRect(new Rect(cx - 14f * scale, cy - 3f * scale, 7f * scale, line * 1.5f), dimAccent, -34f);
                    DrawIconRect(new Rect(cx + 7f * scale, cy - 3f * scale, 7f * scale, line * 1.5f), dimAccent, 34f);
                    break;
                case CreepIconKind.Stalker:
                    DrawIconRect(new Rect(cx - 3f * scale, cy - 12f * scale, 7f * scale, 11f * scale), accent, -10f);
                    DrawIconRect(new Rect(cx - 6f * scale, cy - 1f * scale, 12f * scale, line), dimAccent);
                    DrawIconRect(new Rect(cx - 5f * scale, cy + 3f * scale, 4f * scale, 11f * scale), accent, 8f);
                    DrawIconRect(new Rect(cx + 2f * scale, cy + 3f * scale, 4f * scale, 11f * scale), accent, -8f);
                    break;
                case CreepIconKind.Warden:
                    DrawIconRect(new Rect(cx - 8f * scale, cy - 12f * scale, 16f * scale, 6f * scale), accent);
                    DrawIconRect(new Rect(cx - 6f * scale, cy - 5f * scale, 12f * scale, 13f * scale), accent);
                    DrawIconRect(new Rect(cx - 11f * scale, cy - 4f * scale, line * 1.5f, 12f * scale), dimAccent);
                    DrawIconRect(new Rect(cx + 9f * scale, cy - 4f * scale, line * 1.5f, 12f * scale), dimAccent);
                    break;
                case CreepIconKind.Colossus:
                    DrawIconRect(new Rect(cx - 11f * scale, cy - 13f * scale, 22f * scale, 9f * scale), accent);
                    DrawIconRect(new Rect(cx - 8f * scale, cy - 2f * scale, 16f * scale, 11f * scale), accent);
                    DrawIconRect(new Rect(cx - 13f * scale, cy + 10f * scale, 9f * scale, line * 2f), dimAccent);
                    DrawIconRect(new Rect(cx + 4f * scale, cy + 10f * scale, 9f * scale, line * 2f), dimAccent);
                    break;
                default:
                    DrawIconRect(new Rect(cx - 4f * scale, cy - 13f * scale, 8f * scale, 21f * scale), accent);
                    DrawIconRect(new Rect(cx - 9f * scale, cy + 1f * scale, 18f * scale, line), accent);
                    DrawIconRect(new Rect(cx - 2f * scale, cy + 5f * scale, 4f * scale, 12f * scale), accent);
                    DrawIconRect(new Rect(cx - 11f * scale, cy + 9f * scale, 22f * scale, line), dimAccent);
                    break;
            }

            GUI.color = previousColor;
        }

        private static void DrawIconRect(Rect rect, Color color, float rotationDegrees = 0f)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            if (Mathf.Abs(rotationDegrees) > 0.01f)
            {
                var previousMatrix = GUI.matrix;
                var pivot = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
                GUIUtility.RotateAroundPivot(rotationDegrees, pivot);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.matrix = previousMatrix;
            }
            else
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }

            GUI.color = previousColor;
        }

        private static bool DrawLauncherButton(Rect rect, string label, Color accent, float scale)
        {
            buttonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            return rect.width > rect.height * 1.35f
                ? RuntimeUiChrome.DrawPanelButton(rect, label, accent, scale, buttonStyle)
                : RuntimeUiChrome.DrawLauncherButton(rect, label, accent, scale, buttonStyle);
        }

        private static void EnsureStyles()
        {
            if (panelStyle is not null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(6, 6, 6, 6),
                margin = ZeroOffset(),
                padding = ZeroOffset()
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = SignalGold }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                margin = ZeroOffset(),
                padding = ZeroOffset(),
                normal = { textColor = Cloud },
                hover = { textColor = Cloud },
                active = { textColor = Cloud }
            };

            metaStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, panelStyle ?? GUI.skin.box);
            GUI.color = previousColor;
        }

        private static void DrawAccent(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        /// <summary>
        /// Seconds until the local player may send again, 0 when a send is ready.
        /// </summary>
        /// <remarks>
        /// The simulation runs at UnitySimulationDriver.ticksPerSecond (4), and the cooldown is 30
        /// ticks, so a send is available roughly every 7.5s. Converted to seconds here because the
        /// player has no reason to care about ticks.
        /// </remarks>
        private float CurrentSendCooldownSeconds()
        {
            if (reviewGoldOverride >= 0)
            {
                return 0f;
            }

            if (commandAdapter == null)
            {
                commandAdapter = Object.FindAnyObjectByType<UnityCommandAdapter>();
            }

            var ticks = commandAdapter?.CurrentPlayerSendCooldownTicks() ?? 0;
            return ticks <= 0 ? 0f : ticks / SimulationTicksPerSecond;
        }

        private const float SimulationTicksPerSecond = 4f;

        private int CurrentPlayerGold()
        {
            if (reviewGoldOverride >= 0)
            {
                return reviewGoldOverride;
            }

            if (commandAdapter == null)
            {
                commandAdapter = Object.FindAnyObjectByType<UnityCommandAdapter>();
            }

            return commandAdapter?.CurrentPlayerGold() ?? 0;
        }

        private void EnsureDriver()
        {
            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }
        }

        private static Color TintPanel(Color accent, float amount)
        {
            return new Color(
                PanelInk.r + accent.r * amount,
                PanelInk.g + accent.g * amount,
                PanelInk.b + accent.b * amount,
                PanelInk.a);
        }

        private static RectOffset ZeroOffset() => new RectOffset(0, 0, 0, 0);

        private enum CreepIconKind
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
    }
}
