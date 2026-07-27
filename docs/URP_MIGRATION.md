# URP Migration

**Status: merged to `main` at `0c3792d` on 2026-07-26.** The project now renders on URP
17.5.0, and the migration itself is verified sound (see Acceptance criteria). A significant
**pre-existing, non-URP bug** was found afterward while investigating a review-tool artifact:
all five towers rendered with an unconfigured auto-generated material instead of their tuned
production material. **Fixed and verified — see Known follow-up.** Bloom's cost on a physical
device is still unmeasured (no device access).

Tracking document for moving Line Wards from the Built-in Render Pipeline to the Universal
Render Pipeline. Started and completed 2026-07-26.

Read alongside [the graphics quality plan](GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md), which
records why this is being done: Tier 2 items 6 and 7, both now closed by this migration.

## Why

Tier 2 stalled on bloom. Emissive detail on the towers cannot read without it, and the
per-role emission colours added on 2026-07-26 are muted for the same reason.

The cheap route does not exist. The legacy `com.unity.postprocessing` package's most recent
release, 3.5.4, targets Unity 2019.4; it is unmaintained and not supported on the 6000.5
editor this project uses. Post-processing on a modern editor means URP.

Secondary gains: explicit control over mobile light and shadow budgets, which is where the
Android pixel-light defect came from, and Shader Graph for future effects.

## Migration surface

Measured on 2026-07-26, before any changes.

| Item | Count | Notes |
| --- | --- | --- |
| Project materials on Built-in Standard | 86 | Unity's converter handles most |
| Third-party materials on Built-in Standard | 20 | Stylized weapon kit |
| `Shader.Find("Standard")` call sites | 16 | Mechanical |
| `Shader.Find("Sprites/Default")` and `Unlit/*` | 9 | Mostly fine, verify each |
| Custom shaders we own | 2 | See below |

**The board shader is the real work.** `Assets/Resources/Shaders/LTWBoardVertexColor.shader`
is a Built-in surface shader (`#pragma surface`), a format URP does not support. It must be
rewritten as a URP-compatible shader or a Shader Graph. `LTWContactShadow.shader` is a plain
unlit alpha-blended shader and should port with far less effort.

**Partly anticipated already.** Seven editor scripts already resolve
`Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")`, so they prefer
URP when present and fall back today.

## Ground rules

- All work happens on the `urp-migration` branch. **Nothing merges to `main` without
  explicit approval.**
- `main` stays shippable throughout. If the migration is abandoned, deleting the branch
  costs nothing.
- Unity 6000.5.3f1 only. 6000.3.12f1 silently downgrades `ProjectSettings.asset`.
- The simulation stays untouched: no changes under `src/` or `tests/`, and
  `dotnet test LTW.sln` stays at 77 passing.

## Baseline

The pre-migration capture set lives in
[screenshot-reviews/urp-migration/baseline/](screenshot-reviews/urp-migration/baseline/),
taken from `main` immediately before branching. Every comparison is made against these
frames, not against memory or against the older Tier 1 captures.

Expect intermediate states to look worse. That is normal for a pipeline change and is not
by itself a reason to stop; the acceptance criteria below are what matter.

## Phases

### Phase 1 — install and configure

- [x] Add `com.unity.render-pipelines.universal` to the manifest — 17.5.0, the version bundled with the 6000.5.3f1 editor
- [x] Create a URP asset and renderer, assign in Graphics settings — scripted in `UrpMigrationSetup`; per-quality overrides left empty so all levels inherit the default
- [x] Confirm the project still compiles and the capture harness still runs — required repairing the harness, see Phase 1b

### Phase 1b — repair the capture harness

Assigning URP silently broke the verification tool, which is worth calling out because a
blank capture reads as catastrophic damage when nothing is actually wrong.

- [x] Render through `RenderPipeline.SubmitRenderRequest` under a scriptable pipeline;
      `Camera.Render()` is a Built-in call and does nothing under URP. Both paths are kept
      so captures stay comparable while `main` is Built-in and the branch is URP.
- [x] Re-establish the active render target before readback. A scriptable pipeline binds
      its own targets and does not restore the caller's, so `ReadPixels` sampled the wrong
      surface.
- [x] Skip the GPU HUD overlay pass under a scriptable pipeline. It adds a second camera
      with a Depth-only clear, which preserves colour on Built-in but clears the target
      under URP, wiping the rendered board. `PaintBatchHudOverlay` already composites the
      same HUD on the CPU after readback.
- [x] Report rather than save when a render request is rejected, so the tool cannot fail
      quietly again.

