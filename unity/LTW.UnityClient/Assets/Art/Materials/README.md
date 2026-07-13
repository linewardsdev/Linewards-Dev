# Material Palette

Shared material definitions and material notes belong here.

Use names from `docs/ART_ASSET_PIPELINE.md`, including `mat_team_*`, `mat_role_*`, `mat_signal_*`, and `mat_ui_*`.

## Board Material Targets

The runtime board pass currently uses generated primitive materials in `UnityVerticalSliceRenderer`.
When these become imported materials, keep the same readability hierarchy:

| Material Name | Intent | Readability Target |
| --- | --- | --- |
| `mat_board_deep_field` | Lane backplate and outer board field. | Darkest board value; should recede behind cells, towers, creeps, and HUD. |
| `mat_board_build_band` | Buildable side bands. | Slightly brighter than the field, but clearly darker than the center route. |
| `mat_board_route_core` | Main creep route. | Brightest board surface; continuous north-south path at phone size. |
| `mat_board_route_guide` | Thin route edge guides. | Helps the route read in grayscale without becoming a wall. |
| `mat_board_spawn_gate` | Spawn/entry landmark. | Mint entry cue; must not be confused with leak/life-loss. |
| `mat_board_leak_gate` | Exit/life-loss landmark. | Red danger cue; should read even when creeps and effects are active. |
| `mat_board_owner_rail` | Lane ownership frame. | Player/opponent accent that frames the lane without overpowering gameplay objects. |

For the first production board material set, prioritize value separation before texture detail.
Avoid noisy trims inside playable cells until heavy-send screenshots prove creeps and towers stay readable.
