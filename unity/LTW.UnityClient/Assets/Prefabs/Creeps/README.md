# Creep Prefabs

Place final Unity creep prefabs here:

- `Creep_Runner.prefab`
- `Creep_Brute.prefab`
- `Creep_Swarm.prefab`

Prefab requirements:

- Root object centered on the grid cell.
- Body renderer or mesh.
- Ground shadow.
- Sender-color accent renderer slot.
- Hit-flash compatible material slot.
- Optional low-health marker or variant.
- No gameplay rule logic.

Prefab selection should be driven by a `CreepVisualLibrary` asset assigned to `UnityVerticalSliceRenderer`.
