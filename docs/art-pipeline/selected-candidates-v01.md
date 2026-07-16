# Selected Candidates V01

Date: 2026-07-15

Numbering convention:

- 1-4: top row, left to right.
- 5-8: middle row, left to right.
- 9-12: bottom row, left to right.

## Selected Roles

| Role | Selected Candidate | Contact Sheet |
| --- | ---: | --- |
| Builder | 11 | `docs/art-pipeline/role-contact-sheets/builder_contact_sheet_v01.png` |
| Arrow tower | 3 | `docs/art-pipeline/role-contact-sheets/tower_arrow_contact_sheet_v01.png` |
| Control tower | 6 | `docs/art-pipeline/role-contact-sheets/tower_control_contact_sheet_v01.png` |
| Relay tower | 12 | `docs/art-pipeline/role-contact-sheets/tower_relay_contact_sheet_v01.png` |
| Pulse tower | 8 | `docs/art-pipeline/role-contact-sheets/tower_pulse_contact_sheet_v01.png` |
| Prism tower | 3 | `docs/art-pipeline/role-contact-sheets/tower_prism_contact_sheet_v01.png` |
| Runner creep | 11 | `docs/art-pipeline/role-contact-sheets/creep_runner_contact_sheet_v01.png` |
| Brute creep | 4 | `docs/art-pipeline/role-contact-sheets/creep_brute_contact_sheet_v01.png` |
| Swarm creep | 3 | `docs/art-pipeline/role-contact-sheets/creep_swarm_contact_sheet_v01.png` |
| Shade creep | 2 | `docs/art-pipeline/role-contact-sheets/creep_shade_contact_sheet_v01.png` |
| Siege creep | 11 | `docs/art-pipeline/role-contact-sheets/creep_siege_contact_sheet_v01.png` |

## Next Step

Use these selections for source-plate cleanup. Each selected role still needs:

- isolated source plate;
- normalized scale and facing;
- phone-scale check;
- grayscale check;
- production asset record update;
- Unity token/prefab implementation only after the source plate beats the current proof art.

## Source Plate Drafts

First-pass 512x512 source-plate crops were created under:

`unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/`

Clean normalized source plates were then produced as transparent 1024x1024 `v03` PNGs in the same folder, with grayscale review copies under `SourcePlates/Grayscale/`.

Use the `v03` files as historical staging evidence only. The first live review showed that square source plates were too messy as direct runtime art.

## Production Candidate V04 Correction

Arrow and Runner were regenerated as compact, role-specific source images and processed into trimmed production candidates:

