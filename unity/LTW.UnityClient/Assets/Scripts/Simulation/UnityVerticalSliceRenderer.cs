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
    public sealed class UnityVerticalSliceRenderer : MonoBehaviour
    {
        private const int LaneWidth = 7;

        /// <summary>
        /// Sorting order for world-space floating text. Kept above every board decoration
        /// SpriteRenderer so send banners and damage numbers are never covered by board furniture.
        /// </summary>
        private const int FloatingTextSortingOrder = 100;

        /// <summary>World units a board label rises over its life.</summary>
        private const float FloatingTextRise = 0.5f;
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
        /// Height for board decals that span MORE than their own cell — currently Grovebond's bond
        /// ring and Thorn Snare's bramble zone.
        /// </summary>
        /// <remarks>
        /// Both were drawn at floor level (BoardTopY + ~0.012), which is correct for a decal that
        /// stays inside one cell, and wrong for these two. Grovebond's ring is 1.25-1.9 cells across
        /// so that it visibly reaches the neighbours it is bonded to, and Thorn's is sized to the
        /// braked span. Reaching onto a neighbouring cell means reaching under that cell's raised
        /// build plate, and the ring disappears beneath it — reported from play as "the sapling
        /// underglow is below some of the game board".
        ///
        /// Anchored just under <see cref="TowerBaseClearance"/> rather than to a measured plate
        /// height: towers stand ON the plates, so every plate is necessarily below the height a
        /// tower's own base sits at, and staying below that keeps these decals reading as painted on
        /// the board rather than floating across the towers they belong to.
        /// </remarks>
        private const float SpanningDecalLift = 0.07f;

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

        /// <summary>Board labels currently animating. See SpawnFloatingText and UpdateFloatingLabels.</summary>
        private readonly List<FloatingLabel> floatingLabels = new List<FloatingLabel>();

        private readonly Dictionary<string, GameObject> activeTowers = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> activeCreeps = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, string> activeTowerPoolKeys = new Dictionary<string, string>();
        private readonly Dictionary<string, string> activeCreepPoolKeys = new Dictionary<string, string>();
        private readonly Dictionary<string, Vector3> lastKnownPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, string> lastKnownCreepIds = new Dictionary<string, string>();
        private readonly Dictionary<string, string> towerRolesByCell = new Dictionary<string, string>();

        /// <summary>
        /// Per-cell tower tier, kept alongside <see cref="towerRolesByCell"/> and cleared with it.
        /// A CreepDamagedEvent identifies its tower by grid cell, so this is how the weapon effects
        /// find out how upgraded the tower that fired is.
        /// </summary>
        private readonly Dictionary<string, int> towerTiersByCell = new Dictionary<string, int>();
        private readonly Dictionary<string, int> lastCreepHealth = new Dictionary<string, int>();
        private readonly Dictionary<string, float> creepHitFlashUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, Animator> creepAnimators = new Dictionary<string, Animator>();
        private readonly Dictionary<string, float> towerLastFiredAt = new Dictionary<string, float>();
        private readonly Dictionary<string, Vector3> towerAimTarget = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, float> towerAimYaw = new Dictionary<string, float>();
        private readonly Dictionary<string, SpinPartState> towerSpinPartState = new Dictionary<string, SpinPartState>();
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
        // Sphere-shaped impact effects and cube-shaped beams keep separate pools; sharing one made
        // them hand each other the wrong primitive shape (see GetPooled).
        private readonly Queue<GameObject> effectPool = new Queue<GameObject>();
        private readonly Queue<GameObject> beamPool = new Queue<GameObject>();
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
        private Material shockwaveRingMaterial;
        private readonly Queue<GameObject> shockwaveRingPool = new Queue<GameObject>();
        private readonly List<ExpandingRingEffect> activeShockwaveRings = new List<ExpandingRingEffect>();
        private readonly List<MortarShellEffect> activeMortarShells = new List<MortarShellEffect>();
        private readonly Dictionary<string, GameObject> towerMechanicMarkers = new Dictionary<string, GameObject>();
        // Keyed by the SERVICED tower's id, not the drone's — a tower can have at most one tether
        // regardless of how many drones are adjacent to it (see UpdateTowerServicingTether), so this
        // stays a strict one-per-tower dictionary just like towerMechanicMarkers above. Kept separate
        // from that dictionary rather than merged into it because the two hold different pooled
        // shapes (a flat ring vs. a stretched cube) drawn from different pools; see GetPooled's own
        // comment on why ring and beam pools must not mix.
        private readonly Dictionary<string, GameObject> towerServicingTethers = new Dictionary<string, GameObject>();

        /// <summary>One drifting fog disc per Spore Cloud Bloom, sized to that tower's attack range.</summary>
        /// <remarks>
        /// Its own dictionary and its own pool rather than sharing towerMechanicMarkers, for the same
        /// reason the servicing tethers have theirs: a pooled object carries the material it was
        /// built with, so mixing a fog quad into the shockwave-ring pool would hand a ring the fog
        /// shader (or the reverse) the first time one was recycled.
        /// </remarks>
        private readonly Dictionary<string, GameObject> towerSporeFog = new Dictionary<string, GameObject>();
        private readonly Queue<GameObject> sporeFogPool = new Queue<GameObject>();
        private Material sporeFogMaterial;
        private bool laneCreated;

        public PresentationDetail Detail => presentationDetail;

        public LaneCameraFraming CameraFraming => cameraFraming;

        public int ActiveLaneCameraId => Mathf.Clamp(activeLaneCameraId, 1, LaneCount);

        public int ActivePresentationObjectCount => activeTowers.Count + activeCreeps.Count + timedPresentations.Count;

        public int PooledPresentationObjectCount => towerPool.Count + PooledTowerPrefabCount() + creepPool.Count + PooledCreepPrefabCount() + effectPool.Count + beamPool.Count + textPool.Count;

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

        private readonly Dictionary<string, LTW.Simulation.Primitives.GridPosition> towerVisionTargetsScratch =
            new Dictionary<string, LTW.Simulation.Primitives.GridPosition>();

        private void RenderSnapshot(LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
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
            // firing or cooldown.
            towerVisionTargetsScratch.Clear();
            for (var index = 0; index < snapshot.TowerAimTargets.Count; index++)
            {
                var visionTarget = snapshot.TowerAimTargets[index];
                towerVisionTargetsScratch[visionTarget.TowerEntityId.Value.ToString()] = visionTarget.TargetPosition;
            }

            foreach (var tower in snapshot.Towers)
            {
                var key = tower.EntityId.Value.ToString();
                visibleKeys.Add(key);
                towerRolesByCell[TowerGridKey(tower.Position, tower.LaneId)] = tower.TowerId.Value;
                towerTiersByCell[TowerGridKey(tower.Position, tower.LaneId)] = tower.Tier;
                var visualProfile = towerVisualLibrary != null ? towerVisualLibrary.FindProfile(tower.TowerId.Value) : null;
                var towerObject = GetOrCreateTower(key, visualProfile);
                SetTowerTransform(towerObject, tower.Position, tower.LaneId, tower.TowerId.Value, visualProfile);
                ApplyTowerColor(towerObject, tower.TowerId.Value, tower.OwnerId.Value, visualProfile, tower.Tier);

                UpdateTowerMechanicMarker(key, tower.TowerId.Value, tower.Position, tower.LaneId, snapshot);
                UpdateTowerServicingTether(key, tower, snapshot);
                UpdateSporeFog(key, tower.TowerId.Value, GridToWorld(tower.Position, tower.LaneId), TowerRangeCells(tower.TowerId.Value));

                if (towerVisionTargetsScratch.TryGetValue(key, out var visionTargetPosition))
                {
                    towerAimTarget[key] = GridToWorld(visionTargetPosition, tower.LaneId);
                }

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
                SetCreepTransform(creepObject, CreepTravelPosition(creep), creep.LaneId, creep.CreepId.Value, visualProfile, isNewCreep, hitFlashUntil, key);
                UpdateCreepAnimationSpeed(key, creepObject, creep.CreepId.Value, creep.SpeedPerSecond);
                var healthFraction = CreepHealthFraction(creep.Health, creep.MaxHealth);
                var isHitFlashing = creepHitFlashUntil.TryGetValue(key, out var flashUntil) && Time.time < flashUntil;
                ApplyCreepColor(creepObject, creep.CreepId.Value, creep.SenderId.Value, visualProfile, healthFraction, isHitFlashing);
                if (UsesMeshVisual(creepObject) && ContainsRole(creep.CreepId.Value, "swarm"))
                {
                    // Replaces the single mesh-backed body with a small cluster, not an overlay on
                    // top of it, so this runs unconditionally rather than being gated behind
                    // suppressCreepGameplayOverlays — skipping it would leave the creep showing
                    // nothing, since ConfigureSwarmCluster is what hides the original body.
                    ConfigureSwarmCluster(creepObject, creep.CreepId.Value, creep.SenderId.Value, healthFraction, isHitFlashing);
                }

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
                        SpawnEffect(buildPosition, MintSignal, 0.68f, 0.34f, BurstShape.Rise);
                        // No "WARD" label: it confirmed an action the player had just taken, at the
                        // cell they had just tapped, where the tower is now visibly standing.
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
                        var firedTowerKey = fired.TowerEntityId.Value.ToString();
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
                        var hitPosition = PositionFor(damaged.CreepEntityId.Value.ToString());
                        var damagedCreepId = CreepIdFor(damaged.CreepEntityId.Value.ToString());
                        var towerPosition = GridToWorld(damaged.TowerPosition, damaged.LaneId);
                        var towerRole = TowerRoleAt(damaged.TowerPosition, damaged.LaneId);
                        var towerTier = TowerTierAt(damaged.TowerPosition, damaged.LaneId);
                        var attackBody = ResolveTowerBodyTransform(damaged.TowerEntityId.Value.ToString());
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
                        SpawnEffect(position, LeakRed, 0.86f, 0.42f, BurstShape.Sweep);
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
                        if (leak.SenderId.Value != leak.DefenderId.Value)
                        {
                            var stealPosition = IncomePosition(leak.SenderId.Value);
                            SpawnEffect(stealPosition, MintSignal, 0.5f, 0.3f, BurstShape.Rise);
                            SpawnFloatingText(stealPosition, $"+{leak.LivesLost.Amount} LIFE", MintSignal, 0.66f);
                            SpawnReducedEffectCue(stealPosition, "STOLE", MintSignal);
                        }

                        PlaySound(leakClip);
                        TriggerHapticFeedback();
                        break;
                    case IncomeTickEvent incomeTick:
                        SpawnIncomeLaneCue(incomeTick.PlayerId.Value);
                        SpawnEffect(IncomePosition(incomeTick.PlayerId.Value), SignalGold, 0.46f, 0.22f, BurstShape.Rise);
                        SpawnFloatingText(IncomePosition(incomeTick.PlayerId.Value), $"+{incomeTick.GoldAwarded.Amount} income", SignalGold, 0.58f);
                        SpawnReducedEffectCue(IncomePosition(incomeTick.PlayerId.Value), "INCOME", SignalGold);
                        PlaySound(incomeClip);
                        break;
                    case PlayerEliminatedEvent eliminated:
                        SpawnLaneShutdownCue(eliminated.PlayerId.Value);
                        SpawnEffect(LaneCenter(eliminated.PlayerId.Value) + Vector3.up * 0.2f, LeakRed, 1.15f, 0.55f, BurstShape.Sweep);
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

        private void SpawnEffect(Vector3 position, Color color, float scale, float duration) =>
            SpawnEffect(position, color, scale, duration, BurstShape.Impact);

        /// <summary>
        /// Emits a particle burst for a game event.
        /// </summary>
        /// <remarks>
        /// Every effect in the game routes through here, which is why upgrading this one method from
        /// a primitive to a particle system upgrades roughly twenty call sites at once — build, sell,
        /// spawn, hit, kill, leak, income, elimination, muzzle flash and mortar impact.
        ///
        /// It used to spawn a pooled sphere, set its colour, and release it after `duration`. That is
        /// a shape which appears, holds and vanishes: nothing about it moves, so it read as a debug
        /// gizmo regardless of colour.
        ///
        /// No pooling and no TimedPresentation registration, unlike every other presentation object
        /// here. A burst emits into a shared long-lived system (see LTWParticleBurst), so it costs
        /// particles rather than GameObjects and there is nothing to release. Pooling an emitter per
        /// burst was tried first and measured: peak active presentation objects went from 2,769 to
        /// 6,082 on the same seed, because each emitter must outlive its own particles and most
        /// effects here are shorter than the pad that requires.
        ///
        /// The signature is unchanged so the existing call sites keep their tuned scales and
        /// durations. Those values were chosen against the old flash and still mean the same things —
        /// how big the event is and how long it lasts.
        /// </remarks>
        private void SpawnEffect(Vector3 position, Color color, float scale, float duration, BurstShape shape, Vector3 direction = default)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                return;
            }

            BurstEmitter(shape)?.Emit(position, color, scale, duration, direction);
        }

        /// <summary>The shared emitter for one burst shape, created on first use.</summary>
        /// <remarks>
        /// Parented to this renderer so the emitters are torn down with the match rather than
        /// leaking across resets, and so they never appear in the pooled-object accounting the
        /// batch harness asserts on — they are fixtures, not pooled instances.
        /// </remarks>
        private LTWParticleBurst BurstEmitter(BurstShape shape)
        {
            if (burstEmitters.TryGetValue(shape, out var emitter) && emitter != null)
            {
                return emitter;
            }

            emitter = LTWParticleBurst.Create(transform, shape);
            burstEmitters[shape] = emitter;
            return emitter;
        }

        private readonly Dictionary<BurstShape, LTWParticleBurst> burstEmitters =
            new Dictionary<BurstShape, LTWParticleBurst>();

        /// <summary>Kills every live particle. Called on reset so effects do not survive a match.</summary>
        private void ClearBurstEmitters()
        {
            foreach (var pair in burstEmitters)
            {
                if (pair.Value != null)
                {
                    pair.Value.ClearAll();
                }
            }
        }

        /// <summary>Default beam thickness. Wider than the old 0.06 box, which was a wire.</summary>
        private const float DefaultBeamWidth = 0.16f;

        /// <summary>
        /// How much wider the beam's mesh is than the beam it draws. The shader treats the inner
        /// 1/<see cref="BeamHaloWidthScale"/> of the tube as the bright core and fades a halo across
        /// the rest, so <c>width</c> stays the width of the visible shot at every call site.
        /// </summary>
        private const float BeamHaloWidthScale = 3f;

        private Material weaponBeamMaterial;

        private Material WeaponBeamMaterial()
        {
            if (weaponBeamMaterial == null)
            {
                weaponBeamMaterial = BoardRenderResources.CreateWeaponBeamMaterial("LTW Weapon Beam");
            }

            return weaponBeamMaterial;
        }

        private void SpawnBeam(Vector3 start, Vector3 end, Color color, float duration) =>
            SpawnBeam(start, end, color, duration, DefaultBeamWidth, 1f);

        /// <summary>
        /// A tower's shot: a hot core in a soft glow, tapering toward the target.
        /// </summary>
        /// <remarks>
        /// The mesh is still a cube, but it is no longer drawn as one. LTWWeaponBeam fades alpha
        /// radially from the cube's axis, so the square cross-section never shows and the box reads
        /// as a round tube — which is why this does not need to billboard a quad toward an
        /// orthographic camera, the usual way beam rendering goes wrong.
        ///
        /// <paramref name="width"/> and <paramref name="intensity"/> exist for the per-line and
        /// per-tier work: a GROVE vine is thicker and dimmer than an ARCANE lance, and a tier-3 shot
        /// is heavier than a tier-1 one. Callers that do not care get the defaults.
        ///
        /// Still early-outs under ReducedEffects, so any mechanic whose ONLY tell is a beam is
        /// invisible at that setting. That is a known gap, not a new one.
        /// </remarks>
        private void SpawnBeam(Vector3 start, Vector3 end, Color color, float duration, float width, float intensity)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                return;
            }

            var beam = GetPooled(beamPool, "TowerBeam", PrimitiveType.Cube);
            var midpoint = Vector3.Lerp(start, end, 0.5f);
            var distance = Vector3.Distance(start, end);
            beam.transform.position = midpoint;
            beam.transform.LookAt(end);
            // Wider than the beam being drawn: LTWWeaponBeam fades a soft halo out across the mesh
            // and keeps the requested width as its bright core, so the geometry has to extend past
            // the visible shot or there is no room for the falloff.
            var meshWidth = width * BeamHaloWidthScale;
            beam.transform.localScale = new Vector3(meshWidth, meshWidth, Mathf.Max(0.1f, distance));

            if (beam.TryGetComponent<Renderer>(out var renderer))
            {
                // sharedMaterial would recolour every live beam at once; each shot needs its own.
                renderer.material = WeaponBeamMaterial();
                var instance = renderer.material;
                instance.color = color;
                if (instance.HasProperty("_Color")) instance.SetColor("_Color", color);
                if (instance.HasProperty("_CoreColor"))
                {
                    // The core is the shot's colour pushed toward white, so every beam has a hotter
                    // inside than its edge without needing a second colour authored per caller.
                    instance.SetColor("_CoreColor", Color.Lerp(color, Color.white, 0.72f));
                }

                if (instance.HasProperty("_Intensity")) instance.SetFloat("_Intensity", intensity);
                // Driven from the same constant that widened the mesh, so the core stays exactly the
                // requested width however BeamHaloWidthScale is retuned.
                if (instance.HasProperty("_CoreRadius")) instance.SetFloat("_CoreRadius", 1f / BeamHaloWidthScale);
                // Casting shadows from a glow is wrong and costs a pass per shot.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            timedPresentations.Add(new TimedPresentation(beam, Time.time + duration, beamPool));
        }

        /// <summary>
        /// A flat ring that grows from startScale to endScale and fades to transparent over
        /// duration — a real shockwave, unlike <see cref="SpawnEffect"/>'s static spawn-hold-vanish
        /// flash. Built from the same quad+soft-falloff-shader combo as the ground contact shadow
        /// decals (<see cref="BoardRenderResources.ContactShadowMesh"/>/CreateContactShadowMaterial),
        /// since that shader already gives a soft radially-fading edge for free.
        /// </summary>
        private void SpawnExpandingRing(Vector3 position, Color color, float startScale, float endScale, float duration)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                return;
            }

            var ring = GetPooledShockwaveRing();
            ring.transform.position = position;
            ring.transform.localScale = new Vector3(startScale, 1f, startScale);
            SetColor(ring, color);
            activeShockwaveRings.Add(new ExpandingRingEffect(ring, Time.time, duration, startScale, endScale, color));
        }

        /// <summary>
        /// Simulation ticks per second, read from the driver so a tick count from an event converts
        /// to real seconds. Falls back to the driver's own default if the driver is missing.
        /// </summary>
        private float SimulationTicksPerSecond() => simulationDriver != null ? simulationDriver.TicksPerSecond : 4f;

        /// <summary>
        /// Launches a mortar shell: an arcing projectile plus a ground telegraph at the cell it will
        /// land on.
        /// </summary>
        /// <remarks>
        /// The telegraph is the important half. A Foundry Core deals no damage when it fires and its
        /// shell lands half a second later, so without a marker on the ground the player has no way to
        /// read where or when — the tower becomes hidden dice and reads as broken. With it, the delay
        /// is fair information: you can see the shell in the air and the cell it is committed to.
        ///
        /// The whole flight is animated client-side from the launch event, which is why
        /// TowerFiredEvent carries ImpactTick and ImpactPosition. Keying the landing off the damage
        /// event instead would make a shell that hits nothing visually evaporate in mid-air — and
        /// while the simulation now refuses to fire shells it cannot land, a shell can still lose its
        /// target to another tower during the flight.
        /// </remarks>
        private void SpawnMortarShell(Vector3 from, Vector3 to, float flightSeconds)
        {
            if (PresentationPreferences.ReducedEffects || flightSeconds <= 0f)
            {
                return;
            }

            var shell = GetPooled(effectPool, "MortarShell", PrimitiveType.Sphere);
            shell.transform.localScale = Vector3.one * 0.22f;
            shell.transform.position = from + Vector3.up * MortarLaunchHeight;
            SetColor(shell, MortarShellColor);

            // A ring that contracts onto the impact cell, so its size reads as a countdown.
            var telegraph = GetPooledShockwaveRing();
            telegraph.transform.position = to + Vector3.up * 0.02f;
            telegraph.transform.localScale = new Vector3(MortarTelegraphStartScale, 1f, MortarTelegraphStartScale);
            SetColor(telegraph, MortarTelegraphColor);

            activeMortarShells.Add(new MortarShellEffect(shell, telegraph, from, to, Time.time, flightSeconds));
        }

        private void UpdateMortarShells()
        {
            for (var index = activeMortarShells.Count - 1; index >= 0; index--)
            {
                var shell = activeMortarShells[index];
                var t = Mathf.Clamp01((Time.time - shell.StartTime) / shell.Duration);

                // Straight line across the board, parabola in height: 4t(1-t) peaks at t=0.5 and is
                // zero at both ends, so the shell leaves the stacks and meets the ground exactly on
                // the telegraph.
                var ground = Vector3.Lerp(shell.From, shell.To, t);
                var lift = MortarLaunchHeight + MortarArcHeight * 4f * t * (1f - t);
                shell.Shell.transform.position = new Vector3(ground.x, shell.From.y + lift, ground.z);

                // Telegraph contracts and brightens as impact approaches.
                var telegraphScale = Mathf.Lerp(MortarTelegraphStartScale, MortarTelegraphEndScale, t);
                shell.Telegraph.transform.localScale = new Vector3(telegraphScale, 1f, telegraphScale);
                SetColor(shell.Telegraph, new Color(
                    MortarTelegraphColor.r,
                    MortarTelegraphColor.g,
                    MortarTelegraphColor.b,
                    Mathf.Lerp(MortarTelegraphColor.a * 0.55f, MortarTelegraphColor.a, t)));

                if (t < 1f)
                {
                    continue;
                }

                // Impact. The crater fires whether or not anything was standing there, so a shell that
                // loses its target still visibly lands rather than vanishing.
                SpawnExpandingRing(shell.To + Vector3.up * 0.05f, MortarImpactColor, 0.2f, 1.9f, 0.34f);
                SpawnEffect(shell.To + Vector3.up * 0.12f, MortarImpactColor, 0.6f, 0.24f);

                ReleaseToPool(shell.Shell, effectPool);
                ReleaseToPool(shell.Telegraph, shockwaveRingPool);
                activeMortarShells.RemoveAt(index);
            }
        }

        /// <summary>
        /// Draws the standing ground marker a tower's mechanic needs, if it has one.
        /// </summary>
        /// <remarks>
        /// Two mechanics are invisible without this, and an invisible mechanic is a spreadsheet:
        ///
        /// Thorn Snare brakes creeps that stand in its zone. The creep does visibly crawl, but nothing
        /// says WHERE the zone is, so the player cannot place a second tower to exploit it. A decal
        /// over the braked cells makes the zone a thing you can build around.
        ///
        /// Grovebond gives a Sapling +1 damage per adjacent Grove tower. Three of its four states are
        /// otherwise pixel-identical — the only evidence is a damage number that has to be compared
        /// against a different sapling. The marker's brightness tracks the bonus.
        ///
        /// One pooled quad per tower, updated in place, released with the tower.
        /// </remarks>
        private void UpdateTowerMechanicMarker(
            string key,
            string towerId,
            GridPosition position,
            LaneId laneId,
            LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            var isThorn = towerId.Contains("thorn");
            var isSapling = towerId.Contains("sapling");
            if ((!isThorn && !isSapling) || PresentationPreferences.ReducedEffects)
            {
                ReleaseTowerMechanicMarker(key);
                return;
            }

            if (!towerMechanicMarkers.TryGetValue(key, out var marker) || marker == null)
            {
                marker = GetPooledShockwaveRing();
                marker.name = $"TowerMechanicMarker_{key}";
                towerMechanicMarkers[key] = marker;
            }

            var centre = GridToWorld(position, laneId);
            if (isThorn)
            {
                // Sized to the braked span rather than to the tower: BrambleZoneCells in the
                // simulation is 3, and the zone starts at the first route cell in range.
                marker.transform.position = new Vector3(centre.x, BoardTopY + SpanningDecalLift, centre.z);
                marker.transform.localScale = new Vector3(BrambleMarkerScale, 1f, BrambleMarkerScale);
                SetColor(marker, BrambleMarkerColor);
                return;
            }

            // Grovebond: brightness and size track the bonus, so a bonded cluster reads at a glance.
            var bonus = CountAdjacentGroveTowers(position, laneId, snapshot);
            if (bonus == 0)
            {
                // An unbonded sapling gets no ring at all. That is the clearest possible read of the
                // mechanic: the ring's presence means "this one is bonded".
                ReleaseTowerMechanicMarker(key);
                return;
            }

            marker.transform.position = new Vector3(centre.x, BoardTopY + SpanningDecalLift, centre.z);
            // Floors were originally 0.55 scale / 0.16 alpha, which at the common bonus of 1 was
            // invisible under the tower mesh — verified in a capture. The ring now starts wide enough
            // to clear the silhouette and opaque enough to see, and still grows with the bonus.
            var scale = Mathf.Lerp(1.25f, 1.9f, (bonus - 1) / 2f);
            marker.transform.localScale = new Vector3(scale, 1f, scale);
            SetColor(marker, new Color(
                GrovebondMarkerColor.r,
                GrovebondMarkerColor.g,
                GrovebondMarkerColor.b,
                Mathf.Lerp(0.34f, GrovebondMarkerColor.a, (bonus - 1) / 2f)));
        }

        /// <summary>
        /// Mirrors CombatService.GrovebondBonus: orthogonal only, same lane, same owner, capped at 3.
        /// </summary>
        /// <remarks>
        /// Duplicating the rule in presentation is a real risk of drift, but the alternative is a new
        /// snapshot field carrying a number that only exists to be drawn. Kept honest by the marker
        /// being the only consumer — if it disagrees with the damage numbers, the marker is wrong.
        /// </remarks>
        private static int CountAdjacentGroveTowers(
            GridPosition position,
            LaneId laneId,
            LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            var adjacent = 0;
            for (var index = 0; index < snapshot.Towers.Count; index++)
            {
                var other = snapshot.Towers[index];
                if (other.LaneId.Value != laneId.Value)
                {
                    continue;
                }

                if (Mathf.Abs(other.Position.X - position.X) + Mathf.Abs(other.Position.Y - position.Y) != 1)
                {
                    continue;
                }

                var id = other.TowerId.Value;
                if (id.Contains("sapling") || id.Contains("bloomheart") || id.Contains("thorn")
                    || id.Contains("spore") || id.Contains("canopy"))
                {
                    adjacent++;
                }
            }

            return Mathf.Min(3, adjacent);
        }

        /// <summary>
        /// Draws a persistent tether from a tower to the Repair Drone Spire servicing it, if any.
        /// </summary>
        /// <remarks>
        /// CombatService.EffectiveCooldown reduces a serviced tower's cooldown by one tick and does
        /// nothing else visible, so without this the only evidence was a tower firing slightly
        /// faster than its stated cooldown — not something a player can see, only measure
        /// (GAMEPLAY_REVIEW_FINDINGS.md's open "Repair Drone's [buff] is invisible" item, written
        /// against the mechanic's earlier +1 range shape and stale since Servicing replaced it — a
        /// range halo would now show the wrong thing, since range no longer changes).
        ///
        /// Mirrors CombatService.IsServicedByDrone client-side, the same tradeoff already accepted
        /// for CountAdjacentGroveTowers above: duplicating the adjacency rule here risks drift from
        /// the simulation, but the alternative is a snapshot field that exists only to be drawn, and
        /// the tether being the only consumer keeps it honest — if it disagrees with a tower's
        /// actual fire rate, the tether is wrong, not the mechanic.
        ///
        /// A tower gets at most one tether even if multiple drones are adjacent, since Servicing
        /// does not stack (see EffectiveCooldown's own comment on why). The lowest EntityId among
        /// adjacent drones is picked so the choice is stable and independent of snapshot ordering,
        /// not because the specific choice of drone matters.
        /// </remarks>
        private void UpdateTowerServicingTether(string key, LTW.Simulation.Combat.TowerCombatState tower, LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            if (PresentationPreferences.ReducedEffects || IsRepairDroneTower(tower.TowerId.Value))
            {
                // The drone itself never grows a tether toward whichever neighbour happens to
                // service IT in turn (two adjacent drones is a legal, if unusual, placement) — a
                // tether reads as "this tower is being helped", which is not the drone's story.
                ReleaseTowerServicingTether(key);
                return;
            }

            LTW.Simulation.Combat.TowerCombatState drone = null;
            for (var index = 0; index < snapshot.Towers.Count; index++)
            {
                var other = snapshot.Towers[index];
                if (other.EntityId.Equals(tower.EntityId))
                {
                    continue;
                }

                if (!other.LaneId.Equals(tower.LaneId) || !other.OwnerId.Equals(tower.OwnerId))
                {
                    continue;
                }

                if (!IsRepairDroneTower(other.TowerId.Value))
                {
                    continue;
                }

                if (Mathf.Abs(other.Position.X - tower.Position.X) + Mathf.Abs(other.Position.Y - tower.Position.Y) != 1)
                {
                    continue;
                }

                if (drone == null || other.EntityId.Value < drone.EntityId.Value)
                {
                    drone = other;
                }
            }

            if (drone == null)
            {
                ReleaseTowerServicingTether(key);
                return;
            }

            if (!towerServicingTethers.TryGetValue(key, out var tether) || tether == null)
            {
                tether = GetPooled(beamPool, "ServicingTether", PrimitiveType.Cube);
                towerServicingTethers[key] = tether;
            }

            var from = GridToWorld(tower.Position, tower.LaneId) + Vector3.up * ServicingTetherHeight;
            var to = GridToWorld(drone.Position, drone.LaneId) + Vector3.up * ServicingTetherHeight;
            var midpoint = Vector3.Lerp(from, to, 0.5f);
            var distance = Vector3.Distance(from, to);
            tether.transform.position = midpoint;
            tether.transform.LookAt(to);
            tether.transform.localScale = new Vector3(ServicingTetherThickness, ServicingTetherThickness, Mathf.Max(0.1f, distance));
            // Assert the material, don't just tint it. This shares beamPool with SpawnBeam, which
            // assigns the additive LTWWeaponBeam material — and SetColor only writes .color, so a
            // tether recycled from a released beam would keep that shader and draw as a glowing
            // additive tube instead of a solid line, at random, depending on pool order. Exactly the
            // failure GetPooled's own comment describes for meshes, one dimension over.
            if (tether.TryGetComponent<Renderer>(out var tetherRenderer))
            {
                tetherRenderer.sharedMaterial = BoardRenderResources.SharedOpaque(ServicingTetherColor);
            }
        }

        private static bool IsRepairDroneTower(string towerId) => towerId.IndexOf("repair_drone", StringComparison.OrdinalIgnoreCase) >= 0;

        private void ReleaseTowerServicingTether(string key)
        {
            if (!towerServicingTethers.TryGetValue(key, out var tether))
            {
                return;
            }

            if (tether != null)
            {
                ReleaseToPool(tether, beamPool);
            }

            towerServicingTethers.Remove(key);
        }

        private void ReleaseTowerMechanicMarker(string key)
        {
            if (!towerMechanicMarkers.TryGetValue(key, out var marker))
            {
                return;
            }

            if (marker != null)
            {
                ReleaseToPool(marker, shockwaveRingPool);
            }

            towerMechanicMarkers.Remove(key);
        }

        private void UpdateExpandingRings()
        {
            for (var index = activeShockwaveRings.Count - 1; index >= 0; index--)
            {
                var ring = activeShockwaveRings[index];
                var t = Mathf.Clamp01((Time.time - ring.StartTime) / ring.Duration);
                var scale = Mathf.Lerp(ring.StartScale, ring.EndScale, t);
                ring.Object.transform.localScale = new Vector3(scale, 1f, scale);
                SetColor(ring.Object, new Color(ring.BaseColor.r, ring.BaseColor.g, ring.BaseColor.b, ring.BaseColor.a * (1f - t)));

                if (t >= 1f)
                {
                    ReleaseToPool(ring.Object, shockwaveRingPool);
                    activeShockwaveRings.RemoveAt(index);
                }
            }
        }

        private Material ShockwaveRingMaterial()
        {
            if (shockwaveRingMaterial == null)
            {
                shockwaveRingMaterial = BoardRenderResources.CreateContactShadowMaterial(
                    "LTW Shockwave Ring",
                    Color.white,
                    0.55f);
            }

            return shockwaveRingMaterial;
        }

        /// <summary>
        /// Green spore fog spreading from a Spore Cloud Bloom out to the edge of its range.
        /// </summary>
        /// <remarks>
        /// The fog IS the range indicator: it is densest over the tower and fades to nothing exactly
        /// where the tower stops reaching, so a player can read how far it covers without a range
        /// ring drawn on top.
        ///
        /// One honest imprecision. Range in the simulation is MANHATTAN — CombatService.IsInRange
        /// sums |dx| + |dy| — so the true footprint is a diamond, while this is a circle. A circle
        /// inscribed to touch the diamond's points therefore overstates the diagonals, and one
        /// shrunk to fit understates the axes. It is drawn at the full range because fog with a
        /// visible diamond edge would look authored rather than atmospheric, and because the whole
        /// point of the gradient is that the boundary is not locatable anyway. If the fog is ever
        /// promoted from atmosphere to a precise range READOUT, this has to become a diamond.
        ///
        /// Scale is diameter, hence 2x the range. Sat just above the spanning board decals so the
        /// fog layers over the lane rather than fighting the bramble and bond rings for the same
        /// millimetre.
        /// </remarks>
        private void UpdateSporeFog(string key, string towerId, Vector3 towerPosition, float rangeCells)
        {
            if (!ContainsRole(towerId, "spore") || PresentationPreferences.ReducedEffects)
            {
                ReleaseSporeFog(key);
                return;
            }

            if (!towerSporeFog.TryGetValue(key, out var fog) || fog == null)
            {
                fog = GetPooledSporeFog();
                fog.name = $"SporeFog_{key}";
                towerSporeFog[key] = fog;
            }

            var diameter = Mathf.Max(1f, rangeCells * 2f);
            fog.transform.position = new Vector3(towerPosition.x, BoardTopY + SporeFogLift, towerPosition.z);
            fog.transform.localScale = new Vector3(diameter, 1f, diameter);
        }

        /// <summary>Just above SpanningDecalLift, so fog reads as sitting over the ground markings.</summary>
        private const float SporeFogLift = 0.075f;

        /// <summary>
        /// A tower's attack range in cells, read from the simulation and cached per tower id.
        /// </summary>
        /// <remarks>
        /// Read rather than copied: a client-side table of ranges is exactly the drift that put
        /// three different Arrow costs in the codebase before UnityCommandAdapter started reading
        /// cost from ContentCatalog. Cached because this runs per Spore Cloud per frame and the
        /// lookup is a linear scan of the tower list.
        ///
        /// Falls back to 3 — Spore Cloud's authored range — only if the driver is not wired yet, so
        /// a fog that appears before the simulation is up is the right size rather than a dot.
        /// </remarks>
        private readonly Dictionary<string, float> towerRangeCells = new Dictionary<string, float>();

        private float TowerRangeCells(string towerId)
        {
            if (towerRangeCells.TryGetValue(towerId, out var cached))
            {
                return cached;
            }

            var range = 3f;
            var catalog = simulationDriver != null ? simulationDriver.Content : null;
            if (catalog != null)
            {
                foreach (var tower in catalog.Towers)
                {
                    if (tower.Id.Value == towerId)
                    {
                        range = tower.RangeCells;
                        break;
                    }
                }

                towerRangeCells[towerId] = range;
            }

            return range;
        }

        private Material SporeFogMaterial()
        {
            if (sporeFogMaterial == null)
            {
                sporeFogMaterial = BoardRenderResources.CreateSporeFogMaterial(
                    "LTW Spore Fog",
                    new Color(0.42f, 0.86f, 0.34f, 0.26f),
                    softness: 0.95f,
                    churn: 0.55f,
                    speed: 0.45f);
            }

            return sporeFogMaterial;
        }

        private GameObject GetPooledSporeFog()
        {
            if (sporeFogPool.Count > 0)
            {
                var pooled = sporeFogPool.Dequeue();
                pooled.SetActive(true);
                return pooled;
            }

            var fog = new GameObject("SporeFog");
            fog.AddComponent<MeshFilter>().sharedMesh = BoardRenderResources.ContactShadowMesh;
            var renderer = fog.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SporeFogMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return fog;
        }

        private void ReleaseSporeFog(string key)
        {
            if (!towerSporeFog.TryGetValue(key, out var fog))
            {
                return;
            }

            if (fog != null)
            {
                fog.SetActive(false);
                sporeFogPool.Enqueue(fog);
            }

            towerSporeFog.Remove(key);
        }

        private GameObject GetPooledShockwaveRing()
        {
            if (shockwaveRingPool.Count > 0)
            {
                var pooled = shockwaveRingPool.Dequeue();
                pooled.SetActive(true);
                return pooled;
            }

            var ring = new GameObject("ShockwaveRing");
            ring.AddComponent<MeshFilter>().sharedMesh = BoardRenderResources.ContactShadowMesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ShockwaveRingMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return ring;
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

        /// <summary>
        /// A board label: SDF text with an outline, rising and fading over its life.
        /// </summary>
        /// <remarks>
        /// Was a legacy TextMesh that never assigned a font, so every label on the board rendered in
        /// Unity's built-in Arial — unlit, flat, no outline, over a busy board. That is the "blocky
        /// and plain" read, and the improvement cycle scores it as C10 Typography: "default engine
        /// font". TextMeshPro is used for the SDF rendering rather than for the typeface: it stays
        /// crisp at any distance and gives a real outline, which is what makes small text legible
        /// against the board instead of dissolving into it.
        ///
        /// Motion carries the rest. Text used to appear, hold and vanish, which reads as a label
        /// switching on. It now rises, fades out, and punches up in scale over its first frames, so
        /// it reads as an event that happened.
        /// </remarks>
        private void SpawnFloatingText(Vector3 position, string text, Color color, float duration)
        {
            if (!IsOnActiveLane(position))
            {
                return;
            }

            var textObject = GetTextObject();
            textObject.transform.position = position + Vector3.up * 0.55f;
            // Face the camera rather than lying flat on the board. The old fixed Euler(90,0,0) was
            // only legible because the camera was nearly straight down; at any real tilt the text
            // slants away and loses readability. Billboarding keeps it face-on at any camera angle.
            textObject.transform.rotation = FloatingTextRotation();
            textObject.transform.localScale = Vector3.one;

            var label = textObject.GetComponent<TMPro.TextMeshPro>();
            if (label == null)
            {
                label = textObject.AddComponent<TMPro.TextMeshPro>();
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.enableWordWrapping = false;
                label.fontSize = 3.4f;
                label.raycastTarget = false;
                // The outline is the whole point of moving to SDF: board text sits over lane
                // plating, range halos and creep bodies, and an unoutlined glyph at this size loses
                // its edges against all three.
                //
                // ONE shared outlined material for every label, built once (BoardTextMaterial).
                // Two other routes were tried and neither produced an outline: setting _OutlineWidth
                // and the OUTLINE_ON keyword on fontMaterial, and TMP's per-component outlineWidth
                // and outlineColor. Both compiled, ran, and rendered flat glyphs. Enabling the
                // keyword on a real material and handing it to the component is what TMP actually
                // honours, and it batches rather than instancing a material per label.
                label.fontSharedMaterial = BoardTextMaterial(label.font);
            }

            label.text = text;
            label.color = color;
            label.fontSize = 3.4f * PresentationPreferences.TextScale;

            // Board furniture such as the endpoint gate plates draws through SpriteRenderers with
            // sorting orders up to 3, so board text sorts above all board decoration.
            var textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = FloatingTextSortingOrder;
            }

            floatingLabels.Add(new FloatingLabel(textObject, label, Time.time, duration));
            timedPresentations.Add(new TimedPresentation(textObject, Time.time + duration, textPool));
        }

        /// <summary>
        /// Whether a world position is in the lane the camera is actually framing.
        /// </summary>
        /// <remarks>
        /// Board text was drawn for all eight lanes while the camera frames one, so roughly seven
        /// eighths of every label spawned was instantiated, positioned, billboarded, sorted and
        /// pooled for a lane nobody could see. Measured at 36 text objects a second across a match.
        /// Costs nothing in design to skip: the player is looking at their own lane.
        /// </remarks>
        private bool IsOnActiveLane(Vector3 position)
        {
            if (cameraFraming != LaneCameraFraming.ActiveLane)
            {
                return true;
            }

            var laneCentreX = LaneOffset(ActiveLaneCameraId) + BoardCenterX;
            return Mathf.Abs(position.x - laneCentreX) <= LaneSpacing * 0.5f;
        }

        private Material boardTextMaterial;

        /// <summary>
        /// The shared outlined material every board label draws with.
        /// </summary>
        /// <remarks>
        /// A dark outline is what keeps small text legible over lane plating, range halos and creep
        /// bodies. It is a material variant rather than a per-label property because that is the
        /// form TMP honours, and because one shared material lets all board text batch.
        /// </remarks>
        private Material BoardTextMaterial(TMPro.TMP_FontAsset font)
        {
            if (boardTextMaterial != null)
            {
                return boardTextMaterial;
            }

            boardTextMaterial = new Material(font.material) { name = "LTW Board Text" };
            boardTextMaterial.EnableKeyword("OUTLINE_ON");
            boardTextMaterial.SetFloat("_OutlineWidth", 0.25f);
            boardTextMaterial.SetColor("_OutlineColor", new Color(0.02f, 0.03f, 0.05f, 1f));
            return boardTextMaterial;
        }

        /// <summary>Rises, fades and punches in scale over its life. See SpawnFloatingText.</summary>
        private void UpdateFloatingLabels()
        {
            for (var index = floatingLabels.Count - 1; index >= 0; index--)
            {
                var entry = floatingLabels[index];
                if (entry.Object == null || entry.Label == null)
                {
                    floatingLabels.RemoveAt(index);
                    continue;
                }

                var age = (Time.time - entry.SpawnedAt) / Mathf.Max(0.01f, entry.Duration);
                if (age >= 1f)
                {
                    floatingLabels.RemoveAt(index);
                    continue;
                }

                entry.Object.transform.position = entry.Origin + Vector3.up * (age * FloatingTextRise);
                // Held solid for the first half, then faded, so a short label is legible for most of
                // its life instead of being half-transparent the whole way.
                var alpha = age < 0.5f ? 1f : 1f - (age - 0.5f) * 2f;
                var colour = entry.Label.color;
                entry.Label.color = new Color(colour.r, colour.g, colour.b, alpha);
                // A quick overshoot on arrival, settling to 1.
                var punch = age < 0.18f ? Mathf.Lerp(0.72f, 1.06f, age / 0.18f) : Mathf.Lerp(1.06f, 1f, Mathf.InverseLerp(0.18f, 0.34f, age));
                entry.Object.transform.localScale = Vector3.one * Mathf.Min(punch, 1.06f);
            }
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

        /// <summary>
        /// All the offsets below are expressed relative to the tower's base — the same numeric
        /// vectors that used to be added to <paramref name="towerPosition"/> directly. When a live
        /// Body transform is available they instead go through <paramref name="bodyTransform"/>'s
        /// TransformPoint, so the whole attack cue rotates and drifts with the tower's actual
        /// current animated state (aim rotation, idle drift, recoil) rather than assuming the tower
        /// still sits at its rest pose. Falls back to the old flat world-space offset if the tower
        /// GameObject could not be resolved (e.g. it was removed the same frame).
        /// </summary>
        private void SpawnTowerAttackCue(Vector3 towerPosition, Vector3 hitPosition, string towerId, int damage, Transform bodyTransform, int tier = 1)
        {
            Vector3 At(Vector3 localOffset) =>
                bodyTransform != null ? bodyTransform.TransformPoint(localOffset) : towerPosition + localOffset;

            var shotColor = TowerShotColor(towerId, damage);
            var muzzle = At(Vector3.up * 0.62f);
            // The five bespoke branches below are all Arcane, and take the same thin, bright, quick
            // grammar as every other Arcane tower — they keep their own choreography and colours,
            // which are already tuned, but no longer their own arbitrary beam widths.
            var line = LineFor(towerId);
            var style = StyleFor(line, damage, tier);
            if (IsArrowTower(towerId))
            {
                SpawnBeam(At(new Vector3(-0.5f, 0.62f, -0.18f)), At(new Vector3(0.5f, 0.62f, -0.18f)), shotColor, 0.08f);
                SpawnBeam(At(new Vector3(0f, 0.58f, -0.32f)), At(new Vector3(0f, 0.66f, 0.26f)), SignalGold, 0.08f);
                SpawnBeam(At(new Vector3(0f, 0.62f, 0.08f)), hitPosition + Vector3.up * 0.12f, shotColor, style.Duration, style.Width, style.Intensity);
                SpawnCellFrameCue(hitPosition, shotColor, damage >= 5 ? 0.15f : 0.1f);
                SpawnEffect(At(new Vector3(0f, 0.62f, 0.12f)), shotColor, damage >= 5 ? 0.28f : 0.2f, 0.08f, BurstShape.Muzzle, hitPosition - At(new Vector3(0f, 0.62f, 0.12f)));
                return;
            }

            if (IsControlTower(towerId))
            {
                // The old twin symmetric beams (fixed local offsets either side of centre) were
                // tuned for a Control tower that never turned to aim — with its core+ring assembly
                // now genuinely tracking the target (HeadPivot), a single beam from the core reads
                // as an actual aimed shot rather than a fixed decorative gate. Expanding rings lean
                // into Control's own ring/portal shape, replacing a static glow at the tower and
                // the same blocky SpawnCellFrameCue square other towers' VFX had.
                SpawnBeam(At(Vector3.up * 0.62f), hitPosition + Vector3.up * 0.12f, shotColor, style.Duration * 1.4f, style.Width, style.Intensity);
                SpawnExpandingRing(At(Vector3.up * 0.28f), shotColor, 0.15f, 1.4f, 0.35f);
                SpawnExpandingRing(hitPosition + Vector3.up * 0.18f, shotColor, 0.1f, 0.85f, 0.22f);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.42f : 0.32f, 0.16f);
                return;
            }

            if (IsRelayTower(towerId))
            {
                SpawnBeam(muzzle, hitPosition + Vector3.up * 0.2f, shotColor, style.Duration * 1.6f, style.Width, style.Intensity);
                SpawnBeam(At(new Vector3(-0.34f, 0.34f, 0f)), At(new Vector3(0.34f, 0.34f, 0f)), shotColor, 0.14f);
                SpawnBeam(At(new Vector3(0f, 0.58f, -0.34f)), At(new Vector3(0f, 0.58f, 0.34f)), shotColor, 0.14f);
                SpawnCellFrameCue(towerPosition, shotColor, 0.16f);
                SpawnEffect(muzzle, shotColor, 0.26f, 0.12f, BurstShape.Muzzle, hitPosition - muzzle);
                return;
            }

            if (IsPulseTower(towerId))
            {
                // Previously drew 4 beams connecting the tower's own corners plus 2 more crossing
                // diagonally — a literal square outline that read as dynamic while the whole body
                // still rotated with each shot, but now that Pulse stays fixed (see locksYaw in
                // UpdateTowerMotion), it flashed as an obvious static geometric square every time
                // it fired. Replaced with an actual expanding shockwave — Pulse's whole identity is
                // an energy pulse, and a ring that visibly grows outward from the spinning Ring
                // part reads as that far better than a static glow ever could. A quick gold core
                // pop underneath gives it a starting flash to expand from.
                // Four short radial spokes, thrown outward the instant it fires. Measured at the
                // board camera, this tower's cue put 146 lit pixels on screen at the moment of
                // firing — the lowest in the roster by two orders of magnitude — because everything
                // it drew was an expanding ring or a particle burst, and BOTH of those develop over
                // later frames rather than existing when the shot happens. Spokes are geometry, so
                // they are there immediately, and radiating outward is the one direction language
                // that does not contradict an omnidirectional splash emitter.
                for (var spoke = 0; spoke < 4; spoke++)
                {
                    var heading = Quaternion.Euler(0f, 45f + spoke * 90f, 0f) * Vector3.forward;
                    SpawnBeam(At(Vector3.up * 0.5f), At(Vector3.up * 0.5f) + heading * 0.72f, shotColor, style.Duration, style.Width * 1.3f, style.Intensity);
                }

                SpawnExpandingRing(At(Vector3.up * 0.5f), shotColor, 0.15f, 1.6f, 0.4f);
                SpawnEffect(At(Vector3.up * 0.5f), SignalGold, 0.16f, 0.1f);
                // SpawnCellFrameCue drew the same kind of static square-outline box this VFX used
                // to draw around the tower itself — same problem, same fix: an expanding ring
                // reads as the splash actually spreading from the impact, not a blocky marker.
                SpawnExpandingRing(hitPosition + Vector3.up * 0.18f, shotColor, 0.1f, 0.85f, 0.22f);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.5f : 0.36f, 0.16f);
                return;
            }

            if (IsPrismTower(towerId))
            {
                SpawnBeam(At(new Vector3(-0.16f, 0.7f, 0f)), At(new Vector3(0.16f, 0.7f, 0f)), SignalGold, 0.12f);
                SpawnBeam(At(new Vector3(0f, 0.7f, -0.16f)), At(new Vector3(0f, 0.7f, 0.16f)), SignalGold, 0.12f);
                SpawnEffect(At(Vector3.up * 0.7f), SignalGold, 0.22f, 0.12f);
                SpawnBeam(At(Vector3.up * 0.7f), hitPosition + Vector3.up * 0.16f, shotColor, style.Duration * 1.6f, style.Width, style.Intensity);
                SpawnEffect(hitPosition + Vector3.up * 0.08f, shotColor, damage >= 5 ? 0.46f : 0.32f, 0.18f);
                return;
            }

            var impact = hitPosition + Vector3.up * 0.12f;

            // Per-tower tells. Each of these towers has a mechanic that was invisible while it drew
            // the shared fallback below. Several read their own mechanic straight off `damage`,
            // which is the honest source: Grovebond, Crowd Bloom and Tesla's halving chain all
            // express themselves as damage the simulation already computed.
            if (ContainsRole(towerId, "tesla"))
            {
                // Chain Arc hops backward down the queue, halving each time, and each hop arrives
                // as its own event — so a thinner, dimmer arc for a weaker hop shows the decay.
                var arcColor = Color.Lerp(new Color(0.55f, 0.76f, 1f), new Color(0.86f, 0.95f, 1f), Mathf.Clamp01(damage / 8f));
                SpawnForkedArc(muzzle, impact, arcColor, style, 4);
                SpawnEffect(impact, arcColor, damage >= 5 ? 0.34f : 0.24f, 0.1f);
                return;
            }

            if (ContainsRole(towerId, "gatling"))
            {
                SpawnTracerShot(muzzle, impact, shotColor, style);
                return;
            }

            if (ContainsRole(towerId, "barricade"))
            {
                SpawnSlugShot(muzzle, impact, shotColor, style);
                return;
            }

            if (IsRepairDroneTower(towerId))
            {
                // A support tower, not a weapon: a maintenance pulse rather than a shot. The
                // servicing tether to its neighbours is drawn continuously elsewhere.
                // A thin service beam first, for the same reason as Pulse above: rings and bursts
                // both arrive late, and measured at the instant of firing this tower put 424 lit
                // pixels on screen. Kept deliberately thin and short-lived — this is a support
                // tower and the beam is there to say WHEN it acted, not to look like a weapon.
                SpawnBeam(muzzle, impact, shotColor, style.Duration * 0.8f, style.Width * 0.6f, style.Intensity);
                SpawnExpandingRing(muzzle, shotColor, 0.3f, 0.95f, style.Duration * 1.6f);
                SpawnExpandingRing(impact, shotColor, 0.2f, 0.6f, style.Duration);
                SpawnEffect(impact, shotColor, 0.24f, 0.12f);
                return;
            }

            if (ContainsRole(towerId, "thorn"))
            {
                // Bramble Hold halves speed in a zone. The vines snap taut and release quickly —
                // the brake itself stays shown by the persistent bramble zone decal, so this does
                // not need to hold for the whole duration of the slow.
                SpawnVineLash(muzzle, impact, shotColor, style, 2, 0.34f);
                SpawnEffect(impact, shotColor, damage >= 5 ? 0.34f : 0.26f, style.Duration * 0.5f);
                return;
            }

            if (ContainsRole(towerId, "canopy"))
            {
                // Deep Roots uniquely targets the REARMOST creep, so this lash deliberately reads as
                // heavy and long — it is reaching past nearer creeps to the back of the lane.
                SpawnVineLash(muzzle, impact, shotColor, style.Scaled(1.35f, 1f), 3, 0.42f);
                SpawnExpandingRing(impact, shotColor, 0.14f, 0.8f, style.Duration);
                return;
            }

            if (ContainsRole(towerId, "spore") || ContainsRole(towerId, "bloomheart"))
            {
                // Rot scales off the target's max health and Crowd Bloom off how many creeps share
                // the cell — both arrive as bigger damage, so a bloom sized by damage shows a fat
                // target or a big stack being punished specifically.
                var bloom = Mathf.Lerp(0.75f, 1.8f, Mathf.Clamp01(damage / 10f));
                SpawnBeam(muzzle, impact, shotColor, style.Duration, style.Width, style.Intensity);
                // A particle burst carries the bloom, with the ring only underneath it. Measured at
                // the real camera, SpawnExpandingRing draws a soft low-contrast glow rather than a
                // crisp ring — legible for Control, whose ring is a slow deliberate beat, but far
                // too weak to be the whole tell for two towers whose entire mechanic is "this got
                // bigger because the target was fat / the stack was deep".
                SpawnEffect(hitPosition, shotColor, 0.34f * bloom, style.Duration * 0.7f, BurstShape.Impact);
                SpawnExpandingRing(impact, shotColor, bloom * 0.34f, bloom, style.Duration);
                return;
            }

            if (ContainsRole(towerId, "sapling"))
            {
                // Grovebond adds damage per bonded neighbour, so a shot that visibly thickens with
                // damage is the bond paying off.
                var bonded = style.Scaled(Mathf.Lerp(0.7f, 1.6f, Mathf.Clamp01(damage / 8f)), 1f);
                SpawnBeam(muzzle, impact, shotColor, bonded.Duration, bonded.Width, bonded.Intensity);
                SpawnEffect(impact, shotColor, damage >= 5 ? 0.32f : 0.22f, style.Duration * 0.5f);
                return;
            }

            // The line grammar. Whatever is left reaches this tail — it is where a GROVE spore
            // bloom and a FOUNDRY gatling used to fire the exact same blue box.

            // Replaces two beams that crossed at the tower body: they were fixed to local axes, so
            // they read as a static X unrelated to where the tower was shooting. A burst thrown
            // along the firing direction reads as the weapon actually discharging.
            SpawnEffect(muzzle, shotColor, damage >= 5 ? 0.3f : 0.22f, 0.1f, BurstShape.Muzzle, impact - muzzle);
            SpawnBeam(muzzle, impact, shotColor, style.Duration, style.Width, style.Intensity);

            if (line == TowerLine.Grove)
            {
                // Soft and organic all the way through, including the impact: a bloom opening on
                // the target instead of the hard square SpawnCellFrameCue snaps around its cell.
                SpawnExpandingRing(impact, shotColor, 0.12f, damage >= 5 ? 0.92f : 0.7f, style.Duration);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.42f : 0.3f, style.Duration * 0.6f);
                return;
            }

            SpawnCellFrameCue(hitPosition, shotColor, damage >= 5 ? 0.16f : 0.12f);
            SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.3f : 0.22f, 0.1f);
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
                    // No "REVEAL" word; the reveal VFX on the creep is the tell.
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

        /// <summary>
        /// A jagged, segmented arc between two points. Tesla's Chain Arc.
        /// </summary>
        /// <remarks>
        /// Each hop of the chain arrives as its own CreepDamagedEvent carrying that hop's already
        /// halved damage, so passing the caller's style through unchanged makes the chain's decay
        /// visible without the renderer having to know anything about the chain at all.
        /// </remarks>
        private void SpawnForkedArc(Vector3 start, Vector3 end, Color color, WeaponStyle style, int segments)
        {
            var previous = start;
            var span = end - start;
            // Perpendicular on the ground plane: the kink has to be across the arc, not along it.
            var lateral = Vector3.Cross(span.normalized, Vector3.up).normalized;
            for (var index = 1; index <= segments; index++)
            {
                var t = (float)index / segments;
                var point = start + span * t;
                if (index < segments)
                {
                    // Zero at both ends so the arc still starts at the coil and lands on the target.
                    var envelope = Mathf.Sin(t * Mathf.PI);
                    point += lateral * (UnityEngine.Random.Range(-0.34f, 0.34f) * envelope);
                    point += Vector3.up * (UnityEngine.Random.Range(-0.06f, 0.14f) * envelope);
                }

                SpawnBeam(previous, point, color, style.Duration, style.Width, style.Intensity);
                previous = point;
            }
        }

        /// <summary>
        /// A short bright streak that does not span the whole distance, plus a casing thrown clear.
        /// Gatling's tracer.
        /// </summary>
        /// <remarks>
        /// Deliberately fewer objects than the generic tail it replaces (two beams, two bursts and a
        /// cell frame). Gatling fires every tick, which makes it the one tower here where the effect
        /// could plausibly cost real frame time on a phone.
        /// </remarks>
        private void SpawnTracerShot(Vector3 muzzle, Vector3 target, Color color, WeaponStyle style)
        {
            var direction = (target - muzzle).normalized;
            var distance = Vector3.Distance(muzzle, target);
            // A round in flight, not a rod connecting the barrel to the target: the tracer covers
            // the middle of the gap and leaves both ends open.
            var from = muzzle + direction * (distance * 0.28f);
            var to = muzzle + direction * (distance * 0.78f);
            SpawnBeam(from, to, color, style.Duration * 0.7f, style.Width * 0.8f, style.Intensity * 1.25f);
            SpawnEffect(muzzle, color, 0.26f, 0.07f, BurstShape.Muzzle, direction);

            // Ejected sideways and slightly up, brass rather than muzzle-coloured.
            var eject = Vector3.Cross(direction, Vector3.up).normalized * 0.3f + Vector3.up * 0.12f;
            SpawnBeam(muzzle, muzzle + eject, new Color(0.85f, 0.68f, 0.32f), style.Duration * 0.55f, 0.05f, 0.8f);
        }

        /// <summary>
        /// One heavy short round with a hard muzzle flash behind it. Barricade's slug.
        /// </summary>
        private void SpawnSlugShot(Vector3 muzzle, Vector3 target, Color color, WeaponStyle style)
        {
            var direction = (target - muzzle).normalized;
            SpawnBeam(muzzle, target, color, style.Duration, style.Width * 1.5f, style.Intensity);
            // Recoil reads as a flash driven back past the barrel, opposite the shot.
            SpawnEffect(muzzle - direction * 0.12f, color, 0.4f, 0.11f, BurstShape.Muzzle, -direction);
            SpawnEffect(target, color, 0.36f, 0.12f);
        }

        /// <summary>
        /// Several strands reaching from tower to target, bowed apart. Thorn Snare and Elder Canopy.
        /// </summary>
        private void SpawnVineLash(Vector3 start, Vector3 end, Color color, WeaponStyle style, int strands, float bow)
        {
            const int SegmentsPerStrand = 5;
            var span = end - start;
            var lateral = Vector3.Cross(span.normalized, Vector3.up).normalized;
            for (var strand = 0; strand < strands; strand++)
            {
                // Each strand bows to its own side. Curved along a quadratic through an offset
                // control point rather than bent at a single midpoint: two straight segments meeting
                // at a sharp corner drew a hard geometric diamond, which read as anything but
                // organic — worse than the plain beam it replaced.
                var side = strands == 1 ? 0f : (strand / (float)(strands - 1) - 0.5f) * 2f;
                var control = start + span * 0.5f + lateral * (side * bow) + Vector3.up * 0.1f;
                var previous = start;
                for (var segment = 1; segment <= SegmentsPerStrand; segment++)
                {
                    var t = (float)segment / SegmentsPerStrand;
                    var inverse = 1f - t;
                    var point = inverse * inverse * start + 2f * inverse * t * control + t * t * end;
                    if (segment < SegmentsPerStrand)
                    {
                        // Small irregularity so the strands do not read as drafted curves.
                        point += lateral * (UnityEngine.Random.Range(-0.05f, 0.05f));
                    }

                    // Tapers toward the tip: a tendril, not a cable.
                    var taper = Mathf.Lerp(0.85f, 0.45f, t);
                    SpawnBeam(previous, point, color, style.Duration, style.Width * taper, style.Intensity);
                    previous = point;
                }
            }
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
            // Only the local player's own sends. A banner for an opponent sending into someone
            // else's lane is 12% of all board text and nothing the player can act on.
            if (simulationDriver != null && queued.SenderId.Equals(simulationDriver.LocalPlayerId))
            {
                SpawnFloatingText(senderPosition + Vector3.left * 0.42f, "SEND", color, 0.42f);
            }
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

        /// <summary>Tier of the tower in a cell, defaulting to tier 1 for an unknown cell.</summary>
        private int TowerTierAt(GridPosition position, LaneId laneId) =>
            towerTiersByCell.TryGetValue(TowerGridKey(position, laneId), out var tier) ? tier : 1;

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
            foreach (var ring in activeShockwaveRings) ReleaseToPool(ring.Object, shockwaveRingPool);
            activeShockwaveRings.Clear();

            // Particles are not pooled objects, so they are not covered by any of the releases
            // above and would otherwise drift on across a reset — visible as sparks hanging over an
            // empty board while the next match sets up.
            ClearBurstEmitters();
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
                towerBarrelAngle.Remove(key);
                towerBarrelState.Remove(key);
                towerAimYaw.Remove(key);
                towerSpinPartState.Remove(key);
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
                creepAnimators.Remove(key);
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

        // Real 3D tower prefabs have their own mesh pivot sitting exactly at the model's visual
        // base, but GridToWorld's fixed Y (0.35, shared with every other entity type) sits 0.47
        // units above BoardTopY (-0.12), the board mesh's actual baked floor surface — the same
        // surface each tower's own contact-shadow decal is correctly pinned to (UpdateContactShadow).
        // That gap is invisible from directly overhead, but the tilted match camera projects it into
        // a real, visible vertical disconnect between a tower and its own shadow, reading as
        // "floating"/"off-center". Anchoring real tower prefabs to BoardTopY instead removes the gap
        // without touching GridToWorld itself, which creeps and the (now-unused) primitive tower
        // fallback still rely on. Clearance must clear RangeHalo, the lowest root-level accessory
        // shape at local Y -0.06 (Tower3DImportPipeline.CreateRangeHalo) — those accessories sit
        // below root by design and would otherwise dip beneath BoardTopY and clip into the floor.
        private const float TowerBaseClearance = 0.08f;

        private static void SetTowerTransform(GameObject instance, GridPosition position, LaneId laneId, string towerId, TowerVisualProfile visualProfile)
        {
            var hasRealPrefab = visualProfile != null && visualProfile.Prefab != null;
            var lift = hasRealPrefab ? visualProfile.Lift : TowerRoleLift(towerId);
            var basePosition = GridToWorld(position, laneId);
            var baseY = hasRealPrefab ? BoardTopY + TowerBaseClearance : basePosition.y;
            instance.transform.position = new Vector3(basePosition.x, baseY + lift, basePosition.z);
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

            // Pulse has no clean seam anywhere on its mesh (its 4 spikes run the tower's full
            // height — every split attempt visibly detached their tips, see the Blender renders
            // behind the Pulse ring split), so it has no isolated part that could carry aim
            // rotation without swinging the whole stationary-looking bastion around with it. It
            // stays fixed entirely; only its Ring spins, and only its idle pulse carries the
            // "Pulse" identity — it's a splash/AOE emitter, not a turret that needs to point at a
            // specific target.
            // Was hardcoded to Pulse. Now a per-role trait, because Foundry Core fires upward out
            // of its stacks and Barricade Bastion is a fixed emplacement that fires one direction
            // only — both would contradict their own mechanic if they turned to track a target.
            var locksYaw = TowerMotionProfileFor(visualProfile.Role).LocksYaw;
            var yaw = locksYaw ? 0f : towerAimYaw.TryGetValue(key, out var currentYaw) ? currentYaw : 0f;
            if (!locksYaw && towerAimTarget.TryGetValue(key, out var aimTarget))
            {
                var direction = aimTarget - towerPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    // atan2(x, z) assumes the model's own unrotated mesh faces +Z at yaw 0 — true
                    // for most of these towers, but not universal, and TowerHeadRestHeadingDegrees
                    // corrects for whichever roles it doesn't hold for (measured directly from the
                    // mesh, not assumed).
                    var desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg
                        - TowerHeadRestHeadingDegrees(visualProfile.Role);
                    yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, TowerAimTurnDegreesPerSecond * Time.deltaTime);
                }
            }

            if (!locksYaw)
            {
                towerAimYaw[key] = yaw;
            }

            var timeSinceFired = towerLastFiredAt.TryGetValue(key, out var firedAt) ? Time.time - firedAt : float.MaxValue;
            var recoilDuration = Mathf.Max(0.01f, TowerMotionProfileFor(visualProfile.Role).RecoilDuration);
            var recoil = timeSinceFired < recoilDuration ? 1f - timeSinceFired / recoilDuration : 0f;

            // Recoil is a rigid kick along the tower's current firing axis (position + a small
            // backward pitch), not a squash/stretch scale distortion — these towers are stone and
            // metal, and jelly-deformation on a rigid body reads as wrong regardless of how well
            // it's tuned. Idle keeps a small UNIFORM scale pulse (a "breathing" energy effect,
            // not an axis-skewed squash), which is a different thing from a recoil punch.
            var idleScale = 1f + idle.ScalePulse;
            // Pulse is a stationary splash/AOE emitter, not a mechanical weapon with a kickback —
            // it should show zero recoil-driven position/pitch motion when it fires, only its own
            // VFX flash and ring spin. locksYaw (Pulse-only, see above) doubles as that flag here.
            var recoilProfile = TowerMotionProfileFor(visualProfile.Role);
            var recoilKickScale = recoilProfile.SuppressRecoil ? 0f : recoilProfile.RecoilScale;
            var recoilKick = recoil * 0.16f * recoilKickScale;
            var kickDirection = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            var recoilPosition = kickDirection * recoilKick + Vector3.down * (recoil * 0.04f * recoilKickScale);

            // Turret-style towers (currently just Arrow, split via split_tower_rigid_part.py) have
            // a HeadPivot separate from Base: aim yaw and recoil apply to HeadPivot alone, so only
            // the cannon swivels/kicks while the foundation underneath stays put, like a real
            // turret. HeadPivot is a purpose-built empty with an identity rest transform (see
            // Tower3DImportPipeline — the actual Head mesh is reparented under it), exactly like
            // Body, so it's just as safe to overwrite outright. Towers without one (everything
            // else so far) fall back to turning the whole Body, exactly as before.
            var headPivot = FindDeepChild(body, "HeadPivot");
            if (headPivot != null)
            {
                body.localPosition = idle.PositionOffset;
                body.localRotation = Quaternion.Euler(idle.PitchDegrees, 0f, 0f);
                body.localScale = Vector3.one * idleScale;

                // A HeadPivot kick moves only the isolated barrel relative to a stationary Base —
                // the same 0.16-unit magnitude that read as a subtle whole-model punch before
                // (Base and Head always moved together, so no gap could ever show) instead reads
                // as the barrel flying off its mount, since nothing hides the separation anymore.
                // Scaled down substantially so recoil stays a tight, visibly-connected kick.
                var headRecoilPosition = kickDirection * (recoil * 0.05f * recoilKickScale)
                    + Vector3.down * (recoil * 0.015f * recoilKickScale);
                headPivot.localPosition = headRecoilPosition;
                headPivot.localRotation = Quaternion.Euler(-recoil * 6f * recoilKickScale, yaw, 0f);
            }
            else
            {
                body.localPosition = idle.PositionOffset + recoilPosition;
                body.localRotation = Quaternion.Euler(idle.PitchDegrees - recoil * 10f * recoilKickScale, yaw, 0f);
                body.localScale = Vector3.one * idleScale;
            }

            // Rigid sub-parts (Control's floating ring, Relay's dish, Prism's spire) spin
            // independently of Body's own idle/aim/recoil motion — a continuous spin about the
            // WORLD-vertical axis, not something driven by firing state. Searched by name rather
            // than a fixed path since a spin part sits under whatever depth the imported raw mesh
            // hierarchy happens to nest it at (e.g. Body/Imported3DVisual/LTW_Unity_ExportRoot/Ring),
            // which is an import-pipeline detail this call site shouldn't need to know. Each tower
            // has at most one spin part today, so the first name found wins.
            UpdateBarrelSpin(key, body);

            var spinPart = FindSpinPart(body);
            if (spinPart != null)
            {
                // A plain Quaternion.Euler(0, angle, 0) assumes the part's own local Y axis IS
                // world-up, which isn't guaranteed once an FBX export/import round-trip has done
                // its own Z-up/Y-up axis conversion partway down the hierarchy — it produced an
                // end-over-end tumble instead of a flat Saturn's-rings spin on Control's ring.
                // Instead, convert world-up into whatever axis it actually corresponds to in the
                // part's own rest space (cached once), the same fix pattern used for the Brute
                // rig's degenerate straight-down bone case in rig_quadruped_creep.py.
                if (!towerSpinPartState.TryGetValue(key, out var spinState))
                {
                    var localSpinAxis = spinPart.parent.InverseTransformDirection(Vector3.up).normalized;
                    spinState = new SpinPartState(localSpinAxis, spinPart.localRotation);
                    towerSpinPartState[key] = spinState;
                }

                spinPart.localRotation = Quaternion.AngleAxis(Time.time * TowerRingSpinDegreesPerSecond, spinState.LocalSpinAxis) * spinState.RestLocalRotation;
            }
        }

        private const float TowerRingSpinDegreesPerSecond = 32f;

        private static readonly string[] TowerSpinPartNames = { "Ring", "Dish", "Spire" };

        /// <summary>Barrel spin, in degrees/second, while the gun is actively firing.</summary>
        private const float BarrelFiringSpinDegreesPerSecond = 900f;

        /// <summary>Barrel spin while idle. Not zero — a gatling that stops dead reads as broken.</summary>
        private const float BarrelIdleSpinDegreesPerSecond = 40f;

        /// <summary>How long the barrel takes to coast down from firing speed to idle.</summary>
        private const float BarrelSpindownSeconds = 0.9f;

        private readonly Dictionary<string, float> towerBarrelAngle = new Dictionary<string, float>();
        private readonly Dictionary<string, SpinPartState> towerBarrelState = new Dictionary<string, SpinPartState>();

        /// <summary>
        /// Spins a gatling-style barrel about its own long axis, faster while it is firing.
        /// </summary>
        /// <remarks>
        /// Deliberately NOT folded into the Ring/Dish/Spire spin above, for two reasons. That spin is
        /// about world UP, which is right for a horizontal ring and meaningless for a barrel; and it
        /// is a constant rate, whereas the whole read of a gatling is that it winds up when it starts
        /// working and coasts down when it stops.
        ///
        /// The axis is taken from the barrel's own mesh bounds — its longest extent IS the bore — and
        /// cached in the barrel's LOCAL space. Local matters: the barrel hangs under HeadPivot, which
        /// yaws to aim, so a world-space axis would only be correct at the rotation it happened to be
        /// sampled at. Deriving it from geometry also means it cannot drift out of step with the
        /// measured rest heading the way a second hand-entered constant would.
        ///
        /// The angle is ACCUMULATED rather than computed from Time.time * rate, so that changing the
        /// rate speeds the barrel up instead of teleporting it to a new phase.
        /// </remarks>
        private void UpdateBarrelSpin(string key, Transform body)
        {
            var barrel = FindDeepChild(body, "Barrel");
            if (barrel == null)
            {
                return;
            }

            if (!towerBarrelState.TryGetValue(key, out var state))
            {
                state = new SpinPartState(LongestLocalAxis(barrel), barrel.localRotation);
                towerBarrelState[key] = state;
            }

            var sinceFired = towerLastFiredAt.TryGetValue(key, out var firedAt) ? Time.time - firedAt : float.MaxValue;
            var firing = Mathf.Clamp01(1f - sinceFired / BarrelSpindownSeconds);
            var rate = Mathf.Lerp(BarrelIdleSpinDegreesPerSecond, BarrelFiringSpinDegreesPerSecond, firing);

            var angle = (towerBarrelAngle.TryGetValue(key, out var previous) ? previous : 0f) + rate * Time.deltaTime;
            if (angle > 360f)
            {
                angle -= 360f;
            }

            towerBarrelAngle[key] = angle;
            barrel.localRotation = Quaternion.AngleAxis(angle, state.LocalSpinAxis) * state.RestLocalRotation;
        }

        /// <summary>The part's longest mesh-bounds extent, as a direction in its own local space.</summary>
        private static Vector3 LongestLocalAxis(Transform part)
        {
            var filter = part.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return Vector3.forward;
            }

            var extents = filter.sharedMesh.bounds.extents;
            var meshAxis = extents.x >= extents.y && extents.x >= extents.z
                ? Vector3.right
                : extents.y >= extents.z ? Vector3.up : Vector3.forward;
            var world = filter.transform.TransformDirection(meshAxis);
            var local = part.InverseTransformDirection(world);
            return local.sqrMagnitude < 1e-6f ? Vector3.forward : local.normalized;
        }

        private static Transform FindSpinPart(Transform body)
        {
            for (var index = 0; index < TowerSpinPartNames.Length; index++)
            {
                var found = FindDeepChild(body, TowerSpinPartNames[index]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private readonly struct SpinPartState
        {
            public SpinPartState(Vector3 localSpinAxis, Quaternion restLocalRotation)
            {
                LocalSpinAxis = localSpinAxis;
                RestLocalRotation = restLocalRotation;
            }

            public Vector3 LocalSpinAxis { get; }
            public Quaternion RestLocalRotation { get; }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the tower's actual mesh-bearing Body transform, NOT
        /// <see cref="TowerVisualProfile.BodyRendererPath"/> — that field is "BodyTintAnchor" for
        /// every tower profile (see TowerVisualLibrary.asset), a separate mesh-less empty used only
        /// for the SetProfileColor tint lookup. It is a sibling of Body, not an alias for it (see
        /// Tower3DImportPipeline.GenerateWrapperIfRawExists, which creates both as distinct children
        /// of root). Applying motion to BodyTintAnchor silently animates nothing, since it has no
        /// renderer anywhere under it — this was the actual reason tower idle/aim/recoil motion
        /// (and now ring-spin) never appeared on screen.
        /// </summary>
        private static Transform ResolveTowerMotionTarget(GameObject towerObject, TowerVisualProfile visualProfile)
        {
            return towerObject.transform.Find("Body") ?? towerObject.transform;
        }

        /// <summary>
        /// The tower's Body transform, if the tower is currently active, else null. Attack VFX uses
        /// this to place beams/effects relative to the tower's actual current animated state
        /// (idle drift, aim rotation, recoil) instead of a fixed world-space offset from the grid
        /// position — a tower that has turned to face its target was firing its beam from where it
        /// used to point, not where it currently does, before this was threaded through.
        /// </summary>
        private Transform ResolveTowerBodyTransform(string towerKey)
        {
            if (!activeTowers.TryGetValue(towerKey, out var towerObject) || towerObject == null)
            {
                return null;
            }

            var body = towerObject.transform.Find("Body");
            if (body == null)
            {
                return towerObject.transform;
            }

            // Turret-style towers carry aim/recoil on a HeadPivot instead of Body (see
            // UpdateTowerMotion) — attack VFX must follow whichever transform actually turns, or a
            // muzzle flash fires from where the cannon used to point before it swiveled. Towers
            // without one keep using Body, unchanged.
            var headPivot = FindDeepChild(body, "HeadPivot");
            return headPivot != null ? headPivot : body;
        }

        /// <summary>
        /// Reference creep speed, in cells per tick, that a walk clip is treated as authored for.
        /// A creep at this speed plays its clip at 1.0x.
        /// </summary>
        /// <remarks>
        /// 1 because five of the eight rigged creeps ship at speed 1 (Brute, Obsidian Brute,
        /// Fracture Burrower, Aegis Warden, Siege Colossus) — normalising on the majority means the
        /// common case keeps exactly the playback its rig was tuned against.
        /// </remarks>
        private const int CreepWalkReferenceSpeed = 1;

        /// <summary>
        /// How much of the speed difference the playback rate actually takes up, as an exponent.
        /// </summary>
        /// <remarks>
        /// 0.5, not 1.0, and the under-correction is deliberate. Matching stride to ground speed
        /// exactly would want a linear exponent, but two things make linear the wrong target here:
        ///
        /// - Full correction is unreachable anyway. SpeedPerSecond is cells per TICK at 4 ticks/sec,
        ///   so a speed-2 creep crosses 8 world units/sec on a board where a cell is one unit. The
        ///   Spire Turret Walker's rig was measured at 8.3x residual foot skate even after its stride
        ///   and leg cycle were tuned specifically for this (docs/GD_TUNING_LOG.md, 2026-07-28), and
        ///   that entry also records that pushing the leg cycle further "starts to read as a blur at
        ///   24 footfalls/sec, which trades one artefact for another". Playing a clip at 8x would be
        ///   squarely in that territory.
        /// - The fast creeps are already partly compensated. The five Meshy-rigged bipeds got their
        ///   clip chosen BY speed when they landed — running for the fast ones, walking for the slow
        ///   ones (commit a88dae1) — precisely because a run cycle's longer stride and faster cadence
        ///   cut skate. Layering full linear scaling on top would double-count that.
        ///
        /// So the goal is not to eliminate skate, which the board's scale forbids. It is to stop the
        /// roster sharing ONE playback rate across a 3x speed spread, so a Siege Colossus lumbers and
        /// a Zephyr Wraith scurries instead of both cycling their limbs identically. Yields 1.00x /
        /// 1.41x / 1.73x at speeds 1 / 2 / 3.
        ///
        /// These are reasoned values, not observed ones — nobody has watched them yet. Tracked as its
        /// own visual-tuning item on the gameplay checklist.
        /// </remarks>
        private const float CreepWalkSpeedExponent = 0.5f;

        /// <summary>Playback clamp, so a future speed value cannot drive the clip to a blur or a stall.</summary>
        private const float CreepWalkPlaybackMin = 0.6f;
        private const float CreepWalkPlaybackMax = 2.2f;

        /// <summary>
        /// Matches a rigged creep's clip playback to how fast it actually crosses the board.
        /// </summary>
        /// <remarks>
        /// Without this every rigged creep played its clip at the authored rate no matter how fast it
        /// travelled, which is the runtime half of the foot-skate finding in GD_TUNING_LOG's
        /// 2026-07-28 entry: "Nothing syncs animator playback to movement speed; the clip loops at its
        /// authored rate while the simulation translates the creep independently." That entry attacked
        /// the problem from inside the rig, which was the only lever a Blender script has. This is the
        /// lever it could not reach.
        ///
        /// Speed comes from the presentation snapshot rather than a client-side table keyed on the
        /// creep id — the same correction item 12 made for max health, and for the same reason.
        /// </remarks>
        private void UpdateCreepAnimationSpeed(string key, GameObject instance, string creepId, int speedPerSecond)
        {
            if (!IsRiggedCreep(instance, creepId))
            {
                return;
            }

            if (!creepAnimators.TryGetValue(key, out var animator) || animator == null)
            {
                // Cached per active creep: GetComponentInChildren walks the hierarchy, which is far
                // too expensive to repeat every frame per creep (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items already flags
                // per-frame work in this Update path).
                animator = instance.GetComponentInChildren<Animator>(true);
                creepAnimators[key] = animator;
            }

            if (animator == null)
            {
                return;
            }

            var ratio = Mathf.Max(1, speedPerSecond) / (float)CreepWalkReferenceSpeed;
            animator.speed = Mathf.Clamp(
                Mathf.Pow(ratio, CreepWalkSpeedExponent),
                CreepWalkPlaybackMin,
                CreepWalkPlaybackMax);
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

        /// <summary>
        /// How far a creep may move in one update and still be interpolated rather than snapped.
        /// Anything beyond this is treated as a teleport (a lane transfer) rather than travel.
        /// </summary>
        /// <remarks>
        /// This has to sit above the fastest creep's per-tick travel and below <see cref="LaneSpacing"/>.
        /// CombatService.MoveCreeps advances a creep by SpeedPerSecond WHOLE CELLS per tick, and one
        /// cell is one world unit, so a creep's per-tick travel in world units equals its speed
        /// value. The old 2.5 hardcode predated any creep faster than 2: Crystal Wisp ships at
        /// speed 3, cleared the threshold on every single tick, and so snapped continuously instead
        /// of ever interpolating — reading as teleporting across the lane. Half of LaneSpacing
        /// leaves headroom for future speed tuning while still snapping a lane transfer, which
        /// moves a creep a full LaneSpacing sideways and genuinely is a teleport.
        /// CreepSpeedTests guards the lower bound so a future speed bump fails a test rather than
        /// silently reintroducing the teleport.
        /// </remarks>
        private const float CreepTeleportSnapDistance = LaneSpacing * 0.5f;

        /// <summary>
        /// Where a creep actually sits between its current cell and the one it is walking toward.
        /// </summary>
        /// <remarks>
        /// The simulation moves a creep in whole cells: it banks MovementProgress each tick and steps
        /// exactly one cell when that reaches MovementCost. At the original pace that was one cell per
        /// tick, so "current cell" was never more than 0.25s stale and the lerp below hid it. Slowing
        /// creeps to a third of that (CombatService.BaseMovementCost) makes it up to 0.75s stale, at
        /// which point a creep visibly holds still and then hops.
        ///
        /// So the cell pair and the progress between them are read from the snapshot and resolved
        /// here, on the presentation side of the boundary — the simulation stays all-integer and the
        /// float division happens in the only layer that wants a float. Clamped because a braked
        /// creep's real cost is double what MovementCost reports (see the snapshot's own remark), so
        /// its progress legitimately runs past one cell's worth.
        /// </remarks>
        private static Vector3 CreepTravelPosition(CreepPresentationSnapshot creep)
        {
            var from = GridToWorld(creep.Position, creep.LaneId);
            if (creep.MovementCost <= 0 || creep.NextPosition.Equals(creep.Position))
            {
                return from;
            }

            var fraction = Mathf.Clamp01(creep.MovementProgress / (float)creep.MovementCost);
            return Vector3.Lerp(from, GridToWorld(creep.NextPosition, creep.LaneId), fraction);
        }

        private static void SetCreepTransform(GameObject instance, Vector3 lanePosition, LaneId laneId, string creepId, CreepVisualProfile visualProfile, bool snapToTarget, float hitFlashUntil, string key)
        {
            var roleMotion = CreepRoleMotion(creepId, visualProfile, hitFlashUntil, IsRiggedCreep(instance, creepId), key);
            var targetPosition = lanePosition + CreepRoleOffset(creepId) + roleMotion.PositionOffset;
            instance.transform.position = snapToTarget || Vector3.Distance(instance.transform.position, targetPosition) > CreepTeleportSnapDistance
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
            SetProfileColors(creepObject, visualProfile.SenderAccentRendererPaths, AccentPoolColor(senderColor));
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
                if (IsHealthBarPart(rendererObject.name) || !rendererObject.activeInHierarchy)
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
                // Both ends get their gate plate. An earlier pass removed the spawn end entirely
                // while chasing a panel that appeared behind the HUD scoreboard, which took the
                // spawn gate art with it and left that end of the lane bare. What actually floated
                // was the companion detail and pulse band stacked above the plate: the spawn end
                // sits at the far end of the lane (z=15), so anything lifted off the surface there
                // projects up past the board's top edge under the tilted camera. Those two
                // builders stay gone; the plate itself lies flat on the board and is fine.
                CreateEndpointSpritePlate(laneId, label, center, isSpawn, isPlayerLane);
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
            plate.transform.position = center + new Vector3(0f, isSpawn ? 0.06f : 0.18f, isSpawn ? 0.02f : -0.1f);
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
            // Spire Turret Walker walks the direct route, which runs straight through cells that
            // towers are standing on — so without real clearance it renders INSIDE them. 0.72 is
            // chosen against tower height rather than as a bigger version of the 0.32 used for
            // flying creeps: at 0.32 it still clipped the taller towers, which reads as a bug
            // rather than as the one unit that goes over the maze.
            if (ContainsRole(creepId, "turret_walker"))
            {
                return Vector3.up * 0.72f;
            }

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
        /// Per-role idle motion for a tower's Body child. Yaw is deliberately left untouched here
        /// (stays 0): aim rotation in <see cref="UpdateTowerMotion"/> owns yaw exclusively (applied
        /// to the split Head for turret-style towers, to Body itself otherwise), so idle and aim
        /// never fight over the same axis.
        /// </summary>
        /// <summary>
        /// The match camera is orthographic and tilted (ConfigureDefaultCamera, default 30 degrees
        /// off vertical), so Y-axis position and X-axis pitch reach the screen at only sin(tilt) of
        /// their true magnitude — real, but still partial. Idle motion leans on XZ position and
        /// uniform scale, which lose nothing to that projection, and layers pitch/position on top
        /// rather than relying on them alone.
        /// </summary>
        /// <summary>
        /// How far a tower's mesh, at yaw 0 (HeadPivot/Body's identity rest rotation), actually
        /// faces from world +Z, in the same atan2(x, z) convention the aim-yaw math above uses —
        /// measured directly IN UNITY (a Blender-side measurement of the same mesh gave the wrong
        /// sign, since Blender's FBX exporter mirrors X during its right-handed-to-left-handed
        /// conversion), not asserted. Without this correction the turret still tracks (relative
        /// motion as a target moves is correct either way), just aimed a constant angle away from
        /// the actual target — invisible while the whole Body carried the rotation, obvious once
        /// Arrow's barrel became an isolated, independently-aimed Head. Other roles default to 0
        /// pending the same per-mesh measurement; none have shown the same symptom yet, but none
        /// have an isolated barrel-like part to reveal it either.
        /// </summary>
        /// <summary>
        /// Everything the presentation layer needs to know about a tower role, in one row.
        /// </summary>
        /// <remarks>
        /// This replaces three separate role-keyed switches (rest heading, idle motion, yaw lock).
        /// With five towers that was tolerable; at fifteen it meant three places to edit per tower
        /// and three places to forget. Adding a tower is now one row.
        ///
        /// BreatheAmp is a uniform scale pulse, not a deformation — these are hard-surface
        /// structures and should not squash. DriftAmp is a lateral sway in local units. Sharpness
        /// above 1 makes the pulse peaked rather than sinusoidal, which is what reads as a
        /// heartbeat instead of a sway.
        /// </remarks>
        private readonly struct TowerMotionProfile
        {
            public TowerMotionProfile(
                float breatheHz,
                float breatheAmp,
                float driftHz = 0f,
                float driftAmp = 0f,
                float sharpness = 1f,
                bool locksYaw = false,
                float restHeadingDegrees = 0f,
                bool? suppressRecoil = null,
                float recoilScale = 1f,
                float recoilDuration = TowerRecoilDuration)
            {
                RecoilDuration = recoilDuration;
                SuppressRecoil = suppressRecoil ?? locksYaw;
                BreatheHz = breatheHz;
                BreatheAmp = breatheAmp;
                DriftHz = driftHz;
                DriftAmp = driftAmp;
                Sharpness = sharpness;
                LocksYaw = locksYaw;
                RestHeadingDegrees = restHeadingDegrees;
                RecoilScale = recoilScale;
            }

            public float BreatheHz { get; }
            public float BreatheAmp { get; }
            public float DriftHz { get; }
            public float DriftAmp { get; }
            public float Sharpness { get; }

            /// <summary>Tower never rotates to face its target.</summary>
            public bool LocksYaw { get; }

            /// <summary>
            /// Tower shows no recoil kick when it fires. Defaults to <see cref="LocksYaw"/>, which is
            /// how this behaved when the two were the same flag.
            /// </summary>
            /// <remarks>
            /// They had to come apart for the Barricade Bastion. It locks yaw because it never turns,
            /// but a fixed emplacement's whole read is the kick straight back along its one axis, and
            /// tying the two together left it firing with no reaction at all.
            /// </remarks>
            public bool SuppressRecoil { get; }

            /// <summary>
            /// Multiplier on the firing kick, where 1 is the original uniform magnitude.
            /// </summary>
            /// <remarks>
            /// Recoil used to be one hardcoded magnitude shared by every tower that showed any, so a
            /// 10-gold Sapling Sentinel kicked exactly as hard as a 52-gold Foundry Core lobbing a
            /// mortar shell. Weight is most of what separates these towers visually, and firing was
            /// the one moment that said nothing about it.
            ///
            /// Scales the whole kick — backward travel, downward drop and pitch — so a value stays a
            /// statement about the tower's weight rather than about one axis.
            /// </remarks>
            public float RecoilScale { get; }

            /// <summary>
            /// How long one kick takes to decay. MUST stay under the tower's own firing interval.
            /// </summary>
            /// <remarks>
            /// The shared 0.35s default silently breaks for anything fast. The Gatling Turret's
            /// cooldown is 1 tick — 0.25s at 4 ticks/second — so each kick was re-triggered before
            /// the previous one had decayed, and the gun sat pinned near full recoil instead of
            /// pulsing. That reads as a tower shaking itself apart rather than as rate of fire, and
            /// reducing the MAGNITUDE cannot fix it: the problem is that the animation never
            /// finishes. Fast guns need a short kick, not only a small one.
            /// </remarks>
            public float RecoilDuration { get; }

            /// <summary>
            /// Heading the head's mesh already points at in its rest pose, subtracted from the aim
            /// heading. Must be MEASURED IN UNITY, not Blender: Blender's FBX export mirrors X
            /// during the right-handed to left-handed conversion, which silently flips the sign.
            /// </summary>
            public float RestHeadingDegrees { get; }
        }

        private static TowerMotionProfile TowerMotionProfileFor(TowerVisualRole role)
        {
            switch (role)
            {
                // Aim and recoil live on the split Head turret (UpdateTowerMotion), so Body only
                // needs a faint idle presence — an alert, mostly-still gun emplacement.
                // Rest heading measured in Unity: barrel tip at local (x=0.725, z=0.001) = +90.
                case TowerVisualRole.Arrow:
                    return new TowerMotionProfile(1.4f, 0.015f, restHeadingDegrees: 90f, recoilScale: 1.2f);

                // The arms+core+ring assembly turns to aim and the ring spins independently, both
                // real visible motion, so Body-level sway on top was pure excess. Reads as a
                // mostly-still ancient structure with a faint pulse of life.
                case TowerVisualRole.Control:
                    return new TowerMotionProfile(1.6f, 0.01f, recoilScale: 0.5f);

                // The split Dish spins continuously; Body adds a slow mast sway underneath rather
                // than competing with the dish for attention.
                case TowerVisualRole.Relay:
                    return new TowerMotionProfile(1.1f, 0.02f, driftHz: 0.6f, driftAmp: 0.025f, recoilScale: 0.4f);

                // No cleanly separable emitter part on this mesh, so the name is carried by a
                // heartbeat-shaped pulse on the whole Body: peaked, not sinusoidal. Yaw locked —
                // a dome has no facing, and rotating it read as the whole tower spinning.
                // Amplitude raised from 0.022 after measuring it on screen: the throb that names
                // this tower moved its silhouette 0.62px peak-to-peak at 1080x1920, which is not a
                // pulse anyone can see. Sharpness 3 keeps the shape — most of the cycle sits near
                // rest and it spikes — so a larger amplitude reads as a harder beat rather than as a
                // wobble. Still the weakest px-per-amplitude in the roster: a low flat dome changes
                // very little on screen when it scales, which is why it needs the most: 0.09 only
                // reached 2.55px, so this is the measured amount rather than a guessed one.
                case TowerVisualRole.Pulse:
                    return new TowerMotionProfile(1.1f, 0.112f, sharpness: 3f, locksYaw: true);

                // The split Spire spins continuously; Body adds a faint glow-breathe underneath.
                case TowerVisualRole.Prism:
                    return new TowerMotionProfile(1.8f, 0.025f, recoilScale: 0.8f);

                // --- Foundry line -------------------------------------------------------------
                // Machines: tight, fast, mechanical. Small amplitudes, no lazy drift.
                // Light recoil because it fires every other tick — a full-weight kick repeated that
                // often stops reading as a reaction and turns into a permanent shake.
                // The gun is now split from its pedestal (Head/Base, split_tower_rigid_part.py at
                // z=0.556 where the barrel housing's radius jumps clear of the dome), so aim and
                // recoil drive HeadPivot alone and the base stays planted — previously the whole
                // tower swung and kicked as one piece.
                //
                // Rest heading measured IN UNITY off the generated prefab (98.7 degrees), never in
                // Blender: the FBX export mirrors X, which flips the sign.
                //
                // The kick is small AND short. Short is the load-bearing half: this fires every
                // 0.25s, so anything at the 0.35s default never returns to rest between shots.
                case TowerVisualRole.Gatling:
                    return new TowerMotionProfile(2.4f, 0.012f, restHeadingDegrees: 98.7f, recoilScale: 0.3f, recoilDuration: 0.12f);

                // A coil under load. Fast shallow pulse reads as electrical rather than breathing.
                // The lightest kick of any tower that has one: an arc discharge has no projectile
                // mass behind it, so what little movement there is comes from the coil, not a barrel.
                // Yaw locked: a tiered masonry pagoda cannot swivel on its foundations, and Chain Arc
                // leaps between creeps rather than firing along a line, so it has nothing to point.
                // Amplitude raised from 0.014, which measured 0.89px on screen — invisible. The
                // fast rate is what makes it read as electrical rather than as breathing, so the
                // rate is untouched and only the depth changes.
                case TowerVisualRole.Tesla:
                    return new TowerMotionProfile(3.2f, 0.05f, sharpness: 2f, locksYaw: true, suppressRecoil: false, recoilScale: 0.35f);

                // A furnace. Slow heavy peaked pulse, like a bellows. Yaw locked: it fires upward
                // out of its stacks, so it has no facing to turn toward a target.
                //
                // Recoil explicitly un-suppressed, and the heaviest in the game. It had none at all
                // before, purely because SuppressRecoil defaults to LocksYaw and this tower locks yaw
                // — the same conflation the Barricade Bastion already had to be rescued from. Having
                // no facing is a reason not to TURN; it is not a reason to lob the heaviest shell on
                // the board (14 damage on a 6-tick cooldown) with no reaction whatsoever.
                // Amplitude raised from 0.02, measured at 1.18px — a bellows nobody could see
                // working. The slow rate and the peaked shape are the bellows; only the depth moves.
                case TowerVisualRole.Foundry:
                    return new TowerMotionProfile(0.8f, 0.054f, sharpness: 2.5f, locksYaw: true, suppressRecoil: false, recoilScale: 1.8f);

                // Yaw locked and nearly inert by design — a fixed emplacement that fires along one
                // direction only. Any turn or sway would contradict the mechanic. The kick is
                // oversized to match: with the idle almost dead, firing is the only motion it has,
                // so it has to carry the whole read on its own.
                case TowerVisualRole.Barricade:
                    return new TowerMotionProfile(0.7f, 0.006f, locksYaw: true, suppressRecoil: false, recoilScale: 1.5f);

                // A bolted-down spire, not an aircraft. It previously carried the widest drift in the
                // roster (0.04) to read as "hovering rather than planted" — but the mesh is a pillar
                // on a plinth with a dish on top, so drifting it sideways read as the whole structure
                // sliding around inside its cell rather than as flight. Drift removed and the pulse
                // cut to a faint idle, near Barricade's deliberately-inert level. The thing that
                // should look airborne is the servicing tether it projects, not the building.
                // Yaw locked: a pillar bolted to a plinth cannot rotate, and what it actually projects is
                // a servicing tether to a neighbour, not a shot at a creep.
                case TowerVisualRole.RepairDrone:
                    return new TowerMotionProfile(1.2f, 0.008f, locksYaw: true, suppressRecoil: false, recoilScale: 0.5f);

                // --- Grove line ---------------------------------------------------------------
                // Living things: slower and larger than the machines, with real sway.
                // A huge canopy. Slow, wide sway — the only tower whose drift is meant to read
                // from across the board.
                // Yaw locked: a rooted tree does not pivot to face anything. The widest sway in the roster
                // now carries it alone instead of competing with a rotation.
                case TowerVisualRole.ElderCanopy:
                    return new TowerMotionProfile(0.6f, 0.03f, driftHz: 0.4f, driftAmp: 0.055f, locksYaw: true, suppressRecoil: false, recoilScale: 0.9f);

                // Small and eager. Quicker and springier than its elders.
                // Yaw locked, same reason as its elder. The quick springy sway is the whole read.
                case TowerVisualRole.Sapling:
                    return new TowerMotionProfile(2.0f, 0.028f, driftHz: 1.2f, driftAmp: 0.03f, locksYaw: true, suppressRecoil: false, recoilScale: 0.5f);

                // A flower. Slow open-and-close bloom, peaked so it reads as breathing.
                // Yaw locked: a flower on a stalk. The peaked open-and-close pulse already names the tower.
                // Amplitude raised from 0.035, measured at 1.66px. This is the tower whose NAME is
                // the motion, and after the yaw lock the bloom is the only thing it does; at under
                // two pixels it did not do it.
                case TowerVisualRole.Bloomheart:
                    return new TowerMotionProfile(0.9f, 0.068f, sharpness: 2f, driftHz: 0.5f, driftAmp: 0.02f, locksYaw: true, suppressRecoil: false, recoilScale: 0.6f);

                // Coiled and tense. Very little motion until it strikes, so almost static — which is
                // exactly why the strike itself is one of the hardest kicks here. A snare whose whole
                // character is stored tension needs the release to land.
                // Yaw locked: a snare waits. One that turns to watch a creep approach is not a trap.
                // suppressRecoil is explicitly false so locking yaw does not also remove the snap —
                // stillness THEN a hard snap is the entire characterisation.
                case TowerVisualRole.ThornSnare:
                    return new TowerMotionProfile(0.5f, 0.008f, locksYaw: true, suppressRecoil: false, recoilScale: 1.4f);

                // A fungal bloom venting spores. Slow swell with a lazy drift.
                // Yaw locked: a cloud has no facing, which its own name says.
                case TowerVisualRole.SporeCloud:
                    return new TowerMotionProfile(0.7f, 0.032f, sharpness: 1.6f, driftHz: 0.35f, driftAmp: 0.035f, locksYaw: true, suppressRecoil: false, recoilScale: 0.4f);

                default:
                    return new TowerMotionProfile(1.3f, 0.02f);
            }
        }

        private static float TowerHeadRestHeadingDegrees(TowerVisualRole role) =>
            TowerMotionProfileFor(role).RestHeadingDegrees;

        private static TowerMotion TowerRoleMotion(TowerVisualRole role)
        {
            var profile = TowerMotionProfileFor(role);
            var time = Time.time;

            var wave = Mathf.Sin(time * profile.BreatheHz);
            var breathe = profile.Sharpness > 1f
                ? Mathf.Pow(Mathf.Abs(wave), profile.Sharpness) * profile.BreatheAmp
                : wave * profile.BreatheAmp;

            var drift = Vector3.zero;
            if (profile.DriftAmp > 0f)
            {
                drift = new Vector3(
                    Mathf.Sin(time * profile.DriftHz) * profile.DriftAmp,
                    0f,
                    Mathf.Cos(time * profile.DriftHz * 0.85f) * profile.DriftAmp * 0.8f);
            }

            return new TowerMotion(drift, 0f, breathe);
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

        /// <summary>
        /// Everything that makes one creep move like itself and not like its neighbour, in one row.
        /// </summary>
        /// <remarks>
        /// This replaces a dispatch on <see cref="CreepVisualMotionStyle"/>, which was a set of
        /// SHARED curves: Shade and Ash Revenant executed byte-identical code, and five rigged
        /// creeps all named HeavyBob. Sharing a curve is fine when a style is a family of two, and
        /// stops being fine at fifteen creeps that are supposed to be individually recognisable.
        /// Each creep now owns a row, exactly as each tower owns one in TowerMotionProfileFor.
        ///
        /// The style enum survives as the FALLBACK for a creep id with no row (see
        /// <see cref="CreepMotionProfileForStyle"/>), so authoring a new creep in the visual library
        /// without touching this file still yields sensible motion rather than nothing.
        ///
        /// What reaches the screen: the match camera is orthographic and tilted ~30 degrees off
        /// vertical, so yaw, XZ drift and uniform scale arrive intact while vertical bob arrives at
        /// roughly sin(tilt). Bob and roll are therefore flavour; sway, spin, drift and pulse carry
        /// the read.
        /// </remarks>
        private readonly struct CreepMotionProfile
        {
            public CreepMotionProfile(
                float bobHz = 0f,
                float bobAmp = 0f,
                bool bobRectified = false,
                float swayHz = 0f,
                float swayDegrees = 0f,
                float spinDegreesPerSecond = 0f,
                float driftHz = 0f,
                float driftAmp = 0f,
                float pulseHz = 0f,
                float pulseAmp = 0f,
                float rollDegrees = 0f,
                float pitchDegrees = 0f,
                float forwardOffset = 0f,
                float flinchScale = 0.28f,
                float flinchTilt = 0f)
            {
                BobHz = bobHz;
                BobAmp = bobAmp;
                BobRectified = bobRectified;
                SwayHz = swayHz;
                SwayDegrees = swayDegrees;
                SpinDegreesPerSecond = spinDegreesPerSecond;
                DriftHz = driftHz;
                DriftAmp = driftAmp;
                PulseHz = pulseHz;
                PulseAmp = pulseAmp;
                RollDegrees = rollDegrees;
                PitchDegrees = pitchDegrees;
                ForwardOffset = forwardOffset;
                FlinchScale = flinchScale;
                FlinchTilt = flinchTilt;
            }

            public float BobHz { get; }
            public float BobAmp { get; }

            /// <summary>Use |sin| so the body settles and rises rather than oscillating evenly, which reads as weight bearing rather than floating.</summary>
            public bool BobRectified { get; }

            public float SwayHz { get; }

            /// <summary>Yaw weave AROUND the direction of travel. Distinct from a spin, which never settles.</summary>
            public float SwayDegrees { get; }

            public float SpinDegreesPerSecond { get; }
            public float DriftHz { get; }
            public float DriftAmp { get; }
            public float PulseHz { get; }

            /// <summary>Uniform scale breathe. Never an axis squash — several of these are crystal or stone.</summary>
            public float PulseAmp { get; }

            public float RollDegrees { get; }
            public float PitchDegrees { get; }
            public float ForwardOffset { get; }

            /// <summary>
            /// Scale punch on taking a hit. Inversely tracks weight across the roster — Crystal Wisp
            /// 0.42, Siege Colossus 0.15 — so how hard something rocks when struck says what it
            /// weighs, before its health bar is read.
            /// </summary>
            public float FlinchScale { get; }

            public float FlinchTilt { get; }
        }

        /// <summary>
        /// The per-creep motion row. One per creep in the roster; unknown ids fall through to the
        /// style-derived profile.
        /// </summary>
        private static CreepMotionProfile CreepMotionProfileFor(string creepId, CreepVisualProfile visualProfile, bool isRigged)
        {
            switch (creepId)
            {
                // --- CORE ---------------------------------------------------------------------
                // Light and quick. The fast lateral dart is the whole silhouette read; the forward
                // offset leans it into its own travel.
                case "creep.runner":
                    return new CreepMotionProfile(
                        bobHz: 13f, bobAmp: 0.018f, driftHz: 13f, driftAmp: 0.055f,
                        rollDegrees: 4f, pitchDegrees: 7f, forwardOffset: 0.06f, flinchScale: 0.34f);

                // Rock golem, rigged. Secondary only: a slow load-bearing breathe under the walk.
                case "creep.brute":
                    return new CreepMotionProfile(swayHz: 1.1f, swayDegrees: 2.5f, pulseHz: 1.4f, pulseAmp: 0.020f, flinchScale: 0.24f);

                // A cluster, not a body. Fast erratic jitter plus a continuous turn so the shards
                // never present the same face twice.
                case "creep.swarm":
                    return new CreepMotionProfile(
                        driftHz: 15f, driftAmp: 0.045f, spinDegreesPerSecond: 60f,
                        pulseHz: 9f, pulseAmp: 0.030f, flinchScale: 0.38f);

                // Cloaked drifter. Weaves around its facing rather than pirouetting, and the slow
                // scale pulse does the ghostly fade.
                case "creep.shade":
                    return new CreepMotionProfile(
                        bobHz: 1.9f, bobAmp: 0.050f, swayHz: 1.5f, swayDegrees: 14f,
                        driftHz: 1.1f, driftAmp: 0.060f, pulseHz: 2.3f, pulseAmp: 0.045f, flinchScale: 0.30f);

                // Beast hybrid. A slow shoulder roll winding up under its own mass.
                case "creep.siege":
                    return new CreepMotionProfile(
                        bobHz: 6f, bobAmp: 0.020f, bobRectified: true, rollDegrees: 4f,
                        pulseHz: 1.6f, pulseAmp: 0.030f, flinchScale: 0.22f);

                // --- RAPID --------------------------------------------------------------------
                // Crystal suspended in a cage. Slow turn so the facets read from above; the wide
                // drift is what sells floating rather than hovering in place.
                case "creep.wisp":
                    return new CreepMotionProfile(
                        bobHz: 2.6f, bobAmp: 0.075f, spinDegreesPerSecond: 52f,
                        driftHz: 0.9f, driftAmp: 0.070f, pulseHz: 3.4f, pulseAmp: 0.035f, flinchScale: 0.42f);

                // Ash Revenant. Deliberately NOT Shade's curve, which it used to share outright:
                // where Shade is a slow wide weave, this is quicker, tighter and more agitated, with
                // a strong fade pulse and a slight unresolved turn — ash coming apart and reforming
                // rather than a hood gliding.
                case "creep.revenant":
                    return new CreepMotionProfile(
                        bobHz: 3.1f, bobAmp: 0.070f, swayHz: 2.4f, swayDegrees: 9f,
                        spinDegreesPerSecond: 18f, driftHz: 1.7f, driftAmp: 0.040f,
                        pulseHz: 4.2f, pulseAmp: 0.070f, flinchScale: 0.44f);

                // Bigger, slower golem. Reads as Brute's heavier cousin: lower frequency, wider sway.
                case "creep.obsidian_brute":
                    return new CreepMotionProfile(swayHz: 0.7f, swayDegrees: 3.2f, pulseHz: 0.9f, pulseAmp: 0.025f, flinchScale: 0.20f);

                // No limbs, so the whole body carries the writhe: a coil turn plus a tightening and
                // loosening pulse. Rectified bob so it settles and rises, reading muscular.
                case "creep.serpent":
                    return new CreepMotionProfile(
                        bobHz: 2.2f, bobAmp: 0.035f, bobRectified: true, spinDegreesPerSecond: 34f,
                        driftHz: 1.7f, driftAmp: 0.050f, pulseHz: 2.2f, pulseAmp: 0.055f, flinchScale: 0.26f);

                // Machine, rigged, and its rig already scans its turret. Almost nothing added: a
                // fast shallow pulse that reads as a servo holding load, and no sway at all, because
                // a mechanical walker leaning would fight the gait it was rigged with.
                case "creep.turret_walker":
                    return new CreepMotionProfile(pulseHz: 2.8f, pulseAmp: 0.010f, flinchScale: 0.26f);

                // --- ELITE (all rigged; secondary layer only) ---------------------------------
                // Fast flyer. Banks into its own travel — the widest sway of the rigged set.
                case "creep.zephyr":
                    return new CreepMotionProfile(swayHz: 2.2f, swayDegrees: 6f, pulseHz: 3.0f, pulseAmp: 0.030f, flinchScale: 0.40f);

                // Stealth. The strongest fade pulse of the rigged set, so it reads as phasing rather
                // than merely walking.
                case "creep.stalker":
                    return new CreepMotionProfile(swayHz: 1.6f, swayDegrees: 4f, pulseHz: 2.0f, pulseAmp: 0.050f, flinchScale: 0.34f);

                // Burrower. A slow deep swell, like something surfacing and sinking as it advances.
                case "creep.burrower":
                    return new CreepMotionProfile(swayHz: 0.9f, swayDegrees: 3f, pulseHz: 1.2f, pulseAmp: 0.045f, bobRectified: true, flinchScale: 0.22f);

                // Shield tank. Deliberately the stillest thing in the roster — a guarded advance that
                // gives away nothing. Stillness is the characterisation here, not an absence of one.
                case "creep.warden":
                    return new CreepMotionProfile(swayHz: 0.5f, swayDegrees: 1.5f, pulseHz: 0.8f, pulseAmp: 0.015f, flinchScale: 0.18f);

                // Heaviest thing on the board at 90 hp. Ponderous: the lowest frequencies anywhere in
                // the roster, and the smallest flinch, so hits visibly fail to move it.
                case "creep.colossus":
                    return new CreepMotionProfile(swayHz: 0.4f, swayDegrees: 3.5f, pulseHz: 0.55f, pulseAmp: 0.030f, flinchScale: 0.15f);

                default:
                    return CreepMotionProfileForStyle(creepId, visualProfile, isRigged);
            }
        }

        /// <summary>
        /// Fallback row for a creep with no entry of its own, derived from its authored
        /// <see cref="CreepVisualMotionStyle"/>.
        /// </summary>
        /// <remarks>
        /// Keeps the visual library meaningful: a creep added to CreepVisualLibrary without a row
        /// above still moves like its declared family instead of defaulting to Runner's twitch.
        /// </remarks>
        private static CreepMotionProfile CreepMotionProfileForStyle(string creepId, CreepVisualProfile visualProfile, bool isRigged)
        {
            var style = visualProfile != null ? visualProfile.MotionStyle : CreepVisualMotionStyle.Auto;
            if (style == CreepVisualMotionStyle.Auto)
            {
                if (ContainsRole(creepId, "swarm")) style = CreepVisualMotionStyle.ClusterJitter;
                else if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss")) style = CreepVisualMotionStyle.HeavyBob;
                else if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air")) style = CreepVisualMotionStyle.Hover;
                else if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth")) style = CreepVisualMotionStyle.Shimmer;
                else if (ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege")) style = CreepVisualMotionStyle.SiegeWindup;
                else style = CreepVisualMotionStyle.RunnerDart;
            }

            switch (style)
            {
                case CreepVisualMotionStyle.ClusterJitter:
                    return new CreepMotionProfile(driftHz: 15f, driftAmp: 0.045f, spinDegreesPerSecond: 60f, flinchScale: 0.36f);
                case CreepVisualMotionStyle.HeavyBob:
                    return new CreepMotionProfile(bobHz: 3.4f, bobAmp: 0.055f, bobRectified: true, swayHz: 3.4f, swayDegrees: 1.5f, flinchScale: 0.24f, flinchTilt: 26f);
                case CreepVisualMotionStyle.Hover:
                    return new CreepMotionProfile(bobHz: 2.6f, bobAmp: 0.075f, spinDegreesPerSecond: 52f, driftHz: 0.9f, driftAmp: 0.070f, pulseHz: 3.4f, pulseAmp: 0.035f, flinchScale: 0.40f);
                case CreepVisualMotionStyle.Shimmer:
                    return new CreepMotionProfile(bobHz: 1.9f, bobAmp: 0.050f, swayHz: 1.5f, swayDegrees: 14f, driftHz: 1.1f, driftAmp: 0.060f, pulseHz: 2.3f, pulseAmp: 0.045f, flinchScale: 0.32f);
                case CreepVisualMotionStyle.Coil:
                    return new CreepMotionProfile(bobHz: 2.2f, bobAmp: 0.035f, bobRectified: true, spinDegreesPerSecond: 34f, driftHz: 1.7f, driftAmp: 0.050f, pulseHz: 2.2f, pulseAmp: 0.055f, flinchScale: 0.26f);
                case CreepVisualMotionStyle.SiegeWindup:
                    return new CreepMotionProfile(bobHz: 6f, bobAmp: 0.020f, bobRectified: true, rollDegrees: 4f, flinchScale: 0.24f);
                case CreepVisualMotionStyle.AuraPulse:
                    return new CreepMotionProfile(bobHz: 5f, bobAmp: 0.035f, bobRectified: true, spinDegreesPerSecond: 35f, flinchScale: 0.30f);
                default:
                    return new CreepMotionProfile(bobHz: 13f, bobAmp: 0.018f, driftHz: 13f, driftAmp: 0.055f, rollDegrees: 4f, pitchDegrees: 7f, forwardOffset: 0.06f, flinchScale: 0.34f);
            }
        }

        /// <summary>
        /// Spreads instances of the same creep out of lockstep, in radians.
        /// </summary>
        /// <remarks>
        /// Every creep drove its motion straight off Time.time, so every instance of a given creep
        /// was in perfect phase with every other one. A Swarm send puts several on the board at once
        /// and they jittered as one rigid body; the effect got worse the more of something you sent,
        /// which is exactly backwards. Offsetting by a hash of the entity key makes a group read as
        /// individuals, and is stable for the life of the entity so nothing jumps between frames.
        /// </remarks>
        private static float CreepMotionPhase(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return 0f;
            }

            unchecked
            {
                var hash = 17;
                for (var index = 0; index < key.Length; index++)
                {
                    hash = hash * 31 + key[index];
                }

                return (hash & 0xFFFF) / 65535f * (Mathf.PI * 2f);
            }
        }

        private static CreepMotion CreepRoleMotion(string creepId, CreepVisualProfile visualProfile, float hitFlashUntil, bool isRigged = false, string key = null)
        {
            var profile = CreepMotionProfileFor(creepId, visualProfile, isRigged);
            var time = Time.time + CreepMotionPhase(key);
            var flinch = Mathf.Clamp01((hitFlashUntil - Time.time) / CreepHitFlashDuration);
            var scale = 1f + flinch * profile.FlinchScale;

            // A rigged creep's clip already animates its body, so bob, roll, pitch and drift would
            // fight it — those all describe things a walk cycle is already doing. Sway and the scale
            // pulse are applied to the ROOT transform, which the clip never touches (root motion is
            // off on all eight rigs), so they layer cleanly and are what makes one rigged creep
            // distinguishable from another without re-authoring anyone's skeleton.
            if (isRigged)
            {
                var riggedSway = profile.SwayDegrees == 0f ? 0f : Mathf.Sin(time * profile.SwayHz) * profile.SwayDegrees;
                var riggedPulse = profile.PulseAmp == 0f ? 0f : Mathf.Sin(time * profile.PulseHz) * profile.PulseAmp;
                return new CreepMotion(
                    Vector3.zero,
                    Quaternion.Euler(0f, riggedSway, 0f),
                    scale * (1f + riggedPulse));
            }

            var bobWave = profile.BobHz == 0f
                ? 0f
                : profile.BobRectified
                    ? Mathf.Abs(Mathf.Sin(time * profile.BobHz))
                    : Mathf.Sin(time * profile.BobHz);
            var bob = bobWave * profile.BobAmp;

            var drift = Vector3.zero;
            if (profile.DriftAmp != 0f)
            {
                drift = new Vector3(
                    Mathf.Sin(time * profile.DriftHz) * profile.DriftAmp,
                    0f,
                    Mathf.Cos(time * profile.DriftHz * 0.85f) * profile.DriftAmp * 0.8f);
            }

            var yaw = profile.SpinDegreesPerSecond * time
                + (profile.SwayDegrees == 0f ? 0f : Mathf.Sin(time * profile.SwayHz) * profile.SwayDegrees);
            var roll = profile.RollDegrees == 0f ? 0f : Mathf.Sin(time * profile.BobHz) * profile.RollDegrees;
            var pulse = profile.PulseAmp == 0f ? 0f : Mathf.Sin(time * profile.PulseHz) * profile.PulseAmp;

            // The flinch tilt kicks AGAINST the current roll so a hit visibly interrupts the idle
            // rather than blending into it. The scale punch is what actually carries the reaction:
            // this camera is tilted only ~30 degrees off vertical, so tilt and vertical displacement
            // mostly project away, while uniform scale survives intact.
            var flinchTilt = profile.FlinchTilt == 0f
                ? 0f
                : -Mathf.Sign(roll == 0f ? 1f : roll) * flinch * profile.FlinchTilt;

            return new CreepMotion(
                new Vector3(drift.x, bob - flinch * 0.09f, drift.z + profile.ForwardOffset),
                Quaternion.Euler(profile.PitchDegrees + flinch * 20f, yaw, roll + flinchTilt),
                scale * (1f + pulse));
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

        /// <summary>
        /// Brightness applied to a tower's role marker for each tier it has been upgraded to.
        /// </summary>
        /// <remarks>
        /// An upgraded tower was otherwise pixel-identical to a fresh one, so a player who had
        /// spent gold levelling eight towers had no way to see which eight without tapping each in
        /// turn. Same defect the Repair Drone's buff had before it got a tether: a mechanic you
        /// cannot see is a spreadsheet.
        ///
        /// The role marker carries it rather than the body, because the marker is a small accent
        /// whose colour is already role-coded — shifting it reads as "this one is hotter" without
        /// changing silhouette, footprint or the owner trim that identifies whose it is.
        ///
        /// KNOWN WEAK, and measured rather than assumed: the marker colour is already saturated at
        /// tier 1 (HSV value 1.0), so this does not brighten so much as wash toward white, and the
        /// steps are uneven — RGB delta 0.392 from tier 1 to 2 but only 0.136 from 2 to 3. It is
        /// therefore a hint, not a readout, and telling tier 2 from tier 3 across a busy board is
        /// not something it can be relied on for. The authoritative answer is the TIER line on the
        /// selected-tower panel. A capture at the real game camera could not settle legibility
        /// either way because the towers sit under their own range halos and role labels, so this
        /// wants a human look before anything depends on it.
        /// </remarks>
        private static float TierMarkerBoost(int tier) => tier switch
        {
            >= 3 => 2.05f,
            2 => 1.6f,
            _ => 1.16f
        };

        private static void ApplyTowerColor(GameObject towerObject, string towerId, int ownerId, TowerVisualProfile visualProfile, int tier)
        {
            var roleColor = BoostValue(TowerMarkerColor(towerId), TierMarkerBoost(tier));
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
            SetProfileColor(towerObject, visualProfile.OwnerTrimRendererPath, AccentPoolColor(ownerColor));
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

        private static float CreepHealthFraction(int health, int maxHealth)
        {
            return Mathf.Clamp01(health / (float)Mathf.Max(1, maxHealth));
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

        /// <summary>The three buildable tower lines, as the weapon effects see them.</summary>
        private enum TowerLine
        {
            Arcane,
            Foundry,
            Grove
        }

        /// <summary>How one line's shots are drawn. See <see cref="StyleFor"/>.</summary>
        private readonly struct WeaponStyle
        {
            public WeaponStyle(float width, float intensity, float duration)
            {
                Width = width;
                Intensity = intensity;
                Duration = duration;
            }

            public float Width { get; }

            public float Intensity { get; }

            public float Duration { get; }

            /// <summary>Same shot, drawn heavier. Duration deliberately does not scale — a shot
            /// that lingers longer at higher tier would drift out of step with the fire rate.</summary>
            public WeaponStyle Scaled(float width, float intensity) =>
                new WeaponStyle(Width * width, Intensity * intensity, Duration);
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
        private static Color TowerMarkerColor(string towerId) => TowerCatalog.ForContentId(towerId).Accent;

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

        /// <summary>
        /// Local slot positions for the mesh-backed swarm cluster, roughly matching the layout of
        /// the original 2D sprite's 7-bot cluster (one lead body, others fanned around it) rather
        /// than an evenly spaced ring.
        /// </summary>
        // Slot spread scales with SwarmShardScale — bigger shards packed at the original ±0.15
        // spacing merge back into one mass, which is the "blob" read this cluster exists to avoid.
        private static readonly Vector3[] SwarmClusterSlots =
        {
            new Vector3(0f, 0f, 0.22f),
            new Vector3(-0.22f, 0f, -0.06f),
            new Vector3(0.22f, 0f, -0.06f),
            new Vector3(-0.12f, 0f, -0.22f),
            new Vector3(0.12f, 0f, -0.22f),
        };

        private const float SwarmShardScale = 0.5f;

        /// <summary>
        /// Replaces the single mesh-backed swarm body with a small cluster of scaled-down copies
        /// of the same mesh, each wandering within its own small "bubble" around a slot position.
        /// </summary>
        /// <remarks>
        /// The 2D sprite this creep's role was designed against showed a cluster of small shard
        /// bots, not one large one; the Meshy 3D pass generated (and the pipeline kept) a single
        /// enlarged body instead. This restores the multi-body read using the existing mesh/
        /// material — no new art asset — and reuses the same health-fraction "living count" idea
        /// the old pre-mesh <see cref="ConfigureSwarmMarker"/> primitive overlay used, so the
        /// cluster visibly thins as this creep takes damage instead of just changing color.
        /// </remarks>
        private static void ConfigureSwarmCluster(GameObject creepObject, string creepId, int senderId, float healthFraction, bool isHitFlashing)
        {
            var body = creepObject.transform.Find("Body");
            var imported = body != null ? body.Find("Imported3DVisual") : null;
            if (imported == null)
            {
                return;
            }

            // The pipeline's single big body is hidden, not destroyed — SwarmShard0..4 below are
            // copies of its own mesh/material, so there is nothing else for this creep to show.
            if (imported.gameObject.activeSelf)
            {
                imported.gameObject.SetActive(false);
            }

            var sourceFilter = imported.GetComponentInChildren<MeshFilter>(true);
            var sourceRenderer = imported.GetComponentInChildren<MeshRenderer>(true);
            if (sourceFilter == null || sourceFilter.sharedMesh == null || sourceRenderer == null)
            {
                return;
            }

            var mesh = sourceFilter.sharedMesh;
            var material = sourceRenderer.sharedMaterial;
            var color = CreepBodyColor(creepId, senderId, healthFraction, isHitFlashing);
            var livingCount = Mathf.Clamp(Mathf.CeilToInt(healthFraction * SwarmClusterSlots.Length), 1, SwarmClusterSlots.Length);
            var time = Time.time;

            // The raw FBX mesh is authored tiny (~0.02 units); it only renders at a sane size
            // because the import hierarchy under Imported3DVisual applies a large scale to it
            // (~39x for this asset). Shards are bare children of the CREEP ROOT, so they inherit
            // none of that — cloning sharedMesh at a plain 0.44 localScale produced ~0.01-unit
            // specks, roughly 90x too small, which is why tuning the shard scale and slot spread
            // alone never made any visible difference. Derive the correction from the source
            // transform itself rather than hardcoding it, so it self-corrects if the import
            // pipeline's scaling ever changes. Rotation is carried across the same way, since the
            // import chain may also hold an orientation correction the raw mesh depends on.
            var creepScale = creepObject.transform.lossyScale;
            var meshScale = sourceFilter.transform.lossyScale;
            var importScale = new Vector3(
                meshScale.x / Mathf.Max(0.0001f, creepScale.x),
                meshScale.y / Mathf.Max(0.0001f, creepScale.y),
                meshScale.z / Mathf.Max(0.0001f, creepScale.z));
            var importRotation = Quaternion.Inverse(creepObject.transform.rotation) * sourceFilter.transform.rotation;

            for (var index = 0; index < SwarmClusterSlots.Length; index++)
            {
                var shard = EnsureMeshChild(creepObject, $"SwarmShard{index}", mesh, material);
                var active = index < livingCount;
                shard.SetActive(active);
                if (!active)
                {
                    continue;
                }

                // Each shard's own phase keeps the cluster from moving as one rigid block — the
                // "bubble" is this small per-shard wander around its slot, not a shared pose.
                var phase = index * 1.7f;
                var wobble = new Vector3(
                    Mathf.Sin(time * 2.1f + phase) * 0.045f,
                    Mathf.Sin(time * 3.3f + phase * 1.3f) * 0.03f + 0.03f,
                    Mathf.Cos(time * 2.4f + phase) * 0.045f);
                shard.transform.localPosition = SwarmClusterSlots[index] + wobble;
                shard.transform.localRotation = Quaternion.Euler(0f, (time * 26f + phase * 40f) % 360f, 0f) * importRotation;
                shard.transform.localScale = importScale * SwarmShardScale;
                SetColor(shard, color);
            }

            // Swarm drops the SenderAccent ownership pool entirely: against a spread cluster of
            // small shards (rather than the single solid body every other creep has) it reads as a
            // dominant glow rather than a subtle ground tint. Note this costs Swarm its sender
            // identity read — CreepRoleColor returns a fixed mint for swarm and ignores senderId,
            // so unlike roles that fall through to SenderColor, the shards themselves carry no
            // sender tint to fall back on.
            var senderAccent = creepObject.transform.Find("SenderAccent");
            if (senderAccent != null && senderAccent.gameObject.activeSelf)
            {
                senderAccent.gameObject.SetActive(false);
            }
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

        /// <summary>
        /// Which of the three tower lines a content id belongs to, for weapon styling.
        /// </summary>
        /// <remarks>
        /// Reads <see cref="TowerCatalog"/> rather than restating the grouping, so a tower moved
        /// between lines cannot end up firing one line's weapon from another line's card. Inherits
        /// that catalog's Arrow fallback, which lands on Arcane — the same arm the shot styling used
        /// to take for every unrecognised tower anyway.
        /// </remarks>
        private static TowerLine LineFor(string towerId) => TowerCatalog.ForContentId(towerId).Category switch
        {
            TowerCatalog.CategoryFoundry => TowerLine.Foundry,
            TowerCatalog.CategoryGrove => TowerLine.Grove,
            _ => TowerLine.Arcane
        };

        /// <summary>
        /// The weapon grammar for a line: how wide, how bright and how long its shots draw.
        /// </summary>
        /// <remarks>
        /// Before this, every tower outside the original five fired the same beam at the same width
        /// for the same duration, in one of two colours picked purely by whether damage reached 5 —
        /// so a GROVE spore bloom and a FOUNDRY gatling fired identical blue shots. The three lines
        /// have distinct names, models and card accents, and the weapons ignored all of it.
        ///
        /// Durations stay at or under 0.3s. GROVE is the slow, lingering one by design, but the
        /// board already carries range halos, role-marker labels and health bars, and effects that
        /// outstay that clutter have twice proved unreadable here.
        /// </remarks>
        private static WeaponStyle StyleFor(TowerLine line, int damage, int tier = 1)
        {
            var baseStyle = line switch
            {
                // Thin, cold and quick: precision energy.
                TowerLine.Arcane => new WeaponStyle(0.11f, 1.75f, damage >= 5 ? 0.13f : 0.1f),
                // Heavier and hotter, and it hangs a moment longer: machinery throwing ordnance.
                TowerLine.Foundry => new WeaponStyle(0.17f, 1.35f, damage >= 5 ? 0.16f : 0.13f),
                // Thick, soft and slow: something living reaching out.
                _ => new WeaponStyle(0.26f, 1.0f, damage >= 5 ? 0.3f : 0.24f)
            };

            return baseStyle.Scaled(TierWidthBoost(tier), TierIntensityBoost(tier));
        }

        /// <summary>
        /// How much wider a shot draws at each tier.
        /// </summary>
        /// <remarks>
        /// An upgrade should be something you can see happen, not something you read off a panel.
        /// The tower's own marker colour was carrying that job badly — a measured RGB delta of only
        /// 0.136 between tiers 2 and 3, against 0.392 from 1 to 2 — so the weapon carries it too.
        /// Width is the stronger of the two cues here; intensity alone saturates and stops reading.
        /// </remarks>
        private static float TierWidthBoost(int tier) => tier switch
        {
            >= 3 => 1.5f,
            2 => 1.22f,
            _ => 1f
        };

        private static float TierIntensityBoost(int tier) => tier switch
        {
            >= 3 => 1.35f,
            2 => 1.16f,
            _ => 1f
        };

        private static Color LineShotColor(TowerLine line, int damage) => line switch
        {
            TowerLine.Foundry => damage >= 5 ? new Color(1f, 0.5f, 0.16f) : new Color(1f, 0.62f, 0.26f),
            TowerLine.Grove => damage >= 5 ? new Color(0.44f, 0.9f, 0.34f) : new Color(0.58f, 0.88f, 0.46f),
            _ => damage >= 5 ? SignalGold : ArcaneBlue
        };

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

            // Everything else is keyed to its line. The five above keep their own colours, which are
            // already tuned and are all Arcane anyway.
            return LineShotColor(LineFor(towerId), damage);
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

        /// <summary>One animating board label: where it started, when, and for how long.</summary>
        private readonly struct FloatingLabel
        {
            public FloatingLabel(GameObject @object, TMPro.TextMeshPro label, float spawnedAt, float duration)
            {
                Object = @object;
                Label = label;
                Origin = @object.transform.position;
                SpawnedAt = spawnedAt;
                Duration = duration;
            }

            public GameObject Object { get; }
            public TMPro.TextMeshPro Label { get; }
            public Vector3 Origin { get; }
            public float SpawnedAt { get; }
            public float Duration { get; }
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

        /// <summary>
        /// Bramble zone footprint, in cells. Deliberately NOT tuned for looks.
        /// </summary>
        /// <remarks>
        /// CombatService.BrambleZoneCells is 3, and the zone starts at the first route cell in range,
        /// so this shows the braked span rather than the tower. Shrinking it to calm the visual down
        /// would make it lie about how much lane the brake actually covers — opacity is the knob for
        /// that, not size.
        /// </remarks>
        private const float BrambleMarkerScale = 2.6f;

        /// <summary>
        /// Alpha dropped from 0.34. The marker is 2.6 cells across, and until the decal was lifted
        /// clear of the raised build plates most of that area was hidden under them — so 0.34 was
        /// tuned against a fraction of the footprint that actually shows now. At full visibility the
        /// same value read as a purple slab over the lane ("thorn ground bloom is too much").
        /// </remarks>
        private static readonly Color BrambleMarkerColor = new Color(0.42f, 0.24f, 0.58f, 0.15f);
        private static readonly Color GrovebondMarkerColor = new Color(0.55f, 0.95f, 0.38f, 0.7f);

        // Repair Drone Spire's own catalog accent (TowerCatalog.cs, id 9, label "DRONE"), reused here
        // rather than an invented color so the tether reads as belonging to the drone at a glance.
        // Thickness/height/alpha were raised past a first guess (0.05/0.18/0.62) after a capture at
        // the actual in-game ActiveLane camera distance showed it lost against both towers' own
        // range-halo spheres — the same failure Grovebond's ring hit originally ("invisible under
        // the tower mesh... starts wide enough to clear the silhouette"). A further attempt at 0.48
        // rose into the always-on-top role-marker text layer and read WORSE, not better, so this
        // stayed at the value that measurably improved on the first guess without competing with
        // that text.
        private static readonly Color ServicingTetherColor = new Color(0.95f, 0.82f, 0.45f, 0.85f);
        private const float ServicingTetherThickness = 0.11f;
        private const float ServicingTetherHeight = 0.34f;

        private const float MortarLaunchHeight = 0.62f;
        private const float MortarArcHeight = 1.15f;
        private const float MortarTelegraphStartScale = 1.6f;
        private const float MortarTelegraphEndScale = 0.62f;
        private static readonly Color MortarShellColor = new Color(1f, 0.62f, 0.24f, 1f);
        private static readonly Color MortarTelegraphColor = new Color(1f, 0.45f, 0.18f, 0.72f);
        private static readonly Color MortarImpactColor = new Color(1f, 0.55f, 0.2f, 1f);

        /// <summary>A shell in flight, with the ground telegraph marking where it will land.</summary>
        private readonly struct MortarShellEffect
        {
            public MortarShellEffect(GameObject shell, GameObject telegraph, Vector3 from, Vector3 to, float startTime, float duration)
            {
                Shell = shell;
                Telegraph = telegraph;
                From = from;
                To = to;
                StartTime = startTime;
                Duration = duration;
            }

            public GameObject Shell { get; }
            public GameObject Telegraph { get; }
            public Vector3 From { get; }
            public Vector3 To { get; }
            public float StartTime { get; }
            public float Duration { get; }
        }

        private readonly struct ExpandingRingEffect
        {
            public ExpandingRingEffect(GameObject @object, float startTime, float duration, float startScale, float endScale, Color baseColor)
            {
                Object = @object;
                StartTime = startTime;
                Duration = duration;
                StartScale = startScale;
                EndScale = endScale;
                BaseColor = baseColor;
            }

            public GameObject Object { get; }
            public float StartTime { get; }
            public float Duration { get; }
            public float StartScale { get; }
            public float EndScale { get; }
            public Color BaseColor { get; }
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
