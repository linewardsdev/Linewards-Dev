# AI Asset Intake Scorecard: tower.control / meshy_portal_shrine_blend_unpacked_0724232602

Date: 2026-07-24T23:28:00.003463+00:00
Status: needs_review

## Candidate

- Source: `/Users/admin/Downloads/Meshy_AI_Portal_Shrine_Gamerea_0724232602_texture.blend`
- Prepared FBX: `/Users/admin/LTW/unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/AIDrop/control_meshy_portal_shrine_blend_unpacked_0724232602_prepared.fbx`
- Preview: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/control/meshy_portal_shrine_blend_unpacked_0724232602/preview.png`
- Audit JSON: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/control/meshy_portal_shrine_blend_unpacked_0724232602/audit.json`

## Gate Checks

| Check | Result | Detail |
| --- | --- | --- |
| `has_meshes` | pass | 1 mesh object(s) |
| `triangle_budget` | pass | 14963 / 30000 triangles |
| `material_budget` | pass | 1 / 16 unique materials |
| `transparent_material_budget` | pass | 1 / 2 transparent materials |
| `has_textures_or_materials` | fail | 0 texture(s), 1 material(s) |
| `prepared_footprint` | pass | 1.450 / 1.450 footprint after prepare |

## Human Design Rule

This candidate must only be altered by automated cleanup, normalization, material assignment, or rejection.
Do not hand-model corrective shapes into this candidate. If it fails art quality, reject it and generate a new AI candidate batch.
