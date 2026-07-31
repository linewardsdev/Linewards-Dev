# LTW Agent Guidance

## Project Context

Line Tower Wars is a Unity and C# mobile game MVP. The first playable release is offline: one human player and two simulated opponents in a three-player carousel match.

Read these documents before changing code or project structure:

1. `PROJECT_GUIDE.md` for product rules and MVP scope.
2. `ARCHITECTURE.md` for runtime boundaries and future online direction.
3. `MVP_DEPENDENCIES.md` for approved MVP dependencies and deferred infrastructure.
4. `MVP_IMPLEMENTATION_CHECKLIST.md` for initiative ownership, prerequisites, and acceptance checks.

## Architecture Boundaries

The intended solution structure is:

```text
LTW.Simulation      Pure .NET/C# match rules, state, commands, events, and replay support
LTW.UnityClient     Unity presentation, touch input, rendering, audio, and local persistence
LTW.MatchServer     Deferred headless .NET authority host for future online play
LTW.Tests           Fast tests for LTW.Simulation
```

- `LTW.Simulation` must not reference Unity assemblies, `MonoBehaviour`, `GameObject`, scenes, coroutines, Unity time APIs, or Unity random APIs.
- `LTW.Simulation` must not perform networking, HTTP, authentication, direct file writes, or database access.
- Unity code renders simulation snapshots and translates player intent into commands. It does not duplicate economy, combat, pathing, leak, or match-result rules.
- Bots use the same commands and validation path as the human player.
- A future match server consumes the same simulation library. Do not introduce online-only rule variants into the MVP.

## MVP Scope

Build only the local three-player loop:

- Touch tower placement with open-path validation.
- Creep sending, income, carousel routing, combat, leaks, lives, elimination, and results.
- Two simulated opponents with normal command access.
- Seeded matches, replayable accepted command logs, and deterministic scenario tests.
- iOS-first device validation, followed by Android compatibility validation.

Do not add login, cloud saves, database storage, live networking, matchmaking, ranked systems, monetization, remote configuration, or managed cloud hosting unless the user explicitly expands scope.

## Initiative Ownership

- Work on one `MVP-XX` initiative at a time from `MVP_IMPLEMENTATION_CHECKLIST.md`.
- Before changing a shared simulation contract, inspect that initiative's dependencies and identify any affected downstream initiatives.
- Do not silently change public simulation state, command, event, content, or replay contracts owned by another initiative.
- Keep unrelated refactors out of the task.
- Prefer a dedicated branch or worktree for each parallel initiative.

## Dependencies

- Do not add a third-party package or service without a clear technical need.
- Record every proposed runtime dependency in `MVP_DEPENDENCIES.md` before adopting it.
- Pin selected SDK and package versions when the solution foundation is created.
- Prefer custom, deterministic grid pathing for the MVP unless a measured prototype proves it insufficient.

## Implementation Standards

- Commands are immutable requests. Validate before changing simulation state or spending resources.
- Use a fixed simulation tick; rendering runs independently.
- Keep game balance in versioned content definitions rather than scattered constants.
- Use explicit IDs and value types for players, lanes, entities, grid positions, and ticks.
- Seed every source of gameplay randomness and preserve the seed in replay data.
- Keep Unity presentation objects pooled once repeated creep, projectile, or effect spawning begins.

## Verification

- Add or update focused tests for every simulation rule change.
- Keep `LTW.Tests` runnable with `dotnet test` without Unity open.
- For pathing, test both legal placements and rejected full-block attempts.
- For economy, test spend, income, bounty, cooldown, and no-mutation-on-rejection cases.
- For replays, verify the same seed, content version, and accepted command log reproduce the same final state or state hash.
- For Unity-facing changes, state a concise manual verification path.
- Run the narrowest relevant tests before handoff and report any tests that could not be run.

### Renderer and art changes

`dotnet test` cannot see any of this. If you touch a render setting, a body material, the
URP asset, or a shader, run the headless validators listed in
[Render and art validation](RENDER_AND_ART_VALIDATION.md) — they exit non-zero and each one
exists because of a defect that produced no error and no warning.

- **When adding a check, break the thing it watches and confirm it fails for the right
  reason.** The original post-processing guard tested `profile == null`, which passed
  happily for a profile that existed and was completely hollow.
- **Do not judge an art or render change from a contact sheet or a `-nographics` run.**
  Neither has the board, the light rig or the post stack, so neither can show grounding,
  lighting craft or tone.

### Running Unity headlessly

- **Editor is `6000.5.3f1` and nothing else.** `6000.3.12f1` silently downgrades
  `ProjectSettings.asset` from serialized version 29 to 28.
- **`rm -rf unity/LTW.UnityClient/Temp/__Backupscenes` before every batch run.** A leftover
  backup opens a scene-recovery modal that blocks batchmode forever with a healthy-looking
  log.
- **Quit the interactive editor from its menu, not with a kill** — a force-kill is what
  leaves those backups.

## Device Validation

- Treat the available iOS devices as the first test matrix.
- Validate touch placement, send actions, view swapping, complete matches, and heavy-send stress scenarios on the oldest selected iOS baseline device.
- Validate the same core scenarios on a representative Android device before broad external distribution.
- Record tick time, frame time, memory, active entity counts, and reproducible performance issues with build and replay details.

## Handoff Format

At the end of an initiative, report:

1. Initiative ID and completed checklist items.
2. Files changed and public contracts added or altered.
3. Tests and manual verification performed.
4. Known limitations, risks, and explicit follow-up work.
5. Commit hash or branch name, when one was created.
