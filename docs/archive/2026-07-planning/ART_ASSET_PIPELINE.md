# Art Asset Pipeline

## Purpose

This document defines the folder structure, naming rules, and material taxonomy for LTW art assets. It is the source of truth for where new Unity art should live before renderer integration begins.

## Unity Folder Structure

| Path | Purpose |
| --- | --- |
| `unity/LTW.UnityClient/Assets/Art/Board` | Board, lane, path, build-band, spawn, exit, rail, and trim source art and notes. |
| `unity/LTW.UnityClient/Assets/Art/Towers/Arrow` | Arrow/focused tower source art and notes. |
| `unity/LTW.UnityClient/Assets/Art/Towers/Control` | Control/area tower source art and notes. |
| `unity/LTW.UnityClient/Assets/Art/Towers/Relay` | Relay/utility tower source art and notes. |
| `unity/LTW.UnityClient/Assets/Art/Creeps/Runner` | Runner creep source art and notes. |
| `unity/LTW.UnityClient/Assets/Art/Creeps/Brute` | Brute creep source art and notes. |
| `unity/LTW.UnityClient/Assets/Art/Creeps/Swarm` | Swarm creep source art and notes. |
| `unity/LTW.UnityClient/Assets/Art/UI/Icons` | Build, send, tower, creep, and status icons. |
| `unity/LTW.UnityClient/Assets/Art/Materials` | Shared material palette and material notes. |
| `unity/LTW.UnityClient/Assets/Prefabs/Towers` | Runtime tower prefabs that satisfy the prefab contract. |
| `unity/LTW.UnityClient/Assets/Prefabs/Creeps` | Runtime creep prefabs that satisfy the prefab contract. |
| `unity/LTW.UnityClient/Assets/Prefabs/UI` | Runtime UI prefabs and icon-prefab wrappers. |

## Naming Rules

Use lowercase snake case with category, role, part, and version.

Examples:

- `tower_arrow_body_v01`
- `tower_control_ring_v01`
- `tower_relay_signal_v01`
- `creep_runner_body_v01`
- `creep_brute_armor_v01`
- `creep_swarm_dot_v01`
- `ui_icon_send_runner_v01`
- `ui_icon_tower_control_v01`
- `mat_team_p1_arcane`
- `mat_signal_leak`

Rules:

- Prefix by category: `tower_`, `creep_`, `ui_`, `mat_`, or `vfx_`.
- Put gameplay role before the part name.
- Use `v01`, `v02`, etc. when revisions need to coexist.
- Do not use ambiguous names such as `final`, `new`, `temp`, or `icon_1`.
- Do not encode balance values in art asset names.

## Material Palette

| Material Name | Intent |
| --- | --- |
| `mat_team_p1_arcane` | Human/player ownership accent. |
| `mat_team_p2_violet` | Bot/opponent ownership accent. |
| `mat_team_p3_gold` | Secondary opponent ownership accent. |
| `mat_board_deep_field` | Dark lane backplate and outer board field. |
| `mat_board_build_band` | Buildable side band surface. |
| `mat_board_route_core` | Main north-south creep route. |
| `mat_board_route_guide` | Thin route edge guide for grayscale/value readability. |
| `mat_board_spawn_gate` | Spawn/entry landmark. |
| `mat_board_leak_gate` | Exit/life-loss landmark. |
| `mat_board_owner_rail` | Lane ownership frame and rail accent. |
| `mat_role_tower_arrow` | Focused single-target tower role. |
| `mat_role_tower_control` | Area/control tower role. |
| `mat_role_tower_relay` | Utility/economy tower role. |
| `mat_role_creep_runner` | Fast baseline creep role. |
| `mat_role_creep_brute` | Durable creep role. |
| `mat_role_creep_swarm` | Multiple small creeps / grouped pressure. |
| `mat_signal_build` | Build/placement confirmation. |
| `mat_signal_sell` | Sell/refund confirmation. |
| `mat_signal_hit` | Attack and hit feedback. |
| `mat_signal_leak` | Life-loss, danger, and leak feedback. |
| `mat_signal_income` | Income and economy feedback. |
| `mat_ui_disabled` | Disabled action state. |
| `mat_ui_selected` | Selected action or selected object state. |

## Import Notes

- Prefer prefab child names from `docs/ART_PREFAB_CONTRACT.md`.
- Keep generated or source art grouped by role.
- Keep experimental files in role folders with clear `_wip` names until promoted.
- Do not require renderer code changes for folder-only art drops.
