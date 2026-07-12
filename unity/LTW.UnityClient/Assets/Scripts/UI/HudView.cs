using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using UnityEngine;
using UnityEngine.UI;

namespace LTW.UnityClient.UI
{
    public sealed class HudView : MonoBehaviour
    {
        private const long IncomeIntervalTicks = 50;
        private static readonly Color NightInk = new(0.063f, 0.094f, 0.184f, 0.88f);
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.94f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);

        [SerializeField]
        private bool buildRuntimeHud = true;

        [SerializeField]
        private Canvas? targetCanvas;

        [SerializeField]
        private RectTransform? statusStrip;

        [SerializeField]
        private Text? laneText;

        [SerializeField]
        private Text? livesText;

        [SerializeField]
        private Text? goldText;

        [SerializeField]
        private Text? incomeText;

        [SerializeField]
        private Text? incomeTimerText;

        [SerializeField]
        private Image? incomeTimerFill;

        [SerializeField]
        private Text? pressureText;

        [SerializeField]
        private Image? pressureBadge;

        public string GoldText { get; private set; } = "0";

        public string IncomeText { get; private set; } = "0";

        public string LivesText { get; private set; } = "0";

        public string PressureText { get; private set; } = "0";

        public string IncomeTimerText { get; private set; } = "50";

        public string LaneText { get; private set; } = "Your Line";

        public bool IncomeTickSoon { get; private set; }

        private void Awake()
        {
            EnsureRuntimeHud();
            RefreshVisuals();
        }

        public void Render(VerticalSliceSnapshot snapshot)
        {
            var player = snapshot.Players.Get(new PlayerId(1));
            GoldText = player.Gold.Amount.ToString();
            IncomeText = player.Income.Amount.ToString();
            LivesText = player.Lives.Amount.ToString();
            PressureText = snapshot.Creeps.Count.ToString();
            var ticksUntilIncome = IncomeIntervalTicks - snapshot.Tick.Value % IncomeIntervalTicks;
            IncomeTimerText = ticksUntilIncome.ToString();
            IncomeTickSoon = ticksUntilIncome <= 5;
            LaneText = "Your Line";

            EnsureRuntimeHud();
            RefreshVisuals();
        }

        private void EnsureRuntimeHud()
        {
            if (!buildRuntimeHud || statusStrip is not null)
            {
                return;
            }

            var canvas = ResolveCanvas();
            statusStrip = CreateRect("Match Status Strip", canvas.transform);
            statusStrip.anchorMin = new Vector2(0f, 1f);
            statusStrip.anchorMax = new Vector2(1f, 1f);
            statusStrip.pivot = new Vector2(0.5f, 1f);
            statusStrip.offsetMin = new Vector2(12f, -84f);
            statusStrip.offsetMax = new Vector2(-12f, -12f);

            var stripBackground = statusStrip.gameObject.AddComponent<Image>();
            stripBackground.color = NightInk;

            var layout = statusStrip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            laneText = CreateTextPill(statusStrip, "Your Line", ArcaneBlue, 124f, 18, FontStyle.Bold);
            livesText = CreateStatPill(statusStrip, "LIVES", "0", Danger, 88f);
            goldText = CreateStatPill(statusStrip, "GOLD", "0", SignalGold, 92f);
            incomeText = CreateStatPill(statusStrip, "INCOME", "0", MintSignal, 106f);
            CreateIncomeTimerPill(statusStrip);
            CreatePressurePill(statusStrip);
        }

