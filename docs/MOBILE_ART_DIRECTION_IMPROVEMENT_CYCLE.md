# Mobile Art Direction Improvement Cycle

## Purpose

This document defines the repeatable graphics and art-direction improvement cycle for Line Wards.

It coordinates the existing art pipelines into one mobile-only process that an implementation agent can run repeatedly. It does not replace the specialized pipeline documents. It establishes how to audit them, choose the next coherent visual package, capture evidence, evaluate results, update the correct checklists, and hand work off for implementation testing.

Line Wards is a portrait mobile game. Every capture, review, and acceptance decision in this cycle must use portrait phone framing. Desktop layouts and desktop visual targets are out of scope.

## Visual North Star

Preserve the competitive readability inherited from classic Line Tower Wars while using original Line Wards art:

- Long, skinny north-south defensive lanes dominate the screen.
- The arena reads before the interface.
- Persistent match information hugs the top edge.
- Primary build/send actions use the bottom thumb zones.
- Secondary controls use a restrained right-side rail.
- Towers, creeps, and pressure states read by silhouette before color.
- Board surfaces remain quieter than gameplay objects and feedback.
- Effects explain events without hiding placement cells or leak moments.
- Ward-tech fantasy remains original and does not copy Warcraft III assets, silhouettes, names, icons, sounds, factions, or UI chrome.

## Source Documents

Agents must review these sources before selecting work:

| Source | Authority |
| --- | --- |
| `skill/line-wards-ltw-graphics-art-direction.md` | LTW lineage, mobile lane composition, visual priorities, legal boundary |
| `docs/ART_THEME_AND_ROLE_GUIDE.md` | Tower, creep, icon, accent, silhouette, and phone-size role language |
| `docs/archive/2026-07-planning/GRAPHICS_2000_BASELINE_ROADMAP.md` | Archived graphics maturity stages and baseline definition of done |
| `docs/archive/2026-07-art-pipeline/GRAPHICS_THEME_WORK_BREAKDOWN.md` | Parallel work packages and package-level screenshot requirements |
| `docs/archive/2026-07-art-pipeline/AI_ART_PIPELINE.md` | Generated 2.5D source plates, proof assets, promotion rules, and provenance |
| `docs/archive/2026-07-art-pipeline/PROPER_ART_REPLACEMENT_PASS_CHECKLIST.md` | Production replacement, materials, icons, anchors, and final QA |
| `docs/archive/2026-07-art-pipeline/STYLIZED_WEAPON_KIT_INTEGRATION_CHECKLIST.md` | Third-party source isolation, wrappers, licensing, and kit-derived candidates |
| `docs/art-pipeline/ui-board-art-pipeline.md` | UI chrome, command cards, board materials, gates, and integration stages |
| `docs/art-pipeline/ui-board-contact-sheet-brief.md` | Approved contact-sheet construction and review requirements |
| `docs/art-pipeline/ui-board-pipeline-checklist.md` | Current UI/board production and QA work |
| `docs/art-pipeline/ui-board/selected-candidates-v02.md` | Selected V02 visual directions and implementation order |
| `docs/GAMEPLAY_DEVELOPMENT_CHECKLIST.md` | Current presentation behavior and remaining gameplay-readability acceptance checks |
| `docs/BRANDING_GUIDE.md` | Brand palette, voice, typography, and originality rules |

## Pipeline Precedence

Several pipelines overlap because the project has progressed from generated placeholders through source-kit experiments and AI-assisted production plates. An unchecked task in an older checklist is not automatically the next task.

Use this precedence when records disagree:

1. Inspect the active runtime visual-library mapping and current prefab or sprite references.
2. Inspect the newest dated implementation note and screenshot review for that role or component.
3. Treat the specialized current pipeline as authoritative for its component.
4. Use older checklist items as historical requirements or validation reminders.
5. Update stale checklist wording when current implementation has superseded it.
6. Never replace a promoted runtime asset merely to satisfy an older unchecked source-kit task.

