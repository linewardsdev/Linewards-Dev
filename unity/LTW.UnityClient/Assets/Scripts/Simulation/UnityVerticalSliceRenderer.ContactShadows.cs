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
    /// Contact shadows: one pooled decal per visible unit, pinned to the board plane and sized
    /// from the unit's own measured footprint.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        /// <summary>
        /// How far the contact shadow decal is lifted off <see cref="BoardTopY"/>. Large enough to
        /// clear depth-fighting with the baked lane mesh, small enough to read as painted on.
        /// </summary>
        private const float ContactShadowLift = 0.006f;

        private readonly Dictionary<long, GameObject> activeContactShadows = new Dictionary<long, GameObject>();
        private readonly Dictionary<string, Vector2> unitFootprints = new Dictionary<string, Vector2>();
        private readonly Queue<GameObject> contactShadowPool = new Queue<GameObject>();

        private readonly HashSet<long> visibleContactShadowKeys = new HashSet<long>();

        private Material towerContactShadowMaterial;
        private Material creepContactShadowMaterial;

        /// <summary>
        /// Places the grounding decal for one unit.
        /// </summary>
        /// <remarks>
        /// The match camera is orthographic and looks almost straight down, so a cast shadow from
        /// the key light lands nearly underneath the unit and reads as nothing. A blob pinned to the
        /// board plane is the depth cue that actually survives this projection: it tells the eye
        /// where the unit touches the floor and how big its footprint is.
        ///
        /// The decal is pooled and keyed off the same entity id as the unit itself, so it follows
        /// the existing pooling exactly - reused instances get repositioned, and anything that
        /// leaves the snapshot returns its blob on the same frame the unit is released.
        /// </remarks>
        private void UpdateContactShadow(long key, GameObject unit, string poolKey, Material material, float footprintScale)
        {
            if (material == null || unit == null)
            {
                return;
            }

            visibleContactShadowKeys.Add(key);
            if (!activeContactShadows.TryGetValue(key, out var decal) || decal == null)
            {
                decal = GetPooledContactShadow();
                activeContactShadows[key] = decal;
            }

            var decalRenderer = decal.GetComponent<MeshRenderer>();
            if (decalRenderer.sharedMaterial != material)
            {
                decalRenderer.sharedMaterial = material;
            }

            var footprint = UnitFootprint(poolKey, unit);
            var unitPosition = unit.transform.position;
            decal.transform.position = new Vector3(unitPosition.x, BoardTopY + ContactShadowLift, unitPosition.z);
            decal.transform.localScale = new Vector3(footprint.x * footprintScale, 1f, footprint.y * footprintScale);
        }

        private GameObject GetPooledContactShadow()
        {
            if (contactShadowPool.Count > 0)
            {
                var pooled = contactShadowPool.Dequeue();
                pooled.SetActive(true);
                return pooled;
            }

            var decal = new GameObject("UnitContactShadow");
            decal.transform.SetParent(BoardGeometryRoot().transform, false);
            decal.AddComponent<MeshFilter>().sharedMesh = BoardRenderResources.ContactShadowMesh;
            var decalRenderer = decal.AddComponent<MeshRenderer>();
            decalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            decalRenderer.receiveShadows = false;
            decalRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            decalRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return decal;
        }

        private void ReleaseMissingContactShadows()
        {
            if (activeContactShadows.Count == 0)
            {
                return;
            }

            keysToReleaseScratch.Clear();
            foreach (var pair in activeContactShadows)
            {
                if (!visibleContactShadowKeys.Contains(pair.Key))
                {
                    keysToReleaseScratch.Add(pair.Key);
                }
            }

            for (var index = 0; index < keysToReleaseScratch.Count; index++)
            {
                ReleaseContactShadow(keysToReleaseScratch[index]);
            }
        }

        private void ReleaseContactShadow(long key)
        {
            if (activeContactShadows.TryGetValue(key, out var decal) && decal != null)
            {
                ReleaseToPool(decal, contactShadowPool);
            }

            activeContactShadows.Remove(key);
        }

        private void ReleaseAllContactShadows()
        {
            foreach (var pair in activeContactShadows)
            {
                if (pair.Value != null)
                {
                    ReleaseToPool(pair.Value, contactShadowPool);
                }
            }

            activeContactShadows.Clear();
        }

        /// <summary>
        /// The XZ extent of a unit's body at unit scale, measured once per pool key.
        /// </summary>
        /// <remarks>
        /// Measured from the prefab's own renderers rather than guessed from the role tables, so a
        /// blob matches the silhouette that is actually on screen. Health bars and role-readability
        /// overlays are excluded: they are wider than every creep they sit above and would inflate
        /// the footprint into a puddle. The result is normalised by the instance's scale at
        /// measurement time so the caller can re-apply the live scale each frame.
        /// </remarks>
        private Vector2 UnitFootprint(string poolKey, GameObject unit)
        {
            var scale = unit.transform.lossyScale;
            if (unitFootprints.TryGetValue(poolKey, out var cached))
            {
                return new Vector2(cached.x * Mathf.Abs(scale.x), cached.y * Mathf.Abs(scale.z));
            }

            var renderers = unit.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var bounds = new Bounds(unit.transform.position, Vector3.zero);
            for (var index = 0; index < renderers.Length; index++)
            {
                var candidate = renderers[index];
                if (candidate == null || candidate is SpriteRenderer || IsOverlayRenderer(candidate.gameObject.name))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = candidate.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(candidate.bounds);
            }

            var footprint = hasBounds
                ? new Vector2(
                    bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)))
                : Vector2.one;

            footprint = new Vector2(Mathf.Clamp(footprint.x, 0.4f, 3f), Mathf.Clamp(footprint.y, 0.4f, 3f));
            unitFootprints[poolKey] = footprint;
            return new Vector2(footprint.x * Mathf.Abs(scale.x), footprint.y * Mathf.Abs(scale.z));
        }

        private static bool IsOverlayRenderer(string rendererName) =>
            rendererName.StartsWith("Health", StringComparison.Ordinal) || rendererName.StartsWith("Role", StringComparison.Ordinal);

        private Material TowerContactShadowMaterial()
        {
            if (towerContactShadowMaterial == null)
            {
                towerContactShadowMaterial = BoardRenderResources.CreateContactShadowMaterial(
                    "LTW Tower Contact Shadow",
                    new Color(0.012f, 0.017f, 0.028f, 0.62f),
                    0.62f);
            }

            return towerContactShadowMaterial;
        }

        private Material CreepContactShadowMaterial()
        {
            if (creepContactShadowMaterial == null)
            {
                creepContactShadowMaterial = BoardRenderResources.CreateContactShadowMaterial(
                    "LTW Creep Contact Shadow",
                    new Color(0.014f, 0.018f, 0.03f, 0.5f),
                    0.7f);
            }

            return creepContactShadowMaterial;
        }

        /// <summary>
        /// Packs a unit's contact-shadow decal key. Towers and creeps are keyed from separate id
        /// spaces, so the low bit distinguishes them and stops a tower and a creep that happen to
        /// share an entity id fighting over one decal — the same job the old "t"/"c" string prefix
        /// did, without the concatenation it cost per unit per frame.
        /// </summary>
        private static long ContactShadowKey(long entityId, bool isTower) => (entityId << 1) | (isTower ? 0L : 1L);
    }
}
