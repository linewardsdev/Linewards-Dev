# UI And Board Contact Sheet Brief

Date: 2026-07-16

Use this brief to generate options for `ui-board-art-pass-v02`. The goal is not to copy a classic RTS interface. The goal is to build an original Line Wars visual language with the same level of role clarity, material confidence, and readable command structure.

## Shared Style Block

Original mobile lane-defense strategy game, ward-tech fantasy board, top-down three-quarter readable UI art, early-2000s strategy polish, dark slate metal, worn stone and glass, mint energy accents, blue route glow, gold economy accents, red leak danger, compact mobile readability, strong silhouette, clean value grouping, no text, no logos, no faction marks, no copyrighted game UI, no Warcraft, no Blizzard, no screenshot copy.

## Shared Avoidance Block

Avoid copied RTS command bars, ornate Warcraft-style stone frames, faction crests, skull UI frames, photorealistic chrome, unreadable micro-detail, neon overload, soft blurry gradients, generic sci-fi rectangles, text baked into art, watermarks, screenshots, inventory icons from existing games, and compositions that depend on labels.

## Sheet A: HUD Chrome And Stat Drawer

Purpose:

Find a compact visual treatment for the top stats and optional "Your Line" drawer.

Prompt:

Create a 12 option contact sheet of original compact mobile strategy HUD panel chrome. Each option should show a small stat drawer frame, two narrow stat cells, and a closed tab state. Ward-tech fantasy board style, dark slate material, mint and gold accent rules, high contrast number area, readable on phone, minimal ornament, no text, no logos. Use the shared style block and avoidance block.

Selection criteria:

- stat values would be readable over it;
- closed and open states are visually obvious;
- it does not cover too much lane;
- it avoids heavy decorative borders.

## Sheet B: Command Card Frames

Purpose:

Find build/send card frames that can support tower and creep icons, cost, income impact, selected state, and disabled state.

Prompt:

Create a 12 option contact sheet of original mobile tower-defense command card frames. Each option includes a normal card, selected card, disabled card, and too-expensive card state. Cards are compact, dark slate and glass, one icon window, one cost strip, one small accent strip, mint for build, gold for send, red for unavailable, clean mobile readability, no text, no logos. Use the shared style block and avoidance block.

Selection criteria:

- selected state is readable without relying only on hue;
- disabled state still leaves icon recognizable;
- card frame is not busier than the icon;
- frame can fit current IMGUI card size or a near-term replacement prefab.

## Sheet C: Icon Simplification

Purpose:

Convert V1 tower and creep sprites into simpler command icons where needed.

Prompt:

Create a 12 option contact sheet of simplified readable command icons for a ward-tech lane defense game. Include options for bow/crossbow arrow tower, containment control tower, relay beacon tower, pulse core tower, prism spire tower, runner dart creep, brute shield creep, swarm cluster creep, shade ghost shard creep, and siege ram creep. Transparent background, strong silhouette, one accent color each, no frame, no text, no copied game icon style.

Selection criteria:

- icon reads in grayscale;
- icon connects to in-game sprite;
- detail stays inside a compact square;
- no option looks like another role.

## Sheet D: Board Material Kit

Purpose:

Find authored materials for the lane board while preserving grid and route clarity.

Prompt:

Create a 12 option contact sheet of modular top-down board material tiles for an original ward-tech lane defense game. Each option shows deep field tile, buildable side band tile, center route core tile, route edge guide, lane rail, and subtle gutter. Dark slate, worn stone, metal inlays, blue route energy, muted values, grid-friendly, phone readable, no text, no units, no UI chrome. Use the shared style block and avoidance block.

Selection criteria:

- grid cells remain clear;
- route is obvious but not the brightest object;
- build bands read as placeable zones;
- material repeats without obvious seams.

## Sheet E: Spawn And Leak Gates

Purpose:

Make the top and bottom of a lane understandable before labels.

Prompt:

Create a 12 option contact sheet of top-down lane endpoint gates for an original mobile lane-defense board game. Each option includes a spawn gate at the top and a leak/life-loss gate at the bottom. Spawn gate should suggest incoming energy or portal pressure; leak gate should suggest danger, drain, or life loss. Use dark slate, mint spawn accent, red leak accent, arrow/direction cues, compact board footprint, no text, no logos, no copied game architecture.

Selection criteria:

- top and bottom have different jobs;
- creeps remain visible over the endpoint art;
- direction can be understood without labels;
- leak state can support warning VFX later.

## Sheet F: Map/Lane Toggle And Status Controls

Purpose:

Give the persistent map/lane toggle, pause/restart, and small status controls a coherent style.

Prompt:

Create a 12 option contact sheet of compact mobile strategy control buttons for map toggle, lane toggle, pause, restart, and status state. Original ward-tech fantasy UI, small rectangular and icon-first controls, dark glass, thin metal trim, mint active state, blue inactive state, gold attention state, readable at phone scale, no text, no logos, no copied RTS UI.

Selection criteria:

- button state is clear in one glance;
- can sit onscreen without occupying the temporary start/stop panel;
- does not compete with command cards;
- supports icon-first controls later.

## Required Review Output

For every generated sheet, record:

- file path;
- prompt version;
- selected option numbers;
- rejected option numbers and reasons;
- whether it is safe for production use;
- next edit needed before Unity integration.

