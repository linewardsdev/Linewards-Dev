# Proper Art Replacement Pass Checklist

Date created: 2026-07-15

## Purpose

Move Line Wards from cautious prototype kitbash to a proper art pass where authored/source-kit meshes become the primary readable silhouettes in-game.

The previous pass intentionally kept generated low-poly primitives as the main bodies and used source-kit assets as small accents. That protected gameplay contracts, but it also preserved the placeholder look. This pass should promote selected high-quality meshes into the main visual read while keeping mobile clarity, Line Wards identity, and prefab/runtime contracts intact.

## North Star

- The game should still read instantly on a phone.
- Towers and creeps should look intentionally authored, not like primitive placeholders with decoration.
- Source-kit assets should be transformed into Line Wards ward-tech forms through scale, composition, material treatment, and runtime-safe wrapper prefabs.
- Vendor/source assets remain isolated under `Assets/ThirdParty/StylizedWeaponKit/...`.
- Runtime-facing prefabs, child names, materials, docs, and screenshots use Line Wards naming.

## Non-Negotiables

- Do not wire gameplay/runtime logic directly to third-party source paths.
- Do not remove required prefab contract children.
- Do not let art hide grid cells, path state, creep health, tower targeting, builder position, or HUD controls.
- Do not rely on color alone; every role must pass grayscale readability.
- Do not reintroduce vendor-facing names into runtime prefabs.
- Do not copy protected Warcraft names, silhouettes, UI chrome, factions, icons, sounds, or screenshots.
- Keep `docs/MVP_DEPENDENCIES.md` as the single dependency/provenance record for third-party source assets.

## Definition Of Done

- [x] All five tower roles use primary authored/source-kit silhouettes or have a documented defer reason.
- [x] All five creep roles use primary authored/source-kit silhouettes or have a documented defer reason.
- [x] Builder avatar/tooling uses an intentional authored silhouette.
- [ ] Build/send icons match the final in-game silhouettes.
- [ ] VFX anchors attach to authored visual landmarks.
- [ ] All runtime-facing names are Line Wards names.
- [ ] Full screenshot review passes in normal, grayscale, and reduced-effects modes.
- [ ] Unity prefab/library validation passes.
- [ ] `main` is synced to cloud.

## Workstream A: Art Direction Lock

Owner: Agent 1

- [x] Define the final role language for each tower:
  - Arrow: focused rail / lens / bolt emitter.
  - Control: dish / ring / containment field.
  - Relay: mast / beacon / signal capacitor.
  - Pulse: impact core / drum / shock emitter.
  - Prism: lens spire / crystal focus / beam anchor.
- [x] Define the final role language for each creep:
  - Runner: sharp fast dart.
  - Brute: armored health shell.
  - Swarm: multiple shardlings.
  - Shade: echo/facet shimmer.
  - Siege: directional ram/barrel pressure.
- [x] Confirm builder read:
  - always visible;
  - worker/tool user, not a combat creep;
  - last-selected tower state is clear.
- [x] Record one-line visual intent for each role before building.

Progress:

