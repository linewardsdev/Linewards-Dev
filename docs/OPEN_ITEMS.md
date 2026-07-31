# Open Items

Known gaps and decisions that are open right now. Each item is a thing someone still has
to do or decide; when an item's work lands, delete the item rather than marking it done —
the history is in git and in `GD_TUNING_LOG.md`.

**This file was retired on 2026-07-30 and reopened on 2026-07-31.** It was retired
legitimately: the code-review batch it was carrying (items 5–24, the 2026-07-29 two-pass
review) was worked to completion, and every one of those items is verified fixed —
`RemoveCreep` now fires on lane transfer, `CreepPresentationSnapshot` carries `MaxHealth`,
the destructive visual-library generators are gone, `TowerRolePalette` covers all 15
roles, `BotPlacementCandidates` is deleted, and the bot pressure filter now excludes
`HasLeaked`. It is reopened because the graphics uplift review of 2026-07-31 produced a
new set of open items, and this is the project's tracker for those.

Items 1–4 are carried forward from the retired version, updated against what was verified
on 2026-07-31. Items 5–16 were new that day; most are now resolved and deleted, see the
ledger below. Items 18–20 were opened by the work that resolved them.

**Re-verified 2026-07-31** against the working tree after `4bb48d2` (art-doc archive),
`151df11` (this file committed) and `bff79e3` (code comments recited by name). Items 5–15
and 17 all still hold as written. Item 16 was substantially resolved by the archive commit
and has been rewritten to show what remains versus what closed. Item 3 gained a finding
that settles decision 17.3. Nothing else changed.

**Worked 2026-07-31 (later the same day).** Items 5, 6, 7, 8, 12, 13, 14 and 16 are
resolved and deleted per this file's own rule. Item 15's two flag-level sub-items are
resolved; its two initiative-sized ones remain. Numbers are NOT reused and the remaining
items are NOT renumbered, because item numbers are cited from source comments — the
ledger below keeps a deleted number resolvable for anyone following one of those.

### Resolved 2026-07-31

| Item | Commit | Outcome |
| --- | --- | --- |
| 5 | `24ff842` | Post-processing sub-assets persisted; contents checked at load and in CI. The profile had shipped with three null overrides since `746403b`, so the game rendered with no tonemapper and no bloom for the entire life of the URP migration. |
| 6 | `277c02c` | Premise disproved by measurement. The ORM red channel is exactly 0.000 in every pixel of all 21 packed maps — there is no AO to recover, and binding it would have multiplied every model's ambient by zero. Extraction now runs when there IS occlusion, and reports when there is not. |
| 7 | `19650e6` | 30 tower and creep bodies retuned to smoothness 0.45. Neither cluster's value was the target: 1.0 is wet plastic, 0.12 is dead matte. Ten towers given per-role emission above the bloom threshold; all fifteen creeps raised off a multiplier that made blooming arithmetically impossible. |
| 8 | `b9392db` | Soft shadows, HDR grading, SMAA, and reflections matched to the scene's own ambient. Also deleted a second, hollow copy of the post-processing profile that nothing referenced. |
| 12 | `a1e6386` | Silent-skip fixed, exit codes added, and `audit_intake_scores.py` reads the scorecards back. All 11 `pass` results in the repo turned out to have skipped the normal-map check entirely. |
| 13 | `54de8f1` | Craft axis folded into the cycle doc **and** the report generator, so it appears in generated reports rather than needing to be remembered. |
| 14 | `76deab8` | Coverage report regenerated from the visual libraries and validated by script; both capture states verified as already working. Wave 0.8 re-baseline captured. |
| 16 | `892b64b` | Dangerous editor version struck from 3 runnable commands across 2 archived docs; roster claim and capture-state numbering corrected. |

**The plan that sequences this work is [`GRAPHICS_AA_UPLIFT.md`](GRAPHICS_AA_UPLIFT.md).**
That document holds the wave ordering, the raised quality target, the craft scorecard
extension, and the reference research. This file holds the discrete open items and does
not repeat the plan. Where an item names a wave, the wave is defined there.

---

