# Line Tower Wars Mobile Architecture

## Goal

Build a mobile-first Line Tower Wars prototype in Unity and C# that proves the core loop before committing to multiplayer infrastructure. The first playable version runs a complete three-player carousel match on one device with one human player and two simulated opponents.

The design must keep game rules, simulation, presentation, and future network services separate. `LTW.Simulation` is a pure .NET/C# library with no Unity dependencies. Online multiplayer is a later replacement for the source of player commands, not a rewrite of combat or pathing.

## Architecture Principles

1. **Simulation owns outcomes.** UI, animation, audio, and effects may display game state but must not decide economy, pathing, damage, leaks, or victory.
2. **Commands, not direct mutations.** A player requests actions such as `PlaceTower`, `QueueSend`, or `SellTower`; the simulation validates and applies them.
3. **Fixed-step simulation.** Match logic advances in a fixed tick so that it can be reproduced, tested, and eventually executed by an authoritative server.
4. **Data drives balance.** Towers, creeps, costs, income, and waves live in versioned configuration, rather than scattered gameplay code.
5. **Offline first.** No account, cloud save, matchmaking, or real-time networking is required to play the first prototype.
6. **Instrument before scaling.** Performance and game-balance events are captured locally during the prototype and prepared for future upload.

## High-Level Shape

```text
+------------------------ Mobile App -------------------------+
|                                                              |
|  Presentation Layer                                          |
|  - Touch input, HUD, build/send menus, camera                |
|  - Rendering, animation, sound, particles                    |
|                    |                                         |
|                    v                                         |
|  Command Layer                                               |
|  - Convert input and bot choices into validated commands     |
|                    |                                         |
|                    v                                         |
|  Match Simulation                                            |
|  - Economy, pathing, creeps, towers, combat, lives, results  |
|                    |                                         |
|          +---------+----------+                              |
|          v                    v                              |
|    State Snapshots       Event Stream                        |
|    for rendering         for effects, UI, telemetry          |
|                                                              |
|  Local Data                                                     |
|  - Balance definitions, maps, saves, replays, diagnostics    |
+--------------------------------------------------------------+

Future online services replace local command sources and host the same
simulation for competitive matches.
```

## Solution Boundaries

```text
LTW.Simulation      Pure .NET/C# match rules, commands, state, and replay support
LTW.UnityClient     Unity mobile presentation, touch input, local bots, and local saves
LTW.MatchServer     Headless .NET service that runs authoritative match instances
LTW.Tests           Unit, scenario, replay, and balance tests for LTW.Simulation
```

`LTW.UnityClient` references `LTW.Simulation`; `LTW.MatchServer` also references `LTW.Simulation`. Neither simulation tests nor the match server may depend on Unity assemblies.

## Mobile Client Modules

### Presentation

The presentation layer is responsible for making the game readable and quick to control:

- Own-lane and target-lane view swaps.
- Snap-to-grid and draw-mode placement previews.
- Spawning dock, tech panel, blueprint overlays, and match HUD.
- Sprite or low-poly rendering, animation, audio, haptics, and pooled effects.
- Accessibility settings such as text scale, color support, and reduced effects.

It reads simulation snapshots and events. It never changes match state directly.

### Command Layer

The command layer translates input into immutable requests with a tick number, player ID, and payload. The simulation either accepts a command and emits a resulting event, or rejects it with a clear reason for the UI.

Initial command types:

| Command | Purpose |
| --- | --- |
| `PlaceTower` | Build a tower at a grid position. |
| `SellTower` | Remove an owned tower for the allowed refund. |
| `QueueSend` | Spend gold to send a creep to the next carousel lane. |
| `BuyTech` | Unlock a permitted tech tier or branch. |
| `SetBlueprintStep` | Request the next legal placement from a saved template. |
| `PauseSimulation` | Prototype-only local pause control. |

### Match Simulation

`LTW.Simulation` runs all three lanes, including AI-controlled players. It is pure gameplay logic with no Unity scene-object references, network calls, file writes, or UI dependencies.

Responsibilities:

