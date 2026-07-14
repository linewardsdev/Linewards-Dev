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

## 2000-Baseline Graphics Gap

Reference analysis from early-2000s RTS/TD readability shows that the remaining gap is not primarily polygon count. The quality bar comes from a complete visual stack working together:

- physical board materials with tile wear, grime, cracks, edge contrast, route wear, and grounding shadows;
- authored low-poly assets with strong silhouettes, chunky proportions, and role-specific landmarks;
- hand-painted or stylized texture/value work that separates metal, stone, crystal, energy, trim, and team ownership without relying on labels;
- chunky, short-lived VFX for build, sell, shot, hit, kill, leak, send, income, transfer, and victory moments;
- animation identity for towers and creeps, including attack windup, impact timing, idle/motion loops, and death/arrival reactions;
- designed UI frames, icons, disabled/pressed states, and decision cards that feel like one system rather than debug panels;
- lighting, shadows, and camera framing that make gameplay objects sit in the lane instead of floating over flat primitives.

Current status:

- The build is still in "functional prototype with improving silhouettes," not "2000-baseline vertical slice."
- Primitive-generated tower/creep prefabs helped prove role language, but they should now be treated as placeholders and contract references, not final art.
- More primitive stacking will have diminishing returns. The next meaningful jump requires an asset pipeline shift: authored meshes, stylized textures/materials, icon art, VFX prefabs, and screenshot QA.
- The project should study early-2000s readability techniques, but must not copy Warcraft III names, faction motifs, UI chrome, icons, unit silhouettes, sounds, or screenshots.

Art production priority:

1. Board material pass first, because the lane surface, route, spawn/exit boxes, rails, shadows, and tile value structure will make every other object read better.
2. One authored tower asset second, starting with Arrow, to define the production quality bar and prefab replacement workflow.
3. Convert Control and Relay after Arrow proves the pipeline, then Pulse and Prism.
4. Convert Runner, Brute, and Swarm creeps after the first tower pipeline is stable, then Shade and Siege.
5. Add VFX and animation passes only after the base board/object readability survives heavy-pressure screenshots.

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

- [x] Push local `main` to cloud after final manual smoke test.
- [x] Confirm `origin/main` contains the three recent commits listed above.
- [x] Keep Unity-generated local version/package churn out of the commit unless the project intentionally changes Unity/package baseline.
- [x] Record the current Unity editor version mismatch risk: local editor is `6000.3.12f1`, repo baseline has recently shown `6000.5.3f1` metadata.

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

- [x] Define one owner for the bottom panel region at a time.
- [x] Hide or collapse the placement/tower descriptor while the send dock is expanded.
- [x] Hide or collapse the send dock while the full tower palette is expanded.
- [x] Ensure the builder placement descriptor still exposes the `ALL`/menu affordance when send dock is closed.
- [x] Ensure panel close behavior returns to the expected previous mode.
- [x] Verify touch targets remain large enough after cleanup.
- [ ] Capture fresh default, build menu, send menu, placement descriptor, and heavy combat screenshots.

Status note:

- Agent 1 added runtime ownership between `SendDockController` and `TouchPlacementController`: opening Send closes placement/selection/palette panels, while opening the build palette closes Send.

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
- [x] Runtime health bar metrics cover Runner, Brute, Swarm, Shade, Siege, and boss-style creeps.
- [x] Transfer arrivals have a distinct cue path from fresh sends.
- [x] Screenshot review exists at `docs/screenshot-reviews/creep-health-transfer-pass/review.md`.

Open tuning:

- [x] Tune per-role health bar size and offset so bars are readable without looking chunky under heavy pressure.
- [ ] Add a dedicated damaged-transfer capture state or manual screenshot showing the `TRANSFER` cue and reduced health in the same frame.
- [ ] Confirm health bars work for Runner, Brute, Swarm, Shade, and Siege, not only the most common pressure cases.
- [ ] Confirm health bars remain readable in grayscale.

Status note:

- Agent 3 added role-specific health bar metrics for Runner, Brute, Swarm, Shade, Siege, and boss-style creeps. The runtime now sizes and offsets bars by role instead of using one chunky global bar.

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
- [x] Runtime role-readability overlays exist for Runner, Brute, Swarm, Shade, and Siege.

Open role-readability tasks:

- [ ] Verify all five tower roles are readable at phone size without labels.
- [ ] Verify all five creep roles are readable at phone size without labels.
- [ ] Verify Runner remains readable in groups of 10+.
- [ ] Verify Swarm remains readable without becoming visual noise.
- [ ] Verify Brute remains distinct when mixed with Runner and Swarm.
- [ ] Verify Shade reads as echo/shimmer without relying on transparency alone.
- [ ] Verify Siege reads as directional pressure and is distinct from Brute.
- [ ] Verify reduced-effects mode keeps creep role readable through silhouette and motion.

Status note:

- Agent 3 added runtime role-readability overlays for prefab-backed and fallback creeps: Runner chevron/wake, Brute shoulder plates, Swarm value ring/lead spark, Shade solid echo rails, and Siege ram/warning plates. Phone-size screenshot verification is still required before closing the broader role-readability exit signal.

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

- [x] Runner darts.
- [x] Brute lumbers/bobs.
- [x] Swarm jitters as a cluster.
- [x] Shade flickers or leaves echo offsets.
- [x] Siege lumbers with weight and directionality.

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

- [x] Icon for each tower.
- [x] Icon for each creep/send.
- [x] Card frame treatment for build menu.
- [x] Card frame treatment for send menu.
- [x] Affordability/disabled states.
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

