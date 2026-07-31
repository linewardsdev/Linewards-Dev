# Stylized Weapon Kit Integration Checklist

Date created: 2026-07-15
Imported source kit: `FREE - Stylized Weapons`; vendor attribution is tracked in `docs/MVP_DEPENDENCIES.md`
Source folder: `unity/LTW.UnityClient/Assets/ThirdParty/StylizedWeaponKit/Art/Weapons/Stylized/`
Dependency record: `docs/MVP_DEPENDENCIES.md`
Status reconciled: 2026-07-16

> **Superseded.** This describes the AIPlate sprite era for the original 5 towers and 5 creeps.
> The roster has since grown to **15 towers in three build lines** and **15 creeps in three send
> categories**, on the newer Meshy 3D mesh pipeline — see `docs/TOWER_AND_CREEP_ROSTER.md` for the
> current roster. Read below as history, not current runtime state.

## Runtime Status After AIPlate Promotion

This checklist records a completed exploratory integration track. The active runtime baseline is now the V1 AIPlate sprite pipeline for all five towers and all five creeps, with an authored Builder sprite layer. Weapon-kit wrappers and generated placeholder assets remain isolated fallback/history; agents must not treat unfinished wrapper boxes below as current runtime requirements.

Legend:

- **Superseded:** historical requirement replaced by the active AIPlate implementation.
- **Open QA:** still relevant against the AIPlate baseline.
- **Capture-gated:** cannot be certified until deterministic four-profile portrait capture uses the actual runtime UI, declared seeds, injectable safe areas, and a manifest.

The active follow-up work from this track is final icon production, specialized AIPlate VFX landmarks/attachment alignment, mobile pressure evidence, Builder placement-state QA, and fallback validation. Source-kit roughness/metallic/material normalization is not an active requirement; sprite palette, brightness, and grayscale value normalization owns that concern.

## Purpose

Use the imported stylized weapon kit as a rapid improvement track for Line Wards art, without letting third-party assets blur the original ward-tech identity.

This pack does not add gameplay code. It provides source art assets: prefabs, FBX meshes, materials, and textures. The original demo scene was removed from the project after import cleanup.

## Non-Negotiables

- Keep source-kit art under `Assets/ThirdParty/StylizedWeaponKit/...`.
- Create Line Wards wrapper/polished prefabs under `Assets/Prefabs/...` and role source folders under `Assets/Art/...`; do not make runtime code depend directly on vendor folder structure.
- Preserve prefab contracts from `docs/ART_PREFAB_CONTRACT.md`.
- Preserve original Line Wards ward-tech fantasy from `docs/ART_THEME_AND_ROLE_GUIDE.md`.
- Avoid Warcraft/RTS faction imitation, medieval faction identity, or protected visual-language drift.
- Do not change simulation rules, economy, targeting, pathing, or content balance for this art pass.
- Keep procedural fallbacks until each replacement passes screenshot review.
- Record any production use of third-party assets in `docs/MVP_DEPENDENCIES.md`.
- Verify final store-release license/distribution assumptions before shipping third-party art.

## Agent Split

| Agent | Ownership | Primary Outputs |
| --- | --- | --- |
| Agent 1 | Towers, builder/avatar/tooling, UI icon candidates, integration docs, final capture gate | Tower wrapper prefabs, builder prop pass, icon source candidates, capture/review docs |
| Agent 2 | Creeps, creep role props, combat/send/leak VFX source candidates, pressure readability | Creep wrapper prefabs, role silhouette upgrades, VFX attachment candidates, pressure captures |

Both agents must keep source-kit assets isolated and produce Line Wards-specific prefabs/assets that can be swapped, reverted, or replaced later.

## Asset Inventory

Imported usable categories:

- Axes: `AxeBasic1_2`, `AxeBasic2_1`, `AxeEvolving3_3_2`
- Daggers: `Dagger1_3_5`, `Dagger4_1_3`
- Hammer: `Hammer1_1_3`
- Musket: `Musket1_2_1`
- Polearm: `Polearm2_2_2`
- Scythe: `Scythe1_3_2`
- Shields: `Shield2_1_2`, `Shield3_1_1`
- Staves: `Staff2_2_6`, `Staff4_1_1`, `Staff5_1_1`
- Swords: `Sword1_1_3`, `Sword2_3_3`, `Sword3_1_3`, `Sword5_3_2`

