# Game Menu And Runtime Flow

This document centralizes Line Wards menu behavior so the app shell, runtime HUD, build/send drawers, pause flow, and results flow do not drift across implementation notes.

The short version: the game should feel quiet and intentional before the player starts, fast during live play, and never hide the board behind oversized menus.

## Scope

This doc covers:

- Game shell menus such as title, start, settings, pause, and results.
- Runtime controls such as PLAY, RESET, lane selector, build drawer, send drawer, and close/cancel actions.
- Mobile-safe behavior around safe areas, thumb zones, and visual priority.

This doc does not cover:

- Tower/creep balance details.
- Final art replacement specs.
- Online matchmaking, accounts, monetization, cloud saves, or ranked play.

## Current Prototype Boundary

The current local Unity prototype still starts directly in `Assets/Scenes/LocalVerticalSlice.unity`. There is still no standalone title scene — the scene asset contains zero GameObjects, and every component including the menu shell is created at runtime by `Assets/Scripts/Simulation/LocalVerticalSliceLauncher.cs`.

Since 2026-08-03 the shell is built in **two** technologies, and which one a screen uses is a deliberate line rather than an accident of history:

**Full-screen shell screens — UI Toolkit (UXML + USS).** Title, Pause and Results are full-screen compositions in `Assets/Scripts/UI/ShellScreenView.cs`, rendered through a runtime `UIDocument` the launcher creates. They own the display: the title paints an opaque field so no board reads through it, and pause and results dim the board behind a translucent one. Each has a single dominant mark, one obviously primary action, and subordinate actions distinguished by size and frame as well as colour.

- Title: LINE WARDS wordmark, START GAME, HOW TO PLAY, SETTINGS, QUIT.
- Pause: PAUSED, live lives/gold/income, RESUME, SETTINGS, RESET MATCH (with its consequence spelled out), EXIT TO TITLE.
- Results: VICTORY or DEFEAT, the full eight-seat scoreboard, REMATCH, SETTINGS, EXIT TO TITLE.

**Everything else — still IMGUI**, in `LocalSessionFlowOverlay.OnGUI` and the HUD components beside it:

- READY / pre-match panel before simulation pressure begins.
- HOW TO PLAY panel.
- SETTINGS panel, reachable from all three shell screens.
- Opening BUILD countdown of at least 30 seconds that lets the player place towers before live pressure starts.
- PLAY / PAUSE / RESET controls and the live pause/reset rail.
- Lane selector.
- BUILD drawer.
- SEND drawer.
- Placement close/cancel behavior.
- Optional diagnostics overlay during development.

`LocalSessionFlowOverlay` remains the single owner of session state regardless of which technology draws a screen. It decides which shell screen is up, it publishes `RuntimeUiChrome.ModalScreenActive`, and it performs every action; `ShellScreenView` renders and reports taps back through `IShellScreenActions`. A UI Toolkit panel and the IMGUI HUD read input independently, so a full-screen runtime panel covers the HUD visually while doing nothing at all about the HUD's clicks — the state flag is still what makes a shell screen modal.

IMGUI draws **over** a runtime UI Toolkit panel, verified by capture. That is why SETTINGS can stay IMGUI and still open on top of the title or pause composition it was opened from, instead of dropping the player onto a bare board for its duration.

The READY state must be clean. No bot towers, creep sends, send beams, leak text, or combat FX should appear until the player starts the match.

## Design Principles

1. Keep gameplay visible *while it is playable*.

   Live match controls — build, send, lane select, placement, the opening countdown — should be drawers, compact panels, or overlays with deliberate safe margins, and must not block the active lane.

   Shell screens are the deliberate exception, and the rule was amended on 2026-08-03 to say so rather than be quietly broken. Title, pause and results are not moments of play; they are places. Each owns the whole display. A 348x284 card floating over a running board was the previous reading of this principle and it is what made the title screen look like a debug panel rather than a game. Pause and results still let the board show through their field, because the board is the thing being paused or scored; the title does not, because there is nothing behind it worth seeing yet.

2. Separate shell menus from live commands.

   Title, settings, pause, and results are app/menu states. Build, send, sell, lane select, and placement are live match controls.

3. Runtime menus should not mutate simulation state by opening.

   Opening BUILD, SEND, lane selector, or settings should never place towers, send creeps, start bots, advance ticks, or spend gold.

4. Match start is a real boundary.

   The local match may be constructed in READY state, but bots and live simulation pressure start only when PLAY starts the match.

