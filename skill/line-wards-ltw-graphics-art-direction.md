---
name: line-wards-ltw-graphics-art-direction
description: Use for Line Wards graphics and art direction: LTW-inspired mobile board readability, long skinny north-south lane/grid visuals, tower and creep silhouette language, tech/upgraded-tower visual families, HUD lives/gold/income/timer presentation, combat/economy feedback, visual polish, and concept-art prompts. Scan-level rule: preserve Line Tower Wars pressure readability, portrait-first long-lane maze flow, and send-for-income fantasy; use original ward-tech visuals; support readable boss/fast/swarm/flying/invisible/attacker-style pressure; and never copy Warcraft III names, assets, UI chrome, factions, silhouettes, icons, sounds, or screenshots.
---

# Line Wards LTW Graphics Art Direction

## Purpose

This skill guides visual design, art direction, and graphics implementation for **Line Wards**, a mobile-first competitive tower-wars game inspired by the Warcraft III custom-map tradition of Line Tower Wars.

Use this skill before creating or changing:

- Board, lane, grid, and camera presentation.
- Towers, creeps, projectiles, effects, and feedback.
- HUD, send dock, tech panels, status readouts, and match results.
- Brand, store art, screenshots, icons, trailers, and marketing visuals.

The goal is not to recreate Warcraft III. The goal is to preserve the readable competitive fantasy of classic Line Tower Wars while building an original, touch-first visual language for Line Wards.

## Research Basis

Research sources reviewed for this direction include:

- Line Tower Wars: Reforged on Hive Workshop: https://www.hiveworkshop.com/threads/line-tower-wars-reforged.354130/
- Line Tower Wars: Reforged on the Warcraft 3: Reforged map database: https://maps.w3reforged.com/featured-maps/line-tower-wars-reforged
- Historical Line Tower Wars map listings and version notes: https://maps.w3reforged.com/maps/categories/tower-wars/line-tower-wars
- Line Tower Wars v18.5.01 on Hive Workshop: https://www.hiveworkshop.com/threads/line-tower-wars-v18-5-01.250346/
- Line Tower Wars AI overview and screenshots: https://gaming-tools.com/warcraft-3/line-tower-wars-ai/
- Classic Line Tower Wars design notes on The Helper: https://www.thehelper.net/threads/line-tower-wars.51602/
- Line Tower Wars 45 map/version notes: https://maps.w3reforged.com/maps/categories/tower-wars/line-tower-wars
- Existing project docs: `PROJECT_GUIDE.md`, `ARCHITECTURE.md`, `BRANDING_GUIDE.md`, `MVP_STATUS.md`, and `GAMEPLAY_DEVELOPMENT_CHECKLIST.md`.

Important observed traits from the source material:

- LTW is a player-versus-player tower defense format where players defend their own lanes while sending creeps to steal lives from others.
- Classic LTW variants are built around long, skinny defensive lanes, open-lane mazing, visible towers, hordes of small creeps, income pressure, lives, and last-player-alive victory.
- The Warcraft III versions use dense RTS HUDs, command panels, scoreboards, minimaps, shrines, builder units, tech/research menus, and strong player-color coding.
- Many variants use grass, stone, cityscape, cliffs, lanes, flags, shrines, and elemental/medieval fantasy motifs.
- Reforged-era variants add more persistent competitive UI, leaderboards, seasons, player cosmetics, bots, technologies, and extensive customization.
- Mature LTW variants support many tower upgrades, advanced or combined technologies, boss units, high-health monsters, and specialized creep pressures such as fast, flying, invisible, attacking, aura/spell, and anti-maze units.
- The income timer is not decorative. It is a pressure clock that shapes send timing, greed, panic building, and the player's sense of tempo.

## Non-Negotiable Legal And Brand Boundary

Line Wards may be an homage to the LTW custom-game tradition, but it must be an original game.

Do not use or imitate:

- Warcraft, Warcraft III, Blizzard, Horde, Alliance, Night Elf, Undead, Orc, Human, or other protected names as game-facing content.
- Warcraft III models, textures, icons, sounds, UI frames, spell names, faction symbols, unit silhouettes, buildings, heroes, or voice lines.
- Warcraft-style command-card chrome, exact icon layouts, minimap styling, or faction-color ornamental frames.
- Screenshots or extracted assets from Warcraft III as production art.

