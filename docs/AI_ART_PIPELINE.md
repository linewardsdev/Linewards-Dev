# AI-Assisted Art Pipeline

Date created: 2026-07-15

## Purpose

Move Line Wards beyond low-fi polygon placeholders by using AI-assisted art where it is strongest: concept exploration, consistent 2D/2.5D source plates, texture/material ideation, icon generation, and rapid visual comparison.

This is not a plan to blindly drop raw AI 3D output into Unity. The near-term goal is a controlled, readable, mobile-first production pipeline that creates polished tower and creep visuals faster than hand-modeling every asset from scratch.

## Pipeline Thesis

For the current Line Wards camera and gameplay scale, a polished 2.5D board-token pipeline is more likely to succeed than raw AI-generated 3D models.

Use AI to generate consistent concept sheets and rendered source plates. Then convert approved role art into Unity-ready sprites, billboard/card tokens, icons, and eventually authored meshes only where needed.

This should give the game a stronger early-2000s polished mobile/board-game read without fighting topology, rigging, UV cleanup, and tiny high-detail meshes that disappear at gameplay zoom.

## Non-Negotiables

- Preserve mobile gameplay readability before visual detail.
- Keep the active north-south lane, grid cells, path state, creep flow, and HUD readable.
- Keep creeps smaller/subordinate to towers.
- Use original ward-tech fantasy: lenses, pylons, signal plates, glass cores, arcane circuitry, beacon shapes, shard constructs.
- Do not prompt for, imitate, or import Warcraft, Blizzard, Warcraft III, Horde, Alliance, Night Elf, Undead, Orc, Human, RTS command-card chrome, faction silhouettes, icons, sounds, or screenshots.
- Do not ship player-facing AI-generated content without provenance notes and release/disclosure review.
- Do not let generated detail replace role silhouette. Shape must carry the read.
- Do not commit raw throwaway generations into runtime folders.

## Recommended Tool Roles

| Need | Preferred AI Use | Finalization Step |
| --- | --- | --- |
| Tower/creep ideation | AI concept sheets | Human selects one silhouette per role. |
| In-game role art | AI-rendered 2.5D source plates | Clean transparent sprite/card token in Unity. |
| Icons | AI/rendered role crop from same approved art | Manual crop, contrast, disabled/selected states. |
| Materials/textures | AI texture ideation or Substance-style text-to-texture | Manual palette/value normalization. |
| 3D props | AI/static 3D only for simple props | Manual scale, topology, UV, LOD, material cleanup. |
| Characters/animated units | Avoid full AI 3D for now | Use sprite/card token or authored simple mesh. |

## Folder Structure

Use isolated folders so generated material never gets confused with runtime-ready assets.

```text
docs/art-pipeline/
  ai-art-generation-log.md
  prompt-library.md
  role-contact-sheets/
  review-notes/

unity/LTW.UnityClient/Assets/Art/AIStaging/
  Concepts/
  SourcePlates/
  Rejected/

unity/LTW.UnityClient/Assets/Art/Towers/Production/
  Sprites/
  Materials/
  SourceNotes/

unity/LTW.UnityClient/Assets/Art/Creeps/Production/
  Sprites/
  Materials/
  SourceNotes/

unity/LTW.UnityClient/Assets/Resources/Art/Builder/Production/
  Sprites/

unity/LTW.UnityClient/Assets/Prefabs/Towers/
unity/LTW.UnityClient/Assets/Prefabs/Creeps/
```

Rules:

- `AIStaging` is not runtime-facing.
- Runtime prefabs stay in `Assets/Prefabs/...`.
- Builder runtime sprites may live under `Assets/Resources/Art/Builder/...` because the current Builder avatar is procedural and loaded from code.
- Production sprites/materials use Line Wards names, not generator prompt names.
- Third-party assets stay in their own dependency folders.
- Binary source images should use Git LFS if they become numerous or large.

## Asset Record Template

Every AI-assisted asset that enters production needs a short record.

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

## Prompt Rules

### Base style prompt

Use this as the shared style block for role prompts:

```text
original mobile tower-wars game asset, ward-tech fantasy, clean readable board-game strategy token, top-down three-quarter view, strong silhouette, dark slate base, arcane blue and violet energy, signal gold accents, mint highlights, simple readable forms, polished early-2000s strategy game feel, transparent background, no text, no UI frame
```

