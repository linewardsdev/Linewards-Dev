# URP Migration

**Status: merged to `main` at `0c3792d` on 2026-07-26.** The project now renders on URP
17.5.0. Two follow-ups remain open — see Known follow-up.

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
under URP. That was nearly reported as a regression requiring a rollback. It was not: the
actual match capture measured correctly all along (0.2113 vs 0.2142, 0% blown), and the
contact sheet's own lighting rig is what runs hot under URP, not the game. See Known
follow-up. Lesson: judge acceptance criteria from the game, not from a review tool, even a
supposedly-fixed one.

## Known follow-up

**The role contact sheet renders too hot under URP.** It builds its own scene with a close
camera and no match lighting context, and the same three-point rig that looks correct in a
match blows the models out there. Adding the game's post-processing volume to that scene did
not resolve it, so the cause is the synthetic setup rather than tonemapping.

This does not affect the game. The match captures measure at 0.2113 mean luminance against
the baseline's 0.2142 with no blown pixels. But it does mean the contact sheet is currently
misleading for judging material and colour work, which is the exact failure it was repointed
at the real prefabs to avoid. Tune its lighting before trusting it again.

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
