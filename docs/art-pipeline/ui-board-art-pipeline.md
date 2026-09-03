# UI And Game Board Art Pipeline

Date: 2026-07-16

## Purpose

This is the source-of-truth pipeline for moving the Line Wars UI and game board from prototype readability into a deliberate V1 art pass.

The current V0.1 pass proves that runtime icons and procedural board cues can be wired safely, but it is not the final visual direction. The next pass should not be "make the current buttons prettier." It should produce selected UI chrome, command cards, board material, route, spawn, leak, and lane-frame art that can be tested at phone scale.

## North Star

Line Wars should read like an original early-2000s strategy board game adapted for mobile:

- strong silhouettes before texture detail;
- readable gameplay states before decoration;
- compact UI that supports repeated play;
- board materials that explain where creeps enter, where they travel, where towers can be placed, and where leaks cost lives;
- no copied Warcraft, Blizzard, or other protected UI chrome, factions, icon compositions, screenshots, names, or silhouettes.

## Readability Hierarchy

Use this order when making tradeoffs:

1. Critical state: lives, gold, match state, pressure, ability to afford an action.
2. Active gameplay: creeps, towers, projectiles, builder, placement validity, health bars.
3. Command intent: build/send choices, selected cards, disabled cards, cooldowns.
4. Board explanation: route, build bands, spawn gate, leak gate, lane ownership.
5. Flavor detail: scratches, metal seams, glow, trims, material noise.

If a lower item competes with a higher item at phone scale, simplify or darken it.

## Asset Tracks

### HUD Chrome

Includes top stat drawer, status panel, pause/restart controls, map/lane toggle, and panel frames.

Targets:

- compact enough for portrait phone framing;
- strong text contrast;
- clear closed/open states for optional stat panels;
- no overlap with lane, build dock, or send dock.

### Command Cards

Includes build cards, send cards, selected state, disabled state, too-expensive state, and card hover/press feedback.

Targets:

- icon can identify the tower or creep without reading the text;
- price and income impact remain legible;
- selected card has a gameplay-readable state, not only a color tint;
- disabled cards are visibly unavailable but still recognizable.

### Board Material Kit

Includes deep field, buildable side bands, center route, route edge guides, lane rails, gutters, and background void.

Targets:

- route is visible without becoming brighter than creeps or shots;
- build zones read as deliberate placement bands;
- individual cells remain clear enough for tower placement;
- map view and single-lane view share the same material language.

### Endpoint Gates

Includes spawn gate, leak/life-loss gate, direction markings, endpoint plates, and warning states.

Targets:

- players can tell top is spawn and bottom is life loss;
- leak state is visually distinct from ordinary route material;
- endpoint art does not hide creeps at the top or bottom row.

### Feedback States

Includes placement valid/invalid, send confirmation, pressure increase, income tick, leak warning, reduced-effects mode, and grayscale readability.

Targets:

- feedback should reinforce the board and unit art;
- reduced-effects mode still communicates critical events;
- grayscale captures keep all roles and states separated.

## Folder And Naming Rules

Design docs and review artifacts:

- `docs/art-pipeline/ui-board-art-pipeline.md`
- `docs/art-pipeline/ui-board-contact-sheet-brief.md`
- `docs/art-pipeline/ui-board-pipeline-checklist.md`
- `docs/art-pipeline/ui-board/`
- `docs/screenshot-reviews/ui-board-art-pass-vXX/`

Unity source art:

- `unity/LTW.UnityClient/Assets/Art/UI/`
- `unity/LTW.UnityClient/Assets/Art/UI/Icons/`
- `unity/LTW.UnityClient/Assets/Art/Board/`

Unity runtime art:

- `unity/LTW.UnityClient/Assets/Resources/Art/UI/Icons/`
- future board runtime textures should live under `Assets/Resources/Art/Board/` only if runtime loading is needed.

Naming:

- UI icons: `ui_icon_tower_arrow_v02.png`, `ui_icon_send_runner_v02.png`
- UI chrome: `ui_chrome_command_card_v01.png`, `ui_chrome_stat_panel_v01.png`
- Board textures: `board_route_core_v01.png`, `board_build_band_v01.png`, `board_spawn_gate_v01.png`
- Review folders: `ui-board-art-pass-v02`, `ui-board-art-pass-v03`

