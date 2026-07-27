# Graphics Quality Diagnosis And Improvement Plan

Recorded 2026-07-25, after the AI 3D asset pipeline landed five tower meshes and five
creep meshes from the Meshy intake.

## Summary

The Meshy 3D assets look markedly worse in game than they do in the source previews.
The cause is not the meshes or their textures. It is the scene and material setup around
them: **the game contains no lights**, runs in **Gamma color space**, and its runtime
materials **discard two of the three maps the intake produces** while explicitly disabling
specular highlights and reflections.

A physically based mesh lit only by flat ambient has no diffuse gradient, no specular
response, and casts no shadow. Every cue that makes it read as a three-dimensional object
is absent, so it renders as a flat image of itself. Extended 2D art iteration cannot
recover that, which is why previous art passes produced limited improvement.

## Verified findings

Each item below was confirmed by reading project files, not inferred from appearance.

### 1. The scene has no lights and no camera

`Assets/Scenes/LocalVerticalSlice.unity` contains zero `Light` components and zero
`Camera` components. The camera is constructed at runtime in
`Assets/Scripts/Simulation/LocalVerticalSliceLauncher.cs` (`CreateCamera`, line 68). That
method never creates a light and never assigns `RenderSettings`.

The only illumination in the game is ambient, configured as:

```yaml
m_AmbientMode: 0                 # Skybox
m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}
m_AmbientIntensity: 1
```

Flat, uniform, directionless. This is the single largest contributor to the quality loss.

### 2. Runtime tower materials discard most of the intake

In `Assets/Art/Towers/Production/Materials/mat_tower_arrow_3d_body_runtime_v01.mat`, and
identically in the Control equivalent:

| Slot | State |
| --- | --- |
| `_MainTex` | bound |
| `_MetallicGlossMap` | **not bound** — `Baked_MetallicRoughness.png` is unused |
| `_EmissionMap` | **not bound** — `Baked_Emit.png` is unused |
| `_BumpMap` | not bound — no normal map exists in the set |

Shader keywords are `_GLOSSYREFLECTIONS_OFF` and `_SPECULARHIGHLIGHTS_OFF`, with
`_Glossiness: 0.12` and `_Metallic: 0`. Specular response and reflections are switched
off at the material level. The likely history is that these were disabled to suppress
artifacts that were actually symptoms of finding 1.

### 3. Project renders in Gamma color space

`ProjectSettings.asset` has `m_ActiveColorSpace: 0`. Physically based shading assumes
linear light transport; in Gamma the response curve is wrong and output reads washed out
and plastic. This is the standard cause of "correct in the DCC tool, flat in Unity".

### 4. MetallicRoughness maps import as sRGB

The `.meta` for `Baked_MetallicRoughness.png` carries `sRGBTexture: 1`. Roughness and
metallic are data, not color, and must import linear. Binding the map without correcting
this produces wrong values.

### 5. Android ships a lower quality tier than the editor previews

`QualitySettings.asset` sets `m_PerPlatformDefaultQuality: Android: 2` — the "Medium"
level, with `antiAliasing: 0`, `shadows: 1`, `shadowCascades: 1`, and
`realtimeReflectionProbes: 0`. The editor previews at level 5. Device output is therefore
worse than anything reviewed on desktop.

### 6. The tower body materials were configured as transparent

Found while implementing Tier 1. Each `*_3d_body_runtime_v01` material carried
`m_CustomRenderQueue: 3000`, `RenderType: Transparent`, `_SrcBlend: 5`, `_DstBlend: 10`
and `_ZWrite: 0`, plus a `_SURFACE_TYPE_TRANSPARENT` entry under `m_InvalidKeywords` —
a URP keyword that has no meaning to the Built-in Standard shader it actually uses.

Solid tower meshes were therefore drawn alpha-blended with depth writes disabled, which
costs both their form and their sort order against the board.

### 7. Materials were bound to textures inside the .fbm importer cache

The `_MainTex` slots referenced texture GUIDs living in `*.fbm/` folders. Unity generates
those folders when it unpacks FBX-embedded media and regenerates them — with fresh GUIDs
— on reimport, so the bindings could not survive an import cycle. They now point at the
stable `*_Textures/` sets the intake writes.

