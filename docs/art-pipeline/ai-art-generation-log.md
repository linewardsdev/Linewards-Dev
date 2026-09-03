# AI Art Generation Log

This log records every AI-assisted asset that is considered for Line Wars production use. Keep rough experiments out of runtime folders until a role has passed contact-sheet review, source-plate cleanup, and screenshot QA.

## Status Values

- `staging`: generated or collected, not approved.
- `approved`: selected by review, ready for cleanup or Unity conversion.
- `implemented`: wired into a runtime prefab/icon and validated.
- `rejected`: kept only as evidence for why it should not ship.

## Asset Record Template

```markdown
## asset.role.name

- Date:
- Owner:
- Role:
- Intended silhouette:
- AI/tool used:
- Prompt/version:
- Source image/model path:
- Production asset path:
- Runtime prefab path:
- Human edits performed:
- Third-party inputs:
- Player-facing AI disclosure needed: yes/no/tbd
- Legal/IP notes:
- Screenshot review:
- Status: staging / approved / implemented / rejected
```

## Records

## Production Candidate Set V04

Date: 2026-07-15

The first Unity live review showed that the square `v03` AI source plates were too messy as direct runtime art. Arrow and Runner were regenerated with stricter production-sprite prompts: compact footprint, forward-facing role silhouette, flat chroma-key background, no baked trail/glow/UI, and no full-canvas diagonal weapon composition.

Generated keyed sources:

- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_candidate_v04_key.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_runner_candidate_v04_key.png`

Transparent alpha sources:

- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_candidate_v04_alpha.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_runner_candidate_v04_alpha.png`

Trimmed production candidates:

- `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v04_trimmed.png`
- `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v04_trimmed_grayscale.png`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v04_trimmed.png`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v04_trimmed_grayscale.png`

Review sheet:

- `docs/screenshot-reviews/ai-production-candidate-v04/candidate-v04-review-sheet.png`

Status: staging. These assets are not active runtime defaults. Proof prefabs may be generated for review, but promotion requires contact-sheet, grayscale, and live gameplay review.

## Clean Source Plate Set V03

Date: 2026-07-15

The selected candidates were regenerated on chroma-key backgrounds, processed to transparent alpha PNGs, and normalized to 1024x1024 source plates.

Clean source plates:

- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/builder_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_arrow_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_control_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_relay_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_pulse_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_prism_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/creep_runner_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/creep_brute_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/creep_swarm_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/creep_shade_source_plate_v03.png`
- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/creep_siege_source_plate_v03.png`

Grayscale review copies:

- `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/Grayscale/`

Status: staging, pending phone-scale review and Arrow/Runner Unity proof integration.

## V1 Final Batch Runtime Promotion

Date: 2026-07-15

Pulse, Prism, Shade, and Builder were promoted from selected clean source plates into runtime-ready V1 sprites.

Implemented assets:

