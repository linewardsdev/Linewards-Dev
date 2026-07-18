#nullable enable

using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class LocalSessionFlowOverlay : MonoBehaviour
    {
        private static readonly Color Cloud = new Color(0.957f, 0.969f, 1f, 1f);
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

            if (simulationDriver.LatestMatchSummary is not null)
            {
                return;
            }

            EnsureStyle();
            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var buttonWidth = 62f * scale;
            var buttonHeight = 26f * scale;
            var resetWidth = 34f * scale;
            var resetHeight = 24f * scale;
            var gap = 4f * scale;
            var y = frame.y + 58f * scale;
            var x = frame.xMax - buttonWidth - MobileViewportLayout.EdgeMargin(scale);

            buttonStyle!.fontSize = Mathf.RoundToInt(11f * scale);

            var playLabel = simulationDriver.HasStarted && !simulationDriver.IsPaused ? "PAUSE" : "PLAY";
            var playColor = simulationDriver.HasStarted && !simulationDriver.IsPaused ? SignalGold : MintSignal;
            if (RuntimeUiChrome.DrawPanelButton(new Rect(x, y, buttonWidth, buttonHeight), playLabel, playColor, scale, buttonStyle))
            {
                simulationDriver.TogglePause();
            }

            buttonStyle.fontSize = Mathf.RoundToInt(10f * scale);
            if (RuntimeUiChrome.DrawPanelButton(new Rect(x + buttonWidth - resetWidth, y + buttonHeight + gap, resetWidth, resetHeight), "R", Cloud, scale, buttonStyle))
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
                normal = { textColor = Cloud }
            };
        }
    }
}
