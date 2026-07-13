#nullable enable

using UnityEngine;

namespace LTW.UnityClient.UI
{
    public static class MobileViewportLayout
    {
        public const float PortraitAspect = 9f / 19.5f;

        public static Rect CameraRect()
        {
            var screenAspect = Screen.height <= 0 ? 16f / 9f : (float)Screen.width / Screen.height;
            var width = Mathf.Clamp(PortraitAspect / screenAspect, 0.22f, 1f);
            return new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        public static Rect ScreenRect()
        {
            var cameraRect = CameraRect();
            return new Rect(
                cameraRect.x * Screen.width,
                cameraRect.y * Screen.height,
                cameraRect.width * Screen.width,
                cameraRect.height * Screen.height);
        }

        public static float UiScale() => Mathf.Clamp(Mathf.Min(Screen.width / 1080f, Screen.height / 720f), 0.78f, 1.08f);


        public static float EdgeMargin(float scale) => Mathf.Max(8f * scale, Screen.width * 0.008f);

        public static float TopMargin(float scale) => Mathf.Max(8f * scale, Screen.height * 0.012f);

        public static float BottomDockHeight(float scale) => 144f * scale + BottomMargin(scale);

        public static Rect TopHudRect(float scale, float height)
        {
            var frame = ScreenRect();
            var margin = EdgeMargin(scale);
            return new Rect(frame.x + margin, frame.y + TopMargin(scale), frame.width - margin * 2f, height);
        }

        public static Rect RightRailRect(float scale, float orderFromBottom)
        {
            var frame = ScreenRect();
            var width = 48f * scale;
            var height = 72f * scale;
            return new Rect(
                frame.xMax - width - EdgeMargin(scale) * 0.25f,
                frame.y + 188f * scale + orderFromBottom * (height + 8f * scale),
                width,
                height);
        }

        public static float BottomMargin(float scale) => Mathf.Max(30f * scale, Screen.height * 0.035f);
    }
}
