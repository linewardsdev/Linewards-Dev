# UI Board Art Pass V02: Persistent Controls

Date: 2026-07-16

## Status

Pass with behavior caveat.

## Implemented

- Added reusable option-1-inspired control chrome in `RuntimeUiChrome`.
- Updated `LaneViewToggleController` to use the shared persistent control treatment.
- Enlarged the lane selector touch targets and grouped the expanded lane options.
- Updated `VisualReviewCaptureRunner` deterministic lane-selector overlay to match the V02 control direction.

## Selection Source

- Selected direction: board controls option 1.
- Selection doc: `docs/art-pipeline/ui-board/selected-candidates-v02.md`.
- Candidate crop: `docs/art-pipeline/ui-board/selected-candidates/controls_option_01.png`.

## Capture Evidence

Command:

`Unity.exe -batchmode -projectPath unity/LTW.UnityClient -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureVisualReviewSet -ltwCaptureOutputDir docs/screenshot-reviews/ui-board-art-pass-v02-controls/captures -ltwCaptureGrayscale -ltwExitAfterCapture`

Key captures:

- `captures/04-lane-selector-open.png`
- `captures/grayscale/04-lane-selector-open.png`

Full set:

- `captures/01-default-hud.png`
- `captures/02-build-menu-open.png`
- `captures/03-send-menu-open.png`
- `captures/04-lane-selector-open.png`
- `captures/05-active-combat.png`
- `captures/06-heavy-pressure.png`
- `captures/07-reduced-effects-heavy.png`
- `captures/08-results-or-late-match.png`

Log:

- `docs/screenshot-reviews/ui-board-art-pass-v02-controls/unity-capture.log`

## Observations

- The lane selector now reads as a persistent control cluster instead of a temporary flat rectangle.
- The active state has a clear filled face; inactive options remain visible but secondary.
- The grouped control sits off the active lane and does not block the build/send triggers.

## Caveat

This pass improves the persistent control art treatment only. The current `LaneViewToggleController` still exposes lane selection behavior, not a separate true map-camera mode. A future gameplay/UI pass should decide whether the selected control language becomes:

- a lane picker only;
- a lane/map two-state toggle;
- or a compact combined lane picker plus map button.

