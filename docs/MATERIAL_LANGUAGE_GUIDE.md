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

## Runtime Surface Values (authoritative)

These are set by `TowerBodyMaterialTuning` and `CreepBodyMaterialTuning` in `Assets/Editor`,
which are the single source of truth for the thirty-one body materials (16 towers, 15 creeps). Edit those, not the
`.mat` files — hand edits get overwritten the next time the tools run, and the tools exist
because the values want to be reviewable together.

Both are asserted by validators; see [Render and art validation](RENDER_AND_ART_VALIDATION.md).

### Smoothness is 0.45 on all thirty-one bodies

Measured, not chosen by eye. The baked metallic-gloss maps average 0.579 in their smoothness
channel on towers and 0.652 on creeps, so 0.45 lands **effective** smoothness near 0.26–0.29:
a broad, dim specular lobe with no hotspot.

It replaced two values that were wrong in opposite directions, which is why "match the good
cluster" was the wrong instruction:

| Value | Effective | Reads as |
| ---: | ---: | --- |
| 1.0 | 0.579 | tight glossy highlight — wet plastic on cast stone |
| **0.45** | **0.26–0.29** | broad soft gradient, no hotspot |
| 0.12 | 0.070 | no specular response at all |

**As of 2026-08-08 only one material actually carries 0.45.** The thirty that predate Twin
Crescent sit at 0.42 and fail their validators; Twin Crescent was tuned as it was added and
passes. The constant below is the intent, not the committed state — see open item 39, which
holds the decision about whether to move thirty materials or the constant.

The reference look carries richness through value gradients and rim light rather than
hotspots, and a wide dim lobe is what produces that. Creeps deliberately share the tower
value: they share a frame and a light rig, and two specular widths read as two art styles.

**This is a defensible starting point, not a tuned final value.** It wants a human look
against a capture.

### Emission is authored against the bloom threshold, which is 1.05

**Below the threshold, emission is not dim — it is absent.** There is no partial credit, and
this is the single most common way emissive work here has been wasted.

- **Towers** carry a per-role HDR colour peaking near 2.0, hue-spread so towers stay tellable
  apart by accent. Spore Cloud's is pinned rather than chosen — it is its own fog shader's
  colour scaled to that peak, so the tower and the cloud it emits agree. Barricade is
  deliberately the dimmest and least saturated: it is a wall, and a glowing wall reads as a
  power source.
- **Creeps** carry a flat 1.8 multiplier, because creep emission colour multiplies a baked
  map that already holds the hue. Giving a creep a coloured emission would fight its own map
  rather than tune it. At the previous 1.0 against LDR maps the product could never exceed
  1.0, so no creep in the game could bloom.

Creeps with **no emission map are left black on purpose.** Emission with no map multiplies
against 1 and lights the entire body uniformly — a lantern rather than a highlight. Eight of
fifteen creeps currently have no usable emissive detail; five have no map at all and three
have one that is functionally blank. That is art generation, not a material fix
(`OPEN_ITEMS.md` item 19).

### `_GlossyReflections` must stay on

It was off on ten towers and ten creeps, which limited the scene's environment reflections to
a third of the roster. A reflection source that reaches nothing and a surface that reflects
nothing look identical, so that defect and the missing reflection probe concealed each other
until both were measured.

## Screenshot Gate

Any new material pass should be checked in:

- normal color;
- grayscale;
- heavy pressure;
- reduced effects;
- phone framing;
- at least one active tower/creep interaction.

Passing means a new tester can still identify the path, placement area, active threats, health state, and primary buttons without zooming.
