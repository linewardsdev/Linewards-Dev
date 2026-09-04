#nullable enable

using System.Collections.Generic;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Combat;
using LTW.Simulation.Primitives;
using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    /// <summary>
    /// The builder avatar that walks to the cell being placed on.
    /// </summary>
    public sealed partial class TouchPlacementController
    {
        private const string BuilderSpriteResourcePath = "Art/Builder/Production/Sprites/builder_candidate_v01_trimmed";
        private const string BuilderModelResourcePath = "Prefabs/Builder/Builder_3D";

        private GameObject builderAvatar = null!;
        private SpriteRenderer? builderAvatarSprite;
        private Animator? builderAvatarAnimator;

        private const float BuilderWalkSpeed = 4.5f;
        private const float BuilderWalkBobAmplitude = 0.05f;
        private const float BuilderWalkBobFrequency = 9f;
        private const float BuilderWalkTurnDegreesPerSecond = 720f;

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
                // Raw mesh stands ~2.2 units tall. 0.4 put it at ~0.88 world units — under a single
                // board cell, which read as a dropped prop rather than as the unit doing the work,
                // especially next to towers that occupy most of their own cell. 0.6 (~1.32 units)
                // cleared a cell but, per the 2026-09-01 render review (finding #7), that made the
                // builder taller than every tower once towers were normalised to 0.75-0.95 of a
                // cell — it was the biggest thing on the board. 0.36 puts it at ~0.79 units, in
                // that same band rather than looming over the towers it builds.
                instance.transform.localScale = new Vector3(0.36f, 0.36f, 0.36f);
                builderAvatarAnimator = instance.GetComponentInChildren<Animator>(true);
                return;
            }

            // Fallback if the 3D model asset is missing for any reason (e.g. a build stripped
            // Resources content it shouldn't have) — the original primitive-and-sprite avatar.
            // 0.8 matches the rigged model's post-render-review size (roughly 0.8 of a board
            // cell); this path was still at the old 1.35 and would have shipped an oversized
            // builder if it were ever the one actually loaded.
            builderAvatar.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
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

            var target = BuilderGroundPosition(BuilderTargetCell);
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
                    ghost.transform.position = GhostWorldPosition();
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
            var part = RenderCompat.CreatePrimitive(primitiveType);
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

        // The lane's default column (LaneWidth / 2) is also its unmazed spawn-to-exit route, so an
        // idle builder parked there sits dead centre on the path — and on whatever the player has
        // already built there (render review finding #7: "parks on the Relay ward"). LaneWidth-1 is
        // the lane's east edge column: still a real, buildable cell (ClampToLane already treats
        // LaneWidth-1 as in-range), just one that is not the default route and reads as a gutter
        // next to the board's own EastGutter dressing. Only the IDLE rest position moves; while
        // isPlacing is true the builder still walks to selectedCell exactly as before.
        private static Vector2Int BuilderRestCell => new(LaneWidth - 1, LaneLength - 3);

        private Vector3 BuilderGroundPosition(Vector2Int cell) => GridToWorld(cell, 0.02f) + BuilderRestOffset;

        private Vector2Int BuilderTargetCell => isPlacing ? selectedCell : BuilderRestCell;

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
                builderAvatar.transform.position = BuilderGroundPosition(BuilderTargetCell);
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
    }
}
