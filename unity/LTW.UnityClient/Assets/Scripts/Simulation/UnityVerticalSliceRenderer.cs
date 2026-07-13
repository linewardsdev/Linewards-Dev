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
        [SerializeField] private LaneCameraFraming cameraFraming = LaneCameraFraming.AllLanes;
        [SerializeField] private int activeLaneCameraId = 1;

        private AudioSource feedbackAudioSource = null!;
        private AudioClip towerBuiltClip = null!;
        private AudioClip towerHitClip = null!;
        private AudioClip sendClip = null!;
        private AudioClip creepKilledClip = null!;
        private AudioClip incomeClip = null!;
        private AudioClip leakClip = null!;
        private AudioClip eliminationClip = null!;

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
            towerHitClip = CreateTone("TowerHitCue", 720f, 0.035f);
            sendClip = CreateTone("SendCue", 440f, 0.06f);
            creepKilledClip = CreateTone("CreepKilledCue", 880f, 0.05f);
            incomeClip = CreateTone("IncomeCue", 1040f, 0.045f);
            leakClip = CreateTone("LeakCue", 180f, 0.14f);
            eliminationClip = CreateTone("EliminationCue", 120f, 0.22f);
            ConfigureDefaultCamera();
        }

        public void SetPresentationDetail(PresentationDetail detail)
        {
            presentationDetail = detail;
            if (detail == PresentationDetail.Disabled)
            {
                ReleaseAllActiveObjects();
            }
        }

        private void ConfigureDefaultCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var clampedLane = Mathf.Clamp(activeLaneCameraId, 1, 3);
            var boardCenter = cameraFraming == LaneCameraFraming.ActiveLane
                ? LaneCenter(clampedLane)
                : new Vector3(LaneOffset(2) + BoardCenterX, 0f, BoardCenterZ);
            camera.orthographic = true;
            camera.orthographicSize = cameraFraming == LaneCameraFraming.ActiveLane ? 9.2f : 11.4f;
            camera.transform.position = boardCenter + new Vector3(0f, 17.5f, cameraFraming == LaneCameraFraming.ActiveLane ? -6.2f : -7.4f);
            camera.transform.LookAt(boardCenter);
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
                CreateLaneEnvironmentTrim(lane);
                CreateLaneFlowTickMarks(lane);
                CreateLaneSurfaceBands(lane);
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
                CreateLaneEndpointBox(lane, CenterColumn, LaneLength - 1, "LifeLossBox", LeakRed);
                CreateLaneFrame(lane);
                CreateLaneFlowCues(lane);
                CreateLaneLandmark(lane, CenterColumn, 0, "Spawn", MintSignal, 0.42f);
                CreateLaneLandmark(lane, CenterColumn, LaneLength - 1, "LifeLoss", LeakRed, 0.5f);
                CreateLaneGate(lane, 0, MintSignal, "ENTRY");
                CreateLaneGate(lane, LaneLength - 1, LeakRed, "LEAK");
                CreateLaneEndpointLabel(lane, CenterColumn, 0, "SPAWN", MintSignal);
                CreateLaneEndpointLabel(lane, CenterColumn, LaneLength - 1, "LIFE LOSS", LeakRed);
                CreateLaneLabel(lane);
                CreateLaneOwnershipBadge(lane);
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
                        var buildPosition = GridToWorld(towerPlaced.Position, towerPlaced.LaneId);
                        SpawnCellFrameCue(buildPosition, MintSignal, 0.28f);
                        SpawnEffect(buildPosition, MintSignal, 0.68f, 0.34f);
                        SpawnFloatingText(buildPosition, "WARD", MintSignal, 0.62f);
                        SpawnReducedEffectCue(buildPosition, "BUILD", MintSignal);
                        PlaySound(towerBuiltClip);
                        break;
                    case TowerSoldEvent towerSold:
                        var sellPosition = PositionFor(towerSold.TowerEntityId.Value.ToString());
                        SpawnCellFrameCue(sellPosition, SignalGold, 0.24f);
                        SpawnEffect(sellPosition, SignalGold, 0.42f, 0.24f);
                        SpawnFloatingText(sellPosition, $"+{towerSold.Refund.Amount}", SignalGold, 0.58f);
                        SpawnReducedEffectCue(sellPosition, "SELL", SignalGold);
                        break;
                    case CreepQueuedEvent queued:
                        SpawnSendCue(queued);
                        break;
                    case CreepSpawnedEvent spawned:
                        var spawnPosition = SpawnPosition(spawned.DefenderId.Value);
                        var spawnColor = CreepRoleColor(spawned.CreepId.Value, spawned.SenderId.Value);
                        SpawnCreepArrivalCue(spawned.DefenderId.Value, spawnColor);
                        SpawnEffect(spawnPosition, spawnColor, 0.52f, 0.28f);
                        SpawnFloatingText(spawnPosition, SpawnLabel(spawned.CreepId.Value), spawnColor, 0.48f);
                        SpawnReducedEffectCue(spawnPosition, "SPAWN", spawnColor);
                        break;
                    case CreepDamagedEvent damaged:
                        var hitPosition = PositionFor(damaged.CreepEntityId.Value.ToString());
                        SpawnBeam(GridToWorld(damaged.TowerPosition, damaged.LaneId) + Vector3.up * 0.35f, hitPosition + Vector3.up * 0.12f, MintSignal, 0.16f);
                        SpawnCreepHitCue(hitPosition, new Color(1f, 0.88f, 0.44f), damaged.DamageDealt);
                        SpawnEffect(hitPosition, new Color(1f, 0.88f, 0.44f), 0.24f, 0.12f);
                        if (damaged.DamageDealt >= 5)
                        {
                            SpawnFloatingText(hitPosition + Vector3.left * 0.32f, damaged.DamageDealt.ToString(), new Color(1f, 0.88f, 0.44f), 0.32f);
                            SpawnReducedEffectCue(hitPosition, "HIT", new Color(1f, 0.88f, 0.44f));
                        }

                        PlaySound(towerHitClip);
                        break;
                    case CreepKilledEvent creepKilled:
                        var killPosition = PositionFor(creepKilled.CreepEntityId.Value.ToString());
                        SpawnCreepDeathCue(killPosition, SignalGold);
                        SpawnEffect(killPosition, SignalGold, 0.42f, 0.2f);
                        SpawnFloatingText(killPosition, $"+{creepKilled.BountyAwarded.Amount}", SignalGold, 0.56f);
                        SpawnReducedEffectCue(killPosition, "KILL", SignalGold);
                        PlaySound(creepKilledClip);
                        break;
                    case LeakEvent leak:
                        var position = PositionFor(leak.CreepEntityId.Value.ToString());
                        SpawnLeakGateCue(leak.DefenderId.Value);
                        SpawnEffect(position, LeakRed, 0.86f, 0.42f);
                        SpawnFloatingText(position, $"-{leak.LivesLost.Amount} LIFE", LeakRed, 0.72f);
                        SpawnReducedEffectCue(position, "LEAK", LeakRed);
                        if (leak.BountyAwarded.Amount > 0)
                        {
                            SpawnFloatingText(position + Vector3.right * 0.55f, $"+{leak.BountyAwarded.Amount}", SignalGold, 0.52f);
                        }

                        PlaySound(leakClip);
                        TriggerHapticFeedback();
                        break;
                    case IncomeTickEvent incomeTick:
                        SpawnIncomeLaneCue(incomeTick.PlayerId.Value);
                        SpawnEffect(IncomePosition(incomeTick.PlayerId.Value), SignalGold, 0.46f, 0.22f);
                        SpawnFloatingText(IncomePosition(incomeTick.PlayerId.Value), $"+{incomeTick.GoldAwarded.Amount} income", SignalGold, 0.58f);
                        SpawnReducedEffectCue(IncomePosition(incomeTick.PlayerId.Value), "INCOME", SignalGold);
                        PlaySound(incomeClip);
                        break;
                    case PlayerEliminatedEvent eliminated:
                        SpawnLaneShutdownCue(eliminated.PlayerId.Value);
                        SpawnEffect(LaneCenter(eliminated.PlayerId.Value) + Vector3.up * 0.2f, LeakRed, 1.15f, 0.55f);
                        SpawnFloatingText(LaneCenter(eliminated.PlayerId.Value) + Vector3.up * 1.2f, $"PLAYER {eliminated.PlayerId.Value} OUT", LeakRed, 0.8f);
                        SpawnReducedEffectCue(LaneCenter(eliminated.PlayerId.Value), "OUT", LeakRed);
                        PlaySound(eliminationClip);
                        break;
                    case MatchEndedEvent ended:
                        SpawnVictoryLaneCue(ended.WinnerId.Value);
                        SpawnFloatingText(LaneCenter(ended.WinnerId.Value) + Vector3.up * 1.85f, $"PLAYER {ended.WinnerId.Value} WINS", SignalGold, 1f);
                        SpawnReducedEffectCue(LaneCenter(ended.WinnerId.Value), "WIN", SignalGold);
                        PlaySound(eliminationClip);
                        break;
                }
            }
        }

        private void SpawnEffect(Vector3 position, Color color) => SpawnEffect(position, color, 0.62f, 0.3f);

        private void SpawnReducedEffectCue(Vector3 position, string label, Color color)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                SpawnFloatingText(position + Vector3.up * 0.18f, label, color, 0.5f);
            }
        }

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

        private void SpawnBeam(Vector3 start, Vector3 end, Color color, float duration)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                return;
            }

            var beam = GetPooled(effectPool, "TowerBeam", PrimitiveType.Cube);
            var midpoint = Vector3.Lerp(start, end, 0.5f);
            var distance = Vector3.Distance(start, end);
            beam.transform.position = midpoint;
            beam.transform.LookAt(end);
            beam.transform.localScale = new Vector3(0.045f, 0.045f, Mathf.Max(0.1f, distance));
            SetColor(beam, color);
            timedPresentations.Add(new TimedPresentation(beam, Time.time + duration, effectPool));
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
            if (!PresentationPreferences.AudioMuted && feedbackAudioSource != null && PresentationPreferences.FeedbackVolume > 0f)
            {
                feedbackAudioSource.PlayOneShot(clip, PresentationPreferences.FeedbackVolume);
            }
        }

        private void SpawnCreepArrivalCue(int laneId, Color color)
        {
            var spawn = SpawnPosition(laneId);
            SpawnCellFrameCue(spawn, color, 0.22f);
            SpawnBeam(spawn + new Vector3(-0.54f, 0.22f, 0.54f), spawn + new Vector3(0.54f, 0.22f, -0.54f), color, 0.18f);
            SpawnBeam(spawn + new Vector3(0.54f, 0.22f, 0.54f), spawn + new Vector3(-0.54f, 0.22f, -0.54f), color, 0.18f);
        }

        private void SpawnCreepHitCue(Vector3 position, Color color, int damage)
        {
            var scale = damage >= 5 ? 0.44f : 0.3f;
            SpawnBeam(position + new Vector3(-scale, 0.2f, 0f), position + new Vector3(scale, 0.2f, 0f), color, 0.1f);
            SpawnBeam(position + new Vector3(0f, 0.2f, -scale), position + new Vector3(0f, 0.2f, scale), color, 0.1f);
        }

        private void SpawnCreepDeathCue(Vector3 position, Color color)
        {
            SpawnBeam(position + new Vector3(-0.38f, 0.16f, 0f), position + new Vector3(0.38f, 0.16f, 0f), color, 0.14f);
            SpawnBeam(position + new Vector3(0f, 0.16f, -0.38f), position + new Vector3(0f, 0.16f, 0.38f), color, 0.14f);
            SpawnBeam(position + new Vector3(-0.24f, 0.22f, -0.24f), position + new Vector3(0.24f, 0.22f, 0.24f), color, 0.14f);
        }

        private void SpawnCellFrameCue(Vector3 center, Color color, float duration)
        {
            var northWest = center + new Vector3(-0.48f, 0.18f, 0.48f);
            var northEast = center + new Vector3(0.48f, 0.18f, 0.48f);
            var southWest = center + new Vector3(-0.48f, 0.18f, -0.48f);
            var southEast = center + new Vector3(0.48f, 0.18f, -0.48f);
            SpawnBeam(northWest, northEast, color, duration);
            SpawnBeam(southWest, southEast, color, duration);
            SpawnBeam(northWest, southWest, color, duration);
            SpawnBeam(northEast, southEast, color, duration);
        }

        private void SpawnLeakGateCue(int laneId)
        {
            var offset = LaneOffset(laneId);
            var gateCenter = GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(laneId)) + Vector3.up * 0.24f;
            SpawnEffect(gateCenter, LeakRed, 0.72f, 0.34f);
            SpawnBeam(new Vector3(offset + 0.7f, 0.48f, WorldZ(LaneLength - 1)), new Vector3(offset + LaneWidth - 1.7f, 0.48f, WorldZ(LaneLength - 1)), LeakRed, 0.3f);
        }

        private void SpawnIncomeLaneCue(int laneId)
        {
            var offset = LaneOffset(laneId);
            var west = new Vector3(offset + 0.85f, 0.42f, WorldZ(1));
            var east = new Vector3(offset + LaneWidth - 1.85f, 0.42f, WorldZ(1));
            SpawnEffect(LaneCenter(laneId) + Vector3.up * 0.18f, SignalGold, 0.34f, 0.18f);
            SpawnBeam(west, east, SignalGold, 0.2f);
        }

        private void SpawnLaneShutdownCue(int laneId)
        {
            var offset = LaneOffset(laneId);
            var southwest = new Vector3(offset + 0.55f, 0.52f, 0.45f);
            var northeast = new Vector3(offset + LaneWidth - 1.55f, 0.52f, LaneLength - 0.45f);
            var northwest = new Vector3(offset + 0.55f, 0.52f, LaneLength - 0.45f);
            var southeast = new Vector3(offset + LaneWidth - 1.55f, 0.52f, 0.45f);
            SpawnBeam(southwest, northeast, LeakRed, 0.48f);
            SpawnBeam(northwest, southeast, LeakRed, 0.48f);
        }

        private void SpawnVictoryLaneCue(int laneId)
        {
            var center = LaneCenter(laneId);
            var offset = LaneOffset(laneId);
            SpawnEffect(center + Vector3.up * 0.38f, SignalGold, 1.05f, 0.45f);
            SpawnBeam(new Vector3(offset + 0.65f, 0.5f, BoardCenterZ), new Vector3(offset + LaneWidth - 1.65f, 0.5f, BoardCenterZ), SignalGold, 0.42f);
            SpawnBeam(new Vector3(offset + BoardCenterX, 0.5f, 0.65f), new Vector3(offset + BoardCenterX, 0.5f, LaneLength - 0.65f), SignalGold, 0.42f);
        }

        private void SpawnSendCue(CreepQueuedEvent queued)
        {
            var senderPosition = GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(queued.SenderId.Value)) + Vector3.up * 0.2f;
            var defenderPosition = SpawnPosition(queued.DefenderId.Value);
            var color = CreepRoleColor(queued.CreepId.Value, queued.SenderId.Value);
            SpawnEffect(senderPosition, color, 0.44f, 0.24f);
            SpawnEffect(LaneCenter(queued.SenderId.Value), color, 0.36f, 0.18f);
            SpawnBeam(senderPosition + Vector3.up * 0.18f, defenderPosition + Vector3.up * 0.18f, color, 0.22f);
            SpawnEffect(defenderPosition, color, 0.54f, 0.3f);
            SpawnEffect(LaneCenter(queued.DefenderId.Value), color, 0.42f, 0.2f);
            SpawnFloatingText(senderPosition + Vector3.left * 0.42f, "SEND", color, 0.42f);
            SpawnFloatingText(defenderPosition, $"{queued.Quantity}x {SpawnLabel(queued.CreepId.Value)}", color, 0.56f);
            SpawnReducedEffectCue(defenderPosition, "SEND", color);
            PlaySound(sendClip);
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

            if (ContainsRole(creepId, "aura") || ContainsRole(creepId, "support"))
            {
                return "AURA";
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
            instance.transform.position = GridToWorld(position, laneId) + Vector3.up * TowerRoleLift(towerId);
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
            SetColor(backplate, LaneBackplateColor(laneId));
            laneDecorations.Add(backplate);
        }

        private void CreateLaneEnvironmentTrim(int laneId)
        {
            var offset = LaneOffset(laneId);
            var accent = OwnerAccent(laneId);
            var gutterColor = LaneGutterColor(laneId);
            var focusScale = laneId == 1 ? 1.18f : 0.92f;

            CreateSurfaceBand($"Lane{laneId}WestGutter", new Vector3(offset - 0.62f, -0.245f, BoardCenterZ), new Vector3(0.34f, 0.05f, LaneLength + 0.9f), gutterColor);
            CreateSurfaceBand($"Lane{laneId}EastGutter", new Vector3(offset + LaneWidth - 0.38f, -0.245f, BoardCenterZ), new Vector3(0.34f, 0.05f, LaneLength + 0.9f), gutterColor);
            CreateSurfaceBand($"Lane{laneId}NorthAnchor", new Vector3(offset + BoardCenterX, -0.238f, LaneLength + 0.32f), new Vector3(LaneWidth * 0.62f, 0.055f, 0.24f), LaneAnchorColor(accent, laneId == 1));
            CreateSurfaceBand($"Lane{laneId}SouthAnchor", new Vector3(offset + BoardCenterX, -0.238f, -0.32f), new Vector3(LaneWidth * 0.62f, 0.055f, 0.24f), LaneAnchorColor(accent, laneId == 1));

            CreateCornerPylon(laneId, "NorthWest", new Vector3(offset - 0.64f, -0.08f, LaneLength + 0.25f), accent, focusScale);
            CreateCornerPylon(laneId, "NorthEast", new Vector3(offset + LaneWidth - 0.36f, -0.08f, LaneLength + 0.25f), accent, focusScale);
            CreateCornerPylon(laneId, "SouthWest", new Vector3(offset - 0.64f, -0.08f, -0.25f), accent, focusScale);
            CreateCornerPylon(laneId, "SouthEast", new Vector3(offset + LaneWidth - 0.36f, -0.08f, -0.25f), accent, focusScale);
        }

        private void CreateCornerPylon(int laneId, string name, Vector3 position, Color color, float focusScale)
        {
            var pylon = CreatePrimitive($"Lane{laneId}{name}Pylon", PrimitiveType.Cube);
            pylon.transform.position = position;
            pylon.transform.localScale = new Vector3(0.22f * focusScale, 0.38f * focusScale, 0.22f * focusScale);
            SetColor(pylon, color);
            laneDecorations.Add(pylon);
        }

        private void CreateLaneFlowTickMarks(int laneId)
        {
            var offset = LaneOffset(laneId);
            var color = LaneTickColor(OwnerAccent(laneId), laneId == 1);

            for (var y = 2; y < LaneLength - 1; y += 3)
            {
                var z = WorldZ(y);
                CreateFlowTick(laneId, $"WestTick{y}", new Vector3(offset - 0.58f, -0.16f, z), color, -18f, laneId == 1);
                CreateFlowTick(laneId, $"EastTick{y}", new Vector3(offset + LaneWidth - 0.42f, -0.16f, z), color, 18f, laneId == 1);
            }
        }

        private void CreateFlowTick(int laneId, string name, Vector3 position, Color color, float rotationY, bool isPlayerLane)
        {
            var tick = CreatePrimitive($"Lane{laneId}{name}", PrimitiveType.Cube);
            tick.transform.position = position;
            tick.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
            tick.transform.localScale = new Vector3(isPlayerLane ? 0.1f : 0.075f, 0.055f, isPlayerLane ? 0.42f : 0.32f);
            SetColor(tick, color);
            laneDecorations.Add(tick);
        }

        private void CreateLaneSurfaceBands(int laneId)
        {
            var offset = LaneOffset(laneId);
            var accent = OwnerAccent(laneId);

            CreateSurfaceBand($"Lane{laneId}LeftBuildBand", new Vector3(offset + 1f, -0.255f, BoardCenterZ), new Vector3(1.82f, 0.035f, LaneLength - 1.2f), BuildZoneColor(accent, laneId == 1));
            CreateSurfaceBand($"Lane{laneId}RightBuildBand", new Vector3(offset + 5f, -0.255f, BoardCenterZ), new Vector3(1.82f, 0.035f, LaneLength - 1.2f), BuildZoneColor(accent, laneId == 1));
            CreateSurfaceBand($"Lane{laneId}CenterRouteBand", new Vector3(offset + CenterColumn, -0.248f, BoardCenterZ), new Vector3(1.08f, 0.038f, LaneLength - 0.55f), RouteBandColor(laneId));
            CreateSurfaceBand($"Lane{laneId}NorthFlowWash", new Vector3(offset + BoardCenterX, -0.252f, LaneLength - 2.25f), new Vector3(LaneWidth - 0.7f, 0.032f, 2.2f), EndpointWashColor(SignalGold, laneId == 1));
            CreateSurfaceBand($"Lane{laneId}SouthFlowWash", new Vector3(offset + BoardCenterX, -0.252f, 1.25f), new Vector3(LaneWidth - 0.7f, 0.032f, 2.2f), EndpointWashColor(MintSignal, laneId == 1));
        }

        private void CreateSurfaceBand(string name, Vector3 position, Vector3 scale, Color color)
        {
            var band = CreatePrimitive(name, PrimitiveType.Cube);
            band.transform.position = position;
            band.transform.localScale = scale;
            SetColor(band, color);
            laneDecorations.Add(band);
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

            var halo = CreatePrimitive($"Lane{laneId}{landmarkName}Halo", PrimitiveType.Cylinder);
            halo.transform.position = GridToWorld(new GridPosition(x, y), new LaneId(laneId)) + Vector3.down * 0.31f;
            halo.transform.localScale = new Vector3(scale * 1.95f, 0.045f, scale * 1.95f);
            SetColor(halo, EndpointWashColor(color, laneId == 1));
            laneDecorations.Add(halo);
        }

        private void CreateLaneGate(int laneId, int y, Color color, string label)
        {
            var offset = LaneOffset(laneId);
            var z = WorldZ(y);
            CreateSurfaceBand($"Lane{laneId}{label}GateCrossbar", new Vector3(offset + BoardCenterX, -0.16f, z), new Vector3(LaneWidth - 1.08f, 0.09f, 0.13f), color);
            CreateSurfaceBand($"Lane{laneId}{label}GateLeftPost", new Vector3(offset + 0.58f, 0.02f, z), new Vector3(0.16f, 0.46f, 0.16f), color);
            CreateSurfaceBand($"Lane{laneId}{label}GateRightPost", new Vector3(offset + LaneWidth - 1.58f, 0.02f, z), new Vector3(0.16f, 0.46f, 0.16f), color);
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

        private void CreateLaneOwnershipBadge(int laneId)
        {
            var badgeObject = new GameObject($"Lane{laneId}OwnershipBadge");
            badgeObject.transform.position = new Vector3(LaneOffset(laneId) - 0.85f, 0.08f, BoardCenterZ);
            badgeObject.transform.rotation = Quaternion.Euler(90f, 0f, 90f);
            badgeObject.transform.localScale = Vector3.one * (laneId == 1 ? 0.042f : 0.034f);
            var badge = badgeObject.AddComponent<TextMesh>();
            badge.anchor = TextAnchor.MiddleCenter;
            badge.alignment = TextAlignment.Center;
            badge.fontSize = 44;
            badge.characterSize = 0.18f;
            badge.text = laneId == 1 ? "YOUR LINE" : $"TARGET {laneId}";
            badge.color = OwnerAccent(laneId);
            laneDecorations.Add(badgeObject);

            CreateSurfaceBand($"Lane{laneId}OwnershipBadgeRail", new Vector3(LaneOffset(laneId) - 0.88f, -0.18f, BoardCenterZ), new Vector3(0.1f, 0.08f, LaneLength * 0.45f), LaneAnchorColor(OwnerAccent(laneId), laneId == 1));
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
                return laneId == 1 ? new Color(0.13f, 0.32f, 0.48f) : new Color(0.09f, 0.21f, 0.34f);
            }

            var checker = (x + y + laneId) % 2 == 0 ? 0.026f : 0f;
            var laneTint = laneId == 1 ? 0.025f : 0f;
            var buildColumn = x < CenterColumn ? 0.012f : 0.024f;
            return new Color(0.058f + checker + laneTint + buildColumn, 0.082f + checker + laneTint, 0.14f + checker + laneTint);
        }

        private static Vector3 TowerRoleScale(string towerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control"))
            {
                return new Vector3(0.92f, 0.34f, 0.92f);
            }

            if (ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay"))
            {
                return new Vector3(0.46f, 0.92f, 0.46f);
            }

            return new Vector3(0.48f, 1.08f, 0.48f);
        }

        private static float TowerRoleLift(string towerId)
        {
            if (ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay"))
            {
                return 0.18f;
            }

            return 0.12f;
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

            if (ContainsRole(creepId, "aura") || ContainsRole(creepId, "support"))
            {
                return new Vector3(0.52f, 0.34f, 0.52f);
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

            if (ContainsRole(creepId, "aura") || ContainsRole(creepId, "support"))
            {
                return new Color(0.42f, 1f, 0.72f);
            }

            return SenderColor(senderId);
        }

        private static void ConfigureTowerRoleMarker(GameObject towerObject, string towerId)
        {
            var basePlate = EnsureChild(towerObject, "RoleBasePlate", PrimitiveType.Cylinder);
            ConfigureChild(basePlate, true, new Vector3(0f, -0.26f, 0f), TowerBaseScale(towerId), TowerBaseColor(towerId));

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
            var focusedSpire = EnsureChild(towerObject, "FocusedSpire", PrimitiveType.Cube);
            var focusedSightLine = EnsureChild(towerObject, "FocusedSightLine", PrimitiveType.Cube);
            var focusedLeftVane = EnsureChild(towerObject, "FocusedLeftVane", PrimitiveType.Cube);
            var focusedRightVane = EnsureChild(towerObject, "FocusedRightVane", PrimitiveType.Cube);
            var controlRing = EnsureChild(towerObject, "ControlRing", PrimitiveType.Cylinder);
            var controlDish = EnsureChild(towerObject, "ControlDish", PrimitiveType.Cylinder);
            var controlNodeA = EnsureChild(towerObject, "ControlNodeA", PrimitiveType.Sphere);
            var controlNodeB = EnsureChild(towerObject, "ControlNodeB", PrimitiveType.Sphere);
            var controlNodeC = EnsureChild(towerObject, "ControlNodeC", PrimitiveType.Sphere);
            var relayMast = EnsureChild(towerObject, "RelayMast", PrimitiveType.Cube);
            var relayCore = EnsureChild(towerObject, "RelayCore", PrimitiveType.Sphere);
            var relayCapacitorLeft = EnsureChild(towerObject, "RelayCapacitorLeft", PrimitiveType.Cube);
            var relayCapacitorRight = EnsureChild(towerObject, "RelayCapacitorRight", PrimitiveType.Cube);
            var relaySignalTop = EnsureChild(towerObject, "RelaySignalTop", PrimitiveType.Cylinder);
            var rangeHalo = EnsureChild(towerObject, "RangeReadHalo", PrimitiveType.Cylinder);

            var isControl = ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control") || ContainsRole(towerId, "area");
            var isRelay = ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay");
            var isFocused = !isControl && !isRelay;

            ConfigureChild(lens, isFocused, new Vector3(0f, 1.08f, 0f), new Vector3(0.24f, 0.24f, 0.42f), MintSignal);
            ConfigureChild(focusedSpire, isFocused, new Vector3(0f, 0.64f, 0f), new Vector3(0.16f, 0.92f, 0.16f), TowerMarkerColor(towerId));
            ConfigureChild(focusedSightLine, isFocused, new Vector3(0f, 0.98f, 0.32f), new Vector3(0.12f, 0.08f, 0.62f), MintSignal);
            ConfigureChild(focusedLeftVane, isFocused, new Vector3(-0.26f, 0.36f, -0.02f), new Vector3(0.1f, 0.34f, 0.18f), TowerBaseColor(towerId));
            ConfigureChild(focusedRightVane, isFocused, new Vector3(0.26f, 0.36f, -0.02f), new Vector3(0.1f, 0.34f, 0.18f), TowerBaseColor(towerId));

            ConfigureChild(controlRing, isControl, new Vector3(0f, 0.34f, 0f), new Vector3(1.24f, 0.035f, 1.24f), TowerMarkerColor(towerId));
            ConfigureChild(controlDish, isControl, new Vector3(0f, 0.68f, 0f), new Vector3(1.02f, 0.04f, 1.02f), TowerBaseColor(towerId));
            ConfigureChild(controlNodeA, isControl, new Vector3(0f, 0.76f, 0.46f), new Vector3(0.18f, 0.18f, 0.18f), TowerMarkerColor(towerId));
            ConfigureChild(controlNodeB, isControl, new Vector3(-0.4f, 0.76f, -0.26f), new Vector3(0.16f, 0.16f, 0.16f), TowerMarkerColor(towerId));
            ConfigureChild(controlNodeC, isControl, new Vector3(0.4f, 0.76f, -0.26f), new Vector3(0.16f, 0.16f, 0.16f), TowerMarkerColor(towerId));

            ConfigureChild(relayMast, isRelay, new Vector3(0f, 0.72f, 0f), new Vector3(0.1f, 0.9f, 0.1f), SignalGold);
            ConfigureChild(relayCore, isRelay, new Vector3(0f, 1.24f, 0f), new Vector3(0.28f, 0.28f, 0.28f), SignalGold);
            ConfigureChild(relayCapacitorLeft, isRelay, new Vector3(-0.26f, 0.54f, 0f), new Vector3(0.12f, 0.48f, 0.12f), TowerBaseColor(towerId));
            ConfigureChild(relayCapacitorRight, isRelay, new Vector3(0.26f, 0.54f, 0f), new Vector3(0.12f, 0.48f, 0.12f), TowerBaseColor(towerId));
            ConfigureChild(relaySignalTop, isRelay, new Vector3(0f, 1.48f, 0f), new Vector3(0.48f, 0.045f, 0.48f), MintSignal);

            ConfigureChild(rangeHalo, true, new Vector3(0f, -0.22f, 0f), TowerRangeHaloScale(towerId), TowerMarkerColor(towerId));
        }

        private static Vector3 TowerBaseScale(string towerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control") || ContainsRole(towerId, "area"))
            {
                return new Vector3(1.18f, 0.055f, 1.18f);
            }

            if (ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay"))
            {
                return new Vector3(0.78f, 0.06f, 0.78f);
            }

            return new Vector3(0.72f, 0.06f, 0.72f);
        }

        private static Vector3 TowerRangeHaloScale(string towerId)
        {
            if (ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay"))
            {
                return new Vector3(1.45f, 0.018f, 1.45f);
            }

            return new Vector3(1.88f, 0.018f, 1.88f);
        }

        private static Color TowerBaseColor(string towerId)
        {
            var marker = TowerMarkerColor(towerId);
            return new Color(marker.r * 0.45f, marker.g * 0.45f, marker.b * 0.45f);
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
            var shadow = EnsureChild(creepObject, "GroundShadow", PrimitiveType.Cylinder);
            ConfigureChild(shadow, true, new Vector3(0f, -0.42f, 0f), CreepShadowScale(creepId), new Color(0.015f, 0.022f, 0.035f));

            var nose = EnsureChild(creepObject, "RunnerNose", PrimitiveType.Cube);
            var runnerTail = EnsureChild(creepObject, "RunnerTail", PrimitiveType.Cube);
            var runnerLeftFin = EnsureChild(creepObject, "RunnerLeftFin", PrimitiveType.Cube);
            var runnerRightFin = EnsureChild(creepObject, "RunnerRightFin", PrimitiveType.Cube);
            var armor = EnsureChild(creepObject, "BruteArmor", PrimitiveType.Cube);
            var bruteLeftPlate = EnsureChild(creepObject, "BruteLeftPlate", PrimitiveType.Cube);
            var bruteRightPlate = EnsureChild(creepObject, "BruteRightPlate", PrimitiveType.Cube);
            var bruteCore = EnsureChild(creepObject, "BruteCore", PrimitiveType.Sphere);
            var bossCrown = EnsureChild(creepObject, "BossCrown", PrimitiveType.Cylinder);
            var bossCore = EnsureChild(creepObject, "BossCore", PrimitiveType.Sphere);
            var bossLeftHorn = EnsureChild(creepObject, "BossLeftHorn", PrimitiveType.Cube);
            var bossRightHorn = EnsureChild(creepObject, "BossRightHorn", PrimitiveType.Cube);
            var swarmA = EnsureChild(creepObject, "SwarmDotA", PrimitiveType.Sphere);
            var swarmB = EnsureChild(creepObject, "SwarmDotB", PrimitiveType.Sphere);
            var swarmC = EnsureChild(creepObject, "SwarmDotC", PrimitiveType.Sphere);
            var swarmTrail = EnsureChild(creepObject, "SwarmTrail", PrimitiveType.Cylinder);
            var hover = EnsureChild(creepObject, "AirHoverRing", PrimitiveType.Cylinder);
            var airLeftWing = EnsureChild(creepObject, "AirLeftWing", PrimitiveType.Cube);
            var airRightWing = EnsureChild(creepObject, "AirRightWing", PrimitiveType.Cube);
            var airBeacon = EnsureChild(creepObject, "AirBeacon", PrimitiveType.Sphere);
            var shimmer = EnsureChild(creepObject, "StealthShimmer", PrimitiveType.Cylinder);
            var stealthEchoA = EnsureChild(creepObject, "StealthEchoA", PrimitiveType.Cylinder);
            var stealthEchoB = EnsureChild(creepObject, "StealthEchoB", PrimitiveType.Cylinder);
            var siegeBase = EnsureChild(creepObject, "SiegeBase", PrimitiveType.Cube);
            var siegeBarrel = EnsureChild(creepObject, "SiegeBarrel", PrimitiveType.Cube);
            var siegeSpike = EnsureChild(creepObject, "SiegeSpike", PrimitiveType.Cube);
            var auraField = EnsureChild(creepObject, "AuraField", PrimitiveType.Cylinder);
            var auraCore = EnsureChild(creepObject, "AuraCore", PrimitiveType.Sphere);
            var auraNorthNode = EnsureChild(creepObject, "AuraNorthNode", PrimitiveType.Sphere);
            var auraSouthNode = EnsureChild(creepObject, "AuraSouthNode", PrimitiveType.Sphere);

            var isSwarm = ContainsRole(creepId, "swarm");
            var isBoss = ContainsRole(creepId, "boss");
            var isBrute = isBoss || ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank");
            var isAir = ContainsRole(creepId, "flying") || ContainsRole(creepId, "air");
            var isStealth = ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth");
            var isSiege = ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege");
            var isAura = ContainsRole(creepId, "aura") || ContainsRole(creepId, "support");
            var isRunner = !isSwarm && !isBrute && !isAir && !isStealth && !isSiege && !isAura;
            var senderColor = SenderColor(senderId);

            ConfigureChild(nose, isRunner, new Vector3(0f, 0.02f, 0.42f), new Vector3(0.16f, 0.1f, 0.34f), MintSignal);
            ConfigureChild(runnerTail, isRunner, new Vector3(0f, -0.02f, -0.34f), new Vector3(0.1f, 0.08f, 0.28f), senderColor);
            ConfigureChild(runnerLeftFin, isRunner, new Vector3(-0.24f, 0f, -0.04f), new Vector3(0.08f, 0.08f, 0.26f), senderColor);
            ConfigureChild(runnerRightFin, isRunner, new Vector3(0.24f, 0f, -0.04f), new Vector3(0.08f, 0.08f, 0.26f), senderColor);

            ConfigureChild(armor, isBrute, new Vector3(0f, 0.26f, 0f), isBoss ? new Vector3(0.86f, 0.18f, 0.96f) : new Vector3(0.72f, 0.14f, 0.84f), new Color(1f, 0.72f, 0.38f));
            ConfigureChild(bruteLeftPlate, isBrute, new Vector3(-0.38f, 0.12f, 0.04f), new Vector3(0.18f, 0.28f, 0.62f), new Color(0.74f, 0.38f, 0.22f));
            ConfigureChild(bruteRightPlate, isBrute, new Vector3(0.38f, 0.12f, 0.04f), new Vector3(0.18f, 0.28f, 0.62f), new Color(0.74f, 0.38f, 0.22f));
            ConfigureChild(bruteCore, isBrute && !isBoss, new Vector3(0f, 0.42f, 0.18f), new Vector3(0.22f, 0.22f, 0.22f), SignalGold);
            ConfigureChild(bossCrown, isBoss, new Vector3(0f, 0.72f, 0f), new Vector3(0.92f, 0.055f, 0.92f), LeakRed);
            ConfigureChild(bossCore, isBoss, new Vector3(0f, 0.54f, 0.16f), new Vector3(0.34f, 0.34f, 0.34f), SignalGold);
            ConfigureChild(bossLeftHorn, isBoss, new Vector3(-0.44f, 0.62f, 0.16f), new Vector3(0.16f, 0.16f, 0.42f), LeakRed);
            ConfigureChild(bossRightHorn, isBoss, new Vector3(0.44f, 0.62f, 0.16f), new Vector3(0.16f, 0.16f, 0.42f), LeakRed);

            ConfigureChild(swarmA, isSwarm, new Vector3(-0.42f, 0.05f, -0.24f), new Vector3(0.62f, 0.62f, 0.62f), senderColor);
            ConfigureChild(swarmB, isSwarm, new Vector3(0.38f, 0.05f, 0.26f), new Vector3(0.52f, 0.52f, 0.52f), MintSignal);
            ConfigureChild(swarmC, isSwarm, new Vector3(0.08f, 0.08f, -0.48f), new Vector3(0.44f, 0.44f, 0.44f), new Color(0.75f, 0.95f, 1f));
            ConfigureChild(swarmTrail, isSwarm, new Vector3(0f, -0.18f, 0f), new Vector3(0.82f, 0.03f, 0.82f), senderColor);

            ConfigureChild(hover, isAir, new Vector3(0f, -0.52f, 0f), new Vector3(0.88f, 0.04f, 0.88f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(airLeftWing, isAir, new Vector3(-0.5f, 0.02f, 0f), new Vector3(0.42f, 0.08f, 0.18f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(airRightWing, isAir, new Vector3(0.5f, 0.02f, 0f), new Vector3(0.42f, 0.08f, 0.18f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(airBeacon, isAir, new Vector3(0f, 0.28f, 0f), new Vector3(0.2f, 0.2f, 0.2f), MintSignal);

            ConfigureChild(shimmer, isStealth, new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.05f, 1.1f), new Color(0.86f, 0.96f, 1f));
            ConfigureChild(stealthEchoA, isStealth, new Vector3(0f, -0.18f, 0f), new Vector3(1.34f, 0.03f, 1.34f), new Color(0.36f, 0.5f, 0.58f));
            ConfigureChild(stealthEchoB, isStealth, new Vector3(0f, 0.2f, 0f), new Vector3(0.78f, 0.03f, 0.78f), new Color(0.72f, 0.84f, 0.9f));

            ConfigureChild(siegeBase, isSiege, new Vector3(0f, -0.02f, -0.06f), new Vector3(0.58f, 0.22f, 0.5f), new Color(0.56f, 0.12f, 0.16f));
            ConfigureChild(siegeBarrel, isSiege, new Vector3(0f, 0.08f, 0.42f), new Vector3(0.18f, 0.16f, 0.62f), new Color(1f, 0.38f, 0.44f));
            ConfigureChild(siegeSpike, isSiege, new Vector3(0.28f, 0.08f, 0f), new Vector3(0.38f, 0.16f, 0.2f), new Color(1f, 0.3f, 0.36f));

            ConfigureChild(auraField, isAura, new Vector3(0f, -0.34f, 0f), new Vector3(1.42f, 0.035f, 1.42f), new Color(0.42f, 1f, 0.72f));
            ConfigureChild(auraCore, isAura, new Vector3(0f, 0.24f, 0f), new Vector3(0.28f, 0.28f, 0.28f), MintSignal);
            ConfigureChild(auraNorthNode, isAura, new Vector3(0f, 0.02f, 0.46f), new Vector3(0.18f, 0.18f, 0.18f), SignalGold);
            ConfigureChild(auraSouthNode, isAura, new Vector3(0f, 0.02f, -0.46f), new Vector3(0.18f, 0.18f, 0.18f), SignalGold);
        }

        private static Vector3 CreepShadowScale(string creepId)
        {
            if (ContainsRole(creepId, "swarm"))
            {
                return new Vector3(1.2f, 0.025f, 1.2f);
            }

            if (ContainsRole(creepId, "boss"))
            {
                return new Vector3(1.36f, 0.025f, 1.48f);
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank"))
            {
                return new Vector3(1.05f, 0.025f, 1.25f);
            }

            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                return new Vector3(0.92f, 0.02f, 0.92f);
            }

            if (ContainsRole(creepId, "aura") || ContainsRole(creepId, "support"))
            {
                return new Vector3(1.38f, 0.02f, 1.38f);
            }

            return new Vector3(0.72f, 0.025f, 1.05f);
        }

        private static Color LaneBackplateColor(int laneId)
        {
            return laneId == 1 ? new Color(0.045f, 0.105f, 0.19f) : new Color(0.038f, 0.052f, 0.095f);
        }

        private static Color LaneGutterColor(int laneId) => laneId == 1 ? new Color(0.032f, 0.078f, 0.14f) : new Color(0.026f, 0.036f, 0.07f);

        private static Color LaneAnchorColor(Color accent, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.34f : 0.18f;
            return new Color(accent.r * strength, accent.g * strength, accent.b * strength);
        }

        private static Color LaneTickColor(Color accent, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.5f : 0.26f;
            return new Color(accent.r * strength, accent.g * strength, accent.b * strength);
        }

        private static Color BuildZoneColor(Color tint, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.16f : 0.09f;
            return new Color(0.045f + tint.r * strength, 0.058f + tint.g * strength, 0.09f + tint.b * strength);
        }

        private static Color RouteBandColor(int laneId) => laneId == 1 ? new Color(0.11f, 0.34f, 0.52f) : new Color(0.08f, 0.19f, 0.32f);

        private static Color EndpointWashColor(Color color, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.36f : 0.24f;
            return new Color(color.r * strength, color.g * strength, color.b * strength);
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
        private static readonly Color LeakRed = new Color(1f, 0.32f, 0.24f);

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

    public enum LaneCameraFraming
    {
        AllLanes,
        ActiveLane,
    }
}