5. Mobile decisions beat decorative chrome.

   Art should support fast reading: gold, income, lives, selected tower, selected creep, lane pressure, invalid placement, and match state must remain legible before decorative detail.

## Menu State Model

The app should eventually use these top-level states:

| State | Purpose | Gameplay ticking? | Expected UI |
| --- | --- | --- | --- |
| Title | First app entry | No | Game logo, Start Game, How To Play, Settings |
| Pre-Match | Local setup | No | Bot count/difficulty later, seed/replay later, Start |
| Ready | Match scene loaded but not started | No | Board, HUD, PLAY, no bot activity |
| Build Countdown | Opening build window | No | Board, minimum 30-second countdown, build drawer, SEND blocked |
| Live | Active match | Yes | HUD, BUILD, SEND, lane selector, pause/reset affordance |
| Paused | Temporary stop | No | Resume, Restart, Settings, Exit to Title |
| Results | Match complete | No | Winner, summary, replay/export later, Rematch, Exit |
| Settings | App/match options | No while modal | Audio, reduced effects, text scale, safe area/debug toggles |

For the MVP, Title and Pre-Match can remain overlay-driven inside `LocalVerticalSlice`. A separate scene can wait until the game has more durable mode selection, save/load, and campaign/tutorial needs.

## Runtime Flow

```text
Open LocalVerticalSlice scene
    |
    v
TITLE
    - Start Game
    - How To Play
    - Settings
    - Quit
    |
    | player taps Start Game
    v
BUILD COUNTDOWN
    - Minimum 30-second opening countdown
    - Player can place towers
    - SEND remains blocked
    - Bots do not build/send yet
    |
    | countdown expires or player taps START NOW
    v
LIVE
    - Simulation starts
    - Bot openers build/send
    - Tick loop runs
    - Build/send drawers available
    - Lane selector available
    |
    | pause or match ends
    v
PAUSED or RESULTS
```

READY remains available as a pre-match/inspection state after reset or from secondary menu paths:

```text
READY
    - Board visible
    - HUD visible
    - PLAY available
    - No bots build/send
    - No creep/combat effects
    |
    | player taps PLAY
    v
BUILD COUNTDOWN
    - Minimum 30-second opening countdown
    - Player can place towers
    - SEND remains blocked
    - Bots do not build/send yet
    |
    | countdown expires or player taps START NOW
    v
LIVE
    - Simulation starts
    - Bot openers build/send
    - Tick loop runs
    - Build/send drawers available
    - Lane selector available
```

## Runtime HUD

The runtime HUD should show:

- Current match state: READY, LIVE, PAUSED, or RESULTS.
- Opening build state: BUILD when the countdown is active.
- Player lives.
- Player gold.
- Player income.
- Send readiness or feedback.
- PLAY / PAUSE.
- RESET.

HUD constraints:

- Keep text readable on small portrait phones.
- Do not cover the active build grid.
- Use safe-area-aware positioning.
- Keep debug overlays optional and visually distinct from player-facing UI.

Current implementation references:

- `Assets/Scripts/UI/RuntimeMatchHud.cs`
- `Assets/Scripts/UI/HudView.cs`
- `Assets/Scripts/Simulation/LocalSessionFlowOverlay.cs`
- `Assets/Scripts/Simulation/UnitySimulationDriver.cs`

## Shell Screens (UI Toolkit)

The title, pause and results screens and everything they need:

- `Assets/Scripts/UI/ShellScreenView.cs` — the view, the `IShellScreenActions` contract, safe-area padding, and the two generated backdrop textures.
- `Assets/Resources/UI/ShellScreens.uxml` — all three screens in one document, switched by `display`.
- `Assets/Resources/UI/ShellScreens.uss` — the palette, the three action weights, and the enter transitions.
- `Assets/Resources/UI/LineWardsRuntimeTheme.tss` — imports Unity's default runtime theme.
- `Assets/Resources/UI/LineWardsShellPanelSettings.asset` — 1080x1920 reference surface, `ScaleWithScreenSize`, match width.
- `Assets/Editor/ShellPanelSettingsGenerator.cs` — authors that asset, so its settings live next to the reasoning for them.
- `Assets/Editor/ShellInputCheck.cs` — drives a pointer press on START GAME in Play Mode and asserts the build countdown began.

Two things about this surface are worth knowing before extending it:

- **No EventSystem is needed.** With none in the scene, UI Toolkit falls back to its own runtime event system, which reads legacy `Input` — which is what this project is set to (`activeInputHandler: 0`). `ShellInputCheck` confirms both halves: that the fallback is the live route, and that a press on a button reaches the simulation.
- **Alpha composites in linear.** The project renders in Linear colour space, so a translucent USS colour arrives roughly twice as strong as the sRGB numbers suggest. Opaque colours are exact. See the header comment in `ShellScreens.uss` for the measured figures.

