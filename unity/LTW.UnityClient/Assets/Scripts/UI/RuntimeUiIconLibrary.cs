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
            GUI.DrawTextureWithTexCoords(FitTrimmedRect(rect, uv, texture), texture, uv, true);
            GUI.color = previousColor;
            return true;
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
                    if (pixels[rowOffset + x].a == 0)
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

            return new Rect(
                minX / (float)width,
                minY / (float)height,
                (maxX - minX + 1) / (float)width,
                (maxY - minY + 1) / (float)height);
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

            return new Rect(
                container.x + (container.width - width) * 0.5f,
                container.y + (container.height - height) * 0.5f,
                width,
                height);
        }
    }
}
