#nullable enable

using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Combat;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class TouchPlacementController : MonoBehaviour
    {
        private const int LaneWidth = 7;
        private const int LaneLength = 16;
        private const string BuilderSpriteResourcePath = "Art/Builder/Production/Sprites/builder_candidate_v01_trimmed";

        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color WardViolet = new(0.608f, 0.424f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);
        private static readonly Color DisabledInk = new(0.22f, 0.25f, 0.32f, 0.88f);
        private static readonly Color DisabledText = new(0.55f, 0.59f, 0.68f, 1f);

        private static GUIStyle? panelStyle;
        private static GUIStyle? titleStyle;
        private static GUIStyle? bodyStyle;
        private static GUIStyle? buttonStyle;
        private static GUIStyle? metaStyle;

        [SerializeField]
        private Camera inputCamera = null!;

        [SerializeField]
        private UnityCommandAdapter commandAdapter = null!;

        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        [SerializeField]
        private SendDockController sendDockController = null!;

        [SerializeField]
        private PlacementFeedbackView feedbackView = null!;

        [SerializeField]
        private GameObject ghost = null!;

        private GameObject builderAvatar = null!;
        private SpriteRenderer? builderAvatarSprite;

        [SerializeField]
        private bool showPlacementReadout = true;

        private GameObject selectionRing = null!;

        private bool isPlacing;
        private bool isPaletteExpanded;
        private int selectedTowerRole;
        private int lastSelectedTowerRole;
        private int highlightedTowerRole = -1;
        private Vector2Int selectedCell;
        private TowerCombatState? selectedTower;
        private VerticalSliceCommandResult placementPreview = VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);

        public bool IsPlacing => isPlacing;

        public bool IsTowerPaletteExpanded => isPaletteExpanded;

        public void Initialize(Camera camera, UnityCommandAdapter adapter, PlacementFeedbackView feedback, GameObject placementGhost)
        {
            inputCamera = camera;
            commandAdapter = adapter;
            feedbackView = feedback;
            ghost = placementGhost;
            selectedCell = DefaultBuilderCell();
            EnsureBuilderAvatar();
            builderAvatar.SetActive(true);
            UpdateBuilderAvatar();
        }

        public void BeginTowerPlacement() => BeginTowerPlacement(lastSelectedTowerRole);

        public void BeginControlTowerPlacement() => BeginTowerPlacement(1);

        public void BeginUtilityTowerPlacement() => BeginTowerPlacement(2);

        public void BeginPulseTowerPlacement() => BeginTowerPlacement(3);

        public void BeginPrismTowerPlacement() => BeginTowerPlacement(4);

        private void BeginTowerPlacement(int towerRole)
        {
            CloseSendDock();
            isPlacing = true;
            isPaletteExpanded = false;
            selectedTowerRole = towerRole;
            lastSelectedTowerRole = towerRole;
            highlightedTowerRole = towerRole;
            ghost.SetActive(true);
            builderAvatar.SetActive(true);
            MoveGhost();
            selectedTower = null;
            HideSelectionRing();
            feedbackView.Clear();
        }

        public void CloseBottomPanelsForSend()
        {
            isPaletteExpanded = false;
            selectedTower = null;
            HideSelectionRing();

            if (isPlacing)
            {
                CancelPlacement(false);
            }
        }

        public void NudgeUp() => Nudge(Vector2Int.down);

        public void NudgeDown() => Nudge(Vector2Int.up);

        public void NudgeLeft() => Nudge(Vector2Int.left);

        public void NudgeRight() => Nudge(Vector2Int.right);

        public void CancelPlacement() => CancelPlacement(true);

        private void CancelPlacement(bool clearFeedback)
        {
            isPlacing = false;
            ghost.SetActive(false);
            EnsureBuilderAvatar();
            builderAvatar.SetActive(true);
            UpdateBuilderAvatar();
            if (clearFeedback)
            {
                feedbackView.Clear();
            }
        }

        public void ConfirmPlacement()
        {
            if (!isPlacing)
            {
                return;
            }

            if (!IsSelectedCellInBounds())
            {
                feedbackView.ShowRejected(CommandRejectionReason.InvalidLane);
                placementPreview = VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);
                UpdateGhostColor();
                return;
            }

            var result = selectedTowerRole switch
            {
                1 => commandAdapter.PlaceControlTower(selectedCell.x, selectedCell.y),
                2 => commandAdapter.PlaceUtilityTower(selectedCell.x, selectedCell.y),
                3 => commandAdapter.PlacePulseTower(selectedCell.x, selectedCell.y),
                4 => commandAdapter.PlacePrismTower(selectedCell.x, selectedCell.y),
                _ => commandAdapter.PlaceSampleTower(selectedCell.x, selectedCell.y)
            };
            if (result.Accepted)
            {
                feedbackView.ShowAccepted(SelectedTowerName() + " placed");
                // Keep the builder active with the last selected tower for fast repeat placement.
                lastSelectedTowerRole = selectedTowerRole;
                MoveGhost();
                return;
            }

            if (result.RejectionReason == CommandRejectionReason.InsufficientGold)
            {
                feedbackView.ShowRejected(result.RejectionReason, SelectedTowerCost(), CurrentPlayerGold());
            }
            else
            {
                feedbackView.ShowRejected(result.RejectionReason);
            }
            RefreshPlacementPreview();
        }

        public void SellLastTower()
        {
            RefreshSelectedTowerFromSnapshot();
            var result = selectedTower is not null
                ? commandAdapter.SellTowerAt(selectedTower.Position.X, selectedTower.Position.Y)
                : commandAdapter.SellLastSampleTower();
            if (result.Accepted)
            {
                selectedTower = null;
                HideSelectionRing();
                feedbackView.ShowEconomy("Tower sold");
                return;
            }

            feedbackView.ShowRejected(result.RejectionReason);
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (IsPointerOverRuntimeUi(Input.mousePosition))
            {
                return;
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main!;
            }

            var ray = inputCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var distance))
            {
                return;
            }

            var hit = ray.GetPoint(distance);
            var hitCell = ClampToLane(new Vector2Int(Mathf.RoundToInt(hit.x), WorldZToGridY(hit.z)));
            if (isPlacing)
            {
                selectedCell = hitCell;
                MoveGhost();
                return;
            }

            if (SelectTowerAt(hitCell))
            {
                return;
            }

            selectedTower = null;
            HideSelectionRing();
            selectedCell = hitCell;
            UpdateBuilderAvatar();
        }

        private void OnGUI()
        {
            if (!showPlacementReadout)
            {
                return;
            }

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            DrawTowerPalette(scale);
            if (IsSendDockExpanded())
            {
                return;
            }

            DrawSelectedTowerPanel(scale);

            if (!isPlacing)
            {
                return;
            }

            var rect = PlacementPanelRect(scale, frame);
            var accent = SelectedTowerAccent();

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            titleStyle!.fontSize = Mathf.RoundToInt(17f * scale);
            titleStyle.normal.textColor = accent;
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            bodyStyle.normal.textColor = Cloud;

            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 9f * scale, rect.width - 116f * scale, 24f * scale), $"{SelectedTowerName().ToUpperInvariant()}  {SelectedTowerCost()}G", titleStyle);
            if (DrawLauncherButton(new Rect(rect.xMax - 96f * scale, rect.y + 7f * scale, 80f * scale, 30f * scale), "ALL", SignalGold, scale))
            {
                CancelPlacement(false);
                OpenTowerPalette();
                return;
            }

            var placementLine = placementPreview.Accepted ? $"CELL {selectedCell.x}, {selectedCell.y} READY" : PlacementPreviewText();
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 35f * scale, rect.width - 24f * scale, 20f * scale), placementLine, bodyStyle);
            var actionHint = placementPreview.Accepted ? "BUILDER ONLINE  •  CONFIRM TO BUILD" : PlacementRecoveryText();
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 57f * scale, rect.width - 24f * scale, 20f * scale), actionHint, bodyStyle);
        }

        private bool SelectTowerAt(Vector2Int cell)
        {
            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }

            selectedTower = null;
            HideSelectionRing();
            var snapshot = simulationDriver?.LatestSnapshot;
            if (snapshot is null)
            {
                return false;
            }

            foreach (var tower in snapshot.Towers)
            {
                if (tower.OwnerId.Value == 1 && tower.LaneId.Value == 1 && tower.Position.X == cell.x && tower.Position.Y == cell.y)
                {
                    selectedTower = tower;
                    UpdateSelectionRing(tower);
                    feedbackView.ShowAccepted(TowerRoleName(tower.TowerId.Value) + " selected");
                    return true;
                }
            }

            return false;
        }

        private void DrawSelectedTowerPanel(float scale)
        {
            if (isPlacing || selectedTower is null)
            {
                return;
            }

            var frame = MobileViewportLayout.ScreenRect();
            var rect = SelectedTowerPanelRect(scale, frame);
            var accent = TowerAccent(selectedTower.TowerId.Value);
            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            titleStyle!.fontSize = Mathf.RoundToInt(16f * scale);
            titleStyle.normal.textColor = accent;
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            bodyStyle.normal.textColor = Cloud;

            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 8f * scale, rect.width - 116f * scale, 22f * scale), TowerRoleName(selectedTower.TowerId.Value).ToUpperInvariant(), titleStyle);
            if (DrawLauncherButton(new Rect(rect.xMax - 96f * scale, rect.y + 7f * scale, 80f * scale, 30f * scale), "MENU", SignalGold, scale))
            {
                selectedTower = null;
                HideSelectionRing();
                OpenTowerPalette();
                return;
            }
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 32f * scale, rect.width - 24f * scale, 18f * scale), TowerPurpose(selectedTower.TowerId.Value), bodyStyle);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 51f * scale, rect.width - 24f * scale, 18f * scale), $"CELL {selectedTower.Position.X}, {selectedTower.Position.Y}  OWNER P{selectedTower.OwnerId.Value}", bodyStyle);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 70f * scale, rect.width - 108f * scale, 18f * scale), "Tap another tower or sell this one", bodyStyle);

            if (GUI.Button(new Rect(rect.x + rect.width - 86f * scale, rect.y + 36f * scale, 70f * scale, 42f * scale), "SELL", buttonStyle ?? GUI.skin.button))
            {
                SellLastTower();
            }
        }

        private void Nudge(Vector2Int delta)
        {
            if (!isPlacing)
            {
                return;
            }

            selectedCell += delta;
            selectedCell = ClampToLane(selectedCell);
            MoveGhost();
        }

        private static Vector2Int DefaultBuilderCell() => new(LaneWidth / 2, LaneLength - 3);

        private static Vector2Int ClampToLane(Vector2Int cell) =>
            new(
                Mathf.Clamp(cell.x, 0, LaneWidth - 1),
                Mathf.Clamp(cell.y, 0, LaneLength - 1));

        private static int WorldZToGridY(float z) => LaneLength - 1 - Mathf.RoundToInt(z);

        private static Vector3 GridToWorld(Vector2Int cell, float y) => new(cell.x, y, LaneLength - 1 - cell.y);

        private void MoveGhost()
        {
            ghost.transform.position = GridToWorld(selectedCell, 0.6f);
            UpdateBuilderAvatar();
            ghost.transform.localScale = SelectedTowerGhostScale();
            ConfigurePlacementGhostVisual();
            RefreshPlacementPreview();
        }

        private void RefreshPlacementPreview()
        {
            if (!IsSelectedCellInBounds())
            {
                placementPreview = VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);
                UpdateGhostColor();
                return;
            }

            placementPreview = selectedTowerRole switch
            {
                1 => commandAdapter.PreviewControlTower(selectedCell.x, selectedCell.y),
                2 => commandAdapter.PreviewUtilityTower(selectedCell.x, selectedCell.y),
                3 => commandAdapter.PreviewPulseTower(selectedCell.x, selectedCell.y),
                4 => commandAdapter.PreviewPrismTower(selectedCell.x, selectedCell.y),
                _ => commandAdapter.PreviewSampleTower(selectedCell.x, selectedCell.y)
            };

            UpdateGhostColor();
        }

        private void UpdateGhostColor()
        {
            var color = placementPreview.Accepted ? SelectedTowerAccent() : Danger;
            color.a = placementPreview.Accepted ? 0.74f : 0.86f;
            foreach (var ghostRenderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                ghostRenderer.material.color = color;
            }
        }

        private void ConfigurePlacementGhostVisual()
        {
            var roleId = SelectedTowerRoleId();
            var accent = SelectedTowerAccent();
            var isControl = roleId == "control";
            var isRelay = roleId == "relay";
            var isPulse = roleId == "pulse";
            var isPrism = roleId == "prism";
            var isArrow = roleId == "arrow";

            ConfigureGhostChild("GhostBase", true, new Vector3(0f, -0.28f, 0f), isControl || isPulse ? new Vector3(1.18f, 0.06f, 1.18f) : isRelay ? new Vector3(0.78f, 0.06f, 0.78f) : isPrism ? new Vector3(0.58f, 0.06f, 0.58f) : new Vector3(0.72f, 0.06f, 0.72f), accent);
            ConfigureGhostChild("GhostArrowSpire", isArrow, new Vector3(0f, 0.48f, 0f), new Vector3(0.14f, 0.92f, 0.14f), accent);
            ConfigureGhostChild("GhostArrowBowLeft", isArrow, new Vector3(-0.26f, 0.36f, 0f), new Vector3(0.1f, 0.62f, 0.12f), accent);
            ConfigureGhostChild("GhostArrowBowRight", isArrow, new Vector3(0.26f, 0.36f, 0f), new Vector3(0.1f, 0.62f, 0.12f), accent);
            ConfigureGhostChild("GhostControlRing", isControl, new Vector3(0f, 0.1f, 0f), new Vector3(1.36f, 0.04f, 1.36f), accent);
            ConfigureGhostChild("GhostControlCore", isControl, new Vector3(0f, 0.42f, 0f), new Vector3(0.34f, 0.34f, 0.34f), accent);
            ConfigureGhostChild("GhostRelayMast", isRelay, new Vector3(0f, 0.52f, 0f), new Vector3(0.1f, 1.02f, 0.1f), accent);
            ConfigureGhostChild("GhostRelaySignal", isRelay, new Vector3(0f, 1.08f, 0f), new Vector3(0.5f, 0.04f, 0.5f), accent);
            ConfigureGhostChild("GhostPulseRing", isPulse, new Vector3(0f, 0.08f, 0f), new Vector3(1.44f, 0.04f, 1.44f), accent);
            ConfigureGhostChild("GhostPulseCore", isPulse, new Vector3(0f, 0.44f, 0f), new Vector3(0.44f, 0.44f, 0.44f), accent);
            ConfigureGhostChild("GhostPulseEcho", isPulse, new Vector3(0f, 0.72f, 0f), new Vector3(0.92f, 0.035f, 0.92f), accent);
            ConfigureGhostChild("GhostPrismSpire", isPrism, new Vector3(0f, 0.68f, 0f), new Vector3(0.22f, 1.28f, 0.22f), accent);
            ConfigureGhostChild("GhostPrismLens", isPrism, new Vector3(0f, 1.36f, 0f), new Vector3(0.42f, 0.18f, 0.42f), accent);
            ConfigureGhostChild("GhostPrismBeam", isPrism, new Vector3(0f, 1.08f, 0.34f), new Vector3(0.08f, 0.78f, 0.08f), MintSignal);
        }

        private void EnsureBuilderAvatar()
        {
            if (builderAvatar != null)
            {
                return;
            }

            builderAvatar = new GameObject("Builder Avatar");
            builderAvatar.transform.SetParent(transform, false);
            builderAvatar.transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);
            builderAvatar.SetActive(false);

            CreateBuilderPart("Body", PrimitiveType.Capsule, new Vector3(0f, 0.34f, 0f), new Vector3(0.28f, 0.34f, 0.28f));
            CreateBuilderPart("Pack", PrimitiveType.Cube, new Vector3(0f, 0.38f, -0.2f), new Vector3(0.25f, 0.3f, 0.12f));
            CreateBuilderPart("Visor", PrimitiveType.Cube, new Vector3(0f, 0.53f, 0.18f), new Vector3(0.2f, 0.08f, 0.08f));
            CreateBuilderPart("FootMarker", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.72f, 0.02f, 0.72f));
            CreateBuilderSpriteVisual();
        }

        private void CreateBuilderPart(string partName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.transform.SetParent(builderAvatar.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
        }

        private void UpdateBuilderAvatar()
        {
            if (builderAvatar == null)
            {
                return;
            }

            builderAvatar.transform.position = GridToWorld(selectedCell, 0.02f) + new Vector3(-0.48f, 0f, 0.24f);
            var accent = SelectedTowerAccent();
            accent.a = 1f;
            foreach (var part in builderAvatar.GetComponentsInChildren<Renderer>(true))
            {
                if (part == builderAvatarSprite)
                {
                    part.enabled = true;
                    continue;
                }

                part.material.color = part.gameObject.name switch
                {
                    "Body" => Cloud,
                    "Pack" => PanelInk,
                    "FootMarker" => accent,
                    _ => accent
                };
            }
        }

        private void CreateBuilderSpriteVisual()
        {
            var sprite = Resources.Load<Sprite>(BuilderSpriteResourcePath);
            if (sprite == null)
            {
                return;
            }

            var plate = new GameObject("AIPlateVisual");
            plate.transform.SetParent(builderAvatar.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.27f, 0.04f);
            plate.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plate.transform.localScale = new Vector3(0.145f, 0.145f, 1f);

            builderAvatarSprite = plate.AddComponent<SpriteRenderer>();
            builderAvatarSprite.sprite = sprite;
            builderAvatarSprite.sortingOrder = 12;

            foreach (var part in builderAvatar.GetComponentsInChildren<Renderer>(true))
            {
                if (part != builderAvatarSprite && part.gameObject.name != "FootMarker")
                {
                    part.enabled = false;
                }
            }
        }

        private GameObject EnsureGhostChild(string childName, PrimitiveType primitiveType)
        {
            var child = ghost.transform.Find(childName)?.gameObject;
            if (child != null)
            {
                return child;
            }

            child = GameObject.CreatePrimitive(primitiveType);
            child.name = childName;
            child.transform.SetParent(ghost.transform, false);
            return child;
        }

        private void ConfigureGhostChild(string childName, bool active, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var child = EnsureGhostChild(childName, childName.Contains("Ring") || childName.Contains("Signal") || childName.Contains("Base") ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            child.SetActive(active);
            if (!active)
            {
                return;
            }

            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = localScale;
            child.GetComponent<Renderer>().material.color = color;
        }

        private void UpdateSelectionRing(TowerCombatState tower)
        {
            if (selectionRing == null)
            {
                selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                selectionRing.name = "SelectedTowerRangeRing";
            }

            selectionRing.SetActive(true);
            selectionRing.transform.position = GridToWorld(new Vector2Int(tower.Position.X, tower.Position.Y), 0.06f);
            selectionRing.transform.localScale = TowerSelectionRingScale(tower.TowerId.Value);
            selectionRing.GetComponent<Renderer>().material.color = TowerAccent(tower.TowerId.Value);
        }

        private void HideSelectionRing()
        {
            if (selectionRing != null)
            {
                selectionRing.SetActive(false);
            }
        }

        private Vector3 SelectedTowerGhostScale()
        {
            return SelectedTowerRoleId() switch
            {
                "control" => new Vector3(0.82f, 0.46f, 0.82f),
                "relay" => new Vector3(0.52f, 0.52f, 0.52f),
                "pulse" => new Vector3(0.86f, 0.44f, 0.86f),
                "prism" => new Vector3(0.48f, 1.0f, 0.48f),
                _ => new Vector3(0.62f, 0.78f, 0.62f)
            };
        }

        private string SelectedTowerRoleId()
        {
            return selectedTowerRole switch
            {
                1 => "control",
                2 => "relay",
                3 => "pulse",
                4 => "prism",
                _ => "arrow"
            };
        }

        private static Vector3 TowerSelectionRingScale(string towerId)
        {
            if (towerId.Contains("control")) return new Vector3(1.42f, 0.03f, 1.42f);
            if (towerId.Contains("relay") || towerId.Contains("economy")) return new Vector3(1.18f, 0.03f, 1.18f);
            if (towerId.Contains("pulse")) return new Vector3(1.62f, 0.03f, 1.62f);
            if (towerId.Contains("prism")) return new Vector3(2.12f, 0.03f, 2.12f);
            return new Vector3(1.28f, 0.03f, 1.28f);
        }

        private string SelectedTowerName()
        {
            return selectedTowerRole switch
            {
                1 => "Control ward",
                2 => "Relay ward",
                3 => "Pulse ward",
                4 => "Prism ward",
                _ => "Arrow ward"
            };
        }

        private static string TowerRoleName(string towerId)
        {
            if (towerId.Contains("control")) return "Control ward";
            if (towerId.Contains("relay") || towerId.Contains("economy")) return "Relay ward";
            if (towerId.Contains("pulse")) return "Pulse ward";
            if (towerId.Contains("prism")) return "Prism ward";
            return "Arrow ward";
        }

        private static Color TowerAccent(string towerId)
        {
            if (towerId.Contains("control")) return WardViolet;
            if (towerId.Contains("relay") || towerId.Contains("economy")) return SignalGold;
            if (towerId.Contains("pulse")) return MintSignal;
            if (towerId.Contains("prism")) return new Color(0.72f, 0.94f, 1f);
            return ArcaneBlue;
        }

        private Color SelectedTowerAccent()
        {
            return selectedTowerRole switch
            {
                1 => WardViolet,
                2 => SignalGold,
                3 => MintSignal,
                4 => new Color(0.72f, 0.94f, 1f),
                _ => ArcaneBlue
            };
        }

        private void DrawTowerPalette(float scale)
        {
            if (isPlacing)
            {
                return;
            }

            if (!isPaletteExpanded && IsSendDockExpanded())
            {
                return;
            }

            var frame = MobileViewportLayout.ScreenRect();
            var launcherRect = TowerPaletteLauncherRect(scale, frame);
            if (!isPaletteExpanded)
            {
                if (DrawLauncherButton(launcherRect, "BUILD", MintSignal, scale))
                {
                    OpenTowerPalette();
                }

                return;
            }

            var rect = TowerPalettePanelRect(scale, frame);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), MintSignal);
            if (DrawLauncherButton(launcherRect, "CLOSE", MintSignal, scale))
            {
                isPaletteExpanded = false;
                return;
            }

            titleStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            titleStyle.normal.textColor = MintSignal;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 10f * scale, 120f * scale, 20f * scale), "BUILD", titleStyle);
            if (GUI.Button(new Rect(rect.xMax - 72f * scale, rect.y + 8f * scale, 58f * scale, 32f * scale), "CLOSE", buttonStyle ?? GUI.skin.button))
            {
                isPaletteExpanded = false;
                return;
            }

            var buttonY = rect.y + 78f * scale;
            var buttonHeight = 84f * scale;
            var gap = 8f * scale;
            var buttonWidth = (rect.width - 24f * scale - gap * 2f) / 3f;
            var x = rect.x + 12f * scale;
            var gold = CurrentPlayerGold();

            if (DrawPaletteButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "ARROW", "25G", TowerIconKind.Arrow, ArcaneBlue, gold >= 25, highlightedTowerRole == 0, scale))
            {
                selectedTower = null;
                BeginTowerPlacement();
            }

            x += buttonWidth + gap;
            if (DrawPaletteButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "CTRL", "35G", TowerIconKind.Control, WardViolet, gold >= 35, highlightedTowerRole == 1, scale))
            {
                selectedTower = null;
                BeginControlTowerPlacement();
            }

            x += buttonWidth + gap;
            if (DrawPaletteButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "RELAY", "40G", TowerIconKind.Relay, SignalGold, gold >= 40, highlightedTowerRole == 2, scale))
            {
                selectedTower = null;
                BeginUtilityTowerPlacement();
            }

            var secondRowY = buttonY + buttonHeight + gap;
            var secondRowWidth = (rect.width - 24f * scale - gap) / 2f;
            x = rect.x + 12f * scale;
            if (DrawPaletteButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "PULSE", "45G", TowerIconKind.Pulse, MintSignal, gold >= 45, highlightedTowerRole == 3, scale))
            {
                selectedTower = null;
                BeginPulseTowerPlacement();
            }

            x += secondRowWidth + gap;
            if (DrawPaletteButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), "PRISM", "60G", TowerIconKind.Prism, new Color(0.72f, 0.94f, 1f), gold >= 60, highlightedTowerRole == 4, scale))
            {
                selectedTower = null;
                BeginPrismTowerPlacement();
            }
        }

        private void OpenTowerPalette()
        {
            CloseSendDock();
            isPaletteExpanded = true;
        }

        private void CloseSendDock() => SendDock?.CloseDock();

        private bool IsSendDockExpanded() => SendDock?.IsExpanded == true;

        private SendDockController? SendDock
        {
            get
            {
                if (sendDockController == null)
                {
                    sendDockController = Object.FindAnyObjectByType<SendDockController>();
                }

                return sendDockController;
            }
        }

        private static bool DrawPaletteButton(Rect rect, string label, string meta, TowerIconKind iconKind, Color accent, bool isAffordable, bool isSelected, float scale)
        {
            var displayAccent = isAffordable ? accent : DisabledText;
            var state = isAffordable
                ? isSelected ? CommandCardState.Selected : CommandCardState.Normal
                : CommandCardState.Disabled;
            var style = buttonStyle ?? GUI.skin.button;
            var pressed = RuntimeUiChrome.DrawCommandCard(rect, accent, state, scale);

            var iconRect = RuntimeUiChrome.CommandCardIconRect(rect, scale);
            if (!RuntimeUiIconLibrary.DrawIcon(iconRect, TowerIconResourceName(iconKind), isAffordable))
            {
                DrawTowerIcon(iconRect, iconKind, displayAccent, scale);
            }

            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            buttonStyle.normal.textColor = isAffordable ? Cloud : DisabledText;
            buttonStyle.hover.textColor = buttonStyle.normal.textColor;
            buttonStyle.active.textColor = buttonStyle.normal.textColor;
            GUI.Label(RuntimeUiChrome.CommandCardLabelRect(rect, scale), CompactTowerLabel(label), style);

            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = displayAccent;
            GUI.Label(RuntimeUiChrome.CommandCardMetaRect(rect, scale), meta, metaStyle);
            return pressed;
        }

        private static string CompactTowerLabel(string label)
        {
            return label switch
            {
                "ARROW" => "ARW",
                "RELAY" => "RLY",
                "PULSE" => "PLS",
                "PRISM" => "PRM",
                _ => label
            };
        }

        private static string TowerIconResourceName(TowerIconKind iconKind)
        {
            return iconKind switch
            {
                TowerIconKind.Control => "ui_icon_tower_control_v01",
                TowerIconKind.Relay => "ui_icon_tower_relay_v01",
                TowerIconKind.Pulse => "ui_icon_tower_pulse_v01",
                TowerIconKind.Prism => "ui_icon_tower_prism_v01",
                _ => "ui_icon_tower_arrow_v01"
            };
        }

        private static void DrawTowerIcon(Rect rect, TowerIconKind iconKind, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = accent;

            var cx = rect.x + rect.width * 0.5f;
            var cy = rect.y + rect.height * 0.52f;
            var line = Mathf.Max(2f * scale, 1f);

            switch (iconKind)
            {
                case TowerIconKind.Control:
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy - 5f * scale, 14f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy + 5f * scale, 14f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy - 5f * scale, line, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 5f * scale, cy - 5f * scale, line, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 3f * scale, cy - 1f * scale, 6f * scale, 6f * scale), Texture2D.whiteTexture);
                    break;
                case TowerIconKind.Relay:
                    GUI.DrawTexture(new Rect(cx - line * 0.5f, cy - 11f * scale, line, 22f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 6f * scale, cy + 8f * scale, 12f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 4f * scale, cy - 11f * scale, 8f * scale, 8f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 9f * scale, cy - 1f * scale, 4f * scale, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 5f * scale, cy - 1f * scale, 4f * scale, 12f * scale), Texture2D.whiteTexture);
                    break;
                case TowerIconKind.Pulse:
                    GUI.DrawTexture(new Rect(cx - 10f * scale, cy - 7f * scale, 20f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy + 2f * scale, 14f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 4f * scale, cy - 3f * scale, 8f * scale, 8f * scale), Texture2D.whiteTexture);
                    break;
                case TowerIconKind.Prism:
                    GUI.DrawTexture(new Rect(cx - 3f * scale, cy - 13f * scale, 6f * scale, 24f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 8f * scale, cy - 4f * scale, 16f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 5f * scale, cy + 8f * scale, 10f * scale, line), Texture2D.whiteTexture);
                    break;
                default:
                    GUI.DrawTexture(new Rect(cx - line * 0.5f, cy - 12f * scale, line, 24f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 9f * scale, cy - 6f * scale, line, 15f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 7f * scale, cy - 6f * scale, line, 15f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 5f * scale, cy - 11f * scale, 10f * scale, line), Texture2D.whiteTexture);
                    break;
            }

            GUI.color = previousColor;
        }

        private static bool DrawLauncherButton(Rect rect, string label, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = new Color(PanelInk.r + accent.r * 0.12f, PanelInk.g + accent.g * 0.12f, PanelInk.b + accent.b * 0.12f, PanelInk.a);
            var style = buttonStyle ?? GUI.skin.button;
            var pressed = GUI.Button(rect, GUIContent.none, style);
            GUI.color = previousColor;

            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);
            buttonStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            buttonStyle.normal.textColor = accent;
            GUI.Label(rect, label, style);
            return pressed;
        }

        private string PlacementPreviewText()
        {
            return placementPreview.RejectionReason switch
            {
                CommandRejectionReason.InsufficientGold => "NEED GOLD",
                CommandRejectionReason.CellOccupied => "CELL OCCUPIED",
                CommandRejectionReason.PathBlocked => "PATH BLOCKED",
                CommandRejectionReason.InvalidLane => "OUTSIDE YOUR LINE",
                _ => "CANNOT PLACE"
            };
        }

        private string PlacementRecoveryText()
        {
            return placementPreview.RejectionReason switch
            {
                CommandRejectionReason.InsufficientGold => "Send less or wait for income",
                CommandRejectionReason.CellOccupied => "Pick an empty grid cell",
                CommandRejectionReason.PathBlocked => "Leave a route from spawn to exit",
                CommandRejectionReason.InvalidLane => "Tap inside the highlighted lane",
                _ => "Try a different cell"
            };
        }

        private static void EnsureStyles()
        {
            if (panelStyle is not null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(6, 6, 6, 6),
                margin = ZeroOffset(),
                padding = ZeroOffset()
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = MintSignal }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Cloud }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                margin = ZeroOffset(),
                padding = ZeroOffset(),
                normal = { textColor = Cloud },
                hover = { textColor = Cloud },
                active = { textColor = Cloud }
            };

            metaStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Cloud }
            };
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, panelStyle ?? GUI.skin.box);
            GUI.color = previousColor;
        }

        private static void DrawAccent(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private int SelectedTowerCost()
        {
            return selectedTowerRole switch
            {
                1 => 35,
                2 => 40,
                3 => 45,
                4 => 60,
                _ => 25
            };
        }

        private int CurrentPlayerGold()
        {
            if (commandAdapter == null)
            {
                commandAdapter = Object.FindAnyObjectByType<UnityCommandAdapter>();
            }

            return commandAdapter?.CurrentPlayerGold() ?? 0;
        }

        private static string TowerPurpose(string towerId)
        {
            if (towerId.Contains("control")) return "Area control and clustered pressure";
            if (towerId.Contains("relay") || towerId.Contains("economy")) return "Utility pressure and income support";
            if (towerId.Contains("pulse")) return "Short-range burst against dense pressure";
            if (towerId.Contains("prism")) return "Long-range focus against priority pressure";
            return "Focused single-target defense";
        }

        private static RectOffset ZeroOffset() => new RectOffset(0, 0, 0, 0);

        private enum TowerIconKind
        {
            Arrow,
            Control,
            Relay,
            Pulse,
            Prism
        }

        private bool IsSelectedCellInBounds() =>
            selectedCell.x >= 0 && selectedCell.x < LaneWidth && selectedCell.y >= 0 && selectedCell.y < LaneLength;

        private bool IsPointerOverRuntimeUi(Vector2 screenPosition)
        {
            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var guiPoint = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            if (TowerPaletteLauncherRect(scale, frame).Contains(guiPoint))
            {
                return true;
            }

            if (isPaletteExpanded && TowerPalettePanelRect(scale, frame).Contains(guiPoint))
            {
                return true;
            }

            if (isPlacing && PlacementPanelRect(scale, frame).Contains(guiPoint))
            {
                return true;
            }

            return selectedTower is not null && SelectedTowerPanelRect(scale, frame).Contains(guiPoint);
        }

        private void RefreshSelectedTowerFromSnapshot()
        {
            if (selectedTower is null)
            {
                return;
            }

            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }

            var snapshot = simulationDriver?.LatestSnapshot;
            var current = snapshot?.Towers.FirstOrDefault(tower => tower.EntityId.Equals(selectedTower.EntityId));
            if (current is null)
            {
                selectedTower = null;
                HideSelectionRing();
                return;
            }

            selectedTower = current;
        }

        private static Rect TowerPaletteLauncherRect(float scale, Rect frame)
        {
            var launcherSize = 56f * scale;
            return new Rect(frame.x + 12f * scale, frame.yMax - launcherSize - MobileViewportLayout.BottomMargin(scale), launcherSize, launcherSize);
        }

        private static Rect TowerPalettePanelRect(float scale, Rect frame)
        {
            var width = Mathf.Min(frame.width - 16f * scale, 430f * scale);
            var height = 282f * scale;
            var launcherClearance = 136f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale) - launcherClearance, width, height);
        }

        private static Rect PlacementPanelRect(float scale, Rect frame)
        {
            var width = Mathf.Min(frame.width - 16f * scale, 360f * scale);
            var height = 92f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale), width, height);
        }

        private static Rect SelectedTowerPanelRect(float scale, Rect frame)
        {
            var width = Mathf.Min(frame.width - 16f * scale, 360f * scale);
            var height = 112f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - 112f * scale, width, height);
        }
    }
}
