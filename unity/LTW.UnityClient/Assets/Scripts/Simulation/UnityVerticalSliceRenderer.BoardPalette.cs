using System;
using System.Collections.Generic;
using LTW.Simulation.Combat;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// The board's colour palette: one function per authored surface, plus the gamma lift and
    /// per-tile variation every one of them goes through.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        /// <summary>
        /// Lifts a board surface colour that was authored against the gamma response.
        /// </summary>
        /// <remarks>
        /// The board palette was tuned when the project rendered in gamma, where an albedo near
        /// 0.05 still read as visible stonework. Linear rendering resolves those same values far
        /// darker and the board collapses into an undifferentiated dark field. Treating the
        /// authored number as the intended linear reflectance and converting it back to the sRGB
        /// albedo that produces it restores the original intent, rather than re-tuning a dozen
        /// scattered constants by eye.
        /// </remarks>
        /// <remarks>
        /// A full inverse-gamma conversion is the mathematically exact compensation, but it
        /// overshoots the art direction: it lifts a 0.052 cell to roughly 0.25 and the board reads
        /// as pale concrete rather than night stone. This blends part of the way, which restores
        /// legible tonal separation while keeping the dark palette. Raise to brighten the board,
        /// lower to darken it; 0 is the original gamma-era value and 1 the exact conversion.
        /// </remarks>
        private const float BoardSurfaceLift = 0.35f;

        private static Color BoardSurface(Color authored) => new Color(
            Mathf.Lerp(authored.r, Mathf.LinearToGammaSpace(authored.r), BoardSurfaceLift),
            Mathf.Lerp(authored.g, Mathf.LinearToGammaSpace(authored.g), BoardSurfaceLift),
            Mathf.Lerp(authored.b, Mathf.LinearToGammaSpace(authored.b), BoardSurfaceLift),
            authored.a);

        private static Color CellColor(int laneId, int x, int y)
        {
            if (x == CenterColumn && y == 0)
            {
                return BoardSurface(new Color(0.08f, 0.18f, 0.19f));
            }

            if (x == CenterColumn && y == LaneLength - 1)
            {
                return BoardSurface(new Color(0.16f, 0.055f, 0.052f));
            }

            if (x == CenterColumn)
            {
                var routeVariation = TileVariation(laneId, x, y) * 0.016f;
                return laneId == 1
                    ? BoardSurface(new Color(0.07f + routeVariation, 0.145f + routeVariation, 0.225f + routeVariation))
                    : BoardSurface(new Color(0.044f + routeVariation * 0.7f, 0.085f + routeVariation * 0.7f, 0.145f + routeVariation * 0.7f));
            }

            var checker = (x + y + laneId) % 2 == 0 ? 0.012f : 0f;
            var laneTint = laneId == 1 ? 0.014f : 0f;
            var buildColumn = x < CenterColumn ? 0.004f : 0.01f;
            var stoneVariation = TileVariation(laneId, x, y) * 0.014f;
            var edgeLift = x == 0 || x == LaneWidth - 1 ? 0.008f : 0f;
            return BoardSurface(new Color(0.052f + checker + laneTint + buildColumn + stoneVariation + edgeLift, 0.058f + checker + laneTint + stoneVariation * 0.82f + edgeLift, 0.072f + checker + laneTint + stoneVariation * 0.55f + edgeLift));
        }

        private static Color LaneBackplateColor(int laneId)
        {
            return laneId == 1 ? BoardSurface(new Color(0.025f, 0.045f, 0.072f)) : BoardSurface(new Color(0.018f, 0.026f, 0.046f));
        }

        private static Color LaneGutterColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.018f, 0.034f, 0.054f)) : BoardSurface(new Color(0.014f, 0.018f, 0.032f));

        private static Color LaneAnchorColor(Color accent, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.34f : 0.18f;
            return BoardSurface(new Color(accent.r * strength, accent.g * strength, accent.b * strength));
        }

        private static Color LaneTickColor(Color accent, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.5f : 0.26f;
            return new Color(accent.r * strength, accent.g * strength, accent.b * strength);
        }

        private static Color BuildZoneColor(Color tint, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.08f : 0.045f;
            return BoardSurface(new Color(0.044f + tint.r * strength, 0.052f + tint.g * strength, 0.068f + tint.b * strength));
        }

        private static Color RouteBandColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.074f, 0.16f, 0.25f)) : BoardSurface(new Color(0.044f, 0.09f, 0.16f));

        private static Color RouteGuideColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.16f, 0.33f, 0.52f)) : BoardSurface(new Color(0.085f, 0.18f, 0.32f));

        private static Color RouteInlayColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.085f, 0.22f, 0.36f)) : BoardSurface(new Color(0.044f, 0.12f, 0.22f));

        private static Color RouteRecessColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.018f, 0.032f, 0.046f)) : BoardSurface(new Color(0.009f, 0.018f, 0.03f));

        private static Color RouteRibColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.18f, 0.32f, 0.43f)) : BoardSurface(new Color(0.075f, 0.14f, 0.22f));

        private static Color BuildBandEdgeColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.12f, 0.14f, 0.15f)) : BoardSurface(new Color(0.064f, 0.074f, 0.086f));

        private static Color EndpointApproachPlateColor(bool isSpawn, bool isPlayerLane)
        {
            if (isSpawn)
            {
                return BoardSurface(isPlayerLane ? new Color(0.075f, 0.13f, 0.13f) : new Color(0.036f, 0.068f, 0.068f));
            }

            return BoardSurface(isPlayerLane ? new Color(0.12f, 0.045f, 0.04f) : new Color(0.062f, 0.022f, 0.02f));
        }

        /// <summary>
        /// Exposure for the gate sprite plates, so they sit in the board's value range.
        /// </summary>
        /// <remarks>
        /// Reported from iPad play: the spawn and leak gates "kind of look like 2D sprites in a 3D
        /// world". They are exactly that — a SpriteRenderer on a flat plate — and the giveaway was
        /// brightness rather than geometry. A SpriteRenderer uses Unity's default UNLIT sprite
        /// material, so it takes none of the scene's lighting, and it was drawn at Color.white:
        /// full brightness against a board surface authored at 0.075-0.13
        /// (<see cref="EndpointApproachPlateColor"/>). Roughly eight times the value of everything
        /// touching it, and lit surfaces darken toward their edges while this stayed flat, which is
        /// what reads as a decal laid on top rather than a thing on the board.
        ///
        /// A neutral grey rather than a hue, deliberately: the artwork carries its own colour and a
        /// tinted multiply would shift it. This only lowers exposure, so the gate keeps its palette
        /// and stops out-shining the board.
        ///
        /// Non-player lanes take the same relative drop every other element on a non-player lane
        /// takes, so the gates recede with the lane they belong to instead of staying the brightest
        /// thing on a lane the player is not looking at.
        /// </remarks>
        private static Color EndpointSpriteTint(bool isPlayerLane)
        {
            var exposure = isPlayerLane ? 0.62f : 0.4f;
            return new Color(exposure, exposure, exposure, 1f);
        }

        private static Color EndpointWashColor(Color color, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.42f : 0.25f;
            return new Color(color.r * strength, color.g * strength, color.b * strength);
        }

        private static Color RouteWearColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.055f, 0.075f, 0.088f)) : BoardSurface(new Color(0.034f, 0.048f, 0.062f));

        private static Color BuildBandSeamColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.018f, 0.026f, 0.034f)) : BoardSurface(new Color(0.012f, 0.018f, 0.026f));

        private static Color TileCrackColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.012f, 0.016f, 0.022f)) : BoardSurface(new Color(0.008f, 0.012f, 0.018f));

        private static Color TileEdgeHighlightColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.13f, 0.15f, 0.16f)) : BoardSurface(new Color(0.074f, 0.086f, 0.1f));

        private static Color BoardPlateInsetColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.058f, 0.068f, 0.078f)) : BoardSurface(new Color(0.034f, 0.042f, 0.052f));

        private static Color BoardPlateLightBevelColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.16f, 0.17f, 0.17f)) : BoardSurface(new Color(0.086f, 0.096f, 0.106f));

        private static Color BoardPlateDarkBevelColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.012f, 0.018f, 0.026f)) : BoardSurface(new Color(0.006f, 0.01f, 0.016f));

        private static Color BoardContactShadowColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.008f, 0.014f, 0.02f)) : BoardSurface(new Color(0.004f, 0.008f, 0.014f));

        private static Color LaneFrameTrimColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.065f, 0.078f, 0.088f)) : BoardSurface(new Color(0.034f, 0.042f, 0.052f));

        private static Color LaneFrameHighlightColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.18f, 0.19f, 0.18f)) : BoardSurface(new Color(0.086f, 0.094f, 0.1f));

        private static Color LaneFrameAccentColor(Color accent, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.38f : 0.18f;
            return BoardSurface(new Color(0.035f + accent.r * strength, 0.04f + accent.g * strength, 0.045f + accent.b * strength));
        }

        private static Color EndpointPlateSignalColor(Color color, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.72f : 0.45f;
            return new Color(color.r * strength, color.g * strength, color.b * strength);
        }

        private static Color RouteTriangleColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.33f, 0.58f, 0.78f)) : BoardSurface(new Color(0.14f, 0.27f, 0.43f));

        private static Color EndpointBaseColor(Color color, bool isPlayerLane, bool isSpawn)
        {
            var strength = isPlayerLane ? 0.18f : 0.11f;
            var baseTone = isSpawn ? new Color(0.048f, 0.072f, 0.084f) : new Color(0.055f, 0.038f, 0.038f);
            return BoardSurface(new Color(baseTone.r + color.r * strength, baseTone.g + color.g * strength, baseTone.b + color.b * strength));
        }

        private static Color EndpointStoneRingColor(bool isSpawn, bool isPlayerLane)
        {
            var laneLift = isPlayerLane ? 0.018f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.145f + laneLift, 0.162f + laneLift, 0.172f + laneLift)
                : new Color(0.172f + laneLift, 0.092f + laneLift * 0.4f, 0.086f + laneLift * 0.4f));
        }

        private static Color EndpointOuterRingColor(bool isSpawn, bool isPlayerLane)
        {
            var laneLift = isPlayerLane ? 0.022f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.088f + laneLift, 0.108f + laneLift, 0.122f + laneLift)
                : new Color(0.115f + laneLift, 0.056f + laneLift * 0.35f, 0.054f + laneLift * 0.3f));
        }

        private static Color EndpointInnerPlateColor(bool isSpawn, bool isPlayerLane)
        {
            var laneLift = isPlayerLane ? 0.02f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.072f + laneLift, 0.112f + laneLift, 0.124f + laneLift)
                : new Color(0.092f + laneLift, 0.038f + laneLift * 0.35f, 0.034f + laneLift * 0.35f));
        }

        private static Color EndpointDeepRecessColor(bool isSpawn, bool isPlayerLane)
        {
            var lift = isPlayerLane ? 0.012f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.018f + lift, 0.032f + lift, 0.036f + lift)
                : new Color(0.026f + lift, 0.006f + lift * 0.25f, 0.006f + lift * 0.2f));
        }

        private static Color EndpointStoneHighlightColor(bool isSpawn, bool isPlayerLane)
        {
            var lift = isPlayerLane ? 0.035f : 0.012f;
            return BoardSurface(isSpawn
                ? new Color(0.22f + lift, 0.235f + lift, 0.235f + lift)
                : new Color(0.24f + lift, 0.12f + lift * 0.4f, 0.108f + lift * 0.35f));
        }

        private static Color EndpointRimHighlightColor(bool isSpawn, bool isPlayerLane)
        {
            var lift = isPlayerLane ? 0.028f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.17f + lift, 0.19f + lift, 0.19f + lift)
                : new Color(0.18f + lift, 0.072f + lift * 0.35f, 0.066f + lift * 0.3f));
        }

        private static Color EndpointPortalColor(bool isPlayerLane) =>
            isPlayerLane ? new Color(0.26f, 0.9f, 0.78f) : new Color(0.1f, 0.44f, 0.39f);

        private static Color EndpointPortalBrightColor(bool isPlayerLane) =>
            isPlayerLane ? new Color(0.55f, 1f, 0.9f) : new Color(0.2f, 0.62f, 0.54f);

        private static Color EndpointDrainGlowColor(bool isPlayerLane) =>
            isPlayerLane ? new Color(0.95f, 0.14f, 0.1f) : new Color(0.46f, 0.045f, 0.04f);

        private static Color EndpointLeakHotspotColor(bool isPlayerLane) =>
            isPlayerLane ? new Color(1f, 0.25f, 0.16f) : new Color(0.54f, 0.07f, 0.055f);

        private static float TileVariation(int laneId, int x, int y)
        {
            var hash = laneId * 73856093 ^ x * 19349663 ^ y * 83492791;
            hash = (hash ^ (hash >> 13)) * 1274126177;
            return ((hash & 0x7fffffff) % 1000) / 1000f;
        }
    }
}
