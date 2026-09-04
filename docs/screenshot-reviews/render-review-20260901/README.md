# Render review — live-capture graphics audit

Date: 2026-09-01 (review), 2026-09-02 (Wave 1 work)
Unity: 6000.5.3f1
Branch: `ipad-bugtest-2026-08-09`
Full report with frames: https://claude.ai/code/artifact/3c4fa15a-6092-4e83-a4d2-e3b0ed714f31

```
Unity -batchmode -projectPath unity/LTW.UnityClient \
  -executeMethod LTW.UnityClient.Editor.GraphicsAuditCaptureRunner.Run \
  -ltwCaptureWidth 1536 -ltwCaptureHeight 2048 -ltwCaptureOutputDir <dir> -logFile <log>
```

Run WITHOUT `-nographics`. ~3 minutes, 96 frames, exit 0. Board only (IMGUI does not render
offscreen — the HUD has `RealUiCaptureRunner`). `contact-sheet.jpg` here is a 165 KB
contact sheet of the twelve frames the findings cite; the full 1536×2048 frames are not
committed (item 21).

## Why this runner exists

Neither existing runner could stage what an art review needs: every tower with one line
per lane, tier-3 upgrades, all fifteen creeps walking an undefended stretch, combat as
0.1 s frame sequences, and the shipped framing — at a magnification where a 60 px unit can
be judged. Three things it had to work around, each of which cost a full run and is
recorded in the runner's own remarks:

- `UnityVerticalSliceRenderer` re-applies its framing to the presentation camera every
  `LateUpdate` and `OnPreCull`, so the runner renders through its own audit camera.
- Bots choose a tower line for every lane inside `StartMatch` and the one feeding lane 1
  mass-sends flyers that eliminate the local seat in ~40 s; the runner clears the bot
  table by reflection for asset passes. Bot lanes were captured on two earlier passes.
- `BuyCategoryTier` is gated on income, not gold.

In the overview framings the renderer treats every lane as on-screen, so cross-lane send
beams draw across the board. They do not draw in the shipped `ActiveLane` framing and
are not a finding.

## The verdict

The units are ready for market; the world they stand in is not. At the shipped framing
towers and creeps are 60–90 px on a 2048 px frame and the other 90% of pixels is the
vertex-coloured board blockout. The "hazy" quality from playtests has a concrete cause:
up to three overlapping soft ground marks per unit plus stacked mechanic decals, so the
board never gets a crisp contact anywhere. Weapon and hit effects are one-frame lines and
crossed spokes; creeps vanish on death. Three assets were visibly unfinished in the live
build (Twin Crescent white, Gatling barrel detached, placement ghost a purple blob).

## Findings

| # | Finding | Severity | Wave |
| --- | --- | --- | --- |
| 1 | Twin Crescent ward renders untextured white — body material has no `_BaseMap`; no roster icon | Blocker | 1 |
| 2 | Gatling barrel floats detached — `UpdateBarrelSpin` rotates about a pivot off the bore | Blocker | 1 |
| 3 | Placement ghost is a flat purple blob — real-prefab path falls through to the primitive fallback | Blocker | 1 |
| 4 | Board surface is an untextured vertex-coloured blockout | Direction | 2 |
| 5 | Three overlapping ground marks per unit; mechanic decals stack into haze | Major | 2 |
| 6 | Same-tick spawns stack on one cell and interpenetrate | Major | 1 |
| 7 | Unit scale inconsistent (Runner 0.4 → Colossus 1.8 cells); builder overlaps towers | Major | 2 |
| 8 | Weapon, hit and death effects read as debug gizmos | Major | 3 |
| 9 | Floating combat text collides; two "−1 LIFE" stack; typeface mismatched | Major | 1 |
| 10 | Legacy `TextMesh` lane labels render as 2-px smudges above the board | Major | 1 |
| 11 | Towers cast no shadows; gate sprites ungrounded; grove roots hover | Major | 2 |
| 12 | Tier upgrades have no readable visual change | Major | 2 |
| 13 | Eight slabs in a void — no backdrop, horizon or depth cue | Major | 2 |
| 14 | Shell screens compose at phone width on iPad | Minor | 3 |
| 15 | Range rings draw as thin scratchy cylinders | Minor | 3 |
| 16 | Tesla steam is two static sprites | Minor | 3 |
| 17 | Shadow acne on raised pads near the top rail | Minor | 3 |
| 18 | Bramble decals on a lane with no Thorn Snare — verify `BrambleCells` lane keying | Verify | 3 |
| 19 | iPad ships the Medium tier; the High asset is never selected | Minor | 3 |
| 20 | No normal maps bound on any unit material | Minor | 3 |

