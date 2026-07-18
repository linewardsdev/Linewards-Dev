#nullable enable

using UnityEngine;

namespace LTW.UnityClient.UI
{
    public static class MobileViewportLayout
    {
        public const float PortraitAspect = 9f / 19.5f;

        private static CaptureViewport? captureViewportOverride;

        public static int ViewportWidth => captureViewportOverride?.Width ?? Screen.width;

        public static int ViewportHeight => captureViewportOverride?.Height ?? Screen.height;

        public static Rect SafeArea
        {
            get
            {
                if (captureViewportOverride.HasValue)
                {
                    return captureViewportOverride.Value.SafeArea;
                }

                var width = Mathf.Max(1, Screen.width);
                var height = Mathf.Max(1, Screen.height);
                var safeArea = Screen.safeArea;
                return safeArea.width <= 0f || safeArea.height <= 0f
                    ? new Rect(0f, 0f, width, height)
                    : safeArea;
            }
        }

        public static void SetCaptureViewportOverride(int width, int height, Rect safeArea)
        {
            var resolvedWidth = Mathf.Max(1, width);
            var resolvedHeight = Mathf.Max(1, height);
            var bounds = new Rect(0f, 0f, resolvedWidth, resolvedHeight);
            var resolvedSafeArea = safeArea.width <= 0f || safeArea.height <= 0f
                ? bounds
                : Intersect(bounds, safeArea);
            captureViewportOverride = new CaptureViewport(resolvedWidth, resolvedHeight, resolvedSafeArea);
        }

        public static void ClearCaptureViewportOverride()
        {
            captureViewportOverride = null;
        }

        public static Rect CameraRect()
        {
            var height = ViewportHeight;
            var screenAspect = height <= 0 ? 16f / 9f : (float)ViewportWidth / height;
            var width = Mathf.Clamp(PortraitAspect / screenAspect, 0.22f, 1f);
            return new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        public static Rect ScreenRect()
        {
            var cameraRect = CameraRect();
            return new Rect(
                cameraRect.x * ViewportWidth,
                cameraRect.y * ViewportHeight,
                cameraRect.width * ViewportWidth,
                cameraRect.height * ViewportHeight);
        }

        public static Rect SafeScreenRect()
        {
            var safeArea = SafeArea;
            var guiSafeArea = new Rect(
                safeArea.x,
                ViewportHeight - safeArea.yMax,
                safeArea.width,
                safeArea.height);
            return Intersect(ScreenRect(), guiSafeArea);
        }

        public static float UiScale()
        {
            var frame = ScreenRect();
            return Mathf.Clamp(
                Mathf.Min(frame.width / 430f, frame.height / 932f),
                0.78f,
                2.35f);
        }

        public static float EdgeMargin(float scale) => Mathf.Max(8f * scale, ViewportWidth * 0.008f);

        public static float TopMargin(float scale) => Mathf.Max(8f * scale, ViewportHeight * 0.012f);

        public static float BottomDockHeight(float scale) => 144f * scale + BottomMargin(scale);

        public static Rect TopHudRect(float scale, float height)
        {
            var frame = SafeScreenRect();
            var margin = EdgeMargin(scale);
            return new Rect(
                frame.x + margin,
                frame.y + TopMargin(scale),
                Mathf.Max(0f, frame.width - margin * 2f),
                height);
        }

        public static Rect RightRailRect(float scale, float orderFromBottom)
        {
            var frame = SafeScreenRect();
            var width = 48f * scale;
            var height = 72f * scale;
            return new Rect(
                frame.xMax - width - EdgeMargin(scale) * 0.25f,
                frame.y + 188f * scale + orderFromBottom * (height + 8f * scale),
                width,
                height);
        }

        public static float BottomMargin(float scale)
        {
            var safeFrame = SafeScreenRect();
            var bottomInset = Mathf.Max(0f, ViewportHeight - safeFrame.yMax);
            return Mathf.Max(30f * scale, ViewportHeight * 0.035f) + bottomInset;
        }

        private static Rect Intersect(Rect left, Rect right)
        {
            var xMin = Mathf.Max(left.xMin, right.xMin);
            var yMin = Mathf.Max(left.yMin, right.yMin);
            var xMax = Mathf.Min(left.xMax, right.xMax);
            var yMax = Mathf.Min(left.yMax, right.yMax);
            return xMax <= xMin || yMax <= yMin
                ? new Rect(left.x, left.y, 0f, 0f)
                : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private readonly struct CaptureViewport
        {
            public CaptureViewport(int width, int height, Rect safeArea)
            {
                Width = width;
                Height = height;
                SafeArea = safeArea;
            }

            public int Width { get; }

            public int Height { get; }

            public Rect SafeArea { get; }
        }
    }
}
