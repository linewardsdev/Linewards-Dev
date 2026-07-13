#nullable enable

using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class LaneViewToggleController : MonoBehaviour
    {
        private static readonly Color Cloud = new Color(0.957f, 0.969f, 1f, 1f);
        private static readonly Color LaneBlue = new Color(0.25f, 0.58f, 1f, 1f);
        private static readonly Color PanelInk = new Color(0.055f, 0.067f, 0.11f, 0.88f);
        private static readonly Color PanelEdge = new Color(0.20f, 0.36f, 0.62f, 0.72f);
        private static readonly Color InactiveLane = new Color(0.11f, 0.14f, 0.22f, 0.92f);
        private static readonly Color ActiveLane = new Color(0.10f, 0.36f, 0.74f, 0.96f);

        [SerializeField]
        private UnityVerticalSliceRenderer renderer = null!;

        [SerializeField]
        private bool showRuntimeToggle = true;

        private GUIStyle? buttonStyle;
        private GUIStyle? miniButtonStyle;
        private bool selectorExpanded;

        public bool IsShowingMap => false;

        public string NextViewLabel => LaneShortLabel(renderer?.ActiveLaneCameraId ?? 1);

        public void Initialize(UnityVerticalSliceRenderer presentationRenderer)
        {
            renderer = presentationRenderer;
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

            EnsureStyle();
            var scale = MobileViewportLayout.UiScale();
            var rect = MobileViewportLayout.RightRailRect(scale, 0f);

            buttonStyle!.fontSize = Mathf.RoundToInt(14f * scale);
            var previousColor = GUI.color;
            DrawPanel(Inflate(rect, 2f * scale), selectorExpanded ? PanelEdge : PanelInk);
            GUI.color = selectorExpanded ? ActiveLane : InactiveLane;
            if (GUI.Button(rect, NextViewLabel, buttonStyle))
            {
                ToggleView();
            }

            GUI.color = previousColor;

            if (!selectorExpanded)
            {
                return;
            }

            var buttonHeight = 38f * scale;
            var gap = 6f * scale;
            var panelWidth = 82f * scale;
            var panelX = rect.x - panelWidth - gap;
            var panelRect = new Rect(
                panelX - 5f * scale,
                rect.y - 5f * scale,
                panelWidth + 10f * scale,
                buttonHeight * 3f + gap * 2f + 10f * scale);
            DrawPanel(panelRect, PanelInk);

            for (var lane = 1; lane <= 3; lane++)
            {
                var laneRect = new Rect(panelX, rect.y + (lane - 1) * (buttonHeight + gap), panelWidth, buttonHeight);
                var isActive = renderer.ActiveLaneCameraId == lane;
                DrawPanel(Inflate(laneRect, isActive ? 2f * scale : 1f * scale), isActive ? PanelEdge : InactiveLane);
                if (isActive)
                {
                    DrawAccent(new Rect(laneRect.x, laneRect.y + 4f * scale, 4f * scale, laneRect.height - 8f * scale), LaneBlue);
                }

                GUI.color = isActive ? ActiveLane : InactiveLane;
                miniButtonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
                if (GUI.Button(laneRect, LaneButtonLabel(lane), miniButtonStyle))
                {
                    renderer.SetActiveLaneCameraId(lane);
                    selectorExpanded = false;
                }
            }

            GUI.color = previousColor;
        }

        private void EnsureStyle()
        {
            buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud },
                hover = { textColor = Cloud },
                active = { textColor = Cloud },
                focused = { textColor = Cloud }
            };
            miniButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud },
                hover = { textColor = Cloud },
                active = { textColor = Cloud },
                focused = { textColor = Cloud }
            };
        }

        private static string LaneShortLabel(int laneId) => $"L{Mathf.Clamp(laneId, 1, 3)}";

        private static string LaneButtonLabel(int laneId) => laneId switch
        {
            2 => "Lane 2",
            3 => "Lane 3",
            _ => "Lane 1"
        };

        private static Rect Inflate(Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;
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