Examples:

- An active reviewed AI plate supersedes an older requirement to create a weapon-kit wrapper for the same role unless a new comparison proves the wrapper is better.
- A promoted V02 command-card treatment supersedes the earlier procedural-card task, while its grayscale and phone-size acceptance checks remain valid.
- A lane selector is not a true map-camera control. Do not mark map-view behavior complete until that behavior exists and is intentionally accepted.

## Current Pipeline Baseline

At the time this cycle was created:

- V1 runtime art covers all five tower roles, all five creep roles, and the Builder.
- AI-assisted plates are active for the current role set, with procedural fallbacks retained.
- V02 command-card chrome and persistent lane-selector control chrome have been promoted.
- Selected UI/board directions remain:
  - HUD/stat drawer: option 6.
  - Icon family: option 6.
  - Board material: option 11.
  - Spawn/leak gates: option 11.
- Final silhouette-matched icon rebuilding remains incomplete.
- Material normalization and grayscale value balancing remain incomplete.
- Several authored VFX anchors and role-specific motion passes remain incomplete.
- Full mobile gameplay screenshot certification remains incomplete.
- Older source-kit checklist items must be reconciled against the active AI-plate mappings before implementation.

Agents must verify this baseline against the repository at the start of every run.

## Automated Improvement Loop

### 1. Audit

Create a branch from current `main`, then inspect:

- Active `TowerVisualLibrary` and `CreepVisualLibrary` mappings.
- Builder and runtime UI asset loading.
- Existing prefab contracts and named anchors.
- Selected UI/board candidates.
- Every unchecked item in the relevant pipeline checklists.
- The newest screenshot review for the affected component.
- Existing fallback behavior and source/provenance notes.

Write a short audit section in the run report before changing assets.

### 2. Select One Work Package

Choose one coherent gameplay read, not an isolated decorative object.

Allowed package types:

| Package | Typical Scope |
| --- | --- |
| `GD-Mobile-UI-Board` | HUD chrome, command cards, controls, board materials, gates |
| `GD-Tower-Identity` | Tower silhouette, scale, materials, animation, icon, anchors |
| `GD-Creep-Identity` | Creep silhouette, scale, materials, motion, icon, overlays |
| `GD-Combat-Feedback` | Shots, hits, deaths, leak, send, income, specialist cues |
| `GD-Lane-Readability` | Route/build-zone contrast, endpoints, ownership, pressure |
| `GD-Mobile-Regression` | Capture-only certification after merged graphics work |
| `GD-Art-Pipeline-Hygiene` | Checklist reconciliation, provenance, import settings, fallbacks |

Do not combine unrelated packages merely to increase branch size.

### 3. Capture The Baseline

Run:

`Line Wards/Review/Capture Visual Review Set`

Batch runs may call:

`LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureVisualReviewSet`

Improvement-cycle batch runs should call:

`LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureMobileImprovementCycle`

Recommended arguments:

```text
-ltwCaptureOutputDir docs/screenshot-reviews/<run-name>
-ltwCaptureRunId <run-name>
-ltwCapturePhase before|after
-ltwCaptureSeed 1
-ltwCapturePackage GD-Mobile-UI-Board
-ltwCaptureGrayscale
-ltwExitAfterCapture
```

All captures must emulate portrait mobile screens.

Required phone profiles:

| Profile | Purpose |
| --- | --- |
| `phone-small-portrait` | Compact screen and minimum touch-clearance risk |
| `phone-standard-portrait` | Primary reference composition |
| `phone-tall-portrait` | Tall aspect ratio and vertical distribution |
| `phone-safe-area-portrait` | Notch, status inset, and bottom gesture-area safety |

Required visual states:

