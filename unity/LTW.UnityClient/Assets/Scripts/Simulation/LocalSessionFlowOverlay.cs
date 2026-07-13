#nullable enable

using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class LocalSessionFlowOverlay : MonoBehaviour
    {
        private static readonly Color Cloud = new Color(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Ink = new Color(0.035f, 0.045f, 0.075f, 1f);
        private static readonly Color PanelInk = new Color(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color PanelEdge = new Color(0.70f, 0.88f, 1f, 1f);
        private static readonly Color MintSignal = new Color(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color SignalGold = new Color(1f, 0.784f, 0.29f, 1f);

        private UnitySimulationDriver simulationDriver = null!;
        private LocalPlaytestRecorder? playtestRecorder;
        private GUIStyle? buttonStyle;

        public void Initialize(UnitySimulationDriver driver, LocalPlaytestRecorder recorder)
        {
            simulationDriver = driver;
            playtestRecorder = recorder;
        }

        private void OnGUI()
        {
            if (simulationDriver is null)
            {
                return;
            }

            EnsureStyle();
            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var buttonWidth = 42f * scale;
            var buttonHeight = 26f * scale;
            var gap = 5f * scale;
            var y = frame.y + 144f * scale;
            var x = frame.xMax - buttonWidth * 2f - gap - MobileViewportLayout.EdgeMargin(scale);

            buttonStyle!.fontSize = Mathf.RoundToInt(11f * scale);

            var playLabel = simulationDriver.HasStarted && !simulationDriver.IsPaused ? "PAUSE" : "PLAY";
            var playColor = simulationDriver.HasStarted && !simulationDriver.IsPaused ? SignalGold : MintSignal;
            if (DrawFlatButton(new Rect(x, y, buttonWidth, buttonHeight), playLabel, playColor, Ink, buttonStyle))
            {
                simulationDriver.TogglePause();
            }

            if (DrawFlatButton(new Rect(x + buttonWidth + gap, y, buttonWidth, buttonHeight), "RESET", PanelInk, Cloud, buttonStyle))
            {
                simulationDriver.ResetMatch();
                playtestRecorder?.ResetRecorder();
            }
        }

        private void EnsureStyle()
        {
            buttonStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ink }
            };
        }

        private static bool DrawFlatButton(Rect rect, string label, Color fill, Color text, GUIStyle style)
        {
            DrawRect(Inflate(rect, 1f), PanelEdge);
            DrawRect(rect, fill);

            var previousTextColor = style.normal.textColor;
            style.normal.textColor = text;
            GUI.Label(rect, label, style);
            style.normal.textColor = previousTextColor;

            var currentEvent = Event.current;
            if (currentEvent.type != EventType.MouseUp || !rect.Contains(currentEvent.mousePosition))
            {
                return false;
            }

            currentEvent.Use();
            return true;
        }

        private static Rect Inflate(Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
