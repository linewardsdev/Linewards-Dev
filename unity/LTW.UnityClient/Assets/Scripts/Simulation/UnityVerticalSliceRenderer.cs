using System;
using System.Collections.Generic;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Projects simulation snapshots and events into pooled Unity objects. This class never
    /// changes simulation state; it is safe to disable it for low-spec or diagnostic runs.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class UnityVerticalSliceRenderer : MonoBehaviour
    {
        private const int LaneWidth = 7;
        private const int LaneLength = 16;
        private const int LaneSpacing = 9;
        private const int CenterColumn = 3;
        private const float BoardCenterX = (LaneWidth - 1) * 0.5f;
        private const float BoardCenterZ = (LaneLength - 1) * 0.5f;
        private const string PrimitiveTowerPoolKey = "primitive-tower";
        private const string PrimitiveCreepPoolKey = "primitive-creep";
        private const string DefaultTowerVisualLibraryResourcePath = "TowerVisualLibrary";
        private const string DefaultCreepVisualLibraryResourcePath = "CreepVisualLibrary";

        [SerializeField] private UnitySimulationDriver simulationDriver = null!;
        [SerializeField] private PresentationDetail presentationDetail = PresentationDetail.Full;
        [SerializeField] private LaneCameraFraming cameraFraming = LaneCameraFraming.ActiveLane;
        [SerializeField] private int activeLaneCameraId = 1;
        [SerializeField] private TowerVisualLibrary towerVisualLibrary = null!;
        [SerializeField] private CreepVisualLibrary creepVisualLibrary = null!;
        [SerializeField] private bool suppressCreepGameplayOverlays;

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
        private readonly Dictionary<string, string> activeTowerPoolKeys = new Dictionary<string, string>();
        private readonly Dictionary<string, string> activeCreepPoolKeys = new Dictionary<string, string>();
        private readonly Dictionary<string, Vector3> lastKnownPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, string> lastKnownCreepIds = new Dictionary<string, string>();
        private readonly Dictionary<string, string> towerRolesByCell = new Dictionary<string, string>();
        private readonly Dictionary<string, int> lastCreepHealth = new Dictionary<string, int>();
        private readonly Dictionary<string, float> creepHitFlashUntil = new Dictionary<string, float>();
        private readonly Dictionary<int, GameObject> lanePressureMeters = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> lanePressureCaps = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, TextMesh> lanePressureLabels = new Dictionary<int, TextMesh>();
        private readonly List<GameObject> laneCells = new List<GameObject>();
        private readonly List<GameObject> laneDecorations = new List<GameObject>();
        private readonly HashSet<string> visibleKeys = new HashSet<string>();
        private readonly Queue<GameObject> towerPool = new Queue<GameObject>();
        private readonly Queue<GameObject> creepPool = new Queue<GameObject>();
        private readonly Dictionary<string, Queue<GameObject>> towerPrefabPools = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, Queue<GameObject>> creepPrefabPools = new Dictionary<string, Queue<GameObject>>();
        private readonly Queue<GameObject> effectPool = new Queue<GameObject>();
        private readonly Queue<GameObject> textPool = new Queue<GameObject>();
        private readonly List<TimedPresentation> timedPresentations = new List<TimedPresentation>();

        private Camera presentationCamera = null!;
        private bool laneCreated;

        public PresentationDetail Detail => presentationDetail;

        public LaneCameraFraming CameraFraming => cameraFraming;

        public int ActiveLaneCameraId => Mathf.Clamp(activeLaneCameraId, 1, 3);

        public int ActivePresentationObjectCount => activeTowers.Count + activeCreeps.Count + timedPresentations.Count;

        public int PooledPresentationObjectCount => towerPool.Count + PooledTowerPrefabCount() + creepPool.Count + PooledCreepPrefabCount() + effectPool.Count + textPool.Count;

        public void Initialize(UnitySimulationDriver driver)
        {
            simulationDriver = driver;
        }

        public void SetPresentationCamera(Camera camera)
        {
            presentationCamera = camera;
            ConfigureDefaultCamera();
        }

        private void Awake()
        {
            if (towerVisualLibrary == null)
            {
                towerVisualLibrary = Resources.Load<TowerVisualLibrary>(DefaultTowerVisualLibraryResourcePath);
            }

            if (creepVisualLibrary == null)
            {
                creepVisualLibrary = Resources.Load<CreepVisualLibrary>(DefaultCreepVisualLibraryResourcePath);
            }

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

        public void ToggleCameraFraming()
        {
            SetActiveLaneCameraId(ActiveLaneCameraId % 3 + 1);
        }

        public void SetCameraFraming(LaneCameraFraming framing)
        {
            if (cameraFraming != framing)
            {
                Debug.Log($"LTW camera framing -> {framing}");
            }

            cameraFraming = framing;
            ConfigureDefaultCamera();
        }

        public void SetActiveLaneCameraId(int laneId)
        {
            var nextLane = Mathf.Clamp(laneId, 1, 3);
            if (activeLaneCameraId != nextLane || cameraFraming != LaneCameraFraming.ActiveLane)
            {
                Debug.Log($"LTW active lane camera -> {nextLane}");
            }

            activeLaneCameraId = nextLane;
            cameraFraming = LaneCameraFraming.ActiveLane;
            ConfigureDefaultCamera();
        }

        private void ConfigureDefaultCamera()
        {
            var camera = presentationCamera != null ? presentationCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            var clampedLane = Mathf.Clamp(activeLaneCameraId, 1, 3);
            var boardCenter = cameraFraming switch
            {
                LaneCameraFraming.AllLanes => new Vector3(LaneOffset(2) + BoardCenterX, 0f, BoardCenterZ),
                LaneCameraFraming.BoardOverview => LaneCenter(clampedLane),
                LaneCameraFraming.SpawnGateFocus => GridToWorld(new GridPosition(CenterColumn, 0), new LaneId(clampedLane)) + new Vector3(0f, 0f, -1.15f),
                LaneCameraFraming.LeakGateFocus => GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(clampedLane)) + new Vector3(0f, 0f, 1.15f),
                _ => LaneCenter(clampedLane)
            };
            camera.orthographic = true;
            camera.orthographicSize = cameraFraming switch
            {
                LaneCameraFraming.AllLanes => 11.4f,
                LaneCameraFraming.BoardOverview => 8.2f,
                LaneCameraFraming.SpawnGateFocus => 3.05f,
                LaneCameraFraming.LeakGateFocus => 3.05f,
                _ => 9.2f
            };
            camera.rect = cameraFraming == LaneCameraFraming.ActiveLane
                ? MobileViewportLayout.CameraRect()
                : new Rect(0f, 0f, 1f, 1f);
            var cameraDistance = cameraFraming == LaneCameraFraming.ActiveLane ? -6.2f : -5.8f;
            camera.transform.position = boardCenter + new Vector3(0f, 17.5f, cameraDistance);
            camera.transform.LookAt(boardCenter);
        }

        private void LateUpdate()
        {
            ConfigureDefaultCamera();
        }

        private void OnPreCull()
        {
            ConfigureDefaultCamera();
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

                CreateLaneTileDetailPass(lane);
                CreateLaneEndpointBox(lane, CenterColumn, 0, "SpawnBox", MintSignal);
                CreateLaneEndpointBox(lane, CenterColumn, LaneLength - 1, "LifeLossBox", LeakRed);
                CreateEndpointPlateDetails(lane, 0, MintSignal, lane == 1, true);
                CreateEndpointPlateDetails(lane, LaneLength - 1, LeakRed, lane == 1, false);
                CreateLaneFrame(lane);
                CreateLaneFlowCues(lane);
                CreateLaneLandmark(lane, CenterColumn, 0, "Spawn", MintSignal, 0.28f);
                CreateLaneLandmark(lane, CenterColumn, LaneLength - 1, "LifeLoss", LeakRed, 0.34f);
                CreateLaneGate(lane, 0, MintSignal, "ENTRY");
                CreateLaneGate(lane, LaneLength - 1, LeakRed, "LEAK");
                CreateLaneLabel(lane);
                CreateLaneOwnershipBadge(lane);
            }

            laneCreated = true;
        }

        private void RenderSnapshot(LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            visibleKeys.Clear();
            towerRolesByCell.Clear();
            foreach (var tower in snapshot.Towers)
            {
                var key = tower.EntityId.Value.ToString();
                visibleKeys.Add(key);
                towerRolesByCell[TowerGridKey(tower.Position, tower.LaneId)] = tower.TowerId.Value;
                var visualProfile = towerVisualLibrary != null ? towerVisualLibrary.FindProfile(tower.TowerId.Value) : null;
                var towerObject = GetOrCreateTower(key, visualProfile);
                SetTowerTransform(towerObject, tower.Position, tower.LaneId, tower.TowerId.Value, visualProfile);
                ApplyTowerColor(towerObject, tower.TowerId.Value, tower.OwnerId.Value, visualProfile);
                if (visualProfile == null || visualProfile.Prefab == null)
                {
                    ConfigureTowerRoleMarker(towerObject, tower.TowerId.Value, tower.OwnerId.Value);
                }

                lastKnownPositions[key] = towerObject.transform.position;
            }

            ReleaseMissingTowers();
            visibleKeys.Clear();
            var pressureByLane = new int[4];
            foreach (var creep in snapshot.Creeps)
            {
                var key = creep.EntityId.Value.ToString();
                visibleKeys.Add(key);
                var visualProfile = creepVisualLibrary != null ? creepVisualLibrary.FindProfile(creep.CreepId.Value) : null;
                var isNewCreep = !activeCreeps.ContainsKey(key);
                var creepObject = GetOrCreateCreep(key, visualProfile);
                if (!isNewCreep && lastCreepHealth.TryGetValue(key, out var previousHealth) && creep.Health < previousHealth)
                {
                    creepHitFlashUntil[key] = Time.time + 0.16f;
                }

                SetCreepTransform(creepObject, creep.Position, creep.LaneId, creep.CreepId.Value, visualProfile, isNewCreep);
                var healthFraction = CreepHealthFraction(creep.CreepId.Value, creep.Health);
                var isHitFlashing = creepHitFlashUntil.TryGetValue(key, out var flashUntil) && Time.time < flashUntil;
                ApplyCreepColor(creepObject, creep.CreepId.Value, creep.SenderId.Value, visualProfile, healthFraction, isHitFlashing);
                if (suppressCreepGameplayOverlays)
                {
                    DeactivateCreepGameplayOverlays(creepObject);
                }
                else
                {
                    ConfigureCreepHealthBar(creepObject, creep.CreepId.Value, healthFraction);
                    if (UsesAiPlateVisual(creepObject))
                    {
                        DeactivateRoleReadabilityOverlay(creepObject);
                    }
                    else
                    {
                        ConfigureCreepReadabilityOverlay(creepObject, creep.CreepId.Value, creep.SenderId.Value, healthFraction, isHitFlashing);
                    }

                    if (visualProfile == null || visualProfile.Prefab == null)
                    {
                        ConfigureCreepRoleMarker(creepObject, creep.CreepId.Value, creep.SenderId.Value, healthFraction, isHitFlashing);
                    }
                }

                lastKnownPositions[key] = creepObject.transform.position;
                lastKnownCreepIds[key] = creep.CreepId.Value;
                lastCreepHealth[key] = creep.Health;
                if (creep.LaneId.Value >= 1 && creep.LaneId.Value < pressureByLane.Length)
                {
                    pressureByLane[creep.LaneId.Value]++;
                }
            }

            ReleaseMissingCreeps();
            UpdateLanePressureIndicators(pressureByLane);
        }

        private void UpdateLanePressureIndicators(IReadOnlyList<int> pressureByLane)
        {
            for (var laneId = 1; laneId <= 3; laneId++)
            {
                var pressure = pressureByLane[laneId];
                var meter = GetLanePressureMeter(laneId);
                var color = PressureColor(pressure);
                var fill = Mathf.Clamp(pressure, 0, 12) / 12f;
                var length = Mathf.Lerp(0.28f, LaneLength * 0.54f, fill);
                meter.transform.localScale = new Vector3(0.16f, 0.12f, length);
                meter.transform.position = new Vector3(LaneOffset(laneId) + LaneWidth + 0.18f, -0.08f, 0.35f + length * 0.5f);
                SetColor(meter, color);

                var cap = GetLanePressureCap(laneId);
                cap.SetActive(pressure >= 8);
                cap.transform.position = new Vector3(LaneOffset(laneId) + LaneWidth + 0.18f, 0.04f, 0.35f + length);
                SetColor(cap, LeakRed);

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

            meter = CreatePrimitive($"Lane{laneId}PressureMeter", PrimitiveType.Cube);
            lanePressureMeters[laneId] = meter;
            laneDecorations.Add(meter);
            return meter;
        }

        private GameObject GetLanePressureCap(int laneId)
        {
            if (lanePressureCaps.TryGetValue(laneId, out var cap))
            {
                return cap;
            }

            cap = CreatePrimitive($"Lane{laneId}PressureCap", PrimitiveType.Sphere);
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

        private void RenderEvents(IReadOnlyList<ISimulationEvent> events)
        {
            var leakTransferCandidates = new HashSet<string>();
            var queuedSpawnKeys = new HashSet<string>();
            foreach (var simulationEvent in events)
            {
                if (simulationEvent is LeakEvent leak)
                {
                    leakTransferCandidates.Add(TransferCandidateKey(leak.Tick, leak.SenderId));
                }
                else if (simulationEvent is CreepQueuedEvent queued)
                {
                    queuedSpawnKeys.Add(SpawnEventKey(queued.Tick, queued.SenderId, queued.DefenderId, queued.CreepId.Value));
                }
            }

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
                        var isTransferArrival = leakTransferCandidates.Contains(TransferCandidateKey(spawned.Tick, spawned.SenderId)) &&
                            !queuedSpawnKeys.Contains(SpawnEventKey(spawned.Tick, spawned.SenderId, spawned.DefenderId, spawned.CreepId.Value));
                        if (isTransferArrival)
                        {
                            var transferLabelPosition = spawnPosition + Vector3.back * 1.25f;
                            SpawnCreepTransferArrivalCue(spawned.DefenderId.Value, spawnColor);
                            SpawnFloatingText(transferLabelPosition, "TRANSFER", spawnColor, 0.82f);
                            SpawnReducedEffectCue(transferLabelPosition, "TRANSFER", spawnColor);
                        }
                        else
                        {
                            SpawnCreepArrivalCue(spawned.DefenderId.Value, spawnColor);
                            SpawnFloatingText(spawnPosition, SpawnLabel(spawned.CreepId.Value), spawnColor, 0.48f);
                            SpawnReducedEffectCue(spawnPosition, "SPAWN", spawnColor);
                        }

                        SpawnEffect(spawnPosition, spawnColor, 0.52f, 0.28f);
                        break;
                    case CreepDamagedEvent damaged:
                        var hitPosition = PositionFor(damaged.CreepEntityId.Value.ToString());
                        var damagedCreepId = CreepIdFor(damaged.CreepEntityId.Value.ToString());
                        var towerPosition = GridToWorld(damaged.TowerPosition, damaged.LaneId);
                        var towerRole = TowerRoleAt(damaged.TowerPosition, damaged.LaneId);
                        SpawnTowerAttackCue(towerPosition, hitPosition, towerRole, damaged.DamageDealt);
                        SpawnCreepHitCue(hitPosition, new Color(1f, 0.88f, 0.44f), damaged.DamageDealt);
                        SpawnCreepRoleFeedbackCue(hitPosition, damagedCreepId, damaged.DamageDealt);
                        SpawnEffect(hitPosition, new Color(1f, 0.88f, 0.44f), 0.24f, 0.12f);
                        if (damaged.DamageDealt >= 5)
                        {
                            SpawnFloatingText(hitPosition + Vector3.left * 0.32f, damaged.DamageDealt.ToString(), new Color(1f, 0.88f, 0.44f), 0.32f);
                            SpawnReducedEffectCue(hitPosition, "HIT", new Color(1f, 0.88f, 0.44f));
                        }

                        PlaySound(towerHitClip);
                        break;
                    case CreepKilledEvent creepKilled:
                        var killedCreepKey = creepKilled.CreepEntityId.Value.ToString();
                        var killPosition = PositionFor(killedCreepKey);
                        var killedCreepId = CreepIdFor(killedCreepKey);
                        var deathProfile = creepVisualLibrary != null ? creepVisualLibrary.FindProfile(killedCreepId) : null;
                        SpawnCreepDeathCue(killPosition, SignalGold, killedCreepId, deathProfile);
                        SpawnEffect(killPosition, SignalGold, 0.42f, 0.2f);
                        SpawnFloatingText(killPosition, $"+{creepKilled.BountyAwarded.Amount}", SignalGold, 0.56f);
                        SpawnReducedEffectCue(killPosition, "KILL", SignalGold);
                        PlaySound(creepKilledClip);
                        break;
                    case LeakEvent leak:
                        var leakCreepKey = leak.CreepEntityId.Value.ToString();
                        var position = PositionFor(leakCreepKey);
                        var leakingCreepId = CreepIdFor(leakCreepKey);
                        SpawnLeakGateCue(leak.DefenderId.Value);
                        SpawnCreepLeakRoleCue(position, leakingCreepId);
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
            beam.transform.localScale = new Vector3(0.06f, 0.06f, Mathf.Max(0.1f, distance));
            SetColor(beam, color);
            timedPresentations.Add(new TimedPresentation(beam, Time.time + duration, effectPool));
        }

        private void SpawnFloatingText(Vector3 position, string text, Color color) => SpawnFloatingText(position, text, color, 0.7f);

        private void SpawnFloatingText(Vector3 position, string text, Color color, float duration)
        {
            var textObject = GetTextObject();
            textObject.transform.position = position + Vector3.up * 0.55f;
            textObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            textObject.transform.localScale = Vector3.one;
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

        private void SpawnCreepTransferArrivalCue(int laneId, Color color)
        {
            var spawn = SpawnPosition(laneId);
            SpawnCellFrameCue(spawn, color, 0.34f);
            SpawnBeam(spawn + new Vector3(-0.68f, 0.28f, 0.62f), spawn + new Vector3(0.68f, 0.28f, 0.62f), color, 0.24f);
            SpawnBeam(spawn + new Vector3(-0.68f, 0.28f, -0.62f), spawn + new Vector3(0.68f, 0.28f, -0.62f), color, 0.24f);
            SpawnBeam(spawn + new Vector3(-0.44f, 0.18f, 0f), spawn + new Vector3(0.44f, 0.38f, 0f), SignalGold, 0.24f);
        }

        private void SpawnTowerAttackCue(Vector3 towerPosition, Vector3 hitPosition, string towerId, int damage)
        {
            var shotColor = TowerShotColor(towerId, damage);
            var muzzle = towerPosition + Vector3.up * 0.62f;
            if (IsArrowTower(towerId))
            {
                SpawnBeam(muzzle + new Vector3(-0.5f, 0f, -0.18f), muzzle + new Vector3(0.5f, 0f, -0.18f), shotColor, 0.08f);
                SpawnBeam(muzzle + new Vector3(0f, -0.04f, -0.32f), muzzle + new Vector3(0f, 0.04f, 0.26f), SignalGold, 0.08f);
                SpawnBeam(muzzle + new Vector3(0f, 0f, 0.08f), hitPosition + Vector3.up * 0.12f, shotColor, damage >= 5 ? 0.16f : 0.12f);
                SpawnCellFrameCue(hitPosition, shotColor, damage >= 5 ? 0.15f : 0.1f);
                SpawnEffect(muzzle + new Vector3(0f, 0f, 0.12f), shotColor, damage >= 5 ? 0.28f : 0.2f, 0.08f);
                return;
            }

            if (IsControlTower(towerId))
            {
                SpawnBeam(muzzle + new Vector3(-0.42f, 0f, 0f), hitPosition + Vector3.up * 0.12f, shotColor, 0.18f);
                SpawnBeam(muzzle + new Vector3(0.42f, 0f, 0f), hitPosition + Vector3.up * 0.12f, shotColor, 0.18f);
                SpawnCellFrameCue(hitPosition, shotColor, 0.18f);
                SpawnEffect(towerPosition + Vector3.up * 0.28f, shotColor, 0.42f, 0.16f);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.42f : 0.32f, 0.16f);
                return;
            }

            if (IsRelayTower(towerId))
            {
                SpawnBeam(muzzle, hitPosition + Vector3.up * 0.2f, shotColor, 0.2f);
                SpawnBeam(towerPosition + new Vector3(-0.34f, 0.34f, 0f), towerPosition + new Vector3(0.34f, 0.34f, 0f), shotColor, 0.14f);
                SpawnBeam(towerPosition + new Vector3(0f, 0.58f, -0.34f), towerPosition + new Vector3(0f, 0.58f, 0.34f), shotColor, 0.14f);
                SpawnCellFrameCue(towerPosition, shotColor, 0.16f);
                SpawnEffect(muzzle, shotColor, 0.26f, 0.12f);
                return;
            }

            if (IsPulseTower(towerId))
            {
                SpawnEffect(towerPosition + Vector3.up * 0.28f, shotColor, 0.68f, 0.18f);
                SpawnEffect(towerPosition + Vector3.up * 0.62f, SignalGold, 0.28f, 0.1f);
                SpawnBeam(towerPosition + new Vector3(-0.54f, 0.34f, 0.54f), towerPosition + new Vector3(0.54f, 0.34f, 0.54f), shotColor, 0.14f);
                SpawnBeam(towerPosition + new Vector3(-0.54f, 0.34f, -0.54f), towerPosition + new Vector3(0.54f, 0.34f, -0.54f), shotColor, 0.14f);
                SpawnBeam(towerPosition + new Vector3(-0.54f, 0.34f, -0.54f), towerPosition + new Vector3(-0.54f, 0.34f, 0.54f), shotColor, 0.14f);
                SpawnBeam(towerPosition + new Vector3(0.54f, 0.34f, -0.54f), towerPosition + new Vector3(0.54f, 0.34f, 0.54f), shotColor, 0.14f);
                SpawnBeam(towerPosition + new Vector3(-0.36f, 0.42f, -0.36f), towerPosition + new Vector3(0.36f, 0.42f, 0.36f), SignalGold, 0.12f);
                SpawnBeam(towerPosition + new Vector3(-0.36f, 0.42f, 0.36f), towerPosition + new Vector3(0.36f, 0.42f, -0.36f), SignalGold, 0.12f);
                SpawnCellFrameCue(hitPosition, shotColor, 0.18f);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.5f : 0.36f, 0.16f);
                return;
            }

            if (IsPrismTower(towerId))
            {
                SpawnBeam(muzzle + new Vector3(-0.16f, 0.08f, 0f), muzzle + new Vector3(0.16f, 0.08f, 0f), SignalGold, 0.12f);
                SpawnBeam(muzzle + new Vector3(0f, 0.08f, -0.16f), muzzle + new Vector3(0f, 0.08f, 0.16f), SignalGold, 0.12f);
                SpawnEffect(muzzle + Vector3.up * 0.08f, SignalGold, 0.22f, 0.12f);
                SpawnBeam(muzzle + Vector3.up * 0.08f, hitPosition + Vector3.up * 0.16f, shotColor, damage >= 5 ? 0.22f : 0.18f);
                SpawnEffect(hitPosition + Vector3.up * 0.08f, shotColor, damage >= 5 ? 0.46f : 0.32f, 0.18f);
                return;
            }

            var scale = damage >= 5 ? 0.48f : 0.34f;
            SpawnBeam(muzzle + new Vector3(-scale, 0f, 0f), muzzle + new Vector3(scale, 0f, 0f), shotColor, 0.1f);
            SpawnBeam(muzzle + new Vector3(0f, 0f, -scale), muzzle + new Vector3(0f, 0f, scale), shotColor, 0.1f);
            SpawnBeam(muzzle, hitPosition + Vector3.up * 0.12f, shotColor, 0.14f);
            SpawnCellFrameCue(hitPosition, shotColor, damage >= 5 ? 0.16f : 0.12f);
            SpawnEffect(muzzle, shotColor, damage >= 5 ? 0.3f : 0.22f, 0.1f);
        }

        private void SpawnCreepHitCue(Vector3 position, Color color, int damage)
        {
            var scale = damage >= 5 ? 0.44f : 0.3f;
            SpawnBeam(position + new Vector3(-scale, 0.2f, 0f), position + new Vector3(scale, 0.2f, 0f), color, 0.1f);
            SpawnBeam(position + new Vector3(0f, 0.2f, -scale), position + new Vector3(0f, 0.2f, scale), color, 0.1f);
        }

        private void SpawnCreepRoleFeedbackCue(Vector3 position, string creepId, int damage)
        {
            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                var color = new Color(0.72f, 0.94f, 1f);
                SpawnBeam(position + new Vector3(-0.28f, 0.3f, -0.34f), position + new Vector3(0.28f, 0.3f, 0.34f), color, 0.18f);
                SpawnBeam(position + new Vector3(-0.28f, 0.2f, 0.34f), position + new Vector3(0.28f, 0.2f, -0.34f), new Color(0.36f, 0.5f, 0.58f), 0.18f);
                if (damage >= 5)
                {
                    SpawnFloatingText(position + Vector3.right * 0.34f, "REVEAL", color, 0.38f);
                    SpawnReducedEffectCue(position + Vector3.right * 0.2f, "REVEAL", color);
                }

                return;
            }

            if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
            {
                SpawnBeam(position + new Vector3(0f, 0.24f, -0.5f), position + new Vector3(0f, 0.24f, 0.58f), LeakRed, 0.16f);
                SpawnBeam(position + new Vector3(-0.34f, 0.18f, 0.32f), position + new Vector3(0.34f, 0.18f, 0.32f), SignalGold, 0.14f);
                if (damage >= 5)
                {
                    SpawnReducedEffectCue(position, "SIEGE", LeakRed);
                }
            }
        }

        private void SpawnCreepLeakRoleCue(Vector3 position, string creepId)
        {
            if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
            {
                SpawnBeam(position + new Vector3(-0.46f, 0.22f, -0.48f), position + new Vector3(0.46f, 0.22f, 0.48f), LeakRed, 0.24f);
                SpawnBeam(position + new Vector3(0f, 0.28f, -0.62f), position + new Vector3(0f, 0.28f, 0.62f), SignalGold, 0.24f);
                return;
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                SpawnBeam(position + new Vector3(-0.32f, 0.22f, 0f), position + new Vector3(0.32f, 0.22f, 0f), new Color(0.72f, 0.94f, 1f), 0.22f);
            }
        }

        private void SpawnCreepDeathCue(Vector3 position, Color color, string creepId, CreepVisualProfile visualProfile)
        {
            var deathCueStyle = CreepDeathCueStyleFor(creepId, visualProfile);
            if (deathCueStyle == CreepDeathCueStyle.HeavyShatter)
            {
                SpawnBeam(position + new Vector3(-0.48f, 0.14f, -0.12f), position + new Vector3(0.48f, 0.14f, 0.12f), color, 0.18f);
                SpawnBeam(position + new Vector3(-0.2f, 0.3f, -0.44f), position + new Vector3(0.2f, 0.3f, 0.44f), color, 0.18f);
                SpawnBeam(position + new Vector3(-0.34f, 0.34f, 0.34f), position + new Vector3(0.34f, 0.08f, -0.34f), color, 0.18f);
                return;
            }

            if (deathCueStyle == CreepDeathCueStyle.ShardScatter)
            {
                SpawnBeam(position + new Vector3(-0.46f, 0.12f, 0f), position + new Vector3(-0.12f, 0.24f, 0.36f), color, 0.12f);
                SpawnBeam(position + new Vector3(0.42f, 0.12f, 0.04f), position + new Vector3(0.1f, 0.24f, -0.38f), color, 0.12f);
                SpawnBeam(position + new Vector3(0f, 0.12f, -0.48f), position + new Vector3(0.34f, 0.24f, -0.12f), color, 0.12f);
                SpawnBeam(position + new Vector3(0f, 0.12f, 0.48f), position + new Vector3(-0.34f, 0.24f, 0.12f), color, 0.12f);
                return;
            }

            if (deathCueStyle == CreepDeathCueStyle.SoftDissolve)
            {
                SpawnBeam(position + new Vector3(-0.3f, 0.2f, -0.3f), position + new Vector3(0.3f, 0.2f, 0.3f), color, 0.2f);
                SpawnBeam(position + new Vector3(-0.3f, 0.2f, 0.3f), position + new Vector3(0.3f, 0.2f, -0.3f), color, 0.2f);
                return;
            }

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
            SpawnBeam(senderPosition + Vector3.up * 0.18f, defenderPosition + Vector3.up * 0.18f, color, 0.22f);
            SpawnEffect(defenderPosition, color, 0.54f, 0.3f);
            SpawnFloatingText(senderPosition + Vector3.left * 0.42f, "SEND", color, 0.42f);
            SpawnFloatingText(defenderPosition, $"{queued.Quantity}x {SpawnLabel(queued.CreepId.Value)}", color, 0.56f);
            SpawnReducedEffectCue(defenderPosition, "SEND", color);
            PlaySound(sendClip);
        }

        private string TowerRoleAt(GridPosition position, LaneId laneId)
        {
            return towerRolesByCell.TryGetValue(TowerGridKey(position, laneId), out var towerId) ? towerId : string.Empty;
        }

        private static string TowerGridKey(GridPosition position, LaneId laneId) => $"{laneId.Value}:{position.X}:{position.Y}";

        private static string TransferCandidateKey(SimulationTick tick, PlayerId senderId) => $"{tick.Value}:{senderId.Value}";

        private static string SpawnEventKey(SimulationTick tick, PlayerId senderId, PlayerId defenderId, string creepId) => $"{tick.Value}:{senderId.Value}:{defenderId.Value}:{creepId}";

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
                lastCreepHealth.Remove(key);
                creepHitFlashUntil.Remove(key);
            }
        }

        private void ReleaseAllActiveObjects()
        {
            foreach (var pair in activeTowers) ReleaseTowerToPool(pair.Key, pair.Value);
            foreach (var pair in activeCreeps) ReleaseCreepToPool(pair.Key, pair.Value);
            activeTowers.Clear();
            activeCreeps.Clear();
            activeTowerPoolKeys.Clear();
            activeCreepPoolKeys.Clear();
            lastKnownCreepIds.Clear();
            lastCreepHealth.Clear();
            creepHitFlashUntil.Clear();
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

        private GameObject GetOrCreateTower(string key, TowerVisualProfile visualProfile)
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
            var keysToRelease = new List<string>();
            foreach (var pair in activeTowers)
            {
                if (!visibleKeys.Contains(pair.Key))
                {
                    keysToRelease.Add(pair.Key);
                }
            }

            foreach (var key in keysToRelease)
            {
                ReleaseTowerToPool(key, activeTowers[key]);
                activeTowers.Remove(key);
                activeTowerPoolKeys.Remove(key);
            }
        }

        private void ReleaseTowerToPool(string key, GameObject instance)
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

        private GameObject GetOrCreateCreep(string key, CreepVisualProfile visualProfile)
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
            var keysToRelease = new List<string>();
            foreach (var pair in activeCreeps)
            {
                if (!visibleKeys.Contains(pair.Key))
                {
                    keysToRelease.Add(pair.Key);
                }
            }

            foreach (var key in keysToRelease)
            {
                ReleaseCreepToPool(key, activeCreeps[key]);
                activeCreeps.Remove(key);
                activeCreepPoolKeys.Remove(key);
                lastCreepHealth.Remove(key);
                creepHitFlashUntil.Remove(key);
            }
        }

        private void ReleaseCreepToPool(string key, GameObject instance)
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

        private static void SetTowerTransform(GameObject instance, GridPosition position, LaneId laneId, string towerId, TowerVisualProfile visualProfile)
        {
            var lift = visualProfile != null && visualProfile.Prefab != null ? visualProfile.Lift : TowerRoleLift(towerId);
            instance.transform.position = GridToWorld(position, laneId) + Vector3.up * lift;
            instance.transform.localScale = visualProfile != null && visualProfile.HasScale ? visualProfile.Scale : TowerRoleScale(towerId);
            instance.transform.rotation = Quaternion.identity;
        }

        private static void SetCreepTransform(GameObject instance, GridPosition position, LaneId laneId, string creepId, CreepVisualProfile visualProfile, bool snapToTarget)
        {
            var roleMotion = CreepRoleMotion(creepId, visualProfile);
            var targetPosition = GridToWorld(position, laneId) + CreepRoleOffset(creepId) + roleMotion.PositionOffset;
            instance.transform.position = snapToTarget || Vector3.Distance(instance.transform.position, targetPosition) > 2.5f
                ? targetPosition
                : Vector3.Lerp(instance.transform.position, targetPosition, Mathf.Clamp01(Time.deltaTime * 8f));
            instance.transform.localScale = CreepRoleScale(creepId, visualProfile);
            instance.transform.rotation = roleMotion.Rotation;
        }

        private static void ApplyCreepColor(GameObject creepObject, string creepId, int senderId, CreepVisualProfile visualProfile, float healthFraction, bool isHitFlashing)
        {
            var bodyColor = CreepBodyColor(creepId, senderId, healthFraction, isHitFlashing);
            var senderColor = SenderColor(senderId);
            var damageColor = healthFraction < 0.35f
                ? new Color(0.68f, 0.22f, 0.16f)
                : bodyColor;

            if (visualProfile == null || visualProfile.Prefab == null)
            {
                SetColor(creepObject, bodyColor);
                return;
            }

            SetProfileColor(creepObject, visualProfile.BodyRendererPath, bodyColor);
            SetProfileColors(creepObject, visualProfile.SenderAccentRendererPaths, senderColor);
            SetProfileColors(creepObject, visualProfile.DamageRendererPaths, damageColor);
        }

        private static void ConfigureCreepHealthBar(GameObject creepObject, string creepId, float healthFraction)
        {
            var metrics = CreepHealthBarMetrics.For(creepId);
            var back = EnsureChild(creepObject, "HealthBarBack", PrimitiveType.Cube);
            var fill = EnsureChild(creepObject, "HealthBarFill", PrimitiveType.Cube);
            var midpoint = EnsureChild(creepObject, "HealthBarMidTick", PrimitiveType.Cube);
            var wound = EnsureChild(creepObject, "HealthWoundPip", PrimitiveType.Cube);

            ConfigureHealthBarChild(back, new Vector3(0f, metrics.Y, metrics.Z), new Vector3(metrics.Width, metrics.Height, metrics.Depth), new Color(0.015f, 0.022f, 0.035f));
            var fillWidth = Mathf.Max(metrics.MinFillWidth, metrics.Width * Mathf.Clamp01(healthFraction));
            var fillX = (fillWidth - metrics.Width) * 0.5f;
            ConfigureHealthBarChild(fill, new Vector3(fillX, metrics.Y + metrics.FillLift, metrics.Z), new Vector3(fillWidth, metrics.Height * 1.12f, metrics.Depth * 1.08f), CreepHealthColor(healthFraction));
            ConfigureHealthBarChild(midpoint, new Vector3(0f, metrics.Y + metrics.FillLift * 1.6f, metrics.Z), new Vector3(0.035f, metrics.Height * 1.35f, metrics.Depth * 1.16f), new Color(0.015f, 0.022f, 0.035f));
            ConfigureHealthBarChild(wound, new Vector3(metrics.Width * 0.5f + metrics.WoundOffset, metrics.Y + metrics.FillLift * 1.7f, metrics.Z), new Vector3(metrics.WoundSize, metrics.Height * 1.5f, metrics.Depth * 1.18f), LeakRed);
            wound.SetActive(healthFraction < 0.72f);
        }

        private static void ConfigureHealthBarChild(GameObject child, Vector3 localPosition, Vector3 localScale, Color color)
        {
            ConfigureChild(child, true, localPosition, localScale, color);
            child.transform.rotation = Quaternion.identity;
        }

        private static Color CreepHealthColor(float healthFraction)
        {
            if (healthFraction <= 0.34f)
            {
                return LeakRed;
            }

            if (healthFraction <= 0.66f)
            {
                return SignalGold;
            }

            return MintSignal;
        }

        private static void ConfigureCreepReadabilityOverlay(GameObject creepObject, string creepId, int senderId, float healthFraction, bool isHitFlashing)
        {
            DeactivateRoleReadabilityOverlay(creepObject);

            var roleColor = isHitFlashing ? new Color(1f, 0.94f, 0.62f) : BoostValue(CreepRoleColor(creepId, senderId), 1.14f);
            var senderColor = SenderColor(senderId);
            var damageColor = healthFraction < 0.35f ? LeakRed : roleColor;

            if (ContainsRole(creepId, "swarm"))
            {
                var jitter = Mathf.Sin(Time.time * 19f) * 0.06f;
                ConfigureChild(EnsureChild(creepObject, "RoleSwarmValueRing", PrimitiveType.Cylinder), true, new Vector3(0f, -0.33f, 0f), new Vector3(1.34f, 0.02f, 1.18f), DimValue(roleColor, 0.72f));
                ConfigureChild(EnsureChild(creepObject, "RoleSwarmLeadSpark", PrimitiveType.Sphere), true, new Vector3(0.38f + jitter, 0.18f, 0.52f), new Vector3(0.18f, 0.18f, 0.18f), roleColor);
                return;
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss"))
            {
                ConfigureChild(EnsureChild(creepObject, "RoleBruteLeftShoulder", PrimitiveType.Cube), true, new Vector3(-0.44f, 0.36f, 0.18f), new Vector3(0.22f, 0.18f, 0.46f), damageColor);
                ConfigureChild(EnsureChild(creepObject, "RoleBruteRightShoulder", PrimitiveType.Cube), true, new Vector3(0.44f, 0.36f, 0.18f), new Vector3(0.22f, 0.18f, 0.46f), damageColor);
                ConfigureChild(EnsureChild(creepObject, "RoleBruteCenterPlate", PrimitiveType.Cube), true, new Vector3(0f, 0.5f, 0.36f), new Vector3(0.42f, 0.08f, 0.2f), SignalGold);
                return;
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                var shimmer = Mathf.Sin(Time.time * 8f) * 0.08f;
                ConfigureChild(EnsureChild(creepObject, "RoleShadeLeftEcho", PrimitiveType.Cube), true, new Vector3(-0.34f + shimmer, 0.2f, 0.02f), new Vector3(0.06f, 0.5f, 0.66f), new Color(0.72f, 0.94f, 1f));
                ConfigureChild(EnsureChild(creepObject, "RoleShadeRightEcho", PrimitiveType.Cube), true, new Vector3(0.34f - shimmer, 0.2f, -0.04f), new Vector3(0.06f, 0.42f, 0.58f), new Color(0.36f, 0.5f, 0.58f));
                ConfigureChild(EnsureChild(creepObject, "RoleShadeCoreLine", PrimitiveType.Cube), true, new Vector3(0f, 0.42f, 0.22f), new Vector3(0.1f, 0.08f, 0.46f), roleColor);
                return;
            }

            if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
            {
                var windup = Mathf.Abs(Mathf.Sin(Time.time * 5f));
                ConfigureChild(EnsureChild(creepObject, "RoleSiegeRamHead", PrimitiveType.Cube), true, new Vector3(0f, 0.18f, 0.62f), new Vector3(0.48f, 0.22f, 0.2f), roleColor);
                ConfigureChild(EnsureChild(creepObject, "RoleSiegeWarningLeft", PrimitiveType.Cube), true, new Vector3(-0.38f, 0.24f, 0.14f), new Vector3(0.08f, 0.24f + windup * 0.08f, 0.58f), LeakRed);
                ConfigureChild(EnsureChild(creepObject, "RoleSiegeWarningRight", PrimitiveType.Cube), true, new Vector3(0.38f, 0.24f, 0.14f), new Vector3(0.08f, 0.24f + windup * 0.08f, 0.58f), LeakRed);
                return;
            }

            ConfigureChild(EnsureChild(creepObject, "RoleRunnerChevron", PrimitiveType.Cube), true, new Vector3(0f, 0.28f, 0.62f), new Vector3(0.24f, 0.08f, 0.28f), roleColor);
            ConfigureChild(EnsureChild(creepObject, "RoleRunnerWake", PrimitiveType.Cube), true, new Vector3(0f, -0.2f, -0.72f), new Vector3(0.055f, 0.035f, 0.58f), senderColor);
        }

        private static void DeactivateRoleReadabilityOverlay(GameObject creepObject)
        {
            for (var index = 0; index < CreepReadabilityOverlayNames.Length; index++)
            {
                var marker = creepObject.transform.Find(CreepReadabilityOverlayNames[index]);
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
            }
        }

        private static bool UsesAiPlateVisual(GameObject instance) =>
            instance != null && instance.transform.Find("AIPlateVisual") != null;

        private static void DeactivateCreepGameplayOverlays(GameObject creepObject)
        {
            DeactivateRoleReadabilityOverlay(creepObject);
            DeactivateKnownCreepMarkers(creepObject);
            for (var index = 0; index < CreepHealthOverlayNames.Length; index++)
            {
                var marker = creepObject.transform.Find(CreepHealthOverlayNames[index]);
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
            }
        }

        private static void SetProfileColors(GameObject root, IReadOnlyList<string> paths, Color color)
        {
            for (var index = 0; index < paths.Count; index++)
            {
                SetProfileColor(root, paths[index], color);
            }
        }

        private static void SetProfileColor(GameObject root, string rendererPath, Color color)
        {
            var target = string.IsNullOrWhiteSpace(rendererPath)
                ? root
                : root.transform.Find(rendererPath)?.gameObject;

            if (target != null)
            {
                SetColorInChildren(target, color);
            }
        }

        private static void SetColorInChildren(GameObject instance, Color color)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                renderers[index].material.color = color;
            }
        }

        private static Vector3 GridToWorld(GridPosition position, LaneId laneId) => new Vector3(LaneOffset(laneId.Value) + position.X, 0.35f, WorldZ(position.Y));

        private static float LaneOffset(int laneId) => (laneId - 1) * LaneSpacing;

        private static float WorldZ(int gridY) => LaneLength - 1 - gridY;

        private static Vector3 LaneCenter(int laneId) => new Vector3(LaneOffset(laneId) + BoardCenterX, 0.35f, BoardCenterZ);

        private Vector3 PositionFor(string entityId) => lastKnownPositions.TryGetValue(entityId, out var position) ? position : GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(1));

        private string CreepIdFor(string entityId) => lastKnownCreepIds.TryGetValue(entityId, out var creepId) ? creepId : string.Empty;

        private void CreateLaneFrame(int laneId)
        {
            var offset = LaneOffset(laneId);
            var accent = OwnerAccent(laneId);
            var trimColor = LaneFrameTrimColor(laneId);
            var highlight = LaneFrameHighlightColor(laneId);
            var shadow = BoardContactShadowColor(laneId);
            var railHeight = laneId == 1 ? 0.11f : 0.075f;
            var longRailWidth = laneId == 1 ? 0.11f : 0.075f;
            var sideRailWidth = laneId == 1 ? 0.105f : 0.07f;

            CreateBoardRail($"Lane{laneId}NorthRailShadow", new Vector3(offset + BoardCenterX, -0.075f, LaneLength - 0.12f), new Vector3(LaneWidth + 0.18f, 0.035f, 0.16f), shadow);
            CreateBoardRail($"Lane{laneId}SouthRailShadow", new Vector3(offset + BoardCenterX, -0.075f, -0.88f), new Vector3(LaneWidth + 0.18f, 0.035f, 0.16f), shadow);
            CreateBoardRail($"Lane{laneId}NorthRail", new Vector3(offset + BoardCenterX, -0.045f, LaneLength - 0.18f), new Vector3(LaneWidth + 0.05f, railHeight, longRailWidth), trimColor);
            CreateBoardRail($"Lane{laneId}SouthRail", new Vector3(offset + BoardCenterX, -0.045f, -0.82f), new Vector3(LaneWidth + 0.05f, railHeight, longRailWidth), trimColor);
            CreateBoardRail($"Lane{laneId}WestRail", new Vector3(offset - 0.46f, -0.045f, BoardCenterZ), new Vector3(sideRailWidth, railHeight, LaneLength - 0.18f), trimColor);
            CreateBoardRail($"Lane{laneId}EastRail", new Vector3(offset + LaneWidth - 0.54f, -0.045f, BoardCenterZ), new Vector3(sideRailWidth, railHeight, LaneLength - 0.18f), trimColor);
            CreateBoardRail($"Lane{laneId}NorthRailHighlight", new Vector3(offset + BoardCenterX, 0.004f, LaneLength - 0.24f), new Vector3(LaneWidth - 0.18f, 0.018f, 0.026f), highlight);
            CreateBoardRail($"Lane{laneId}SouthRailHighlight", new Vector3(offset + BoardCenterX, 0.004f, -0.76f), new Vector3(LaneWidth - 0.18f, 0.018f, 0.026f), highlight);

            CreateLaneFrameAccentChip(laneId, "NorthWest", new Vector3(offset - 0.48f, 0.018f, LaneLength - 0.22f), accent);
            CreateLaneFrameAccentChip(laneId, "NorthEast", new Vector3(offset + LaneWidth - 0.52f, 0.018f, LaneLength - 0.22f), accent);
            CreateLaneFrameAccentChip(laneId, "SouthWest", new Vector3(offset - 0.48f, 0.018f, -0.78f), accent);
            CreateLaneFrameAccentChip(laneId, "SouthEast", new Vector3(offset + LaneWidth - 0.52f, 0.018f, -0.78f), accent);
        }

        private void CreateLaneFrameAccentChip(int laneId, string name, Vector3 position, Color accent)
        {
            var chip = CreateBoardRail($"Lane{laneId}{name}RailAccent", position, new Vector3(0.2f, 0.018f, 0.055f), LaneFrameAccentColor(accent, laneId == 1));
            chip.transform.rotation = Quaternion.Euler(0f, name.Contains("West", StringComparison.OrdinalIgnoreCase) ? 22f : -22f, 0f);
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
            CreateSurfaceBand($"Lane{laneId}CenterRouteBand", new Vector3(offset + CenterColumn, -0.248f, BoardCenterZ), new Vector3(1.04f, 0.04f, LaneLength - 0.55f), RouteBandColor(laneId));
            CreateSurfaceBand($"Lane{laneId}RouteLeftGuide", new Vector3(offset + CenterColumn - 0.54f, -0.236f, BoardCenterZ), new Vector3(0.045f, 0.045f, LaneLength - 0.72f), RouteGuideColor(laneId));
            CreateSurfaceBand($"Lane{laneId}RouteRightGuide", new Vector3(offset + CenterColumn + 0.54f, -0.236f, BoardCenterZ), new Vector3(0.045f, 0.045f, LaneLength - 0.72f), RouteGuideColor(laneId));
            CreateSurfaceBand($"Lane{laneId}RouteCenterInlay", new Vector3(offset + CenterColumn, -0.229f, BoardCenterZ), new Vector3(0.12f, 0.026f, LaneLength - 1.1f), RouteInlayColor(laneId));
            CreateSurfaceBand($"Lane{laneId}RouteWestRecess", new Vector3(offset + CenterColumn - 0.69f, -0.246f, BoardCenterZ), new Vector3(0.08f, 0.03f, LaneLength - 0.9f), RouteRecessColor(laneId));
            CreateSurfaceBand($"Lane{laneId}RouteEastRecess", new Vector3(offset + CenterColumn + 0.69f, -0.246f, BoardCenterZ), new Vector3(0.08f, 0.03f, LaneLength - 0.9f), RouteRecessColor(laneId));
            CreateSurfaceBand($"Lane{laneId}LeftBuildOuterEdge", new Vector3(offset + 0.08f, -0.226f, BoardCenterZ), new Vector3(0.045f, 0.028f, LaneLength - 1.45f), BuildBandEdgeColor(laneId));
            CreateSurfaceBand($"Lane{laneId}LeftBuildInnerEdge", new Vector3(offset + 1.92f, -0.226f, BoardCenterZ), new Vector3(0.045f, 0.028f, LaneLength - 1.45f), BuildBandEdgeColor(laneId));
            CreateSurfaceBand($"Lane{laneId}RightBuildInnerEdge", new Vector3(offset + 4.08f, -0.226f, BoardCenterZ), new Vector3(0.045f, 0.028f, LaneLength - 1.45f), BuildBandEdgeColor(laneId));
            CreateSurfaceBand($"Lane{laneId}RightBuildOuterEdge", new Vector3(offset + 5.92f, -0.226f, BoardCenterZ), new Vector3(0.045f, 0.028f, LaneLength - 1.45f), BuildBandEdgeColor(laneId));
            CreateSurfaceBand($"Lane{laneId}NorthFlowWash", new Vector3(offset + BoardCenterX, -0.252f, LaneLength - 2.25f), new Vector3(LaneWidth - 0.7f, 0.032f, 2.2f), EndpointWashColor(MintSignal, laneId == 1));
            CreateSurfaceBand($"Lane{laneId}SouthFlowWash", new Vector3(offset + BoardCenterX, -0.252f, 1.25f), new Vector3(LaneWidth - 0.7f, 0.032f, 2.2f), EndpointWashColor(LeakRed, laneId == 1));
            CreateSurfaceBand($"Lane{laneId}SpawnApproachPlate", new Vector3(offset + BoardCenterX, -0.218f, LaneLength - 1.72f), new Vector3(LaneWidth - 1.15f, 0.032f, 0.34f), EndpointApproachPlateColor(true, laneId == 1));
            CreateSurfaceBand($"Lane{laneId}LeakApproachPlate", new Vector3(offset + BoardCenterX, -0.218f, 0.72f), new Vector3(LaneWidth - 1.15f, 0.032f, 0.34f), EndpointApproachPlateColor(false, laneId == 1));
        }

        private void CreateLaneTileDetailPass(int laneId)
        {
            var offset = LaneOffset(laneId);
            var routeWear = RouteWearColor(laneId);
            var seamColor = BuildBandSeamColor(laneId);
            var crackColor = TileCrackColor(laneId);
            var edgeColor = TileEdgeHighlightColor(laneId);

            CreateSurfaceBand($"Lane{laneId}WestRailContactShadow", new Vector3(offset - 0.43f, -0.118f, BoardCenterZ), new Vector3(0.12f, 0.018f, LaneLength - 0.2f), BoardContactShadowColor(laneId));
            CreateSurfaceBand($"Lane{laneId}EastRailContactShadow", new Vector3(offset + LaneWidth - 0.57f, -0.118f, BoardCenterZ), new Vector3(0.12f, 0.018f, LaneLength - 0.2f), BoardContactShadowColor(laneId));

            for (var y = 1; y < LaneLength - 1; y++)
            {
                var z = WorldZ(y);
                var jitter = TileVariation(laneId, CenterColumn, y) - 0.5f;
                if (y % 2 == 0)
                {
                    var wear = CreateSurfaceBand($"Lane{laneId}RouteWear_{y}", new Vector3(offset + CenterColumn + jitter * 0.18f, -0.098f, z + jitter * 0.08f), new Vector3(0.48f + Mathf.Abs(jitter) * 0.18f, 0.018f, 0.16f), routeWear);
                    wear.transform.rotation = Quaternion.Euler(0f, jitter * 14f, 0f);
                }

                if (y % 4 == 1)
                {
                    CreateSurfaceBand($"Lane{laneId}RouteLeftChip_{y}", new Vector3(offset + CenterColumn - 0.5f, -0.092f, z), new Vector3(0.16f, 0.016f, 0.08f), edgeColor);
                    CreateSurfaceBand($"Lane{laneId}RouteRightChip_{y}", new Vector3(offset + CenterColumn + 0.5f, -0.092f, z - 0.18f), new Vector3(0.12f, 0.016f, 0.09f), edgeColor);
                }

                if (y % 3 == 0)
                {
                    var rib = CreateSurfaceBand($"Lane{laneId}RouteRib_{y}", new Vector3(offset + CenterColumn, -0.086f, z - 0.32f), new Vector3(0.72f, 0.014f, 0.028f), RouteRibColor(laneId));
                    rib.transform.rotation = Quaternion.Euler(0f, (y % 2 == 0 ? 10f : -10f), 0f);
                }
            }

            for (var y = 2; y < LaneLength - 2; y += 4)
            {
                var z = WorldZ(y) - 0.5f;
                CreateSurfaceBand($"Lane{laneId}LeftBuildSeam_{y}", new Vector3(offset + 1f, -0.088f, z), new Vector3(1.5f, 0.014f, 0.035f), seamColor);
                CreateSurfaceBand($"Lane{laneId}RightBuildSeam_{y}", new Vector3(offset + 5f, -0.088f, z), new Vector3(1.5f, 0.014f, 0.035f), seamColor);
            }

            for (var y = 1; y < LaneLength - 1; y += 2)
            {
                var z = WorldZ(y);
                CreateBoardPlateInset(laneId, $"LeftInset_{y}", new Vector3(offset + 1f, -0.078f, z), laneId == 1);
                CreateBoardPlateInset(laneId, $"RightInset_{y}", new Vector3(offset + 5f, -0.078f, z), laneId == 1);
                if (y % 4 == 1)
                {
                    CreateBoardPlateInset(laneId, $"RouteInset_{y}", new Vector3(offset + CenterColumn, -0.074f, z), laneId == 1, 0.72f, 0.52f);
                }
            }

            for (var y = 3; y < LaneLength - 2; y += 5)
            {
                var westCrack = CreateSurfaceBand($"Lane{laneId}WestPlateCrack_{y}", new Vector3(offset + 1.45f, -0.082f, WorldZ(y) + 0.18f), new Vector3(0.035f, 0.014f, 0.44f), crackColor);
                westCrack.transform.rotation = Quaternion.Euler(0f, -22f, 0f);
                var eastCrack = CreateSurfaceBand($"Lane{laneId}EastPlateCrack_{y}", new Vector3(offset + 4.55f, -0.082f, WorldZ(y) - 0.08f), new Vector3(0.032f, 0.014f, 0.36f), crackColor);
                eastCrack.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            }
        }

        private void CreateBoardPlateInset(int laneId, string name, Vector3 center, bool isPlayerLane, float width = 1.25f, float depth = 0.64f)
        {
            var inset = CreateSurfaceBand($"Lane{laneId}{name}Field", center + Vector3.down * 0.004f, new Vector3(width, 0.012f, depth), BoardPlateInsetColor(laneId));
            inset.transform.rotation = Quaternion.Euler(0f, (TileVariation(laneId, Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.z)) - 0.5f) * 3f, 0f);
            CreateSurfaceBand($"Lane{laneId}{name}NorthBevel", center + new Vector3(0f, 0.003f, depth * 0.5f), new Vector3(width * 0.92f, 0.012f, 0.026f), BoardPlateLightBevelColor(laneId));
            CreateSurfaceBand($"Lane{laneId}{name}SouthBevel", center + new Vector3(0f, 0.002f, -depth * 0.5f), new Vector3(width * 0.92f, 0.012f, 0.026f), BoardPlateDarkBevelColor(laneId));
            CreateSurfaceBand($"Lane{laneId}{name}WestBevel", center + new Vector3(-width * 0.5f, 0.002f, 0f), new Vector3(0.026f, 0.012f, depth * 0.84f), BoardPlateDarkBevelColor(laneId));
            CreateSurfaceBand($"Lane{laneId}{name}EastBevel", center + new Vector3(width * 0.5f, 0.003f, 0f), new Vector3(0.026f, 0.012f, depth * 0.84f), BoardPlateLightBevelColor(laneId));

            if (isPlayerLane)
            {
                CreateSurfaceBand($"Lane{laneId}{name}CornerChip", center + new Vector3(width * 0.28f, 0.006f, depth * 0.22f), new Vector3(width * 0.18f, 0.012f, 0.028f), TileEdgeHighlightColor(laneId));
            }
        }

        private void CreateEndpointPlateDetails(int laneId, int y, Color signalColor, bool isPlayerLane, bool isSpawn)
        {
            var offset = LaneOffset(laneId);
            var z = WorldZ(y);
            var signal = EndpointPlateSignalColor(signalColor, isPlayerLane);
            var shadow = BoardContactShadowColor(laneId);
            var direction = isSpawn ? -1f : 1f;
            var label = isSpawn ? "Spawn" : "Leak";
            var center = new Vector3(offset + CenterColumn, -0.03f, z);

            CreateEndpointDisc($"Lane{laneId}{label}FoundationShadow", center + Vector3.down * 0.055f, isPlayerLane ? 2.62f : 2.3f, 0.032f, BoardContactShadowColor(laneId));
            CreateEndpointDisc($"Lane{laneId}{label}OuterStoneRing", center + Vector3.down * 0.025f, isPlayerLane ? 2.34f : 2.04f, 0.05f, EndpointStoneRingColor(isSpawn, isPlayerLane));
            CreateEndpointDisc($"Lane{laneId}{label}StoneRing", center + Vector3.up * 0.008f, isPlayerLane ? 1.96f : 1.72f, 0.045f, EndpointOuterRingColor(isSpawn, isPlayerLane));
            CreateEndpointDisc($"Lane{laneId}{label}InnerPlate", center + Vector3.up * 0.04f, isPlayerLane ? 1.28f : 1.08f, 0.04f, EndpointInnerPlateColor(isSpawn, isPlayerLane));
            CreateEndpointDisc($"Lane{laneId}{label}DeepRecess", center + new Vector3(0f, 0.058f, direction * 0.03f), isSpawn ? 0.82f : 0.96f, 0.018f, EndpointDeepRecessColor(isSpawn, isPlayerLane));
            CreateEndpointDisc($"Lane{laneId}{label}SignalCore", center + new Vector3(0f, 0.074f, direction * 0.08f), isSpawn ? 0.42f : 0.56f, 0.05f, signal);
            CreateEndpointDisc($"Lane{laneId}{label}CoreHotspot", center + new Vector3(0f, 0.106f, direction * 0.09f), isSpawn ? 0.24f : 0.32f, 0.018f, isSpawn ? EndpointPortalBrightColor(isPlayerLane) : EndpointLeakHotspotColor(isPlayerLane));
            CreateEndpointStoneSegments(laneId, label, center, isSpawn, isPlayerLane);

            CreateSurfaceBand($"Lane{laneId}{label}PlateWestButtress", new Vector3(offset + CenterColumn - 1.38f, 0.018f, z), new Vector3(0.18f, 0.05f, 1.34f), EndpointOuterRingColor(isSpawn, isPlayerLane));
            CreateSurfaceBand($"Lane{laneId}{label}PlateEastButtress", new Vector3(offset + CenterColumn + 1.38f, 0.018f, z), new Vector3(0.18f, 0.05f, 1.34f), EndpointOuterRingColor(isSpawn, isPlayerLane));
            CreateSurfaceBand($"Lane{laneId}{label}PlateNorthButtress", new Vector3(offset + CenterColumn, 0.012f, z + 0.68f), new Vector3(2.5f, 0.048f, 0.16f), EndpointOuterRingColor(isSpawn, isPlayerLane));
            CreateSurfaceBand($"Lane{laneId}{label}PlateSouthButtress", new Vector3(offset + CenterColumn, 0.012f, z - 0.68f), new Vector3(2.5f, 0.048f, 0.16f), EndpointOuterRingColor(isSpawn, isPlayerLane));
            CreateSurfaceBand($"Lane{laneId}{label}PlateWestShadow", new Vector3(offset + CenterColumn - 1.52f, -0.004f, z), new Vector3(0.08f, 0.014f, 1.34f), shadow);
            CreateSurfaceBand($"Lane{laneId}{label}PlateEastShadow", new Vector3(offset + CenterColumn + 1.52f, -0.004f, z), new Vector3(0.08f, 0.014f, 1.34f), shadow);
            CreateSurfaceBand($"Lane{laneId}{label}PlateNorthShadow", new Vector3(offset + CenterColumn, -0.004f, z + 0.76f), new Vector3(2.68f, 0.014f, 0.07f), shadow);
            CreateSurfaceBand($"Lane{laneId}{label}PlateSouthShadow", new Vector3(offset + CenterColumn, -0.004f, z - 0.76f), new Vector3(2.68f, 0.014f, 0.07f), shadow);
            CreateSurfaceBand($"Lane{laneId}{label}PlateCoreMark", new Vector3(offset + CenterColumn, 0.082f, z + direction * 0.18f), new Vector3(0.68f, 0.016f, 0.15f), signal);
            CreateSurfaceBand($"Lane{laneId}{label}PlateLeftMark", new Vector3(offset + CenterColumn - 0.48f, 0.04f, z + direction * 0.02f), new Vector3(0.48f, 0.016f, 0.11f), signal);
            CreateSurfaceBand($"Lane{laneId}{label}PlateRightMark", new Vector3(offset + CenterColumn + 0.48f, 0.04f, z + direction * 0.02f), new Vector3(0.48f, 0.016f, 0.11f), signal);
            var leftChevron = CreateSurfaceBand($"Lane{laneId}{label}PlateChevronLeft", new Vector3(offset + CenterColumn - 0.29f, 0.05f, z + direction * 0.42f), new Vector3(0.095f, 0.016f, 0.44f), signal);
            leftChevron.transform.rotation = Quaternion.Euler(0f, direction * 32f, 0f);
            var rightChevron = CreateSurfaceBand($"Lane{laneId}{label}PlateChevronRight", new Vector3(offset + CenterColumn + 0.29f, 0.05f, z + direction * 0.42f), new Vector3(0.095f, 0.016f, 0.44f), signal);
            rightChevron.transform.rotation = Quaternion.Euler(0f, direction * -32f, 0f);

            if (isSpawn)
            {
                CreateEndpointDisc($"Lane{laneId}{label}PortalGlow", center + new Vector3(0f, 0.092f, -0.14f), isPlayerLane ? 0.88f : 0.66f, 0.026f, EndpointPortalColor(isPlayerLane));
                CreateEndpointDisc($"Lane{laneId}{label}PortalThroat", center + new Vector3(0f, 0.126f, -0.18f), isPlayerLane ? 0.46f : 0.36f, 0.04f, EndpointDeepRecessColor(isSpawn, isPlayerLane));
                var portalFacetA = CreateSurfaceBand($"Lane{laneId}{label}PortalFacetA", new Vector3(offset + CenterColumn, 0.116f, z - 0.12f), new Vector3(0.14f, 0.018f, 0.62f), EndpointPortalBrightColor(isPlayerLane));
                portalFacetA.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                var portalFacetB = CreateSurfaceBand($"Lane{laneId}{label}PortalFacetB", new Vector3(offset + CenterColumn, 0.118f, z - 0.12f), new Vector3(0.14f, 0.018f, 0.62f), EndpointPortalBrightColor(isPlayerLane));
                portalFacetB.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
                var portalFacetC = CreateSurfaceBand($"Lane{laneId}{label}PortalFacetC", new Vector3(offset + CenterColumn, 0.142f, z - 0.18f), new Vector3(0.09f, 0.018f, 0.84f), EndpointPortalBrightColor(isPlayerLane));
                portalFacetC.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                CreateSurfaceBand($"Lane{laneId}{label}PortalNorthRim", new Vector3(offset + CenterColumn, 0.102f, z + 0.26f), new Vector3(0.82f, 0.016f, 0.08f), EndpointRimHighlightColor(isSpawn, isPlayerLane));
                CreateSurfaceBand($"Lane{laneId}{label}PortalSouthRim", new Vector3(offset + CenterColumn, 0.102f, z - 0.56f), new Vector3(0.82f, 0.016f, 0.08f), EndpointRimHighlightColor(isSpawn, isPlayerLane));
                CreateSurfaceBand($"Lane{laneId}{label}PortalSideRimWest", new Vector3(offset + CenterColumn - 0.48f, 0.112f, z - 0.16f), new Vector3(0.08f, 0.018f, 0.72f), EndpointRimHighlightColor(isSpawn, isPlayerLane));
                CreateSurfaceBand($"Lane{laneId}{label}PortalSideRimEast", new Vector3(offset + CenterColumn + 0.48f, 0.112f, z - 0.16f), new Vector3(0.08f, 0.018f, 0.72f), EndpointRimHighlightColor(isSpawn, isPlayerLane));
                CreateSurfaceBand($"Lane{laneId}{label}ChevronForwardA", new Vector3(offset + CenterColumn, 0.074f, z - 0.82f), new Vector3(0.115f, 0.016f, 0.42f), signal).transform.rotation = Quaternion.Euler(0f, 42f, 0f);
                CreateSurfaceBand($"Lane{laneId}{label}ChevronForwardB", new Vector3(offset + CenterColumn, 0.074f, z - 0.82f), new Vector3(0.115f, 0.016f, 0.42f), signal).transform.rotation = Quaternion.Euler(0f, -42f, 0f);
                CreateEndpointPylon(laneId, $"{label}WestPylon", new Vector3(offset + CenterColumn - 1.02f, 0.11f, z + 0.28f), signal, isPlayerLane);
                CreateEndpointPylon(laneId, $"{label}EastPylon", new Vector3(offset + CenterColumn + 1.02f, 0.11f, z + 0.28f), signal, isPlayerLane);
                CreateEndpointPylon(laneId, $"{label}NorthWestPylon", new Vector3(offset + CenterColumn - 0.72f, 0.09f, z - 0.62f), EndpointPortalBrightColor(isPlayerLane), isPlayerLane);
                CreateEndpointPylon(laneId, $"{label}NorthEastPylon", new Vector3(offset + CenterColumn + 0.72f, 0.09f, z - 0.62f), EndpointPortalBrightColor(isPlayerLane), isPlayerLane);
            }
            else
            {
                CreateEndpointDisc($"Lane{laneId}{label}DrainGlow", center + new Vector3(0f, 0.09f, -0.05f), isPlayerLane ? 0.96f : 0.76f, 0.026f, EndpointDrainGlowColor(isPlayerLane));
                CreateEndpointDisc($"Lane{laneId}{label}DrainPit", center + new Vector3(0f, 0.118f, -0.02f), isPlayerLane ? 0.68f : 0.54f, 0.035f, EndpointDeepRecessColor(isSpawn, isPlayerLane));
                CreateSurfaceBand($"Lane{laneId}{label}DrainWell", new Vector3(offset + CenterColumn, 0.104f, z - 0.02f), new Vector3(1.08f, 0.022f, 0.92f), EndpointDeepRecessColor(isSpawn, isPlayerLane));
                for (var i = -3; i <= 3; i++)
                {
                    var slat = CreateSurfaceBand($"Lane{laneId}{label}DrainSlat{i}", new Vector3(offset + CenterColumn + i * 0.14f, 0.13f, z + 0.02f), new Vector3(0.058f, 0.022f, 0.82f), EndpointRimHighlightColor(isSpawn, isPlayerLane));
                    slat.transform.rotation = Quaternion.Euler(0f, i * 2.5f, 0f);
                }

                CreateSurfaceBand($"Lane{laneId}{label}DrainCrossbar", new Vector3(offset + CenterColumn, 0.132f, z + 0.35f), new Vector3(0.9f, 0.018f, 0.065f), signal);
                CreateSurfaceBand($"Lane{laneId}{label}DrainJawNorth", new Vector3(offset + CenterColumn, 0.14f, z + 0.62f), new Vector3(1.24f, 0.032f, 0.11f), EndpointLeakHotspotColor(isPlayerLane));
                CreateSurfaceBand($"Lane{laneId}{label}DrainJawSouth", new Vector3(offset + CenterColumn, 0.14f, z - 0.62f), new Vector3(1.24f, 0.032f, 0.11f), EndpointLeakHotspotColor(isPlayerLane));
                CreateSurfaceBand($"Lane{laneId}{label}DrainArrowLeft", new Vector3(offset + CenterColumn - 0.22f, 0.132f, z - 0.88f), new Vector3(0.13f, 0.018f, 0.52f), signal).transform.rotation = Quaternion.Euler(0f, -36f, 0f);
                CreateSurfaceBand($"Lane{laneId}{label}DrainArrowRight", new Vector3(offset + CenterColumn + 0.22f, 0.132f, z - 0.88f), new Vector3(0.13f, 0.018f, 0.52f), signal).transform.rotation = Quaternion.Euler(0f, 36f, 0f);
            }
        }

        private void CreateEndpointStoneSegments(int laneId, string label, Vector3 center, bool isSpawn, bool isPlayerLane)
        {
            var radius = isPlayerLane ? 1.04f : 0.9f;
            var color = EndpointRimHighlightColor(isSpawn, isPlayerLane);
            var shadow = EndpointDeepRecessColor(isSpawn, isPlayerLane);
            for (var index = 0; index < 10; index++)
            {
                var angle = index * Mathf.PI * 2f / 10f;
                var x = Mathf.Sin(angle) * radius;
                var z = Mathf.Cos(angle) * radius;
                var segment = CreateSurfaceBand(
                    $"Lane{laneId}{label}StoneSegment{index}",
                    center + new Vector3(x, 0.102f + (index % 2) * 0.006f, z),
                    new Vector3(0.36f, 0.018f, 0.115f),
                    index % 2 == 0 ? color : EndpointStoneHighlightColor(isSpawn, isPlayerLane));
                segment.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);

                if (index % 2 == 1)
                {
                    var groove = CreateSurfaceBand(
                        $"Lane{laneId}{label}StoneGroove{index}",
                        center + new Vector3(Mathf.Sin(angle + 0.16f) * (radius * 0.86f), 0.096f, Mathf.Cos(angle + 0.16f) * (radius * 0.86f)),
                        new Vector3(0.18f, 0.012f, 0.045f),
                        shadow);
                    groove.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg + 18f, 0f);
                }
            }
        }

        private void CreateEndpointDisc(string name, Vector3 position, float diameter, float height, Color color)
        {
            var disc = CreatePrimitive(name, PrimitiveType.Cylinder);
            disc.transform.position = position;
            disc.transform.localScale = new Vector3(diameter, height, diameter);
            SetColor(disc, color);
            laneDecorations.Add(disc);
        }

        private void CreateEndpointPylon(int laneId, string name, Vector3 position, Color color, bool isPlayerLane)
        {
            var pylon = CreatePrimitive($"Lane{laneId}{name}", PrimitiveType.Cylinder);
            pylon.transform.position = position;
            pylon.transform.localScale = Vector3.one * (isPlayerLane ? 0.22f : 0.17f);
            pylon.transform.localScale = new Vector3(pylon.transform.localScale.x, isPlayerLane ? 0.42f : 0.32f, pylon.transform.localScale.z);
            SetColor(pylon, color);
            laneDecorations.Add(pylon);
        }

        private GameObject CreateSurfaceBand(string name, Vector3 position, Vector3 scale, Color color)
        {
            var band = CreatePrimitive(name, PrimitiveType.Cube);
            band.transform.position = position;
            band.transform.localScale = scale;
            SetColor(band, color);
            laneDecorations.Add(band);
            return band;
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
            var color = RouteTriangleColor(laneId);
            var shaft = CreatePrimitive($"Lane{laneId}Flow_{y}_Shaft", PrimitiveType.Cube);
            shaft.transform.position = new Vector3(offset + CenterColumn, -0.005f, WorldZ(y));
            shaft.transform.localScale = new Vector3(0.035f, 0.032f, 0.18f);
            SetColor(shaft, color);
            laneDecorations.Add(shaft);

            var eastHead = CreatePrimitive($"Lane{laneId}Flow_{y}_HeadA", PrimitiveType.Cube);
            eastHead.transform.position = new Vector3(offset + CenterColumn + 0.12f, 0f, WorldZ(y) - 0.18f);
            eastHead.transform.rotation = Quaternion.Euler(0f, 42f, 0f);
            eastHead.transform.localScale = new Vector3(0.05f, 0.035f, 0.24f);
            SetColor(eastHead, color);
            laneDecorations.Add(eastHead);

            var westHead = CreatePrimitive($"Lane{laneId}Flow_{y}_HeadB", PrimitiveType.Cube);
            westHead.transform.position = new Vector3(offset + CenterColumn - 0.12f, 0f, WorldZ(y) - 0.18f);
            westHead.transform.rotation = Quaternion.Euler(0f, -42f, 0f);
            westHead.transform.localScale = new Vector3(0.05f, 0.035f, 0.24f);
            SetColor(westHead, color);
            laneDecorations.Add(westHead);

            var triangleBase = CreatePrimitive($"Lane{laneId}Flow_{y}_Base", PrimitiveType.Cube);
            triangleBase.transform.position = new Vector3(offset + CenterColumn, -0.002f, WorldZ(y) + 0.03f);
            triangleBase.transform.localScale = new Vector3(0.28f, 0.028f, 0.035f);
            SetColor(triangleBase, new Color(color.r * 0.72f, color.g * 0.72f, color.b * 0.72f));
            laneDecorations.Add(triangleBase);
        }

        private GameObject CreateBoardRail(string name, Vector3 position, Vector3 scale, Color color)
        {
            var rail = CreatePrimitive(name, PrimitiveType.Cube);
            rail.transform.position = position;
            rail.transform.localScale = scale;
            SetColor(rail, color);
            laneDecorations.Add(rail);
            return rail;
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
            var gateColor = EndpointPlateSignalColor(color, laneId == 1);
            CreateSurfaceBand($"Lane{laneId}{label}GateCrossbar", new Vector3(offset + BoardCenterX, -0.16f, z), new Vector3(LaneWidth - 2.1f, 0.08f, 0.1f), gateColor);
            CreateSurfaceBand($"Lane{laneId}{label}GateLeftBrace", new Vector3(offset + 1.05f, -0.08f, z), new Vector3(0.46f, 0.12f, 0.12f), gateColor);
            CreateSurfaceBand($"Lane{laneId}{label}GateRightBrace", new Vector3(offset + LaneWidth - 2.05f, -0.08f, z), new Vector3(0.46f, 0.12f, 0.12f), gateColor);
        }

        private void CreateLaneEndpointBox(int laneId, int x, int y, string boxName, Color color)
        {
            var isSpawn = y == 0;
            var disc = CreatePrimitive($"Lane{laneId}{boxName}", PrimitiveType.Cylinder);
            disc.transform.position = GridToWorld(new GridPosition(x, y), new LaneId(laneId)) + Vector3.down * 0.46f;
            disc.transform.localScale = new Vector3(laneId == 1 ? 1.88f : 1.6f, 0.13f, laneId == 1 ? 1.88f : 1.6f);
            SetColor(disc, EndpointBaseColor(color, laneId == 1, isSpawn));
            laneDecorations.Add(disc);
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
                return new Color(0.08f, 0.18f, 0.19f);
            }

            if (x == CenterColumn && y == LaneLength - 1)
            {
                return new Color(0.16f, 0.055f, 0.052f);
            }

            if (x == CenterColumn)
            {
                var routeVariation = TileVariation(laneId, x, y) * 0.016f;
                return laneId == 1
                    ? new Color(0.07f + routeVariation, 0.145f + routeVariation, 0.225f + routeVariation)
                    : new Color(0.044f + routeVariation * 0.7f, 0.085f + routeVariation * 0.7f, 0.145f + routeVariation * 0.7f);
            }

            var checker = (x + y + laneId) % 2 == 0 ? 0.012f : 0f;
            var laneTint = laneId == 1 ? 0.014f : 0f;
            var buildColumn = x < CenterColumn ? 0.004f : 0.01f;
            var stoneVariation = TileVariation(laneId, x, y) * 0.014f;
            var edgeLift = x == 0 || x == LaneWidth - 1 ? 0.008f : 0f;
            return new Color(0.052f + checker + laneTint + buildColumn + stoneVariation + edgeLift, 0.058f + checker + laneTint + stoneVariation * 0.82f + edgeLift, 0.072f + checker + laneTint + stoneVariation * 0.55f + edgeLift);
        }

        private static Vector3 TowerRoleScale(string towerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control") || ContainsRole(towerId, "pulse"))
            {
                return new Vector3(0.92f, 0.34f, 0.92f);
            }

            if (IsRelayTower(towerId))
            {
                return new Vector3(0.46f, 0.92f, 0.46f);
            }

            if (ContainsRole(towerId, "prism"))
            {
                return new Vector3(0.42f, 1.22f, 0.42f);
            }

            return new Vector3(0.48f, 1.08f, 0.48f);
        }

        private static float TowerRoleLift(string towerId)
        {
            if (IsRelayTower(towerId))
            {
                return 0.18f;
            }

            return 0.12f;
        }

        private static Vector3 CreepRoleScale(string creepId, CreepVisualProfile visualProfile)
        {
            if (visualProfile != null && visualProfile.HasScale)
            {
                return new Vector3(
                    visualProfile.Scale.x * 1.18f,
                    visualProfile.Scale.y * 1.12f,
                    visualProfile.Scale.z * 1.18f);
            }

            if (ContainsRole(creepId, "swarm"))
            {
                return new Vector3(0.24f, 0.12f, 0.24f);
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank"))
            {
                return new Vector3(0.9f, 0.5f, 1.02f);
            }

            if (ContainsRole(creepId, "boss"))
            {
                return new Vector3(0.82f, 0.72f, 0.82f);
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                return new Vector3(0.36f, 0.2f, 0.58f);
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

            return new Vector3(0.42f, 0.24f, 0.72f);
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

        private static CreepMotion CreepRoleMotion(string creepId, CreepVisualProfile visualProfile)
        {
            var time = Time.time;
            var motionStyle = visualProfile != null ? visualProfile.MotionStyle : CreepVisualMotionStyle.Auto;
            if (motionStyle == CreepVisualMotionStyle.ClusterJitter || motionStyle == CreepVisualMotionStyle.Auto && ContainsRole(creepId, "swarm"))
            {
                var pulse = Mathf.Sin(time * 15f) * 0.045f;
                return new CreepMotion(new Vector3(0.16f + pulse, 0f, -pulse * 0.65f), Quaternion.Euler(0f, time * 60f, 0f));
            }

            if (motionStyle == CreepVisualMotionStyle.HeavyBob || motionStyle == CreepVisualMotionStyle.Auto && (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss")))
            {
                var weight = Mathf.Abs(Mathf.Sin(time * 3.4f)) * 0.055f;
                var sway = Mathf.Sin(time * 3.4f) * 1.5f;
                return new CreepMotion(new Vector3(-0.16f, -weight, 0f), Quaternion.Euler(0f, 0f, sway));
            }

            if (motionStyle == CreepVisualMotionStyle.Hover || motionStyle == CreepVisualMotionStyle.Auto && (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air")))
            {
                var hover = Mathf.Sin(time * 5f) * 0.08f;
                return new CreepMotion(Vector3.up * hover, Quaternion.Euler(0f, time * 80f, 0f));
            }

            if (motionStyle == CreepVisualMotionStyle.Shimmer || motionStyle == CreepVisualMotionStyle.Auto && (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth")))
            {
                var shimmer = Mathf.Sin(time * 8f) * 0.035f;
                return new CreepMotion(new Vector3(shimmer, 0.02f, 0.04f), Quaternion.Euler(0f, time * 45f, 0f));
            }

            if (motionStyle == CreepVisualMotionStyle.SiegeWindup || motionStyle == CreepVisualMotionStyle.Auto && (ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege")))
            {
                var windup = Mathf.Sin(time * 6f) * 4f;
                return new CreepMotion(Vector3.zero, Quaternion.Euler(0f, 0f, windup));
            }

            if (motionStyle == CreepVisualMotionStyle.AuraPulse)
            {
                var pulse = Mathf.Abs(Mathf.Sin(time * 5f)) * 0.035f;
                return new CreepMotion(Vector3.up * pulse, Quaternion.Euler(0f, time * 35f, 0f));
            }

            var dart = Mathf.Sin(time * 13f) * 0.055f;
            return new CreepMotion(new Vector3(dart, 0f, 0.06f), Quaternion.Euler(7f, 0f, -Mathf.Sin(time * 13f) * 4f));
        }

        private static CreepDeathCueStyle CreepDeathCueStyleFor(string creepId, CreepVisualProfile visualProfile)
        {
            var deathCueStyle = visualProfile != null ? visualProfile.DeathCueStyle : CreepDeathCueStyle.Auto;
            if (deathCueStyle != CreepDeathCueStyle.Auto)
            {
                return deathCueStyle;
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss"))
            {
                return CreepDeathCueStyle.HeavyShatter;
            }

            if (ContainsRole(creepId, "swarm"))
            {
                return CreepDeathCueStyle.ShardScatter;
            }

            if (ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth") || ContainsRole(creepId, "aura") || ContainsRole(creepId, "support"))
            {
                return CreepDeathCueStyle.SoftDissolve;
            }

            return CreepDeathCueStyle.SparkBurst;
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

            if (IsRelayTower(towerId))
            {
                return SignalGold;
            }

            return OwnerAccent(ownerId);
        }

        private static void ApplyTowerColor(GameObject towerObject, string towerId, int ownerId, TowerVisualProfile visualProfile)
        {
            var roleColor = BoostValue(TowerMarkerColor(towerId), 1.16f);
            var baseColor = BoostValue(TowerBaseColor(towerId), 1.08f);
            var ownerColor = OwnerAccent(ownerId);
            var rangeColor = DimValue(roleColor, 0.7f);

            if (visualProfile == null || visualProfile.Prefab == null)
            {
                SetColor(towerObject, TowerRoleColor(towerId, ownerId));
                return;
            }

            SetProfileColor(towerObject, visualProfile.BodyRendererPath, baseColor);
            SetProfileColor(towerObject, visualProfile.RoleMarkerRendererPath, roleColor);
            SetProfileColor(towerObject, visualProfile.OwnerTrimRendererPath, ownerColor);
            SetProfileColor(towerObject, visualProfile.RangeHaloRendererPath, rangeColor);
        }

        private static Color BoostValue(Color color, float amount) =>
            new Color(Mathf.Clamp01(color.r * amount), Mathf.Clamp01(color.g * amount), Mathf.Clamp01(color.b * amount), color.a);

        private static Color DimValue(Color color, float amount) =>
            new Color(Mathf.Clamp01(color.r * amount), Mathf.Clamp01(color.g * amount), Mathf.Clamp01(color.b * amount), color.a);

        private static Color CreepRoleColor(string creepId, int senderId)
        {
            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss"))
            {
                return new Color(1f, 0.62f, 0.26f);
            }

            if (ContainsRole(creepId, "swarm"))
            {
                return new Color(0.72f, 1f, 0.86f);
            }

            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                return new Color(0.82f, 0.72f, 1f);
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
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

        private static Color CreepBodyColor(string creepId, int senderId, float healthFraction, bool isHitFlashing)
        {
            if (isHitFlashing)
            {
                return new Color(1f, 0.94f, 0.62f);
            }

            var baseColor = CreepRoleColor(creepId, senderId);
            if (healthFraction >= 0.45f)
            {
                return baseColor;
            }

            var damageTint = ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank")
                ? new Color(0.68f, 0.22f, 0.16f)
                : new Color(0.42f, 0.48f, 0.58f);
            var amount = Mathf.InverseLerp(0.45f, 0.05f, healthFraction) * 0.55f;
            return Color.Lerp(baseColor, damageTint, amount);
        }

        private static float CreepHealthFraction(string creepId, int health)
        {
            return Mathf.Clamp01(health / (float)Mathf.Max(1, CreepMaxHealth(creepId)));
        }

        private static int CreepMaxHealth(string creepId)
        {
            if (ContainsRole(creepId, "swarm"))
            {
                return 5;
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank"))
            {
                return 24;
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                return 14;
            }

            if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
            {
                return 48;
            }

            if (ContainsRole(creepId, "boss"))
            {
                return 60;
            }

            return 10;
        }

        private static void ConfigureTowerRoleMarker(GameObject towerObject, string towerId, int ownerId)
        {
            var roleColor = TowerMarkerColor(towerId);
            var baseColor = TowerBaseColor(towerId);
            var ownerColor = OwnerAccent(ownerId);
            var isControl = IsControlTower(towerId);
            var isRelay = IsRelayTower(towerId);
            var isPulse = IsPulseTower(towerId);
            var isPrism = IsPrismTower(towerId);
            var isFocused = !isControl && !isRelay && !isPulse && !isPrism;

            var basePlate = EnsureChild(towerObject, "RoleBasePlate", PrimitiveType.Cylinder);
            var ownerTrim = EnsureChild(towerObject, "OwnerTrim", PrimitiveType.Cylinder);
            var ownerPylon = EnsureChild(towerObject, "OwnerPylon", PrimitiveType.Cube);
            var ownerPennant = EnsureChild(towerObject, "OwnerPennant", PrimitiveType.Cube);
            var rangeHalo = EnsureChild(towerObject, "RangeReadHalo", PrimitiveType.Cylinder);

            ConfigureChild(basePlate, true, new Vector3(0f, -0.28f, 0f), TowerBaseScale(towerId), baseColor);
            ConfigureChild(ownerTrim, true, new Vector3(0f, -0.19f, 0f), TowerOwnerTrimScale(towerId), ownerColor);
            ConfigureChild(ownerPylon, true, new Vector3(-0.42f, 0.18f, -0.42f), new Vector3(0.08f, 0.52f, 0.08f), ownerColor);
            ConfigureChild(ownerPennant, true, new Vector3(-0.24f, 0.44f, -0.42f), new Vector3(0.34f, 0.12f, 0.06f), ownerColor);
            ConfigureChild(rangeHalo, true, new Vector3(0f, -0.22f, 0f), TowerRangeHaloScale(towerId), roleColor);

            var focusedBowLeft = EnsureChild(towerObject, "FocusedBowLeft", PrimitiveType.Cube);
            var focusedBowRight = EnsureChild(towerObject, "FocusedBowRight", PrimitiveType.Cube);
            var focusedSpire = EnsureChild(towerObject, "FocusedSpire", PrimitiveType.Cube);
            var focusedString = EnsureChild(towerObject, "FocusedString", PrimitiveType.Cube);
            var focusedLens = EnsureChild(towerObject, "FocusedLens", PrimitiveType.Sphere);
            var focusedArrowHead = EnsureChild(towerObject, "FocusedArrowHead", PrimitiveType.Cube);

            ConfigureChild(focusedBowLeft, isFocused, new Vector3(-0.28f, 0.58f, 0f), new Vector3(0.1f, 0.82f, 0.14f), baseColor);
            ConfigureChild(focusedBowRight, isFocused, new Vector3(0.28f, 0.58f, 0f), new Vector3(0.1f, 0.82f, 0.14f), baseColor);
            ConfigureChild(focusedSpire, isFocused, new Vector3(0f, 0.66f, -0.02f), new Vector3(0.14f, 1.08f, 0.14f), roleColor);
            ConfigureChild(focusedString, isFocused, new Vector3(0f, 0.82f, 0.34f), new Vector3(0.08f, 0.72f, 0.08f), MintSignal);
            ConfigureChild(focusedLens, isFocused, new Vector3(0f, 1.18f, 0.08f), new Vector3(0.24f, 0.24f, 0.34f), MintSignal);
            ConfigureChild(focusedArrowHead, isFocused, new Vector3(0f, 1.34f, 0.08f), new Vector3(0.34f, 0.18f, 0.18f), roleColor);

            var controlRing = EnsureChild(towerObject, "ControlRing", PrimitiveType.Cylinder);
            var controlDish = EnsureChild(towerObject, "ControlDish", PrimitiveType.Cylinder);
            var controlCore = EnsureChild(towerObject, "ControlCore", PrimitiveType.Sphere);
            var controlNorthArc = EnsureChild(towerObject, "ControlNorthArc", PrimitiveType.Cube);
            var controlSouthArc = EnsureChild(towerObject, "ControlSouthArc", PrimitiveType.Cube);
            var controlNodeA = EnsureChild(towerObject, "ControlNodeA", PrimitiveType.Sphere);
            var controlNodeB = EnsureChild(towerObject, "ControlNodeB", PrimitiveType.Sphere);
            var controlNodeC = EnsureChild(towerObject, "ControlNodeC", PrimitiveType.Sphere);

            ConfigureChild(controlRing, isControl, new Vector3(0f, 0.24f, 0f), new Vector3(1.42f, 0.04f, 1.42f), roleColor);
            ConfigureChild(controlDish, isControl, new Vector3(0f, 0.58f, 0f), new Vector3(1.1f, 0.05f, 1.1f), baseColor);
            ConfigureChild(controlCore, isControl, new Vector3(0f, 0.78f, 0f), new Vector3(0.34f, 0.34f, 0.34f), roleColor);
            ConfigureChild(controlNorthArc, isControl, new Vector3(0f, 0.78f, 0.54f), new Vector3(0.82f, 0.08f, 0.12f), roleColor);
            ConfigureChild(controlSouthArc, isControl, new Vector3(0f, 0.78f, -0.54f), new Vector3(0.82f, 0.08f, 0.12f), roleColor);
            ConfigureChild(controlNodeA, isControl, new Vector3(0f, 0.88f, 0.52f), new Vector3(0.18f, 0.18f, 0.18f), MintSignal);
            ConfigureChild(controlNodeB, isControl, new Vector3(-0.46f, 0.82f, -0.28f), new Vector3(0.16f, 0.16f, 0.16f), MintSignal);
            ConfigureChild(controlNodeC, isControl, new Vector3(0.46f, 0.82f, -0.28f), new Vector3(0.16f, 0.16f, 0.16f), MintSignal);

            var relayMast = EnsureChild(towerObject, "RelayMast", PrimitiveType.Cube);
            var relayCore = EnsureChild(towerObject, "RelayCore", PrimitiveType.Sphere);
            var relayCapacitorLeft = EnsureChild(towerObject, "RelayCapacitorLeft", PrimitiveType.Cube);
            var relayCapacitorRight = EnsureChild(towerObject, "RelayCapacitorRight", PrimitiveType.Cube);
            var relaySignalTop = EnsureChild(towerObject, "RelaySignalTop", PrimitiveType.Cylinder);
            var relayLowerSignal = EnsureChild(towerObject, "RelayLowerSignal", PrimitiveType.Cylinder);
            var relaySignalBeam = EnsureChild(towerObject, "RelaySignalBeam", PrimitiveType.Cube);

            ConfigureChild(relayMast, isRelay, new Vector3(0f, 0.76f, 0f), new Vector3(0.1f, 1.12f, 0.1f), SignalGold);
            ConfigureChild(relayCore, isRelay, new Vector3(0f, 1.18f, 0f), new Vector3(0.3f, 0.3f, 0.3f), SignalGold);
            ConfigureChild(relayCapacitorLeft, isRelay, new Vector3(-0.28f, 0.5f, 0f), new Vector3(0.12f, 0.56f, 0.12f), baseColor);
            ConfigureChild(relayCapacitorRight, isRelay, new Vector3(0.28f, 0.5f, 0f), new Vector3(0.12f, 0.56f, 0.12f), baseColor);
            ConfigureChild(relaySignalTop, isRelay, new Vector3(0f, 1.54f, 0f), new Vector3(0.56f, 0.04f, 0.56f), MintSignal);
            ConfigureChild(relayLowerSignal, isRelay, new Vector3(0f, 1.34f, 0f), new Vector3(0.38f, 0.035f, 0.38f), SignalGold);
            ConfigureChild(relaySignalBeam, isRelay, new Vector3(0f, 1.44f, 0f), new Vector3(0.06f, 0.4f, 0.06f), MintSignal);

            var pulseCore = EnsureChild(towerObject, "PulseCore", PrimitiveType.Sphere);
            var pulseRingA = EnsureChild(towerObject, "PulseRingA", PrimitiveType.Cylinder);
            var pulseRingB = EnsureChild(towerObject, "PulseRingB", PrimitiveType.Cylinder);
            var pulseArcNorth = EnsureChild(towerObject, "PulseArcNorth", PrimitiveType.Cube);
            var pulseArcSouth = EnsureChild(towerObject, "PulseArcSouth", PrimitiveType.Cube);

            ConfigureChild(pulseCore, isPulse, new Vector3(0f, 0.58f, 0f), new Vector3(0.44f, 0.44f, 0.44f), roleColor);
            ConfigureChild(pulseRingA, isPulse, new Vector3(0f, 0.34f, 0f), new Vector3(1.26f, 0.04f, 1.26f), roleColor);
            ConfigureChild(pulseRingB, isPulse, new Vector3(0f, 0.86f, 0f), new Vector3(0.92f, 0.035f, 0.92f), MintSignal);
            ConfigureChild(pulseArcNorth, isPulse, new Vector3(0f, 0.72f, 0.48f), new Vector3(0.78f, 0.08f, 0.12f), roleColor);
            ConfigureChild(pulseArcSouth, isPulse, new Vector3(0f, 0.72f, -0.48f), new Vector3(0.78f, 0.08f, 0.12f), roleColor);

            var prismSpire = EnsureChild(towerObject, "PrismSpire", PrimitiveType.Cube);
            var prismLens = EnsureChild(towerObject, "PrismLens", PrimitiveType.Sphere);
            var prismBeam = EnsureChild(towerObject, "PrismBeamRead", PrimitiveType.Cube);
            var prismLeftFacet = EnsureChild(towerObject, "PrismLeftFacet", PrimitiveType.Cube);
            var prismRightFacet = EnsureChild(towerObject, "PrismRightFacet", PrimitiveType.Cube);

            ConfigureChild(prismSpire, isPrism, new Vector3(0f, 0.82f, 0f), new Vector3(0.2f, 1.34f, 0.2f), roleColor);
            ConfigureChild(prismLens, isPrism, new Vector3(0f, 1.52f, 0.02f), new Vector3(0.36f, 0.24f, 0.36f), MintSignal);
            ConfigureChild(prismBeam, isPrism, new Vector3(0f, 1.08f, 0.42f), new Vector3(0.06f, 0.82f, 0.06f), MintSignal);
            ConfigureChild(prismLeftFacet, isPrism, new Vector3(-0.22f, 0.66f, 0f), new Vector3(0.08f, 0.82f, 0.12f), baseColor);
            ConfigureChild(prismRightFacet, isPrism, new Vector3(0.22f, 0.66f, 0f), new Vector3(0.08f, 0.82f, 0.12f), baseColor);
        }

        private static bool IsControlTower(string towerId) => ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control") || ContainsRole(towerId, "area");

        private static bool IsArrowTower(string towerId) => ContainsRole(towerId, "arrow") || ContainsRole(towerId, "basic");

        private static bool IsRelayTower(string towerId) => ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay");

        private static bool IsPulseTower(string towerId) => ContainsRole(towerId, "pulse");

        private static bool IsPrismTower(string towerId) => ContainsRole(towerId, "prism");

        private static Vector3 TowerOwnerTrimScale(string towerId)
        {
            if (IsControlTower(towerId) || IsPulseTower(towerId))
            {
                return new Vector3(1.28f, 0.035f, 1.28f);
            }

            if (IsPrismTower(towerId))
            {
                return new Vector3(0.78f, 0.035f, 0.78f);
            }

            if (IsRelayTower(towerId))
            {
                return new Vector3(0.92f, 0.035f, 0.92f);
            }

            return new Vector3(0.84f, 0.035f, 0.84f);
        }

        private static Vector3 TowerBaseScale(string towerId)
        {
            if (IsControlTower(towerId) || IsPulseTower(towerId))
            {
                return new Vector3(1.18f, 0.055f, 1.18f);
            }

            if (IsPrismTower(towerId))
            {
                return new Vector3(0.62f, 0.06f, 0.62f);
            }

            if (IsRelayTower(towerId))
            {
                return new Vector3(0.78f, 0.06f, 0.78f);
            }

            return new Vector3(0.72f, 0.06f, 0.72f);
        }

        private static Vector3 TowerRangeHaloScale(string towerId)
        {
            if (IsRelayTower(towerId))
            {
                return new Vector3(1.45f, 0.018f, 1.45f);
            }

            if (IsPulseTower(towerId))
            {
                return new Vector3(1.72f, 0.018f, 1.72f);
            }

            if (IsPrismTower(towerId))
            {
                return new Vector3(2.25f, 0.018f, 2.25f);
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
            if (IsControlTower(towerId) || IsPulseTower(towerId))
            {
                return new Vector3(0.8f, 0.08f, 0.8f);
            }

            if (IsRelayTower(towerId))
            {
                return new Vector3(0.34f, 0.34f, 0.34f);
            }

            if (IsPrismTower(towerId))
            {
                return new Vector3(0.18f, 0.36f, 0.18f);
            }

            return new Vector3(0.22f, 0.22f, 0.22f);
        }

        private static Color TowerMarkerColor(string towerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "ice") || ContainsRole(towerId, "control"))
            {
                return new Color(0.72f, 0.94f, 1f);
            }

            if (ContainsRole(towerId, "pulse") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "fire") || ContainsRole(towerId, "area"))
            {
                return new Color(1f, 0.7f, 0.28f);
            }

            if (IsPrismTower(towerId))
            {
                return new Color(0.72f, 0.94f, 1f);
            }

            if (IsRelayTower(towerId))
            {
                return SignalGold;
            }

            return MintSignal;
        }

        private static void ConfigureCreepRoleMarker(GameObject creepObject, string creepId, int senderId, float healthFraction, bool isHitFlashing)
        {
            DeactivateKnownCreepMarkers(creepObject);
            var shadow = EnsureChild(creepObject, "GroundShadow", PrimitiveType.Cylinder);
            ConfigureChild(shadow, true, new Vector3(0f, -0.42f, 0f), CreepShadowScale(creepId), new Color(0.015f, 0.022f, 0.035f));

            var isSwarm = ContainsRole(creepId, "swarm");
            var isBoss = ContainsRole(creepId, "boss");
            var isBrute = isBoss || ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank");
            var isAir = ContainsRole(creepId, "flying") || ContainsRole(creepId, "air");
            var isStealth = ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth");
            var isSiege = ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege");
            var isAura = ContainsRole(creepId, "aura") || ContainsRole(creepId, "support");
            var isRunner = !isSwarm && !isBrute && !isAir && !isStealth && !isSiege && !isAura;

            if (isRunner)
            {
                ConfigureRunnerMarker(creepObject, senderId, healthFraction, isHitFlashing);
                return;
            }

            if (isBrute)
            {
                ConfigureBruteMarker(creepObject, isBoss, healthFraction, isHitFlashing);
                return;
            }

            if (isSwarm)
            {
                ConfigureSwarmMarker(creepObject, senderId, healthFraction, isHitFlashing);
                return;
            }

            if (isAir)
            {
                ConfigureAirMarker(creepObject);
                return;
            }

            if (isStealth)
            {
                ConfigureStealthMarker(creepObject);
                return;
            }

            if (isSiege)
            {
                ConfigureSiegeMarker(creepObject);
                return;
            }

            if (isAura)
            {
                ConfigureAuraMarker(creepObject);
            }
        }

        private static void ConfigureRunnerMarker(GameObject creepObject, int senderId, float healthFraction, bool isHitFlashing)
        {
            var senderColor = SenderColor(senderId);
            var accent = isHitFlashing ? new Color(1f, 0.94f, 0.62f) : MintSignal;
            var damageScale = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(healthFraction));

            ConfigureChild(EnsureChild(creepObject, "RunnerNose", PrimitiveType.Cube), true, new Vector3(0f, 0.04f, 0.56f), new Vector3(0.14f, 0.1f, 0.56f), accent);
            ConfigureChild(EnsureChild(creepObject, "RunnerTail", PrimitiveType.Cube), true, new Vector3(0f, -0.02f, -0.44f), new Vector3(0.08f, 0.08f, 0.38f * damageScale), senderColor);
            ConfigureChild(EnsureChild(creepObject, "RunnerLeftFin", PrimitiveType.Cube), true, new Vector3(-0.3f, 0f, -0.06f), new Vector3(0.07f, 0.08f, 0.34f), senderColor);
            ConfigureChild(EnsureChild(creepObject, "RunnerRightFin", PrimitiveType.Cube), true, new Vector3(0.3f, 0f, -0.06f), new Vector3(0.07f, 0.08f, 0.34f), senderColor);
            ConfigureChild(EnsureChild(creepObject, "RunnerSpeedLine", PrimitiveType.Cube), true, new Vector3(0f, -0.18f, -0.82f), new Vector3(0.045f, 0.035f, 0.62f * damageScale), senderColor);
        }

        private static void ConfigureBruteMarker(GameObject creepObject, bool isBoss, float healthFraction, bool isHitFlashing)
        {
            var armorColor = isHitFlashing ? new Color(1f, 0.94f, 0.62f) : new Color(1f, 0.72f, 0.38f);
            var plateColor = healthFraction < 0.35f ? new Color(0.68f, 0.22f, 0.16f) : new Color(0.74f, 0.38f, 0.22f);

            ConfigureChild(EnsureChild(creepObject, "BruteArmor", PrimitiveType.Cube), true, new Vector3(0f, 0.26f, 0f), isBoss ? new Vector3(0.96f, 0.22f, 1.08f) : new Vector3(0.92f, 0.18f, 1.02f), armorColor);
            ConfigureChild(EnsureChild(creepObject, "BruteLeftPlate", PrimitiveType.Cube), true, new Vector3(-0.5f, 0.12f, 0.04f), new Vector3(0.24f, 0.34f, 0.74f), plateColor);
            ConfigureChild(EnsureChild(creepObject, "BruteRightPlate", PrimitiveType.Cube), true, new Vector3(0.5f, 0.12f, 0.04f), new Vector3(0.24f, 0.34f, 0.74f), plateColor);
            ConfigureChild(EnsureChild(creepObject, "BruteCore", PrimitiveType.Sphere), !isBoss, new Vector3(0f, 0.48f, 0.2f), Vector3.one * Mathf.Lerp(0.18f, 0.28f, Mathf.Clamp01(healthFraction)), SignalGold);

            if (!isBoss)
            {
                return;
            }

            ConfigureChild(EnsureChild(creepObject, "BossCrown", PrimitiveType.Cylinder), true, new Vector3(0f, 0.72f, 0f), new Vector3(0.92f, 0.055f, 0.92f), LeakRed);
            ConfigureChild(EnsureChild(creepObject, "BossCore", PrimitiveType.Sphere), true, new Vector3(0f, 0.54f, 0.16f), new Vector3(0.34f, 0.34f, 0.34f), SignalGold);
            ConfigureChild(EnsureChild(creepObject, "BossLeftHorn", PrimitiveType.Cube), true, new Vector3(-0.44f, 0.62f, 0.16f), new Vector3(0.16f, 0.16f, 0.42f), LeakRed);
            ConfigureChild(EnsureChild(creepObject, "BossRightHorn", PrimitiveType.Cube), true, new Vector3(0.44f, 0.62f, 0.16f), new Vector3(0.16f, 0.16f, 0.42f), LeakRed);
        }

        private static void ConfigureSwarmMarker(GameObject creepObject, int senderId, float healthFraction, bool isHitFlashing)
        {
            var senderColor = isHitFlashing ? new Color(1f, 0.94f, 0.62f) : SenderColor(senderId);
            var time = Time.time;
            var jitter = Mathf.Sin(time * 18f) * 0.08f;
            var livingDots = healthFraction > 0.66f ? 5 : healthFraction > 0.33f ? 4 : 3;

            ConfigureChild(EnsureChild(creepObject, "SwarmDotA", PrimitiveType.Sphere), true, new Vector3(-0.58f + jitter, 0.05f, -0.3f), new Vector3(0.58f, 0.58f, 0.58f), senderColor);
            ConfigureChild(EnsureChild(creepObject, "SwarmDotB", PrimitiveType.Sphere), true, new Vector3(0.56f - jitter, 0.05f, 0.28f), new Vector3(0.5f, 0.5f, 0.5f), MintSignal);
            ConfigureChild(EnsureChild(creepObject, "SwarmDotC", PrimitiveType.Sphere), true, new Vector3(0.04f, 0.08f, -0.62f - jitter), new Vector3(0.42f, 0.42f, 0.42f), new Color(0.72f, 1f, 0.86f));
            ConfigureChild(EnsureChild(creepObject, "SwarmDotD", PrimitiveType.Sphere), livingDots >= 4, new Vector3(-0.18f - jitter, 0.06f, 0.6f), new Vector3(0.36f, 0.36f, 0.36f), senderColor);
            ConfigureChild(EnsureChild(creepObject, "SwarmDotE", PrimitiveType.Sphere), livingDots >= 5, new Vector3(0.66f, 0.05f, -0.18f + jitter), new Vector3(0.32f, 0.32f, 0.32f), MintSignal);
            ConfigureChild(EnsureChild(creepObject, "SwarmTrail", PrimitiveType.Cylinder), true, new Vector3(0f, -0.18f, 0f), new Vector3(1.18f, 0.025f, 1.02f), senderColor);
        }

        private static void ConfigureAirMarker(GameObject creepObject)
        {
            ConfigureChild(EnsureChild(creepObject, "AirHoverRing", PrimitiveType.Cylinder), true, new Vector3(0f, -0.52f, 0f), new Vector3(0.88f, 0.04f, 0.88f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(EnsureChild(creepObject, "AirLeftWing", PrimitiveType.Cube), true, new Vector3(-0.5f, 0.02f, 0f), new Vector3(0.42f, 0.08f, 0.18f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(EnsureChild(creepObject, "AirRightWing", PrimitiveType.Cube), true, new Vector3(0.5f, 0.02f, 0f), new Vector3(0.42f, 0.08f, 0.18f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(EnsureChild(creepObject, "AirBeacon", PrimitiveType.Sphere), true, new Vector3(0f, 0.28f, 0f), new Vector3(0.2f, 0.2f, 0.2f), MintSignal);
        }

        private static void ConfigureStealthMarker(GameObject creepObject)
        {
            ConfigureChild(EnsureChild(creepObject, "StealthShimmer", PrimitiveType.Cylinder), true, new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.05f, 1.1f), new Color(0.86f, 0.96f, 1f));
            ConfigureChild(EnsureChild(creepObject, "StealthEchoA", PrimitiveType.Cylinder), true, new Vector3(0f, -0.18f, 0f), new Vector3(1.34f, 0.03f, 1.34f), new Color(0.36f, 0.5f, 0.58f));
            ConfigureChild(EnsureChild(creepObject, "StealthEchoB", PrimitiveType.Cylinder), true, new Vector3(0f, 0.2f, 0f), new Vector3(0.78f, 0.03f, 0.78f), new Color(0.72f, 0.84f, 0.9f));
        }

        private static void ConfigureSiegeMarker(GameObject creepObject)
        {
            ConfigureChild(EnsureChild(creepObject, "SiegeBase", PrimitiveType.Cube), true, new Vector3(0f, -0.02f, -0.06f), new Vector3(0.58f, 0.22f, 0.5f), new Color(0.56f, 0.12f, 0.16f));
            ConfigureChild(EnsureChild(creepObject, "SiegeBarrel", PrimitiveType.Cube), true, new Vector3(0f, 0.08f, 0.42f), new Vector3(0.18f, 0.16f, 0.62f), new Color(1f, 0.38f, 0.44f));
            ConfigureChild(EnsureChild(creepObject, "SiegeSpike", PrimitiveType.Cube), true, new Vector3(0.28f, 0.08f, 0f), new Vector3(0.38f, 0.16f, 0.2f), new Color(1f, 0.3f, 0.36f));
        }

        private static void ConfigureAuraMarker(GameObject creepObject)
        {
            ConfigureChild(EnsureChild(creepObject, "AuraField", PrimitiveType.Cylinder), true, new Vector3(0f, -0.34f, 0f), new Vector3(1.42f, 0.035f, 1.42f), new Color(0.42f, 1f, 0.72f));
            ConfigureChild(EnsureChild(creepObject, "AuraCore", PrimitiveType.Sphere), true, new Vector3(0f, 0.24f, 0f), new Vector3(0.28f, 0.28f, 0.28f), MintSignal);
            ConfigureChild(EnsureChild(creepObject, "AuraNorthNode", PrimitiveType.Sphere), true, new Vector3(0f, 0.02f, 0.46f), new Vector3(0.18f, 0.18f, 0.18f), SignalGold);
            ConfigureChild(EnsureChild(creepObject, "AuraSouthNode", PrimitiveType.Sphere), true, new Vector3(0f, 0.02f, -0.46f), new Vector3(0.18f, 0.18f, 0.18f), SignalGold);
        }

        private static void DeactivateKnownCreepMarkers(GameObject creepObject)
        {
            for (var index = 0; index < CreepMarkerNames.Length; index++)
            {
                var marker = creepObject.transform.Find(CreepMarkerNames[index]);
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
            }
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

            if (ContainsRole(creepId, "siege"))
            {
                return new Vector3(1.05f, 0.025f, 0.95f);
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                return new Vector3(0.92f, 0.02f, 1.12f);
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
            return laneId == 1 ? new Color(0.025f, 0.045f, 0.072f) : new Color(0.018f, 0.026f, 0.046f);
        }

        private static Color LaneGutterColor(int laneId) => laneId == 1 ? new Color(0.018f, 0.034f, 0.054f) : new Color(0.014f, 0.018f, 0.032f);

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

        private static Color TowerShotColor(string towerId, int damage)
        {
            if (IsControlTower(towerId) || IsPrismTower(towerId))
            {
                return new Color(0.72f, 0.94f, 1f);
            }

            if (IsPulseTower(towerId))
            {
                return new Color(1f, 0.7f, 0.28f);
            }

            if (IsRelayTower(towerId))
            {
                return damage >= 5 ? SignalGold : MintSignal;
            }

            return damage >= 5 ? SignalGold : ArcaneBlue;
        }

        private static Color BuildZoneColor(Color tint, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.08f : 0.045f;
            return new Color(0.044f + tint.r * strength, 0.052f + tint.g * strength, 0.068f + tint.b * strength);
        }

        private static Color RouteBandColor(int laneId) => laneId == 1 ? new Color(0.074f, 0.16f, 0.25f) : new Color(0.044f, 0.09f, 0.16f);

        private static Color RouteGuideColor(int laneId) => laneId == 1 ? new Color(0.16f, 0.33f, 0.52f) : new Color(0.085f, 0.18f, 0.32f);

        private static Color RouteInlayColor(int laneId) => laneId == 1 ? new Color(0.085f, 0.22f, 0.36f) : new Color(0.044f, 0.12f, 0.22f);

        private static Color RouteRecessColor(int laneId) => laneId == 1 ? new Color(0.018f, 0.032f, 0.046f) : new Color(0.009f, 0.018f, 0.03f);

        private static Color RouteRibColor(int laneId) => laneId == 1 ? new Color(0.18f, 0.32f, 0.43f) : new Color(0.075f, 0.14f, 0.22f);

        private static Color BuildBandEdgeColor(int laneId) => laneId == 1 ? new Color(0.12f, 0.14f, 0.15f) : new Color(0.064f, 0.074f, 0.086f);

        private static Color EndpointApproachPlateColor(bool isSpawn, bool isPlayerLane)
        {
            if (isSpawn)
            {
                return isPlayerLane ? new Color(0.075f, 0.13f, 0.13f) : new Color(0.036f, 0.068f, 0.068f);
            }

            return isPlayerLane ? new Color(0.12f, 0.045f, 0.04f) : new Color(0.062f, 0.022f, 0.02f);
        }

        private static Color EndpointWashColor(Color color, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.42f : 0.25f;
            return new Color(color.r * strength, color.g * strength, color.b * strength);
        }

        private static Color RouteWearColor(int laneId) => laneId == 1 ? new Color(0.055f, 0.075f, 0.088f) : new Color(0.034f, 0.048f, 0.062f);

        private static Color BuildBandSeamColor(int laneId) => laneId == 1 ? new Color(0.018f, 0.026f, 0.034f) : new Color(0.012f, 0.018f, 0.026f);

        private static Color TileCrackColor(int laneId) => laneId == 1 ? new Color(0.012f, 0.016f, 0.022f) : new Color(0.008f, 0.012f, 0.018f);

        private static Color TileEdgeHighlightColor(int laneId) => laneId == 1 ? new Color(0.13f, 0.15f, 0.16f) : new Color(0.074f, 0.086f, 0.1f);

        private static Color BoardPlateInsetColor(int laneId) => laneId == 1 ? new Color(0.058f, 0.068f, 0.078f) : new Color(0.034f, 0.042f, 0.052f);

        private static Color BoardPlateLightBevelColor(int laneId) => laneId == 1 ? new Color(0.16f, 0.17f, 0.17f) : new Color(0.086f, 0.096f, 0.106f);

        private static Color BoardPlateDarkBevelColor(int laneId) => laneId == 1 ? new Color(0.012f, 0.018f, 0.026f) : new Color(0.006f, 0.01f, 0.016f);

        private static Color BoardContactShadowColor(int laneId) => laneId == 1 ? new Color(0.008f, 0.014f, 0.02f) : new Color(0.004f, 0.008f, 0.014f);

        private static Color LaneFrameTrimColor(int laneId) => laneId == 1 ? new Color(0.065f, 0.078f, 0.088f) : new Color(0.034f, 0.042f, 0.052f);

        private static Color LaneFrameHighlightColor(int laneId) => laneId == 1 ? new Color(0.18f, 0.19f, 0.18f) : new Color(0.086f, 0.094f, 0.1f);

        private static Color LaneFrameAccentColor(Color accent, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.38f : 0.18f;
            return new Color(0.035f + accent.r * strength, 0.04f + accent.g * strength, 0.045f + accent.b * strength);
        }

        private static Color EndpointPlateSignalColor(Color color, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.72f : 0.45f;
            return new Color(color.r * strength, color.g * strength, color.b * strength);
        }

        private static Color RouteTriangleColor(int laneId) => laneId == 1 ? new Color(0.33f, 0.58f, 0.78f) : new Color(0.14f, 0.27f, 0.43f);

        private static Color EndpointBaseColor(Color color, bool isPlayerLane, bool isSpawn)
        {
            var strength = isPlayerLane ? 0.18f : 0.11f;
            var baseTone = isSpawn ? new Color(0.048f, 0.072f, 0.084f) : new Color(0.055f, 0.038f, 0.038f);
            return new Color(baseTone.r + color.r * strength, baseTone.g + color.g * strength, baseTone.b + color.b * strength);
        }

        private static Color EndpointStoneRingColor(bool isSpawn, bool isPlayerLane)
        {
            var laneLift = isPlayerLane ? 0.018f : 0f;
            return isSpawn
                ? new Color(0.145f + laneLift, 0.162f + laneLift, 0.172f + laneLift)
                : new Color(0.172f + laneLift, 0.092f + laneLift * 0.4f, 0.086f + laneLift * 0.4f);
        }

        private static Color EndpointOuterRingColor(bool isSpawn, bool isPlayerLane)
        {
            var laneLift = isPlayerLane ? 0.022f : 0f;
            return isSpawn
                ? new Color(0.088f + laneLift, 0.108f + laneLift, 0.122f + laneLift)
                : new Color(0.115f + laneLift, 0.056f + laneLift * 0.35f, 0.054f + laneLift * 0.3f);
        }

        private static Color EndpointInnerPlateColor(bool isSpawn, bool isPlayerLane)
        {
            var laneLift = isPlayerLane ? 0.02f : 0f;
            return isSpawn
                ? new Color(0.072f + laneLift, 0.112f + laneLift, 0.124f + laneLift)
                : new Color(0.092f + laneLift, 0.038f + laneLift * 0.35f, 0.034f + laneLift * 0.35f);
        }

        private static Color EndpointDeepRecessColor(bool isSpawn, bool isPlayerLane)
        {
            var lift = isPlayerLane ? 0.012f : 0f;
            return isSpawn
                ? new Color(0.018f + lift, 0.032f + lift, 0.036f + lift)
                : new Color(0.026f + lift, 0.006f + lift * 0.25f, 0.006f + lift * 0.2f);
        }

        private static Color EndpointStoneHighlightColor(bool isSpawn, bool isPlayerLane)
        {
            var lift = isPlayerLane ? 0.035f : 0.012f;
            return isSpawn
                ? new Color(0.22f + lift, 0.235f + lift, 0.235f + lift)
                : new Color(0.24f + lift, 0.12f + lift * 0.4f, 0.108f + lift * 0.35f);
        }

        private static Color EndpointRimHighlightColor(bool isSpawn, bool isPlayerLane)
        {
            var lift = isPlayerLane ? 0.028f : 0f;
            return isSpawn
                ? new Color(0.17f + lift, 0.19f + lift, 0.19f + lift)
                : new Color(0.18f + lift, 0.072f + lift * 0.35f, 0.066f + lift * 0.3f);
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

        private static readonly string[] CreepMarkerNames =
        {
            "GroundShadow",
            "RunnerNose",
            "RunnerTail",
            "RunnerLeftFin",
            "RunnerRightFin",
            "RunnerSpeedLine",
            "BruteArmor",
            "BruteLeftPlate",
            "BruteRightPlate",
            "BruteCore",
            "BossCrown",
            "BossCore",
            "BossLeftHorn",
            "BossRightHorn",
            "SwarmDotA",
            "SwarmDotB",
            "SwarmDotC",
            "SwarmDotD",
            "SwarmDotE",
            "SwarmTrail",
            "AirHoverRing",
            "AirLeftWing",
            "AirRightWing",
            "AirBeacon",
            "StealthShimmer",
            "StealthEchoA",
            "StealthEchoB",
            "SiegeBase",
            "SiegeBarrel",
            "SiegeSpike",
            "AuraField",
            "AuraCore",
            "AuraNorthNode",
            "AuraSouthNode"
        };

        private static readonly string[] CreepReadabilityOverlayNames =
        {
            "RoleRunnerChevron",
            "RoleRunnerWake",
            "RoleBruteLeftShoulder",
            "RoleBruteRightShoulder",
            "RoleBruteCenterPlate",
            "RoleSwarmValueRing",
            "RoleSwarmLeadSpark",
            "RoleShadeLeftEcho",
            "RoleShadeRightEcho",
            "RoleShadeCoreLine",
            "RoleSiegeRamHead",
            "RoleSiegeWarningLeft",
            "RoleSiegeWarningRight"
        };

        private static readonly string[] CreepHealthOverlayNames =
        {
            "HealthBarBack",
            "HealthBarFill",
            "HealthBarMidTick",
            "HealthWoundPip"
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

        private readonly struct CreepHealthBarMetrics
        {
            private CreepHealthBarMetrics(float width, float height, float depth, float y, float z, float minFillWidth, float woundOffset, float woundSize)
            {
                Width = width;
                Height = height;
                Depth = depth;
                Y = y;
                Z = z;
                MinFillWidth = minFillWidth;
                WoundOffset = woundOffset;
                WoundSize = woundSize;
            }

            public float Width { get; }
            public float Height { get; }
            public float Depth { get; }
            public float Y { get; }
            public float Z { get; }
            public float FillLift => Height * 0.18f;
            public float MinFillWidth { get; }
            public float WoundOffset { get; }
            public float WoundSize { get; }

            public static CreepHealthBarMetrics For(string creepId)
            {
                if (ContainsRole(creepId, "swarm"))
                {
                    return new CreepHealthBarMetrics(1.08f, 0.07f, 0.16f, 0.68f, 0.62f, 0.07f, 0.08f, 0.1f);
                }

                if (ContainsRole(creepId, "boss"))
                {
                    return new CreepHealthBarMetrics(1.58f, 0.095f, 0.22f, 1.26f, 0.78f, 0.1f, 0.12f, 0.14f);
                }

                if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank"))
                {
                    return new CreepHealthBarMetrics(1.38f, 0.085f, 0.2f, 1.08f, 0.76f, 0.09f, 0.11f, 0.13f);
                }

                if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
                {
                    return new CreepHealthBarMetrics(1.42f, 0.08f, 0.2f, 0.96f, 0.82f, 0.09f, 0.11f, 0.13f);
                }

                if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
                {
                    return new CreepHealthBarMetrics(1.06f, 0.07f, 0.16f, 0.84f, 0.66f, 0.07f, 0.08f, 0.1f);
                }

                return new CreepHealthBarMetrics(0.98f, 0.07f, 0.16f, 0.78f, 0.72f, 0.07f, 0.08f, 0.1f);
            }
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
        BoardOverview,
        SpawnGateFocus,
        LeakGateFocus,
    }
}
