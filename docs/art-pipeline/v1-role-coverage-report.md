# V1 Role Coverage Report

Date: 2026-07-31

Canonical record of which runtime asset and which target reference each tower, creep and
Builder role resolves to. `MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md`'s Target Reference Requirement makes the
Production Reference column below the promotion gate's target for identity work, so a wrong
or missing entry here is not a documentation problem — it is a gate that cannot be run.

Paths are verified by `tools/art_pipeline/validate_role_coverage.py`, which is why they are
written as full repo-relative paths rather than prose.

## Runtime Coverage

| Category | Role | Runtime ID | Runtime Asset | Production Reference | Status |
| --- | --- | --- | --- | --- | --- |
| Tower | Arrow | `tower.arrow` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Arrow_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed.png` | Implemented |
| Tower | Control | `tower.control` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Control_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_control_candidate_v01_trimmed.png` | Implemented |
| Tower | Relay | `tower.relay` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Relay_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_relay_candidate_v01_trimmed.png` | Implemented |
| Tower | Pulse | `tower.pulse` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Pulse_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_pulse_candidate_v01_trimmed.png` | Implemented |
| Tower | Prism | `tower.prism` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Prism_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_prism_candidate_v01_trimmed.png` | Implemented |
| Tower | Gatling | `tower.gatling` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Gatling_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Tesla | `tower.tesla` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Tesla_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Foundry | `tower.foundry` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Foundry_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Barricade | `tower.barricade` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Barricade_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Repair Drone | `tower.repair_drone` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_RepairDrone_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Elder Canopy | `tower.elder_canopy` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_ElderCanopy_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Sapling | `tower.sapling` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Sapling_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Bloomheart | `tower.bloomheart` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Bloomheart_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Thorn Snare | `tower.thorn_snare` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_ThornSnare_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Spore Cloud | `tower.spore_cloud` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_SporeCloud_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Tower | Twin Crescent | `tower.twin_crescent` | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_TwinCrescent_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Runner | `creep.runner` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Runner_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v07_trimmed.png` | Implemented |
| Creep | Brute | `creep.brute` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Brute_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_brute_candidate_v02b_trimmed.png` | Implemented |
| Creep | Swarm | `creep.swarm` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Swarm_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_swarm_candidate_v01_trimmed.png` | Implemented |
| Creep | Shade | `creep.shade` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Shade_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v02_trimmed.png` | Implemented |
| Creep | Siege | `creep.siege` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Siege_3D.prefab` | `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_siege_candidate_v01_trimmed.png` | Implemented |
| Creep | Wisp | `creep.wisp` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Wisp_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Revenant | `creep.revenant` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Revenant_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Obsidian Brute | `creep.obsidian_brute` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_ObsidianBrute_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Serpent | `creep.serpent` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Serpent_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Turret Walker | `creep.turret_walker` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_TurretWalker_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Zephyr | `creep.zephyr` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Zephyr_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Stalker | `creep.stalker` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Stalker_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Burrower | `creep.burrower` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Burrower_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Warden | `creep.warden` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Warden_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Creep | Colossus | `creep.colossus` | `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Colossus_3D.prefab` | **none** — see Target Reference Gap | Implemented, no target reference |
| Builder | Builder | Procedural avatar | `TouchPlacementController` sprite layer | `unity/LTW.UnityClient/Assets/Resources/Art/Builder/Production/Sprites/builder_candidate_v01_trimmed.png` | Implemented |

## Target Reference Gap

**Twenty of the thirty roles have no production reference, so the improvement cycle's
target-reference match score cannot be assigned for them.** This is a gate that reports
nothing for two thirds of the roster, not a gate that passes them.

The cause is a change of era rather than an oversight. The Production Reference column
holds 2D painted plates from the era when roles were painted before they were modelled.
Only the original five towers and five creeps ever went through that step; the twenty
added afterwards were generated directly as 3D candidates and never had a plate to be
matched against.

The consequence is worth stating plainly, because the numbers look better than the
situation: every role that CAN be scored against a target scores against a 2D plate that
its own 3D model superseded. So the ten with references are being measured against
something retired, and the twenty without are not being measured at all.

Two ways out, and this report does not pick one:

- Retire the target-reference score for identity work and replace it with the craft axis,
  which measures the built asset rather than its distance from a plate.
- Promote a current capture per role as its own reference, re-baselined when the asset
  changes, so the target reflects the 3D era.

Tracked as OPEN_ITEMS item 14. Sequence it before Wave 1, since the promotion gate is what
every other art item is eventually checked by.

## Notes

- All tower and creep roles are wired through `TowerVisualLibrary.asset` or
  `CreepVisualLibrary.asset`; the Runtime ID column is read from those two assets.
- Runtime assets are the `_3D` prefabs. The `_AIPlate` prefabs this report used to name were
  deleted on 2026-07-26 when the 2D-plate era was retired, and the report was not updated —
  so every Runtime Asset path in it pointed at a file that did not exist, for five days,
  while remaining the document the promotion gate resolves its targets through.
- Builder remains the exception to the prefab/library pattern because it is a procedural
  placement avatar. It loads a production sprite from `Resources` and keeps `FootMarker` for
  placement feedback.
- Shade was corrected after review from v01 to v02 because the first production sprite read
  as a diagonal projectile/VFX burst rather than a compact lane creep.
