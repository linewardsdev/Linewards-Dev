# Screenshot UI Review

Status: Needs Review

## Summary

- Target branch: `art-creep-starter-set`.
- Target platform/view: Unity Game view, mobile/portrait readability.
- Intended coverage: default HUD, build menu, send menu after sending, lane selector, active combat, heavy pressure, reduced effects, and results screen.
- Fresh screenshot capture now works after macOS permissions were fixed.
- Added `VisualReviewCaptureRunner` so Unity can create the review set from inside the editor instead of relying on remote mouse clicks.
- Captured default HUD, build menu, send menu, lane selector, active combat, heavy pressure, reduced effects, and match-complete/late-match screenshots.
- The visual baseline is now usable for the `art-creep-starter-set` branch, but several UI/readability issues remain.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| High | Results overlay collision | `captures/08-results-or-late-match.png` shows the match-complete text overlapping the lane, pause/reset controls, lane selector, and active creep path. | Move results into a bounded panel or centered modal that clears top HUD/right rail controls and wraps/scales text. |
| Medium | Build/send menu text crowding | `captures/02-build-menu-open.png` and `03-send-menu-open.png` show card labels/metas clipped or visually colliding at the captured Game view size. | Give menu cards more vertical room, reduce per-card copy, or use tighter icon/label hierarchy before creep art polish. |
| Medium | Creep heavy-pressure readability | `captures/06-heavy-pressure.png` and `07-reduced-effects-heavy.png` show a dense vertical stack where individual creep roles are hard to distinguish once many units overlap. | Prioritize stronger Runner/Brute/Swarm silhouettes, spacing offsets, and value separation in `art-creep-starter-set`. |
| Medium | Board readability baseline | `captures/01-default-hud.png` and combat captures show the center route is brighter than build bands, while the darker build zones recede behind towers/creeps. | Keep the board palette direction; validate again after creep art changes because brighter creeps may change contrast balance. |
| Low | Lane selector clarity | `captures/04-lane-selector-open.png` shows the lane selector is readable and much clearer than the older dark lane button. | Keep this treatment; only revisit if future HUD spacing changes crowd the right rail. |

## Screenshot Notes

- `captures/01-default-hud.png`: Current ready HUD. Board route/build-band contrast reads clearly; top HUD and bottom controls stay compact.
- `captures/02-build-menu-open.png`: Build palette opens, but button text is cramped/clipped.
- `captures/03-send-menu-open.png`: Send dock opens and stays on screen; text density is high and lower labels are cramped.
- `captures/04-lane-selector-open.png`: Lane selector is readable and no longer overly dark.
- `captures/05-active-combat.png`: Towers and route remain visible during light combat.
- `captures/06-heavy-pressure.png`: Heavy pressure is visible, but creep roles compress into a hard-to-parse vertical stack.
- `captures/07-reduced-effects-heavy.png`: Reduced-effects heavy pressure remains functionally visible, with the same creep role separation concern.
- `captures/08-results-or-late-match.png`: Match completion appears, but result text overlaps core gameplay UI.

## Missing Coverage

- Manual verification that the send menu remains open after a real player send tap/click.
- A cropped phone-safe capture without Unity editor chrome, if needed for final mobile signoff.

## Creep Starter Branch Goals

Start `art-creep-starter-set` with these constraints:

1. Improve Runner, Brute, and Swarm silhouettes without changing simulation rules.
2. Keep each creep readable by shape before relying on color.
3. Test against the darker build bands and brighter center route from the board-material pass.
4. Avoid final-detail polish until heavy-send and reduced-effects captures pass review.