`UrpCaptureDiagnostic` was added to isolate this: it renders one camera to a texture and
reports the readback mean, which is what proved the render path was working and narrowed
the fault to the overlay pass.

### Phase 2 — materials and shaders

- [x] Convert materials — 99 converted, 0 left unconverted, via `UrpMaterialConverter`
- [x] Rewrite `LTWBoardVertexColor` for URP — hand-written HLSL forward pass using
      `UniversalFragmentPBR`; shadow, depth and depth-normals passes borrowed from URP's Lit
      via `UsePass` rather than reimplemented
- [x] Port `LTWContactShadow` — needed only the `RenderPipeline` tag
- [x] Update the 16 `Shader.Find("Standard")` sites — routed through a new `RenderCompat`
      helper that resolves per active pipeline. These are why gate bars and lane chevrons stayed
      magenta after the asset conversion: they are built in code, so the converter never saw them
- [x] Verify the tower and creep body materials keep their bindings — no magenta remains in
      any capture state

### Phase 3 — lighting and palette re-tune

Two measured deltas against the baseline surfaced first, both resolved:

- **Board occupied less width.** 373 px against the baseline's 976 px in the same mid-board
  band. Cause: `MobileViewportLayout` falls back to `Screen` when no capture viewport
  override is set, and in batch mode that is a small landscape surface, so
  `ConfigureDefaultCamera` letterboxed the presentation camera to roughly 42% of the target
  width. Built-in tolerated it; a scriptable pipeline honours the viewport rect. Fixed by
  having the capture describe its own surface for the whole session (the rect is set during
  `LateUpdate` on play frames, not at readback). Board width recovered to 871 px.

  Ruled out along the way, by measurement rather than assumption: camera aspect, which URP
  derives from the destination render texture and which `UrpCaptureDiagnostic` confirmed
  against a deliberately non-square target; orthographic size, logged as identical at 9.2;
  and pipeline render scale, which is 1.

- **Board looked darker.** 0.130–0.144 mean luminance under URP against the baseline's
  0.214. This tracked the same letterboxing: once the viewport fix landed, luminance
  recovered to 0.209 with no changes to `BoardSurfaceLift` or the light rig. The letterbox
  bars had been dragging the average down, not the pipeline's response.

- [x] Re-check the three-point rig — renders correctly under URP without intensity changes
- [x] Re-check `BoardSurfaceLift` — left at 0.35. Board luminance came back to 0.209 against
      the baseline's 0.214 once the letterboxed viewport was fixed, so no re-tune was needed
- [x] Configure mobile light and shadow budgets on the URP asset — MSAA 2x, 1024 main
      light shadowmap, four additional lights per object with shadows enabled. These are the
      controls the Android `pixelLightCount` workaround was standing in for
- [x] Enable tonemapping and bloom — `UrpPostProcessingSetup` writes a volume profile to
      Resources; the launcher attaches a global volume and enables post-processing on the
      presentation camera. Neutral tonemapping rather than ACES, which would shift the palette
      warm and undo the board and role colour work

### Phase 3d — emission keyword loss

Found during Phase 4 review: every 3D body material lost its `_EMISSION` keyword during
the shader swap in Phase 2, even where the emission map and colour survived intact. Emission
was effectively off project-wide, so the Phase 3 bloom work had almost nothing to act on and
the migration would have looked far less complete than it was.

- [x] First repair pass re-enabled the keyword by auditing bound map + non-black colour
      rather than trusting the keyword state — but set
      `globalIlluminationFlags = EmissiveIsBlack`, which Built-in treats as "exclude from GI
      only" but URP's material validation reads as "emission is black" and strips the
      keyword right back out on the next import. The repair could not survive a reimport.
- [x] Second pass switched the flag to `MaterialGlobalIlluminationFlags.None`. Verified by
      repairing, forcing a full reimport, and confirming all ten body materials retained
      the keyword afterward.

### Phase 4 — verification and decision

- [x] Full capture set, in [screenshot-reviews/urp-migration/after/](screenshot-reviews/urp-migration/after/)
- [x] `dotnet test LTW.sln` still 77 passing
- [x] Zero compile and shader errors
- [x] Reported and merged on approval — merged to `main` at `0c3792d` on 2026-07-26

## Acceptance criteria

The migration is worth merging only if all of these hold. All four confirmed before merge:

1. **Every capture state renders without missing shaders, wrong colours or lost geometry.**
   Confirmed — 15/15 frames, 0 shader errors.
