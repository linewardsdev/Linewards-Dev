# Screenshot UI Review

Status: Needs Review

## Summary

- The Unity visual capture runner now successfully produced the full 8-shot review set for the prefab-foundation runtime pass.
- The new tower/creep prefab-backed presentation is visible in active and heavy-pressure captures.
- The screenshots are valid evidence, but the art baseline is not a visual pass yet: role silhouettes remain very small at phone framing, and the Play/Pause/Reset controls are cramped.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Medium | Match controls | `01-default-hud.png`, `05-active-combat.png`: Play/Pause and Reset sit tightly together on the right side of the lane and visually compete with the board. | Move match controls into a cleaner compact strip or combine Reset into a less prominent secondary action. |
| Medium | Role readability | `05-active-combat.png`, `06-heavy-pressure.png`, `07-reduced-effects-heavy.png`: prefab-backed towers/creeps are visible, but details collapse quickly at phone framing. | Next art pass should enlarge/strengthen role silhouettes before adding decorative detail. |
| Medium | Heavy pressure clarity | `06-heavy-pressure.png`: projectiles, tiny creeps, and lane arrows compete in the center lane. | Increase value/shape separation for creep roles and reduce route-arrow brightness during combat. |
| Low | Reduced effects | `07-reduced-effects-heavy.png`: reduced-effects state remains readable, but role identity is mostly carried by color/position rather than silhouette. | Add reduced-effects silhouette checks to the next review gate. |

## Screenshot Notes

- `captures/01-default-hud.png`: Valid default state. Board and HUD are visible. Match controls are cramped.
- `captures/02-build-menu-open.png`: Captured build menu state for layout regression comparison.
- `captures/03-send-menu-open.png`: Captured send menu state for layout regression comparison.
- `captures/04-lane-selector-open.png`: Captured lane selector state for layout regression comparison.
- `captures/05-active-combat.png`: Prefab-backed runtime visuals appear in combat; silhouettes are present but small.
- `captures/06-heavy-pressure.png`: Heavy pressure evidence exists; readability still needs art-scale/silhouette work.
- `captures/07-reduced-effects-heavy.png`: Reduced-effects evidence exists; gameplay remains visible.
- `captures/08-results-or-late-match.png`: Late-match/results coverage exists for comparison.

## Missing Coverage

- Before/after side-by-side crops comparing primitive-only towers to prefab-backed towers.
- Grayscale/value crop pass for creep and tower silhouette distinction.
- Interactive visual confirmation in the Unity Game view at the exact target mobile aspect/scale.
