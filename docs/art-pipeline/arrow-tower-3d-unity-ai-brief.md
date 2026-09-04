# Arrow Tower 3D Unity AI Brief

Date: 2026-07-18
Owner: Codex + Unity AI Assistant
Status: ready for Unity AI generation experiment

## Purpose

Convert the approved Arrow tower design drawing into a real Unity-ready 3D tower model, replacing the current flat `Tower_Arrow_AIPlate` proof sprite only if the generated model improves in-game readability.

This is a targeted proof of whether Unity AI can turn the Line Wards source drawings into proper authored 3D assets without losing mobile clarity.

## Source Drawing

Use this as the primary attached image in Unity AI Assistant:

`unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_arrow_source_plate_v03.png`

Reference/current runtime proof sprite:

`unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed.png`

Existing runtime prefab to compare against:

`unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Arrow_AIPlate.prefab`

## Why Arrow First

- It is a common tower and should be instantly readable.
- Its silhouette has clear 3D parts: base, central lens, forward rail, arrowhead, side bow arms.
- It can be a static model with simple optional animation, so the first experiment avoids rigging complexity.
- The current sprite proof looks better than primitives, but it is still a flat plate. A good 3D model should feel more embedded in the board.

## Required Prefab Contract

The final Unity prefab must preserve the runtime child names from `docs/ART_PREFAB_CONTRACT.md`.

Required children:

| Child | Requirement |
| --- | --- |
| `Body` | Main 3D model or parent object for the visible tower mesh. |
| `RoleMarker` | Small readable role accent; can be render-disabled if the model itself carries the role. |
| `OwnerTrim` | Tintable ownership accent. |
| `RangeHalo` | Existing selection/range readability ring, usually kept as a simple flat helper. |

Arrow-specific optional children:

| Child | Requirement |
| --- | --- |
| `Muzzle` | Empty or small visible marker at the forward rail/bolt tip for attack VFX origin. |
| `BowLeft` | Left crescent/arc limb if separated. |
| `BowRight` | Right crescent/arc limb if separated. |
| `Lens` | Central glowing core/lens if separated. |

## Target Output Folder

Stage generated models here first:

`unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Arrow/`

If accepted, promote production-ready files to:

`unity/LTW.UnityClient/Assets/Art/Towers/Arrow/`

Runtime prefab target:

`unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Arrow_3D.prefab`

Do not overwrite `Tower_Arrow_AIPlate.prefab` until the 3D version wins an in-game review.

## Unity AI Assistant Prompt

Use Agent mode only after creating a Unity checkpoint or confirming Git status is clean enough to review. Attach `tower_arrow_source_plate_v03.png`.

```text
Create a Unity-ready 3D model variant for the Line Wards Arrow tower using the attached source drawing as the visual reference.

Goal: a mobile-readable ward-tech fantasy arrow/rail tower for a tower-wars board. It should feel like a real 3D asset, not a flat sprite.

Core design features to preserve:
- compact circular/hex stone-metal base
- central glowing blue/cyan lens core
- long forward luminous bolt rail or arrow barrel
- clear forward arrowhead/muzzle direction
- left and right crescent bow arms
- dark slate metal/stone body
- signal-gold bevel accents
- cyan/blue energy channel
- small mint gems only where they help readability

Production constraints:
- original Line Wards ward-tech fantasy style
- not Warcraft-like, not medieval faction architecture, no copied game silhouette
- readable from a top-down three-quarter mobile camera
- low-to-moderate polygon count suitable for mobile
- simple materials: dark slate, gold trim, cyan emissive glass/energy, mint accent
- no tiny surface noise that disappears at phone scale
- pivot centered at base, model roughly fits one board cell
- forward/muzzle direction should point toward local +Z if possible
- create separate named child objects where practical:
  Body, Lens, BowLeft, BowRight, Muzzle, OwnerTrim, RoleMarker, RangeHalo

Please generate 3 distinct 3D variants. Favor strong silhouette and clean geometry over ornate detail. Keep each variant as a prefab or model asset in:
Assets/Art/AIStaging/Models/Towers/Arrow/
```

## Review Criteria

The 3D Arrow model is better than the current sprite only if it passes all of these:

- Reads as "focused arrow / precision tower" at active-lane phone scale.
- Does not look like a generic sword, rocket, cannon, or crystal tower.
- Forward muzzle direction is obvious.
- Side bow arms remain visible after scaling.
- Lens/core remains visible in grayscale.
- It does not obscure creeps, build tiles, selection halos, or nearby UI.
- It looks integrated into the board rather than pasted on top.
- The model keeps or improves attack VFX origin clarity.

## Codex Integration Plan After Unity AI Generates Variants

1. Inspect generated assets and pick one candidate for runtime proof.
2. Create `Tower_Arrow_3D.prefab` with the required child names.
3. Preserve `Tower_Arrow_AIPlate.prefab` as rollback.
4. Wire `tower.arrow` in `TowerVisualLibrary.asset` to the 3D proof prefab only for the review pass.
5. Run `dotnet build LTW.sln`.
6. Let Unity reload/import and check `unity/LTW.UnityClient/Logs/Editor.log`.
7. Capture active-lane tower proof evidence.
8. Promote only if the 3D model clearly beats the existing AIPlate sprite.

