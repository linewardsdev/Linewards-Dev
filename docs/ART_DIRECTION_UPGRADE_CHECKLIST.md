# Art Direction Upgrade Checklist

## Purpose

This checklist turns the Line Wards graphics direction into an upgrade plan for moving beyond primitive placeholders while preserving LTW-style competitive readability.

Use it after the GD readability baseline is stable. The goal is original ward-tech fantasy art that keeps the long north-south lanes, tower roles, creep pressure, economy timing, and leak moments readable on mobile.

## Upgrade Principles

- [ ] Preserve the long, skinny north-south lane as the dominant visual shape.
- [ ] Improve role readability before adding decorative detail.
- [ ] Use original Line Wards ward-tech fantasy motifs, not Warcraft III silhouettes, assets, names, UI chrome, sounds, or faction language.
- [ ] Keep every art upgrade testable in Play Mode under real creep density.
- [ ] Make reduced-effects mode a first-class visual target, not an afterthought.
- [ ] Pair color with shape, height, motion, label, or iconography so gameplay does not depend on color alone.
- [ ] Prefer small complete vertical-slice asset sets over one polished object surrounded by placeholders.

## Phase A: Board And Lane Art

### Lane Composition

- [ ] Define final cell proportions for the 7x18 vertical-slice lane and expected future 8x20 option.
- [x] Create a clean base-board material set: deep field, buildable cells, center route, border rails, spawn, exit, and lane accents.
- [x] Add distinct buildable side bands that remain visible without overpowering towers.
- [x] Make the center creep route brighter and more continuous than build zones.
- [x] Add subtle lane ownership tinting for player lane and opponent lanes.
- [x] Add spawn and exit gates with clear vertical flow direction.
- [ ] Add first-pass lane background/environment trim outside the grid without adding clutter inside the playable cells.
- [ ] Verify spawn, exit, build zones, occupied cells, and path cells are readable at normal phone scale.

### Placement States

- [ ] Design visual states for legal placement, occupied cell, insufficient gold, outside lane, and path blocked.
- [ ] Add an optional path-preservation preview when a placement would block the route.
- [ ] Define selected-tower highlight treatment that differs from placement ghost treatment.
- [ ] Ensure invalid placement uses shape/pulse/label as well as danger color.
- [ ] Verify placement UI and ghost do not hide the selected grid cell.

### Camera And Framing

- [ ] Define desktop Play Mode camera target framing for all three lanes.
- [ ] Define portrait mobile framing for the active player lane.
- [ ] Add camera-safe margins for HUD, send dock, placement palette, and results overlay.
- [ ] Verify tower selection and placement accuracy with the chosen camera angle.
- [ ] Create screenshot references for ready, mid-combat, heavy pressure, and match complete states.

## Phase B: Tower Art

### Role Families

- [ ] Create concept sheet for the three starter tower roles: Arrow, Control, Relay.
- [ ] Define role silhouettes independent of color:
  - [ ] Arrow: tall, narrow, focused emitter.
  - [ ] Control: wider, flatter, ring or dish emitter.
  - [ ] Relay: mast, capacitor, core, or support beacon.
- [ ] Define footprint/base treatment for ownership and occupied-cell clarity.
- [ ] Define attack/readiness state for each tower.
- [ ] Define disabled/unaffordable/preview treatment for each tower in the palette.

### Upgrade Language

- [ ] Define upgrade tier visual rules: height, core size, ring count, attachments, light intensity, or animated elements.
- [ ] Define tech-family visual rules without copying Warcraft factions or spell schools.
- [ ] Add upgrade readability rule: upgraded tower must look like a stronger descendant of its base tower.
- [ ] Add downgrade/sell transition visual, if needed.
- [ ] Create placeholders for locked upgrades or future disabled upgrade hooks.

### Production Assets

- [ ] Replace primitive Arrow tower with original production or polished prototype asset.
- [ ] Replace primitive Control tower with original production or polished prototype asset.
- [ ] Replace primitive Relay tower with original production or polished prototype asset.
- [ ] Add LOD or simplified versions for reduced-effects/low-spec presentation.
- [ ] Confirm tower assets are readable against build bands, center path, and creep colors.

## Phase C: Creep Art

### Starter Creep Roles

- [ ] Create concept sheet for Runner, Brute, and Swarm.
- [ ] Define role silhouettes independent of color:
  - [ ] Runner: small, sharp, forward-pointing, fast read.
  - [ ] Brute: heavy, wide, armored, slow read.
  - [ ] Swarm: multiple tiny bodies or clustered repeated units.
- [ ] Define movement timing for each role.
- [ ] Define readable ground shadows for each role.
- [ ] Define hit, death, leak, and spawn state treatments.

### Advanced Pressure Library

- [ ] Define Boss silhouette and scale language.
- [ ] Define Flying/Air hover and shadow language.
- [ ] Define Invisible/Stealth shimmer/reveal language.
- [ ] Define Attacker/Siege windup and tower-threat language.
- [ ] Define Aura/Support ring or field language.
- [ ] Define anti-maze or path-breaking pressure visuals if that mechanic is added.

### Density Validation

- [ ] Verify Runner remains readable in groups of 10+.
- [ ] Verify Swarm remains readable without becoming visual noise.
- [ ] Verify Brute remains distinct when mixed with Runner/Swarm.
- [ ] Verify creeps remain visible behind towers and effects.
- [ ] Verify reduced-effects mode keeps creep type readable through silhouette and motion.

## Phase D: Combat And Economy Feedback

### Combat Events