- `tower.pulse`: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_pulse_candidate_v01_trimmed.png` and `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Pulse_AIPlate.prefab`
- `tower.prism`: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_prism_candidate_v01_trimmed.png` and `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Prism_AIPlate.prefab`
- `creep.shade`: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v01_trimmed.png` and `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Shade_AIPlate.prefab`
- Builder avatar: `unity/LTW.UnityClient/Assets/Resources/Art/Builder/Production/Sprites/builder_candidate_v01_trimmed.png`, loaded by `TouchPlacementController`

Human edits performed:

- normalized transparent source plates to runtime-safe square sprites;
- created grayscale copies for value review;
- preserved required prefab contract children while keeping primitive renderers disabled;
- cleared old Shade overlay tint paths;
- kept Builder procedural movement/placement logic intact while replacing the visible placeholder body with a painted source plate.

Status: implemented, pending hands-on Unity scale/readability review.

### tower.control.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Control tower
- Intended silhouette: containment dish / ring emitter / suspended core.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/prompt-library.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/tower_control_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### tower.relay.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Relay tower
- Intended silhouette: mast / beacon / signal capacitor.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/prompt-library.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/tower_relay_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### tower.pulse.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Pulse tower
- Intended silhouette: impact core / drum / shock emitter.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/prompt-library.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/tower_pulse_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### tower.prism.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Prism tower
- Intended silhouette: lens spire / crystal focus / beam anchor.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/prompt-library.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/tower_prism_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### creep.brute.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Brute creep
- Intended silhouette: armored health shell / broad plates / heavy core.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/prompt-library.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/creep_brute_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### creep.swarm.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Swarm creep
- Intended silhouette: multiple shardlings / cluster pressure / repeated small shapes.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/prompt-library.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/creep_swarm_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### creep.shade.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Shade creep
- Intended silhouette: echo/facet shimmer / dark glass shard / stealth cue.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/prompt-library.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/creep_shade_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### creep.siege.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Siege creep
- Intended silhouette: directional ram / barrel pressure / warning glow.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/prompt-library.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/creep_siege_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### builder.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Builder avatar
- Intended silhouette: friendly worker/tool user / construction wand / non-combat avatar.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/prompt-library.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/builder_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### tower.arrow.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Arrow tower
- Intended silhouette: focused rail / lens / bolt emitter.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/arrow-runner-contact-sheet-brief.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/tower_arrow_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### creep.runner.contact-sheet-v01

- Date: 2026-07-15
- Owner: Codex
- Role: Runner creep
- Intended silhouette: sharp fast dart / spine pressure construct.
- AI/tool used: built-in image generation tool
- Prompt/version: `docs/art-pipeline/arrow-runner-contact-sheet-brief.md`, 2026-07-15
- Source image/model path: `docs/art-pipeline/role-contact-sheets/creep_runner_contact_sheet_v01.png`
- Production asset path: pending selected source plate
- Runtime prefab path: pending
- Human edits performed: none
- Third-party inputs: none
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing
- Legal/IP notes: prompt used required protected-game avoidance block
- Screenshot review: pending phone-scale and grayscale review
- Status: staging

### tower.arrow.source-kit-proof-v01

- Date: 2026-07-15
- Owner: Agent 1
- Role: Arrow tower
- Intended silhouette: focused rail / lens / bolt emitter.
- AI/tool used: none for current proof-of-method; source-kit mesh promotion.
- Prompt/version: n/a
- Source image/model path: `unity/LTW.UnityClient/Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Musket/_PrefabsMusket/Musket1_2_1.prefab`
- Production asset path: `unity/LTW.UnityClient/Assets/Art/Towers/Arrow/`
- Runtime prefab path: `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Arrow.prefab`
- Human edits performed: runtime wrapper scale/composition adjustments; primitive body reduced to support/contract role.
- Third-party inputs: Stylized Weapon Kit, tracked in `docs/MVP_DEPENDENCIES.md`.
- Player-facing AI disclosure needed: no for this source-kit proof slice.
- Legal/IP notes: no protected-game prompt or reference used.
- Screenshot review: source-kit proof screenshots under `docs/screenshot-reviews/`.
- Status: implemented

### creep.runner.source-kit-proof-v01

- Date: 2026-07-15
- Owner: Agent 2
- Role: Runner creep
- Intended silhouette: sharp fast dart / spine pressure construct.
- AI/tool used: none for current proof-of-method; source-kit mesh promotion.
- Prompt/version: n/a
- Source image/model path: `unity/LTW.UnityClient/Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/Daggers/_PrefabsDaggers/Dagger4_1_3.prefab`
- Production asset path: `unity/LTW.UnityClient/Assets/Art/Creeps/Runner/`
- Runtime prefab path: `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Runner.prefab`
- Human edits performed: runtime wrapper scale/composition adjustments; procedural dart parts reduced to support cues.
- Third-party inputs: Stylized Weapon Kit, tracked in `docs/MVP_DEPENDENCIES.md`.
- Player-facing AI disclosure needed: no for this source-kit proof slice.
- Legal/IP notes: no protected-game prompt or reference used.
- Screenshot review: creep evidence under `docs/screenshot-reviews/stylized-weapon-kit-integration/`.
- Status: implemented

### tower.arrow.refinement-v05

- Date: 2026-07-15
- Owner: Codex
- Role: Arrow tower
- Intended silhouette: focused rail ward with explicit bow limbs, forward arrowhead, central lens, and readable armored base.
- AI/tool used: built-in image generation tool, edited through local chroma-key removal.
- Prompt/version: focused second-pass refinement prompt, 2026-07-15.
- Source image/model path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_candidate_v05_key.png`
- Production candidate path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_candidate_v05_alpha.png`
- Runtime prefab path: pending review; current runtime remains `Assets/Prefabs/Towers/Tower_Arrow_AIPlate.prefab` using Arrow v04.
- Human edits performed: local black-background removal with soft matte and despill; no geometry or paintover edits.
- Third-party inputs: none.
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing.
- Legal/IP notes: original ward-tech prompt with protected-game avoidance block.
- Screenshot review: pending reduced-size and grayscale capture.
- Status: staging

### creep.runner.refinement-v07

- Date: 2026-07-15
- Owner: Codex
- Role: Runner creep
- Intended silhouette: widened fast dart construct with segmented spine, short side fins, and a readable forward point.
- AI/tool used: built-in image generation tool, edited through local chroma-key removal.
- Prompt/version: focused second-pass refinement prompt, 2026-07-15.
- Source image/model path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_runner_candidate_v07_key.png`
- Production candidate path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_runner_candidate_v07_alpha.png`
- Runtime prefab path: pending review; current runtime remains `Assets/Prefabs/Creeps/Creep_Runner_AIPlate.prefab` using Runner v06c.
- Human edits performed: local black-background removal with soft matte and despill; no geometry or paintover edits.
- Third-party inputs: none.
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing.
- Legal/IP notes: original ward-tech prompt with protected-game avoidance block.
- Screenshot review: pending reduced-size and grayscale capture.
- Status: staging

### tower.arrow.refinement-v06

- Date: 2026-07-15
- Owner: Codex
- Role: Arrow tower
- Intended silhouette: chunky crossbow ward with broad gold bow limbs, bright cyan firing rail, large lens core, and armored base.
- AI/tool used: built-in image generation tool, edited through local chroma-key removal.
- Prompt/version: focused third-pass refinement prompt using Arrow v05 as identity/style reference, 2026-07-15.
- Source image/model path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_candidate_v06_key.png`
- Production candidate path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_candidate_v06_alpha.png`
- Runtime sprite path: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed.png`
- Runtime prefab path: `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Arrow_AIPlate.prefab`
- Human edits performed: magenta chroma-key removal with soft matte/despill; alpha crop/pad normalization to 1024x1024; prefab sprite scale increased to improve active-lane readability; primitive support mesh renderers disabled.
- Third-party inputs: none.
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing.
- Legal/IP notes: original ward-tech prompt with protected-game avoidance block.
- Screenshot review: pending hands-on active-lane confirmation.
- Status: active runtime proof

