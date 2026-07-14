# Next Phase Improvement Plan

## Purpose

This document consolidates the active next-phase work from:

- `GRAPHICS_2000_BASELINE_ROADMAP.md`
- `CREEP_GRAPHICS_ART_TRACKING.md`
- `GAMEPLAY_DEVELOPMENT_CHECKLIST.md`

Use this as the working tracker for the next implementation phase. The source documents remain useful for history and deeper context, but this file should be the primary checklist for near-term execution.

## Current Baseline

The current local build has a playable Unity vertical slice with:

- three 7x18 north-south lanes;
- one human player and two bot opponents;
- five tower roles: Arrow, Control, Relay, Pulse, Prism;
- five creep roles: Runner, Brute, Swarm, Shade, Siege;
- runtime tower and creep presentation libraries with primitive fallbacks;
- build, send, sell, pause, restart, lane switching, match results, and replay/report support;
- presentation cues for build, sell, send, spawn, hit, kill, leak, income, elimination, and victory;
- reduced-effects support;
- automated visual capture tooling;
- current .NET test suite passing at 65 tests.

Recent local commits not yet pushed at the time this plan was written:

- `6a348e9 Preserve creep health across lane transfers`
- `8b6265e Add creep health readability cues`
- `6e2e020 Prefer forward creep pathing around towers`

## Non-Negotiables

- Preserve phone-size readability before adding visual detail.
- Preserve the long, skinny north-south lane as the dominant visual shape.
- Keep board surfaces quieter than towers, creeps, projectiles, leak cues, and HUD decisions.
- Use original Line Wards ward-tech fantasy language: signal fragments, prism bodies, rune plates, glass cores, pulse fields, arcane board-game clarity.
- Do not copy Warcraft III names, assets, silhouettes, UI chrome, icons, sounds, factions, or screenshots.
- Every visual upgrade must pass screenshot review under normal pressure, heavy pressure, and reduced effects.
- Simulation rules must stay independent from Unity presentation code.

## Phase Goal

Move from "playable prototype with improving art" to "coherent 2000-baseline vertical slice."

The target is not modern AAA polish. The target is a readable, original, low-poly tactical board game where a first-time tester can understand lane flow, tower roles, creep pressure, health, placement, sends, and major combat events without explanation.

## Agent Ownership

| Agent | Assigned Workstreams | Focus |
| --- | --- | --- |
| Agent 1 | A, C, H, J | Cloud sync, bottom-panel UI cleanup, UI/icons, screenshot QA |
| Agent 2 | B, E, I | Manual Unity smoke testing, board/path readability, gameplay scenarios |
| Agent 3 | D, F, G | Creep health/transfer tuning, role readability, motion/VFX |

Ownership is intentionally balanced by count and by dependency shape. Agent 1 keeps the integration/UI/screenshot gate work together. Agent 2 owns test/play/readability validation. Agent 3 owns creep/tower visual readability and feedback polish.

## Workstream A: Cloud Sync And Baseline Lock

Owner: Agent 1

Goal: make sure current correctness and presentation fixes are safely shared before new work piles on.

- [ ] Push local `main` to cloud after final manual smoke test.
- [ ] Confirm `origin/main` contains the three recent commits listed above.
- [ ] Keep Unity-generated local version/package churn out of the commit unless the project intentionally changes Unity/package baseline.
- [ ] Record the current Unity editor version mismatch risk: local editor is `6000.3.12f1`, repo baseline has recently shown `6000.5.3f1` metadata.

Exit signal:

- Cloud `main` contains the creep health persistence fix, health readability cues, and forward-preferred pathing.

## Workstream B: Manual Unity Smoke Test

Owner: Agent 2

Goal: validate that the latest fixes feel correct in Play Mode before broader art/UI work.

- [ ] Place towers near and on the center route and confirm creeps prefer forward progress before lateral detours.
- [ ] Confirm center-route towers still create legal detours when they do not fully block the path.
- [ ] Confirm invalid path-blocking placements are rejected and preserve gold.
- [ ] Damage a creep before it leaks and confirm the next-lane creep keeps reduced health.
- [ ] Confirm health bars are readable in normal combat.
- [ ] Confirm health bars remain readable in heavy pressure.
- [ ] Confirm health bars do not make clustered creeps unreadable.
- [ ] Confirm Send Pressure and builder/tower descriptor panels do not stack in a way that blocks the active placement area.
- [ ] Confirm reduced-effects mode still communicates send, hit, kill, leak, income, and transfer cues.

Exit signal:

- A tester can complete placement, send, sell, reset, and lane-view checks without a blocking UI or pathing confusion.

## Workstream C: Bottom Panel And Mobile HUD Cleanup

Owner: Agent 1

