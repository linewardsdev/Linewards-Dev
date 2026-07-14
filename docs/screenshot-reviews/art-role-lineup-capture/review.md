# Screenshot UI Review

Status: Pass

## Summary

- Added a dedicated role-lineup capture path for art reviews.
- The capture places all five tower roles on the active lane: Arrow, Control, Relay, Pulse, and Prism.
- The capture queues visible opponent sends for all five creep roles into the active lane: Runner, Brute, Swarm, Shade, and Siege.
- Normal, reduced-effects, and grayscale copies are generated for phone-scale silhouette/value checks.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Low | Creep detail scale | `01-role-lineup.png` shows the mixed creep set in the center route, but individual creep details remain small at gameplay zoom. | Use this as a gameplay-scale baseline; add a separate contact-sheet/close-view capture if creep asset comparison needs per-role detail. |
| Low | Tower/footer overlap | Bottom tower and leak-gate visuals sit close to the Build/Send buttons. | Keep future bottom-lane art short or ensure it cannot obscure button labels. |

## Screenshot Notes

- `captures/01-role-lineup.png`: all five tower roles are visible at once; mixed creep pressure is visible in the center lane.
- `captures/grayscale/01-role-lineup.png`: tower silhouettes separate by broad shape/value, with Prism/Relay height and Control/Pulse width still readable.
- `captures/02-role-lineup-reduced-effects.png`: reduced-effects mode keeps the lineup composition readable without heavy FX reliance.
- `captures/grayscale/02-role-lineup-reduced-effects.png`: value-only read remains usable for broad composition checks.

## Capture Contents

Tower placements:

- Arrow: left upper build band.
- Control: right upper build band.
- Relay: left lower build band.
- Pulse: right lower build band.
- Prism: upper-left/mid build band.

Visible creep sends:

- Runner: 1
- Brute: 1
- Swarm: 3
- Shade: 1
- Siege: 1

## Missing Coverage

- A dedicated close-up or contact-sheet capture for per-role creep silhouette comparison.
- A later heavy-pressure lineup with repeated mixed-role waves.