### Negative prompt / avoidance block

Use this avoidance block for every generation:

```text
no Warcraft, no Blizzard, no Warcraft III, no Horde, no Alliance, no Night Elf, no Undead, no Orc, no Human faction, no RTS command card, no copied game icon, no medieval faction banner, no realistic gore, no busy background, no tiny unreadable details, no text labels, no watermark
```

### Tower role prompts

Arrow:

```text
focused rail ward tower, compact base with long luminous bolt rail, glass lens core, forward muzzle, elegant side limbs, single-target precision silhouette, original ward-tech fantasy, [base style prompt], [avoidance block]
```

Control:

```text
containment dish ward tower, wide circular ring emitter, suspended core, restraint arcs, crowd-control field silhouette, original ward-tech fantasy, [base style prompt], [avoidance block]
```

Relay:

```text
signal relay ward tower, tall beacon mast, capacitor fins, glowing antenna crown, support/economy silhouette, original ward-tech fantasy, [base style prompt], [avoidance block]
```

Pulse:

```text
impact pulse ward tower, heavy drum core, shockwave rings, pressure vents, area damage silhouette, original ward-tech fantasy, [base style prompt], [avoidance block]
```

Prism:

```text
prism lens ward tower, tall crystal focus spire, glass aperture, beam anchor facets, precision energy silhouette, original ward-tech fantasy, [base style prompt], [avoidance block]
```

### Creep role prompts

Runner:

```text
fast pressure construct creep, sharp dart body, glowing spine, low profile, speed streak cue, small readable silhouette, original ward-tech fantasy, [base style prompt], [avoidance block]
```

Brute:

```text
armored health shell creep, broad rounded plates, heavy core, slow tank silhouette, small but chunky board-game token, original ward-tech fantasy, [base style prompt], [avoidance block]
```

Swarm:

```text
swarm pressure creep group, several tiny shardlings moving as a cluster, repeated small silhouettes, volume pressure read, original ward-tech fantasy, [base style prompt], [avoidance block]
```

Shade:

```text
stealth shade creep, dark glass shard body, echo facets, shimmer outline, readable invisible-unit cue, original ward-tech fantasy, [base style prompt], [avoidance block]
```

Siege:

```text
siege pressure creep, directional ram body, forward impact plate, warning glow, tower-attacker silhouette, original ward-tech fantasy, [base style prompt], [avoidance block]
```

Builder:

```text
friendly builder avatar, small worker/tool user, visible construction wand or signal tool, non-combat silhouette, mint/gold friendly cues, original ward-tech fantasy, [base style prompt], [avoidance block]
```

## Production Workflow

### Stage 1: Role contact sheet

For each role, generate 8 to 16 variations using the shared style block. Do not implement from a single pretty image.

Required review questions:

- Can the role be recognized from silhouette alone?
- Does it read at phone gameplay scale?
- Is it distinct from the other four roles in its category?
- Does it avoid protected-game/faction resemblance?
- Does the design still feel like Line Wards?

Output:

- Contact sheet image in `docs/art-pipeline/role-contact-sheets/`.
- One review note with selected candidate and rejection reasons.

### Stage 2: Source plate cleanup

For the selected candidate:

- Generate or edit a clean transparent-background source plate.
- Prefer top-down three-quarter view.
- Keep lighting direction consistent across all roles.
- Remove text, watermarking, frame artifacts, extra props, and noisy detail.
- Create normal and grayscale checks.

Output:

- `Assets/Art/AIStaging/SourcePlates/{role}_source_plate_v01.png`
- Review note with prompt/tool/provenance.

### Stage 3: Trimmed Production Candidate

Do not feed square concept/source plates directly into runtime prefabs.

For each selected role:

- Regenerate or edit the role as a compact, forward-facing production sprite.
- Remove baked VFX, trails, glow fields, UI framing, and background logic.
- Remove the chroma key to alpha.
- Trim to the visible silhouette and re-pad on a consistent transparent canvas.
- Produce normal and grayscale copies.
- Make a tiny-scale review sheet before opening Unity.

Output:

- `Assets/Art/Towers/Production/Sprites/{role}_candidate_v##_trimmed.png`
- `Assets/Art/Creeps/Production/Sprites/{role}_candidate_v##_trimmed.png`
- Review sheet under `docs/screenshot-reviews/{pass-name}/`.

Gate:

- If the tiny-scale review does not beat the current authored/source-kit baseline, stop and iterate the image. Do not build a runtime prefab.

### Stage 4: Unity Proof Token

Convert the source plate into a runtime object.

Recommended near-term implementation:

- Transparent sprite or quad/card mesh.
- Optional simple shadow plane.
- Optional small base/ownership ring.
- Required runtime child names preserved.
- One prefab per role under `Assets/Prefabs/...`.

For towers:

- Keep `Body`, `RoleMarker`, `OwnerTrim`, and `RangeHalo`.
- `Body` can be the sprite/card renderer if that is the real visual body.
- Keep VFX anchors like `Muzzle`, `Lens`, `ControlCore`, etc.
- Footprint must still fit the occupied grid cell.

For creeps:

- Keep `Body`, `GroundShadow`, and `RoleMarker`.
- `Body` can be the sprite/card renderer if that is the real visual body.
- Health/damage cues must align with the visible token.
- Proof prefab generation must not mutate active runtime visual libraries.
- Runtime promotion is a separate, intentional step after screenshots pass.

### Stage 5: Icon extraction

Every implemented role must produce menu icons from the same approved art.

Required states:

- Normal
- Selected
- Disabled/too expensive
- Grayscale-readable

Icons should make tower/send menus match what appears in the lane.

### Stage 6: Screenshot QA gate

Run screenshot review before calling a role done.

Minimum captures:

- Tower lineup normal.
- Tower lineup grayscale.
- Creep role lineup normal.
- Creep role lineup grayscale.
- Runner x10 pressure.
- Swarm heavy pressure.
- Active combat with HUD visible.
- Reduced-effects combat.
- Build/send menu icons.

Acceptance:

- First-time viewer can tell the role category without labels.
- The lane path and grid remain readable.
- Art does not hide placement state, builder position, leaks, health, or HUD.
- Grayscale still distinguishes role families.

## Vertical Slice Plan

Start with two roles because they are already causing pain and have clear readability targets.

### Slice A: Arrow Tower

Goal:

- Replace primitive/kitbashed Arrow with a polished ward-tech rail turret token.

Tasks:

- [ ] Generate Arrow contact sheet.
- [ ] Select one silhouette.
- [ ] Produce transparent source plate.
- [ ] Build `Tower_Arrow` sprite/card prefab wrapper.
- [ ] Preserve `Body`, `RoleMarker`, `OwnerTrim`, `RangeHalo`, `Muzzle`, and `Lens`.
- [ ] Rebuild Arrow build icon from the same source.
- [ ] Capture normal/grayscale screenshots.

### Slice B: Runner Creep

Goal:

- Replace primitive/kitbashed Runner with a sharp readable dart-construct token.

Tasks:

- [ ] Generate Runner contact sheet.
- [ ] Select one silhouette.
- [ ] Produce transparent source plate.
- [ ] Build `Creep_Runner` sprite/card prefab wrapper.
- [ ] Preserve `Body`, `GroundShadow`, `RoleMarker`, and `Damage`.
- [ ] Rebuild Runner send icon from the same source.
- [ ] Capture Runner x10 pressure normal/grayscale screenshots.

## Decision Gate

After Arrow and Runner:

- If the 2.5D token pipeline creates a visible quality jump, apply it to the remaining eight tower/creep roles.
- If it looks flat or cheap in motion, keep the approved concept sheets but move to authored simple meshes with AI-assisted texture/material passes.
- If AI output is inconsistent, use the prompts only as concept art and commission/build a small coherent asset set manually.

Current decision:

- [x] 2026-07-15: Arrow v06 and Runner v07 established the V1 art benchmark. Continue with the 2.5D token pipeline for the remaining towers, creeps, and Builder using `docs/art-pipeline/v1-art-fast-track.md`.

## Implementation Checklist

- [x] Create `docs/art-pipeline/ai-art-generation-log.md`.
  - [x] 2026-07-15: Added generation log with asset record template and current Arrow/Runner source-kit proof records.
