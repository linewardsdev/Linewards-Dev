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
        private static readonly Color DisabledInk = new(0.22f, 0.25f, 0.32f, 0.88f);
        private static readonly Color DisabledText = new(0.55f, 0.59f, 0.68f, 1f);

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
        private TouchPlacementController touchPlacementController = null!;

        [SerializeField]
        private bool showRuntimeDock = true;

        private bool isExpanded;

        public bool IsExpanded => isExpanded;

        public void CloseDock() => isExpanded = false;

        public void Initialize(UnityCommandAdapter adapter, PlacementFeedbackView feedback)
        {
            commandAdapter = adapter;
            feedbackView = feedback;
        }

        public void SendRunner() => Send(commandAdapter.SendSampleCreep(), "Runner sent", 10);

        public void SendBrute() => Send(commandAdapter.SendBruteCreep(), "Brute sent", 18);

        public void SendSwarm() => Send(commandAdapter.SendSwarmCreep(), "Swarm sent", 18);

        public void SendShade() => Send(commandAdapter.SendShadeCreep(), "Shade sent", 24);

        public void SendSiege() => Send(commandAdapter.SendSiegeCreep(), "Siege sent", 40);

        private void OnGUI()
        {
            if (!showRuntimeDock)
            {
                return;
            }

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var launcherSize = 56f * scale;
            var launcherRect = new Rect(frame.xMax - launcherSize - 12f * scale, frame.yMax - launcherSize - MobileViewportLayout.BottomMargin(scale), launcherSize, launcherSize);
            var touchPlacement = TouchPlacement;
            if (touchPlacement?.IsTowerPaletteExpanded == true)
            {
                isExpanded = false;
                return;
            }

            if (!isExpanded)
            {
                if (DrawLauncherButton(launcherRect, "SEND", SignalGold, scale))
                {
                    touchPlacement?.CloseBottomPanelsForSend();
                    isExpanded = true;
                }

                return;
            }

            var width = Mathf.Min(frame.width - 16f * scale, 430f * scale);
            var height = 172f * scale;
            var launcherClearance = 66f * scale;
            var rect = new Rect(frame.xMax - width - 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale) - launcherClearance, width, height);

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

            var gold = CurrentPlayerGold();
            metaStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            metaStyle.normal.textColor = MintSignal;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 37f * scale, rect.width - 24f * scale, 18f * scale), $"GOLD {gold}", metaStyle);

            var buttonY = rect.y + 54f * scale;
            var buttonHeight = 50f * scale;
            var gap = 6f * scale;
            var buttonWidth = (rect.width - 24f * scale - gap * 2f) / 3f;
            var x = rect.x + 12f * scale;

            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "RUN", "10G  +1", CreepIconKind.Runner, ArcaneBlue, gold >= 10, scale))
            {
                SendRunner();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "BRUTE", "18G  +2", CreepIconKind.Brute, WardViolet, gold >= 18, scale))
            {
                SendBrute();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "SWARM", "18G  +3", CreepIconKind.Swarm, SignalGold, gold >= 18, scale))
            {
                SendSwarm();
            }

            var secondRowY = buttonY + buttonHeight + gap;
            var secondRowWidth = (rect.width - 24f * scale - gap) / 2f;
            x = rect.x + 12f * scale;
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "SHADE", "24G  +3", CreepIconKind.Shade, MintSignal, gold >= 24, scale))
            {
                SendShade();
            }

            x += secondRowWidth + gap;
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "SIEGE", "40G  +4", CreepIconKind.Siege, new Color(1f, 0.62f, 0.26f), gold >= 40, scale))
            {
                SendSiege();
            }
        }

        private TouchPlacementController? TouchPlacement
        {
            get
            {
                if (touchPlacementController == null)
                {
                    touchPlacementController = Object.FindAnyObjectByType<TouchPlacementController>();
                }

                return touchPlacementController;
            }
        }

        private void Send(LTW.Simulation.Bridge.VerticalSliceCommandResult result, string successMessage, int cost)
        {
            if (result.Accepted)
            {
                feedbackView.ShowEconomy(successMessage);
                return;
            }

            if (result.RejectionReason == LTW.Simulation.Commands.CommandRejectionReason.InsufficientGold)
            {
                feedbackView.ShowRejected(result.RejectionReason, cost, CurrentPlayerGold());
            }
            else
            {
                feedbackView.ShowRejected(result.RejectionReason);
            }
        }

        private static bool DrawSendButton(Rect rect, string label, string meta, CreepIconKind iconKind, Color accent, bool isAffordable, float scale)
        {
            var displayAccent = isAffordable ? accent : DisabledText;
            var previousColor = GUI.color;
            GUI.color = isAffordable ? TintPanel(accent, 0.08f) : DisabledInk;
            var previousEnabled = GUI.enabled;
            GUI.enabled = isAffordable;
            var pressed = GUI.Button(rect, GUIContent.none, buttonStyle);
            GUI.enabled = previousEnabled;
            GUI.color = previousColor;

            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), displayAccent);
            var iconRect = new Rect(rect.x + 7f * scale, rect.y + 9f * scale, 20f * scale, 27f * scale);
            DrawCreepIcon(iconRect, iconKind, displayAccent, scale);

            buttonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            buttonStyle.normal.textColor = isAffordable ? Cloud : DisabledText;
            buttonStyle.hover.textColor = buttonStyle.normal.textColor;
            buttonStyle.active.textColor = buttonStyle.normal.textColor;
            GUI.Label(new Rect(rect.x + 26f * scale, rect.y + 9f * scale, rect.width - 28f * scale, 21f * scale), label, buttonStyle);

            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = displayAccent;
            GUI.Label(new Rect(rect.x + 26f * scale, rect.y + 34f * scale, rect.width - 28f * scale, 17f * scale), meta, metaStyle);
            return pressed;
        }

        private static void DrawCreepIcon(Rect rect, CreepIconKind iconKind, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = accent;

            var cx = rect.x + rect.width * 0.5f;
            var cy = rect.y + rect.height * 0.52f;
            var line = Mathf.Max(2f * scale, 1f);

            switch (iconKind)
            {
                case CreepIconKind.Brute:
                    GUI.DrawTexture(new Rect(cx - 10f * scale, cy - 7f * scale, 20f * scale, 14f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 13f * scale, cy - 2f * scale, 5f * scale, 9f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 8f * scale, cy - 2f * scale, 5f * scale, 9f * scale), Texture2D.whiteTexture);
                    break;
                case CreepIconKind.Swarm:
                    GUI.DrawTexture(new Rect(cx - 9f * scale, cy - 6f * scale, 5f * scale, 5f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 1f * scale, cy - 11f * scale, 5f * scale, 5f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 7f * scale, cy - 4f * scale, 5f * scale, 5f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 5f * scale, cy + 4f * scale, 5f * scale, 5f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 4f * scale, cy + 8f * scale, 5f * scale, 5f * scale), Texture2D.whiteTexture);
                    break;
                case CreepIconKind.Shade:
                    GUI.color = new Color(accent.r, accent.g, accent.b, 0.42f);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy - 8f * scale, 14f * scale, 15f * scale), Texture2D.whiteTexture);
                    GUI.color = accent;
                    GUI.DrawTexture(new Rect(cx - 3f * scale, cy - 5f * scale, 13f * scale, 13f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 1f * scale, cy + 8f * scale, 5f * scale, 5f * scale), Texture2D.whiteTexture);
                    break;
                case CreepIconKind.Siege:
                    GUI.DrawTexture(new Rect(cx - 11f * scale, cy - 6f * scale, 22f * scale, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 5f * scale, cy + 5f * scale, 10f * scale, 10f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 14f * scale, cy - 1f * scale, 4f * scale, 9f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 10f * scale, cy - 1f * scale, 4f * scale, 9f * scale), Texture2D.whiteTexture);
                    break;
                default:
                    GUI.DrawTexture(new Rect(cx - 5f * scale, cy - 11f * scale, 10f * scale, 18f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - line * 0.5f, cy + 3f * scale, line, 13f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 8f * scale, cy + 2f * scale, 16f * scale, line), Texture2D.whiteTexture);
                    break;
            }

            GUI.color = previousColor;
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

        private int CurrentPlayerGold()
        {
            if (commandAdapter == null)
            {
                commandAdapter = Object.FindAnyObjectByType<UnityCommandAdapter>();
            }

            return commandAdapter?.CurrentPlayerGold() ?? 0;
        }

        private void EnsureDriver()
        {
            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }
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

        private enum CreepIconKind
        {
            Runner,
            Brute,
            Swarm,
            Shade,
            Siege
        }
    }
}
