#nullable enable

using System.Linq;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class RuntimeMatchHud : MonoBehaviour
    {
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color WardViolet = new(0.608f, 0.424f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);

        private static GUIStyle? panelStyle;
        private static GUIStyle? titleStyle;
        private static GUIStyle? statStyle;
        private static GUIStyle? metaStyle;

        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        [SerializeField]
        private bool showRuntimeHud = true;

        private void OnGUI()
        {
            if (!showRuntimeHud)
            {
                return;
            }

            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }

            var snapshot = simulationDriver?.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            EnsureStyles();
            var scale = UiScale();
            var margin = 12f * scale;
            var width = Mathf.Min(Screen.width - margin * 2f, 610f * scale);
            var height = 78f * scale;
            var rect = new Rect((Screen.width - width) * 0.5f, margin, width, height);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), StateAccent());

            var player = snapshot.Players.Get(new PlayerId(1));
            var cooldown = Mathf.Max(0, player.NextSendAvailableTick.Value - snapshot.Tick.Value);
            DrawStat(new Rect(rect.x + 12f * scale, rect.y + 9f * scale, 118f * scale, 56f * scale), "LIVES", player.Lives.Amount.ToString(), player.Lives.Amount <= 30 ? Danger : MintSignal, scale);
            DrawStat(new Rect(rect.x + 135f * scale, rect.y + 9f * scale, 108f * scale, 56f * scale), "GOLD", player.Gold.Amount.ToString(), SignalGold, scale);
            DrawStat(new Rect(rect.x + 248f * scale, rect.y + 9f * scale, 112f * scale, 56f * scale), "INCOME", "+" + player.Income.Amount, MintSignal, scale);
            DrawStat(new Rect(rect.x + 365f * scale, rect.y + 9f * scale, 118f * scale, 56f * scale), "SEND", cooldown == 0 ? "READY" : cooldown.ToString(), cooldown == 0 ? MintSignal : SignalGold, scale);

            titleStyle!.fontSize = Mathf.RoundToInt(13f * scale);
            titleStyle.normal.textColor = StateAccent();
            var state = simulationDriver!.LatestMatchSummary is not null ? "RESULTS" : !simulationDriver.HasStarted ? "READY" : simulationDriver.IsPaused ? "PAUSED" : "LIVE";
            GUI.Label(new Rect(rect.xMax - 116f * scale, rect.y + 11f * scale, 100f * scale, 22f * scale), state, titleStyle);
            metaStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            metaStyle.normal.textColor = Cloud;
            GUI.Label(new Rect(rect.xMax - 116f * scale, rect.y + 38f * scale, 100f * scale, 20f * scale), $"TICK {snapshot.Tick.Value}", metaStyle);

            DrawOpponentStrip(rect, snapshot.Players.Players.Where(playerState => playerState.PlayerId.Value != 1).ToArray(), scale);
        }

        private Color StateAccent()
        {
            if (simulationDriver == null)
            {
                return ArcaneBlue;
            }

            if (simulationDriver.LatestMatchSummary is not null)
            {
                return SignalGold;
            }

            if (!simulationDriver.HasStarted || simulationDriver.IsPaused)
            {
                return WardViolet;
            }

            return MintSignal;
        }

        private static void DrawOpponentStrip(Rect parent, PlayerEconomyState[] opponents, float scale)
        {
            var x = parent.x + 12f * scale;
            var y = parent.yMax + 6f * scale;
            var width = Mathf.Min(138f * scale, (parent.width - 24f * scale) / Mathf.Max(1, opponents.Length) - 6f * scale);
            foreach (var opponent in opponents)
            {
                var accent = opponent.IsEliminated ? Danger : opponent.PlayerId.Value == 2 ? WardViolet : ArcaneBlue;
                var rect = new Rect(x, y, width, 22f * scale);
                DrawPanel(rect, new Color(PanelInk.r, PanelInk.g, PanelInk.b, 0.76f));
                DrawAccent(new Rect(rect.x, rect.yMax - 2f * scale, rect.width, 2f * scale), accent);
                metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
                metaStyle.normal.textColor = Cloud;
                GUI.Label(rect, $"P{opponent.PlayerId.Value}  {opponent.Lives.Amount}L  +{opponent.Income.Amount}", metaStyle);
                x += width + 8f * scale;
            }
        }

        private static void DrawStat(Rect rect, string label, string value, Color accent, float scale)
        {
            metaStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            metaStyle.normal.textColor = accent;
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 16f * scale), label, metaStyle);

            statStyle!.fontSize = Mathf.RoundToInt(20f * scale);
            statStyle.normal.textColor = Cloud;
            GUI.Label(new Rect(rect.x, rect.y + 20f * scale, rect.width, 28f * scale), value, statStyle);
        }

        private static float UiScale() => Mathf.Clamp(Mathf.Min(Screen.width / 1080f, Screen.height / 720f), 0.74f, 1.12f);

        private static void EnsureStyles()
        {
            if (panelStyle is not null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box) { border = new RectOffset(6, 6, 6, 6), margin = ZeroOffset(), padding = ZeroOffset() };
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, normal = { textColor = Cloud } };
            statStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, normal = { textColor = Cloud } };
            metaStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, normal = { textColor = Cloud } };
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
