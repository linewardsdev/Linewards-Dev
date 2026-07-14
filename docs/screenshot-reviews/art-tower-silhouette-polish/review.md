# Screenshot UI Review

Status: Pass with limited tower-role coverage

## Summary

- Tower prefab generation now uses role-first silhouettes aligned to `docs/ART_THEME_AND_ROLE_GUIDE.md`.
- Required runtime child paths remain stable: `Body`, `RoleMarker`, `OwnerTrim`, and `RangeHalo`.
- Runtime captures show the placed Arrow/Control towers remain readable in phone-size lane view, including grayscale heavy-pressure capture.
- The current visual capture runner only places Arrow and Control towers, so Relay, Pulse, and Prism are validated by prefab contract/generator output but not fully proven in live screenshot context yet.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Low | Capture coverage | `05-active-combat.png` and `06-heavy-pressure.png` place Arrow/Control only. | Add a dedicated tower-lineup capture that places all five tower roles once the capture harness scope expands. |
| Low | Heavy-pressure overlap | `06-heavy-pressure.png` shows tower visuals remain visible, but pressure/beam activity partially covers mid-lane shapes. | Keep tower role markers high-contrast and avoid relying on fine details for role identity. |

## Screenshot Notes

- `captures/02-build-menu-open.png`: build menu remains legible and does not obscure the bottom tower controls more than expected.
- `captures/05-active-combat.png`: placed towers are distinguishable from lane/grid and creeps at mobile gameplay scale.
- `captures/06-heavy-pressure.png`: tower silhouettes survive active combat clutter; Arrow/Control remain findable through FX.
- `captures/grayscale/06-heavy-pressure.png`: silhouettes continue to separate from the board by value, though fine role details are intentionally secondary.
- `captures/08-results-or-late-match.png`: results overlay does not introduce a tower-art regression.

## Prefab Contract Evidence

- `Tower_Arrow.prefab`: `BoltRail`, `BowLeft`, `BowRight`, `BowString`, `ArrowHead`.
- `Tower_Control.prefab`: `ControlRing`, `ControlCore`, `ClampNorth`, `ClampSouth`, `ClampEast`, `ClampWest`.
- `Tower_Relay.prefab`: `RelayMast`, `RelayCore`, `RelaySignal`, `CapacitorLeft`, `CapacitorRight`.
- `Tower_Pulse.prefab`: `PulseCore`, `PulseRingA`, `PulseRingB`, `PulseEmitter`.
- `Tower_Prism.prefab`: `PrismSpire`, `PrismLens`, `PrismBeamHint`, `FacetLeft`, `FacetRight`.

## Missing Coverage

- A live gameplay capture with all five towers placed at once.
- A close-range/editor prefab contact sheet for comparing all five tower silhouettes side by side.
