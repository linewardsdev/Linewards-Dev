using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
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

        [SerializeField]
        private bool showRuntimeHud = true;

        private long incomeTicksRemaining = IncomeIntervalTicks;

        public string GoldText { get; private set; } = "0";

        public string IncomeText { get; private set; } = "0";

        public string LivesText { get; private set; } = "0";

        public string PressureText { get; private set; } = "0";

        public string IncomeTimerText { get; private set; } = "50";

        public string LaneText { get; private set; } = "Your Line";

        public bool IncomeTickSoon { get; private set; }

        public void Render(VerticalSliceSnapshot snapshot)
        {
            var player = snapshot.Players.Get(new PlayerId(1));
            GoldText = player.Gold.Amount.ToString();
            IncomeText = player.Income.Amount.ToString();
            LivesText = player.Lives.Amount.ToString();
            PressureText = snapshot.Creeps.Count.ToString();
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

            var scale = Mathf.Clamp(Screen.width / 1080f, 0.72f, 1.15f);
            var margin = 12f * scale;
            var height = 72f * scale;
            var strip = new Rect(margin, margin, Screen.width - margin * 2f, height);
            DrawPanel(strip, NightInk);

            var x = strip.x + 10f * scale;
            var y = strip.y + 8f * scale;
            var pillHeight = strip.height - 16f * scale;
            var gap = 8f * scale;

            x = DrawLanePill(x, y, 128f * scale, pillHeight, scale);
            x += gap;
            x = DrawStatPill(x, y, 88f * scale, pillHeight, "LIVES", LivesText, Danger, scale);
            x += gap;
            x = DrawStatPill(x, y, 94f * scale, pillHeight, "GOLD", GoldText, SignalGold, scale);
            x += gap;
            x = DrawStatPill(x, y, 108f * scale, pillHeight, "INCOME", $"+{IncomeText}", MintSignal, scale);
            x += gap;
            x = DrawTimerPill(x, y, 120f * scale, pillHeight, scale);
            x += gap;
            DrawStatPill(x, y, 114f * scale, pillHeight, "PRESSURE", PressureText, PressureText == "0" ? ArcaneBlue : Danger, scale);
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
        }

        private float DrawLanePill(float x, float y, float width, float height, float scale)
        {
            var rect = new Rect(x, y, width, height);
            DrawPanel(rect, TintPanel(ArcaneBlue, 0.05f));
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), ArcaneBlue);

            laneStyle!.fontSize = Mathf.RoundToInt(17f * scale);
            GUI.Label(rect, LaneText.ToUpperInvariant(), laneStyle);
            return rect.xMax;
        }

        private static float DrawStatPill(float x, float y, float width, float height, string label, string value, Color accent, float scale)
        {
            var rect = new Rect(x, y, width, height);
            DrawPanel(rect, TintPanel(accent, 0.045f));
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            labelStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            valueStyle!.fontSize = Mathf.RoundToInt(18f * scale);
            valueStyle.normal.textColor = accent;

            GUI.Label(new Rect(rect.x, rect.y + 6f * scale, rect.width, 18f * scale), label, labelStyle);
            GUI.Label(new Rect(rect.x, rect.y + 22f * scale, rect.width, rect.height - 22f * scale), value, valueStyle);
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
