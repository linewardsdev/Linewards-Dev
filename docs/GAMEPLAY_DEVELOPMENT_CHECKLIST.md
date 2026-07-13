# LTW Gameplay Development Checklist

## Purpose

The current build can launch a local Unity vertical slice, but it is not yet mature enough to justify mobile-device distribution work. This checklist forks the next phase toward game development: readable play, meaningful choices, pacing, content variety, feedback, and repeatable playtest evidence.

Resume iOS TestFlight work only after this fork produces a local desktop/Unity session that is worth testing on a device.

## Current Baseline

- The local Unity scene loads `Assets/Scenes/LocalVerticalSlice.unity` without current console errors.
- The simulation supports three side-by-side 7x18 long north-south lanes, one human player, two bots, placement, sends, selling, replay export, carousel creep handoff, and match summaries.
- Automated .NET tests pass, including deterministic local-match coverage.
- Presentation systems exist for lane cells, towers, creeps, events, pooled objects, audio cues, vibration hooks, and presentation modes.

## Gameplay Fork Board

| ID | Initiative | Owner | Depends On | Exit Signal |
| --- | --- | --- | --- | --- |
| GD-00 | Playable-loop baseline | Integration | MVP-06, MVP-08, MVP-09 | A local Unity session can be started, restarted, and observed end to end. |
| GD-01 | Board readability and camera | Presentation | GD-00 | A tester understands lanes, paths, spawn, life loss, ownership, and pressure without explanation. |
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

- [x] Frame the side-by-side 7x18 long north-south lane grid clearly for the current local Unity camera baseline.
- [x] Make own lane, target lanes, spawn, exit, towers, and creep paths visually distinct.
- [x] Add an obvious selected-lane or inspected-lane state.
- [x] Add placement preview feedback for legal, blocked, unaffordable, and path-blocking cells.

Placement preview now queries the simulation bridge before confirmation, so legal, occupied, unaffordable, invalid-lane, and path-blocking outcomes use the same rules as actual placement.
The empty local scene now creates the runtime tower palette, send dock, placement ghost, and feedback toast during bootstrap. The lanes render side by side with top spawn boxes, bottom life-loss boxes, distinct build bands, a brighter center route, spawn/leak gates, and lane ownership tinting. Unity-side creep rendering uses larger markers, slower local ticks, top-to-bottom projection, and interpolation so movement is easier to read.
The first art-upgrade pass also aligns board material bands, endpoint halos, gate trim, and the default orthographic camera, outside-lane gutters, and corner anchors to the side-by-side north-south lane layout so the LTW-style vertical flow stays readable.

### Acceptance Checks

- [ ] A first-time tester can identify where enemies enter, where they exit, and which lane they own.
- [ ] Creep movement and tower ranges remain readable when several creeps are active.
- [ ] UI controls do not cover the active placement area.

## GD-02: Placement And Tower Controls

### Deliverables

- [x] Support a tower palette with at least three tower slots, even if some content is still placeholder.
- [x] Add inspect/select behavior for existing towers.
- [x] Add sell and upgrade hooks or disabled states with clear affordance.
- [x] Make confirm/cancel, invalid feedback, and recovery fast enough for repeated play.

Current tower palette exposes Arrow, Control, Relay, selected-tower inspect, and selected/last-tower selling for the local vertical slice. Upgrade remains intentionally out of scope until tower progression exists.

### Acceptance Checks

- [ ] A tester can complete ten placements and corrections without confusion or a blocking dialog.
- [ ] Invalid placements preserve gold and explain the failure in the HUD.
- [ ] Tower selection state is never ambiguous after a send, sell, reset, or lane swap.

## GD-03: Tower, Creep, And Send Content

### Deliverables

- [x] Add first-pass content for three tower roles: reliable single-target, area/control, and economy or utility.
- [x] Add first-pass content for three creep/send roles: runner, brute, and swarm.
- [ ] Give each creep/send a different cost, income gain, cooldown, and pressure profile.
- [x] Extend tests so new content validates through the existing simulation contracts.

Current sends have distinct cost, income, speed/health, and quantity pressure. Cooldown remains global through economy rules, so per-send cooldown differentiation remains open.

### Acceptance Checks

- [ ] Each tower is best at a different problem.
- [ ] Each creep/send creates a different defensive response.
- [ ] Content can be tuned without changing Unity presentation code.

## GD-04: Economy, Pacing, And Match Length

### Deliverables