## Build Drawer

BUILD opens a compact tower drawer. The drawer should support five tower choices without dense text.

Recommended tower button content:

| Tower | Compact label | Required info |
| --- | --- | --- |
| Arrow | ARROW | Cost, selected state |
| Control | CTRL | Cost, selected state |
| Relay | RELAY | Cost, selected state |
| Pulse | PULSE | Cost, selected state |
| Prism | PRISM | Cost, selected state |

Behavior:

- Tapping BUILD opens the drawer.
- Tapping a tower selects that tower and enters placement mode.
- Tower type selection should always be possible; affordability is enforced when the player taps BUILD.
- Placement mode shows the builder/ghost, selected cell, validity, direct tower-type switch buttons, and explicit ALL / BUILD / CANCEL controls.
- A close/cancel affordance must always be available while the drawer or placement mode is open.
- Closing the drawer must not place a tower or spend gold.
- Invalid placement should explain the reason without a blocking dialog.

Current implementation references:

- `Assets/Scripts/UI/TouchPlacementController.cs`
- `Assets/Scripts/UI/PlacementFeedbackView.cs`
- `Assets/Scripts/Simulation/UnityCommandAdapter.cs`

## Send Drawer

SEND opens a category picker first, then a compact creep drawer mirroring the build drawer — split into three categories of 5 creeps each (CORE / RAPID / ELITE), since 10 in one flat drawer was too crowded. Category names are no longer placeholders: **CORE** is the founding five, **RAPID** is the cooldown-exempt set (every one of its creeps sets `ignoresSendCooldown`, which is what the name refers to), and **ELITE** is the Meshy-rigged bipeds added 2026-07-28. All three carry real, sendable content — see `docs/TOWER_AND_CREEP_ROSTER.md`. The picker panel is taller than the creep grids because three full-width cards do not fit the grid height.

Recommended creep button content, Category 1 (original roster, unchanged):

| Creep | Compact label | Required info |
| --- | --- | --- |
| Runner | RUN | Cost, income gain |
| Brute | BRUTE | Cost, income gain |
| Swarm | SWARM | Cost, income gain |
| Shade | SHADE | Cost, income gain |
| Siege | SIEGE | Cost, income gain |

Category 2 (2026-07-28):

| Creep | Compact label | Required info |
| --- | --- | --- |
| Crystal Wisp | WISP | Cost, income gain |
| Ash Revenant | ASH | Cost, income gain |
| Obsidian Brute | OBRT | Cost, income gain |
| Serpent Coil | COIL | Cost, income gain |
| Spire Turret Walker | WALK | Cost, income gain |

Behavior:

- SEND is disabled or gives clear feedback before the match starts.
- Sending is allowed only in LIVE state.
- Opening the drawer (or the category picker within it) must not send anything.
- Tapping a category opens its 5-creep grid; tapping any creep in either category queues a send through the simulation command path.
- BACK returns from a category's creep grid to the category picker without closing the drawer; CLOSE collapses the whole drawer from either level.
- The UI should make it clear that sends attack the next active opponent lane, not the sender's own lane.

Current implementation references:

- `Assets/Scripts/UI/SendDockController.cs`
- `Assets/Scripts/Simulation/UnityCommandAdapter.cs`
- `LTW.Simulation.Bridge.LocalVerticalSlice.QueueSend`
- `LTW.Simulation.Bridge.LocalMatchTopology`

## Lane Selector

The lane selector lets the player inspect lanes without replacing the match state.

Behavior:

- The active lane camera defaults to lane 1.
- Selector supports lanes 1 through 8.
- Selecting a lane changes only the camera/presentation view.
- It must not start the match, reset the match, or mutate simulation state.
- Labels should remain compact: `L1`, `L2`, ... `L8`.

Current implementation references:

- `Assets/Scripts/UI/LaneViewToggleController.cs`
- `Assets/Scripts/Simulation/UnityVerticalSliceRenderer.cs`

## Pause Menu

The pause menu is the first true in-match menu beyond compact runtime drawers.

Shipped options, in order of weight:

- RESUME — primary.
- SETTINGS — secondary.
- RESET MATCH — secondary, framed in ward violet and carrying the line "Clears every tower and returns the board to READY." The frame is a colour and colour alone does not communicate state (`docs/BRANDING_GUIDE.md`); the sentence is what actually warns.
- EXIT TO TITLE — tertiary, unframed.

