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

        /// <summary>
        /// Sorting order for world-space floating text. Kept above every board decoration
        /// SpriteRenderer so send banners and damage numbers are never covered by board furniture.
        /// </summary>
        private const int FloatingTextSortingOrder = 100;
        private const int LaneLength = 16;
        private const int LaneCount = 8;
        private const int LaneSpacing = 9;
        private const int CenterColumn = 3;
        private const float BoardCenterX = (LaneWidth - 1) * 0.5f;
        private const float BoardCenterZ = (LaneLength - 1) * 0.5f;
        private const string PrimitiveTowerPoolKey = "primitive-tower";
        private const string PrimitiveCreepPoolKey = "primitive-creep";
        private const string DefaultTowerVisualLibraryResourcePath = "TowerVisualLibrary";
        private const string DefaultCreepVisualLibraryResourcePath = "CreepVisualLibrary";
        private const string SpawnGateSpriteResourcePath = "Art/Board/Endpoints/board_spawn_gate_v03";
        private const string LeakGateSpriteResourcePath = "Art/Board/Endpoints/board_leak_gate_v03";
        private const string BoardDeepFieldTextureResourcePath = "Art/Board/Materials/board_deep_field_option_11";
        private const string BoardBuildBandTextureResourcePath = "Art/Board/Materials/board_build_band_option_11";
        private const string BoardRouteCoreTextureResourcePath = "Art/Board/Materials/board_route_core_option_11";

        /// <summary>
        /// World Y of the walkable board surface: the top face of a lane cell, which sits at
        /// <c>GridToWorld().y - 0.53 + 0.06</c>. Contact shadows are pinned to this plane so they
        /// stay glued to the floor regardless of how far a unit's own pivot floats above it.
        /// </summary>
        private const float BoardTopY = -0.12f;

        /// <summary>
        /// How far the contact shadow decal is lifted off <see cref="BoardTopY"/>. Large enough to
        /// clear depth-fighting with the baked lane mesh, small enough to read as painted on.
        /// </summary>
        private const float ContactShadowLift = 0.006f;

        /// <summary>
        /// Vertex shade applied to the bottom edge of every baked board box, blending back to the
        /// authored colour at the top. The primitive cubes used to gain their seam definition from
        /// self-shadowing across the 0.04 gap between tiles; baking the same falloff into the mesh
        /// keeps the grid legible and adds contact occlusion under every raised strip.
        /// </summary>
        private const float BoardSeamShade = 0.74f;

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
        private readonly Dictionary<string, float> towerLastFiredAt = new Dictionary<string, float>();
        private readonly Dictionary<string, Vector3> towerAimTarget = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, float> towerAimYaw = new Dictionary<string, float>();
        private readonly Dictionary<int, GameObject> lanePressureMeters = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> lanePressureCaps = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, TextMesh> lanePressureLabels = new Dictionary<int, TextMesh>();
        private static MaterialPropertyBlock lanePressureMeterPropertyBlock;
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BackgroundColorPropertyId = Shader.PropertyToID("_BackgroundColor");
        private static readonly int FillPropertyId = Shader.PropertyToID("_Fill");
        private readonly List<GameObject> laneCells = new List<GameObject>();
        private readonly List<GameObject> laneDecorations = new List<GameObject>();
        private readonly List<BoardPiece> pendingBoardPieces = new List<BoardPiece>();

        /// <summary>Total board pieces folded into the baked lane meshes, for the geometry budget log.</summary>
        private int bakedBoardPieceCount;
        private readonly BoardMeshBuilder boardMeshBuilder = new BoardMeshBuilder();
        private readonly Dictionary<string, Material> referencePlateMaterials = new Dictionary<string, Material>();
        private readonly Dictionary<string, GameObject> activeContactShadows = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Vector2> unitFootprints = new Dictionary<string, Vector2>();
        private readonly Queue<GameObject> contactShadowPool = new Queue<GameObject>();

        /// <summary>Measured local-space body top per creep role, used to place health bars.</summary>
        private static readonly Dictionary<string, float> CreepBodyTopByRole = new Dictionary<string, float>();

        /// <summary>Local-space gap between a creep's body top and its health bar.</summary>
        private const float HealthBarGap = 0.12f;
        private readonly HashSet<string> visibleContactShadowKeys = new HashSet<string>();
        private readonly HashSet<string> visibleKeys = new HashSet<string>();
        private readonly Queue<GameObject> towerPool = new Queue<GameObject>();
        private readonly Queue<GameObject> creepPool = new Queue<GameObject>();
        private readonly Dictionary<string, Queue<GameObject>> towerPrefabPools = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, Queue<GameObject>> creepPrefabPools = new Dictionary<string, Queue<GameObject>>();
        private readonly Queue<GameObject> effectPool = new Queue<GameObject>();
        private readonly Queue<GameObject> textPool = new Queue<GameObject>();
        private readonly List<TimedPresentation> timedPresentations = new List<TimedPresentation>();
        private readonly List<SpawnGatePulseElement> spawnGatePulseElements = new List<SpawnGatePulseElement>();

        /// <summary>
        /// Spawn gate artwork, brightness-pulsed to show the gate is live. This replaces the
        /// geometric pulse bars, which achieved the same thing by laying flat cubes straight across
        /// the middle of the portal sprite and hiding the detail it was drawn with.
        /// </summary>
        private readonly List<SpriteRenderer> spawnGateSpriteRenderers = new List<SpriteRenderer>();

        private Camera presentationCamera = null!;
        private Sprite spawnGateSprite;
        private Sprite leakGateSprite;
        private Texture2D boardDeepFieldTexture;
        private Texture2D boardBuildBandTexture;
        private Texture2D boardRouteCoreTexture;
        private GameObject boardGeometryRoot;
        private Material towerContactShadowMaterial;
        private Material creepContactShadowMaterial;
        private bool laneCreated;

        public PresentationDetail Detail => presentationDetail;

        public LaneCameraFraming CameraFraming => cameraFraming;

        public int ActiveLaneCameraId => Mathf.Clamp(activeLaneCameraId, 1, LaneCount);

        public int ActivePresentationObjectCount => activeTowers.Count + activeCreeps.Count + timedPresentations.Count;

        public int PooledPresentationObjectCount => towerPool.Count + PooledTowerPrefabCount() + creepPool.Count + PooledCreepPrefabCount() + effectPool.Count + textPool.Count;

        private bool EndpointSpritesAvailable => spawnGateSprite != null && leakGateSprite != null;

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

            spawnGateSprite = Resources.Load<Sprite>(SpawnGateSpriteResourcePath);
            leakGateSprite = Resources.Load<Sprite>(LeakGateSpriteResourcePath);
            boardDeepFieldTexture = Resources.Load<Texture2D>(BoardDeepFieldTextureResourcePath);
            boardBuildBandTexture = Resources.Load<Texture2D>(BoardBuildBandTextureResourcePath);
            boardRouteCoreTexture = Resources.Load<Texture2D>(BoardRouteCoreTextureResourcePath);

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
            SetActiveLaneCameraId(ActiveLaneCameraId % LaneCount + 1);
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
            var nextLane = Mathf.Clamp(laneId, 1, LaneCount);
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

            var clampedLane = Mathf.Clamp(activeLaneCameraId, 1, LaneCount);
            var boardCenter = cameraFraming switch
            {
                LaneCameraFraming.AllLanes => AllLaneCenter(),
                LaneCameraFraming.BoardOverview => LaneCenter(clampedLane),
                LaneCameraFraming.SpawnGateFocus => GridToWorld(new GridPosition(CenterColumn, 0), new LaneId(clampedLane)) + new Vector3(0f, 0f, -1.15f),
                LaneCameraFraming.LeakGateFocus => GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(clampedLane)) + new Vector3(0f, 0f, 1.15f),
                _ => LaneCenter(clampedLane)
            };
            var isActiveLaneFraming = cameraFraming == LaneCameraFraming.ActiveLane;
            var defaultTilt = isActiveLaneFraming ? DefaultActiveLaneTiltDegrees : DefaultOverviewTiltDegrees;
            var tiltDegrees = CameraTiltDegrees > 0f ? CameraTiltDegrees : defaultTilt;
            var tiltRadians = tiltDegrees * Mathf.Deg2Rad;

            // The board lies flat, so its on-screen length shrinks by cos(tilt). Without
            // compensating the orthographic size, tilting the camera just adds dead space above and
            // below the board instead of showing more of the units. Scaling the size by the same
            // factor keeps the board filling the frame exactly as it did at the default tilt, and
            // has the side effect of making units larger on screen at steeper angles.
            var calibrationTilt = isActiveLaneFraming
                ? ActiveLaneZoomCalibrationTiltDegrees
                : OverviewZoomCalibrationTiltDegrees;
            var tiltZoom = Mathf.Cos(tiltRadians) / Mathf.Cos(calibrationTilt * Mathf.Deg2Rad);

            camera.orthographic = true;
            camera.orthographicSize = tiltZoom * cameraFraming switch
            {
                LaneCameraFraming.AllLanes => 34f,
                LaneCameraFraming.BoardOverview => 8.2f,
                LaneCameraFraming.SpawnGateFocus => 3.05f,
                LaneCameraFraming.LeakGateFocus => 3.05f,
                _ => 9.2f
            };
            camera.rect = cameraFraming == LaneCameraFraming.ActiveLane
                ? MobileViewportLayout.CameraRect()
                : new Rect(0f, 0f, 1f, 1f);
            // Expressed as a tilt off vertical rather than a height/offset pair, because the tilt
            // is what actually governs how much of a unit's vertical form and vertical motion
            // survives projection: only sin(tilt) of it reaches the screen. At the original 19.5
            // degrees that is 33%, which is why animation reads so weakly from this view. The
            // camera is orthographic, so the orbit radius affects only the angle, never the zoom.
            var radius = isActiveLaneFraming ? 18.57f : 18.44f;
            camera.transform.position = boardCenter + new Vector3(
                0f,
                radius * Mathf.Cos(tiltRadians),
                -radius * Mathf.Sin(tiltRadians));
            camera.transform.LookAt(boardCenter);
        }

        /// <summary>
        /// Tilt off vertical for the gameplay camera.
        /// </summary>
        /// <remarks>
        /// Was 19.5 (active lane) / 18.3 (overview), which is close enough to straight down that
        /// only sin(19.5) = 33% of any vertical motion survived projection — the reason procedural
        /// animation read as almost nothing from this view no matter how far its magnitudes were
        /// pushed. At 30 degrees that rises to 50%, and units read as sculpted objects rather than
        /// flat discs, while grid cells stay square enough to tap accurately on a phone. Steeper
        /// angles were captured and compared: 40 gave more dimensionality but visibly squashed the
        /// build slots and increased creep-on-creep occlusion down the lane.
        /// The zoom compensation below keeps the board framed identically at any of these values.
        /// </remarks>
        private const float DefaultActiveLaneTiltDegrees = 30f;
        private const float DefaultOverviewTiltDegrees = 30f;

        /// <summary>
        /// The tilt the orthographicSize values above were originally hand-tuned against. Zoom
        /// compensation is measured from here, not from the current default — otherwise raising the
        /// default would cancel its own compensation and put the dead space straight back.
        /// </summary>
        private const float ActiveLaneZoomCalibrationTiltDegrees = 19.5f;
        private const float OverviewZoomCalibrationTiltDegrees = 18.3f;

        /// <summary>
        /// Command-line override (-ltwCameraTilt &lt;degrees&gt;) used to A/B the camera angle from
        /// headless captures without editing the default.
        /// </summary>
        private static readonly float CameraTiltDegrees = ResolveCameraTiltOverride();

        private static float ResolveCameraTiltOverride()
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == "-ltwCameraTilt" && float.TryParse(args[index + 1], out var degrees))
                {
                    return degrees;
                }
            }

            return -1f;
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
            UpdateSpawnGatePulse();
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

            for (var lane = 1; lane <= LaneCount; lane++)
            {
                CreateLaneBackplate(lane);
                CreateLaneEnvironmentTrim(lane);
                CreateLaneFlowTickMarks(lane);
                CreateLaneSurfaceBands(lane);
                CreateLaneFloor(lane);
                CreateLaneReferenceMaterialOverlays(lane);
                CreateLaneTileDetailPass(lane);
                if (!EndpointSpritesAvailable)
                {
                    CreateLaneEndpointBox(lane, CenterColumn, 0, "SpawnBox", MintSignal);
                    CreateLaneEndpointBox(lane, CenterColumn, LaneLength - 1, "LifeLossBox", LeakRed);
                }

                CreateEndpointPlateDetails(lane, 0, MintSignal, lane == 1, true);
                CreateEndpointPlateDetails(lane, LaneLength - 1, LeakRed, lane == 1, false);
                CreateLaneFrame(lane);
                CreateLaneFlowCues(lane);
                if (!EndpointSpritesAvailable)
                {
                    CreateLaneLandmark(lane, CenterColumn, 0, "Spawn", MintSignal, 0.28f);
                    CreateLaneLandmark(lane, CenterColumn, LaneLength - 1, "LifeLoss", LeakRed, 0.34f);
                    CreateLaneGate(lane, 0, MintSignal, "ENTRY");
                    CreateLaneGate(lane, LaneLength - 1, LeakRed, "LEAK");
                }

                CreateLaneLabel(lane);
                CreateLaneOwnershipBadge(lane);
                BakeLaneBoardMesh(lane);
            }

            laneCreated = true;
            LogBoardGeometryBudget();
        }

        /// <summary>
        /// Emits the lane's cell grid straight into the lane mesh.
        /// </summary>
        /// <remarks>
        /// Geometry matches the primitive cubes this replaces exactly - same centre, same
        /// 0.96 x 0.12 x 0.96 extent, so the 0.04 gap that draws the grid is untouched and
        /// <see cref="GridToWorld"/> still describes where a cell is. The only change is that the
        /// per-cell colour now lives in the vertex stream rather than in 112 material instances.
        /// </remarks>
        private void CreateLaneFloor(int laneId)
        {
            for (var x = 0; x < LaneWidth; x++)
            {
                for (var y = 0; y < LaneLength; y++)
                {
                    var center = GridToWorld(new GridPosition(x, y), new LaneId(laneId)) + Vector3.down * 0.53f;
                    boardMeshBuilder.AddBox(center, new Vector3(0.96f, 0.12f, 0.96f), CellColor(laneId, x, y), BoardSeamShade);
                }
            }
        }

        /// <summary>
        /// Folds every board piece queued for this lane into one mesh and spawns the single
        /// renderer that draws it.
        /// </summary>
        /// <remarks>
        /// The decoration helpers hand back transform-only proxies so callers can keep rotating and
        /// nudging them exactly as before; the proxies are consumed here and destroyed. Baking per
        /// lane rather than per board keeps frustum culling useful - the active-lane framing only
        /// ever sees one lane - and keeps every mesh comfortably inside a 16-bit index buffer.
        /// </remarks>
        private void BakeLaneBoardMesh(int laneId)
        {
            for (var index = 0; index < pendingBoardPieces.Count; index++)
            {
                var piece = pendingBoardPieces[index];
                if (piece.Object == null)
                {
                    continue;
                }

                var pieceTransform = piece.Object.transform.localToWorldMatrix;
                if (piece.PrimitiveType == PrimitiveType.Cube)
                {
                    boardMeshBuilder.AddBox(pieceTransform, piece.Color, BoardSeamShade);
                }
                else
                {
                    boardMeshBuilder.AddMesh(BoardMeshBuilder.PrimitiveMesh(piece.PrimitiveType), pieceTransform, piece.Color);
                }

                Destroy(piece.Object);
            }

            bakedBoardPieceCount += pendingBoardPieces.Count;
            pendingBoardPieces.Clear();
            if (boardMeshBuilder.IsEmpty)
            {
                return;
            }

            var mesh = boardMeshBuilder.CreateMesh($"LTW Lane{laneId} Board");
            boardMeshBuilder.Clear();

            var surface = new GameObject($"Lane{laneId}BoardSurface");
            surface.transform.SetParent(BoardGeometryRoot().transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = surface.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = BoardRenderResources.BoardSurfaceMaterial;
            meshRenderer.receiveShadows = true;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            laneCells.Add(surface);
        }

        private GameObject BoardGeometryRoot()
        {
            if (boardGeometryRoot == null)
            {
                boardGeometryRoot = new GameObject("LTW Board Geometry");
            }

            return boardGeometryRoot;
        }

        /// <summary>
        /// Records a board decoration for baking and returns a transform-only stand-in.
        /// </summary>
        /// <remarks>
        /// Callers routinely rotate or reposition the object they get back, so the piece cannot be
        /// baked at creation time. The stand-in carries nothing but a <see cref="Transform"/> - no
        /// renderer, no material, and notably no collider, which the old primitives all carried
        /// despite nothing in the client ever raycasting the board.
        /// </remarks>
        private GameObject CreateBoardPiece(string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color)
        {
            var piece = new GameObject(name);
            piece.transform.position = position;
            piece.transform.localScale = scale;
            pendingBoardPieces.Add(new BoardPiece(piece, primitiveType, color));
            return piece;
        }

        /// <summary>
        /// Creates a board decoration that has to survive as a real renderer because something
        /// animates it later. These share a material per colour so GPU instancing can still fold
        /// them together.
        /// </summary>
        private GameObject CreateLiveBoardPiece(string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color)
        {
            var instance = CreatePrimitive(name, primitiveType);
            DestroyPrimitiveCollider(instance);
            instance.transform.position = position;
            instance.transform.localScale = scale;
            var meshRenderer = instance.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = BoardRenderResources.SharedOpaque(color);
            }

            laneDecorations.Add(instance);
            return instance;
        }

        private void DestroyPrimitiveCollider(GameObject instance)
        {
            var collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private void LogBoardGeometryBudget()
        {
            var boardRenderers = 0;
            for (var index = 0; index < laneDecorations.Count; index++)
            {
                if (laneDecorations[index] != null && laneDecorations[index].GetComponent<Renderer>() != null)
                {
                    boardRenderers++;
                }
            }

            Debug.Log(
                $"LTW board geometry -> {laneCells.Count} baked lane meshes, {boardRenderers} remaining board renderers, " +
                $"{bakedBoardPieceCount} pieces baked (each was previously its own renderer and material instance)");
        }

        private void RenderSnapshot(LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            visibleKeys.Clear();
            visibleContactShadowKeys.Clear();
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
                else
                {
                    UpdateTowerMotion(towerObject, key, towerObject.transform.position, visualProfile);
                }

                // Towers and creeps are keyed from separate id spaces, so the decal key is prefixed
                // to stop a tower and a creep that happen to share an entity id fighting over one.
                UpdateContactShadow("t" + key, towerObject, TowerPoolKey(visualProfile), TowerContactShadowMaterial(), 0.94f);
                lastKnownPositions[key] = towerObject.transform.position;
            }

            ReleaseMissingTowers();
            visibleKeys.Clear();
            var pressureByLane = new int[LaneCount + 1];
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

                var hitFlashUntil = creepHitFlashUntil.TryGetValue(key, out var flashUntilValue) ? flashUntilValue : 0f;
                SetCreepTransform(creepObject, creep.Position, creep.LaneId, creep.CreepId.Value, visualProfile, isNewCreep, hitFlashUntil);
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
                    if (UsesAiPlateVisual(creepObject) || UsesMeshVisual(creepObject))
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

                UpdateContactShadow("c" + key, creepObject, CreepPoolKey(visualProfile), CreepContactShadowMaterial(), 0.86f);
                lastKnownPositions[key] = creepObject.transform.position;
                lastKnownCreepIds[key] = creep.CreepId.Value;
                lastCreepHealth[key] = creep.Health;
                if (creep.LaneId.Value >= 1 && creep.LaneId.Value < pressureByLane.Length)
                {
                    pressureByLane[creep.LaneId.Value]++;
                }
            }

            ReleaseMissingCreeps();
            ReleaseMissingContactShadows();
            UpdateLanePressureIndicators(pressureByLane);
        }

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
        private void UpdateContactShadow(string key, GameObject unit, string poolKey, Material material, float footprintScale)
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

            var keysToRelease = new List<string>();
            foreach (var pair in activeContactShadows)
            {
                if (!visibleContactShadowKeys.Contains(pair.Key))
                {
                    keysToRelease.Add(pair.Key);
                }
            }

            for (var index = 0; index < keysToRelease.Count; index++)
            {
                ReleaseContactShadow(keysToRelease[index]);
            }
        }

        private void ReleaseContactShadow(string key)
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
                    case TowerFiredEvent fired:
                        var firedTowerKey = fired.TowerEntityId.Value.ToString();
                        towerLastFiredAt[firedTowerKey] = Time.time;
                        towerAimTarget[firedTowerKey] = GridToWorld(fired.TargetPosition, fired.LaneId);
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

        /// <summary>
        /// Orientation that presents flat world-space text square-on to the gameplay camera.
        /// Falls back to the old board-flat orientation only if no camera is resolvable.
        /// </summary>
        private Quaternion FloatingTextRotation()
        {
            var camera = presentationCamera != null ? presentationCamera : Camera.main;
            return camera != null ? camera.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
        }

        private void SpawnFloatingText(Vector3 position, string text, Color color, float duration)
        {
            var textObject = GetTextObject();
            textObject.transform.position = position + Vector3.up * 0.55f;
            // Face the camera rather than lying flat on the board. The old fixed Euler(90,0,0) was
            // only legible because the camera was nearly straight down; at any real tilt the text
            // slants away and loses readability. Billboarding keeps it face-on at any camera angle.
            textObject.transform.rotation = FloatingTextRotation();
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

            // Board furniture such as the endpoint gate plates draws through SpriteRenderers with
            // sorting orders up to 3. A TextMesh renderer defaults to 0, so send banners spawning
            // over a gate were being covered by it. Sort floating text above all board decoration.
            var textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = FloatingTextSortingOrder;
            }

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
            // The large "{qty}x {NAME}" spawn banner over the defender's gate was removed: it
            // dominated the top of the board and duplicated information the send dock already
            // shows. The sender-side SEND cue and the gate effect still mark the event.
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

        private static Vector3 AllLaneCenter() => new Vector3((LaneOffset(1) + LaneOffset(LaneCount)) * 0.5f + BoardCenterX, 0f, BoardCenterZ);

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
            ReleaseAllContactShadows();
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
                towerLastFiredAt.Remove(key);
                towerAimTarget.Remove(key);
                towerAimYaw.Remove(key);
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

        private const float TowerRecoilDuration = 0.35f;
        private const float TowerAimTurnDegreesPerSecond = 260f;

        /// <summary>
        /// Idle motion, aim rotation and fire recoil all apply to the tower's Body child, not the
        /// root — RoleMarker/OwnerTrim/RangeHalo are siblings of Body (see
        /// Tower3DImportPipeline.GenerateWrapperIfRawExists) and are rotationally symmetric shapes
        /// that should never visibly kick or spin with an attack reaction.
        /// </summary>
        private void UpdateTowerMotion(GameObject towerObject, string key, Vector3 towerPosition, TowerVisualProfile visualProfile)
        {
            var body = ResolveTowerMotionTarget(towerObject, visualProfile);
            var idle = TowerRoleMotion(visualProfile.Role);

            var yaw = towerAimYaw.TryGetValue(key, out var currentYaw) ? currentYaw : 0f;
            if (towerAimTarget.TryGetValue(key, out var aimTarget))
            {
                var direction = aimTarget - towerPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    var desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, TowerAimTurnDegreesPerSecond * Time.deltaTime);
                }
            }

            towerAimYaw[key] = yaw;

            var timeSinceFired = towerLastFiredAt.TryGetValue(key, out var firedAt) ? Time.time - firedAt : float.MaxValue;
            var recoil = timeSinceFired < TowerRecoilDuration ? 1f - timeSinceFired / TowerRecoilDuration : 0f;

            // Scale-based feedback (breathe idle + squash/stretch recoil) carries this tower's
            // motion, not position/pitch — see TowerRoleMotion's remark on why those mostly don't
            // reach the screen under this camera. A squash/stretch punch reads from any angle.
            var idleScale = 1f + idle.ScalePulse;
            var recoilStretch = recoil * 0.22f;
            var recoilSquash = recoil * 0.35f;

            body.localPosition = idle.PositionOffset + Vector3.down * (recoil * 0.1f);
            body.localRotation = Quaternion.Euler(idle.PitchDegrees + recoil * 18f, yaw, 0f);
            body.localScale = new Vector3(idleScale + recoilStretch, idleScale - recoilSquash, idleScale + recoilStretch);
        }

        private static Transform ResolveTowerMotionTarget(GameObject towerObject, TowerVisualProfile visualProfile)
        {
            if (string.IsNullOrWhiteSpace(visualProfile.BodyRendererPath))
            {
                return towerObject.transform;
            }

            return towerObject.transform.Find(visualProfile.BodyRendererPath) ?? towerObject.transform;
        }

        /// <summary>
        /// Whether a creep role's prefab carries a skeletal rig, cached per role. Rigged creeps let
        /// their animation clip own idle motion instead of the procedural bob, which would
        /// otherwise double up with the clip's own body movement.
        /// </summary>
        private static readonly Dictionary<string, bool> RiggedByRole = new Dictionary<string, bool>();

        private static bool IsRiggedCreep(GameObject instance, string creepId)
        {
            if (RiggedByRole.TryGetValue(creepId, out var cached))
            {
                return cached;
            }

            var rigged = instance.GetComponentInChildren<Animator>(true) != null;
            RiggedByRole[creepId] = rigged;
            return rigged;
        }

        private static void SetCreepTransform(GameObject instance, GridPosition position, LaneId laneId, string creepId, CreepVisualProfile visualProfile, bool snapToTarget, float hitFlashUntil)
        {
            var roleMotion = CreepRoleMotion(creepId, visualProfile, hitFlashUntil, IsRiggedCreep(instance, creepId));
            var targetPosition = GridToWorld(position, laneId) + CreepRoleOffset(creepId) + roleMotion.PositionOffset;
            instance.transform.position = snapToTarget || Vector3.Distance(instance.transform.position, targetPosition) > 2.5f
                ? targetPosition
                : Vector3.Lerp(instance.transform.position, targetPosition, Mathf.Clamp01(Time.deltaTime * 8f));
            instance.transform.localScale = CreepRoleScale(creepId, visualProfile) * roleMotion.ScaleMultiplier;
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

        /// <summary>
        /// Local-space top of a creep's body, measured once per role and cached.
        /// </summary>
        /// <remarks>
        /// The health bar heights in <see cref="CreepHealthBarMetrics"/> were hand-tuned against
        /// the flat 2D plate profiles, whose Y scale was around 0.54. The 3D wrappers scale
        /// uniformly, which pushed the same constants anywhere from 0.91x to 2.03x of a creep's
        /// height: the swarm bar sat inside its model while the brute's floated well clear.
        /// Measuring the body instead keeps every bar the same short distance above its creep and
        /// survives the next scale change, which retuning the constants would not.
        /// </remarks>
        private static float CreepBodyTop(GameObject creepObject, string creepId)
        {
            if (CreepBodyTopByRole.TryGetValue(creepId, out var cached))
            {
                return cached;
            }

            var top = 0f;
            var renderers = creepObject.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var rendererObject = renderers[index].gameObject;
                if (IsHealthBarPart(rendererObject.name))
                {
                    continue;
                }

                var localTop = creepObject.transform.InverseTransformPoint(renderers[index].bounds.max).y;
                if (localTop > top)
                {
                    top = localTop;
                }
            }

            if (top <= 0.01f)
            {
                // Renderers are not ready yet; leave the cache empty so a later frame measures it.
                return 0f;
            }

            CreepBodyTopByRole[creepId] = top;
            return top;
        }

        private static bool IsHealthBarPart(string name) =>
            name == "HealthBarBack" || name == "HealthBarFill" || name == "HealthBarMidTick" || name == "HealthWoundPip";

        /// <summary>
        /// Two-piece health bar: a dark backing and a coloured fill, shown only once a creep has
        /// actually taken damage.
        /// </summary>
        /// <remarks>
        /// This used to stack four separate cubes per creep — backing, fill, a dark mid-tick
        /// splitting the fill in half, and a red "wound pip" hanging off the end — and drew all of
        /// them on every creep at all times, including at full health. On screen that read as a
        /// cluster of unrelated coloured lines floating above each unit rather than as one bar, and
        /// at the spawn gate it appeared as a stray green/red streak before its creep was even
        /// visible. The mid-tick and wound pip carried no information the fill width did not
        /// already convey, so both are gone; hiding the bar at full health removes it entirely for
        /// most units most of the time.
        /// </remarks>
        private static void ConfigureCreepHealthBar(GameObject creepObject, string creepId, float healthFraction)
        {
            var metrics = CreepHealthBarMetrics.For(creepId);

            // Measure before the bar parts exist, so they cannot inflate the body's top.
            var bodyTop = CreepBodyTop(creepObject, creepId);
            var barY = bodyTop > 0f ? bodyTop + HealthBarGap : metrics.Y;

            var back = EnsureChild(creepObject, "HealthBarBack", PrimitiveType.Cube);
            var fill = EnsureChild(creepObject, "HealthBarFill", PrimitiveType.Cube);

            var damaged = healthFraction < 0.999f;
            back.SetActive(damaged);
            fill.SetActive(damaged);
            if (!damaged)
            {
                DeactivateChild(creepObject, "HealthBarMidTick");
                DeactivateChild(creepObject, "HealthWoundPip");
                return;
            }

            ConfigureHealthBarChild(back, new Vector3(0f, barY, metrics.Z), new Vector3(metrics.Width, metrics.Height, metrics.Depth), new Color(0.015f, 0.022f, 0.035f));
            var fillWidth = Mathf.Max(metrics.MinFillWidth, metrics.Width * Mathf.Clamp01(healthFraction));
            var fillX = (fillWidth - metrics.Width) * 0.5f;
            ConfigureHealthBarChild(fill, new Vector3(fillX, barY + metrics.FillLift, metrics.Z), new Vector3(fillWidth, metrics.Height * 1.12f, metrics.Depth * 1.08f), CreepHealthColor(healthFraction));

            // Retired parts: pooled creeps can carry them over from a previous life, so they are
            // explicitly switched off rather than merely no longer created.
            DeactivateChild(creepObject, "HealthBarMidTick");
            DeactivateChild(creepObject, "HealthWoundPip");
        }

        private static void DeactivateChild(GameObject root, string childName)
        {
            var child = root.transform.Find(childName);
            if (child != null && child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
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

        /// <summary>
        /// True when a creep is one of the generated 3D wrappers built by Creep3DImportPipeline.
        /// </summary>
        /// <remarks>
        /// The role readability overlay exists to tell flat 2D plates apart: it sticks coloured
        /// primitives onto the creep at offsets tuned for the plate profiles. A generated mesh
        /// carries its own silhouette and material identity, and those offsets land wrong at the
        /// wrapper's uniform scale - the swarm value ring sits at -0.33, below the board - so the
        /// overlay is suppressed for meshes. Ownership still reads from the SenderAccent decal.
        /// </remarks>
        private static bool UsesMeshVisual(GameObject instance) =>
            instance != null && instance.transform.Find("Body/Imported3DVisual") != null;

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
            // Same problem as the gutter ticks: a tilted slab on the frame corner reads as a
            // stray shard rather than trim, so it is full-detail only.
            if (BoardDetail != BoardDetailLevel.Full)
            {
                return;
            }

            var chip = CreateBoardRail($"Lane{laneId}{name}RailAccent", position, new Vector3(0.2f, 0.018f, 0.055f), LaneFrameAccentColor(accent, laneId == 1));
            chip.transform.rotation = Quaternion.Euler(0f, name.Contains("West", StringComparison.OrdinalIgnoreCase) ? 22f : -22f, 0f);
        }

        private void CreateLaneBackplate(int laneId)
        {
            CreateBoardPiece(
                $"Lane{laneId}Backplate",
                PrimitiveType.Cube,
                new Vector3(LaneOffset(laneId) + BoardCenterX, -0.28f, BoardCenterZ),
                new Vector3(LaneWidth + 1.35f, 0.08f, LaneLength + 1.35f),
                LaneBackplateColor(laneId));
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
            CreateBoardPiece(
                $"Lane{laneId}{name}Pylon",
                PrimitiveType.Cube,
                position,
                new Vector3(0.22f * focusScale, 0.38f * focusScale, 0.22f * focusScale),
                color);
        }

        private void CreateLaneFlowTickMarks(int laneId)
        {
            // Angled slabs down both gutters, originally every third row. They restate the flow
            // direction the in-lane arrows already give, and because they are tilted cubes sitting
            // proud of the gutter they read as loose blue shards stuck to the board edge rather
            // than as trim. Only kept at full detail.
            if (BoardDetail != BoardDetailLevel.Full)
            {
                return;
            }

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
            var tick = CreateBoardPiece(
                $"Lane{laneId}{name}",
                PrimitiveType.Cube,
                position,
                new Vector3(isPlayerLane ? 0.1f : 0.075f, 0.055f, isPlayerLane ? 0.42f : 0.32f),
                color);
            tick.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
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

        /// <summary>
        /// How much scatter decoration the board surface carries.
        /// </summary>
        /// <remarks>
        /// The original pass laid down roughly 130 extra pieces per lane — a beveled inset plate
        /// every other row on both build columns plus one in the walking route, wear smudges,
        /// edge chips, ribs, seams and cracks. Under the near-overhead camera this read as
        /// scattered floating boxes rather than surface texture. Override with
        /// -ltwBoardDetail minimal|reduced|full.
        /// </remarks>
        private enum BoardDetailLevel
        {
            Minimal,
            Reduced,
            Full
        }

        private static readonly BoardDetailLevel BoardDetail = ResolveBoardDetail();

        private static BoardDetailLevel ResolveBoardDetail()
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] != "-ltwBoardDetail")
                {
                    continue;
                }

                switch (args[index + 1].ToLowerInvariant())
                {
                    case "minimal": return BoardDetailLevel.Minimal;
                    case "reduced": return BoardDetailLevel.Reduced;
                    case "full": return BoardDetailLevel.Full;
                }
            }

            return BoardDetailLevel.Reduced;
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

            // Scatter detail inside the walking route is the worst offender: creeps travel over it,
            // so it competes with the units for attention on exactly the pixels that matter most.
            if (BoardDetail == BoardDetailLevel.Full)
            {
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
            }

            if (BoardDetail != BoardDetailLevel.Minimal)
            {
                for (var y = 2; y < LaneLength - 2; y += 4)
                {
                    var z = WorldZ(y) - 0.5f;
                    CreateSurfaceBand($"Lane{laneId}LeftBuildSeam_{y}", new Vector3(offset + 1f, -0.088f, z), new Vector3(1.5f, 0.014f, 0.035f), seamColor);
                    CreateSurfaceBand($"Lane{laneId}RightBuildSeam_{y}", new Vector3(offset + 5f, -0.088f, z), new Vector3(1.5f, 0.014f, 0.035f), seamColor);
                }
            }

            // The build-column plates are the only inset that carries meaning — they mark where a
            // tower can go — so they survive every level. The one in the walking route does not.
            for (var y = 1; y < LaneLength - 1; y += 2)
            {
                var z = WorldZ(y);
                CreateBoardPlateInset(laneId, $"LeftInset_{y}", new Vector3(offset + 1f, -0.078f, z), laneId == 1);
                CreateBoardPlateInset(laneId, $"RightInset_{y}", new Vector3(offset + 5f, -0.078f, z), laneId == 1);
                if (y % 4 == 1 && BoardDetail == BoardDetailLevel.Full)
                {
                    CreateBoardPlateInset(laneId, $"RouteInset_{y}", new Vector3(offset + CenterColumn, -0.074f, z), laneId == 1, 0.72f, 0.52f);
                }
            }

            if (BoardDetail == BoardDetailLevel.Full)
            {
                for (var y = 3; y < LaneLength - 2; y += 5)
                {
                    var westCrack = CreateSurfaceBand($"Lane{laneId}WestPlateCrack_{y}", new Vector3(offset + 1.45f, -0.082f, WorldZ(y) + 0.18f), new Vector3(0.035f, 0.014f, 0.44f), crackColor);
                    westCrack.transform.rotation = Quaternion.Euler(0f, -22f, 0f);
                    var eastCrack = CreateSurfaceBand($"Lane{laneId}EastPlateCrack_{y}", new Vector3(offset + 4.55f, -0.082f, WorldZ(y) - 0.08f), new Vector3(0.032f, 0.014f, 0.36f), crackColor);
                    eastCrack.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
                }
            }
        }

        private void CreateLaneReferenceMaterialOverlays(int laneId)
        {
            var offset = LaneOffset(laneId);
            const float plateY = -0.178f;
            if (boardDeepFieldTexture != null)
            {
                CreateReferenceBoardPlate($"Lane{laneId}LeftReferenceField", boardDeepFieldTexture, new Vector3(offset + 1.02f, plateY, BoardCenterZ), new Vector2(1.84f, LaneLength - 2.2f), 0.62f);
                CreateReferenceBoardPlate($"Lane{laneId}RightReferenceField", boardDeepFieldTexture, new Vector3(offset + 4.98f, plateY, BoardCenterZ), new Vector2(1.84f, LaneLength - 2.2f), 0.62f);
            }

            if (boardBuildBandTexture != null)
            {
                CreateReferenceBoardPlate($"Lane{laneId}LeftReferenceBuildBand", boardBuildBandTexture, new Vector3(offset + 1f, plateY + 0.006f, BoardCenterZ), new Vector2(1.64f, LaneLength - 2.45f), 0.68f);
                CreateReferenceBoardPlate($"Lane{laneId}RightReferenceBuildBand", boardBuildBandTexture, new Vector3(offset + 5f, plateY + 0.006f, BoardCenterZ), new Vector2(1.64f, LaneLength - 2.45f), 0.68f);
            }

            if (boardRouteCoreTexture != null)
            {
                CreateReferenceBoardPlate($"Lane{laneId}ReferenceRouteCore", boardRouteCoreTexture, new Vector3(offset + CenterColumn, plateY + 0.012f, BoardCenterZ), new Vector2(1.22f, LaneLength - 1.8f), 0.72f);
            }
        }

        private void CreateReferenceBoardPlate(string name, Texture2D texture, Vector3 center, Vector2 size, float alpha)
        {
            var plate = CreatePrimitive(name, PrimitiveType.Quad);
            plate.transform.position = center;
            plate.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            plate.transform.localScale = new Vector3(size.x, size.y, 1f);

            DestroyPrimitiveCollider(plate);
            plate.GetComponent<Renderer>().sharedMaterial = ReferencePlateMaterial(texture, alpha);
            laneDecorations.Add(plate);
        }

        /// <summary>
        /// One material per texture/alpha pair rather than per plate. Eight lanes ask for the same
        /// five overlays, so this turns forty material instances into three shared ones that batch.
        /// </summary>
        private Material ReferencePlateMaterial(Texture2D texture, float alpha)
        {
            var key = $"{texture.name}:{Mathf.RoundToInt(alpha * 1000f)}";
            if (referencePlateMaterials.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Texture") ?? RenderCompat.Lit;
            var material = new Material(shader)
            {
                name = $"LTW Board Reference {texture.name}",
                enableInstancing = true,
                mainTexture = texture,
                color = new Color(1f, 1f, 1f, alpha)
            };

            referencePlateMaterials[key] = material;
            return material;
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
            var hasEndpointSprite = isSpawn ? spawnGateSprite != null : leakGateSprite != null;

            if (hasEndpointSprite)
            {
                CreateEndpointSpritePlate(laneId, label, center, isSpawn, isPlayerLane);
                if (isSpawn)
                {
                    CreateSpawnGateSpriteCompanionDetails(laneId, center, signal, isPlayerLane);
                }

                return;
            }

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

            CreateEndpointSpritePlate(laneId, label, center, isSpawn, isPlayerLane);
        }

        private void CreateEndpointSpritePlate(int laneId, string label, Vector3 center, bool isSpawn, bool isPlayerLane)
        {
            var sprite = isSpawn ? spawnGateSprite : leakGateSprite;
            if (sprite == null)
            {
                return;
            }

            var plate = new GameObject($"Lane{laneId}{label}ReferenceSpritePlate");
            plate.transform.position = center + new Vector3(0f, 0.18f, isSpawn ? 0.02f : -0.1f);
            plate.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var scale = isPlayerLane
                ? (isSpawn ? 0.54f : 0.5f)
                : (isSpawn ? 0.46f : 0.42f);
            plate.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = plate.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 3;
            renderer.color = Color.white;
            laneDecorations.Add(plate);

            if (isSpawn)
            {
                spawnGateSpriteRenderers.Add(renderer);
            }
        }

        private void CreateSpawnGateSpriteCompanionDetails(int laneId, Vector3 center, Color signal, bool isPlayerLane)
        {
            var offset = LaneOffset(laneId);
            var z = center.z;
            var pulseColor = EndpointPortalBrightColor(isPlayerLane);
            var rimColor = EndpointRimHighlightColor(true, isPlayerLane);
            var recessColor = EndpointDeepRecessColor(true, isPlayerLane);
            var routeColor = EndpointPortalColor(isPlayerLane);
            var scale = isPlayerLane ? 1f : 0.82f;

            CreateSurfaceBand($"Lane{laneId}SpawnSocketShadow", center + new Vector3(0f, 0.042f, -0.03f), new Vector3(2.36f * scale, 0.018f, 1.48f * scale), BoardContactShadowColor(laneId));
            CreateSurfaceBand($"Lane{laneId}SpawnInsetWest", new Vector3(offset + CenterColumn - 1.12f * scale, 0.122f, z - 0.04f), new Vector3(0.12f, 0.026f, 1.38f * scale), recessColor);
            CreateSurfaceBand($"Lane{laneId}SpawnInsetEast", new Vector3(offset + CenterColumn + 1.12f * scale, 0.122f, z - 0.04f), new Vector3(0.12f, 0.026f, 1.38f * scale), recessColor);
            CreateSurfaceBand($"Lane{laneId}SpawnSocketNorthLip", new Vector3(offset + CenterColumn, 0.13f, z + 0.78f * scale), new Vector3(2.08f * scale, 0.024f, 0.1f), rimColor);
            CreateSurfaceBand($"Lane{laneId}SpawnSocketSouthLip", new Vector3(offset + CenterColumn, 0.13f, z - 0.84f * scale), new Vector3(2.08f * scale, 0.024f, 0.1f), rimColor);

            // The two pulse bars sat flat across the middle of the gate sprite, covering the glowing
            // core the artwork already draws, and the intake chevrons repeated the chevron shapes
            // the same sprite carries at its base. Both are geometry competing with finished art,
            // so at anything below full detail the gate is left to speak for itself and the
            // "this gate is live" signal comes from pulsing the sprite's brightness instead.
            if (BoardDetail == BoardDetailLevel.Full)
            {
                CreateSpawnGatePulseBand(laneId, "OuterPulse", center + new Vector3(0f, 0.205f, -0.04f), new Vector3(1.52f * scale, 0.012f, 0.075f), pulseColor, 0f, 0.12f, 0.012f);
                CreateSpawnGatePulseBand(laneId, "InnerPulse", center + new Vector3(0f, 0.216f, -0.04f), new Vector3(0.86f * scale, 0.014f, 0.06f), pulseColor, 0.47f, 0.1f, 0.014f);

                var intakeA = CreateSpawnGatePulseBand(laneId, "IntakeChevronA", new Vector3(offset + CenterColumn - 0.22f * scale, 0.224f, z - 1.02f * scale), new Vector3(0.1f, 0.018f, 0.56f * scale), signal, 0.16f, 0.08f, 0.018f);
                intakeA.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                var intakeB = CreateSpawnGatePulseBand(laneId, "IntakeChevronB", new Vector3(offset + CenterColumn + 0.22f * scale, 0.224f, z - 1.02f * scale), new Vector3(0.1f, 0.018f, 0.56f * scale), signal, 0.16f, 0.08f, 0.018f);
                intakeB.transform.rotation = Quaternion.Euler(0f, -35f, 0f);
            }

            // The rune row is five small cubes strung across the mouth of the gate. It was intended
            // as arcane trim but reads as a dashed coloured line drawn over the lane, so it only
            // survives at full detail.
            if (BoardDetail != BoardDetailLevel.Full)
            {
                return;
            }

            for (var index = -2; index <= 2; index++)
            {
                var rune = CreateSpawnGatePulseBand(
                    laneId,
                    $"RouteRune{index}",
                    new Vector3(offset + CenterColumn + index * 0.26f * scale, 0.182f, z - 1.42f * scale),
                    new Vector3(0.12f, 0.012f, 0.045f),
                    index == 0 ? pulseColor : routeColor,
                    0.25f + index * 0.09f,
                    0.05f,
                    0.01f);
                rune.transform.rotation = Quaternion.Euler(0f, index * -8f, 0f);
            }
        }

        /// <summary>
        /// Pulse bands animate every frame, so unlike the rest of the board furniture they cannot
        /// be baked into the static lane mesh and stay as real renderers. They share a material per
        /// colour and all draw the same cube mesh, so GPU instancing collapses them anyway.
        /// </summary>
        private GameObject CreateSpawnGatePulseBand(int laneId, string name, Vector3 position, Vector3 scale, Color color, float phase, float scalePulse, float liftPulse)
        {
            var band = CreateLiveBoardPiece($"Lane{laneId}Spawn{name}", PrimitiveType.Cube, position, scale, color);
            spawnGatePulseElements.Add(new SpawnGatePulseElement(band, position, scale, phase, scalePulse, liftPulse));
            return band;
        }

        private void UpdateSpawnGatePulse()
        {
            // Brightness pulse on the gate artwork itself. Kept subtle: this reads as the portal
            // breathing, where anything stronger looks like a flicker fault.
            for (var index = 0; index < spawnGateSpriteRenderers.Count; index++)
            {
                var spriteRenderer = spawnGateSpriteRenderers[index];
                if (spriteRenderer == null)
                {
                    continue;
                }

                var glow = 0.9f + (Mathf.Sin(Time.time * 1.8f) + 1f) * 0.5f * 0.1f;
                spriteRenderer.color = new Color(glow, glow, glow, 1f);
            }

            if (spawnGatePulseElements.Count == 0)
            {
                return;
            }

            var time = Time.time * 1.8f;
            foreach (var element in spawnGatePulseElements)
            {
                if (element.Object == null)
                {
                    continue;
                }

                var wave = (Mathf.Sin(time + element.Phase * Mathf.PI * 2f) + 1f) * 0.5f;
                var scale = 1f + wave * element.ScalePulse;
                element.Object.transform.localScale = new Vector3(element.BaseScale.x * scale, element.BaseScale.y, element.BaseScale.z * scale);
                element.Object.transform.position = element.BasePosition + Vector3.up * (wave * element.LiftPulse);
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
            CreateBoardPiece(name, PrimitiveType.Cylinder, position, new Vector3(diameter, height, diameter), color);
        }

        private void CreateEndpointPylon(int laneId, string name, Vector3 position, Color color, bool isPlayerLane)
        {
            var radius = isPlayerLane ? 0.22f : 0.17f;
            CreateBoardPiece(
                $"Lane{laneId}{name}",
                PrimitiveType.Cylinder,
                position,
                new Vector3(radius, isPlayerLane ? 0.42f : 0.32f, radius),
                color);
        }

        private GameObject CreateSurfaceBand(string name, Vector3 position, Vector3 scale, Color color) =>
            CreateBoardPiece(name, PrimitiveType.Cube, position, scale, color);

        private void CreateLaneFlowCues(int laneId)
        {
            // Direction of travel only needs establishing, not repeating every three tiles down a
            // lane that already reads as one-way. Spacing them out also clears the route for units.
            var spacing = BoardDetail switch
            {
                BoardDetailLevel.Full => 3,
                BoardDetailLevel.Reduced => 5,
                _ => 7
            };

            for (var y = 2; y < LaneLength - 1; y += spacing)
            {
                CreateFlowArrow(laneId, y);
            }
        }

        private void CreateFlowArrow(int laneId, int y)
        {
            var offset = LaneOffset(laneId);
            var color = RouteTriangleColor(laneId);
            CreateBoardPiece(
                $"Lane{laneId}Flow_{y}_Shaft",
                PrimitiveType.Cube,
                new Vector3(offset + CenterColumn, -0.005f, WorldZ(y)),
                new Vector3(0.035f, 0.032f, 0.18f),
                color);

            var eastHead = CreateBoardPiece(
                $"Lane{laneId}Flow_{y}_HeadA",
                PrimitiveType.Cube,
                new Vector3(offset + CenterColumn + 0.12f, 0f, WorldZ(y) - 0.18f),
                new Vector3(0.05f, 0.035f, 0.24f),
                color);
            eastHead.transform.rotation = Quaternion.Euler(0f, 42f, 0f);

            var westHead = CreateBoardPiece(
                $"Lane{laneId}Flow_{y}_HeadB",
                PrimitiveType.Cube,
                new Vector3(offset + CenterColumn - 0.12f, 0f, WorldZ(y) - 0.18f),
                new Vector3(0.05f, 0.035f, 0.24f),
                color);
            westHead.transform.rotation = Quaternion.Euler(0f, -42f, 0f);

            CreateBoardPiece(
                $"Lane{laneId}Flow_{y}_Base",
                PrimitiveType.Cube,
                new Vector3(offset + CenterColumn, -0.002f, WorldZ(y) + 0.03f),
                new Vector3(0.28f, 0.028f, 0.035f),
                new Color(color.r * 0.72f, color.g * 0.72f, color.b * 0.72f));
        }

        private GameObject CreateBoardRail(string name, Vector3 position, Vector3 scale, Color color) =>
            CreateBoardPiece(name, PrimitiveType.Cube, position, scale, color);

        private void CreateLaneLandmark(int laneId, int x, int y, string landmarkName, Color color, float scale)
        {
            CreateBoardPiece(
                $"Lane{laneId}{landmarkName}Beacon",
                PrimitiveType.Cylinder,
                GridToWorld(new GridPosition(x, y), new LaneId(laneId)) + Vector3.down * 0.23f,
                new Vector3(scale, 0.22f, scale),
                color);

            CreateBoardPiece(
                $"Lane{laneId}{landmarkName}Halo",
                PrimitiveType.Cylinder,
                GridToWorld(new GridPosition(x, y), new LaneId(laneId)) + Vector3.down * 0.31f,
                new Vector3(scale * 1.95f, 0.045f, scale * 1.95f),
                EndpointWashColor(color, laneId == 1));
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
            var diameter = laneId == 1 ? 1.88f : 1.6f;
            CreateBoardPiece(
                $"Lane{laneId}{boxName}",
                PrimitiveType.Cylinder,
                GridToWorld(new GridPosition(x, y), new LaneId(laneId)) + Vector3.down * 0.46f,
                new Vector3(diameter, 0.13f, diameter),
                EndpointBaseColor(color, laneId == 1, isSpawn));
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

        /// <summary>
        /// Lifts a board surface colour that was authored against the gamma response.
        /// </summary>
        /// <remarks>
        /// The board palette was tuned when the project rendered in gamma, where an albedo near
        /// 0.05 still read as visible stonework. Linear rendering resolves those same values far
        /// darker and the board collapses into an undifferentiated dark field. Treating the
        /// authored number as the intended linear reflectance and converting it back to the sRGB
        /// albedo that produces it restores the original intent, rather than re-tuning a dozen
        /// scattered constants by eye.
        /// </remarks>
        /// <remarks>
        /// A full inverse-gamma conversion is the mathematically exact compensation, but it
        /// overshoots the art direction: it lifts a 0.052 cell to roughly 0.25 and the board reads
        /// as pale concrete rather than night stone. This blends part of the way, which restores
        /// legible tonal separation while keeping the dark palette. Raise to brighten the board,
        /// lower to darken it; 0 is the original gamma-era value and 1 the exact conversion.
        /// </remarks>
        private const float BoardSurfaceLift = 0.35f;

        private static Color BoardSurface(Color authored) => new Color(
            Mathf.Lerp(authored.r, Mathf.LinearToGammaSpace(authored.r), BoardSurfaceLift),
            Mathf.Lerp(authored.g, Mathf.LinearToGammaSpace(authored.g), BoardSurfaceLift),
            Mathf.Lerp(authored.b, Mathf.LinearToGammaSpace(authored.b), BoardSurfaceLift),
            authored.a);

        private static Color CellColor(int laneId, int x, int y)
        {
            if (x == CenterColumn && y == 0)
            {
                return BoardSurface(new Color(0.08f, 0.18f, 0.19f));
            }

            if (x == CenterColumn && y == LaneLength - 1)
            {
                return BoardSurface(new Color(0.16f, 0.055f, 0.052f));
            }

            if (x == CenterColumn)
            {
                var routeVariation = TileVariation(laneId, x, y) * 0.016f;
                return laneId == 1
                    ? BoardSurface(new Color(0.07f + routeVariation, 0.145f + routeVariation, 0.225f + routeVariation))
                    : BoardSurface(new Color(0.044f + routeVariation * 0.7f, 0.085f + routeVariation * 0.7f, 0.145f + routeVariation * 0.7f));
            }

            var checker = (x + y + laneId) % 2 == 0 ? 0.012f : 0f;
            var laneTint = laneId == 1 ? 0.014f : 0f;
            var buildColumn = x < CenterColumn ? 0.004f : 0.01f;
            var stoneVariation = TileVariation(laneId, x, y) * 0.014f;
            var edgeLift = x == 0 || x == LaneWidth - 1 ? 0.008f : 0f;
            return BoardSurface(new Color(0.052f + checker + laneTint + buildColumn + stoneVariation + edgeLift, 0.058f + checker + laneTint + stoneVariation * 0.82f + edgeLift, 0.072f + checker + laneTint + stoneVariation * 0.55f + edgeLift));
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

        /// <summary>
        /// Per-role idle motion for a tower's Body child. Only Control has a tuned idle motion so
        /// far — the other four roles share a small generic default until their own pass tunes
        /// them individually. Yaw is deliberately left untouched here (stays 0): aim rotation in
        /// <see cref="UpdateTowerMotion"/> owns yaw exclusively, so idle and aim never fight over
        /// the same axis.
        /// </summary>
        /// <summary>
        /// The match camera is orthographic but tilted only ~19 degrees off vertical
        /// (ConfigureDefaultCamera), so Y-axis position and X-axis pitch mostly project away —
        /// only ~sin(19 deg) of either ever reaches the screen. Idle motion leans on XZ position
        /// and uniform scale instead, since neither loses effect to that projection.
        /// </summary>
        private static TowerMotion TowerRoleMotion(TowerVisualRole role)
        {
            var time = Time.time;
            if (role == TowerVisualRole.Control)
            {
                var breathe = Mathf.Sin(time * 1.6f) * 0.05f;
                var driftX = Mathf.Sin(time * 0.9f) * 0.05f;
                var driftZ = Mathf.Cos(time * 0.7f) * 0.04f;
                var wobble = Mathf.Sin(time * 1.1f) * 6f;
                return new TowerMotion(new Vector3(driftX, 0f, driftZ), wobble, breathe);
            }

            var defaultBreathe = Mathf.Sin(time * 1.3f) * 0.02f;
            return new TowerMotion(Vector3.zero, 0f, defaultBreathe);
        }

        /// <summary>
        /// Per-role idle motion applied on top of the creep's lane position.
        /// </summary>
        /// <remarks>
        /// Position offsets here must stay centred on the lane. The swarm and brute styles used to
        /// carry a constant lateral shift of 0.16, which read acceptably when creeps were flat 2D
        /// plates at roughly half the current scale but sits them visibly off-centre now they are
        /// 3D meshes. Only oscillating components belong in the offset; a constant one is a
        /// misalignment.
        /// </remarks>
        private const float CreepHitFlashDuration = 0.16f;

        private static CreepMotion CreepRoleMotion(string creepId, CreepVisualProfile visualProfile, float hitFlashUntil, bool isRigged = false)
        {
            var time = Time.time;

            // A rigged creep's walk clip already animates its body, so the procedural idle bob and
            // sway are redundant and fight it. Keep only the hit reaction, which the clip does not
            // cover, expressed as a scale punch so it still reads under this camera angle.
            if (isRigged)
            {
                var riggedFlinch = Mathf.Clamp01((hitFlashUntil - time) / CreepHitFlashDuration);
                return new CreepMotion(Vector3.zero, Quaternion.identity, 1f + riggedFlinch * 0.28f);
            }

            var motionStyle = visualProfile != null ? visualProfile.MotionStyle : CreepVisualMotionStyle.Auto;
            if (motionStyle == CreepVisualMotionStyle.ClusterJitter || motionStyle == CreepVisualMotionStyle.Auto && ContainsRole(creepId, "swarm"))
            {
                var pulse = Mathf.Sin(time * 15f) * 0.045f;
                return new CreepMotion(new Vector3(pulse, 0f, -pulse * 0.65f), Quaternion.Euler(0f, time * 60f, 0f));
            }

            if (motionStyle == CreepVisualMotionStyle.HeavyBob || motionStyle == CreepVisualMotionStyle.Auto && (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss")))
            {
                var weight = Mathf.Abs(Mathf.Sin(time * 3.4f)) * 0.055f;
                var sway = Mathf.Sin(time * 3.4f) * 1.5f;
                // Hit-flinch: a quick opposite-direction tilt plus a squash/stretch punch, layered
                // on top of the continuous idle sway, decaying over the same 0.16s window
                // creepHitFlashUntil already tracks for the colour flash. The scale punch is what
                // actually carries this — the match camera is orthographic and tilted only ~19
                // degrees off vertical (see UpdateTowerMotion's remark), so tilt/position changes
                // mostly project away and read as almost nothing on screen.
                var flinch = Mathf.Clamp01((hitFlashUntil - time) / CreepHitFlashDuration);
                var flinchTilt = -Mathf.Sign(sway == 0f ? 1f : sway) * flinch * 26f;
                var flinchScale = 1f + flinch * 0.28f;
                return new CreepMotion(new Vector3(0f, -weight - flinch * 0.09f, 0f), Quaternion.Euler(flinch * 20f, 0f, sway + flinchTilt), flinchScale);
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

        /// <summary>
        /// The identity colour for a tower role. Everything else derives from it:
        /// <see cref="TowerBaseColor"/> dims it, and the 3D body materials carry the matching
        /// emission tint, so marker, base and glow all agree.
        /// </summary>
        /// <remarks>
        /// Hues are deliberately spread. The previous mapping returned the same pale blue for
        /// control and for prism, and placed relay within roughly fifteen degrees of pulse, so two
        /// pairs of roles were effectively indistinguishable by colour. Roles are matched through
        /// the explicit predicates rather than loose substring tests, which is what let prism fall
        /// through to control's branch.
        /// </remarks>
        private static Color TowerMarkerColor(string towerId) => TowerRolePalette.For(towerId);

        private static void ConfigureCreepRoleMarker(GameObject creepObject, string creepId, int senderId, float healthFraction, bool isHitFlashing)
        {
            DeactivateKnownCreepMarkers(creepObject);

            // The old per-creep GroundShadow cylinder is gone. Its -0.42 local offset was tuned
            // against the flat 2D plate profiles, where the shallow scale left it just above the
            // board; at the uniform scale the 3D wrappers use it sinks below the surface and is
            // never seen. Grounding now comes from the pooled contact shadow decals, which are
            // instanced and sit on the board rather than inside it.

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

        private static Color LaneBackplateColor(int laneId)
        {
            return laneId == 1 ? BoardSurface(new Color(0.025f, 0.045f, 0.072f)) : BoardSurface(new Color(0.018f, 0.026f, 0.046f));
        }

        private static Color LaneGutterColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.018f, 0.034f, 0.054f)) : BoardSurface(new Color(0.014f, 0.018f, 0.032f));

        private static Color LaneAnchorColor(Color accent, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.34f : 0.18f;
            return BoardSurface(new Color(accent.r * strength, accent.g * strength, accent.b * strength));
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
            return BoardSurface(new Color(0.044f + tint.r * strength, 0.052f + tint.g * strength, 0.068f + tint.b * strength));
        }

        private static Color RouteBandColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.074f, 0.16f, 0.25f)) : BoardSurface(new Color(0.044f, 0.09f, 0.16f));

        private static Color RouteGuideColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.16f, 0.33f, 0.52f)) : BoardSurface(new Color(0.085f, 0.18f, 0.32f));

        private static Color RouteInlayColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.085f, 0.22f, 0.36f)) : BoardSurface(new Color(0.044f, 0.12f, 0.22f));

        private static Color RouteRecessColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.018f, 0.032f, 0.046f)) : BoardSurface(new Color(0.009f, 0.018f, 0.03f));

        private static Color RouteRibColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.18f, 0.32f, 0.43f)) : BoardSurface(new Color(0.075f, 0.14f, 0.22f));

        private static Color BuildBandEdgeColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.12f, 0.14f, 0.15f)) : BoardSurface(new Color(0.064f, 0.074f, 0.086f));

        private static Color EndpointApproachPlateColor(bool isSpawn, bool isPlayerLane)
        {
            if (isSpawn)
            {
                return BoardSurface(isPlayerLane ? new Color(0.075f, 0.13f, 0.13f) : new Color(0.036f, 0.068f, 0.068f));
            }

            return BoardSurface(isPlayerLane ? new Color(0.12f, 0.045f, 0.04f) : new Color(0.062f, 0.022f, 0.02f));
        }

        private static Color EndpointWashColor(Color color, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.42f : 0.25f;
            return new Color(color.r * strength, color.g * strength, color.b * strength);
        }

        private static Color RouteWearColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.055f, 0.075f, 0.088f)) : BoardSurface(new Color(0.034f, 0.048f, 0.062f));

        private static Color BuildBandSeamColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.018f, 0.026f, 0.034f)) : BoardSurface(new Color(0.012f, 0.018f, 0.026f));

        private static Color TileCrackColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.012f, 0.016f, 0.022f)) : BoardSurface(new Color(0.008f, 0.012f, 0.018f));

        private static Color TileEdgeHighlightColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.13f, 0.15f, 0.16f)) : BoardSurface(new Color(0.074f, 0.086f, 0.1f));

        private static Color BoardPlateInsetColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.058f, 0.068f, 0.078f)) : BoardSurface(new Color(0.034f, 0.042f, 0.052f));

        private static Color BoardPlateLightBevelColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.16f, 0.17f, 0.17f)) : BoardSurface(new Color(0.086f, 0.096f, 0.106f));

        private static Color BoardPlateDarkBevelColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.012f, 0.018f, 0.026f)) : BoardSurface(new Color(0.006f, 0.01f, 0.016f));

        private static Color BoardContactShadowColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.008f, 0.014f, 0.02f)) : BoardSurface(new Color(0.004f, 0.008f, 0.014f));

        private static Color LaneFrameTrimColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.065f, 0.078f, 0.088f)) : BoardSurface(new Color(0.034f, 0.042f, 0.052f));

        private static Color LaneFrameHighlightColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.18f, 0.19f, 0.18f)) : BoardSurface(new Color(0.086f, 0.094f, 0.1f));

        private static Color LaneFrameAccentColor(Color accent, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.38f : 0.18f;
            return BoardSurface(new Color(0.035f + accent.r * strength, 0.04f + accent.g * strength, 0.045f + accent.b * strength));
        }

        private static Color EndpointPlateSignalColor(Color color, bool isPlayerLane)
        {
            var strength = isPlayerLane ? 0.72f : 0.45f;
            return new Color(color.r * strength, color.g * strength, color.b * strength);
        }

        private static Color RouteTriangleColor(int laneId) => laneId == 1 ? BoardSurface(new Color(0.33f, 0.58f, 0.78f)) : BoardSurface(new Color(0.14f, 0.27f, 0.43f));

        private static Color EndpointBaseColor(Color color, bool isPlayerLane, bool isSpawn)
        {
            var strength = isPlayerLane ? 0.18f : 0.11f;
            var baseTone = isSpawn ? new Color(0.048f, 0.072f, 0.084f) : new Color(0.055f, 0.038f, 0.038f);
            return BoardSurface(new Color(baseTone.r + color.r * strength, baseTone.g + color.g * strength, baseTone.b + color.b * strength));
        }

        private static Color EndpointStoneRingColor(bool isSpawn, bool isPlayerLane)
        {
            var laneLift = isPlayerLane ? 0.018f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.145f + laneLift, 0.162f + laneLift, 0.172f + laneLift)
                : new Color(0.172f + laneLift, 0.092f + laneLift * 0.4f, 0.086f + laneLift * 0.4f));
        }

        private static Color EndpointOuterRingColor(bool isSpawn, bool isPlayerLane)
        {
            var laneLift = isPlayerLane ? 0.022f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.088f + laneLift, 0.108f + laneLift, 0.122f + laneLift)
                : new Color(0.115f + laneLift, 0.056f + laneLift * 0.35f, 0.054f + laneLift * 0.3f));
        }

        private static Color EndpointInnerPlateColor(bool isSpawn, bool isPlayerLane)
        {
            var laneLift = isPlayerLane ? 0.02f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.072f + laneLift, 0.112f + laneLift, 0.124f + laneLift)
                : new Color(0.092f + laneLift, 0.038f + laneLift * 0.35f, 0.034f + laneLift * 0.35f));
        }

        private static Color EndpointDeepRecessColor(bool isSpawn, bool isPlayerLane)
        {
            var lift = isPlayerLane ? 0.012f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.018f + lift, 0.032f + lift, 0.036f + lift)
                : new Color(0.026f + lift, 0.006f + lift * 0.25f, 0.006f + lift * 0.2f));
        }

        private static Color EndpointStoneHighlightColor(bool isSpawn, bool isPlayerLane)
        {
            var lift = isPlayerLane ? 0.035f : 0.012f;
            return BoardSurface(isSpawn
                ? new Color(0.22f + lift, 0.235f + lift, 0.235f + lift)
                : new Color(0.24f + lift, 0.12f + lift * 0.4f, 0.108f + lift * 0.35f));
        }

        private static Color EndpointRimHighlightColor(bool isSpawn, bool isPlayerLane)
        {
            var lift = isPlayerLane ? 0.028f : 0f;
            return BoardSurface(isSpawn
                ? new Color(0.17f + lift, 0.19f + lift, 0.19f + lift)
                : new Color(0.18f + lift, 0.072f + lift * 0.35f, 0.066f + lift * 0.3f));
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

        private readonly struct CreepMotion
        {
            public CreepMotion(Vector3 positionOffset, Quaternion rotation, float scaleMultiplier = 1f)
            {
                PositionOffset = positionOffset;
                Rotation = rotation;
                ScaleMultiplier = scaleMultiplier;
            }

            public Vector3 PositionOffset { get; }
            public Quaternion Rotation { get; }
            public float ScaleMultiplier { get; }
        }

        private readonly struct TowerMotion
        {
            public TowerMotion(Vector3 positionOffset, float pitchDegrees, float scalePulse)
            {
                PositionOffset = positionOffset;
                PitchDegrees = pitchDegrees;
                ScalePulse = scalePulse;
            }

            public Vector3 PositionOffset { get; }
            public float PitchDegrees { get; }
            public float ScalePulse { get; }
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

        /// <summary>
        /// A board decoration queued for baking into the lane mesh: the transform-only stand-in
        /// handed back to the construction code, plus the shape and colour it stands for.
        /// </summary>
        private readonly struct BoardPiece
        {
            public BoardPiece(GameObject @object, PrimitiveType primitiveType, Color color)
            {
                Object = @object;
                PrimitiveType = primitiveType;
                Color = color;
            }

            public GameObject Object { get; }
            public PrimitiveType PrimitiveType { get; }
            public Color Color { get; }
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

        private readonly struct SpawnGatePulseElement
        {
            public SpawnGatePulseElement(GameObject @object, Vector3 basePosition, Vector3 baseScale, float phase, float scalePulse, float liftPulse)
            {
                Object = @object;
                BasePosition = basePosition;
                BaseScale = baseScale;
                Phase = phase;
                ScalePulse = scalePulse;
                LiftPulse = liftPulse;
            }

            public GameObject Object { get; }
            public Vector3 BasePosition { get; }
            public Vector3 BaseScale { get; }
            public float Phase { get; }
            public float ScalePulse { get; }
            public float LiftPulse { get; }
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
