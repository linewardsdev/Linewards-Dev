# V1 Role Coverage Report

Date: 2026-07-15

This report records the current V1 art coverage for all tower roles, creep roles, and the Builder avatar.

## Runtime Coverage

| Category | Role | Runtime ID | Runtime Asset | Production Sprite | Status |
| --- | --- | --- | --- | --- | --- |
| Tower | Arrow | `tower.arrow` | `Assets/Prefabs/Towers/Tower_Arrow_AIPlate.prefab` | `Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed.png` | Implemented |
| Tower | Control | `tower.control` | `Assets/Prefabs/Towers/Tower_Control_AIPlate.prefab` | `Assets/Art/Towers/Production/Sprites/tower_control_candidate_v01_trimmed.png` | Implemented |
| Tower | Relay | `tower.relay` | `Assets/Prefabs/Towers/Tower_Relay_AIPlate.prefab` | `Assets/Art/Towers/Production/Sprites/tower_relay_candidate_v01_trimmed.png` | Implemented |
| Tower | Pulse | `tower.pulse` | `Assets/Prefabs/Towers/Tower_Pulse_AIPlate.prefab` | `Assets/Art/Towers/Production/Sprites/tower_pulse_candidate_v01_trimmed.png` | Implemented |
| Tower | Prism | `tower.prism` | `Assets/Prefabs/Towers/Tower_Prism_AIPlate.prefab` | `Assets/Art/Towers/Production/Sprites/tower_prism_candidate_v01_trimmed.png` | Implemented |
| Creep | Runner | `creep.runner` | `Assets/Prefabs/Creeps/Creep_Runner_AIPlate.prefab` | `Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v07_trimmed.png` | Implemented |
| Creep | Brute | `creep.brute` | `Assets/Prefabs/Creeps/Creep_Brute_AIPlate.prefab` | `Assets/Art/Creeps/Production/Sprites/creep_brute_candidate_v02b_trimmed.png` | Implemented |
| Creep | Swarm | `creep.swarm` | `Assets/Prefabs/Creeps/Creep_Swarm_AIPlate.prefab` | `Assets/Art/Creeps/Production/Sprites/creep_swarm_candidate_v01_trimmed.png` | Implemented |
| Creep | Shade | `creep.shade` | `Assets/Prefabs/Creeps/Creep_Shade_AIPlate.prefab` | `Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v01_trimmed.png` | Implemented |
| Creep | Siege | `creep.siege` | `Assets/Prefabs/Creeps/Creep_Siege_AIPlate.prefab` | `Assets/Art/Creeps/Production/Sprites/creep_siege_candidate_v01_trimmed.png` | Implemented |
| Builder | Builder | Procedural avatar | `TouchPlacementController` sprite layer | `Assets/Resources/Art/Builder/Production/Sprites/builder_candidate_v01_trimmed.png` | Implemented |

## Notes

- All tower and creep roles are wired through `TowerVisualLibrary.asset` or `CreepVisualLibrary.asset`.
- All tower and creep runtime prefabs preserve required contract children while placing the painted V1 source plate on `AIPlateVisual`.
- Builder is the only exception to the prefab/library pattern because it is a procedural placement avatar. It now loads a production sprite from `Resources` and keeps the existing `FootMarker` for placement feedback.
- Remaining work after this role coverage pass is gameplay review, scale tuning, icon matching, VFX anchor polish, and screenshot QA.
