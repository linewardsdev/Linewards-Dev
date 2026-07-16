# Selected Source Plates V01

Date: 2026-07-15

These source plates track the selected contact-sheet candidates through cleanup. They are staging assets, not final production sprites.

Important:

- `v01` files are first-pass contact-sheet crops with the original dark contact-sheet background.
- `ChromaKey/*_v02_key.png` files are regenerated clean candidates on a magenta removal background.
- `*_source_plate_v02.png` files are transparent alpha extractions from the chroma-key plates.
- `*_source_plate_v03.png` files are normalized 1024x1024 transparent source plates with role-family scale rules.
- `Grayscale/*_source_plate_v03_grayscale.png` files are grayscale review copies.
- Use `v03` files for the next phone-scale and Unity proof tests.

## Source Plates

| Role | Selected Candidate | Source Plate |
| --- | ---: | --- |
| Builder | 11 | `builder_source_plate_v03.png` |
| Arrow tower | 3 | `tower_arrow_source_plate_v03.png` |
| Control tower | 6 | `tower_control_source_plate_v03.png` |
| Relay tower | 12 | `tower_relay_source_plate_v03.png` |
| Pulse tower | 8 | `tower_pulse_source_plate_v03.png` |
| Prism tower | 3 | `tower_prism_source_plate_v03.png` |
| Runner creep | 11 | `creep_runner_source_plate_v03.png` |
| Brute creep | 4 | `creep_brute_source_plate_v03.png` |
| Swarm creep | 3 | `creep_swarm_source_plate_v03.png` |
| Shade creep | 2 | `creep_shade_source_plate_v03.png` |
| Siege creep | 11 | `creep_siege_source_plate_v03.png` |

## Cleanup Checklist

- [x] Remove or replace contact-sheet background.
- [x] Normalize object scale by role family.
- [x] Create grayscale review copies.
- [ ] Compare Arrow and Runner against current source-kit proof prefabs.
- [ ] Promote only if source plate is clearer at gameplay scale.
- [ ] Record final production asset paths in `docs/art-pipeline/ai-art-generation-log.md`.

## V03 Notes

- Towers are normalized larger than creeps to preserve the intended hierarchy.
- Swarm is normalized wider than single creeps because the role is a cluster.
- Pulse and Control include broad energy rings in the generated plate; verify these do not fight runtime VFX or grid readability before promotion.
- Siege's generated plate has some vehicle-like wheel mass; review carefully against the "not a tank vehicle" requirement before promotion.