Status note:

- Agent 1 added build/send affordability states. Cards remain visible but dim when the player lacks gold, and disabled cards do not fire actions.

## Workstream K: 2000-Baseline Art Production Pipeline

Owner: Shared, with Agent 2 owning board/path readability validation

Goal: move from generated primitive placeholders to authored, original Line Wards production art while preserving phone-size gameplay clarity.

Board and lane material tasks:

- [ ] Create a board material pass for route tiles, build bands, spawn gate, life-loss gate, lane rails, and side gutters.
- [ ] Add route wear/value contrast so the creep path reads even under towers, creeps, and VFX.
- [ ] Add quiet tile variation, cracks, grime, and edge highlights without overpowering gameplay objects.
- [ ] Add grounding shadows or ambient-occlusion-style contact treatment for towers and creeps.
- [ ] Verify spawn and exit boxes are understandable without text.

Authored asset pipeline tasks:

- [ ] Define source-art folder and naming rules for authored meshes/textures that replace generated placeholders.
- [ ] Create the first authored Arrow tower mesh and texture as the quality-bar asset.
- [ ] Replace generated `Tower_Arrow.prefab` while preserving the stable prefab contract child paths.
- [ ] Capture Arrow in default, active combat, heavy pressure, and grayscale review frames.
- [ ] Document what worked before converting Control and Relay.

Texture/material tasks:

- [ ] Define stylized material language for stone, metal, crystal, energy, trim, health, and ownership.
- [ ] Establish palette/value rules that work in grayscale.
- [ ] Avoid one-hue board themes and keep the board visually quieter than towers, creeps, shots, and UI decisions.

VFX/animation preparation tasks:

- [ ] List required VFX prefabs for build, sell, shot, hit, kill, leak, send, income, transfer, and results.
- [ ] Define per-role tower attack motion targets: Arrow bolt, Control field pulse, Relay signal ping, Pulse shockwave, Prism beam charge.
- [ ] Define per-role creep motion targets: Runner dart, Brute lumber, Swarm jitter, Shade echo, Siege weighted pressure.

Exit signal:

- A screenshot of the authored board plus one authored tower looks like an intentional original low-poly tactics game, not primitives on a flat grid, while still passing mobile, grayscale, heavy-pressure, and reduced-effects readability checks.

## Workstream I: Gameplay Scenario Tests And Tuning Evidence

Owner: Agent 2

Goal: make balance and pacing changes repeatable.

Scenario tests:

- [x] Add low-pressure scenario test.
- [x] Add normal-pressure scenario test.
- [x] Add heavy-pressure scenario test.
- [x] Add mixed-pressure scenario test.
- [x] Record evidence for mixed send pressure.

Tuning checks:

- [ ] Each tower is best at a different problem.
- [ ] Each creep/send creates a different defensive response.
- [ ] Content can be tuned without changing Unity presentation code.
- [ ] A normal local match has visible escalation within the first few minutes.
- [ ] Defensive play and sending both have understandable value.
- [ ] Matches avoid both instant collapse and long no-progress stalls.
- [ ] Bots produce visible pressure without hidden advantages.

Current evidence:

- `GameplayScenarioTests` records deterministic low, normal, heavy, and mixed pressure evidence.
- Scenario output includes tick, accepted sends, multi-quantity sends, bot decisions, tower counts, active/damaged creep counts, total income/lives, damage events, and leak events.
- Full .NET suite currently passes at 69 tests.

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

Status note:

- Agent 1 attempted the full visual capture gate for this pass. Normal GUI launch exited before invoking the capture method, and `-batchmode -nographics` crashed inside Unity camera rendering before writing captures. This checklist remains open until a GUI/editor capture run can produce fresh pixels.

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

1. Keep cloud `main` synced and protect Unity/package local churn.
2. Finish the manual Unity smoke test for pathing, health persistence, reduced effects, and bottom-panel behavior.
3. Repair or complete the screenshot QA gate so every art/UI branch produces fresh pixels.
4. Start the board material/readability pass: route tiles, build bands, spawn/exit gates, rails, gutters, shadows, and tile variation.
5. Build one authored production-quality Arrow tower asset and use it to prove the replacement pipeline.
6. Capture heavy-pressure and grayscale evidence for the board plus authored Arrow asset.
7. Convert Control and Relay once Arrow proves the pipeline, then Pulse and Prism.
8. Convert Runner, Brute, and Swarm, then Shade and Siege.
9. Add role motion and VFX passes after base object readability is stable.
10. Continue UI icon/card polish in parallel, but keep it subordinate to board readability and placement-safe mobile framing.
11. Use `GameplayScenarioTests` for repeatable low, normal, heavy, and mixed pressure tuning evidence.

## Definition Of Done For This Phase

- [ ] Cloud `main` is current with local fixes.
- [ ] Bottom panel stacking is resolved.
- [ ] Creep pathing feels natural around center-lane towers.
- [ ] Wounded creeps remain wounded across lanes and the player can see it.
- [ ] All 10 current roster roles are readable at phone size.
- [ ] Board materials, route wear, spawn/exit gates, rails, and shadows make the lane feel authored rather than flat.
- [ ] At least one authored production-quality tower asset replaces its generated primitive placeholder.
- [ ] Heavy pressure remains readable without zooming.
- [ ] Reduced-effects mode remains gameplay-complete.
- [ ] Build/send UI supports fast decisions without covering the board.
- [ ] Scenario tests cover low, normal, heavy, and mixed pressure.
- [ ] Screenshot review passes or has only low-severity polish items.
- [ ] The style feels original to Line Wards.
