#nullable enable

using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class LaneViewToggleController : MonoBehaviour
    {
        private static readonly Color Cloud = new Color(0.957f, 0.969f, 1f, 1f);
        private static readonly Color LaneBlue = new Color(0.25f, 0.58f, 1f, 1f);

        [SerializeField]
        private UnityVerticalSliceRenderer renderer = null!;

        [SerializeField]
        private bool showRuntimeToggle = true;

        private GUIStyle? buttonStyle;
        private GUIStyle? miniButtonStyle;
        private bool selectorExpanded;

        public bool IsShowingMap => false;

        public string NextViewLabel => selectorExpanded ? "×" : LaneShortLabel(renderer?.ActiveLaneCameraId ?? 1);

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

            buttonStyle!.fontSize = Mathf.RoundToInt(13f * scale);
            var previousColor = GUI.color;
            GUI.color = selectorExpanded ? LaneBlue : Cloud;
            if (GUI.Button(rect, NextViewLabel, buttonStyle))
            {
                ToggleView();
            }

            GUI.color = previousColor;

            if (!selectorExpanded)
            {
                return;
            }

            var buttonHeight = 36f * scale;
            var gap = 6f * scale;
            var panelWidth = 74f * scale;
            var panelX = rect.x - panelWidth - gap;
            for (var lane = 1; lane <= 3; lane++)
            {
                var laneRect = new Rect(panelX, rect.y + (lane - 1) * (buttonHeight + gap), panelWidth, buttonHeight);
                var isActive = renderer.ActiveLaneCameraId == lane;
                GUI.color = isActive ? LaneBlue : Cloud;
                miniButtonStyle!.fontSize = Mathf.RoundToInt(11f * scale);
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
                normal = { textColor = Cloud }
            };
            miniButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };
        }

        private static string LaneShortLabel(int laneId) => $"L{Mathf.Clamp(laneId, 1, 3)}";

        private static string LaneButtonLabel(int laneId) => laneId switch
        {
            2 => "Lane 2",
            3 => "Lane 3",
            _ => "Lane 1"
        };
    }
}
