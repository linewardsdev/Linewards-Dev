# The top HUD bar: three faults

2026-08-03. Reported as "weird background behind it, it's not the full width, the blue boxes
look weird" — three separate causes.

## 1. The background was a 1.90-aspect panel stretched to 6.92

`DrawHudFrame` drew `ui_hud_chrome_option_06` through `DrawChromeTexture`, whose default is
`ScaleMode.StretchToFill`. That texture is **512x270, aspect 1.90**. The strip's aspect is
**6.92**. So it was stretched **3.65x horizontally**, which is why the concentric rings authored
into the plate arrived as long smears behind the readout.

No scale mode fixes it. `ScaleToFit` letterboxes a plate that has to span the strip;
`ScaleAndCrop` shows a slice of the middle and discards the authored edges. The asset would need
nine-slicing, or redrawing at the strip's aspect, to work here. Widening the strip (below) takes
the stretch to about 4.2x, so it gets worse as the layout gets more correct.

Now drawn procedurally — the path that was already sitting underneath as the fallback. Flat,
consistent with every other HUD panel, correct at any width.

## 2. It was 736px wide in an 853px dock

`CompactTopHudRect` clamped to `358 * scale` and centred the result. `TopHudRect` had already
taken the edge margin off both sides, so the clamp was a second and tighter inset on top of it.

Measured at the 1080x1920 capture surface: `UiScale` resolves to **2.060** (it references a
430x932 surface, which is 9:19.5 — the same portrait target as the shell screens), `TopHudRect`
allots **853**, and the clamp gave **737.5**. Measured plate before: **736**. After: **852**.

Not a device artefact. On the 430-point design surface the margin leaves 414 and the clamp threw
56 of them away, so the strip was narrower than its own dock everywhere — which is what left the
board's corner pylons stranded in the gap either side of it.

`DrawHudHeader` needed no change: LINE and LIVE are `min(fixed, proportion)` and settle on their
fixed widths, so the gain goes to the centre readout, which is the element that wanted room.

## 3. The blue boxes were bare cubes standing on the board

`CreateCornerPylon` built a **0.22 x 0.38 x 0.22 cube at y −0.08**, so it stood 0.11 proud of a
board surface at y 0 — untextured, flat-shaded, taller than it was wide, floating just off the
plate edge. At the shipped camera that is a solid blue rectangle with no relationship to anything
near it.

`CreateLaneFlowTickMarks`, immediately below it in the same file, already carries this exact
finding about a different element — *"tilted cubes sitting proud of the gutter ... read as loose
blue shards stuck to the board edge rather than as trim"* — and was cut back to full detail only
because of it. The reasoning was never applied to the pylons.

Now **0.30 x 0.045 x 0.30 at y −0.012**: a flat chip lying on the plate, dimensioned like the
surface bands this file uses for trim everywhere else.

**Caveat, because it is not what it looks like.** In `04-pylon-after.png` the remaining blue reads
as a tidy L-shaped bracket around the bar's corner. That is *occlusion*, not design — the widened
strip now covers the chip's upper-right. It happens to look deliberate at this surface and may not
at another. The chip itself is the fix; the bracket is a coincidence, and worth knowing before
anyone relies on it.