Goal: remove the most visible current UI regression: overlapping bottom panels.

Observed issue:

- When Send Pressure is open, the bottom builder/tower descriptor can remain visible underneath it. This stacks `SEND PRESSURE`, tower descriptor text, and close buttons in the same phone area.

Tasks:

- [ ] Define one owner for the bottom panel region at a time.
- [ ] Hide or collapse the placement/tower descriptor while the send dock is expanded.
- [ ] Hide or collapse the send dock while the full tower palette is expanded.
- [ ] Ensure the builder placement descriptor still exposes the `ALL`/menu affordance when send dock is closed.
- [ ] Ensure panel close behavior returns to the expected previous mode.
- [ ] Verify touch targets remain large enough after cleanup.
- [ ] Capture fresh default, build menu, send menu, placement descriptor, and heavy combat screenshots.

Exit signal:

- No active bottom UI panel visually overlaps another active bottom UI panel in phone framing.

## Workstream D: Creep Health And Transfer Readability

Owner: Agent 3

Goal: make health persistence trustworthy to the player.

Completed:

- [x] Simulation preserves creep health across lane transfers.
- [x] Regression test covers wounded creep entering the next lane.
- [x] Runtime creep health bars exist.
- [x] Damaged creeps have persistent visual cues through health fill, tinting, hit flash, and wound pip.
- [x] Transfer arrivals have a distinct cue path from fresh sends.
- [x] Screenshot review exists at `docs/screenshot-reviews/creep-health-transfer-pass/review.md`.

Open tuning:

- [ ] Tune per-role health bar size and offset so bars are readable without looking chunky under heavy pressure.
- [ ] Add a dedicated damaged-transfer capture state or manual screenshot showing the `TRANSFER` cue and reduced health in the same frame.
- [ ] Confirm health bars work for Runner, Brute, Swarm, Shade, and Siege, not only the most common pressure cases.
- [ ] Confirm health bars remain readable in grayscale.

Exit signal:

- A tester can tell that a wounded creep remains wounded after entering another lane.

## Workstream E: Board Readability And Path Clarity

Owner: Agent 2

Goal: keep the core lane and path behavior readable under real tower formations.

Completed:

- [x] Long north-south lane framing exists.
- [x] Spawn, life-loss, build bands, center route, lane rails, lane ownership, and pressure indicators exist.
- [x] Pathing now prefers forward movement before right detours.

Tasks:

- [ ] Confirm a first-time tester can identify where enemies enter and where they exit.
- [ ] Confirm tower placement does not visually hide the active path or selected cell.
- [ ] Confirm tower range and creep movement are readable with several active creeps.
- [ ] Add or improve path-preservation preview for placements that would block the route.
- [ ] Capture ready, mid-combat, heavy pressure, invalid placement, selected tower, and results references.

Exit signal:

- Lane flow, tower placement, and creep route changes are understandable without reading logs.

## Workstream F: Tower, Creep, And Send Role Readability

Owner: Agent 3

Goal: make the 5x2 roster readable by shape, motion, and role behavior, not only labels.

Current tower status:

- [x] Arrow, Control, Relay, Pulse, and Prism have role silhouettes and runtime visual language.
- [x] Tower visual library and prefab pipeline exist.

Current creep status:

- [x] Runner, Brute, Swarm, Shade, and Siege have prefab/profile support.
- [x] Generated placeholder prefabs and material/profile wiring exist.
- [x] Creep visual library validator exists.

Open role-readability tasks:

- [ ] Verify all five tower roles are readable at phone size without labels.
- [ ] Verify all five creep roles are readable at phone size without labels.
- [ ] Verify Runner remains readable in groups of 10+.
- [ ] Verify Swarm remains readable without becoming visual noise.
- [ ] Verify Brute remains distinct when mixed with Runner and Swarm.
- [ ] Verify Shade reads as echo/shimmer without relying on transparency alone.
- [ ] Verify Siege reads as directional pressure and is distinct from Brute.
- [ ] Verify reduced-effects mode keeps creep role readable through silhouette and motion.

Exit signal:

- A first-time tester can identify tower and creep roles from gameplay-scale screenshots.

## Workstream G: Role Motion And Combat Feedback

Owner: Agent 3

Goal: make the game feel alive while preserving clarity under pressure.

Tower motion tasks:

- [ ] Arrow tracks/fires with a crisp bolt or beam.
- [ ] Control pulses a field/ring.
- [ ] Relay sends a signal ping.
- [ ] Pulse expands a short shockwave.
- [ ] Prism charges and releases a focused beam.

Creep motion tasks:

- [ ] Runner darts.
- [ ] Brute lumbers/bobs.
- [ ] Swarm jitters as a cluster.
- [ ] Shade flickers or leaves echo offsets.
- [ ] Siege lumbers with weight and directionality.

