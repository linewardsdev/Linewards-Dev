# LTW MVP Dependencies

## Purpose

This document defines the minimum code, tooling, and infrastructure required to ship the first playable Line Tower Wars mobile MVP:

- One mobile player and two simulated opponents.
- A complete three-player carousel match on one device.
- Touch placement, sending, economy, combat, leaks, elimination, and match results.
- Repeatable tests and device performance evidence.

The MVP is offline. It does not require login, cloud saves, matchmaking, a persistent database, a live game server, or a monetization platform.

## Dependency Rules

1. Add a package only when it removes material implementation risk or time.
2. Pin tool, Unity package, and NuGet package versions once selected.
3. Keep `LTW.Simulation` free of Unity, HTTP, file-system, database, and third-party game-engine dependencies.
4. Prefer small, well-supported dependencies over framework stacks.
5. Record license, owner, version, purpose, and removal cost for every third-party runtime dependency.
6. Treat optional polish tools as deferred until the gameplay loop is proven.

## Required Toolchain

| Dependency | Required For | MVP Decision |
| --- | --- | --- |
| Unity LTS editor | iOS/Android client, touch UI, rendering, device builds | Required. Use a currently supported LTS release and commit the Unity project version metadata. |
| Unity iOS build support | iOS TestFlight builds and device profiling | Required early. iOS is the first performance-validation platform. |
| Unity Android build support | Android internal builds and cross-platform device profiling | Required before broader distribution, after the iOS-first vertical slice. |
| Supported .NET SDK | `LTW.Simulation`, server-ready code, and automated tests | Required. Pin the SDK with `global.json` after choosing the Unity-compatible C# target. |
| Git | Source control and reviewable change history | Required. |
| IDE with Unity and .NET support | Development and debugging | Required. Visual Studio or Rider are both suitable. |
| Physical test devices | Real performance and touch-control validation | Required. Use the available iOS devices as the first test matrix; add a representative Android device before broadening content scope. |

## Solution And Code Dependencies

```text
LTW.Simulation      Pure .NET/C# match rules
LTW.UnityClient     Unity app and presentation adapters
LTW.Tests           Fast tests for LTW.Simulation
LTW.MatchServer     Deferred .NET authority host for online play
```

Dependency direction:

```text
LTW.UnityClient  --->  LTW.Simulation  <---  LTW.Tests
                                  ^
                                  |
                         LTW.MatchServer (deferred)
```

`LTW.Simulation` must not reference the Unity client, and `LTW.Tests` must not require the Unity editor to run.

## Core Simulation Items

These are code dependencies, not optional design notes. The MVP cannot prove its core loop without them.

| Item | Responsibility | MVP Priority |
| --- | --- | --- |
| Match state | Player, lane, grid, gold, income, lives, creeps, towers, cooldowns, and match phase | Must have |
| Fixed-tick runner | Advances simulation using a stable tick rate independent of render frames | Must have |
| Command model | Immutable `PlaceTower`, `SellTower`, `QueueSend`, `BuyTech`, and local-pause commands | Must have |
| Command validator | Rejects illegal ownership, gold, cooldown, placement, and pathing actions before state changes | Must have |
| Economy system | Income ticks, send cost, income gains, kill bounty, leak bounty, and refunds | Must have |
| Carousel router | Routes sends A to B, B to C, and C to A | Must have |
| Grid and path service | Occupancy, valid path checks, cached routes, and placement preview support | Must have |
| Creep system | Spawn, movement, exit/leak detection, health, traits, and targeting data | Must have |
| Tower/combat system | Target selection, attack timing, damage, projectile intent, and creep death events | Must have |
| Life and results system | Life loss, elimination, overtime hook, winner selection, and match summary | Must have |
| Event stream | Typed simulation events consumed by UI, audio, effects, summaries, and diagnostics | Must have |
| Seeded random source | Reproducible random decisions for bots and any randomized gameplay | Must have |
| Content definitions | Versioned tower, creep, tech, map, and bot parameters loaded outside rule code | Must have |
| Replay record | Seed, content version, and accepted command log | Should have before balance work |
| Bot controller | Greedy, balanced, and defensive command producers using the normal command model | Must have for the three-player MVP |