## Candidate Mapping

### Tower Candidates

| Line Wards Role | Candidate Pack Assets | Intended Read | Agent |
| --- | --- | --- | --- |
| Arrow Ward | `Musket1_2_1`, `Sword1_1_3`, `Polearm2_2_2` | Focused weapon rail, bolt spine, narrow aim line | Agent 1 |
| Control Ward | `Shield2_1_2`, `Shield3_1_1`, staff caps | Wide ring/dish/containment field | Agent 1 |
| Relay Ward | `Staff2_2_6`, `Staff4_1_1`, `Staff5_1_1` | Mast, signal beacon, capacitor/support read | Agent 1 |
| Pulse Ward | `Hammer1_1_3`, shields, circular staff pieces | Compact burst core, weighty shock emitter | Agent 1 |
| Prism Ward | `Staff2_2_6`, `Scythe1_3_2`, emissive sword/staff materials | Tall lens-spire, crystal/focus silhouette | Agent 1 |

### Creep Candidates

| Line Wards Role | Candidate Pack Assets | Intended Read | Agent |
| --- | --- | --- | --- |
| Runner | Daggers, small swords, polearm tips | Low dart/needle shard with speed fins | Agent 2 |
| Brute | Shields, hammer head, axe bodies | Wide armored pressure shell | Agent 2 |
| Swarm | Dagger shards, sword fragments | Repeated small shardlings, not one blob | Agent 2 |
| Shade | Scythe, emissive staff/sword textures | Broken echo/facet silhouette, not transparency-only | Agent 2 |
| Siege | Hammer, musket barrel, shield plates | Directional ram/cannon pressure body | Agent 2 |
| Builder Avatar | Dagger/hammer/axe tool props plus existing builder body | Tool-bearing lane worker, always visible and readable | Agent 1 |

### UI / Icon Candidates

| UI Need | Candidate Pack Assets | Intended Read | Agent |
| --- | --- | --- | --- |
| Tower build icons | Same candidate per tower role | Keep icon silhouette matched to prefab silhouette | Agent 1 |
| Send icons | Runner/dagger, Brute/shield, Swarm/shards, Shade/scythe, Siege/hammer-musket | Fast card recognition in send dock | Agent 2 |
| Builder/tower descriptor affordances | Hammer/tool/dagger motif | “Build/confirm/last tower” support language | Agent 1 |

### VFX / Feedback Candidates

| Event | Candidate Source | Intended Use | Agent |
| --- | --- | --- | --- |
| Arrow shot | Musket/sword rail orientation | Beam/bolt anchor placement | Agent 1 |
| Control field | Shield rings/circular forms | Containment pulse source | Agent 1 |
| Relay income/signal | Staff heads/caps | Signal ping anchor | Agent 1 |
| Pulse splash | Hammer/shield mass | Radial impact source | Agent 2 |
| Prism beam | Staff/scythe/sword emissive material | Beam origin/lens language | Agent 1 |
| Siege warning | Hammer/musket parts | Directional warning, impact cue | Agent 2 |
| Shade reveal/resist | Scythe/emissive textures | Shimmer/facet reveal cue | Agent 2 |

## Technical Implementation Rules

- Prefer nested prefab/wrapper composition:
  - vendor prefab as hidden source/reference;
  - Line Wards prefab root with required child names;
  - child transforms scaled/rotated for top-down mobile gameplay.
- Required tower children:
  - `Body`
  - `RoleMarker`
  - `OwnerTrim`
  - `RangeHalo`
- Required creep children:
  - `Body`
  - `GroundShadow`
  - `RoleMarker`
