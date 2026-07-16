# UI And Board Art Pass V01

Date: 2026-07-16

## Purpose

Extend the V1 art pipeline from towers, creeps, and Builder into the player-facing UI and game board without changing gameplay rules. This pass prioritizes recognition and material hierarchy over decorative detail.

## Runtime Changes

### Build And Send Icons

The build and send menu cards now load Resources sprites derived from the current V1 runtime silhouettes. The old procedural IMGUI glyphs remain as fallback if a sprite is missing.

Runtime helper:

- `unity/LTW.UnityClient/Assets/Scripts/UI/RuntimeUiIconLibrary.cs`

Resources icon sprites:

- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_tower_arrow_v01.png`
- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_tower_control_v01.png`
- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_tower_relay_v01.png`
- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_tower_pulse_v01.png`
- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_tower_prism_v01.png`
- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_send_runner_v01.png`
- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_send_brute_v01.png`
- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_send_swarm_v01.png`
- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_send_shade_v01.png`
- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/ui_icon_send_siege_v01.png`

Review evidence:

- `docs/art-pipeline/ui-board-pass-v01-icon-review.png`

### Board Material Polish

The board remains procedural in `UnityVerticalSliceRenderer`, but V01 adds authored-material cues that should survive phone framing:

- thin build-band edge lines so placeable side zones read as deliberate plates;
- a restrained center route inlay so the creep path reads as constructed board material;
- spawn/life-loss endpoint chevrons so entry and leak plates are clearer without relying only on labels.

These additions must stay quieter than towers, creeps, projectiles, health/readability cues, and HUD decisions.

## Acceptance For Next Review

- Build menu icons match in-lane tower silhouettes at card size.
- Send menu icons match in-lane creep silhouettes at card size.
- Icons remain readable when disabled/too expensive.
- Board route remains clear in active-lane and map views.
- Build side bands read as placement zones without bright labels.
- Spawn and life-loss plates remain understandable when labels are visually ignored.
- Grayscale captures preserve UI icon, board, tower, creep, and projectile separation.

## Still Open

- Fresh Unity screenshot QA for default HUD, build menu open, send menu open, lane selector open, heavy pressure, and grayscale.
- Final icon paint pass if the derived full-color sprites prove too detailed at actual phone scale.
- Authored board texture/mesh replacement after the procedural pass survives gameplay review.