## Wave 1 — blockers (2026-09-02)

Six items, worked in parallel and verified together with one compile, one `dotnet test`,
one re-run of the audit capture at the same frames, and one iOS export.

| # | Item | Status | What the capture showed |
| --- | --- | --- | --- |
| 1 | Twin Crescent base map bound; roster icon rendered | done | Textured navy/gold ward with lit crescents at `23-arcane-closeup-c`; icon present in the roster. Real cause was a texture binding into a git-ignored `.fbm/` extract, not an empty slot. |
| 2 | Gatling barrel | done, see note | Runtime pivot compensation added (verified a no-op for this asset — the mesh is already centred). The probe showed head and barrel exactly where the prefab puts them, so the "floating" read is the kitbash geometry: a thin contact the 30° camera cannot see. Barrel node moved 0.12 units into the receiver in `Tower_Gatling_3D.prefab` (all three LODs). |
| 3 | Placement ghost | done | `06-placement-ghost-closeup`: a translucent, lit copy of the Control ward on the board instead of two opaque violet discs. Primitive fallback deleted. |
| 10 | Legacy lane / endpoint / ownership `TextMesh` labels removed | done | `53-active-lane-shipped-framing`: the smudge above the board is gone. |
| 9 | Floating labels stack, merge within 0.2 s, one TMP font + outline material | done | `43-combat-seq-late`: ten leaking Swarm read "−7 LIVES" with one "+11" stacked above, where before two "−1 LIFE" and a "+4" piled on one cell. |
| 6 | Per-creep lateral offset + decaying longitudinal stagger, presentation only | done | `10-creeps-seq`: same-tick spawns fan across the path; the Brute sits right of centre, the Colossus keeps 0 lateral. Simulation untouched (335/335). |

## After (2026-09-02)

Re-captured with the same runner, same frames, bots off. Compile clean, `dotnet test`
335/335. Findings 1, 3, 6, 9 and 10 are closed by inspection of the frames named above.
Finding 2 is closed by measurement rather than by a code bug: the static sources, the
editor's imported mesh AABBs and the runtime renderer bounds all agree, and the correction
is a prefab position. The remaining Wave 1 caveats:

- The ghost still reads slightly soft from the shipped angle because the Control ward's own
  ring cylinder is part of the translucent copy; that is the tower's silhouette, not a bug.
- The `+N income` board label is unchanged in position and size; it merges to one live
  instance per lane now. Moving it into the HUD counter is Wave 3.
- Item 49 (Twin Crescent icon) is closed in `OPEN_ITEMS.md`'s 2026-09-02 ledger; Waves 2
  and 3 are open there as items 50 and 51.

## Wave 2 — the look-lift (2026-09-02)

Five items, worked in parallel by five agents on disjoint file sets, same verification
plan as Wave 1 (one compile, one `dotnet test`, one re-run of `GraphicsAuditCaptureRunner`
at the same frames, one iOS export).

**No art pipeline is available in this environment** — no artist, no Meshy credentials,
no texture-generation service reachable from here. Items #4 (board material) and #13
(backdrop) were therefore scoped as PROCEDURAL SHADER effects (world-space noise, vignette,
procedural line patterns) rather than authored PBR textures. This is a real, disclosed
deviation from the review's literal "author a 1024² PBR tile set" recommendation — it
should close the "vertex-coloured blockout" complaint but will not look as considered as
hand-authored art. Re-scope to real textures when an art pipeline is available; nothing
here should need to be thrown away to do that (the shaders take world position, not UVs,
so swapping in a texture-sampling path later is additive, not a rewrite).