The screen also shows the local seat's live lives, gold and effective income, so a player who paused to think can see where they stand without dismissing the menu.

Pause behavior:

- Pausing stops simulation ticking.
- Opening settings from pause should keep the match paused.
- Returning from settings should return to paused, not automatically resume.
- Restart should return to READY with no towers, creeps, bot decisions, or transient FX.

## Settings Menu

Recommended MVP settings:

- Audio mute.
- Feedback volume.
- Reduced effects.
- Text scale.
- Safe area/debug overlay toggles for development builds.

Settings are currently accessible from the title shell, pause menu, and results action bar.

Current development shortcuts already exist for some presentation toggles. The overlay settings panel now exposes the player-facing subset.

## Results Menu

Results appears when the match ends.

Shipped contents:

- VICTORY or DEFEAT as a word, gold for a win, cloud for a loss — and the winning seat named in the line below either way, so the outcome survives being read in greyscale.
- The full eight-seat scoreboard: seat (the local one marked `(YOU)`), state as a word (WON / OUT / ALIVE), lives, income and gold. The winner and the local seat are marked by a coloured left rail rather than a tinted row.
- REMATCH — primary.
- SETTINGS — secondary.
- EXIT TO TITLE — tertiary.
- Replay/export button only for development or later UX polish.

`MatchResultsBillboard` was **removed** on 2026-08-03. It drew the scoreboard as a separate IMGUI card floating above the old results action bar, and that scoreboard now lives inside the results screen. Keeping it would have meant a component that could never draw — a summary exists only while a shell screen owns the display, so its `ModalScreenActive` gate would have been permanently closed — and, worse, two visual languages stacked on one screen.

Current implementation references:

- `Assets/Scripts/UI/ShellScreenView.cs`
- `Assets/Scripts/Simulation/LocalSessionFlowOverlay.cs`
- `LTW.Simulation.Economy.MatchSummary`

## Main Menu Roadmap

The current title/menu shell is overlay-driven inside the local vertical slice. A later standalone title scene should use the same structure once the game needs durable mode selection, save/load, campaign, or tutorial routing:

```text
Title
  Continue later
  New Local Match / Start Game
  How To Play
  Settings
  Credits / Legal later

New Local Match
  Start
  Bot setup later
  Seed/replay setup later

Settings
  Audio
  Effects
  Text/accessibility
```

Avoid adding account, matchmaking, cloud save, store, or monetization entries during the offline MVP unless the scope explicitly changes.

## Acceptance Checklist

Before calling menu/runtime flow healthy:

- [ ] READY state shows no pre-start bot towers, creep sends, send text, leak text, or combat FX.
- [ ] PLAY starts the opening build countdown once and only once.
- [ ] BUILD countdown runs for at least 30 seconds.
- [ ] BUILD countdown allows tower placement while blocking sends and simulation ticks.
- [ ] Countdown expiry starts the simulation once and only once.
- [ ] RESET returns to clean READY state.
- [ ] BUILD opens/closes without spending gold or placing a tower.
- [ ] Tower selection enters placement mode with visible tower-switch, ALL, BUILD, and CANCEL touch controls.
- [ ] SEND opens/closes without queueing creeps.
- [ ] SEND actions are rejected or clearly blocked before LIVE state.
- [ ] Lane selector changes camera only.
- [ ] Pause stops ticks and resumes cleanly.
- [ ] Results prevents further live commands unless restarting/rematching.
- [ ] HUD/drawers respect portrait safe areas.
- [ ] Debug diagnostics can be disabled or hidden for player-facing captures.

## Screenshot Evidence States

The durable capture set for menu/runtime flow should include:

1. Ready before PLAY.
2. Live shortly after PLAY.
3. Build drawer open.
4. Tower selected / placement preview valid.
5. Tower selected / placement preview invalid.
6. Send drawer open.
7. Lane selector open.
8. Paused.
9. Results, both outcomes — VICTORY and DEFEAT differ in composition, not only in numbers.
10. Reduced-effects live combat.
11. Title.
12. Settings over the title, which is the shot that proves IMGUI still lands on top of the runtime panel.

Capture evidence should use actual Game View UI rather than camera-only renders when evaluating HUD/menu readability. `Assets/Editor/RealUiCaptureRunner.cs` covers all of the above and must be run with **no** `-batchmode` flag; it pins the Game view itself to 1080x1920, waits out each screen's enter transition before shooting, and fails the run if fewer files reach disk than shots were taken.
