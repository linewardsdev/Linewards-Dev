# Mobile Art Improvement Cycle Report

## Audit

- Run ID: `mobile-art-breakthrough-spawn-leak-board-pass`
- Package: `GD-Mobile-UI-Board-SpawnLeak`
- Intensity: `breakthrough`
- Expected delta: Large visual direction push that may temporarily break spacing or balance.
- Actual delta: Pending agent visual scoring against the captured evidence.
- Phase: `after`
- Seed: `1`
- Current manifest: `after-capture-manifest.json`
- Comparison manifest: `missing`

## Scope

- Intended gameplay read: mobile portrait art-direction evidence for the selected package.
- Assets and systems changed: recorded by the implementation branch; this report covers capture evidence.
- Explicit exclusions: automated visual taste judgment, final promotion approval, and live manual play feel.

## Target References

Every visual score in this run must compare the captured runtime output to these selected targets, not just to general taste.

| Target | Track | Selected Option | Reference | Runtime Translation Target | Required Evidence | Match Score |
| --- | --- | --- | --- | --- | --- | ---: |
| `ui-command-cards-option-04` | Command Cards | Option 4 | [view](target-references/ui-command-cards-option-04.png) | Use simple readable command-card chrome with clear selected, disabled, and normal states. | build-menu-open, build-card-selected, send-menu-open, send-card-disabled | 2/3 |
| `ui-controls-option-01` | Map/Lane/Status Controls | Option 1 | [view](target-references/ui-controls-option-01.png) | Keep persistent map/lane controls icon-first, reachable, and visually separate from temporary status panels. | default-hud, lane-selector-open | 1/3 |
| `ui-hud-chrome-option-06` | HUD Chrome | Option 6 | [view](target-references/ui-hud-chrome-option-06.png) | Translate the dimensional HUD module into compact portrait-safe stat chrome without overlapping lane action. | default-hud, active-combat, grayscale default-hud | 1/3 |
| `ui-icon-family-option-06` | Icon Family | Option 6 | [view](target-references/ui-icon-family-option-06.png) | Use simplified role silhouettes for command readability after card sizing is stable. | build-card-selected, send-card-disabled, grayscale command states | 0/3 |
| `board-material-option-11` | Board Material | Option 11 | [view](target-references/board-material-option-11.png) | Use restrained slate board materials and triangular route cues that support units instead of overpowering them. | board-overview, active-combat, grayscale board-overview | 2/3 |
| `spawn-leak-gates-option-11` | Endpoint Gates | Option 11 | [view](target-references/spawn-leak-gates-option-11.png) | Translate the compact circular spawn platform and drain-like leak gate into readable endpoint art at lane scale. | spawn-gate-focus, leak-gate-focus, board-overview, grayscale endpoint focus | 2/3 |

## Capture Matrix

- Captures passed: `60/60`
- Grayscale copies present: `60/60`
- Canonical matrix complete: `True`

