# AI Asset Intake Scorecard: creep.shade / meshy_shard_wraith_blend_0725021339

Date: 2026-07-25T02:24:41.738319+00:00
Status: pass

## Candidate

- Source: `/Users/admin/Downloads/Meshy_AI_Shard_Wraith_Gameread_0725021339_texture.blend`
- Prepared FBX: `/Users/admin/LTW/unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Creeps/Shade/AIDrop/shade_meshy_shard_wraith_blend_0725021339_prepared.fbx`
- Preview: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/shade/meshy_shard_wraith_blend_0725021339/preview.png`
- Audit JSON: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/shade/meshy_shard_wraith_blend_0725021339/audit.json`

## Gate Checks

| Check | Result | Detail |
| --- | --- | --- |
| `has_meshes` | pass | 1 mesh object(s) |
| `triangle_budget` | pass | 14789 / 30000 triangles |
| `material_budget` | pass | 1 / 16 unique materials |
| `transparent_material_budget` | pass | 1 / 2 transparent materials |
| `has_textures_or_materials` | pass | 3 texture(s), 1 material(s) |
| `prepared_footprint` | pass | 0.530 / 0.900 footprint after prepare |

## Human Design Rule

This candidate must only be altered by automated cleanup, normalization, material assignment, or rejection.
Do not hand-model corrective shapes into this candidate. If it fails art quality, reject it and generate a new AI candidate batch.
