# Shared stylized unit shader — 2026-08-01

Recommendation 1 of [`../meshy-asset-review-20260801/PATH_TO_AAA.md`](../meshy-asset-review-20260801/PATH_TO_AAA.md),
carrying out `GRAPHICS_AA_UPLIFT.md` section 4: 139 of 149 materials were stock `URP/Lit`
with zero authored shading, and the reference study concluded a shared stylized shader
across all units is a bigger visual jump than any per-asset work.

Built, applied to 62 unit materials, and verified in-engine. Unity 6000.5.3f1, batch mode.

## The most important finding

**The shipped Meshy assets are much better than the game makes them look.**
`before_shipped_urp_lit.png` is the first time either asset has been rendered in this
project at poster size under neutral lighting, and the arrow tower and turret walker are
both genuinely good — purple-and-gold plating with a glowing barrel, a teal-crystal spider
mech with gold joints and clean panel work.

That reframes the whole uplift. The problem was never that these assets lack quality; it is
that nothing in the runtime was showing it. Which is the same conclusion the uplift doc
reached from the reference frames, arrived at from the opposite direction.

## What is here

| File | Shows |
| --- | --- |
| `before_shipped_urp_lit.png` | The two units exactly as shipped, stock `URP/Lit`, nothing modified |
| `after_stylized.png` | The same units and lighting under `LTW/Stylized Unit` |

Both were rendered by `StylizedUnitPreviewCapture.CaptureCurrent`, which renders whatever
is on disk. The before was captured with the migration reverted (`git stash`) and the after
with it re-applied, so neither is a reconstruction.

## Honest read of the comparison

**The shader works.** Both units render correctly, gain a cool rim that separates them from
the board, and show stronger form definition on the tower's plating and the walker's legs.
The gold trim reads as metal through a gradient rather than a hotspot, which is what the
reference study asked for.

**The defaults are not yet right, and they cost something real.** The after is brighter and
flatter in colour than the before: the deep purples and blues that give these units their
mood are lifted toward neutral silver. The cause is in the migration defaults, not the
shader — `_ShadeColor` is too light and `_ShadeStrength` at 0.85 raises the shadow end far
enough to wash out saturation. **A tuning pass should pull `_ShadeColor` darker and more
saturated before anyone judges this against the shipped look.** The uplift doc's own warning
applies exactly here: these are "a starting bracket to tune by eye against a capture, not a
number to adopt on trust."

## Two bugs the verification caught

Both are recorded because each was found by looking rather than by reasoning, and the first
one nearly shipped.

**1. Restyling overlay materials destroyed the tower.** The first migration pass took all
113 matching materials, including role markers, sender accents, owner trim and energy
markers. Those are flat readability graphics, not unit surface, and giving them a shading
ramp and a rim made the arrow tower render as a **solid cyan blob** — its cyan energy marker
took over the whole model. The exclusion list now covers them and the pass migrates 62.
Kept as a rule worth remembering: a unit's material list contains UI as well as surface.

**2. A reconstructed "before" is worthless.** The first comparison built its before by
cloning `URP/Lit` materials off the migrated ones and force-enabling `_EMISSION`. That
produced a red cast present in no shipped configuration, and would have made the shader look
far better than it is. The fix was to render the real materials from disk in two passes.

## Using it

- `Line Wards > Review > Capture Units As Currently Authored` — one render of what is on disk.
- `LTW > Art > Stylized Units > 1. Report What Would Change (Dry Run)` — prints, writes nothing.
- `LTW > Art > Stylized Units > 2. Apply Migration` — applies; re-runnable and idempotent.
- `Line Wards > Review > Diagnose Unit Materials` — prints each renderer's material, shader,
  albedo and colour. This is what identified bug 1.

All four run under `-batchmode -executeMethod`; `Apply` skips its confirmation dialog there.

## Not done

- **No device or in-game verification.** These are editor renders at poster size. Nothing
  here has been seen at the 46–105px the units actually occupy, which is the size the whole
  uplift is ultimately judged at, and the project's own history warns repeatedly about
  concluding from measurement without looking.
- **The remaining recommendations 2–7 are untouched** — authored roughness with simplified
  albedo (`_AlbedoFlatten` ships at 0, deliberately), baked AO, LODs, normal bakes, contour
  separation, and lighting ownership.
- **AO is inert on most materials.** `_OcclusionStrength` is set to 0 wherever a material has
  no occlusion map, which is nearly all of them — item 6 established there is no AO on disk to
  bind. The shader's AO tint path is built and waiting on recommendation 3.
