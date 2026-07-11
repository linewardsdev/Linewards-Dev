using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class ViewSwapController : MonoBehaviour
    {
        [SerializeField]
        private Camera laneCamera = null!;

        [SerializeField]
        private Transform ownLaneView = null!;

        [SerializeField]
        private Transform targetLaneView = null!;

        private bool showingTarget;

        public void ToggleTargetLane()
        {
            showingTarget = !showingTarget;
            laneCamera.transform.SetPositionAndRotation(
                showingTarget ? targetLaneView.position : ownLaneView.position,
                showingTarget ? targetLaneView.rotation : ownLaneView.rotation);
        }

        public void ShowOwnLane()
        {
            showingTarget = false;
            laneCamera.transform.SetPositionAndRotation(ownLaneView.position, ownLaneView.rotation);
        }
    }
}
