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
on 2026-07-31. Items 5–16 are new.

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

## 5. URGENT — post-processing is not running in the committed build

`Assets/Resources/LTW_PostProcessing.asset`, the profile loaded at runtime by
`LocalVerticalSliceLauncher`, contains three **null** component references:

```yaml
m_Name: LTW_PostProcessing
components:
- {fileID: 0}
- {fileID: 0}
- {fileID: 0}
```

`Assets/DefaultVolumeProfile.asset`, the URP global default, is `components: []`.

The tonemapper, bloom and colour-adjustment values exist only inside
`Assets/Editor/UrpPostProcessingSetup.cs` as an editor menu item
(`Line Wards/Migration/Create Post Processing Profile`). It was run once in someone's
editor and the resulting sub-assets never landed on disk — the only commit that ever
touched the profile is `746403b "Phase 3b: add tonemapping and bloom"`.

**The game therefore renders with no tonemapper and no bloom.** Linear HDR values clip
straight to sRGB; every emissive surface authored at 2.0 intensity clamps to flat white.
The entire URP migration was undertaken to get bloom, and bloom has never run in a
committed build. This is the single largest cause of the flat, unpolished look, and it is
a bug rather than an art deficiency.

Fixing it by re-running the menu item leaves it free to regress the same silent way.
**Land the fix and a guard together**: a load-time assertion that the profile is non-null,
has three components, and none are null — plus an editor test. Wave 0.1–0.2.

## 6. AO is produced by the source assets and thrown away by our own script

Meshy ships ORM-packed textures, where the **red channel is ambient occlusion**.
`tools/art_pipeline/repack_metallic_smoothness.py` converts ORM into Unity's
metallic/smoothness layout and discards R in the process.

So AO exists on disk for 20 of 30 assets and is being deleted by a script we own. Writing
it out as `Baked_Occlusion.png` and binding `_OcclusionMap` is roughly a 15-line change.
**Highest payoff-per-effort item in the entire uplift** — no generation credits, no new
authoring, data already present. Wave 1.1.

## 7. Ten of fifteen tower bodies are dead matte, from a two-agent material split

Verified 2026-07-31. Tower body materials fall into exactly two clusters with no
intermediate values, and the split is precisely the original 5 towers versus the 10 added
later:

| Cluster | Towers | `_Smoothness` | `_EmissionColor` |
| --- | --- | --- | --- |
| A | arrow, control, prism, pulse, relay | **1.0** | per-role, peaks at **2.0** (arrow `0.604, 1.278, 2.0`) |
| B | barricade, bloomheart, elder_canopy, foundry, gatling, repair_drone, sapling, spore_cloud, tesla, thorn_snare | **0.12** | identical `0.02, 0.035, 0.05` on all ten |

Cluster B multiplies its metallic-gloss map by 0.12 — no specular response, no shine, on
two thirds of the roster. Its emission sits far below the bloom threshold of 1.05, so
those towers will not bloom even once item 5 is fixed. Cluster B is also exactly the 10
towers that never received a `split_tower_rigid_part.py` pass.

Creep bodies split three ways on the same pattern; burrower, colossus, stalker, warden and
zephyr have **no emission map and black emission colour**.

**Corrected 2026-07-31 after examining reference frames directly.** The original fix here
said "bring Cluster B to Cluster A's values." That is wrong and would overshoot. The
reference look has **no sharp specular highlights at all** — metal reads through a broad
soft value gradient plus a rim, never a hotspot. `_Smoothness: 1.0` produces exactly the
glossy hotspot the target avoids.

Both clusters are off-target in opposite directions: 0.12 is dead matte, 1.0 is wet
plastic. Start around **0.35–0.5** and tune by eye against a real capture — and note that
in the reference the richness is carried by **AO and rim light, not by specular**, which
makes item 6 the more important half of this fix. Give the five zero-emission creeps
emission as well. Wave 0.6, and see `GRAPHICS_AA_UPLIFT.md` §4.1.

## 8. Render-setup defects that cost nothing to fix

Each verified 2026-07-31, each a flag or a line:

- **Soft shadows are requested but disabled.** `LocalVerticalSliceLauncher` sets
  `LightShadows.Soft`; the URP asset has `m_SoftShadowsSupported: 0`, so URP strips the
  variant and the key light renders hard, aliased 1024-map shadows.
- **No camera antialiasing.** `cameraData.antialiasing` is never set, so it defaults to
  `None`. MSAA 2x alone leaves visible stair-stepping on crystal and spire silhouettes at
  phone DPI.
- **No reflection probe.** Every smooth surface reflects Unity's stock procedural
  blue-grey sky while the camera clears to `(0.06, 0.08, 0.12)` navy. The reflections do
  not match the scene — a classic prototype tell.
