#nullable enable

using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    /// <summary>
    /// A private off-board stage that turns one unit in front of a camera and renders it to a
    /// texture the codex screen can display.
    /// </summary>
    /// <remarks>
    /// The runtime half of <c>LTW.UnityClient.Editor.StylizedUnitPreviewCapture</c>, which has been
    /// building exactly this stage for review captures since the URP migration. Everything in its
    /// <c>BuildStage</c> is runtime-legal; only the two asset-loading calls are editor-only, and
    /// both have a Resources equivalent already used elsewhere in the client.
    ///
    /// ISOLATION IS BY DISTANCE, NOT BY LAYER. The stage sits a thousand units under the board. The
    /// presentation camera is orthographic at size 15.5 with an 80-unit far plane
    /// (<c>LocalVerticalSliceLauncher.CreateCamera</c>), so it cannot see down here, and this
    /// camera's own far plane cannot see back up. That is worth one sentence of explanation to
    /// avoid adding a layer to ProjectSettings — layers are global, permanent and shared with every
    /// other system, which is a large thing to spend on a menu screen.
    ///
    /// LIGHTING IS THE SCENE'S OWN RIG. Directional lights are infinite, so the three the launcher
    /// creates already reach the stage — meaning the unit is lit here exactly as it is lit in a
    /// match, which is the honest reference for a codex. The editor capture's dedicated key and rim
    /// are deliberately NOT ported for the same reason: they are directional too, so they would
    /// spill straight onto the live board. The one light added here is a point light, which is
    /// positional and therefore cannot.
    /// </remarks>
    public sealed class UnitPreviewStage : MonoBehaviour
    {
        /// <summary>How far below the board the stage sits.</summary>
        private const float StageDepth = -1000f;

        /// <summary>
        /// Render target size, 4:3 landscape to match the card.
        /// </summary>
        /// <remarks>
        /// Not square, and the roster is why. Measured across all thirty prefabs, almost everything
        /// is wider than it is tall — the Arrow ward is 1.45 x 0.90, the Pulse 1.45 x 0.70 — so a
        /// square frame fits them by their width and then leaves a third of its height empty. Only
        /// the four rigged Meshy bipeds are genuinely tall, and they are the ones with the most
        /// headroom to give up.
        /// </remarks>
        private const int TextureWidth = 640;

        private const int TextureHeight = 480;

        /// <summary>Degrees per second the subject turns.</summary>
        /// <remarks>
        /// Slow on purpose. A showcase turn should let someone read a silhouette; at much above
        /// this it stops reading as presentation and starts reading as a fidget. One full rotation
        /// takes about fifteen seconds.
        /// </remarks>
        private const float SpinDegreesPerSecond = 24f;

        /// <summary>
        /// The three-quarter angle the subject starts at.
        /// </summary>
        /// <remarks>
        /// The same 205 degrees <c>StylizedUnitPreviewCapture</c> poses its subjects at, so a codex
        /// entry opens on the same face the review captures have been judged against.
        /// </remarks>
        private const float HeroYaw = 205f;

        /// <summary>
        /// Margin left around the subject, as a multiple of its measured on-screen extent.
        /// </summary>
        /// <remarks>
        /// Small, because the extent it multiplies is now the real one — the swept cylinder
        /// projected through the camera's pitch — rather than the largest raw bounds axis. The
        /// first version padded 25% on top of a measurement that was already conservative for
        /// anything wider than it is tall, which is most of the tower roster, and every ward sat in
        /// the middle of the card looking like a thumbnail.
        /// </remarks>
        private const float FramingPadding = 1.02f;

        /// <summary>How far above the subject's centre the camera sits, and how far back.</summary>
        /// <remarks>Together these set the viewing angle, and only the angle: an orthographic
        /// camera's framing comes entirely from its size.</remarks>
        private const float CameraRise = 1.05f;

        private const float CameraSetback = 2.6f;

        private Camera? stageCamera;
        private RenderTexture? texture;
        private GameObject? subjectRoot;
        private GameObject? subject;
        private string currentUnitId = string.Empty;
        private float spin;

        private TowerVisualLibrary? towerLibrary;
        private CreepVisualLibrary? creepLibrary;

        /// <summary>The live render target, or null before the first unit is shown.</summary>
        public RenderTexture? Texture => texture;

        /// <summary>Content id of whatever is currently on the stage, empty when it is clear.</summary>
        public string CurrentUnitId => currentUnitId;

        /// <summary>Puts a tower on the stage, by simulation content id (e.g. "tower.relay").</summary>
        public void ShowTower(string contentId)
        {
            if (currentUnitId == contentId)
            {
                return;
            }

            towerLibrary ??= Resources.Load<TowerVisualLibrary>("TowerVisualLibrary");
            var profile = towerLibrary != null ? towerLibrary.FindProfile(contentId) : null;
            if (profile == null || profile.Prefab == null)
            {
                Debug.LogError($"CODEX no tower visual profile with a prefab for '{contentId}'.");
                return;
            }

            Mount(
                contentId,
                profile.Prefab,
                profile.HasScale ? profile.Scale : Vector3.one,
                TowerVisualTuning.SpinPartRestTiltDegrees(profile.Role));
        }

        /// <summary>Puts a creep on the stage, by simulation content id (e.g. "creep.wisp").</summary>
        public void ShowCreep(string contentId)
        {
            if (currentUnitId == contentId)
            {
                return;
            }

            creepLibrary ??= Resources.Load<CreepVisualLibrary>("CreepVisualLibrary");
            var profile = creepLibrary != null ? creepLibrary.FindProfile(contentId) : null;
            if (profile == null || profile.Prefab == null)
            {
                Debug.LogError($"CODEX no creep visual profile with a prefab for '{contentId}'.");
                return;
            }

            Mount(contentId, profile.Prefab, profile.HasScale ? profile.Scale : Vector3.one, spinPartRestTilt: 0f);
        }

        /// <summary>Empties the stage and stops the camera. Called when the codex closes.</summary>
        /// <remarks>
        /// The camera is disabled rather than destroyed. A camera with a target texture renders
        /// every frame whether or not anything reads it, and the codex is a screen a player leaves
        /// open for seconds and then abandons for a whole match — paying a full render pass per
        /// frame for the rest of that match would be a real cost on a phone, and one nothing on
        /// screen would reveal.
        /// </remarks>
        public void Clear()
        {
            if (subject != null)
            {
                Destroy(subject);
                subject = null;
            }

            currentUnitId = string.Empty;

            if (stageCamera != null)
            {
                stageCamera.enabled = false;
            }
        }

        private void Mount(string contentId, GameObject prefab, Vector3 scale, float spinPartRestTilt)
        {
            EnsureStage();

            if (subject != null)
            {
                Destroy(subject);
            }

            subject = Instantiate(prefab, subjectRoot!.transform);
            subject.name = $"Codex Subject ({contentId})";
            subject.transform.localPosition = Vector3.zero;
            subject.transform.localRotation = Quaternion.identity;
            subject.transform.localScale = scale;

            // Ten of the fifteen creep prefabs carry an Animator with a committed controller, so
            // they idle here for nothing — but only if they are told to. The default culling mode
            // stops an Animator whose renderers are not visible to the MAIN camera, and this stage
            // is a thousand units away from it, so the idle would silently freeze on exactly the
            // units that have one.
            foreach (var animator in subject.GetComponentsInChildren<Animator>(true))
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            ApplySpinPartRestTilt(spinPartRestTilt);

            currentUnitId = contentId;
            spin = HeroYaw;
            subjectRoot.transform.localRotation = Quaternion.Euler(0f, spin, 0f);

            // After the tilt, never before: a tipped dish occupies a different bounding box than a
            // flat one, and framing the flat pose would crop the tipped one.
            Frame();

            if (stageCamera != null)
            {
                stageCamera.enabled = true;
            }
        }

        /// <summary>
        /// Puts a tower's spinning sub-part into the same rest pose the board gives it.
        /// </summary>
        /// <remarks>
        /// The board renderer tips the Relay's dish off horizontal before sweeping it
        /// (<c>UnityVerticalSliceRenderer.SpinPartRestTiltDegrees</c>, which has the measurements).
        /// Without this the codex would show that dish lying flat while the game shows it tipped —
        /// on the one screen whose whole job is to show the player what a tower looks like.
        ///
        /// Rest pose only. The codex does not sweep the part: the whole subject is already turning
        /// on the stage, and a second rotation on top of that reads as a wobble rather than as a
        /// mechanism.
        /// </remarks>
        private void ApplySpinPartRestTilt(float tilt)
        {
            if (tilt == 0f || subject == null)
            {
                return;
            }

            foreach (var name in TowerVisualTuning.SpinPartNames)
            {
                var part = FindDeep(subject.transform, name);
                if (part == null)
                {
                    continue;
                }

                var axis = part.parent.InverseTransformDirection(Vector3.forward).normalized;
                part.localRotation = Quaternion.AngleAxis(tilt, axis) * part.localRotation;
                return;
            }
        }

        private static Transform? FindDeep(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (var index = 0; index < parent.childCount; index++)
            {
                var found = FindDeep(parent.GetChild(index), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Fits the camera to whatever is on the stage.
        /// </summary>
        /// <remarks>
        /// Not optional. The authored profile scales span 0.59 to 1.05 across the towers and 0.208
        /// to 1.36 across the creeps, so a fixed orthographic size would either crop the Colossus
        /// or leave a Wisp as a speck. The bounds are read from the instantiated object AFTER its
        /// scale is applied, which is the only point at which they are true.
        ///
        /// Measured on the pre-spin pose and left alone afterwards. Re-fitting every frame as the
        /// subject turns would pump the framing in and out with the silhouette's width, which reads
        /// as the camera breathing.
        /// </remarks>
        private void Frame()
        {
            if (stageCamera == null || subject == null)
            {
                return;
            }

            // Only renderers that are actually DRAWING. Every tower prefab carries a RangeHalo whose
            // renderer ships disabled — it is switched on when a tower is selected — and it is a
            // ring far wider than the tower inside it. Including it fitted the camera to the halo
            // and left every ward sitting in the middle of the card at about a third of the size it
            // should be, with nothing visible to explain why.
            var renderers = subject.GetComponentsInChildren<Renderer>(true);
            var found = false;
            var bounds = new Bounds(subject.transform.position, Vector3.zero);
            foreach (var candidate in renderers)
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = candidate.bounds;
                    found = true;
                    continue;
                }

                bounds.Encapsulate(candidate.bounds);
            }

            if (!found)
            {
                Debug.LogError($"CODEX '{currentUnitId}' instantiated with no renderers; nothing to frame.");
                return;
            }

            // Aim at the subject's middle rather than its feet, so a tall unit is not bottom-heavy
            // in the frame and a flat one is not floating.
            //
            // The offset is fixed rather than scaled by the bounds, and that is what keeps every
            // entry consistent: an orthographic camera's framing comes entirely from its size, so
            // distance changes nothing and the offset's only effect is the angle it looks down
            // from. Fixed offset, fixed angle — about 22 degrees, near the 28 the review captures
            // have always used.
            var focus = bounds.center;
            stageCamera.transform.position = new Vector3(focus.x, focus.y + CameraRise, focus.z - CameraSetback);
            stageCamera.transform.LookAt(focus);

            // Fit to the SWEPT volume, not to this pose.
            //
            // The subject turns, so its on-screen width changes with its yaw — the Arrow ward is
            // 1.45 across and 1.05 deep, so fitting the pose it happens to start in would crop it a
            // few seconds later. Treating it as a cylinder of this radius makes the framing
            // rotation-invariant, at the cost of a little air around the narrow face.
            var radius = Mathf.Sqrt(bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z);

            // The vertical extent has to be measured through the camera's pitch, or a wide, squat
            // unit is fitted by its height and overflows sideways while a tall one is fitted by its
            // width and is left tiny.
            var pitch = stageCamera.transform.eulerAngles.x * Mathf.Deg2Rad;
            var vertical = bounds.extents.y * Mathf.Cos(pitch) + radius * Mathf.Sin(pitch);

            // orthographicSize is the view's VERTICAL half-height; the horizontal half-width it
            // buys is that times the aspect. So the width requirement has to be divided by the
            // aspect before the two can be compared, and that division is the whole benefit of a
            // landscape frame: at 4:3 a wide unit needs three quarters of the size a square frame
            // would have demanded for it.
            var aspect = TextureWidth / (float)TextureHeight;
            stageCamera.orthographicSize =
                Mathf.Max(0.2f, Mathf.Max(vertical, radius / aspect) * FramingPadding);
        }

        private void Update()
        {
            if (subjectRoot == null || subject == null)
            {
                return;
            }

            spin += SpinDegreesPerSecond * Time.unscaledDeltaTime;
            if (spin >= 360f)
            {
                spin -= 360f;
            }

            // unscaledDeltaTime, because the codex is a menu. The pause screen already stops the
            // simulation by holding ticks rather than by touching Time.timeScale today, but a menu
            // turntable that stops when the game is paused would be a strange thing to have written
            // deliberately, and this costs nothing to get right now.
            subjectRoot.transform.localRotation = Quaternion.Euler(0f, spin, 0f);
        }

        private void EnsureStage()
        {
            if (stageCamera != null)
            {
                return;
            }

            var origin = new Vector3(0f, StageDepth, 0f);
            transform.position = origin;

            subjectRoot = new GameObject("Codex Subject Root");
            subjectRoot.transform.SetParent(transform, false);

            texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "Codex Preview",
                antiAliasing = 4
            };
            texture.Create();

            var cameraObject = new GameObject("Codex Preview Camera");
            cameraObject.transform.SetParent(transform, false);
            stageCamera = cameraObject.AddComponent<Camera>();
            stageCamera.orthographic = true;
            stageCamera.orthographicSize = 1.05f;
            stageCamera.nearClipPlane = 0.1f;
            stageCamera.farClipPlane = 20f;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;

            // Alpha zero, so the card's own USS background shows through the empty part of the
            // frame instead of the unit sitting on an opaque box that no other element has.
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            stageCamera.targetTexture = texture;

            // Left Untagged, which is the default for a new GameObject and is the point: it must
            // never answer Camera.main, and it carries no AudioListener.
            stageCamera.enabled = false;

            // Positional, so it cannot reach the board a thousand units above. This is the one
            // light the stage adds on top of the scene's directional rig; its job is to lift the
            // side of the silhouette the key does not reach, so the model separates from the card.
            var fillObject = new GameObject("Codex Fill Light");
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.localPosition = new Vector3(-1.6f, 1.4f, -1.8f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.55f, 0.78f, 1f);
            fill.intensity = 2.2f;
            fill.range = 12f;
            fill.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            if (texture != null)
            {
                if (stageCamera != null)
                {
                    stageCamera.targetTexture = null;
                }

                texture.Release();
                Destroy(texture);
                texture = null;
            }
        }
    }
}
