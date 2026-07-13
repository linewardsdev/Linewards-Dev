#nullable enable

using LTW.Simulation.Commands;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class PlacementFeedbackView : MonoBehaviour
    {
        private const float MessageDurationSeconds = 1.35f;
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);

        private static GUIStyle? panelStyle;
        private static GUIStyle? messageStyle;

        private float hideAtSeconds;
        private Color accent = MintSignal;

        [SerializeField]
        private bool showRuntimeToast = true;

        public string FeedbackText { get; private set; } = string.Empty;

        public void ShowAccepted() => ShowAccepted("Placed");

        public void ShowAccepted(string message)
        {
            Show(message, MintSignal);
        }

        public void ShowRejected(CommandRejectionReason reason)
        {
            var message = reason switch
            {
                CommandRejectionReason.InsufficientGold => "Need more gold",
                CommandRejectionReason.CooldownActive => "Send is cooling down",
                CommandRejectionReason.MatchPaused => "Start or resume match",
                CommandRejectionReason.CellOccupied => "Cell already has a tower",
                CommandRejectionReason.PathBlocked => "Keep a path open",
                CommandRejectionReason.InvalidLane => "Choose your lane",
                CommandRejectionReason.PlayerEliminated => "Player is eliminated",
                _ => "Action unavailable"
            };
            Show(message, Danger);
        }

        public void ShowEconomy(string message)
        {
            Show(message, SignalGold);
        }

        public void Clear()
        {
            FeedbackText = string.Empty;
            hideAtSeconds = 0f;
        }

        private void Show(string message, Color messageAccent)
        {
            FeedbackText = message;
            accent = messageAccent;
            hideAtSeconds = Time.unscaledTime + MessageDurationSeconds;
        }

        private void OnGUI()
        {
            if (!showRuntimeToast || string.IsNullOrEmpty(FeedbackText))
            {
                return;
            }

            if (hideAtSeconds > 0f && Time.unscaledTime > hideAtSeconds)
            {
                Clear();
                return;
            }

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var width = Mathf.Min(frame.width - 16f * scale, 390f * scale);
            var height = 48f * scale;
            var rect = new Rect(frame.x + (frame.width - width) * 0.5f, frame.yMax - height - 206f * scale, width, height);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            messageStyle!.fontSize = Mathf.RoundToInt(18f * scale);
            messageStyle.normal.textColor = Cloud;
            GUI.Label(rect, FeedbackText.ToUpperInvariant(), messageStyle);
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

            messageStyle = new GUIStyle(GUI.skin.label)
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

        private static RectOffset ZeroOffset() => new RectOffset(0, 0, 0, 0);
    }
}
