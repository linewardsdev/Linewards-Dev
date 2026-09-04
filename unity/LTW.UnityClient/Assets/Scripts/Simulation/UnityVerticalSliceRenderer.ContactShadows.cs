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

        /// <summary>
        /// Footprint scale for a hovering creep's ground-anchor decal (see <see cref="UpdateContactShadow"/>'s
        /// finding #5 remark) - tighter than a grounded creep's old 0.86, so it reads as "this
        /// creep's own footprint" rather than the wider blob a real cast shadow would have thrown.
        /// </summary>
        private const float FlyerGroundAnchorFootprintScale = 0.62f;

        private readonly Dictionary<long, GameObject> activeContactShadows = new Dictionary<long, GameObject>();
        private readonly Dictionary<string, Vector2> unitFootprints = new Dictionary<string, Vector2>();
        private readonly Queue<GameObject> contactShadowPool = new Queue<GameObject>();

        private readonly HashSet<long> visibleContactShadowKeys = new HashSet<long>();

        /// <summary>
        /// Keys (see <see cref="ContactShadowKey"/>) whose cast-shadow mode has already been set
        /// for the creep instance currently holding that key.
        /// </summary>
        /// <remarks>
        /// Needed because of pooling, not just to save a per-frame walk. A creep without its own
        /// prefab (<c>GetPooledCreep</c>'s primitive fallback) is drawn from one shared pool
        /// regardless of role, so the exact GameObject a new grounded creep receives may be one a
        /// hovering creep left with its shadow switched off two spawns ago. Every key seen for the
        /// first time gets <see cref="SetCreepCastShadowMode"/> applied once, in whichever direction
        /// its own hover status calls for, which corrects that stale state rather than assuming a
        /// freshly-pooled instance starts clean.
        /// </remarks>
        private readonly HashSet<long> creepShadowModeConfigured = new HashSet<long>();

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
        ///
        /// Finding #5 (2026-09-01 render review): a creep was stacking up to three soft ground
        /// marks under it at once - its real cast shadow from the key light, this decal, and the
        /// SenderAccent owner-glow pool - and nothing on the board read as crisp. Towers keep this
        /// decal exactly as before (their cast shadow reads as nothing at this camera angle, per
        /// the remark above, so the decal is their only grounding cue). Creeps no longer do: a
        /// grounded creep now keeps only its real cast shadow, and a hovering one (turret_walker /
        /// flying / air - <see cref="IsHoveringCreep"/>) gets this decal back as its ONLY grounding
        /// cue, because <see cref="SetCreepCastShadowMode"/> turns its cast shadow off
        /// instead: the key light elongates a cast shadow further the higher its caster stands, and
        /// a flyer's hover height is exactly what was producing the "detached" streak on the ground
        /// that finding #5 flagged. Sizing that replacement decal to the flyer's own measured
        /// footprint (rather than the ground unit's usual scale) is the "clamp flyer shadow offset
        /// to their ground cell" half of the fix.
        ///
        /// <see cref="ContactShadowKey"/>'s low bit tells towers and creeps apart, which is what the
        /// branch below reads rather than adding a parameter - the call sites in the main file are
        /// out of scope for this pass.
        /// </remarks>
        private void UpdateContactShadow(long key, GameObject unit, string poolKey, Material material, float footprintScale)
        {
            if (unit == null)
            {
                return;
            }

            var isCreep = (key & 1L) == 1L;
            if (isCreep)
            {
                var creepId = CreepIdFor(key >> 1);
                var isHovering = IsHoveringCreep(creepId);
                if (creepShadowModeConfigured.Add(key))
                {
                    SetCreepCastShadowMode(unit, castsShadow: !isHovering);
                }

                if (!isHovering)
                {
                    // Grounded creep: the real cast shadow is the only ground mark it keeps.
                    return;
                }

                // Hovering creep: rebuild the decal at the creep's own tight footprint rather than
                // the caller's requested scale, which was tuned for a grounded creep's usual (wider,
                // half-hidden-under-the-body) blob.
                footprintScale = FlyerGroundAnchorFootprintScale;
            }

            if (material == null)
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
            // A despawned creep's shadow-mode bookkeeping is stale the moment its decal is gone (or,
            // for a grounded creep, was never created) - without this a long match would grow this
            // set by one entry per creep forever, and a pooled instance handed to a new creep would
            // wrongly be treated as "already configured" for a hover status that is no longer true.
            creepShadowModeConfigured.Remove(key);
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
            creepShadowModeConfigured.Clear();
        }

        /// <summary>
        /// Sets whether a creep's own body casts a shadow, once per creep.
        /// </summary>
        /// <remarks>
        /// See <see cref="UpdateContactShadow"/>'s finding #5 remark: the key light's shadow
        /// projects further as its caster stands higher off the ground plane, so turret_walker and
        /// flying/air creeps - lifted 0.32-0.72 above the board by <see cref="CreepRoleOffset"/> -
        /// cast a visibly elongated, detached-looking shadow. Disabling the projection outright
        /// (rather than trying to bias or clamp it per-renderer, which Unity does not expose per
        /// caster on a single directional light) and handing the creep back a tight ground-anchor
        /// decal via <see cref="UpdateContactShadow"/> is the bounded fix: one exception path for
        /// hovering creeps only, everything else - including every grounded creep's real cast
        /// shadow - unchanged.
        ///
        /// Both directions are real, not just "turn off for flyers": creeps without their own
        /// prefab share one pool regardless of role (see <see cref="creepShadowModeConfigured"/>'s
        /// remark), so a grounded creep can receive an instance a flyer left with its shadow off,
        /// and this has to put it back on rather than assume every pooled instance starts clean.
        ///
        /// Excludes the same overlay renderers <see cref="UnitFootprint"/> already excludes
        /// (health bar, role-readability markers) plus the SenderAccent ownership pool, none of
        /// which should have their shadow state flipped just because the body they sit under does -
        /// a health bar has never cast a shadow and should not start because it happened to sit on
        /// a creep whose hover status just changed pools.
        /// </remarks>
        private static void SetCreepCastShadowMode(GameObject unit, bool castsShadow)
        {
            var mode = castsShadow
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            var renderers = unit.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var candidate = renderers[index];
                if (candidate == null)
                {
                    continue;
                }

                var rendererName = candidate.gameObject.name;
                if (IsOverlayRenderer(rendererName) || rendererName.IndexOf("SenderAccent", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                candidate.shadowCastingMode = mode;
            }
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

        /// <summary>
        /// The one soft-shadow material creeps still use, and only for the hovering-creep ground
        /// anchor drawn by <see cref="UpdateContactShadow"/> - a grounded creep no longer gets a
        /// decal at all (finding #5, 2026-09-01 render review). Left with its original name and
        /// tuning rather than renamed, since it is the same blob a grounded creep used to get, just
        /// no longer called for one.
        /// </summary>
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
