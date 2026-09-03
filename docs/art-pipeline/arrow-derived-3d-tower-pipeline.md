# Arrow-Derived 3D Tower Pipeline

Date: 2026-07-23  
Owner: Codex + implementation agents  
Status: draft for review

## Purpose

Use the successful Arrow tower 3D proof as the canonical example for a repeatable Line Wars tower 3D intake pipeline.

The goal is not to depend on Unity AI, procedural mesh scripts, or sprite-card extrusion as magic generators. Those attempts have now proven the same lesson: the repo pipeline can normalize and integrate a good 3D source asset, but it cannot invent polished 3D art from weak geometry.

The useful process is:

```text
approved 2D/2.5D design target
  -> externally generated, kitbashed, or hand-modeled 3D source asset
  -> Blender/source cleanup pass
  -> normalized Unity runtime wrapper
  -> intentional TowerVisualLibrary promotion
  -> mobile screenshot + animation certification
```

This lets Meshy, Tripo, Blender, Asset Store kitbashing, Unity-side animation, or hand-authored meshes provide raw source assets while Line Wars owns the final runtime merge process.

## Production Decision: Retire The Broken Shortcut

We are retiring the failed shortcut:

```text
2.5D source plate
  -> Unity/procedural auto-generated geometry
  -> immediate gameplay promotion
```

That process repeatedly produced assets that looked worse in-game than the AIPlate baseline: straight polygons, flat card tricks, muddy silhouettes, and models with less apparent detail after import. It is now considered a rejected experiment, not an active production path.

Going forward:

- Unity/editor scripts are for importing, wrapping, validating, and promoting assets, not for creating final art quality.
- Procedural geometry may be used only for invisible anchors, range halos, owner trim, placeholders, or temporary proof scaffolds.
- A 3D candidate must start as a real external/source asset: `.glb`, `.fbx`, Blender-authored mesh, or a curated asset-pack mesh.
- Every candidate gets a source cleanup pass before runtime promotion.
- No rejected proof candidate may be promoted to `TowerVisualLibrary.asset`.
- The active fallback remains the AIPlate sprite/token unless the 3D asset is clearly better in live gameplay.

## Target Pipeline For Detailed Animated 3D

This is the pipeline we should use to reach the actual end goal: detailed, readable, animated 3D game assets.

```text
1. Concept lock
   - Choose the role silhouette and style target from existing Line Wars plates.
   - Confirm it still reads at phone scale.

2. External 3D creation
   - Generate/model a proper mesh outside the runtime wrapper.
   - Preferred sources: Blender, Meshy/Tripo-style image-to-3D/text-to-3D, curated asset-kit kitbash, or human modeling.
   - Output `.glb`, `.fbx`, or `.blend` into AIStaging or a dedicated source folder.

3. Blender/source cleanup
   - Fix pivot at base.
   - Correct forward axis.
   - Remove hidden junk geometry.
   - Simplify tiny unreadable greebles.
   - Assign body/trim/energy material regions.
   - Prepare animation bones or separated moving parts.

4. Unity wrapper intake
   - Generate the Line Wars runtime wrapper.
   - Preserve required contract children and anchors.
   - Normalize scale, lift, material policy, shadows, bounds, and visual hooks.

5. Animation pass
   - Towers: idle core pulse, ring rotation, muzzle charge/fire, selected shimmer.
   - Creeps: locomotion loop, hit flash, death/dissolve, lane-transfer continuity.
   - Keep animation readable from the actual board camera.

6. Review and promotion
   - Compare against the current AIPlate fallback.
   - Capture normal and grayscale screenshots.
   - Promote only if the 3D version is clearly better in-game.
```

## Current Arrow Evidence

Arrow is the reference case because we have the full chain in-repo:

