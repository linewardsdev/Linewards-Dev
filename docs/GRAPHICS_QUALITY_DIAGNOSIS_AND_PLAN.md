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

### Tier 2 — shading and pipeline

6. **Evaluate a URP migration.** Built-in can look good once Tier 1 lands. URP adds
   per-object shadow control, mobile post-processing, and shader graph authoring for the
   energy and owner materials. Decide after Tier 1, on evidence.
7. **Neutral or ACES tonemapping plus restrained bloom**, once emission is bound. Bloom
   is what makes emissive tower detail read.
8. **Author emission per tower role** so arrow, pulse, relay, prism and control separate
   at a glance by glow colour.
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
still open.

### Tier 3 — game board

10. **Replace primitive cells with a board mesh and tiling material.** Hundreds of
    primitives on default materials are both flat and costly.
11. **Ground every unit** with a blob shadow or contact decal. Under an orthographic
    camera this is the strongest available depth cue.
12. **Express lane state through material** — emissive strength or fill amount — rather
    than stacked cube geometry.
13. **Enable static batching and GPU instancing** on board geometry to fund the shadow
    cost added in Tier 1.

### Tier 4 — towers and creeps

14. **Re-export the Meshy drops with normal maps** and extend
    `blender_prepare_tower_source.py` to extract them.
15. ~~**Wire the five 3D creeps into `CreepVisualLibrary`**~~ — done 2026-07-25 via
    `Creep3DImportPipeline` and `Creep3DProofSetGenerator`. The `_AIPlate` prefabs and the
    126 MB `SourcePlates` folder are now unreferenced by the libraries and can be retired
    once the 3D creeps have been reviewed in a real match.
16. **Run a silhouette pass at true game scale.** Towers occupy roughly 100 px on a
    phone; confirm all ten read distinctly at that size in grayscale.
17. **Lower `maxTextureSize` to 1024 and stop committing 4096 sources.**
18. **Tint team colour through `_Color` on the owner material** instead of separate
    texture sets, so the treatment stays consistent as the roster grows.

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

## Working note

Prior art cycles iterated 2D source material to solve what is a lighting and material
binding problem. Before committing further art effort, verify the change under the Tier 1
setup — the existing assets have not yet been seen lit.
