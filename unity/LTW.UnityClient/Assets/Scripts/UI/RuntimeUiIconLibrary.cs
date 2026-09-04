#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    internal static class RuntimeUiIconLibrary
    {
        private const string ResourceRoot = "Art/UI/Icons/";

        private static readonly Dictionary<string, Texture2D?> TextureCache = new();
        private static readonly Dictionary<string, Rect> TrimCache = new();

        /// <summary>
        /// Draws one roster icon, scaled to fill its slot by the SUBJECT's own silhouette rather
        /// than by the source canvas.
        /// </summary>
        /// <remarks>
        /// Reported live three times over as icons looking "cropped and out of place": every icon
        /// in the roster is authored on the same 128x128 canvas, but how much of that canvas the
        /// actual creature or tower fills varies from 28.7% (Repair Drone, a slim silhouette with a
        /// lot of headroom left by its own artwork) to 100% (Barricade, which fills its canvas edge
        /// to edge). <c>GUI.DrawTexture(..., ScaleMode.ScaleToFit, ...)</c> scales the whole CANVAS
        /// to fit the icon rect, so two icons of equal visual "weight" in their source art render at
        /// wildly different sizes on screen purely from how tightly each was cropped when exported —
        /// which is exactly what read as some icons looking "pasted" smaller than others sharing the
        /// same row. Confirmed by direct measurement (an alpha bounding-box scan) across all 30
        /// roster icons before writing this, not guessed from a single report.
        ///
        /// The fix trims to each texture's own alpha bounding box — computed once per texture via
        /// <see cref="Texture2D.GetPixels32"/> and cached by resource name — and fits THAT region to
        /// the icon rect instead of the full canvas, preserving the subject's own aspect ratio the
        /// same way <see cref="ScaleMode.ScaleToFit"/> would. This needed every icon's Texture
        /// Importer set to Read/Write Enabled (they were not), which costs roughly 64KB of retained
        /// CPU-side memory per 128x128 icon — under 2MB total across the whole roster, immaterial
        /// next to the multi-hundred-MB baseline this project's own memory investigation already
        /// measured elsewhere.
        /// </remarks>
        /// <summary>
        /// Alpha below this is fringe, not subject. The scan used to accept any non-zero alpha,
        /// so one stray sub-visible pixel anywhere on the canvas would have re-framed the whole
        /// icon; measured across the roster (2026-09-03) the visible box and the any-alpha box
        /// differ by at most a pixel today, so this is a guard, not a change in framing. (Twin
        /// Crescent's light backdrop, in the same report, was not a trim problem at all: its
        /// importer alone had mipmaps on with alpha-is-transparency off, which bleeds white into
        /// the lower mips — fixed in its .meta, which now matches the other thirty.)
        /// </summary>
        private const byte VisibleAlpha = 32;

        /// <summary>
        /// Margin kept around the trimmed subject, as a fraction of the trimmed size, so the glow
        /// and anti-aliased edge just outside the visible-alpha box are still drawn instead of
        /// being cut hard at the UV boundary.
        /// </summary>
        private const float TrimMargin = 0.06f;

        /// <summary>
        /// Fraction of the slot left clear on every side. With no inset, every subject was fitted
        /// edge to edge, so spires, glows and the ground-lines baked into several icons landed on
        /// the well's accent ring and read as clipped — the "cropped" in the report. This is what
        /// the well's ring needs to look like a boundary the icon sits INSIDE rather than one it
        /// collides with.
        /// </summary>
        private const float SlotInset = 0.09f;

        /// <summary>
        /// Cap on how far trimming may enlarge a subject beyond a plain canvas fit. Trimming
        /// alone scaled Repair Drone (41 px wide on its 128 canvas) to three times the size the
        /// canvas fit would give it, so the slimmest silhouettes became the largest icons in the
        /// rail — the "out of place" in the report. 1.5 lets a tightly-cropped subject grow enough
        /// to match its neighbours' visual weight without letting a sliver dominate the row.
        /// </summary>
        private const float MaxTrimZoom = 1.5f;

        public static bool DrawIcon(Rect rect, string resourceName, bool enabled)
        {
            var texture = LoadTexture(resourceName);
            if (texture == null)
            {
                return false;
            }

            var previousColor = GUI.color;
            GUI.color = enabled ? Color.white : new Color(0.62f, 0.66f, 0.74f, 0.42f);
            var uv = TrimmedUv(resourceName, texture);
            var slot = Inset(rect, SlotInset);
            GUI.DrawTextureWithTexCoords(FitTrimmedRect(slot, uv, texture), texture, uv, true);
            GUI.color = previousColor;
            return true;
        }

        private static Rect Inset(Rect rect, float fraction)
        {
            var dx = rect.width * fraction;
            var dy = rect.height * fraction;
            return new Rect(rect.x + dx, rect.y + dy, Mathf.Max(1f, rect.width - dx * 2f), Mathf.Max(1f, rect.height - dy * 2f));
        }

        private static Texture2D? LoadTexture(string resourceName)
        {
            if (TextureCache.TryGetValue(resourceName, out var cached))
            {
                return cached;
            }

            var texture = Resources.Load<Texture2D>(ResourceRoot + resourceName);
            TextureCache[resourceName] = texture;
            return texture;
        }

        /// <summary>The subject's own bounding box within its texture, in UV space, cached per resource.</summary>
        private static Rect TrimmedUv(string resourceName, Texture2D texture)
        {
            if (TrimCache.TryGetValue(resourceName, out var cached))
            {
                return cached;
            }

            var uv = ComputeTrimmedUv(texture);
            TrimCache[resourceName] = uv;
            return uv;
        }

        /// <summary>
        /// Scans the alpha channel for the subject's silhouette. Falls back to the full 0-1 UV rect
        /// — today's behaviour — for anything unreadable or fully transparent, so a texture import
        /// setting nobody remembered to flip degrades to "no worse than before" rather than throwing.
        /// </summary>
        private static Rect ComputeTrimmedUv(Texture2D texture)
        {
            var fallback = new Rect(0f, 0f, 1f, 1f);
            if (!texture.isReadable)
            {
                return fallback;
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException)
            {
                return fallback;
            }

            var width = texture.width;
            var height = texture.height;
            var minX = width;
            var maxX = -1;
            var minY = height;
            var maxY = -1;

            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * width;
                for (var x = 0; x < width; x++)
                {
                    if (pixels[rowOffset + x].a < VisibleAlpha)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return fallback;
            }

            // The visible box, then a margin around it (clamped to the canvas) so the halo and
            // anti-aliased edge just outside the threshold are drawn rather than cut at the UV
            // boundary — see TrimMargin.
            var boxWidth = maxX - minX + 1;
            var boxHeight = maxY - minY + 1;
            var marginX = boxWidth * TrimMargin;
            var marginY = boxHeight * TrimMargin;
            var x0 = Mathf.Max(0f, minX - marginX);
            var y0 = Mathf.Max(0f, minY - marginY);
            var x1 = Mathf.Min(width, maxX + 1 + marginX);
            var y1 = Mathf.Min(height, maxY + 1 + marginY);

            return new Rect(x0 / width, y0 / height, (x1 - x0) / width, (y1 - y0) / height);
        }

        /// <summary>
        /// A centred sub-rect of <paramref name="container"/> matching the trimmed region's own
        /// aspect ratio — <see cref="ScaleMode.ScaleToFit"/>'s own behaviour, aimed at the trimmed
        /// bounds instead of the full texture.
        /// </summary>
        private static Rect FitTrimmedRect(Rect container, Rect uv, Texture2D texture)
        {
            var contentWidth = uv.width * texture.width;
            var contentHeight = uv.height * texture.height;
            if (contentWidth <= 0f || contentHeight <= 0f || container.height <= 0f)
            {
                return container;
            }

            var contentAspect = contentWidth / contentHeight;
            var containerAspect = container.width / container.height;

            float width, height;
            if (contentAspect > containerAspect)
            {
                width = container.width;
                height = width / contentAspect;
            }
            else
            {
                height = container.height;
                width = height * contentAspect;
            }

            // Cap the zoom trimming buys, relative to what a plain canvas fit would draw — see
            // MaxTrimZoom. Screen pixels per texel for the trimmed fit against the same for the
            // whole canvas fitted into this container.
            var canvasFitScale = Mathf.Min(container.width / texture.width, container.height / texture.height);
            var trimmedScale = width / contentWidth;
            var maxScale = canvasFitScale * MaxTrimZoom;
            if (trimmedScale > maxScale)
            {
                width = contentWidth * maxScale;
                height = contentHeight * maxScale;
            }

            return new Rect(
                container.x + (container.width - width) * 0.5f,
                container.y + (container.height - height) * 0.5f,
                width,
                height);
        }
    }
}