- [x] Create `docs/art-pipeline/prompt-library.md`.
  - [x] 2026-07-15: Added shared style, avoidance block, role prompts, contact-sheet pattern, and source-plate pattern.
- [x] Create `Assets/Art/AIStaging/` folder structure.
  - [x] 2026-07-15: Added `Concepts`, `SourcePlates`, and `Rejected` staging folders with README guardrails.
- [x] Create `Assets/Art/Towers/Production/` folder structure.
  - [x] 2026-07-15: Added production `Sprites`, `Materials`, and `SourceNotes` folders with README guardrails.
- [x] Create `Assets/Art/Creeps/Production/` folder structure.
  - [x] 2026-07-15: Added production `Sprites`, `Materials`, and `SourceNotes` folders with README guardrails.
- [x] Generate Arrow contact sheet.
  - [x] 2026-07-15: Added ready-to-run Arrow contact-sheet prompt brief in `docs/art-pipeline/arrow-runner-contact-sheet-brief.md`.
  - [x] 2026-07-15: Generated `docs/art-pipeline/role-contact-sheets/tower_arrow_contact_sheet_v01.png` and staged review note.
- [x] Generate Runner contact sheet.
  - [x] 2026-07-15: Added ready-to-run Runner contact-sheet prompt brief in `docs/art-pipeline/arrow-runner-contact-sheet-brief.md`.
  - [x] 2026-07-15: Generated `docs/art-pipeline/role-contact-sheets/creep_runner_contact_sheet_v01.png` and staged review note.
- [x] Generate remaining tower contact sheets.
  - [x] 2026-07-15: Generated Control, Relay, Pulse, and Prism contact sheets with staged review notes.
- [x] Generate remaining creep contact sheets.
  - [x] 2026-07-15: Generated Brute, Swarm, Shade, and Siege contact sheets with staged review notes.
- [x] Generate Builder contact sheet.
  - [x] 2026-07-15: Generated Builder contact sheet with staged review note.
- [x] Select preferred contact-sheet candidates.
  - [x] 2026-07-15: Recorded user-selected candidates in `docs/art-pipeline/selected-candidates-v01.md` and updated review notes.
- [x] Produce first-pass selected source-plate crops.
  - [x] 2026-07-15: Created 512x512 source-plate drafts for all selected candidates under `Assets/Art/AIStaging/SourcePlates/`.
- [x] Produce clean transparent source plates.
  - [x] 2026-07-15: Regenerated selected candidates on chroma-key backgrounds, removed key to alpha PNGs, normalized 1024x1024 `v03` source plates, and created grayscale review copies.
- [x] Implement Arrow token prefab.
  - [x] 2026-07-15: Generated `Assets/Prefabs/Towers/Tower_Arrow_AIPlate.prefab` from `tower_arrow_source_plate_v03.png` for proof review.
- [x] Implement Runner token prefab.
  - [x] 2026-07-15: Generated `Assets/Prefabs/Creeps/Creep_Runner_AIPlate.prefab` from `creep_runner_source_plate_v03.png` for proof review.
- [x] Restore active runtime defaults after live review.
  - [x] 2026-07-15: Restored `tower.arrow` and `creep.runner` visual library references to the cleaner authored/source-kit prefabs; AI source-plate prefabs remain staged proof assets only.
- [x] Produce Arrow/Runner trimmed production candidates.
  - [x] 2026-07-15: Generated compact `v04` Arrow and Runner candidates, removed chroma key, trimmed to real silhouette, wrote grayscale copies, and captured `docs/screenshot-reviews/ai-production-candidate-v04/candidate-v04-review-sheet.png`.
- [x] Update proof generator to avoid accidental runtime promotion.
  - [x] 2026-07-15: `AiSourcePlateProofGenerator` now reads the `v04` production sprites and generates proof prefabs without changing active visual libraries.
- [x] Validate corrected v04 proof flow.
  - [x] 2026-07-15: Batchmode generation confirmed: "Generated AI source plate proof prefabs. Runtime visual libraries were not changed."
  - [x] 2026-07-15: `ValidateAiSourcePlateProofPrefabs`, `ValidateTowerPlaceholderPrefabs`, and `ValidateCreepVisualLibrary` passed.
  - [x] 2026-07-15: Captured v04 Unity proof sheets under `docs/screenshot-reviews/ai-production-candidate-v04/unity-proof-captures/`.
