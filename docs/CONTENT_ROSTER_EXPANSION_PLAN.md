# LTW Content Roster Expansion Plan

## Purpose

Expand Line Wards from the current starter set into a readable 10-role roster:

- 5 tower build categories.
- 5 creep/send categories.
- Mobile-safe build and send menus that can show all 5 choices without recreating the crowding problems fixed in the visual baseline.

This is a research and implementation-tracking document. It should guide the next gameplay/content branch before final polished art.

## Current Baseline

The current playable baseline supports:

- Tower content through `TowerDefinition`: id, name, cost, range, damage, attack cooldown.
- Creep content through `CreepDefinition`: id, name, cost, income gain, kill bounty, leak bounty, health, speed.
- Three playable towers:
  - `tower.arrow`
  - `tower.control`
  - `tower.relay`
- Three playable sends:
  - `creep.runner`
  - `creep.brute`
  - `creep.swarm`
- Mobile HUD baseline:
  - Compact top stat strip.
  - Bottom-left build launcher.
  - Bottom-right send launcher.
  - Expanded menus float above the bottom launcher row.
  - Results render as a modal card.

The immediate expansion should preserve the simple content model where possible. True special abilities can follow after the stat-only roster is playable.

## Design Principles

1. Each tower should answer a different kind of pressure.
2. Each creep should ask a different defensive question.
3. The first 5x2 roster should be readable through stats, shape, motion, and menu identity before special-case mechanics.
4. Mobile buttons should stay compact: icon/name/cost first, deeper details second.
5. Names and visuals should remain original Line Wards ward-tech fantasy: wards, signal fragments, prism bodies, rune plates, pulse fields, glass cores.
6. Avoid Warcraft, Blizzard, medieval faction, or nostalgia-first RTS language and silhouettes.

## Proposed 5 Tower Categories

| Slot | Id | Name | Primary Role | Stat Identity | Best Against | Art Read |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `tower.arrow` | Arrow Ward | Reliable single-target damage | Low cost, quick cadence, moderate range | Runner, general early pressure | Tall/pointed focus ward with a clear firing spine |
| 2 | `tower.control` | Control Ward | Area control / soft crowd answer | Medium cost, lower damage, slower cadence | Swarm, stacked lanes | Ring/field silhouette with wide base and pulse halo |
| 3 | `tower.relay` | Relay Ward | Utility/economy support | Higher cost, low damage, short range | Scaling/economy play | Signal mast/core, gold/mint relay accents |
| 4 | `tower.pulse` | Pulse Ward | Splash-style pressure answer | Medium-high cost, short range, higher burst/cooldown | Swarm, dense Runner waves | Compact core with expanding disc/echo rings |
| 5 | `tower.prism` | Prism Ward | Long-range specialist | High cost, long range, slower cadence, high damage | Brute, Siege, priority targets | Tall crystalline prism, clean beam-read silhouette |

### Tower Implementation Notes

For the first implementation pass, these can fit the existing `TowerDefinition` model:

- Arrow: baseline quick single-target.
- Control: lower damage, slower cooldown, same/simple range until slow mechanics exist.
- Relay: low damage and cost/income-adjacent identity, even if economy support is not yet mechanical.
- Pulse: short range, bigger damage, slower cooldown as a temporary stand-in for splash.
- Prism: high range, high damage, slow cooldown.

Later simulation extensions can add:

- Splash radius.
- Slow or mark effects.
- Relay aura/economy modifier.
- Priority targeting.
- Detection or anti-stealth.

## Proposed 5 Creep/Send Categories

| Slot | Id | Name | Primary Role | Stat Identity | Defensive Question | Art Read |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `creep.runner` | Runner | Cheap speed pressure | Low cost, low health, faster speed | Can the defender react and cover the route early? | Sharp, forward-pointing dart construct |
| 2 | `creep.brute` | Brute | Health pressure | Higher cost, high health, slower speed | Does the defender have enough focused DPS? | Wide armored pressure core |
| 3 | `creep.swarm` | Swarm | Multi-body pressure | Cheap per unit, low health, sent in quantity | Does the defender have area/control coverage? | Clustered signal mites/shards |
| 4 | `creep.shade` | Shade | Stealth/readability pressure | Medium cost, modest health, faster speed | Can the defender handle low-visibility pressure? | Shimmering ghost-signal with offset echo trail |
| 5 | `creep.siege` | Siege | Slow high-threat pressure | High cost, very high health, slow speed | Can the defender burst down a dangerous late send? | Heavy crystal ram/core with windup markings |

### Why Shade And Siege Before Boss Or Air

Boss is better treated as an event, tier modifier, or late-match escalation rather than a normal menu button.

Air likely requires pathing, targeting, and tower-eligibility rules to feel honest. It should wait until the roster has enough ground-game variety.

Shade and Siege can initially work within the current content model:

- Shade can start as faster/moderately fragile with stealth-inspired visuals only.
- Siege can start as slow/high-health/high-cost with danger visuals only.

Later simulation extensions can add:

- Detection/reveal.
- Siege windup or leak multiplier.
- Anti-maze pressure if that mechanic is added.
- Special bounties or income tuning by role.

## Mobile Menu Plan

### Build Menu

The build menu should support 5 tower buttons without dense text.

Recommended button content:

- Icon or role glyph.
- Short name.
- Cost.

