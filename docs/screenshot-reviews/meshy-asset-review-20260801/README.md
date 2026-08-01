# Meshy asset review — Arrow tower and Spire Turret Walker (2026-08-01)

Review of the two Meshy-generated production assets, plus a proposed alternative for each
built procedurally in Blender through the new Blender MCP bridge.

Every number below is measured, not estimated. The Meshy meshes were imported into Blender
and counted; the on-screen sizes come from the project's own measurement in OPEN_ITEMS
item 11 (`screenshot-reviews/creep-leg-visibility/`).

## Method

All four renders use the **shipped camera**: orthographic, **30° tilt**
(`UnityVerticalSliceRenderer.DefaultActiveLaneTiltDegrees`). The game-size pass is rendered
at 235×134 so the walker lands at **46px tall** — the measured average height of a creep
that has an animator — then nearest-upscaled for inspection. That pass is the one that
matters; the poster render flatters everything.

## Measured

| | Meshy Arrow | Proposed Ward Arrow | Meshy Walker | Proposed Spire Walker |
| --- | ---: | ---: | ---: | ---: |
| Triangles | 15,323 | **1,124** | 14,737 | **1,008** |
| Vertices | 7,649 | 600 | 7,367 | 540 |
| Materials | 1 | 3 | 1 | 3 |
| Texture maps | 4 × 1024 | 0 | **7 × 1024** | 0 |
| Footprint (X×Y) | 1.45 × 0.85 | 0.91 × 1.05 | 0.72 × 0.90 | 0.74 × 0.44 |
| LODs | none | none | none | none |

## What is good about the Meshy assets

- **The palette and finish are genuinely strong.** Deep indigo body, warm gold trim, teal
  energy. It is original ward-tech, not Warcraft medieval — the brand boundary holds.
- **The gold trim is doing the real work.** It is the highest-contrast element and the one
  thing that survives downscaling on the tower. Any replacement must keep it.
- **One material and one UV set each** — one draw call, atlas-friendly. Better than what
  most generated assets give you, and better than my proposals, which use three.
- **The walker's silhouette is excellent at poster size** — an unmistakable arachnid, with
  a vertical spire that reads from directly above. It is the more characterful of the two.
- **The tower reads well at game size.** See `meshy_gamesize.png`: gold banding, teal
  chips and barrel direction all survive. The tower is not the problem asset.

## What needs improvement

**1. The triangle budget is inverted against on-screen size.** 14,737 triangles for a creep
that renders **46 pixels tall**, with **no LODGroup on either prefab** (verified: 0
occurrences in both). That is roughly 320 triangles per vertical pixel. The game has been
measured carrying 266 creeps at once.

**2. The walker's defining feature does not survive to screen.** This is the finding worth
acting on. In `meshy_gamesize.png` the eight thin dark legs dissolve into the dark board —
what remains is a teal smear. The silhouette that makes the asset good at poster size is
gone at the size players see. The project already measured the related effect: below ~35px,
leg articulation moves 2–4 pixels.

**3. Texture budget, especially the walker.** Seven 1024 maps including **three separate
1024 BaseColor maps at 2.2 MB each** — 6.6 MB of base colour for one small creep. There are
also redundant pairs: `texture_0_metallic` + `texture_0_roughness` alongside
`Baked_MetallicSmoothness`. The tower ships **both** `Baked_MetallicRoughness` and
`Baked_MetallicSmoothness` (1.4 MB each); one of the two is dead weight.

**4. The tower's barrel leaves its cell.** Footprint is 1.45 wide against a base of ~0.85 —
the barrel overhangs by more than half a cell. On a 7×16 mazing grid, a tower that visually
occupies cells it does not block is a placement-readability problem, not just an art one.

**5. Meshy bakes lighting into base colour.** The `Baked_*` maps carry their own AO and
highlights, which fight the game's actual lighting and are why these read flat under the
board's own key light.

**6. The base is the weakest part of the tower's silhouette** — a shapeless dome. See
`meshy_silhouette.png`: the barrel and hood carry the read, the base contributes nothing.

## The proposals

`LTW_WardArrow_Tower.fbx`, `LTW_SpireWalker_Creep.fbx`, source in
`ltw_proposed_assets.blend`. Built to the same normalisation targets the prep reports use
(tower 1.25 tall / 1.45 max footprint; creep 0.75 tall / 0.9 max footprint).

Design rules, each one derived from a failure above:

- **Silhouette before detail.** Stepped octagonal plinth on the tower, compact faceted body
  on the creep. Both hold a shape at 46px.
- **Four thick legs instead of eight thin ones.** Roughly 3× the limb width, splayed wider.
  This is the direct fix for finding 2 — compare the two `*_gamesize.png` files.
- **Emissive mass carried high and forward**, where nothing occludes it.
- **Gold rim lines on every step**, because that is what was already working.
- **Everything inside its cell.** The tower's barrels are short and splayed 17° rather than
  long and protruding.
- **No textures at all** — flat material colours. At 46px the maps were not resolving; this
  removes 6.6 MB and gets the same read.

## Honest assessment of the proposals

They are **more readable and ~14× cheaper, but less characterful**. The Meshy walker has
menace and craft that my four-legged version does not; mine reads a little like a lamp on
legs. The Meshy tower is a better *object* than mine — I would keep it and fix its
footprint and budget rather than replace it.

Three concrete caveats:

- **Three materials instead of one** is a regression on draw calls. Production would need
  these atlased into a single material before shipping.
- **No UVs, no rig, no animation.** These are silhouette and budget studies, not
  drop-in replacements.
- **Not seen in the game.** Nothing here has been through the Unity import pipeline or
  looked at on the actual board, so this shares the weakness the project's own R1 keeps
  naming. Treat the comparison as evidence about *silhouette and budget*, not as a
  finished art call.

**The recommendation is not "replace these with mine."** It is: keep Meshy for concepting,
then retopologise to a budget and rebuild the silhouette for 46px, using the proposals as
the target for what that budget looks like. The single highest-value change is a decimation
plus LOD pass on the existing meshes — that captures most of the cost win without losing
the character my versions gave up.

## Files

| File | Shows |
| --- | --- |
| `meshy_beauty.png` / `proposed_beauty.png` | Poster render, shipped camera |
| `meshy_silhouette.png` / `proposed_silhouette.png` | Flat silhouette pass |
| `meshy_gamesize.png` / `proposed_gamesize.png` | **46px render, upscaled — the decisive comparison** |
| `LTW_*.fbx`, `ltw_proposed_assets.blend` | Proposed meshes and source |
