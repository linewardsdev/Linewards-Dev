---
name: screenshot-ui-review
description: Consistent screenshot-based UI, HUD, gameplay readability, and visual-regression review. Use when Codex needs to inspect screenshots or captured frames for UI clarity, mobile readability, art readability, layout regressions, overlay safety, before/after comparisons, game visual polish, or Line Wars creep/tower/lane screenshot audits.
---

# Screenshot UI Review

## Core workflow

1. Gather screenshot inputs and context:
   - paths to screenshots, videos, or rendered frames;
   - target platform/aspect ratio;
   - what changed;
   - intended user action or gameplay state.
2. If screenshots are not already available, capture or request them. For browser/app screenshots, use the available browser/app tools. For Unity/game work, prefer screenshots produced by a Unity-capable session.
3. Inspect each image visually. Use image viewing tools when local files exist.
4. Review against the rubric in `references/ui-screenshot-rubric.md`.
5. Produce a concise report with:
   - pass/fail/needs-review status;
   - top issues by severity;
   - screenshot-specific evidence;
   - recommended next fixes;
   - any missing shots needed for confidence.

## Report format

Use this structure unless the user requests a different format:

```markdown
# Screenshot UI Review

Status: Pass | Needs Review | Blocked

## Summary
- ...

## Findings
| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
| High | ... | ... | ... |

## Screenshot Notes
- `path/or/name.png`: ...

## Missing Coverage
- ...
```

For repeated reviews, run `scripts/create_review_report.py` to scaffold the Markdown report from screenshot paths.

## Line Wars creep/art review

For Line Wars creep or gameplay screenshots, always check:

- runner, brute, and swarm silhouettes are distinguishable without labels;
- phone-size readability;
- grayscale/value readability;
- heavy-send pressure with 20+ visible creeps;
- creeps do not hide grid cells, leak events, tower targets, lane map controls, or HUD actions;
- hit, kill, death, leak, and reduced-effects cues remain understandable;
- visual language stays original ward-tech fantasy and avoids protected-game silhouettes.

If the screenshots are generated placeholders, judge readability and composition first. Do not recommend final polish before silhouette readability passes.

## Severity levels

- High: blocks gameplay comprehension, touch use, or critical feedback.
- Medium: causes confusion, inconsistent state, or avoidable visual noise.
- Low: polish issue that does not block play or comprehension.

## Handling uncertainty

Call out uncertainty instead of over-claiming. If a screenshot is missing a required state, list it under Missing Coverage and request/capture the specific shot.
