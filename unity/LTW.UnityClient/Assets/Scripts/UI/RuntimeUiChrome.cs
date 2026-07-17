#nullable enable

using UnityEngine;

namespace LTW.UnityClient.UI
{
    internal enum CommandCardState
    {
        Normal,
        Selected,
        Disabled,
        Error
    }

    internal static class RuntimeUiChrome
    {
        private static readonly Color CardBack = new(0.038f, 0.052f, 0.078f, 0.96f);
        private static readonly Color CardInset = new(0.075f, 0.093f, 0.122f, 0.94f);
        private static readonly Color SlateEdge = new(0.34f, 0.36f, 0.38f, 0.92f);
        private static readonly Color DeepEdge = new(0.012f, 0.016f, 0.022f, 0.92f);
        private static readonly Color DisabledEdge = new(0.24f, 0.25f, 0.28f, 0.84f);
        private static readonly Color ErrorRed = new(0.94f, 0.24f, 0.18f, 1f);

        public static bool DrawCommandCard(Rect rect, Color accent, CommandCardState state, float scale)
        {
            DrawCommandCardChrome(rect, accent, state, scale);

            var previousEnabled = GUI.enabled;
            GUI.enabled = state is not CommandCardState.Disabled and not CommandCardState.Error;
            var pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = previousEnabled;
            return pressed;
        }

        public static Rect CommandCardIconRect(Rect rect, float scale)
        {
            var size = Mathf.Min(48f * scale, rect.height - 30f * scale);
            return new Rect(rect.x + (rect.width - size) * 0.5f, rect.y + 10f * scale, size, size);
        }

        public static Rect CommandCardLabelRect(Rect rect, float scale)
        {
            return new Rect(rect.x + 7f * scale, rect.yMax - 38f * scale, rect.width - 14f * scale, 18f * scale);
        }

        public static Rect CommandCardMetaRect(Rect rect, float scale)
        {
            return new Rect(rect.x + 7f * scale, rect.yMax - 20f * scale, rect.width - 14f * scale, 15f * scale);
        }

        public static bool DrawControlButton(Rect rect, string label, Color accent, bool active, float scale, GUIStyle labelStyle)
        {
            DrawControlChrome(rect, accent, active, scale);

            var previousTextColor = labelStyle.normal.textColor;
            labelStyle.normal.textColor = active ? new Color(0.02f, 0.035f, 0.052f, 1f) : new Color(accent.r, accent.g, accent.b, 0.92f);
            GUI.Label(rect, label, labelStyle);
            labelStyle.normal.textColor = previousTextColor;

            var currentEvent = Event.current;
            if (currentEvent.type != EventType.MouseUp || !rect.Contains(currentEvent.mousePosition))
            {
                return false;
            }

            currentEvent.Use();
            return true;
        }

        private static void DrawCommandCardChrome(Rect rect, Color accent, CommandCardState state, float scale)
        {
            var stateAccent = StateAccent(accent, state);
            var edge = state == CommandCardState.Disabled ? DisabledEdge : SlateEdge;
            var fill = state == CommandCardState.Disabled
                ? new Color(0.07f, 0.078f, 0.094f, 0.9f)
                : Tint(CardBack, stateAccent, state == CommandCardState.Selected ? 0.16f : 0.07f);

            Fill(rect, DeepEdge);
            Fill(Shrink(rect, 2f * scale), edge);
            Fill(Shrink(rect, 4f * scale), fill);

            var inset = new Rect(rect.x + 7f * scale, rect.y + 7f * scale, rect.width - 14f * scale, rect.height - 14f * scale);
            Fill(inset, CardInset);
            DrawMetalRails(rect, edge, scale);
            DrawCornerHardware(rect, stateAccent, scale);

            var iconRect = CommandCardIconRect(rect, scale);
            var iconWell = Shrink(iconRect, -5f * scale);
            Fill(iconWell, new Color(0.012f, 0.018f, 0.026f, 0.82f));
            DrawOutline(iconWell, new Color(stateAccent.r, stateAccent.g, stateAccent.b, 0.58f), Mathf.Max(1f, 1f * scale));

            var costStrip = new Rect(rect.x + rect.width * 0.18f, rect.yMax - 8f * scale, rect.width * 0.64f, 3f * scale);
            Fill(costStrip, new Color(stateAccent.r, stateAccent.g, stateAccent.b, state == CommandCardState.Disabled ? 0.42f : 0.92f));

            if (state == CommandCardState.Selected)
            {
                DrawOutline(Shrink(rect, 1f * scale), new Color(stateAccent.r, stateAccent.g, stateAccent.b, 0.92f), Mathf.Max(2f, 2f * scale));
                Fill(new Rect(rect.x + 11f * scale, rect.y + 4f * scale, rect.width - 22f * scale, 2f * scale), stateAccent);
                Fill(new Rect(rect.x + rect.width * 0.18f, rect.y + 9f * scale, rect.width * 0.64f, 5f * scale), new Color(stateAccent.r, stateAccent.g, stateAccent.b, 0.9f));
                Fill(new Rect(rect.x + 8f * scale, rect.y + rect.height * 0.38f, 5f * scale, rect.height * 0.24f), stateAccent);
                Fill(new Rect(rect.xMax - 13f * scale, rect.y + rect.height * 0.38f, 5f * scale, rect.height * 0.24f), stateAccent);
                Fill(Shrink(iconWell, 5f * scale), new Color(stateAccent.r, stateAccent.g, stateAccent.b, 0.16f));
            }
            else if (state == CommandCardState.Error)
            {
                Fill(new Rect(rect.x + 10f * scale, rect.yMax - 7f * scale, rect.width - 20f * scale, 3f * scale), ErrorRed);
            }
        }

