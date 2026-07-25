# AI Asset Intake Scorecard: tower.control / pipeline_smoke_v39

Date: 2026-07-24T13:33:52.632391+00:00
Status: needs_review

## Candidate

- Source: `/Users/admin/LTW/unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_prepared_v39_clear_read.fbx`
- Prepared FBX: `/Users/admin/LTW/unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/AIDrop/control_pipeline_smoke_v39_prepared.fbx`
- Preview: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/control/pipeline_smoke_v39/preview.png`
- Audit JSON: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/control/pipeline_smoke_v39/audit.json`

## Gate Checks

| Check | Result | Detail |
| --- | --- | --- |
| `has_meshes` | pass | 426 mesh object(s) |
| `triangle_budget` | fail | 141496 / 25000 triangles |
| `material_budget` | fail | 14 / 12 unique materials |
| `transparent_material_budget` | fail | 14 / 2 transparent materials |
| `has_textures_or_materials` | pass | 0 texture(s), 14 material(s) |
| `prepared_footprint` | pass | 1.450 / 1.450 footprint after prepare |

## Human Design Rule

This candidate must only be altered by automated cleanup, normalization, material assignment, or rejection.
Do not hand-model corrective shapes into this candidate. If it fails art quality, reject it and generate a new AI candidate batch.
