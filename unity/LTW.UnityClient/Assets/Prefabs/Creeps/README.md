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
- Death cue style configured in `CreepVisualLibrary`.
- Optional low-health marker or variant.
- No gameplay rule logic.

Prefab selection should be driven by a `CreepVisualLibrary` asset assigned to `UnityVerticalSliceRenderer`.

## Placeholder Generation

Use `Line Wars > Art > Generate Placeholder Creep Prefabs` in the Unity editor to generate first-pass placeholder prefabs and materials.

The generator creates:

- `Assets/Prefabs/Creeps/Creep_Runner.prefab`
- `Assets/Prefabs/Creeps/Creep_Brute.prefab`
- `Assets/Prefabs/Creeps/Creep_Swarm.prefab`
- procedural low-poly mesh assets under `Assets/Art/Creeps/GeneratedMeshes/`
- generated materials under `Assets/Art/Creeps/GeneratedMaterials/`

It also updates `Assets/Resources/CreepVisualLibrary.asset` so the local vertical slice can load the generated prefabs through the existing presentation renderer.

After generation, run `Line Wars > Art > Validate Creep Visual Library` to check for missing prefabs and invalid body/accent/damage renderer paths.

The generator also writes `Assets/Art/Creeps/GeneratedPlaceholderReport.md` with the prefab/material paths and follow-up review checklist.
