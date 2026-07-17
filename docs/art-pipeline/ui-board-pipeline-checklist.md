# UI And Game Board Pipeline Checklist

Date: 2026-07-16

Source of truth: `docs/art-pipeline/ui-board-art-pipeline.md`

## V0.1 Baseline

- [x] Runtime build icons load from `Assets/Resources/Art/UI/Icons/`.
- [x] Runtime send icons load from `Assets/Resources/Art/UI/Icons/`.
- [x] Procedural fallback glyphs remain for missing icon sprites.
- [x] Board route/build-band/endpoint cues have a first procedural polish pass.
- [x] Static icon review exists at `docs/art-pipeline/ui-board-pass-v01-icon-review.png`.
- [x] Unity screenshot review exists at `docs/screenshot-reviews/ui-board-art-pass-v01/review.md`.

## V0.1 Known Limits

- [ ] HUD chrome is still mostly prototype IMGUI.
- [ ] Top stat area still needs a deliberate open/closed drawer design.
- [ ] Command cards use derived sprite icons, not final simplified command icons.
- [ ] Board material is still procedural, not authored tile/gate art.
- [ ] Spawn and leak endpoints need selected gate art.
- [ ] Map/lane toggle style is functional but not part of a finished UI kit.

## V0.2 Planning

- [x] Create UI/board art pipeline doc.
- [x] Create UI/board contact-sheet brief.
- [x] Create UI/board execution checklist.
- [x] Create UI/board component inventory.
- [x] Generate HUD chrome contact sheet.
  - [x] `docs/art-pipeline/ui-board/contact-sheets/ui_board_hud_chrome_contact_sheet_v01.png`
- [x] Generate command card frame contact sheet.
  - [x] `docs/art-pipeline/ui-board/contact-sheets/ui_board_command_cards_contact_sheet_v01.png`
- [x] Generate icon simplification contact sheet.
  - [x] `docs/art-pipeline/ui-board/contact-sheets/ui_board_icon_simplification_contact_sheet_v01.png`
- [x] Generate board material kit contact sheet.
  - [x] `docs/art-pipeline/ui-board/contact-sheets/ui_board_material_kit_contact_sheet_v01.png`
- [x] Generate spawn/leak gate contact sheet.
  - [x] `docs/art-pipeline/ui-board/contact-sheets/ui_board_spawn_leak_gates_contact_sheet_v01.png`
- [x] Generate map/lane toggle and status control contact sheet.
  - [x] `docs/art-pipeline/ui-board/contact-sheets/ui_board_controls_contact_sheet_v01.png`
- [x] Record first-pass contact sheet review notes.
  - [x] `docs/art-pipeline/ui-board/contact-sheet-review-v01.md`
- [x] Record selected candidates and rejection notes.
  - [x] `docs/art-pipeline/ui-board/selected-candidates-v02.md`
  - [x] Cropped selected previews under `docs/art-pipeline/ui-board/selected-candidates/`.

## V0.2 Production

- [x] Promote selected command card frame art.
  - [x] Selected direction: contact-sheet option 4.
  - [x] 2026-07-16: Added shared runtime command-card chrome in `RuntimeUiChrome`, wired build/send cards, and captured review evidence under `docs/screenshot-reviews/ui-board-art-pass-v02-command-cards/`.
- [x] Promote selected HUD/stat drawer frame art.
  - [x] Selected direction: contact-sheet option 6.
  - [x] 2026-07-16: Added first runtime compact HUD chrome pass in `HudView` and aligned deterministic screenshot overlays with the compact dropdown direction.
- [x] Promote selected route/build-band board material art.
  - [x] Selected direction: contact-sheet option 11.
  - [x] 2026-07-16: Added first procedural option-11 board material slice: slate cell palette, quieter route band, bevel/seam emphasis, and triangular route cue language in `UnityVerticalSliceRenderer`.
- [x] Promote selected spawn/leak gate art.
  - [x] Selected direction: contact-sheet option 11.
  - [x] 2026-07-16: Replaced bright rectangular endpoint blocks with compact circular spawn/leak platforms, spawn pylons/chevrons, and leak drain slats.
- [x] Promote selected map/lane/status control art.
  - [x] Selected direction: contact-sheet option 1.
  - [x] 2026-07-16: Added shared persistent control chrome in `RuntimeUiChrome`, wired the lane selector, and captured review evidence under `docs/screenshot-reviews/ui-board-art-pass-v02-controls/`.
- [x] Improve command-card phone readability.
  - [x] 2026-07-16: Enlarged build/send command cards, centered role icons, increased label/meta text, lifted expanded drawers clear of bottom launchers, and captured scored evidence under `docs/screenshot-reviews/mobile-art-ui-readability-pass/`.
- [x] Add command-state and focused pressure capture coverage.
  - [x] 2026-07-16: Expanded the managed mobile capture matrix from 8 to 12 states with selected build card, disabled send card, Runner x10 pressure, and heavy Swarm pressure evidence under `docs/screenshot-reviews/mobile-art-state-coverage-pass/`.
