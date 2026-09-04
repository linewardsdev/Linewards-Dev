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
    /// <summary>
    /// The placement ghost and the tower selection rings: the world-space objects that show
    /// where a tower would land and which towers are currently selected.
    /// </summary>
    public sealed partial class TouchPlacementController
    {
        /// <summary>
        /// One instantiated ghost per tower content id, or null once a role has been found to have
        /// no drawable model — the null is cached so the error below is logged once, not on every
        /// nudge of the cursor.
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<string, GameObject?> ghostModels = new();
        private TowerVisualLibrary? towerVisualLibrary;
        private Material? ghostMaterial;

        private const string GhostMaterialResourcePath = "Art/UI/Materials/PlacementGhost";

        private readonly List<GameObject> selectionRings = new();

        /// <summary>
        /// Rest diameter of the ring at the same index in <see cref="selectionRings"/>, before the
        /// pulse; <see cref="PulseSelectionRings"/> writes the scaled value to the transform each
        /// frame, so the transform itself cannot be the place the rest size is remembered.
        /// </summary>
        private readonly List<float> selectionRingDiameters = new();

        // World Y of the selection ring: BoardTopY (-0.12, private to UnityVerticalSliceRenderer)
        // plus 0.01 — above the board face so it never z-fights it, below the tower's own
        // accessories (the range halo sits at PlacedTowerBaseY - 0.06 = -0.10). The old disc sat at
        // 0.06, a full 0.18 above the board, which is part of why it floated like a marker.
        private const float SelectionRingY = -0.11f;
        private const float SelectionRingAlpha = 0.5f;

        // The contact-shadow shader fades from (1 - softness) of the radius to the rim, squared.
        // MechanicRingMesh's inner rim sits at 0.65 of the radius and must stay inside the full-
        // strength plateau (softness < 0.35) to be a clean cut; 0.28 leaves the band full from
        // 0.65 to 0.72 and soft over the outer 28%, so the ring reads thinner than the mesh's 35%.
        private const float SelectionRingSoftness = 0.28f;

        // Board units between the tower's footprint radius and the ring's outer radius.
        private const float SelectionRingClearance = 0.15f;
        private const float SelectionRingPulseAmplitude = 0.05f;
        private const float SelectionRingPulsePeriod = 1.2f;

        private void MoveGhost()
        {
            // The builder now walks along with tower placement instead of vanishing for it, so
            // the ghost preview shouldn't appear at the destination the instant a new cell is
            // picked either — it stays hidden until the builder actually arrives there
            // (TickBuilderWalk re-enables it once the walk finishes), reading as the builder
            // setting the tower down rather than a preview floating in ahead of them.
            UpdateBuilderAvatar();
            ghost.SetActive(false);
            ghost.transform.position = GhostWorldPosition();
            ConfigurePlacementGhostVisual();
            RefreshPlacementPreview();
        }

        /// <summary>
        /// Where the ghost root stands: the floor anchor the renderer gives a placed prefab, so the
        /// preview is at the height the tower will actually land at rather than hovering above it.
        /// The profile's own lift is applied to the model underneath, exactly as the renderer does.
        /// </summary>
        private Vector3 GhostWorldPosition() => GridToWorld(selectedCell, UnityVerticalSliceRenderer.PlacedTowerBaseY);

        /// <summary>
        /// Tints the one material every ghost renderer shares: the mint "ok" signal while the cell
        /// would be accepted, the danger red while it would be rejected.
        /// </summary>
        /// <remarks>
        /// Written through <see cref="RenderCompat.SetAlbedo"/> rather than <c>Material.color</c>
        /// so the tint lands on URP's <c>_BaseColor</c>, which is the property the ghost material's
        /// shader actually reads. Alpha is honoured because that material is a transparent surface;
        /// on the towers' own opaque stylized shader it was silently discarded.
        /// </remarks>
        private void UpdateGhostColor()
        {
            // R6 (2026-09-02 re-audit): at 0.74 / 0.86 the ghost read as a solid violet tower at
            // shipped distance, and the role accent said nothing about legality. Validity is the
            // whole message now: MintSignal / Danger, the same pair the BUILD button uses, at one
            // alpha low enough that the board shows through.
            var color = placementPreview.Accepted ? MintSignal : Danger;
            color.a = 0.45f;
            var material = GhostMaterial();
            if (material != null)
            {
                RenderCompat.SetAlbedo(material, color);
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
        ///
        /// There is no primitive fallback any more. The ghost root is an empty, so a role whose
        /// model cannot be built draws nothing and logs an error naming the role — the 2026-09-01
        /// render review found the old fallback disc reading as a finished feature, which is worse
        /// than an absent preview because nobody reports it.
        /// </remarks>
        private void ConfigurePlacementGhostVisual()
        {
            var entry = TowerCatalog.ForRole(selectedTowerRole);
            foreach (var cached in ghostModels)
            {
                if (cached.Value != null)
                {
                    cached.Value.SetActive(cached.Key == entry.ContentId);
                }
            }

            if (ghostModels.ContainsKey(entry.ContentId))
            {
                return;
            }

            ghostModels[entry.ContentId] = BuildGhostModel(entry);
        }

        /// <summary>
        /// Instantiates the role's tower prefab under the ghost root, resolved the same way the
        /// board renderer resolves a placed tower: the catalog's simulation content id looked up
        /// in the shared <see cref="TowerVisualLibrary"/>.
        /// </summary>
        private GameObject? BuildGhostModel(TowerCatalog.Entry entry)
        {
            if (towerVisualLibrary == null)
            {
                towerVisualLibrary = TowerVisualLibrary.LoadDefault();
            }

            var profile = towerVisualLibrary != null ? towerVisualLibrary.FindProfile(entry.ContentId) : null;
            if (profile == null || profile.Prefab == null)
            {
                Debug.LogError(
                    $"PLACEMENT GHOST '{entry.ContentId}' ({entry.DisplayName}) has no visual profile with a prefab " +
                    $"in Resources/{TowerVisualLibrary.DefaultResourcePath}; the ghost stays hidden for this role.");
                return null;
            }

            var material = GhostMaterial();
            if (material == null)
            {
                // Logged by GhostMaterial. Hidden rather than drawn with the tower's own opaque
                // materials, which would look like a tower already standing there.
                return null;
            }

            var model = Instantiate(profile.Prefab, ghost.transform);
            model.name = $"GhostModel_{entry.RoleId}";
            model.transform.localPosition = Vector3.up * profile.Lift;
            model.transform.localRotation = Quaternion.identity;
            // Same root scale the renderer's SetTowerTransform gives a placed instance (the
            // Control ward: 0.75 on the prefab root, Body left at 1 — Arcane has no breathe
            // amplitude). The ghost root and the match root are both unit-scaled, so the chains
            // are equal; the 2026-09-02 re-audit's "1.3x" is the close-up framing, not a scale.
            // The no-scale fallback still diverges (renderer: TowerRoleScale; here: one), but
            // every shipped profile authors a scale, so it is logged rather than mirrored.
            model.transform.localScale = profile.HasScale ? profile.Scale : Vector3.one;
            if (!profile.HasScale)
            {
                Debug.LogWarning($"PLACEMENT GHOST '{entry.ContentId}' profile has no scale; the ghost draws at 1 while a placed tower uses the renderer's role fallback.");
            }

            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            // The owner pool and range halo are board decals the renderer colours per owner and
            // per range; re-tinted as ghost they are a flat plate under the model, not a tower.
            HideGhostAccessory(model, profile.OwnerTrimRendererPath);
            HideGhostAccessory(model, profile.RangeHaloRendererPath);

            // One shared translucent material across the whole model, on EVERY material slot —
            // assigning sharedMaterial alone leaves a multi-material renderer's other submeshes
            // opaque. Keeping the tower's own materials would make the preview look like a
            // finished tower already standing there, which is exactly the confusion a ghost has
            // to avoid.
            foreach (var modelRenderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = modelRenderer.sharedMaterials;
                for (var index = 0; index < slots.Length; index++)
                {
                    slots[index] = material;
                }

                modelRenderer.sharedMaterials = slots;
                modelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                modelRenderer.receiveShadows = false;
            }

            return model;
        }

        private static void HideGhostAccessory(GameObject model, string rendererPath)
        {
            if (string.IsNullOrWhiteSpace(rendererPath))
            {
                return;
            }

            var accessory = model.transform.Find(rendererPath);
            if (accessory != null)
            {
                accessory.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The ghost's material: a runtime copy of the PlacementGhost asset, shared by every
        /// renderer of every cached ghost model so one tint write recolours the whole preview.
        /// </summary>
        /// <remarks>
        /// An asset rather than <c>Shader.Find</c> at runtime, for two reasons. The previous
        /// <c>Sprites/Default</c> pick was unlit with depth writes off, so a tower collapsed into
        /// its flat silhouette and every overlapping part stacked toward opaque — the Control ward
        /// read as two solid violet discs. And a material in Resources carries its shader into the
        /// build, where a <c>Shader.Find</c> of something nothing else references returns null.
        ///
        /// Copied, not used directly: tinting the loaded asset would dirty it in the editor.
        /// </remarks>
        private Material? GhostMaterial()
        {
            if (ghostMaterial != null)
            {
                return ghostMaterial;
            }

            var asset = Resources.Load<Material>(GhostMaterialResourcePath);
            if (asset == null)
            {
                Debug.LogError($"PLACEMENT GHOST material is missing at Resources/{GhostMaterialResourcePath}; ghosts stay hidden.");
                return null;
            }

            ghostMaterial = new Material(asset) { name = "PlacementGhost (runtime)" };
            return ghostMaterial;
        }

        private void UpdateSelectionRing(TowerCombatState tower) => ShowSelectionRings(new[] { tower });

        /// <summary>
        /// Puts a selection ring under every selected tower, growing the pool as the selection does.
        /// </summary>
        /// <remarks>
        /// Was a single GameObject, which was right while exactly one tower could be selected. With
        /// a multi-selection the ring has to be per tower or the board shows one highlighted tower
        /// out of five and the player has no way to see what a batch is about to act on.
        ///
        /// A ring, not a disc. The 2026-09-02 re-audit (OPEN_ITEMS item 53, frames
        /// 33-selected-tower-ring / 36-tier-pair-closeup) found the primitive cylinder here was an
        /// opaque, hard-edged, light-blue plate about 1.6 cells across that hid the board under the
        /// tower and read as a debug marker. It is now the renderer's own board-decal vocabulary:
        /// <see cref="BoardRenderResources.MechanicRingMesh"/> (the braked-cell annulus, inner
        /// radius 65% of outer, so the board shows through the middle) on a
        /// <see cref="BoardRenderResources.CreateContactShadowMaterial"/> whose soft rim fades out
        /// rather than cutting, tinted with the tower's accent at <see cref="SelectionRingAlpha"/>.
        /// The transform holds only position; scale is written by <see cref="PulseSelectionRings"/>.
        /// </remarks>
        private void ShowSelectionRings(IReadOnlyList<TowerCombatState> towers)
        {
            for (var index = 0; index < towers.Count; index++)
            {
                if (index >= selectionRings.Count)
                {
                    selectionRings.Add(CreateSelectionRing());
                    selectionRingDiameters.Add(0f);
                }

                var ring = selectionRings[index];
                var tower = towers[index];
                var accent = TowerAccent(tower.TowerId.Value);
                accent.a = SelectionRingAlpha;
                ring.SetActive(true);
                ring.transform.position = GridToWorld(new Vector2Int(tower.Position.X, tower.Position.Y), SelectionRingY);
                selectionRingDiameters[index] = TowerSelectionRingDiameter(tower.TowerId.Value);
                // sharedMaterial is this ring's own instance (see CreateSelectionRing), so the write
                // recolours one ring and clones nothing.
                ring.GetComponent<Renderer>().sharedMaterial.color = accent;
            }

            for (var index = towers.Count; index < selectionRings.Count; index++)
            {
                selectionRings[index].SetActive(false);
            }

            // Sized now rather than at the next LateUpdate, so a ring shown this frame never draws
            // one frame at whatever scale its last owner left it.
            PulseSelectionRings();
        }

        /// <summary>
        /// One annulus with its own soft-edged decal material. Per ring, not shared across the
        /// pool: every selected tower carries its own accent, and one material would recolour
        /// every ring to the last tower's.
        /// </summary>
        private static GameObject CreateSelectionRing()
        {
            var ring = new GameObject("SelectedTowerRing");
            ring.AddComponent<MeshFilter>().sharedMesh = BoardRenderResources.MechanicRingMesh;
            var ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = BoardRenderResources.CreateContactShadowMaterial(
                "SelectedTowerRing (runtime)",
                Color.white,
                SelectionRingSoftness);
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
            return ring;
        }

        private void HideSelectionRing()
        {
            for (var index = 0; index < selectionRings.Count; index++)
            {
                if (selectionRings[index] != null)
                {
                    selectionRings[index].SetActive(false);
                }
            }
        }

        /// <remarks>
        /// Here rather than a line in <c>Update</c> (TouchPlacementController.cs): that method
        /// returns early on almost every frame — no tap, or an eliminated seat — and the ring has
        /// to breathe on all of them. No other part of the class defines LateUpdate.
        /// </remarks>
        private void LateUpdate() => PulseSelectionRings();

        /// <summary>
        /// The rings' slow breathe: ±<see cref="SelectionRingPulseAmplitude"/> of radius over a
        /// <see cref="SelectionRingPulsePeriod"/> cycle. Hidden rings are left alone; they are
        /// resized the moment <see cref="ShowSelectionRings"/> brings them back.
        /// </summary>
        private void PulseSelectionRings()
        {
            var breathe = 1f + SelectionRingPulseAmplitude * Mathf.Sin(Time.time * (2f * Mathf.PI / SelectionRingPulsePeriod));
            for (var index = 0; index < selectionRings.Count; index++)
            {
                var ring = selectionRings[index];
                if (ring == null || !ring.activeSelf)
                {
                    continue;
                }

                // MechanicRingMesh is unit-diameter, so the XZ scale IS the world diameter.
                var diameter = selectionRingDiameters[index] * breathe;
                ring.transform.localScale = new Vector3(diameter, 1f, diameter);
            }
        }

        private static float TowerSelectionRingDiameter(string towerId) =>
            2f * (TowerFootprintRadius(towerId) + SelectionRingClearance);

        // Footprint RADIUS in board units — a placed tower stands in one 1-unit cell, so these are
        // fractions of half a cell. Estimates in the per-role ordering the old disc table used
        // (Control's ward is the 0.75-scaled prefab; Prism the widest), not measurements: nothing
        // reachable from here measures a placed prefab's bounds (the renderer's unitFootprints is
        // private). As before, the fallback is a deliberate shared default for the roles without a
        // branch, not a wrong answer borrowed from Arrow's.
        private static float TowerFootprintRadius(string towerId)
        {
            if (towerId.Contains("control")) return 0.36f;
            if (towerId.Contains("relay") || towerId.Contains("economy")) return 0.33f;
            if (towerId.Contains("pulse")) return 0.44f;
            if (towerId.Contains("prism")) return 0.50f;
            return 0.40f;
        }
    }
}