Allowed homage:

- Competitive tower-wars structure.
- Open-lane mazing with towers as blockers.
- Income gained by offensive sends.
- Clear player colors, visible lives, visible income, and pressure readability.
- Fantasy-adjacent towers, creeps, spells, elements, and tech, provided they are original in naming and silhouette.

## Visual North Star

Line Wards should feel like a tactical board under pressure: clean enough for mobile, energetic enough for competitive play, and readable at a glance.

The art direction is:

- **Readable fantasy strategy**, not realistic battle simulation.
- **Original ward-tech fantasy**, not Warcraft medieval imitation.
- **Low-fi but intentional**, with strong silhouettes and restrained effects.
- **Board-first**, where the long north-south lane shape, path state, creep flow, and tower intent remain the dominant visual information.
- **Competitive and clever**, matching the brand voice in `BRANDING_GUIDE.md`.

The player should be able to glance at the screen and answer:

1. Where do creeps enter and exit?
2. Which lane am I looking at?
3. Which towers are mine and what are they doing?
4. What pressure is incoming?
5. Is the next income tick close enough to change my decision?
6. Can I afford a defensive answer or an offensive send?
7. Am I winning the economy race or falling behind?

## Core Visual Pillars

### 1. Readability Before Detail

Classic LTW works because the battlefield is understandable even when many creeps are moving. Preserve that clarity.

- Keep tower footprints obvious on the grid.
- Keep creeps smaller than towers and visually grouped by role.
- Keep attack ranges, targeting, and projectile feedback simple.
- Prefer fewer, clearer effects over layered particles.
- Use animation timing and color cues to communicate threat before adding decorative detail.

### 2. The Lane Is The Hero

The active lane is the main screen. It should never feel like a background behind UI.

- Frame the lane as a long, skinny north-south defensive line, not a squat arena.
- In portrait mobile play, creeps should visually travel along the phone's vertical axis.
- Show spawn and exit as unmistakable landmarks at opposite vertical ends of the lane.
- Make open path, blocked cells, tower cells, and invalid placement states legible.
- Do not let menus cover the current placement area during normal play.
- Avoid decorative terrain clutter inside the playable grid.

### 3. Pressure Must Be Visible

LTW tension comes from the dual economy: defending while deciding whether to send.

- Incoming creep count, type, and danger should be visible before leaks happen.
- Send actions should create a visible outgoing or target-lane feedback moment.
- Income gains should feel rewarding but not obscure combat.
- The income timer should be persistent, glanceable, and more prominent near zero.
- Leaks should be unmistakable: direction, life loss, sender reward, and emotional beat.

### 4. Original Ward-Tech Fantasy

Use the brand palette and idea of wards, lanes, signals, and arcane circuitry to separate Line Wards from Warcraft.

Good motifs:

- Ward pylons, signal towers, rune plates, glass cores, beacon spires, prism emitters.
- Lanes as arena boards with subtle circuit/rune routing.
- Creeps as summoned pressure constructs, critters, brutes, swarms, and bosses with original silhouettes.
- Tech as elemental signal schools, not copied faction trees.

Avoid motifs that look like direct Warcraft race/faction units or buildings.

### 5. Mobile First, Not RTS Nostalgia First

Warcraft III LTW had a dense command UI because it lived inside an RTS shell. Line Wards should preserve the information priority, not the shell.

- Use larger touch targets and fewer simultaneous panels.
- Replace command-card density with a send dock, compact tower palette, contextual placement controls, and glanceable status strips.
- Support fast decisions under pressure without requiring tiny icon hunting.
- Make status information persistent enough for competitive play but compact enough for phone screens.

## Board And Environment Direction

### Board Shape

The MVP should move toward three long, skinny north-south lanes. Treat each lane as a mobile portrait defensive line, with a working vertical-slice target around 7x18 or 8x20 cells rather than a squat 12x9 arena.

Recommended board language:

- Slightly raised grid tiles on a dark field.
- Subtle lane border with player-color accent.
- Spawn and exit landmarks at the south and north ends of the lane.
- Creep flow cues that read vertically at a glance.
- Optional path preview line when placement mode is active.
- Soft under-tile glow for valid placement and sharper warning treatment for invalid placement.

### Terrain

Classic LTW often uses grass, stone, cityscape, and cliff-like boundaries. Line Wards should reinterpret these as clean mobile board materials.

