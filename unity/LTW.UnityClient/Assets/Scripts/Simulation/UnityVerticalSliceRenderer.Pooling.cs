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
    /// Object pooling for towers and creeps: acquire, release, the per-prefab pools behind the
    /// two shared primitive pools, and the release sweeps that retire what a snapshot stopped naming.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        private const string PrimitiveTowerPoolKey = "primitive-tower";
        private const string PrimitiveCreepPoolKey = "primitive-creep";

        private readonly Dictionary<long, string> activeTowerPoolKeys = new Dictionary<long, string>();
        private readonly Dictionary<long, string> activeCreepPoolKeys = new Dictionary<long, string>();

        private readonly Queue<GameObject> towerPool = new Queue<GameObject>();
        private readonly Queue<GameObject> creepPool = new Queue<GameObject>();
        private readonly Dictionary<string, Queue<GameObject>> towerPrefabPools = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, Queue<GameObject>> creepPrefabPools = new Dictionary<string, Queue<GameObject>>();

        private GameObject GetOrCreateTower(long key, TowerVisualProfile visualProfile)
        {
            var poolKey = TowerPoolKey(visualProfile);
            if (activeTowers.TryGetValue(key, out var instance))
            {
                if (activeTowerPoolKeys.TryGetValue(key, out var activePoolKey) && activePoolKey == poolKey)
                {
                    return instance;
                }

                ReleaseTowerToPool(key, instance);
                activeTowers.Remove(key);
            }

            instance = GetPooledTower(poolKey, visualProfile);
            activeTowers[key] = instance;
            activeTowerPoolKeys[key] = poolKey;
            return instance;
        }

        private GameObject GetPooledTower(string poolKey, TowerVisualProfile visualProfile)
        {
            if (visualProfile == null || visualProfile.Prefab == null)
            {
                return GetPooled(towerPool, "WardTower", PrimitiveType.Cylinder);
            }

            var pool = GetTowerPrefabPool(poolKey);
            var instance = pool.Count > 0 ? pool.Dequeue() : Instantiate(visualProfile.Prefab);
            instance.name = visualProfile.Prefab.name;
            instance.SetActive(true);
            return instance;
        }

        private Queue<GameObject> GetTowerPrefabPool(string poolKey)
        {
            if (towerPrefabPools.TryGetValue(poolKey, out var pool))
            {
                return pool;
            }

            pool = new Queue<GameObject>();
            towerPrefabPools[poolKey] = pool;
            return pool;
        }

        private int PooledTowerPrefabCount()
        {
            var count = 0;
            foreach (var pair in towerPrefabPools)
            {
                count += pair.Value.Count;
            }

            return count;
        }

        private void ReleaseMissingTowers()
        {
            keysToReleaseScratch.Clear();
            foreach (var pair in activeTowers)
            {
                if (!visibleKeys.Contains(pair.Key))
                {
                    keysToReleaseScratch.Add(pair.Key);
                }
            }

            foreach (var key in keysToReleaseScratch)
            {
                ReleaseTowerToPool(key, activeTowers[key]);
                activeTowers.Remove(key);
                activeTowerPoolKeys.Remove(key);
                towerLastFiredAt.Remove(key);
                towerAimTarget.Remove(key);
                towerBarrelAngle.Remove(key);
                towerBarrelState.Remove(key);
                towerAimYaw.Remove(key);
                towerSpinPartState.Remove(key);
                towerMotionParts.Remove(key);
                // Sold or destroyed towers must not leave their mechanic decal on the board.
                ReleaseTowerMechanicMarker(key);
                ReleaseSporeFog(key);
                // Covers a removed tower that was itself the serviced end of a tether. If it was
                // instead the DRONE end, the tower on the other end self-heals on its own next
                // UpdateTowerServicingTether call (it re-scans for an adjacent drone every frame,
                // same as Grovebond's ring already does when an adjacent Grove tower is sold) —
                // no special case needed for that direction.
                ReleaseTowerServicingTether(key);
            }
        }

        private void ReleaseTowerToPool(long key, GameObject instance)
        {
            if (activeTowerPoolKeys.TryGetValue(key, out var poolKey) && poolKey != PrimitiveTowerPoolKey)
            {
                ReleaseToPool(instance, GetTowerPrefabPool(poolKey));
                return;
            }

            ReleaseToPool(instance, towerPool);
        }

        private static string TowerPoolKey(TowerVisualProfile visualProfile)
        {
            if (visualProfile == null || visualProfile.Prefab == null)
            {
                return PrimitiveTowerPoolKey;
            }

            return string.IsNullOrWhiteSpace(visualProfile.TowerId)
                ? visualProfile.Prefab.name
                : visualProfile.TowerId;
        }

        private GameObject GetOrCreateCreep(long key, CreepVisualProfile visualProfile)
        {
            var poolKey = CreepPoolKey(visualProfile);
            if (activeCreeps.TryGetValue(key, out var instance))
            {
                if (activeCreepPoolKeys.TryGetValue(key, out var activePoolKey) && activePoolKey == poolKey)
                {
                    return instance;
                }

                ReleaseCreepToPool(key, instance);
                activeCreeps.Remove(key);
            }

            instance = GetPooledCreep(poolKey, visualProfile);
            activeCreeps[key] = instance;
            activeCreepPoolKeys[key] = poolKey;
            return instance;
        }

        private GameObject GetPooledCreep(string poolKey, CreepVisualProfile visualProfile)
        {
            if (visualProfile == null || visualProfile.Prefab == null)
            {
                return GetPooled(creepPool, "PressureCreep", PrimitiveType.Sphere);
            }

            var pool = GetCreepPrefabPool(poolKey);
            var instance = pool.Count > 0 ? pool.Dequeue() : Instantiate(visualProfile.Prefab);
            instance.name = visualProfile.Prefab.name;
            instance.SetActive(true);
            return instance;
        }

        private Queue<GameObject> GetCreepPrefabPool(string poolKey)
        {
            if (creepPrefabPools.TryGetValue(poolKey, out var pool))
            {
                return pool;
            }

            pool = new Queue<GameObject>();
            creepPrefabPools[poolKey] = pool;
            return pool;
        }

        private int PooledCreepPrefabCount()
        {
            var count = 0;
            foreach (var pair in creepPrefabPools)
            {
                count += pair.Value.Count;
            }

            return count;
        }

        /// <summary>
        /// How long a killed/leaked creep's own instance lingers — shrinking, sinking and
        /// darkening, see <see cref="UpdateDyingCreeps"/> — before <see cref="ReleaseMissingCreeps"/>
        /// is allowed to actually pool it.
        /// </summary>
        /// <remarks>
        /// Finding #8 (2026-09-01 render review): a creep's model used to be pooled the instant
        /// its key dropped out of the snapshot's <see cref="visibleKeys"/> — often the very same
        /// frame the death cue was still drawing — so the cue was decorating an already-empty cell.
        ///
        /// Wave 5 (R4, re-judged 2026-09-02): Wave 3's 0.36s hold was not visible in any frame of
        /// a 0.1s capture. Nothing in the release path cuts it short — the only early exits are a
        /// match reset (HasStarted false) and a presentation Disabled reset, neither of which the
        /// capture runner takes mid-combat — but the capture renders at whatever rate the batch
        /// editor manages under a 1536x2048 ReadPixels per frame, so Update() and this hold may
        /// see only one or two frames in 0.36s. Two changes for that: 0.4s, the top of the asked
        /// range, and an ease-in curve that keeps the creep near full size for the first third
        /// while the darkening says "dead", so any frame in the hold is recognisably a corpse
        /// rather than a slightly smaller creep.
        /// </remarks>
        private const float CreepDyingDuration = 0.4f;

        /// <summary>Scale a dying creep collapses to: small enough to be gone, large enough that the last frame still has a shape.</summary>
        private const float CreepDyingEndScale = 0.15f;

        /// <summary>How far every dying creep sinks into the board by the end of the hold.</summary>
        private const float CreepDyingSinkDepth = 0.3f;

        /// <summary>How high HeavyShatter/ShardScatter pop before they start sinking. See <see cref="UpdateDyingCreeps"/>.</summary>
        private const float CreepDyingPopHeight = 0.22f;

        /// <summary>
        /// Per-entity dying state, one map per fact — matching how <see cref="creepHitFlashUntil"/>
        /// and its neighbours in UnityVerticalSliceRenderer.cs already track transient per-creep
        /// state, rather than a new struct-keyed table.
        /// </summary>
        private readonly Dictionary<long, float> creepDyingStartedAt = new Dictionary<long, float>();
        private readonly Dictionary<long, float> creepDyingUntil = new Dictionary<long, float>();
        private readonly Dictionary<long, Vector3> creepDyingBasePosition = new Dictionary<long, Vector3>();
        private readonly Dictionary<long, Vector3> creepDyingBaseScale = new Dictionary<long, Vector3>();
        private readonly Dictionary<long, CreepDeathCueStyle> creepDyingStyle = new Dictionary<long, CreepDeathCueStyle>();

        /// <summary>
        /// The renderers a dying creep darkens and the colours they had when it died — the same
        /// set <c>ApplyCreepColor</c> tints for the body (the profile's BodyRendererPath, or the
        /// root renderer for a primitive creep), so the darkening lands where the body colour does
        /// and the next creep to take this instance from the pool restores exactly that set.
        /// </summary>
        /// <remarks>
        /// The lists are recycled through <see cref="dyingRendererListPool"/> /
        /// <see cref="dyingColorListPool"/> so a death costs no allocation once the pools have
        /// warmed; the per-frame tint is a SetColor on a material that was already instanced by
        /// the body-colour path.
        /// </remarks>
        private readonly Dictionary<long, List<Renderer>> creepDyingRenderers = new Dictionary<long, List<Renderer>>();
        private readonly Dictionary<long, List<Color>> creepDyingBaseColors = new Dictionary<long, List<Color>>();
        private readonly Stack<List<Renderer>> dyingRendererListPool = new Stack<List<Renderer>>();
        private readonly Stack<List<Color>> dyingColorListPool = new Stack<List<Color>>();

        /// <summary>Scratch list for <see cref="UpdateDyingCreeps"/>, reused so a normal frame with no deaths allocates nothing.</summary>
        private readonly List<long> dyingCreepKeysScratch = new List<long>();

        /// <summary>
        /// Starts a creep's dying send-off the moment its key drops out of the snapshot, rather
        /// than waiting for RenderEvents to process whatever CreepKilledEvent/LeakEvent explains
        /// it away.
        /// </summary>
        /// <remarks>
        /// This has to be keyed off the snapshot dropping the entity, not off the event, even
        /// though the event is the more obvious hook: UnityVerticalSliceRenderer.cs's Update()
        /// calls <c>RenderSnapshot</c> (which reaches <see cref="ReleaseMissingCreeps"/>) and
        /// THEN <c>RenderEvents</c> in that order, every frame, and
        /// <c>UnitySimulationDriver.RefreshSnapshot</c> republishes <c>LatestSnapshot</c> and
        /// drains <c>LatestEvents</c> together — so the snapshot that no longer contains a killed
        /// creep and the CreepKilledEvent that explains why arrive in the very same frame, with
        /// the snapshot processed first. A hook placed in SpawnCreepDeathCue/
        /// SpawnCreepLeakRoleCue (both in Cues.cs, called from RenderEvents) would run AFTER this
        /// method had already released the creep on the very same frame — which is, precisely,
        /// finding #8's bug. Starting the send-off here instead means there is no event to wait
        /// for: any creep whose key silently drops out of <see cref="visibleKeys"/> was either
        /// killed or leaked (a creep never leaves a lane any other way), so this is
        /// unconditionally correct without needing to know which.
        /// </remarks>
        private void BeginCreepDying(long key, GameObject creepObject)
        {
            var creepId = lastKnownCreepIds.TryGetValue(key, out var id) ? id : string.Empty;
            var visualProfile = creepVisualLibrary != null ? creepVisualLibrary.FindProfile(creepId) : null;

            creepDyingStartedAt[key] = Time.time;
            creepDyingUntil[key] = Time.time + CreepDyingDuration;
            creepDyingBasePosition[key] = creepObject.transform.position;
            creepDyingBaseScale[key] = creepObject.transform.localScale;
            // Same lookup SpawnCreepDeathCue itself does for the flash half (CreepIdFor +
            // creepVisualLibrary.FindProfile), so the model treatment and the flash agree on
            // which style a given creep died with.
            creepDyingStyle[key] = CreepDeathCueStyleFor(creepId, visualProfile);

            var renderers = dyingRendererListPool.Count > 0 ? dyingRendererListPool.Pop() : new List<Renderer>();
            var colors = dyingColorListPool.Count > 0 ? dyingColorListPool.Pop() : new List<Color>();
            renderers.Clear();
            colors.Clear();
            CollectDyingRenderers(creepObject, visualProfile, renderers, colors);
            creepDyingRenderers[key] = renderers;
            creepDyingBaseColors[key] = colors;
        }

        /// <summary>
        /// The body renderers a death darkens, mirroring the body half of <c>ApplyCreepColor</c>:
        /// everything under the profile's BodyRendererPath, or only the root renderer for a
        /// primitive creep. Renderers whose material has no main colour (TMP labels, fill bars)
        /// are skipped rather than written to.
        /// </summary>
        private static void CollectDyingRenderers(GameObject creepObject, CreepVisualProfile visualProfile, List<Renderer> renderers, List<Color> colors)
        {
            if (visualProfile == null || visualProfile.Prefab == null)
            {
                if (creepObject.TryGetComponent<Renderer>(out var rootRenderer))
                {
                    renderers.Add(rootRenderer);
                }
            }
            else
            {
                var target = string.IsNullOrWhiteSpace(visualProfile.BodyRendererPath)
                    ? creepObject
                    : creepObject.transform.Find(visualProfile.BodyRendererPath)?.gameObject;
                if (target != null)
                {
                    target.GetComponentsInChildren(true, renderers);
                }
            }

            for (var index = renderers.Count - 1; index >= 0; index--)
            {
                var material = renderers[index].sharedMaterial;
                if (material == null || (!material.HasProperty("_Color") && !material.HasProperty("_BaseColor")))
                {
                    renderers.RemoveAt(index);
                }
            }

            for (var index = 0; index < renderers.Count; index++)
            {
                colors.Add(renderers[index].material.color);
            }
        }

        /// <summary>Drops every per-key dying entry and hands its lists back to the pools.</summary>
        private void ClearCreepDyingState(long key)
        {
            creepDyingStartedAt.Remove(key);
            creepDyingUntil.Remove(key);
            creepDyingBasePosition.Remove(key);
            creepDyingBaseScale.Remove(key);
            creepDyingStyle.Remove(key);
            if (creepDyingRenderers.TryGetValue(key, out var renderers))
            {
                renderers.Clear();
                dyingRendererListPool.Push(renderers);
                creepDyingRenderers.Remove(key);
            }

            if (creepDyingBaseColors.TryGetValue(key, out var colors))
            {
                colors.Clear();
                dyingColorListPool.Push(colors);
                creepDyingBaseColors.Remove(key);
            }
        }

        /// <summary>
        /// Shrinks, sinks and darkens each dying creep's own instance toward the board, per
        /// <see cref="CreepDeathCueStyle"/>, so a kill or leak reads as something happening to
        /// THIS creep rather than a flash decorating an already-empty cell.
        /// </summary>
        /// <remarks>
        /// Called from <see cref="UpdateExpandingRings"/> rather than from a new MonoBehaviour
        /// message: Update() itself lives in UnityVerticalSliceRenderer.cs, another agent's file
        /// this pass, but UpdateExpandingRings already runs unconditionally every frame from
        /// there — before the presentationDetail early return — which is exactly the timing this
        /// needs, and it is already the same kind of "decay a transient visual against Time.time"
        /// work.
        ///
        /// The base every style shares (Wave 5): scale 1 to <see cref="CreepDyingEndScale"/> on
        /// an ease-in, a sink of <see cref="CreepDyingSinkDepth"/>, and a darken to a third of the
        /// body colour. The styles only vary the opening: HeavyShatter and ShardScatter pop up
        /// first, SoftDissolve sinks late. The pooled instance is restored by the next creep to
        /// take it — SetCreepTransform resets scale and position and ApplyCreepColor the colours,
        /// both on a new creep's first frame — so nothing here needs undoing at release.
        ///
        /// No per-frame allocations: <see cref="dyingCreepKeysScratch"/> is a reused field, same
        /// pattern as <see cref="keysToReleaseScratch"/>.
        /// </remarks>
        private void UpdateDyingCreeps()
        {
            if (creepDyingUntil.Count == 0)
            {
                return;
            }

            dyingCreepKeysScratch.Clear();
            foreach (var pair in creepDyingUntil)
            {
                dyingCreepKeysScratch.Add(pair.Key);
            }

            foreach (var key in dyingCreepKeysScratch)
            {
                if (!activeCreeps.TryGetValue(key, out var creepObject) || creepObject == null)
                {
                    // Released some other way before its deadline (a full reset via
                    // ReleaseAllActiveObjects releases every active creep unconditionally and
                    // does not know about this map) — self-heal here rather than leaving orphaned
                    // entries, so a reset can never leave a dying creep "stuck".
                    ClearCreepDyingState(key);
                    continue;
                }

                var startedAt = creepDyingStartedAt[key];
                var duration = Mathf.Max(0.01f, creepDyingUntil[key] - startedAt);
                var t = Mathf.Clamp01((Time.time - startedAt) / duration);
                var baseScale = creepDyingBaseScale[key];
                var basePosition = creepDyingBasePosition[key];
                var easeIn = t * t;

                float shrink;
                float sink;
                switch (creepDyingStyle[key])
                {
                    case CreepDeathCueStyle.HeavyShatter:
                    {
                        // A quick upward pop first — debris thrown clear — then the collapse.
                        const float Pop = 0.2f;
                        var collapse = Mathf.InverseLerp(Pop, 1f, t);
                        shrink = t < Pop ? Mathf.Lerp(1f, 1.08f, t / Pop) : Mathf.Lerp(1.08f, CreepDyingEndScale, collapse * collapse);
                        sink = t < Pop ? Mathf.Lerp(0f, CreepDyingPopHeight, t / Pop) : Mathf.Lerp(CreepDyingPopHeight, -CreepDyingSinkDepth, collapse * collapse);
                        break;
                    }

                    case CreepDeathCueStyle.ShardScatter:
                    {
                        // A smaller, quicker pop than HeavyShatter — shards, not a boss carcass.
                        const float Pop = 0.12f;
                        var collapse = Mathf.InverseLerp(Pop, 1f, t);
                        shrink = t < Pop ? Mathf.Lerp(1f, 1.05f, t / Pop) : Mathf.Lerp(1.05f, CreepDyingEndScale, collapse * collapse);
                        sink = Mathf.Lerp(0f, -CreepDyingSinkDepth, easeIn);
                        break;
                    }

                    case CreepDeathCueStyle.SoftDissolve:
                        // No pop at all, and the sink lags the shrink, so it reads as fading
                        // rather than being hit.
                        shrink = Mathf.Lerp(1f, CreepDyingEndScale, easeIn);
                        sink = Mathf.Lerp(0f, -CreepDyingSinkDepth, easeIn * easeIn);
                        break;
                    default:
                        shrink = Mathf.Lerp(1f, CreepDyingEndScale, easeIn);
                        sink = Mathf.Lerp(0f, -CreepDyingSinkDepth, easeIn);
                        break;
                }

                creepObject.transform.localScale = baseScale * Mathf.Max(0f, shrink);
                creepObject.transform.position = basePosition + Vector3.up * sink;

                // Darkens faster than it shrinks: the first frame of the hold should already
                // read as a corpse, before the silhouette has changed much.
                if (creepDyingRenderers.TryGetValue(key, out var renderers) && creepDyingBaseColors.TryGetValue(key, out var colors))
                {
                    var darken = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.6f));
                    for (var index = 0; index < renderers.Count && index < colors.Count; index++)
                    {
                        var renderer = renderers[index];
                        if (renderer == null)
                        {
                            continue;
                        }

                        var baseColor = colors[index];
                        renderer.material.color = new Color(
                            Mathf.Lerp(baseColor.r, baseColor.r * 0.3f, darken),
                            Mathf.Lerp(baseColor.g, baseColor.g * 0.3f, darken),
                            Mathf.Lerp(baseColor.b, baseColor.b * 0.3f, darken),
                            baseColor.a);
                    }
                }
            }
        }

        private void ReleaseMissingCreeps()
        {
            keysToReleaseScratch.Clear();

            // Honoring the dying delay only while a match is actually in progress: ResetMatch()
            // (UnitySimulationDriver) flips HasStarted false the instant it runs and does not
            // flip it back true until the next match's build countdown ends, so gating on it here
            // is what lets a reset reclaim every creep immediately instead of leaving a dying
            // creep's shrinking corpse animating for up to CreepDyingDuration into a supposedly
            // empty board. UpdateDyingCreeps' own defensive cleanup (see its remark) then clears
            // the now-orphaned dying-state entries on the very next frame.
            var honorDyingDelay = simulationDriver != null && simulationDriver.HasStarted;

            foreach (var pair in activeCreeps)
            {
                var key = pair.Key;
                if (visibleKeys.Contains(key))
                {
                    continue;
                }

                if (honorDyingDelay)
                {
                    // First tick this key is missing: start its send-off here rather than in
                    // response to CreepKilledEvent/LeakEvent — see BeginCreepDying's remark for
                    // why the event is already too late to catch this same release sweep.
                    if (!creepDyingUntil.TryGetValue(key, out var dyingUntil))
                    {
                        BeginCreepDying(key, pair.Value);
                        dyingUntil = creepDyingUntil[key];
                    }

                    if (Time.time < dyingUntil)
                    {
                        continue;
                    }
                }

                keysToReleaseScratch.Add(key);
            }

            foreach (var key in keysToReleaseScratch)
            {
                ReleaseCreepToPool(key, activeCreeps[key]);
                activeCreeps.Remove(key);
                activeCreepPoolKeys.Remove(key);
                lastCreepHealth.Remove(key);
                creepHitFlashUntil.Remove(key);
                creepHitFlashApplied.Remove(key);
                creepFacingYaw.Remove(key);
                creepAnimators.Remove(key);
                creepSpread.Remove(key);
                ClearCreepDyingState(key);
            }
        }

        private void ReleaseCreepToPool(long key, GameObject instance)
        {
            if (activeCreepPoolKeys.TryGetValue(key, out var poolKey) && poolKey != PrimitiveCreepPoolKey)
            {
                ReleaseToPool(instance, GetCreepPrefabPool(poolKey));
                return;
            }

            ReleaseToPool(instance, creepPool);
        }

        private static string CreepPoolKey(CreepVisualProfile visualProfile)
        {
            if (visualProfile == null || visualProfile.Prefab == null)
            {
                return PrimitiveCreepPoolKey;
            }

            return string.IsNullOrWhiteSpace(visualProfile.CreepId)
                ? visualProfile.Prefab.name
                : visualProfile.CreepId;
        }

        /// <summary>
        /// Takes an instance from a primitive pool, guaranteeing it actually renders the requested
        /// primitive shape.
        /// </summary>
        /// <remarks>
        /// The primitiveType argument used to be honoured only when the pool happened to be empty
        /// (it was the argument to CreatePrimitive, nothing more), so any pool shared by two
        /// different shapes would silently hand back the wrong one. SpawnEffect (Sphere) and
        /// SpawnBeam (Cube) shared one pool, so a released beam cube came back as an "impact
        /// effect" and rendered a hard-edged box at the tower instead of a round glow — appearing
        /// at random, independent of board position, because it depended on what happened to be at
        /// the head of the queue. Those two now use separate pools, and this re-asserts the mesh on
        /// every take so the same class of mistake cannot silently reappear for any other pool.
        /// </remarks>
        private GameObject GetPooled(Queue<GameObject> pool, string name, PrimitiveType primitiveType)
        {
            var instance = pool.Count > 0 ? pool.Dequeue() : CreatePrimitive(name, primitiveType);
            instance.name = name;

            var expectedMesh = BoardMeshBuilder.PrimitiveMesh(primitiveType);
            if (expectedMesh != null && instance.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != expectedMesh)
            {
                meshFilter.sharedMesh = expectedMesh;
            }

            instance.SetActive(true);
            return instance;
        }

        private GameObject GetTextObject()
        {
            var instance = textPool.Count > 0 ? textPool.Dequeue() : new GameObject("FloatingText");
            instance.name = "FloatingText";
            instance.SetActive(true);
            return instance;
        }

        // RenderCompat.CreatePrimitive, not GameObject.CreatePrimitive: URP hands a player-built
        // primitive a null material, which draws magenta. See RenderCompat.CreatePrimitive.
        private static GameObject CreatePrimitive(string name, PrimitiveType primitiveType)
        {
            var instance = RenderCompat.CreatePrimitive(primitiveType);
            instance.name = name;
            return instance;
        }

        private static void ReleaseToPool(GameObject instance, Queue<GameObject> pool)
        {
            instance.SetActive(false);
            pool.Enqueue(instance);
        }
    }
}
