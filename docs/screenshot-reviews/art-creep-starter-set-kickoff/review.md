# Screenshot UI Review

Status: Needs Review

## Summary

- Target branch: `art-creep-starter-set`.
- Target platform/view: Unity Game view, mobile/portrait readability.
- Intended coverage: default HUD, build menu, send menu after sending, lane selector, active combat, heavy pressure, reduced effects, and results screen.
- Fresh screenshot capture now works after macOS permissions were fixed.
- One current Unity editor/Game view screenshot was captured.
- Build menu, send menu, lane selector, combat, heavy pressure, reduced effects, and results captures are still missing because remote click/hotkey driving did not change the Game view state from this shell session.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| High | Missing interaction states | Only the default/ready Game view state was captured. Build, send, lane selector, combat, heavy pressure, reduced effects, and results are still missing. | Capture these states manually from the visible Unity editor or add a dedicated in-editor visual capture runner. |
| Medium | Board readability baseline | `captures/01-current-default.png` shows the post-merge board in ready state. The center route is visibly brighter than build bands, and spawn/leak endpoints are more distinct than the prior dark pass. | Use this as a baseline only; do not mark heavy-pressure or normal-phone-scale verification complete until combat captures exist. |
| Medium | Creep starter work needs combat baseline | Runner, Brute, and Swarm polishing depends on the new board contrast, but no active creep screenshot was captured in this run. | Start creep polish with silhouette and value separation goals, then validate against fresh combat screenshots before finalizing assets. |

## Screenshot Notes

- `captures/01-current-default.png`: Current Unity editor/Game view ready state. Useful for board route/build-band contrast and HUD placement baseline.

## Missing Coverage

- Build menu open.
- Send menu open and still open after sending.
- Lane selector open.
- Active combat with current board materials.
- Heavy pressure with 20+ visible creeps.
- Reduced-effects heavy pressure.
- Results screen.

## Creep Starter Branch Goals

Start `art-creep-starter-set` with these constraints:

1. Improve Runner, Brute, and Swarm silhouettes without changing simulation rules.
2. Keep each creep readable by shape before relying on color.
3. Test against the darker build bands and brighter center route from the board-material pass.
4. Avoid final-detail polish until heavy-send and reduced-effects captures pass review.