- [x] Tune v04 proof prefab scale.
  - [x] 2026-07-15: Increased Arrow/Runner proof sprite scales and captured normal/grayscale scale-tuned contact sheets under `docs/screenshot-reviews/ai-production-candidate-v04/unity-proof-captures-scale-tuned/`.
- [x] Run v04 active-lane review.
  - [x] 2026-07-15: Added review-only cloned visual library override for gameplay captures, so active-lane proof screenshots can use AI proof prefabs without mutating runtime assets.
  - [x] 2026-07-15: Captured active-lane normal, grayscale, and reduced-effects evidence under `docs/screenshot-reviews/ai-production-candidate-v04/active-lane-proof-captures/`.
  - [x] 2026-07-15: Captured polished review-only active-lane evidence under `docs/screenshot-reviews/ai-production-candidate-v04/active-lane-proof-captures-polished-v2/`, including a zoom review sheet.
  - [x] 2026-07-15: Verdict: v04 Arrow is promotion-worthy pending explicit approval. v04 Runner validates the proof pipeline and passes grayscale, but should get a v05 color/value pass before final runtime promotion.
- [x] Promote reviewed Arrow/Runner proof assets to runtime.
  - [x] 2026-07-15: User explicitly approved runtime deployment. `tower.arrow` now points at `Tower_Arrow_AIPlate.prefab`; `creep.runner` now points at `Creep_Runner_AIPlate.prefab` using the v07 Runner refinement sprite.
  - [x] 2026-07-15: Promotion remained separate from proof generation/capture.
- [x] Produce Runner v05/v06 color/value polish if the team wants a cleaner final creep read.
  - [x] 2026-07-15: Generated Runner v05 and v06 candidates; v05 fixed color but became too chunky, while v06 restored a long/narrow fast-dart silhouette.
  - [x] 2026-07-15: Added v06b/v06c local cleanup passes to remove magenta fringe and strengthen teal/cyan gameplay read.
  - [x] 2026-07-15: Updated the proof generator so `Creep_Runner_AIPlate.prefab` uses `creep_runner_candidate_v06c_trimmed.png`.
  - [x] 2026-07-15: Hardened the proof generator to force generated production PNGs to import as Unity sprites before prefab generation.
  - [x] 2026-07-15: Captured v06 review evidence under `docs/screenshot-reviews/ai-production-candidate-v05/`.
- [x] Review Runner active-lane overlay behavior before promotion.
  - [x] 2026-07-15: Added review-only suppression for creep health/readability overlays, cleared stale prefab pools during AI proof capture, and changed the AI proof active-lane scenario to Runner-only pressure so combat/projectile effects do not mask the sprite.
- [ ] Final Runner runtime scale/overlay pass.
  - [x] 2026-07-15: Runner v07 is live, scaled down to reduce overlap, and primitive scaffold renderers are disabled.
- [ ] Rebuild Arrow and Runner icons.
- [x] Apply V1 fast-track to remaining roles.
  - [x] Control
    - [x] 2026-07-15: Generated Control v01 V1 production sprite, created `Tower_Control_AIPlate.prefab`, and promoted `tower.control` to the AI plate visual library entry.
  - [x] Relay
    - [x] 2026-07-15: Generated Relay v01 V1 production sprite, created `Tower_Relay_AIPlate.prefab`, and wired `tower.relay` to the AI plate visual library entry for active proof review.
  - [x] Pulse
    - [x] 2026-07-15: Promoted `tower_pulse_candidate_v01_trimmed.png` through `Tower_Pulse_AIPlate.prefab` and wired `tower.pulse` to the AI plate visual library entry.
  - [x] Prism
    - [x] 2026-07-15: Promoted `tower_prism_candidate_v01_trimmed.png` through `Tower_Prism_AIPlate.prefab` and wired `tower.prism` to the AI plate visual library entry.
  - [x] Brute
    - [x] 2026-07-15: Generated Brute v01, replaced it with Brute v02, then applied v02b to further mute the yellow-block armor read. `Creep_Brute_AIPlate.prefab` now uses `creep_brute_candidate_v02b_trimmed.png`.
  - [x] Swarm
    - [x] 2026-07-15: Generated Swarm v01 V1 production sprite, created `Creep_Swarm_AIPlate.prefab`, and wired `creep.swarm` to the AI plate visual library entry for active proof review.
  - [x] Shade
    - [x] 2026-07-15: Promoted `creep_shade_candidate_v01_trimmed.png` through `Creep_Shade_AIPlate.prefab`, wired `creep.shade`, and cleared old overlay tint paths.
  - [x] Siege
    - [x] 2026-07-15: Generated Siege v01 V1 production sprite, created `Creep_Siege_AIPlate.prefab`, and wired `creep.siege` to the AI plate visual library entry for active proof review.
  - [x] Builder
    - [x] 2026-07-15: Added `builder_candidate_v01_trimmed.png` as a Resources-loaded sprite layer over the procedural Builder avatar.
