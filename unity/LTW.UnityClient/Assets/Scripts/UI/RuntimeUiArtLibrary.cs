#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    internal static class RuntimeUiArtLibrary
    {
        private const string ChromeRoot = "Art/UI/Chrome/";
        private static readonly Dictionary<string, Texture2D?> TextureCache = new();

        public static bool DrawChromeTexture(Rect rect, string resourceName, Color tint, ScaleMode scaleMode = ScaleMode.StretchToFill)
        {
            var texture = LoadChromeTexture(resourceName);
            if (texture == null)
            {
                return false;
            }

            var previousColor = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, texture, scaleMode, true);
            GUI.color = previousColor;
            return true;
        }

        private static Texture2D? LoadChromeTexture(string resourceName)
        {
            if (TextureCache.TryGetValue(resourceName, out var cached))
            {
                return cached;
            }

            var resourcePath = ChromeRoot + resourceName;
            var texture = Resources.Load<Texture2D>(resourcePath);
#if UNITY_EDITOR
            texture ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/" + resourcePath + ".png");
#endif
            if (texture != null)
            {
                TextureCache[resourceName] = texture;
            }

            return texture;
        }
    }
}
