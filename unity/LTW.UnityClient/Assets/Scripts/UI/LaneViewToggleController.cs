#nullable enable

using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class LaneViewToggleController : MonoBehaviour
    {
        [SerializeField]
        private UnityVerticalSliceRenderer renderer = null!;

        [SerializeField]
        private bool showRuntimeToggle;

        public bool IsShowingMap => renderer != null && renderer.CameraFraming == LaneCameraFraming.AllLanes;

        public string NextViewLabel => IsShowingMap ? "LANE" : "MAP";

        public void Initialize(UnityVerticalSliceRenderer presentationRenderer)
        {
            renderer = presentationRenderer;
            ShowLaneView();
        }

        public void ToggleView()
        {
            if (IsShowingMap)
            {
                ShowLaneView();
                return;
            }

            ShowMapView();
        }

        public void ShowLaneView()
        {
            renderer?.SetCameraFraming(LaneCameraFraming.ActiveLane);
        }

        public void ShowMapView()
        {
            renderer?.SetCameraFraming(LaneCameraFraming.AllLanes);
        }

        private void OnGUI()
        {
            if (!showRuntimeToggle || renderer == null)
            {
                return;
            }
        }

    }
}