Preferred materials:

- Dark stone or polished slate base.
- Arcane blue/violet route channels.
- Gold economy highlights.
- Mint valid-placement glow.
- Player-color trims, banners, or corner markers.

Avoid:

- Busy grass blades or high-frequency terrain noise inside the grid.
- Warcraft-like cliffs, doodads, crates, torches, banners, and race architecture.
- Brown/orange medieval mud-and-stone dominance.

### Camera

The camera should favor board comprehension.

- Use a fixed or lightly eased top-down/isometric view.
- Favor portrait framing where the player's lane runs bottom-to-top.
- Keep cell shapes consistent enough for accurate placement.
- Avoid dramatic perspective that makes grid selection ambiguous.
- During view swap, communicate whose lane is shown with a clear label and accent color.

## Tower Direction

Towers need strong silhouettes and clear roles. LTW history rewards tower teching and upgrade recognition, so upgraded towers should read as stronger descendants of their base role rather than unrelated objects.

Initial role language:

| Role | Visual Shape | Motion/Effect | Readability Goal |
| --- | --- | --- | --- |
| Reliable single-target | Tall pylon, narrow emitter, focused lens | Thin beam, bolt, or clean shot | Player sees it deleting priority targets. |
| Area/control | Wider base, ring emitter, rotating dish | Pulse ring, splash burst, slow field | Player sees where crowd control happens. |
| Economy/utility | Small relay, banner, signal mast, capacitor | Gold tick, charge loop, buff line | Player sees this is strategic support, not raw damage. |

Tower rules:

- Footprint must match occupied grid cells exactly.
- Base shape should distinguish tower ownership and placement state.
- Upgrade state can add height, glow, rotating elements, stronger cores, ring count, or small attachments.
- Tech family should be readable through secondary shape language: flame/plasma bloom, frost/crystal facets, lightning prongs, holy/solar halos, void/negative-space cores, water/prism flow, earth/plate mass, or other original non-Warcraft motifs.
- Do not rely only on color to distinguish role.
- Do not over-animate idle towers; moving creeps and active attacks need visual priority.

## Creep Direction

Creeps are the pressure language of the game. They need to be readable in groups.

Initial role language:

| Role | Shape | Movement | Threat Read |
| --- | --- | --- | --- |
| Runner/Fast | Small, sharp, low profile | Fast, darting | Speed pressure. |
| Brute/High HP | Large, rounded or armored | Slow, heavy | Health pressure. |
| Swarm | Tiny repeated units | Clustered flow | Volume pressure. |
| Boss | Oversized, distinct core or crest | Slow, ceremonial | Event pressure. |
| Flying/Air | Elevated shadow, hover bob, wing/float cue | Ignores maze path visually | Anti-ground-defense pressure. |
| Invisible/Stealth | Shimmer outline, intermittent reveal | Subtle, readable when detected | Detection pressure. |
| Attacker/Siege | Weaponized silhouette, impact windup | Stops or pulses near towers | Tower-damage pressure. |
| Aura/support | Clear ring, banner, or trailing field | Mid-speed | Modifier pressure. |

Creep rules:

- Creep silhouettes must remain visible against the board and tower bases.
- Group identity should be clear from size and motion before reading labels.
- Bosses can be dramatic but should not hide the path, exit, or tower targets.
- Flying or maze-ignoring units must be visually distinct from path-following creeps before they leak.
- Invisible/stealth units must never become literally unreadable; use shimmer, reveal pings, or detector-linked outlines.
- Attacker/siege units need a clear warning before damaging or disabling towers.
- Damage states can use flash, cracks, reduced glow, or small hit reactions; avoid gore.

## Effects And Feedback

Effects should clarify simulation events.

Priority events:

- Tower built.
- Tower sold.
- Tower shot.
- Creep hit.
- Creep killed.
- Creep leaked.
- Send queued.
- Income tick.
- Player eliminated.
- Match won/lost.

Effect guidance:

- Use short, clean bursts with fast decay.
- Keep combat effects low enough that pathing and placement remain readable.
- Use gold for economy, mint for valid/positive placement, blue/violet for ward energy, and red/orange only as one part of danger communication.
- Provide reduced-effects mode that preserves all functional cues.

## HUD And UI Direction