## 1. `_EMISSION` keyword loss on tower body materials — self-healing, root cause still unknown

The tower body materials repeatedly lost their `_EMISSION` shader keyword and went
silently non-emissive (at least 4 times across two sessions), from a trigger that was
never isolated — it recurred from a plain compile-only Editor pass with no
material-touching code involved, and from ordinary batchmode capture runs.

**Guarded, not root-caused.** `Assets/Editor/TowerEmissionKeywordGuard.cs` runs on every
asset import pass and re-enables `_EMISSION` plus resets `globalIlluminationFlags` on any
material found wrong. Verified by deliberately stripping the keyword on Control's body
material and confirming the guard corrected it in the same batchmode pass.

**Coverage is now complete** — `TowerBodyMaterialPaths` lists all 15 tower body materials
(verified 2026-07-31). The original guard covered only the first 5, which left the 10
newer towers exposed; that gap is closed.

Still open: the actual trigger. If tower emission ever looks wrong despite the guard,
check the Editor log for repeated `TowerEmissionKeywordGuard` corrections firing every
session — that would mean something is fighting it faster than expected, and is the
signal to root-cause this rather than lean on the guard indefinitely.

## 2. Bloom cost on a physical Android device is unmeasured

Bloom and tonemapping were tuned and verified in the Editor and via headless capture only.
Nobody has profiled post-processing cost on a real Android device. Deferred deliberately —
no device access during that work.

**Now more urgent than when this was written, for an unexpected reason.** Per item 5, the
post-processing profile is empty in the committed build, so nothing has been paying bloom's
cost. The first build that actually restores post-processing is also the first build where
this cost appears at all. Profile on device as part of Wave 0's re-baseline, not later.

## 3. No asset has a normal map, and none has ambient occlusion

Meshy was never asked to generate normal maps, so nothing in the game has one.

**Verified 2026-07-31, and worse than previously recorded.** The original item said "all
ten" against the 5+5 roster. Actual current state:

- Normal maps on LTW assets: **0 of 30**. Every LTW material has `_BumpMap` set to
  `m_Texture: {fileID: 0}`.
- Occlusion maps bound: **0**.
- For contrast, `Assets/ThirdParty/StylizedWeaponKit` has **20 of 20** materials with
  normal maps. That is the visible quality delta, sitting in the same project.

The intake scorecard reports this (`has_normal_map` in `ai_asset_intake.py`) but does not
block on it — see item 12. Two routes, still neither pursued:

- Re-run Meshy generation with normal maps requested (costs generation credits).
- Bake from a high-poly or height-derived source in Blender. Note that a *synthetic*
  albedo-derived bake was previously considered and rejected as a silent visual tradeoff;
  if it is revisited, make that call deliberately and look at the result.

**Finding that favours the first route (2026-07-31).** Exactly one normal map exists
anywhere in the art tree:
`Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d_Assets/selected.fbm/NormalGL_*.png`.
It sits in an FBX embedded-media cache — a `.fbm/` folder, which `.gitignore` excludes — so
it is neither tracked nor bound to any material, and the "0 bound" figures above stand.

Its significance is that **Meshy has already produced a usable normal map for one of our
own assets.** The route was never blocked or unavailable; it simply was not asked for on
subsequent generations, and the one that did arrive was dropped by an intake path that
ignores `.fbm/` contents.

Checked whether this is recoverable at scale: **it is not.** Of 11 `.fbm/` caches in the
art tree, only the arrow tower's contains a normal map. So unlike the AO in item 6, there
is no free win hiding here — but the regeneration route is now *proven* rather than
assumed, which is what decision 17.3 was waiting on.

See also item 6 — AO does not need generating at all, only un-discarding.

## 4. Leg rigs on shell-bodied creeps may be invisible from the game camera

Building a two-segment (thigh+shin) leg rig for the Brute revealed that its armour shell
overhangs to the ground on every side, fully hiding the legs from the actual top-down game
camera — confirmed by rendering from that exact camera angle, where zero leg geometry is
visible at any frame of the walk cycle. The rig itself works and is kept, but produces no
visible gameplay difference for this creature.