### tower.control.v1-batch1

- Date: 2026-07-15
- Owner: Codex
- Role: Control tower
- Intended silhouette: broad containment ring / suspended cyan core / restraint arcs.
- AI/tool used: built-in image generation tool, edited through local chroma-key removal.
- Prompt/version: V1 fast-track production prompt using Control source plate and Arrow v06 benchmark, 2026-07-15.
- Source image/model path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_control_candidate_v01_key.png`
- Production candidate path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_control_candidate_v01_alpha.png`
- Runtime sprite path: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_control_candidate_v01_trimmed.png`
- Runtime prefab path: `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Control_AIPlate.prefab`
- Human edits performed: magenta chroma-key removal with soft matte/despill; alpha crop/pad normalization to 1024x1024; primitive support mesh renderers disabled.
- Third-party inputs: none.
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing.
- Legal/IP notes: original ward-tech prompt with protected-game avoidance block.
- Screenshot review: pending hands-on active-lane confirmation.
- Status: active runtime proof

### creep.brute.v1-batch1-v02b

- Date: 2026-07-15
- Owner: Codex
- Role: Brute creep
- Intended silhouette: chunky armored health-shell / heavy cyan core / squat tank pressure.
- AI/tool used: built-in image generation tool, edited through local chroma-key removal.
- Prompt/version: V1 fast-track production prompt using Brute source plate and Runner v07 benchmark, 2026-07-15.
- Source image/model path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_brute_candidate_v02_key.png`
- Production candidate path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_brute_candidate_v02_alpha.png`
- Runtime sprite path: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_brute_candidate_v02b_trimmed.png`
- Runtime prefab path: `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Brute_AIPlate.prefab`
- Human edits performed: v02 image edit muted the blocky gold armor plates into darker brass/gunmetal trim; v02b deterministic color cleanup further reduced bright yellow plate mass; magenta chroma-key removal with soft matte/despill; alpha crop/pad normalization to 1024x1024; primitive support mesh renderers disabled.
- Third-party inputs: none.
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing.
- Legal/IP notes: original ward-tech prompt with protected-game avoidance block.
- Screenshot review: pending hands-on active-lane confirmation.
- Status: active runtime proof

### tower.relay.v1-batch2

