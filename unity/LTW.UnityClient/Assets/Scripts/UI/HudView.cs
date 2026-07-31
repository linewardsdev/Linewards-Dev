#nullable enable

using LTW.Simulation.Bridge;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class HudView : MonoBehaviour
    {
        // Startup default only, before a UnitySimulationDriver is attached — every runtime read
        // goes through simulationDriver.IncomeIntervalTicks instead (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items; this
        // used to be the only copy and could silently disagree with the sim's real value).
        private const long IncomeIntervalTicks = 50;
        private static readonly Color NightInk = new(0.063f, 0.094f, 0.184f, 0.9f);
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.94f);
        private static readonly Color DeepInk = new(0.016f, 0.022f, 0.036f, 0.96f);
        private static readonly Color SlateEdge = new(0.32f, 0.34f, 0.38f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);

        private static GUIStyle? pillStyle;
        private static GUIStyle? labelStyle;
        private static GUIStyle? valueStyle;
        private static GUIStyle? buttonStyle;
        private static GUIStyle? metaStyle;

        [SerializeField]
        private bool showRuntimeHud = true;

        [SerializeField]
        private bool statsExpanded;

        private long incomeTicksRemaining = IncomeIntervalTicks;
        private UnitySimulationDriver simulationDriver = null!;
        private long lastObservedTick = -1;
        private int kills;
        private int leaks;

        public string GoldText { get; private set; } = "0";

        public string IncomeText { get; private set; } = "0";

        public string LivesText { get; private set; } = "0";

        public string PressureText { get; private set; } = "0";

        public string IncomeTimerText { get; private set; } = "50";

        public string MatchTimeText { get; private set; } = "0";

        public string KillsText { get; private set; } = "0";

        public string LeaksText { get; private set; } = "0";

        public bool IncomeTickSoon { get; private set; }

        public void Initialize(UnitySimulationDriver driver)
        {
            simulationDriver = driver;
        }

        private void Update()
        {
            if (simulationDriver == null || simulationDriver.LatestSnapshot == null)
            {
                return;
            }

            Render(simulationDriver.LatestSnapshot, simulationDriver.LatestEvents);
        }

        public void Render(VerticalSliceSnapshot snapshot, System.Collections.Generic.IReadOnlyList<ISimulationEvent> events)
        {
            if (snapshot.Tick.Value < lastObservedTick)
            {
                kills = 0;
                leaks = 0;
            }

            foreach (var simulationEvent in events)
            {
                if (simulationEvent.Tick.Value <= lastObservedTick)
                {
                    continue;
                }

                if (simulationEvent is CreepKilledEvent killed && killed.DefenderId.Equals(simulationDriver.LocalPlayerId))
                {
                    kills++;
                }
                else if (simulationEvent is LeakEvent leak && leak.DefenderId.Equals(simulationDriver.LocalPlayerId))
                {
                    leaks += leak.LivesLost.Amount;
                }
            }

            lastObservedTick = snapshot.Tick.Value;
            var player = snapshot.Players.Get(simulationDriver.LocalPlayerId);
            GoldText = player.Gold.Amount.ToString();
            IncomeText = player.Income.Amount.ToString();
            LivesText = player.Lives.Amount.ToString();
            PressureText = snapshot.Creeps.Count.ToString();
            MatchTimeText = snapshot.Tick.Value.ToString();
            KillsText = kills.ToString();
            LeaksText = leaks.ToString();
            var incomeIntervalTicks = simulationDriver.IncomeIntervalTicks;
            incomeTicksRemaining = incomeIntervalTicks - snapshot.Tick.Value % incomeIntervalTicks;
            IncomeTimerText = incomeTicksRemaining.ToString();
            IncomeTickSoon = incomeTicksRemaining <= 5;
        }

        private void OnGUI()
        {
            if (!showRuntimeHud)
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

            EnsureStyles();
            if (simulationDriver != null && simulationDriver.LatestSnapshot != null)
            {
                Render(simulationDriver.LatestSnapshot, simulationDriver.LatestEvents);
            }

            var scale = MobileViewportLayout.UiScale();
            var gap = 4f * scale;
            var headerHeight = 52f * scale;
            var drawerHeight = 78f * scale;
            var height = statsExpanded ? headerHeight + gap + drawerHeight : headerHeight;
            var strip = CompactTopHudRect(scale, height);
            DrawHudFrame(strip, StateAccent(), scale);
            DrawHudHeader(strip, headerHeight, scale);

            if (!statsExpanded)
            {
                return;
            }

            var drawer = new Rect(strip.x + gap, strip.y + headerHeight + gap, strip.width - gap * 2f, drawerHeight - gap);
            DrawPanel(drawer, TintPanel(ArcaneBlue, 0.035f));
            DrawAccent(new Rect(drawer.x + 8f * scale, drawer.y, drawer.width - 16f * scale, 2f * scale), SlateEdge);
            var cellWidth = (drawer.width - gap * 3f) / 4f;
            var rowHeight = 48f * scale;
            var x = strip.x + gap;
            var topY = drawer.y;

            x = DrawStatPill(x, topY, cellWidth, rowHeight, "LIVES", LivesText, Danger, scale);
            x += gap;
            x = DrawStatPill(x, topY, cellWidth, rowHeight, "GOLD", GoldText, SignalGold, scale);
            x += gap;
            x = DrawStatPill(x, topY, cellWidth, rowHeight, "INCOME", $"+{IncomeText}", MintSignal, scale);
            x += gap;
            DrawTimerPill(x, topY, cellWidth, rowHeight, scale);
            DrawSecondaryStats(new Rect(drawer.x + 6f * scale, topY + rowHeight + 4f * scale, drawer.width - 12f * scale, 20f * scale), scale);
        }

        private static void EnsureStyles()
        {
            if (pillStyle is not null)
            {
                return;
            }

            pillStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(6, 6, 6, 6),
                margin = ZeroOffset(),
                padding = ZeroOffset()
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(Cloud.r, Cloud.g, Cloud.b, 0.68f) }
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                margin = ZeroOffset(),
                padding = ZeroOffset(),
                normal = { textColor = Cloud }
            };

            metaStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(Cloud.r, Cloud.g, Cloud.b, 0.74f) }
            };
        }

        private void DrawHudHeader(Rect strip, float headerHeight, float scale)
        {
            var header = new Rect(strip.x + 4f * scale, strip.y + 4f * scale, strip.width - 8f * scale, headerHeight - 8f * scale);
            DrawPanel(header, new Color(DeepInk.r, DeepInk.g, DeepInk.b, 0.62f));

            var gap = 5f * scale;
            var leftWidth = Mathf.Min(76f * scale, header.width * 0.24f);
            var stateWidth = Mathf.Min(60f * scale, header.width * 0.18f);
            var centerWidth = Mathf.Max(92f * scale, header.width - leftWidth - stateWidth - gap * 2f - 6f * scale);

            var left = new Rect(header.x + 6f * scale, header.y + 4f * scale, leftWidth, header.height - 8f * scale);
            var center = new Rect(left.xMax + gap, left.y, centerWidth, left.height);
            var state = new Rect(center.xMax + gap, left.y, stateWidth, left.height);

            DrawHudCell(left, ArcaneBlue, statsExpanded, scale);
            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            if (RuntimeUiChrome.DrawPanelButton(left, statsExpanded ? "HIDE" : "LINE", ArcaneBlue, scale, buttonStyle))
            {
                statsExpanded = !statsExpanded;
            }

            DrawHudCell(center, SignalGold, true, scale);
            valueStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            valueStyle.normal.textColor = Cloud;
            var summary = $"L{LivesText}  G{GoldText}  +{IncomeText}  P{PressureText}";
            GUI.Label(new Rect(center.x + 8f * scale, center.y, center.width - 16f * scale, center.height), summary, valueStyle);

            DrawHudCell(state, StateAccent(), true, scale);
            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = StateAccent();
            var stateText = simulationDriver != null && simulationDriver.IsOpeningBuildCountdown
                ? "BUILD"
                : simulationDriver != null && simulationDriver.HasStarted ? "LIVE" : "READY";
            GUI.Label(state, stateText, metaStyle);
        }

        private static Rect CompactTopHudRect(float scale, float height)
        {
            var full = MobileViewportLayout.TopHudRect(scale, height);
            var width = Mathf.Min(full.width, 358f * scale);
            return new Rect(full.x + (full.width - width) * 0.5f, full.y, width, height);
        }

        private static float DrawStatPill(float x, float y, float width, float height, string label, string value, Color accent, float scale)
        {
            var rect = new Rect(x, y, width, height);
            DrawHudCell(rect, accent, false, scale);

            labelStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            valueStyle!.fontSize = Mathf.RoundToInt(16f * scale);
            valueStyle.normal.textColor = accent;

            GUI.Label(new Rect(rect.x, rect.y + 4f * scale, rect.width, 14f * scale), label, labelStyle);
            GUI.Label(new Rect(rect.x, rect.y + 16f * scale, rect.width, rect.height - 16f * scale), value, valueStyle);
            return rect.xMax;
        }

        private void DrawSecondaryStats(Rect rect, float scale)
        {
            metaStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            metaStyle.normal.textColor = new Color(Cloud.r, Cloud.g, Cloud.b, 0.76f);
            GUI.Label(rect, $"KILLS {KillsText}   LEAKS {LeaksText}   CREEPS {PressureText}   TIME {MatchTimeText}", metaStyle);
        }

        private float DrawTimerPill(float x, float y, float width, float height, float scale)
        {
            var accent = IncomeTickSoon ? MintSignal : SignalGold;
            var rect = new Rect(x, y, width, height);
            DrawHudCell(rect, accent, IncomeTickSoon, scale);

            labelStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            valueStyle!.fontSize = Mathf.RoundToInt(18f * scale);
            valueStyle.normal.textColor = accent;

            GUI.Label(new Rect(rect.x, rect.y + 6f * scale, rect.width, 18f * scale), "TICK", labelStyle);
            GUI.Label(new Rect(rect.x, rect.y + 21f * scale, rect.width, 24f * scale), IncomeTickSoon ? $"{IncomeTimerText}!" : IncomeTimerText, valueStyle);

            var barBack = new Rect(rect.x + 10f * scale, rect.yMax - 11f * scale, rect.width - 20f * scale, 4f * scale);
            DrawAccent(barBack, new Color(Cloud.r, Cloud.g, Cloud.b, 0.16f));

            // DrawTimerPill can run before Initialize (OnGUI draws the HUD frame regardless of
            // whether a driver is attached yet), so this cannot assume simulationDriver is non-null
            // the way Render already safely does.
            var incomeIntervalTicks = simulationDriver != null ? simulationDriver.IncomeIntervalTicks : IncomeIntervalTicks;
            var fill = 1f - Mathf.Clamp01((float)incomeTicksRemaining / incomeIntervalTicks);
            DrawAccent(new Rect(barBack.x, barBack.y, barBack.width * fill, barBack.height), accent);
            return rect.xMax;
        }

        private Color StateAccent()
        {
            if (simulationDriver != null && simulationDriver.IsOpeningBuildCountdown)
            {
                return SignalGold;
            }

            return simulationDriver != null && simulationDriver.HasStarted ? MintSignal : ArcaneBlue;
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, pillStyle ?? GUI.skin.box);
            GUI.color = previousColor;
        }

        private static void DrawHudFrame(Rect rect, Color accent, float scale)
        {
            if (RuntimeUiArtLibrary.DrawChromeTexture(rect, "ui_hud_chrome_option_06", new Color(1f, 1f, 1f, 0.92f)))
            {
                Fill(new Rect(rect.x + 10f * scale, rect.yMax - 5f * scale, rect.width - 20f * scale, 4f * scale), accent);
                Fill(new Rect(rect.x + 6f * scale, rect.y + 10f * scale, 4f * scale, rect.height - 20f * scale), new Color(accent.r, accent.g, accent.b, 0.42f));
                Fill(new Rect(rect.xMax - 10f * scale, rect.y + 10f * scale, 4f * scale, rect.height - 20f * scale), new Color(SignalGold.r, SignalGold.g, SignalGold.b, 0.42f));
                return;
            }

            Fill(rect, DeepInk);
            Fill(new Rect(rect.x + 4f * scale, rect.y + 4f * scale, rect.width - 8f * scale, rect.height - 8f * scale), NightInk);
            Fill(new Rect(rect.x + 10f * scale, rect.y + 3f * scale, rect.width - 20f * scale, 2f * scale), SlateEdge);
            Fill(new Rect(rect.x + 10f * scale, rect.yMax - 5f * scale, rect.width - 20f * scale, 4f * scale), accent);
            Fill(new Rect(rect.x + 6f * scale, rect.y + 10f * scale, 4f * scale, rect.height - 20f * scale), new Color(accent.r, accent.g, accent.b, 0.55f));
            Fill(new Rect(rect.xMax - 10f * scale, rect.y + 10f * scale, 4f * scale, rect.height - 20f * scale), new Color(SignalGold.r, SignalGold.g, SignalGold.b, 0.52f));
        }

        private static void DrawHudCell(Rect rect, Color accent, bool active, float scale)
        {
            var edge = active ? accent : SlateEdge;
            Fill(rect, new Color(0.006f, 0.009f, 0.014f, 0.78f));
            Fill(new Rect(rect.x + 2f * scale, rect.y + 2f * scale, rect.width - 4f * scale, rect.height - 4f * scale), edge);
            Fill(new Rect(rect.x + 4f * scale, rect.y + 4f * scale, rect.width - 8f * scale, rect.height - 8f * scale), TintPanel(accent, active ? 0.09f : 0.045f));
            Fill(new Rect(rect.x + 8f * scale, rect.yMax - 7f * scale, rect.width - 16f * scale, 3f * scale), accent);
            Fill(new Rect(rect.x + 7f * scale, rect.y + 7f * scale, 10f * scale, 2f * scale), accent);
            Fill(new Rect(rect.xMax - 17f * scale, rect.y + 7f * scale, 10f * scale, 2f * scale), accent);
        }

        private static void DrawAccent(Rect rect, Color color)
        {
            Fill(rect, color);
        }

        private static void Fill(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
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
    }
}
