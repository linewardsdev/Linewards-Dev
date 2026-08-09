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
    /// Presentation camera: framing modes, the active-lane selection, and the tilt/zoom
    /// configuration reapplied every frame.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        private Camera presentationCamera = null!;

        public LaneCameraFraming CameraFraming => cameraFraming;

        public int ActiveLaneCameraId => Mathf.Clamp(activeLaneCameraId, 1, LaneCount);

        public void SetPresentationCamera(Camera camera)
        {
            presentationCamera = camera;
            ConfigureDefaultCamera();
        }

        public void ToggleCameraFraming()
        {
            SetActiveLaneCameraId(ActiveLaneCameraId % LaneCount + 1);
        }

        public void SetCameraFraming(LaneCameraFraming framing)
        {
            if (cameraFraming != framing)
            {
                Debug.Log($"LTW camera framing -> {framing}");
            }

            cameraFraming = framing;
            ConfigureDefaultCamera();
        }

        public void SetActiveLaneCameraId(int laneId)
        {
            var nextLane = Mathf.Clamp(laneId, 1, LaneCount);
            if (activeLaneCameraId != nextLane || cameraFraming != LaneCameraFraming.ActiveLane)
            {
                Debug.Log($"LTW active lane camera -> {nextLane}");
            }

            activeLaneCameraId = nextLane;
            cameraFraming = LaneCameraFraming.ActiveLane;
            ConfigureDefaultCamera();
        }

        /// <summary>
        /// Re-applies just the viewport rect, every frame.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="ConfigureDefaultCamera"/>, which runs only when the framing or
        /// lane changes and therefore cannot see a drawer opening. Only the rect is touched: the
        /// position, tilt and orthographic size are unchanged by a drawer, and recomputing them
        /// every frame would re-run a LookAt for nothing.
        /// </remarks>
        private void RefreshCameraViewport()
        {
            if (cameraFraming != LaneCameraFraming.ActiveLane)
            {
                return;
            }

            var camera = presentationCamera != null ? presentationCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            var desired = MobileViewportLayout.CameraRect();
            if (camera.rect != desired)
            {
                camera.rect = desired;
            }
        }

        private void ConfigureDefaultCamera()
        {
            var camera = presentationCamera != null ? presentationCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            var clampedLane = Mathf.Clamp(activeLaneCameraId, 1, LaneCount);
            var boardCenter = cameraFraming switch
            {
                LaneCameraFraming.AllLanes => AllLaneCenter(),
                LaneCameraFraming.BoardOverview => LaneCenter(clampedLane),
                LaneCameraFraming.SpawnGateFocus => GridToWorld(new GridPosition(CenterColumn, 0), new LaneId(clampedLane)) + new Vector3(0f, 0f, -1.15f),
                LaneCameraFraming.LeakGateFocus => GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(clampedLane)) + new Vector3(0f, 0f, 1.15f),
                _ => LaneCenter(clampedLane)
            };
            var isActiveLaneFraming = cameraFraming == LaneCameraFraming.ActiveLane;
            var defaultTilt = isActiveLaneFraming ? DefaultActiveLaneTiltDegrees : DefaultOverviewTiltDegrees;
            var tiltDegrees = CameraTiltDegrees > 0f ? CameraTiltDegrees : defaultTilt;
            var tiltRadians = tiltDegrees * Mathf.Deg2Rad;

            // The board lies flat, so its on-screen length shrinks by cos(tilt). Without
            // compensating the orthographic size, tilting the camera just adds dead space above and
            // below the board instead of showing more of the units. Scaling the size by the same
            // factor keeps the board filling the frame exactly as it did at the default tilt, and
            // has the side effect of making units larger on screen at steeper angles.
            var calibrationTilt = isActiveLaneFraming
                ? ActiveLaneZoomCalibrationTiltDegrees
                : OverviewZoomCalibrationTiltDegrees;
            var tiltZoom = Mathf.Cos(tiltRadians) / Mathf.Cos(calibrationTilt * Mathf.Deg2Rad);

            camera.orthographic = true;
            camera.orthographicSize = tiltZoom * cameraFraming switch
            {
                LaneCameraFraming.AllLanes => 34f,
                LaneCameraFraming.BoardOverview => 8.2f,
                LaneCameraFraming.SpawnGateFocus => 3.05f,
                LaneCameraFraming.LeakGateFocus => 3.05f,
                _ => 9.2f
            };
            camera.rect = cameraFraming == LaneCameraFraming.ActiveLane
                ? MobileViewportLayout.CameraRect()
                : new Rect(0f, 0f, 1f, 1f);
            // Expressed as a tilt off vertical rather than a height/offset pair, because the tilt
            // is what actually governs how much of a unit's vertical form and vertical motion
            // survives projection: only sin(tilt) of it reaches the screen. At the original 19.5
            // degrees that is 33%, which is why animation reads so weakly from this view. The
            // camera is orthographic, so the orbit radius affects only the angle, never the zoom.
            var radius = isActiveLaneFraming ? 18.57f : 18.44f;
            camera.transform.position = boardCenter + new Vector3(
                0f,
                radius * Mathf.Cos(tiltRadians),
                -radius * Mathf.Sin(tiltRadians));
            camera.transform.LookAt(boardCenter);
        }

        /// <summary>
        /// Tilt off vertical for the gameplay camera.
        /// </summary>
        /// <remarks>
        /// Was 19.5 (active lane) / 18.3 (overview), which is close enough to straight down that
        /// only sin(19.5) = 33% of any vertical motion survived projection — the reason procedural
        /// animation read as almost nothing from this view no matter how far its magnitudes were
        /// pushed. At 30 degrees that rises to 50%, and units read as sculpted objects rather than
        /// flat discs, while grid cells stay square enough to tap accurately on a phone. Steeper
        /// angles were captured and compared: 40 gave more dimensionality but visibly squashed the
        /// build slots and increased creep-on-creep occlusion down the lane.
        /// The zoom compensation below keeps the board framed identically at any of these values.
        /// </remarks>
        private const float DefaultActiveLaneTiltDegrees = 30f;
        private const float DefaultOverviewTiltDegrees = 30f;

        /// <summary>
        /// The tilt the orthographicSize values above were originally hand-tuned against. Zoom
        /// compensation is measured from here, not from the current default — otherwise raising the
        /// default would cancel its own compensation and put the dead space straight back.
        /// </summary>
        private const float ActiveLaneZoomCalibrationTiltDegrees = 19.5f;
        private const float OverviewZoomCalibrationTiltDegrees = 18.3f;

        /// <summary>
        /// Command-line override (-ltwCameraTilt &lt;degrees&gt;) used to A/B the camera angle from
        /// headless captures without editing the default.
        /// </summary>
        private static readonly float CameraTiltDegrees = ResolveCameraTiltOverride();

        private static float ResolveCameraTiltOverride()
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == "-ltwCameraTilt" && float.TryParse(args[index + 1], out var degrees))
                {
                    return degrees;
                }
            }

            return -1f;
        }
    }
}
