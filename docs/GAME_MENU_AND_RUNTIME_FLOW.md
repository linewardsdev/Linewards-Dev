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

The current local Unity prototype starts directly in `Assets/Scenes/LocalVerticalSlice.unity`.

There is no dedicated title screen or full app shell yet. The current "game menu" is the runtime HUD layered over the local match:

- PLAY / PAUSE / RESET controls.
- Lane selector.
- BUILD drawer.
- SEND drawer.
- Placement close/cancel behavior.
- Match results overlay.
- Optional diagnostics overlay during development.

The READY state must be clean. No bot towers, creep sends, send beams, leak text, or combat FX should appear until the player starts the match.

## Design Principles

1. Keep gameplay visible.

   Menus should be drawers, compact panels, or overlays with deliberate safe margins. Avoid blocking the active lane unless the player is in a pause/settings/result state.

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
| Title | First app entry | No | Game logo, Continue, New Match, Settings |
| Pre-Match | Local setup | No | Bot count/difficulty later, seed/replay later, Start |
| Ready | Match scene loaded but not started | No | Board, HUD, PLAY, no bot activity |
| Live | Active match | Yes | HUD, BUILD, SEND, lane selector, pause/reset affordance |
| Paused | Temporary stop | No | Resume, Restart, Settings, Exit to Title |
| Results | Match complete | No | Winner, summary, replay/export later, Rematch, Exit |
| Settings | App/match options | No while modal | Audio, reduced effects, text scale, safe area/debug toggles |

For the MVP, Title and Pre-Match can be deferred. Ready, Live, Paused, Results, and Settings/presentation toggles are the priority.

## Runtime Flow

```text
Open LocalVerticalSlice scene
    |
    v
READY
    - Board visible
    - HUD visible
    - PLAY available
    - No bots build/send
    - No creep/combat effects
    |
    | player taps PLAY
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

## Runtime HUD

The runtime HUD should show:

- Current match state: READY, LIVE, PAUSED, or RESULTS.
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
- Placement mode shows the builder/ghost, selected cell, validity, and confirm/cancel controls.
- A close/cancel affordance must always be available while the drawer or placement mode is open.
- Closing the drawer must not place a tower or spend gold.
- Invalid placement should explain the reason without a blocking dialog.

Current implementation references:

- `Assets/Scripts/UI/TouchPlacementController.cs`
- `Assets/Scripts/UI/PlacementFeedbackView.cs`
- `Assets/Scripts/Simulation/UnityCommandAdapter.cs`

## Send Drawer

SEND opens a compact creep drawer mirroring the build drawer.

Recommended creep button content:

| Creep | Compact label | Required info |
| --- | --- | --- |
| Runner | RUN | Cost, income gain |
| Brute | BRUTE | Cost, income gain |
| Swarm | SWARM | Cost, income gain |
| Shade | SHADE | Cost, income gain |
| Siege | SIEGE | Cost, income gain |

Behavior:

- SEND is disabled or gives clear feedback before the match starts.
- Sending is allowed only in LIVE state.
- Opening the drawer must not send anything.
- Tapping a creep queues a send through the simulation command path.
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

Recommended MVP options:

- Resume.
- Restart match.
- Settings.
- Exit to title once a title screen exists.

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

Settings should be accessible from pause first. A title-screen settings entry can come later.

Current development shortcuts already exist for some presentation toggles, but they are not a player-facing settings menu yet.

## Results Menu

Results appears when the match ends.

Recommended MVP contents:

- Winner.
- Player lives/gold/income summary.
- Rematch.
- Exit to title once a title screen exists.
- Replay/export button only for development or later UX polish.

Current implementation references:

- `Assets/Scripts/Simulation/MatchResultsBillboard.cs`
- `LTW.Simulation.Economy.MatchSummary`

## Main Menu Roadmap

The title/main menu can wait until the local match loop is stable, but the target structure should be:

```text
Title
  Continue
  New Local Match
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
- [ ] PLAY starts the simulation once and only once.
- [ ] RESET returns to clean READY state.
- [ ] BUILD opens/closes without spending gold or placing a tower.
- [ ] Tower selection enters placement mode with a visible close/cancel affordance.
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
9. Results.
10. Reduced-effects live combat.

Capture evidence should use actual Game View UI rather than camera-only renders when evaluating HUD/menu readability.