- [x] 2026-07-15: Locked the initial silhouette language for all tower and creep roles.
- [x] 2026-07-15: Selected Arrow + Runner as the proof-of-method pair for source-kit mesh promotion.
- [x] 2026-07-15: Scaffolded the AI-assisted 2.5D token pipeline in `docs/art-pipeline/` and `Assets/Art/*/Production/`; generated art remains gated behind contact-sheet review, provenance logging, and screenshot QA.
- [x] 2026-07-15: Generated AI contact sheets for all tower roles, all creep roles, and Builder under `docs/art-pipeline/role-contact-sheets/`; candidate selection and source-plate cleanup remain open.
- [x] 2026-07-15: Recorded user-selected candidates for all generated contact sheets in `docs/art-pipeline/selected-candidates-v01.md`; source-plate cleanup remains open.
- [x] 2026-07-15: Created first-pass 512x512 source-plate crops for selected candidates under `Assets/Art/AIStaging/SourcePlates/`; transparent cleanup and Unity runtime integration remain open.
- [x] 2026-07-15: Produced normalized transparent 1024x1024 `v03` source plates and grayscale review copies under `Assets/Art/AIStaging/SourcePlates/`; Arrow/Runner Unity proof integration remains open.
- [x] 2026-07-15: Generated Arrow and Runner AI source-plate proof prefabs and validated prefab/library contracts in Unity batchmode.
- [x] 2026-07-15: Captured AI source-plate proof contact sheets under `docs/screenshot-reviews/ai-source-plate-proof/captures/`; proof assets are token-safe after scale correction, but final promotion still needs source-specific crop/pose cleanup and phone-scale gameplay QA.
- [x] 2026-07-15: After live review, restored Arrow and Runner runtime visual libraries to the cleaner authored/source-kit prefabs; AI source-plate prefabs remain staged for review only.
- [x] 2026-07-15: Corrected the AI pipeline with Arrow/Runner `v04` trimmed production candidates; source plates must now pass tiny-scale review before Unity proof-prefab work.
- [x] 2026-07-15: Updated `AiSourcePlateProofGenerator` so generating proof prefabs no longer changes active runtime visual libraries.
- [x] 2026-07-15: Validated corrected v04 proof flow in Unity batchmode; proof generation did not mutate runtime libraries, AI proof validation passed, and tower/creep baseline validators passed.
- [x] 2026-07-15: Tuned v04 proof prefab sprite scale and captured normal/grayscale proof sheets; Arrow and Runner now read cleanly in contact-sheet review.
- [x] 2026-07-15: Captured v04 active-lane proof review using cloned in-memory visual libraries; Arrow and Runner read at phone-scale in normal, grayscale, and reduced-effects captures.
- [x] 2026-07-15: Captured polished v04 active-lane zoom review under `docs/screenshot-reviews/ai-production-candidate-v04/active-lane-proof-captures-polished-v2/zoom-review-sheet.png`; Arrow is ready for explicit promotion decision, Runner should receive a v05 color/value polish before final promotion.
- [x] 2026-07-15: Generated Runner v05/v06 polish candidates. v06 is the better long/narrow fast-dart source plate and is now wired into the AI proof prefab only.
- [x] 2026-07-15: Added v06b/v06c color cleanup, review-only overlay suppression, stale prefab-pool clearing, and Runner-only active-lane proof capture so the Runner sprite can be judged without combat/projectile clutter.
- [x] 2026-07-15: User approved runtime deployment. `tower.arrow` now uses `Tower_Arrow_AIPlate.prefab`; `creep.runner` now uses `Creep_Runner_AIPlate.prefab` with the v06c Runner sprite.
- [x] 2026-07-15: Arrow v06 and Runner v07 now define the V1 2.5D token benchmark. Both use sprite-only painted plates with primitive contract children render-disabled and live-tuned scale.
- [x] 2026-07-15: Added `docs/art-pipeline/v1-art-fast-track.md` to capture the repeatable path for the remaining towers, creeps, and Builder.
- [x] 2026-07-15: Completed the remaining V1 role batch for Pulse, Prism, Shade, and Builder. Combat roles use AIPlate prefabs; Builder is now a Resources-loaded sprite layer over the procedural placement avatar.
- [x] 2026-07-15: Replaced Shade v01 with Shade v02 after review found v01 too diagonal/projectile-like; v02 uses a compact vertical dark-crystal body with nearby echo facets.

Acceptance:

- The team can explain each role by silhouette before opening Unity.
- The plan avoids generic medieval/faction reads and keeps ward-tech fantasy.

## Workstream B: Source Mesh Promotion Plan

Owner: Agent 1 towers/builder, Agent 2 creeps

- [x] Choose the primary mesh/mesh group for each tower.
  - [x] Arrow: `Musket1_2_1.prefab` promoted as the long-axis authored rail/lens silhouette; old primitive body remains as a smaller contract/footprint support.
  - [x] Control: V1 AI source plate `tower_control_candidate_v01_trimmed.png` promoted as the dish/ring containment silhouette.
  - [x] Relay: V1 AI source plate `tower_relay_candidate_v01_trimmed.png` promoted as the mast/beacon support silhouette.
  - [x] Pulse: V1 AI source plate `tower_pulse_candidate_v01_trimmed.png` promoted as the impact drum/shock emitter silhouette.
  - [x] Prism: V1 AI source plate `tower_prism_candidate_v01_trimmed.png` promoted as the crystal lens-spire silhouette.