- Date: 2026-07-15
- Owner: Codex
- Role: Relay tower
- Intended silhouette: tall signal relay ward / beacon mast / capacitor fins / cyan support core.
- AI/tool used: built-in image generation tool, edited through local chroma-key removal.
- Prompt/version: V1 fast-track production prompt using Relay source plate and Arrow/Control benchmark, 2026-07-15.
- Source image/model path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_relay_candidate_v01_key.png`
- Production candidate path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_relay_candidate_v01_alpha.png`
- Runtime sprite path: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_relay_candidate_v01_trimmed.png`
- Runtime prefab path: `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Relay_AIPlate.prefab`
- Human edits performed: magenta chroma-key removal with soft matte/despill; alpha crop/pad normalization to 930x930; grayscale copy; primitive support mesh renderers disabled.
- Third-party inputs: none.
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing.
- Legal/IP notes: original ward-tech prompt with protected-game avoidance block.
- Screenshot review: pending hands-on active-lane confirmation.
- Status: active runtime proof

### creep.siege.v1-batch2

- Date: 2026-07-15
- Owner: Codex
- Role: Siege creep
- Intended silhouette: directional ram/vehicle pressure creep / armored front / slow heavy lane threat.
- AI/tool used: built-in image generation tool, edited through local chroma-key removal.
- Prompt/version: V1 fast-track production prompt using Siege source plate and Runner/Brute benchmark, 2026-07-15.
- Source image/model path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_siege_candidate_v01_key.png`
- Production candidate path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_siege_candidate_v01_alpha.png`
- Runtime sprite path: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_siege_candidate_v01_trimmed.png`
- Runtime prefab path: `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Siege_AIPlate.prefab`
- Human edits performed: magenta chroma-key removal with soft matte/despill; alpha crop/pad normalization to 920x920; grayscale copy; primitive support mesh renderers disabled; runtime role/damage tint paths cleared so generated paint is not flattened.
- Third-party inputs: none.
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing.
- Legal/IP notes: original ward-tech prompt with protected-game avoidance block.
- Screenshot review: pending hands-on active-lane confirmation.
- Status: active runtime proof

### creep.swarm.v1-followup

- Date: 2026-07-15
- Owner: Codex
- Role: Swarm creep
- Intended silhouette: clustered shardling pressure unit / repeated small dark-glass bodies / cyan cores.
- AI/tool used: built-in image generation tool, edited through local chroma-key removal.
- Prompt/version: focused V1 follow-up production prompt after live review found the scaled placeholder too rough, 2026-07-15.
- Source image/model path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_swarm_candidate_v01_key.png`
- Production candidate path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_swarm_candidate_v01_alpha.png`
- Runtime sprite path: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_swarm_candidate_v01_trimmed.png`
- Runtime prefab path: `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Swarm_AIPlate.prefab`
- Human edits performed: magenta chroma-key removal with soft matte/despill; alpha crop/pad normalization to 920x920; grayscale copy; primitive support mesh renderers disabled; runtime role/damage tint paths cleared so generated paint is not flattened.
- Third-party inputs: none.
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing.
- Legal/IP notes: original ward-tech prompt with protected-game avoidance block.
- Screenshot review: pending hands-on active-lane confirmation.
- Status: active runtime proof

### creep.shade.v1-correction-v02

- Date: 2026-07-15
- Owner: Codex
- Role: Shade creep
- Intended silhouette: compact stealth creep / dark glass body / nearby echo facets.
- AI/tool used: built-in image generation tool, edited through local chroma-key removal.
- Prompt/version: focused correction prompt after v01 reviewed as too diagonal and VFX/projectile-like, 2026-07-15.
- Source image/model path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_shade_candidate_v02_key.png`
- Production candidate path: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_shade_candidate_v02_alpha.png`
- Runtime sprite path: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v02_trimmed.png`
- Runtime prefab path: `unity/LTW.UnityClient/Assets/Prefabs/Creeps/Creep_Shade_AIPlate.prefab`
- Human edits performed: green chroma-key removal with soft matte/despill; alpha crop/pad normalization to 920x920; grayscale copy; prefab sprite reference updated from v01 to v02.
- Third-party inputs: none.
- Player-facing AI disclosure needed: tbd if promoted to runtime/marketing.
- Legal/IP notes: original ward-tech prompt with protected-game avoidance block.
- Screenshot review: `docs/art-pipeline/shade-creep-v02-review.png`.
- Status: active runtime proof
