using System;
using System.Collections.Generic;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Projects simulation snapshots and events into pooled Unity objects. This class never
    /// changes simulation state; it is safe to disable it for low-spec or diagnostic runs.
    /// </summary>
    public sealed class UnityVerticalSliceRenderer : MonoBehaviour
    {
        private const int LaneLength = 12;
        private const int LaneDepth = 9;
        private const int LaneSpacing = 10;
        private const int CenterRow = 4;

        [SerializeField] private UnitySimulationDriver simulationDriver = null!;
        [SerializeField] private PresentationDetail presentationDetail = PresentationDetail.Full;

        private AudioSource feedbackAudioSource = null!;
        private AudioClip towerBuiltClip = null!;
        private AudioClip creepKilledClip = null!;
        private AudioClip leakClip = null!;

        private readonly Dictionary<string, GameObject> activeTowers = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> activeCreeps = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Vector3> lastKnownPositions = new Dictionary<string, Vector3>();
        private readonly List<GameObject> laneCells = new List<GameObject>();
        private readonly List<GameObject> laneDecorations = new List<GameObject>();
        private readonly HashSet<string> visibleKeys = new HashSet<string>();
        private readonly Queue<GameObject> towerPool = new Queue<GameObject>();
        private readonly Queue<GameObject> creepPool = new Queue<GameObject>();
        private readonly Queue<GameObject> effectPool = new Queue<GameObject>();
        private readonly Queue<GameObject> textPool = new Queue<GameObject>();
        private readonly List<TimedPresentation> timedPresentations = new List<TimedPresentation>();

        private bool laneCreated;

        public PresentationDetail Detail => presentationDetail;

        public int ActivePresentationObjectCount => activeTowers.Count + activeCreeps.Count + timedPresentations.Count;

        public int PooledPresentationObjectCount => towerPool.Count + creepPool.Count + effectPool.Count + textPool.Count;

        public void Initialize(UnitySimulationDriver driver)
        {
            simulationDriver = driver;
        }

        private void Awake()
        {
            feedbackAudioSource = gameObject.AddComponent<AudioSource>();
            feedbackAudioSource.playOnAwake = false;
            feedbackAudioSource.spatialBlend = 0f;
            towerBuiltClip = CreateTone("TowerBuiltCue", 660f, 0.07f);
            creepKilledClip = CreateTone("CreepKilledCue", 880f, 0.05f);
            leakClip = CreateTone("LeakCue", 180f, 0.14f);
        }

        public void SetPresentationDetail(PresentationDetail detail)
        {
            presentationDetail = detail;
            if (detail == PresentationDetail.Disabled)
            {
                ReleaseAllActiveObjects();
            }
        }

        private void Update()
        {
            ReleaseExpiredPresentations();
            if (presentationDetail == PresentationDetail.Disabled || simulationDriver == null)
            {
                return;
            }

            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot == null)
            {
                return;
            }

            EnsureLane();
            RenderSnapshot(snapshot);
            if (presentationDetail == PresentationDetail.Full)
            {
                RenderEvents(simulationDriver.LatestEvents);
            }
        }

        private void EnsureLane()
        {
            if (laneCreated)
            {
                return;
            }

            for (var lane = 1; lane <= 3; lane++)
            {
                CreateLaneFrame(lane);
                for (var x = 0; x < LaneLength; x++)
                {
                    for (var y = 0; y < LaneDepth; y++)
                    {
                        var cell = CreatePrimitive($"Lane{lane}Cell_{x}_{y}", PrimitiveType.Cube);
                        cell.transform.position = new Vector3(x, -0.18f, LaneOffset(lane) + y);
                        cell.transform.localScale = new Vector3(0.96f, 0.12f, 0.96f);
                        SetColor(cell, CellColor(lane, x, y));
                        laneCells.Add(cell);
                    }
                }

                CreateLaneLandmark(lane, 0, CenterRow, "Spawn", MintSignal, 0.42f);
                CreateLaneLandmark(lane, LaneLength - 1, CenterRow, "Exit", SignalGold, 0.5f);
                CreateLaneLabel(lane);
            }

            laneCreated = true;
        }

        private void RenderSnapshot(LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            visibleKeys.Clear();
            foreach (var tower in snapshot.Towers)
            {
                var key = tower.EntityId.Value.ToString();
                visibleKeys.Add(key);
                var towerObject = GetOrCreate(activeTowers, towerPool, key, "WardTower", PrimitiveType.Cylinder);
                SetTowerTransform(towerObject, tower.Position, tower.LaneId, tower.TowerId.Value);
                SetColor(towerObject, TowerRoleColor(tower.TowerId.Value, tower.OwnerId.Value));
                ConfigureTowerRoleMarker(towerObject, tower.TowerId.Value);
                lastKnownPositions[key] = towerObject.transform.position;
            }

            ReleaseMissing(activeTowers, towerPool);
            visibleKeys.Clear();
            foreach (var creep in snapshot.Creeps)
            {
                var key = creep.EntityId.Value.ToString();
                visibleKeys.Add(key);
                var creepObject = GetOrCreate(activeCreeps, creepPool, key, "PressureCreep", PrimitiveType.Sphere);
                SetCreepTransform(creepObject, creep.Position, creep.LaneId, creep.CreepId.Value);
                SetColor(creepObject, CreepRoleColor(creep.CreepId.Value, creep.SenderId.Value));
                lastKnownPositions[key] = creepObject.transform.position;
            }

            ReleaseMissing(activeCreeps, creepPool);
        }

        private void RenderEvents(IReadOnlyList<ISimulationEvent> events)
        {
            foreach (var simulationEvent in events)
            {
                switch (simulationEvent)
                {
                    case TowerPlacedEvent towerPlaced:
                        SpawnEffect(GridToWorld(towerPlaced.Position, towerPlaced.LaneId), new Color(0.3f, 0.8f, 1f));
                        PlaySound(towerBuiltClip);
                        break;
                    case CreepSpawnedEvent spawned:
                        SpawnEffect(new Vector3(0f, 0.35f, LaneOffset(spawned.DefenderId.Value) + CenterRow), new Color(0.35f, 1f, 0.5f));
                        break;
                    case CreepKilledEvent creepKilled:
                        SpawnEffect(PositionFor(creepKilled.CreepEntityId.Value.ToString()), new Color(1f, 0.9f, 0.25f));
                        SpawnFloatingText(PositionFor(creepKilled.CreepEntityId.Value.ToString()), $"+{creepKilled.BountyAwarded.Amount}", Color.yellow);
                        PlaySound(creepKilledClip);
                        break;
                    case LeakEvent leak:
                        var position = PositionFor(leak.CreepEntityId.Value.ToString());
                        SpawnEffect(position, new Color(1f, 0.2f, 0.2f));
                        SpawnFloatingText(position, $"-{leak.LivesLost.Amount} life", new Color(1f, 0.35f, 0.35f));
                        PlaySound(leakClip);
                        TriggerHapticFeedback();
                        break;
                    case IncomeTickEvent incomeTick:
                        SpawnFloatingText(new Vector3(0f, 1.35f, 1f), $"+{incomeTick.GoldAwarded.Amount} income", new Color(0.35f, 1f, 0.5f));
                        break;
                    case PlayerEliminatedEvent eliminated:
                        SpawnFloatingText(new Vector3(2.5f, 1.55f, 1f), $"Player {eliminated.PlayerId.Value} eliminated", Color.red);
                        break;
                }
            }
        }

        private void SpawnEffect(Vector3 position, Color color)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                return;
            }

            var effect = GetPooled(effectPool, "ImpactEffect", PrimitiveType.Sphere);
            effect.transform.position = position;
            effect.transform.localScale = Vector3.one * 0.62f;
            SetColor(effect, color);
            timedPresentations.Add(new TimedPresentation(effect, Time.time + 0.3f, effectPool));
        }

        private void SpawnFloatingText(Vector3 position, string text, Color color)
        {
            var textObject = GetTextObject();
            textObject.transform.position = position + Vector3.up * 0.55f;
            textObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            textObject.transform.localScale = Vector3.one * 0.01f;
            var mesh = textObject.GetComponent<TextMesh>();
            if (mesh == null)
            {
                mesh = textObject.AddComponent<TextMesh>();
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.alignment = TextAlignment.Center;
                mesh.characterSize = 0.16f;
                mesh.fontSize = 42;
            }

            mesh.text = text;
            mesh.color = color;
            mesh.characterSize = 0.16f * PresentationPreferences.TextScale;
            timedPresentations.Add(new TimedPresentation(textObject, Time.time + 0.7f, textPool));
        }

        private static void TriggerHapticFeedback()
        {
            if (!PresentationPreferences.ReducedEffects)
            {
#if UNITY_IOS || UNITY_ANDROID
                Handheld.Vibrate();
#endif
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (!PresentationPreferences.ReducedEffects && feedbackAudioSource != null)
            {
                feedbackAudioSource.PlayOneShot(clip, 0.25f);
            }
        }

        private static AudioClip CreateTone(string name, float frequency, float duration)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * index / sampleRate) * 0.2f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void ReleaseExpiredPresentations()
        {
            for (var index = timedPresentations.Count - 1; index >= 0; index--)
            {
                var presentation = timedPresentations[index];
                if (Time.time < presentation.ReleaseAt)
                {
                    continue;
                }

                ReleaseToPool(presentation.Object, presentation.Pool);
                timedPresentations.RemoveAt(index);
            }
        }

        private void ReleaseMissing(Dictionary<string, GameObject> activeObjects, Queue<GameObject> pool)
        {
            var keysToRelease = new List<string>();
            foreach (var pair in activeObjects)
            {
                if (!visibleKeys.Contains(pair.Key))
                {
                    keysToRelease.Add(pair.Key);
                }
            }

            foreach (var key in keysToRelease)
            {
                ReleaseToPool(activeObjects[key], pool);
                activeObjects.Remove(key);
            }
        }

        private void ReleaseAllActiveObjects()
        {
            foreach (var pair in activeTowers) ReleaseToPool(pair.Value, towerPool);
            foreach (var pair in activeCreeps) ReleaseToPool(pair.Value, creepPool);
            activeTowers.Clear();
            activeCreeps.Clear();
            foreach (var presentation in timedPresentations) ReleaseToPool(presentation.Object, presentation.Pool);
            timedPresentations.Clear();
        }

        private GameObject GetOrCreate(Dictionary<string, GameObject> activeObjects, Queue<GameObject> pool, string key, string name, PrimitiveType primitiveType)
        {
            if (activeObjects.TryGetValue(key, out var instance)) return instance;
            instance = GetPooled(pool, name, primitiveType);
            activeObjects[key] = instance;
            return instance;
        }

        private GameObject GetPooled(Queue<GameObject> pool, string name, PrimitiveType primitiveType)
        {
            var instance = pool.Count > 0 ? pool.Dequeue() : CreatePrimitive(name, primitiveType);
            instance.name = name;
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

        private static GameObject CreatePrimitive(string name, PrimitiveType primitiveType)
        {
            var instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = name;
            return instance;
        }

        private static void ReleaseToPool(GameObject instance, Queue<GameObject> pool)
        {
            instance.SetActive(false);
            pool.Enqueue(instance);
        }

        private static void SetTransform(GameObject instance, GridPosition position, LaneId laneId, float scale)
        {
            instance.transform.position = GridToWorld(position, laneId);
            instance.transform.localScale = Vector3.one * scale;
        }

        private static void SetTowerTransform(GameObject instance, GridPosition position, LaneId laneId, string towerId)
        {
            instance.transform.position = GridToWorld(position, laneId) + Vector3.up * 0.12f;
            instance.transform.localScale = TowerRoleScale(towerId);
        }

        private static void SetCreepTransform(GameObject instance, GridPosition position, LaneId laneId, string creepId)
        {
            instance.transform.position = GridToWorld(position, laneId) + CreepRoleOffset(creepId);
            instance.transform.localScale = CreepRoleScale(creepId);
        }

        private static Vector3 GridToWorld(GridPosition position, LaneId laneId) => new Vector3(position.X, 0.35f, position.Y + LaneOffset(laneId.Value));

        private static float LaneOffset(int laneId) => (laneId - 1) * LaneSpacing;

        private Vector3 PositionFor(string entityId) => lastKnownPositions.TryGetValue(entityId, out var position) ? position : new Vector3(LaneLength - 1, 0.35f, CenterRow);

        private void CreateLaneFrame(int laneId)
        {
            var offset = LaneOffset(laneId);
            var accent = OwnerAccent(laneId);
            CreateBoardRail($"Lane{laneId}NorthRail", new Vector3((LaneLength - 1) * 0.5f, -0.06f, offset - 0.62f), new Vector3(LaneLength + 0.35f, 0.16f, 0.12f), accent);
            CreateBoardRail($"Lane{laneId}SouthRail", new Vector3((LaneLength - 1) * 0.5f, -0.06f, offset + LaneDepth - 0.38f), new Vector3(LaneLength + 0.35f, 0.16f, 0.12f), accent);
            CreateBoardRail($"Lane{laneId}WestRail", new Vector3(-0.62f, -0.06f, offset + (LaneDepth - 1) * 0.5f), new Vector3(0.12f, 0.16f, LaneDepth + 0.35f), accent);
            CreateBoardRail($"Lane{laneId}EastRail", new Vector3(LaneLength - 0.38f, -0.06f, offset + (LaneDepth - 1) * 0.5f), new Vector3(0.12f, 0.16f, LaneDepth + 0.35f), accent);
        }

        private void CreateBoardRail(string name, Vector3 position, Vector3 scale, Color color)
        {
            var rail = CreatePrimitive(name, PrimitiveType.Cube);
            rail.transform.position = position;
            rail.transform.localScale = scale;
            SetColor(rail, color);
            laneDecorations.Add(rail);
        }

        private void CreateLaneLandmark(int laneId, int x, int y, string landmarkName, Color color, float scale)
        {
            var marker = CreatePrimitive($"Lane{laneId}{landmarkName}Beacon", PrimitiveType.Cylinder);
            marker.transform.position = new Vector3(x, 0.08f, LaneOffset(laneId) + y);
            marker.transform.localScale = new Vector3(scale, 0.16f, scale);
            SetColor(marker, color);
            laneDecorations.Add(marker);
        }

        private void CreateLaneLabel(int laneId)
        {
            var labelObject = new GameObject($"Lane{laneId}Label");
            labelObject.transform.position = new Vector3(2.2f, 0.08f, LaneOffset(laneId) - 1.08f);
            labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = Vector3.one * 0.035f;
            var label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleLeft;
            label.alignment = TextAlignment.Left;
            label.fontSize = 44;
            label.characterSize = 0.18f;
            label.text = laneId == 1 ? "YOUR LINE" : $"OPPONENT {laneId}";
            label.color = OwnerAccent(laneId);
            laneDecorations.Add(labelObject);
        }

        private static Color CellColor(int laneId, int x, int y)
        {
            if (x == 0 && y == CenterRow)
            {
                return MintSignal;
            }

            if (x == LaneLength - 1 && y == CenterRow)
            {
                return SignalGold;
            }

            if (y == CenterRow)
            {
                return new Color(0.12f, 0.28f, 0.42f);
            }

            var checker = (x + y + laneId) % 2 == 0 ? 0.02f : 0f;
            return new Color(0.07f + checker, 0.1f + checker, 0.17f + checker);
        }

        private static Vector3 TowerRoleScale(string towerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control"))
            {
                return new Vector3(0.82f, 0.46f, 0.82f);
            }

            if (ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay"))
            {
                return new Vector3(0.52f, 0.52f, 0.52f);
            }

            return new Vector3(0.62f, 0.78f, 0.62f);
        }

        private static Vector3 CreepRoleScale(string creepId)
        {
            if (ContainsRole(creepId, "swarm"))
            {
                return new Vector3(0.28f, 0.18f, 0.28f);
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank"))
            {
                return new Vector3(0.52f, 0.42f, 0.52f);
            }

            if (ContainsRole(creepId, "boss"))
            {
                return new Vector3(0.82f, 0.72f, 0.82f);
            }

            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                return new Vector3(0.42f, 0.18f, 0.42f);
            }

            if (ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege"))
            {
                return new Vector3(0.46f, 0.3f, 0.34f);
            }

            return new Vector3(0.36f, 0.24f, 0.36f);
        }

        private static Vector3 CreepRoleOffset(string creepId)
        {
            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                return Vector3.up * 0.32f;
            }

            if (ContainsRole(creepId, "boss"))
            {
                return Vector3.up * 0.16f;
            }

            return Vector3.up * 0.02f;
        }

        private static Color TowerRoleColor(string towerId, int ownerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "ice") || ContainsRole(towerId, "control"))
            {
                return new Color(0.56f, 0.86f, 1f);
            }

            if (ContainsRole(towerId, "splash") || ContainsRole(towerId, "fire") || ContainsRole(towerId, "area"))
            {
                return new Color(1f, 0.58f, 0.22f);
            }

            if (ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay"))
            {
                return SignalGold;
            }

            return OwnerAccent(ownerId);
        }

        private static Color CreepRoleColor(string creepId, int senderId)
        {
            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss"))
            {
                return new Color(1f, 0.55f, 0.35f);
            }

            if (ContainsRole(creepId, "swarm"))
            {
                return new Color(0.75f, 0.95f, 1f);
            }

            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                return new Color(0.82f, 0.72f, 1f);
            }

            if (ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                return new Color(0.72f, 0.84f, 0.9f, 0.62f);
            }

            if (ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege"))
            {
                return new Color(1f, 0.38f, 0.44f);
            }

            return SenderColor(senderId);
        }

        private static void ConfigureTowerRoleMarker(GameObject towerObject, string towerId)
        {
            var marker = towerObject.transform.Find("RoleMarker")?.gameObject;
            if (marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "RoleMarker";
                marker.transform.SetParent(towerObject.transform, false);
            }

            marker.transform.localPosition = Vector3.up * 0.72f;
            marker.transform.localScale = TowerMarkerScale(towerId);
            SetColor(marker, TowerMarkerColor(towerId));
        }

        private static Vector3 TowerMarkerScale(string towerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control") || ContainsRole(towerId, "area"))
            {
                return new Vector3(0.8f, 0.08f, 0.8f);
            }

            if (ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay"))
            {
                return new Vector3(0.34f, 0.34f, 0.34f);
            }

            return new Vector3(0.22f, 0.22f, 0.22f);
        }

        private static Color TowerMarkerColor(string towerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "ice") || ContainsRole(towerId, "control"))
            {
                return new Color(0.72f, 0.94f, 1f);
            }

            if (ContainsRole(towerId, "splash") || ContainsRole(towerId, "fire") || ContainsRole(towerId, "area"))
            {
                return new Color(1f, 0.7f, 0.28f);
            }

            if (ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay"))
            {
                return SignalGold;
            }

            return MintSignal;
        }

        private static bool ContainsRole(string contentId, string role) => contentId.IndexOf(role, StringComparison.OrdinalIgnoreCase) >= 0;

        private static Color SenderColor(int playerId) => playerId % 3 == 0 ? new Color(0.95f, 0.42f, 0.5f) : playerId % 3 == 1 ? SignalGold : WardViolet;

        private static Color OwnerAccent(int playerId) => playerId switch
        {
            1 => ArcaneBlue,
            2 => WardViolet,
            _ => SignalGold
        };

        private static readonly Color NightInk = new Color(0.063f, 0.094f, 0.184f);
        private static readonly Color ArcaneBlue = new Color(0.302f, 0.639f, 1f);
        private static readonly Color WardViolet = new Color(0.608f, 0.424f, 1f);
        private static readonly Color SignalGold = new Color(1f, 0.784f, 0.29f);
        private static readonly Color MintSignal = new Color(0.349f, 0.882f, 0.714f);

        private static void SetColor(GameObject instance, Color color)
        {
            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
        }

        private readonly struct TimedPresentation
        {
            public TimedPresentation(GameObject @object, float releaseAt, Queue<GameObject> pool)
            {
                Object = @object;
                ReleaseAt = releaseAt;
                Pool = pool;
            }

            public GameObject Object { get; }
            public float ReleaseAt { get; }
            public Queue<GameObject> Pool { get; }
        }
    }

    public enum PresentationDetail
    {
        Disabled,
        Simplified,
        Full,
    }
}