### Explicitly Avoid In The Simulation Core

- `MonoBehaviour`, `GameObject`, scenes, coroutines, Unity timing, or Unity random APIs.
- Networking clients, WebSocket libraries, HTTP clients, authentication code, or cloud SDKs.
- Direct disk or database writes.
- Animation, audio, particles, screen-space coordinates, or touch input.
- Runtime dependency on a generic pathfinding package until a prototype proves custom grid pathing is insufficient.

## Unity Client Dependencies

| Dependency | Purpose | MVP Decision |
| --- | --- | --- |
| Unity Input System | Touch, taps, holds, drag/draw placement, and device input abstraction | Required. |
| Unity Test Framework | Unity-side smoke tests and play-mode checks | Required, but core rules stay in `LTW.Tests`. |
| Unity UI system | HUD, spawning dock, placement controls, results, and settings | Required. Choose one UI approach and keep it consistent. |
| Unity profiler and memory tools | Frame-time, memory, and object-count validation on real devices | Required. |
| Object-pool implementation | Reuse presentation objects for creeps, projectiles, effects, and floating text | Required once repeated spawning begins. Start simple and profile before adding complexity. |
| Unity Addressables | Remote or modular asset delivery | Deferred. The MVP can ship content in the app build. |
| Unity Analytics or third-party analytics SDK | Product telemetry | Deferred until a playable internal build exists and privacy requirements are chosen. |
| Crash-reporting SDK | Production crash triage | Deferred until internal device distribution begins. |
| Cinemachine | Camera behavior | Optional. Add only if native camera controls become cumbersome. |
| URP or a 2D renderer package | Rendering pipeline choice | Deferred until the 2D versus low-poly 3D prototype decision is made. |

## Test Dependencies

| Dependency | Purpose | MVP Decision |
| --- | --- | --- |
| .NET test framework | Unit and scenario testing for `LTW.Simulation` | Required. Use one conventional .NET framework and keep tests runnable with `dotnet test`. |
| Deterministic scenario fixtures | Repeat known bot seeds, content versions, and command sequences | Required. |
| Replay test harness | Re-run a saved command log and compare final state or state hashes | Required before substantial balance iteration. |
| Benchmark harness | Measure simulation tick, path validation, and heavy-send scenarios | Recommended after the vertical slice works. |
| Code coverage tooling | Track coverage trends | Deferred. Useful once the test suite is stable; not a substitute for scenario tests. |

### Pinned MVP-00 Development Dependencies

| Dependency | Version | Scope |
| --- | --- | --- |
| .NET SDK | `10.0.301` | Solution restore, build, formatting, and tests. |
| Microsoft.NET.Test.Sdk | `17.14.1` | .NET test execution. |
| xUnit | `2.9.3` | Simulation unit and scenario tests. |
| xunit.runner.visualstudio | `3.1.4` | Test discovery in IDE and CI environments. |

These are development and test dependencies only. `LTW.Simulation` has no third-party runtime package dependency.

Initial acceptance tests:

1. A legal tower placement keeps every route open and spends the expected gold.
2. An invalid blocking placement is rejected without spending gold.
3. A send routes to the next carousel player and adds the intended income.
4. A leak reduces only the defender's lives and grants the configured sender bounty.
5. A fixed seed and command log reproduce the same result.
6. Three bots can finish a complete match without exceptions or invalid state.

## Local Data And Content

| Item | Purpose | MVP Decision |
| --- | --- | --- |
| Versioned content files | Tower, creep, tech, map, and bot tuning data | Required. Keep in source control. |
| Content validation | Detect missing IDs, invalid costs, broken references, and impossible grid data before play | Required. |
| Local save adapter | Settings, optional blueprints, recent match summaries, and diagnostics | Required, with a simple device-local format. |
| Local replay storage | Debugging and balance reproduction | Recommended. Keep bounded by count or storage size. |
| Cloud save | Cross-device profile and blueprint sync | Deferred. |
| Remote configuration | Live balance changes | Deferred. Ship balance in the build until patch cadence is understood. |

