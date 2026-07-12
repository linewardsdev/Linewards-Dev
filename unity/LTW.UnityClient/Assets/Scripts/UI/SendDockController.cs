using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class SendDockController : MonoBehaviour
    {
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color WardViolet = new(0.608f, 0.424f, 1f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);

        private static GUIStyle? panelStyle;
        private static GUIStyle? titleStyle;
        private static GUIStyle? buttonStyle;
        private static GUIStyle? metaStyle;

        [SerializeField]
        private UnityCommandAdapter commandAdapter = null!;

        [SerializeField]
        private PlacementFeedbackView feedbackView = null!;

        [SerializeField]
        private bool showRuntimeDock = true;

        public void Initialize(UnityCommandAdapter adapter, PlacementFeedbackView feedback)
        {
            commandAdapter = adapter;
            feedbackView = feedback;
        }

        public void SendRunner() => Send(commandAdapter.SendSampleCreep(), "Runner sent");

        public void SendBrute() => Send(commandAdapter.SendBruteCreep(), "Brute sent");

        public void SendSwarm() => Send(commandAdapter.SendSwarmCreep(), "Swarm sent");

        private void OnGUI()
        {
            if (!showRuntimeDock)
            {
                return;
            }

            EnsureStyles();

            var scale = Mathf.Clamp(Screen.width / 1080f, 0.72f, 1.15f);
            var width = Mathf.Min(Screen.width - 32f * scale, 390f * scale);
            var height = 154f * scale;
            var rect = new Rect(Screen.width - width - 12f * scale, Screen.height - height - 18f * scale, width, height);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), SignalGold);

            titleStyle!.fontSize = Mathf.RoundToInt(14f * scale);
            titleStyle.normal.textColor = SignalGold;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 8f * scale, rect.width - 24f * scale, 22f * scale), "SEND PRESSURE", titleStyle);

            var buttonY = rect.y + 38f * scale;
            var buttonHeight = 94f * scale;
            var gap = 8f * scale;
            var buttonWidth = (rect.width - 24f * scale - gap * 2f) / 3f;
            var x = rect.x + 12f * scale;

            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "RUNNER", "10g  +1", ArcaneBlue, scale))
            {
                SendRunner();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "BRUTE", "18g  +2", WardViolet, scale))
            {
                SendBrute();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "SWARM", "3x 6g  +1", SignalGold, scale))
            {
                SendSwarm();
            }
        }

        private void Send(LTW.Simulation.Bridge.VerticalSliceCommandResult result, string successMessage)
        {
            if (result.Accepted)
            {
                feedbackView.ShowEconomy(successMessage);
                return;
            }

            feedbackView.ShowRejected(result.RejectionReason);
        }

        private static bool DrawSendButton(Rect rect, string label, string meta, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = TintPanel(accent, 0.06f);
            var pressed = GUI.Button(rect, GUIContent.none, buttonStyle);
            GUI.color = previousColor;

            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            buttonStyle!.fontSize = Mathf.RoundToInt(15f * scale);
            buttonStyle.normal.textColor = Cloud;
            buttonStyle.hover.textColor = Cloud;
            buttonStyle.active.textColor = Cloud;
            GUI.Label(new Rect(rect.x, rect.y + 18f * scale, rect.width, 26f * scale), label, buttonStyle);

            metaStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            metaStyle.normal.textColor = accent;
            GUI.Label(new Rect(rect.x, rect.y + 49f * scale, rect.width, 22f * scale), meta, metaStyle);
            return pressed;
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
