# Graphics 2000 Baseline Roadmap

## Purpose

Move Line Wards from the current primitive/debug-art look toward a polished early-2000s readable strategy-game baseline.

The target is not modern AAA fidelity. The target is a cohesive, original, mobile-readable ward-tech fantasy style with authored low-poly silhouettes, simple materials, readable animation, and satisfying combat feedback.

Working shorthand:

- Current feel: "1980s" debug primitives.
- Target feel: "2000" polished prototype.

This roadmap should guide graphics branches after the 5x2 roster baseline.

## Non-Negotiables

- Preserve mobile readability before visual detail.
- Keep the long north-south lane flow readable at phone size.
- Keep board surfaces quieter than towers, creeps, projectiles, and leak feedback.
- Use original Line Wards ward-tech fantasy: signal fragments, prism bodies, rune plates, glass cores, pulse fields, arcane board-game clarity.
- Do not copy Warcraft III names, assets, silhouettes, UI chrome, icons, sounds, factions, or screenshots.
- Every graphics upgrade must pass screenshot review under normal and heavy pressure.

## Target Quality Bar

The game should read as a cohesive low-poly tactical board game:

- authored tower and creep silhouettes instead of cubes/cylinders as the main visual identity;
- limited but intentional material palette;
- clear role colors and value separation;
- small role-specific animations;
- simple VFX for attacks, hits, deaths, sends, leaks, and economy;
- UI cards/icons that feel designed without becoming crowded.

## Stage 1: Authored Low-Poly Prefab Foundation

Goal: stop looking like debug geometry.

Replace primitive-first visuals with simple authored prefabs while keeping primitive fallbacks available.

### Tower Prefabs

- [x] Arrow Ward: tall focused emitter / firing spine.
- [x] Control Ward: wide ring, dish, or field controller.
- [x] Relay Ward: mast, capacitor, support beacon, or signal core.
- [x] Pulse Ward: compact burst core with expanding ring language.
- [x] Prism Ward: tall crystal lens-spire for long-range focus.

### Creep Prefabs

- [x] Runner: dart shard / ward-spark.
- [x] Brute: armored pressure core.
- [x] Swarm: clustered shardlings/signal mites.
- [x] Shade: shimmer body with echo/afterimage structure.
- [x] Siege: heavy directional ram/core.

### Implementation Tasks

- [x] Confirm prefab contract for towers, matching or extending `ART_PREFAB_CONTRACT.md`.
- [x] Confirm prefab contract for creeps, matching current `CreepVisualLibrary`.
- [x] Add or update tower visual library if needed.
- [x] Extend creep prefab generator/library to include Shade and Siege.
- [x] Keep runtime fallback geometry for missing assets.
- [x] Add import/readme notes for every tower and creep role folder.
- [ ] Capture before/after screenshots.

### Exit Signal

- All 10 roles have authored placeholder/polished-prototype prefabs.
- A tester can distinguish all tower and creep roles without labels in active combat screenshots.

## Stage 2: Material And Lighting Language

Goal: make the world feel intentional.

### Material Kit

- [x] Dark slate board material.
- [x] Slightly brighter route material.
- [x] Muted build-zone material.
- [x] Blue/mint ward energy material.
- [x] Gold economy/relay material.
- [x] Violet control material.
- [x] Warm pulse material.
- [x] Pale prism material.
- [x] Red leak/danger material.
- [x] Shade shimmer/transparent material.
- [x] Creep shadow material.

### Lighting/Post

- [ ] Tune orthographic-friendly shadows.
- [ ] Add subtle bloom only for energy accents.
- [x] Avoid noisy textures inside playable cells.
- [x] Preserve contrast between board, towers, creeps, HUD, and effects.
- [x] Verify reduced-effects mode still reads.

### Exit Signal

- Screenshots look cohesive even before final VFX.
- Board no longer looks like flat debug cells, but gameplay objects still dominate attention.

## Stage 3: Role-Specific Animation

Goal: make units feel alive without adding clutter.

### Tower Motion