- Fixed tick clock and income ticks.
- Gold, income, cost, cooldown, and bounty accounting.
- Carousel routing: player A sends to B, B sends to C, C sends to A.
- Grid occupancy and pre-spend path validation.
- Creep movement, targeting, collision policy, tower attacks, and damage.
- Leaks, lives, elimination, overtime, and results.
- Deterministic pseudo-randomness seeded when a match begins.

The prototype target should begin at 10 to 20 simulation ticks per second. Render interpolation can make movement smooth without making gameplay timing dependent on frame rate.

### Bot Controller

Bots are command producers, not special-case game logic. Each bot receives the same public lane state available to a player and issues the same commands through the command layer.

Three configurable bot profiles:

- **Greedy:** prioritizes income and sends from the start; exempt from the lane-pressure hold below.
- **Balanced:** maintains a simple defense threshold and sends steadily.
- **Defensive:** builds early and shifts to aggression later.

This gives us repeatable balance tests without needing online players.

Any subset of lanes 2-8 can be independently bot-enabled or left empty (`LocalMatchOptions.WithLane`, `BotLaneOptions`) rather than the earlier all-or-nothing "every non-human lane gets a bot" rule.

Bot decisions are state-driven, not tick-scheduled: each profile's tuning (aggression, defense bias, minimum gold reserve, minimum tower coverage, and its tower **build order**) lives in content data (`ContentCatalog.BotProfiles`, via `BotProfileDefinition`) rather than hardcoded constants. A bot keeps building towers past any fixed count as long as gold above its reserve floor and an unused placement slot remain (`BotController.TryBuild`); it holds sends until its own minimum tower coverage is met (`BotController.HasMinimumDefenseCoverage`) and while its own lane's incoming creep health exceeds a coverage-scaled pressure threshold (`BotController.IsLaneUnderPressure`); and creep-tier preference now gates on accumulated income rather than elapsed ticks. See `docs/GD_TUNING_LOG.md`'s "Per-Lane Bot Toggle + Reactive Bot Spending" entry for the full rationale, including two real bugs this design caught (a premature-send exploit from cheap towers, and a permanent send-lockout from an unscaled pressure threshold).

**All of it lives in `LTW.Simulation/Bots/`, and reaches the match through one interface.** `BotController.TakeTurn` is a bot's whole tick — send, build, buy a category tier, raise a tower, in that order — and it sees the board only through `IBotMatchContext`: player state, owned towers, live creep health in a lane, the current route, and a placement probe that answers what a build *would* do without doing it. `LocalVerticalSlice` implements that interface with a private adapter and otherwise knows nothing about how bots decide; it owns only the order the seats decide in (player-id order, for replay determinism). Every bot action goes through the same `PlaceTower`/`QueueSend`/`BuyCategoryTier`/`UpgradeTower` methods a human tap does, so a bot cannot build in another seat's lane or overspend — it is refused by the same rules. Until 2026-08-01 the decisions lived in the bridge instead, and their build orders named `SampleVerticalSliceContent` constants directly, which made bot behaviour a property of the simulation assembly rather than of the catalog being played (`OPEN_ITEMS.md` item 26).

### Content And Persistence

Game content should be separate from code and versioned with the app:

- Tower, creep, tech, and wave definitions.
- Map and grid layouts.
- Bot profile parameters.
- Balance-version identifier embedded in every match record.

Persist locally:

- Settings and accessibility preferences.
- Blueprint templates.
- Match summaries and optional replay command logs.
- Performance diagnostics and telemetry queue.

Cloud profile storage is deferred until the game has a reason to support cross-device accounts.

## Simulation Data Flow

```text
Touch input or bot decision
        |
        v
Command with target tick
        |
        v
Validation: ownership, gold, cooldown, grid, open path
        |
        +--> Reject event for the UI
        |
        v
Apply to simulation state
        |
        v
Advance fixed tick
        |
        +--> State snapshot for renderer
        +--> Events for UI, effects, audio, and telemetry
```

## Pathing And Performance

Pathing is the principal technical risk, so it should be isolated behind a `PathService` contract.

- A tower placement is first evaluated against a temporary grid.
- The command is rejected unless every active route retains a valid path from spawn to exit.
- Only affected lanes should be recalculated after a placement or sale.
- Creep movement should use cached paths until the grid changes.
- Unit and projectile pools are owned by presentation, while simulation entities remain lightweight IDs and numeric state.