- `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v04_trimmed.png`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v04_trimmed.png`

Review evidence:

- `docs/screenshot-reviews/ai-production-candidate-v04/candidate-v04-review-sheet.png`
- `docs/screenshot-reviews/ai-production-candidate-v04/active-lane-proof-captures-polished-v2/zoom-review-sheet.png`

Use the `v04` trimmed sprites, not the square `v03` source plates, for Arrow/Runner proof-prefab review.

Current promotion note:

- Arrow v04 is the stronger production candidate and can move to an explicit runtime-promotion decision.
- Runner v04 proved the pipeline but read too dark/magenta-heavy in active-lane review.
- Runner v05 improved color but became too chunky.
- Runner v06 restored the long/narrow fast-dart silhouette.
- Runner v06c is the current runtime Runner candidate after magenta-fringe cleanup. It passes contact-sheet review and has Runner-only active-lane proof evidence. It is now live for testing, but still needs final runtime scale/overlay tuning.
- Arrow v04 and Runner v06c were promoted to runtime on 2026-07-15 after explicit user approval.

## Refinement Candidates V05/V07

The next focused refinement pass is now wired into the live AI proof prefabs for hands-on review:

- Arrow v05: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_candidate_v05_alpha.png`
- Runner v07: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_runner_candidate_v07_alpha.png`

Normalized 1024x1024 production-style review copies are also available:

- `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v05_trimmed.png`
- `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v05_trimmed_grayscale.png`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v07_trimmed.png`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v07_trimmed_grayscale.png`

Arrow v05 strengthens the bow-limb, rail, and forward arrowhead read while reducing the amount of tiny surface ornament. Runner v07 widens the body and adds a more legible side-fin silhouette so the creep does not collapse into a projectile at phone scale. Both are now active proof sprites; v04/v06c remain available as rollback/reference candidates.

## Arrow Runtime Refinement V06

After hands-on lane review, Arrow v05 was visible but still too narrow and low-contrast at gameplay scale. Arrow v06 is now the active Arrow proof sprite:

- Source key: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_candidate_v06_key.png`
- Alpha staging plate: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_candidate_v06_alpha.png`
- Runtime sprite: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed.png`
- Grayscale review copy: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed_grayscale.png`

V06 deliberately exaggerates the horizontal bow limbs, central cyan firing rail, and lens core so the tower reads as a crossbow/arrow ward in the active lane. `Tower_Arrow_AIPlate.prefab` now points at v06, uses a slightly larger `AIPlateVisual` scale, and keeps primitive support children render-disabled so they cannot cover the painted plate.

## V1 Batch 1: Control And Brute

Control and Brute are the first roles executed from `docs/art-pipeline/v1-art-fast-track.md`.

- Control source key: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_control_candidate_v01_key.png`
- Control runtime sprite: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_control_candidate_v01_trimmed.png`
- Control runtime prefab: `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Control_AIPlate.prefab`
- Brute source key: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_brute_candidate_v02_key.png`
- Brute runtime sprite: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_brute_candidate_v02b_trimmed.png`
- Brute runtime prefab: `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Brute_AIPlate.prefab`

Both prefabs follow the Arrow/Runner V1 benchmark: painted source plate on `AIPlateVisual`, required contract children preserved but render-disabled, and live visual library promotion for hands-on scale review. Brute v02b supersedes v01/v02 because the bright gold armor blocks still read as strange yellow rectangles at gameplay scale.

## V1 Batch 2: Relay And Siege

Relay and Siege are the second pair executed from `docs/art-pipeline/v1-art-fast-track.md`.

- Relay source key: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_relay_candidate_v01_key.png`
- Relay alpha staging plate: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_relay_candidate_v01_alpha.png`
- Relay runtime sprite: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_relay_candidate_v01_trimmed.png`
- Relay grayscale copy: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_relay_candidate_v01_trimmed_grayscale.png`
- Relay runtime prefab: `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Relay_AIPlate.prefab`
- Siege source key: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_siege_candidate_v01_key.png`
- Siege alpha staging plate: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_siege_candidate_v01_alpha.png`
- Siege runtime sprite: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_siege_candidate_v01_trimmed.png`
- Siege grayscale copy: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_siege_candidate_v01_trimmed_grayscale.png`
- Siege runtime prefab: `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Siege_AIPlate.prefab`

Both prefabs follow the same V1 AI plate contract: painted source plate on `AIPlateVisual`, required contract children preserved but render-disabled, and runtime library wiring for hands-on active-lane review. Relay starts at the Control-style tower plate scale. Siege uses a larger sprite plate plus a moderate visual-library scale bump because its opaque silhouette is much narrower than Brute while still needing to read as the heavy pressure unit.

## V1 Swarm Follow-Up

The old Swarm placeholder remained rough after a scale bump, so Swarm received a focused V1 repaint pass.

- Swarm source key: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_swarm_candidate_v01_key.png`
- Swarm alpha staging plate: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_swarm_candidate_v01_alpha.png`
- Swarm runtime sprite: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_swarm_candidate_v01_trimmed.png`
- Swarm grayscale copy: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_swarm_candidate_v01_trimmed_grayscale.png`
- Swarm runtime prefab: `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Swarm_AIPlate.prefab`

Swarm v01 uses repeated dark-glass shardlings with cyan cores so the role reads as a cluster instead of one tiny primitive unit. The runtime profile clears sender/damage tint paths so the painted shardlings are not flattened by old placeholder overlays.

## V1 Final Batch: Pulse, Prism, Shade, And Builder

Pulse, Prism, Shade, and Builder were promoted from the selected `v03` source plates into runtime-ready V1 assets.

- Pulse source plate: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_pulse_source_plate_v03.png`
- Pulse runtime sprite: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_pulse_candidate_v01_trimmed.png`
- Pulse grayscale copy: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_pulse_candidate_v01_trimmed_grayscale.png`
- Pulse runtime prefab: `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Pulse_AIPlate.prefab`
- Prism source plate: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_prism_source_plate_v03.png`
- Prism runtime sprite: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_prism_candidate_v01_trimmed.png`
- Prism grayscale copy: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_prism_candidate_v01_trimmed_grayscale.png`
- Prism runtime prefab: `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Prism_AIPlate.prefab`
- Shade source plate: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/creep_shade_source_plate_v03.png`
- Shade runtime sprite: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v01_trimmed.png`
- Shade grayscale copy: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v01_trimmed_grayscale.png`
- Shade runtime prefab: `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Shade_AIPlate.prefab`
- Builder source plate: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/builder_source_plate_v03.png`
- Builder runtime sprite: `unity/LTW.UnityClient/Assets/Resources/Art/Builder/Production/Sprites/builder_candidate_v01_trimmed.png`
- Builder grayscale copy: `unity/LTW.UnityClient/Assets/Resources/Art/Builder/Production/Sprites/builder_candidate_v01_trimmed_grayscale.png`

Pulse and Prism now use the same sprite-only AIPlate prefab pattern as Arrow, Control, and Relay. Shade now uses the creep AIPlate pattern with old sender/damage overlay paths cleared so the dark-glass silhouette is not flattened. Builder remains procedural but now loads a painted worker/tool sprite from `Resources` and keeps the placement `FootMarker` visible.

## Shade Runtime Correction V02

Live review found the first Shade production sprite too diagonal and VFX-like; it read more like a projectile burst than a lane creep. Shade v02 is now the active proof sprite:

- Source key: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_shade_candidate_v02_key.png`
- Alpha staging plate: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_shade_candidate_v02_alpha.png`
- Runtime sprite: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v02_trimmed.png`
- Grayscale review copy: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v02_trimmed_grayscale.png`

V02 keeps the stealth/dark-glass role language but changes the read to a compact vertical crystalline body with nearby echo facets. V01 remains in the repo as rollback/reference evidence.