### 8. The visual review tool rendered the wrong prefabs

The most consequential finding, and the reason prior art cycles could not converge.

`VisualReviewCaptureRunner.RenderRoleContactSheet` instantiated `Tower_Control.prefab`,
`Tower_Relay.prefab`, `Tower_Pulse.prefab` and `Tower_Prism.prefab` — the pre-3D primitive
prefabs — while `TowerVisualLibrary` loads the `_3D` Meshy prefabs at runtime. It also
built its own scene, camera and single directional light, bypassing the runtime setup.

Every screenshot review captured through the tool therefore showed placeholder geometry
under lighting the game does not use. Art was being judged against something the player
never sees. Fixed, with before and after captures in
[the Tier 1 evidence folder](screenshot-reviews/tier1-lighting-pass/).

### 9. Only the towers reached the game; the creeps did not

`CreepVisualLibrary` loads `Creep_*_AIPlate` for all five roles, so the Brute, Runner,
Shade, Siege and Swarm meshes from the intake are present in the repository but absent
from the build. Half the 3D investment is not yet in the game.

### 10. Supporting observations

- **No normal maps.** The intake extracts BaseColor, MetallicRoughness and Emit only, so
  all surface detail is flattened into albedo. See `tools/art_pipeline/blender_prepare_tower_source.py`.
- **The board is untextured primitives.** `UnityVerticalSliceRenderer.cs` builds lane
  cells, meters, pylons and rails from `GameObject.CreatePrimitive` on default materials;
  board plates use `Unlit/Transparent` (line 1646).
- **Source textures are 4096²** while the importer clamps to `maxTextureSize: 2048`. The
  4K originals add repository weight with no rendered benefit at phone scale.
- **The 2D creep plates are still live.** `CreepVisualLibrary` references
  `Creep_Runner_AIPlate` and `Creep_Shade_AIPlate`, so `Assets/Art/AIStaging/SourcePlates`
  (126 MB) cannot be retired until the 3D creeps are wired in.

## Improvement plan

Ordered by rendered improvement per unit of effort.

### Tier 1 — foundation

Expected to account for most of the visible gap. Small, reversible changes.

1. **Add a three-point light rig at runtime.** Key directional light (warm, intensity
   ~1.2, pitched ~50°, shadows enabled), fill (cool, ~0.35, opposing), rim/back (~0.5)
   for silhouette separation against the board.
2. **Switch to Linear color space.** Requires one retune pass over board and UI tints;
   do that immediately after, as its own change.
3. **Bind `_MetallicGlossMap` and `_EmissionMap`, clear `_GLOSSYREFLECTIONS_OFF` and
   `_SPECULARHIGHLIGHTS_OFF`, raise `_Glossiness`.** Recovers the material variation the
   intake already produces.
4. **Set `sRGBTexture: 0` on every MetallicRoughness map.** Prerequisite for item 3
   reading correctly.
5. **Replace flat ambient with a gradient** — warm sky, neutral equator, cool ground.
   Approximates bounce grounding at negligible cost.

### Tier 2 status — all four items now closed, 2026-07-26

**Item 9, Android quality (2026-07-25),** later superseded by the URP asset. Android mapped
to the Quality-settings Medium level, which allowed a single pixel light while the runtime
rig is three directional lights, so on device the fill and rim degraded to vertex and
spherical harmonic contributions. First fix raised Medium to three pixel lights, MSAA 2x and
two shadow cascades. Once the URP migration landed, the mobile light/shadow budget moved
onto the URP pipeline asset directly (MSAA 2x, 1024 shadowmap, 4 additional lights/object)
— see [URP_MIGRATION.md](URP_MIGRATION.md) — which is the real fix; the Quality-settings
change was a stopgap for Built-in and no longer does anything now the project renders URP.

**Item 8, per-role emission (2026-07-25) — now genuinely done, see fix below.**
Measured across the five baked emission maps, coverage runs 2.5 to 15 percent but the mean
colour is near neutral cyan-grey on every tower, so the maps carried no role identity.
`_EmissionColor` was tinted per role on `mat_tower_*_3d_body_runtime_v01.mat`.