- **Colour grading mode is LDR**, so HDR values cannot be graded even once grading exists.
- **Depth texture and opaque texture are both off**, which blocks SSAO, soft particles and
  any depth- or refraction-based effect.
- **`m_RendererFeatures` is `[]`** — no SSAO, no decals, no custom passes.

Wave 0.3–0.7, except SSAO which needs the depth texture first (Wave 1.3).

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

## 12. The intake gate does not gate

`ai_asset_intake.py` produces a real scorecard, and nothing consumes it:

- `has_normal_map` is **advisory** — a failure yields `status: needs_review`, never `fail`.
  Only zero meshes or zero triangles produce a hard fail.
- The check is **silently skipped** when the audit found no texture paths, so an asset
  with no maps at all scores a clean pass.
- **No CI job, editor gate or promotion script reads `score.json`.** 14 of 30 shipped
  assets carry `status: needs_review`; all 30 are in production.

Making status blocking is cheap. The reason it was not is that it would have blocked every
asset in the game — which is the finding, not an objection. Sequence it after item 3 so
the gate has something to pass. Wave 1.4.

## 13. The promotion scorecard cannot detect the problem it needs to catch

`MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md` §4 defines a good 0–3 scorecard across 15
categories with blocking rules and a promotion gate. **Every one of the 15 is a
readability criterion** — arena fit, lane readability, gate clarity, touch clearance,
silhouette identity, grayscale separation, heavy-pressure readability, signal priority,
motion clarity, palette cohesion, icon match, originality, fallback behaviour.

None measures surface quality, material richness, effect quality, animation richness,
lighting craft or UI craft. A build can score 3/3 on all fifteen and look exactly like the
current one. A 12-category craft axis is drafted in `GRAPHICS_AA_UPLIFT.md` §5 and needs
folding into the cycle doc.

## 14. The promotion gate points at deleted assets, and two capture states are broken

Both block the review process that every other item depends on:

- **The target-reference gate cannot be satisfied.** The improvement cycle makes a
  "target-reference match score" a promotion requirement, against the canonical targets in
  `art-pipeline/v1-role-coverage-report.md` — which lists `Tower_*_AIPlate.prefab` and
  `Sprites/*_trimmed.png`. Those were deleted on 2026-07-26. Fix before Wave 1.
- **`runner-10-pressure` and `swarm-heavy-pressure` land nothing in the framed lane**
  (also tracked in `GAMEPLAY_REVIEW_FINDINGS.md`). Heavy-pressure readability is a
  *blocking* scorecard category, so it currently cannot be reviewed at all.

## 15. Performance debt that will land before ship

Not urgent for look, but it will constrain what the uplift can afford:

- **No LOD groups exist**, while `m_EnableLODCrossFade: 1` is set on the URP asset for LOD
  groups that do not exist. Every unit is ~15,000 triangles at LOD0 forever; a 40-creep
  swarm is ~600k triangles. There is no decimation stage anywhere in the Blender pipeline.
- **Quality tiers are decorative.** All six have `customRenderPipeline: {fileID: 0}`, so
  every tier resolves to the same URP asset. There is effectively one quality level, and it
  is identical on a flagship and a budget phone.
- **`gpuSkinning: 0`** — the eight skinned creeps deform on CPU.
- **Three of the four custom shaders use built-in-pipeline `UnityCG.cginc`** and are not
  SRP-Batcher compatible, so the 30 contact-shadow quads each break batching.

Wave 4.

## 16. Art docs that are stale or actively dangerous

- **`GRAPHICS_THEME_WORK_BREAKDOWN.md` gives a batchmode command using Unity
  `6000.3.12f1`** — the version that silently downgrades `ProjectSettings.asset` from
  serialized version 29 to 28. Running the documented command as written corrupts project
  settings. Fix this one first; it is the only item here that can damage the repo.
- **Three docs still describe a 5+5 roster as current** —
  `ART_THEME_AND_ROLE_GUIDE.md` (the primary theme authority, so 10 towers and 10 creeps
  have no silhouette spec, no "Avoid" list and no phone-size test),
  `MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md`, and `VFX_AND_ANIMATION_TARGETS.md`.
- **"Creeps stay smaller than towers" is stated as non-negotiable in three docs and is now
  false by design.** The health-tracking scale rule puts Siege Colossus at 1.247 against
  towers at ~0.74. The decision was sound; the docs were never updated to match.
- **Two incompatible capture-state numbering schemes are in use.** Following
  `VFX_AND_ANIMATION_TARGETS.md` literally now tests the wrong three frames.

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
