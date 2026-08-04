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
    /// Projects simulation snapshots and events into pooled Unity objects. This class never
    /// changes simulation state; it is safe to disable it for low-spec or diagnostic runs.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed partial class UnityVerticalSliceRenderer : MonoBehaviour
    {
        private const int LaneWidth = 7;

        private const int LaneLength = 16;
        private const int LaneCount = 8;
        private const int LaneSpacing = 9;
        private const int CenterColumn = 3;
        private const float BoardCenterX = (LaneWidth - 1) * 0.5f;
        private const float BoardCenterZ = (LaneLength - 1) * 0.5f;

        private const string DefaultTowerVisualLibraryResourcePath = "TowerVisualLibrary";
        private const string DefaultCreepVisualLibraryResourcePath = "CreepVisualLibrary";

        /// <summary>
        /// World Y of the walkable board surface: the top face of a lane cell, which sits at
        /// <c>GridToWorld().y - 0.53 + 0.06</c>. Contact shadows are pinned to this plane so they
        /// stay glued to the floor regardless of how far a unit's own pivot floats above it.
        /// </summary>
        private const float BoardTopY = -0.12f;

        [SerializeField] private UnitySimulationDriver simulationDriver = null!;
        [SerializeField] private PresentationDetail presentationDetail = PresentationDetail.Full;
        [SerializeField] private LaneCameraFraming cameraFraming = LaneCameraFraming.ActiveLane;
        [SerializeField] private int activeLaneCameraId = 1;
        [SerializeField] private TowerVisualLibrary towerVisualLibrary = null!;
        [SerializeField] private CreepVisualLibrary creepVisualLibrary = null!;
        [SerializeField] private bool suppressCreepGameplayOverlays;

        private LTWAudioDirector audioDirector = null!;

        /// <summary>
        /// Per-entity presentation state, keyed by <see cref="EntityId.Value"/> itself.
        /// </summary>
        /// <remarks>
        /// A long rather than its decimal string. These are indexed once per entity per FRAME, and a
        /// string key means an <c>EntityId.Value.ToString()</c> allocation on every one of those
        /// lookups plus a character-by-character hash and comparison to resolve it; a long key hashes
        /// to itself and compares in one instruction. Nothing here ever wanted text — the string was
        /// only ever a dictionary key, never displayed.
        /// </remarks>
        private readonly Dictionary<long, GameObject> activeTowers = new Dictionary<long, GameObject>();
        private readonly Dictionary<long, GameObject> activeCreeps = new Dictionary<long, GameObject>();

        private readonly Dictionary<long, Vector3> lastKnownPositions = new Dictionary<long, Vector3>();
        private readonly Dictionary<long, string> lastKnownCreepIds = new Dictionary<long, string>();
        private readonly Dictionary<int, string> towerRolesByCell = new Dictionary<int, string>();

        /// <summary>
        /// Per-cell tower tier, kept alongside <see cref="towerRolesByCell"/> and cleared with it.
        /// A CreepDamagedEvent identifies its tower by grid cell, so this is how the weapon effects
        /// find out how upgraded the tower that fired is.
        /// </summary>
        private readonly Dictionary<int, int> towerTiersByCell = new Dictionary<int, int>();
        private readonly Dictionary<long, int> lastCreepHealth = new Dictionary<long, int>();
        private readonly Dictionary<long, float> creepHitFlashUntil = new Dictionary<long, float>();

        /// <summary>Which way each creep is currently facing, in degrees of yaw.</summary>
        /// <remarks>
        /// Creeps had no facing at all: their rotation came entirely from idle role motion, so one
        /// walking a corner kept pointing down the lane and slid sideways through it. Reported from
        /// play as creeps not turning when they hit an obstacle.
        ///
        /// Held per creep because turning has to be SMOOTHED. Snapping to each new heading the tick
        /// a route step changes direction reads as a flicker, not a turn, and a mazed lane is mostly
        /// corners.
        ///
        /// Yaw only. These models are normalised at import — the per-creep ImportEulerAngles of 0,
        /// 90, 135 and 180 exist to point every one of them the same way — and the lane runs along
        /// +Z, so a straight walk resolves to identity and nothing changes on the straights.
        /// </remarks>
        private readonly Dictionary<long, float> creepFacingYaw = new Dictionary<long, float>();

        /// <summary>Creeps currently inside a bramble zone, for the snare cue's rising edge —
        /// the sound plays when a creep is CAUGHT, not every tick it stays held.</summary>
        private readonly HashSet<long> brakedCreepKeys = new HashSet<long>();

        /// <summary>
        /// The hit-flash state each creep's colours were last written for.
        /// </summary>
        /// <remarks>
        /// Colour is per-SNAPSHOT work with one per-frame input: the flash decays on
        /// <see cref="Time.time"/> and expires mid-tick, so a colour pass gated purely on the
        /// snapshot would leave a struck creep pale until the next tick — up to 0.25s of the wrong
        /// colour against a 0.16s flash. Tracking what was last applied re-runs the colour pass on
        /// the frame the flash turns on and the frame it turns off, and on no other frame, which is
        /// two applications per hit rather than sixty per second per creep.
        /// </remarks>
        private readonly Dictionary<long, bool> creepHitFlashApplied = new Dictionary<long, bool>();

        private readonly HashSet<long> visibleKeys = new HashSet<long>();

        /// <summary>Scratch list for the release sweeps, reused so they stop allocating per frame.</summary>
        private readonly List<long> keysToReleaseScratch = new List<long>();

        private readonly List<TimedPresentation> timedPresentations = new List<TimedPresentation>();

        /// <summary>How many beams are alive, so the cap can be checked without scanning.</summary>
        /// <remarks>
        /// A counter rather than a second list, because beams already live in timedPresentations and
        /// a parallel list would be two things to keep in step. Incremented where one is added and
        /// decremented where one expires; both sites are the only places beams enter and leave.
        /// </remarks>
        private int liveBeams;

        public PresentationDetail Detail => presentationDetail;

        public int ActivePresentationObjectCount => activeTowers.Count + activeCreeps.Count + timedPresentations.Count;

        /// <summary>
        /// Towers currently drawn, on its own rather than folded into the aggregate above.
        /// </summary>
        /// <remarks>
        /// Split out for <see cref="Editor.OpeningCountdownFreshnessCheck"/>, which asks whether a
        /// tower built during the opening countdown reached the board. The aggregate cannot answer
        /// that: a timed presentation appearing in the same frame moves it by the same amount a tower
        /// would, so a check written against it passes whether or not the tower was drawn — measured,
        /// when that check was first run against a deliberately broken change signal and only its
        /// snapshot assertion fired.
        /// </remarks>
        public int ActiveTowerPresentationCount => activeTowers.Count;

        public int PooledPresentationObjectCount => towerPool.Count + PooledTowerPrefabCount() + creepPool.Count + PooledCreepPrefabCount() + effectPool.Count + beamPool.Count + textPool.Count;

        public void Initialize(UnitySimulationDriver driver)
        {
            simulationDriver = driver;
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

            spawnGateSprite = Resources.Load<Sprite>(SpawnGateSpriteResourcePath);
            leakGateSprite = Resources.Load<Sprite>(LeakGateSpriteResourcePath);
            boardDeepFieldTexture = Resources.Load<Texture2D>(BoardDeepFieldTextureResourcePath);
            boardBuildBandTexture = Resources.Load<Texture2D>(BoardBuildBandTextureResourcePath);
            boardRouteCoreTexture = Resources.Load<Texture2D>(BoardRouteCoreTextureResourcePath);

            audioDirector = gameObject.AddComponent<LTWAudioDirector>();
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
            UpdateFloatingLabels();
            UpdateExpandingRings();
            UpdateMortarShells();
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
            UpdateSpawnGatePulse();
            RenderSnapshot(snapshot);
            if (presentationDetail == PresentationDetail.Full)
            {
                RenderEvents(simulationDriver.LatestEvents);
            }
        }

        private readonly Dictionary<long, LTW.Simulation.Primitives.GridPosition> towerVisionTargetsScratch =
            new Dictionary<long, LTW.Simulation.Primitives.GridPosition>();

        /// <summary>Creeps per lane, reused rather than reallocated on every pass.</summary>
        private readonly int[] pressureByLane = new int[LaneCount + 1];

        /// <summary>
        /// The presentation revision the per-snapshot half of <see cref="RenderSnapshot"/> last ran
        /// for. <see cref="long.MinValue"/> means "nothing applied yet", which no real revision is.
        /// </summary>
        private long lastAppliedSnapshotRevision = long.MinValue;

        /// <summary>
        /// A value that changes exactly when the per-snapshot half of the render has something new
        /// to do.
        /// </summary>
        /// <remarks>
        /// The tick number alone is not it, and this is the whole reason the gate is a hash rather
        /// than a comparison against <c>snapshot.Tick</c>. Commands apply the moment they are
        /// accepted, not on the next tick — <c>LocalVerticalSlice.PlaceTower</c>/<c>UpgradeTower</c>/
        /// <c>SellTowerAt</c> all mutate state synchronously — and the opening build countdown is a
        /// thirty-second window in which the tick does not advance at all while the player builds and
        /// upgrades. Gated on the tick, a tower upgraded during the countdown would keep its old tier
        /// colour and tier marker until the match started.
        ///
        /// So this folds in everything the per-snapshot half reads: the tick (which is what refreshes
        /// aim targets), and per tower its identity, owner, cell and tier. Creeps are folded in too
        /// even though they only ever change on a tick, because it costs a few integer operations per
        /// creep and removes the need to be right about that.
        ///
        /// Deliberately a hash rather than per-entity dirty flags: it is allocation-free, it is one
        /// comparison at the call site, and a 64-bit collision would have to be engineered. Its
        /// failure mode is also the mild one — a stale frame, not a wrong one.
        /// </remarks>
        private static long SnapshotPresentationRevision(LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            unchecked
            {
                // FNV-1a over the fields, 64-bit.
                var hash = 14695981039346656037UL;
                hash = Mix(hash, (ulong)snapshot.Tick.Value);
                hash = Mix(hash, (ulong)snapshot.Towers.Count);
                for (var index = 0; index < snapshot.Towers.Count; index++)
                {
                    var tower = snapshot.Towers[index];
                    hash = Mix(hash, (ulong)tower.EntityId.Value);
                    hash = Mix(hash, (ulong)((tower.LaneId.Value << 24) ^ (tower.Position.X << 16) ^ (tower.Position.Y << 8) ^ tower.Tier));
                    hash = Mix(hash, (ulong)tower.OwnerId.Value);
                }

                hash = Mix(hash, (ulong)snapshot.Creeps.Count);
                for (var index = 0; index < snapshot.Creeps.Count; index++)
                {
                    var creep = snapshot.Creeps[index];
                    hash = Mix(hash, (ulong)creep.EntityId.Value);
                    hash = Mix(hash, (ulong)creep.Health);
                }

                return (long)hash;
            }
        }

        private static ulong Mix(ulong hash, ulong value)
        {
            unchecked
            {
                return (hash ^ value) * 1099511628211UL;
            }
        }

        /// <summary>
        /// Projects one snapshot into the pooled objects, splitting per-FRAME work from per-SNAPSHOT
        /// work.
        /// </summary>
        /// <remarks>
        /// This runs every frame, at around 60 fps, against a snapshot that changes at 4 Hz. Almost
        /// everything it used to do was therefore repeated fifteen times per input change: colours
        /// recomputed and written through <c>Renderer.material</c>, health bars rebuilt, range halos
        /// and role overlays reconfigured, the servicing-tether and Grovebond adjacency scans re-run
        /// (both of which are O(towers) per tower), and eight pressure gauges re-tinted and their
        /// TextMesh labels re-assigned, which regenerates a mesh each time.
        ///
        /// What stays per frame is everything that has to move smoothly BETWEEN ticks, and the test
        /// for that is mechanical rather than a judgement call: anything reading <see cref="Time"/>.
        /// Tower idle breathe/drift, aim turn, recoil, ring and barrel spin (<c>UpdateTowerMotion</c>,
        /// which reads both <c>Time.time</c> and <c>Time.deltaTime</c>); creep travel interpolation
        /// and the per-creep bob/sway/flinch (<c>SetCreepTransform</c> and <c>CreepRoleMotion</c>);
        /// the swarm cluster's per-shard wobble; the primitive-fallback role overlays, whose jitter
        /// and shimmer are also on <c>Time.time</c>; and the creep contact shadow, which follows a
        /// unit that is moving. A tower's contact shadow is NOT in that set — idle motion moves the
        /// Body child, never the root the decal is pinned to, so the decal is genuinely static.
        ///
        /// The hit flash is the one input that is neither: it is triggered by a snapshot (health
        /// went down) and expires on the clock (0.16s later, mid-tick). Colour therefore re-applies
        /// on a snapshot change OR when the flash state flips, tracked in
        /// <see cref="creepHitFlashApplied"/>.
        /// </remarks>
        private void RenderSnapshot(LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            var revision = SnapshotPresentationRevision(snapshot);
            var snapshotChanged = revision != lastAppliedSnapshotRevision;

            if (snapshotChanged)
            {
                visibleKeys.Clear();
                visibleContactShadowKeys.Clear();
                towerRolesByCell.Clear();
                towerTiersByCell.Clear();

                // A tower's vision (used purely for aim-tracking) is deliberately wider than its real
                // attack range (CombatService.GetTowerAimSnapshots) so the turret has time to turn
                // toward a target BEFORE it's actually close enough to fire — without this, the first
                // shot always fires from whatever the head's previous/idle heading was, since
                // towerAimTarget used to only ever get set at the moment of firing (TowerFiredEvent),
                // leaving zero time to visibly turn beforehand. Refreshed every snapshot, not gated by
                // firing or cooldown. The TARGET is per-snapshot; the turn toward it is per-frame, in
                // UpdateTowerMotion.
                towerVisionTargetsScratch.Clear();
                for (var index = 0; index < snapshot.TowerAimTargets.Count; index++)
                {
                    var visionTarget = snapshot.TowerAimTargets[index];
                    towerVisionTargetsScratch[visionTarget.TowerEntityId.Value] = visionTarget.TargetPosition;
                }
            }

            foreach (var tower in snapshot.Towers)
            {
                var key = tower.EntityId.Value;
                var visualProfile = towerVisualLibrary != null ? towerVisualLibrary.FindProfile(tower.TowerId.Value) : null;
                var hasPrefab = visualProfile != null && visualProfile.Prefab != null;
                GameObject towerObject;

                if (snapshotChanged)
                {
                    visibleKeys.Add(key);
                    var cellKey = TowerGridKey(tower.Position, tower.LaneId);
                    towerRolesByCell[cellKey] = tower.TowerId.Value;
                    towerTiersByCell[cellKey] = tower.Tier;
                    towerObject = GetOrCreateTower(key, visualProfile);
                    SetTowerTransform(towerObject, tower.Position, tower.LaneId, tower.TowerId.Value, visualProfile);
                    ApplyTowerColor(towerObject, tower.TowerId.Value, tower.OwnerId.Value, visualProfile, tower.Tier);

                    UpdateTowerMechanicMarker(key, tower.TowerId.Value, tower.Position, tower.LaneId, snapshot);
                    UpdateTowerServicingTether(key, tower, snapshot);
                    UpdateSporeFog(key, tower.TowerId.Value, GridToWorld(tower.Position, tower.LaneId), TowerRangeCells(tower.TowerId.Value));

                    if (towerVisionTargetsScratch.TryGetValue(key, out var visionTargetPosition))
                    {
                        towerAimTarget[key] = GridToWorld(visionTargetPosition, tower.LaneId);
                    }

                    if (!hasPrefab)
                    {
                        ConfigureTowerRoleMarker(towerObject, tower.TowerId.Value, tower.OwnerId.Value);
                    }

                    UpdateContactShadow(ContactShadowKey(key, isTower: true), towerObject, TowerPoolKey(visualProfile), TowerContactShadowMaterial(), 0.94f);
                    lastKnownPositions[key] = towerObject.transform.position;
                }
                else if (!activeTowers.TryGetValue(key, out towerObject) || towerObject == null)
                {
                    // Unreachable while the revision is doing its job: a tower present in the
                    // snapshot but absent from the pool means the tower set changed, which changes
                    // the revision. Kept as a guard rather than an assumption, since the alternative
                    // is a null dereference in the motion call below.
                    continue;
                }

                if (hasPrefab)
                {
                    UpdateTowerMotion(towerObject, key, towerObject.transform.position, visualProfile);
                }
            }

            if (snapshotChanged)
            {
                ReleaseMissingTowers();

                // After the tower loop and gated on snapshotChanged, because the braked cells only
                // move when the simulation does — a tower built or sold, or a lane re-mazed. Running
                // it per frame would rebuild the same decals sixty times a second, which is the cost
                // item 24 was opened to remove from this renderer.
                UpdateBrambleCells(snapshot);

                visibleKeys.Clear();
                Array.Clear(pressureByLane, 0, pressureByLane.Length);
            }

            foreach (var creep in snapshot.Creeps)
            {
                var key = creep.EntityId.Value;
                var visualProfile = creepVisualLibrary != null ? creepVisualLibrary.FindProfile(creep.CreepId.Value) : null;
                var hasPrefab = visualProfile != null && visualProfile.Prefab != null;
                GameObject creepObject;
                var isNewCreep = false;

                if (snapshotChanged)
                {
                    visibleKeys.Add(key);
                    isNewCreep = !activeCreeps.ContainsKey(key);
                    creepObject = GetOrCreateCreep(key, visualProfile);
                    if (!isNewCreep && lastCreepHealth.TryGetValue(key, out var previousHealth) && creep.Health < previousHealth)
                    {
                        creepHitFlashUntil[key] = Time.time + CreepHitFlashDuration;
                    }
                }
                else if (!activeCreeps.TryGetValue(key, out creepObject) || creepObject == null)
                {
                    // See the tower guard above: unreachable, kept so it cannot become a crash.
                    continue;
                }

                var healthFraction = CreepHealthFraction(creep.Health, creep.MaxHealth);
                var hitFlashUntil = creepHitFlashUntil.TryGetValue(key, out var flashUntilValue) ? flashUntilValue : 0f;
                var isHitFlashing = Time.time < hitFlashUntil;

                // Per frame: where the creep is, and the bob/sway/flinch it carries. Both interpolate
                // continuously, so both would visibly stutter at the snapshot rate.
                SetCreepTransform(creepObject, CreepTravelPosition(creep), creep.LaneId, creep.CreepId.Value, visualProfile, isNewCreep, hitFlashUntil, key,
                    CreepFacingYaw(creep, key, isNewCreep));

                var flashChanged = !creepHitFlashApplied.TryGetValue(key, out var appliedFlash) || appliedFlash != isHitFlashing;
                if (snapshotChanged || flashChanged)
                {
                    ApplyCreepColor(creepObject, creep.CreepId.Value, creep.SenderId.Value, visualProfile, healthFraction, isHitFlashing);
                    creepHitFlashApplied[key] = isHitFlashing;
                }

                // Hoisted: each of these is a transform path search, and the pair was being run up to
                // three times per creep per frame to answer the same question three times.
                var usesMeshVisual = UsesMeshVisual(creepObject);
                var usesPlateVisual = UsesAiPlateVisual(creepObject);

                if (usesMeshVisual && ContainsRole(creep.CreepId.Value, "swarm"))
                {
                    // Replaces the single mesh-backed body with a small cluster, not an overlay on
                    // top of it, so this runs unconditionally rather than being gated behind
                    // suppressCreepGameplayOverlays — skipping it would leave the creep showing
                    // nothing, since ConfigureSwarmCluster is what hides the original body. Per
                    // frame, not per snapshot: each shard wanders around its slot on Time.time, and
                    // that wander IS the swarm read.
                    ConfigureSwarmCluster(creepObject, creep.CreepId.Value, creep.SenderId.Value, healthFraction, isHitFlashing);
                }

                if (!suppressCreepGameplayOverlays)
                {
                    // Both of these stay per frame: their jitter, shimmer and windup all run off
                    // Time.time, so they animate. Nothing ships on this path — every creep in the
                    // roster has a mesh prefab — but that makes it cheap to leave alone rather than
                    // a reason to risk freezing it.
                    if (!usesPlateVisual && !usesMeshVisual)
                    {
                        ConfigureCreepReadabilityOverlay(creepObject, creep.CreepId.Value, creep.SenderId.Value, healthFraction, isHitFlashing);
                    }

                    if (!hasPrefab)
                    {
                        ConfigureCreepRoleMarker(creepObject, creep.CreepId.Value, creep.SenderId.Value, healthFraction, isHitFlashing);
                    }
                }

                if (snapshotChanged)
                {
                    UpdateCreepAnimationSpeed(key, creepObject, creep.CreepId.Value, creep.SpeedPerSecond);
                    if (suppressCreepGameplayOverlays)
                    {
                        DeactivateCreepGameplayOverlays(creepObject);
                    }
                    else
                    {
                        ConfigureCreepHealthBar(creepObject, creep.CreepId.Value, healthFraction);
                        if (usesPlateVisual || usesMeshVisual)
                        {
                            DeactivateRoleReadabilityOverlay(creepObject);
                        }
                    }

                    lastKnownCreepIds[key] = creep.CreepId.Value;
                    lastCreepHealth[key] = creep.Health;
                    if (creep.IsBraked && brakedCreepKeys.Add(key))
                    {
                        audioDirector.Play(LTWAudioCue.CreepSnared);
                    }
                    else if (!creep.IsBraked)
                    {
                        brakedCreepKeys.Remove(key);
                    }
                    if (creep.LaneId.Value >= 1 && creep.LaneId.Value < pressureByLane.Length)
                    {
                        pressureByLane[creep.LaneId.Value]++;
                    }
                }

                // Per frame, unlike the tower decal above: a creep's root transform is what walks
                // down the lane, so its blob has to walk with it.
                UpdateContactShadow(ContactShadowKey(key, isTower: false), creepObject, CreepPoolKey(visualProfile), CreepContactShadowMaterial(), 0.86f);
                lastKnownPositions[key] = creepObject.transform.position;
            }

            if (snapshotChanged)
            {
                ReleaseMissingCreeps();
                ReleaseMissingContactShadows();
                UpdateLanePressureIndicators(pressureByLane);
                lastAppliedSnapshotRevision = revision;
            }
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
                        SpawnEffect(buildPosition, MintSignal, 0.68f, 0.34f, BurstShape.Rise);
                        // No "WARD" label: it confirmed an action the player had just taken, at the
                        // cell they had just tapped, where the tower is now visibly standing.
                        SpawnReducedEffectCue(buildPosition, "BUILD", MintSignal);
                        audioDirector.Play(LTWAudioCue.TowerPlaced);
                        break;
                    case CategoryTierPurchasedEvent tierPurchased:
                        audioDirector.Play(LTWAudioCue.TierPurchased);
                        break;
                    case TowerUpgradedEvent towerUpgraded:
                        audioDirector.Play(LTWAudioCue.TowerUpgraded);
                        break;
                    case TowerSoldEvent towerSold:
                        var sellPosition = PositionFor(towerSold.TowerEntityId.Value);
                        SpawnCellFrameCue(sellPosition, SignalGold, 0.24f);
                        SpawnEffect(sellPosition, SignalGold, 0.42f, 0.24f);
                        SpawnFloatingText(sellPosition, $"+{towerSold.Refund.Amount}", SignalGold, 0.58f);
                        SpawnReducedEffectCue(sellPosition, "SELL", SignalGold);
                        audioDirector.Play(LTWAudioCue.TowerSold);
                        break;
                    case TowerEarnedGoldEvent earned:
                        // Shown AT the tower, not in the gold total. This mechanic has always worked
                        // and has never been visible: +1 arriving silently inside a total that also
                        // receives income reads as nothing happening, which is why the tower that
                        // has it was reported as broken. Deliberately quiet — it fires on every hit,
                        // so a short small number that does not stack up the screen.
                        SpawnFloatingText(
                            GridToWorld(earned.Position, earned.LaneId) + Vector3.up * 0.85f,
                            $"+{earned.Amount.Amount}",
                            SignalGold,
                            0.42f);
                        break;
                    case CreepQueuedEvent queued:
                        SpawnSendCue(queued);
                        break;
                    case CreepSpawnedEvent spawned:
                        var spawnPosition = SpawnPosition(spawned.DefenderId.Value);
                        var spawnColor = CreepRoleColor(spawned.CreepId.Value, spawned.SenderId.Value);
                        var isTransferArrival = leakTransferCandidates.Contains(TransferCandidateKey(spawned.Tick, spawned.SenderId)) &&
                            !queuedSpawnKeys.Contains(SpawnEventKey(spawned.Tick, spawned.SenderId, spawned.DefenderId, spawned.CreepId.Value));
                        // A lane transfer is deliberately SILENT. It used to announce itself with
                        // three beams across the gate and a floating "TRANSFER" label, and on a
                        // board where several lanes hand creeps along at once that reads as stray
                        // lasers firing at nothing — the cue describes bookkeeping (one entity
                        // retired, its successor created in the next lane) rather than anything the
                        // player did or can answer.
                        //
                        // The creep itself is unaffected: it walks in at the gate and is visible the
                        // whole way, which is the part that actually matters. Detection is kept
                        // rather than deleted precisely so the ordinary arrival cue below does NOT
                        // fire for a transfer — without the flag a hand-off would be announced as a
                        // fresh spawn, which is a different wrong answer.
                        if (!isTransferArrival)
                        {
                            SpawnCreepArrivalCue(spawned.DefenderId.Value, spawnColor);
                            // No name label. 20.6% of board text spent naming a model whose entire
                            // silhouette pass exists to make it identifiable without one — and if it
                            // is not identifiable, the label hides that rather than fixing it.
                            SpawnReducedEffectCue(spawnPosition, "SPAWN", spawnColor);

                            // Inside the branch, not after it. Left outside, this burst kept firing
                            // on every hand-off and the gate still flashed on transfers — silencing
                            // the beams while leaving this would have moved the problem rather than
                            // fixed it.
                            SpawnEffect(spawnPosition, spawnColor, 0.52f, 0.28f, BurstShape.Rise);
                        }

                        break;
                    case TowerFiredEvent fired:
                        // One shot voice per tower family, resolved through the same per-cell role
                        // map the weapon visuals use. ForContentId falls back to entry 0 (arcane)
                        // for an unknown id, so a roster addition degrades to the default zap
                        // rather than to silence.
                        audioDirector.Play(
                            TowerCatalog.ForContentId(TowerRoleAt(fired.TowerPosition, fired.LaneId)).Category switch
                            {
                                TowerCatalog.CategoryFoundry => LTWAudioCue.TowerShotFoundry,
                                TowerCatalog.CategoryGrove => LTWAudioCue.TowerShotGrove,
                                _ => LTWAudioCue.TowerShot
                            });
                        var firedTowerKey = fired.TowerEntityId.Value;
                        towerLastFiredAt[firedTowerKey] = Time.time;
                        towerAimTarget[firedTowerKey] = GridToWorld(fired.TargetPosition, fired.LaneId);
                        // An impact tick in the future means indirect fire. Direct-fire towers report
                        // "here, now", so this needs no per-tower special case.
                        if (fired.ImpactTick.Value > fired.Tick.Value)
                        {
                            SpawnMortarShell(
                                GridToWorld(fired.TowerPosition, fired.LaneId),
                                GridToWorld(fired.ImpactPosition, fired.LaneId),
                                (float)(fired.ImpactTick.Value - fired.Tick.Value) / SimulationTicksPerSecond());
                        }

                        break;
                    case CreepDamagedEvent damaged:
                        var hitPosition = PositionFor(damaged.CreepEntityId.Value);
                        var damagedCreepId = CreepIdFor(damaged.CreepEntityId.Value);
                        var towerPosition = GridToWorld(damaged.TowerPosition, damaged.LaneId);
                        var towerRole = TowerRoleAt(damaged.TowerPosition, damaged.LaneId);
                        var towerTier = TowerTierAt(damaged.TowerPosition, damaged.LaneId);
                        var attackBody = ResolveTowerBodyTransform(damaged.TowerEntityId.Value);
                        SpawnTowerAttackCue(towerPosition, hitPosition, towerRole, damaged.DamageDealt, attackBody, towerTier);
                        SpawnCreepHitCue(hitPosition, new Color(1f, 0.88f, 0.44f), damaged.DamageDealt);
                        SpawnCreepRoleFeedbackCue(hitPosition, damagedCreepId, damaged.DamageDealt);
                        SpawnEffect(hitPosition, new Color(1f, 0.88f, 0.44f), 0.24f, 0.12f);
                        if (damaged.DamageDealt >= 5)
                        {
                            // No damage number. It was 34.7% of all board text, and it floated over a
                            // creep that already carries a health bar saying the same thing
                            // continuously and exactly. The reduced-effects cue stays: at that
                            // setting the bar is the ONLY remaining tell, so this is the fallback.
                            SpawnReducedEffectCue(hitPosition, "HIT", new Color(1f, 0.88f, 0.44f));
                        }

                        audioDirector.Play(LTWAudioCue.CreepHit);
                        break;
                    case CreepKilledEvent creepKilled:
                        var killedCreepKey = creepKilled.CreepEntityId.Value;
                        var killPosition = PositionFor(killedCreepKey);
                        var killedCreepId = CreepIdFor(killedCreepKey);
                        var deathProfile = creepVisualLibrary != null ? creepVisualLibrary.FindProfile(killedCreepId) : null;
                        SpawnCreepDeathCue(killPosition, SignalGold, killedCreepId, deathProfile);
                        SpawnEffect(killPosition, SignalGold, 0.42f, 0.2f);
                        SpawnFloatingText(killPosition, $"+{creepKilled.BountyAwarded.Amount}", SignalGold, 0.56f);
                        SpawnReducedEffectCue(killPosition, "KILL", SignalGold);
                        audioDirector.Play(LTWAudioCue.CreepKilled);
                        break;
                    case LeakEvent leak:
                        var leakCreepKey = leak.CreepEntityId.Value;
                        var position = PositionFor(leakCreepKey);
                        var leakingCreepId = CreepIdFor(leakCreepKey);
                        SpawnLeakGateCue(leak.DefenderId.Value);
                        SpawnCreepLeakRoleCue(position, leakingCreepId);
                        // 0.86 made this the second-largest burst in the game, behind only the 1.15
                        // of a player being eliminated — which happens once per seat per match,
                        // where a leak happens constantly. Brought to 0.6: still the heaviest thing
                        // that routinely occurs, and no longer competing with the end of someone's
                        // match. It is now also the only burst at this position, since the gate cue
                        // no longer raises a second one on the same spot.
                        SpawnEffect(position, LeakRed, 0.6f, 0.42f, BurstShape.Sweep);
                        SpawnFloatingText(position, $"-{leak.LivesLost.Amount} LIFE", LeakRed, 0.72f);
                        SpawnReducedEffectCue(position, "LEAK", LeakRed);
                        if (leak.BountyAwarded.Amount > 0)
                        {
                            SpawnFloatingText(position + Vector3.right * 0.55f, $"+{leak.BountyAwarded.Amount}", SignalGold, 0.52f);
                        }

                        // The other half of the transaction, on the sender's own lane. A life is
                        // stolen rather than destroyed, and without showing the gain the mechanic is
                        // invisible to the player who earned it — they would see their own life
                        // counter move with no cue explaining why.
                        //
                        // Shown at the sender's lane rather than at the leak, because the two events
                        // happen in different places and the point is that a leak over there is a
                        // gain over here.
                        //
                        // Only when the local seat is the one gaining. The justification above is
                        // specifically that the player who earned the life would otherwise see their
                        // counter move with no explanation — which is an argument about the person
                        // watching, and there is nobody to inform when two opponents trade a leak.
                        // Ungated it also meant one leak lit up two lanes at once, the defender's and
                        // a sender's somewhere else on the board, for an exchange the player was not
                        // part of.
                        if (leak.SenderId.Value != leak.DefenderId.Value && IsLocalSeat(leak.SenderId))
                        {
                            var stealPosition = IncomePosition(leak.SenderId.Value);
                            SpawnEffect(stealPosition, MintSignal, 0.5f, 0.3f, BurstShape.Rise);
                            SpawnFloatingText(stealPosition, $"+{leak.LivesLost.Amount} LIFE", MintSignal, 0.66f);
                            SpawnReducedEffectCue(stealPosition, "STOLE", MintSignal);
                        }

                        audioDirector.Play(LTWAudioCue.CreepLeaked);
                        TriggerHapticFeedback();
                        break;
                    case IncomeTickEvent incomeTick:
                        // One of these fires for every seat still alive, all on the same tick. At
                        // eight lanes, a 50-tick interval and 4 ticks a second, that is eight lane
                        // beams, eight bursts and eight labels going off together every 12.5
                        // seconds — a synchronised pulse across the whole board, seven eighths of it
                        // describing other people's economies. Nothing there is actionable: an
                        // opponent's income is worth knowing as a number, which is what the scoreboard
                        // is for, and is not worth a beam across their lane on a timer.
                        //
                        // The local seat's own income keeps every part of its cue. That one answers a
                        // real question — why the gold total just jumped.
                        if (IsLocalSeat(incomeTick.PlayerId))
                        {
                            SpawnIncomeLaneCue(incomeTick.PlayerId.Value);
                            SpawnEffect(IncomePosition(incomeTick.PlayerId.Value), SignalGold, 0.46f, 0.22f, BurstShape.Rise);
                            SpawnFloatingText(IncomePosition(incomeTick.PlayerId.Value), $"+{incomeTick.GoldAwarded.Amount} income", SignalGold, 0.58f);
                            SpawnReducedEffectCue(IncomePosition(incomeTick.PlayerId.Value), "INCOME", SignalGold);
                            audioDirector.Play(LTWAudioCue.IncomeTick);
                        }

                        break;
                    case PlayerEliminatedEvent eliminated:
                        SpawnLaneShutdownCue(eliminated.PlayerId.Value);
                        SpawnEffect(LaneCenter(eliminated.PlayerId.Value) + Vector3.up * 0.2f, LeakRed, 1.15f, 0.55f, BurstShape.Sweep);
                        SpawnFloatingText(LaneCenter(eliminated.PlayerId.Value) + Vector3.up * 1.2f, $"PLAYER {eliminated.PlayerId.Value} OUT", LeakRed, 0.8f);
                        SpawnReducedEffectCue(LaneCenter(eliminated.PlayerId.Value), "OUT", LeakRed);
                        audioDirector.Play(LTWAudioCue.PlayerEliminated);
                        break;
                    case MatchEndedEvent ended:
                        SpawnVictoryLaneCue(ended.WinnerId.Value);
                        SpawnFloatingText(LaneCenter(ended.WinnerId.Value) + Vector3.up * 1.85f, $"PLAYER {ended.WinnerId.Value} WINS", SignalGold, 1f);
                        SpawnReducedEffectCue(LaneCenter(ended.WinnerId.Value), "WIN", SignalGold);
                        audioDirector.Play(
                            simulationDriver != null && ended.WinnerId.Equals(simulationDriver.LocalPlayerId)
                                ? LTWAudioCue.MatchWon
                                : LTWAudioCue.MatchLost);
                        break;
                }
            }
        }

        /// <summary>
        /// Simulation ticks per second, read from the driver so a tick count from an event converts
        /// to real seconds. Falls back to the driver's own default if the driver is missing.
        /// </summary>
        private float SimulationTicksPerSecond() => simulationDriver != null ? simulationDriver.TicksPerSecond : 4f;

        private string TowerRoleAt(GridPosition position, LaneId laneId)
        {
            return towerRolesByCell.TryGetValue(TowerGridKey(position, laneId), out var towerId) ? towerId : string.Empty;
        }

        /// <summary>Tier of the tower in a cell, defaulting to tier 1 for an unknown cell.</summary>
        private int TowerTierAt(GridPosition position, LaneId laneId) =>
            towerTiersByCell.TryGetValue(TowerGridKey(position, laneId), out var tier) ? tier : 1;

        /// <summary>
        /// Packs a lane cell into one int, so the per-cell maps are not keyed by a formatted string.
        /// </summary>
        /// <remarks>
        /// Both maps are rebuilt for every tower on the board on every snapshot and read once per
        /// damage event, and the string form cost two allocations per tower per rebuild for a value
        /// nothing ever reads as text. A lane is 1-8, X is 0-6 and Y is 0-15, so a byte each is an
        /// order of magnitude more room than the board has and the packing cannot collide.
        /// </remarks>
        private static int TowerGridKey(GridPosition position, LaneId laneId) =>
            (laneId.Value << 16) | ((position.X & 0xFF) << 8) | (position.Y & 0xFF);

        private static string TransferCandidateKey(SimulationTick tick, PlayerId senderId) => $"{tick.Value}:{senderId.Value}";

        private static string SpawnEventKey(SimulationTick tick, PlayerId senderId, PlayerId defenderId, string creepId) => $"{tick.Value}:{senderId.Value}:{defenderId.Value}:{creepId}";

        private static Vector3 SpawnPosition(int laneId) => GridToWorld(new GridPosition(CenterColumn, 0), new LaneId(laneId));

        private static Vector3 AllLaneCenter() => new Vector3((LaneOffset(1) + LaneOffset(LaneCount)) * 0.5f + BoardCenterX, 0f, BoardCenterZ);

        private static Vector3 IncomePosition(int playerId) => new Vector3(LaneOffset(playerId) + 1.2f, 1.25f, WorldZ(1));

        private void ReleaseExpiredPresentations()
        {
            for (var index = timedPresentations.Count - 1; index >= 0; index--)
            {
                var presentation = timedPresentations[index];
                if (Time.time < presentation.ReleaseAt)
                {
                    continue;
                }

                if (ReferenceEquals(presentation.Pool, beamPool))
                {
                    liveBeams--;
                }

                ReleaseToPool(presentation.Object, presentation.Pool);
                timedPresentations.RemoveAt(index);
            }
        }

        /// <summary>How fast a creep swings round to a new heading, in degrees per second.</summary>
        /// <remarks>
        /// 540 is a turn the eye reads as deliberate — about a third of a second for the 180 a
        /// switchback demands — without letting a creep drift visibly sideways while it comes round.
        /// </remarks>
        private const float CreepTurnDegreesPerSecond = 540f;

        /// <summary>
        /// The yaw a creep should be drawn at: its heading along the route, smoothed.
        /// </summary>
        /// <remarks>
        /// Heading comes from the cell it is walking TOWARD, not from frame-to-frame movement.
        /// Differencing positions would work while a creep moves and produce a zero-length vector the
        /// moment it stops or the snapshot repeats, which is most frames at four ticks a second.
        ///
        /// A creep that has arrived — its next cell is its current one, at the end of a route — keeps
        /// the facing it had rather than snapping to some default.
        /// </remarks>
        private float CreepFacingYaw(CreepPresentationSnapshot creep, long key, bool isNew)
        {
            var heading = GridToWorld(creep.NextPosition, creep.LaneId) - GridToWorld(creep.Position, creep.LaneId);
            var known = creepFacingYaw.TryGetValue(key, out var current);
            if (heading.sqrMagnitude < 0.0001f)
            {
                return known ? current : 0f;
            }

            var target = Quaternion.LookRotation(heading, Vector3.up).eulerAngles.y;

            // A creep that has just spawned takes its heading immediately. Easing in from zero would
            // spin every arrival through whatever angle separates the gate from due north.
            var yaw = isNew || !known
                ? target
                : Mathf.MoveTowardsAngle(current, target, CreepTurnDegreesPerSecond * Time.deltaTime);

            creepFacingYaw[key] = yaw;
            return yaw;
        }

        private void ReleaseAllActiveObjects()
        {
            // Everything the per-snapshot half applied is about to be handed back to the pools, so
            // the revision it was applied for no longer describes anything. Without this, presentation
            // being switched off and on again inside one unchanged snapshot would leave an empty board.
            lastAppliedSnapshotRevision = long.MinValue;
            towerMotionParts.Clear();
            foreach (var pair in activeTowers) ReleaseTowerToPool(pair.Key, pair.Value);
            foreach (var pair in activeCreeps) ReleaseCreepToPool(pair.Key, pair.Value);
            ReleaseAllContactShadows();
            activeTowers.Clear();
            activeCreeps.Clear();
            activeTowerPoolKeys.Clear();
            activeCreepPoolKeys.Clear();
            lastKnownCreepIds.Clear();
            lastCreepHealth.Clear();
            creepHitFlashUntil.Clear();
            creepHitFlashApplied.Clear();
            foreach (var presentation in timedPresentations) ReleaseToPool(presentation.Object, presentation.Pool);
            liveBeams = 0;
            timedPresentations.Clear();
            foreach (var ring in activeShockwaveRings) ReleaseToPool(ring.Object, shockwaveRingPool);
            activeShockwaveRings.Clear();

            // Particles are not pooled objects, so they are not covered by any of the releases
            // above and would otherwise drift on across a reset — visible as sparks hanging over an
            // empty board while the next match sets up.
            ClearBurstEmitters();
        }

        /// <summary>
        /// Owner/sender colour softened for the pool under a unit. These renderers run the
        /// soft-falloff shader, and colour is written straight onto the material — including its
        /// alpha — so an opaque team colour would drive the pool back to full strength and undo
        /// the falloff. Held well under 1 so the pool reads as a tint on the board, not a light.
        /// </summary>
        private static Color AccentPoolColor(Color color) => new Color(color.r, color.g, color.b, 0.34f);

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

        private Vector3 PositionFor(long entityId) => lastKnownPositions.TryGetValue(entityId, out var position) ? position : GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(1));

        private string CreepIdFor(long entityId) => lastKnownCreepIds.TryGetValue(entityId, out var creepId) ? creepId : string.Empty;

        private static Color BoostValue(Color color, float amount) =>
            new Color(Mathf.Clamp01(color.r * amount), Mathf.Clamp01(color.g * amount), Mathf.Clamp01(color.b * amount), color.a);

        private static Color DimValue(Color color, float amount) =>
            new Color(Mathf.Clamp01(color.r * amount), Mathf.Clamp01(color.g * amount), Mathf.Clamp01(color.b * amount), color.a);

        private static GameObject EnsureChild(GameObject parent, string name, PrimitiveType primitiveType)
        {
            var child = parent.transform.Find(name)?.gameObject;
            if (child != null)
            {
                return child;
            }

            // The creep health bars are built here, and they are what shipped magenta: a player's
            // GameObject.CreatePrimitive returns a renderer with no material. See
            // RenderCompat.CreatePrimitive.
            child = RenderCompat.CreatePrimitive(primitiveType);
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

        /// <summary>
        /// Find-or-create a child carrying a copy of an existing mesh/material, for cases like
        /// <see cref="ConfigureSwarmCluster"/> that need several small instances of a creep's own
        /// body mesh rather than a Unity primitive shape (see <see cref="EnsureChild"/>).
        /// </summary>
        private static GameObject EnsureMeshChild(GameObject parent, string name, Mesh mesh, Material material)
        {
            var existing = parent.transform.Find(name)?.gameObject;
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            return child;
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

        /// <summary>
        /// Colours a board object from the shared-material cache instead of cloning a private
        /// material for it. Only safe where the set of colours an object can take is small and
        /// known; anything with a continuously varying tint must keep using <see cref="SetColor"/>.
        /// </summary>
        private static void SetSharedColor(GameObject instance, Color color)
        {
            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = BoardRenderResources.SharedOpaque(color);
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

