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

- [ ] Add `com.unity.render-pipelines.universal` to the manifest
- [ ] Create a URP asset and renderer, assign in Graphics and Quality settings
- [ ] Confirm the project still compiles and the capture harness still runs

### Phase 2 — materials and shaders

- [ ] Run Unity's Built-in to URP material converter
- [ ] Rewrite `LTWBoardVertexColor` for URP, preserving vertex-colour albedo and the
      `BoardMeshBuilder.ToRenderSpace` linear decode
- [ ] Port `LTWContactShadow`
- [ ] Update the 16 `Shader.Find("Standard")` sites
- [ ] Verify the tower and creep body materials keep base colour, metallic-smoothness and
      emission bindings

### Phase 3 — lighting and palette re-tune

- [ ] Re-check the three-point rig; URP light intensity units differ from Built-in
- [ ] Re-check `BoardSurfaceLift`, which was tuned against Built-in's response
- [ ] Configure mobile light and shadow budgets on the URP asset, replacing the
      `pixelLightCount` workaround in Quality settings
- [ ] Enable tonemapping and bloom, the reason for the migration

### Phase 4 — verification and decision

- [ ] Full capture set, compared frame by frame against the baseline
- [ ] `dotnet test LTW.sln` still 77 passing
- [ ] Zero compile and shader errors
- [ ] Report to the user with side-by-side frames; **merge only on approval**

## Acceptance criteria

The migration is worth merging only if all of these hold:

1. Every capture state renders without missing shaders, wrong colours or lost geometry.
2. The board reads at least as well as the baseline, with the dark palette intact.
3. Tower emission reads **better** than the baseline. This is the entire point; if bloom
   does not deliver visibly more than Built-in did, the migration has not paid for itself.
4. No regression in creep scale, alignment, health bars, contact shadows or role colour,
   all of which were fixed on 2026-07-26.

## Rollback

`git checkout main` and delete the branch. Nothing else is required, since no main-line
commit depends on the migration.

## Log

Append an entry per working session: what changed, what broke, what is outstanding.

- **2026-07-26** — Document created, baseline captured, branch opened. No engine changes yet.