2. **The board reads at least as well as the baseline, with the dark palette intact.**
   Confirmed — 0.2113 mean luminance against the baseline's 0.2142, 0% blown pixels, on the
   actual match capture.
3. **Tower emission reads better than the baseline.** Confirmed once the Phase 3d keyword
   fix landed and bloom had real emission to act on. Towers and the gate/pressure bars
   visibly glow where the baseline was flat.
4. **No regression in creep scale, alignment, health bars, contact shadows or role colour.**
   Confirmed against the match capture.

**Caution on how criterion 4 was first judged.** The initial check used the role contact
sheet — a tool that builds its own synthetic scene — and it showed the models washed out
under URP. That was nearly reported as a regression requiring a rollback. It was not a URP
regression: the migration itself is sound, and the whole-frame match luminance (0.2113 vs
0.2142, 0% blown) is accurate as a migration-parity check. But the wash the contact sheet was
showing turned out to be real, not a review-tool artifact — see Known follow-up: all five
towers' visible mesh renders with an unconfigured auto-generated material, a pre-existing bug
unrelated to URP that the contact sheet happened to make visible and the frame-wide metric
was never going to catch. Lesson: a review tool showing something ugly is a lead worth
chasing to an actual root cause, not something to explain away once the obvious culprit (here,
URP) is cleared.

## Known follow-up

### Open: the `_EMISSION` keyword loss on the five tower body materials is not fully solved

This was previously believed fixed twice this session (switching `globalIlluminationFlags`
from `EmissiveIsBlack` to `None`, then re-enabling the keyword directly and confirming it
held across one reimport). While doing the item 1-7 follow-up work on 2026-07-26, the
keyword was found stripped again on all five `mat_tower_*_3d_body_runtime_v01.mat` files
after a plain `-quit -nographics` compile-only pass with no material-touching code involved
— the same class of loss, recurring from a trigger that has not been isolated. Re-enabled it
again and confirmed it now holds across one more compile pass, but given it has now silently
recurred twice despite the GI-flag fix supposedly addressing the root cause, **do not trust
this as permanently fixed.** Anyone touching these five materials should re-check
`grep _EMISSION` on them before relying on emission rendering, especially after any bare
Editor relaunch or reimport with no obvious cause. A real fix would likely need either a
`ShaderGraph`/`MaterialPostprocessor`-level hook that force-corrects this on every import
rather than a one-time manual poke, or a deeper root-cause of what specifically re-triggers
Unity's keyword sync — neither was pursued here since it was out of scope for the tower
material-assignment fix this section otherwise documents.

### Correction: the contact sheet wash-out was not emission intensity

An earlier version of this section concluded the wash was caused by tower/creep emission
intensity (authored at 2x, tuned for gameplay scale) being too strong at the contact sheet's
closer framing, and recommended retuning shipping emission values. **That conclusion was
wrong**, for two compounding reasons, both now fixed or corrected below:

1. The contact sheet's captures were non-deterministic (see the commit "Make role contact
   sheet captures deterministic with warm-up frames"): the same committed material state
   produced two different stable renders depending on process timing, and several of the
   "emission causes the wash" A/B comparisons were unknowingly comparing across that
   coin-flip rather than across the actual variable being changed.
2. Editing `mat_tower_*_3d_body_runtime_v01.mat` and re-rendering produced **byte-identical**
   output before and after the edit — a red flag that should have been chased immediately
   rather than read as "no effect." It led to the real finding below.

### The real root cause: all five towers render with an unconfigured, auto-generated FBX material

`Tower3DImportPipeline.GenerateWrapperIfRawExists` branches on `spec.PreserveSourceMaterials`:

```csharp
if (spec.PreserveSourceMaterials)
{
    NormalizeRendererPolicy(generatedInstance);   // tweaks blend/shadow settings only
}
else
{
    ApplyRuntimeMaterial(generatedInstance, recipe.BodyMaterial);   // assigns the tuned material
}
```

Every one of the five tower specs in `Tower3DProofSetGenerator.cs` passes
`preserveSourceMaterials: true`. `NormalizeRendererPolicy` never assigns
`mat_tower_*_3d_body_runtime_v01.mat` to anything — it only adjusts render queue and shadow
settings on whatever material is already on the renderer. That material is whatever Unity's
FBX importer auto-generated on import (named e.g. `Material_0.001`), because the source
FBX's material was never explicitly remapped to the production asset.

Confirmed by direct probe on the Control tower's actual body mesh renderer
(`Tower_Control_3D/Body/Imported3DVisual/LTW_Unity_ExportRoot/Mesh1.0`):