The first benchmark should simulate three lanes under a heavy send scenario across the available iOS test devices. Record tick duration, active creeps, path recalculation time, memory usage, and dropped render frames. Validate on representative Android hardware before broadening content scope or distribution.

## Testing Strategy

The simulation should be testable without launching the game client.

| Test Type | Examples |
| --- | --- |
| Unit | Income calculations, costs, bounty, cooldowns, damage, and life loss. |
| Rule | No valid command may fully block a lane; invalid commands do not spend gold. |
| Scenario | Fixed bot seeds produce expected match outcomes and duration ranges. |
| Replay | A recorded command log reproduces the same final state with the same content version and seed. |
| Device | Long simulated matches remain within frame-time and memory budgets. |

## Prototype Infrastructure

The initial infrastructure can be intentionally small:

- Source repository and protected `main` branch.
- Automated checks for simulation tests and formatting on every pull request.
- Build pipeline for iOS TestFlight when a playable client exists, followed by Android internal distribution for cross-platform validation.
- Crash reporting and product telemetry SDK, configured to keep personal data minimal.
- A small shared balance-data workflow, initially committed to source control.

No persistent game server, database, matchmaking queue, login provider, or cloud-hosted state is required for the simulated MVP.

## Future Online Architecture

When the local prototype proves fun, the online version should be server-authoritative. `LTW.MatchServer` should begin as a headless .NET service in one regional container, using WebSockets for client connections. This is appropriate for discrete build, sell, and send commands; transport can be revisited only if measurements show a need for lower-level networking.

```text
Mobile clients
   | commands and state updates
   v
Matchmaking service --> authoritative match instance --> match results service
   |                         |                         |
   v                         v                         v
Account/profile store     replay/telemetry store     ranking and cosmetics
```

Clients submit commands. The authoritative match instance validates them and broadcasts state updates or a compact event stream. The server owns random seeds, economy, path validation, combat, leaks, and final results. It consumes the same `LTW.Simulation` library used for local matches and test replays.

Candidate service boundaries:

- **Identity and profile:** account, settings, cosmetics, blueprints.
- **Matchmaking:** region, queue, party, and skill matching.
- **Match runtime:** isolated authoritative simulation per live match.
- **Results and ranking:** durable results, rating updates, seasonal progression.
- **Content delivery:** signed, versioned balance and cosmetic assets.
- **Observability:** crashes, match health, desync reports, and performance metrics.

Technology selection for those services is intentionally deferred. The first decision should follow prototype results: whether the engine can host a deterministic headless simulation efficiently, and how much concurrent match capacity we need.

## Security And Fair Play

For the simulated prototype, protect local saves from accidental corruption but do not treat them as trusted competitive data.

For online play:

- The server validates every command and owns all currency and result calculations.
- Clients never submit damage, gold totals, paths, leaks, or ratings as authoritative facts.
- Replay logs and content versions support dispute and desync investigation.
- Rate limits and command cooldowns are enforced server-side.
- A player may only build in their own home lane; send targets are derived from topology and
  never accepted from the client. See `docs/MULTIPLAYER_SEATS_AND_AUTHORITY.md` for the full
  list of command-authority rules the simulation enforces today.
- Telemetry collects only what is needed for operations, product decisions, and fraud detection.

## Delivery Phases

1. **Local vertical slice:** one lane, one player, fixed waves, placement, basic combat, and profiling.
2. **Simulated three-player match:** carousel routing, bots, income, elimination, and match results.
3. **Repeatability:** command logs, deterministic seed verification, automated simulation tests, and balance scenarios.
4. **Device validation:** iOS TestFlight builds across the available test-device matrix, followed by Android internal builds, crash reporting, telemetry, and performance budgets.
5. **Online spike:** headless authoritative simulation, command transport, and a small private match test.
6. **Production multiplayer:** accounts, matchmaking, ranking, cosmetics, and scalable match hosting.

## Decisions To Make Before Implementation

1. Create the Unity project, `LTW.Simulation`, `LTW.MatchServer`, and `LTW.Tests` solution structure.
2. Define first-device performance targets and the actual devices used for validation.
3. Define the first map grid dimensions and pathing algorithm prototype.
4. Define the initial balance-data format and validation rules.
