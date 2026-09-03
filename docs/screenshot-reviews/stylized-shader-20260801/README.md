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

## In-game A/B, 2026-08-03 — the finding that split the defaults

Everything above this section was judged from editor renders at poster size. On 2026-08-03
the shader was finally compared **inside the running game**, flipping between stock and
stylized in one editor session with the camera held still. That is the first time this work
has been judged at the size players see, and it changed the design.

**Towers and creeps wanted opposite things from the same numbers.**

| Role | Before | After (single shared defaults) | Verdict |
| --- | --- | --- | --- |
| Arrow tower | deep saturated violet; outlines dissolve into the grid cells behind them | clear silhouette, legible plate rings — but violet washed out to silver-lavender | **too bright** |
| Turret walker | near-black voids; only the teal leg emissive identified them | carapace and legs legible, teal now sits *on* a visible body | **about right** |

The tower result was a genuine loss, not a nitpick: violet is the arrow line's identity, and
the shared defaults traded it away for readability it did not need as badly as the creeps did.
The walker result was the opposite — it fixed exactly the failure the rim and contour were
written for, a dark unit on a dark board with no edge.

The cause is that the two roles start from different places. **Creeps are dark objects that
need lifting; towers are mid-value objects that need their depth preserved.** One set of
numbers cannot do both, so `StylizedUnitMaterialMigration` now seeds per-role defaults chosen
by folder:

- **Creeps** keep the shipped values unchanged. They are working.
- **Towers** get `_RampStart` 0.30 → 0.40 (more of each surface stays in shade), `_SpecStrength`
  0.25 → 0.15 (the remaining brightness was specular on top), and a **violet** `_ShadeColor`
  and `_AOTint` in place of the neutral indigo. The shade colour is the one that matters most:
  neutral-cool was actively dragging purple toward grey across the whole shadowed half, so the
  shade was fighting the role's identity instead of deepening it.

**Two things this did not solve, stated plainly.**

The walker is now *readable*, which is a lower bar than *good*. The gold joints and panel work
visible in the poster render still do not survive at 46px, and no shader value will bring them
back — that is normal-map and LOD territory, or accepting that this size carries only
silhouette and value.

Only the arrow tower and turret walker have baked AO. The ring definition that made the tower's
dome read is partly AO, and the other 19 roles do not have it yet. Recommendation 3 of
`PATH_TO_AAA.md` is now the highest-value remaining work, ahead of any further shader tuning.

## Using it

**The tuning loop.** `LTW > Art > Stylized Units > A-B Compare` has two entries, BEFORE
(stock URP/Lit) and AFTER (stylized). Flipping to AFTER **re-seeds the current authored
defaults**, so the loop is: edit the constants in `StylizedUnitMaterialMigration`, let Unity
recompile, flip to AFTER, look. Both directions report a count to the Console and **it must
read 62 every time** — a changing count means the scope rules have drifted, which is how two
separate bugs were caught (see below).

Two practical traps, both hit during the 2026-08-03 session:

- **Unity does not recompile scripts while in Play Mode.** Flipping to AFTER right after
  editing a default will silently apply the OLD values. Exit Play Mode, wait for the reload,
  then flip.
- **Whichever side you leave it on is what is on disk and what gets committed.** Finish on
  AFTER unless you mean not to.

- `Line Wars > Review > Capture Units As Currently Authored` — one render of what is on disk.
- `LTW > Art > Stylized Units > 1. Report What Would Change (Dry Run)` — prints, writes nothing.
- `LTW > Art > Stylized Units > 2. Apply Migration` — applies; re-runnable and idempotent.
- `Line Wars > Review > Diagnose Unit Materials` — prints each renderer's material, shader,
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

## AO bound across the roster, 2026-08-04

`after_ao_bound_roster.png` is the same two units after occlusion was bound to all 30 roles
(P0 row 9 counted 2 of 119 materials before this). Compared with `after_stylized.png` above,
the tower's plate rings gain real crevice depth and the walker's joints separate from its
carapace — which is what section 4.1 predicted when it called AO "the single largest
contributor to units reading as solid objects rather than lit shapes".

The binding had been sitting undone since the bake: 30 maps existed on disk and 0 were
bound, because Unity held the editor bridge during the session that produced them.

**One bug worth remembering.** The first binding run reported success at 26 of 30. The four
it skipped — elder_canopy, repair_drone, thorn_snare, spore_cloud — disagree with their bake
about separators (`tower_eldercanopy_3d_ao_v01` vs `mat_tower_elder_canopy_3d_body_...`), and
a `Contains()` match cannot distinguish "this role has no AO" from "this role's AO is spelled
differently". It failed silently with a plausible-looking count. Matching now strips
separators — the third time that exact fix has been needed in this codebase, after
`Tower_OwnerTrim` in the stylized migration.
