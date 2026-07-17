# Mobile Art UI/Board Reference Promotion Review

Date: 2026-07-17

Package: `GD-Mobile-UI-Board`

Capture:

- Path: `docs/screenshot-reviews/mobile-art-ui-board-reference-promotion-pass/`
- Phase: `after`
- Seed: `4`
- Intensity: `aggressive`
- Frames: 126 PNGs, including color and grayscale mobile views

## Result

- Command cards now draw cropped selected-reference frames from command card option 4.
- HUD chrome and lane controls now have first-pass selected-reference texture hooks with procedural fallback.
- The board now draws option-11 reference material overlays for deep field, build bands, and route core while retaining procedural grid/readability details.
- Leak gate orientation and bottom alignment fixes remain included in this working set.

## Scores

| Area | Score | Notes |
| --- | ---: | --- |
| Command card target usage | 3/3 | Build/send cards visibly use the selected option-4 frame language. |
| Command card layout fit | 2/3 | Frames work, but labels and icons need a vertical spacing pass. |
| HUD target usage | 1/3 | Texture hook exists, but the selected module is too compressed in collapsed HUD state. |
| Control target usage | 1/3 | Texture hook exists, but the circular control language is too subtle at current button size. |
| Board material target usage | 2/3 | Option-11 texture language is present and restrained, but still reads as overlay proof rather than final tile material. |

## Follow-Ups

- Recompose command cards as dedicated runtime card assets sized for current mobile rectangles, then move labels/icons to avoid crowding.
- Create dedicated small circular control textures from option 1 instead of cropping from the full sheet.
- Recompose HUD option 6 into compact collapsed and expanded drawer states.
- Convert board material overlays into tileable slices and tune opacity against active combat.
