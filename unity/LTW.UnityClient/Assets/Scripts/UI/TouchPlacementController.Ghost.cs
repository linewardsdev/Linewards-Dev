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
        /// Tints the one material every ghost renderer shares: the role's accent while the cell
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
            var color = placementPreview.Accepted ? SelectedTowerAccent() : Danger;
            color.a = placementPreview.Accepted ? 0.74f : 0.86f;
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
            model.transform.localScale = profile.HasScale ? profile.Scale : Vector3.one;

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
        /// Puts a range ring under every selected tower, growing the pool as the selection does.
        /// </summary>
        /// <remarks>
        /// Was a single GameObject, which was right while exactly one tower could be selected. With
        /// a multi-selection the ring has to be per tower or the board shows one highlighted tower
        /// out of five and the player has no way to see what a batch is about to act on.
        /// </remarks>
        private void ShowSelectionRings(IReadOnlyList<TowerCombatState> towers)
        {
            for (var index = 0; index < towers.Count; index++)
            {
                if (index >= selectionRings.Count)
                {
                    var created = RenderCompat.CreatePrimitive(PrimitiveType.Cylinder);
                    created.name = "SelectedTowerRangeRing";
                    selectionRings.Add(created);
                }

                var ring = selectionRings[index];
                var tower = towers[index];
                ring.SetActive(true);
                ring.transform.position = GridToWorld(new Vector2Int(tower.Position.X, tower.Position.Y), 0.06f);
                ring.transform.localScale = TowerSelectionRingScale(tower.TowerId.Value);
                ring.GetComponent<Renderer>().material.color = TowerAccent(tower.TowerId.Value);
            }

            for (var index = towers.Count; index < selectionRings.Count; index++)
            {
                selectionRings[index].SetActive(false);
            }
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
    }
}