Follow-up (2026-07-28): the Spire Turret Walker checked out fully visible, unlike the
Brute — but the check surfaced two different defects instead (51x foot skate, and a
symmetric trot whose mirrored contact poses are indistinguishable head-on). It has its own
rig script now, `tools/art_pipeline/rig_turret_walker.py`.

**Standing rule:** if `rig_quadruped_creep.py` is reused for another quadruped-shaped
creep, render-check from the actual game camera angle *first*, before investing in leg
articulation. For a low, wide, or heavily-armoured silhouette the legs may again be fully
occluded, in which case body/head motion is the only lever that will read.

---

# Graphics uplift items (opened 2026-07-31)

Findings from the holistic graphics review. Full context, sequencing and the raised
quality target are in [`GRAPHICS_AA_UPLIFT.md`](GRAPHICS_AA_UPLIFT.md).

## 9. There is no VFX system at all

Zero `ParticleSystem` instances in any prefab, scene or script. Also zero `TrailRenderer`,
zero `LineRenderer`, zero VFX Graph assets, zero decal projectors.

Every effect in the game is a pooled Unity primitive with a flat colour: impacts are
spheres, tower beams are stretched cubes, muzzle flash is two crossed cubes, the repair
tether is another stretched cube. `VFX_AND_ANIMATION_TARGETS.md` names 14 VFX prefabs that
should exist; **none of them do** — the design work is complete and the authoring has never
started.

Probably the largest single contributor to perceived production value after item 5.
Wave 2.1–2.2.

## 10. No font asset, and the HUD structurally cannot be animated

Two separate problems that have to be solved together.

- **There is not a single `.ttf`, `.otf` or SDF asset in the project.** The game renders
  in Unity's default IMGUI skin font, on 100% of frames. `BRANDING_GUIDE.md` describes
  typography in prose but names no typeface. This is one of the fastest "unfinished" tells
  in a mobile game.
- **The entire HUD is IMGUI** — `OnGUI` across 9 files, `GUI.skin`-derived styles, with no
  uGUI, no UI Toolkit, no TextMeshPro and no tween library. IMGUI is immediate-mode: there
  is no retained object to animate, so *every* UI motion item — scale pops, easing,
  transitions, state tweens — is blocked until the HUD is migrated.

**This is the largest hidden cost in the uplift and no existing doc acknowledges it.**
Decide the target technology before committing to any UI polish date. Wave 3.1–3.2.

## 11. Seven creeps have no animation, and the eight that do have one clip

- **No Animator at all:** revenant, runner, serpent, shade, siege, swarm, wisp. These are
  rigid meshes sliding along the lane.
- **The other eight have exactly one state, `Walk`.** No idle, attack, hit reaction, death
  or spawn anywhere in the roster.
- Creep death is instantaneous — the unit pops out of existence.
- Tower motion is entirely procedural and is genuinely well done; this item is not about
  towers.

Wave 2.3–2.5.

## 15. Performance debt that will land before ship

Not urgent for look, but it will constrain what the uplift can afford. **The two flag-level
sub-items are resolved in `b22aefd`; the two below are what remain, and neither is a flag.**

- **Quality tiers are decorative.** All six have `customRenderPipeline: {fileID: 0}`, so
  every tier resolves to the same URP asset. There is effectively one quality level, and it
  is identical on a flagship and a budget phone. Fixing this means authoring a URP asset per
  tier and deciding what each tier gives up — a look decision, not a settings edit.
- **Three of the four custom shaders use built-in-pipeline `UnityCG.cginc`** and are not
  SRP-Batcher compatible, so the 30 contact-shadow quads each break batching.
  `LTWContactShadow`, `LTWSporeFog` and `LTWFillBar`; `LTWBoardVertexColor` is already
  clean. Each is a rewrite to URP HLSL with a visual re-verification, since these three
  shaders are exactly the ones with no texture to compare against — a regression in them
  looks like a lighting change rather than a broken shader.
