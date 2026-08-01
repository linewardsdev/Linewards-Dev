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
        private readonly System.Collections.Generic.Dictionary<string, GameObject?> ghostModels = new();
        private TowerVisualLibrary? towerVisualLibrary;
        private Material? ghostMaterial;

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
            ghost.transform.position = GridToWorld(selectedCell, 0.6f);
            ConfigurePlacementGhostVisual();
            // A resolved model already carries its profile's own scale, so the root has to stay at
            // one or the two multiply and the preview comes out larger than the placed tower.
            ghost.transform.localScale = HasGhostModel() ? Vector3.one : SelectedTowerGhostScale();
            RefreshPlacementPreview();
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
    }
}
