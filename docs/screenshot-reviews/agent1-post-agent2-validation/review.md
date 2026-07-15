# Screenshot UI Review

Status: Pass with follow-ups

## Summary

- Fresh Agent 1 validation captures were generated after Agent 2's board material, authored Arrow, and role readability work.
- HUD/menu capture coverage is intact: default HUD, build menu, send menu, lane selector, active combat, heavy pressure, reduced-effects heavy pressure, and results are all present.
- The builder avatar is visible in the default and menu states, and manual testing confirmed it stays visible, preserves its position, and tower selection defaults to the builder cell.
- The board material pass improves route wear, grid value separation, endpoint readability, and authored-board feel without overpowering towers, creeps, or HUD decisions.
- The authored Arrow tower reads as a clear crossbow/bolt tower at phone framing and remains distinct in grayscale heavy pressure.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Low | HUD capture fidelity | `02-build-menu-open.png`, `03-send-menu-open.png`, `04-lane-selector-open.png`, and `08-results-or-late-match.png` show deterministic HUD capture overlays. | Continue using this batch path for branch evidence; keep live Game View checks for exact IMGUI behavior. |
| Low | Board readability | `06-heavy-pressure.png` and `captures/grayscale/06-heavy-pressure.png` preserve route, tower, creep, projectile, and HUD separation. | Use this as the baseline before the next board/material polish pass. |
| Low | Authored Arrow | `06-heavy-pressure.png` and grayscale heavy pressure show Arrow's bow limbs, central rail, and footprint staying readable near other towers. | Use Arrow as the quality bar before converting Control and Relay. |
| Medium | Remaining role pressure proof | Current fresh captures do not specifically prove Runner group-of-10, Swarm heavy-noise, Shade non-alpha readability, or damaged-transfer health persistence in one frame. | Keep those follow-ups open and add targeted capture states or manual screenshots. |

## Screenshot Notes

- `captures/01-default-hud.png`: default board/HUD state with builder visible near the lower lane.
- `captures/02-build-menu-open.png`: build menu remains readable and does not cover the active board more than expected.
- `captures/03-send-menu-open.png`: send menu remains readable, with creep income values visible.
- `captures/04-lane-selector-open.png`: lane selector rail remains visible on the right edge without crowding bottom actions.
- `captures/05-active-combat.png`: active combat includes authored Arrow, builder, towers, creeps, and projectile feedback.
- `captures/06-heavy-pressure.png`: heavy pressure remains readable at phone framing.
- `captures/07-reduced-effects-heavy.png`: reduced-effects pressure still communicates lane flow and object separation.
- `captures/08-results-or-late-match.png`: results overlay is visible over late combat without hiding bottom actions.
- `captures/grayscale/06-heavy-pressure.png`: route, Arrow, towers, creeps, HUD, and projectiles retain usable value separation.

## Missing Coverage

- Dedicated damaged-transfer capture showing `TRANSFER` and reduced health in the same frame.
- Runner group-of-10 readability.
- Swarm heavy-pressure/noise readability.
- Shade non-alpha/facet readability.
- Exact live IMGUI screenshot capture, beyond deterministic batch HUD overlays.
