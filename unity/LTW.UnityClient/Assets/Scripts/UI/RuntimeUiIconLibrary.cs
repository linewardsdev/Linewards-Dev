#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    internal static class RuntimeUiIconLibrary
    {
        private const string ResourceRoot = "Art/UI/Icons/";

        private static readonly Dictionary<string, Texture2D?> TextureCache = new();

        public static bool DrawIcon(Rect rect, string resourceName, bool enabled)
        {
            var texture = LoadTexture(resourceName);
            if (texture == null)
            {
                return false;
            }

            var previousColor = GUI.color;
            GUI.color = enabled ? Color.white : new Color(0.62f, 0.66f, 0.74f, 0.42f);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
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
    }
}
