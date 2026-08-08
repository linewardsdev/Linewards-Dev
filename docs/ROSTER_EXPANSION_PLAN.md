# Roster Expansion Plan — 5 to 8 per category

**Drafted 2026-08-06.** Proposal for review — unit names, mechanics and numbers are sketches
for the owner and the balance record to work over, not commitments. What this document is
firm about is the *routes*: which slots can be built from parts already owned, which need a
generation pass, and what each mechanic sketch costs in simulation work.

## Goal and shape

Expand both rosters from 5 per category to **8 per category**: +9 towers, +9 creeps, 48
units total. Eight rather than ten because every category surface in the client is built
around small grids — the send dock's card rows, the build palette, the tier panels — and
eight keeps two rows of four on a phone without redesigning any of them. Going to ten later
is additive; going past ten means UI work this plan deliberately avoids.

## The three cost tiers, stated up front

Art is the smaller half of a new unit here. Every existing tower carries a measured
mechanic with sim code, tests and a tuning-log entry. Each sketch below is tagged:

- **[A] Reuse** — existing machinery, new numbers. Sim cost: a definition row and tests.
- **[B] Variation** — an existing system with a new parameter or role token. Sim cost:
  small, bounded change to one service, plus tests.
- **[C] New system** — nothing like it exists. Sim cost: real design, a new mechanic in
  `CombatService`, tests, bot awareness, and a tuning cycle. **Budget these like the
  bramble or servicing work, not like a stat line.**

The plan deliberately averages one [C] per category side, not three.

## Art routes

- **KITBASH(parents)** — assembled in Blender from the named owned meshes. Style-matched by
  construction: same parts, same textures, same silhouette language. Free.
- **GENERATE** — a genuinely new silhouette, batched into one paid generation month once
  every GENERATE slot below is designed, so the subscription does a month's work in one
  push and the style stays coherent within the batch.

Everything downstream of the base mesh is the existing pipeline either way: prep and
normalization, stylized-shader materials, AO bake, LODs, rig scripts for walkers, icons,
capture review.

---

## Towers

### ARCANE (5 → 8)

| Slot | Name (sketch) | Art route | Mechanic sketch | Tier |
| --- | --- | --- | --- | --- |
| A6 | Twin Crescent Ward | KITBASH(arrow ×2 heads) | Fires two independent shots per cooldown, each choosing its own target — the anti-chaff arrow | [B] |
| A7 | Lens Ward | KITBASH(control + prism crystal) | Marks its target: marked creeps take +25% from every tower for 2 ticks. One mark live at a time | [C] |
| A8 | Echo Relay | GENERATE | When an orthogonally adjacent ARCANE tower fires, fires a free half-damage shot at the same target — the offensive mirror of Servicing | [B] |

### FOUNDRY (5 → 8)

| Slot | Name (sketch) | Art route | Mechanic sketch | Tier |
| --- | --- | --- | --- | --- |
| F6 | Flak Battery | KITBASH(gatling ×2 barrels, wider stance) | Gatling cadence one tick faster, damage 3 — saturation fire, priced for lanes that leak swarms | [A] |
| F7 | Magnetron Spire | KITBASH(tesla coil + relay dish) | On hit, drags the target one route cell backward (cooldown-limited per creep). Movement interaction — respect the bramble lesson: route indices, mazed route only | [C] |
| F8 | Siege Foundry | GENERATE | The mortar's bigger sibling: slower, harder, impact splashes the landing cell's orthogonal neighbours | [B] |

### GROVE (5 → 8)

| Slot | Name (sketch) | Art route | Mechanic sketch | Tier |
| --- | --- | --- | --- | --- |
| G6 | Bramble Wall | KITBASH(thorn snare + barricade frame) | A thorn tower whose zone minimum is 5 cells instead of 3 — the dedicated brake, weak gun | [A] |
| G7 | Mycelial Node | KITBASH(spore cloud + sapling pair) | Extends Grovebond: counts as adjacent to every GROVE tower within 2 cells, not 1 | [B] |
| G8 | Heartwood Sentinel | GENERATE | Damage scales +10% per other GROVE tower the owner has standing — the payoff for committing to the line | [B] |

## Creeps

### CORE (5 → 8)

| Slot | Name (sketch) | Art route | Mechanic sketch | Tier |
| --- | --- | --- | --- | --- |
| C6 | Bulk Brute | KITBASH(brute, scaled + obsidian plates) | Pure stat brute between Brute and Siege | [A] |
| C7 | Shard Runner | KITBASH(runner + swarm shards on back) | On death, splits into two Swarm at its cell. **Death-spawn is new machinery** — flag accordingly | [C] |
| C8 | Dune Skimmer | GENERATE | Speed-2 mid-cost lane filler; CORE currently has no fast mid-price body | [A] |

### SUPPORT (5 → 8) — every SUPPORT creep carries a role

| Slot | Name (sketch) | Art route | Mechanic sketch | Tier |
| --- | --- | --- | --- | --- |
| S6 | Forge Tick | KITBASH(turret walker, half scale) | Cheap flyer chaff — the wisp of the air lane | [A] |
| S7 | Gloom Chanter | KITBASH(shade + wisp orb) | New role *Veil*: towers targeting creeps near it lose 1 range | [C] |
| S8 | Banner Warden | GENERATE | New role *Rally*: when a nearby creep dies, survivors in its radius take one accelerated step. Reuses the Pacesetter movement path | [B] |

### ELITE (5 → 8)

| Slot | Name (sketch) | Art route | Mechanic sketch | Tier |
| --- | --- | --- | --- | --- |
| E6 | Twin Zephyr | KITBASH(zephyr, paired silhouette + recolor) | Faster, thinner Zephyr — the pure race body | [A] |
| E7 | Cracked Colossus | KITBASH(colossus + prism crystal shards) | Leaks for 2 lives instead of 1 (`LeakLifeLossFor` already supports per-creep values) | [A] |
| E8 | Abyssal Serpent | GENERATE | Regenerates 1 health per tick while unhurt for 3 ticks — the sustain body Rot exists to answer | [B] |

---

## Tallies and sequencing

**Art:** 12 KITBASH, 6 GENERATE. **Sim:** 6×[A], 7×[B], 4×[C] (one per category side except
CORE/ARCANE sharing the splitter/mark pair — if that is too many [C]s, Lens Ward and Shard
Runner are the two to demote to later waves; each category still gains three units).

1. **Proofs first** (done — see `screenshot-reviews/roster-expansion-proofs-20260806/`):
   the same-mesh route is CONFIRMED (Twin Crescent passes beside its parent), the
   cross-mesh route needs interactive placement sessions rather than headless scripts, and
   a split-parts pass over the donor meshes should precede the sibling wave.
2. **Sibling wave:** the 12 kitbash units' art, while their [A]/[B] sims land in parallel.
3. **Design lock, then one generation month** for the 6 GENERATE slots as a single batch.
4. **[C] mechanics last**, one at a time, each with its own tuning-log entry — the record
   is explicit about what happens when mechanics land in bulk untuned.

## Per-unit integration checklist (all automated or scripted today)

Definition row → catalog entry + role palette colour → icon → prep/normalize FBX →
stylized-shader materials via migration → AO bake+bind → LODs → rig script if legged →
bot build-order content → tests → capture review. The only manual steps are the design
decisions and looking at the captures.
