# Agent Scored Review: Endpoint Sprite Promotion

Date: 2026-07-17

Run ID: `mobile-art-endpoint-sprite-promotion`

## Summary

The selected spawn/leak gate reference art is now used by the runtime board renderer. This closes the gap where automation compared against option 11 but Unity still rendered procedural endpoint primitives.

## Runtime Changes Verified

- Added selected option 11 endpoint sprites under `Assets/Resources/Art/Board/Endpoints/`.
- Added `BoardEndpointSpriteImporter` so endpoint PNGs import as transparent Unity sprites.
- Updated `UnityVerticalSliceRenderer` to `Resources.Load<Sprite>` the selected spawn/leak endpoint art.
- Suppressed the previous primitive endpoint landmark/gate stack when both reference sprites load.
- Kept the procedural endpoint stack as a safe fallback if either sprite is missing.

## Evidence

- Board overview: `after/phone-standard-portrait/12-board-overview.png`
- Spawn focus: `after/phone-standard-portrait/13-spawn-gate-focus.png`
- Leak focus: `after/phone-standard-portrait/14-leak-gate-focus.png`
- Grayscale spawn focus: `after/phone-standard-portrait/grayscale/13-spawn-gate-focus.png`
- Grayscale leak focus: `after/phone-standard-portrait/grayscale/14-leak-gate-focus.png`

## Scores

| Category | Score | Notes |
| --- | ---: | --- |
| Target-reference usage | 3/3 | The chosen option 11 spawn/leak art is now loaded as actual runtime sprite art. |
| Endpoint readability | 2/3 | Spawn and leak are much more identifiable than the prior primitive blocks; lane direction remains readable. |
| Crop/matte polish | 1/3 | The crop is transparent and no longer a full rectangle, but edge matte, lighting match, and final footprint still need a dedicated polish pass. |
| Board integration | 2/3 | Sprites sit in the correct event zones and no longer depend on the procedural endpoint prop stack, but the board material still needs a broader authored-tile pass. |
| Grayscale readability | 2/3 | Endpoint silhouettes remain visible in grayscale; leak/spawn contrast can still be improved with cleaner edge treatment and stronger local value grouping. |

## Follow-Up

- Normalize endpoint crop matte with softer antialiasing and less opaque source-floor residue.
- Tune spawn/leak scale after testing live with creeps crossing endpoints.
- Generate or author final board material tiles so the gates feel native to the lane instead of promoted as isolated plates.
