# Screenshot UI Review

Status: Needs Review

## Summary

- Main was cleaned before review; generated Unity package/plugin import crumbs were removed.
- Existing `merge-ui-pass-after` screenshots were inspected as historical evidence for board readability, heavy-send readability, and HUD overlap risks.
- Fresh automated Unity capture could not be produced because another Unity instance currently has `unity/LTW.UnityClient` open.
- The next art branch is `art-board-materials`; board work should start from lane/path/material readability, then retest with fresh phone-size screenshots.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| High | Fresh visual coverage | Unity batch capture was blocked by the open editor project, so current HUD changes could not be verified from new pixels. | Capture a new current set from the editor: default HUD, build menu, send menu, lane selector, combat, heavy pressure, reduced effects, and results. |
| Medium | Historical HUD overlap | `docs/screenshot-reviews/merge-ui-pass-after/02-play-start.png` and `03-heavy-send.png` show the old central start/pause panel covering active lane space. | Treat as a regression watch item only; current code has moved session controls away from the old large panel, but needs fresh confirmation. |
| Medium | Historical top bar content | `01-ready.png` and `04-reduced-effects-heavy.png` show old `YOUR LINE` and `RED FX` treatments that were later removed/reworked. | Verify the current stat dropdown and reduced-effects state in fresh captures. |
| Medium | Board material contrast | Existing captures show readable vertical flow and center route arrows, but the dark field/build zones are close in value. | In `art-board-materials`, increase center route continuity, build-band separation, and spawn/exit landmarks before adding decorative trim. |
| Low | Creep/tower silhouettes | Existing heavy-send captures show tower shapes and several creep markers remain visible, but density is not enough to prove 20+ creep readability. | Retest with heavy-send stress and reduced effects once fresh capture is available. |

## Screenshot Notes

- `docs/screenshot-reviews/merge-ui-pass-after/01-ready.png`: Historical ready state. Board flow is clear, but top bar and ready panel are known outdated UI.
- `docs/screenshot-reviews/merge-ui-pass-after/02-play-start.png`: Historical running state. The old session panel occupies the lower-middle lane and should not be used as current acceptance evidence.
- `docs/screenshot-reviews/merge-ui-pass-after/03-heavy-send.png`: Historical heavy-send state. Tower silhouettes and active pressure meter are visible, but the old HUD overlaps important board area.
- `docs/screenshot-reviews/merge-ui-pass-after/04-reduced-effects-heavy.png`: Historical reduced-effects state. Reduced cues remain readable at a glance, but the `RED FX` top-bar state is outdated.

## Missing Coverage

- Current default HUD after the stat-dropdown and hotkey-control changes.
- Build menu open.
- Send menu open and staying open after sending.
- Lane selector open.
- Active combat with current HUD.
- Heavy pressure with 20+ visible creeps.
- Reduced-effects heavy pressure.
- Results screen.

## Branch Recommendation

Start `art-board-materials` with these priorities:

1. Define stronger base-board material roles for deep field, build bands, center route, borders, spawn, and exit.
2. Improve value separation before color polish so phone-size readability survives grayscale and reduced-effects modes.
3. Preserve the long north-south lane as the dominant visual shape.
4. Avoid adding environmental trim until the center route, build cells, towers, and creeps remain readable under pressure.