| Stage | Path |
| --- | --- |
| 2.5D source plate | `Assets/Art/AIStaging/SourcePlates/tower_arrow_source_plate_v03.png` |
| Production candidate sprite | `Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed.png` |
| Raw Unity AI prefab | `Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d.prefab` |
| Raw imported FBX | `Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d_Assets/selected.fbx` |
| Raw generated color texture | `Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d_Assets/selected.fbm/Color_c880fcfa-bfa6-467d-9d72-569a70753096.png` |
| Raw generated normal texture | `Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d_Assets/selected.fbm/NormalGL_c880fcfa-bfa6-467d-9d72-569a70753096.png` |
| Runtime wrapper | `Assets/Prefabs/Towers/Tower_Arrow_3D.prefab` |
| Import cleanup script | `Assets/Editor/ArrowTower3DImportCleanup.cs` |
| Runtime proof wrapper script | `Assets/Editor/ArrowTower3DProofGenerator.cs` |
| Full tower 3D wrapper script | `Assets/Editor/Tower3DProofSetGenerator.cs` |
| Source notes | `Assets/Art/Towers/Arrow/arrow_3d_unity_ai_source_notes.md` |

## What We Need To Reverse-Engineer From Arrow

The Arrow result gives us enough information to extract a reusable recipe.

### Source plate requirements

- Transparent or cleanly keyed background.
- Strong silhouette at phone gameplay scale.
- Clear top-down three-quarter read.
- Role-specific feature hierarchy:
  - primary silhouette;
  - secondary role cue;
  - small accent details only where they survive mobile scale.
- No UI frame, text, watermark, busy background, or IP-adjacent faction cues.

### Raw model import requirements

- Raw generated/imported model remains in `Assets/Art/AIStaging/...`.
- Runtime prefabs never point directly at throwaway generation folders without a wrapper.
- Import settings are normalized:
  - calculated normals;
  - calculated tangents;
  - readable mesh disabled unless a tool requires it;
  - colliders stripped from runtime wrapper;
  - shadows disabled for board readability;
  - model centered and lifted correctly.

### Runtime wrapper requirements

Every tower 3D wrapper must preserve the Line Wars prefab contract:

- `Body`
- `BodyTintAnchor`
- `RoleMarker`
- `OwnerTrim`
- `RangeHalo`

Role-specific anchors should be present even if empty, because gameplay/VFX can target them later:

| Role | Required role anchors |
| --- | --- |
| Arrow | `Muzzle`, `Lens`, `BowLeft`, `BowRight` |
| Control | `Muzzle`, `Lens`, `ControlCore`, `ControlRing`, `PulseEmitter` |
| Relay | `Muzzle`, `Lens`, `RelayMast`, `RelaySignal`, `RelayCore` |
| Pulse | `Muzzle`, `Lens`, `PulseCore`, `PulseRingA`, `PulseEmitter` |
| Prism | `Muzzle`, `Lens`, `PrismSpire`, `PrismLens`, `BeamAnchor` |

### Runtime material requirements

The Arrow proof intentionally moved away from raw generator material behavior:

- board-view material;
- no heavy specular;
- no normal-map dependence at phone scale;
- no shadows;
- stable brightness;
- limited palette;
- optional low emission for energy read;
- material instancing enabled where useful.

This is important. A generated model can look impressive in an isolated preview and still fail in the Line Wars board camera. Runtime material normalization is part of the pipeline, not a polish afterthought.

## Viable Pipeline Options

### Option A: Generalize the Arrow import/wrapper recipe

Take the existing Arrow cleanup and proof scripts and turn them into one reusable tool:

```text
Tower3DImportPipeline
  - clean model import settings
  - clean texture import settings
  - create/update runtime material
  - instantiate raw generated prefab under Body
  - apply rotation/scale/lift correction
  - strip colliders
  - disable shadows
  - create required anchors
  - update TowerVisualLibrary only when intentionally promoted
```

This is the recommended path.

Pros:

- fastest path from what already works;
- keeps Unity merge deterministic;
- lets us use any raw 3D source;
- reduces one-off hand edits;
- preserves rollback to AIPlate sprites.

Cons:

- still requires a human or separate tool to produce raw 3D meshes;
- role-specific anchor placement may need manual tuning at first.

### Option B: Build a style contract, not a generation clone

Use Arrow only to define the visual target:

- chunky readable silhouette;
- dark slate body;
- cyan/blue/violet energy;
- signal-gold trim;
- compact one-cell footprint;
- no tiny noisy detail;
- top-down three-quarter readability.