- [x] Choose the primary mesh/mesh group for each creep.
  - [x] Runner: `Dagger4_1_3.prefab` promoted as the sharp dart/spine silhouette; old primitive body remains as a smaller contract/health anchor support.
  - [x] Brute: V1 AI source plate `creep_brute_candidate_v02b_trimmed.png` promoted as the heavy armored shell silhouette.
  - [x] Swarm: V1 AI source plate `creep_swarm_candidate_v01_trimmed.png` promoted as the clustered shardling silhouette.
  - [x] Shade: V1 AI source plate `creep_shade_candidate_v02_trimmed.png` promoted as the echo/facet stealth silhouette after v01 was rejected as too VFX/projectile-like.
  - [x] Siege: V1 AI source plate `creep_siege_candidate_v01_trimmed.png` promoted as the ram/barrel pressure silhouette.
- [x] Choose the primary mesh/tool prop for the builder.
- [x] For each role, document:
  - source asset names;
  - intended Line Wards role;
  - required rotation/scale;
  - material treatment needed;
  - whether old primitive body remains as invisible/utility support only.
- [x] Reject assets that only look good in isolation but fail top-down phone readability.

Acceptance:

- Each promoted source asset has a reason to exist as the main silhouette.
- No role depends on tiny accent detail for its read.

## Workstream C: Tower Primary Art Replacement

Owner: Agent 1

- [x] Replace Arrow’s primitive-dominant body with primary authored rail/lens silhouette.
  - [x] 2026-07-15 slice: enlarged `ArrowRailVisual`, reduced primitive base/body dominance, kept `Muzzle`, `Lens`, `BowLeft`, and `BowRight` anchors.
  - [x] 2026-07-15 aggressive pass: made `ArrowRailVisual` the dominant tower silhouette, shrank primitive `Body`/`Base` into utility support, and increased Arrow runtime visual scale.
- [x] Replace Control’s primitive-dominant body with primary authored dish/ring silhouette.
  - [x] 2026-07-15 V1 batch: Generated `tower_control_candidate_v01_trimmed.png`, created `Tower_Control_AIPlate.prefab`, and promoted `tower.control` to the sprite-only AI plate runtime visual.
- [x] Replace Relay’s primitive-dominant body with primary authored mast/beacon silhouette.
  - [x] 2026-07-15 V1 batch: Generated `tower_relay_candidate_v01_trimmed.png`, created `Tower_Relay_AIPlate.prefab`, and wired `tower.relay` to the sprite-only AI plate runtime proof.
- [x] Replace Pulse’s primitive-dominant body with primary authored impact-core silhouette.
  - [x] 2026-07-15 V1 batch: Generated `tower_pulse_candidate_v01_trimmed.png`, created `Tower_Pulse_AIPlate.prefab`, and wired `tower.pulse` to the sprite-only AI plate runtime proof.
- [x] Replace Prism’s primitive-dominant body with primary authored lens-spire silhouette.
  - [x] 2026-07-15 V1 batch: Generated `tower_prism_candidate_v01_trimmed.png`, created `Tower_Prism_AIPlate.prefab`, and wired `tower.prism` to the sprite-only AI plate runtime proof.
- [x] Keep required children on every tower:
  - `Body`
  - `RoleMarker`
  - `OwnerTrim`
  - `RangeHalo`
- [ ] Keep or add role anchors:
  - Arrow: `Muzzle`, `Lens`
  - Control: `ControlRing`, `ControlCore`, `PulseEmitter`
  - Relay: `RelayMast`, `RelayCore`, `RelaySignal`
  - Pulse: `PulseCore`, `PulseRingA`, `PulseEmitter`
  - Prism: `PrismSpire`, `PrismLens`, `BeamAnchor`
- [x] Ensure tower footprint still matches occupied grid cell.
- [x] Ensure tower ownership and selection states still read.

Acceptance:

- Towers no longer look like primitive placeholders.
- Tower roles remain distinct without labels.
- Towers do not obscure path or placement state.

## Workstream D: Creep Primary Art Replacement

Owner: Agent 2

