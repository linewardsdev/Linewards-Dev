#nullable enable

using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Combat;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class TouchPlacementController : MonoBehaviour
    {
        private const int LaneWidth = 7;
        private const int LaneLength = 16;
        private const string BuilderSpriteResourcePath = "Art/Builder/Production/Sprites/builder_candidate_v01_trimmed";
        private const string BuilderModelResourcePath = "Prefabs/Builder/Builder_3D";

        private static readonly Color PanelInk = new(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color ArcaneBlue = new(0.302f, 0.639f, 1f, 1f);
        private static readonly Color MintSignal = new(0.349f, 0.882f, 0.714f, 1f);
        private static readonly Color WardViolet = new(0.608f, 0.424f, 1f, 1f);
        private static readonly Color SignalGold = new(1f, 0.784f, 0.29f, 1f);
        private static readonly Color Cloud = new(0.957f, 0.969f, 1f, 1f);
        private static readonly Color Danger = new(1f, 0.32f, 0.24f, 1f);
        private static readonly Color DisabledInk = new(0.22f, 0.25f, 0.32f, 0.88f);
        private static readonly Color DisabledText = new(0.55f, 0.59f, 0.68f, 1f);

        private static GUIStyle? panelStyle;
        private static GUIStyle? titleStyle;
        private static GUIStyle? bodyStyle;
        private static GUIStyle? buttonStyle;
        private static GUIStyle? metaStyle;

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
        private readonly System.Collections.Generic.Dictionary<string, GameObject?> ghostModels = new();
        private TowerVisualLibrary? towerVisualLibrary;
        private Material? ghostMaterial;

        private GameObject builderAvatar = null!;
        private SpriteRenderer? builderAvatarSprite;
        private Animator? builderAvatarAnimator;

        private const float BuilderWalkSpeed = 4.5f;
        private const float BuilderWalkBobAmplitude = 0.05f;
        private const float BuilderWalkBobFrequency = 9f;
        private const float BuilderWalkTurnDegreesPerSecond = 720f;

        [SerializeField]
        private bool showPlacementReadout = true;

        private GameObject selectionRing = null!;

        private bool isPlacing;
        private bool isPaletteExpanded;
        private int selectedTowerRole;
        private int lastSelectedTowerRole;
        private int highlightedTowerRole = -1;
        private int selectedTowerCategory = -1;
        private Vector2Int selectedCell;
        private TowerCombatState? selectedTower;
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
            // handles the same case (OPEN_ITEMS.md item 24).
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
            HideSelectionRing();
            selectedCell = hitCell;
            UpdateBuilderAvatar();
        }

        private void OnGUI()
        {
            if (!showPlacementReadout)
            {
                return;
            }

            EnsureStyles();

            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            DrawTowerPalette(scale);
            if (IsSendDockExpanded())
            {
                return;
            }

            DrawSelectedTowerPanel(scale);

            if (!isPlacing)
            {
                return;
            }

            var rect = PlacementPanelRect(scale, frame);
            var accent = SelectedTowerAccent();

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            titleStyle!.fontSize = Mathf.RoundToInt(17f * scale);
            titleStyle.normal.textColor = accent;
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            bodyStyle.normal.textColor = Cloud;

            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 9f * scale, rect.width - 24f * scale, 24f * scale), $"{SelectedTowerName().ToUpperInvariant()}  {SelectedTowerCost()}G", titleStyle);

            var placementLine = placementPreview.Accepted ? $"CELL {selectedCell.x}, {selectedCell.y} READY" : PlacementPreviewText();
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 35f * scale, rect.width - 24f * scale, 20f * scale), placementLine, bodyStyle);
            var actionHint = placementPreview.Accepted ? "BUILDER ONLINE  •  TAP BUILD" : PlacementRecoveryText();
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 57f * scale, rect.width - 24f * scale, 20f * scale), actionHint, bodyStyle);

            var switchY = rect.y + 82f * scale;
            var switchHeight = 24f * scale;
            var switchGap = 4f * scale;
            var switchWidth = (rect.width - 24f * scale - switchGap * 4f) / 5f;
            var switchX = rect.x + 12f * scale;
            if (DrawPlacementSwitchButton(new Rect(switchX, switchY, switchWidth, switchHeight), "ARW", 0, LTW.UnityClient.Simulation.TowerRolePalette.Arrow, scale))
            {
                BeginTowerPlacement(0);
                return;
            }

            switchX += switchWidth + switchGap;
            if (DrawPlacementSwitchButton(new Rect(switchX, switchY, switchWidth, switchHeight), "CTRL", 1, LTW.UnityClient.Simulation.TowerRolePalette.Control, scale))
            {
                BeginControlTowerPlacement();
                return;
            }

            switchX += switchWidth + switchGap;
            if (DrawPlacementSwitchButton(new Rect(switchX, switchY, switchWidth, switchHeight), "RLY", 2, LTW.UnityClient.Simulation.TowerRolePalette.Relay, scale))
            {
                BeginUtilityTowerPlacement();
                return;
            }

            switchX += switchWidth + switchGap;
            if (DrawPlacementSwitchButton(new Rect(switchX, switchY, switchWidth, switchHeight), "PLS", 3, LTW.UnityClient.Simulation.TowerRolePalette.Pulse, scale))
            {
                BeginPulseTowerPlacement();
                return;
            }

            switchX += switchWidth + switchGap;
            if (DrawPlacementSwitchButton(new Rect(switchX, switchY, switchWidth, switchHeight), "PRM", 4, LTW.UnityClient.Simulation.TowerRolePalette.Prism, scale))
            {
                BeginPrismTowerPlacement();
                return;
            }

            var buttonHeight = 30f * scale;
            var gap = 6f * scale;
            var allWidth = 64f * scale;
            var buildWidth = 104f * scale;
            var cancelWidth = 86f * scale;
            var totalWidth = allWidth + buildWidth + cancelWidth + gap * 2f;
            var buttonY = rect.yMax - 38f * scale;
            var buttonX = rect.center.x - totalWidth * 0.5f;

            if (DrawLauncherButton(new Rect(buttonX, buttonY, allWidth, buttonHeight), "ALL", SignalGold, scale))
            {
                CancelPlacement(false);
                OpenTowerPalette();
                return;
            }

            buttonX += allWidth + gap;
            if (DrawLauncherButton(new Rect(buttonX, buttonY, buildWidth, buttonHeight), "BUILD", placementPreview.Accepted ? MintSignal : Danger, scale))
            {
                ConfirmPlacement();
                return;
            }

            buttonX += buildWidth + gap;
            if (DrawLauncherButton(new Rect(buttonX, buttonY, cancelWidth, buttonHeight), "CANCEL", Danger, scale))
            {
                CancelPlacement();
                return;
            }
        }

        private bool DrawPlacementSwitchButton(Rect rect, string label, int towerRole, Color accent, float scale)
        {
            buttonStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            return RuntimeUiChrome.DrawControlButton(rect, label, accent, selectedTowerRole == towerRole, scale, buttonStyle);
        }

        private bool SelectTowerAt(Vector2Int cell)
        {
            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }

            selectedTower = null;
            HideSelectionRing();
            var snapshot = simulationDriver?.LatestSnapshot;
            if (snapshot is null)
            {
                return false;
            }

            foreach (var tower in snapshot.Towers)
            {
                if (tower.OwnerId.Equals(simulationDriver.LocalPlayerId) && tower.LaneId.Equals(simulationDriver.LocalPlayerLaneId) && tower.Position.X == cell.x && tower.Position.Y == cell.y)
                {
                    selectedTower = tower;
                    UpdateSelectionRing(tower);
                    HideBuilderAvatar();
                    ghost.SetActive(false);
                    feedbackView.ShowAccepted(TowerRoleName(tower.TowerId.Value) + " selected");
                    return true;
                }
            }

            return false;
        }

        private void DrawSelectedTowerPanel(float scale)
        {
            if (isPlacing || selectedTower is null)
            {
                return;
            }

            var frame = MobileViewportLayout.ScreenRect();
            var rect = SelectedTowerPanelRect(scale, frame);
            var accent = TowerAccent(selectedTower.TowerId.Value);
            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), accent);

            titleStyle!.fontSize = Mathf.RoundToInt(16f * scale);
            titleStyle.normal.textColor = accent;
            bodyStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            bodyStyle.normal.textColor = Cloud;

            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 8f * scale, rect.width - 156f * scale, 22f * scale), TowerRoleName(selectedTower.TowerId.Value).ToUpperInvariant(), titleStyle);
            if (DrawLauncherButton(new Rect(rect.xMax - 142f * scale, rect.y + 7f * scale, 78f * scale, 30f * scale), "BUILD", SignalGold, scale))
            {
                selectedTower = null;
                HideSelectionRing();
                OpenTowerPalette();
                return;
            }
            if (RuntimeUiChrome.DrawPanelButton(new Rect(rect.xMax - 56f * scale, rect.y + 7f * scale, 40f * scale, 30f * scale), "X", accent, scale, buttonStyle ?? GUI.skin.button))
            {
                selectedTower = null;
                HideSelectionRing();
                return;
            }
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 32f * scale, rect.width - 24f * scale, 18f * scale), TowerPurpose(selectedTower.TowerId.Value), bodyStyle);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 51f * scale, rect.width - 24f * scale, 18f * scale), $"CELL {selectedTower.Position.X}, {selectedTower.Position.Y}  OWNER P{selectedTower.OwnerId.Value}", bodyStyle);
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 70f * scale, rect.width - 108f * scale, 18f * scale), "Tap another tower or sell this one", bodyStyle);

            buttonStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            if (RuntimeUiChrome.DrawPanelButton(new Rect(rect.x + rect.width - 86f * scale, rect.y + 36f * scale, 70f * scale, 42f * scale), "SELL", Danger, scale, buttonStyle))
            {
                SellLastTower();
            }
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

        private void MoveGhost()
        {
            // The builder now walks along with tower placement instead of vanishing for it, so
            // the ghost preview shouldn't appear at the destination the instant a new cell is
            // picked either — it stays hidden until the builder actually arrives there
            // (TickBuilderWalk re-enables it once the walk finishes), reading as the builder
            // setting the tower down rather than a preview floating in ahead of them.
            UpdateBuilderAvatar();
            ghost.SetActive(false);
            ghost.transform.position = GridToWorld(selectedCell, 0.6f);
            ConfigurePlacementGhostVisual();
            // A resolved model already carries its profile's own scale, so the root has to stay at
            // one or the two multiply and the preview comes out larger than the placed tower.
            ghost.transform.localScale = HasGhostModel() ? Vector3.one : SelectedTowerGhostScale();
            RefreshPlacementPreview();
        }

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

        private void UpdateGhostColor()
        {
            var color = placementPreview.Accepted ? SelectedTowerAccent() : Danger;
            color.a = placementPreview.Accepted ? 0.74f : 0.86f;
            foreach (var ghostRenderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                ghostRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Shows a translucent copy of the actual tower model for the selected role.
        /// </summary>
        /// <remarks>
        /// The preview used to be assembled from tinted cylinders and cubes — a stack of coloured
        /// primitives roughly standing in for each tower's proportions. That was reasonable when
        /// the towers themselves were primitives, but they are 3D models now, so the preview was
        /// showing a shape that no longer matched what you would get. Instantiating the real
        /// prefab means the silhouette under your finger is the silhouette you are about to place.
        /// </remarks>
        private void ConfigurePlacementGhostVisual()
        {
            var roleId = SelectedTowerRoleId();
            foreach (var cached in ghostModels)
            {
                if (cached.Value != null)
                {
                    cached.Value.SetActive(cached.Key == roleId);
                }
            }

            if (ghostModels.TryGetValue(roleId, out var existing) && existing != null)
            {
                return;
            }

            var model = BuildGhostModel(roleId);
            ghostModels[roleId] = model;
            if (model == null)
            {
                // No library or prefab: fall back to the plain ghost root so placement still has
                // something to point at rather than nothing at all.
                EnsureGhostFallback(true);
                return;
            }

            EnsureGhostFallback(false);
        }

        private GameObject? BuildGhostModel(string roleId)
        {
            var library = towerVisualLibrary != null
                ? towerVisualLibrary
                : towerVisualLibrary = Resources.Load<TowerVisualLibrary>("TowerVisualLibrary");
            var profile = library != null ? library.FindProfile($"tower.{roleId}") : null;
            if (profile == null || profile.Prefab == null)
            {
                return null;
            }

            var model = Instantiate(profile.Prefab, ghost.transform);
            model.name = $"GhostModel_{roleId}";
            model.transform.localPosition = Vector3.up * profile.Lift;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = profile.HasScale ? profile.Scale : Vector3.one;

            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            // One shared unlit translucent material across the whole model. Keeping the tower's
            // own materials would make the preview look like a finished tower already standing
            // there, which is exactly the confusion a ghost has to avoid.
            foreach (var modelRenderer in model.GetComponentsInChildren<Renderer>(true))
            {
                modelRenderer.sharedMaterial = GhostMaterial();
                modelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                modelRenderer.receiveShadows = false;
            }

            return model;
        }

        private bool HasGhostModel() =>
            ghostModels.TryGetValue(SelectedTowerRoleId(), out var model) && model != null;

        private Material GhostMaterial()
        {
            if (ghostMaterial != null)
            {
                return ghostMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            ghostMaterial = new Material(shader) { name = "PlacementGhost" };
            return ghostMaterial;
        }

        /// <summary>Shows or hides the ghost root's own renderer, used only when no model resolves.</summary>
        private void EnsureGhostFallback(bool visible)
        {
            if (ghost.TryGetComponent<Renderer>(out var rootRenderer))
            {
                rootRenderer.enabled = visible;
            }
        }

        private void EnsureBuilderAvatar()
        {
            if (builderAvatar != null)
            {
                return;
            }

            builderAvatar = new GameObject("Builder Avatar");
            builderAvatar.transform.SetParent(transform, false);
            builderAvatar.SetActive(false);

            var model = Resources.Load<GameObject>(BuilderModelResourcePath);
            if (model != null)
            {
                // Rigged biped (Meshy) with a real Walk cycle — TickBuilderWalk drives its
                // Animator's "Walking" bool instead of the old bob-only primitive avatar.
                var instance = Instantiate(model, builderAvatar.transform);
                instance.name = "Model";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                // Raw mesh stands ~2.2 units tall; scaled down to sit roughly level with the other
                // 3D units on the board (see measure_builder_bounds notes in the pipeline).
                instance.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                builderAvatarAnimator = instance.GetComponentInChildren<Animator>(true);
                return;
            }

            // Fallback if the 3D model asset is missing for any reason (e.g. a build stripped
            // Resources content it shouldn't have) — the original primitive-and-sprite avatar.
            builderAvatar.transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);
            CreateBuilderPart("Body", PrimitiveType.Capsule, new Vector3(0f, 0.34f, 0f), new Vector3(0.28f, 0.34f, 0.28f));
            CreateBuilderPart("Pack", PrimitiveType.Cube, new Vector3(0f, 0.38f, -0.2f), new Vector3(0.25f, 0.3f, 0.12f));
            CreateBuilderPart("Visor", PrimitiveType.Cube, new Vector3(0f, 0.53f, 0.18f), new Vector3(0.2f, 0.08f, 0.08f));
            CreateBuilderSpriteVisual();
        }

        private void HideBuilderAvatar()
        {
            if (builderAvatar != null)
            {
                builderAvatar.SetActive(false);
            }
        }

        /// <summary>
        /// Walks the builder avatar toward whatever cell is currently selected instead of
        /// teleporting it there — called every frame (not gated behind input, unlike the rest of
        /// <see cref="Update"/>) so the walk keeps progressing across frames with no clicks.
        /// A simple bob (no leg geometry exists on this primitive-built avatar) plus turning to
        /// face the direction of travel is enough to read as an actual walk rather than a slide.
        /// </summary>
        private void TickBuilderWalk()
        {
            if (builderAvatar == null || !builderAvatar.activeSelf)
            {
                return;
            }

            var target = BuilderGroundPosition(selectedCell);
            var current = builderAvatar.transform.position;
            var flatCurrent = new Vector3(current.x, 0f, current.z);
            var flatTarget = new Vector3(target.x, 0f, target.z);
            var toTarget = flatTarget - flatCurrent;
            var distance = toTarget.magnitude;

            if (distance < 0.02f)
            {
                builderAvatar.transform.position = target;
                builderAvatarAnimator?.SetBool("Walking", false);
                if (isPlacing)
                {
                    // Arrived — only now does the tower ghost actually appear, settled exactly
                    // onto the real cell, reading as the builder having walked over and set it
                    // down rather than a preview that was already floating there.
                    ghost.transform.position = GridToWorld(selectedCell, 0.6f);
                    ghost.SetActive(true);
                }

                return;
            }

            builderAvatarAnimator?.SetBool("Walking", true);
            var direction = toTarget / distance;
            var step = Mathf.Min(distance, BuilderWalkSpeed * Time.deltaTime);
            var moved = flatCurrent + direction * step;

            // The rigged model's own Walk clip already animates a real up-down bounce from its
            // leg motion — layering the old procedural sine bob on top (built for the legless
            // primitive avatar) would double up as an odd extra wobble, so it's skipped whenever
            // a real Animator is driving the character.
            var bob = builderAvatarAnimator == null
                ? Mathf.Abs(Mathf.Sin(Time.time * BuilderWalkBobFrequency)) * BuilderWalkBobAmplitude
                : 0f;
            builderAvatar.transform.position = new Vector3(moved.x, target.y + bob, moved.z);

            var desiredRotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);
            builderAvatar.transform.rotation = Quaternion.RotateTowards(
                builderAvatar.transform.rotation,
                desiredRotation,
                BuilderWalkTurnDegreesPerSecond * Time.deltaTime);
        }

        private void CreateBuilderPart(string partName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(primitiveType);
            if (part == null || builderAvatar == null)
            {
                return;
            }

            part.name = partName;
            part.transform.SetParent(builderAvatar.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
        }

        private static Vector3 BuilderRestOffset => new(-0.48f, 0f, 0.24f);

        private Vector3 BuilderGroundPosition(Vector2Int cell) => GridToWorld(cell, 0.02f) + BuilderRestOffset;

        private void UpdateBuilderAvatar()
        {
            if (builderAvatar == null)
            {
                EnsureBuilderAvatar();
            }

            if (builderAvatar == null)
            {
                return;
            }

            // Only snap instantly the first time the avatar appears (there's no sensible "previous
            // cell" to walk in from yet). Every cell change after that is picked up by
            // TickBuilderWalk instead, which walks the avatar across the board rather than
            // teleporting it — this call just needs to make sure it's visible/coloured.
            var alreadyVisible = builderAvatar.activeSelf;
            if (!alreadyVisible)
            {
                builderAvatar.transform.position = BuilderGroundPosition(selectedCell);
            }

            builderAvatar.SetActive(true);

            // The rigged model carries its own painted PBR material — recolouring it the way the
            // primitive/sprite fallback does below would just wash out its texture with a flat
            // tint, so it's left alone entirely.
            if (builderAvatarAnimator != null)
            {
                return;
            }

            var accent = SelectedTowerAccent();
            accent.a = 1f;
            foreach (var part in builderAvatar.GetComponentsInChildren<Renderer>(true))
            {
                if (part == builderAvatarSprite)
                {
                    part.enabled = true;
                    continue;
                }

                if (builderAvatarSprite != null)
                {
                    part.enabled = false;
                    continue;
                }

                part.material.color = part.gameObject.name switch
                {
                    "Body" => Cloud,
                    "Pack" => PanelInk,
                    _ => accent
                };
            }
        }

        private void CreateBuilderSpriteVisual()
        {
            var sprite = Resources.Load<Sprite>(BuilderSpriteResourcePath);
            if (sprite == null)
            {
                return;
            }

            var plate = new GameObject("AIPlateVisual");
            plate.transform.SetParent(builderAvatar.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.27f, 0.04f);
            plate.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plate.transform.localScale = new Vector3(0.145f, 0.145f, 1f);

            builderAvatarSprite = plate.AddComponent<SpriteRenderer>();
            builderAvatarSprite.sprite = sprite;
            builderAvatarSprite.sortingOrder = 12;

            foreach (var part in builderAvatar.GetComponentsInChildren<Renderer>(true))
            {
                if (part != builderAvatarSprite)
                {
                    part.enabled = false;
                }
            }
        }

        private void UpdateSelectionRing(TowerCombatState tower)
        {
            if (selectionRing == null)
            {
                selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                selectionRing.name = "SelectedTowerRangeRing";
            }

            selectionRing.SetActive(true);
            selectionRing.transform.position = GridToWorld(new Vector2Int(tower.Position.X, tower.Position.Y), 0.06f);
            selectionRing.transform.localScale = TowerSelectionRingScale(tower.TowerId.Value);
            selectionRing.GetComponent<Renderer>().material.color = TowerAccent(tower.TowerId.Value);
        }

        private void HideSelectionRing()
        {
            if (selectionRing != null)
            {
                selectionRing.SetActive(false);
            }
        }

        private Vector3 SelectedTowerGhostScale()
        {
            return SelectedTowerRoleId() switch
            {
                "control" => new Vector3(0.82f, 0.46f, 0.82f),
                "relay" => new Vector3(0.52f, 0.52f, 0.52f),
                "pulse" => new Vector3(0.86f, 0.44f, 0.86f),
                "prism" => new Vector3(0.48f, 1.0f, 0.48f),
                _ => new Vector3(0.62f, 0.78f, 0.62f)
            };
        }

        private string SelectedTowerRoleId()
        {
            return LTW.UnityClient.Simulation.TowerCatalog.ForRole(selectedTowerRole).RoleId;
        }

        // Unlike name/colour below, ring scale has no per-tower value in TowerCatalog to fall back to
        // — the fallback here is a deliberate shared default size for the 10 towers added since this
        // was written, not a wrong answer borrowed from Arrow's branch.
        private static Vector3 TowerSelectionRingScale(string towerId)
        {
            if (towerId.Contains("control")) return new Vector3(1.42f, 0.03f, 1.42f);
            if (towerId.Contains("relay") || towerId.Contains("economy")) return new Vector3(1.18f, 0.03f, 1.18f);
            if (towerId.Contains("pulse")) return new Vector3(1.62f, 0.03f, 1.62f);
            if (towerId.Contains("prism")) return new Vector3(2.12f, 0.03f, 2.12f);
            return new Vector3(1.28f, 0.03f, 1.28f);
        }

        private string SelectedTowerName()
        {
            return LTW.UnityClient.Simulation.TowerCatalog.ForRole(selectedTowerRole).DisplayName;
        }

        private static string TowerRoleName(string towerId) =>
            LTW.UnityClient.Simulation.TowerCatalog.ForContentId(towerId).DisplayName;

        private static Color TowerAccent(string towerId) =>
            LTW.UnityClient.Simulation.TowerCatalog.ForContentId(towerId).Accent;

        private Color SelectedTowerAccent()
        {
            return LTW.UnityClient.Simulation.TowerCatalog.ForRole(selectedTowerRole).Accent;
        }

        private void DrawTowerPalette(float scale)
        {
            if (isPlacing)
            {
                return;
            }

            if (!isPaletteExpanded && IsSendDockExpanded())
            {
                return;
            }

            var frame = MobileViewportLayout.ScreenRect();
            var launcherRect = TowerPaletteLauncherRect(scale, frame);
            if (!isPaletteExpanded)
            {
                if (DrawLauncherButton(launcherRect, "BUILD", MintSignal, scale))
                {
                    OpenTowerPalette();
                }

                return;
            }

            var rect = TowerPalettePanelRect(scale, frame);

            DrawPanel(rect, PanelInk);
            DrawAccent(new Rect(rect.x, rect.yMax - 4f * scale, rect.width, 4f * scale), MintSignal);
            if (DrawLauncherButton(launcherRect, "CLOSE", MintSignal, scale))
            {
                isPaletteExpanded = false;
                return;
            }

            titleStyle!.fontSize = Mathf.RoundToInt(12f * scale);
            titleStyle.normal.textColor = MintSignal;
            GUI.Label(new Rect(rect.x + 12f * scale, rect.y + 10f * scale, 120f * scale, 20f * scale), "BUILD", titleStyle);
            // The launcher slot above is already CLOSE while the palette is expanded; a header
            // CLOSE duplicated it. See the matching note in SendDockController.
            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);

            var buttonY = rect.y + 84f * scale;
            var buttonHeight = 84f * scale;
            var gap = 8f * scale;

            if (selectedTowerCategory < 0)
            {
                DrawTowerCategoryPicker(rect, buttonY, buttonHeight, gap, scale);
                return;
            }

            if (RuntimeUiChrome.DrawPanelButton(new Rect(rect.xMax - 72f * scale, rect.y + 8f * scale, 58f * scale, 32f * scale), "BACK", MintSignal, scale, buttonStyle))
            {
                selectedTowerCategory = -1;
                return;
            }

            DrawTowerCategoryGrid(rect, buttonY, buttonHeight, gap, scale);
        }

        /// <summary>
        /// Category chooser, mirroring the send dock. Card height is divided out of the panel's
        /// actual height so adding a category cannot push the last card off the panel.
        /// </summary>
        private void DrawTowerCategoryPicker(Rect rect, float buttonY, float buttonHeight, float gap, float scale)
        {
            var labels = LTW.UnityClient.Simulation.TowerCatalog.CategoryLabels;
            var cardHeight = RuntimeUiChrome.CategoryCardHeight(
                rect, buttonY, gap, labels.Length, buttonHeight, scale);
            var cardWidth = rect.width - 24f * scale;
            var x = rect.x + 12f * scale;

            for (var category = 0; category < labels.Length; category++)
            {
                var y = buttonY + category * (cardHeight + gap);
                var accent = CategoryAccent(category);
                var cardRect = new Rect(x, y, cardWidth, cardHeight);
                var pressed = RuntimeUiChrome.DrawCommandCard(cardRect, accent, CommandCardState.Normal, scale);

                buttonStyle!.fontSize = Mathf.RoundToInt(13f * scale);
                buttonStyle.normal.textColor = Cloud;
                GUI.Label(RuntimeUiChrome.CommandCardLabelRect(cardRect, scale), labels[category], buttonStyle);

                metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
                metaStyle.normal.textColor = accent;
                GUI.Label(RuntimeUiChrome.CommandCardMetaRect(cardRect, scale), "5 TOWERS", metaStyle);

                if (pressed)
                {
                    selectedTowerCategory = category;
                }
            }
        }

        /// <summary>
        /// The five towers of the selected category, laid out 3 over 2 like the send grids.
        /// </summary>
        /// <remarks>
        /// Driven off TowerCatalog rather than a hardcoded button per tower. The previous version
        /// spelled out each button with its own literal price, which is how the palette came to
        /// advertise 20 gold for a tower the simulation charged 14 for.
        /// </remarks>
        private void DrawTowerCategoryGrid(Rect rect, float buttonY, float buttonHeight, float gap, float scale)
        {
            var gold = CurrentPlayerGold();
            var entries = new System.Collections.Generic.List<LTW.UnityClient.Simulation.TowerCatalog.Entry>(
                LTW.UnityClient.Simulation.TowerCatalog.InCategory(selectedTowerCategory));
            if (entries.Count == 0)
            {
                return;
            }

            var firstRow = Mathf.Min(3, entries.Count);
            var firstRowWidth = (rect.width - 24f * scale - gap * (firstRow - 1)) / firstRow;
            var x = rect.x + 12f * scale;

            for (var index = 0; index < firstRow; index++)
            {
                DrawCatalogPaletteButton(new Rect(x, buttonY, firstRowWidth, buttonHeight), entries[index], gold, scale);
                x += firstRowWidth + gap;
            }

            var remaining = entries.Count - firstRow;
            if (remaining <= 0)
            {
                return;
            }

            var secondRowY = buttonY + buttonHeight + gap;
            var secondRowWidth = (rect.width - 24f * scale - gap * (remaining - 1)) / remaining;
            x = rect.x + 12f * scale;
            for (var index = firstRow; index < entries.Count; index++)
            {
                DrawCatalogPaletteButton(new Rect(x, secondRowY, secondRowWidth, buttonHeight), entries[index], gold, scale);
                x += secondRowWidth + gap;
            }
        }

        private void DrawCatalogPaletteButton(Rect buttonRect, LTW.UnityClient.Simulation.TowerCatalog.Entry entry, int gold, float scale)
        {
            var cost = commandAdapter != null ? commandAdapter.TowerCost(entry.Role) : 0;
            if (DrawCatalogCard(buttonRect, entry, cost, gold >= cost, highlightedTowerRole == entry.Role, scale))
            {
                selectedTower = null;
                BeginTowerPlacement(entry.Role);
            }
        }

        /// <summary>
        /// Palette card whose icon is resolved from the tower's content id.
        /// </summary>
        /// <remarks>
        /// DrawPaletteButton takes a TowerIconKind, a five-value enum from when there were five
        /// towers, so every new tower had to borrow one of the original pictures. All fifteen now
        /// have a rendered icon named after their content id, so the id is what we look up. The
        /// procedural DrawTowerIcon fallback still covers a missing file.
        /// </remarks>
        private bool DrawCatalogCard(Rect rect, LTW.UnityClient.Simulation.TowerCatalog.Entry entry, int cost, bool isAffordable, bool isSelected, float scale)
        {
            var displayAccent = isAffordable ? entry.Accent : DisabledText;
            var state = isSelected ? CommandCardState.Selected : CommandCardState.Normal;
            var pressed = RuntimeUiChrome.DrawCommandCard(rect, entry.Accent, state, scale);

            var iconRect = RuntimeUiChrome.CommandCardIconRect(rect, scale);
            if (!RuntimeUiIconLibrary.DrawIcon(iconRect, $"ui_icon_tower_{entry.RoleId}_v01", isAffordable))
            {
                DrawTowerIcon(iconRect, TowerIconForRole(entry.Role), displayAccent, scale);
            }

            buttonStyle!.fontSize = Mathf.RoundToInt(10f * scale);
            buttonStyle.normal.textColor = isAffordable ? Cloud : DisabledText;
            buttonStyle.hover.textColor = buttonStyle.normal.textColor;
            buttonStyle.active.textColor = buttonStyle.normal.textColor;
            GUI.Label(RuntimeUiChrome.CommandCardLabelRect(rect, scale), entry.ShortLabel, buttonStyle);

            metaStyle!.fontSize = Mathf.RoundToInt(9f * scale);
            metaStyle.normal.textColor = isAffordable
                ? new Color(
                    Mathf.Lerp(displayAccent.r, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.g, 1f, 0.55f),
                    Mathf.Lerp(displayAccent.b, 1f, 0.55f),
                    1f)
                : displayAccent;
            GUI.Label(RuntimeUiChrome.CommandCardMetaRect(rect, scale), $"{cost}G", metaStyle);

            return pressed;
        }

        private static Color CategoryAccent(int category) => category switch
        {
            1 => new Color(0.87f, 0.62f, 0.28f),
            2 => new Color(0.45f, 0.78f, 0.36f),
            _ => LTW.UnityClient.Simulation.TowerRolePalette.Arrow
        };

        /// <summary>
        /// Icon for a role. Only the original five have authored icons; the new towers fall back to
        /// the procedural shape closest to their silhouette until real icons are rendered.
        /// </summary>
        private static TowerIconKind TowerIconForRole(int role) => role switch
        {
            1 => TowerIconKind.Control,
            2 => TowerIconKind.Relay,
            3 => TowerIconKind.Pulse,
            4 => TowerIconKind.Prism,
            5 => TowerIconKind.Arrow,
            6 => TowerIconKind.Prism,
            7 => TowerIconKind.Pulse,
            8 => TowerIconKind.Control,
            9 => TowerIconKind.Relay,
            10 => TowerIconKind.Prism,
            11 => TowerIconKind.Arrow,
            12 => TowerIconKind.Relay,
            13 => TowerIconKind.Pulse,
            14 => TowerIconKind.Control,
            _ => TowerIconKind.Arrow
        };

        private void OpenTowerPalette()
        {
            CloseSendDock();
            isPaletteExpanded = true;
            selectedTowerCategory = -1;
        }

        private void CloseSendDock() => SendDock?.CloseDock();

        private bool IsSendDockExpanded() => SendDock?.IsExpanded == true;

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

        private static void DrawTowerIcon(Rect rect, TowerIconKind iconKind, Color accent, float scale)
        {
            var previousColor = GUI.color;
            GUI.color = accent;

            var cx = rect.x + rect.width * 0.5f;
            var cy = rect.y + rect.height * 0.52f;
            var line = Mathf.Max(2f * scale, 1f);

            switch (iconKind)
            {
                case TowerIconKind.Control:
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy - 5f * scale, 14f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy + 5f * scale, 14f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy - 5f * scale, line, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 5f * scale, cy - 5f * scale, line, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 3f * scale, cy - 1f * scale, 6f * scale, 6f * scale), Texture2D.whiteTexture);
                    break;
                case TowerIconKind.Relay:
                    GUI.DrawTexture(new Rect(cx - line * 0.5f, cy - 11f * scale, line, 22f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 6f * scale, cy + 8f * scale, 12f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 4f * scale, cy - 11f * scale, 8f * scale, 8f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 9f * scale, cy - 1f * scale, 4f * scale, 12f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 5f * scale, cy - 1f * scale, 4f * scale, 12f * scale), Texture2D.whiteTexture);
                    break;
                case TowerIconKind.Pulse:
                    GUI.DrawTexture(new Rect(cx - 10f * scale, cy - 7f * scale, 20f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 7f * scale, cy + 2f * scale, 14f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 4f * scale, cy - 3f * scale, 8f * scale, 8f * scale), Texture2D.whiteTexture);
                    break;
                case TowerIconKind.Prism:
                    GUI.DrawTexture(new Rect(cx - 3f * scale, cy - 13f * scale, 6f * scale, 24f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 8f * scale, cy - 4f * scale, 16f * scale, line), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 5f * scale, cy + 8f * scale, 10f * scale, line), Texture2D.whiteTexture);
                    break;
                default:
                    GUI.DrawTexture(new Rect(cx - line * 0.5f, cy - 12f * scale, line, 24f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 9f * scale, cy - 6f * scale, line, 15f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 7f * scale, cy - 6f * scale, line, 15f * scale), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 5f * scale, cy - 11f * scale, 10f * scale, line), Texture2D.whiteTexture);
                    break;
            }

            GUI.color = previousColor;
        }

        private static bool DrawLauncherButton(Rect rect, string label, Color accent, float scale)
        {
            var style = buttonStyle ?? GUI.skin.button;
            buttonStyle!.fontSize = Mathf.RoundToInt(11f * scale);
            return rect.width > rect.height * 1.35f
                ? RuntimeUiChrome.DrawPanelButton(rect, label, accent, scale, style)
                : RuntimeUiChrome.DrawLauncherButton(rect, label, accent, scale, style);
        }

        private string PlacementPreviewText()
        {
            return placementPreview.RejectionReason switch
            {
                CommandRejectionReason.InsufficientGold => "NEED GOLD",
                CommandRejectionReason.CellOccupied => "CELL OCCUPIED",
                CommandRejectionReason.PathBlocked => "PATH BLOCKED",
                CommandRejectionReason.InvalidLane => "OUTSIDE YOUR LINE",
                _ => "CANNOT PLACE"
            };
        }

        private string PlacementRecoveryText()
        {
            return placementPreview.RejectionReason switch
            {
                CommandRejectionReason.InsufficientGold => "Send less or wait for income",
                CommandRejectionReason.CellOccupied => "Pick an empty grid cell",
                CommandRejectionReason.PathBlocked => "Leave a route from spawn to exit",
                CommandRejectionReason.InvalidLane => "Tap inside the highlighted lane",
                _ => "Try a different cell"
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

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                margin = ZeroOffset(),
                padding = ZeroOffset(),
                normal = { textColor = Cloud },
                hover = { textColor = Cloud },
                active = { textColor = Cloud }
            };

            metaStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
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

        private static string TowerPurpose(string towerId)
        {
            if (towerId.Contains("control")) return "Area control and clustered pressure";
            if (towerId.Contains("relay") || towerId.Contains("economy")) return "Utility pressure and income support";
            if (towerId.Contains("pulse")) return "Short-range burst against dense pressure";
            if (towerId.Contains("prism")) return "Long-range focus against priority pressure";
            return "Focused single-target defense";
        }

        private static RectOffset ZeroOffset() => new RectOffset(0, 0, 0, 0);

        private enum TowerIconKind
        {
            Arrow,
            Control,
            Relay,
            Pulse,
            Prism
        }

        private bool IsSelectedCellInBounds() =>
            selectedCell.x >= 0 && selectedCell.x < LaneWidth && selectedCell.y >= 0 && selectedCell.y < LaneLength;

        private bool IsPointerOverRuntimeUi(Vector2 screenPosition)
        {
            var scale = MobileViewportLayout.UiScale();
            var frame = MobileViewportLayout.ScreenRect();
            var guiPoint = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            if (TowerPaletteLauncherRect(scale, frame).Contains(guiPoint))
            {
                return true;
            }

            if (isPaletteExpanded && TowerPalettePanelRect(scale, frame).Contains(guiPoint))
            {
                return true;
            }

            if (isPlacing && PlacementPanelRect(scale, frame).Contains(guiPoint))
            {
                return true;
            }

            return selectedTower is not null && SelectedTowerPanelRect(scale, frame).Contains(guiPoint);
        }

        private void RefreshSelectedTowerFromSnapshot()
        {
            if (selectedTower is null)
            {
                return;
            }

            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
            }

            var snapshot = simulationDriver?.LatestSnapshot;
            var current = snapshot?.Towers.FirstOrDefault(tower => tower.EntityId.Equals(selectedTower.EntityId));
            if (current is null)
            {
                selectedTower = null;
                HideSelectionRing();
                return;
            }

            selectedTower = current;
        }

        private static Rect TowerPaletteLauncherRect(float scale, Rect frame)
        {
            var launcherWidth = 76f * scale;
            var launcherHeight = 44f * scale;
            return new Rect(frame.x + 12f * scale, frame.yMax - launcherHeight - MobileViewportLayout.BottomMargin(scale), launcherWidth, launcherHeight);
        }

        private static Rect TowerPalettePanelRect(float scale, Rect frame)
        {
            var width = Mathf.Min(frame.width - 16f * scale, 430f * scale);
            var height = 282f * scale;
            var launcherClearance = 136f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale) - launcherClearance, width, height);
        }

        private static Rect PlacementPanelRect(float scale, Rect frame)
        {
            var width = Mathf.Min(frame.width - 16f * scale, 360f * scale);
            var height = 160f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - MobileViewportLayout.BottomMargin(scale), width, height);
        }

        private static Rect SelectedTowerPanelRect(float scale, Rect frame)
        {
            var width = Mathf.Min(frame.width - 16f * scale, 360f * scale);
            var height = 112f * scale;
            return new Rect(frame.x + 8f * scale, frame.yMax - height - 112f * scale, width, height);
        }
    }
}
