# UI And Board Selected Candidates V02

Date: 2026-07-16

These are the user-selected UI/game-board art directions for the V0.2 production pass.

Overview image: `docs/art-pipeline/ui-board/selected-candidates-v02-overview.png`

## Selected Options

| Track | Selected option | Source sheet | Cropped preview |
| --- | --- | --- | --- |
| Command card frames | 4 | `docs/art-pipeline/ui-board/contact-sheets/ui_board_command_cards_contact_sheet_v01.png` | `docs/art-pipeline/ui-board/selected-candidates/command_cards_option_04.png` |
| Map/lane/status controls | 1 | `docs/art-pipeline/ui-board/contact-sheets/ui_board_controls_contact_sheet_v01.png` | `docs/art-pipeline/ui-board/selected-candidates/controls_option_01.png` |
| HUD chrome/stat drawer | 6 | `docs/art-pipeline/ui-board/contact-sheets/ui_board_hud_chrome_contact_sheet_v01.png` | `docs/art-pipeline/ui-board/selected-candidates/hud_chrome_option_06.png` |
| Icon simplification family | 6 | `docs/art-pipeline/ui-board/contact-sheets/ui_board_icon_simplification_contact_sheet_v01.png` | `docs/art-pipeline/ui-board/selected-candidates/icon_family_option_06.png` |
| Board material kit | 11 | `docs/art-pipeline/ui-board/contact-sheets/ui_board_material_kit_contact_sheet_v01.png` | `docs/art-pipeline/ui-board/selected-candidates/board_material_option_11.png` |
| Spawn/leak gates | 11 | `docs/art-pipeline/ui-board/contact-sheets/ui_board_spawn_leak_gates_contact_sheet_v01.png` | `docs/art-pipeline/ui-board/selected-candidates/spawn_leak_gates_option_11.png` |

## Production Interpretation

### Command Cards: Option 4

Use the simple rounded frame language, with restrained corner hardware and readable state variants. This should be the first runtime target because build/send cards are currently the most visually exposed UI surface.

Implementation notes:

- Preserve normal, selected, disabled, and error/too-expensive states.
- Keep the icon window dark and quiet so simplified role icons stay readable.
- Use mint for build/selected confirmation and red only for unavailable/error.

### Map/Lane/Status Controls: Option 1

Use the circular icon-first button language for persistent controls. This should move map/lane state away from temporary-panel styling and into a consistent onscreen control treatment.

Implementation notes:

- Map and lane should be paired controls or a single two-state toggle with this visual language.
- Pause/restart/status controls can borrow the same ring/chrome but should stay secondary to build/send decisions.

### HUD Chrome: Option 6

Use the rounded center module and side accent capsules as the stat drawer direction. Option 6 is more dimensional than the flattest options, so scale and opacity need care in portrait view.

Implementation notes:

- Convert the large circular center into a compact drawer cap or status node.
- Keep stat cells rectangular and high-contrast.
- Do not let the top chrome overlap the active lane at mobile aspect.

### Icon Family: Option 6

Use this as the simplified command-icon benchmark. It keeps the tower row cohesive and gives the creep row clear role reads without relying only on full production sprites.

Implementation notes:

- Preserve Arrow crossbow, Control containment tower, Relay beacon, Pulse core, Prism spire.
- Preserve Runner as a dart, Brute as a shield body, Swarm as clustered shards, Shade as a ghost/shard echo, Siege as a ram/drill.
- Test in grayscale before replacing V01 derived runtime icons.

### Board Material: Option 11

Use the restrained slate material and triangle route cues as the board material direction. It is quieter than many other options and should support the current lane readability.

Implementation notes:

- Route arrows should be visible but never brighter than creeps, shots, health bars, or HUD critical values.
- Buildable side bands should use material seams and value, not bright labels.
- Avoid overusing warm/gold accents inside the board because gold already means economy.

### Spawn/Leak Gates: Option 11

Use the circular platform spawn and drain-like leak gate as the endpoint direction. This option is compact and easier to fit into the current lane footprint than the heavier architectural gates.

Implementation notes:

- Spawn should lean mint/blue and feel like entry pressure.
- Leak should lean red/dark and feel like drain/life loss.
- Crop and scale so creeps remain visible at the first and last route cells.

## Next Implementation Order

1. Crop and normalize command-card frame states from option 4.
2. Build a runtime command-card frame proof using the current build/send docks.
3. Crop and normalize controls option 1 for map/lane/status buttons.
4. Prototype the HUD drawer with option 6 as the visual target.
5. Prototype board material option 11 as either procedural sampled colors or authored slices.
6. Prototype spawn/leak option 11 endpoint plates.
7. Only replace V01 derived icons with option 6 simplified icons after card/chrome sizing is stable.

## QA Gate

The V0.2 implementation is not complete until it has:

- phone portrait default view;
- build menu open;
- send menu open;
- selected card;
- too-expensive card;
- map/lane toggle states;
- heavy pressure;
- leak/life-loss moment;
- grayscale captures.