- [ ] Arrow tracks/fires with a crisp bolt or beam.
- [ ] Control pulses a field/ring.
- [ ] Relay sends a signal ping.
- [ ] Pulse expands a short shockwave.
- [ ] Prism charges and releases a focused beam.

### Creep Motion

- [ ] Runner darts.
- [ ] Brute lumbers/bobs.
- [ ] Swarm jitters as a cluster.
- [ ] Shade flickers or leaves echo offsets.
- [ ] Siege lumbers with weight and directionality.

### Exit Signal

- Motion helps identify roles at phone size.
- Heavy pressure remains readable with reduced effects enabled.

## Stage 4: Combat And Economy VFX

Goal: make game feedback satisfying and understandable.

Prioritized VFX:

1. [x] Tower fire effect per tower role.
2. [x] Creep hit reaction.
3. [x] Creep death/bounty effect.
4. [ ] Leak effect.
5. [ ] Send/arrival effect.
6. [ ] Income tick/economy pulse.
7. [ ] Pulse splash effect.
8. [ ] Prism priority hit effect.
9. [ ] Shade resistance/reveal cue.
10. [ ] Siege warning/leak cue.

### Exit Signal

- A tester can understand build, send, hit, kill, leak, income, Pulse splash, Prism priority, Shade resistance, and Siege danger from visuals.

## Stage 5: UI Art And Icons

Goal: make the HUD feel designed, not merely functional.

### Build/Send UI

- [ ] Icon for each tower.
- [ ] Icon for each creep/send.
- [ ] Card frame treatment for build menu.
- [ ] Card frame treatment for send menu.
- [ ] Affordability/disabled state.
- [ ] Selected/pressed state.
- [ ] Compact detail strip or role hint if needed.

### Match UI

- [ ] Top stats strip polish.
- [ ] Selected tower panel polish.
- [ ] Results modal polish.
- [ ] Role glyph language for tower/creep families.

### Exit Signal

- UI looks intentional while preserving the compact mobile layout.
- No third-line microcopy returns to build/send cards.

## Stage 6: Screenshot Gates

Every graphics branch must rerun the visual capture set:

- [x] `01-default-hud.png`
- [x] `02-build-menu-open.png`
- [x] `03-send-menu-open.png`
- [x] `04-lane-selector-open.png`
- [x] `05-active-combat.png`
- [x] `06-heavy-pressure.png`
- [x] `07-reduced-effects-heavy.png`
- [x] `08-results-or-late-match.png`

Review against:

- mobile readability;
- role silhouette clarity;
- board/path clarity;
- UI overlap safety;
- heavy pressure readability;
- reduced-effects readability;
- originality / no protected visual language drift.

## Recommended Branch Sequence

1. `art-prefab-foundation`
   - Create/confirm prefab pipeline.
   - Replace primitive-first tower and creep visuals with authored low-poly prefabs.

2. `art-material-lighting-pass`
   - Build material palette.
   - Tune lighting, shadows, bloom, and board contrast.

3. `art-combat-vfx-pass`
   - Add attack, hit, death, leak, send, income, and role-specific mechanic effects.

4. `art-ui-icon-pass`
   - Add tower/send icons and card treatments.

5. `art-polish-2000-baseline`
   - Consistency pass.
   - Full screenshot review.
   - Lock a new visual baseline.

## Immediate Next Recommendation

Start with:

```text
art-prefab-foundation
```

Reason: until towers and creeps stop being primitive-generated geometry, every material, VFX, and UI polish pass will still feel like dressing up debug art.

## Definition Of Done For The 2000 Baseline

- [ ] All 10 roster roles have authored low-poly visuals.
- [ ] All 10 roster roles are readable at phone size.
- [ ] Heavy pressure with mixed roles remains readable.
- [ ] Reduced-effects mode remains understandable.
- [ ] Build/send menus have icon/card treatment without crowding.
- [ ] Combat/economy feedback communicates the major mechanics.
- [ ] Screenshot review passes or has only low-severity polish issues.
- [ ] The style feels original to Line Wards.
