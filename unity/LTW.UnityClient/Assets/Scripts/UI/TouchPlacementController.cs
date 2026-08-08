#nullable enable

using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Combat;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed partial class TouchPlacementController : MonoBehaviour
    {
        private const int LaneWidth = 7;
        private const int LaneLength = 16;

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

        [SerializeField]
        private bool showPlacementReadout = true;

        private bool isPlacing;
        private bool isPaletteExpanded;
        private int selectedTowerRole;
        private int lastSelectedTowerRole;

        private Vector2Int selectedCell;

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
            MoveGhost();
            selectedTower = null;
            HideSelectionRing();
            feedbackView.Clear();
        }

        public void CloseBottomPanelsForSend()
        {
            isPaletteExpanded = false;
            selectedTower = null;

            // Multi-select ends here too. Its RAISE and SELL buttons live in the launcher strip the
            // dock covers, so leaving the mode on while sending left it active but unreachable —
            // board taps kept toggling towers into a batch the player could not see or act on.
            SetMultiSelectMode(false);
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

            var result = commandAdapter.PlaceTowerByRole(selectedTowerRole, selectedCell.x, selectedCell.y);
            if (result.Accepted)
            {
                feedbackView.ShowAccepted(SelectedTowerName() + " placed");
                lastSelectedTowerRole = selectedTowerRole;
                // Keep placement active with the selected tower so the compact tower-specific panel
                // remains available. Players can tap ALL there to swap tower types.
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
            // Everything this component offers is gone once the seat is out. Placement, selling and
            // upgrading are all refused by the simulation for an eliminated player, and the lane the
            // taps would land on has been wiped — so a board tap could only move a cursor around an
            // empty grid or arm a batch that can never be acted on.
            //
            // The teardown lives HERE rather than in OnGUI, which is where it was first written.
            // OnGUI does not run in batchmode, so a state rule enforced from a draw callback cannot
            // be checked by EliminatedSeatCheck — it failed exactly that way, and the panels really
            // did stay open in a headless run. A rule about what state is legal belongs on the frame
            // tick; OnGUI's job is only to not draw it.
            //
            // Ahead of TickBuilderWalk, so the builder goes with the controls: it is the world-space
            // half of the build affordance, and leaving it pacing a lane that has just been cleared
            // of everything else is the same lie the live BUILD button was.
            if (IsLocalSeatEliminated)
            {
                CloseBottomPanelsForSend();
                CloseSendDock();
                HideBuilderAvatar();
                return;
            }

            TickBuilderWalk();

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
                inputCamera = Camera.main;
            }

            // No camera tagged MainCamera yet (e.g. a scene still loading) — skip this tap rather
            // than dereference null, matching how UnityVerticalSliceRenderer.ConfigureDefaultCamera
            // handles the same case (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items).
            if (inputCamera == null)
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
            if (!isMultiSelectMode)
            {
                HideSelectionRing();
            }

            selectedCell = hitCell;
            UpdateBuilderAvatar();
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

        private void RefreshPlacementPreview()
        {
            if (!IsSelectedCellInBounds())
            {
                placementPreview = VerticalSliceCommandResult.Reject(CommandRejectionReason.InvalidLane);
                UpdateGhostColor();
                return;
            }

            placementPreview = commandAdapter.PreviewTowerByRole(selectedTowerRole, selectedCell.x, selectedCell.y);

            UpdateGhostColor();
        }

        private void OpenTowerPalette()
        {
            CloseSendDock();
            // Symmetric with CloseBottomPanelsForSend. Today the BUILD button is not even drawn
            // while multi-select is on — RAISE occupies its slot — so this cannot currently be
            // reached in that state. It is here so that stops being load-bearing: any future route
            // into the palette ends the mode rather than leaving it live under a panel.
            SetMultiSelectMode(false);
            isPaletteExpanded = true;
            selectedTowerCategory = -1;
        }

        private void CloseSendDock() => SendDock?.CloseDock();

        private bool IsSendDockExpanded() => SendDock?.IsExpanded == true;

        /// <summary>
        /// Whether the seat this client drives is out of the match.
        /// </summary>
        /// <remarks>
        /// Resolves the driver lazily, the same way <see cref="SendDock"/> and
        /// <c>SelectTowerAt</c> already do, because this component is wired up by
        /// UnityMatchBootstrapper in some scenes and found by type in others.
        /// </remarks>
        private bool IsLocalSeatEliminated
        {
            get
            {
                if (simulationDriver == null)
                {
                    simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
                }

                return simulationDriver != null && simulationDriver.IsLocalSeatEliminated;
            }
        }

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

        private int SelectedTowerCost()
        {
            // Read from the simulation's catalog. These used to be literals here and in the
            // palette buttons, and both had drifted from what the simulation actually charges.
            return commandAdapter != null ? commandAdapter.TowerCost(selectedTowerRole) : 0;
        }

        private int CurrentPlayerGold()
        {
            if (commandAdapter == null)
            {
                commandAdapter = Object.FindAnyObjectByType<UnityCommandAdapter>();
            }

            return commandAdapter?.CurrentPlayerGold() ?? 0;
        }

        /// <summary>The line this seat has committed to, or -1 while every line is still open.</summary>
        /// <remarks>
        /// Defaults to -1 rather than 0 when there is no adapter: with no simulation to ask, showing
        /// every category as available is the honest answer, and it also keeps the palette usable in
        /// the scene view where no match is running.
        /// </remarks>
        private int CurrentPlayerTowerLine()
        {
            if (commandAdapter == null)
            {
                commandAdapter = Object.FindAnyObjectByType<UnityCommandAdapter>();
            }

            return commandAdapter?.CurrentPlayerTowerLine() ?? -1;
        }

        private bool IsSelectedCellInBounds() =>
            selectedCell.x >= 0 && selectedCell.x < LaneWidth && selectedCell.y >= 0 && selectedCell.y < LaneLength;
    }
}

