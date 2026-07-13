# Creep Material Palette

Use this material direction for generated placeholders and final polished creep prefabs.

## Slots

| Slot | Purpose | Suggested Color | Notes |
| --- | --- | --- | --- |
| Body | Main readable silhouette | Arcane blue/cyan | Must remain readable on the dark slate board. |
| Sender Accent | Player ownership/read | Gold, violet, or red by sender | This slot is runtime-tinted by `UnityVerticalSliceRenderer`. |
| Damage | Low-health/cracked read | Warm red-orange or desaturated slate | This slot is runtime-tinted when health is low. |
| Shadow | Board contact | Transparent dark ink | Keep soft and small so it does not hide grid cells. |

## Style Rules

- Prefer flat colors with subtle gradients over noisy textures.
- Keep value contrast high enough for phone-size readability.
- Avoid medieval metal, Warcraft-like faction colors, creature skin, gore, or nostalgia RTS details.
- Treat final material polish as a readability pass first and a decoration pass second.
