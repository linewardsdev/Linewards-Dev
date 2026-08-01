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
ledger below. Items 18–20 were opened by the work that resolved them. Items 21–28 are
from the 2026-08-01 code review (simulation, Unity client, CI, and repository mechanics);
items 29–32 are from the same day's live iOS-simulator playtest of the device build
(item 29 was withdrawn the same day as a reviewer misread — it is kept, marked, so the
claim is not chased; item 33 was split out of item 30 when that item was resolved).

**Worked 2026-08-01.** Items 22, 27, 30 and 31 are resolved and deleted per this file's own
rule, and item 23 is rewritten to show what closed versus what remains. Numbers are still not
reused. Items 24 and 25 touch `UnityVerticalSliceRenderer`, which was being actively
edited in the shared working tree that day — whoever picks them up should check for
in-flight client work first.

Item 30's resolution disproved its own root cause, so item 15's resolution note is corrected
in place rather than left contradicting the ledger, and item 33 carries out the MSAA finding
that item 30 had parked at the end of itself. Item 31 asked for two things and got one, so
item 34 carries out the defeat moment rather than letting the ledger imply it shipped.

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
| 15 (part) | `b22aefd`, `9c504f3`, `1551cd5` | LOD cross-fade and GPU skinning fixed and guarded; two shaders moved to URP HLSL for SRP batching (LTWFillBar deliberately left on the GPU-instancing path, against the item's wording — see the 2026-08-01 row for item 30, which kept that batching decision and fixed the pipeline the pass was written for); three per-tier URP assets authored and assigned across all six quality levels. |

### Resolved 2026-08-01

| Item | Commit | Outcome |
| --- | --- | --- |
| 22 | `264991c` | `SimulationPluginSyncTests` compares the committed Unity plugin against the source build — declared members always, IL when built Release, which is what CI does. Deliberately not a byte comparison: MVID, PE stamp and PDB id are build identity and differ between machines on an in-sync plugin (measured: 148 differing bytes in an otherwise identical 135,680). Verified by flipping one constant and watching the IL half fail while the member half correctly stayed green. Caught its own first real drift twice during the session that wrote it. |
| 27 | `264991c` | Eighteen `First`/`FirstOrDefault` catalog scans in per-tick bot and upgrade paths replaced with an id index, matching what `CombatContent` already did. |
| 32 (new) | `bd384c7` | The batch playtest's 180s wall-clock budget had quietly become too small rather than generous, so it failed intermittently — including on a clean tree, which cost an hour of bisecting changes that were not the cause. Not a regression: `MatchEscalationRules` deliberately closes matches by escalating sent-creep health, and its own sweep table records "8-lane close 4472" for the start tick it picked. Measured directly — all-bot matches are deterministic and seeds 1-5 each ended at exactly tick 4471. Budget raised to 420s, roughly 3x the slowest observed run. `Finish` also now LOGS its failure reason: it had recorded it only into the report written on success, so a timeout exited 1 with an empty log. |
| 23 (part) | `264991c` | Movement and healing no longer rebuild the creep array per creep, and `CombatState` no longer copies twice per mutation or copies the collection that did not change. The attack phase is still quadratic — see the rewritten item. |
| 31 | `667f58f` | **The income half was misdiagnosed in the item.** `HudView` was already reading the live snapshot value, not an authored constant — the live value simply does not change on elimination, because the simulation withholds the *payment* (a `Where` filter in `ApplyIncomeTick`) rather than zeroing the *number*. Measured: the eliminated seat's `Income` was exactly 10, the value every seat starts at, and its gold did not move across a full income interval. `Income` is deliberately kept — it is the economy the seat built and is what `MatchSummary` reports — so there was no live value meaning "what this seat earns" for a HUD to read. `PlayerEconomyState.EffectiveIncome` is now that value; its test measures the gold actually paid across an income tick and requires the two to agree, so the derived value cannot drift from the filter. The rest was client gating: the dock and palette now close and stop drawing their launchers, board taps are dropped, the builder avatar goes with them, and the stats bar reads `+0` with an `OUT` state and a SPECTATING strip — a strip rather than a modal, because the match continues without this seat. The teardown sits on the frame tick rather than in `OnGUI`, where it was first written: `OnGUI` does not run in batchmode, so `EliminatedSeatCheck` (a headless play-mode check in the shape of `SessionModalityCheck`) failed the first version with both panels still open. That check also forces both panels back open *after* elimination and requires them shut a frame later, because "closed once" is what a naive fix achieves and "cannot be open" is what this defect needed. Verified by looking at the capture: `real-11` shows `L0 G8396 +0 P112`, `OUT`, the spectator strip and no BUILD/MULTI/SEND; `real-12` shows both panels still gone one frame after being forced open behind the HUD's back. **Not resolved:** the defeat/results moment, split out as item 34. |
| 30 | `11524ec` | **The magenta was not the shader.** LTWFillBar really was a built-in-pipeline pass under a `UniversalPipeline` tag and is now URP HLSL, but it was not what shipped magenta: compiled explicitly for Metal/iOS, the *old* CGPROGRAM pass succeeds on all four variants it has (vertex and fragment × `INSTANCING_ON` on and off, 2080/2933/1512/2132 bytes of bytecode), so there was never a missing variant to fall back from. The real cause is that `UniversalRenderPipelineAsset.defaultMaterial` is wrapped in `#if UNITY_EDITOR` with a bare `return null` for players, so every `GameObject.CreatePrimitive` object in a build arrives with a working mesh, a working renderer and **no material** — and the creep health bars are exactly that, two `PrimitiveType.Cube` children from `EnsureChild`. That is why it looked correct in the Editor for the whole life of the URP migration. Measured on the device shots themselves: all magenta sits in the 38–60% x band where the creeps walk, in bars of exactly the two-piece back+fill silhouette `ConfigureCreepHealthBar` builds; the lane pressure meters live in the lane gutters and are not magenta in any of the four captures — they are not even in frame, so "and all eight pressure meters" was inference, not observation. `RenderCompat.CreatePrimitive` now backfills a material only when one is missing, so the Editor path is byte-for-byte unchanged and only the player is repaired; the five runtime primitive sites moved onto it (health bars and every pooled board primitive, the builder avatar, the tower selection rings, and the placement ghost — `BoardMeshBuilder.PrimitiveMesh` is left alone, since it destroys its probe before anything renders). Verified by play-mode capture at 1080x1920 with graphics enabled: 0 magenta pixels of 2,073,600 in both framings, health bars drawing gold-on-dark, and all eight gauges drawing per-lane red/amber fills off one shared material — which is also the proof the `MaterialPropertyBlock` instancing path survived the HLSL conversion. An isolated render through the converted shader returns exactly the property-block values (1,0,0) and (0,0,1) either side of the fill threshold. **Not verified on device:** neither half of this can reproduce in the Editor by construction, so the fix is argued from URP's own source and the shipped pixels, and wants a device re-test to close. |

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

## 10. The HUD structurally cannot be animated — the font half is now resolved

Was two problems. One is fixed; the other is untouched and is the expensive one.

**Resolved 2026-08-01 — the project now has a font asset and an SDF text stack.** The
board-text pass added `com.unity.ugui` (which is how TextMeshPro ships in Unity 6) and
imported TMP's essential resources, so LiberationSans SDF is a real asset in the project.
Board labels were rebuilt on it: SDF glyphs with a dark outline on one shared material,
rising and fading with a scale punch. The original wording — "not a single `.ttf`, `.otf`
or SDF asset in the project", "no TextMeshPro" — is no longer true.

Two things learned there that the HUD migration will hit as well, recorded so it does not
cost the same time twice:

- TMP renders **nothing at all, silently**, until its essential resources are imported.
  That import is normally a modal editor prompt, which a batch run never sees.
  `Assets/Editor/TmpEssentialsImporter.cs` does it non-interactively and is idempotent.
- An outline does not apply through `fontMaterial` keyword pokes, nor through TMP's
  per-component `outlineWidth`. Both compile, run, and produce flat glyphs with no error.
  Only a shared `Material` with `OUTLINE_ON` enabled, assigned via `fontSharedMaterial`,
  is honoured — and it batches, which the per-label routes do not.

**Still open: the entire HUD is IMGUI** — `OnGUI` across 9 files, `GUI.skin`-derived
styles. IMGUI is immediate-mode: there is no retained object to animate, so every UI motion
item — scale pops, easing, transitions, state tweens — remains blocked until the HUD is
migrated. The HUD also still draws in Unity's default IMGUI skin font, because that is a
property of IMGUI rather than of the missing asset; having the asset does not change it.

What has changed is the risk profile. The dependency decision is made, the package is in,
and the text stack is proven in-tree on real content, so the migration no longer has to
carry that question. **Decide the target technology (uGUI vs UI Toolkit) before committing
to any UI polish date.** Wave 3.1-3.2.

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

  **`LTWFillBar` was left on the GPU-instancing path, against this item's wording, and it
  still is.** It is driven by a `MaterialPropertyBlock` across the eight lane pressure meters,
  which share one material and differ only by fill; the SRP Batcher skips any renderer carrying
  a property block, and claims the draw from GPU instancing whenever it *can* take a shader, so
  the two paths are exclusive and instancing is the one that pays here. "Three of the four
  shaders" was accurate as a count and wrong as a prescription.

  **Corrected 2026-08-01 (`11524ec`).** What that note got wrong was not the batching choice but
  the assumption that batching was the only axis. The pass was still built-in-pipeline
  (`CGPROGRAM`, `UnityCG.cginc`, `fixed4`) under a `"RenderPipeline" = "UniversalPipeline"` tag,
  which is a defect independent of how it batches. It is now URP HLSL **with** the instancing
  buffer kept, so this sub-item is closed on both axes rather than traded off. See the
  2026-08-01 ledger row for item 30.

## 17. Decisions the owner still needs to make

Listed here so they do not sit invisibly inside the plan doc:

1. **Confirm the raised quality target** in `GRAPHICS_AA_UPLIFT.md` §3, which replaces the
   retired "2000 polished prototype" bar. It is a real scope increase, not a reframing, and
   everything else follows from it.
2. **HUD technology** (item 10) — uGUI + TextMeshPro is conventional and lower-risk;
   UI Toolkit is more modern but a larger migration. All UI motion waits on this.
   **Better informed as of 2026-08-01:** TextMeshPro is now already in the project and
   proven on real in-game content, because the board-text pass needed it. If the answer is
   uGUI + TMP, half the dependency work is done and the text stack is known to work; if the
   answer is UI Toolkit, TMP stays in regardless, since board labels are world-space and do
   not migrate with the HUD. That asymmetry did not exist when this decision was written and
   it lowers the cost of the conventional option specifically.
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

---

# Code and repo health items (opened 2026-08-01)

Findings from the 2026-08-01 code review (simulation, Unity client, CI, and repository
mechanics). Verified against a clean tree at `9ac06b9` with all 268 tests passing.
These deliberately do not repeat R1–R5 below, which still stand.

## 21. The git repository is 1.58 GiB and growing, with no LFS

The pack contains 25–54 MB binary blobs committed directly: Meshy FBX exports, 4K baked
textures, and intermediates under `Assets/Art/AIStaging/.../AIDrop/` and
`GeneratedAssets/`. Every clone and CI checkout pays this forever, and each art
regeneration adds another copy to history — the migration only gets more expensive with
delay.

Two decisions, then one mechanical task:

- Adopt Git LFS for `*.fbx` and art `*.png` (and whether to rewrite history or migrate
  from here forward).
- Decide whether AI staging intermediates (raw AIDrop contents, as opposed to selected
  production assets) belong in the repo at all.

## 23. O(N²) state copying in the combat tick — movement and healing fixed, the attack phase is not

**Substantially resolved in `264991c`; one phase remains.**

Resolved: movement rebuilt the whole creep array once per creep, so the phase every creep
passes through every tick cost N array rebuilds — roughly 70,000 element copies at the 266
creeps the capture harness has measured. It now builds one list and adopts it. Healing did
the same plus a linear scan per healed creep, and now indexes by entity id. `CombatState`
no longer copies twice per mutation (`Replace` copied, then the constructor copied again),
and no longer copies the collection that did not change — `RemoveCreep` was rebuilding the
tower array too.

**Still open: the attack phase.** `ApplyDamage` still calls `ReplaceCreep`, and
`RemoveCreep` again on a kill, once per damaging hit — so a board where every tower hits
every tick is still quadratic. Fixing it needs a mutable buffer threaded through all four
damage paths (direct fire, splash, chain, artillery), which is why it was left out of the
first pass rather than folded in: that change cannot be reviewed by inspection the way the
movement one can.

Note for whoever takes it: entity order and event order are load-bearing. `SelectTarget`,
Pulse's splash `Take(2)` and `ChainArc` all tie-break on entity id, and several tests
assert on event ordering.

## 24. The renderer re-does per-snapshot work at per-frame rate, with string keys

`UnityVerticalSliceRenderer.RenderSnapshot` runs every frame (~60 fps) against a snapshot
that changes at ~4 Hz. Per entity per frame it allocates `EntityId.Value.ToString()`,
concatenates `"t" + key` / `"c" + key` shadow keys, and re-applies colors, health bars
and overlays whose inputs only change on a new tick; `new int[LaneCount + 1]` is also
allocated each frame.

Two parts: key the entity dictionaries by `long` instead of `string` (removes most of the
per-frame garbage), and split the loop into per-frame work (transform interpolation,
hit-flash) versus per-snapshot work (everything else), gated on a snapshot tick number.

## 25. The two biggest client classes need splitting before the HUD migration lands

`UnityVerticalSliceRenderer` is 6,370 lines and owns board mesh baking, object pooling,
VFX, contact shadows, tower and creep motion, pressure meters, and camera configuration;
`TouchPlacementController` is 2,007. The client has no test framework, so these files are
where regressions hide. The seams are already visible: `BoardMeshBuilder` exists, the
pooling code is self-contained, and creep vs tower presentation barely interact.

Sequence this *before* the item-10 HUD migration, which will churn these same files.

## 26. Bot AI is embedded in the match bridge and hardcoded against sample content

About a third of `LocalVerticalSlice` is bot decision logic — `TryPlaceBotTower`,
`BestMazingPlacement`, `TryBuyBotTier`, `TryUpgradeBotTower`, pressure heuristics, and
three build-order arrays — despite `Bots/` and `BotController` existing for exactly this.
Worse, the build orders reference `SampleVerticalSliceContent.*TowerId` constants
directly, so bot behaviour is compiled against sample content inside the simulation
assembly — against the project's own "data drives balance" principle, and in tension with
`BotProfileDefinition` already living in content.

Move the decision logic into `Bots/` and express build orders as content data. This
serves R3 directly.

## 28. CI never compiles the Unity client — gate written, blocked on a licence secret

**Partly resolved 2026-08-01. The remaining half needs an owner action, not engineering.**

The item's "at minimum" clause turned out to be **already satisfied**: item 22's
`SimulationPluginSyncTests` runs under CI's `--configuration Release`, which is exactly the
configuration where its IL comparison is meaningful, so plugin drift is already caught on
every push.

The compile itself is now written: `docs/ci/unity-compile.yml` builds the client
with `game-ci/unity-builder`. A build rather than an `-executeMethod` that returns, because
editor scripts compile during import and runtime scripts during the build, so only a build
covers both halves — and the editor tooling is the half that keeps breaking.

**Blocked on two owner actions, neither of them engineering.**

1. **A push credential with GitHub's `workflow` scope.** The token this repo is pushed with
   does not have it, so any commit touching `.github/workflows/` is rejected outright — and
   that rejects the whole push, including unrelated work in the same ref. The workflow is
   therefore staged at [`ci/unity-compile.yml`](ci/unity-compile.yml) with the one-line
   `git mv` to activate it in [`ci/README.md`](ci/README.md).
2. **A Unity licence in the repository secrets.** It is an account credential and cannot live
   in the repo. The job is gated on `UNITY_LICENSE` existing, so with none configured it
   reports "not configured", skips, and cannot break the existing pipeline. Adding
   `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` turns it on with no further edit.

**Unverified, and unverifiable until then.** Without a licence the workflow cannot be run
even once, so the first licensed run is the real test of that file rather than a formality.

---

# Live device-build playtest items (opened 2026-08-01)

Findings from the first interactive play session of the actual iOS build — the R1/R2
pass, run on the iPhone 17 simulator. Full session notes and screenshots in
[`screenshot-reviews/ios-sim-live-playtest-20260801/`](screenshot-reviews/ios-sim-live-playtest-20260801/).

## 29. WITHDRAWN — "creeps are invisible in the device build" was a misread

**This item was wrong and is retained only so nobody chases it.** It originally claimed
the iOS build stripped the physics module and that no creep body rendered. Both halves
are false, disproved the same day by re-running the build paused and inspecting the lane
at native resolution (`ios-sim-live-playtest-20260801/04-paused-creeps-render-correctly.png`):
**creeps render correctly** — full mesh, texture, contact shadow, per-creep motion.

What was actually true, and what it turned out to mean:

- The `Can't add component because class 'BoxCollider' doesn't exist!` console spam is
  real, but **benign**. The physics assemblies *are* in the built app
  (`UnityEngine.PhysicsModule.dll` is listed in its `ScriptingAssemblies.json`); the
  native classes are dropped by engine code stripping (`stripEngineCode: 1`).
  `GameObject.CreatePrimitive` logs the failure and still returns a working
  mesh+renderer object, so nothing visual depends on it. Nothing in the client depends
  on colliders either: the only `Raycast` in runtime code is
  `TouchPlacementController:283`, which is `UnityEngine.Plane.Raycast` (pure math, not
  physics), and `UnityVerticalSliceRenderer.DestroyPrimitiveCollider` exists purely to
  *delete* the colliders primitives arrive with.
- So the only real cost is log noise — and a small free win: primitives are being created
  with colliders the code immediately destroys. Worth suppressing at the source rather
  than adding the physics module.
- The lives drain that prompted the original claim (220→0 in ~3 minutes) was ordinary
  play: a three-tower defence against seven sending bots, not blindness.

Lesson for future device reviews, since this is the second time the *reviewer* rather
than the code was the defect: a dark creep on a dark board next to a bright magenta
health bar reads as "artifact, no unit" at a glance. Pause the match and inspect at
native resolution before calling something invisible.

## 33. MSAA sample-count mismatch on Metal: attachments created with 4 samples, render passes asking for 1

Carried out of item 30, which noted it as "separately, and probably unrelated" — it is
separate, so it is kept rather than folded into that item's resolution. The device log
repeats three messages together:

```
RenderPass: Attachment 0 was created with 4 samples but 1 samples were requested
EndRenderPass: Not inside a Renderpass
NextSubPass: Not inside a Renderpass
```

The 4 is not arbitrary: `Assets/Settings/LTW_URP_High.asset` carries `m_MSAA: 4`, and it is
the only asset in the project that does (Medium and the base asset are 2, Low is 1). So the
first message is an attachment allocated at the High tier's sample count meeting a pass that
asked for one sample, and the two "not inside a Renderpass" lines are the native render-pass
sequence coming apart afterwards rather than three independent faults.

Not investigated beyond that, and **not** reproduced — like the item-30 defects it needs a
Metal player, not the Editor. Worth pairing with a device re-test rather than chased from
here. Note that `QualitySettings.asset`'s own `antiAliasing` fields are dead under URP
(item 15), so the sample count in play is always the URP asset's, whichever tier is active.

## 34. A defeated seat gets a spectator state but no defeat moment

Carried out of item 31, which asked for two things — "a defeat/results moment **and** a
spectator state for the rest of the match". The spectator state shipped in `667f58f`: the
seat's controls stand down, the board stays watchable, and a strip under the HUD says
`YOU ARE OUT • SPECTATING`. The defeat moment did not, and is kept as its own item rather
than folded into that resolution, because it is a design decision and not a defect.

What is missing is the beat where the player is *told they lost*, with their own numbers,
at the moment it happens. Today the transition is: the lane wipes, a `PLAYER 1 OUT`
floating text plays over it, and three buttons vanish. The strip is what stops that reading
as a crash; it is not a result.

The reason this cannot be borrowed from what already exists: `MatchSummary` and
`MatchEndedEvent` fire only when the whole match resolves to one survivor
(`EconomyService.TryCreateMatchSummary` returns null until `ActivePlayers.Count == 1`), and
`LocalSessionFlowOverlay` drives its results panel off `LatestMatchSummary`. A seat
eliminated at tick 900 of a 4,400-tick match has no summary to show and will not have one
for a long time. So this needs its own answer to three questions, none of which the
simulation currently has an opinion on:

1. What does a mid-match defeat screen say? Placement is not known yet — the seat is out,
   but whether it finished 8th or 3rd depends on a match that is still running.
2. Is it modal? `RuntimeUiChrome.ModalScreenActive` is the existing "a session screen owns
   the display" flag and would work, but taking the screen fights the spectator state that
   was just built, so at most it should be dismissible.
3. Does the player get an exit? There is a RESET in the live rail, but "leave this match"
   and "reset this match" are not the same act, and neither is currently offered as a
   consequence of losing.

Worth pairing with R1 (play the game with human hands) rather than designed from here: how
long a defeated player actually wants to keep watching is the input this needs, and nobody
has watched yet.

---

# Recommended next improvements (2026-07-31)

Direction-level guidance from the 2026-07-31 whole-repo review (code, all docs, pipelines,
and the reference study in `GRAPHICS_AA_UPLIFT.md`). These are recommendations, not
defects — they rank what to do next across the whole project, and they are one reviewer's
perspective for the owner and both agents to weigh. Remove entries as they are acted on or
overruled.

**The observation underneath all five:** this project has exceptional *measurement*
discipline and near-zero *experience* verification. Roughly 20 acceptance boxes across
GD-01→10 are blocked on nothing but a human playing the game; nearly every tuning-log
entry ends "Not verified: how this feels to a human"; and every balance number is
bot-vs-bot — measured, for most of the record, against bots that never mazed, could build
only 5 of 15 towers, and stopped sending mid-match. The cheapest high-leverage act
available is converting measurement into experience.

## R1. Play the game with human hands — before more systems land

One hour of play with written notes unblocks more acceptance boxes than any code change,
and it is the only thing that can invalidate work *before* it compounds. The repo's own
history shows the cost of skipping it: three art pipelines were built and abandoned
because nobody looked, and the upgrade-tier system was designed to break a "stalemate"
that turned out to be a bot bug. `MVP_STATUS.md`'s own Next Work Order starts with exactly
this pass. Everything below is cheaper after it.

## R2. Put a build on a physical phone immediately after

A mobile-first game that has never run on a phone. Both device-validation docs now say
the tooling blockers are gone — Xcode confirmed installed, free Personal Team signing
suffices, Unity bundles the Android SDK. The self-imposed "prove the loop first" gate was
sensible a month ago; at 217 tests and a playable loop it is inverted: thermal, touch-
target and arm's-length readability findings will reshape the graphics uplift, and they
should arrive **before** Wave 1 art spending, not after. Item 2 (bloom cost on device)
becomes measurable the same day.

## R3. Treat bot quality as a product feature, not a test harness

Bots are simultaneously the measurement instrument for every balance number and the
shipped opponent of the offline MVP — bot quality *is* product quality here. Mazing and
the pressure bug are fixed; the remaining gap is that `BotTowerForSlot` reaches only 5 of
15 towers, and two profiles degenerate into repeating one tower forever. Until closed,
every mechanic-contribution measurement is measuring towers the opponent never builds,
and every human playtest (R1) is against an opponent doing a fraction of what the game
can do.

## R4. Build the command queue at a tick boundary next, structurally

`MULTIPLAYER_SEATS_AND_AUTHORITY.md` names it "the largest structural change remaining."
It needs no networking, is testable with the existing batch harness, and everything
online (seat table, lobby, transport, server) sits behind it. It also fixes a latent
defect already on record: commands apply mid-tick, which is part of why the replay record
cannot reproduce a match. The cost of this change only grows with every system built on
the current assumption.

## R5. Consolidate status into fewer living documents

The status docs contradict each other faster than two agents reconcile them:
`GAMEPLAY_DEVELOPMENT_CHECKLIST` GD-09 says the upgrade tiers are "not implemented" —
they shipped 2026-07-30 with 217 tests; `MVP_IMPLEMENTATION_CHECKLIST` is 18 days stale;
`MVP_STATUS` says three lanes where the sim runs eight. Suggested rule: declare
`GAMEPLAY_DEVELOPMENT_CHECKLIST`, this file, and `GRAPHICS_AA_UPLIFT.md` the only live
trackers; banner `MVP_IMPLEMENTATION_CHECKLIST` and `MVP_STATUS` as historical; and adopt
the tuning log's habit repo-wide — every status claim carries the commit SHA it was true
at. With two agents writing concurrently, every duplicated status is a future
contradiction.

## Explicitly not next

Recorded so effort is not spent re-deciding: more balance tuning (invalid until R3 and
R1); monetization (correctly deferred by its own doc); the match server (correctly gated
on the loop being fun); graphics Waves 2–3 (blocked on Wave 0's re-baseline capture being
looked at by a human — which is R1 again).

