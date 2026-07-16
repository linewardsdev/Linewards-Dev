# UI And Board Component Inventory V01

Date: 2026-07-16

This inventory defines the runtime surfaces that the UI/board art pipeline needs to cover before V1 can be called complete.

## HUD And Status

| Component | Current role | Needed states | V1 art need |
| --- | --- | --- | --- |
| Your Line label | Confirms active player lane | default | compact label or drawer tab |
| Stat strip | Lives, gold, income, time, kills, leaks, tick, pressure | open, closed, alert, low-life | stat drawer frame and stat cell style |
| Match state panel | Ready/running/paused/ended | ready, running, paused, ended | panel chrome and state accent |
| Start/Pause/Restart controls | Match control | enabled, disabled, pressed | command button style |
| Map/Lane toggle | Camera mode switch | map, lane, pressed, disabled | persistent onscreen toggle style |

## Build Dock

| Component | Current role | Needed states | V1 art need |
| --- | --- | --- | --- |
| Build dock trigger | Opens tower palette | closed, open, pressed | compact icon-first trigger |
| Tower card | Selects Arrow, Control, Relay, Pulse, Prism | enabled, selected, disabled, too-expensive | command card frame and simplified tower icon |
| Cost strip | Shows gold price | enough gold, not enough gold | readable price micro-panel |
| Placement preview | Shows where selected tower can go | valid, invalid, occupied | placement decal and invalid state |

## Send Dock

| Component | Current role | Needed states | V1 art need |
| --- | --- | --- | --- |
| Send dock trigger | Opens creep palette | closed, open, pressed | compact icon-first trigger |
| Send card | Selects Runner, Brute, Swarm, Shade, Siege | enabled, selected, disabled, too-expensive | command card frame and simplified creep icon |
| Income impact strip | Shows income change | positive, neutral, disabled | small gold/pressure accent strip |
| Send confirmation feedback | Shows successful send | normal, sent, cooldown | pulse or flash state |

## Lane Board

| Component | Current role | Needed states | V1 art need |
| --- | --- | --- | --- |
| Deep field/backplate | Holds the lane grid visually | default | low-noise tile material |
| Buildable side bands | Tower placement zones | default, valid placement, invalid placement | placeable material and feedback overlay |
| Center route | Creep path | default, heavy pressure, leak warning | route material with restrained direction cues |
| Route edge guides | Separates path from build zones | default | edge line or inlay material |
| Grid cells | Placement readability | default, hover/preview, occupied | subtle cell separators |
| Lane rails/gutters | Frames each lane and ownership color | active, inactive, opponent | rail material and color accent rules |

## Endpoint Gates

| Component | Current role | Needed states | V1 art need |
| --- | --- | --- | --- |
| Spawn gate | Creep entry at top | idle, spawn, heavy spawn | top gate plate/portal art |
| Leak gate | Life loss at bottom | idle, leak warning, leak hit | bottom gate/drain art |
| Direction markings | Communicates top-to-bottom flow | default | board-integrated arrows or chevrons |

## Required Contact Sheets

Use this inventory to drive the V02 contact sheets:

- HUD chrome and stat drawer sheet covers the HUD/status rows.
- Command card frame sheet covers both build and send cards.
- Icon simplification sheet covers tower and creep command identity.
- Board material kit sheet covers deep field, build bands, route, edge guides, cells, and rails.
- Spawn/leak gate sheet covers endpoint gates and direction markings.
- Map/lane toggle sheet covers persistent camera controls and status buttons.

## Integration Notes

- The existing IMGUI controllers can accept incremental art changes if each slice keeps a fallback path.
- Runtime icons already load from `Assets/Resources/Art/UI/Icons/`; future command-card frame art may need a similar runtime path or a prefab conversion.
- Board art is currently procedural in `UnityVerticalSliceRenderer`; authored board materials should first match the current procedural regions before replacing them.
- Do not change gameplay rules during art integration unless the UI layout itself exposes a bug.