Then accept source meshes from Unity AI, Blender, external asset packs, or hand modeling.

Pros:

- avoids overfitting to Unity AI output;
- future-proof;
- best long-term art direction.

Cons:

- needs stricter review discipline;
- less automatable than Option A by itself.

### Option C: Add per-tower manifests

Each tower gets a small data record that tells the pipeline what to do:

```json
{
  "towerId": "tower.control",
  "role": "Control",
  "sourcePlate": "Assets/Art/AIStaging/SourcePlates/tower_control_source_plate_v03.png",
  "rawGeneratedPrefab": "Assets/Art/AIStaging/Models/Towers/Control/tower_control_3d.prefab",
  "runtimePrefab": "Assets/Prefabs/Towers/Tower_Control_3D.prefab",
  "scale": [1.12, 1.08, 1.12],
  "lift": 0.14,
  "anchors": ["Muzzle", "Lens", "ControlCore", "ControlRing", "PulseEmitter"]
}
```

Pros:

- makes the process inspectable;
- easy for multiple agents to divide work;
- gives us a checklist and script input in one place.

Cons:

- slightly more setup;
- manifest schema needs to stay simple.

### Option D: Use Blender as an inspection/repair stage

Use Blender or mesh scripts to inspect/repair:

- pivot;
- bounds;
- mesh count;
- material slots;
- orientation;
- polygon count;
- UV/texture assignments.

Pros:

- good for fixing malformed AI meshes;
- useful if Unity imports are inconsistent.

Cons:

- adds another dependency;
- not needed for the first generalized Unity wrapper pass.

## Recommended Path

Use the source-asset-first version of Option A plus Option C.

In plain terms: use a reusable Unity editor intake pipeline driven by per-tower specs/manifests, with Arrow as the golden example. The missing ingredient is not another wrapper script; it is a better raw/source 3D asset before Unity.

Do not try to reverse-engineer Unity AI’s private generation internals. Reverse-engineer our successful import and runtime integration process.

## Graphic Design Addendum

This pipeline needs an art-direction gate before it becomes an asset factory. The script can make imported assets technically valid, but technical validity is not the same thing as a polished Line Wars visual.

The target is early-2000s polished strategy-board readability: chunky, iconic, slightly toy-like, with enough bevel/energy/material detail to feel upgraded from flat primitives without becoming noisy miniature sci-fi props.

### Line Wars tower style bible

All tower 3D assets should share the same visual DNA:

- ward-tech fantasy, not medieval castle, not realistic military, not generic sci-fi turret;
- compact board-game miniature proportions;
- dark slate/blue-black base material;
- restrained signal-gold bevel or trim accents;
- cyan/blue/violet energy core as the brightest readable feature;
- mint/friendly highlights only where they help ownership or gameplay read;
- clean bevels and chunky planes instead of tiny surface greebles;
- strong readable top silhouette;
- slightly exaggerated role feature so the tower reads at phone scale;
- one-cell footprint discipline.

Forbidden drift:

- spindly details that disappear on mobile;
- high-frequency texture noise;
- realistic gunmetal/tactical turret language;
- medieval faction towers, banners, shields, faction crests, or Warcraft-like silhouettes;
- full-bright glowing bodies that compete with projectiles, creeps, or HUD;
- black-on-black forms where the silhouette vanishes into the board.

### Shape language by role

Every tower must pass a flat-silhouette test before material polish.

The rough black shape should still communicate the role:

| Role | Primary silhouette | Secondary cue | Avoid |
| --- | --- | --- | --- |
| Arrow | long forward rail / bow form | central lens and muzzle | generic cannon, rocket, sword |
| Control | wide ring/dish | suspended core, containment arcs | flat satellite dish with no core |
| Relay | tall antenna/mast | signal crown, capacitor fins | fragile needle, plain radio tower |
| Pulse | squat heavy drum | shock rings, pressure vents | generic barrel/cannon |
| Prism | tall faceted spire/lens | beam aperture, crystal planes | plain crystal shard with no machine base |

Silhouette gate:

- [ ] Reads in black at gameplay scale.
- [ ] Reads in grayscale at gameplay scale.
- [ ] Reads when placed beside the other four towers.
- [ ] Does not confuse with a creep, builder, projectile, or endpoint.
- [ ] Has one dominant role cue, not five competing ideas.

