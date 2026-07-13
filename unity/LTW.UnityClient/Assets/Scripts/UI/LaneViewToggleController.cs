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

        private bool showingMap;

        public bool IsShowingMap => showingMap;

        public string NextViewLabel => showingMap ? "LANE" : "MAP";

        public void Initialize(UnityVerticalSliceRenderer presentationRenderer)
        {
            renderer = presentationRenderer;
            showingMap = false;
            ApplyCurrentView();
        }

        public void ToggleView()
        {
            showingMap = !showingMap;
            ApplyCurrentView();
        }

        public void ShowLaneView()
        {
            showingMap = false;
            ApplyCurrentView();
        }

        public void ShowMapView()
        {
            showingMap = true;
            ApplyCurrentView();
        }

        private void OnGUI()
        {
            if (!showRuntimeToggle || renderer == null)
            {
                return;
            }
        }

        private void ApplyCurrentView()
        {
            if (renderer == null)
            {
                return;
            }

            renderer.SetCameraFraming(showingMap ? LaneCameraFraming.AllLanes : LaneCameraFraming.ActiveLane);
        }
    }
}