| Profile | State | Color | Grayscale | Status |
| --- | --- | --- | --- | --- |
| `phone-small-portrait` | `default-hud` | [view](after/phone-small-portrait/01-default-hud.png) | [view](after/phone-small-portrait/grayscale/01-default-hud.png) | Pass |
| `phone-small-portrait` | `build-menu-open` | [view](after/phone-small-portrait/02-build-menu-open.png) | [view](after/phone-small-portrait/grayscale/02-build-menu-open.png) | Pass |
| `phone-small-portrait` | `build-card-selected` | [view](after/phone-small-portrait/03-build-card-selected.png) | [view](after/phone-small-portrait/grayscale/03-build-card-selected.png) | Pass |
| `phone-small-portrait` | `send-menu-open` | [view](after/phone-small-portrait/04-send-menu-open.png) | [view](after/phone-small-portrait/grayscale/04-send-menu-open.png) | Pass |
| `phone-small-portrait` | `send-card-disabled` | [view](after/phone-small-portrait/05-send-card-disabled.png) | [view](after/phone-small-portrait/grayscale/05-send-card-disabled.png) | Pass |
| `phone-small-portrait` | `lane-selector-open` | [view](after/phone-small-portrait/06-lane-selector-open.png) | [view](after/phone-small-portrait/grayscale/06-lane-selector-open.png) | Pass |
| `phone-small-portrait` | `active-combat` | [view](after/phone-small-portrait/07-active-combat.png) | [view](after/phone-small-portrait/grayscale/07-active-combat.png) | Pass |
| `phone-small-portrait` | `runner-10-pressure` | [view](after/phone-small-portrait/08-runner-10-pressure.png) | [view](after/phone-small-portrait/grayscale/08-runner-10-pressure.png) | Pass |
| `phone-small-portrait` | `swarm-heavy-pressure` | [view](after/phone-small-portrait/09-swarm-heavy-pressure.png) | [view](after/phone-small-portrait/grayscale/09-swarm-heavy-pressure.png) | Pass |
| `phone-small-portrait` | `heavy-pressure` | [view](after/phone-small-portrait/10-heavy-pressure.png) | [view](after/phone-small-portrait/grayscale/10-heavy-pressure.png) | Pass |
| `phone-small-portrait` | `reduced-effects-heavy` | [view](after/phone-small-portrait/11-reduced-effects-heavy.png) | [view](after/phone-small-portrait/grayscale/11-reduced-effects-heavy.png) | Pass |
| `phone-small-portrait` | `board-overview` | [view](after/phone-small-portrait/12-board-overview.png) | [view](after/phone-small-portrait/grayscale/12-board-overview.png) | Pass |
| `phone-small-portrait` | `spawn-gate-focus` | [view](after/phone-small-portrait/13-spawn-gate-focus.png) | [view](after/phone-small-portrait/grayscale/13-spawn-gate-focus.png) | Pass |
| `phone-small-portrait` | `leak-gate-focus` | [view](after/phone-small-portrait/14-leak-gate-focus.png) | [view](after/phone-small-portrait/grayscale/14-leak-gate-focus.png) | Pass |
| `phone-small-portrait` | `results-or-late-match` | [view](after/phone-small-portrait/15-results-or-late-match.png) | [view](after/phone-small-portrait/grayscale/15-results-or-late-match.png) | Pass |
| `phone-standard-portrait` | `default-hud` | [view](after/phone-standard-portrait/01-default-hud.png) | [view](after/phone-standard-portrait/grayscale/01-default-hud.png) | Pass |
| `phone-standard-portrait` | `build-menu-open` | [view](after/phone-standard-portrait/02-build-menu-open.png) | [view](after/phone-standard-portrait/grayscale/02-build-menu-open.png) | Pass |
| `phone-standard-portrait` | `build-card-selected` | [view](after/phone-standard-portrait/03-build-card-selected.png) | [view](after/phone-standard-portrait/grayscale/03-build-card-selected.png) | Pass |
| `phone-standard-portrait` | `send-menu-open` | [view](after/phone-standard-portrait/04-send-menu-open.png) | [view](after/phone-standard-portrait/grayscale/04-send-menu-open.png) | Pass |
| `phone-standard-portrait` | `send-card-disabled` | [view](after/phone-standard-portrait/05-send-card-disabled.png) | [view](after/phone-standard-portrait/grayscale/05-send-card-disabled.png) | Pass |
| `phone-standard-portrait` | `lane-selector-open` | [view](after/phone-standard-portrait/06-lane-selector-open.png) | [view](after/phone-standard-portrait/grayscale/06-lane-selector-open.png) | Pass |
| `phone-standard-portrait` | `active-combat` | [view](after/phone-standard-portrait/07-active-combat.png) | [view](after/phone-standard-portrait/grayscale/07-active-combat.png) | Pass |
| `phone-standard-portrait` | `runner-10-pressure` | [view](after/phone-standard-portrait/08-runner-10-pressure.png) | [view](after/phone-standard-portrait/grayscale/08-runner-10-pressure.png) | Pass |
| `phone-standard-portrait` | `swarm-heavy-pressure` | [view](after/phone-standard-portrait/09-swarm-heavy-pressure.png) | [view](after/phone-standard-portrait/grayscale/09-swarm-heavy-pressure.png) | Pass |
| `phone-standard-portrait` | `heavy-pressure` | [view](after/phone-standard-portrait/10-heavy-pressure.png) | [view](after/phone-standard-portrait/grayscale/10-heavy-pressure.png) | Pass |
| `phone-standard-portrait` | `reduced-effects-heavy` | [view](after/phone-standard-portrait/11-reduced-effects-heavy.png) | [view](after/phone-standard-portrait/grayscale/11-reduced-effects-heavy.png) | Pass |
| `phone-standard-portrait` | `board-overview` | [view](after/phone-standard-portrait/12-board-overview.png) | [view](after/phone-standard-portrait/grayscale/12-board-overview.png) | Pass |
| `phone-standard-portrait` | `spawn-gate-focus` | [view](after/phone-standard-portrait/13-spawn-gate-focus.png) | [view](after/phone-standard-portrait/grayscale/13-spawn-gate-focus.png) | Pass |
| `phone-standard-portrait` | `leak-gate-focus` | [view](after/phone-standard-portrait/14-leak-gate-focus.png) | [view](after/phone-standard-portrait/grayscale/14-leak-gate-focus.png) | Pass |
| `phone-standard-portrait` | `results-or-late-match` | [view](after/phone-standard-portrait/15-results-or-late-match.png) | [view](after/phone-standard-portrait/grayscale/15-results-or-late-match.png) | Pass |
| `phone-tall-portrait` | `default-hud` | [view](after/phone-tall-portrait/01-default-hud.png) | [view](after/phone-tall-portrait/grayscale/01-default-hud.png) | Pass |
| `phone-tall-portrait` | `build-menu-open` | [view](after/phone-tall-portrait/02-build-menu-open.png) | [view](after/phone-tall-portrait/grayscale/02-build-menu-open.png) | Pass |
| `phone-tall-portrait` | `build-card-selected` | [view](after/phone-tall-portrait/03-build-card-selected.png) | [view](after/phone-tall-portrait/grayscale/03-build-card-selected.png) | Pass |
| `phone-tall-portrait` | `send-menu-open` | [view](after/phone-tall-portrait/04-send-menu-open.png) | [view](after/phone-tall-portrait/grayscale/04-send-menu-open.png) | Pass |
| `phone-tall-portrait` | `send-card-disabled` | [view](after/phone-tall-portrait/05-send-card-disabled.png) | [view](after/phone-tall-portrait/grayscale/05-send-card-disabled.png) | Pass |
| `phone-tall-portrait` | `lane-selector-open` | [view](after/phone-tall-portrait/06-lane-selector-open.png) | [view](after/phone-tall-portrait/grayscale/06-lane-selector-open.png) | Pass |
| `phone-tall-portrait` | `active-combat` | [view](after/phone-tall-portrait/07-active-combat.png) | [view](after/phone-tall-portrait/grayscale/07-active-combat.png) | Pass |
| `phone-tall-portrait` | `runner-10-pressure` | [view](after/phone-tall-portrait/08-runner-10-pressure.png) | [view](after/phone-tall-portrait/grayscale/08-runner-10-pressure.png) | Pass |
| `phone-tall-portrait` | `swarm-heavy-pressure` | [view](after/phone-tall-portrait/09-swarm-heavy-pressure.png) | [view](after/phone-tall-portrait/grayscale/09-swarm-heavy-pressure.png) | Pass |
| `phone-tall-portrait` | `heavy-pressure` | [view](after/phone-tall-portrait/10-heavy-pressure.png) | [view](after/phone-tall-portrait/grayscale/10-heavy-pressure.png) | Pass |
| `phone-tall-portrait` | `reduced-effects-heavy` | [view](after/phone-tall-portrait/11-reduced-effects-heavy.png) | [view](after/phone-tall-portrait/grayscale/11-reduced-effects-heavy.png) | Pass |
| `phone-tall-portrait` | `board-overview` | [view](after/phone-tall-portrait/12-board-overview.png) | [view](after/phone-tall-portrait/grayscale/12-board-overview.png) | Pass |
| `phone-tall-portrait` | `spawn-gate-focus` | [view](after/phone-tall-portrait/13-spawn-gate-focus.png) | [view](after/phone-tall-portrait/grayscale/13-spawn-gate-focus.png) | Pass |
| `phone-tall-portrait` | `leak-gate-focus` | [view](after/phone-tall-portrait/14-leak-gate-focus.png) | [view](after/phone-tall-portrait/grayscale/14-leak-gate-focus.png) | Pass |
| `phone-tall-portrait` | `results-or-late-match` | [view](after/phone-tall-portrait/15-results-or-late-match.png) | [view](after/phone-tall-portrait/grayscale/15-results-or-late-match.png) | Pass |
| `phone-safe-area-portrait` | `default-hud` | [view](after/phone-safe-area-portrait/01-default-hud.png) | [view](after/phone-safe-area-portrait/grayscale/01-default-hud.png) | Pass |
| `phone-safe-area-portrait` | `build-menu-open` | [view](after/phone-safe-area-portrait/02-build-menu-open.png) | [view](after/phone-safe-area-portrait/grayscale/02-build-menu-open.png) | Pass |
| `phone-safe-area-portrait` | `build-card-selected` | [view](after/phone-safe-area-portrait/03-build-card-selected.png) | [view](after/phone-safe-area-portrait/grayscale/03-build-card-selected.png) | Pass |
| `phone-safe-area-portrait` | `send-menu-open` | [view](after/phone-safe-area-portrait/04-send-menu-open.png) | [view](after/phone-safe-area-portrait/grayscale/04-send-menu-open.png) | Pass |
| `phone-safe-area-portrait` | `send-card-disabled` | [view](after/phone-safe-area-portrait/05-send-card-disabled.png) | [view](after/phone-safe-area-portrait/grayscale/05-send-card-disabled.png) | Pass |
| `phone-safe-area-portrait` | `lane-selector-open` | [view](after/phone-safe-area-portrait/06-lane-selector-open.png) | [view](after/phone-safe-area-portrait/grayscale/06-lane-selector-open.png) | Pass |
| `phone-safe-area-portrait` | `active-combat` | [view](after/phone-safe-area-portrait/07-active-combat.png) | [view](after/phone-safe-area-portrait/grayscale/07-active-combat.png) | Pass |
| `phone-safe-area-portrait` | `runner-10-pressure` | [view](after/phone-safe-area-portrait/08-runner-10-pressure.png) | [view](after/phone-safe-area-portrait/grayscale/08-runner-10-pressure.png) | Pass |
| `phone-safe-area-portrait` | `swarm-heavy-pressure` | [view](after/phone-safe-area-portrait/09-swarm-heavy-pressure.png) | [view](after/phone-safe-area-portrait/grayscale/09-swarm-heavy-pressure.png) | Pass |
| `phone-safe-area-portrait` | `heavy-pressure` | [view](after/phone-safe-area-portrait/10-heavy-pressure.png) | [view](after/phone-safe-area-portrait/grayscale/10-heavy-pressure.png) | Pass |
| `phone-safe-area-portrait` | `reduced-effects-heavy` | [view](after/phone-safe-area-portrait/11-reduced-effects-heavy.png) | [view](after/phone-safe-area-portrait/grayscale/11-reduced-effects-heavy.png) | Pass |
| `phone-safe-area-portrait` | `board-overview` | [view](after/phone-safe-area-portrait/12-board-overview.png) | [view](after/phone-safe-area-portrait/grayscale/12-board-overview.png) | Pass |
| `phone-safe-area-portrait` | `spawn-gate-focus` | [view](after/phone-safe-area-portrait/13-spawn-gate-focus.png) | [view](after/phone-safe-area-portrait/grayscale/13-spawn-gate-focus.png) | Pass |
| `phone-safe-area-portrait` | `leak-gate-focus` | [view](after/phone-safe-area-portrait/14-leak-gate-focus.png) | [view](after/phone-safe-area-portrait/grayscale/14-leak-gate-focus.png) | Pass |
| `phone-safe-area-portrait` | `results-or-late-match` | [view](after/phone-safe-area-portrait/15-results-or-late-match.png) | [view](after/phone-safe-area-portrait/grayscale/15-results-or-late-match.png) | Pass |

