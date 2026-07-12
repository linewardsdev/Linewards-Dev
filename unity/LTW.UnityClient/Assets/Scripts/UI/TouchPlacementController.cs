using LTW.Simulation.Bridge;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class TouchPlacementController : MonoBehaviour
    {
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

        [SerializeField]
        private Camera inputCamera = null!;

        [SerializeField]
        private UnityCommandAdapter commandAdapter = null!;

        [SerializeField]
        private PlacementFeedbackView feedbackView = null!;

        [SerializeField]
        private GameObject ghost = null!;

        [SerializeField]
        private bool showPlacementReadout = true;

        private bool isPlacing;
        private int selectedTowerRole;
        private Vector2Int selectedCell;
        private bool selectedCellIsOnBoard;

        public void BeginTowerPlacement() => BeginTowerPlacement(0);

        public void BeginControlTowerPlacement() => BeginTowerPlacement(1);

        public void BeginUtilityTowerPlacement() => BeginTowerPlacement(2);

        private void BeginTowerPlacement(int towerRole)
        {
            isPlacing = true;
            selectedTowerRole = towerRole;
            selectedCell = new Vector2Int(1, 3);
            selectedCellIsOnBoard = true;
            ghost.SetActive(true);
            MoveGhost();
            feedbackView.Clear();
        }

        public void NudgeUp() => Nudge(Vector2Int.up);

        public void NudgeDown() => Nudge(Vector2Int.down);

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
            UpdateGhostColor();
        }

        public void SellLastTower()
        {
            var result = commandAdapter.SellLastSampleTower();
            if (result.Accepted)
            {
                feedbackView.ShowEconomy("Tower sold");
                return;
            }

            feedbackView.ShowRejected(result.RejectionReason);
        }

        private void Update()
        {
            if (!isPlacing || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            var ray = inputCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var distance))
            {
                return;
            }

            var hit = ray.GetPoint(distance);
            selectedCell = new Vector2Int(Mathf.RoundToInt(hit.x), Mathf.RoundToInt(hit.z));
            MoveGhost();
        }

        private void OnGUI()
        {
            if (!showPlacementReadout || !isPlacing)
            {
                return;
            }

            EnsureStyles();

            var scale = Mathf.Clamp(Screen.width / 1080f, 0.72f, 1.15f);
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
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 35f * scale, rect.width - 24f * scale, 20f * scale), selectedCellIsOnBoard ? $"CELL {selectedCell.x}, {selectedCell.y}" : "OUTSIDE YOUR LINE", bodyStyle);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 55f * scale, rect.width - 24f * scale, 20f * scale), selectedCellIsOnBoard ? "Tap board or nudge, then confirm" : "Tap inside the highlighted lane", bodyStyle);
        }

        private void Nudge(Vector2Int delta)
        {
            if (!isPlacing)
            {
                return;
            }

            selectedCell += delta;
            selectedCell.x = Mathf.Clamp(selectedCell.x, 0, 11);
            selectedCell.y = Mathf.Clamp(selectedCell.y, 0, 8);
            MoveGhost();
        }

        private void MoveGhost()
        {
            ghost.transform.position = new Vector3(selectedCell.x, 0.6f, selectedCell.y);
            ghost.transform.localScale = selectedTowerRole switch
            {
                1 => new Vector3(0.82f, 0.46f, 0.82f),
                2 => new Vector3(0.52f, 0.52f, 0.52f),
                _ => new Vector3(0.62f, 0.78f, 0.62f)
            };
            selectedCellIsOnBoard = IsOwnLaneCell(selectedCell);
            UpdateGhostColor();
        }

        private void UpdateGhostColor()
        {
            var renderer = ghost.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var color = selectedCellIsOnBoard ? SelectedTowerAccent() : Danger;
            color.a = 0.72f;
            renderer.material.color = color;
        }

        private static bool IsOwnLaneCell(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < 12 && cell.y >= 0 && cell.y < 9;
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

        private Color SelectedTowerAccent()
        {
            return selectedTowerRole switch
            {
                1 => WardViolet,
                2 => SignalGold,
                _ => ArcaneBlue
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
    }
}
