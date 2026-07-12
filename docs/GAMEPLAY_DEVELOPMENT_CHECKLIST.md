# LTW Gameplay Development Checklist

## Purpose

The current build can launch a local Unity vertical slice, but it is not yet mature enough to justify mobile-device distribution work. This checklist forks the next phase toward game development: readable play, meaningful choices, pacing, content variety, feedback, and repeatable playtest evidence.

Resume iOS TestFlight work only after this fork produces a local desktop/Unity session that is worth testing on a device.

## Current Baseline

- The local Unity scene loads `Assets/Scenes/LocalVerticalSlice.unity` without current console errors.
- The simulation supports three 12x9 lanes, one human player, two bots, placement, sends, selling, replay export, and match summaries.
- Automated .NET tests pass, including deterministic local-match coverage.
- Presentation systems exist for lane cells, towers, creeps, events, pooled objects, audio cues, vibration hooks, and presentation modes.

## Gameplay Fork Board

| ID | Initiative | Owner | Depends On | Exit Signal |
| --- | --- | --- | --- | --- |
| GD-00 | Playable-loop baseline | Integration | MVP-06, MVP-08, MVP-09 | A local Unity session can be started, restarted, and observed end to end. |
| GD-01 | Board readability and camera | Presentation | GD-00 | A tester understands lanes, paths, spawn, exit, ownership, and pressure without explanation. |
| GD-02 | Placement and tower controls | Gameplay UI | GD-00 | Placing, canceling, selling, and selecting towers feels deliberate and recoverable. |
| GD-03 | Tower, creep, and send content | Simulation | GD-00 | The game has at least three distinct tower choices and three distinct creep/send pressures. |
| GD-04 | Economy, pacing, and match length | Gameplay tuning | GD-03 | A match has early, middle, and closing pressure instead of a flat simulation run. |
| GD-05 | Bot behavior as opponents | Simulation QA | GD-03, GD-04 | Bots create readable pressure patterns and react enough to feel like opponents. |
| GD-06 | Session flow and results | Integration | GD-04 | Start, pause, reset, win/loss, results, and replay export form a complete local session. |
| GD-07 | Game feel and feedback | Presentation | GD-01, GD-04 | Builds, hits, kills, leaks, income ticks, and eliminations are satisfying and legible. |
| GD-08 | Playtest evidence and tuning notes | Design QA | GD-01 through GD-07 | At least three local playtest runs produce notes, metrics, and prioritized fixes. |

## GD-00: Playable-Loop Baseline

### Deliverables

- [ ] Add or document a single local run command for building the simulation DLL and opening the Unity scene.
- [ ] Confirm the human lane can place, send, sell, reset, and observe bots in Play Mode.
- [ ] Capture one full local run with seed, match duration, winner, replay path, and Unity console status.
- [ ] Record the most painful usability gaps found during the run.

### Acceptance Checks

- [ ] A developer can reproduce the local run from a clean checkout without Xcode.
- [ ] Play Mode can restart without stale match state or duplicate scene objects.
- [ ] No critical Unity console errors appear during startup, match play, or reset.

## GD-01: Board Readability And Camera

### Deliverables

- [ ] Frame the 12x9 lane grid clearly at desktop and mobile aspect ratios.
- [ ] Make own lane, target lane, spawn, exit, blocked cells, towers, and creep paths visually distinct.
- [ ] Add an obvious selected-lane or inspected-lane state.
- [ ] Add placement preview feedback for legal, blocked, unaffordable, and path-blocking cells.

### Acceptance Checks

- [ ] A first-time tester can identify where enemies enter, where they exit, and which lane they own.
- [ ] Creep movement and tower ranges remain readable when several creeps are active.
- [ ] UI controls do not cover the active placement area.

## GD-02: Placement And Tower Controls

### Deliverables

- [ ] Support a tower palette with at least three tower slots, even if some content is still placeholder.
- [ ] Add inspect/select behavior for existing towers.
- [ ] Add sell and upgrade hooks or disabled states with clear affordance.
- [ ] Make confirm/cancel, invalid feedback, and recovery fast enough for repeated play.

