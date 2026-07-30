# LTW Gameplay Development Checklist

## Purpose

The current build can launch a local Unity vertical slice, but it is not yet mature enough to justify mobile-device distribution work. This checklist forks the next phase toward game development: readable play, meaningful choices, pacing, content variety, feedback, and repeatable playtest evidence.

Resume iOS TestFlight work only after this fork produces a local desktop/Unity session that is worth testing on a device.

## Current Baseline

- The local Unity scene loads `Assets/Scenes/LocalVerticalSlice.unity` without current console errors.
- The simulation supports eight side-by-side 7x16 long north-south lanes, an explicit local seat, bots on the remaining lanes, placement with lane-ownership authority, sends (cooldown-free — the send cooldown was deliberately removed), selling, replay export, carousel creep handoff, and match summaries.
- Content roster: 15 towers in three build lines (ARCANE / FOUNDRY / GROVE) and 15 creeps in three send categories (CORE / RAPID / ELITE). Costs live only in `ContentCatalog`; the client reads them at display time.
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

- [x] Add or document a single local run command for building the simulation DLL and opening the Unity scene.
- [x] Confirm the human lane can place, send, sell, reset, and observe bots in Play Mode.
- [x] Capture one full local run with seed, match duration, winner, replay path, and Unity console status.
- [x] Record the most painful usability gaps found during the run.

Current evidence: the Unity batch playtest runner opens `Assets/Scenes/LocalVerticalSlice.unity`, starts Play Mode, runs the local three-player match, exports replay/report files, verifies reset cleanup, and writes repo evidence under `docs/playtest-evidence/`. The latest pass completed at tick 910 with P3 winning, 46 accepted replay commands, no critical Unity compile/runtime errors, and reset returning active presentation objects to zero. The run also exposed and fixed a match-end over-advance bug by making completed local matches ignore further `AdvanceOneTick` calls.