## Build And Delivery Infrastructure

| Item | Purpose | MVP Decision |
| --- | --- | --- |
| GitHub repository | Source, documents, code review, and releases | Already in place. |
| Branch protection for `main` | Prevent accidental direct changes once active development begins | Recommended before adding implementation code. |
| `.editorconfig` and formatting rules | Consistent C# formatting and fewer review-only changes | Required when the solution is created. |
| `global.json` and dependency lock files | Reproducible SDK and NuGet resolution | Required when the solution is created. |
| CI workflow | Run `dotnet test`, validate formatting, and report failures on pull requests | Required with the first simulation code. |
| iOS TestFlight distribution | Installable builds for iOS device testing | Required after the first playable loop. |
| Android internal distribution | Installable builds for cross-platform validation | Required after the iOS baseline is stable and before broader distribution. |
| Signed release pipeline | Store submission and production releases | Deferred. |
| Secrets store | Protect API keys, signing material, and service credentials | Deferred until a build service or external SDK needs a secret. |

## Performance Validation Dependencies

The game is not ready to expand content until it can produce evidence from real devices.

Required measurements:

- Simulation tick time, including worst-case path validation.
- Render frame time and dropped frames during a heavy three-lane send.
- Active creep, projectile, and pooled-presentation-object counts.
- Managed and native memory use across a complete match.
- Battery and thermal behavior during an extended session.
- Touch-to-command latency for placement and send actions.

The first test matrix should use the available iOS devices, including the oldest supported device as the initial baseline. Add a representative Android device before broader content scope or distribution.

## Deferred Online Dependencies

Do not introduce these into the simulated MVP:

| Dependency | Reason To Defer | Trigger To Revisit |
| --- | --- | --- |
| `LTW.MatchServer` runtime | No online players yet | Local three-player loop is fun and stable. |
| WebSocket transport | No remote clients yet | Private online match spike. |
| Container image and registry | No server deployment yet | First headless server proof. |
| Cloud host or managed game hosting | No concurrency or regional demand yet | External multiplayer test with real players. |
| Database | No trusted accounts, results, or cloud profiles yet | Accounts or durable online results. |
| Authentication provider | No account requirement yet | Cross-device identity or multiplayer access. |
| Matchmaking | Fixed local simulated players | Private online matches are ready to expand. |
| Ranking and anti-cheat services | No competitive online results | Ranked mode planning. |
| Terraform or other infrastructure-as-code | No cloud resources to manage | First repeatable cloud environment. |

## Recommended Build Order

1. Create the Unity project, .NET solution, `LTW.Simulation`, and `LTW.Tests`.
2. Add fixed ticks, match state, commands, content loading, economy, and grid/path validation.
3. Add one tower, one creep, one map, one player, and tests for legal and illegal placement.
4. Add the Unity adapter, touch placement, basic rendering, and object pooling only when spawning repeats.
5. Add carousel sends, two bots, leaks, life, elimination, and results.
6. Add replay records, deterministic scenarios, and heavy-send benchmarks.
7. Produce an iOS TestFlight build and validate across the available iOS devices.
8. Produce an Android internal build for cross-platform validation.
9. Add content breadth only after the complete loop meets performance and usability targets on both platforms.

## Completion Gate For The MVP Foundation

The foundational dependency work is complete when:

- `dotnet test` runs the simulation suite without Unity installed or open.
- Unity runs a three-player match by referencing `LTW.Simulation`.
- Two bots use the same command and validation path as the player.
- A replay can reproduce a known match result with the same seed and content version.
- An iOS TestFlight build completes a stress scenario across the selected iOS baseline devices.
- An Android internal build completes the same stress scenario on the selected Android baseline device.
- The project has no runtime dependency on cloud infrastructure or a live backend.
