# Tier 1 Lighting Pass Evidence

Captured 2026-07-25 with `Line Wars/Review/Capture Role Contact Sheet` under Unity
6000.5.3f1, after the Tier 1 lighting changes (three-point light rig, linear colour space,
gradient ambient).

| File | What it shows |
| --- | --- |
| `before-legacy-primitive-prefabs.png` | The contact sheet as it behaved before this pass |
| `after-3d-prefabs-lit.png` | The same capture path pointed at the prefabs the game loads |

The difference between these two images is **not** an art change. Both were rendered from
the same code path on the same day. The earlier capture instantiated
`Tower_Control.prefab`, `Tower_Relay.prefab`, `Tower_Pulse.prefab` and
`Tower_Prism.prefab` — the pre-3D primitive prefabs — while `TowerVisualLibrary` loads the
`_3D` Meshy prefabs at runtime.

Every screenshot review captured through this tool before 2026-07-25 therefore shows
placeholder geometry rather than the towers the game renders, which is worth keeping in
mind when reading the archived review passes.

`RenderRoleContactSheet` now instantiates the `_3D` tower prefabs and mirrors the runtime
three-point rig and gradient ambient from `LocalVerticalSliceLauncher`. Creeps were still on
the `_AIPlate` prefabs at the time of this capture; both `CreepVisualLibrary` and this
contact sheet were later repointed at the 3D creep meshes, and the `_AIPlate` prefabs were
retired entirely on 2026-07-26.

Both prefab lists must track the visual libraries. If they drift again, reviews stop
describing the shipping build.
