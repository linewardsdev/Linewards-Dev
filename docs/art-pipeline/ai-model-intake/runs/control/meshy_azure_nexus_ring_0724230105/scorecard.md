# AI Asset Intake Scorecard: tower.control / meshy_azure_nexus_ring_0724230105

Date: 2026-07-24T23:03:14.935704+00:00
Status: needs_review

## Candidate

- Source: `/Users/admin/Downloads/Meshy_AI_Azure_Nexus_Ring_0724230105_generate.fbx`
- Prepared FBX: `/Users/admin/LTW/unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/AIDrop/control_meshy_azure_nexus_ring_0724230105_prepared.fbx`
- Preview: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/control/meshy_azure_nexus_ring_0724230105/preview.png`
- Audit JSON: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/control/meshy_azure_nexus_ring_0724230105/audit.json`

## Gate Checks

| Check | Result | Detail |
| --- | --- | --- |
| `has_meshes` | pass | 1 mesh object(s) |
| `triangle_budget` | fail | 712746 / 25000 triangles |
| `material_budget` | pass | 0 / 12 unique materials |
| `transparent_material_budget` | pass | 0 / 2 transparent materials |
| `has_textures_or_materials` | fail | 0 texture(s), 0 material(s) |
| `prepared_footprint` | pass | 1.450 / 1.450 footprint after prepare |

## Human Design Rule

This candidate must only be altered by automated cleanup, normalization, material assignment, or rejection.
Do not hand-model corrective shapes into this candidate. If it fails art quality, reject it and generate a new AI candidate batch.
