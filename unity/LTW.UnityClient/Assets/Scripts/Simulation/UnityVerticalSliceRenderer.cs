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
        private const int LaneWidth = 7;
        private const int LaneLength = 18;
        private const int LaneSpacing = 9;
        private const int CenterColumn = 3;
        private const float BoardCenterX = (LaneWidth - 1) * 0.5f;
        private const float BoardCenterZ = (LaneLength - 1) * 0.5f;

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
                CreateLaneBackplate(lane);
                CreateLaneFrame(lane);
                CreateLaneFlowCues(lane);
                for (var x = 0; x < LaneWidth; x++)
                {
                    for (var y = 0; y < LaneLength; y++)
                    {
                        var cell = CreatePrimitive($"Lane{lane}Cell_{x}_{y}", PrimitiveType.Cube);
                        cell.transform.position = GridToWorld(new GridPosition(x, y), new LaneId(lane)) + Vector3.down * 0.53f;
                        cell.transform.localScale = new Vector3(0.96f, 0.12f, 0.96f);
                        SetColor(cell, CellColor(lane, x, y));
                        laneCells.Add(cell);
                    }
                }

                CreateLaneEndpointBox(lane, CenterColumn, 0, "SpawnBox", MintSignal);
                CreateLaneEndpointBox(lane, CenterColumn, LaneLength - 1, "LifeLossBox", new Color(1f, 0.32f, 0.24f));
                CreateLaneLandmark(lane, CenterColumn, 0, "Spawn", MintSignal, 0.42f);
                CreateLaneLandmark(lane, CenterColumn, LaneLength - 1, "LifeLoss", new Color(1f, 0.32f, 0.24f), 0.5f);
                CreateLaneEndpointLabel(lane, CenterColumn, 0, "SPAWN", MintSignal);
                CreateLaneEndpointLabel(lane, CenterColumn, LaneLength - 1, "LIFE LOSS", new Color(1f, 0.32f, 0.24f));
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
                var isNewCreep = !activeCreeps.ContainsKey(key);
                var creepObject = GetOrCreate(activeCreeps, creepPool, key, "PressureCreep", PrimitiveType.Sphere);
                SetCreepTransform(creepObject, creep.Position, creep.LaneId, creep.CreepId.Value, isNewCreep);
                SetColor(creepObject, CreepRoleColor(creep.CreepId.Value, creep.SenderId.Value));
                ConfigureCreepRoleMarker(creepObject, creep.CreepId.Value, creep.SenderId.Value);
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
                        SpawnEffect(GridToWorld(towerPlaced.Position, towerPlaced.LaneId), MintSignal, 0.68f, 0.34f);
                        SpawnFloatingText(GridToWorld(towerPlaced.Position, towerPlaced.LaneId), "WARD", MintSignal, 0.62f);
                        PlaySound(towerBuiltClip);
                        break;
                    case TowerSoldEvent towerSold:
                        SpawnEffect(PositionFor(towerSold.TowerEntityId.Value.ToString()), SignalGold, 0.42f, 0.24f);
                        SpawnFloatingText(PositionFor(towerSold.TowerEntityId.Value.ToString()), $"+{towerSold.Refund.Amount}", SignalGold, 0.58f);
                        break;
                    case CreepQueuedEvent queued:
                        SpawnSendCue(queued);
                        break;
                    case CreepSpawnedEvent spawned:
                        SpawnEffect(SpawnPosition(spawned.DefenderId.Value), CreepRoleColor(spawned.CreepId.Value, spawned.SenderId.Value), 0.52f, 0.28f);
                        SpawnFloatingText(SpawnPosition(spawned.DefenderId.Value), SpawnLabel(spawned.CreepId.Value), CreepRoleColor(spawned.CreepId.Value, spawned.SenderId.Value), 0.48f);
                        break;
                    case CreepKilledEvent creepKilled:
                        var killPosition = PositionFor(creepKilled.CreepEntityId.Value.ToString());
                        SpawnEffect(killPosition, SignalGold, 0.42f, 0.2f);
                        SpawnFloatingText(killPosition, $"+{creepKilled.BountyAwarded.Amount}", SignalGold, 0.56f);
                        PlaySound(creepKilledClip);
                        break;
                    case LeakEvent leak:
                        var position = PositionFor(leak.CreepEntityId.Value.ToString());
                        SpawnEffect(position, new Color(1f, 0.22f, 0.28f), 0.86f, 0.42f);
                        SpawnFloatingText(position, $"-{leak.LivesLost.Amount} LIFE", new Color(1f, 0.35f, 0.35f), 0.72f);
                        if (leak.BountyAwarded.Amount > 0)
                        {
                            SpawnFloatingText(position + Vector3.right * 0.55f, $"+{leak.BountyAwarded.Amount}", SignalGold, 0.52f);
                        }

                        PlaySound(leakClip);
                        TriggerHapticFeedback();
                        break;
                    case IncomeTickEvent incomeTick:
                        SpawnEffect(IncomePosition(incomeTick.PlayerId.Value), SignalGold, 0.46f, 0.22f);
                        SpawnFloatingText(IncomePosition(incomeTick.PlayerId.Value), $"+{incomeTick.GoldAwarded.Amount} income", SignalGold, 0.58f);
                        break;
                    case PlayerEliminatedEvent eliminated:
                        SpawnEffect(LaneCenter(eliminated.PlayerId.Value) + Vector3.up * 0.2f, new Color(1f, 0.18f, 0.24f), 1.15f, 0.55f);
                        SpawnFloatingText(LaneCenter(eliminated.PlayerId.Value) + Vector3.up * 1.2f, $"PLAYER {eliminated.PlayerId.Value} OUT", new Color(1f, 0.35f, 0.35f), 0.8f);
                        break;
                    case MatchEndedEvent ended:
                        SpawnFloatingText(LaneCenter(ended.WinnerId.Value) + Vector3.up * 1.85f, $"PLAYER {ended.WinnerId.Value} WINS", SignalGold, 1f);
                        break;
                }
            }
        }

        private void SpawnEffect(Vector3 position, Color color) => SpawnEffect(position, color, 0.62f, 0.3f);

        private void SpawnEffect(Vector3 position, Color color, float scale, float duration)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                return;
            }

            var effect = GetPooled(effectPool, "ImpactEffect", PrimitiveType.Sphere);
            effect.transform.position = position;
            effect.transform.localScale = Vector3.one * scale;
            SetColor(effect, color);
            timedPresentations.Add(new TimedPresentation(effect, Time.time + duration, effectPool));
        }

        private void SpawnFloatingText(Vector3 position, string text, Color color) => SpawnFloatingText(position, text, color, 0.7f);

        private void SpawnFloatingText(Vector3 position, string text, Color color, float duration)
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
            timedPresentations.Add(new TimedPresentation(textObject, Time.time + duration, textPool));
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

        private void SpawnSendCue(CreepQueuedEvent queued)
        {
            var senderPosition = GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(queued.SenderId.Value)) + Vector3.up * 0.2f;
            var defenderPosition = SpawnPosition(queued.DefenderId.Value);
            var color = CreepRoleColor(queued.CreepId.Value, queued.SenderId.Value);
            SpawnEffect(senderPosition, color, 0.44f, 0.24f);
            SpawnEffect(defenderPosition, color, 0.54f, 0.3f);
            SpawnFloatingText(defenderPosition, $"{queued.Quantity}x {SpawnLabel(queued.CreepId.Value)}", color, 0.56f);
        }

        private static Vector3 SpawnPosition(int laneId) => GridToWorld(new GridPosition(CenterColumn, 0), new LaneId(laneId));

        private static Vector3 IncomePosition(int playerId) => new Vector3(LaneOffset(playerId) + 1.2f, 1.25f, WorldZ(1));

        private static string SpawnLabel(string creepId)
        {
            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank"))
            {
                return "BRUTE";
            }

            if (ContainsRole(creepId, "swarm"))
            {
                return "SWARM";
            }

            if (ContainsRole(creepId, "boss"))
            {
                return "BOSS";
            }

            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                return "AIR";
            }

            if (ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                return "STEALTH";
            }

            if (ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege"))
            {
                return "SIEGE";
            }

            return "RUNNER";
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

        private static void SetCreepTransform(GameObject instance, GridPosition position, LaneId laneId, string creepId, bool snapToTarget)
        {
            var roleMotion = CreepRoleMotion(creepId);
            var targetPosition = GridToWorld(position, laneId) + CreepRoleOffset(creepId) + roleMotion.PositionOffset;
            instance.transform.position = snapToTarget || Vector3.Distance(instance.transform.position, targetPosition) > 2.5f
                ? targetPosition
                : Vector3.Lerp(instance.transform.position, targetPosition, Mathf.Clamp01(Time.deltaTime * 8f));
            instance.transform.localScale = CreepRoleScale(creepId);
            instance.transform.rotation = roleMotion.Rotation;
        }

        private static Vector3 GridToWorld(GridPosition position, LaneId laneId) => new Vector3(LaneOffset(laneId.Value) + position.X, 0.35f, WorldZ(position.Y));

        private static float LaneOffset(int laneId) => (laneId - 1) * LaneSpacing;

        private static float WorldZ(int gridY) => LaneLength - 1 - gridY;

        private static Vector3 LaneCenter(int laneId) => new Vector3(LaneOffset(laneId) + BoardCenterX, 0.35f, BoardCenterZ);

        private Vector3 PositionFor(string entityId) => lastKnownPositions.TryGetValue(entityId, out var position) ? position : GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(1));

        private void CreateLaneFrame(int laneId)
        {
            var offset = LaneOffset(laneId);
            var accent = OwnerAccent(laneId);
            var railHeight = laneId == 1 ? 0.22f : 0.14f;
            var longRailWidth = laneId == 1 ? 0.18f : 0.1f;
            var endRailWidth = laneId == 1 ? 0.18f : 0.1f;
            CreateBoardRail($"Lane{laneId}NorthRail", new Vector3(offset + BoardCenterX, -0.06f, LaneLength - 0.38f), new Vector3(LaneWidth + 0.35f, railHeight, longRailWidth), accent);
            CreateBoardRail($"Lane{laneId}SouthRail", new Vector3(offset + BoardCenterX, -0.06f, -0.62f), new Vector3(LaneWidth + 0.35f, railHeight, longRailWidth), accent);
            CreateBoardRail($"Lane{laneId}WestRail", new Vector3(offset - 0.62f, -0.06f, BoardCenterZ), new Vector3(endRailWidth, railHeight, LaneLength + 0.35f), accent);
            CreateBoardRail($"Lane{laneId}EastRail", new Vector3(offset + LaneWidth - 0.38f, -0.06f, BoardCenterZ), new Vector3(endRailWidth, railHeight, LaneLength + 0.35f), accent);
        }

        private void CreateLaneBackplate(int laneId)
        {
            var backplate = CreatePrimitive($"Lane{laneId}Backplate", PrimitiveType.Cube);
            backplate.transform.position = new Vector3(LaneOffset(laneId) + BoardCenterX, -0.28f, BoardCenterZ);
            backplate.transform.localScale = new Vector3(LaneWidth + 1.35f, 0.08f, LaneLength + 1.35f);
            SetColor(backplate, laneId == 1 ? new Color(0.055f, 0.12f, 0.22f) : new Color(0.045f, 0.065f, 0.12f));
            laneDecorations.Add(backplate);
        }

        private void CreateLaneFlowCues(int laneId)
        {
            for (var y = 2; y < LaneLength - 1; y += 3)
            {
                CreateFlowArrow(laneId, y);
            }
        }

        private void CreateFlowArrow(int laneId, int y)
        {
            var offset = LaneOffset(laneId);
            var color = laneId == 1 ? new Color(0.42f, 0.76f, 1f) : new Color(0.24f, 0.4f, 0.68f);
            var shaft = CreatePrimitive($"Lane{laneId}Flow_{y}_Shaft", PrimitiveType.Cube);
            shaft.transform.position = new Vector3(offset + CenterColumn, 0.02f, WorldZ(y));
            shaft.transform.localScale = new Vector3(0.07f, 0.05f, 0.48f);
            SetColor(shaft, color);
            laneDecorations.Add(shaft);

            var eastHead = CreatePrimitive($"Lane{laneId}Flow_{y}_HeadA", PrimitiveType.Cube);
            eastHead.transform.position = new Vector3(offset + CenterColumn + 0.12f, 0.025f, WorldZ(y) - 0.27f);
            eastHead.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            eastHead.transform.localScale = new Vector3(0.06f, 0.05f, 0.24f);
            SetColor(eastHead, color);
            laneDecorations.Add(eastHead);

            var westHead = CreatePrimitive($"Lane{laneId}Flow_{y}_HeadB", PrimitiveType.Cube);
            westHead.transform.position = new Vector3(offset + CenterColumn - 0.12f, 0.025f, WorldZ(y) - 0.27f);
            westHead.transform.rotation = Quaternion.Euler(0f, -35f, 0f);
            westHead.transform.localScale = new Vector3(0.06f, 0.05f, 0.24f);
            SetColor(westHead, color);
            laneDecorations.Add(westHead);
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
            marker.transform.position = GridToWorld(new GridPosition(x, y), new LaneId(laneId)) + Vector3.down * 0.23f;
            marker.transform.localScale = new Vector3(scale, 0.22f, scale);
            SetColor(marker, color);
            laneDecorations.Add(marker);
        }

        private void CreateLaneEndpointBox(int laneId, int x, int y, string boxName, Color color)
        {
            var box = CreatePrimitive($"Lane{laneId}{boxName}", PrimitiveType.Cube);
            box.transform.position = GridToWorld(new GridPosition(x, y), new LaneId(laneId)) + Vector3.down * 0.45f;
            box.transform.localScale = new Vector3(2.45f, 0.18f, 1.1f);
            SetColor(box, color);
            laneDecorations.Add(box);
        }

        private void CreateLaneEndpointLabel(int laneId, int x, int y, string labelText, Color color)
        {
            var labelObject = new GameObject($"Lane{laneId}{labelText}Label");
            labelObject.transform.position = GridToWorld(new GridPosition(x, y), new LaneId(laneId)) + new Vector3(-1.15f, -0.17f, labelText == "SPAWN" ? 0.7f : -0.7f);
            labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = Vector3.one * 0.022f;
            var label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 42;
            label.characterSize = 0.16f;
            label.text = labelText;
            label.color = color;
            laneDecorations.Add(labelObject);
        }

        private void CreateLaneLabel(int laneId)
        {
            var labelObject = new GameObject($"Lane{laneId}Label");
            labelObject.transform.position = new Vector3(LaneOffset(laneId) + 0.2f, 0.1f, LaneLength + 0.88f);
            labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = Vector3.one * (laneId == 1 ? 0.04f : 0.032f);
            var label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleLeft;
            label.alignment = TextAlignment.Left;
            label.fontSize = 44;
            label.characterSize = 0.18f;
            label.text = laneId == 1 ? "YOUR LINE - DEFEND" : $"OPPONENT {laneId} - SEND TARGET";
            label.color = OwnerAccent(laneId);
            laneDecorations.Add(labelObject);
        }

        private static Color CellColor(int laneId, int x, int y)
        {
            if (x == CenterColumn && y == 0)
            {
                return MintSignal;
            }

            if (x == CenterColumn && y == LaneLength - 1)
            {
                return SignalGold;
            }

            if (x == CenterColumn)
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
                return new Vector3(0.46f, 0.28f, 0.46f);
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank"))
            {
                return new Vector3(0.74f, 0.56f, 0.74f);
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

            return new Vector3(0.56f, 0.34f, 0.56f);
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

        private static CreepMotion CreepRoleMotion(string creepId)
        {
            var time = Time.time;
            if (ContainsRole(creepId, "swarm"))
            {
                var pulse = Mathf.Sin(time * 12f) * 0.025f;
                return new CreepMotion(new Vector3(pulse, 0f, -pulse), Quaternion.identity);
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss"))
            {
                var weight = Mathf.Abs(Mathf.Sin(time * 4f)) * 0.035f;
                return new CreepMotion(Vector3.down * weight, Quaternion.identity);
            }

            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                var hover = Mathf.Sin(time * 5f) * 0.08f;
                return new CreepMotion(Vector3.up * hover, Quaternion.Euler(0f, time * 80f, 0f));
            }

            if (ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                return new CreepMotion(Vector3.zero, Quaternion.Euler(0f, time * 45f, 0f));
            }

            if (ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege"))
            {
                var windup = Mathf.Sin(time * 6f) * 4f;
                return new CreepMotion(Vector3.zero, Quaternion.Euler(0f, 0f, windup));
            }

            var dart = Mathf.Sin(time * 10f) * 0.04f;
            return new CreepMotion(Vector3.right * dart, Quaternion.identity);
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

            var lens = EnsureChild(towerObject, "FocusedLens", PrimitiveType.Sphere);
            var controlRing = EnsureChild(towerObject, "ControlRing", PrimitiveType.Cylinder);
            var relayMast = EnsureChild(towerObject, "RelayMast", PrimitiveType.Cube);
            var relayCore = EnsureChild(towerObject, "RelayCore", PrimitiveType.Sphere);

            var isControl = ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control") || ContainsRole(towerId, "area");
            var isRelay = ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay");
            var isFocused = !isControl && !isRelay;

            ConfigureChild(lens, isFocused, new Vector3(0f, 0.98f, 0f), new Vector3(0.28f, 0.28f, 0.28f), MintSignal);
            ConfigureChild(controlRing, isControl, new Vector3(0f, 0.42f, 0f), new Vector3(1.04f, 0.04f, 1.04f), TowerMarkerColor(towerId));
            ConfigureChild(relayMast, isRelay, new Vector3(0f, 0.68f, 0f), new Vector3(0.12f, 0.74f, 0.12f), SignalGold);
            ConfigureChild(relayCore, isRelay, new Vector3(0f, 1.08f, 0f), new Vector3(0.26f, 0.26f, 0.26f), SignalGold);
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

        private static void ConfigureCreepRoleMarker(GameObject creepObject, string creepId, int senderId)
        {
            var nose = EnsureChild(creepObject, "RunnerNose", PrimitiveType.Cube);
            var armor = EnsureChild(creepObject, "BruteArmor", PrimitiveType.Cube);
            var swarmA = EnsureChild(creepObject, "SwarmDotA", PrimitiveType.Sphere);
            var swarmB = EnsureChild(creepObject, "SwarmDotB", PrimitiveType.Sphere);
            var hover = EnsureChild(creepObject, "AirHoverRing", PrimitiveType.Cylinder);
            var shimmer = EnsureChild(creepObject, "StealthShimmer", PrimitiveType.Cylinder);
            var spike = EnsureChild(creepObject, "SiegeSpike", PrimitiveType.Cube);

            var isSwarm = ContainsRole(creepId, "swarm");
            var isBrute = ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss");
            var isAir = ContainsRole(creepId, "flying") || ContainsRole(creepId, "air");
            var isStealth = ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth");
            var isSiege = ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege");
            var isRunner = !isSwarm && !isBrute && !isAir && !isStealth && !isSiege;

            ConfigureChild(nose, isRunner, new Vector3(0.24f, 0f, 0f), new Vector3(0.28f, 0.1f, 0.1f), MintSignal);
            ConfigureChild(armor, isBrute, new Vector3(0f, 0.26f, 0f), new Vector3(0.64f, 0.14f, 0.64f), new Color(1f, 0.72f, 0.38f));
            ConfigureChild(swarmA, isSwarm, new Vector3(-0.38f, 0.05f, -0.22f), new Vector3(0.55f, 0.55f, 0.55f), SenderColor(senderId));
            ConfigureChild(swarmB, isSwarm, new Vector3(0.34f, 0.05f, 0.24f), new Vector3(0.45f, 0.45f, 0.45f), MintSignal);
            ConfigureChild(hover, isAir, new Vector3(0f, -0.52f, 0f), new Vector3(0.88f, 0.04f, 0.88f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(shimmer, isStealth, new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.05f, 1.1f), new Color(0.86f, 0.96f, 1f));
            ConfigureChild(spike, isSiege, new Vector3(0.28f, 0.08f, 0f), new Vector3(0.38f, 0.16f, 0.2f), new Color(1f, 0.3f, 0.36f));
        }

        private static GameObject EnsureChild(GameObject parent, string name, PrimitiveType primitiveType)
        {
            var child = parent.transform.Find(name)?.gameObject;
            if (child != null)
            {
                return child;
            }

            child = GameObject.CreatePrimitive(primitiveType);
            child.name = name;
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static void ConfigureChild(GameObject child, bool active, Vector3 localPosition, Vector3 localScale, Color color)
        {
            child.SetActive(active);
            if (!active)
            {
                return;
            }

            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = localScale;
            SetColor(child, color);
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

        private readonly struct CreepMotion
        {
            public CreepMotion(Vector3 positionOffset, Quaternion rotation)
            {
                PositionOffset = positionOffset;
                Rotation = rotation;
            }

            public Vector3 PositionOffset { get; }
            public Quaternion Rotation { get; }
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
