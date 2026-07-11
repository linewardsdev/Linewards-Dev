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
        private Vector2Int selectedCell;

        public void BeginTowerPlacement()
        {
            isPlacing = true;
            selectedCell = Vector2Int.zero;
            ghost.SetActive(true);
            MoveGhost();
            feedbackView.Clear();
        }

        public void NudgeUp() => Nudge(Vector2Int.up);

        public void NudgeDown() => Nudge(Vector2Int.down);

        public void NudgeLeft() => Nudge(Vector2Int.left);

        public void NudgeRight() => Nudge(Vector2Int.right);

        public void CancelPlacement()
        {
            isPlacing = false;
            ghost.SetActive(false);
            feedbackView.Clear();
        }

        public void ConfirmPlacement()
        {
            if (!isPlacing)
            {
                return;
            }

            var result = commandAdapter.PlaceSampleTower(selectedCell.x, selectedCell.y);
            if (result.Accepted)
            {
                feedbackView.ShowAccepted();
                CancelPlacement();
                return;
            }

            feedbackView.ShowRejected(result.RejectionReason);
        }

        public void SellLastTower()
        {
            var result = commandAdapter.SellLastSampleTower();
            if (result.Accepted)
            {
                feedbackView.Clear();
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
        }
    }
}
