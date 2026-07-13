#nullable enable

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
        private const int LaneLength = 18;

        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color WardViolet = new(0.608f, 0.424f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);

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
        private PlacementFeedbackView feedbackView = null!;

        [SerializeField]
        private GameObject ghost = null!;

        [SerializeField]
        private bool showPlacementReadout = true;

        private GameObject selectionRing = null!;

        private bool isPlacing;
        private bool isPaletteExpanded;
        private int selectedTowerRole;
        private Vector2Int selectedCell;
        private TowerCombatState? selectedTower;
        private VerticalSliceCommandResult placementPreview = VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);

        public bool IsPlacing => isPlacing;

        public void Initialize(Camera camera, UnityCommandAdapter adapter, PlacementFeedbackView feedback, GameObject placementGhost)
        {
            inputCamera = camera;
            commandAdapter = adapter;
            feedbackView = feedback;
            ghost = placementGhost;
        }

        public void BeginTowerPlacement() => BeginTowerPlacement(0);

        public void BeginControlTowerPlacement() => BeginTowerPlacement(1);

        public void BeginUtilityTowerPlacement() => BeginTowerPlacement(2);

        private void BeginTowerPlacement(int towerRole)
        {
            isPlacing = true;
            isPaletteExpanded = false;
            selectedTowerRole = towerRole;
            selectedCell = new Vector2Int(2, 2);
            ghost.SetActive(true);
            MoveGhost();
            selectedTower = null;
            HideSelectionRing();
            feedbackView.Clear();
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
                _ => commandAdapter.PlaceSampleTower(selectedCell.x, selectedCell.y)
            };
            if (result.Accepted)
            {
                feedbackView.ShowAccepted(SelectedTowerName() + " placed");
                CancelPlacement(false);
                return;
            }

            feedbackView.ShowRejected(result.RejectionReason);
            RefreshPlacementPreview();
        }

        public void SellLastTower()
        {
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
            selectedCell = new Vector2Int(Mathf.RoundToInt(hit.x), 17 - Mathf.RoundToInt(hit.z));
            if (isPlacing)
            {
                MoveGhost();
                return;
            }

            SelectTowerAt(selectedCell);
        }

        private void OnGUI()
        {
            if (!showPlacementReadout)
            {
                return;
            }

            EnsureStyles();

            var scale = Mathf.Clamp(Screen.width / 1080f, 0.72f, 1.15f);
            DrawTowerPalette(scale);
            DrawSelectedTowerPanel(scale);

            if (!isPlacing)
            {
                return;
            }

            var width = Mathf.Min(Screen.width - 32f * scale, 330f * scale);
            var height = 86f * scale;
            var rect = new Rect(12f * scale, Screen.height - height - 18f * scale, width, height);
            var accent = SelectedTowerAccent();

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            titleStyle!.fontSize = Mathf.RoundToInt(17f * scale);
            titleStyle.normal.textColor = accent;
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            bodyStyle.normal.textColor = Cloud;

            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 9f * scale, rect.width - 24f * scale, 24f * scale), SelectedTowerName().ToUpperInvariant(), titleStyle);
            var placementLine = placementPreview.Accepted ? $"CELL {selectedCell.x}, {selectedCell.y} READY" : PlacementPreviewText();
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 35f * scale, rect.width - 24f * scale, 20f * scale), placementLine, bodyStyle);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 55f * scale, rect.width - 24f * scale, 20f * scale), placementPreview.Accepted ? "Tap board or nudge, then confirm" : PlacementRecoveryText(), bodyStyle);
        }

        private void SelectTowerAt(Vector2Int cell)
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
                return;
            }

            foreach (var tower in snapshot.Towers)
            {
                if (tower.OwnerId.Value == 1 && tower.LaneId.Value == 1 && tower.Position.X == cell.x && tower.Position.Y == cell.y)
                {
                    selectedTower = tower;
                    UpdateSelectionRing(tower);
                    feedbackView.ShowAccepted(TowerRoleName(tower.TowerId.Value) + " selected");
                    return;
                }
            }
        }

        private void DrawSelectedTowerPanel(float scale)
        {
            if (isPlacing || selectedTower is null)
            {
                return;
            }

            var width = Mathf.Min(Screen.width - 32f * scale, 330f * scale);
            var height = 96f * scale;
            var rect = new Rect(12f * scale, Screen.height - height - 178f * scale, width, height);
            var accent = TowerAccent(selectedTower.TowerId.Value);
            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            titleStyle!.fontSize = Mathf.RoundToInt(16f * scale);
            titleStyle.normal.textColor = accent;
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            bodyStyle.normal.textColor = Cloud;

            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 8f * scale, rect.width - 24f * scale, 22f * scale), TowerRoleName(selectedTower.TowerId.Value).ToUpperInvariant(), titleStyle);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 32f * scale, rect.width - 24f * scale, 18f * scale), $"CELL {selectedTower.Position.X}, {selectedTower.Position.Y}  OWNER P{selectedTower.OwnerId.Value}", bodyStyle);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 51f * scale, rect.width - 24f * scale, 18f * scale), "Sell selected or tap another tower", bodyStyle);

            if (GUI.Button(new Rect(rect.x + rect.width - 86f * scale, rect.y + 28f * scale, 70f * scale, 36f * scale), "SELL", buttonStyle ?? GUI.skin.button))
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
            selectedCell.x = Mathf.Clamp(selectedCell.x, 0, LaneWidth - 1);
            selectedCell.y = Mathf.Clamp(selectedCell.y, 0, LaneLength - 1);
            MoveGhost();
        }

        private void MoveGhost()
        {
            ghost.transform.position = new Vector3(selectedCell.x, 0.6f, 17 - selectedCell.y);
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
            var isArrow = roleId == "arrow";

            ConfigureGhostChild("GhostBase", true, new Vector3(0f, -0.28f, 0f), isControl ? new Vector3(1.18f, 0.06f, 1.18f) : isRelay ? new Vector3(0.78f, 0.06f, 0.78f) : new Vector3(0.72f, 0.06f, 0.72f), accent);
            ConfigureGhostChild("GhostArrowSpire", isArrow, new Vector3(0f, 0.48f, 0f), new Vector3(0.14f, 0.92f, 0.14f), accent);
            ConfigureGhostChild("GhostArrowBowLeft", isArrow, new Vector3(-0.26f, 0.36f, 0f), new Vector3(0.1f, 0.62f, 0.12f), accent);
            ConfigureGhostChild("GhostArrowBowRight", isArrow, new Vector3(0.26f, 0.36f, 0f), new Vector3(0.1f, 0.62f, 0.12f), accent);
            ConfigureGhostChild("GhostControlRing", isControl, new Vector3(0f, 0.1f, 0f), new Vector3(1.36f, 0.04f, 1.36f), accent);
            ConfigureGhostChild("GhostControlCore", isControl, new Vector3(0f, 0.42f, 0f), new Vector3(0.34f, 0.34f, 0.34f), accent);
            ConfigureGhostChild("GhostRelayMast", isRelay, new Vector3(0f, 0.52f, 0f), new Vector3(0.1f, 1.02f, 0.1f), accent);
            ConfigureGhostChild("GhostRelaySignal", isRelay, new Vector3(0f, 1.08f, 0f), new Vector3(0.5f, 0.04f, 0.5f), accent);
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
            selectionRing.transform.position = new Vector3(tower.Position.X, 0.06f, 17 - tower.Position.Y);
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
                _ => new Vector3(0.62f, 0.78f, 0.62f)
            };
        }

        private string SelectedTowerRoleId()
        {
            return selectedTowerRole switch
            {
                1 => "control",
                2 => "relay",
                _ => "arrow"
            };
        }

        private static Vector3 TowerSelectionRingScale(string towerId)
        {
            if (towerId.Contains("control")) return new Vector3(1.42f, 0.03f, 1.42f);
            if (towerId.Contains("relay") || towerId.Contains("economy")) return new Vector3(1.18f, 0.03f, 1.18f);
            return new Vector3(1.28f, 0.03f, 1.28f);
        }

        private string SelectedTowerName()
        {
            return selectedTowerRole switch
            {
                1 => "Control ward",
                2 => "Relay ward",
                _ => "Arrow ward"
            };
        }

        private static string TowerRoleName(string towerId)
        {
            if (towerId.Contains("control")) return "Control ward";
            if (towerId.Contains("relay") || towerId.Contains("economy")) return "Relay ward";
            return "Arrow ward";
        }

        private static Color TowerAccent(string towerId)
        {
            if (towerId.Contains("control")) return WardViolet;
            if (towerId.Contains("relay") || towerId.Contains("economy")) return SignalGold;
            return ArcaneBlue;
        }

        private Color SelectedTowerAccent()
        {
            return selectedTowerRole switch
            {
                1 => WardViolet,
                2 => SignalGold,
                _ => ArcaneBlue
            };
        }

        private void DrawTowerPalette(float scale)
        {
            if (isPlacing)
            {
                return;
            }

            var launcherSize = 58f * scale;
            var launcherRect = new Rect(12f * scale, Screen.height - launcherSize - 18f * scale, launcherSize, launcherSize);
            if (!isPaletteExpanded)
            {
                if (DrawLauncherButton(launcherRect, "BUILD", MintSignal, scale))
                {
                    isPaletteExpanded = true;
                }

                return;
            }

            var width = Mathf.Min(Screen.width - 32f * scale, 390f * scale);
            var height = 142f * scale;
            var rect = new Rect(12f * scale, Screen.height - height - 18f * scale, width, height);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), MintSignal);
            if (DrawLauncherButton(launcherRect, "CLOSE", MintSignal, scale))
            {
                isPaletteExpanded = false;
                return;
            }

            titleStyle!.fontSize = Mathf.RoundToInt(14f * scale);
            titleStyle.normal.textColor = MintSignal;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 8f * scale, rect.width - 72f * scale, 22f * scale), "WARD PALETTE", titleStyle);
            if (GUI.Button(new Rect(rect.xMax - 58f * scale, rect.y + 8f * scale, 44f * scale, 28f * scale), "CLOSE", buttonStyle ?? GUI.skin.button))
            {
                isPaletteExpanded = false;
                return;
            }

            var buttonY = rect.y + 38f * scale;
            var buttonHeight = 74f * scale;
            var gap = 8f * scale;
            var buttonWidth = (rect.width - 24f * scale - gap * 3f) / 4f;
            var x = rect.x + 12f * scale;

            if (DrawPaletteButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "ARROW", "25g", ArcaneBlue, scale))
            {
                selectedTower = null;
                BeginTowerPlacement();
            }

            x += buttonWidth + gap;
            if (DrawPaletteButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "CONTROL", "35g", WardViolet, scale))
            {
                selectedTower = null;
                BeginControlTowerPlacement();
            }

            x += buttonWidth + gap;
            if (DrawPaletteButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "RELAY", "40g", SignalGold, scale))
            {
                selectedTower = null;
                BeginUtilityTowerPlacement();
            }

            x += buttonWidth + gap;
            if (DrawPaletteButton(new Rect(x, buttonY, buttonWidth, buttonHeight), "SELL", "refund", Danger, scale))
            {
                SellLastTower();
                isPaletteExpanded = false;
            }
        }

        private static bool DrawPaletteButton(Rect rect, string label, string meta, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = new Color(PanelInk.r + accent.r * 0.06f, PanelInk.g + accent.g * 0.06f, PanelInk.b + accent.b * 0.06f, PanelInk.a);
            var style = buttonStyle ?? GUI.skin.button;
            var pressed = GUI.Button(rect, GUIContent.none, style);
            GUI.color = previousColor;

            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            buttonStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            buttonStyle.normal.textColor = Cloud;
            GUI.Label(new Rect(rect.x, rect.y + 12f * scale, rect.width, 24f * scale), label, style);

            metaStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            metaStyle.normal.textColor = accent;
            GUI.Label(new Rect(rect.x, rect.y + 39f * scale, rect.width, 18f * scale), meta, metaStyle);
            DrawAccent(new Rect(rect.x + rect.width * 0.28f, rect.y + 61f * scale, rect.width * 0.44f, 3f * scale), accent);
            return pressed;
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

        private static RectOffset ZeroOffset() => new RectOffset(0, 0, 0, 0);

        private bool IsSelectedCellInBounds() =>
            selectedCell.x >= 0 && selectedCell.x < LaneWidth && selectedCell.y >= 0 && selectedCell.y < LaneLength;
    }
}