## Scorecard

Machine coverage scores are not final art scores. Agent reviewer score must be filled before handoff.

| Category | Machine Coverage | Reviewer Score | Evidence | Notes |
| --- | ---: | ---: | --- | --- |
| Mobile arena fit | 2/3 | /3 | [default-hud](after/phone-standard-portrait/01-default-hud.png), [active-combat](after/phone-standard-portrait/07-active-combat.png) | Canonical evidence exists; assign the final visual score during review. |
| Long north-south lane readability | 2/3 | /3 | [default-hud](after/phone-standard-portrait/01-default-hud.png), [active-combat](after/phone-standard-portrait/07-active-combat.png) | Canonical evidence exists; assign the final visual score during review. |
| Spawn, route, and leak-gate clarity | 2/3 | /3 | [board-overview](after/phone-standard-portrait/12-board-overview.png), [spawn-gate-focus](after/phone-standard-portrait/13-spawn-gate-focus.png), [leak-gate-focus](after/phone-standard-portrait/14-leak-gate-focus.png), [results-or-late-match](after/phone-standard-portrait/15-results-or-late-match.png) | Canonical evidence exists; assign the final visual score during review. |
| UI edge discipline and touch clearance | 2/3 | /3 | [default-hud](after/phone-standard-portrait/01-default-hud.png), [build-menu-open](after/phone-standard-portrait/02-build-menu-open.png), [send-menu-open](after/phone-standard-portrait/04-send-menu-open.png), [lane-selector-open](after/phone-standard-portrait/06-lane-selector-open.png) | Canonical evidence exists; assign the final visual score during review. |
| Tower silhouette and role identity | 1/3 | /3 | [default-hud](after/phone-standard-portrait/01-default-hud.png), [active-combat](after/phone-standard-portrait/07-active-combat.png) | Canonical evidence exists, but this category requires one or more focused captures before lock. |
| Creep silhouette and threat identity | 2/3 | /3 | [active-combat](after/phone-standard-portrait/07-active-combat.png), [runner-10-pressure](after/phone-standard-portrait/08-runner-10-pressure.png), [swarm-heavy-pressure](after/phone-standard-portrait/09-swarm-heavy-pressure.png) | Canonical evidence exists; assign the final visual score during review. |
| Grayscale value separation | 2/3 | /3 | [default-hud](after/phone-standard-portrait/01-default-hud.png), [active-combat](after/phone-standard-portrait/07-active-combat.png) | Canonical evidence exists; assign the final visual score during review. |
| Heavy-pressure readability | 2/3 | /3 | [runner-10-pressure](after/phone-standard-portrait/08-runner-10-pressure.png), [swarm-heavy-pressure](after/phone-standard-portrait/09-swarm-heavy-pressure.png), [heavy-pressure](after/phone-standard-portrait/10-heavy-pressure.png) | Canonical evidence exists; assign the final visual score during review. |
| Reduced-effects readability | 2/3 | /3 | [reduced-effects-heavy](after/phone-standard-portrait/11-reduced-effects-heavy.png) | Canonical evidence exists; assign the final visual score during review. |
| Combat signal priority | 2/3 | /3 | [active-combat](after/phone-standard-portrait/07-active-combat.png), [heavy-pressure](after/phone-standard-portrait/10-heavy-pressure.png) | Canonical evidence exists; assign the final visual score during review. |
| Motion clarity | 1/3 | /3 | [default-hud](after/phone-standard-portrait/01-default-hud.png), [active-combat](after/phone-standard-portrait/07-active-combat.png) | Canonical evidence exists, but this category requires one or more focused captures before lock. |
| Palette and material cohesion | 2/3 | /3 | [default-hud](after/phone-standard-portrait/01-default-hud.png), [active-combat](after/phone-standard-portrait/07-active-combat.png) | Canonical evidence exists; assign the final visual score during review. |
| Icon-to-runtime silhouette match | 2/3 | /3 | [build-card-selected](after/phone-standard-portrait/03-build-card-selected.png), [send-card-disabled](after/phone-standard-portrait/05-send-card-disabled.png), [active-combat](after/phone-standard-portrait/07-active-combat.png) | Canonical evidence exists; assign the final visual score during review. |
| Original Line Wards identity | 2/3 | /3 | [default-hud](after/phone-standard-portrait/01-default-hud.png), [active-combat](after/phone-standard-portrait/07-active-combat.png) | Canonical evidence exists; assign the final visual score during review. |
| Fallback and missing-asset behavior | 1/3 | /3 | [default-hud](after/phone-standard-portrait/01-default-hud.png), [active-combat](after/phone-standard-portrait/07-active-combat.png) | Canonical evidence exists, but this category requires one or more focused captures before lock. |

