# Graphics AA Uplift — Scaffolding And Plan

**Status:** active. Opened 2026-07-31.
**Supersedes the quality target** set in `archive/2026-07-planning/GRAPHICS_2000_BASELINE_ROADMAP.md`
and restated in `ART_THEME_AND_ROLE_GUIDE.md` and `skill/line-wards-ltw-graphics-art-direction.md`.
It does **not** supersede those documents' readability rules, palette, or silhouette grammar —
all of that is kept and built on.

This document is the single entry point for the graphics uplift. It exists because the
project's art documentation is spread across eleven files that specify readability in
detail and polish essentially not at all, and because three findings below mean the
current look is not primarily an art problem.

---

## Execution status — updated 2026-07-31

**Wave 0 is complete, including 0.8.** Wave 2.1 is complete. Wave 1.4 and three of Wave 4's
four items are complete. Three planned items turned out to rest on wrong premises and were
resolved differently; those are called out below because following the plan as written
would have made two of them actively worse.

| Wave item | State | Note |
| --- | --- | --- |
| 0.1–0.2 post-processing profile | **done** | The profile held three null sub-assets since `746403b`; the game had no tonemapper and no bloom for the whole URP migration |
| 0.3, 0.4, 0.7 flags | **done** | Soft shadows, SMAA, HDR grading |
| 0.5 reflections | **done, differently** | Generated from the scene's own ambient gradient rather than a probe — a probe captures at launch when the board is empty and would then be wrong all match |
| 0.6 material split | **done** | 30 bodies at 0.45. The plan's caution against copying 1.0 was right; the plan's 0.35–0.5 range was right |
| 0.8 re-baseline | **done** | `screenshot-reviews/aa-uplift-wave0-rebaseline/` |
| 1.1 recover AO | **premise disproved** | See below |
| 1.4 blocking intake gate | **done, gate off** | Silent-skip fixed; blocking mechanism ships behind `--strict`, off because it would fail every asset (blocked on 1.2) |
| 2.1 particle system | **done** | See below |
| 2.2 build the 14 VFX prefabs | **superseded** | Covered by the code system; the prefabs were never buildable |
| 2.4 motion for 7 creeps | **partly wrong** | They already have per-creep procedural motion; what they lack is skeletal deformation |
| 4.x LOD cross-fade, GPU skinning, quality tiers, SRP batching | **done** | Only LOD groups themselves remain, and that needs a decimation stage built first |

### Three premises that did not survive contact

**1.1 — "AO exists on disk and our script discards it."** It does not. The ORM red channel
is exactly 0.000 in every pixel of all 21 packed maps, and no occlusion map exists anywhere
in the art tree. Implementing this as written would have bound an all-zero `_OcclusionMap`,
and zero means *fully occluded* — the change advertised as free image quality would have put
all thirty assets into full ambient darkness. The repacker now extracts occlusion when there
is occlusion and reports when there is not.

**2.1/2.2 — "authoring never started."** `com.unity.modules.particlesystem` was not in the
package manifest, so `ParticleSystem` did not exist as a type. The fourteen prefabs were
unbuildable, not unbuilt.

**0.6 second half — "give the 5 zero-emission creeps emission."** They have no emission map
on disk, and emission without a map lights the whole body uniformly. They are left dark
deliberately. It is also eight creeps, not five: three more have maps that are functionally
blank.

### Where the durable knowledge now lives

This document stays the *plan*. What the work produced lives in:

- [Render and art validation](RENDER_AND_ART_VALIDATION.md) — the validators, the invariants
  they protect, exit codes, and the headless-run conventions
- [Material language guide](MATERIAL_LANGUAGE_GUIDE.md) — the authoritative surface values
- [VFX and animation targets](VFX_AND_ANIMATION_TARGETS.md) — the VFX system as built

---

## 1. The three findings that reframe this work

Do these before commissioning any new art. Each was verified directly against the repo
on 2026-07-31, at commit `c92d6fb` or later.

### 1.1 Post-processing is not running in the committed build