**Correction, 2026-07-26:** this tinting had never actually rendered on any tower. Found
while investigating a URP contact-sheet artifact (see
[URP_MIGRATION.md](URP_MIGRATION.md) Known follow-up): all five tower specs passed
`preserveSourceMaterials: true`, so the tuned body material was created but never assigned to
the visible mesh — the mesh rendered with Unity's unconfigured, auto-generated FBX import
material instead. The glow visible in the match capture referenced below was that
auto-material's own uncalibrated full-white emission, not the per-role tinting described
here. Creeps were unaffected — their import pipeline has no equivalent branch and their
per-role tinting does render.

**Fixed, 2026-07-26:** flipped `preserveSourceMaterials` to `false` and regenerated. A second,
independent bug surfaced during the fix — regenerating unconditionally reset the tuned body
material back to generic defaults regardless of this flag — and was fixed in the same pass
(`Tower3DImportPipeline.CreateBodyMaterial`; full detail in `URP_MIGRATION.md`'s Known
follow-up). Verified at the data level (prefab references the tuned material's guid) and
visually (role contact sheet + real match capture now show genuine per-role tuned emission
and metallic detail, saved to `docs/screenshot-reviews/tower-material-fix/`). Item 8 is now
done for both towers and creeps.

The role-colour collision noted at the time — `TowerMarkerColor` giving control and prism
the same pale blue, and placing relay and pulse within ~15 degrees of hue — was fixed
separately by introducing `TowerRolePalette` as the single source of truth for role colour,
consumed by both the board renderer and the HUD build cards. That fix is real and does apply
(it governs `RoleMarker`/`OwnerTrim`, which are separate small accent meshes generated
directly, not part of the imported body mesh). See the 2D-plate-era constant class-of-bug
section below for the related creep fixes from the same pass.

**Items 6 and 7, URP and bloom (2026-07-26).** The migration itself is done and verified —
see [URP_MIGRATION.md](URP_MIGRATION.md) (match luminance 0.2113 vs 0.2142, no blown pixels).
Bloom and tonemapping work correctly on whatever is actually lit and emissive in the scene,
and now that towers render their intended per-role emission (item 8, above), that is what
bloom is reacting to.

**Tier 2 is complete** — items 6, 7, 8 and 9 all hold. One open item remains, tracked in
`URP_MIGRATION.md`'s Known follow-up section: bloom's real cost, which has not been measured
on a physical device (no device access).

### Tier 2 — shading and pipeline

6. ~~**Evaluate a URP migration.**~~ — done 2026-07-26. See
   [URP_MIGRATION.md](URP_MIGRATION.md) for the full tracker. Migrated to URP 17.5.0 and
   merged to `main`. Surfaced and fixed several latent bugs Built-in had been hiding: a
   letterboxed capture viewport, a silently-broken review harness, runtime shaders
   resolving to null, and emission keywords being stripped by an `EmissiveIsBlack` flag
   that Built-in tolerated but URP's validation does not.
7. ~~**Neutral or ACES tonemapping plus restrained bloom**~~ — done 2026-07-26 as part of
   the URP migration. Neutral tonemapping, bloom threshold 1.05, intensity 0.9. Confirmed
   emissive tower detail now reads visibly better than the Built-in baseline.
8. ~~**Author emission per tower role**~~ — done 2026-07-26, see the correction and fix
   above: the tuning existed since 2026-07-25 but never rendered until the tower
   material-assignment bug was fixed.
9. **Give Android a dedicated quality level**: `antiAliasing: 2` (MSAA 2x is inexpensive
   on tile-based mobile GPUs), `shadowCascades: 2`, `shadowResolution: 1`.

### Tier 3 status, 2026-07-25

Items 10, 11 and 13 landed. The board is now baked into one vertex-coloured mesh per lane
by `BoardMeshBuilder`, and towers and creeps carry soft contact shadow decals. Measured on
the eight-lane board: **1730 renderers before, 152 after**, with 1586 pieces baked.

Construction was deliberately left alone. Pieces are still positioned by the existing code
and only then recorded and merged, so the baked geometry is positionally identical to the
primitives it replaced. Animated elements stay separate via `CreateLiveBoardPiece`.

**Watch for this when baking colours.** Vertex colours must be decoded to linear through
`BoardMeshBuilder.ToRenderSpace`. In linear colour space a colour assigned through
`Material.color` is sRGB-decoded on its way to the GPU, but a colour written into the
vertex stream is not, so baking authored values unchanged renders the board roughly twice
as bright. `AddBox` and `AddMesh` apply the decode themselves so no caller can forget.

Item 12, expressing lane state through material rather than stacked cube geometry, is
now also done — see below.

### Tier 3 — game board

10. **Replace primitive cells with a board mesh and tiling material.** Hundreds of
    primitives on default materials are both flat and costly.
11. **Ground every unit** with a blob shadow or contact decal. Under an orthographic
    camera this is the strongest available depth cue.
12. ~~**Express lane state through material**~~ — done 2026-07-26. The lane pressure gauge
    (`UnityVerticalSliceRenderer.UpdateLanePressureIndicators`) used to rescale a cube's Z
    every tick to show fill level — geometry changing every frame, which defeats static
    batching, is exactly the "stacked cube geometry" this item named. Replaced with a
    fixed-size quad (`BoardRenderResources.FillBarMesh`/`FillBarMaterial`) and a new
    `LTW/Fill Bar` shader (`Assets/Resources/Shaders/LTWFillBar.shader`) that reads fill as
    a threshold along the mesh's U coordinate, driven per-lane through a
    `MaterialPropertyBlock` rather than by touching the shared material. Verified the scale
    never changes with pressure via a temporary diagnostic (logged fixed `(8.64, 1, 0.16)`
    across pressure 0 through 25) and that `_Fill` varies correctly (0 → 1.0 across the
    pressure range) before removing the diagnostic. Also had to fix a real shader bug found
    during verification: the fragment stage never received the vertex stage's instance ID
    (`UNITY_TRANSFER_INSTANCE_ID` was missing), which failed to compile on Metal — fixed
    before this could ship broken. The gauge's empty-state background color was also bumped
    from near-transparent to a visible opaque housing color, since the original was nearly
    invisible against the dark board and made an empty gauge look like it wasn't there at
    all rather than reading as "empty."
13. **Enable static batching and GPU instancing** on board geometry to fund the shadow
    cost added in Tier 1.

### Tier 4 — towers and creeps

14. **Re-export the drops with normal maps.** Note the earlier wording here was wrong: the
    prepare script needs no extension. `export_packed_images` already exports every packed
    image generically, so a normal map would come through if one existed. No drop has ever
    contained one, which is a generator export setting rather than a pipeline gap. The
    intake now reports `has_normal_map` on the scorecard so this stops passing unnoticed.
    Re-checked 2026-07-26: this remains a Meshy-generation-time decision, not something
    fixable in this codebase — either re-run generation with normal maps requested, or
    accept flat shading. A synthetic height-derived normal bake was considered and rejected
    without trying it, since faking one is a real visual/artistic tradeoff that shouldn't be
    made silently.
15. ~~**Wire the five 3D creeps into `CreepVisualLibrary`**~~ — done 2026-07-25 via
    `Creep3DImportPipeline` and `Creep3DProofSetGenerator`. The `_AIPlate` prefabs and the
    127 MB `SourcePlates` folder have now been retired (2026-07-26): both were unreferenced
    by the libraries, confirmed unreferenced by any other code path, and deleted along with
    `AiSourcePlateProofGenerator.cs` (their only remaining generator/validator) and the dead
    `CaptureAiProofGameplayReviewSet` capture path in `VisualReviewCaptureRunner.cs` that
    still pointed at two of the AIPlate prefabs.
16. ~~**Run a silhouette pass at true game scale.**~~ — done 2026-07-26. Captured the
    true-scale role lineup scenario (`CaptureRoleLineupReviewSet`, real gameplay proportions,
    not the contact sheet's per-unit close-up framing) in grayscale and cross-checked against
    a grayscale role contact sheet. All ten units read as distinct shapes at both close-up
    and true relative game scale. One near-miss investigated and ruled out: the Arrow tower
    appeared to wash into an indistinct glowing blob in the lineup capture, but this turned
    out to be the automated test scenario placing Arrow at row 14 of 16 — almost directly on
    the spawn gate tile — so it was overlapping the gate's own pulse/glow decoration, not a
    defect in Arrow's silhouette. Confirmed by disabling effects (the blob and banner both
    disappeared together) and by Arrow reading fine in every other capture this session.
    Separately, decided the Runner creep's flat, elongated "blade" silhouette (0.90 long by
    0.23 tall, noted as an open question in the Tier 4 creep-wiring log below) should be kept
    as-is: it reads as clearly distinct from the other four creeps at both scales checked,
    and the flatness reads as a legitimate "fast unit" silhouette rather than a defect, so
    re-sourcing the model isn't worth spending Meshy generation credits on.
