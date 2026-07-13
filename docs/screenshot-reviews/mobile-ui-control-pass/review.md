# Screenshot UI Review

Status: Blocked

## Summary
- Core control wiring was reviewed in code after the HUD/menu changes.
- Stats drawer was simplified from eight equal-weight tiles into four primary stats plus one compact secondary line.
- Primary HUD cleanup removed the top-left `YOUR LINE` tile usage and removed the `RED FX` tile from the header.
- Final visual screenshot review is blocked because local macOS screen capture failed with `could not create image from display`.

## Findings
| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| Medium | Screenshot coverage | No fresh post-change screenshot could be captured from the Unity Game view. | Capture default HUD, stats open, build menu open, send menu open, lane selector open, and running state from Unity. |
| Low | Primary HUD hierarchy | Stats drawer now prioritizes lives, gold, income, and income timer; secondary stats are compact text. | Verify phone-size readability in the next capture pass. |

## Screenshot Notes
- Missing: post-change Unity screenshots.

## Missing Coverage
- Default HUD.
- Stats drawer open.
- Build menu open.
- Send menu open.
- Lane selector open.
- Running state with PLAY/PAUSE controls visible.
