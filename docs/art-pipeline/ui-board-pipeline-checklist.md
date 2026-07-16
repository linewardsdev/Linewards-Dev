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
- [ ] Generate HUD chrome contact sheet.
- [ ] Generate command card frame contact sheet.
- [ ] Generate icon simplification contact sheet.
- [ ] Generate board material kit contact sheet.
- [ ] Generate spawn/leak gate contact sheet.
- [ ] Generate map/lane toggle and status control contact sheet.
- [ ] Record selected candidates and rejection notes.

## V0.2 Production

- [ ] Promote selected command card frame art.
- [ ] Promote selected HUD/stat drawer frame art.
- [ ] Promote selected route/build-band board material art.
- [ ] Promote selected spawn/leak gate art.
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
