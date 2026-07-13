#nullable enable

using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class MatchResultsBillboard : MonoBehaviour
    {
        private static readonly Color PanelInk = new(0.035f, 0.045f, 0.075f, 0.94f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);

        private static GUIStyle? titleStyle;
        private static GUIStyle? rowStyle;

        private UnitySimulationDriver simulationDriver = null!;

        public void Initialize(UnitySimulationDriver driver)
        {
            simulationDriver = driver;
        }

        private void OnGUI()
        {
            var summary = simulationDriver?.LatestMatchSummary;
            if (summary is null)
            {
                return;
            }

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var width = Mathf.Min(frame.width - 18f * scale, 260f * scale);
            var height = 136f * scale;
            var rect = new Rect(
                frame.x + (frame.width - width) * 0.5f,
                frame.y + 54f * scale,
                width,
                height);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), SignalGold);

            titleStyle!.fontSize = Mathf.RoundToInt(15f * scale);
            titleStyle.normal.textColor = SignalGold;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 8f * scale, rect.width - 24f * scale, 24f * scale), "MATCH COMPLETE", titleStyle);

            rowStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            rowStyle.normal.textColor = Cloud;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 31f * scale, rect.width - 24f * scale, 16f * scale), $"P{summary.WinnerId.Value} wins  •  T{summary.CompletedAtTick.Value}", rowStyle);

            var y = rect.y + 51f * scale;
            foreach (var player in summary.Players)
            {
                var status = player.IsEliminated ? "OUT" : "ACTIVE";
                var color = player.PlayerId.Value == summary.WinnerId.Value ? MintSignal : Cloud;
                rowStyle.normal.textColor = color;
                GUI.Label(
                    new Rect(rect.x + 8f * scale, y, rect.width - 16f * scale, 14f * scale),
                    $"P{player.PlayerId.Value} {status}   L{player.Lives.Amount}   +{player.Income.Amount}   {player.Gold.Amount}G",
                    rowStyle);
                y += 14f * scale;
            }
        }

        private static void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = SignalGold }
            };

            rowStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            DrawAccent(rect, color);
            DrawAccent(new Rect(rect.x, rect.y, rect.width, 2f), SignalGold);
            DrawAccent(new Rect(rect.x, rect.y, 2f, rect.height), SignalGold);
            DrawAccent(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), SignalGold);
        }

        private static void DrawAccent(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
