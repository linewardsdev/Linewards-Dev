using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LTW.UnityClient.UI;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Creates the local vertical slice when a development scene is played. It deliberately
    /// composes client components only; match rules remain in LTW.Simulation.
    /// </summary>
    public static class LocalVerticalSliceLauncher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Object.FindAnyObjectByType<UnitySimulationDriver>() != null)
            {
                return;
            }

            var matchObject = new GameObject("LTW Local Vertical Slice");
            var driver = matchObject.AddComponent<UnitySimulationDriver>();
            var commands = matchObject.AddComponent<UnityCommandAdapter>();
            var bootstrapper = matchObject.AddComponent<UnityMatchBootstrapper>();
            var renderer = matchObject.AddComponent<UnityVerticalSliceRenderer>();
            var replayExporter = matchObject.AddComponent<LocalReplayExporter>();
            var playtestRecorder = matchObject.AddComponent<LocalPlaytestRecorder>();
            var performanceSampler = matchObject.AddComponent<DevicePerformanceSampler>();
            var stressHarness = matchObject.AddComponent<HeavySendStressHarness>();
            var shellScreens = CreateShellScreens(matchObject);
            var sessionOverlay = matchObject.AddComponent<LocalSessionFlowOverlay>();
            var diagnosticsOverlay = matchObject.AddComponent<DiagnosticsOverlay>();
            var controls = matchObject.AddComponent<LocalVerticalSliceDevelopmentControls>();
            var feedback = matchObject.AddComponent<PlacementFeedbackView>();
            var hud = matchObject.AddComponent<HudView>();
            var seatLeaderboard = matchObject.AddComponent<SeatLeaderboardView>();
            var sendDock = matchObject.AddComponent<SendDockController>();
            var placement = matchObject.AddComponent<TouchPlacementController>();
            var laneViewToggle = matchObject.AddComponent<LaneViewToggleController>();
            var camera = CreateCamera();
            CreateBackgroundCamera(camera);
            CreateLightRig(matchObject);
            CreatePostProcessing(matchObject, camera);

            renderer.Initialize(driver);
            renderer.SetPresentationCamera(camera);
            laneViewToggle.Initialize(renderer, driver);
            hud.Initialize(driver);
            seatLeaderboard.Initialize(driver);
            replayExporter.Initialize(driver);
            playtestRecorder.Initialize(driver, replayExporter);
            performanceSampler.Initialize(driver, renderer);
            stressHarness.Initialize(commands, performanceSampler);
            sessionOverlay.Initialize(driver, playtestRecorder, shellScreens);
            diagnosticsOverlay.Initialize(driver);
            controls.Initialize(commands, driver, renderer, replayExporter, playtestRecorder, stressHarness, placement, laneViewToggle, feedback);
            bootstrapper.Initialize(driver, commands);
            renderer.SetCameraFraming(renderer.CameraFraming);
            CreateRuntimeHud(matchObject, camera, commands, feedback, sendDock, placement);
        }

        /// <summary>
        /// Creates the runtime UI Toolkit surface the title, pause and results screens render into.
        /// </summary>
        /// <remarks>
        /// Created here, at runtime, for the same reason every other component is: the scene asset
        /// <c>Assets/Scenes/LocalVerticalSlice.unity</c> contains zero GameObjects. The one thing
        /// that could not be built this way is the <c>PanelSettings</c>, which is a ScriptableObject
        /// and therefore has to exist on disk — see
        /// <c>LTW.UnityClient.Editor.ShellPanelSettingsGenerator</c>, which authors and commits it.
        ///
        /// No EventSystem and no input module are added alongside it. UI Toolkit falls back to its
        /// own runtime event system when the scene has none, and that fallback reads legacy
        /// <c>Input</c>, which is exactly what this project is set to (<c>activeInputHandler: 0</c>).
        /// Verified rather than assumed: <c>LTW.UnityClient.Editor.ShellInputCheck</c> drives a
        /// press at the centre of START GAME through that path and asserts the build countdown began.
        ///
        /// Its own GameObject rather than another component on the match object, because a
        /// UIDocument's lifetime is the panel's: disabling this object should take the shell down
        /// without touching the simulation.
        /// </remarks>
        private static ShellScreenView CreateShellScreens(GameObject matchObject)
        {
            var shellObject = new GameObject("LTW Shell Screens");
            shellObject.transform.SetParent(matchObject.transform, false);
            return shellObject.AddComponent<ShellScreenView>();
        }

        private static Camera CreateCamera()
        {
            var existingCameras = Camera.allCameras;
            for (var index = 0; index < existingCameras.Length; index++)
            {
                existingCameras[index].enabled = false;
            }

            var cameraObject = new GameObject("LTW Presentation Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 15.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.transform.position = new Vector3(12f, 28f, -10f);
            camera.transform.LookAt(new Vector3(12f, 0f, 8.5f));
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.enabled = true;

            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            return camera;
        }

        /// <summary>
        /// Builds the three-point rig the board is lit by. The scene asset carries no lights, so
        /// without this every mesh renders under flat ambient only: no diffuse gradient, no
        /// specular response and no contact shadow, which reads as a flat image of the model
        /// rather than a solid object.
        /// </summary>
        private static void CreateLightRig(GameObject matchObject)
        {
            if (Object.FindAnyObjectByType<Light>() != null)
            {
                return;
            }

            var rig = new GameObject("LTW Light Rig");
            rig.transform.SetParent(matchObject.transform, false);

            // The presentation camera sits at -Z looking toward +Z, so board-facing surfaces carry
            // -Z normals. The key is yawed off-axis to keep tower faces from flattening out.
            var key = CreateDirectionalLight(rig, "Key", new Vector3(50f, -35f, 0f), new Color(1f, 0.957f, 0.878f), 1.2f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.55f;

            CreateDirectionalLight(rig, "Fill", new Vector3(30f, 145f, 0f), new Color(0.722f, 0.804f, 1f), 0.35f);

            // Rim travels back toward the camera to separate silhouettes from the board beneath.
            CreateDirectionalLight(rig, "Rim", new Vector3(15f, 180f, 0f), new Color(0.851f, 0.902f, 1f), 0.5f);

            ApplyGradientAmbient();
        }

        private static Light CreateDirectionalLight(GameObject rig, string name, Vector3 eulerAngles, Color color, float intensity)
        {
            var lightObject = new GameObject($"LTW {name} Light");
            lightObject.transform.SetParent(rig.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(eulerAngles);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>
        /// Replaces the flat ambient colour with a sky/equator/ground gradient. It approximates
        /// bounce grounding at no runtime cost and keeps undersides from going fully dead.
        /// </summary>
        private static readonly Color AmbientSky = new Color(0.322f, 0.361f, 0.451f);
        private static readonly Color AmbientEquator = new Color(0.212f, 0.227f, 0.259f);
        private static readonly Color AmbientGround = new Color(0.114f, 0.125f, 0.157f);

        private static void ApplyGradientAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            ApplyMatchingReflections();
        }

        /// <summary>Face size of the generated reflection cubemap.</summary>
        /// <remarks>
        /// 32 is chosen for the mip chain, not the detail. The source is a vertical gradient with no
        /// features, so face resolution buys nothing; but reflection roughness samples down the mip
        /// chain, and 32 gives six levels to fall through. At 4 or 8 the roughest materials land on a
        /// near-1x1 mip and every smooth surface collapses to the same flat tone.
        /// </remarks>
        private const int ReflectionCubemapSize = 32;

        /// <summary>
        /// Points environment reflections at the scene's own ambient gradient.
        /// </summary>
        /// <remarks>
        /// Ambient was already authored as a trilight gradient, but reflections are a separate
        /// channel: they kept defaulting to <see cref="DefaultReflectionMode.Skybox"/>, and with no
        /// skybox assigned that means Unity's stock procedural sky. So every smooth surface on the
        /// board mirrored a daylit blue-grey sky while the camera cleared to near-black navy and the
        /// ambient said dim slate. Nothing in the scene was ever that bright or that blue — it is the
        /// specific mismatch that reads as "prototype" no matter how good the models are.
        ///
        /// Generated rather than authored as an asset: it is exactly the three colours above, so a
        /// committed cubemap would be a second copy of them that could drift out of sync with the
        /// ambient it is supposed to match. Deriving it here makes that impossible by construction.
        ///
        /// A ReflectionProbe was the other option and is rejected on timing: probes capture the scene
        /// as it stands, and this runs at launch when the board is empty, so it would bake an empty
        /// board and then be wrong for the entire match unless refreshed — paying a six-face render
        /// to arrive at a worse version of a gradient we can compute.
        /// </remarks>
        private static void ApplyMatchingReflections()
        {
            var cubemap = new Cubemap(ReflectionCubemapSize, TextureFormat.RGBAHalf, true)
            {
                name = "LTW Ambient Reflection",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[ReflectionCubemapSize * ReflectionCubemapSize];
            for (var face = 0; face < 6; face++)
            {
                for (var y = 0; y < ReflectionCubemapSize; y++)
                {
                    for (var x = 0; x < ReflectionCubemapSize; x++)
                    {
                        // Texel centres, mapped to the [-1, 1] face plane.
                        var u = ((x + 0.5f) / ReflectionCubemapSize) * 2f - 1f;
                        var v = ((y + 0.5f) / ReflectionCubemapSize) * 2f - 1f;
                        var direction = FaceDirection((CubemapFace)face, u, v).normalized;
                        pixels[(y * ReflectionCubemapSize) + x] = GradientColor(direction.y);
                    }
                }

                cubemap.SetPixels(pixels, (CubemapFace)face);
            }

            cubemap.Apply(true);

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cubemap;
            RenderSettings.reflectionIntensity = 1f;
        }

        /// <summary>
        /// The direction a texel on a cubemap face points, in Unity's left-handed cube convention.
        /// </summary>
        private static Vector3 FaceDirection(CubemapFace face, float u, float v) => face switch
        {
            CubemapFace.PositiveX => new Vector3(1f, -v, -u),
            CubemapFace.NegativeX => new Vector3(-1f, -v, u),
            CubemapFace.PositiveY => new Vector3(u, 1f, v),
            CubemapFace.NegativeY => new Vector3(u, -1f, -v),
            CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
            _ => new Vector3(-u, -v, -1f),
        };

        /// <summary>
        /// The same sky/equator/ground blend Unity applies for trilight ambient, so a mirror surface
        /// reflects the light it is already being lit by.
        /// </summary>
        private static Color GradientColor(float upness) => upness >= 0f
            ? Color.Lerp(AmbientEquator, AmbientSky, upness)
            : Color.Lerp(AmbientEquator, AmbientGround, -upness);

        /// <summary>Overrides the profile must carry: tonemapping, bloom, colour adjustments.</summary>
        internal const int ExpectedPostProcessingOverrides = 3;

        /// <summary>
        /// Whether the profile is not merely present but actually carries its overrides.
        /// </summary>
        /// <remarks>
        /// The previous check was `profile == null`, which tested the wrong thing — and is why this
        /// shipped broken for the entire life of the URP migration. The asset existed, so the check
        /// passed; but its three component references serialized as null, because the generator
        /// created them as ScriptableObjects and never called AssetDatabase.AddObjectToAsset (see
        /// UrpPostProcessingSetup.GetOrAdd). A Volume was built from an empty profile on every
        /// launch: no tonemapper, no bloom, linear HDR clipping straight to sRGB, and every
        /// 2.0-intensity emissive clamping to flat white.
        ///
        /// Nothing reported it from either side. The generator logged "3 override(s)" because they
        /// existed in memory at that moment, and this guard said nothing because the file was on
        /// disk. So the check is now on CONTENTS and it is an error rather than a warning: a wrong
        /// render setup is invisible in a screenshot until someone compares against a much older
        /// build.
        /// </remarks>
        internal static bool IsPostProcessingProfileUsable(VolumeProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("LTW_PostProcessing profile not found in Resources; rendering with no tonemapper and no bloom.");
                return false;
            }

            if (profile.components == null || profile.components.Count != ExpectedPostProcessingOverrides)
            {
                Debug.LogError(
                    $"LTW_PostProcessing has {(profile.components == null ? 0 : profile.components.Count)} override(s), expected {ExpectedPostProcessingOverrides}. " +
                    "Re-run Line Wars/Migration/Create Post Processing Profile and commit the resulting sub-assets.");
                return false;
            }

            for (var index = 0; index < profile.components.Count; index++)
            {
                if (profile.components[index] == null)
                {
                    Debug.LogError(
                        $"LTW_PostProcessing override {index} is null — the profile's sub-assets never reached disk. " +
                        "Re-run Line Wars/Migration/Create Post Processing Profile and commit the resulting sub-assets.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Attaches the global post-processing volume and enables it on the presentation camera.
        /// </summary>
        /// <remarks>
        /// Bloom is the capability the URP migration exists to obtain. Tower emission maps cover
        /// only a small fraction of each texture, so the per-role emission colours read as barely
        /// tinted highlights without it.
        ///
        /// This is a no-op under the Built-in pipeline, which has no volume system, so the launcher
        /// stays valid on both while the migration is in flight.
        /// </remarks>
        private static void CreatePostProcessing(GameObject matchObject, Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                return;
            }

            var profile = Resources.Load<VolumeProfile>("LTW_PostProcessing");
            if (!IsPostProcessingProfileUsable(profile))
            {
                return;
            }

            var volumeObject = new GameObject("LTW Post Processing");
            volumeObject.transform.SetParent(matchObject.transform, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            var cameraData = camera.GetUniversalAdditionalCameraData();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;

                // SMAA on top of the pipeline's MSAA 2x. MSAA alone only antialiases geometric
                // edges the rasteriser knows about, which leaves the crystal and spire
                // silhouettes stair-stepped at phone DPI; SMAA is a post pass and catches those
                // plus the shader-authored edges (contact shadows, spore fog rims) that MSAA
                // cannot see at all. Low quality deliberately: this is a fixed orthographic
                // camera with no fast motion, where the higher presets buy detail nobody can
                // resolve at the cost of a heavier full-screen pass on a mobile GPU.
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.Low;
            }
        }

        private static Camera CreateBackgroundCamera(Camera presentationCamera)
        {
            var cameraObject = new GameObject("LTW Background Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = presentationCamera.backgroundColor;
            camera.cullingMask = 0;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.depth = presentationCamera.depth - 1f;
            camera.enabled = true;
            return camera;
        }

        private static void CreateRuntimeHud(
            GameObject matchObject,
            Camera camera,
            UnityCommandAdapter commands,
            PlacementFeedbackView feedback,
            SendDockController sendDock,
            TouchPlacementController placement)
        {
            var ghost = RenderCompat.CreatePrimitive(PrimitiveType.Cylinder);
            ghost.name = "Placement Ghost";
            ghost.transform.SetParent(matchObject.transform, false);
            ghost.transform.localScale = new Vector3(0.62f, 0.78f, 0.62f);
            ghost.SetActive(false);

            placement.Initialize(camera, commands, feedback, ghost);
            sendDock.Initialize(commands, feedback);
        }
    }

    public sealed class LocalVerticalSliceDevelopmentControls : MonoBehaviour
    {
        private UnityCommandAdapter commands = null!;
        private UnitySimulationDriver driver = null!;
        private UnityVerticalSliceRenderer renderer = null!;
        private LocalReplayExporter replayExporter = null!;
        private LocalPlaytestRecorder playtestRecorder = null!;
        private HeavySendStressHarness stressHarness = null!;
        private TouchPlacementController placement = null!;
        private LaneViewToggleController laneViewToggle = null!;
        private PlacementFeedbackView feedback = null!;

        public void Initialize(
            UnityCommandAdapter commandAdapter,
            UnitySimulationDriver simulationDriver,
            UnityVerticalSliceRenderer presentationRenderer,
            LocalReplayExporter exporter,
            LocalPlaytestRecorder recorder,
            HeavySendStressHarness harness,
            TouchPlacementController placementController,
            LaneViewToggleController viewToggleController,
            PlacementFeedbackView feedbackView)
        {
            commands = commandAdapter;
            driver = simulationDriver;
            renderer = presentationRenderer;
            replayExporter = exporter;
            playtestRecorder = recorder;
            stressHarness = harness;
            placement = placementController;
            laneViewToggle = viewToggleController;
            feedback = feedbackView;
        }

        private void Update()
        {
            if (commands == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.B)) placement.BeginTowerPlacement();
            if (Input.GetKeyDown(KeyCode.C)) placement.BeginControlTowerPlacement();
            if (Input.GetKeyDown(KeyCode.U)) placement.BeginUtilityTowerPlacement();
            if (Input.GetKeyDown(KeyCode.T)) placement.BeginPulseTowerPlacement();
            if (Input.GetKeyDown(KeyCode.Y)) placement.BeginPrismTowerPlacement();
            if (Input.GetKeyDown(KeyCode.Return)) placement.ConfirmPlacement();
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (placement.IsPlacing)
                {
                    placement.ConfirmPlacement();
                }
                else
                {
                    driver.TogglePause();
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape)) placement.CancelPlacement();
            if (Input.GetKeyDown(KeyCode.UpArrow)) placement.NudgeUp();
            if (Input.GetKeyDown(KeyCode.DownArrow)) placement.NudgeDown();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) placement.NudgeLeft();
            if (Input.GetKeyDown(KeyCode.RightArrow)) placement.NudgeRight();
            if (Input.GetKeyDown(KeyCode.S)) ShowSendResult(commands.SendSampleCreep(), "Runner sent");
            if (Input.GetKeyDown(KeyCode.V)) ShowSendResult(commands.SendBruteCreep(), "Brute sent");
            if (Input.GetKeyDown(KeyCode.W)) ShowSendResult(commands.SendSwarmCreep(), "Swarm sent");
            if (Input.GetKeyDown(KeyCode.D)) ShowSendResult(commands.SendShadeCreep(), "Shade sent");
            if (Input.GetKeyDown(KeyCode.G)) ShowSendResult(commands.SendSiegeCreep(), "Siege sent");
            if (Input.GetKeyDown(KeyCode.X)) commands.SellLastSampleTower();
            if (Input.GetKeyDown(KeyCode.R))
            {
                driver.ResetMatch();
                playtestRecorder.ResetRecorder();
            }
            if (Input.GetKeyDown(KeyCode.E)) replayExporter.ExportCurrentReplay();
            if (Input.GetKeyDown(KeyCode.P))
            {
                var reportPath = playtestRecorder.ExportNow();
                feedback.ShowEconomy(reportPath is null ? "Finish match first" : $"Saved {System.IO.Path.GetFileName(reportPath)}");
            }
            if (Input.GetKeyDown(KeyCode.H)) stressHarness.StartRun();
            if (Input.GetKeyDown(KeyCode.M)) PresentationPreferences.ToggleAudioMuted();
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) PresentationPreferences.AdjustFeedbackVolume(-0.1f);
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) PresentationPreferences.AdjustFeedbackVolume(0.1f);
            if (Input.GetKeyDown(KeyCode.F)) PresentationPreferences.ReducedEffects = !PresentationPreferences.ReducedEffects;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                laneViewToggle.ToggleView();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) renderer.SetPresentationDetail(PresentationDetail.Full);
            if (Input.GetKeyDown(KeyCode.Alpha2)) renderer.SetPresentationDetail(PresentationDetail.Simplified);
            if (Input.GetKeyDown(KeyCode.Alpha3)) renderer.SetPresentationDetail(PresentationDetail.Disabled);
        }

        private void ShowSendResult(LTW.Simulation.Bridge.VerticalSliceCommandResult result, string successMessage)
        {
            if (result.Accepted)
            {
                feedback.ShowEconomy(successMessage);
                return;
            }

            feedback.ShowRejected(result.RejectionReason);
        }
    }
}
