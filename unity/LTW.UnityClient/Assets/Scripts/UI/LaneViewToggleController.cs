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

        public bool IsShowingMap => renderer != null && renderer.CameraFraming == LaneCameraFraming.AllLanes;

        public string NextViewLabel => IsShowingMap ? "LANE" : "MAP";

        public void Initialize(UnityVerticalSliceRenderer presentationRenderer)
        {
            renderer = presentationRenderer;
            ShowLaneView();
        }

        public void ToggleView()
        {
            if (IsShowingMap)
            {
                ShowLaneView();
                return;
            }

            ShowMapView();
        }

        public void ShowLaneView()
        {
            renderer?.SetCameraFraming(LaneCameraFraming.ActiveLane);
        }

        public void ShowMapView()
        {
            renderer?.SetCameraFraming(LaneCameraFraming.AllLanes);
        }

        private void OnGUI()
        {
            if (!showRuntimeToggle || renderer == null)
            {
                return;
            }

            EnsureStyle();
            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var width = 72f * scale;
            var height = 40f * scale;
            var rect = new Rect(
                frame.xMax - width - 8f * scale,
                frame.yMax - height - MobileViewportLayout.BottomMargin(scale) - 74f * scale,
                width,
                height);

            buttonStyle!.fontSize = Mathf.RoundToInt(13f * scale);
            var previousColor = GUI.color;
            GUI.color = IsShowingMap ? LaneBlue : Cloud;
            if (GUI.Button(rect, NextViewLabel, buttonStyle))
            {
                ToggleView();
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
        }
    }
}
