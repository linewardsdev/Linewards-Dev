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

            // The board is lifted clear of whatever drawer is open, rather than drawn underneath it.
            // Reported from an iPad: the build menu covers the last row of the lane, so those cells
            // cannot be seen or built on. An orthographic camera maps its size to whatever viewport
            // height it is given, so shortening the viewport draws the WHOLE lane smaller rather
            // than cropping the bottom off it — every cell stays reachable, which a draggable panel
            // would only have achieved while the player held it out of the way.
            var height = Mathf.Clamp01(1f - DockInsetFraction());

            // Width scales with height so the viewport keeps its ASPECT as it shortens. An
            // orthographic camera's size is vertical, so a shorter-but-equally-wide viewport has a
            // wider aspect and reveals more world sideways — which at LaneSpacing 9 means the
            // neighbours' lanes sliding into view around the player's own. Scaling both together
            // draws the same board smaller instead, which is the intent: nothing new appears, and
            // nothing is hidden under the drawer.
            var scaledWidth = width * height;
            return new Rect((1f - scaledWidth) * 0.5f, 1f - height, scaledWidth, height);
        }

        /// <summary>The open drawer's height as a fraction of the screen, clamped to something sane.</summary>
        /// <remarks>
        /// Capped at 0.45 so a mis-set inset can never squeeze the board to nothing. The drawers
        /// this reflects are 282 reference units against a 932 reference height, so a correct value
        /// sits well under the cap and only a bug reaches it.
        /// </remarks>
        private static float DockInsetFraction()
        {
            var height = ViewportHeight;
            if (height <= 0 || RuntimeUiChrome.BottomDockInset <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp(RuntimeUiChrome.BottomDockInset / height, 0f, 0.45f);
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

        /// <summary>
        /// The HUD frame: the board column at FULL height, whatever the camera is doing.
        /// </summary>
        /// <remarks>
        /// Deliberately no longer derived from <see cref="CameraRect"/>, and the difference is a
        /// feedback loop rather than a nicety. The drawers are positioned inside this frame, and
        /// the camera is inset by however much of the bottom a drawer covers — so deriving this
        /// from the camera means the drawer's own height moves the frame it is measured in, which
        /// moves the drawer, which changes the inset. The first attempt did exactly that and left
        /// the top HUD strip floating in the middle of the board.
        ///
        /// Only the CAMERA is lifted clear of a drawer. The HUD stays anchored to the screen, which
        /// is also what a player expects: buttons that shuffle upward as another panel opens are
        /// harder to hit than ones that stay put.
        /// </remarks>
        public static Rect ScreenRect()
        {
            var width = BoardColumnFraction();
            return new Rect(
                (1f - width) * 0.5f * ViewportWidth,
                0f,
                width * ViewportWidth,
                ViewportHeight);
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
