# Arrow And Runner Contact Sheet Brief

Date: 2026-07-15

## Goal

Generate first-pass AI concept contact sheets for the proof-of-method pair:

- Arrow tower: must beat the current source-kit rail/lens silhouette.
- Runner creep: must beat the current source-kit dart/spine silhouette.

These images are not runtime assets yet. They are selection inputs for the 2.5D token pipeline.

## Global Requirements

- 12 candidates per role.
- Transparent background when possible.
- Top-down three-quarter gameplay camera.
- Strong silhouette before surface detail.
- Readable at phone scale.
- No UI frame, no text, no labels, no watermark.
- No protected-game references, faction motifs, screenshots, command-card chrome, or copied icon compositions.

## Shared Style Block

```text
original mobile tower-wars game asset, ward-tech fantasy, clean readable board-game strategy token, top-down three-quarter view, strong silhouette, dark slate base, arcane blue and violet energy, signal gold accents, mint highlights, simple readable forms, polished early-2000s strategy game feel, transparent background, no text, no UI frame
```

## Required Avoidance Block

```text
no Warcraft, no Blizzard, no Warcraft III, no Horde, no Alliance, no Night Elf, no Undead, no Orc, no Human faction, no RTS command card, no copied game icon, no medieval faction banner, no realistic gore, no busy background, no tiny unreadable details, no text labels, no watermark
```

## Arrow Contact Sheet Prompt

```text
Create 12 distinct silhouette variations for a focused rail ward tower, compact base with long luminous bolt rail, glass lens core, forward muzzle, elegant side limbs, single-target precision silhouette, original ward-tech fantasy. Use this style: original mobile tower-wars game asset, ward-tech fantasy, clean readable board-game strategy token, top-down three-quarter view, strong silhouette, dark slate base, arcane blue and violet energy, signal gold accents, mint highlights, simple readable forms, polished early-2000s strategy game feel, transparent background, no text, no UI frame. Avoid: no Warcraft, no Blizzard, no Warcraft III, no Horde, no Alliance, no Night Elf, no Undead, no Orc, no Human faction, no RTS command card, no copied game icon, no medieval faction banner, no realistic gore, no busy background, no tiny unreadable details, no text labels, no watermark.
```

Expected output:

- `docs/art-pipeline/role-contact-sheets/tower_arrow_contact_sheet_v01.png`
- `docs/art-pipeline/review-notes/tower_arrow_contact_sheet_v01.md`

Selection bias:

- Prefer a dominant long rail/lens silhouette.
- Reject candidates that look like a generic gun, musket, medieval turret, or decorative statue.
- Reject candidates whose muzzle/lens would be unclear at the current gameplay camera.

## Runner Contact Sheet Prompt

```text
Create 12 distinct silhouette variations for a fast pressure construct creep, sharp dart body, glowing spine, low profile, speed streak cue, small readable silhouette, original ward-tech fantasy. Use this style: original mobile tower-wars game asset, ward-tech fantasy, clean readable board-game strategy token, top-down three-quarter view, strong silhouette, dark slate base, arcane blue and violet energy, signal gold accents, mint highlights, simple readable forms, polished early-2000s strategy game feel, transparent background, no text, no UI frame. Avoid: no Warcraft, no Blizzard, no Warcraft III, no Horde, no Alliance, no Night Elf, no Undead, no Orc, no Human faction, no RTS command card, no copied game icon, no medieval faction banner, no realistic gore, no busy background, no tiny unreadable details, no text labels, no watermark.
```

Expected output:

- `docs/art-pipeline/role-contact-sheets/creep_runner_contact_sheet_v01.png`
- `docs/art-pipeline/review-notes/creep_runner_contact_sheet_v01.md`

Selection bias:

- Prefer a low, narrow, directional dart/spine silhouette.
- Reject candidates that look like a tower projectile, a human character, or a generic insect.
- Reject candidates that only read through glow/color and fail in grayscale.

## Review Template

```markdown
# [Role] Contact Sheet V01 Review

- Date:
- Owner:
- Tool/model:
- Prompt library version: 2026-07-15
- Contact sheet path:
- Current proof asset compared against:
- Selected candidate:
- Rejected candidates:
- Phone-scale readability verdict:
- Grayscale readability verdict:
- Protected-IP/provenance verdict:
- Decision: reject / regenerate / clean source plate
- Next asset record:
```

