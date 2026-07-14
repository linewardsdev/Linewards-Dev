# Builder Placement Concept

Status: presentation prototype active. The builder avatar has been reintroduced as a separate visual layer while the existing preview/confirm placement flow remains authoritative.

## Goal

Replace the detached “pick a tower, drag a ghost, confirm” feeling with an in-world builder avatar: a small creep-like worker that can move freely across the player lane and build the last selected tower when the player chooses a square.

The short-term goal is better mobile readability and faster tower placement. The long-term goal is making building feel like part of the lane world instead of a separate menu mode.

## Recommended MVP Interpretation

For the first implementation, the builder should be a presentation-layer placement avatar, not a simulation entity.

That means:

- It does not block creeps.
- It does not have health.
- It does not affect combat, targeting, income, send timing, or lane ownership.
- It can visually move anywhere on the player lane, but actual builds still use the existing placement validation.
- It remembers the last selected tower role.
- Selecting a square moves the avatar and previews that remembered tower.

This gives us the better UX without opening pathing, balance, networking, or save-state questions too early.

## Player Flow

1. Player taps `BUILD`.
2. Player chooses a tower category, such as Arrow, Control, Relay, Pulse, or Prism.
3. The palette closes.
4. The builder avatar becomes active and takes on the selected tower accent color.
5. Player taps a lane square.
6. The builder moves to that square and the existing preview/confirm step evaluates the target.
7. If the player confirms and the build succeeds, the tower appears and the builder remains at the target cell.
8. The builder remembers that tower, allowing the player to tap another cell and confirm another placement quickly.
9. If the build fails, the builder remains visible and the feedback text explains why.
10. Player can select another tower or close/cancel builder mode without changing simulation state.

## Core Rules

- Last selected tower is persistent during the session.
- Tapping a tower card changes the remembered tower.
- Tapping a valid empty build square previews the remembered tower; confirmation places it.
- Tapping an invalid square shows rejection feedback without closing builder mode.
- Tapping an existing owned tower while builder mode is inactive selects that tower for inspection/sell.
- Tapping an existing owned tower while builder mode is active should reject as occupied rather than switching to sell mode.
- The builder is visually free-moving, but the command adapter remains the authority for whether a tower can actually be placed.
- Gold remains the limiting factor.
- The existing confirm action remains required for the avatar prototype to prevent accidental touch placement.
- A successful placement does not close builder mode; the last selected tower remains armed for repeat placement.

## Builder Visual Direction

The builder should read as friendly/player-owned, not as an enemy creep.

Recommended first-pass look:

- Small ward-tech worker/drone silhouette.
- Mint/blue player-owned body language.
- Accent trim changes to match selected tower role.
- Small circular foot marker on the currently selected cell.
- Soft path/target reticle when the player taps a square.
- Red rejected pulse for invalid placement.
- Tiny build burst when a tower successfully places.

Avoid making it look like a combat unit until/unless the design intentionally adds builder vulnerability later.

## UI Changes

The build menu can stay compact:

- `BUILD` opens tower choices.
- Tower card tap selects tower role and activates builder mode.
- The build button text can change to the selected tower name while builder mode is active.
- A small close/cancel affordance should exit builder mode.
- The old placement panel should be removed or reduced to one compact feedback line.

## Current Prototype Boundary

The avatar is intentionally not a simulation entity. It is created by `TouchPlacementController`, has no colliders, follows the selected cell, and changes accent with the selected tower. Tower preview, validation, gold checks, and confirmation still use the existing command adapter path. This boundary is deliberate after the earlier tap-to-build prototype caused placement regressions.

Suggested active-state text:

- `ARROW`
- `CTRL`
- `RELAY`
- `PULSE`
- `PRISM`

This aligns with the current mobile-first UI direction: fewer panels, more lane-native interaction.

## Minimal Implementation Path

### Step 1: Refactor placement confirmation

In `TouchPlacementController`, split the current `ConfirmPlacement()` logic into a reusable helper, for example:

- `TryPlaceSelectedTower(bool stayInBuilderMode)`

The existing confirm hotkey can call it with `false` to preserve old behavior during transition.

Builder taps can call it with `true` so successful placement does not close placement mode.

### Step 2: Add builder mode state

Add explicit state separate from the old ghost behavior:

- `builderModeActive`
- `lastSelectedTowerRole`
- `builderAvatar`
- `builderTargetCell`

The existing `selectedTowerRole` can likely become the active builder tower role.

### Step 3: Change tap behavior while placing

Current behavior while `isPlacing` is:

- tap cell;
- move ghost;
- wait for confirm.

Builder behavior should become:

- tap cell;
- move builder and ghost/reticle;
- preview placement;
- if preview accepted, place immediately;
- keep builder mode active.

### Step 4: Keep command validation unchanged

Do not duplicate placement rules in UI.

The builder should continue to call:

- `PreviewSampleTower`
- `PreviewControlTower`
- `PreviewUtilityTower`
- `PreviewPulseTower`
- `PreviewPrismTower`
- `PlaceSampleTower`
- `PlaceControlTower`
- `PlaceUtilityTower`
- `PlacePulseTower`
- `PlacePrismTower`

The simulation remains the source of truth.

### Step 5: Replace or restyle the ghost

The existing placement ghost can become:

- the selected cell reticle;
- the builder destination marker;
- or the base for a temporary builder avatar prototype.

For the first prototype, reuse the ghost object and add child primitives until a proper builder prefab exists.

### Step 6: Add visual review captures

Use the screenshot UI review flow to verify:

- builder mode inactive;
- builder mode active with each tower category;
- valid placement;
- invalid/occupied placement;
- low-gold placement rejection;
- small/mobile viewport readability.

## Risks And Open Questions

- Accidental builds: immediate placement is faster, but easier to mis-tap.
- Tower selection vs tower placement: active builder mode should clearly override tower inspection.
- Cancel affordance: player needs an obvious way to leave builder mode.
- Animation expectations: if the builder visibly walks slowly, players may expect delayed build timing.
- Lane boundaries: visual freedom should not imply illegal building is allowed.
- Multiplayer future: if builder becomes a real unit later, it will need deterministic sim support.

## Recommendation

Prototype this as a UI/presentation upgrade first.

Do not add a true simulation builder entity yet. Once the tap-to-build flow feels good, we can decide whether the builder should become a deeper gameplay mechanic with travel time, vulnerability, upgrades, or multiple workers.

## Acceptance Checklist

Implementation status: not active in code.

- [ ] Selecting a tower role activates builder placement mode.
- [ ] Tapping an empty valid square builds the selected tower immediately.
- [ ] Builder mode remains active after a successful build.
- [ ] Tapping an invalid square shows rejection feedback and keeps builder mode active.
- [ ] Tapping an occupied square rejects instead of opening sell/inspect while builder mode is active.
- [ ] Player can exit builder mode intentionally.
- [ ] Existing confirm hotkey still works as a fallback during transition.
- [ ] Gold and placement legality continue to come from simulation command validation.
- [ ] Mobile screenshots show the builder, target cell, feedback, and tower role clearly.