> **RESOLVED 2026-07-31 in `24ff842`.** Kept because it is the clearest worked example of the
> failure mode this whole document is about — a defect that every system reported as success.
> Root cause was one missing call: `UrpPostProcessingSetup` used `VolumeProfile.Add<T>` and
> never `AssetDatabase.AddObjectToAsset`, so the components existed in memory, logged "3
> override(s)", and deserialized as `{fileID: 0}` on the next reload. Guarded on both sides
> now; see [Render and art validation](RENDER_AND_ART_VALIDATION.md).

`Assets/Resources/LTW_PostProcessing.asset` — the profile loaded at runtime by
`LocalVerticalSliceLauncher` — contains three **null** component references:

```yaml
m_Name: LTW_PostProcessing
components:
- {fileID: 0}
- {fileID: 0}
- {fileID: 0}
```

`Assets/DefaultVolumeProfile.asset`, the URP global default, is `components: []`.

The tonemapper, bloom and colour-adjustment values exist only inside
`Assets/Editor/UrpPostProcessingSetup.cs`, as an editor menu item
(`Line Wards/Migration/Create Post Processing Profile`). It was run once in someone's
editor; the resulting sub-assets never landed on disk. The only commit that ever touched
the profile is `746403b "Phase 3b: add tonemapping and bloom"`.

**Consequence: the game renders with no tonemapper and no bloom.** Linear HDR values clip
straight to sRGB. Every emissive surface authored at 2.0 intensity clamps to flat white.
The entire URP migration was undertaken to get bloom, and bloom has never run in a
committed build. This is the single largest cause of "no shine", and it is a bug, not an
art deficiency.

**Guard it.** Fixing this by re-running the menu item leaves it free to regress the same
silent way. Add a runtime assertion — profile is non-null, `components.Count == 3`, no
component null — that fails loudly on load, plus an editor test. Otherwise the next
person to regenerate the profile reintroduces this and nobody notices for a month.

### 1.2 The promotion scorecard cannot detect the problem

`MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md` §4 defines a genuinely good 0–3 scorecard
across 15 categories, with blocking rules and a promotion gate. Every one of the 15
categories is a **readability** criterion: arena fit, lane readability, gate clarity,
touch clearance, silhouette identity, grayscale separation, heavy-pressure readability,
reduced-effects readability, signal priority, motion clarity, palette cohesion,
icon-to-runtime match, originality, fallback behaviour.

Not one measures surface quality, material richness, effect quality, animation richness,
lighting craft, or UI craft. **A build can score 3/3 on all fifteen and look exactly like
the current one** — clear, correct, legible, and unpolished. Section 5 adds the missing
axis rather than replacing the existing one.

### 1.3 The documented target is deliberately below AA

`GRAPHICS_2000_BASELINE_ROADMAP.md`:

> **The target is not modern AAA fidelity.** The target is a cohesive, original,
> mobile-readable ward-tech fantasy style with authored low-poly silhouettes, simple
> materials, readable animation, and satisfying combat feedback.
> Working shorthand: Current feel: "1980s" debug primitives. Target feel: "2000" polished
> prototype.

