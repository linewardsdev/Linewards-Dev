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

- [ ] Define final cell proportions for the 7x18 vertical-slice lane and expected future 8x20 option.
- [ ] Create a clean base-board material set: deep field, buildable cells, center route, border rails, spawn, exit, and lane accents.
- [ ] Add distinct buildable side bands that remain visible without overpowering towers.
- [ ] Make the center creep route brighter and more continuous than build zones.
- [ ] Add subtle lane ownership tinting for player lane and opponent lanes.
- [ ] Add spawn and exit gates with clear vertical flow direction.
- [ ] Add first-pass lane background/environment trim outside the grid without adding clutter inside the playable cells.
- [ ] Verify spawn, exit, build zones, occupied cells, and path cells are readable at normal phone scale.

## Phase B: Tower Art

- [ ] Create concept sheet for the three starter tower roles: Arrow, Control, Relay.
- [ ] Define Arrow as tall, narrow, focused-emitter silhouette.
- [ ] Define Control as wider, flatter, ring/dish silhouette.
- [ ] Define Relay as mast, capacitor, core, or support beacon silhouette.
- [ ] Define upgrade tier rules: height, core size, ring count, attachments, light intensity, or animated elements.
- [ ] Replace primitive Arrow, Control, and Relay towers with original role-readable assets.
- [ ] Confirm tower assets are readable against build bands, center path, and creep colors.

## Phase C: Creep Art

- [ ] Create concept sheet for Runner, Brute, and Swarm.
- [ ] Define Runner as small, sharp, forward-pointing, fast read.
- [ ] Define Brute as heavy, wide, armored, slow read.
- [ ] Define Swarm as multiple tiny bodies or clustered repeated units.
- [ ] Define Boss, Flying/Air, Invisible/Stealth, Attacker/Siege, Aura/Support, and anti-maze visual language.
- [ ] Verify creep roles stay readable in heavy-density and reduced-effects scenarios.

## Phase D: Combat And Economy Feedback

- [ ] Define tower build, sell, shot, creep hit, death, leak, income, elimination, and match-end effects.
- [ ] Design send queued feedback from sender to target lane.
- [ ] Design near-income-tick urgency state.
- [ ] Ensure economy feedback does not obscure combat or placement cells.
- [ ] Define reduced-effects replacement cues for every functional event.

## Phase E: HUD And Mobile UI Art

- [ ] Design compact status strip for lives, gold, income, and income timer.
- [ ] Design lane identity treatment for player lane and opponent lanes.
- [ ] Design incoming pressure indicator.
- [ ] Replace placeholder tower palette with role cards that show cost, role, affordability, and selected state.
- [ ] Design send dock cards for Runner, Brute, Swarm with cost, income gain, quantity, and cooldown.
- [ ] Design selected tower panel with role, position, sell, and future upgrade slot.
- [ ] Confirm HUD does not cover active placement area.

## Phase F: Asset Pipeline And Governance

- [ ] Define asset naming convention by gameplay role and tier.
- [ ] Create folders for board, towers, creeps, effects, UI, icons, and concepts.
- [ ] Track source files separately from exported runtime assets.
- [ ] Include license/source notes for every non-code asset.
- [ ] Define target texture sizes, mesh/poly budgets, material limits, and particle/effect budgets for mobile.
- [ ] Verify pooled presentation objects work with upgraded assets.
- [ ] Audit every concept and asset for Warcraft/Blizzard visual similarity risk.

## Phase G: Visual QA Gates

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

- [ ] A first-time tester can identify lane flow, spawn, exit, ownership, tower roles, and creep roles without explanation.
- [ ] Heavy pressure remains readable without zooming.
- [ ] Reduced-effects mode remains gameplay-complete.
- [ ] Starter towers and creeps have original, role-specific visual language.
- [ ] HUD supports fast mobile decisions without hiding the board.
- [ ] Asset naming, source tracking, and legal boundaries are documented and followed.
