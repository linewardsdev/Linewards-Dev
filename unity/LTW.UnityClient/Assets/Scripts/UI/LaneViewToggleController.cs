#nullable enable

using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class LaneViewToggleController : MonoBehaviour
    {
        private static readonly Color Cloud = new Color(0.957f, 0.969f, 1f, 1f);
        private static readonly Color LaneBlue = new Color(0.25f, 0.58f, 1f, 1f);
        private static readonly Color PanelInk = new Color(0.035f, 0.043f, 0.07f, 0.98f);
        private static readonly Color PanelEdge = new Color(0.34f, 0.58f, 0.95f, 1f);
        private static readonly Color InactiveLane = new Color(0.18f, 0.22f, 0.32f, 1f);
        private static readonly Color ActiveLane = new Color(0.14f, 0.46f, 0.95f, 1f);

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
            var railRect = MobileViewportLayout.RightRailRect(scale, 0f);
            var rect = new Rect(
                railRect.xMax - 40f * scale,
                railRect.y + 10f * scale,
                40f * scale,
                46f * scale);

            buttonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            var previousColor = GUI.color;
            DrawPanel(Inflate(rect, selectorExpanded ? 2f * scale : 1f * scale), selectorExpanded ? PanelEdge : PanelInk);
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

            var buttonHeight = 32f * scale;
            var gap = 5f * scale;
            var panelWidth = 46f * scale;
            var panelX = rect.x - panelWidth - gap;
            var panelRect = new Rect(
                panelX - 4f * scale,
                rect.y - 4f * scale,
                panelWidth + 8f * scale,
                buttonHeight * 3f + gap * 2f + 8f * scale);
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
            2 => "L2",
            3 => "L3",
            _ => "L1"
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
