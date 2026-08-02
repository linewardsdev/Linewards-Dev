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

        private void ReleaseMissingCreeps()
        {
            keysToReleaseScratch.Clear();
            foreach (var pair in activeCreeps)
            {
                if (!visibleKeys.Contains(pair.Key))
                {
                    keysToReleaseScratch.Add(pair.Key);
                }
            }

            foreach (var key in keysToReleaseScratch)
            {
                ReleaseCreepToPool(key, activeCreeps[key]);
                activeCreeps.Remove(key);
                activeCreepPoolKeys.Remove(key);
                lastCreepHealth.Remove(key);
                creepHitFlashUntil.Remove(key);
                creepHitFlashApplied.Remove(key);
                creepAnimators.Remove(key);
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
