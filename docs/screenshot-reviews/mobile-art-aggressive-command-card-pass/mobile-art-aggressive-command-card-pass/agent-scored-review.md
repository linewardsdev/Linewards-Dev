# Agent-Scored Aggressive Command Card Pass

## Summary

This pass used `-ltwIntensity aggressive` to push the command cards further toward icon-first mobile controls. Runtime cards now use larger icon wells, compact role codes, and smaller meta/cost text so the card face is less text-heavy.

## Verdict

- Result: Pass for aggressive command-card readability delta, with medium polish follow-ups.
- Runtime promotion: Keep the compact role-code direction for now.
- Main improvement: command card labels and costs no longer crowd the icon as heavily.
- Main limitation: role codes are a practical bridge, not final naming/UI language.
- Next package: replace text role codes with icon/tab/chip language and improve resource chips.

## Agent Scorecard

| Category | Score | Assessment |
| --- | ---: | --- |
| Command card readability | 2/3 | Better hierarchy: icon first, compact label second, cost third. |
| Mobile touch/read fit | 2/3 | Cards remain stable and easier to scan at phone scale. |
| Grayscale value separation | 2/3 | Icons and compact labels survive grayscale better than the long labels. |
| Aggressive delta strength | 2/3 | The change is visible and meaningfully changes the card read. |
| Final UI polish | 1/3 | The role codes are still utilitarian and need a designed final treatment. |

## Findings

### High

- None.

### Medium

- Role codes (`ARW`, `BRT`, `SWM`) improve spacing but are less self-explanatory for new players.
- Cost/meta text is cleaner but should become a proper tiny resource chip or iconized cost indicator.
- The command card art is still procedural chrome; a final card skin should eventually replace the IMGUI look.

### Low

- Selected and disabled states still read after the compact-label change.
- The larger icon well makes the AI plate command icons carry more of the role identity.

## Evidence

- Focused sheet: `agent-review-contact-sheets/standard-command-card-compact-color-grayscale.png`
- Full matrix: `agent-review-contact-sheets/standard-12-state-command-card-after-matrix.png`
- Standard send drawer: `after/phone-standard-portrait/04-send-menu-open.png`
- Standard disabled send drawer: `after/phone-standard-portrait/05-send-card-disabled.png`
