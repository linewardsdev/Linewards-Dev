# Screenshot UI Review

Status: Needs Review

## Summary

- The visual capture gate is no longer blocked: the graphics batch command produced the full 8-shot set plus grayscale copies in `captures/`.
- These captures are valid game-camera evidence for board, route, tower, creep, heavy-pressure, reduced-effects, and grayscale readability.
- They are not yet valid HUD/menu evidence: IMGUI/HUD overlays are missing from the batch-rendered screenshots, so `02-build-menu-open.png`, `03-send-menu-open.png`, and other UI-labeled states show only the board camera.
- Heavy-pressure and reduced-effects captures are readable enough for current placeholder art review, but they confirm the next plan priority: board material/authored asset work should come before more primitive-detail stacking.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| High | HUD/menu capture coverage | `captures/02-build-menu-open.png` and `captures/03-send-menu-open.png` do not show the Build or Send IMGUI overlays, despite their labels. The batch path renders active cameras, not the full Game view UI. | Add a GUI/editor capture path for HUD states, or extend the capture runner to render IMGUI/UI overlays into capture output before closing Workstream J. |
| Medium | Board material flatness | `captures/01-default-hud.png`, `captures/05-active-combat.png`, and grayscale captures show clear route/build bands, but the board still reads as flat primitive surfaces. | Proceed with Workstream K board material pass: route wear, build-band value separation, gate detail, rails/gutters, and grounding shadows. |
| Medium | Heavy-pressure role density | `captures/06-heavy-pressure.png` and `captures/grayscale/06-heavy-pressure.png` keep the lane understandable, but tower bodies, beam lines, and creep clusters compete near the lower-middle fight. | Preserve current larger silhouettes, then use authored board/asset values and shorter/chunkier VFX to separate pressure from towers. |
| Low | Reduced-effects coverage | `captures/07-reduced-effects-heavy.png` remains understandable and less noisy than full effects, but it still needs HUD-state proof. | Keep reduced-effects gameplay presentation; rerun with UI-capable capture before signoff. |
| Low | Originality/art direction | Current visuals remain original ward-tech placeholders with no protected UI chrome or recognizable protected-game silhouettes. | Continue with original authored Line Wards material and asset language. |

## Screenshot Notes

- `captures/01-default-hud.png`: Clean board-camera baseline. Route and build bands are readable, but no HUD is visible.
- `captures/02-build-menu-open.png`: Capture exists, but Build menu is absent; not valid for build-menu UI review.
- `captures/03-send-menu-open.png`: Capture exists, but Send menu is absent; not valid for send-menu UI review.
- `captures/04-lane-selector-open.png`: Capture exists, but lane selector UI is absent; not valid for lane-selector UI review.
- `captures/05-active-combat.png`: Useful art/readability frame. Towers are distinguishable and board route remains visible.
- `captures/06-heavy-pressure.png`: Useful stress frame. Heavy pressure is visible; tower/creep/VFX overlap is readable but visually busy.
- `captures/07-reduced-effects-heavy.png`: Useful reduced-effects frame. Gameplay remains understandable with less VFX noise.
- `captures/08-results-or-late-match.png`: Capture exists, but results/HUD overlay is absent; useful only as board/late-state evidence.
- `captures/grayscale/06-heavy-pressure.png`: Useful value-check evidence. Route, towers, and creeps remain visible, but authored value hierarchy is still needed.

## Missing Coverage

- Full Game view screenshots that include IMGUI/HUD overlays.
- Valid Build menu, Send menu, lane selector, stats drawer, placement descriptor, and results overlay captures.
- Manual or UI-capable capture showing damaged transfer with `TRANSFER` cue and reduced health in the same frame.

## Verdict

The screenshot gate is partially repaired. Batch capture now produces reliable game-camera pixels and grayscale value copies, but Workstream J cannot be marked complete until HUD/menu overlays are captured. Use this capture set for board/art baseline review, not final UI signoff.