- Add optional role children only where useful, matching `docs/ART_PREFAB_CONTRACT.md`.
- Use one or two vendor meshes per wrapper at first. Do not over-kitbash tiny details that vanish on phone.
- Normalize scale against current runtime prefabs before wiring into visual libraries.
- Keep colliders, scripts, and gameplay state off visual-only nested parts unless required by existing runtime presentation code.
- Use material instances only when a Line Wards palette/value adjustment is needed.
- Make every role pass grayscale and phone-size tests before calling it complete.

## Workstream A: Asset Triage And Baseline Contact Sheet

Owner: Agent 1

- [x] Remove unused imported demo scene content after contact-sheet capture.
- [x] Create a source inventory/contact sheet of all 18 imported prefabs.
- [x] Identify top 2 candidates per tower role.
- [x] Identify top 2 candidates per builder/tooling role.
- [x] Record candidate screenshots or notes under `docs/screenshot-reviews/stylized-weapon-kit-triage/`.
- [x] Mark any assets that are too medieval, too noisy, or too hard to read from above as rejected for runtime use.
- [x] Confirm source/license note remains in `docs/MVP_DEPENDENCIES.md`.

Status note:

- Agent 1 added `Line Wards/Review/Capture Stylized Weapon Kit Contact Sheet` and captured normal/grayscale evidence in `docs/screenshot-reviews/stylized-weapon-kit-triage/`. Triage review recommends starting wrappers with Arrow, Relay, and Control.

Acceptance:

- Agents can pick assets without reopening the demo scene.
- Rejected candidates are documented so the team does not churn on them later.

## Workstream B: Tower Wrapper Prefabs

Owner: Agent 1

- [x] Create `Tower_Arrow_WardPrototype.prefab` or replace `Tower_Arrow.prefab` through a safe wrapper branch.
- [x] Create `Tower_Control_WardPrototype.prefab` or wrapper replacement.
- [x] Create `Tower_Relay_WardPrototype.prefab` or wrapper replacement.
- [x] **Superseded:** do not create `Tower_Pulse_WardPrototype.prefab`; `Tower_Pulse_AIPlate.prefab` is active.
- [x] **Superseded:** do not create `Tower_Prism_WardPrototype.prefab`; `Tower_Prism_AIPlate.prefab` is active.
- [x] Required contract children exist on every active AIPlate tower prefab: `Body`, `RoleMarker`, `OwnerTrim`, and `RangeHalo`.
- [ ] Add or align specialized role landmarks on the active AIPlate prefabs where needed:
  - [x] Arrow retains generic `Muzzle` and `Lens` landmarks.
  - [ ] Control: `ControlRing`, `ControlCore`, `PulseEmitter`
  - [ ] Relay: `RelayMast`, `RelayCore`, `RelaySignal`
  - [ ] Pulse: `PulseCore`, `PulseRingA`, `PulseEmitter`
  - [ ] Prism: `PrismSpire`, `PrismLens`, `BeamAnchor`
- [x] Update `TowerVisualLibrary` if wrapper names or references change.
- [ ] Verify procedural fallback still works if a ward wrapper is missing.
- [x] Capture tower lineup normal and grayscale.

Status note:

- Agent 1 generated kit-backed wrapper replacements for Arrow, Relay, and Control through `Line Wards/Art/Generate Ward Prototype Tower Wrappers`.
- Validation passed with `Line Wards/Art/Validate Tower Placeholder Prefabs`.
- Normal, grayscale, and reduced-effects evidence lives under `docs/screenshot-reviews/ward-tower-wrapper-prototypes/`.
- The current wrappers keep the original prototype gameplay silhouette as the primary read and use kit meshes as embedded detail accents. This is intentional for safety; a later polish pass can promote more vendor mesh detail once phone-size readability is stable.

Acceptance:

- All five towers remain visually distinct without labels.
- No tower hides the path, selected cell, or bottom controls.
- Runtime does not depend directly on vendor folder names.

## Workstream C: Builder Avatar And Tool Language

Owner: Agent 1

- [x] **Superseded:** weapon-kit prop selection was replaced by the authored `builder_candidate_v01_trimmed.png` design.
- [x] **Superseded:** do not add a weapon-kit prop wrapper; the active Builder plate includes its construction tool.
- [x] The active authored Builder remains layered over the procedural placement avatar and distinct from creeps/towers.
- [ ] Ensure selected-tower/last-tower state is readable near the builder without creating accidental placement confusion.
- [ ] Verify builder does not jump lanes or reset position when selecting a tower.
- [ ] Capture default builder, selected tower, confirm placement, and build-complete states.