17. ~~**Lower `maxTextureSize` to 1024 and stop committing 4096 sources.**~~ — done
    2026-07-26. All 33 raw `Baked_BaseColor`/`Baked_Emit`/`Baked_MetallicRoughness` sources
    across the five towers and five creeps were 4096×4096 and directly referenced by the
    runtime materials (there was no separate, smaller repacked copy). Downsized in place to
    1024×1024 (488 MB → 61 MB in `Assets/Art/AIStaging/Models`), set `maxTextureSize: 1024`
    on every affected `.meta` (default and all platform overrides), and capped
    `blender_prepare_tower_source.py`'s `export_packed_images` at 1024 so future intake drops
    come in at this size automatically instead of needing a manual downsize pass.
18. ~~**Tint team colour through `_Color` on the owner material**~~ — checked 2026-07-26,
    already done. `UnityVerticalSliceRenderer.SetColorInChildren` already tints via
    `renderer.material.color` (which URP resolves to `_BaseColor`), and there are no
    separate per-owner texture sets anywhere in the project to replace. No change needed.

## Status

Tier 1 was implemented on 2026-07-25.

| Item | State | Where |
| --- | --- | --- |
| 1. Three-point light rig | done | `LocalVerticalSliceLauncher.CreateLightRig` |
| 2. Linear colour space | done | `ProjectSettings.asset`, `m_ActiveColorSpace: 1` |
| 3. Bind metallic and emission, restore opaque shading | done | `mat_tower_*_3d_body_runtime_v01.mat` |
| 4. Linear import for data maps | done | `Baked_Metallic*.png.meta`, `sRGBTexture: 0` |
| 5. Gradient ambient | done | `LocalVerticalSliceLauncher.ApplyGradientAmbient` |

