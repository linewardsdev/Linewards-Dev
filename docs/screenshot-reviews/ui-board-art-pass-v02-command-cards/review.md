# UI Board Art Pass V02: Command Cards

Date: 2026-07-16

## Status

Pass with follow-up polish.

## Implemented

- Added shared runtime command-card chrome in `RuntimeUiChrome`.
- Updated build cards in `TouchPlacementController` to use the V02 command-card treatment.
- Updated send cards in `SendDockController` to use the same treatment.
- Increased build/send card height and panel spacing so the selected option-4 frame language has room to read.
- Updated `VisualReviewCaptureRunner` deterministic HUD overlays so batch screenshots reflect the new command-card direction.

## Selection Source

- Selected direction: command card option 4.
- Selection doc: `docs/art-pipeline/ui-board/selected-candidates-v02.md`.
- Candidate crop: `docs/art-pipeline/ui-board/selected-candidates/command_cards_option_04.png`.

## Capture Evidence

Command:

`Unity.exe -batchmode -projectPath unity/LTW.UnityClient -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureVisualReviewSet -ltwCaptureOutputDir docs/screenshot-reviews/ui-board-art-pass-v02-command-cards/captures -ltwCaptureGrayscale -ltwExitAfterCapture`

Captured:

- `captures/01-default-hud.png`
- `captures/02-build-menu-open.png`
- `captures/03-send-menu-open.png`
- `captures/04-lane-selector-open.png`
- `captures/05-active-combat.png`
- `captures/06-heavy-pressure.png`
- `captures/07-reduced-effects-heavy.png`
- `captures/08-results-or-late-match.png`
- grayscale copies under `captures/grayscale/`

Log:

- `docs/screenshot-reviews/ui-board-art-pass-v02-command-cards/unity-capture.log`

## Observations

- Build and send cards now read as framed command-card controls instead of flat placeholder buttons.
- Normal color and grayscale captures preserve icon, label, price, and state separation.
- The larger card height improves legibility and makes the selected option-4 direction visible at phone framing.
- The batch capture uses deterministic overlay paint for HUD/menu evidence, so final manual Game View inspection is still useful before declaring the whole UI V1 complete.

## Follow-Ups

- Add selected and too-expensive/error screenshots from live interaction once the UI state harness supports them directly.
- Continue V02 with persistent map/lane/status controls from selected option 1.
- Continue V02 with HUD/stat drawer chrome from selected option 6.
- Continue V02 with board material and spawn/leak gate runtime slices.