Classic LTW keeps lives, income, gold, and player status visible. Preserve this priority.

Persistent information:

- Player lives.
- Gold.
- Income.
- Income tick timer, with a clear near-tick pulse.
- Current lane owner / viewed lane.
- Incoming pressure indicator.
- Send/cooldown state.
- Current tech family or upgrade tier when it affects available choices.

Mobile UI principles:

- Controls must respect thumb zones.
- Use compact status strips rather than heavy RTS panels.
- Send dock should show category, cost, income gain, cooldown, and role.
- Tower palette should show role and affordability immediately.
- Placement controls should appear near the ghost tower but avoid blocking the grid cell under consideration.

Do not copy Warcraft III's bottom command card, portrait panel, resource bar layout, minimap frame, or iconography.

## Color And Lighting

Start from `BRANDING_GUIDE.md`:

| Role | Hex | Usage |
| --- | --- | --- |
| Night ink | `#10182F` | Main field, UI background, deep contrast. |
| Arcane blue | `#4DA3FF` | Primary ward energy, route accents, player-safe information. |
| Ward violet | `#9B6CFF` | Secondary energy, tech, magical pressure. |
| Signal gold | `#FFC84A` | Economy, income ticks, reward moments. |
| Mint signal | `#59E1B6` | Valid placement, success, ready state. |
| Cloud | `#F4F7FF` | High-contrast text and icon foregrounds. |

Functional color rules:

- Do not use red/green as the only state distinction.
- Pair color with shape, icon, motion, or label.
- Player colors should be accents, not full-screen tints.
- Enemy pressure should be readable in colorblind-safe ways.

## Asset Production Rules

For production assets:

- Prefer original sprites, low-poly models, or simple mesh/VFX assets made for Line Wards.
- Keep files source-controlled and named by gameplay role, not temporary concept names.
- Build small vertical-slice sets before making a full catalogue.
- Test assets in-game under real board density before polishing them.
- Every tower and creep concept should include: role, silhouette, footprint/size, animation need, color accent, and readability risk.

For generated concept art:

- Use it only as inspiration or temporary concept material unless licensing and source tracking are clear.
- Prompts must avoid asking for Warcraft, Blizzard, Warcraft III, Horde, Alliance, Night Elf, Undead, Orc, or Human style.
- Preferred prompt language: "original mobile tower-wars game", "ward-tech fantasy", "clean readable board-game strategy", "arcane signal pylons", "competitive tower defense".

## Visual Acceptance Checks

A graphics change is not done until it passes these checks:

- At normal phone size, the active long north-south lane, spawn, exit, towers, creeps, and leaks are identifiable without zooming.
- A first-time tester can tell which tower role is selected from shape and UI label.
- A first-time tester can tell which creep type is incoming from size, motion, and silhouette.
- Combat effects do not hide placement cells or leak events.
- Reduced-effects mode still communicates all gameplay-critical events.
- The board remains readable with heavy-send pressure.
- No asset appears to be copied from Warcraft III or any other protected game.

## Near-Term Graphics Work Queue

Use this order while the project is in GD-00 through GD-08:

1. Establish a clean long-lane board skin, targeting a skinny north-south 7x18 or 8x20 lane with spawn, exit, player accent, and placement states.
2. Create placeholder-but-distinct silhouettes for three tower roles.
3. Create placeholder-but-distinct silhouettes for runner, brute, and swarm creeps.
4. Improve projectile, hit, kill, leak, send, and income feedback with reduced-effects support.
5. Create a compact mobile HUD treatment for lives, gold, income, tick timer, and lane identity.
6. Add visual placeholders for tech family or upgrade tier when those systems become visible.
7. Verify readability in Play Mode before making polished assets.
8. Run at least three local playtests and capture screenshots or short clips for design review.

## Do And Do Not Summary

Do:

- Preserve LTW's pressure, lane, income, and survival readability.
- Design from the active long north-south lane outward.
- Make towers and creeps readable by silhouette and motion.
- Keep UI fast, compact, and mobile-native.
- Use the Line Wards palette and ward-tech fantasy language.

Do not:

- Copy Warcraft assets, UI, names, factions, silhouettes, or sounds.
- Make effects more important than the board state.
- Hide the lane under menus.
- Rely on tiny icons or PC RTS command-panel assumptions.
- Add visual polish before testing readability under heavy creep pressure.