        private static void DrawMetalRails(Rect rect, Color edge, float scale)
        {
            var rail = Mathf.Max(2f, 2f * scale);
            Fill(new Rect(rect.x + 12f * scale, rect.y + 4f * scale, rect.width - 24f * scale, rail), edge);
            Fill(new Rect(rect.x + 12f * scale, rect.yMax - 6f * scale, rect.width - 24f * scale, rail), edge);
            Fill(new Rect(rect.x + 4f * scale, rect.y + 12f * scale, rail, rect.height - 24f * scale), edge);
            Fill(new Rect(rect.xMax - 6f * scale, rect.y + 12f * scale, rail, rect.height - 24f * scale), edge);
        }

        private static void DrawCornerHardware(Rect rect, Color accent, float scale)
        {
            var corner = 9f * scale;
            var line = Mathf.Max(2f, 2f * scale);
            var hardware = new Color(accent.r, accent.g, accent.b, 0.86f);

            Fill(new Rect(rect.x + 5f * scale, rect.y + 5f * scale, corner, line), hardware);
            Fill(new Rect(rect.x + 5f * scale, rect.y + 5f * scale, line, corner), hardware);
            Fill(new Rect(rect.xMax - 5f * scale - corner, rect.y + 5f * scale, corner, line), hardware);
            Fill(new Rect(rect.xMax - 7f * scale, rect.y + 5f * scale, line, corner), hardware);
            Fill(new Rect(rect.x + 5f * scale, rect.yMax - 7f * scale, corner, line), hardware);
            Fill(new Rect(rect.x + 5f * scale, rect.yMax - 5f * scale - corner, line, corner), hardware);
            Fill(new Rect(rect.xMax - 5f * scale - corner, rect.yMax - 7f * scale, corner, line), hardware);
            Fill(new Rect(rect.xMax - 7f * scale, rect.yMax - 5f * scale - corner, line, corner), hardware);
        }

        private static void DrawControlChrome(Rect rect, Color accent, bool active, float scale)
        {
            var outer = active ? accent : SlateEdge;
            var face = active ? new Color(accent.r, accent.g, accent.b, 0.86f) : Tint(CardBack, accent, 0.12f);
            var inner = active ? new Color(0.86f, 0.97f, 1f, 0.58f) : new Color(accent.r, accent.g, accent.b, 0.36f);
            var pad = Mathf.Max(2f * scale, 2f);
            var ring = Mathf.Max(3f * scale, 2f);

            Fill(rect, DeepEdge);
            Fill(Shrink(rect, pad), outer);
            Fill(Shrink(rect, pad + ring), face);

            var markWidth = Mathf.Max(3f * scale, 2f);
            var railX = rect.x + rect.width * 0.22f;
            Fill(new Rect(railX, rect.y + rect.height * 0.24f, markWidth, rect.height * 0.52f), inner);
            Fill(new Rect(railX - markWidth * 1.2f, rect.y + rect.height * 0.28f, markWidth, markWidth), outer);
            Fill(new Rect(railX - markWidth * 1.2f, rect.y + rect.height * 0.5f - markWidth * 0.5f, markWidth, markWidth), outer);
            Fill(new Rect(railX - markWidth * 1.2f, rect.yMax - rect.height * 0.28f - markWidth, markWidth, markWidth), outer);
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void Fill(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static Rect Shrink(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
        }

        private static Color StateAccent(Color accent, CommandCardState state)
        {
            return state switch
            {
                CommandCardState.Disabled => new Color(0.42f, 0.45f, 0.5f, 0.88f),
                CommandCardState.Error => ErrorRed,
                _ => accent
            };
        }

        private static Color Tint(Color baseColor, Color tint, float amount)
        {
            return new Color(
                baseColor.r + tint.r * amount,
                baseColor.g + tint.g * amount,
                baseColor.b + tint.b * amount,
                baseColor.a);
        }
    }
}
