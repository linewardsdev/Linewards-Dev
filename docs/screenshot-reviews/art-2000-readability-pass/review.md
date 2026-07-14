# Screenshot UI Review

Status: Needs Review

## Summary

- The 2000-readability pass improved the active-lane visual hierarchy: route arrows are quieter, prefab units read larger, tower/creep hits have stronger shape cues, and the Play/Reset controls no longer collide horizontally.
- The capture runner now produced both color and grayscale evidence: 8 normal screenshots plus 8 grayscale/value screenshots.
- This pass is an improvement, not the final art baseline. The remaining issues are now more specific: lane selector/control density on the right side, value separation under beam-heavy combat, and final role-specific asset silhouettes.

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Medium | Right-side control density | `captures/01-default-hud.png`, `captures/06-heavy-pressure.png`: Play/Pause, reset, lane selector, and occasional beam lines occupy the same right-side strip. | Next UI pass should reserve a dedicated control rail or move reset behind a secondary gesture/menu. |
| Medium | Heavy-pressure value separation | `captures/grayscale/06-heavy-pressure.png`: creeps and tower bodies survive grayscale better than before, but beams and right-side UI still compete. | Keep beams thicker but shorten/shape them by tower role; avoid long diagonal lines through the control rail. |
| Medium | Final role silhouette quality | `captures/05-active-combat.png`, `captures/07-reduced-effects-heavy.png`: units are larger and readable as objects, but still placeholder-level. | Next art pass should replace generated meshes with stronger low-poly role silhouettes. |
| Low | Route readability | `captures/05-active-combat.png`: route arrows are quieter and no longer dominate combat. | Keep current route brightness as the baseline unless future testers lose path direction. |

## Screenshot Notes

- `captures/01-default-hud.png`: Play/Reset cluster is cleaner; lane selector remains close but usable.
- `captures/05-active-combat.png`: Larger tower/creep silhouettes and quieter arrows improve first read.
- `captures/06-heavy-pressure.png`: Hit-frame cues and thicker beams communicate active targeting better.
- `captures/07-reduced-effects-heavy.png`: Reduced-effects state remains readable, with larger silhouettes helping role visibility.
- `captures/grayscale/06-heavy-pressure.png`: Value-only review is now available and shows improved but not final separation.

## Missing Coverage

- Side-by-side crop comparison against the previous prefab-foundation capture set.
- Interactive phone-scale Unity Game view confirmation after merge/push.
- Final polished mesh/icon review for the 10 role identities.
