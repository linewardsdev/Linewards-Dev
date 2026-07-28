# AI Asset Intake Scorecard: creep.serpent / meshy_serpent_v01

Date: 2026-07-28T14:29:24.281224+00:00
Status: needs_review

## Candidate

- Source: `/Users/admin/Downloads/Serpent Coil FBX.fbx`
- Prepared FBX: `/Users/admin/LTW-new-creeps/unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Creeps/Serpent/AIDrop/serpent_meshy_serpent_v01_prepared.fbx`
- Preview: `/Users/admin/LTW-new-creeps/docs/art-pipeline/ai-model-intake/runs/serpent/meshy_serpent_v01/preview.png`
- Audit JSON: `/Users/admin/LTW-new-creeps/docs/art-pipeline/ai-model-intake/runs/serpent/meshy_serpent_v01/audit.json`

## Gate Checks

| Check | Result | Detail |
| --- | --- | --- |
| `has_meshes` | pass | 1 mesh object(s) |
| `triangle_budget` | pass | 15135 / 25000 triangles |
| `material_budget` | pass | 1 / 12 unique materials |
| `transparent_material_budget` | pass | 1 / 2 transparent materials |
| `has_textures_or_materials` | pass | 9 texture(s), 1 material(s) |
| `has_normal_map` | fail | no normal map: surface detail will read flat, re-export with one if the silhouette needs it |
| `prepared_footprint` | pass | 0.900 / 0.900 footprint after prepare |

## Human Design Rule

This candidate must only be altered by automated cleanup, normalization, material assignment, or rejection.
Do not hand-model corrective shapes into this candidate. If it fails art quality, reject it and generate a new AI candidate batch.