        private Canvas ResolveCanvas()
        {
            if (targetCanvas is not null)
            {
                return targetCanvas;
            }

            targetCanvas = GetComponentInParent<Canvas>();
            if (targetCanvas is not null)
            {
                return targetCanvas;
            }

            var canvasObject = new GameObject("Match HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            targetCanvas = canvasObject.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            return targetCanvas;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var instance = new GameObject(name, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<RectTransform>();
        }

        private static Text CreateTextPill(RectTransform parent, string text, Color accent, float preferredWidth, int fontSize, FontStyle style)
        {
            var pill = CreatePill(parent, preferredWidth, accent);
            var label = CreateText("Value", pill, fontSize, style, Cloud, TextAnchor.MiddleCenter);
            label.text = text;
            return label;
        }

        private static Text CreateStatPill(RectTransform parent, string label, string value, Color accent, float preferredWidth)
        {
            var pill = CreatePill(parent, preferredWidth, accent);
            var stack = pill.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(8, 8, 5, 5);
            stack.spacing = 1f;
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            var labelText = CreateText("Label", pill, 10, FontStyle.Bold, new Color(Cloud.r, Cloud.g, Cloud.b, 0.68f), TextAnchor.MiddleCenter);
            labelText.text = label;

            var valueText = CreateText("Value", pill, 18, FontStyle.Bold, accent, TextAnchor.MiddleCenter);
            valueText.text = value;
            return valueText;
        }

        private void CreateIncomeTimerPill(RectTransform parent)
        {
            var pill = CreatePill(parent, 118f, SignalGold);
            var stack = pill.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(8, 8, 5, 5);
            stack.spacing = 3f;
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            var labelText = CreateText("Label", pill, 10, FontStyle.Bold, new Color(Cloud.r, Cloud.g, Cloud.b, 0.68f), TextAnchor.MiddleCenter);
            labelText.text = "TICK";

            incomeTimerText = CreateText("Value", pill, 18, FontStyle.Bold, SignalGold, TextAnchor.MiddleCenter);
            incomeTimerText.text = "50";

            var barRoot = CreateRect("Timer Bar", pill);
            barRoot.sizeDelta = new Vector2(0f, 4f);
            var barLayout = barRoot.gameObject.AddComponent<LayoutElement>();
            barLayout.preferredHeight = 4f;
            barLayout.minHeight = 4f;

            var barBack = barRoot.gameObject.AddComponent<Image>();
            barBack.color = new Color(Cloud.r, Cloud.g, Cloud.b, 0.16f);

            var fillRoot = CreateRect("Fill", barRoot);
            fillRoot.anchorMin = new Vector2(0f, 0f);
            fillRoot.anchorMax = new Vector2(1f, 1f);
            fillRoot.offsetMin = Vector2.zero;
            fillRoot.offsetMax = Vector2.zero;
            incomeTimerFill = fillRoot.gameObject.AddComponent<Image>();
            incomeTimerFill.type = Image.Type.Filled;
            incomeTimerFill.fillMethod = Image.FillMethod.Horizontal;
            incomeTimerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            incomeTimerFill.color = SignalGold;
        }

        private void CreatePressurePill(RectTransform parent)
        {
            var pill = CreatePill(parent, 110f, ArcaneBlue);
            pressureBadge = pill.GetComponent<Image>();

            var stack = pill.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(8, 8, 5, 5);
            stack.spacing = 1f;
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            var labelText = CreateText("Label", pill, 10, FontStyle.Bold, new Color(Cloud.r, Cloud.g, Cloud.b, 0.68f), TextAnchor.MiddleCenter);
            labelText.text = "PRESSURE";

            pressureText = CreateText("Value", pill, 18, FontStyle.Bold, ArcaneBlue, TextAnchor.MiddleCenter);
            pressureText.text = "0";
        }

        private static RectTransform CreatePill(RectTransform parent, float preferredWidth, Color accent)
        {
            var pill = CreateRect("HUD Pill", parent);
            var background = pill.gameObject.AddComponent<Image>();
            background.color = new Color(PanelInk.r + accent.r * 0.04f, PanelInk.g + accent.g * 0.04f, PanelInk.b + accent.b * 0.04f, PanelInk.a);

            var layoutElement = pill.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.minWidth = Mathf.Min(preferredWidth, 80f);
            layoutElement.flexibleWidth = 0f;

            return pill;
        }

        private static Text CreateText(string name, RectTransform parent, int fontSize, FontStyle style, Color color, TextAnchor alignment)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void RefreshVisuals()
        {
            if (laneText is not null)
            {
                laneText.text = LaneText.ToUpperInvariant();
            }

            if (livesText is not null)
            {
                livesText.text = LivesText;
            }

            if (goldText is not null)
            {
                goldText.text = GoldText;
            }

            if (incomeText is not null)
            {
                incomeText.text = $"+{IncomeText}";
            }

            if (incomeTimerText is not null)
            {
                incomeTimerText.text = IncomeTickSoon ? $"{IncomeTimerText}!" : IncomeTimerText;
                incomeTimerText.color = IncomeTickSoon ? MintSignal : SignalGold;
            }

            if (incomeTimerFill is not null)
            {
                var remaining = Mathf.Clamp01(float.Parse(IncomeTimerText) / IncomeIntervalTicks);
                incomeTimerFill.fillAmount = 1f - remaining;
                incomeTimerFill.color = IncomeTickSoon ? MintSignal : SignalGold;
            }

            if (pressureText is not null)
            {
                pressureText.text = PressureText;
                pressureText.color = PressureText == "0" ? ArcaneBlue : Danger;
            }

            if (pressureBadge is not null)
            {
                pressureBadge.color = PressureText == "0"
                    ? new Color(PanelInk.r + ArcaneBlue.r * 0.04f, PanelInk.g + ArcaneBlue.g * 0.04f, PanelInk.b + ArcaneBlue.b * 0.04f, PanelInk.a)
                    : new Color(PanelInk.r + Danger.r * 0.06f, PanelInk.g + Danger.g * 0.04f, PanelInk.b + Danger.b * 0.03f, PanelInk.a);
            }
        }
    }
}
