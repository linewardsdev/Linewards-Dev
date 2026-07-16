#nullable enable

using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class LaneViewToggleController : MonoBehaviour
    {
        private static readonly Color PanelInk = new Color(0.08f, 0.12f, 0.20f, 0.86f);
        private static readonly Color InactiveLane = new Color(0.46f, 0.57f, 0.72f, 1f);
        private static readonly Color ActiveLane = new Color(0.22f, 0.72f, 1f, 1f);
        private static readonly Color MintSignal = new Color(0.349f, 0.882f, 0.714f, 1f);

        [SerializeField]
        private UnityVerticalSliceRenderer renderer = null!;

        private UnitySimulationDriver? simulationDriver;

        [SerializeField]
        private bool showRuntimeToggle = true;

        private GUIStyle? buttonStyle;
        private GUIStyle? miniButtonStyle;
        private bool selectorExpanded;

        public bool IsShowingMap => false;

        public string NextViewLabel => LaneShortLabel(renderer?.ActiveLaneCameraId ?? 1);

        public void Initialize(UnityVerticalSliceRenderer presentationRenderer, UnitySimulationDriver? driver = null)
        {
            renderer = presentationRenderer;
            simulationDriver = driver;
            ShowLaneView();
        }

        public void ToggleView()
        {
            if (selectorExpanded)
            {
                selectorExpanded = false;
                return;
            }

            selectorExpanded = true;
        }

        public void ShowLaneView()
        {
            renderer?.SetActiveLaneCameraId(1);
            selectorExpanded = false;
        }

        public void ShowMapView()
        {
            selectorExpanded = true;
        }

        private void OnGUI()
        {
            if (!showRuntimeToggle || renderer == null)
            {
                return;
            }

            if (simulationDriver?.LatestMatchSummary is not null)
            {
                selectorExpanded = false;
                return;
            }

            EnsureStyle();
            var scale = MobileViewportLayout.UiScale();
            var railRect = MobileViewportLayout.RightRailRect(scale, 0f);
            var rect = new Rect(
                railRect.xMax - 44f * scale,
                railRect.y - 58f * scale,
                44f * scale,
                44f * scale);

            buttonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            if (RuntimeUiChrome.DrawControlButton(rect, NextViewLabel, selectorExpanded ? MintSignal : ActiveLane, selectorExpanded, scale, buttonStyle))
            {
                ToggleView();
            }

            if (!selectorExpanded)
            {
                return;
            }

            var buttonSize = 38f * scale;
            var gap = 5f * scale;
            var panelWidth = buttonSize;
            var panelX = rect.x - panelWidth - gap;
            var panelRect = new Rect(
                panelX - 4f * scale,
                rect.y - 4f * scale,
                panelWidth + 8f * scale,
                buttonSize * 3f + gap * 2f + 8f * scale);
            DrawPanel(panelRect, PanelInk);

            for (var lane = 1; lane <= 3; lane++)
            {
                var laneRect = new Rect(panelX, rect.y + (lane - 1) * (buttonSize + gap), panelWidth, buttonSize);
                var isActive = renderer.ActiveLaneCameraId == lane;
                miniButtonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
                if (RuntimeUiChrome.DrawControlButton(laneRect, LaneButtonLabel(lane), isActive ? ActiveLane : InactiveLane, isActive, scale, miniButtonStyle))
                {
                    renderer.SetActiveLaneCameraId(lane);
                    selectorExpanded = false;
                }
            }
        }

        private void EnsureStyle()
        {
            buttonStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ActiveLane }
            };
            miniButtonStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ActiveLane }
            };
        }

        private static string LaneShortLabel(int laneId) => $"L{Mathf.Clamp(laneId, 1, 3)}";

        private static string LaneButtonLabel(int laneId) => laneId switch
        {
            2 => "L2",
            3 => "L3",
            _ => "L1"
        };

        private static void DrawPanel(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

    }
}
