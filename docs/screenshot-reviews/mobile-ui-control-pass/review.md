# Screenshot UI Review

Status: Needs Review

## Summary
- Visual review was run against the provided Unity Game-view screenshot and the current HUD code state.
- The provided screenshot predates the latest HUD cleanup, so findings that mention `YOUR LINE`, `RED FX`, or the old dark lane button are treated as already-addressed implementation evidence, not current verified pixels.
- The latest code now uses the top-left tile as the stats dropdown trigger, removes the `RED FX` tile, simplifies the stats drawer, and keeps PLAY/RESET as compact upper-right controls below the expanded stats drawer zone.
- Final sign-off still needs fresh post-change screenshots because local macOS screen capture failed with `could not create image from display`.

## Findings
| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| High | Touch/control overlap risk | Earlier screenshot showed the READY/start panel occupying the lower-center phone area close to BUILD/SEND controls. Follow-up code review found the first compact top placement could collide with the stats drawer. | Addressed in code by moving PLAY/RESET to compact upper-right controls below the expanded stats drawer zone; verify with a fresh running-state screenshot. |
| Medium | Lane selector readability | Earlier screenshot showed the lane button as a nearly black small tile with low-contrast `L3` text. | Addressed in code by custom flat drawing and brighter fill/text contrast; verify lane selector open/closed screenshots. |
| Medium | Primary HUD hierarchy | Earlier screenshot showed top-left `YOUR LINE` and right-side `RED FX`, both low-value player-facing labels in prime HUD space. | Addressed in code: top-left is now `STATS ▼` / `HIDE`, and `RED FX` tile is removed. Verify default HUD screenshot. |
| Medium | Screenshot coverage | No fresh post-change screenshot could be captured from the Unity Game view. | Capture default HUD, stats open, build menu open, send menu open, lane selector open, and running state from Unity. |
| Low | Stats drawer density | Current code now uses four primary stat tiles plus one secondary stat line, which should reduce visual load versus the previous eight-tile grid. | Verify phone-size readability in the next capture pass, especially the secondary stat line. |

## Screenshot Notes
- User-provided Unity screenshot: pre-latest-cleanup evidence. Confirmed useful issues: dark lane selector, low-value `YOUR LINE`, unwanted `RED FX`, and bottom menu crowding.
- Missing: post-change Unity screenshots.

## Missing Coverage
- Default HUD.
- Stats drawer open.
- Build menu open.
- Send menu open.
- Lane selector open.
- Running state with PLAY/PAUSE controls visible.
- Busy/heavy-send state with 20+ creeps.
- Critical feedback state, especially leak/build/send feedback.
- Reduced-effects state.