| Property | Auto-generated `Material_0.001` (what actually renders) | `mat_tower_control_3d_body_runtime_v01.mat` (what has been tuned all session, never assigned) |
| --- | --- | --- |
| `_BaseMap` | `Baked_BaseColor` (correct, auto-detected by the importer) | Same texture |
| `_MetallicGlossMap` | **not bound** | Bound, repacked, correct |
| `_Smoothness` | flat shader default, `0.5` | Tuned per intake |
| `_EmissionColor` | **`(1,1,1,1)`, full white, keyword on** | Tuned per role, e.g. Control `(1.216, 0.848, 2)` |

The full-white, uncalibrated emission is what actually washes the tower out — not the tuned
material's 2x intensity, which has never been rendered by any tower in this project.

**Everything this session (and prior sessions) did to `mat_tower_*_3d_body_runtime_v01.mat`
— the URP shader conversion, the emission keyword fix, the `EmissiveIsBlack` fix, the
metallic-smoothness repack, per-role emission tuning — has had zero visual effect on any
tower, because that asset was never assigned to a visible renderer.** The URP migration's
board/lighting/bloom work is unaffected by this (verified against the real match capture,
which does show towers glowing — that glow is coming from the auto material's own
uncalibrated white emission, not from the tuned one).

**Creeps do not have this bug.** `Creep3DImportPipeline.GenerateWrapper` calls
`ApplyMaterial(instance, bodyMaterial)` unconditionally — no preserve-source branch exists
for creeps. Confirmed by probe: the Brute creep's body renderer correctly uses
`mat_creep_brute_3d_body_v01`.

### Fixed: `preserveSourceMaterials` flipped to `false`, plus a second bug found during the fix

All five specs in `Tower3DProofSetGenerator.cs` now pass `preserveSourceMaterials: false`, so
`ApplyRuntimeMaterial` assigns the tuned, URP-converted `mat_tower_*_3d_body_runtime_v01.mat`
to every tower's body renderer.

The first regeneration attempt surfaced a **second, previously-latent bug**: regardless of
`preserveSourceMaterials`, `CreateMaterialRecipe` unconditionally called `CreateBodyMaterial`,
which re-ran `ConfigureBodyMaterial` on the body material **even when it already existed**.
Because `PreserveSourceAlpha` is `true` for all five specs, this took the transparent branch —
resetting the hand-tuned material back to generic recipe defaults (shared `BodyColor`
`(0.82, 0.95, 1)` instead of the tuned per-role color, near-zero emission `(0.02, 0.035, 0.05)`
instead of the tuned value e.g. Control's `(1.216, 0.848, 2)`, `Smoothness` `1` → `0.12`,
`Opaque` → `Transparent`, `ZWrite 1` → `0`). This silently destroyed two prior sessions' worth
of tuning ("Restore opaque PBR shading", "Give each tower role its own emission colour") the
moment the generator ran, independent of the assignment bug above. Caught before committing by
reviewing the regenerated `.mat` diffs, not by visual inspection — the resulting render looked
plausible ("richer than before") but was in fact neither the tuned material nor the original
auto-generated one.

Fixed in `Tower3DImportPipeline.CreateBodyMaterial`: only run `ConfigureBodyMaterial` when the
asset is newly created; return an existing material untouched. Reverted the clobbered `.mat`
files from git and regenerated again with the corrected pipeline — this time the body materials
show zero diff (tuned values preserved) while the prefabs correctly reference the tuned
material's guid.

Verified:
- Data level: `Tower_Control_3D.prefab` references `mat_tower_control_3d_body_runtime_v01.mat`'s
  guid (`be545f9603d7041e69253391dbd5c30e`) via a `PrefabInstance` material override.
- Visual: role contact sheet and a real match capture
  (`docs/screenshot-reviews/tower-material-fix/`) both show towers with genuine mesh/material
  detail (metallic sheen, tuned per-role color and emission) at gameplay scale, not the
  full-white auto-material glow.
- `dotnet test` 77/77 passing, 0 compile/shader errors.

Creeps were unaffected by either bug (confirmed earlier: no preserve-source branch, and no
equivalent unconditional-reconfigure call in `Creep3DImportPipeline`).

## Rollback

`git checkout main` and delete the branch. Nothing else is required, since no main-line
commit depends on the migration.

## Log

Append an entry per working session: what changed, what broke, what is outstanding.

