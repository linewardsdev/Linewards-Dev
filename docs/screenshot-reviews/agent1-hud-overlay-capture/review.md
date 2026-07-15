# Screenshot UI Review

Status: Needs Review

## Summary

- Agent 1 restored a HUD-capable batch capture path at `docs/screenshot-reviews/agent1-hud-overlay-capture/captures/`.
- The batch set now includes visible default HUD, Build menu, Send Pressure menu, lane selector, active combat, heavy pressure, reduced-effects heavy pressure, and results/late-match states.
- The menu/HUD elements are deterministic capture overlays painted into batch PNGs, not live IMGUI screenshots. They are suitable for visual-regression coverage and branch evidence, but a final human Game View spot-check is still required before signing off exact runtime UI behavior.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Medium | Capture fidelity | `02-build-menu-open.png`, `03-send-menu-open.png`, `04-lane-selector-open.png`, and `08-results-or-late-match.png` now visibly show their intended states, but through deterministic overlay paint rather than live IMGUI capture. | Use these for automated branch evidence; keep one manual Unity/Game View pass for exact runtime UI alignment. |
| Low | Mobile readability | HUD/menu text is intentionally chunky and readable in portrait framing. | Continue using this capture path as a regression gate while authored UI assets evolve. |
| Low | Board/art evidence | Active combat, heavy pressure, reduced-effects, and grayscale copies remain useful for board, tower, creep, and value-readability checks. | Use this capture set as the baseline before Agent 2's board material pass. |

## Screenshot Notes

- `01-default-hud.png`: default portrait board/HUD state exported.
- `02-build-menu-open.png`: Build menu state is visible with five tower cards and top/bottom HUD controls.
- `03-send-menu-open.png`: Send Pressure state is visible with five creep cards and income values.
- `04-lane-selector-open.png`: lane selector rail is visible on the right edge.
- `05-active-combat.png`: active combat board state exported.
- `06-heavy-pressure.png`: heavy-pressure board state exported.
- `07-reduced-effects-heavy.png`: reduced-effects pressure state exported.
- `08-results-or-late-match.png`: results modal state is visible over late combat.

## Missing Coverage

- Live IMGUI/Game View screenshot validation remains needed for final UI signoff.
- Dedicated damaged-transfer capture remains tracked separately under Workstream D.
