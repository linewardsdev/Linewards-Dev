# UI And Board Contact Sheet Review V01

Date: 2026-07-16

Purpose: record the first V0.2 contact-sheet batch for the UI/game-board art pipeline.

## Generated Sheets

| Sheet | Path | Status |
| --- | --- | --- |
| HUD chrome/stat drawer | `docs/art-pipeline/ui-board/contact-sheets/ui_board_hud_chrome_contact_sheet_v01.png` | Generated, awaiting selection |
| Command card frames | `docs/art-pipeline/ui-board/contact-sheets/ui_board_command_cards_contact_sheet_v01.png` | Generated, awaiting selection |
| Icon simplification | `docs/art-pipeline/ui-board/contact-sheets/ui_board_icon_simplification_contact_sheet_v01.png` | Generated, awaiting selection |
| Board material kit | `docs/art-pipeline/ui-board/contact-sheets/ui_board_material_kit_contact_sheet_v01.png` | Generated, awaiting selection |
| Spawn/leak gates | `docs/art-pipeline/ui-board/contact-sheets/ui_board_spawn_leak_gates_contact_sheet_v01.png` | Generated, awaiting selection |
| Map/lane/status controls | `docs/art-pipeline/ui-board/contact-sheets/ui_board_controls_contact_sheet_v01.png` | Generated, awaiting selection |

## Preliminary Read

These are not final selections. They are first-pass observations to make the next review faster.

### HUD Chrome

Strong candidates:

- 2: broad stat cells with readable interior space and restrained side accents.
- 3: clean rectangular top mass and strong left/right resource posts.
- 8: flatter, less ornate, likely easier to adapt to the current HUD.
- 11: compact and readable with clear drawer-like center.

Risks:

- 4, 6, and 10 have large circular centers that may compete with lane content.
- 7 is flavorful but may read too damaged/noisy at phone scale.

### Command Cards

Strong candidates:

- 2: clean state progression and readable selected/disabled/error variants.
- 4: simple frame with low ornamental load.
- 9: strong top badge area for selected or role accent.
- 12: rounded but compact, good for touch targets.

Risks:

- 8 is probably too ornamental for repeated cards.
- 10 and 11 may be too soft/rounded compared with the current ward-tech tower art.

### Icon Simplification

Strong candidates:

- 3: Arrow and Siege silhouettes are especially clear.
- 5: compact tower family with readable creep row.
- 9: strong tower row, readable Brute and Siege.
- 12: cohesive style with good command-card scale potential.

Risks:

- Runner and Swarm remain close in some options if reduced too far.
- Shade often reads as a ghost shape; final art should keep the shard/echo identity.

### Board Material Kit

Strong candidates:

- 2: clear route arrows and disciplined side/build material.
- 3: strong center route language without excessive glow.
- 9: restrained and grid-friendly.
- 12: good route/rail balance with low material noise.

Risks:

- 7 and 10 have warmer/gold accents that could compete with economy/UI color.
- Some kits include round route nodes that may suggest interactable objects if overused.

### Spawn/Leak Gates

Strong candidates:

- 2: clean top-to-bottom flow and clear leak grate.
- 4: strong portal/drain contrast, but may need scale restraint.
- 8: compact rectangular gate pair that could fit the current lane footprint.
- 11: simple, readable, and likely easiest to implement.

Risks:

- 1, 3, 7, 9, and 12 are more architectural and may hide creeps near endpoints.
- Red leak glow must be toned down during integration.

### Map/Lane/Status Controls

Strong candidates:

- 2: compact octagonal buttons with clear icon language.
- 3: blue inactive set reads well and stays restrained.
- 5: strong active-state set for match controls.
- 9: square-ish buttons may integrate best with existing UI layout.

Risks:

- 7 diamond buttons are stylish but may waste horizontal space.
- 10 and 11 are too color-specific for a full neutral control set.

## Selection Needed

Next review should pick one option number for each sheet:

- HUD chrome/stat drawer:
- Command card frames:
- Icon simplification:
- Board material kit:
- Spawn/leak gates:
- Map/lane/status controls:

Once selected, V0.2 production should crop/normalize those options and implement the first runtime slice in this order:

1. Command card frame treatment.
2. Persistent map/lane/status controls.
3. HUD/stat drawer treatment.
4. Board route/build-band material treatment.
5. Spawn/leak gate treatment.
6. Final simplified icons if V01 derived icons still feel too noisy.