### Value hierarchy

Use value deliberately. The board is dark and narrow, so visual contrast is gameplay readability.

Recommended value order:

1. projectile / attack VFX: brightest moving element;
2. tower energy core or muzzle: brightest static tower element;
3. selected/range/ownership accent: bright enough to read, never brighter than active attack VFX;
4. gold trim: mid-bright accent;
5. body/base: dark-to-mid mass;
6. board/path/grid: darker background support.

Rules:

- The body should not be pure black.
- Energy should not flood the whole model.
- Gold trim should guide the eye, not sparkle everywhere.
- Grayscale review is mandatory because hue alone is not enough.
- The role cue must remain readable with saturation removed.

### Material recipes

The runtime pipeline should normalize imported materials into a small set of Line Wars material buckets.

| Bucket | Purpose | Direction |
| --- | --- | --- |
| Body | Main mass | dark slate, low shine, readable bevels |
| Trim | Mechanical accent | signal gold, medium brightness, restrained coverage |
| Energy | Core/lens/muzzle | cyan/blue/violet, limited emission, highest tower contrast |
| Owner | Player ownership cue | mint/blue accent, small surface area |
| Preview | build ghost/placement | translucent, bright enough to place, not confused with active tower |
| Disabled | cannot afford/invalid | desaturated/dimmed, still identifiable |
| Range | selection/range halo | readable but low opacity, never hides creeps/path |

Default policy:

- ignore generated normal maps unless they materially improve gameplay-scale readability;
- disable heavy specular;
- disable shadows on runtime wrappers unless a later board-lighting pass proves they help;
- prefer stable board-view materials over raw generator materials;
- allow per-role material variants only if they preserve shared palette and value hierarchy.

### Camera and lighting contract

Assets must be authored for the real game camera, not for a portfolio render.

Required assumptions:

- top-down three-quarter board view;
- portrait phone layout;
- one active lane visible;
- tower sits on one build cell;
- forward/muzzle direction should align to gameplay firing expectations;
- pivot centered at base;
- bottom contact point/lift must feel planted on the tile;
- no visible detail should require rotating the camera.

Lighting policy:

- runtime look should survive flat or simple scene lighting;
- silhouettes should not depend on dramatic rim lights;
- emissive elements must be controlled so they do not bloom into UI/path readability;
- assets should be checked in normal color and grayscale.

### Model and texture budget

Keep 3D assets mobile-friendly and visually controlled.

Initial target budget per tower:

| Item | Target |
| --- | --- |
| Triangle count | 500-3,000 preferred; over 5,000 requires justification |
| Materials | 1 shared runtime material preferred; 2-4 max if role-specific buckets are needed |
| Textures | 1024 max preferred; 2048 only for source/staging |
| Renderer count | low single digits preferred |
| Collider count | zero in visual wrapper |
| Animation | optional idle only; no readability-critical motion |

Budget gate:

- [ ] No tiny geometry below gameplay readability.
- [ ] No dense texture noise.
- [ ] No excessive material slots.
- [ ] No large hidden mesh pieces.
- [ ] No runtime colliders in the visual wrapper.

### Contact-sheet review requirements

Every candidate should be reviewed as part of the set, not alone.

Required screenshots/contact sheets:

- all five towers side by side, unselected;
- all five towers side by side, grayscale;
- selected tower with range halo;
- disabled/cannot-afford state;
- builder next to selected tower;
- active lane with creeps passing nearby;
- dense combat with projectiles/VFX;
- tower placed near spawn/leak endpoints;
- smallest supported phone profile;
- tallest supported phone profile.

Review questions:

- Which tower do you see first, and should it be first?
- Can you name each role without labels?
- Does any tower overpower creeps?
- Does any tower vanish into the board?
- Does selection/ownership remain readable?
- Does the asset still look good after downscaling?

### VFX and anchor art direction

Anchors are not only technical points. They define the visual feel of each tower.