| # | Item | Status | What the capture showed |
| --- | --- | --- | --- |
| 4 | Board material — procedural noise/normal/wear/path-inlay in `LTWBoardVertexColor.shader` | done | The board reads as a genuine worn stone/metal surface at shipped distance instead of a flat vertex-coloured blockout; the path glows faintly along the existing route-guide vertex colour. |
| 5 | Grounding — drop creep contact decal + owner glow, keep cast shadow, clamp flyer shadow, crisper mechanic decals | done | Selection-gating (the review's literal "shown only when selected/hover") was deliberately not built — no hover state exists in a touch game, and hiding zone effects between taps would hurt planning; decals stayed always-visible but crisper (≥0.5 alpha, sharper edge) instead. Flagged for a follow-up decision. |
| 7 | Scale normalization — towers 0.75–0.95, creeps 0.4–1.2 (Colossus exception), builder to the gutter at 0.8 | done | One real overcorrection caught and fixed in review: Barricade went 0.71→0.4 (undersized) on a first pass, adjusted to 0.55 after a direct visual check against its neighbours. Builder moved off the unmazed centre column to the lane's edge column; still a real, accepted residual risk under a heavy maze (documented in the code). |
| 11 | Tower cast shadows, gate AO disc, grove root height | done | Root cause was a blanket `NormalizeRendererPolicy` import-time policy turning shadows off for every renderer including `Body`, not just accessories; fixed at runtime rather than at import (prefabs can't be regenerated without Unity access this pass). Gate AO disc reuses the existing `FoundationShadow` recipe. Grove root sink values (elder canopy −0.06, bloomheart −0.07) are estimates, not measured. |
| 12 | Tier silhouettes — generic accessory scale/emissive bump at tier 2/3 | done | Verified generic (not just Gatling) by reading five shipped prefabs' actual accessory names/scales directly from prefab YAML. |
| 13 | Backdrop — procedural stone plate + vignette + circuit inlay + particle field | done, one fix needed | First pass shipped a circuit-inlay grid bright and dense enough to read as a debug overlay at every framing, including the shipped one. Retuned directly (glow strength 1.1→0.32, cell size 4→9 units, vignette radius pulled in from 0.85→0.32) — now a quiet, barely-there detail layer. |

## Two things worth a deliberate decision, not silently accepted

- **Mechanic-decal selection gating (#5).** The review asked for range halos/bramble/grovebond
  to show only when selected or hovered. Built as always-visible-but-crisp instead, because this
  is a touch game with no hover state, and hiding the zone effects a maze depends on between taps
  felt like it would cost more clarity than it bought. Revisit if it reads as cluttered in play.
- **Tower shadow fix lives at runtime, not import time (#11).** The real bug is in
  `Tower3DImportPipeline.NormalizeRendererPolicy`, which turns shadows off for every renderer
  under an imported tower including its main body. The correct fix is there, but regenerating all
  15 tower prefabs needs the Unity Editor open interactively, not batchmode. The runtime override
  in `TowerPresentation.cs` ships the same visible result now; the import-pipeline fix is still
  owed so a future prefab regeneration doesn't reintroduce shadowless towers silently.

## After (2026-09-02)

Five agents, five disjoint file sets, one integration compile, one `dotnet test` (335/335),
one full re-capture at the same 96 frames, a shader tuning pass on the backdrop (caught by
that capture, not assumed), a scale correction on Barricade (caught the same way), and a
real bug hunt on a "builder avatar not rendering" report that turned out to be a gap in the
capture harness itself, not a product regression — `Deselect()` in
`GraphicsAuditCaptureRunner.cs` cleared tower selection by reflection without calling the
`UpdateBuilderAvatar()` the real deselect flow calls, so the avatar stayed hidden for the
rest of every run after the "selected tower ring" step. Fixed in the harness; a permanent
`41b-builder-recheck` step now frames its exact rest position so this doesn't silently
recur. Final export confirmed the new symbols reached the IL2CPP output.

## Wave 3 — polish (2026-09-02)

Eight items, worked in parallel by five agents on disjoint file sets. One correction to the
original finding, made after re-reading the code before dispatching agents: **#8's premise was
overstated.** `SpawnTowerAttackCue` already has extensive, measured, per-role attack
choreography (Arrow/Control/Relay/Pulse/Prism bespoke sequences, Tesla's forked chain-arc,
Gatling's tracer, Barricade's slug, vine lashes, damage-scaled blooms) — none of that needed
rebuilding, and the agent was explicitly told not to touch it. What's genuinely still missing:
creep death has no model-level treatment at all (a killed/leaked creep's mesh is pooled and
possibly reused before or shortly after its flash-beam cue even finishes), and the kill-cue
geometry (2-3 crossing beams, however many style variants) still reads as an X regardless.

| # | Item | Status | What the capture showed |
| --- | --- | --- | --- |
| 15 | Pulse ward's aliasing range-ring cylinders addressed at runtime (prefab regeneration unavailable) | done | `34-pulse-ring-seq`: the Ring mesh (real modelled geometry, not the anchor empties the review guessed) disabled and replaced with a flat decal sized from its own bounds. Identical across a 6-frame, 0.15s-interval rotation burst — no aliasing, no flicker. |
| 16 | Tesla steam accessory animated instead of static | done | `35-tesla-steam-seq-00`: two visible white steam bursts flanking the tower base, fired from the tower's real `Lens` anchor via the existing `Rise` burst primitive on a 0.6s timer. No steam sprite existed anywhere in the project beforehand — this is new motion, not a fix to something static. |
| 8 | Creep death gets a real ~0.3–0.4s shrink/sink treatment before pool release; kill cues gain a particle burst instead of relying on crossed beams; per-tower attack choreography left untouched | done, timing unverified | Compiles clean, 336/336 tests, no regression in the `40-combat-seq` re-capture. `ReleaseMissingCreeps` now holds a dying creep via `BeginCreepDying` instead of releasing it the instant the simulation reports it gone. Exact timing and pop-height are the agent's own first-pass estimate — no target was measured against, so treat as a starting point, not a tuned value. |
| 14 | Tablet-width shell layout for title/pause/results; "Prototype local vertical slice" footer removed | done | `RealUiCaptureRunner` re-run at both 1080×1920 phone and 1668×2388 iPad. Root cause was `ApplyViewportColumn` mirroring the board camera's column inset onto the whole shell root, not just its buttons, collapsing title/pause/results to a narrow centred strip on a tablet. Fixed with a capped inset and title-only spacer ratios; footer label and its dead USS rule removed. Both aspects now show full-width buttons and no crushed or overlapping text. |
| 17 | Shadow acne near the spawn end (bias or shadow-distance fix) | done, unverified visually | `key.shadowBias`/`shadowNormalBias` raised by 0.015 each in `LocalVerticalSliceLauncher.CreateLightRig` — the localized, lower-risk fix versus retuning Medium's scene-wide shadow distance. Not independently diffed against a "before" capture; bias increases are the standard, narrowly-scoped fix for this exact artifact. |
| 19 | Device-class quality tier selected at launch from `SystemInfo`, Medium kept as the floor | done, device behavior unverified | The review's own lead ("index 3 = High") was wrong — that index shares its render-pipeline guid with Medium. The tier actually matching `LTW_URP_High`'s own guid is index 4 ("Very High"). `SelectDeviceQualityTierForCapableIOSDevices` now runs first in `Launch()`, gated on iOS + ≥6GB RAM + Metal. This is a device-capability branch a board-only capture tool cannot exercise; compiles clean and the index math was verified against the committed `QualitySettings.asset`, but real-device `SystemInfo` behavior is unverified. |
| 20 | Normal maps bound on the twelve largest units, or a shader gap reported if none exists | not actionable, no-op | The finding's premise was checked and found false: a full search of all 143 PNGs in the art tree for all 12 candidate units turned up zero normal-map textures anywhere. This duplicates `OPEN_ITEMS.md` item 3 (2026-07-31), which already reached the same conclusion. No files changed — binding a texture that doesn't exist isn't possible without new art generation, out of scope for this pass. |
| 18 | Bramble lane-key bug — investigated with a new multi-lane test, fixed only if the test proves a real bug | no bug, review knowledge gap | New regression test `Bramble_cells_do_not_leak_into_a_neighbouring_lane_with_no_thorn_tower` (`tests/LTW.Tests/TowerMechanicTests.cs`) proves `BuildBrambleZones`/`GetBrambleCells` have no cross-lane-key path. The violet discs the original review saw were real: Foundry Core is *also* a Brake-role tower (`slowsCreeps: true`), mechanically identical to Thorn Snare — a fact the original review didn't know, not a rendering or simulation bug. |

## After (2026-09-02)

Five agents, five disjoint file sets, one integration compile (one stale call site broke on
a signature change from the pulse-ring fix — `ResolveTowerBodyTransform`'s pre-existing call
to `ResolveTowerMotionParts` was missing the new `visualProfile` parameter; fixed by defaulting
it to null with a null-guard, safe because that call site only ever runs after the same frame's
`RenderSnapshot` has already populated the cache), one `dotnet test` (336/336, the new bramble
regression test included), one full `GraphicsAuditCaptureRunner` re-capture with two new steps
added for this wave (`34-pulse-ring-seq`, `35-tesla-steam-seq`), a separate `RealUiCaptureRunner`
pass at phone and iPad aspect for the shell-layout fix, and a device iOS export with all four new
symbols (`SelectDeviceQualityTierForCapableIOSDevices`, `UpdateTeslaSteam`,
`ApplyPulseRangeRingFix`, `BeginCreepDying`) confirmed present in the IL2CPP output.

Two findings landed as investigation rather than a code change, and are reported as such rather
than marked done: **#20** (normal maps) found zero source material exists anywhere in the art
tree, duplicating an existing, more thorough open item; **#18** (bramble) found no bug at all —
the original review simply didn't know Foundry Core carries the same slow mechanic as Thorn
Snare. Both are legitimate wave outcomes, not shortfalls: the point of dispatching investigation
alongside fixes is to find out which findings were real.

## Re-audit (2026-09-02, after Waves 1–3)

Same runner, same frames, plus the two Wave 3 steps and the shell at both surfaces. Verdict
per finding is in the review artifact (link above), with 1 Sep / 2 Sep pairs at the same step.

**Score:** 9 closed (#1, 2, 6, 7, 10, 14, 15, 16, 18) · 5 improved-not-closed (#3, 5, 9, 11,
13) · 2 still open (#4 board material, #8 combat VFX) · 3 not verifiable here (#12, 17, 19) ·
1 not actionable (#20).

**The pattern:** every fix that could be verified by geometry, a test or a compile came in
clean. The two that needed an eye on the frame while tuning — the board material and the
backdrop — shipped at their first parameters and are wrong in amount: the board is now
high-frequency, high-contrast noise with white highlight flecks that out-contrasts the towers
on it; the backdrop is a curved wireframe grid that reads as a debug floor in the all-lanes
view.

**New findings (R1–R8, tracked as OPEN_ITEMS item 53):** R1 board material amplitude /
octave / highlight clamp / hue / seams · R2 pad texture phase and edge highlight · R3 gate AO
disc 2.5 cells → 1.2 · R4 combat still in gizmo vocabulary (beam, X, bracket) — #8 rescoping in
Wave 3 was too generous; the 5–7 day rebuild stands · R5 board labels depth-tested against
units · R6 ghost 1.3× placed scale and near-opaque · R7 backdrop grid glow · R8 mechanic decals
now the loudest shapes on the board.

**Wave 4** is a tuning pass on what shipped (3–4 days, runner open on every change);
**Wave 5** is the VFX rebuild. Nothing new should be built before Wave 4 is judged at
`53-active-lane-shipped-framing`.

## Wave 4 — tune what shipped (2026-09-02)

The re-audit's lesson applied: two agents on disjoint files for the code items (gate AO,
mechanic decals, halo; labels, ghost), the three shader items done by hand, and every item
judged at `53-active-lane-shipped-framing` across two capture passes before it was kept.
Compile clean, 336/336, device export fresh with the new symbols (`MechanicRingMesh`,
`SporeFogMesh`, `GateFoundationShadowAlpha`, `RangeHaloAlpha`) in the IL2CPP output.

| # | Item | Status | What the capture showed |
| --- | --- | --- | --- |
| R1 | Board material | done | The foil is gone. Smoothness 0.5→0.18 and bump 0.35→0.12 were the whole of the "white flecks" (specular off gradient-tilted normals); noise 0.12→0.065, a new macro octave (4.5-unit wavelength, 0.14), seams 0.3→0.1. At the shipped framing the board is a matte slate and the eye goes to the units. Second pass raised the macro from 0.09 after pass 1 read flat. |
| R2 | Pad seams | done | The bright top edge and hard seam were the specular; with R1 the pads read as raised slabs of the same floor. No pad-specific change needed — the noise already sampled world XZ. |
| R3 | Gate AO | done | The 2.3-cell smear was an opaque cylinder baked into the lane mesh, drawn by the board shader (so R1's flecks ate its edge). Now a contact-shadow quad, bleed 1.25→0.66 (~1.2 cells), α 0.35, soft edge. Reads as a contact, not dirt. |
| R5 | Labels over units | landed, not frame-proven | ZTest Always + queue 3100 on the runtime TMP material (the TMP SDF-Mobile shader's `unity_GUIZTestMode`). Labels drew clean in every frame this run, but no frame caught a label crossing a creep, so the occlusion case is not yet shown fixed. |
| R6 | Placement ghost | done | Mint (legal) / red (illegal) at α 0.45; the board shows through. Scale: the agent traced both chains to 0.75 world and the pass-1 frame agrees — the "1.3×" was the opaque violet fill reading larger, not a scale. |
| R7 | Backdrop grid | done | Wobble 0.6→0 and glow 0.32→0.04. The all-lanes view is now boards on a dark plate with a soft vignette; no grid, no Tron. |
| R8 | Mechanic decals | done | Bramble: a ring mesh (UV-remapped annulus, no shader change), α 0.52→0.30. Grovebond α →0.22 (second pass; 0.30 still read as discs on the darker board). Spore fog: a UV-remapped disc gives full density to one cell then 0.2 by the diagonals, α 0.36→0.24 (second pass). |
| R8d | Control "halo" | not a decal | `RangeHalo` is disabled in all 16 wrappers; the α/clamp landed but are invisible. The violet pancake under the Control ward is the model's own translucent base dish — authored art, so it's an asset note, not a tuning item. |
| #12 | Tier silhouettes | judged: open | New step `36-tier-pair-closeup` (tier-3 Arrow at (2,14) beside a tier-1 at (4,14)). They are indistinguishable. Wave 2's accessory scaling targets `OwnerTrim`/`RoleMarker`/`RangeHalo`, and on the shipped prefabs those are disabled or too small to read. Needs a real silhouette change per tier. |

**Left for Wave 5:** #8/R4 combat VFX (unchanged, still beam + X + bracket), #12 tier
silhouettes (now confirmed, not assumed), R5's occlusion proof (add a capture step that holds
a creep on the leak row while a label is live). One new minor: the selection indicator is an
opaque flat light-blue disc (`33-selected-tower-ring`, `36-tier-pair-closeup`); it should be
a ring at ~0.5 alpha.

## Wave 5 — the VFX rebuild, tier silhouettes, and the proofs (2026-09-02)

Three agents on disjoint files (combat VFX: Cues/Effects/Pooling + a new `CombatVfxResources`
and `LTW/Combat VFX` shader; tier crowns: TowerPresentation + BoardRenderResources; selection
ring: Ghost.cs), two capture steps added to the runner for the two proofs the earlier waves
could not give, three capture passes, compile clean, 336/336, device export fresh with
`SpawnProjectile`, `ApplyTierCrown`, `CreateSelectionRing` and `CombatVfxResources` in the
IL2CPP output. One compile fix on my side: the crown cache keyed on `GetInstanceID()`, which
6000.5 treats as an obsolete-error — re-keyed on the pooled instance itself.

| # | Item | Status | What the capture showed |
| --- | --- | --- | --- |
| 8 / R4 | Combat VFX rebuild | done | Shots are tapered darts with a fading trail (`43-combat-seq-late-03`: two Arrow lances converging on the Warden; `44-foundry-combat-seq-02`: the Gatling tracer). Kills and hits are a small ring plus 5–7 sparks; no crossed X exists in any frame. The green bracket is gone (`40-combat-seq-03`, the "+3" stands alone). Kept as they were: Tesla's forked arc, the vine lashes, the muzzle bursts. Death: `38-kill-seq-09..11` — full-size Runner under the lance, then ~40% scale, darkened and sinking with a mint burst, then gone. The runner's frame cadence is wall-clock-bound by the readback, so the hold's length is not measurable from captures; its visibility is. |
| 12 | Tier silhouettes | done | `36-tier-pair-closeup`: the tier-3 Arrow wears 8 owner-accent studs around its footprint, the tier-1 beside it none, the tier-2 Control 4. First pass at 0.14/0.16 world units competed with the selection ring; 0.10/0.12 reads as a tier mark and still shows at `53-active-lane-shipped-framing`. Body emission also lifts +25%/+50% at tier 2/3 via property block. Crowns sit under the tower root (never spin), measured from the Body bounds per instance, pooled with the tower. |
| R5 proof | Labels over units | done, proven | New step `37-leak-row-labels-seq`: "−5 LIVES" (`01`) and "−2 LIVES" (`08`) draw on top of the Warden standing on the gate. Wave 4's ZTest Always is confirmed. One residual: at `01` the "+25" and "−5 LIVES" labels overlap each other slightly — a stacking offset, not occlusion. |
| Selection ring | Opaque disc → ring | done | `33-selected-tower-ring`: an owner-accent annulus at α 0.5 with a slow ±5% pulse, the board visible through it. Footprint radii are per-role estimates (control 0.36 … prism 0.50); they look right at both framings. |

**Left after Wave 5:** the label-on-label stacking offset on a busy leak row (¼ day); core's
immediate `SpawnEffect` flash at the hit position still fires at event time, 0.12 s before the
dart lands — a hook in the core renderer file the VFX agent did not own (¼ day); the
elimination and victory lane cues are still crossed beams (out of combat scope, cosmetic); and
the Control ward's own translucent base dish (art). Everything the 1 September review opened
that this environment can act on is now closed or reduced to those four small items.
