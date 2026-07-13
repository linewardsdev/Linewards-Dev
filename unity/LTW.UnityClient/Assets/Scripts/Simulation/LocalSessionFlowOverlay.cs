#nullable enable

using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class LocalSessionFlowOverlay : MonoBehaviour
    {
        private static readonly Color Cloud = new Color(0.957f, 0.969f, 1f, 1f);
        private static readonly Color PanelInk = new Color(0.08f, 0.12f, 0.22f, 0.92f);
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
            var buttonWidth = 48f * scale;
            var buttonHeight = 28f * scale;
            var gap = 6f * scale;
            var y = frame.y + 86f * scale;
            var x = frame.x + MobileViewportLayout.EdgeMargin(scale);

            buttonStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            var previousColor = GUI.color;

            var playLabel = simulationDriver.HasStarted && !simulationDriver.IsPaused ? "PAUSE" : "PLAY";
            GUI.color = simulationDriver.HasStarted && !simulationDriver.IsPaused ? SignalGold : MintSignal;
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), playLabel, buttonStyle))
            {
                simulationDriver.TogglePause();
            }

            GUI.color = PanelInk;
            if (GUI.Button(new Rect(x + buttonWidth + gap, y, buttonWidth, buttonHeight), "RESET", buttonStyle))
            {
                simulationDriver.ResetMatch();
                playtestRecorder?.ResetRecorder();
            }

            GUI.color = previousColor;
        }

        private void EnsureStyle()
        {
            buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };
        }
    }
}
