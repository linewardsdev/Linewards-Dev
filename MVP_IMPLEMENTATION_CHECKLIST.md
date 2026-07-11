# LTW MVP Implementation Checklist

## Goal

Deliver an offline Unity MVP with one human player, two simulated opponents, a three-player carousel, touch placement, sends, economy, combat, leaks, elimination, replayable results, and iOS device validation.

This is a work plan for parallel agents. Every initiative has one owner, an explicit dependency set, and a completion check. An initiative is complete only when its acceptance checks pass, not when code merely exists.

## Working Rules

- `LTW.Simulation` is the shared source of truth. It has no Unity dependency.
- Each initiative gets one owner agent. Other agents may contribute only through a reviewed contract or a separate branch.
- The foundation owner approves changes to shared simulation contracts: state, commands, events, content definitions, and public interfaces.
- The integration owner is the only agent that combines changes across work streams after the foundation is stable.
- Do not add a third-party package, cloud service, or persistent backend without recording it in `MVP_DEPENDENCIES.md`.
- Keep commits scoped to one checklist item wherever practical.
- Every simulation feature needs a test. Every Unity feature needs a usable manual verification path.

## Dependency Map

```text
MVP-00 Solution foundation
        |
        v
MVP-01 Simulation contracts and content model
   |          |          |
   v          v          v
MVP-02     MVP-03     MVP-04
Pathing    Economy     Creep and tower combat
   |          |          |
   +-----+----+----------+
         v
MVP-05 Bots, replay, and match results
         |
         v
MVP-06 Unity simulation bridge and vertical slice
   |             |
   v             v
MVP-07        MVP-08
Touch UI       Presentation and pooling
   |             |
   +------+------+
          v
MVP-09 Full local three-player MVP integration
          |
          v
MVP-10 iOS TestFlight and device validation
          |
          v
MVP-11 Android compatibility validation
```

## Initiative Board

| ID | Initiative | Suggested Owner | Depends On | Parallel With |
| --- | --- | --- | --- | --- |
| MVP-00 | Solution foundation and CI | Foundation agent | None | None |
| MVP-01 | Simulation contracts and content model | Simulation agent | MVP-00 | None |
| MVP-02 | Grid, occupancy, and path validation | Pathing agent | MVP-01 | MVP-03, MVP-04 |
| MVP-03 | Economy, carousel, lives, and results rules | Economy agent | MVP-01 | MVP-02, MVP-04 |
| MVP-04 | Creep movement, tower combat, and events | Combat agent | MVP-01 | MVP-02, MVP-03 |
| MVP-05 | Bots, replay records, and scenario suite | Simulation QA agent | MVP-02, MVP-03, MVP-04 | None |
| MVP-06 | Unity bridge and local vertical slice | Unity integration agent | MVP-02, MVP-03, MVP-04 | None |
| MVP-07 | Touch placement and match HUD | Mobile UI agent | MVP-06 | MVP-08 |
| MVP-08 | Rendering, pooling, and feedback | Presentation agent | MVP-06 | MVP-07 |
| MVP-09 | Three-player MVP integration and tuning | Integration agent | MVP-05, MVP-07, MVP-08 | None |
| MVP-10 | iOS distribution, profiling, and acceptance | Mobile QA agent | MVP-09 | Documentation only |
| MVP-11 | Android compatibility validation | Mobile QA agent | MVP-10 | Documentation only |

## MVP-00: Solution Foundation And CI

**Owner:** Foundation agent
**Status:** [ ] In progress - local foundation complete; Unity and hosted CI verification pending
**Dependencies:** None

### Deliverables

- [x] Unity project shell created with pinned version metadata.
- [x] .NET solution created with `LTW.Simulation` and `LTW.Tests`.
- [x] `LTW.UnityClient` created or mapped to the Unity project boundary.
- [x] `LTW.MatchServer` represented as a deferred placeholder only; no server runtime required.
- [x] `global.json`, `.editorconfig`, and dependency lock strategy added.
- [x] Baseline CI configured to run formatting checks and `dotnet test`.
- [x] Repository README links to the architecture, dependencies, and this checklist.

### Acceptance Checks

- [x] A locked restore, format check, and `dotnet test` pass from the repository checkout.
- [x] `LTW.Simulation` has no Unity references, enforced by an architecture test.
- [ ] CI passes on a pull request containing only a trivial simulation test.
- [ ] Unity opens the project shell with the pinned editor version.

## MVP-01: Simulation Contracts And Content Model

**Owner:** Simulation agent
**Status:** [ ] Not started
**Dependencies:** MVP-00

### Deliverables

