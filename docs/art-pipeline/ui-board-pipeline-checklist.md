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

- [ ] Promote selected command card frame art.
  - [ ] Selected direction: contact-sheet option 4.
- [ ] Promote selected HUD/stat drawer frame art.
  - [ ] Selected direction: contact-sheet option 6.
- [ ] Promote selected route/build-band board material art.
  - [ ] Selected direction: contact-sheet option 11.
- [ ] Promote selected spawn/leak gate art.
  - [ ] Selected direction: contact-sheet option 11.
- [ ] Normalize crops, scale, alpha, and grayscale copies.
- [ ] Add source notes for every promoted generated asset.
- [ ] Add Unity import settings and `.meta` files.
- [ ] Wire first runtime slice behind safe fallback.

## V0.2 QA

- [ ] Capture phone portrait default single-lane view.
- [ ] Capture map view.
- [ ] Capture build menu open.
- [ ] Capture send menu open.
- [ ] Capture selected command card.
- [ ] Capture disabled/too-expensive command card.
- [ ] Capture heavy Runner pressure.
- [ ] Capture heavy Swarm pressure.
- [ ] Capture leak/life-loss moment.
- [ ] Capture grayscale set.
- [ ] Write review under `docs/screenshot-reviews/ui-board-art-pass-v02/review.md`.

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
