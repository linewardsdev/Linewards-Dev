# Screenshot UI Review

Status: Needs Review

## Summary
- Creep health is now visible in gameplay captures through persistent bar indicators.
- Damaged creeps retain visual damage treatment through color tinting, health fill, and wound pip treatment.
- Transfer arrivals now use a distinct cue path from fresh sends.
- Full visual capture set completed with color and grayscale screenshots.

## Findings
| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Medium | Health bar scale | `captures/06-heavy-pressure.png` and `captures/grayscale/06-heavy-pressure.png` show health bars clearly, but the largest bars can overlap bodies and nearby units under pressure. | Keep the feature, then tune per-role bar size/offset once health readability is validated in manual play. |
| Low | Active combat coverage | `captures/05-active-combat.png` captures tower combat before visible creep pressure reaches the lower fight area. | Add a dedicated damaged-transfer capture state if transfer continuity becomes a frequent regression target. |
| Low | Reduced effects | `captures/07-reduced-effects-heavy.png` keeps creep bodies and health bars readable without full effects. | Preserve the current reduced-effects health bar treatment. |

## Screenshot Notes
- `captures/06-heavy-pressure.png`: Health bars read as horizontal bars at phone framing; pressure and tower visuals remain understandable.
- `captures/grayscale/06-heavy-pressure.png`: Health bars remain visible without relying on color.
- `captures/07-reduced-effects-heavy.png`: Reduced-effects state keeps creep pressure readable with health indicators.
- `captures/05-active-combat.png`: Baseline combat layout remains clean, but this frame has limited creep visibility.

## Missing Coverage
- Direct frame of a wounded creep entering the next lane with the `TRANSFER` cue visible.
