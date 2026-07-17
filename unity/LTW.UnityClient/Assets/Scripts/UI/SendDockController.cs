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
        private int highlightedCreepRole = -1;
        private int reviewGoldOverride = -1;

        public bool IsExpanded => isExpanded;

        public void CloseDock() => isExpanded = false;

        public void Initialize(UnityCommandAdapter adapter, PlacementFeedbackView feedback)
        {
            commandAdapter = adapter;
            feedbackView = feedback;
        }

        public void SendRunner() => Send(commandAdapter.SendSampleCreep(), "Runner sent", 10, 0);

        public void SendBrute() => Send(commandAdapter.SendBruteCreep(), "Brute sent", 18, 1);

        public void SendSwarm() => Send(commandAdapter.SendSwarmCreep(), "Swarm sent", 18, 2);

        public void SendShade() => Send(commandAdapter.SendShadeCreep(), "Shade sent", 24, 3);

        public void SendSiege() => Send(commandAdapter.SendSiegeCreep(), "Siege sent", 40, 4);

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
            var height = 282f * scale;
            var launcherClearance = 136f * scale;
            var rect = new Rect(frame.xMax - width - 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale) - launcherClearance, width, height);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), SignalGold);
            if (DrawLauncherButton(launcherRect, "CLOSE", SignalGold, scale))
            {
                isExpanded = false;
                return;
            }

            titleStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            titleStyle.normal.textColor = SignalGold;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 10f * scale, 120f * scale, 20f * scale), "SEND", titleStyle);
            if (GUI.Button(new Rect(rect.xMax - 72f * scale, rect.y + 8f * scale, 58f * scale, 32f * scale), "CLOSE", buttonStyle))
            {
                isExpanded = false;
                return;
            }

            var gold = CurrentPlayerGold();
            metaStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            metaStyle.normal.textColor = MintSignal;
            GUI.Label(new Rect(rect.xMax - 132f * scale, rect.y + 12f * scale, 58f * scale, 18f * scale), $"G{gold}", metaStyle);

            var buttonY = rect.y + 84f * scale;
            var buttonHeight = 84f * scale;
            var gap = 8f * scale;
            var buttonWidth = (rect.width - 24f * scale - gap * 2f) / 3f;
            var x = rect.x + 12f * scale;

            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "RUN", "10G  +1", CreepIconKind.Runner, ArcaneBlue, gold >= 10, highlightedCreepRole == 0, scale))
            {
                SendRunner();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "BRUTE", "18G  +2", CreepIconKind.Brute, WardViolet, gold >= 18, highlightedCreepRole == 1, scale))
            {
                SendBrute();
            }

            x += buttonWidth + gap;
            if (DrawSendButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "SWARM", "18G  +3", CreepIconKind.Swarm, SignalGold, gold >= 18, highlightedCreepRole == 2, scale))
            {
                SendSwarm();
            }

            var secondRowY = buttonY + buttonHeight + gap;
            var secondRowWidth = (rect.width - 24f * scale - gap) / 2f;
            x = rect.x + 12f * scale;
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "SHADE", "24G  +3", CreepIconKind.Shade, MintSignal, gold >= 24, highlightedCreepRole == 3, scale))
            {
                SendShade();
            }

            x += secondRowWidth + gap;
            if (DrawSendButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "SIEGE", "40G  +4", CreepIconKind.Siege, new Color(1f, 0.62f, 0.26f), gold >= 40, highlightedCreepRole == 4, scale))
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

        private void Send(LTW.Simulation.Bridge.VerticalSliceCommandResult result, string successMessage, int cost, int creepRole)
        {
            if (result.Accepted)
            {
                highlightedCreepRole = creepRole;
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

        private static bool DrawSendButton(Rect rect, string label, string meta, CreepIconKind iconKind, Color accent, bool isAffordable, bool isSelected, float scale)
        {
            var displayAccent = isAffordable ? accent : DisabledText;
            var state = isAffordable
                ? isSelected ? CommandCardState.Selected : CommandCardState.Normal
                : CommandCardState.Disabled;
            var pressed = RuntimeUiChrome.DrawCommandCard(rect, accent, state, scale);

            var iconRect = RuntimeUiChrome.CommandCardIconRect(rect, scale);
            if (!RuntimeUiIconLibrary.DrawIcon(iconRect, CreepIconResourceName(iconKind), isAffordable))
            {
                DrawCreepIcon(iconRect, iconKind, displayAccent, scale);
            }

            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            buttonStyle.normal.textColor = isAffordable ? Cloud : DisabledText;
            buttonStyle.hover.textColor = buttonStyle.normal.textColor;
            buttonStyle.active.textColor = buttonStyle.normal.textColor;
            GUI.Label(RuntimeUiChrome.CommandCardLabelRect(rect, scale), CompactCreepLabel(label), buttonStyle);

            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = displayAccent;
            GUI.Label(RuntimeUiChrome.CommandCardMetaRect(rect, scale), meta, metaStyle);
            return pressed;
        }

        private static string CompactCreepLabel(string label)
        {
            return label switch
            {
                "BRUTE" => "BRT",
                "SWARM" => "SWM",
                "SHADE" => "SHD",
                "SIEGE" => "SGE",
                _ => label
            };
        }

        private static string CreepIconResourceName(CreepIconKind iconKind)
        {
            return iconKind switch
            {
                CreepIconKind.Brute => "ui_icon_send_brute_v01",
                CreepIconKind.Swarm => "ui_icon_send_swarm_v01",
                CreepIconKind.Shade => "ui_icon_send_shade_v01",
                CreepIconKind.Siege => "ui_icon_send_siege_v01",
                _ => "ui_icon_send_runner_v01"
            };
        }

        private static void DrawCreepIcon(Rect rect, CreepIconKind iconKind, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = accent;

            var cx = rect.x + rect.width * 0.5f;
            var cy = rect.y + rect.height * 0.52f;
            var line = Mathf.Max(2f * scale, 1f);
            var dimAccent = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01(accent.a * 0.48f));

            switch (iconKind)
            {
                case CreepIconKind.Brute:
                    DrawIconRect(new Rect(cx - 11f * scale, cy - 8f * scale, 22f * scale, 16f * scale), accent);
                    DrawIconRect(new Rect(cx - 15f * scale, cy - 4f * scale, 6f * scale, 12f * scale), accent);
                    DrawIconRect(new Rect(cx + 9f * scale, cy - 4f * scale, 6f * scale, 12f * scale), accent);
                    DrawIconRect(new Rect(cx - 5f * scale, cy - 2f * scale, 10f * scale, 4f * scale), Cloud);
                    break;
                case CreepIconKind.Swarm:
                    DrawIconRect(new Rect(cx - 10f * scale, cy - 7f * scale, 7f * scale, 7f * scale), accent, -25f);
                    DrawIconRect(new Rect(cx - 1f * scale, cy - 12f * scale, 7f * scale, 7f * scale), accent, 18f);
                    DrawIconRect(new Rect(cx + 8f * scale, cy - 4f * scale, 7f * scale, 7f * scale), accent, -18f);
                    DrawIconRect(new Rect(cx - 5f * scale, cy + 4f * scale, 7f * scale, 7f * scale), accent, 32f);
                    DrawIconRect(new Rect(cx + 5f * scale, cy + 8f * scale, 6f * scale, 6f * scale), accent, -35f);
                    DrawIconRect(new Rect(cx - 11f * scale, cy + 11f * scale, 23f * scale, line), dimAccent);
                    break;
                case CreepIconKind.Shade:
                    DrawIconRect(new Rect(cx - 10f * scale, cy - 8f * scale, 9f * scale, 18f * scale), dimAccent, -16f);
                    DrawIconRect(new Rect(cx + 2f * scale, cy - 9f * scale, 8f * scale, 18f * scale), dimAccent, 16f);
                    DrawIconRect(new Rect(cx - 3f * scale, cy - 12f * scale, 7f * scale, 23f * scale), accent);
                    DrawIconRect(new Rect(cx - 8f * scale, cy + 6f * scale, 18f * scale, line), accent);
                    break;
                case CreepIconKind.Siege:
                    DrawIconRect(new Rect(cx - 10f * scale, cy - 7f * scale, 20f * scale, 14f * scale), accent);
                    DrawIconRect(new Rect(cx - 4f * scale, cy - 13f * scale, 8f * scale, 9f * scale), accent);
                    DrawIconRect(new Rect(cx - 3f * scale, cy - 1f * scale, 6f * scale, 17f * scale), Cloud);
                    DrawIconRect(new Rect(cx - 14f * scale, cy + 6f * scale, 28f * scale, line), accent);
                    break;
                default:
                    DrawIconRect(new Rect(cx - 4f * scale, cy - 13f * scale, 8f * scale, 21f * scale), accent);
                    DrawIconRect(new Rect(cx - 9f * scale, cy + 1f * scale, 18f * scale, line), accent);
                    DrawIconRect(new Rect(cx - 2f * scale, cy + 5f * scale, 4f * scale, 12f * scale), accent);
                    DrawIconRect(new Rect(cx - 11f * scale, cy + 9f * scale, 22f * scale, line), dimAccent);
                    break;
            }

            GUI.color = previousColor;
        }

        private static void DrawIconRect(Rect rect, Color color, float rotationDegrees = 0f)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            if (Mathf.Abs(rotationDegrees) > 0.01f)
            {
                var previousMatrix = GUI.matrix;
                var pivot = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
                GUIUtility.RotateAroundPivot(rotationDegrees, pivot);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.matrix = previousMatrix;
            }
            else
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
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
            if (reviewGoldOverride >= 0)
            {
                return reviewGoldOverride;
            }

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
