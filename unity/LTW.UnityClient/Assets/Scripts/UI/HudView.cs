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
        private const long IncomeIntervalTicks = 50;
        private static readonly Color NightInk = new(0.063f, 0.094f, 0.184f, 0.9f);
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.94f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);

        private static GUIStyle? pillStyle;
        private static GUIStyle? labelStyle;
        private static GUIStyle? valueStyle;
        private static GUIStyle? laneStyle;
        private static GUIStyle? buttonStyle;

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

        public string LaneText { get; private set; } = "Your Line";

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

                if (simulationEvent is CreepKilledEvent killed && killed.DefenderId.Value == 1)
                {
                    kills++;
                }
                else if (simulationEvent is LeakEvent leak && leak.DefenderId.Value == 1)
                {
                    leaks += leak.LivesLost.Amount;
                }
            }

            lastObservedTick = snapshot.Tick.Value;
            var player = snapshot.Players.Get(new PlayerId(1));
            GoldText = player.Gold.Amount.ToString();
            IncomeText = player.Income.Amount.ToString();
            LivesText = player.Lives.Amount.ToString();
            PressureText = snapshot.Creeps.Count.ToString();
            MatchTimeText = snapshot.Tick.Value.ToString();
            KillsText = kills.ToString();
            LeaksText = leaks.ToString();
            incomeTicksRemaining = IncomeIntervalTicks - snapshot.Tick.Value % IncomeIntervalTicks;
            IncomeTimerText = incomeTicksRemaining.ToString();
            IncomeTickSoon = incomeTicksRemaining <= 5;
            LaneText = "Your Line";
        }

        private void OnGUI()
        {
            if (!showRuntimeHud)
            {
                return;
            }

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var margin = 8f * scale;
            var gap = 4f * scale;
            var headerHeight = 42f * scale;
            var drawerHeight = 96f * scale;
            var height = statsExpanded ? headerHeight + gap + drawerHeight : headerHeight;
            var strip = new Rect(frame.x + margin, frame.y + margin, frame.width - margin * 2f, height);
            DrawPanel(strip, NightInk);
            DrawHudHeader(strip, headerHeight, scale);

            if (!statsExpanded)
            {
                return;
            }

            var drawer = new Rect(strip.x + gap, strip.y + headerHeight + gap, strip.width - gap * 2f, drawerHeight - gap);
            var cellWidth = (drawer.width - gap * 3f) / 4f;
            var rowHeight = (drawer.height - gap) * 0.5f;
            var x = strip.x + gap;
            var topY = drawer.y;
            var bottomY = topY + rowHeight + gap;

            x = DrawStatPill(x, topY, cellWidth, rowHeight, "LIVES", LivesText, Danger, scale);
            x += gap;
            x = DrawStatPill(x, topY, cellWidth, rowHeight, "GOLD", GoldText, SignalGold, scale);
            x += gap;
            x = DrawStatPill(x, topY, cellWidth, rowHeight, "INCOME", $"+{IncomeText}", MintSignal, scale);
            x += gap;
            DrawTimerPill(x, topY, cellWidth, rowHeight, scale);

            x = drawer.x;
            x = DrawStatPill(x, bottomY, cellWidth, rowHeight, "TIME", MatchTimeText, Cloud, scale);
            x += gap;
            x = DrawStatPill(x, bottomY, cellWidth, rowHeight, "KILLS", KillsText, MintSignal, scale);
            x += gap;
            x = DrawStatPill(x, bottomY, cellWidth, rowHeight, "LEAKS", LeaksText, LeaksText == "0" ? ArcaneBlue : Danger, scale);
            x += gap;
            DrawStatPill(x, bottomY, cellWidth, rowHeight, "PRESS", PressureText, PressureText == "0" ? ArcaneBlue : Danger, scale);
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

            laneStyle = new GUIStyle(valueStyle)
            {
                alignment = TextAnchor.MiddleCenter,
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
        }

        private void DrawHudHeader(Rect strip, float headerHeight, float scale)
        {
            var header = new Rect(strip.x + 4f * scale, strip.y + 4f * scale, strip.width - 8f * scale, headerHeight - 8f * scale);
            DrawPanel(header, TintPanel(ArcaneBlue, 0.05f));
            DrawAccent(new Rect(header.x, header.yMax - 3f * scale, header.width, 3f * scale), ArcaneBlue);

            laneStyle!.fontSize = Mathf.RoundToInt(14f * scale);
            laneStyle.normal.textColor = Cloud;
            GUI.Label(new Rect(header.x + 10f * scale, header.y, 110f * scale, header.height), LaneText.ToUpperInvariant(), laneStyle);

            valueStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            valueStyle.normal.textColor = Cloud;
            var summary = $"{LivesText}L   {GoldText}G   +{IncomeText}";
            GUI.Label(new Rect(header.x + 126f * scale, header.y, header.width - 224f * scale, header.height), summary, valueStyle);

            buttonStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            var label = statsExpanded ? "HIDE" : "STATS";
            if (GUI.Button(new Rect(header.xMax - 84f * scale, header.y + 3f * scale, 74f * scale, header.height - 6f * scale), label, buttonStyle))
            {
                statsExpanded = !statsExpanded;
            }
        }

        private float DrawLanePill(float x, float y, float width, float height, float scale)
        {
            var rect = new Rect(x, y, width, height);
            DrawPanel(rect, TintPanel(ArcaneBlue, 0.05f));
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), ArcaneBlue);

            laneStyle!.fontSize = Mathf.RoundToInt(14f * scale);
            GUI.Label(rect, LaneText.ToUpperInvariant(), laneStyle);
            return rect.xMax;
        }

        private static float DrawStatPill(float x, float y, float width, float height, string label, string value, Color accent, float scale)
        {
            var rect = new Rect(x, y, width, height);
            DrawPanel(rect, TintPanel(accent, 0.045f));
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            labelStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            valueStyle!.fontSize = Mathf.RoundToInt(16f * scale);
            valueStyle.normal.textColor = accent;

            GUI.Label(new Rect(rect.x, rect.y + 4f * scale, rect.width, 14f * scale), label, labelStyle);
            GUI.Label(new Rect(rect.x, rect.y + 16f * scale, rect.width, rect.height - 16f * scale), value, valueStyle);
            return rect.xMax;
        }

        private float DrawTimerPill(float x, float y, float width, float height, float scale)
        {
            var accent = IncomeTickSoon ? MintSignal : SignalGold;
            var rect = new Rect(x, y, width, height);
            DrawPanel(rect, TintPanel(accent, IncomeTickSoon ? 0.075f : 0.045f));
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            labelStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            valueStyle!.fontSize = Mathf.RoundToInt(18f * scale);
            valueStyle.normal.textColor = accent;

            GUI.Label(new Rect(rect.x, rect.y + 6f * scale, rect.width, 18f * scale), "TICK", labelStyle);
            GUI.Label(new Rect(rect.x, rect.y + 21f * scale, rect.width, 24f * scale), IncomeTickSoon ? $"{IncomeTimerText}!" : IncomeTimerText, valueStyle);

            var barBack = new Rect(rect.x + 10f * scale, rect.yMax - 11f * scale, rect.width - 20f * scale, 4f * scale);
            DrawAccent(barBack, new Color(Cloud.r, Cloud.g, Cloud.b, 0.16f));

            var fill = 1f - Mathf.Clamp01((float)incomeTicksRemaining / IncomeIntervalTicks);
            DrawAccent(new Rect(barBack.x, barBack.y, barBack.width * fill, barBack.height), accent);
            return rect.xMax;
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, pillStyle ?? GUI.skin.box);
            GUI.color = previousColor;
        }

        private static void DrawAccent(Rect rect, Color color)
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