Supporting change: `tools/art_pipeline/repack_metallic_smoothness.py` converts the glTF
metallic-roughness packing into Unity's metallic-smoothness layout, at 1024 by default.
Fold it into the intake so future drops arrive already converted.

### First in-match review, 2026-07-25

Captured through `Line Wards/Review/Capture Visual Review Set`. Evidence in
[the gameplay folder](screenshot-reviews/tier1-lighting-pass/gameplay/).

**The linear switch darkens the board; it does not brighten it.** Earlier notes in this
document predicted the opposite. Converting gamma-authored colour values to linear pulls
midtones *down* — a cell authored at 0.2 now resolves near 0.03 — so the board reads as an
undifferentiated dark field. The retune must raise board values, not restrain them.

What the capture confirmed:

- **Towers read correctly.** Lit, metallic, with real form. This is the Tier 1 payoff.
- **Creeps are far too small.** A swarm creep occupies roughly 40 px against a builder that
  reads clearly at about three times that. The runtime scales were derived from model
  geometry and validated on the role contact sheet, which frames isolated prefabs close up.
  The match camera is orthographic at size 15.5 across eight lanes, so the contact sheet
  could not surface the problem. **Validate unit scale against a match capture, never
  against the contact sheet.**
- **The board is now the dominant weakness.** It is built from `CreatePrimitive` cubes on
  default materials, so there is no surface detail for the new lighting to reveal, and the
  linear conversion pushed it darker still.