| Role | Attack/VFX read |
| --- | --- |
| Arrow | focused bolt, sharp muzzle flash, narrow line energy |
| Control | containment pulse, ring expansion, slowed-field shimmer |
| Relay | signal ping, beacon pulse, link/chain cue |
| Pulse | radial shock burst, heavier impact flash |
| Prism | narrow beam, lens charge, refracted hit sparkle |

Every runtime wrapper should support:

- idle energy read;
- attack origin clarity;
- selected state;
- build preview;
- invalid placement state;
- optional upgrade/readiness accent later.

VFX should remain brighter than tower body detail. If the static tower already looks like it is constantly exploding, it will leave no room for gameplay feedback.

### Tower, creep, builder, and board contrast

The board has four major visual classes. They must not fight each other.

| Class | Visual role |
| --- | --- |
| Towers | anchored, heavier, more vertical, player-owned |
| Creeps | smaller, lower, moving, readable as threats |
| Builder | friendly mobile avatar, distinct from creeps, construction-focused |
| Board/path | readable container, lower contrast than gameplay objects |

Rules:

- towers should be visually heavier than creeps but not so large that they block path state;
- creeps should remain readable when passing a tower;
- builder should not be mistaken for a creep or tower projectile;
- projectiles and hit VFX should be the brightest moving information;
- ownership tint should be visible but not paint the whole asset.

### Art acceptance gates

A 3D tower is acceptable only if it passes all of these:

- [ ] Role silhouette is obvious at phone scale.
- [ ] Tower belongs visually to the same family as Arrow.
- [ ] Tower remains readable in grayscale.
- [ ] Tower does not overpower creeps or builder.
- [ ] Tower does not obscure build cells, path state, endpoints, or HUD.
- [ ] Static material leaves room for VFX to be brighter.
- [ ] Runtime wrapper follows the prefab contract.
- [ ] Screenshot contact sheet passes review.
- [ ] The 3D version is clearly better in-game than the AIPlate fallback.

## Proposed Implementation

### 1. Create a reusable tower 3D import pipeline

Target script:

`Assets/Editor/Tower3DImportPipeline.cs`

Responsibilities:

- load a raw generated/imported prefab;
- validate that it has mesh renderers/filters;
- create/update runtime wrapper prefab;
- create contract children;
- apply per-role rotation/scale/lift;
- apply board-safe material;
- disable shadows;
- strip colliders;
- write source notes;
- optionally update `TowerVisualLibrary.asset`.

### 2. Convert hard-coded Arrow behavior into data

The current Arrow-specific values should become one spec entry:

| Field | Arrow value |
| --- | --- |
| tower id | `tower.arrow` |
| role | `Arrow` |
| raw prefab | `Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d.prefab` |
| runtime prefab | `Assets/Prefabs/Towers/Tower_Arrow_3D.prefab` |
| scale | `1.36, 1.36, 1.36` |
| lift | `0.16` |
| import rotation | existing Arrow correction from proof script |
| anchors | `Muzzle`, `Lens`, `BowLeft`, `BowRight` |

### 3. Add specs for the remaining towers

Expected raw prefab staging paths:

| Role | Raw generated/imported prefab |
| --- | --- |
| Control | `Assets/Art/AIStaging/Models/Towers/Control/tower_control_3d.prefab` |
| Relay | `Assets/Art/AIStaging/Models/Towers/Relay/tower_relay_3d.prefab` |
| Pulse | `Assets/Art/AIStaging/Models/Towers/Pulse/tower_pulse_3d.prefab` |
| Prism | `Assets/Art/AIStaging/Models/Towers/Prism/tower_prism_3d.prefab` |

### 4. Keep promotion separate from wrapper generation

Wrapper generation should be safe and reversible.

Promotion should only happen after all five towers have acceptable runtime wrappers and screenshot review passes. The promotion command should continue to fail if any required 3D runtime prefab is missing.

### 5. Add validation commands

Minimum validation:

- raw prefab exists;
- raw prefab has at least one `MeshRenderer`;
- runtime wrapper exists;
- required contract children exist;
- required role anchors exist;
- `TowerVisualLibrary.asset` references the expected prefab when promoted;
- no colliders remain under visible runtime model;
- all renderers have board-safe material;
- shadows disabled;
- bounds fit expected one-cell footprint.

