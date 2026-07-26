# URP Migration

Tracking document for moving Line Wards from the Built-in Render Pipeline to the Universal
Render Pipeline. Started 2026-07-26.

Read alongside [the graphics quality plan](GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md), which
records why this is being done: Tier 2 items 6 and 7.

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

Two measured deltas against the baseline, both expected from a pipeline change:

- **Board is darker.** Mean board luminance 0.130 under URP against 0.214 on the baseline.
  `BoardSurfaceLift` and the light rig were both tuned against Built-in's response.
- **Board occupies less width.** Resolved. `MobileViewportLayout` falls back to `Screen`
  when no capture viewport override is set, and in batch mode that is a small landscape
  surface, so `ConfigureDefaultCamera` letterboxed the presentation camera to roughly 42%
  of the target width. The Built-in path tolerated it; a scriptable pipeline honours the
  viewport rect. The capture now describes its own surface for the whole session, since the
  rect is set during `LateUpdate` on play frames rather than at readback. Board width went
  from 373 px to 871 px against the baseline's 976 px, and mean board luminance from 0.144
  to 0.209 against the baseline's 0.214: the letterbox bars were also what made the board
  look darker.

  Ruled out along the way, by measurement rather than assumption: camera aspect, which URP
  derives from the destination texture and which `UrpCaptureDiagnostic` confirmed against a
  deliberately non-square target; orthographic size, logged as identical; and pipeline
  render scale, which is 1.

### Phase 3 — lighting and palette re-tune

Two measured deltas against the baseline, both expected from a pipeline change:

- **Board is darker.** Mean board luminance 0.130 under URP against 0.214 on the baseline.
  `BoardSurfaceLift` and the light rig were both tuned against Built-in's response.
- **Board occupies less width.** Roughly 380 px against 730 px for the board itself. Not
  yet explained, and the obvious candidates have been ruled out by measurement:

  - `UrpCaptureDiagnostic` proves URP derives the projection from the destination render
    texture and ignores a pinned `Camera.aspect`. With a deliberately non-square 128x256
    target, the rendered size matched the RT-aspect prediction exactly, 64 px, against 24 px
    for the screen-aspect prediction. The capture target is 1080x1920, so the projection it
    receives is already correct.
  - Camera aspect and orthographic size were logged during capture as 0.5625 and 9.2, which
    is the framing the board is expected to fill about two thirds of the width at.
  - The pipeline asset uses render scale 1 and MSAA 1, so no resolution scaling is involved.

  Vertical framing is correct: the board fills the expected share of the height. Only the
  horizontal extent is short, by close to a factor of two. The next thing to test is whether
  `SetCameraFraming` resolves to a different orthographic size during the URP run than it
  does on `main`, which would mean the difference is in game state rather than rendering.


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

### Phase 4 — verification and decision

- [x] Full capture set, in [screenshot-reviews/urp-migration/after/](screenshot-reviews/urp-migration/after/)
- [x] `dotnet test LTW.sln` still 77 passing
- [x] Zero compile and shader errors
- [x] Reported and merged on approval

## Acceptance criteria

The migration is worth merging only if all of these hold:

1. Every capture state renders without missing shaders, wrong colours or lost geometry.
2. The board reads at least as well as the baseline, with the dark palette intact.
3. Tower emission reads **better** than the baseline. This is the entire point; if bloom
   does not deliver visibly more than Built-in did, the migration has not paid for itself.
4. No regression in creep scale, alignment, health bars, contact shadows or role colour,
   all of which were fixed on 2026-07-26.

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

- **2026-07-26** — Document created, baseline captured, branch opened. No engine changes yet.
- **2026-07-26** — Phase 1: URP 17.5.0 resolved from the editor's bundled packages, since the
  public registry only publishes legacy versions. Pipeline asset and renderer created under
  `Assets/Settings` and assigned as the default pipeline. Compiles clean, simulation tests
  still 77 passing. Materials are not converted yet, so the game is expected to render
  mostly magenta until Phase 2.
- **2026-07-26** — Phases 2 and 3 complete: materials and shaders converted, capture viewport
  letterbox fixed, tonemapping and bloom enabled, mobile budgets moved onto the URP asset.
  Board back to parity with the baseline and emissive detail now reads. Awaiting review.
- **2026-07-26** — Phase 1b: repaired the capture harness for scriptable pipelines. Three
  separate faults, each of which produced a blank frame that could have been misread as
  URP destroying the rendering. Captures now show the genuine intermediate state: board
  magenta from unconverted materials, sprites and HUD rendering correctly.
