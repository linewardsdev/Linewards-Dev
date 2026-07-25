# AI Asset Intake Scorecard: tower.relay / meshy_beacon_array_blend_0725015112

Date: 2026-07-25T01:52:03.590381+00:00
Status: pass

## Candidate

- Source: `/Users/admin/Downloads/Meshy_AI_Beacon_Array_Gameread_0725015112_texture.blend`
- Prepared FBX: `/Users/admin/LTW/unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Relay/AIDrop/relay_meshy_beacon_array_blend_0725015112_prepared.fbx`
- Preview: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/relay/meshy_beacon_array_blend_0725015112/preview.png`
- Audit JSON: `/Users/admin/LTW/docs/art-pipeline/ai-model-intake/runs/relay/meshy_beacon_array_blend_0725015112/audit.json`

## Gate Checks

| Check | Result | Detail |
| --- | --- | --- |
| `has_meshes` | pass | 1 mesh object(s) |
| `triangle_budget` | pass | 15005 / 30000 triangles |
| `material_budget` | pass | 1 / 16 unique materials |
| `transparent_material_budget` | pass | 1 / 2 transparent materials |
| `has_textures_or_materials` | pass | 3 texture(s), 1 material(s) |
| `prepared_footprint` | pass | 0.988 / 1.450 footprint after prepare |

## Human Design Rule

This candidate must only be altered by automated cleanup, normalization, material assignment, or rejection.
Do not hand-model corrective shapes into this candidate. If it fails art quality, reject it and generate a new AI candidate batch.