- [ ] Define tower build effect.
- [ ] Define tower sell/refund effect.
- [ ] Define tower shot effect per tower role.
- [ ] Define creep hit reaction.
- [ ] Define creep death/bounty effect.
- [ ] Define leak/life-loss effect.
- [ ] Define player elimination effect.
- [ ] Define match victory/defeat effect.

### Economy And Timing

- [ ] Design income tick pulse and gold gain treatment.
- [ ] Design send queued feedback from sender to target lane.
- [ ] Design send cooldown ready/not-ready states.
- [ ] Design near-income-tick urgency state.
- [ ] Ensure economy feedback does not obscure combat or placement cells.

### Reduced Effects

- [ ] Define reduced-effects replacement cues for every functional event.
- [ ] Verify reduced-effects mode communicates build, send, hit, kill, leak, income, elimination, and match end.
- [ ] Add reduced-effects screenshot/video capture to QA checklist.
- [ ] Confirm haptics/audio are not the only way to notice critical events.

## Phase E: HUD And Mobile UI Art

### Persistent HUD

- [ ] Design compact status strip for lives, gold, income, and income timer.
- [ ] Make income timer more prominent near tick.
- [ ] Design lane identity treatment for player lane and opponent lanes.
- [ ] Design incoming pressure indicator.
- [ ] Design match state overlay for ready, paused, running, complete.
- [ ] Confirm HUD does not cover active placement area.

### Tower And Send Controls

- [ ] Replace placeholder tower palette with role cards that show cost, role, affordability, and selected state.
- [ ] Design send dock cards for Runner, Brute, Swarm with cost, income gain, quantity, and cooldown.
- [ ] Design disabled/unaffordable states for tower and send controls.
- [ ] Design selected tower panel with role, position, sell, and future upgrade slot.
- [ ] Ensure all touch targets meet mobile sizing expectations.

### Results And Playtest UI

- [ ] Design match results visual hierarchy: winner, duration, lives, income, gold, replay/report path.
- [ ] Design local diagnostics overlay style for development without obscuring the board.
- [ ] Design playtest report prompt or capture confirmation.
- [ ] Add screenshots for ready, paused, selected tower, invalid placement, heavy combat, and results.

## Phase F: Asset Pipeline And Governance

### Naming And Storage

- [ ] Define asset naming convention by gameplay role and tier.
- [ ] Create folders for board, towers, creeps, effects, UI, icons, and concepts.
- [ ] Track source files separately from exported runtime assets.
- [ ] Include license/source notes for every non-code asset.
- [ ] Keep generated concept art clearly labeled as concept/reference unless production rights are established.

### Technical Constraints

- [ ] Define target texture sizes for mobile.
- [ ] Define mesh/poly budget or sprite atlas budget for each asset type.
- [ ] Define material count limits for towers, creeps, board, and UI.
- [ ] Define particle/effect budget for heavy-send scenarios.
- [ ] Verify pooled presentation objects work with upgraded assets.
- [ ] Verify low-spec/reduced presentation modes still render correctly.

### Legal Boundary

- [ ] Audit every concept and asset for Warcraft/Blizzard visual similarity risk.
- [ ] Avoid copied RTS command-card chrome, minimap styling, race motifs, unit silhouettes, and icon compositions.
- [ ] Rename any placeholder content that drifts toward protected names or faction language.
- [ ] Keep homage at the level of game structure and readability, not asset imitation.

## Phase G: Visual QA Gates

### Per-Asset Gate

- [ ] Asset has an assigned gameplay role.
- [ ] Asset reads correctly in top-down/isometric gameplay camera.
- [ ] Asset is distinguishable by silhouette without color.
- [ ] Asset works on the dark board material.
- [ ] Asset works in reduced-effects mode.
- [ ] Asset does not obscure path, placement, or leaks.
- [ ] Asset has source/license notes.

### Per-Branch Gate

- [ ] Capture before/after screenshots.
- [ ] Test normal and reduced-effects presentation.
- [ ] Test at least one heavy-send or dense-creep scenario.
- [ ] Check desktop and mobile-ish aspect ratios.
- [ ] Confirm UI does not cover placement-critical cells.
- [ ] Update this checklist or GD checklist with completed scope.
- [ ] Record any new readability risks for tuning/testing agents.

## Suggested Art Upgrade Branch Order

1. [ ] \`art-board-materials\`: replace primitive lane skin with polished board/cell/gate materials.
2. [ ] \`art-tower-starter-set\`: replace Arrow, Control, Relay placeholders with original role-readable assets.
3. [ ] \`art-creep-starter-set\`: replace Runner, Brute, Swarm placeholders with original role-readable assets.
4. [ ] \`art-combat-feedback-set\`: polish build, shot, hit, kill, leak, income, and send feedback.
5. [ ] \`art-mobile-hud-set\`: polish status strip, tower palette, send dock, selected tower panel, and results.
6. [ ] \`art-heavy-pressure-qa\`: validate readability under dense creeps, reduced effects, and mobile framing.
7. [ ] \`art-tech-upgrade-language\`: define and prototype tower upgrade/tech family visuals once gameplay supports them.

## Done Signal

The art direction upgrade phase is ready to move from prototype polish to production art when:

- [ ] A first-time tester can identify lane flow, spawn, exit, ownership, tower roles, and creep roles without explanation.
- [ ] Heavy pressure remains readable without zooming.
- [ ] Reduced-effects mode remains gameplay-complete.
- [ ] Starter towers and creeps have original, role-specific visual language.
- [ ] HUD supports fast mobile decisions without hiding the board.
- [ ] Asset naming, source tracking, and legal boundaries are documented and followed.
