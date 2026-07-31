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

**Worked 2026-07-31 (later the same day).** Items 5, 6, 7, 8, 9, 12, 13, 14 and 16 are
resolved and deleted per this file's own rule, and item 15 is fully resolved bar the two
parts noted in it. Numbers are NOT reused and the remaining items are NOT renumbered,
because item numbers are cited from source comments — the ledger below keeps a deleted
number resolvable for anyone following one of those.

Items 1, 4 and 11 are not resolved but are substantially narrowed by measurement rather
than left as written; each carries its own dated finding.

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
| 9 | `652249d` | VFX system built. Root cause was structural rather than neglect: `com.unity.modules.particlesystem` was not in the manifest, so `ParticleSystem` did not exist as a type — the fourteen named VFX prefabs were unbuildable, not unbuilt. One shared world-space emitter per shape rather than one pooled per burst, which measured 2,769 → 6,082 peak objects and was abandoned; the shipped design runs at 2,118, *below* the pre-VFX baseline. |
| 15 (part) | `b22aefd`, `9c504f3`, `1551cd5` | LOD cross-fade and GPU skinning fixed and guarded; two shaders moved to URP HLSL for SRP batching (LTWFillBar deliberately left on the GPU-instancing path, against the item's wording); three per-tier URP assets authored and assigned across all six quality levels. |

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

**Trigger narrowed to one code path, 2026-07-31.** The signal this item said to watch for
was checked across 73 Unity sessions in a single day, and it is firing — but not from where
this item assumed.

| Run type | Sessions | Corrected |
| --- | ---: | --- |
| Full capture set (`CaptureVisualReviewSet`) | 3 | **8 every time** |
| Role lineup capture (`CaptureRoleLineupReviewSet`) | 1 | **15** |
| Compile-only / validate-only, `-nographics` | 14 | 0 |
| Batch playtest, `-nographics` | 6 | 0 |
| Batch playtest, **with** graphics | 1 | 0 |

Three things follow, and the third contradicts this item as written:

- **The count is not arbitrary — it equals the number of tower prefabs the run
  instantiates.** 8 is the review defence line; 15 is the whole roster in the lineup
  capture. Whatever the mechanism, it is per-tower.
- **It always fires at shutdown, never mid-run** — line 1511 of 1598, 1576 of 1663, 1586
  of 1673, 983 of 1029. The guard is an asset post-processor, so it is catching damage the
  run itself did, on the final import.
- **It does NOT recur "from a plain compile-only Editor pass".** Zero corrections across
  fourteen compile and validate runs. It is also not ordinary Play Mode: six batch
  playtests corrected nothing, and neither did a playtest run WITH graphics, which rules
  out the graphics device as the discriminator.

So the trigger is specific to `VisualReviewCaptureRunner` and scales with tower count.
Ruled out by inspection: the runner's only two `sharedMaterial` writes are on backdrop
cubes it creates itself, not on tower materials.

Not yet isolated to a line. The remaining suspects are the reflection-driven scenario
setup (`SetPrivateField`/`SetPrivateBool`), `PrefabUtility.InstantiatePrefab` — which
unlike `Object.Instantiate` leaves the instance connected to the asset — and the
render-to-texture path. Whoever picks this up should start by running one capture with
the defence-line placement disabled and checking whether the count drops to zero.

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

**The rule now has a number, 2026-07-31.** Applied to the whole roster at once and measured
rather than judged per creep — see
[`screenshot-reviews/creep-leg-visibility/`](screenshot-reviews/creep-leg-visibility/).
The camera is orthographic at size 15.5 over a 1920px capture, so it resolves 61.9 pixels
per world unit, and every creep's on-screen height follows from its library scale.

**Below roughly 35px on-screen height, leg articulation moves 2–4 pixels and is not the
lever** — body, tilt and silhouette motion are, which is what `CreepMotionProfile` already
provides for all fifteen.

Occlusion still has to be checked per creep, exactly as above. Height caps how much a leg
COULD read; an overhanging shell takes it to zero regardless.

---

# Graphics uplift items (opened 2026-07-31)

Findings from the holistic graphics review. Full context, sequencing and the raised
quality target are in [`GRAPHICS_AA_UPLIFT.md`](GRAPHICS_AA_UPLIFT.md).

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

- **No Animator at all:** revenant, runner, serpent, shade, siege, swarm, wisp. Verified
  2026-07-31, the list is exactly right.
- **The other eight have exactly one state, `Walk`.** No idle, attack, hit reaction, death
  or spawn anywhere in the roster.
- Creep death is instantaneous — the unit pops out of existence.
- Tower motion is entirely procedural and is genuinely well done; this item is not about
  towers.

**Two corrections, 2026-07-31.**

*"Rigid meshes sliding along the lane" is not accurate.* All fifteen creeps carry per-creep
procedural motion through `CreepMotionProfile` — fifteen distinct rows of bob, sway, drift,
spin and pulse. What the seven lack is skeletal deformation, not motion. That matters for
sequencing, because the visible gap is smaller than the item implies.

*The effort is inverted relative to on-screen size.* Measured at the shipped camera (see
item 4 and [`screenshot-reviews/creep-leg-visibility/`](screenshot-reviews/creep-leg-visibility/)):
creeps that HAVE an animator average 46px tall, creeps with none average 80px. The five
smallest creeps in the game — stalker at 16px through warden at 33px — all have rigs, and a
leg on them swings 2–4 pixels. Three of the five largest have none.

Also note these seven are static Meshy exports with **no armature at all**, so each needs a
rig built and skinned before a clip can exist. That is the real cost of this item, and it is
per creep.

Suggested order, by return per rig rather than by the list above: **siege, serpent, runner**
first (105/95/84px, no animator); then revenant and shade; then swarm and wisp, which are
small and abstract enough that body motion probably reads better than legs regardless.

Wave 2.3–2.5.

## 15. Performance debt that will land before ship

**Four of the five sub-items are resolved (`b22aefd`, `9c504f3`, `1551cd5`). One remains,
and it is the expensive one.**

- **Still open: no LOD groups and no decimation stage.** Every unit is ~15,000 triangles at
  LOD0 forever; a 40-creep swarm is ~600k triangles, and the harness has measured frames
  with 266 creeps on camera. There is no decimation step anywhere in the Blender pipeline,
  so this needs one built before LOD groups can be authored — it is an art-pipeline
  initiative, not a settings change. Wave 4.

  `m_EnableLODCrossFade` has been turned off in the meantime so nothing pays for a
  transition that cannot happen, and `RenderSetupValidation` fails if the flag and the
  project disagree in either direction — so whoever adds LOD groups later will be told to
  switch it back on rather than silently getting pops.

Resolved:

- `m_EnableLODCrossFade` was on with zero LODGroup components in the project, compiling the
  `LOD_FADE_CROSSFADE` variant of every shader for nothing.
- `gpuSkinning: 0` had the eight skinned creeps deforming on CPU on a mobile target.
- Quality tiers all resolved to one URP asset. Three per-tier assets now exist and are
  assigned across all six levels, guarded by `QualityTierSetup.ValidateTierAssets`. Note the
  tiers were worse than "decorative": their own shadow, AA and light-count fields were
  carefully varied AND entirely dead, because URP ignores all of them.
- `LTWContactShadow` and `LTWSporeFog` moved to URP HLSL with a `UnityPerMaterial` CBUFFER,
  so the SRP Batcher can take them — contact shadows are the case that mattered, since there
  is one under every unit and each is a separate cloned material.

  **`LTWFillBar` was deliberately NOT converted, against this item's wording.** It is built
  for GPU instancing and driven by a `MaterialPropertyBlock` across the eight lane pressure
  meters, which share one material and differ only by fill — they instance into one draw
  call. Converting it would lose the instancing and gain nothing, because
  `MaterialPropertyBlock` disables SRP batching anyway. "Three of the four shaders" was
  accurate as a count and wrong as a prescription.

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