Acceptance:

- Builder reads as an intentional lane worker/tool user.
- Builder remains visually subordinate to placement state and tower/creep pressure.

## Workstream D: Creep Wrapper Prefabs

Owner: Agent 2

- [x] Create `Creep_Runner_WardPrototype.prefab` or wrapper replacement.
- [x] Create `Creep_Brute_WardPrototype.prefab` or wrapper replacement.
- [x] Create `Creep_Swarm_WardPrototype.prefab` or wrapper replacement.
- [x] Create `Creep_Shade_WardPrototype.prefab` or wrapper replacement.
- [x] Create `Creep_Siege_WardPrototype.prefab` or wrapper replacement.
- [x] Ensure required child names exist on every creep wrapper.
- [x] Ensure role children exist where useful:
  - Runner: `Nose`, `Tail`, `FinLeft`, `FinRight`
  - Brute: `Armor`, `PlateLeft`, `PlateRight`, `Core`
  - Swarm: `SwarmDotA`, `SwarmDotB`, `SwarmDotC`, `Trail`
  - Shade: `Shimmer`, `EchoA`, `EchoB`
  - Siege: `Base`, `Barrel`, `Spike`
- [x] Update `CreepVisualLibrary` if wrapper names or references change.
- [x] Verify damaged health bars/wound pips still sit correctly on each role.
- [x] Capture role roster normal and grayscale.

Status note:

- Agent 2 updated `CreepVisualPrefabGenerator` so generated Line Wards wrappers keep the procedural silhouettes as fallbacks and nest source-kit parts for Runner, Brute, Swarm, Shade, and Siege when the imported pack is present. Generated prefabs now include required `Body`, `GroundShadow`, and `RoleMarker` children plus role-specific child names from `docs/ART_PREFAB_CONTRACT.md`.
- Unity regeneration updated `Assets/Prefabs/Creeps/Creep_*.prefab`, `Assets/Resources/CreepVisualLibrary.asset`, and `Assets/Art/Creeps/GeneratedPlaceholderReport.md`. File-side checks confirmed the required child names and source-kit nested parts are present.
- Agent 2 captured role-lineup, pressure, damaged-transfer, reduced-effects, and grayscale evidence under `docs/screenshot-reviews/stylized-weapon-kit-integration/`. Review verdict is pass with one low-severity staged leak-text follow-up.

Acceptance:

- Runner, Brute, Swarm, Shade, and Siege remain distinct at phone size.
- Swarm reads as multiple small units without becoming noisy.
- Shade reads without relying only on alpha.
- Siege reads as directional pressure distinct from Brute.

## Workstream E: UI Icon And Card Source Pass

Owner: Shared

Agent 1:

- [ ] Produce 5 tower icon candidates from tower silhouettes.
- [ ] Keep tower icons aligned with build-menu role language.
- [ ] Place source/export notes under `Assets/Art/UI/Icons/`.

Agent 2:

- [x] Produce 5 creep/send icon candidates from creep silhouettes.
- [x] Keep send icons aligned with send dock pressure roles.
- [x] Confirm icons still read in disabled/affordability states.

Agent 2 note:

- Agent 2 updated `SendDockController` procedural glyphs and `Assets/Art/UI/Icons/README.md` so Runner, Brute, Swarm, Shade, and Siege send cards map back to the kit-derived creep wrapper silhouettes. Disabled/affordability states keep the same silhouettes with reduced value.

Shared acceptance:

- Icons are readable in current build/send card sizes.
- No third-line microcopy returns to cards.
- Icons are role-readable in grayscale.

## Workstream F: Combat And Feedback Attachment Pass

Owner: Shared

Agent 1:

- [ ] Verify Arrow shot alignment against the active AIPlate `Muzzle` landmark.
- [ ] Add and attach Relay signal/economy feedback to an authored AIPlate landmark.
- [ ] Add and attach Prism beam feedback to an authored AIPlate lens/spire landmark.