- [x] Run first aggressive UI spacing automation pass.
  - [x] 2026-07-16: Added `-ltwIntensity aggressive`, replaced long build/send drawer headers with compact tabs, increased drawer spacing, and captured evidence under `docs/screenshot-reviews/mobile-art-aggressive-ui-spacing-pass/`.
- [x] Run aggressive command-card icon-first pass.
  - [x] 2026-07-16: Enlarged command icon wells, switched command cards to compact role codes, reduced card label/meta scale, and captured evidence under `docs/screenshot-reviews/mobile-art-aggressive-command-card-pass/`.
- [x] Add targeted spawn/leak board automation pass.
  - [x] 2026-07-16: Added 15-state mobile capture coverage with board overview, spawn-gate focus, and leak-gate focus states; trimmed the lane to 16 rows; strengthened procedural endpoint plates; captured scored evidence under `docs/screenshot-reviews/mobile-art-spawn-leak-board-pass/`.
- [ ] Normalize crops, scale, alpha, and grayscale copies.
- [ ] Add source notes for every promoted generated asset.
- [ ] Add Unity import settings and `.meta` files.
- [x] Wire first runtime slice behind safe fallback.
  - [x] Command cards use procedural V02 chrome and keep existing icon fallback behavior.

## V0.2 QA

- [x] Capture phone portrait default single-lane view.
  - [x] `docs/screenshot-reviews/ui-board-art-pass-v02/captures/01-default-hud.png`
- [ ] Capture map view.
  - [ ] True map-camera behavior remains outside the current lane-selector-only control slice.
- [x] Capture build menu open.
  - [x] `docs/screenshot-reviews/ui-board-art-pass-v02/captures/02-build-menu-open.png`
- [x] Capture send menu open.
  - [x] `docs/screenshot-reviews/ui-board-art-pass-v02/captures/03-send-menu-open.png`
- [x] Capture selected command card.
  - [x] `docs/screenshot-reviews/mobile-art-state-coverage-pass/mobile-art-state-coverage-pass/after/phone-standard-portrait/03-build-card-selected.png`
- [x] Capture disabled/too-expensive command card.
  - [x] `docs/screenshot-reviews/mobile-art-state-coverage-pass/mobile-art-state-coverage-pass/after/phone-standard-portrait/05-send-card-disabled.png`
- [x] Capture heavy Runner pressure.
  - [x] `docs/screenshot-reviews/mobile-art-state-coverage-pass/mobile-art-state-coverage-pass/after/phone-standard-portrait/08-runner-10-pressure.png`
- [x] Capture heavy Swarm pressure.
  - [x] `docs/screenshot-reviews/mobile-art-state-coverage-pass/mobile-art-state-coverage-pass/after/phone-standard-portrait/09-swarm-heavy-pressure.png`
- [x] Capture heavy pressure.
  - [x] `docs/screenshot-reviews/ui-board-art-pass-v02/captures/06-heavy-pressure.png`
- [x] Capture leak/life-loss moment.
  - [x] `docs/screenshot-reviews/ui-board-art-pass-v02/captures/08-results-or-late-match.png`
- [x] Capture board-only spawn/leak focus states.
  - [x] `docs/screenshot-reviews/mobile-art-spawn-leak-board-pass/mobile-art-spawn-leak-board-pass/after/phone-standard-portrait/12-board-overview.png`
  - [x] `docs/screenshot-reviews/mobile-art-spawn-leak-board-pass/mobile-art-spawn-leak-board-pass/after/phone-standard-portrait/13-spawn-gate-focus.png`
  - [x] `docs/screenshot-reviews/mobile-art-spawn-leak-board-pass/mobile-art-spawn-leak-board-pass/after/phone-standard-portrait/14-leak-gate-focus.png`
- [x] Capture grayscale set.
  - [x] `docs/screenshot-reviews/ui-board-art-pass-v02/captures/grayscale/`
- [x] Write command-card slice review.
  - [x] `docs/screenshot-reviews/ui-board-art-pass-v02-command-cards/review.md`
- [x] Write persistent-controls slice review.
  - [x] `docs/screenshot-reviews/ui-board-art-pass-v02-controls/review.md`
- [x] Write full V02 review under `docs/screenshot-reviews/ui-board-art-pass-v02/review.md`.

## Done Criteria For UI/Board V1

- [ ] HUD is compact and readable in portrait phone view.
- [ ] Optional stats drawer can be open or closed without breaking layout.
- [ ] Build/send cards identify roles by icon at phone scale.
- [ ] Selected and disabled states are readable in grayscale.
- [ ] Board route, build zones, spawn gate, and leak gate are understandable without labels.
- [ ] Heavy creep pressure remains readable.
- [ ] Map/lane toggle is always onscreen and visually consistent.
- [ ] No protected-game UI chrome or copied compositions are used.
- [ ] Screenshot QA records a `Pass` or `Pass with low-severity polish follow-ups`.