Combat/economy feedback tasks:

- [ ] Tune leak/life-loss effect.
- [ ] Tune send/arrival effect.
- [ ] Tune income tick/economy pulse.
- [ ] Add or strengthen Pulse splash cue.
- [ ] Add or strengthen Prism priority-hit cue.
- [ ] Add or strengthen Shade resistance/reveal cue.
- [ ] Add or strengthen Siege warning/leak cue.
- [ ] Confirm haptics/audio are not the only way to notice critical events.

Exit signal:

- A tester notices important events without reading logs, and feedback improves understanding without hiding the grid.

## Workstream H: UI Art, Icons, And Decision Speed

Owner: Agent 1

Goal: make the HUD feel designed while preserving compact mobile decisions.

Build/send UI tasks:

- [ ] Icon for each tower.
- [ ] Icon for each creep/send.
- [ ] Card frame treatment for build menu.
- [ ] Card frame treatment for send menu.
- [ ] Affordability/disabled states.
- [ ] Selected/pressed states.
- [ ] Compact detail strip or role hint if needed.
- [ ] Ensure no third-line microcopy returns to build/send cards.

Match UI tasks:

- [ ] Top stats strip polish.
- [ ] Selected tower panel polish.
- [ ] Results modal polish.
- [ ] Role glyph language for tower/creep families.
- [ ] Incoming pressure indicator polish.
- [ ] Income timer emphasis near tick.

Exit signal:

- Build/send decisions are fast, readable, and do not hide placement-critical cells.

## Workstream I: Gameplay Scenario Tests And Tuning Evidence

Owner: Agent 2

Goal: make balance and pacing changes repeatable.

Scenario tests:

- [ ] Add low-pressure scenario test.
- [ ] Add normal-pressure scenario test.
- [ ] Add heavy-pressure scenario test.
- [ ] Add mixed-pressure scenario test.
- [ ] Record evidence for mixed send pressure.

Tuning checks:

- [ ] Each tower is best at a different problem.
- [ ] Each creep/send creates a different defensive response.
- [ ] Content can be tuned without changing Unity presentation code.
- [ ] A normal local match has visible escalation within the first few minutes.
- [ ] Defensive play and sending both have understandable value.
- [ ] Matches avoid both instant collapse and long no-progress stalls.
- [ ] Bots produce visible pressure without hidden advantages.

Exit signal:

- Balance changes can be evaluated through deterministic scenarios and playtest evidence.

## Workstream J: Screenshot QA Gate

Owner: Agent 1

Goal: every meaningful graphics/UI branch produces visible evidence.

Required capture set:

- [ ] `01-default-hud.png`
- [ ] `02-build-menu-open.png`
- [ ] `03-send-menu-open.png`
- [ ] `04-lane-selector-open.png`
- [ ] `05-active-combat.png`
- [ ] `06-heavy-pressure.png`
- [ ] `07-reduced-effects-heavy.png`
- [ ] `08-results-or-late-match.png`

Review dimensions:

- [ ] Mobile readability.
- [ ] Role silhouette clarity.
- [ ] Board/path clarity.
- [ ] UI overlap safety.
- [ ] Heavy pressure readability.
- [ ] Reduced-effects readability.
- [ ] Grayscale/value readability.
- [ ] Originality and no protected visual-language drift.

Exit signal:

- Screenshot review is `Pass`, or `Needs Review` with only low-severity polish issues.

## Recommended Execution Order

1. Bottom panel cleanup.
2. Manual Unity smoke test for pathing, health persistence, and panel behavior.
3. Push the current local commits plus UI cleanup to cloud.
4. Tune creep health bar sizing and add damaged-transfer capture coverage.
5. Run a role-readability pass for all five creeps and five towers.
6. Add scenario tests for low, normal, heavy, and mixed pressure.
7. Start UI icon/card polish once gameplay panels stop overlapping.
8. Start final polished art replacement only after role silhouettes pass heavy-pressure screenshot review.

## Definition Of Done For This Phase

- [ ] Cloud `main` is current with local fixes.
- [ ] Bottom panel stacking is resolved.
- [ ] Creep pathing feels natural around center-lane towers.
- [ ] Wounded creeps remain wounded across lanes and the player can see it.
- [ ] All 10 current roster roles are readable at phone size.
- [ ] Heavy pressure remains readable without zooming.
- [ ] Reduced-effects mode remains gameplay-complete.
- [ ] Build/send UI supports fast decisions without covering the board.
- [ ] Scenario tests cover low, normal, heavy, and mixed pressure.
- [ ] Screenshot review passes or has only low-severity polish items.
- [ ] The style feels original to Line Wards.
