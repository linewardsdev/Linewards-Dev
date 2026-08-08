#nullable enable

using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
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
        //   SUPPORT — force multipliers rather than bodies. Four buff the creeps around them or
        //           slow the towers shooting at them; the fifth walks over the maze entirely. The
        //           category was called RAPID for an exemption from a send cooldown that has been
        //           set to 0 for a long time, so the name described nothing a player could observe.
        //   ELITE — the Meshy-rigged bipeds: costlier, heavier, and back on the normal cooldown,
        //           because price is what paces them.
        private static readonly string[] CategoryLabels = { "CORE", "SUPPORT", "ELITE" };

        /// <summary>
        /// Whether a category's grid contains any card the send cooldown actually gates.
        /// </summary>
        /// <remarks>
        /// Replaces a hardcoded <c>selectedCategory != 1</c> test. Category 2 (SUPPORT) is entirely
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

        /// <summary>
        /// Whether a GUI-space point lands on this dock's launcher or its expanded panel.
        /// </summary>
        /// <remarks>
        /// Exists because TouchPlacementController's world-tap gate knew about every runtime panel
        /// EXCEPT this one, so taps on the send dock fell through to the board. Harmless-looking
        /// while a stray board tap only moved a build cursor; disruptive once multi-select made
        /// every stray tap toggle a tower in or out of a batch, which is how it was found.
        ///
        /// The dock answers for its own geometry rather than exporting rects for the other
        /// controller to re-derive. A second copy of these numbers is exactly how the two panels
        /// drifted apart before.
        /// </remarks>
        public bool ContainsPoint(Vector2 guiPoint)
        {
            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            return LauncherRect(scale, frame).Contains(guiPoint)
                || (isExpanded && PanelRect(scale, frame).Contains(guiPoint));
        }

        private static Rect LauncherRect(float scale, Rect frame)
        {
            var launcherWidth = 76f * scale;
            var launcherHeight = 44f * scale;
            return new Rect(frame.xMax - launcherWidth - 12f * scale, frame.yMax - launcherHeight - MobileViewportLayout.BottomMargin(scale), launcherWidth, launcherHeight);
        }

        private static Rect PanelRect(float scale, Rect frame)
        {
            var width = Mathf.Min(frame.width - 16f * scale, 430f * scale);
            var height = 282f * scale;
            var launcherClearance = 136f * scale;
            return new Rect(frame.xMax - width - 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale) - launcherClearance, width, height);
        }

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

        public void SendRunner() => Send(commandAdapter.SendSampleCreep(), "Runner queued", commandAdapter.SendCost(SampleVerticalSliceContent.CreepId), 0);

        public void SendBrute() => Send(commandAdapter.SendBruteCreep(), "Brute queued", commandAdapter.SendCost(SampleVerticalSliceContent.BruteCreepId), 1);

        public void SendSwarm() => Send(commandAdapter.SendSwarmCreep(), "Swarm queued", commandAdapter.SendCost(SampleVerticalSliceContent.SwarmCreepId), 2);

        public void SendShade() => Send(commandAdapter.SendShadeCreep(), "Shade queued", commandAdapter.SendCost(SampleVerticalSliceContent.ShadeCreepId), 3);

        public void SendSiege() => Send(commandAdapter.SendSiegeCreep(), "Siege queued", commandAdapter.SendCost(SampleVerticalSliceContent.SiegeCreepId), 4);

        public void SendWisp() => Send(commandAdapter.SendWispCreep(), "Wisp queued", commandAdapter.SendCost(SampleVerticalSliceContent.WispCreepId), 5);

        public void SendRevenant() => Send(commandAdapter.SendRevenantCreep(), "Revenant queued", commandAdapter.SendCost(SampleVerticalSliceContent.RevenantCreepId), 6);

        public void SendObsidianBrute() => Send(commandAdapter.SendObsidianBruteCreep(), "Obsidian Brute sent", commandAdapter.SendCost(SampleVerticalSliceContent.ObsidianBruteCreepId), 7);

        public void SendSerpent() => Send(commandAdapter.SendSerpentCreep(), "Serpent queued", commandAdapter.SendCost(SampleVerticalSliceContent.SerpentCreepId), 8);

        public void SendTurretWalker() => Send(commandAdapter.SendTurretWalkerCreep(), "Turret Walker sent", commandAdapter.SendCost(SampleVerticalSliceContent.TurretWalkerCreepId), 9);

        public void SendZephyr() => Send(commandAdapter.SendZephyrCreep(), "Zephyr Wraith sent", commandAdapter.SendCost(SampleVerticalSliceContent.ZephyrCreepId), 10);

        public void SendBurrower() => Send(commandAdapter.SendBurrowerCreep(), "Fracture Burrower sent", commandAdapter.SendCost(SampleVerticalSliceContent.BurrowerCreepId), 11);

        public void SendStalker() => Send(commandAdapter.SendStalkerCreep(), "Umbral Stalker sent", commandAdapter.SendCost(SampleVerticalSliceContent.StalkerCreepId), 12);

        public void SendWarden() => Send(commandAdapter.SendWardenCreep(), "Aegis Warden sent", commandAdapter.SendCost(SampleVerticalSliceContent.WardenCreepId), 13);

        public void SendColossus() => Send(commandAdapter.SendColossusCreep(), "Siege Colossus sent", commandAdapter.SendCost(SampleVerticalSliceContent.ColossusCreepId), 14);

        /// <summary>
        /// Keeps the dock shut for a seat that is out of the match.
        /// </summary>
        /// <remarks>
        /// EconomyService rejects every send from an eliminated player, so an open dock offers
        /// fifteen creeps that would each be refused — it was staying open and fully browsable over
        /// the elimination banner (OPEN_ITEMS.md item 31).
        ///
        /// This component enforces its own invariant rather than leaving it to
        /// TouchPlacementController, which also calls <see cref="CloseDock"/> when the seat is out.
        /// Two components, one rule, and no dependence on which of their Updates runs first — and
        /// the dock keeps holding it if that component is ever absent from a scene.
        ///
        /// In Update rather than OnGUI because OnGUI does not run in batchmode, so a rule enforced
        /// from a draw callback is one EliminatedSeatCheck cannot verify. It caught this.
        /// </remarks>
        private void Update()
        {
            if (IsLocalSeatEliminated)
            {
                CloseDock();
            }
        }

        /// <summary>Whether the seat this client drives is out of the match.</summary>
        private bool IsLocalSeatEliminated
        {
            get
            {
                EnsureDriver();
                return simulationDriver != null && simulationDriver.IsLocalSeatEliminated;
            }
        }

        private void OnGUI()
        {
            if (!showRuntimeDock)
            {
                return;
            }

            // A session-flow screen owns the display. Standing down is what makes it modal: a
            // scrim can dim this component but cannot stop it taking the click, because IMGUI
            // dispatches events in draw order and the HUD draws first.
            if (RuntimeUiChrome.ModalScreenActive)
            {
                return;
            }

            // The local seat is out. Update has already closed the dock; this stops the SEND launcher
            // being drawn, which is the control that would otherwise reopen it. Both halves are
            // needed: closing without this leaves SEND on screen, and this without closing leaves
            // `isExpanded` true, which keeps the tower palette suppressed by IsSendDockExpanded.
            if (IsLocalSeatEliminated)
            {
                return;
            }

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var launcherRect = LauncherRect(scale, frame);
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

            // One height for both states. The picker used to need a taller panel because it stacked
            // three full-width cards; laid out as a row sized to the card art's own aspect it fits
            // inside the same 282 the creep grids use, so the dock no longer resizes under the
            // player as they step between the picker and a category.
            var rect = PanelRect(scale, frame);

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

            // The shipped cooldown is 0 ticks (74b8519), so isSendCoolingDown is always false and
            // this whole block is currently inert — kept, not deleted, so the dock explains itself
            // again the moment a cooldown returns, the same reasoning as CurrentSendCooldownSeconds
            // above. When a cooldown is active, without a countdown the dock just looks broken while
            // it runs.
            // Hidden on a grid whose every card is cooldown-exempt (Category 2 / SUPPORT), where a
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
        /// Category chooser shown before any 5-creep grid: a row of cards carrying a name, a
        /// "5 sends" hint and the category's tier.
        /// </summary>
        /// <remarks>
        /// Geometry comes from <see cref="RuntimeUiChrome.CategoryCardRect"/>, which sizes each card
        /// to the card art's own aspect and centres the row. That is deliberate rather than
        /// incidental: this picker has twice put a card outside the dock and over the board by
        /// fixing a height, and then, once the height was derived, spent three full-width rows
        /// stretching portrait art across them. The shared helper is the only place either can
        /// happen now.
        /// </remarks>
        private void DrawCategoryPicker(Rect rect, float buttonY, float buttonHeight, float gap, float scale)
        {
            var accents = new[] { ArcaneBlue, WardViolet, SignalGold };
            var gold = CurrentPlayerGold();

            for (var category = 0; category < CategoryLabels.Length; category++)
            {
                var cardRect = RuntimeUiChrome.CategoryCardRect(rect, buttonY, gap, category, CategoryLabels.Length, scale);
                if (DrawCategoryCard(cardRect, CategoryLabels[category], accents[category], scale))
                {
                    selectedCategory = category;
                }

                DrawCategoryTier(cardRect, category, accents[category], gold, scale);
            }
        }

        /// <summary>
        /// Tier readout and upgrade button for one send category.
        /// </summary>
        /// <remarks>
        /// Drawn AFTER the card so the button sits above the card's own full-rect hit target;
        /// otherwise tapping upgrade would also select the category and drop the player into its
        /// creep grid.
        /// </remarks>
        private void DrawCategoryTier(Rect cardRect, int category, Color accent, int gold, float scale)
        {
            if (commandAdapter == null)
            {
                return;
            }

            var tier = commandAdapter.SendCategoryTier(category);
            var cost = commandAdapter.NextTierCost(LTW.Simulation.Commands.CategoryKind.SendCategory, category);
            var requiredIncome = commandAdapter.NextTierMinimumIncome(LTW.Simulation.Commands.CategoryKind.SendCategory, category);
            var pressed = RuntimeUiChrome.DrawCategoryTierRow(
                cardRect, tier, commandAdapter.MaxCategoryTier, cost, gold >= cost, accent, scale, metaStyle!, buttonStyle!,
                requiredIncome, commandAdapter.CurrentPlayerIncome());

            if (pressed)
            {
                var result = commandAdapter.BuySendCategoryTier(category);
                if (result.Accepted)
                {
                    feedbackView.ShowEconomy($"{CategoryLabels[category]} TIER {tier + 1}");
                }
                else
                {
                    feedbackView.ShowRejected(result.RejectionReason, cost, CurrentPlayerGold());
                }
            }
        }

        private static bool DrawCategoryCard(Rect rect, string label, Color accent, float scale)
        {
            // Hit region excludes the tier row, or the card's own button eats the upgrade button's
            // click before it is ever delivered.
            var pressed = RuntimeUiChrome.DrawCommandCard(
                rect, accent, CommandCardState.Normal, scale, RuntimeUiChrome.CategoryCardSelectRect(rect, scale));

            buttonStyle!.fontSize = Mathf.RoundToInt(13f * scale);
            buttonStyle.normal.textColor = Cloud;
            buttonStyle.hover.textColor = Cloud;
            buttonStyle.active.textColor = Cloud;
            // 0.20/0.44, not 0.32/0.58. Those were tuned for an 84-tall card; on the 104-tall card
            // the tier row needs, 0.58 put "5 SENDS" straight on top of "TIER n" — confirmed in a
            // real-UI capture, where the two labels rendered as one unreadable smear.
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.20f, rect.width, 22f * scale), label, buttonStyle);

            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = accent;
            metaStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.44f, rect.width, 18f * scale), "5 SENDS", metaStyle);
            return pressed;
        }

        /// <summary>One send card: everything the dock needs to draw it and act on it.</summary>
        /// <remarks>
        /// The three category grids used to be three near-identical blocks of hardcoded
        /// DrawSendButton calls, with the display order fixed by the order they happened to be
        /// written in. Ordering the cards by price meant editing three blocks by hand and redoing it
        /// after every rebalance. Describing a card as data instead lets one draw routine sort them,
        /// and the sort reads cost from the SIMULATION, so a price change reorders the dock on its
        /// own rather than silently leaving it wrong.
        /// </remarks>
        private readonly struct SendCard
        {
            public SendCard(string label, CreepIconKind icon, Color accent, ContentId creepId, int role, System.Action send, bool ignoresCooldown = false)
            {
                Label = label;
                Icon = icon;
                Accent = accent;
                CreepId = creepId;
                Role = role;
                Send = send;
                IgnoresCooldown = ignoresCooldown;
            }

            public string Label { get; }
            public CreepIconKind Icon { get; }
            public Color Accent { get; }
            public ContentId CreepId { get; }

            /// <summary>Selection identity, and what highlightedCreepRole is compared against. Travels with the card so sorting cannot renumber it.</summary>
            public int Role { get; }

            public System.Action Send { get; }
            public bool IgnoresCooldown { get; }
        }

        private SendCard[] CategoryOneCards() => new[]
        {
            new SendCard("RUNNER", CreepIconKind.Runner, ArcaneBlue, SampleVerticalSliceContent.CreepId, 0, SendRunner),
            new SendCard("BRUTE", CreepIconKind.Brute, WardViolet, SampleVerticalSliceContent.BruteCreepId, 1, SendBrute),
            new SendCard("SWARM", CreepIconKind.Swarm, SignalGold, SampleVerticalSliceContent.SwarmCreepId, 2, SendSwarm),
            new SendCard("SHADE", CreepIconKind.Shade, MintSignal, SampleVerticalSliceContent.ShadeCreepId, 3, SendShade),
            new SendCard("SIEGE", CreepIconKind.Siege, new Color(1f, 0.62f, 0.26f), SampleVerticalSliceContent.SiegeCreepId, 4, SendSiege)
        };

        // Every Category 2 card still sets ignoresCooldown. It is inert while the send cooldown is 0
        // and is no longer what names the category, but it is kept so the exemption is already right
        // if a cooldown ever returns.
        private SendCard[] CategoryTwoCards() => new[]
        {
            new SendCard("WISP", CreepIconKind.Wisp, ArcaneBlue, SampleVerticalSliceContent.WispCreepId, 5, SendWisp, ignoresCooldown: true),
            new SendCard("REVENANT", CreepIconKind.Revenant, WardViolet, SampleVerticalSliceContent.RevenantCreepId, 6, SendRevenant, ignoresCooldown: true),
            new SendCard("OBSIDIAN", CreepIconKind.ObsidianBrute, new Color(0.92f, 0.32f, 0.28f), SampleVerticalSliceContent.ObsidianBruteCreepId, 7, SendObsidianBrute, ignoresCooldown: true),
            new SendCard("SERPENT", CreepIconKind.Serpent, MintSignal, SampleVerticalSliceContent.SerpentCreepId, 8, SendSerpent, ignoresCooldown: true),
            new SendCard("WALKER", CreepIconKind.TurretWalker, new Color(0.42f, 0.82f, 0.86f), SampleVerticalSliceContent.TurretWalkerCreepId, 9, SendTurretWalker, ignoresCooldown: true)
        };

        private SendCard[] CategoryThreeCards() => new[]
        {
            new SendCard("WRAITH", CreepIconKind.Zephyr, ArcaneBlue, SampleVerticalSliceContent.ZephyrCreepId, 10, SendZephyr),
            new SendCard("BURROW", CreepIconKind.Burrower, new Color(0.85f, 0.55f, 0.25f), SampleVerticalSliceContent.BurrowerCreepId, 11, SendBurrower),
            new SendCard("STALKER", CreepIconKind.Stalker, WardViolet, SampleVerticalSliceContent.StalkerCreepId, 12, SendStalker),
            new SendCard("WARDEN", CreepIconKind.Warden, MintSignal, SampleVerticalSliceContent.WardenCreepId, 13, SendWarden),
            new SendCard("COLOSSUS", CreepIconKind.Colossus, new Color(1f, 0.45f, 0.30f), SampleVerticalSliceContent.ColossusCreepId, 14, SendColossus)
        };

        private void DrawCategoryOneCreeps(Rect rect, float buttonY, float buttonHeight, float gap, int gold, float scale) =>
            DrawSendCards(CategoryOneCards(), rect, buttonY, buttonHeight, gap, gold, scale);

        private void DrawCategoryTwoCreeps(Rect rect, float buttonY, float buttonHeight, float gap, int gold, float scale) =>
            DrawSendCards(CategoryTwoCards(), rect, buttonY, buttonHeight, gap, gold, scale);

        private void DrawCategoryThreeCreeps(Rect rect, float buttonY, float buttonHeight, float gap, int gold, float scale) =>
            DrawSendCards(CategoryThreeCards(), rect, buttonY, buttonHeight, gap, gold, scale);

        /// <summary>
        /// Draws one category's cards cheapest first, left to right, three across then two.
        /// </summary>
        /// <remarks>
        /// Ties are broken by role so the order is stable rather than dependent on sort internals —
        /// Thorn Snare and Spore Cloud are both 34 gold on the tower side, and the creep roster can
        /// tie the same way after a rebalance.
        /// </remarks>
        private void DrawSendCards(SendCard[] cards, Rect rect, float buttonY, float buttonHeight, float gap, int gold, float scale)
        {
            var costs = new int[cards.Length];
            var incomes = new int[cards.Length];
            for (var index = 0; index < cards.Length; index++)
            {
                // SendCost, not CreepCost: a press queues SendQuantity creeps and EconomyService
                // charges for all of them. Pricing the card at the unit cost is what let Swarm
                // display "6G", enable at 6 gold, and then be rejected for needing 18.
                costs[index] = commandAdapter != null ? commandAdapter.SendCost(cards[index].CreepId) : 0;

                // Read every frame rather than baked into the card, because it is not a constant:
                // past the income taper's knee the same button grants steadily less, down to zero at
                // the ceiling. The old hardcoded "+1"/"+2" strings would have gone quietly wrong
                // exactly when the player most needs to know what a send is still worth.
                incomes[index] = commandAdapter != null ? commandAdapter.SendIncomeGain(cards[index].CreepId) : 0;
            }

            var order = new int[cards.Length];
            for (var index = 0; index < order.Length; index++)
            {
                order[index] = index;
            }

            System.Array.Sort(order, (left, right) =>
            {
                var byCost = costs[left].CompareTo(costs[right]);
                return byCost != 0 ? byCost : cards[left].Role.CompareTo(cards[right].Role);
            });

            const int firstRowCount = 3;
            var firstRowWidth = (rect.width - 24f * scale - gap * (firstRowCount - 1)) / firstRowCount;
            var secondRowCount = Mathf.Max(1, cards.Length - firstRowCount);
            var secondRowWidth = (rect.width - 24f * scale - gap * (secondRowCount - 1)) / secondRowCount;
            var secondRowY = buttonY + buttonHeight + gap;

            var x = rect.x + 12f * scale;
            for (var slot = 0; slot < cards.Length; slot++)
            {
                var card = cards[order[slot]];
                var cost = costs[order[slot]];
                var income = incomes[order[slot]];
                var inFirstRow = slot < firstRowCount;
                if (slot == firstRowCount)
                {
                    x = rect.x + 12f * scale;
                }

                var width = inFirstRow ? firstRowWidth : secondRowWidth;
                var y = inFirstRow ? buttonY : secondRowY;

                // Enabled on queue space, NOT on gold, and that is the point of the send queue. The
                // card used to grey out the moment a player could not afford it, which is exactly
                // the tap the queue exists to accept: state the intent now, pay when income lands.
                // Leaving the affordability gate here would have kept the old behaviour behind a
                // queue nobody could reach.
                var queued = commandAdapter != null ? commandAdapter.QueuedSendCount(card.CreepId) : 0;
                var hasQueueSpace = queued < LTW.Simulation.Bridge.LocalVerticalSlice.MaxQueuedSendsPerCreep;

                // The count is on the card because a tap no longer produces a creep immediately.
                // Without it a queued tap and a tap that did nothing look identical, which is the
                // one thing that would make queueing feel broken rather than helpful.
                var meta = queued > 0 ? $"{cost}G  +{income}   x{queued}" : $"{cost}G  +{income}";
                if (DrawSendButton(new Rect(x, y, width, buttonHeight), card.Label, meta, card.Icon, card.Accent, hasQueueSpace, highlightedCreepRole == card.Role, scale, card.IgnoresCooldown))
                {
                    card.Send();
                }

                x += width + gap;
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

        /// <summary>
        /// Panel background, delegated to the shared chrome.
        /// </summary>
        /// <remarks>
        /// This used to build its own style from `GUI.skin.box`, which meant the background was
        /// Unity's built-in editor-skin box with a navy tint over it. Three files had an identical
        /// copy of that, and a fourth drew a flat rect with hairline edges instead — so the HUD's
        /// panels disagreed with each other and none of them matched the chamfered buttons.
        /// </remarks>
        private static void DrawPanel(Rect rect, Color color) =>
            RuntimeUiChrome.DrawPanel(rect, color, MobileViewportLayout.UiScale());

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
        /// The shipped cooldown is 0 ticks (74b8519 removed it), so this currently always returns 0.
        /// Reads CurrentPlayerSendCooldownTicks() live rather than assuming that, so if the cooldown
        /// is ever restored this converts whatever tick count comes back at
        /// UnitySimulationDriver.TicksPerSecond into seconds without needing a code change — the
        /// player has no reason to care about ticks either way. Reads TicksPerSecond from the driver
        /// instance rather than a local duplicate constant (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items) so a future change
        /// to the sim rate can't silently desync this conversion from the real one.
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

            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }

            var ticks = commandAdapter?.CurrentPlayerSendCooldownTicks() ?? 0;
            var ticksPerSecond = simulationDriver != null ? simulationDriver.TicksPerSecond : 4f;
            return ticks <= 0 ? 0f : ticks / ticksPerSecond;
        }

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