1. `01-default-hud.png`
2. `02-build-menu-open.png`
3. `03-build-card-selected.png`
4. `04-send-menu-open.png`
5. `05-send-card-disabled.png`
6. `06-lane-selector-open.png`
7. `07-active-combat.png`
8. `08-runner-10-pressure.png`
9. `09-swarm-heavy-pressure.png`
10. `10-heavy-pressure.png`
11. `11-reduced-effects-heavy.png`
12. `12-board-overview.png`
13. `13-spawn-gate-focus.png`
14. `14-leak-gate-focus.png`
15. `15-results-or-late-match.png`

### Target Reference Requirement

Every improvement-cycle pass must declare the selected target art it is trying to approach. The report generator resolves known package names through `VisualTargetReferenceCatalog` and copies those references into:

`docs/screenshot-reviews/<run-name>/<run-name>/target-references/`

The generated `improvement-cycle-review.md` must include a Target References table before the capture matrix. A pass is not ready for approval unless the agent review assigns both:

- a visual/readability score for the runtime capture;
- a target-reference match score against the selected image.

For UI/board work, the canonical targets are the cropped selections in:

`docs/art-pipeline/ui-board/selected-candidates/`

For tower, creep, and builder identity work, the canonical targets are the current promoted V1 production sprites recorded in:

`docs/art-pipeline/v1-role-coverage-report.md`

Add focused captures when relevant:

- Tower lineup normal and grayscale.
- Creep lineup normal and grayscale.
- Selected tower and range state.
- Placement preview states.
- Builder select, confirm, and build-complete states.
- Leak/life-loss moment.
- Siege warning and Shade resist/reveal.
- Income tick and outgoing send feedback.

Every relevant frame needs a grayscale/value copy.

Optional intensity flag:

- `-ltwIntensity safe`: small polish, minimal layout risk.
- `-ltwIntensity medium`: visible focused subsystem change.
- `-ltwIntensity aggressive`: noticeable runtime visual/layout change; medium polish debt is acceptable if the pass is not subtle.
- `-ltwIntensity breakthrough`: large direction push that may temporarily break spacing or balance.

### 4. Score The Baseline

Score each category from 0 to 3:

| Score | Meaning |
| --- | --- |
| 0 | Broken, absent, or misleading |
| 1 | Functional but unclear or visibly placeholder |
| 2 | Readable with low- or medium-severity polish issues |
| 3 | Cohesive, mobile-readable, and ready to lock as baseline |

#### Axis A — Readability (blocking)

- Mobile arena fit.
- Long north-south lane readability.
- Spawn, route, and leak-gate clarity.
- UI edge discipline and touch clearance.
- Tower silhouette and role identity.
- Creep silhouette and threat identity.
- Grayscale value separation.
- Heavy-pressure readability.
- Reduced-effects readability.
- Combat signal priority.
- Motion clarity.
- Palette and material cohesion.
- Icon-to-runtime silhouette match.
- Original Line Wards identity.
- Fallback and missing-asset behavior.

A score of 0 in arena fit, lane readability, touch clearance, heavy pressure, reduced effects, or originality blocks promotion.

#### Axis B — Craft (advisory until Wave 3, then blocking)

Every one of the fifteen categories above is a readability criterion. That is a real gap
rather than a stylistic quibble: **a build can score 3 on all fifteen and look exactly like
the current one.** Readability asks whether the player can tell what a thing is; nothing
above asks whether it looks like a finished game. These twelve ask the second question.

Same 0–3 scale and the same verdict vocabulary as Axis A.