## Review Checklist

### Process

- [ ] Agree that Arrow is the canonical reference.
- [x] Agree that Unity AI/procedural generation is a possible source experiment, not the production pipeline.
- [x] Retire Unity/procedural/sprite-card auto-generation as a promotion path after Control visual rejection.
- [ ] Agree to keep raw generated assets in `AIStaging`.
- [ ] Agree that runtime wrapper generation and promotion are separate.
- [ ] Agree that promotion requires the full five-tower 3D set.

### Script work

- [x] Create `Tower3DImportPipeline.cs`.
- [x] Convert Arrow wrapper generation to use shared pipeline logic.
- [x] Add per-role specs for Arrow, Control, Relay, Pulse, and Prism.
- [x] Add validation for mesh presence, contract children, anchors, materials, shadows, and bounds.
- [x] Add source-note generation per tower.
- [x] Keep existing Arrow scripts until the new pipeline fully replaces them.

### Audit fixes

- [x] Add material-recipe support instead of one blunt shared material.
- [x] Preserve optional source albedo while normalizing shader, smoothness, emission, and shadow behavior.
- [x] Make range halo transparency explicit instead of relying only on alpha color.
- [x] Add real fallback `RoleMarker` and `OwnerTrim` geometry so runtime color hooks have visible targets.
- [x] Add import position and import scale fields so future raw 3D assets are not forced into Arrow-only assumptions.

### Art work

- [ ] Confirm Arrow 3D remains the style benchmark.
- [ ] Produce/import/source-clean Control polished 3D candidate.
- [ ] Produce/import/source-clean Relay polished 3D candidate.
- [ ] Produce/import/source-clean Pulse polished 3D candidate.
- [ ] Produce/import/source-clean Prism polished 3D candidate.
- [ ] Normalize all five through the shared material recipe.
- [ ] Capture normal and grayscale screenshots.
- [ ] Compare against current AIPlate tower baseline.

### Rejected experiments

- [x] Rejected procedural Control raw 3D candidate generated at `Assets/Art/AIStaging/Models/Towers/Control/tower_control_3d.prefab`; it tested as visually worse than the active AIPlate art and must remain staging-only until replaced.
- [x] Rejected hybrid mesh-card Control raw 3D candidate using `tower_control_candidate_v01_alpha.png`; it preserved the image better than primitives but still read as less detailed than the active AIPlate in-game.

### Acceptance

- [ ] All five towers read as one visual family.
- [ ] Each tower’s role is distinguishable at phone scale.
- [ ] Towers do not hide creeps, builder, path state, grid cells, or HUD.
- [ ] Grayscale role read survives.
- [ ] Runtime validation passes.
- [ ] Screenshot review passes before promotion.

## Open Review Questions

1. Should we keep one shared material for all 3D towers, or allow one material per tower role with strict palette limits?
2. Should generated normal maps be ignored by default, as Arrow currently does, or allowed when they materially improve read?
3. Should anchor positions be data-driven in the manifest, or created as zeroed placeholders first and hand-positioned later?
4. Should the first implementation produce only wrappers, or also auto-promote when all five pass validation?
5. Should this pipeline be tower-only for now, or designed so creep 3D/source-plate conversion can reuse it later?

## Near-Term Recommendation

Keep the shared Unity pipeline as a conservative intake and wrapper generator.

Do not auto-promote. Do not use procedural Control or mesh-card Control as gameplay art. The next real production test should be one vertical slice:

1. Choose Control tower as the first replacement target.
2. Use `docs/art-pipeline/source-asset-briefs/control-3d-source-brief-v01.md` to create or acquire a real `.fbx` Control source asset from external 3D generation, Blender modeling, or kitbash.
3. Clean it with `tools/art_pipeline/blender_prepare_tower_source.py`: pivot, scale, footprint, material names, and optional moving parts.
4. Stage it at `Assets/Art/AIStaging/Models/Towers/Control/tower_control_3d.prefab`.
5. Mark the Control spec promotable only after screenshot review proves it beats `Tower_Control_AIPlate.prefab`.

After that one slice works, repeat for Relay, Pulse, and Prism as a set.