- [x] Replace Runner’s primitive-dominant body with primary authored dart/spine silhouette.
  - [x] 2026-07-15 slice: enlarged `RunnerSpineVisual`, reduced procedural dart dominance, kept `Body`, `GroundShadow`, `RoleMarker`, and damage/speed cue children.
  - [x] 2026-07-15 aggressive pass: made `RunnerSpineVisual` the dominant creep silhouette, shrank procedural dart parts into support cues, and increased Runner runtime visual scale so it survives gameplay camera distance.
- [x] Replace Brute’s primitive-dominant body with primary authored armor/shell silhouette.
  - [x] 2026-07-15 V1 batch: Generated Brute v01, replaced it with v02, then applied `creep_brute_candidate_v02b_trimmed.png` to further mute blocky yellow armor reads. `creep.brute` now uses the sprite-only AI plate runtime visual.
- [x] Replace Swarm’s primitive-dominant body with authored multi-shard silhouette.
  - [x] 2026-07-15 V1 batch: Generated `creep_swarm_candidate_v01_trimmed.png`, created `Creep_Swarm_AIPlate.prefab`, and wired `creep.swarm` to the sprite-only AI plate runtime proof with primitive role/damage overlays disabled.
- [x] Replace Shade’s primitive-dominant body with authored echo/facet silhouette.
  - [x] 2026-07-15 V1 batch: Generated `creep_shade_candidate_v01_trimmed.png`, created `Creep_Shade_AIPlate.prefab`, and wired `creep.shade` to the sprite-only AI plate runtime proof with primitive role/damage overlays disabled.
  - [x] 2026-07-15 correction: Replaced the active Shade sprite with `creep_shade_candidate_v02_trimmed.png` to remove the diagonal burst read and restore a compact creep silhouette.
- [x] Replace Siege’s primitive-dominant body with authored ram/barrel silhouette.
  - [x] 2026-07-15 V1 batch: Generated `creep_siege_candidate_v01_trimmed.png`, created `Creep_Siege_AIPlate.prefab`, and wired `creep.siege` to the sprite-only AI plate runtime proof with primitive role/damage overlays disabled.
- [x] Keep required children on every creep:
  - `Body`
  - `GroundShadow`
  - `RoleMarker`
- [x] Preserve damage/health visual alignment.
- [ ] Test role readability under 10+ visible creeps.
- [ ] Test Swarm under heavy pressure.

Acceptance:

- Creeps look authored but remain smaller/subordinate to towers.
- Heavy pressure does not become visual soup.
- Shade is readable without relying only on transparency.

## Workstream E: Material And Palette Pass

Owner: Shared

- [ ] Create Line Wards material instances for promoted art.
- [ ] Reduce generic medieval/weapon reads through palette and value treatment.
- [ ] Use ward-tech palette consistently:
  - blue/violet energy;
  - mint positive/valid cues;
  - gold economy/ownership accents;
  - restrained red/orange danger cues.
- [ ] Normalize roughness/metallic/emissive values across towers and creeps.
- [ ] Make grayscale value separation pass role-by-role.
- [ ] Avoid over-bright source textures that fight HUD, path, or effects.

Acceptance:

- Source-kit parts feel like Line Wards assets.
- Color supports role read but shape still carries the role.

## Workstream F: Builder And Placement Art

Owner: Agent 1

- [x] Promote builder avatar from simple placeholder to authored worker/tool silhouette.
  - [x] 2026-07-15 V1 batch: Added `builder_candidate_v01_trimmed.png` as a Resources-loaded sprite layer on the procedural Builder avatar.
- [x] Add a visible tool/prop that matches the build fantasy.
  - [x] 2026-07-15 V1 batch: Builder source plate includes a readable construction hook/tool and keeps the existing `FootMarker` for placement feedback.
- [x] Ensure builder remains always visible.
- [x] Ensure tower selection defaults to builder position.
- [ ] Ensure confirm placement is visually clear and touch-safe.
- [x] Ensure builder does not look like a creep or tower.

Acceptance:

- Player can instantly identify “this is my builder.”
- Placement mode feels intentional, not accidental.

## Workstream G: Icons And UI Silhouette Match

Owner: Shared

