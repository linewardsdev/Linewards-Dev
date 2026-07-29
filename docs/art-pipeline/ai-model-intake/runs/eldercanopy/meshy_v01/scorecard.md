# AI Asset Intake Scorecard: tower.eldercanopy / meshy_v01

Date: 2026-07-29T03:57:39.924638+00:00
Status: needs_review

## Candidate

- Source: `/Users/admin/Downloads/Meshy_AI_Elder_Canopy_Gameread_0729035524_texture.glb`
- Prepared FBX: `/Users/admin/LTW/unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Eldercanopy/AIDrop/eldercanopy_meshy_v01_prepared.fbx`
- Preview: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/eldercanopy/meshy_v01/preview.png`
- Audit JSON: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/eldercanopy/meshy_v01/audit.json`

## Gate Checks

| Check | Result | Detail |
| --- | --- | --- |
| `has_meshes` | pass | 1 mesh object(s) |
| `triangle_budget` | pass | 15526 / 25000 triangles |
| `material_budget` | pass | 1 / 12 unique materials |
| `transparent_material_budget` | pass | 1 / 2 transparent materials |
| `has_textures_or_materials` | pass | 3 texture(s), 1 material(s) |
| `has_normal_map` | fail | no normal map: surface detail will read flat, re-export with one if the silhouette needs it |
| `prepared_footprint` | pass | 1.294 / 1.450 footprint after prepare |

## Human Design Rule

This candidate must only be altered by automated cleanup, normalization, material assignment, or rejection.
Do not hand-model corrective shapes into this candidate. If it fails art quality, reject it and generate a new AI candidate batch.