## High

- None recorded.

## Medium

- No opposite-phase manifest was found, so this run cannot yet compare before and after evidence.

## Low

- Machine scores measure evidence coverage and target-reference presence. The working graphics or implementation agent must assign visual quality scores before handoff.
- Batch HUD overlays are deterministic approximations of runtime UI; live Game View checks remain useful before final lock.

## Pipeline Reconciliation

### Completed Evidence
- 6 selected target reference image(s) copied into this run for direct visual comparison.
- Four portrait phone profiles captured: small, standard, tall, and safe-area.
- 15 canonical visual states captured for every selected profile.
- Selected command-card, disabled command-card, Runner x10 pressure, and heavy Swarm pressure states are included in the canonical matrix.
- Board overview, spawn-gate focus, and leak-gate focus states are included for endpoint review.
- Grayscale copies generated for value/readability review.
- Machine-readable manifest generated for the current phase.

### Open Evidence Gaps
- true map-camera view, if the package touches map/lane behavior

### Recommended Next Packages
- Run the opposite `before` phase with the same run id and seed.
- GD-Mobile-UI-Board: agent-score selected/disabled command states and continue HUD typography scale work.
- GD-Creep-Identity: agent-score Runner x10 and heavy Swarm pressure evidence, then tune silhouettes if needed.
- GD-Art-Pipeline-Hygiene: update the owning checklist with this report path and target-reference match scores after agent scoring.

## Verdict

- Result: Capture pass complete; before/after comparison pending
- Runtime promotion: pending human review.
- Fallback status: verify in the owning package before promotion.
- Next package: use the first applicable recommendation above.

## Agent Visual Score

- Review: [agent-scored-review.md](agent-scored-review.md)
- Comparison sheet: [target-ref-board-spawn-leak-comparison.png](agent-review-contact-sheets/target-ref-board-spawn-leak-comparison.png)
- Result: Pass with polish follow-ups; board material and endpoint gates improved to 2/3 target match.