- [x] Establish the UI/game-board pipeline.
  - [x] 2026-07-16: Added `docs/art-pipeline/ui-board-art-pipeline.md`, `docs/art-pipeline/ui-board-contact-sheet-brief.md`, and `docs/art-pipeline/ui-board-pipeline-checklist.md` so UI chrome, command cards, board materials, endpoint gates, and screenshot QA have a repeatable path.
- [ ] Rebuild tower build icons from final tower silhouettes.
  - [x] 2026-07-16: Added V01 runtime Resources icons derived from current V1 tower production sprites and wired build cards to load them with procedural fallback.
- [ ] Rebuild send icons from final creep silhouettes.
  - [x] 2026-07-16: Added V01 runtime Resources icons derived from current V1 creep production sprites, including Shade v02, and wired send cards to load them with procedural fallback.
- [ ] Confirm icons read in:
  - enabled state;
  - disabled/too-expensive state;
  - selected state;
  - grayscale.
- [ ] Keep icons aligned with in-game shape language.
- [ ] Avoid third-line microcopy.

Acceptance:

- Players can connect menu choices to in-game objects by shape.

## Workstream H: VFX Anchor Realignment

Owner: Shared

- [ ] Attach Arrow shots to authored `Muzzle`/rail.
- [ ] Attach Control pulses to authored ring/core.
- [ ] Attach Relay signals/economy pings to authored mast/beacon.
- [ ] Attach Pulse shockwaves to authored impact core.
- [ ] Attach Prism beam to authored lens/spire.
- [ ] Attach Shade reveal and Siege warning cues to authored creep landmarks.
- [ ] Verify reduced-effects mode still communicates all critical events.

Acceptance:

- Effects reinforce the authored art instead of floating from old placeholder centers.

## Workstream I: Screenshot QA Gate

Owner: Agent 1 final integration, Agent 2 supplies creep-specific evidence

- [ ] Capture tower lineup normal.
- [ ] Capture tower lineup grayscale.
- [ ] Capture creep role lineup normal.
- [ ] Capture creep role lineup grayscale.
- [x] Capture Arrow/Runner AI production candidate review sheet.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v04/candidate-v04-review-sheet.png`.
- [x] Capture Arrow/Runner v04 Unity proof contact sheet normal.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v04/unity-proof-captures/01-role-contact-sheet.png`.
- [x] Capture Arrow/Runner v04 Unity proof contact sheet grayscale.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v04/unity-proof-captures/grayscale/01-role-contact-sheet.png`.
- [x] Capture Arrow/Runner v04 scale-tuned Unity proof contact sheet normal.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v04/unity-proof-captures-scale-tuned/01-role-contact-sheet.png`.
- [x] Capture Arrow/Runner v04 scale-tuned Unity proof contact sheet grayscale.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v04/unity-proof-captures-scale-tuned/grayscale/01-role-contact-sheet.png`.
- [x] Capture Arrow/Runner v04 active-lane proof normal.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v04/active-lane-proof-captures/01-ai-v04-active-lane.png`.
- [x] Capture Arrow/Runner v04 active-lane proof grayscale.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v04/active-lane-proof-captures/grayscale/01-ai-v04-active-lane.png`.
- [x] Capture Arrow/Runner v04 active-lane reduced-effects proof.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v04/active-lane-proof-captures/02-ai-v04-active-lane-reduced-effects.png`.
- [x] Capture Arrow/Runner v04 polished active-lane zoom proof.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v04/active-lane-proof-captures-polished-v2/zoom-review-sheet.png`.
  - [x] 2026-07-15: Result: Arrow passes as a rail/crossbow ward candidate; Runner is readable but needs a brighter, cleaner v05 dart pass before final runtime art lock.
- [x] Capture Runner v05/v06 tiny-scale and Unity proof evidence.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v05/runner-v04-v05-v06-review-sheet.png`.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v05/runner-v06-v06b-v06c-color-review-sheet.png`.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v05/01-role-contact-sheet.png`.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v05/grayscale/01-role-contact-sheet.png`.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-production-candidate-v05/active-lane-v06c-zoom-sheet.png`.
  - [x] 2026-07-15: Result: v06c improves Runner source art and proof capture reliability, but runtime promotion still needs final scale/overlay tuning.