Agent 2:

- [x] Attach Pulse splash source to kit-derived Pulse wrapper.
- [x] Attach Shade reveal/resist source to kit-derived Shade wrapper.
- [x] Attach Siege warning/leak source to kit-derived Siege wrapper.

Agent 2 note:

- Agent 2 strengthened runtime feedback in `UnityVerticalSliceRenderer`: Pulse attacks get a brighter core/radial splash read, Shade hits/leaks get reveal/slip cues tied to the `ShadeEchoVisual` silhouette, and Siege hits/leaks get directional warning/breach cues tied to the `SiegeBarrelVisual` silhouette. Reduced-effects text cues are included for high-signal hit/leak events.

Shared acceptance:

- VFX anchors improve role read instead of adding clutter.
- Heavy-pressure screenshots remain readable.
- Reduced-effects mode still communicates send, hit, kill, leak, income, and transfer cues.

## Workstream G: Screenshot QA Gate

Owner: Agent 1 final integration, Agent 2 supplies creep-specific evidence

This workstream is now capture-gated. Existing kit-era screenshots remain historical evidence; final AIPlate certification requires deterministic small, standard, tall, and safe-area portrait captures using the actual runtime UI.

- [ ] Capture full visual review set after tower wrappers.
- [x] Capture role lineup after creep wrappers.
- [x] Capture Runner x10 pressure.
- [x] Capture Swarm heavy pressure.
- [x] Capture Shade readability.
- [x] Capture damaged transfer with reduced health.
- [x] Capture reduced-effects combat.
- [x] Capture grayscale copies for all relevant frames.
- [x] Write review under `docs/screenshot-reviews/stylized-weapon-kit-integration/`.
- [x] Record any medium/high readability regressions in this checklist before merge.

Status note:

- Agent 2 supplied the creep-specific evidence set at `docs/screenshot-reviews/stylized-weapon-kit-integration/review.md`. No medium/high readability regressions were found; one low-severity staged leak-text overlap remains for later capture/HUD polish.

Acceptance:

- Review verdict is `Pass` or `Pass with low-severity polish follow-ups`.
- Any rejected candidate is documented with the reason.

## Workstream H: Repo Hygiene And Sync

Owner: Agent 1

- [ ] Do not commit Unity-generated package/version churn unless intentionally changing editor/package baseline.
- [ ] Keep `ProjectSettings.asset` changes out unless tied to a deliberate project setting decision.
- [ ] Commit wrappers, source notes, visual library references, and screenshot evidence together.
- [ ] Run `dotnet test LTW.sln` if simulation-facing code changes.
- [ ] Run Unity import/compile check after prefab/library changes.
- [ ] Push to cloud after reviewed implementation slices.

## Suggested Implementation Order

1. Agent 1: asset triage/contact sheet.
2. Agent 1: Arrow, Control, Relay tower wrappers.
3. Agent 2: Runner, Brute, Swarm creep wrappers.
4. Agent 1: Pulse, Prism tower wrappers and builder prop.
5. Agent 2: Shade, Siege creep wrappers and role-specific VFX anchors.
6. Shared: UI icon candidates.
7. Agent 1: full screenshot QA gate and docs update.
8. Agent 1: merge/sync after review.

## Definition Of Done

- [x] **Superseded:** kit-derived wrapper completion is not required because AIPlate prefabs own the active baseline; kit assets remain isolated fallback/history.
- [x] All five tower roles use improved active AIPlate silhouettes.
- [x] All five creep roles use improved active AIPlate silhouettes.
- [x] Builder avatar/tooling uses the active authored Builder plate.
- [ ] Final build/send icons match the active silhouettes in normal, selected, disabled, and grayscale states.
- [ ] VFX attachment points align with active AIPlate landmarks.
- [ ] Full four-profile mobile screenshot review exists with grayscale evidence after capture automation is complete.
- [ ] `docs/MVP_DEPENDENCIES.md` still records the asset source/license note.
- [ ] `main` is synced to cloud after accepted implementation.
