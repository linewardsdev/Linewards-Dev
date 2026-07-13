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
        private LaneViewToggleController? laneViewToggle;
        private GUIStyle? titleStyle;
        private GUIStyle? bodyStyle;
        private GUIStyle? buttonStyle;

        public void Initialize(UnitySimulationDriver driver, LocalPlaytestRecorder recorder, LaneViewToggleController viewToggle)
        {
            simulationDriver = driver;
            playtestRecorder = recorder;
            laneViewToggle = viewToggle;
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
            var width = Mathf.Min(frame.width - 16f * scale, 360f * scale);
            var height = 158f * scale;
            var rect = new Rect(frame.x + (frame.width - width) * 0.5f, frame.y + 112f * scale, width, height);

            var previousColor = GUI.color;
            GUI.color = PanelInk;
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;

            titleStyle!.fontSize = Mathf.RoundToInt(16f * scale);
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            buttonStyle!.fontSize = Mathf.RoundToInt(13f * scale);

            var stateText = simulationDriver.LatestMatchSummary is not null
                ? "MATCH COMPLETE"
                : !simulationDriver.HasStarted
                    ? "READY"
                    : simulationDriver.IsPaused ? "PAUSED" : "RUNNING";
            titleStyle.normal.textColor = simulationDriver.LatestMatchSummary is not null ? SignalGold : MintSignal;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 8f * scale, rect.width - 24f * scale, 22f * scale), stateText, titleStyle);

            var help = !simulationDriver.HasStarted
                ? "Review the board, place towers, then start."
                : simulationDriver.IsPaused ? "Paused. Adjust placement or resume." : "Space pauses. R restarts.";
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 33f * scale, rect.width - 24f * scale, 20f * scale), help, bodyStyle);

            var buttonY = rect.y + 64f * scale;
            var buttonWidth = (rect.width - 36f * scale) / 2f;
            if (GUI.Button(new Rect(rect.x + 12f * scale, buttonY, buttonWidth, 34f * scale), simulationDriver.HasStarted && !simulationDriver.IsPaused ? "PAUSE" : "START", buttonStyle))
            {
                simulationDriver.TogglePause();
            }

            if (GUI.Button(new Rect(rect.x + 24f * scale + buttonWidth, buttonY, buttonWidth, 34f * scale), "RESTART", buttonStyle))
            {
                simulationDriver.ResetMatch();
                playtestRecorder?.ResetRecorder();
            }

            if (laneViewToggle is not null
                && GUI.Button(new Rect(rect.x + 12f * scale, rect.y + 106f * scale, rect.width - 24f * scale, 34f * scale), laneViewToggle.NextViewLabel, buttonStyle))
            {
                laneViewToggle.ToggleView();
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
