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

        /// <summary>True when a capture has described its own surface, rather than falling back to Screen.</summary>
        public static bool HasCaptureViewportOverride => captureViewportOverride.HasValue;

        public static void ClearCaptureViewportOverride()
        {
            captureViewportOverride = null;
        }

        /// <summary>
        /// Narrowest side rail worth placing UI in, as a fraction of screen width.
        /// </summary>
        /// <remarks>
        /// Below this a rail is too thin to hold a readable seat row, so the margin is given back to
        /// the board instead of left as dead space. 0.12 is chosen against the actual device range:
        /// a 16:9 phone leaves 0.09 a side and absorbs it, while the narrowest tablet — iPad mini at
        /// 0.657 — leaves 0.148, which is 221px of its 1488 and enough for a compact rail.
        /// </remarks>
        public const float MinSideRailFraction = 0.12f;

        /// <summary>
        /// The board column, and whether there is usable margin either side of it.
        /// </summary>
        /// <remarks>
        /// The board is a 7x16 grid framed to fill the view vertically — orthographicSize is 8.45
        /// against a 16-cell lane — so it CANNOT be scaled up to fill a wider screen. Zooming in to
        /// fill an iPad's width crops the top or bottom of the player's own lane, which is
        /// unplayable. Widening the camera does not magnify the board either; an orthographic
        /// camera's size is vertical, so extra width only reveals more world sideways, and with
        /// LaneSpacing 9 against LaneWidth 7 that is a 2-unit gutter and then a sliver of the
        /// neighbours' lanes.
        ///
        /// So the margin is a fact of the board's shape, and the only question is what fills it.
        /// Two answers, chosen by how much there is:
        ///
        /// - Not enough for a rail (phones): give it back to the board. The camera spans the full
        ///   width and shows a little more gutter, which is empty board surround rather than dead
        ///   screen. A 16:9 iPhone SE was losing 17.9% of its display to this.
        /// - Enough for a rail (tablets): keep the board column and hand the margin to the HUD and
        ///   the seat leaderboard, so the extra width does work instead of being padding.
        ///
        /// Only a 19.5:9 phone was ever clean. Everything else — 5" phones included — was
        /// letterboxed, which is why this is not a tablet-only fix.
        /// </remarks>
        public static Rect CameraRect()
        {
            var width = BoardColumnFraction();
            return new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        private static float BoardColumnFraction()
        {
            var height = ViewportHeight;
            var screenAspect = height <= 0 ? 16f / 9f : (float)ViewportWidth / height;
            var natural = Mathf.Clamp(PortraitAspect / screenAspect, 0.22f, 1f);

            // Absorb a margin too thin to be useful rather than leaving it dark.
            var railFraction = (1f - natural) * 0.5f;
            return railFraction < MinSideRailFraction ? 1f : natural;
        }

        /// <summary>Whether this screen is wide enough to carry UI beside the board.</summary>
        public static bool HasSideRails => BoardColumnFraction() < 1f;

        /// <summary>
        /// The usable margin on one side of the board, in GUI pixels. Zero-width when there is none.
        /// </summary>
        /// <remarks>
        /// Intersected with the safe area on its own, not derived from <see cref="SafeScreenRect"/>,
        /// because that one is clipped to the board column and a rail lies entirely outside it.
        /// </remarks>
        public static Rect SideRailRect(bool rightSide)
        {
            if (!HasSideRails)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            var board = ScreenRect();
            var rail = rightSide
                ? new Rect(board.xMax, 0f, ViewportWidth - board.xMax, ViewportHeight)
                : new Rect(0f, 0f, board.xMin, ViewportHeight);

            var safeArea = SafeArea;
            var guiSafeArea = new Rect(safeArea.x, ViewportHeight - safeArea.yMax, safeArea.width, safeArea.height);
            return Intersect(rail, guiSafeArea);
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