Local batch command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Voucher-Management\vouchermanagement\LTW\unity\LTW.UnityClient' -executeMethod LTW.UnityClient.Editor.LocalPlaytestBatchRunner.Run -logFile 'C:\Voucher-Management\vouchermanagement\LTW\unity-batch-playtest.log'
```

The batch runner also accepts local-match variants:

```powershell
-ltwEvidenceLabel seed-202-greedy-balanced -ltwSeed 202 -ltwP2 Greedy -ltwP3 Balanced -ltwP2Creep creep.brute -ltwP3Creep creep.swarm
```

### Acceptance Checks

- [x] A developer can reproduce the local run from a clean checkout without Xcode.
- [x] Play Mode can restart without stale match state or duplicate scene objects.
- [x] No critical Unity console errors appear during startup, match play, or reset.

## GD-01: Board Readability And Camera

### Deliverables

- [x] Frame the side-by-side 7x16 long north-south lane grid clearly for the current local Unity camera baseline.
- [x] Make own lane, target lanes, spawn, exit, towers, and creep paths visually distinct.
- [x] Add an obvious selected-lane or inspected-lane state.
- [x] Add placement preview feedback for legal, blocked, unaffordable, and path-blocking cells.

Placement preview now queries the simulation bridge before confirmation, so legal, occupied, unaffordable, invalid-lane, and path-blocking outcomes use the same rules as actual placement.
The empty local scene now creates the runtime tower palette, send dock, placement ghost, and feedback toast during bootstrap. The lanes render side by side with top spawn boxes, bottom life-loss boxes, distinct build bands, a brighter center route, spawn/leak gates, and lane ownership tinting. Unity-side creep rendering uses larger markers, slower local ticks, top-to-bottom projection, and interpolation so movement is easier to read.
The first art-upgrade pass also aligns board material bands, endpoint halos, gate trim, the default player-lane orthographic camera, runtime map/lane camera toggle, outside-lane gutters, corner anchors, gutter ownership badges, directional gutter tick marks, and lane-edge incoming pressure meters with danger caps to the side-by-side north-south lane layout so the LTW-style vertical flow stays readable.

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

The tower palette now exposes all 15 towers through a three-category picker (ARCANE/FOUNDRY/GROVE), plus selected-tower inspect and selected/last-tower selling for the local vertical slice. The screen-fit UI layout follows an arena-first frame: persistent match state hugs the top edge, primary actions stay in bottom corners, and secondary view controls sit on the right rail. The build palette and send dock now collapse into compact bottom-corner popout buttons with clearer costs, role labels, affordability/cooldown states, and mobile-safe spacing so the board stays visible during normal play. The tower UI now shares the upgraded role language with the board: role-shaped placement ghosts, stronger invalid-state tinting, selected-tower range rings, and a role-purpose inspect panel make the build/inspect flow easier to read. Upgrade remains intentionally out of scope until tower progression exists.

### Acceptance Checks

- [ ] A tester can complete ten placements and corrections without confusion or a blocking dialog.
- [ ] Invalid placements preserve gold and explain the failure in the HUD.
- [ ] Tower selection state is never ambiguous after a send, sell, reset, or lane swap.

## GD-03: Tower, Creep, And Send Content

### Deliverables

- [x] Add first-pass content for five tower roles: reliable single-target, area/control, relay utility, pulse burst, and prism long-range.
- [x] Expand to 15 towers in three build lines, surfaced through a category picker (2026-07-29).
- [x] Give the new towers special behaviour (2026-07-29). **All fifteen** now have one: Barricade's fixed up-lane arc, Foundry's delayed mortar, Tesla's chain arc, Repair Drone's Servicing (adjacent towers fire one tick faster — replaced an earlier range-buff version that measured as decoration, see `docs/GD_TUNING_LOG.md`), Elder Canopy's back-most targeting, Grovebond, Rot, Reaping Bloom and Bramble Hold, alongside the existing Pulse splash, Prism priority and Relay gold.
- [x] Generate wrapper prefabs and `TowerVisualLibrary` profiles for the ten new towers (2026-07-29). All fifteen render their own textured mesh.
- [x] Build the presentation for the mechanics (2026-07-29). Mortar arc and contracting impact telegraph, bramble zone decal, Grovebond ring scaled to the bonus, Barricade recoil, and Repair Drone's Servicing tether (a persistent line to the tower it's servicing).
- [ ] Chain Arc's hops are still invisible — it reuses the generic beam cue rather than drawing its own arc per hop.
- [x] Add first-pass content for five creep/send roles: runner, brute, swarm, shade, and siege.
- [x] Give each creep/send a different cost, income gain, and pressure profile.
- [x] Extend tests so new content validates through the existing simulation contracts.

Current sends have distinct cost, income, speed/health, and quantity pressure across the 15-creep roster. Tower art now has stronger role silhouettes for Arrow/focused, Control/area, Relay/utility, Pulse/burst, and Prism/long-range wards, including owner trim, role props, role-shaped placement previews, and selected-tower rings. Pulse now splashes nearby creeps, Prism prioritizes Shade/high-health pressure, Shade resists non-detection damage, and Siege leaks for extra life loss. The send cooldown was deliberately removed (see `docs/GD_TUNING_LOG.md`); sends are paced by cost and income alone now.

### Acceptance Checks

- [ ] Each tower is best at a different problem. All fifteen now have a distinct mechanic, and `TowerRosterTests.No_tower_is_strictly_dominated_by_another` rules out towers nobody would ever build. Still unticked because nothing has been PLAYED: distinctness on paper is not the same as each tower having a situation where it is the right buy.
- [ ] Each creep/send creates a different defensive response.
- [x] Content can be tuned without changing Unity presentation code. Tower costs, ranges, damage and cooldowns are read from `ContentCatalog`; the client holds no copy.

## GD-09: Category Upgrade Tiers

Design: `docs/CATEGORY_UPGRADE_TIERS_PLAN.md` (designed 2026-07-29, not implemented).

Three tiers for each of the six categories — tower lines ARCANE / FOUNDRY / GROVE and send categories
CORE / RAPID / ELITE. Tier 1 is free and default; tiers 2 and 3 are purchased at roughly 2.5x the
previous cost. Creeps scale on health (100 / 150 / 225%), towers on damage (100 / 140 / 190%).

**Correction (2026-07-30): the "closing mechanism" justification below no longer holds.** The P1
stalemate this section cites was a bug — a bot pressure check counted creeps that had already left
the lane, so bots stopped sending permanently — not a design gap. Fixed, the same seed completes at
tick 926. The tier structure (three independent tiers per category, escalating cost, one stat per
side) is still worth building on its own merits, but the 225%/190% calibration was chosen to let a
maxed attacker break a maxed defender's stalemate, a problem that no longer exists, and the
"two tier-3 bots still reach a result" ship gate below now passes trivially. Both need redoing
before implementation. Full detail in `docs/CATEGORY_UPGRADE_TIERS_PLAN.md`'s own revision note.

~~This is also the closing mechanism the game currently lacks~~ — see the correction above. Creep
scaling is set deliberately ahead of tower scaling at maximum investment so a fully-invested attacker
can break a fully-invested defence.

### Deliverables

- [ ] Per-player, per-category tier state on `PlayerEconomyState`, defaulting to tier 1, with the
      existing `With*` methods copying it through. **Six independent tracks** — upgrading one category
      never upgrades another, so this is six integers, not one per side.
- [ ] `BuyCategoryTierCommand` plus an `InvalidTier` rejection reason. Tiers must be bought in order.
- [ ] Creep health multiplier applied at SPAWN, so upgrading never retroactively heals creeps already
      walking.
- [ ] Tower damage multiplier applied at SHOT TIME, so it improves towers already standing. The seam is
      already prepared: apply it to `baseDamage` in `AttackWithTowers` and it reaches the primary hit, the
      mechanics and Pulse's splash in one place.
- [x] Prepare the damage seam so tiers cannot silently miss things (2026-07-29). `baseDamage` / `shotDamage`
      split, Pulse's splash moved onto `baseDamage`, and Grovebond and Crowd Bloom converted from flat
      bonuses to percentages of base so they do not decay as damage scales. All 159 tests unchanged.
- [x] Make the category pickers safe for an extra control (2026-07-29). Both now share
      `RuntimeUiChrome.CategoryCardHeight`, which derives card height from the panel.
- [ ] Tier controls on the existing category picker cards in both the build palette and the send dock,
      without reintroducing the fixed card height that overflowed the panel twice.
- [ ] Bots buy tiers. Without this the feature makes them strictly worse opponents than they are now.

### Acceptance Checks

- [ ] Tier 1 is free and default for all six categories.
- [ ] Skipping a tier is rejected, so the escalating cost is actually paid.
- [ ] A tier-3 attacker beats a tier-1 defender.
- [ ] ~~**Two tier-3 bots still reach a result.**~~ No longer a meaningful gate — the stalemate this
      guarded against was a bot-pressure bug, already fixed, so two tier-3 bots now reach a result
      regardless of this feature. Replace with a gate that still tests something: e.g. a tier-3
      attacker still beats a tier-3 defender in a comparable number of ticks to today's baseline,
      rather than merely "a match ends."
- [ ] All four existing measurement harnesses re-run at tier 3. Flat mechanic bonuses (Grovebond's
      `+1 per neighbour` especially) are worth proportionally less against scaled damage and may need
      to scale too.

## GD-04: Economy, Pacing, And Match Length

### Deliverables

- [x] Define target ranges for first send, first leak, first elimination, and match completion.
- [x] Tune starting gold, income interval, send rewards, bounties, lives, and cooldowns around those ranges.
- [x] Add scenario tests for low-pressure, normal-pressure, and heavy-pressure matches.
- [x] Record current known balance problems in a tuning log.

Initial target ranges and known balance questions are recorded in `docs/GD_TUNING_LOG.md`. The first pacing pass raises local lives to 220, delays bot send spending during the opening, and guards the deterministic local match against a 150-2000 tick completion target (widened from an earlier 900-1800 after a bot-pressure bug that stopped bots from ever sending again was fixed — see `docs/GD_TUNING_LOG.md`, "The Stalemate Was A Bug, Not Balance"). `tests/LTW.Tests/GameplayScenarioTests.cs` now covers low-pressure (stable opening defense), normal-pressure (income and active combat), and heavy-pressure (escalation without hidden bot advantages) scenarios; the full solution test suite passes under `dotnet test LTW.sln --configuration Release` (a specific count isn't quoted deliberately — it has drifted stale in prose before and will again).

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

Bot profiles now surface through the local diagnostics overlay and playtest report. Balanced and Defensive bots place first-pass defensive tower packages before creating send pressure, giving playtests visible opponent behavior without hidden advantages.

**Updated 2026-07-27/28** (`swarm-multibot-cluster`): bot behavior is now reactive rather than tick-scheduled. Any lane 2-8 can be independently bot-enabled or left empty. Tuning (aggression, defense bias, minimum gold reserve) lives in content data, not hardcoded constants. Towers keep building past any fixed count as long as gold above a reserve floor and an unused placement slot remain — a bot is no longer capped at "two early towers" or "three towers," it builds as much as it can actually afford. Sends are held until a profile's own minimum tower coverage is met (Balanced 3, Defensive 4, Greedy none) and while the bot's own lane is under heavy incoming pressure (scaled by its own tower count, so more defense raises its tolerance rather than leaving it stuck). Creep-tier preference now gates on accumulated income instead of elapsed ticks. Full rationale, including two real bugs this design caught, in `docs/GD_TUNING_LOG.md`.

### Acceptance Checks

- [x] Bots produce visible pressure without depending on hidden advantages. Demonstrated repeatedly across this session's Unity batch playtests (`docs/playtest-evidence/local-unity-batch-*`), including full 8-lane matches with a mix of profiles.
- [x] Bot matches vary by profile while remaining deterministic for a fixed seed. Now covered by an automated test: `VerticalSliceBridgeTests.Bot_decisions_are_deterministic_for_the_same_seed_and_options`.
- [x] A full local match can reach a winner through bot and human actions. Confirmed across every batch playtest run this session (e.g. `local-unity-batch-step3-reactive-bots-20260727-204354.md`, winner P6 at tick 680).

## GD-06: Session Flow And Results

### Deliverables

- [x] Add a lightweight start state instead of dropping directly into an unclear running match.
- [x] Add pause/resume and restart flow for local testing.
- [x] Improve post-match results with winner, duration, player economy/life state, and replay/report export path.
- [x] Ensure reset clears pooled presentation objects and HUD state.

The local session now starts in a ready state with a runtime start/pause/restart overlay. The results billboard shows winner, completion tick, and each player's final economy/life state. Replays and playtest reports can be exported from the local hotkeys. Pressing `P` shows a runtime toast when a Markdown playtest report is saved, and explains that the match must finish first if no completed replay exists yet. The batch evidence runner now verifies that reset returns the simulation to tick 0, clears creeps/towers/results, and leaves zero active presentation objects.

### Acceptance Checks

- [x] A tester can start, finish, review, and restart without leaving Play Mode.
- [x] Results match the simulation summary and replay diagnostics.
- [x] Reset does not leave ghost objects, stale selected towers, or old match text.

## GD-07: Game Feel And Feedback

### Deliverables

- [x] Add distinct feedback for tower build, tower shot, creep hit, creep death, leak, send, income tick, and elimination.
- [x] Keep effects readable under reduced-effects mode.
- [x] Add simple audio mix controls or global mute for desktop testing.
- [x] Review text scale and contrast in the HUD.

Tower build/sell events now frame the affected cell, creep spawn/death events add arrival and collapse cues, and tower attacks emit damage events so Unity can show role-specific focused beams, control pulses, relay signals, tower muzzle flashes, hit glints, and hit cues before kills. Send actions now add lane-to-lane pressure beams plus sender/defender pulses, leaks pulse the life-loss gate, income ticks glint across the lane, and elimination/victory events add lane-level shutdown/win cues. Reduced-effects mode now adds text-only fallback cues while skipping burst effects and beams, desktop hotkeys cover reduced effects/mute/volume, the runtime HUD surfaces lives/gold/income/send readiness, and HUD panels use compact high-contrast runtime styling. Tower and creep silhouettes now carry role-specific bases, halos, shadows, markers, starter tower silhouette props, and creep role props so Arrow, Control, Relay, Runner, Brute, Swarm, Boss, Air, Stealth, Siege, and Aura/Support pressures remain distinguishable without transient effects.

### Acceptance Checks

- [ ] The player notices important events without reading logs.
- [ ] Feedback improves understanding without hiding the grid.
- [ ] Reduced-effects mode remains functionally clear.

## GD-08: Playtest Evidence And Tuning Notes

### Deliverables

- [x] Run at least three local playtests using different seeds or bot profiles.
- [x] Record seed, duration, winner, first leak time, elimination time, replay path, and tester notes.
- [x] Prioritize fixes into must-fix, should-fix, and later buckets.
- [x] Decide whether the next fork should be more gameplay, local UX polish, or mobile validation.

The local playtest recorder writes Markdown reports with seed/content/map, completion tick, winner, first send/leak/elimination observations, replay path, bot profiles, recent bot decisions, and tester-note prompts. Reports are saved under Unity's persistent data path; on the current Windows editor setup this is `C:\Users\engch\AppData\LocalLow\DefaultCompany\LTW_UnityClient\Playtests`.

Current automated evidence:

- `local-unity-batch-20260713-055905.md`: default seed 1, P2 Balanced runner, P3 Defensive runner, completed tick 910, P3 won, report `playtest-910.md`.
- `local-unity-batch-seed-202-greedy-balanced-20260713-062229.md`: seed 202, P2 Greedy brute, P3 Balanced swarm, completed tick 396, P2 won, report `playtest-396.md`.
- `local-unity-batch-seed-303-defensive-greedy-20260713-062317.md`: seed 303, P2 Defensive swarm, P3 Greedy brute, completed tick 435, P3 won, report `playtest-435.md`.

Current prioritized fixes:

- Must-fix before device validation: complete one manual visual-readability pass while actively placing, sending, selling, resetting, and swapping map/lane view.
- Should-fix next: reduce accelerated-run presentation-effect pool growth or add a normal-speed stress capture so pooling evidence reflects realistic frame pacing.
- Later: resume iOS/TestFlight setup only after the manual pass confirms the local loop is coherent enough to benefit from device testing.

### Acceptance Checks

- [x] There is enough evidence to explain what is fun, confusing, slow, or broken.
- [x] The next work queue is based on playtest observations, not only implementation completeness.
- [x] iOS TestFlight work is resumed only if local play is coherent enough to benefit from device testing.
