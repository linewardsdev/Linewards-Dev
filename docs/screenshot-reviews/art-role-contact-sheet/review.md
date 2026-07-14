# Screenshot UI Review

Status: Pass

## Summary

- Added a close-up contact-sheet capture for the current five tower roles and five creep roles.
- The sheet renders prefab assets directly in a temporary editor scene, so it is useful for silhouette and value comparison without gameplay camera clutter.
- Tower range halos are hidden only for this contact sheet so the tower bodies can be compared cleanly.
- Normal and grayscale copies are generated.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Medium | Shade creep silhouette | `captures/grayscale/01-role-contact-sheet.png` shows Shade reading as a broad disk more than an echo/broken-outline creep. | In `art-creep-silhouette-polish`, break up Shade with offset echo shards or a crescent/afterimage profile that does not rely on transparency alone. |
| Low | Tower family | `captures/01-role-contact-sheet.png` shows all five towers separated by broad motifs: bow, dish, mast, drum, spire. | Keep these motifs as the icon/VFX source language. |
| Low | Brute/Siege distinction | Brute reads as wide armored mass; Siege reads directional and ram-like. | Preserve Siege's forward pressure shape and avoid widening it into another Brute. |

## Screenshot Notes

- `captures/01-role-contact-sheet.png`: close-up color comparison of all current tower and creep prefabs.
- `captures/grayscale/01-role-contact-sheet.png`: value/silhouette check without color dependence.

## Contact Sheet Contents

Towers:

- Arrow
- Control
- Relay
- Pulse
- Prism

Creeps:

- Runner
- Brute
- Swarm
- Shade
- Siege

## Missing Coverage

- Contact sheet does not show attack, hit, death, leak, or movement state.
- Contact sheet does not replace the gameplay-scale role lineup; use it alongside `docs/screenshot-reviews/art-role-lineup-capture/`.