| # | Category | 0 | 3 |
| --- | --- | --- | --- |
| C1 | Surface detail | Flat, untextured normals; detail reads only as albedo noise | Normal + AO present and legible at phone size; forms read as sculpted |
| C2 | Material differentiation | All units share one apparent material | Metal, crystal, bark, stone are distinguishable at a glance without colour |
| C3 | Specular and highlight behaviour | No highlight, or blown to white | Highlights travel across forms as they rotate; gradients read as intended |
| C4 | Grounding | Units float; no contact cue | Contact shadow plus AO reads the unit as standing on the board |
| C5 | Lighting craft | Flat, ambient-only read | Key/fill/rim separate the unit from the board; reflections match the scene |
| C6 | Tone and grade | Untonemapped clipping, or a muddy grade | Highlights roll off; the palette survives the grade; blacks are not crushed |
| C7 | Impact feedback | Flat primitive cue | Hit reads as an event — particle, flash, and a response on the target |
| C8 | Death and spawn | Instant pop | Deaths and arrivals have a beat the eye can follow |
| C9 | Projectile craft | Untextured stretched primitive | Trail, muzzle and impact read as one coherent effect |
| C10 | Typography | Default engine font | An authored typeface, consistently applied, legible at phone size |
| C11 | UI craft | Flat rects, hard edges, no state motion | Framed, layered, with tweened state changes and readable hierarchy |
| C12 | Motion richness | One clip, or none | Idle, move, hit and death read distinctly per unit |

Craft categories are **scored from the first uplift pass and advisory until Wave 3**, then
blocking. They are not blocking on arrival for the same reason the intake gate is not yet
strict: several of them score 0 across the whole roster today, and a gate that fails
everything on the day it lands gets switched off within the hour. Scoring them from the
start is what makes the trend visible; blocking on them is what makes it stick.

Two of them cannot reach 3 by any amount of tuning and are limited by missing source data
rather than by settings, which is worth knowing before a pass is scored against them:

- **C1** needs normal and AO maps. Neither exists: the ORM red channel is measurably empty
  across all 21 packed maps, and exactly one normal map exists anywhere in the tree, in an
  untracked FBX media cache. Blocked on OPEN_ITEMS item 3.
- **C12** needs more than one clip per unit.

#### Method rule for both axes

Score against a real match capture rendered with the game's own lighting and
post-processing. Never against a contact sheet, never with `-nographics`, never against a
painted mock. A contact sheet has no board, no light rig and no post stack, so it cannot
show grounding, lighting craft or tone — and a capture taken with `-nographics` silently
answers a different question than the one being asked.

### 5. Implement The Focused Pass

Implementation rules:

- Preserve simulation behavior unless the assigned package explicitly includes gameplay changes.
- Preserve long, skinny north-south lane dimensions and vertical travel.
- Keep runtime fallbacks until the replacement passes review.
- Preserve prefab contracts and required child names.
- Keep third-party source assets isolated from Line Wards runtime wrappers.
- Keep generated source plates, trimmed production candidates, proof prefabs, and runtime promotion as separate stages.
- Do not let proof generation silently change active runtime libraries.
- Derive icons from the same role silhouette used in play.
- Record source, generation, license, and promotion notes.
- Prefer large readable shapes over detail that disappears at phone scale.
- Avoid UI growth that reduces the arena or covers placement-critical cells.

### 6. Re-Capture Identical Evidence

Use the same profiles, state seeds, camera framing, presentation mode, and capture names used for the baseline.

Store evidence under:

`docs/screenshot-reviews/<branch-name>/`

Recommended structure:

```text
docs/screenshot-reviews/<branch-name>/
  before/
    phone-small-portrait/
    phone-standard-portrait/
    phone-tall-portrait/
    phone-safe-area-portrait/
  after/
    phone-small-portrait/
    phone-standard-portrait/
    phone-tall-portrait/
    phone-safe-area-portrait/
  grayscale/
  contact-sheet.png
  review.md
```

The managed improvement-cycle runner writes this compatible structure plus:

- `before-capture-manifest.json` or `after-capture-manifest.json`
- `before-review.md` or `after-review.md`
- `cycle-scorecard.json`
- `improvement-cycle-review.md`

`capture-manifest.json` and `review.md` remain as current-phase compatibility outputs. The phase-specific manifest files are the before/after comparison source of truth.

### 7. Compare And Decide

The working graphics or implementation agent must complete scoring before handoff. Do not return a blank reviewer-score table to the user.

The review must record:

- Before and after score table.
- Improvements visible in specific captures.
- Regressions or unresolved risks.
- Any difference between normal and grayscale readability.
- Any pressure-state overlap, clipping, or obscured controls.
- Whether fallbacks still work.
- Whether the asset is approved for runtime promotion.
- Exact medium- and high-severity issues.
- Recommended next package.

Allowed verdicts:

- `Pass`
- `Pass with low-severity polish follow-ups`
- `Revise before promotion`
- `Reject and retain current runtime asset`

### 8. Update Documentation

Before handoff:

- Update the specialized pipeline checklist that owned the work.
- Mark items complete only when evidence exists.
- Add dated notes for partial completion.
- Correct stale items that were superseded by a newer pipeline.
- Update source/provenance records when assets changed.
- Link the screenshot review from the owning checklist.
- Update this document only when the process or baseline changes.

## Report Template

Create or review `docs/screenshot-reviews/<branch-name>/<run-id>/improvement-cycle-review.md`:

```markdown
# <Package Name> Mobile Art Review

## Audit
- Active runtime mapping:
- Owning pipeline:
- Relevant open items:
- Previous evidence:

## Scope
- Intended gameplay read:
- Assets and systems changed:
- Explicit exclusions:

## Capture Matrix
- Phone profiles:
- Visual states:
- Seeds/presentation modes:

## Scorecard
### Axis A — Readability
| Category | Before | After | Evidence |
| --- | ---: | ---: | --- |

### Axis B — Craft
| Category | Before | After | Evidence |
| --- | ---: | ---: | --- |

## Findings
### High
### Medium
### Low

## Pipeline Reconciliation
- Items completed:
- Items superseded or rewritten:
- Items still open:

## Verdict
- Result:
- Runtime promotion:
- Fallback status:
- Next package:
```

## Promotion Gate

A visual asset or UI treatment may become the locked runtime baseline only when:

- It improves or preserves the relevant score categories on **both** axes.
- No blocking category scores 0. Axis A is blocking now; Axis B becomes blocking at Wave 3.
- Both axes are scored, even while Axis B is advisory. An unscored craft category is not the
  same as a passing one, and leaving it blank is how the fifteen readability categories came
  to be mistaken for a complete picture.
- It reads at normal phone scale without zooming.
- It passes grayscale review.
- It remains readable during heavy pressure.
- Reduced-effects mode preserves critical information.
- It does not cover active placement cells or essential controls.
- Its icon matches its runtime silhouette where applicable.
- Its source and license or generation record are documented.
- Its fallback behavior is verified.
- It preserves original Line Wards visual identity.
- The owning checklist and screenshot review are updated.

## Near-Term Automated Queue

Use this order unless a new high-severity visual regression takes priority:

1. Promote selected HUD/stat drawer option 6.
2. Promote selected board material option 11.
3. Promote selected spawn/leak gate option 11.
4. Rebuild and promote simplified icon family option 6 from active silhouettes.
5. Normalize tower and creep materials and grayscale values.
6. Align VFX origins to authored role landmarks.
7. Add role-specific tower and creep motion.
8. Complete leak, send, income, Pulse, Prism, Shade, and Siege feedback.
9. Run the full mobile screenshot certification pass.
10. Reconcile and close superseded source-kit and earlier AI-pipeline checklist items.

## Branch And Handoff Convention

Recommended branch names:

- `gd-mobile-ui-board-<date>`
- `gd-tower-identity-<role>-<date>`
- `gd-creep-identity-<role>-<date>`
- `gd-combat-feedback-<event>-<date>`
- `gd-mobile-visual-regression-<date>`

The graphics agent hands off:

- Branch name and commit.
- Owning work package.
- Changed assets and runtime mappings.
- Screenshot review path.
- Score changes and unresolved findings.
- Required Unity validation.
- Any simulation tests required because scope crossed presentation boundaries.

Testing and tuning may be performed by a separate implementation agent, but the graphics branch is not complete without its mobile visual evidence and documentation updates.