- **2026-07-26** — Document created, baseline captured (15 frames, `screenshot-reviews/urp-migration/baseline/`), branch opened. No engine changes yet.
- **2026-07-26** — Phase 1: URP 17.5.0 resolved from the editor's bundled packages, since the
  public registry only publishes legacy versions. Pipeline asset and renderer created under
  `Assets/Settings` and assigned as the default pipeline. Compiles clean, simulation tests
  still 77 passing. Materials are not converted yet, so the game is expected to render
  mostly magenta until Phase 2.
- **2026-07-26** — Phase 1b: repaired the capture harness for scriptable pipelines. Three
  separate faults, each of which produced a blank frame that could have been misread as
  URP destroying the rendering. Captures now show the genuine intermediate state: board
  magenta from unconverted materials, sprites and HUD rendering correctly.
- **2026-07-26** — Phase 2: 99 materials converted with none left unconverted, board shader
  hand-rewritten as URP HLSL, contact shadow ported, all 16 runtime `Shader.Find("Standard")`
  sites routed through the new `RenderCompat` helper. No magenta remaining in any state.
- **2026-07-26** — Phase 3: viewport letterbox found and fixed (see above), which also
  resolved the board-darkness delta as a side effect. Tonemapping and bloom enabled via a
  new volume profile. Mobile light/shadow budget moved onto the URP asset (MSAA 2x, 1024
  shadowmap, 4 additional lights/object), replacing the old `pixelLightCount` workaround.
- **2026-07-26** — Phase 3d: emission keyword loss found and fixed in two passes (see above).
  This was found during the first merge review pass, using the role contact sheet, which
  nearly caused a false regression report — see Known follow-up and the acceptance-criteria
  caution above.
- **2026-07-26** — Phase 4: full capture set recaptured post-fix, match luminance confirmed
  against baseline, all four acceptance criteria confirmed. Merged to `main` at `0c3792d`.
  `urp-migration` branch kept, now 1 commit behind `main`; safe to delete once confidence
  builds, per Rollback.
- **2026-07-26** — Contact sheet follow-up, first pass: concluded the wash was emission
  intensity (2x, tuned for gameplay scale) being too strong at the contact sheet's closer
  framing. Eight other hypotheses tested and ruled out along the way. Fixed the contact
  sheet's own remaining Built-in-only `camera.Render()` calls as a genuine correctness fix,
  unrelated to the wash. Did not change shipping emission values, correctly deferring that as
  an art decision — but the underlying diagnosis was itself wrong; see the next two entries.
- **2026-07-26** — Contact sheet follow-up, second pass: while preparing an A/B emission
  comparison for the user, found the captures were non-deterministic — identical committed
  material state produced two different stable renders depending on process timing. Traced to
  the first two frames after building the scene rendering differently from every frame after.
  Fixed with two warm-up frames before every contact sheet capture; verified stable across
  repeated process launches. This invalidated several of the prior pass's A/B conclusions,
  which had been comparing across that coin-flip.
- **2026-07-26** — Contact sheet follow-up, third pass: with captures now deterministic, an
  emission edit to `mat_tower_control_3d_body_runtime_v01.mat` produced byte-identical render
  output, which should not be possible for a real material edit. Traced to the actual cause:
  all five tower specs pass `preserveSourceMaterials: true`, so
  `Tower3DImportPipeline.ApplyRuntimeMaterial` is never called for towers and their visible
  body mesh renders with Unity's auto-generated, unconfigured FBX import material instead —
  full-white uncalibrated emission, no metallic map, default flat smoothness. Every tower
  material fix made this session and in prior sessions has had zero visual effect, because
  none of it was ever assigned to a visible renderer. Creeps are unaffected (confirmed by
  probe; their import pipeline has no such branch). See Known follow-up for full detail and
  the two possible fixes. Not fixed this session — this needs a visual check nobody has done,
  since these towers have never rendered with their intended material.
- **2026-07-26** — Contact sheet follow-up, fourth pass: flipped `preserveSourceMaterials` to
  `false` and regenerated. First regeneration attempt clobbered the tuned body materials back
  to generic defaults (a second, independent bug in `CreateBodyMaterial` — see Known
  follow-up); caught via `.mat` diff review before committing, not visually. Fixed
  `CreateBodyMaterial` to leave an existing material untouched, reverted the clobbered assets,
  regenerated again. Verified clean at the data level (guid reference) and visually (contact
  sheet + real match capture, saved to `docs/screenshot-reviews/tower-material-fix/`). 77/77
  tests passing, 0 compile/shader errors. This closes the tower half of Tier 2 item 8 in the
  graphics plan.
