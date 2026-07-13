#nullable enable

using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class LaneViewToggleController : MonoBehaviour
    {
        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);

        private static GUIStyle? buttonStyle;

        [SerializeField]
        private UnityVerticalSliceRenderer renderer = null!;

        [SerializeField]
        private bool showRuntimeToggle = true;

        private bool showingMap;

        public void Initialize(UnityVerticalSliceRenderer presentationRenderer)
        {
            renderer = presentationRenderer;
            showingMap = false;
            ApplyCurrentView();
        }

        public void ToggleView()
        {
            showingMap = !showingMap;
            ApplyCurrentView();
        }

        private void OnGUI()
        {
            if (!showRuntimeToggle || renderer == null)
            {
                return;
            }

            EnsureStyles();

            var scale = Mathf.Clamp(Screen.width / 1080f, 0.72f, 1.15f);
            var width = 72f * scale;
            var height = 42f * scale;
            var rect = new Rect(Screen.width - width - 12f * scale, 218f * scale, width, height);
            var nextView = showingMap ? "LANE" : "MAP";

            var previousColor = GUI.color;
            GUI.color = new Color(PanelInk.r + ArcaneBlue.r * 0.1f, PanelInk.g + ArcaneBlue.g * 0.1f, PanelInk.b + ArcaneBlue.b * 0.1f, PanelInk.a);
            buttonStyle!.fontSize = Mathf.RoundToInt(13f * scale);
            if (GUI.Button(rect, nextView, buttonStyle))
            {
                ToggleView();
            }

            GUI.color = previousColor;
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), ArcaneBlue);
        }

        private static void EnsureStyles()
        {
            if (buttonStyle is not null)
            {
                return;
            }

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = Cloud },
                hover = { textColor = Cloud },
                active = { textColor = Cloud }
            };
        }

        private static void DrawAccent(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void ApplyCurrentView()
        {
            if (renderer == null)
            {
                return;
            }

            renderer.SetCameraFraming(showingMap ? LaneCameraFraming.AllLanes : LaneCameraFraming.ActiveLane);
        }
    }
}