- [ ] Define value types for player ID, lane ID, grid position, entity ID, tick, gold, income, and lives.
- [ ] Define immutable command contracts: `PlaceTower`, `SellTower`, `QueueSend`, `BuyTech`, and `PauseSimulation`.
- [ ] Define command result and rejection-reason contracts.
- [ ] Define simulation events required by presentation and results.
- [ ] Define state snapshots that Unity can read without mutating simulation state.
- [ ] Define versioned content contracts for towers, creeps, tech, maps, and bot profiles.
- [ ] Implement a seeded random-source interface.
- [ ] Add validation for duplicate IDs, missing references, invalid costs, and invalid map data.

### Acceptance Checks

- [ ] A test can load valid sample content and reject malformed content.
- [ ] Commands can be created and validated without launching Unity.
- [ ] The public contracts are reviewed before MVP-02 through MVP-08 begin.

## MVP-02: Grid, Occupancy, And Path Validation

**Owner:** Pathing agent
**Status:** [ ] Not started
**Dependencies:** MVP-01

### Deliverables

- [ ] Implement lane grid, spawn, exit, walkable cells, and occupied cells.
- [ ] Implement a deterministic path search for a grid lane.
- [ ] Implement temporary-grid validation for a proposed tower placement.
- [ ] Reject a placement that removes every valid spawn-to-exit route.
- [ ] Cache or invalidate paths only for lanes changed by a placement or sale.
- [ ] Expose legal-placement and rejection information for Unity ghost placement.

### Acceptance Checks

- [ ] A legal placement produces a valid route.
- [ ] A blocking placement is rejected before gold is spent.
- [ ] The same map and placement sequence produces the same path result.
- [ ] A heavy placement scenario has a recorded benchmark result.

## MVP-03: Economy, Carousel, Lives, And Results

**Owner:** Economy agent
**Status:** [ ] Not started
**Dependencies:** MVP-01

### Deliverables

- [ ] Implement fixed-tick clock and income-tick schedule.
- [ ] Implement gold, income, send cost, income gain, cooldown, kill bounty, leak bounty, and sell refund rules.
- [ ] Implement three-player carousel routing.
- [ ] Implement life loss, elimination, and winner selection.
- [ ] Define a compact match summary with placements and key economy statistics.

### Acceptance Checks

- [ ] A send targets the next carousel lane and changes income exactly once.
- [ ] Insufficient-gold and cooldown violations reject without changing state.
- [ ] A leak affects the defender and credits the sender according to the configured rules.
- [ ] A completed elimination sequence produces one unambiguous winner.

## MVP-04: Creep Movement, Tower Combat, And Events

**Owner:** Combat agent
**Status:** [ ] Not started
**Dependencies:** MVP-01

### Deliverables

- [ ] Implement creep spawning, health, speed, path following, and exit detection.
- [ ] Implement one initial tower type with range, target selection, attack timing, and damage.
- [ ] Implement creep death, kill bounty intent, and leak events.
- [ ] Provide lightweight entity snapshots for Unity rendering.
- [ ] Keep projectiles visual-only unless projectile travel is needed for gameplay timing.

### Acceptance Checks

- [ ] A tower damages and kills a creep within expected ticks.
- [ ] A creep that reaches the exit emits one leak event only.
- [ ] Movement and combat results reproduce for a fixed seed and command sequence.
- [ ] The simulation remains independent of Unity objects and time APIs.

## MVP-05: Bots, Replay Records, And Scenario Suite

**Owner:** Simulation QA agent
**Status:** [ ] Not started
**Dependencies:** MVP-02, MVP-03, MVP-04

### Deliverables

- [ ] Implement greedy, balanced, and defensive bot decision profiles.
- [ ] Make bots issue normal commands through the command validator.
- [ ] Implement replay records containing seed, content version, map ID, player configuration, and accepted commands.
- [ ] Implement replay execution and final-state comparison.
- [ ] Add scenario tests for a complete three-player simulated match.
- [ ] Add a stress scenario for heavy sends and repeated placement validation.

### Acceptance Checks

- [ ] Three bots can complete a match without invalid state or unhandled exceptions.
- [ ] Replaying a saved match produces the same final state or state hash.
- [ ] Bot profiles demonstrably produce different income-versus-defense behavior.

## MVP-06: Unity Bridge And Local Vertical Slice

**Owner:** Unity integration agent
**Status:** [ ] Not started
**Dependencies:** MVP-02, MVP-03, MVP-04

### Deliverables

- [ ] Create a Unity match bootstrapper that loads content and starts `LTW.Simulation`.
- [ ] Advance simulation with a fixed-step accumulator while rendering independently.
- [ ] Translate Unity input requests into simulation commands.
- [ ] Read state snapshots and events without direct simulation mutation.
- [ ] Display one lane, one tower, one creep, gold, income, and lives.
- [ ] Provide a development-only match reset and diagnostic overlay.