- **Still no LOD groups and no decimation stage.** Every unit is ~15,000 triangles at LOD0
  forever; a 40-creep swarm is ~600k triangles. `m_EnableLODCrossFade` has been turned off
  to stop paying for a transition that cannot happen, and `RenderSetupValidation` now fails
  if the flag and the project disagree in either direction — so adding LOD groups later will
  be told to switch it back on rather than silently getting pops.

Resolved in `b22aefd`: `m_EnableLODCrossFade` was on with zero LODGroup components in the
project, compiling the `LOD_FADE_CROSSFADE` variant of every shader for nothing; and
`gpuSkinning: 0` had the eight skinned creeps deforming on CPU on a mobile target.

Wave 4.

## 17. Decisions the owner still needs to make

Listed here so they do not sit invisibly inside the plan doc:

1. **Confirm the raised quality target** in `GRAPHICS_AA_UPLIFT.md` §3, which replaces the
   retired "2000 polished prototype" bar. It is a real scope increase, not a reframing, and
   everything else follows from it.
2. **HUD technology** (item 10) — uGUI + TextMeshPro is conventional and lower-risk;
   UI Toolkit is more modern but a larger migration. All UI motion waits on this.
3. **Normal map route** (item 3) — Meshy re-generation versus a Blender bake stage.
4. **Whether to re-source albedo.** The reference research says simple albedo plus authored
   roughness is what produces the target look; Meshy's generated albedo is the opposite.
   This is the most expensive item implied by the plan and the one that most determines
   whether the result reads as on-reference or merely improved.
## 18. Eleven committed metallic/smoothness maps cannot be regenerated from the repo

Re-running `repack_metallic_smoothness.py` rewrites 11 of the 21 committed
`Baked_MetallicSmoothness.png` files with up to 0.46 per-pixel difference in metallic and
0.34 in smoothness. Not non-determinism — a repeat run is byte-identical.

Cause: those 11 date from `07240c3`, when their source maps were 4096x4096 and were
box-averaged down to 1024. `c18b3bc` later replaced the sources with 1024 versions. So the
committed textures derive from data no longer in the repo, and the repo cannot regenerate
its own artefacts.

Which version is better is genuinely arguable — averaging 16 texels is not obviously worse
than whatever resample produced the current 1024 source — so this is a decision, not a
cleanup. What is not arguable is that re-running the pipeline silently changes 11 tracked
art textures, which will keep surfacing as mystery churn in unrelated commits.

Affects: brute, runner, shade, siege, swarm, arrow, control (x2), prism, pulse, relay.

## 19. Eight of fifteen creeps have no usable emissive detail

Sharper than the count in the old item 7, and measured from the maps rather than the
materials:

- **Five have no `Baked_Emit.png` at all**: burrower, colossus, stalker, warden, zephyr.
- **Three have one that is functionally blank** — 0.00% of texture above quarter
  brightness: obsidianbrute (peak 0.224), revenant (0.047), shade (0.259).

The five without maps are left with black emission deliberately: emission with no map
multiplies against 1 and would light the entire body uniformly, a lantern rather than a
highlight. `CreepBodyMaterialTuning.ValidateTuning` reports them by name on every run.

This is art generation, not a material fix — it needs emission maps authored or
regenerated. Pairs with item 3, since both are "the generator was never asked for this map".

## 20. The target-reference gate measures ten roles against a retired era, and twenty against nothing

Split out of item 14, which is otherwise resolved, because this part is a decision rather
than a repair.

`MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md` scores a target-reference match against the
production references in `art-pipeline/v1-role-coverage-report.md`. Those references are 2D
painted plates from the paint-then-model era. Only the original five towers and five creeps
ever had one; the twenty added later were generated directly as 3D.

So the gate measures ten roles against artwork their own 3D models superseded, and cannot
score the other twenty at all. `validate_role_coverage.py --strict` exits 2 and names them.

Two ways out, and the coverage report deliberately does not pick one:

- Retire the target-reference score for identity work and replace it with the craft axis,
  which measures the built asset rather than its distance from a plate.
- Promote a current capture per role as its own reference, re-baselined when the asset
  changes, so the target reflects the 3D era.

Sequence before Wave 1: the promotion gate is what every other art item is checked by.

