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
    /// Board construction: the per-lane furniture, the endpoint plates and gates, and the
    /// bake that folds all of it into one mesh per lane through <see cref="BoardMeshBuilder"/>.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        private const string SpawnGateSpriteResourcePath = "Art/Board/Endpoints/board_spawn_gate_v03";
        private const string LeakGateSpriteResourcePath = "Art/Board/Endpoints/board_leak_gate_v03";
        private const string BoardDeepFieldTextureResourcePath = "Art/Board/Materials/board_deep_field_option_11";
        private const string BoardBuildBandTextureResourcePath = "Art/Board/Materials/board_build_band_option_11";
        private const string BoardRouteCoreTextureResourcePath = "Art/Board/Materials/board_route_core_option_11";

        /// <summary>
        /// Vertex shade applied to the bottom edge of every baked board box, blending back to the
        /// authored colour at the top. The primitive cubes used to gain their seam definition from
        /// self-shadowing across the 0.04 gap between tiles; baking the same falloff into the mesh
        /// keeps the grid legible and adds contact occlusion under every raised strip.
        /// </summary>
        private const float BoardSeamShade = 0.74f;

        private readonly List<GameObject> laneCells = new List<GameObject>();
        private readonly List<GameObject> laneDecorations = new List<GameObject>();
        private readonly List<BoardPiece> pendingBoardPieces = new List<BoardPiece>();

        /// <summary>Total board pieces folded into the baked lane meshes, for the geometry budget log.</summary>
        private int bakedBoardPieceCount;
        private readonly BoardMeshBuilder boardMeshBuilder = new BoardMeshBuilder();
        private readonly Dictionary<string, Material> referencePlateMaterials = new Dictionary<string, Material>();

        private readonly List<SpawnGatePulseElement> spawnGatePulseElements = new List<SpawnGatePulseElement>();

        /// <summary>
        /// Spawn gate artwork, brightness-pulsed to show the gate is live. This replaces the
        /// geometric pulse bars, which achieved the same thing by laying flat cubes straight across
        /// the middle of the portal sprite and hiding the detail it was drawn with.
        /// </summary>
        private readonly List<SpriteRenderer> spawnGateSpriteRenderers = new List<SpriteRenderer>();

        private Sprite spawnGateSprite;
        private Sprite leakGateSprite;
        private Texture2D boardDeepFieldTexture;
        private Texture2D boardBuildBandTexture;
        private Texture2D boardRouteCoreTexture;
        private GameObject boardGeometryRoot;

        private bool laneCreated;

        private bool EndpointSpritesAvailable => spawnGateSprite != null && leakGateSprite != null;

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
}
