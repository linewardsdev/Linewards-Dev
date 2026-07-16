# UI Board Art Pass V02 Review

Date: 2026-07-16

## Summary

This pass promotes the selected V0.2 UI/game-board directions into the runtime prototype as procedural slices:

- Command cards: selected option 4, already wired through `RuntimeUiChrome`.
- Persistent controls: selected option 1, already wired through `LaneViewToggleController`.
- HUD chrome/stat drawer: selected option 6, first compact dropdown treatment in `HudView` and the deterministic capture overlay.
- Board material kit: selected option 11, first slate/stone procedural material pass in `UnityVerticalSliceRenderer`.
- Spawn/leak gates: selected option 11, first circular platform/drain runtime pass in `UnityVerticalSliceRenderer`.

## Evidence

Capture command:

`Unity.exe -batchmode -projectPath unity/LTW.UnityClient -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureVisualReviewSet -ltwCaptureOutputDir docs/screenshot-reviews/ui-board-art-pass-v02/captures -ltwCaptureGrayscale -ltwExitAfterCapture`

Captured states:

- `captures/01-default-hud.png`
- `captures/02-build-menu-open.png`
- `captures/03-send-menu-open.png`
- `captures/04-lane-selector-open.png`
- `captures/05-active-combat.png`
- `captures/06-heavy-pressure.png`
- `captures/07-reduced-effects-heavy.png`
- `captures/08-results-or-late-match.png`
- `captures/grayscale/`

## Findings

| Severity | Area | Finding | Follow-up |
| --- | --- | --- | --- |
| Medium | HUD polish | Compact HUD is much closer to the dropdown direction, but text spacing is still tight in the deterministic overlay and needs a hand-tuned typography pass. | Reduce stat density or move pressure/time into the expanded drawer. |
| Medium | Board brightness | The board now reads more like slate material, but the lane may be slightly dark in default view. | Tune route/build values after live playtesting on the target screen. |
| Medium | Spawn visibility | Spawn platform is visible but partially competes with the top HUD/camera crop. | Revisit camera top padding and endpoint scale after live review. |
| Low | Leak gate | Leak drain is a visible improvement over the old block, but it still needs a stronger "life loss" read during motion. | Add a short leak animation/VFX pulse later. |
| Low | Icon family | Runtime still uses the existing `Resources/Art/UI/Icons` files; procedural fallback glyphs are secondary. | Replace icons only after command-card sizing is fully stable. |

## Status

Pass with polish follow-ups. The selected UI/board directions are now represented across the runtime screen, but the HUD typography, board brightness, and endpoint framing need additional live tuning.
