#nullable enable

using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class LocalSessionFlowOverlay : MonoBehaviour
    {
        private static readonly Color PanelInk = new Color(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color MintSignal = new Color(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color SignalGold = new Color(1f, 0.784f, 0.29f, 1f);
        private static readonly Color Cloud = new Color(0.957f, 0.969f, 1f, 1f);

        private UnitySimulationDriver simulationDriver = null!;
        private LocalPlaytestRecorder? playtestRecorder;
        private GUIStyle? titleStyle;
        private GUIStyle? bodyStyle;
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

            EnsureStyles();
            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var running = simulationDriver.HasStarted && !simulationDriver.IsPaused && simulationDriver.LatestMatchSummary is null;
            var width = Mathf.Min(frame.width - 18f * scale, running ? 226f * scale : 322f * scale);
            var height = running ? 38f * scale : 72f * scale;
            var rect = new Rect(
                frame.x + (frame.width - width) * 0.5f,
                frame.yMax - MobileViewportLayout.BottomMargin(scale) - 62f * scale - height - 8f * scale,
                width,
                height);

            var previousColor = GUI.color;
            GUI.color = PanelInk;
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;

            titleStyle!.fontSize = Mathf.RoundToInt((running ? 12f : 15f) * scale);
            bodyStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            buttonStyle!.fontSize = Mathf.RoundToInt(12f * scale);

            var stateText = simulationDriver.LatestMatchSummary is not null
                ? "MATCH COMPLETE"
                : !simulationDriver.HasStarted
                    ? "READY"
                    : simulationDriver.IsPaused ? "PAUSED" : "RUNNING";
            titleStyle.normal.textColor = simulationDriver.LatestMatchSummary is not null ? SignalGold : MintSignal;

            if (running)
            {
                GUI.Label(new Rect(rect.x + 10f * scale, rect.y, 72f * scale, rect.height), stateText, titleStyle);
                var compactButtonWidth = 62f * scale;
                if (GUI.Button(new Rect(rect.xMax - compactButtonWidth * 2f - 14f * scale, rect.y + 5f * scale, compactButtonWidth, 28f * scale), "PAUSE", buttonStyle))
                {
                    simulationDriver.TogglePause();
                }

                if (GUI.Button(new Rect(rect.xMax - compactButtonWidth - 8f * scale, rect.y + 5f * scale, compactButtonWidth, 28f * scale), "RESET", buttonStyle))
                {
                    simulationDriver.ResetMatch();
                    playtestRecorder?.ResetRecorder();
                }

                return;
            }

            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 6f * scale, rect.width - 24f * scale, 20f * scale), stateText, titleStyle);

            var help = !simulationDriver.HasStarted
                ? "Review the board, place towers, then start."
                : simulationDriver.IsPaused ? "Paused. Adjust placement or resume." : "Space pauses. R restarts.";
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 27f * scale, rect.width - 24f * scale, 18f * scale), help, bodyStyle);

            var buttonY = rect.y + 45f * scale;
            var buttonWidth = (rect.width - 36f * scale) / 2f;
            if (GUI.Button(new Rect(rect.x + 12f * scale, buttonY, buttonWidth, 22f * scale), simulationDriver.HasStarted && !simulationDriver.IsPaused ? "PAUSE" : "START", buttonStyle))
            {
                simulationDriver.TogglePause();
            }

            if (GUI.Button(new Rect(rect.x + 24f * scale + buttonWidth, buttonY, buttonWidth, 22f * scale), "RESTART", buttonStyle))
            {
                simulationDriver.ResetMatch();
                playtestRecorder?.ResetRecorder();
            }
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = MintSignal }
            };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Cloud }
            };
            buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };
        }
    }
}
