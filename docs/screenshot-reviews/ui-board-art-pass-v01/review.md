# UI And Board Art Pass V01 Review

Date: 2026-07-16

## Status

Captured after retry.

## Implemented

- Build/send card icons now load derived V1 silhouette sprites from `Assets/Resources/Art/UI/Icons/`.
- Procedural IMGUI glyphs remain as fallback if an icon sprite is missing.
- Board material polish adds build-zone edge lines, a center route inlay, and endpoint chevrons.
- Static icon review sheet is available at `docs/art-pipeline/ui-board-pass-v01-icon-review.png`.

## Capture Evidence

Command:

`Unity.exe -batchmode -projectPath unity/LTW.UnityClient -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureVisualReviewSet -ltwCaptureOutputDir docs/screenshot-reviews/ui-board-art-pass-v01/captures -ltwCaptureGrayscale -ltwExitAfterCapture`

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

- `docs/screenshot-reviews/ui-board-art-pass-v01/unity-capture.log`

Note: the first capture attempt exited early during Unity project load. A plain import/compile pass succeeded, then the capture runner completed normally.

## Observations

- Build menu captures now show V1 tower silhouette icons on each card.
- Send menu captures now show V1 creep silhouette icons on each card.
- Grayscale send-menu capture keeps the icon silhouettes visible enough for review, while labels remain the primary guaranteed read.
- The procedural board route, build bands, and endpoint plate details remain subordinate to the HUD and unit silhouettes.

## Review Risks

- Derived full-color icons are visible, but still carry more internal detail than final command icons should. A later polish pass should create simplified icon-only art using the same silhouettes.
- New board edge/inlay marks should be checked under heavy pressure to ensure they do not compete with creep health/readability cues.
