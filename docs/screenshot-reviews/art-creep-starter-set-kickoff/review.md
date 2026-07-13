# Screenshot UI Review

Status: Pass for current corrective pass; Needs Review for final art polish

## Summary

- Target branch: `art-creep-starter-set`.
- Target platform/view: Unity Game view, mobile/portrait readability.
- Intended coverage: default HUD, build menu, send menu after sending, lane selector, active combat, heavy pressure, reduced effects, and results screen.
- Fresh screenshot capture now works after macOS permissions were fixed.
- Added `VisualReviewCaptureRunner` so Unity can create the review set from inside the editor instead of relying on remote mouse clicks.
- Captured default HUD, build menu, send menu, lane selector, active combat, heavy pressure, reduced effects, and match-complete/late-match screenshots.
- The `1-4` corrective pass is complete: results overlay collision, build/send menu crowding, creep role readability starter shapes, and the Unity capture rerun.
- The visual baseline is usable for the `art-creep-starter-set` branch. Remaining work is now polish-level art readability rather than broken mobile UI layout.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Resolved | Results overlay collision | `captures/08-results-or-late-match.png` now uses a bounded dark results card and hides pause/reset plus lane selector controls during match completion. | Keep this modal behavior; future results details should stay within the card bounds. |
| Resolved | Build/send menu text crowding | `captures/02-build-menu-open.png` and `03-send-menu-open.png` now place expanded menus above the bottom dock and reduce cards to a two-line hierarchy. | Keep expanded menus above the launcher row; use iconography later if more options are added. |
| Medium | Creep heavy-pressure readability | `captures/06-heavy-pressure.png` and `07-reduced-effects-heavy.png` show improved Runner/Brute/Swarm silhouette separation, but the center-lane stack still compresses under sustained pressure. | Next art pass should add authored sprite/mesh variants or stronger per-role value bands once the placeholder geometry direction is accepted. |
| Medium | Board readability baseline | `captures/01-default-hud.png` and combat captures show the center route is brighter than build bands, while the darker build zones recede behind towers/creeps. | Keep the board palette direction; validate again after creep art changes because brighter creeps may change contrast balance. |
| Low | Lane selector clarity | `captures/04-lane-selector-open.png` shows the lane selector is readable and much clearer than the older dark lane button. | Keep this treatment; only revisit if future HUD spacing changes crowd the right rail. |

## Screenshot Notes

- `captures/01-default-hud.png`: Current ready HUD. Board route/build-band contrast reads clearly; top HUD and bottom controls stay compact.
- `captures/02-build-menu-open.png`: Build palette opens above the launcher row; labels/costs are readable at the captured mobile-safe size.
- `captures/03-send-menu-open.png`: Send dock opens above the launcher row, stays on screen, and the three send cards are readable.
- `captures/04-lane-selector-open.png`: Lane selector is readable and no longer overly dark.
- `captures/05-active-combat.png`: Towers and route remain visible during light combat.
- `captures/06-heavy-pressure.png`: Heavy pressure is visible; role silhouettes are more distinct than the previous capture, though dense stacks still want final art treatment.
- `captures/07-reduced-effects-heavy.png`: Reduced-effects heavy pressure remains functionally visible with the same remaining dense-stack polish concern.
- `captures/08-results-or-late-match.png`: Match completion now appears in a bounded modal card without pause/reset or lane-selector collisions.

## Missing Coverage

- Manual verification that the send menu remains open after a real player send tap/click.
- A cropped phone-safe capture without Unity editor chrome, if needed for final mobile signoff.

## Creep Starter Branch Goals

Start `art-creep-starter-set` with these constraints:

1. Improve Runner, Brute, and Swarm silhouettes without changing simulation rules.
2. Keep each creep readable by shape before relying on color.
3. Test against the darker build bands and brighter center route from the board-material pass.
4. Avoid final-detail polish until heavy-send and reduced-effects captures pass review.