Echoed in `ART_THEME_AND_ROLE_GUIDE.md` ("closer to polished early-2000s strategy
readability than modern noise") and the art-direction skill ("low-fi but intentional").

**The docs and the owner's assessment are not in conflict — the docs are executing an
early-2000s target faithfully, and the game looks like what they asked for.** The target
itself is what has been outgrown. Section 3 states the replacement. This is the one item
in this document that is a decision, not a finding.

---

## 2. Verified current state

Numbers below were checked directly, not inferred. They are the baseline this plan moves.

### Surface and material

| Fact | Value |
| --- | --- |
| Normal maps on LTW assets | **0 of 30** (`_BumpMap` is `m_Texture: {fileID: 0}` in every LTW material) |
| Normal maps in `ThirdParty/StylizedWeaponKit` | **20 of 20** — the visible quality delta, in the same project |
| Ambient occlusion maps bound | **0** |
| AO data available but discarded | **Yes** — Meshy ships ORM packing; the R channel *is* AO, and `repack_metallic_smoothness.py` throws it away |
| Texture map types produced per asset | BaseColor, MetallicSmoothness, Emit (25/30), MetallicRoughness. **No Normal, no Occlusion, no height/curvature/detail/mask** |
| Custom shaders | 4 hand-written (`BoardVertexColor`, `ContactShadow`, `SporeFog`, `FillBar`). 3 of the 4 use built-in-pipeline `UnityCG.cginc` and are **not SRP-Batcher compatible** |
| Shader Graph assets | **0** |
| Materials on stock `URP/Lit` | **119 of 149** — every tower, every creep |
| Triangles per unit | ~15,000, one submesh, one material, LOD0 only. No decimation stage exists in the pipeline |
| LOD groups | **0**, while `m_EnableLODCrossFade: 1` is set for LOD groups that do not exist |

### The material split — clear evidence of two-agent drift

Tower body materials fall into exactly two clusters with no intermediate values, and the
split is precisely the original 5 towers versus the 10 added later:

| Cluster | Towers | `_Smoothness` | `_EmissionColor` |
| --- | --- | --- | --- |
| **A** | arrow, control, prism, pulse, relay | **1.0** | per-role, peaks at **2.0** (arrow `0.604, 1.278, 2.0`) |
| **B** | barricade, bloomheart, elder_canopy, foundry, gatling, repair_drone, sapling, spore_cloud, tesla, thorn_snare | **0.12** | identical `0.02, 0.035, 0.05` on all ten — effectively black |

Cluster B multiplies its metallic-gloss map by 0.12: **dead matte, no specular response,
no shine, on two thirds of the tower roster.** Its emission at 0.05 sits far below the
bloom threshold of 1.05, so those towers would not bloom even once bloom is restored.
Cluster B is also the same 10 towers that never received a `split_tower_rigid_part.py`
pass. Two batches, two authors, one shipped.

Creep bodies split three ways on the same pattern; five creeps (burrower, colossus,
stalker, warden, zephyr) have **no emission map and black emission colour**.

### Lighting and render setup

| Fact | Value |
| --- | --- |
| Scene contents | `LocalVerticalSlice.unity` has **zero GameObjects**; the whole scene is built at runtime |
| Light rig | Key 1.2 @ 50° (shadow strength 0.55), fill 0.35, rim 0.5 |
| Soft shadows | Code requests `LightShadows.Soft`; URP asset has `m_SoftShadowsSupported: 0` → **hard aliased 1024-map shadows** |
| Shadow cascades | 1 |
| Reflection probes | **0** — every smooth surface reflects Unity's stock procedural blue-grey sky against a `(0.06, 0.08, 0.12)` navy clear colour. The reflections do not match the scene |
| Light probes | 0 placed (renderers set to Blend Probes) |
| Baked lighting | None possible — `generateSecondaryUV: 0` on every FBX, so no lightmap UVs exist |
| Depth texture | **Off** — blocks SSAO, soft particles, any depth effect |
| Opaque texture | Off — blocks refraction/distortion |
| Renderer features | **`[]`** — no SSAO, no decals, no custom passes |
| Camera AA | **Not set** (None). MSAA 2x only |
| Fog | Off. Orthographic camera, no DOF, no vignette → **zero depth cueing** |
| Quality tiers | 6 tiers, all with `customRenderPipeline: {fileID: 0}` → **decorative**. One quality level on flagship and budget phones alike |
| Colour grading mode | LDR — cannot grade HDR even once grading exists |

### Effects and animation

| Fact | Value |
| --- | --- |
| `ParticleSystem` instances | **0** (also 0 `TrailRenderer`, 0 `LineRenderer`, 0 VFX Graph, 0 decal projectors) |
| How every effect is drawn | Pooled Unity primitives with flat colour — impacts are spheres, beams are stretched cubes, muzzle flash is two crossed cubes |
| VFX prefabs named in `VFX_AND_ANIMATION_TARGETS.md` | 14 specified, **0 exist** |
| Creeps with an Animator | **8 of 15** — revenant, runner, serpent, shade, siege, swarm, wisp are rigid meshes sliding along the lane |
| Animation states per creep | **1** (`Walk`). No idle, attack, hit, death, or spawn |
| Tower rigs / clips | 0 — all tower motion is procedural, and this part is well done |
| Font assets | **0** — no `.ttf`, `.otf`, or SDF asset. The game renders in Unity's default IMGUI skin font |
| HUD technology | IMGUI (`OnGUI`, 9 files). No uGUI, no UI Toolkit, no TextMeshPro, no tween library |

**The font and the IMGUI HUD deserve emphasis.** Default-Unity-font text is the fastest
"unfinished" tell in mobile games and it is on 100% of frames. And IMGUI is immediate-mode:
there is no retained object to animate, so *every* UI juice item — scale pops, easing,
tweens, transitions — is blocked until the HUD moves to uGUI or UI Toolkit. No existing
doc acknowledges this dependency, and it is the largest hidden cost in the uplift.

---

## 3. The target, restated

**Replaces "2000 polished prototype".**

> Target: a cohesive, original, **modern stylized mobile** look — the production tier of
> Clash Royale, Brawl Stars, or Rush Royale — reached through deliberate, consistent
> material and lighting craft rather than fidelity or polygon budget. Readability remains
> the first constraint and is never traded for polish.

Three things this explicitly does **not** mean, so the target cannot be misread as licence
to undo work that was done for good reasons:

- **Not photorealism, and not higher polycount.** ~15k tris per unit is already generous.
  The gap is surface treatment, lighting and effects, not geometry density.
- **Not louder.** Every art doc's rule that board surfaces stay quiet and effects must not
  obscure placement cells still holds. Screen shake and heavy camera effects stay rejected.
- **Not a restart.** The palette, silhouette grammar, prefab anchor contract, motion
  profiles and intake budgets are kept as-is. This adds a layer; it does not replace one.

---

## 4. Reference reading — what actually makes Clash Royale look expensive

Derived from published breakdowns of Supercell's own workflow (sources at the end), and
mapped onto what LTW is currently doing. The point of this section is that the reference's
techniques map unusually well onto LTW's specific defects.

| Principle in the reference | What LTW does today | Implication |
| --- | --- | --- |
| **PBR is used, then deliberately broken.** Albedo kept simple — a couple of colour variations. The roughness map does the heavy lifting, tuned *rougher than reality* to produce broad, smooth colour gradients even on metal. | Albedo is Meshy's generated output: busy, photographic, uncontrolled. Roughness is untouched from Meshy except for a blanket 0.12/1.0 smoothness multiplier applied in two batches. | The single highest-value art change is **authored roughness with simple albedo**, not more detail. This inverts the instinct to add texture. |
| **Shader-heavy.** Masks authored per asset drive shader parameters; assets are not stock-lit. | 119 of 149 materials are stock `URP/Lit`. Zero Shader Graph. No rim, no ramp, no gradient term. | A single shared stylized character/prop shader (rim + gradient ramp + AO tint) applied across all 30 units is a bigger visual jump than any per-asset work. |
| **Strong contrast, distinctive silhouettes, bright saturated colour.** | Silhouette work is genuinely strong and well documented. Contrast is undercut by a missing tonemapper and no depth cueing. | Silhouette is the one axis already at target. Protect it; fix what is flattening it. |
| **Boxy, simplified forms with soft gradients** rather than surface noise. | Meshy output tends to fine surface noise; no simplification stage exists. | Consider a Blender simplification/re-topology stage, or bias Meshy prompts toward flat planar forms. |
| **Lighting is authored per shot by a render artist**, colour and light tuned last. | Runtime three-point rig, never revisited; tonemapper absent; reflections mismatch the scene. | Lighting and grade are a first-class authoring task with an owner, not a one-time setup. |

### 4.1 Direct observation of reference frames (2026-07-31)

The table above was assembled from published descriptions of Supercell's workflow. Reference
gameplay frames were then examined directly — a mid-battle arena frame and a high-resolution
character close-up. **Looking changed one conclusion and sharpened three others.** Recorded
here because the project's own history warns repeatedly that measurement without looking is
how this codebase gets misled.

**The correction — neither of our material clusters is the target.**

The reference has **no sharp specular highlights anywhere.** On a close-up of an armoured
character, metal reads as metal purely through a *broad, smooth value gradient* — lighter
toward the key, falling off gradually across the form — plus a cool rim on the silhouette
edge. There is no hotspot. Nothing looks wet or glossy.

That means the Wave 0.6 instruction to bring Cluster B up to Cluster A's `_Smoothness: 1.0`
**is wrong, and would overshoot into glossy plastic.** Both current clusters are off-target
in opposite directions:

| | `_Smoothness` | Reads as |
| --- | --- | --- |
| Cluster B (10 towers) | 0.12 | dead matte, no light response at all |
| Cluster A (5 towers) | 1.0 | sharp mirror hotspot, wet-looking |
| **Reference behaviour** | **roughly 0.35–0.5** | broad soft gradient, no hotspot |

Treat 0.35–0.5 as a starting bracket to tune by eye against a capture, not a number to
adopt on trust. The visual richness in the reference is carried by **AO and rim, not by
specular** — which independently raises the priority of items 6 and 1.5.

**What else the frames showed, that the written sources did not:**

- **Ambient occlusion is doing enormous work.** Deep, soft contact darkening in every
  crevice — under a helmet brim, between armour plates, in chainmail, in cloth folds. It is
  the single largest contributor to units reading as solid objects rather than lit shapes.
  This is the strongest argument yet for item 6 (AO is already on disk and being discarded).
- **Surface detail is real, not implied.** Chainmail reads as individual rings; leather and
  cloth have grain. That is normal-map or sculpted-geometry work, and it is legible at
  gameplay size — not just in close-up.
- **A dark edge separates units from the board.** Whether an outline pass or shadow
  shaping, units carry a darkened contour that keeps them readable over both light green
  and dark navy backgrounds.
- **Saturation hierarchy is strict and inverted from ours.** The ground is a mid-value,
  *desaturated* green; units and effects are saturated and higher-contrast. The board never
  competes. LTW already documents this principle — the reference shows how far to push it.
- **Explosions are layered rounded volumes**, not wispy particles: a hot near-white core, a
  saturated orange body, and darker rounded smoke puffs at the edge, each with a clear
  silhouette. Chunky and readable, which is achievable with modest particle counts —
  relevant to item 9's cost.
- **UI is flat, chunky and outlined.** Cards sit in a framed tray with thick cream borders
  and rounded corners; cost badges are solid colour circles with bold white numerals and a
  dark stroke; the elixir bar is a flat segmented fill. Selection state is a brighter border.
  Every text element carries an outline or stroke for legibility. There are no soft
  gradients-on-gradients and no thin type anywhere.

**Method note:** these observations come from viewing reference frames in a browser, not
from downloading or storing any asset. No reference image is committed to this repo.

**IP note:** this is a principles reference. Study the technique — roughness-driven
gradients, simple albedo, shader-driven rim, AO-carried depth — and do not copy Clash
Royale's designs, silhouettes, character concepts, colour identity, or assets. LTW's
originality requirement is already a blocking scorecard category and stays blocking.

---

## 5. Scorecard extension — the craft axis

Add to `MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md` §4 alongside the existing 15
readability categories. Same 0–3 scale, same verdict vocabulary. Readability categories
keep their blocking status; these are scored from the first uplift pass and become
blocking at Wave 3.

| # | Category | 0 | 3 |
| --- | --- | --- | --- |
| C1 | **Surface detail** | Flat, untextured normals; detail reads only as albedo noise | Normal + AO present and legible at phone size; forms read as sculpted |
| C2 | **Material differentiation** | All units share one apparent material | Metal, crystal, bark, stone are distinguishable at a glance without colour |
| C3 | **Specular and highlight behaviour** | No highlight, or blown to white | Highlights travel across forms as they rotate; gradients read as intended |
| C4 | **Grounding** | Units float; no contact cue | Contact shadow plus AO reads the unit as standing on the board |
| C5 | **Lighting craft** | Flat, ambient-only read | Key/fill/rim separate the unit from the board; reflections match the scene |
| C6 | **Tone and grade** | Untonemapped clipping, or a muddy grade | Highlights roll off; the palette survives the grade; blacks are not crushed |
| C7 | **Impact feedback** | Flat primitive cue | Hit reads as an event — particle, flash, and a response on the target |
| C8 | **Death and spawn** | Instant pop | Deaths and arrivals have a beat the eye can follow |
| C9 | **Projectile craft** | Untextured stretched primitive | Trail, muzzle and impact read as one coherent effect |
| C10 | **Typography** | Default engine font | An authored typeface, consistently applied, legible at phone size |
| C11 | **UI craft** | Flat rects, hard edges, no state motion | Framed, layered, with tweened state changes and readable hierarchy |
| C12 | **Motion richness** | One clip, or none | Idle, move, hit and death read distinctly per unit |

**Method rule, carried over from the existing cycle and reinforced:** score against a real
match capture rendered with the game's own lighting and post-processing — never against a
contact sheet, never with `-nographics`, never against a painted mock. The project has
already been burned three separate ways by exactly this (see §7).

---

## 6. The waves

Sequenced so that each wave's output is judged under a correct renderer. **Wave 0 is
mandatory before any art is commissioned or evaluated** — art authored or reviewed against
a broken renderer is what produced three abandoned pipelines already.

### Wave 0 — Repair the foundation (no new art)

Cheap, mostly configuration, and the largest single visual jump available.

| # | Task | Where | Effort |
| --- | --- | --- | --- |
| 0.1 | Regenerate the post-processing profile and **commit the sub-assets**; verify the `.asset` holds three real `MonoBehaviour` docs | `Assets/Resources/LTW_PostProcessing.asset` | minutes |
| 0.2 | Add a load-time assertion + editor test that the profile is non-null and has no null components | new | small |
| 0.3 | Enable `m_SoftShadowsSupported` — code already requests soft shadows and silently gets hard ones | `LTW_UniversalRenderPipeline.asset` | one flag |
| 0.4 | Set `cameraData.antialiasing = SMAA` | `LocalVerticalSliceLauncher.cs` | one line |
| 0.5 | Add a reflection probe or a custom cubemap matching the navy clear colour | scene bootstrap | small |
| 0.6 | Reconcile the material split — move **both** clusters toward a tuned middle (start ~0.35–0.5 smoothness, see §4.1), and give the 5 zero-emission creeps emission. Do **not** simply copy Cluster A's 1.0 onto the other ten | `Art/**/Materials/*.mat` | small, but tune by eye |
| 0.7 | Set `m_ColorGradingMode` to HDR | URP asset | one flag |
| 0.8 | Re-capture the full review state set and re-baseline. **Do not skip** — every later judgement depends on it | capture runners | medium |

Wave 0 changes no art and should visibly change the game.

### Wave 1 — Surface quality

| # | Task | Notes |
| --- | --- | --- |
| 1.1 | Stop discarding AO — write the ORM red channel out as `Baked_Occlusion.png` and bind `_OcclusionMap` | ~15 lines in `repack_metallic_smoothness.py`; recovers AO for 20 of 30 assets from data already on disk. Highest payoff in the plan |
| 1.2 | Normal maps for all 30 — re-run Meshy with normals requested, or add a Blender bake stage | The intake gate has failed this 15 times; two routes were documented and neither tried |
| 1.3 | Enable depth texture and add the SSAO renderer feature | `m_RendererFeatures` is currently `[]` |
| 1.4 | Make `has_normal_map` a **blocking** intake gate, and fix that it is silently skipped when no textures are found | `ai_asset_intake.py` — the check is advisory today and nothing reads `score.json` |
| 1.5 | Author a shared stylized surface shader — rim/fresnel + gradient ramp + AO tint | The reference's core technique; also what the rim light was added to approximate |
| 1.6 | Roughness authoring pass — simple albedo, tuned roughness, per material family | Requires §4's inversion: less albedo detail, more roughness control |

### Wave 2 — Effects and motion

| # | Task | Notes |
| --- | --- | --- |
| 2.1 | Introduce a particle system at all — impact burst, muzzle flash, projectile trail | Currently zero. Biggest perceived-production-value jump available |
| 2.2 | Build the 14 VFX prefabs already specified | Design work is done in `VFX_AND_ANIMATION_TARGETS.md`; authoring is not |
| 2.3 | Death and spawn beats | Deaths are instantaneous; the pooling/lifecycle blocker is already identified |
| 2.4 | Animator or procedural motion for the 7 creeps that have neither | revenant, runner, serpent, shade, siege, swarm, wisp |
| 2.5 | Second clip per rigged creep — hit reaction, then death | One state each is the whole state machine today |

### Wave 3 — UI and typography

| # | Task | Notes |
| --- | --- | --- |
| 3.1 | **Decide the HUD technology.** IMGUI structurally blocks all UI motion | Prerequisite for 3.3–3.4. Largest hidden cost in the uplift; decide before committing to a date |
| 3.2 | Adopt a real typeface (TextMeshPro SDF) | Zero font assets exist; affects every frame |
| 3.3 | Card, panel and button treatment — 9-slice frames, layering, drop shadows | No spec exists; `art-pipeline/ui-board/selected-candidates-v02.md` has approved directions to build from |
| 3.4 | State motion — scale pops, easing, transitions | Blocked by 3.1 |
| 3.5 | Rebuild the icon family | Open in four docs since 2026-07-15; blocked because its source silhouettes were deleted 2026-07-26 |

### Wave 4 — Performance and shipping

Deliberately last, but do not let it fall off: `~15k tris × 30 units` with **no LODs**,
CPU skinning (`gpuSkinning: 0`), non-SRP-batchable custom shaders, and six quality tiers
that all resolve to one URP asset. A 40-creep swarm is ~600k triangles at LOD0. Bloom cost
on a physical Android device has never been profiled — carried over from the retired
`OPEN_ITEMS.md` item 2.

---

## 7. Do not re-propose these

Three whole art pipelines have already been tried and abandoned, with reasons documented
precisely enough that repeating them would be waste.

- **Procedural / Unity-generated geometry.** Control tower ran v01→v39 as a scripted
  Blender loop. Verdict: *"more local variants tended to add grain, fuzz, overdraw, and
  noise faster than they added real fidelity."* Hard rule recorded: *"If the next action is
  'make another procedural Blender variant,' stop."*
- **The stylized weapon kit.** 18 vendor prefabs, wrappers built for 8 roles, never
  actually replaced the placeholder look. Superseded wholesale.
- **The AIPlate 2.5D sprite era** (2026-07-15 → 07-26). Full contact-sheet → plate →
  candidate → promotion cycle for every role, many within-role iterations, all deleted.

**And the finding that explains why all three failed** — from the retired
`GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md`:

> The Meshy 3D assets look markedly worse in game than they do in the source previews.
> **The cause is not the meshes or their textures.** It is the scene and material setup
> around them: the game contains no lights, runs in Gamma colour space, and its runtime
> materials discard two of the three maps the intake produces.
> **Working note: Prior art cycles iterated 2D source material to solve what is a lighting
> and material binding problem.**

That diagnosis was correct and was acted on — lights, linear colour, metallic/emission
binding all landed. §1.1 is the same class of failure one layer further out: the
post-processing stage that the URP migration existed to enable has never actually run.
**The pattern to watch for is iterating art to fix a renderer defect.** Wave 0 exists
specifically to close that trap before this wave of art work starts.

Also already tried and rejected, with reasons: ACES tonemapping (shifts the palette warm,
undoing the board and role colour work); synthetic height-derived normal bakes (rejected
without trying, as a silent visual tradeoff — worth revisiting explicitly under 1.2); leg
rigs on shell-bodied creeps (built, works, and is invisible from the game camera).

**Method warnings worth carrying into every wave**, each learned the hard way here:

- Establish what is real in a capture before drawing conclusions. A CPU-painted HUD mock
  once produced five false UI defects and two font "fixes" that changed nothing.
- Verify that a fix changes the artefact. One font fix was applied twice and both times
  the captures were byte-identical.
- Never run captures with `-nographics` — it makes every frame flat colour while still
  reporting success.
- Nearly every art entry in `GD_TUNING_LOG.md` ends with *"Not verified: how this reads in
  motion to a human eye."* **The measurement discipline here is excellent; the looking
  discipline is the gap.** Every wave needs a human to watch it move.

---

## 8. Known blockers and stale references to clear

**Most of this section is cleared as of 2026-07-31.** What remains is listed first.

Still open:

- **`ART_THEME_AND_ROLE_GUIDE.md` still describes a 5+5 roster.** It is the primary theme
  authority, so 10 towers and 10 creeps have no silhouette spec, no "Avoid" list and no
  phone-size test. The other two docs that carried this have been corrected.
- **"Creeps stay smaller than towers" is stated as non-negotiable and is false by design** —
  the health-tracking scale rule puts Siege Colossus at 1.247 against towers at ~0.74. The
  decision was sound; the docs were never updated.

Cleared:

- ~~The promotion gate points at deleted assets.~~ Fixed in `76deab8`. The coverage report is
  regenerated from the two visual libraries, names the `_3D` prefabs, and is checked by
  `tools/art_pipeline/validate_role_coverage.py`. **The larger finding it exposed is now
  `OPEN_ITEMS.md` item 20**: production references exist only for the original ten roles, so
  the gate scores ten against 2D plates their own 3D models superseded and cannot score the
  other twenty at all.
- ~~Two capture states land nothing in frame.~~ Already fixed in the tree; the item was stale.
  Verified at 64 and 75 on-camera creeps in the framed lane.
- ~~`GRAPHICS_THEME_WORK_BREAKDOWN.md` gives a command using Unity `6000.3.12f1`.~~ Fixed in
  `892b64b`, and it was in **two** archived docs across three runnable commands, not one. Both
  now carry a banner not to restore the version from history.
- ~~Two incompatible capture-state numbering schemes.~~ Fixed in `892b64b`. Worse than
  recorded: the cited numbers opened two static UI frames with no creeps in them. The rule is
  now to cite the state NAME, never the number.

---

## 9. Open decisions

1. **Confirm the target in §3.** Everything else follows from it. It is a real scope
   increase, not a reframing.
2. **HUD technology (3.1).** uGUI + TextMeshPro is the conventional, lower-risk choice;
   UI Toolkit is more modern but a larger migration from IMGUI. All UI motion waits on it.
3. **Normal map route (1.2).** Re-run Meshy with normals requested (costs generation
   credits, best quality) versus a Blender bake stage (free, more pipeline work, and a
   synthetic bake was previously rejected as a silent tradeoff).
4. **Whether to re-source albedo.** §4's core lesson is simple albedo plus authored
   roughness. Meshy's generated albedo is the opposite. Reworking it is the difference
   between a good-looking pass and a genuinely on-reference one, and it is the most
   expensive item implied by this plan.

---

## Sources

Reference-principle research for §4:

- [Supercell Helsinki: Creating Stylized Content for Clash of Clans and Clash Royale](https://magazine.substance3d.com/supercell-helsinki-creating-stylized-content-for-clash-of-clans-and-clash-royale/)
- [Supercell Helsinki (Adobe mirror)](https://www.adobe.com/products/substance3d/magazine/supercell-helsinki-creating-stylized-content-for-clash-of-clans-and-clash-royale.html)
- [UX in Clash Royale — The Rookies](https://discover.therookies.co/2020/02/24/game-design-ux-best-practices-detailed-breakdown-of-clash-royale/)
- [Creating Clash Royale Stylized Games — RetroStyle Games](https://retrostylegames.com/portfolio/spell-arena-3d-characters-clash-royale/)

Repo state in §1 and §2 was verified directly against the working tree on 2026-07-31.