- [x] Record V1 role coverage.
  - [x] 2026-07-15: Added `docs/art-pipeline/v1-role-coverage-report.md` showing all five towers, all five creeps, and Builder mapped to their runtime assets.
- [x] Run Unity prefab/library validation.
  - [x] 2026-07-15: `ValidateAiSourcePlateProofPrefabs` passed in Unity batchmode.
  - [x] 2026-07-15: `ValidateTowerPlaceholderPrefabs` passed in Unity batchmode.
  - [x] 2026-07-15: `ValidateCreepVisualLibrary` passed in Unity batchmode.
- [x] Run proof contact-sheet screenshot QA.
  - [x] 2026-07-15: Captured normal and grayscale proof sheets under `docs/screenshot-reviews/ai-source-plate-proof/captures/`.
  - [x] 2026-07-15: Result: Arrow/Runner AI plates render without breaking token footprint after scale correction, but they should remain proof assets until source-specific crop/pose cleanup and gameplay-pressure review beat the authored/source-kit silhouettes.
- [ ] Run full gameplay screenshot QA.
  - [ ] Runner x10 pressure.
  - [ ] Arrow/Runner in active lane combat at phone-scale camera.
  - [x] 2026-07-15: Captured Arrow/Runner proof active-lane zoom evidence for v04 under `docs/screenshot-reviews/ai-production-candidate-v04/active-lane-proof-captures-polished-v2/zoom-review-sheet.png`.
  - [x] 2026-07-15: Captured Runner v06 Unity proof contact sheet and active-lane evidence under `docs/screenshot-reviews/ai-production-candidate-v05/`.
  - [ ] Build/send icon match once icons are rebuilt.
- [x] Update `PROPER_ART_REPLACEMENT_PASS_CHECKLIST.md` with the selected pipeline.
  - [x] 2026-07-15: Added AI-assisted 2.5D token pipeline setup note and kept generation/implementation work open.
- [ ] Push to cloud.

## Research Notes

- Layer recommends dedicated generated-asset folders, import presets, naming conventions, Git LFS for binary generated assets, and an art review gate before generated assets enter final builds: https://www.layer.ai/integrations/unity
- Sloyd recommends treating AI 3D as a pipeline input that still needs topology, UV, LOD, material, compatibility, and performance checks. It also notes AI 3D currently works best for props/backgrounds and struggles more with characters/animations: https://www.sloyd.ai/blog/7-best-practices-for-ai-generated-3d-models-in-game-development
- Meshy’s Unity workflow highlights export format, material setup, scale correction, render-pipeline setup, optimization, LODs, and animation import as recurring handoff issues: https://www.meshy.ai/tutorials/3d-model-for-unity-workflow
- Steam/Valve disclosure guidance distinguishes backend workflow tools from AI-generated content that ships with the game or appears in marketing. Player-facing AI content requires release/disclosure review: https://www.gamedeveloper.com/business/valve-tweaks-and-clarifies-ai-disclosure-rules-for-steam
- Unity Asset Store content transparency policy emphasizes IP/legal compliance and moderation of prohibited/restricted content, which reinforces the need for provenance notes and safe prompt boundaries: https://unity.com/legal/asset-store-content-transparency
