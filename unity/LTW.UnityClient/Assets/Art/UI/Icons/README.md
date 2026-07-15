# UI Icons

Build, send, tower, creep, status, cooldown, and selected-state icon art belongs here.

Use names such as `ui_icon_send_runner_v01` and `ui_icon_tower_control_v01`.

## Current Runtime Icon Contract

The current build and send cards draw small procedural IMGUI glyphs directly in:

- `Assets/Scripts/UI/TouchPlacementController.cs`
- `Assets/Scripts/UI/SendDockController.cs`

These glyphs are intentionally simple placeholders for final imported UI art. Keep future sprite or vector replacements aligned to this shape language so players can transfer recognition from the prototype UI to polished cards.

## Tower Role Glyphs

| Role | Runtime text | Shape read | Final asset target |
| --- | --- | --- | --- |
| Arrow | `ARROW` | Bow limbs plus central bolt rail | `ui_icon_tower_arrow_v01` |
| Control | `CTRL` | Containment square/ring with center core | `ui_icon_tower_control_v01` |
| Relay | `RELAY` | Mast, beacon node, and side capacitors | `ui_icon_tower_relay_v01` |
| Pulse | `PULSE` | Compact core with stacked pulse bands | `ui_icon_tower_pulse_v01` |
| Prism | `PRISM` | Tall faceted spire and lens bands | `ui_icon_tower_prism_v01` |

## Creep/Send Role Glyphs

| Role | Runtime text | Shape read | Final asset target |
| --- | --- | --- | --- |
| Runner | `RUN` | Narrow dagger/dart body, cross fins, tail wake | `ui_icon_send_runner_v01` |
| Brute | `BRUTE` | Wide shield shell with side plates and bright core slit | `ui_icon_send_brute_v01` |
| Swarm | `SWARM` | Five rotated shard dots plus a low trail line | `ui_icon_send_swarm_v01` |
| Shade | `SHADE` | Split echo facets around a central shimmer line | `ui_icon_send_shade_v01` |
| Siege | `SIEGE` | Heavy ram base, top pressure block, forward barrel | `ui_icon_send_siege_v01` |

## Source-Kit Creep Source Mapping

Agent 2 source pass, 2026-07-15:

| Send icon | Runtime source motif | source-kit candidate |
| --- | --- | --- |
| `ui_icon_send_runner_v01` | Dagger spine plus speed fins from `Creep_Runner` | `Dagger4_1_3` |
| `ui_icon_send_brute_v01` | Shield shell plus armored side plates from `Creep_Brute` | `Shield2_1_2` |
| `ui_icon_send_swarm_v01` | Repeated shard cluster from `Creep_Swarm` | `Sword3_1_3`, `Dagger4_1_3` |
| `ui_icon_send_shade_v01` | Scythe echo and split shimmer facets from `Creep_Shade` | `Scythe1_3_2` |
| `ui_icon_send_siege_v01` | Directional barrel/ram body from `Creep_Siege` | `Musket1_2_1` |

The runtime IMGUI glyphs in `SendDockController` now follow these silhouettes. Final sprite exports should preserve these reads in enabled, disabled, and grayscale states.

## Replacement Rules

- Icons must read at build/send card size before color is considered.
- Use silhouette plus one accent color; avoid tiny internal detail.
- Keep tower icons tied to tower prefab motifs from the role contact sheet.
- Keep send icons tied to creep silhouettes from the role contact sheet.
- Do not use Warcraft/RTS command-card chrome, faction motifs, or copied icon compositions.