- [x] Define target ranges for first send, first leak, first elimination, and match completion.
- [ ] Tune starting gold, income interval, send rewards, bounties, lives, and cooldowns around those ranges.
- [ ] Add scenario tests for low-pressure, normal-pressure, and heavy-pressure matches.
- [x] Record current known balance problems in a tuning log.

Initial target ranges and known balance questions are recorded in `docs/GD_TUNING_LOG.md`. Scenario coverage has started with send cooldown and early-pressure tests, but the full low/normal/heavy suite remains open.

### Acceptance Checks

- [ ] A normal local match has visible escalation within the first few minutes.
- [ ] Defensive play and sending both have understandable value.
- [ ] Matches avoid both instant collapse and long no-progress stalls.

## GD-05: Bot Behavior As Opponents

### Deliverables

- [x] Make bot profiles easy to identify in match setup or diagnostics.
- [x] Tune bots to place, send, and recover in patterns a human can learn from.
- [x] Add at least one pressure bot and one defensive bot profile.
- [x] Log bot decisions in replay diagnostics.

Bot profiles now surface through the local diagnostics overlay and playtest report. Balanced and Defensive bots now place first-pass defensive towers before creating send pressure, giving playtests visible opponent behavior without hidden advantages.

### Acceptance Checks

- [ ] Bots produce visible pressure without depending on hidden advantages.
- [ ] Bot matches vary by profile while remaining deterministic for a fixed seed.
- [ ] A full local match can reach a winner through bot and human actions.

## GD-06: Session Flow And Results

### Deliverables

- [x] Add a lightweight start state instead of dropping directly into an unclear running match.
- [x] Add pause/resume and restart flow for local testing.
- [x] Improve post-match results with winner, duration, player economy/life state, and replay/report export path.
- [ ] Ensure reset clears pooled presentation objects and HUD state.

The local session now starts in a ready state with a runtime start/pause/restart overlay. The results billboard shows winner, completion tick, and each player's final economy/life state. Replays and playtest reports can be exported from the local hotkeys. Pressing `P` shows a runtime toast when a Markdown playtest report is saved, and explains that the match must finish first if no completed replay exists yet.

### Acceptance Checks

- [ ] A tester can start, finish, review, and restart without leaving Play Mode.
- [ ] Results match the simulation summary and replay diagnostics.
- [ ] Reset does not leave ghost objects, stale selected towers, or old match text.

## GD-07: Game Feel And Feedback

### Deliverables

- [x] Add distinct feedback for tower build, tower shot, creep hit, creep death, leak, send, income tick, and elimination.
- [x] Keep effects readable under reduced-effects mode.
- [x] Add simple audio mix controls or global mute for desktop testing.
- [x] Review text scale and contrast in the HUD.

Tower attacks now emit damage events so Unity can show lane beams and hit cues before kills. Send actions now add lane-to-lane pressure beams plus sender/defender pulses. Reduced-effects mode keeps text/readability cues while skipping burst effects and beams, desktop hotkeys cover reduced effects/mute/volume, and HUD panels use compact high-contrast runtime styling. Tower and creep silhouettes now carry role-specific bases, halos, shadows, markers, starter tower silhouette props, and creep role props so Arrow, Control, Relay, Runner, Brute, and Swarm remain distinguishable without transient effects.

### Acceptance Checks

- [ ] The player notices important events without reading logs.
- [ ] Feedback improves understanding without hiding the grid.
- [ ] Reduced-effects mode remains functionally clear.

## GD-08: Playtest Evidence And Tuning Notes

### Deliverables

- [ ] Run at least three local playtests using different seeds or bot profiles.
- [x] Record seed, duration, winner, first leak time, elimination time, replay path, and tester notes.
- [ ] Prioritize fixes into must-fix, should-fix, and later buckets.
- [ ] Decide whether the next fork should be more gameplay, local UX polish, or mobile validation.

The local playtest recorder writes Markdown reports with seed/content/map, completion tick, winner, first send/leak/elimination observations, replay path, bot profiles, recent bot decisions, and tester-note prompts. Reports are saved under Unity's persistent data path; on the current Windows editor setup this is `C:\Users\engch\AppData\LocalLow\DefaultCompany\LTW_UnityClient\Playtests`.

### Acceptance Checks

- [ ] There is enough evidence to explain what is fun, confusing, slow, or broken.
- [ ] The next work queue is based on playtest observations, not only implementation completeness.
- [ ] iOS TestFlight work is resumed only if local play is coherent enough to benefit from device testing.
