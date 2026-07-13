#nullable enable

using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class SendDockController : MonoBehaviour
    {
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
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
        private UnitySimulationDriver simulationDriver = null!;

        [SerializeField]
        private bool showRuntimeDock = true;

        private bool isExpanded;

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

            var scale = UiScale();
            var launcherSize = 64f * scale;
            var launcherRect = new Rect(Screen.width - launcherSize - 12f * scale, Screen.height - launcherSize - BottomMargin(scale), launcherSize, launcherSize);
            if (!isExpanded)
            {
                if (DrawLauncherButton(launcherRect, "SEND", SignalGold, scale))
                {
                    isExpanded = true;
                }

                return;
            }

            var width = Mathf.Min(Screen.width - 32f * scale, 430f * scale);
            var height = 176f * scale;
            var rect = new Rect(Screen.width - width - 12f * scale, Screen.height - height - BottomMargin(scale), width, height);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), SignalGold);
            if (DrawLauncherButton(launcherRect, "CLOSE", SignalGold, scale))
            {
                isExpanded = false;
                return;
            }

            titleStyle!.fontSize = Mathf.RoundToInt(14f * scale);
            titleStyle.normal.textColor = SignalGold;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 8f * scale, rect.width - 72f * scale, 22f * scale), "SEND PRESSURE", titleStyle);
            if (GUI.Button(new Rect(rect.xMax - 58f * scale, rect.y + 8f * scale, 44f * scale, 28f * scale), "CLOSE", buttonStyle))
            {
                isExpanded = false;
                return;
            }

            var cooldown = SendCooldown();
            var gold = PlayerGold();
            metaStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            metaStyle.normal.textColor = cooldown == 0 ? MintSignal : SignalGold;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 37f * scale, rect.width - 24f * scale, 18f * scale), cooldown == 0 ? "READY TO SEND" : $"COOLDOWN {cooldown} TICKS", metaStyle);

            var buttonY = rect.y + 62f * scale;
            var buttonHeight = 90f * scale;
            var gap = 8f * scale;
            var buttonWidth = (rect.width - 24f * scale - gap * 2f) / 3f;
            var x = rect.x + 12f * scale;

            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "RUNNER", "10g  +1", "fast", ArcaneBlue, gold >= 10 && cooldown == 0, scale))
            {
                SendRunner();
                isExpanded = false;
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "BRUTE", "18g  +2", "tank", WardViolet, gold >= 18 && cooldown == 0, scale))
            {
                SendBrute();
                isExpanded = false;
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "SWARM", "3x 6g  +1", "wide", SignalGold, gold >= 18 && cooldown == 0, scale))
            {
                SendSwarm();
                isExpanded = false;
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

        private static bool DrawSendButton(Rect rect, string label, string meta, string purpose, Color accent, bool enabled, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = TintPanel(accent, enabled ? 0.08f : 0.025f);
            GUI.enabled = enabled;
            var pressed = GUI.Button(rect, GUIContent.none, buttonStyle);
            GUI.enabled = true;
            GUI.color = previousColor;

            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), enabled ? accent : new Color(accent.r, accent.g, accent.b, 0.38f));

            buttonStyle!.fontSize = Mathf.RoundToInt(14f * scale);
            buttonStyle.normal.textColor = enabled ? Cloud : new Color(Cloud.r, Cloud.g, Cloud.b, 0.5f);
            buttonStyle.hover.textColor = buttonStyle.normal.textColor;
            buttonStyle.active.textColor = buttonStyle.normal.textColor;
            GUI.Label(new Rect(rect.x, rect.y + 13f * scale, rect.width, 24f * scale), label, buttonStyle);

            metaStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            metaStyle.normal.textColor = enabled ? accent : new Color(accent.r, accent.g, accent.b, 0.48f);
            GUI.Label(new Rect(rect.x, rect.y + 42f * scale, rect.width, 20f * scale), meta, metaStyle);
            GUI.Label(new Rect(rect.x, rect.y + 63f * scale, rect.width, 18f * scale), enabled ? purpose : "wait", metaStyle);
            return enabled && pressed;
        }

        private static bool DrawLauncherButton(Rect rect, string label, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = TintPanel(accent, 0.12f);
            var pressed = GUI.Button(rect, GUIContent.none, buttonStyle);
            GUI.color = previousColor;

            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);
            buttonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            buttonStyle.normal.textColor = accent;
            GUI.Label(rect, label, buttonStyle);
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

        private int PlayerGold()
        {
            EnsureDriver();
            var snapshot = simulationDriver?.LatestSnapshot;
            return snapshot?.Players.Get(new PlayerId(1)).Gold.Amount ?? 0;
        }

        private int SendCooldown()
        {
            EnsureDriver();
            var snapshot = simulationDriver?.LatestSnapshot;
            if (snapshot is null)
            {
                return 0;
            }

            var player = snapshot.Players.Get(new PlayerId(1));
            var cooldownTicks = player.NextSendAvailableTick.Value - snapshot.Tick.Value;
            return cooldownTicks <= 0 ? 0 : cooldownTicks > int.MaxValue ? int.MaxValue : (int)cooldownTicks;
        }

        private void EnsureDriver()
        {
            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }
        }

        private static float UiScale() => Mathf.Clamp(Mathf.Min(Screen.width / 1080f, Screen.height / 720f), 0.74f, 1.12f);

        private static float BottomMargin(float scale) => Mathf.Max(18f * scale, Screen.height * 0.025f);

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