### Acceptance Checks

- [ ] A tester can complete ten placements and corrections without confusion or a blocking dialog.
- [ ] Invalid placements preserve gold and explain the failure in the HUD.
- [ ] Tower selection state is never ambiguous after a send, sell, reset, or lane swap.

## GD-03: Tower, Creep, And Send Content

### Deliverables

- [ ] Add first-pass content for three tower roles: reliable single-target, area/control, and economy or utility.
- [ ] Add first-pass content for three creep/send roles: runner, brute, and swarm.
- [ ] Give each creep/send a different cost, income gain, cooldown, and pressure profile.
- [ ] Extend tests so new content validates through the existing simulation contracts.

### Acceptance Checks

- [ ] Each tower is best at a different problem.
- [ ] Each creep/send creates a different defensive response.
- [ ] Content can be tuned without changing Unity presentation code.

## GD-04: Economy, Pacing, And Match Length

### Deliverables

- [ ] Define target ranges for first send, first leak, first elimination, and match completion.
- [ ] Tune starting gold, income interval, send rewards, bounties, lives, and cooldowns around those ranges.
- [ ] Add scenario tests for low-pressure, normal-pressure, and heavy-pressure matches.
- [ ] Record current known balance problems in a tuning log.

### Acceptance Checks

- [ ] A normal local match has visible escalation within the first few minutes.
- [ ] Defensive play and sending both have understandable value.
- [ ] Matches avoid both instant collapse and long no-progress stalls.

## GD-05: Bot Behavior As Opponents

### Deliverables

- [ ] Make bot profiles easy to identify in match setup or diagnostics.
- [ ] Tune bots to place, send, and recover in patterns a human can learn from.
- [ ] Add at least one pressure bot and one defensive bot profile.
- [ ] Log bot decisions in replay diagnostics.

### Acceptance Checks

- [ ] Bots produce visible pressure without depending on hidden advantages.
- [ ] Bot matches vary by profile while remaining deterministic for a fixed seed.
- [ ] A full local match can reach a winner through bot and human actions.

## GD-06: Session Flow And Results

### Deliverables

- [ ] Add a lightweight start state instead of dropping directly into an unclear running match.
- [ ] Add pause/resume and restart flow for local testing.
- [ ] Improve post-match results with winner, duration, leaks, sends, towers built, and replay export path.
- [ ] Ensure reset clears pooled presentation objects and HUD state.

### Acceptance Checks

- [ ] A tester can start, finish, review, and restart without leaving Play Mode.
- [ ] Results match the simulation summary and replay diagnostics.
- [ ] Reset does not leave ghost objects, stale selected towers, or old match text.

## GD-07: Game Feel And Feedback

### Deliverables

- [ ] Add distinct feedback for tower build, tower shot, creep hit, creep death, leak, send, income tick, and elimination.
- [ ] Keep effects readable under reduced-effects mode.
- [ ] Add simple audio mix controls or global mute for desktop testing.
- [ ] Review text scale and contrast in the HUD.

### Acceptance Checks

- [ ] The player notices important events without reading logs.
- [ ] Feedback improves understanding without hiding the grid.
- [ ] Reduced-effects mode remains functionally clear.

## GD-08: Playtest Evidence And Tuning Notes

### Deliverables

- [ ] Run at least three local playtests using different seeds or bot profiles.
- [ ] Record seed, duration, winner, first leak time, elimination time, replay path, and tester notes.
- [ ] Prioritize fixes into must-fix, should-fix, and later buckets.
- [ ] Decide whether the next fork should be more gameplay, local UX polish, or mobile validation.

### Acceptance Checks

- [ ] There is enough evidence to explain what is fun, confusing, slow, or broken.
- [ ] The next work queue is based on playtest observations, not only implementation completeness.
- [ ] iOS TestFlight work is resumed only if local play is coherent enough to benefit from device testing.
