# Line Wards Material Language Guide

This guide is the baseline art-production rule set for moving Line Wards from primitive readability toward a more intentional early-2000s tactics style.

## Goals

- Keep the board quieter than decisions: towers, creeps, shots, health, send/leak cues, and HUD elements must always win the value hierarchy.
- Use materials to explain gameplay: route, buildable cells, ownership, danger, health, and pressure should read before decorative detail.
- Preserve originality: Line Wards should feel like a compact neon board defense game, not a direct clone of another mobile strategy interface.

## Material Families

| Family | Use | Visual Rules |
| --- | --- | --- |
| Stone / tile | Build bands, lane floor, quiet board structure | Low saturation, small value steps, restrained cracks/chips, no high-contrast noise in active placement areas |
| Worn route | Creep path | Slightly brighter value than build bands, directional wear marks, repeated arrows/tick marks only where they aid flow reading |
| Metal / rail | Lane rails, gates, tower bases, hard UI trim | Stronger edge highlights, darker underside/contact shadows, chunky bevel impression |
| Crystal / focus | Prism/energy tower accents, premium targeting language | High-value core, small saturated accents, used sparingly so it does not compete with health or gold |
| Energy / signal | Shots, send, transfer, income, selected states | Short-lived, high contrast, distinct shapes per event; never rely on color alone |
| Health | Creep health bars, wound pips, damage state | High value contrast, consistent position per role, visible in grayscale, no decorative clutter behind the bar |
| Ownership | Player/lane identity | Border/rail accents and small badges; avoid flooding the whole board with owner color |

## Palette And Value Rules

- Board baseline should sit in dark-to-mid values; gameplay actors should be one clear value step brighter.
- Route tiles must remain readable in grayscale through value and edge treatment, not only blue hue.
- Health, leak, transfer, and income cues need value contrast and text/shape support so reduced-effects mode remains playable.
- Use saturation in layers:
  1. HUD decisions and critical alerts.
  2. Towers, creeps, shots, and health.
  3. Spawn/exit/route guidance.
  4. Board decoration.
- Avoid one-hue themes. A lane can lean cool or warm, but the route, build bands, rails, actors, and alerts need separate values and materials.

## Screenshot Gate

Any new material pass should be checked in:

- normal color;
- grayscale;
- heavy pressure;
- reduced effects;
- phone framing;
- at least one active tower/creep interaction.

Passing means a new tester can still identify the path, placement area, active threats, health state, and primary buttons without zooming.
