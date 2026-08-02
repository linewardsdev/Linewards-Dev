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
    /// The per-lane pressure gauges in the lane gutters: meter, cap and label, drawn off one
    /// shared material through a MaterialPropertyBlock.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        private readonly Dictionary<int, GameObject> lanePressureMeters = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> lanePressureCaps = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, TextMesh> lanePressureLabels = new Dictionary<int, TextMesh>();
        private static MaterialPropertyBlock lanePressureMeterPropertyBlock;
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BackgroundColorPropertyId = Shader.PropertyToID("_BackgroundColor");
        private static readonly int FillPropertyId = Shader.PropertyToID("_Fill");

        private static readonly float LanePressureMeterLength = LaneLength * 0.54f;

        private void UpdateLanePressureIndicators(IReadOnlyList<int> pressureByLane)
        {
            for (var laneId = 1; laneId <= LaneCount; laneId++)
            {
                var pressure = pressureByLane[laneId];
                var meter = GetLanePressureMeter(laneId);
                var color = PressureColor(pressure);
                var fill = Mathf.Clamp(pressure, 0, 12) / 12f;
                // Fixed footprint: fill level reads through the LTW/Fill Bar shader's _Fill
                // threshold, not by rescaling the mesh, so the gauge never breaks batching by
                // changing geometry every tick the way a growing cube did.
                meter.transform.localScale = new Vector3(LanePressureMeterLength, 1f, 0.16f);
                // -90 (not +90) so the mesh's U=0 edge lands at the near/base end of the gauge:
                // the filled region then grows outward from the base as pressure rises, matching
                // the direction the old growing cube always animated in.
                meter.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                meter.transform.position = new Vector3(LaneOffset(laneId) + LaneWidth + 0.18f, -0.08f, 0.35f + LanePressureMeterLength * 0.5f);
                SetFillBarProperties(meter, color, fill);

                var cap = GetLanePressureCap(laneId);
                cap.SetActive(pressure >= 8);
                cap.transform.position = new Vector3(LaneOffset(laneId) + LaneWidth + 0.18f, 0.04f, 0.35f + LanePressureMeterLength);
                SetSharedColor(cap, LeakRed);

                var label = GetLanePressureLabel(laneId);
                label.text = PressureLabel(pressure);
                label.color = color;
            }
        }

        private GameObject GetLanePressureMeter(int laneId)
        {
            if (lanePressureMeters.TryGetValue(laneId, out var meter))
            {
                return meter;
            }

            meter = new GameObject($"Lane{laneId}PressureMeter");
            var meshFilter = meter.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = BoardRenderResources.FillBarMesh;
            var meshRenderer = meter.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = BoardRenderResources.FillBarMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            lanePressureMeters[laneId] = meter;
            laneDecorations.Add(meter);
            return meter;
        }

        // Opaque enough to read as a gauge housing against the dark board even at zero fill;
        // the earlier low-alpha value blended into the board so an empty gauge looked invisible
        // rather than like a gauge sitting at zero.
        private static readonly Color LanePressureMeterBackground = new Color(0.22f, 0.25f, 0.28f, 0.92f);

        private static void SetFillBarProperties(GameObject instance, Color fillColor, float fill)
        {
            var renderer = instance.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            lanePressureMeterPropertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(lanePressureMeterPropertyBlock);
            lanePressureMeterPropertyBlock.SetColor(BaseColorPropertyId, fillColor);
            lanePressureMeterPropertyBlock.SetColor(BackgroundColorPropertyId, LanePressureMeterBackground);
            lanePressureMeterPropertyBlock.SetFloat(FillPropertyId, fill);
            renderer.SetPropertyBlock(lanePressureMeterPropertyBlock);
        }

        private GameObject GetLanePressureCap(int laneId)
        {
            if (lanePressureCaps.TryGetValue(laneId, out var cap))
            {
                return cap;
            }

            cap = CreatePrimitive($"Lane{laneId}PressureCap", PrimitiveType.Sphere);
            DestroyPrimitiveCollider(cap);
            cap.transform.localScale = new Vector3(0.38f, 0.18f, 0.38f);
            lanePressureCaps[laneId] = cap;
            laneDecorations.Add(cap);
            return cap;
        }

        private TextMesh GetLanePressureLabel(int laneId)
        {
            if (lanePressureLabels.TryGetValue(laneId, out var label))
            {
                return label;
            }

            var labelObject = new GameObject($"Lane{laneId}PressureLabel");
            labelObject.transform.position = new Vector3(LaneOffset(laneId) + LaneWidth + 0.34f, 0.08f, LaneLength * 0.58f);
            labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 90f);
            labelObject.transform.localScale = Vector3.one * 0.03f;
            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 40;
            label.characterSize = 0.16f;
            lanePressureLabels[laneId] = label;
            laneDecorations.Add(labelObject);
            return label;
        }

        private static Color PressureColor(int pressure)
        {
            if (pressure >= 8)
            {
                return LeakRed;
            }

            if (pressure >= 4)
            {
                return SignalGold;
            }

            return MintSignal;
        }

        private static string PressureLabel(int pressure)
        {
            if (pressure == 0)
            {
                return "CALM";
            }

            return pressure >= 8 ? $"DANGER {pressure}" : $"PRESS {pressure}";
        }
    }
}
