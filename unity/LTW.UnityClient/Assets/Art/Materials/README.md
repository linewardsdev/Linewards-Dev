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

## 5x2 Role Material Targets

The 2000-baseline pass should use a small material kit before introducing detailed textures.
Keep role identity readable in grayscale first, then reinforce with hue.

| Material Name | Role | Value / Color Intent | Notes |
| --- | --- | --- | --- |
| `mat_role_tower_arrow` | Arrow Ward | Bright mint/blue emitter on dark body. | Focused single-target read; narrow/tall highlight. |
| `mat_role_tower_control` | Control Ward | Cool pale blue/violet ring. | Should read as field/control, not raw damage. |
| `mat_role_tower_relay` | Relay Ward | Gold + mint signal accents. | Economy/support identity; avoid making it look like a damage tower. |
| `mat_role_tower_pulse` | Pulse Ward | Warm gold/orange pulse core. | Burst/splash identity; use rings more than flame language. |
| `mat_role_tower_prism` | Prism Ward | Pale cyan lens/facet material. | Long-range focus; brightest point should be lens/beam anchor. |
| `mat_role_creep_runner` | Runner | Sharp blue/mint body with sender accent. | Fast and small; value contrast matters more than detail. |
| `mat_role_creep_brute` | Brute | Heavy orange armor/body. | Wider/darker body with bright core. |
| `mat_role_creep_swarm` | Swarm | Small mint shard bodies. | Multiple small pieces should not sparkle into noise. |
| `mat_role_creep_shade` | Shade | Desaturated pale shimmer, semi-transparent only if still readable. | Do not rely on alpha alone; include echo/facet value contrast. |
| `mat_role_creep_siege` | Siege | Red-orange heavy pressure body. | Must signal extra leak danger without medieval siege styling. |

## Lighting Guardrails

- Use one dominant soft directional light and restrained ambient fill.
- Avoid strong shadows that hide creeps on the center route.
- Keep bloom subtle and limited to energy accents.
- Test material changes in `06-heavy-pressure.png` and `07-reduced-effects-heavy.png` before accepting them.
- Board material values should stay below tower/creep role materials.

## Screenshot Acceptance

A material pass is not accepted until:

- build/send menus remain readable;
- all five tower roles are distinguishable in active combat;
- all five creep roles are distinguishable in mixed pressure;
- reduced-effects heavy pressure still communicates path, creeps, towers, and leaks;
- no material or lighting choice makes the board louder than gameplay objects.