Every promoted generated image needs a matching source note that records source prompt, date, approval reason, edits, and runtime use.

## Production Stages

### Stage 0: Runtime Audit

Capture the current game before generating replacement art.

Required evidence:

- single-lane default HUD;
- map view;
- build menu open;
- send menu open;
- selected/disabled cards;
- heavy creep pressure;
- grayscale versions of the same states.

Output:

- `docs/screenshot-reviews/ui-board-art-pass-vXX/review.md`

### Stage 1: Component Inventory

List every UI and board part that needs art, including state variants.

Output:

- card list;
- panel list;
- board material list;
- endpoint/gate list;
- feedback state list.

Current inventory:

- `docs/art-pipeline/ui-board/component-inventory-v01.md`

### Stage 2: Contact Sheets

Generate small sets of options before integrating anything.

Required sheets:

- HUD panel and stat drawer chrome;
- command card frames and selected/disabled states;
- tower/send icon simplification sheet;
- board route/build-band material sheet;
- spawn and leak gate sheet;
- map/lane toggle and compact status control sheet.

Use original descriptive prompts. Do not ask for protected game styles, copied RTS UI, faction marks, or screenshot-like compositions.

### Stage 3: Selection Review

Pick candidates based on tiny-scale readability, not full-size prettiness.

Selection questions:

- Can the asset be identified at phone size?
- Does it survive grayscale?
- Does it support the current lane and HUD layout?
- Does it look original enough to ship?
- Can it be implemented without rewriting gameplay UI?

Output:

- selected candidate table;
- rejection notes for options that are too noisy, too generic, too close to protected references, or too detailed.

### Stage 4: Asset Normalization

Prepare the selected assets for Unity.

Rules:

- crop transparent padding;
- normalize scale across the same asset type;
- remove baked labels, text, watermarks, UI frames, and background;
- create grayscale review copies;
- keep source images separate from production sprites;
- use Unity import settings that preserve crisp phone readability.

### Stage 5: Runtime Integration

Integrate in the smallest safe slice.

Order:

1. Replace command card icon sprites.
2. Replace or skin command card frames.
3. Skin HUD panels and toggles.
4. Replace procedural board cues with authored board material pieces.
5. Add endpoint gate art.
6. Add feedback state art.

Runtime fallback is allowed for early slices, but final V1 should not depend on placeholder procedural glyphs for primary recognition.

### Stage 6: Screenshot QA Gate

No UI/board art pass is done until it has evidence.

Required captures:

- phone portrait single-lane view;
- map view;
- build menu;
- send menu;
- selected card;
- disabled/too-expensive card;
- heavy Runner pressure;
- heavy Swarm pressure;
- leak/life-loss moment;
- grayscale versions.

Pass criteria:

- no HUD overlap at mobile aspect;
- buttons close/open predictably;
- map/lane toggle remains reachable onscreen;
- board remains quieter than units and critical HUD;
- spawn and leak endpoints are understandable;
- command cards can be recognized by icon and state;
- screenshots are linked from the checklist.

## Rejection Criteria

Reject or rework assets that:

- look like generic black sci-fi boxes;
- rely on tiny texture detail that disappears at phone scale;
- overpower creeps, towers, placement state, or projectiles;
- use copied or too-close protected UI chrome;
- make build/send choices require reading every label;
- hide grid cells or route direction;
- work in a static sheet but fail in the Unity screenshot gate.

## Current Baseline

V0.1 is implemented and documented in `docs/art-pipeline/ui-board-art-pass-v01.md`.

What V0.1 proves:

- runtime build/send icon loading works through `RuntimeUiIconLibrary`;
- procedural fallback glyphs remain in place;
- board cues can be enhanced in `UnityVerticalSliceRenderer`;
- screenshot capture can review UI icons, board states, and grayscale.

What V0.1 does not prove:

- final HUD chrome direction;
- final command card style;
- authored board material direction;
- endpoint gate art direction;
- final phone-scale layout polish.

## Immediate Next Pass

The next implementation pass should be `ui-board-art-pass-v02`.

Deliverables:

- contact sheets for HUD chrome, command cards, board material, and endpoint gates;
- selected candidate notes;
- first authored command-card frame and HUD stat drawer treatment;
- first authored route/build-band/gate material treatment;
- Unity screenshot QA in normal and grayscale.