Avoid putting full role text inside every button. Use one selected-detail strip instead.

Example compact labels:

| Button | Label | Cost |
| --- | --- | --- |
| Arrow | `ARROW` | `25G` |
| Control | `CTRL` | `35G` |
| Relay | `RELAY` | `40G` |
| Pulse | `PULSE` | `45G` |
| Prism | `PRISM` | `60G` |

### Send Menu

The send menu should mirror the build menu:

| Button | Label | Cost |
| --- | --- | --- |
| Runner | `RUN` | `10G` |
| Brute | `BRUTE` | `18G` |
| Swarm | `SWARM` | `18G` |
| Shade | `SHADE` | `24G` |
| Siege | `SIEGE` | `40G` |

Recommended detail strip after a selection or press-hold:

- Role one-liner.
- Cost.
- Income gain.
- Quantity, if applicable.
- Best defensive answer, if known.

### Menu Layout Rules

- Expanded menus stay above the bottom launcher row.
- Five buttons may use a single horizontal row only if phone capture proves readable.
- If five horizontal buttons crowd, use a 3+2 grid inside the expanded panel.
- Do not add third-line microcopy inside cards.
- Use disabled/unaffordable tinting, not long error text inside cards.

## Suggested First-Pass Stats

These numbers are starting points for implementation, not final balance.

### Towers

| Id | Cost | Range | Damage | Cooldown | Intent |
| --- | ---: | ---: | ---: | ---: | --- |
| `tower.arrow` | 25 | 2 | 5 | 2 | Baseline early defense |
| `tower.control` | 35 | 2 | 3 | 3 | Control placeholder |
| `tower.relay` | 40 | 1 | 1 | 5 | Utility/economy placeholder |
| `tower.pulse` | 45 | 1 | 8 | 4 | Splash placeholder until true AOE |
| `tower.prism` | 60 | 4 | 12 | 6 | Long-range heavy hitter |

### Creeps

| Id | Cost | Income | Kill Bounty | Leak Bounty | Health | Speed | Quantity Intent |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `creep.runner` | 10 | 1 | 1 | 2 | 10 | 1 | Single or small chain |
| `creep.brute` | 18 | 2 | 2 | 3 | 24 | 1 | Single heavy body |
| `creep.swarm` | 6 | 1 | 1 | 1 | 5 | 2 | Multi-send group |
| `creep.shade` | 24 | 3 | 2 | 4 | 14 | 2 | Fast pressure with low-visibility visuals |
| `creep.siege` | 40 | 4 | 4 | 6 | 48 | 1 | Slow late pressure |

## Implementation Phases

### Phase 1: Research And Contract

- [x] Define the 5 tower categories.
- [x] Define the 5 creep categories.
- [x] Decide that first pass stays mostly stat-based.
- [ ] Confirm final names and ids.
- [ ] Confirm whether the menu uses one row of five or a 3+2 grid.
- [ ] Confirm whether Shade/Siege replace Air/Boss for the first five.

### Phase 2: Simulation Content

- [ ] Add `tower.pulse` and `tower.prism` to sample content.
- [ ] Add `creep.shade` and `creep.siege` to sample content.
- [ ] Add tests that sample content validates with 5 towers and 5 creeps.
- [ ] Add affordability/command tests for the new content.
- [ ] Add scenario tests for low, normal, heavy, and mixed-pressure sends.

### Phase 3: Unity Controls

- [ ] Expand build menu from 3 tower buttons to 5.
- [ ] Expand send menu from 3 creep buttons to 5.
- [ ] Add hotkeys for the new tower and send slots.
- [ ] Keep expanded menus above the bottom launcher row.
- [ ] Add selected-detail strip or compact role hint if needed.
- [ ] Verify no text crowding at phone-size capture.

### Phase 4: Presentation And Readability

- [ ] Add primitive/fallback visual language for Pulse and Prism towers.
- [ ] Add primitive/fallback visual language for Shade and Siege creeps.
- [ ] Add asset folders/readmes for new tower and creep roles.
- [ ] Update art checklist for the 5x2 roster.
- [ ] Run screenshot capture set with build menu, send menu, active combat, heavy pressure, and results.

### Phase 5: Balance And Bots

- [ ] Let bots choose from more than one send type by profile.
- [ ] Let bots use expanded tower roles by profile.
- [ ] Tune opening gold and bot reserve behavior if new costs distort pacing.
- [ ] Record playtest evidence for mixed send pressure.
- [ ] Update `GD_TUNING_LOG.md` with first-pass findings.

## Open Questions

1. Should Relay become a real economy modifier now, or remain a low-damage utility placeholder until tower abilities exist?
2. Should Pulse get true splash immediately, or should it remain a stat placeholder for the first implementation pass?
3. Should Shade have real stealth/detection now, or visual stealth only?
4. Should Siege have a special leak penalty/windup, or just high-health pressure?
5. Should the mobile menu use 5 horizontal buttons or a 3+2 grid?

## Recommended Next Branch

Use a dedicated branch:

```text
content-5x2-roster-research
```

Suggested first implementation branch after this doc:

```text
content-5x2-roster-prototype
```

## Exit Signal

This expansion is ready for implementation handoff when:

- The roster names and ids are accepted.
- The first-pass stat table is approved for prototyping.
- The menu layout decision is made.
- The implementation checklist above is copied into the active work tracker or referenced by the branch.
