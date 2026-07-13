#nullable enable

using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class LaneViewToggleController : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.035f, 0.045f, 0.075f, 1f);
        private static readonly Color PanelInk = new Color(0.08f, 0.12f, 0.20f, 0.86f);
        private static readonly Color PanelEdge = new Color(0.70f, 0.88f, 1f, 1f);
        private static readonly Color InactiveLane = new Color(0.46f, 0.57f, 0.72f, 1f);
        private static readonly Color ActiveLane = new Color(0.22f, 0.72f, 1f, 1f);

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
                railRect.xMax - 34f * scale,
                railRect.y + 12f * scale,
                34f * scale,
                38f * scale);

            buttonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            if (DrawFlatButton(rect, NextViewLabel, selectorExpanded ? ActiveLane : InactiveLane, PanelEdge, Ink, buttonStyle))
            {
                ToggleView();
            }

            if (!selectorExpanded)
            {
                return;
            }

            var buttonHeight = 28f * scale;
            var gap = 4f * scale;
            var panelWidth = 38f * scale;
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
                miniButtonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
                if (DrawFlatButton(laneRect, LaneButtonLabel(lane), isActive ? ActiveLane : InactiveLane, PanelEdge, Ink, miniButtonStyle))
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
                normal = { textColor = Ink }
            };
            miniButtonStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ink }
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
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static bool DrawFlatButton(Rect rect, string label, Color fill, Color border, Color text, GUIStyle style)
        {
            DrawPanel(Inflate(rect, 1f), border);
            DrawPanel(rect, fill);

            var previousTextColor = style.normal.textColor;
            style.normal.textColor = text;
            GUI.Label(rect, label, style);
            style.normal.textColor = previousTextColor;

            var currentEvent = Event.current;
            if (currentEvent.type != EventType.MouseDown || !rect.Contains(currentEvent.mousePosition))
            {
                return false;
            }

            currentEvent.Use();
            return true;
        }

    }
}
