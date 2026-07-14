# Graphics Theme Work Breakdown

## Purpose

Break the next Line Wards graphics work into parallel-friendly chunks that combine:

- the implementation roadmap in `GRAPHICS_2000_BASELINE_ROADMAP.md`;
- the silhouette and theme rules in `ART_THEME_AND_ROLE_GUIDE.md`;
- the current visual evidence in `docs/screenshot-reviews/art-2000-readability-pass/`.

Use this as the coordination layer when multiple agents split the art/theme work.

## Current Baseline

Completed:

- 5 tower and 5 creep runtime roles exist.
- Prefab-backed tower and creep visual libraries exist.
- Generated placeholder prefabs/materials exist.
- The renderer falls back to procedural primitives when prefabs are missing.
- Full screenshot capture works, including grayscale/value copies.
- The latest capture review is `docs/screenshot-reviews/art-2000-readability-pass/review.md`.

Still not done:

- Final or polished-prototype tower silhouettes.
- Final or polished-prototype creep silhouettes.
- Role icons for build/send cards.
- Right-side control rail cleanup.
- Full role-specific VFX language.

## Theme Source Of Truth

For all production art and polished prototypes, follow:

- `ART_THEME_AND_ROLE_GUIDE.md`

Especially:

- towers and creeps must read by silhouette before color;
- ward-tech fantasy, not medieval/Warcraft-style fantasy;
- simple early-2000s strategy readability over high-detail modern noise;
- phone-size readability is the acceptance gate.

## Recommended Work Packages

### 1. `art-tower-silhouette-polish`

Goal: replace generated tower placeholders with stronger low-poly prototype silhouettes.

Source sections:

- `ART_THEME_AND_ROLE_GUIDE.md` → Tower Role Guide
- `ART_PREFAB_CONTRACT.md` → tower child-name contract

Scope:

- Arrow: bow/crossbow/tension-limb silhouette with obvious firing rail.
- Control: wide dish/ring/field projector.
- Relay: mast/capacitor/beacon support read.
- Pulse: compact core with shock-ring language.
- Prism: tall crystal lens-spire.

Do not:

- change simulation rules;
- rely only on glow color;
- add tiny details that disappear on phone captures.

Acceptance:

- All five towers remain mapped in `TowerVisualLibrary`.
- Required children remain: `Body`, `RoleMarker`, `OwnerTrim`, `RangeHalo`.
- Normal and grayscale captures show distinct tower silhouettes.

### 2. `art-creep-silhouette-polish`

Goal: replace generated creep placeholders with stronger low-poly prototype silhouettes.

Source sections:

- `ART_THEME_AND_ROLE_GUIDE.md` → Creep Role Guide
- `CREEP_GRAPHICS_ART_TRACKING.md`

Scope:

- Runner: low sharp dart/needle core.
- Brute: wide armored pressure core.
- Swarm: 3-5 readable clustered shardlings.
- Shade: echo/broken outline that reads without transparency alone.
- Siege: directional ram/cannon/wedge pressure body distinct from Brute.

Acceptance:

- All five creeps remain mapped in `CreepVisualLibrary`.
- Normal, heavy-pressure, reduced-effects, and grayscale captures keep role readability.
- Creeps do not hide tower targets, leak gate, or path cells.

### 3. `art-ui-role-icons`

Goal: make build/send cards feel intentional by adding simple role glyphs.

Source sections:

- `ART_THEME_AND_ROLE_GUIDE.md` → Build/Send Icon Direction
- `GRAPHICS_2000_BASELINE_ROADMAP.md` → Stage 5

Scope:

- 5 tower icons.
- 5 creep/send icons.
- Icon import targets under `Assets/Art/UI/Icons`.
- Keep icons simple enough to read on mobile cards.

Acceptance:

- Icons match role silhouettes from tower/creep work.
- Build/send menus remain compact and legible.
- No third-line microcopy returns to cards.

### 4. `art-mobile-control-rail`

Goal: resolve the remaining right-side control density noted in the latest screenshot review.

Source evidence:

- `docs/screenshot-reviews/art-2000-readability-pass/review.md`

Scope:

- Give Play/Pause, Reset, and Lane selector a dedicated layout rule.
- Prevent combat beams from visually crossing important control labels where possible.
- Keep touch targets usable without covering placement-critical cells.

Acceptance:

- Default, active-combat, and heavy-pressure captures show controls without crowding.
- Lane selector remains easy to read.
- Reset is available but visually secondary.

### 5. `art-combat-vfx-role-pass`

Goal: move from generic combat cues to role-specific combat language.

Source sections:

- `ART_THEME_AND_ROLE_GUIDE.md` → Global Shape Rules
- `GRAPHICS_2000_BASELINE_ROADMAP.md` → Stage 4

Scope:

- Arrow: crisp bolt.
- Control: field/fork/containment cue.
- Relay: signal ping/link cue.
- Pulse: short radial shockwave.
- Prism: thin focus beam.
- Shade: shimmer/reveal/resist cue.
- Siege: warning/impact cue.

Acceptance:

- Heavy-pressure screenshots remain readable.
- Reduced-effects mode still communicates critical events.
- VFX does not obscure grid cells or UI controls.

## Suggested Parallel Split

Safe parallel split:

1. Agent A: `art-tower-silhouette-polish`
2. Agent B: `art-creep-silhouette-polish`
3. Agent C: `art-ui-role-icons`
4. Main/integration agent: `art-mobile-control-rail` and final screenshot gate

Do `art-combat-vfx-role-pass` after tower/creep silhouettes settle, because VFX should attach to the final role shapes.

## Required Validation For Every Package

Run or request:

```text
Line Wards/Review/Capture Visual Review Set
```

For batchmode:

```text
/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity \
  -projectPath /Users/admin/LTW/unity/LTW.UnityClient \
  -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureVisualReviewSet \
  -logFile /Users/admin/LTW/unity-visual-capture.log \
  -ltwExitAfterCapture \
  -ltwCaptureGrayscale \
  -ltwCaptureOutputDir /Users/admin/LTW/docs/screenshot-reviews/<branch-name>/captures
```

Minimum evidence:

- default HUD;
- build menu open;
- send menu open;
- lane selector open;
- active combat;
- heavy pressure;
- reduced-effects heavy;
- results or late match;
- grayscale copies for value/silhouette checks.

## Integration Rule

Prefer small complete sets over isolated polish. A branch should improve a coherent gameplay read, not one beautiful object surrounded by confusing placeholders.