- **The `1x RUNNER` overlay draws behind the spawn gate panel**, a HUD z-order defect
  unrelated to lighting.
- The violet marker on the lane is `WardViolet`, an intended colour constant, not a
  missing-material placeholder.

### Follow-up pass, same day

Both problems above were addressed and re-verified against a fresh match capture.

**Creep scale.** `UnitBoundsReport` was added to log runtime prefab bounds, since unit
scale can only be judged against the match camera. It showed the shortfall was height, not
width — widths already approached a full cell, so the uniform 1.6-1.8x increase first
proposed would have pushed siege and runner wider than the lane they walk. Scales are now
derived from measured height: creeps sit at 0.64 to 0.70 against towers at roughly 0.74.

**Board palette.** All board colour helpers now route through `BoardSurface`, which lifts
values authored for gamma. The exact inverse-gamma conversion proved too strong — it
turned night stone into pale concrete and made the buildable slots read as holes — so the
helper blends part of the way via `BoardSurfaceLift`, currently 0.35. That constant is the
single dial for board brightness; raise to brighten, lower to darken.

**Runner** was resolved by pitching the imported mesh 35 degrees nose-up rather than by
scale, which could not help once width was capped at the lane. Measured height went from
0.230 to 0.448 with width unchanged. Set the spec's `ImportEulerAngles` back to
`Vector3.zero` to return it flat, or re-source the model if the banked read is unwanted.

Rotating about the model base swings geometry below the board, so
`Creep3DImportPipeline.SeatOnGround` now reseats each imported mesh on y = 0 from its
measured bounds. Any future spec can request a rotation without sinking.

**The send banner z-order defect** is fixed. Floating text is a world-space `TextMesh`
whose renderer defaults to sorting order 0, while endpoint gate plates draw through
`SpriteRenderer`s at sorting order 3. Both live in the transparent queue, so the gate drew
last and covered the banner. Floating text now sorts at `FloatingTextSortingOrder`, above
all board decoration.

### Creep 3D wiring, 2026-07-25

All five creep meshes now load through `CreepVisualLibrary`. Two notes for whoever picks
this up next:

- **Runtime scales are estimates.** They come from the prepared model heights in the
  intake reports, not from play testing, and place creeps at roughly three quarters of
  arrow tower height. Check them against the real board.
- **Runner reads as a low blade.** Its prepared bounds are 0.90 long by 0.23 tall, so it
  sits much flatter than the other four. That is the mesh, not the wrapper. Either accept
  it as the silhouette or re-source the model.

## A class of bug: constants tuned for the 2D plates

Recorded 2026-07-26 after four separate defects turned out to share one cause.

The creep visuals were originally flat 2D plate prefabs, scaled non-uniformly at roughly
`(0.9, 0.54, 1.2)`. Anything positioned relative to a creep was hand-tuned against that
shape. The 3D wrappers scale uniformly at roughly 1.0, so every one of those constants now
resolves somewhere it was never meant to:

| Constant | Tuned for plates | Result on 3D meshes |
| --- | --- | --- |
| Swarm and brute lateral motion offset, `0.16` | nudge stopping billboards overlapping | creep sits visibly off lane centre |
| Creep `GroundShadow` local offset, `-0.42` | just above the board | below the surface, never rendered |
| `CreepHealthBarMetrics` heights | bar just above the plate | 0.91x to 2.03x of creep height |
| Board palette authored near 0.05 albedo | visible stonework under gamma | near black under linear |

**When fixing one of these, prefer deriving the value over retuning it.** The health bar
now measures the creep body rather than carrying a per-role constant, so it survives the
next scale change; retuned constants would not. Treat any remaining hand-tuned offset that
positions something relative to a creep as suspect until checked against a match capture.

## Working note

Prior art cycles iterated 2D source material to solve what is a lighting and material
binding problem. Before committing further art effort, verify the change under the Tier 1
setup — the existing assets have not yet been seen lit.