- [x] Capture AI source-plate proof contact sheet normal.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-source-plate-proof/captures/01-role-contact-sheet.png`.
- [x] Capture AI source-plate proof contact sheet grayscale.
  - [x] 2026-07-15: `docs/screenshot-reviews/ai-source-plate-proof/captures/grayscale/01-role-contact-sheet.png`.
- [ ] Capture Runner x10 pressure.
- [ ] Capture Swarm heavy pressure.
- [x] Capture Shade readability.
  - [x] 2026-07-15: `docs/art-pipeline/shade-creep-review-current.png` showed v01 read as diagonal/projectile-like.
  - [x] 2026-07-15: `docs/art-pipeline/shade-creep-v02-review.png` confirms v02 is more compact and creep-like for the next live Unity review.
- [ ] Capture Siege leak/warning.
- [ ] Capture active combat with all tower attack roles.
- [ ] Capture reduced-effects combat.
- [ ] Capture builder select/confirm/build-complete states.
- [ ] Capture build menu and send menu with final icons.
  - [x] 2026-07-16: Added static review sheet `docs/art-pipeline/ui-board-pass-v01-icon-review.png` before live Unity capture.
  - [x] 2026-07-16: Captured V01 UI/board Unity screenshot review under `docs/screenshot-reviews/ui-board-art-pass-v01/`, including normal and grayscale build/send states.
- [ ] Capture UI/board V02 authored direction after contact-sheet selection.
  - [x] 2026-07-16: Generated contact sheets and preliminary review notes under `docs/art-pipeline/ui-board/`.
  - [x] 2026-07-16: Recorded selected UI/board V02 options in `docs/art-pipeline/ui-board/selected-candidates-v02.md`.
  - [x] 2026-07-16: Implemented the first V02 runtime command-card slice from selected option 4 and captured evidence under `docs/screenshot-reviews/ui-board-art-pass-v02-command-cards/`.
  - [ ] HUD/stat drawer chrome.
  - [x] command card frames.
  - [ ] board material kit.
  - [ ] spawn/leak gates.
  - [ ] map/lane toggle and status controls.
- [ ] Write review under `docs/screenshot-reviews/proper-art-replacement-pass/`.
- [ ] Record every medium/high issue before merge.

Acceptance:

- Verdict is `Pass` or `Pass with low-severity polish follow-ups`.
- No mobile-critical readability regressions remain.

## Workstream J: Repo Hygiene And Sync

Owner: Agent 1

- [ ] Keep source-kit assets isolated under `Assets/ThirdParty/StylizedWeaponKit/...`.
- [ ] Keep Line Wards runtime wrappers under `Assets/Prefabs/...`.
- [ ] Keep Line Wards material instances under `Assets/Art/...`.
- [ ] Do not commit Unity package/project churn unless intentional.
- [x] Run Unity prefab/library validation.
  - [x] 2026-07-15: `ValidateTowerPlaceholderPrefabs` passed in Unity batchmode.
  - [x] 2026-07-15: `ValidateCreepVisualLibrary` passed in Unity batchmode.
- [ ] Run `dotnet test LTW.sln` only if simulation-facing code changes.
  - [x] 2026-07-15: `dotnet test LTW.sln --no-restore` passed, 69/69.
- [ ] Update this checklist after each completed slice.
  - [x] 2026-07-15: Tracked Arrow + Runner proof-of-method slice.
- [x] Push to cloud after accepted implementation slices.
  - [x] 2026-07-15: Pushed Arrow + Runner proof-of-method slice to `origin/main`.

## Suggested Execution Order

1. Run hands-on Unity review for all V1 promoted roles: Arrow, Control, Relay, Pulse, Prism, Runner, Brute, Swarm, Shade, Siege, and Builder.
2. Tune per-role runtime scale where overlap or tiny-scale readability fails.
3. Rebuild tower build icons and send icons from the final V1 silhouettes.
4. Realign VFX anchors for Pulse, Prism, Shade, Siege, and any remaining tower effects.
5. Run full screenshot QA gate in normal, grayscale, reduced-effects, and pressure scenarios.
6. Merge/sync once the visual read is stable.
