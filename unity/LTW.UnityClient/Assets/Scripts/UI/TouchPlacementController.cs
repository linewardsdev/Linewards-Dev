using LTW.Simulation.Bridge;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class TouchPlacementController : MonoBehaviour
    {
        [SerializeField]
        private Camera inputCamera = null!;

        [SerializeField]
        private UnityCommandAdapter commandAdapter = null!;

        [SerializeField]
        private PlacementFeedbackView feedbackView = null!;

        [SerializeField]
        private GameObject ghost = null!;

        private bool isPlacing;
        private int selectedTowerRole;
        private Vector2Int selectedCell;

        public void BeginTowerPlacement() => BeginTowerPlacement(0);

        public void BeginControlTowerPlacement() => BeginTowerPlacement(1);

        public void BeginUtilityTowerPlacement() => BeginTowerPlacement(2);

        private void BeginTowerPlacement(int towerRole)
        {
            isPlacing = true;
            selectedTowerRole = towerRole;
            selectedCell = Vector2Int.zero;
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

        private void Nudge(Vector2Int delta)
        {
            if (!isPlacing)
            {
                return;
            }

            selectedCell += delta;
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
    }
}