### Acceptance Checks

- [ ] A player can run one lane locally, place a tower, send a creep, and see it resolve.
- [ ] Unity runs the same command sequence to the expected simulation result.
- [ ] The bridge contains no duplicate combat, economy, or pathing rules.

## MVP-07: Touch Placement And Match HUD

**Owner:** Mobile UI agent
**Status:** [ ] Not started
**Dependencies:** MVP-06

### Deliverables

- [ ] Implement mobile-safe HUD for gold, income, lives, and wave/send pressure.
- [ ] Implement tower selection, tap-to-snap ghost placement, nudge controls, confirm, and cancel.
- [ ] Show immediate invalid-path and insufficient-gold feedback.
- [ ] Implement the send dock for the first creep category and unit options.
- [ ] Implement own-lane and target-lane view swap.
- [ ] Keep controls accessible without covering the active grid.

### Acceptance Checks

- [ ] A tester can place, cancel, and sell a tower using only touch controls.
- [ ] An invalid placement is understandable and recoverable without opening a blocking dialog.
- [ ] A tester can send a creep and identify the resulting income change.

## MVP-08: Rendering, Pooling, And Feedback

**Owner:** Presentation agent
**Status:** [ ] Not started
**Dependencies:** MVP-06

### Deliverables

- [ ] Render towers, creeps, lane cells, spawn, exit, and ownership clearly.
- [ ] Add pooled presentation objects for creeps, projectiles if used, hit effects, and floating text.
- [ ] Render key simulation events: tower built, creep spawned, creep killed, leak, elimination, and income tick.
- [ ] Add readable low-cost feedback: basic sound, optional haptics, and restrained effects.
- [ ] Add settings for reduced effects and basic text-size support.

### Acceptance Checks

- [ ] Repeated creep waves do not create unbounded presentation objects.
- [ ] The player can distinguish owned towers, incoming creeps, leaks, and sends at a glance.
- [ ] The visual layer can be disabled or simplified without changing simulation outcomes.

## MVP-09: Full Local Three-Player Integration And Tuning

**Owner:** Integration agent
**Status:** [ ] Not started
**Dependencies:** MVP-05, MVP-07, MVP-08

### Deliverables

- [ ] Combine one human player and two bots into a complete carousel match.
- [ ] Add basic post-match summary and replay export for diagnostics.
- [ ] Tune initial tower, creep, income, bounty, and life values to reach the target match window.
- [ ] Run a heavy-send stress scenario during a full match.
- [ ] Document known balance and usability issues for the next iteration.

### Acceptance Checks

- [ ] A complete match starts, resolves, and returns to a usable post-match state.
- [ ] The match reaches a winner without manual intervention.
- [ ] Typical simulated matches fall in the intended early target range, or the deviation is documented with data.
- [ ] No known critical command, pathing, or state-replay failures remain.

## MVP-10: iOS TestFlight And Device Validation

**Owner:** Mobile QA agent
**Status:** [ ] Not started
**Dependencies:** MVP-09

### Deliverables

- [ ] Configure iOS signing and a TestFlight-capable build process.
- [ ] Define the iOS test matrix from the available devices, including the oldest supported device as the baseline.
- [ ] Capture tick time, frame time, memory, active entity count, and thermal observations during normal and stress matches.
- [ ] Run touch placement, send dock, view swap, and results-flow usability checks.
- [ ] File and prioritize reproducible defects with device, build, seed, and replay details.

### Acceptance Checks

- [ ] TestFlight build installs and runs on every selected iOS test device.
- [ ] The stress scenario completes on the baseline iOS device without crash, unrecoverable hitching, or corrupted match state.
- [ ] Performance results and known limitations are recorded in the repository.

## MVP-11: Android Compatibility Validation

**Owner:** Mobile QA agent
**Status:** [ ] Not started
**Dependencies:** MVP-10

### Deliverables

- [ ] Produce an Android internal build.
- [ ] Validate the same core interaction and stress scenarios on a representative Android device.
- [ ] Record platform-specific performance or input differences.
- [ ] Create remediation items for any material cross-platform gaps.

### Acceptance Checks

- [ ] The complete local three-player match works on the selected Android device.
- [ ] The stress scenario completes without corrupted state or a blocker-level performance failure.
- [ ] Cross-platform behavior differences are either resolved or explicitly accepted for the MVP.

## MVP Release Gate

- [ ] MVP-00 through MVP-10 are complete.
- [ ] MVP-11 is complete before broad external distribution.
- [ ] The local match loop is fun enough to justify an online multiplayer spike.
- [ ] Replays, diagnostics, and device evidence are available for every release candidate.
- [ ] No cloud backend is required to play the MVP.
