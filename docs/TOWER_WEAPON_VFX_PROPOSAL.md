# Tower Weapon Effects — Review And Proposal

Status: **implemented** (2026-07-31). All three layers are built; each section below carries a
"Built" note recording what shipped and what was learned doing it. The owner's answers to the open
questions are in "Decisions" at the end, and the body has been updated to match them.

Prompted by a review note that the tower weapons "are basically cheap looking lasers and they do
not align with the theme of the towers". Both halves of that are correct, and this document records
what the code actually does, why it reads that way, and what to do about it.

## What the code actually does

All firing visuals come from one method, `UnityVerticalSliceRenderer.SpawnTowerAttackCue`, called
from the `CreepDamagedEvent` branch. Three separate problems compound inside it.

### 1. A "beam" is a stretched box

`SpawnBeam` builds every beam in the game from a Unity cube primitive:

```csharp
var beam = GetPooled(beamPool, "TowerBeam", PrimitiveType.Cube);
beam.transform.localScale = new Vector3(0.06f, 0.06f, Mathf.Max(0.1f, distance));
```

A `0.06 × 0.06 × distance` rectangular prism with a flat unlit colour. No taper, no soft edge, no
bright core against a dimmer glow, no travel along its length, no texture. It reads as a cheap laser
because geometrically it *is* one: a long thin box that appears and disappears.

This single primitive is the raw material of nearly every weapon effect on the board, so its
cheapness is inherited by all fifteen towers at once.

The three sections that follow describe the code **as it was before this work**, and are kept in the
past tense deliberately: they are the argument for the change, and the "Built" notes below only make
sense against them.

### 2. Ten of the fifteen towers share one generic effect

`SpawnTowerAttackCue` has bespoke branches for exactly five towers — Arrow, Control, Relay, Pulse
and Prism, the original roster. Everything else falls through to the same tail:

```csharp
SpawnBeam(left, right, shotColor, 0.1f);      // a crossing beam
SpawnBeam(front, back, shotColor, 0.1f);      // another crossing beam
SpawnBeam(muzzle, hit, shotColor, 0.14f);     // a beam to the target
SpawnCellFrameCue(hitPosition, shotColor);    // a square outline
```

**Gatling, Tesla, Foundry Core, Barricade, Repair Drone, Elder Canopy, Sapling, Bloomheart, Thorn
Snare and Spore Cloud all draw exactly that.** Verified by searching the method for per-tower
predicates: it contains five, for the original five.

`docs/VFX_AND_ANIMATION_TARGETS.md` corroborates the history — its "Tower Shot Targets" table only
ever specified shots for those same five. The ten towers added later were never given a visual
design, and inherited a fallback that was written as a stopgap.

The consequence is worst where the tower is most distinctive:

- **Tesla's Chain Arc** — the mechanic the tower exists for, hopping backward down a queue and
  halving each time — draws the same straight box three times, with no visible link between hops.
- **Spore Cloud's Rot**, which scales damage off the target's max health, draws a blue box.
- **Thorn Snare's Bramble Hold**, which halves a creep's speed, draws a blue box. The creep visibly
  slows, but nothing connects that to the tower that did it.
- **Elder Canopy's Deep Roots**, which uniquely targets the *rearmost* creep, draws a blue box, so
  the one tower whose targeting is legible from a distance is not drawn to show it.

### 3. Four shot colours across fifteen towers, none keyed to the line

`TowerShotColor` returns: one colour shared by Control and Prism, one for Pulse, one for Relay, and
for **everything else** either gold or arcane blue depending only on whether damage ≥ 5.

So a GROVE spore bloom and a FOUNDRY gatling fire the same blue. The three lines — ARCANE, FOUNDRY,
GROVE — have distinct names, distinct models, and distinct accent colours on their menu cards, and
the weapons ignore all of it.

### The counter-example already in the codebase

The Foundry Core's mortar is the exception and the proof: a real shell that arcs, a ground telegraph
that contracts onto the impact cell as a countdown, and a crater on landing. It reads unmistakably as
artillery. It was built because the mechanic was unreadable without it — the same argument this
document is making for the other nine.

## Proposal

Three layers. The order matters: Layer 1 raises the floor for everything, and per-tower work done
before it would be decoration on a bad foundation.

### Layer 1 — Replace the beam primitive

One change to `SpawnBeam`, improving all fifteen towers simultaneously.

Replace the cube with a camera-facing quad strip carrying a bright core and a soft falloff to
transparent at the edges, tapered toward the target, with a short travel-and-fade rather than an
instant on/off.

No new art pipeline is required. `LTWContactShadow.shader` already provides soft radial falloff (the
shockwave rings and contact shadows use it) and `LTWParticleAdditive.shader` already exists for
additive glow. A beam shader would sit alongside `LTWSporeFog.shader`, which established the pattern
of a small purpose-built shader for one effect.

**This alone addresses most of "cheap looking lasers."** It is also the cheapest to try and the
easiest to judge — if the primitive still looks poor, the rest is not worth starting.

**Built 2026-07-31.** `LTWWeaponBeam.shader` plus a rewritten `SpawnBeam`, which gained `width` and
`intensity` parameters for the per-line and per-tier work that follows. The mesh is still the cube —
replacing it was unnecessary once the shading was right, and it avoids billboarding a quad toward an
orthographic camera.

Two things cost real time and are worth not rediscovering:

- **A cube rasterizes only its surface.** The first version faded alpha on "distance from this
  fragment to the cube's central axis", which on a side face is *always* exactly the maximum — so
  every pixel of every beam shaded to alpha 0 and nothing drew at all, with no shader error and no
  warning. The fix measures how close the **view ray** passes to the axis instead, which treats the
  box as the solid tube it represents. Any future volumetric effect built on a primitive solid has
  the same trap waiting.
- **A soft edge needs somewhere to fade out to.** With the mesh exactly as wide as the beam, the
  falloff had no room and the result was a smoother but still hard, uniform line. The mesh is now
  built `BeamHaloWidthScale` (3x) wider than the shot, the shader keeps the requested width as its
  bright core, and the halo spreads across the remainder.

A third defect surfaced while verifying, pre-existing and unrelated to appearance: `SpawnBeam` and
the Repair Drone's servicing tether share `beamPool`, and the tether only ever set `.color`. Once
`SpawnBeam` began assigning its own material, a tether recycled from a released beam would keep the
additive shader and draw as a glowing tube at random, depending on pool order — the same failure
`GetPooled`'s own comment documents for meshes. The tether now asserts its material rather than
tinting whatever it was handed.

### Layer 2 — Give each line a visual language

Not a palette entry per line, a grammar:

| Line | Language | Should read as |
| --- | --- | --- |
| **ARCANE** | Thin, clean, cold blue-white. Instant beams. Ring and portal geometry. Minimal debris. | Precision energy |
| **FOUNDRY** | Hot orange. **Projectiles rather than beams** — tracers, muzzle flash, recoil, smoke, spent casings. | Machinery firing ordnance |
| **GROVE** | Thick, slow, organic green. Spores, vines, drifting particulate that lingers after the hit. | Living things reaching out |

The FOUNDRY row is the biggest single lever after Layer 1: five towers currently fire energy beams
when their entire identity is industrial machinery. Gatling and Barricade in particular should be
throwing physical rounds.

**Built 2026-07-31.** `LineFor` reads the line from `TowerCatalog` rather than restating the
grouping, so a tower moved between lines cannot fire one line's weapon from another line's card.
`StyleFor` returns the width, intensity and duration; `LineShotColor` the colour. Two shape changes
came with it: the pair of beams that crossed at the tower body were pinned to local axes and read as
a static X unrelated to where the tower was aiming, replaced by a burst thrown along the firing
direction; and GROVE impacts open a bloom instead of snapping a hard square around the target cell.

Measured on seed 1 at a fixed tick rate, this **cut** peak presentation objects from 35,448 to
29,060 — the crossing beams cost more than the grammar that replaced them.

### Layer 3 — Per-tower tells for the ten with none

Ordered by how much the effect would make an otherwise invisible mechanic legible. **This table is
the part to mark up.**

| Tower | Line | Mechanic | Today | Proposed | Makes visible |
| --- | --- | --- | --- | --- | --- |
| **Tesla Coil** | Foundry | Chain Arc, 2 extra hops, halving | Same box ×3 | Jagged forked lightning per hop, each visibly dimmer and thinner | The chain, its direction, and its decay |
| **Spore Cloud** | Grove | Rot — damage from target max health | Blue box | Green spore burst that clings to the target; visibly heavier against fat creeps | That it punishes big targets specifically |
| **Thorn Snare** | Grove | Bramble Hold — half speed in a 3-cell zone | Blue box | Vines snapping taut from tower to creep, tension held while braked | *Why* that creep slowed |
| **Elder Canopy** | Grove | Deep Roots — targets rearmost | Blue box | Slow heavy root-lash reaching past nearer creeps to the back of the lane | That it deliberately ignores the leader |
| **Gatling** | Foundry | Fires every tick | Blue box | Rapid tracer stream, muzzle flash, ejected casings | Its rate of fire, which is its whole point |
| **Sapling** | Grove | Grovebond, +1 per neighbour | Blue box | Thin shot that visibly thickens with each bonded neighbour | The bond paying off |
| **Bloomheart** | Grove | Crowd Bloom, +1 per creep on cell | Blue box | Bloom that intensifies with the size of the crowd hit | That a stack is what feeds it |
| **Barricade** | Foundry | Fixed emplacement, up-lane only | Blue box | Heavy short slug with recoil, fired only up-lane | Its fixed arc and refusal to turn |
| **Repair Drone** | Foundry | Servicing — neighbours fire faster | Blue box | Keep the existing servicing tether; make its own shot a light maintenance pulse | Support role, distinct from a weapon |
| **Foundry Core** | Foundry | Stack Mortar | **Already good** | Leave alone | — |

The five original towers keep their existing bespoke effects, which are already tuned; they inherit
Layer 1's better primitive and Layer 2's line grammar without being rewritten.

**Built 2026-07-31.** Nine branches, four new shape helpers (`SpawnForkedArc`, `SpawnTracerShot`,
`SpawnSlugShot`, `SpawnVineLash`), and tier plumbed to the cue site through `towerTiersByCell`.

The load-bearing idea is that **several of these mechanics express themselves as damage the
simulation has already computed**, so the effect can read its own mechanic without the renderer
knowing anything about it. Tesla's chain hops arrive as separate events carrying each hop's halved
damage, so an arc sized by damage shows the chain decaying. Grovebond adds damage per bonded
neighbour and Crowd Bloom per creep on the cell, so a shot that thickens with damage *is* the bond
paying off.

Three findings from capturing at the real camera:

- **The vine lash was wrong on the first attempt.** Two straight segments meeting at a midpoint drew
  a hard geometric diamond — less organic than the plain beam it replaced. It is now a quadratic
  curve through an offset control point, sampled over five segments with slight jitter and tapering
  toward the tip.
- **`SpawnExpandingRing` draws a soft low-contrast glow, not a crisp ring.** That is legible for
  Control, whose ring is a slow deliberate beat, but far too weak to carry Spore Cloud's and
  Bloomheart's bloom, which is those two towers' entire tell. A particle burst leads there now, with
  the ring only underneath it. Worth knowing before reaching for that primitive again.
- **Gatling got cheaper, not dearer.** The concern was that the tower firing every tick was the most
  likely thing here to cost real frame time. Its shared fallback spawned seven beams and a burst per
  shot (three beams plus `SpawnCellFrameCue`'s four); the tracer spawns two beams and a burst. All
  nine tells together still measure ~6% below the Layer 1 baseline: 33,156 against 35,448.

## Verification

Effects that look right in isolation have twice this week turned out invisible in play, so:

1. **Capture every effect at the real `ActiveLane` camera**, not a close-up. The board is busy with
   range halos, role-marker labels and creep health bars, and the earlier Repair Drone tether and
   tower-tier colour tell both proved hard to read against it.
2. **Capture at `ReducedEffects` as well.** `SpawnBeam` and `SpawnEffect` early-out entirely under
   that setting, so a mechanic whose only tell is a beam becomes invisible on low-end devices.
   **Checked 2026-07-31:** unchanged by this work, and no worse than before — `SpawnCellFrameCue`,
   the one cue that looked like it might survive, is itself built out of `SpawnBeam`, so the entire
   attack-cue path already vanished at that setting. The gap is real but pre-existing.
3. **Check frame cost.** Gatling fires every tick; a tracer-and-casings effect on it is the most
   likely thing here to cost real performance on a phone.
4. **Compare against the Foundry mortar**, which is the in-house benchmark for a weapon that reads
   as its theme.

## Visibility pass (2026-07-31)

Item 1 above is now measured rather than eyeballed, the same way the tower idle pass was.
`Assets/Editor/WeaponEffectVisibilityProbe.cs` places one of every tower, fires ONE cue at a time
at damage 6 / tier 1, and counts how many pixels the shot brightens at the real board camera
against the declared 1080x1920 surface. Evidence in
`docs/screenshot-reviews/weapon-effect-visibility-*.md`.

### Isolating the shot took three attempts

Worth recording, because the first two produce confident numbers that mean nothing:

1. **Diff against a live board.** Every tower came back at 24/24 frames and 24-40k lit pixels. That
   is not the weapons — it is rings spinning, dishes turning, spore fog churning and fifteen towers
   breathing, with the shot lost inside it.
2. **An ambient control run, subtracted.** Worse: ambient drifts further from its own baseline the
   longer a window runs, so a control measured over the same 24 frames overshot, and four towers
   scored *negative* against it.
3. **`Time.timeScale = 0`.** Ambient is then not estimated, it is zero, and what remains is exactly
   the geometry the shot added.

**Scope of the number, stated plainly:** with time frozen this measures what a cue puts on screen at
the *instant of firing*. Expanding rings open from a fraction of their radius and particle bursts
have not emitted yet, so neither contributes. That is a limitation of the instrument AND the answer
to a real question — whether a shot registers when it happens, or only afterwards.

### What it found

Thirteen of fifteen towers lit 2,700-18,600 pixels. Two lit almost nothing:

| Tower | Before | After | Change |
| --- | --- | --- | --- |
| Pulse Ward | 146px | 2,411px | four radial spokes at the muzzle |
| Repair Drone | 424px | 5,003px | a thin service beam to the target |

They were the only two towers whose cue contained **no immediately-drawn geometry at all** — both
were built entirely from `SpawnExpandingRing` and `SpawnEffect`, and both of those arrive over later
frames. Every other tower draws beams, which exist the moment the shot does.

The fixes stay in character rather than bolting a weapon onto a support tower. Pulse gets spokes
radiating outward, the one direction language that does not contradict an omnidirectional splash
emitter; the Repair Drone's beam is deliberately thin and short, there to say *when* it acted rather
than to look like ordnance.

This also confirms, with a number, the thing the Layer 3 notes recorded by eye: `SpawnExpandingRing`
is a weak carrier. It is fine as a supporting layer and cannot be the whole of a cue.

## Decisions

Answered by the owner 2026-07-31.

**1. FOUNDRY fires physical projectiles where it fits, not everywhere.** Gatling throws tracers with
ejected casings, Barricade a heavy slug, Foundry Core keeps its mortar shell. **Tesla stays
lightning** — a coil throwing solid rounds fights its own name and mechanic — and Repair Drone stays
a support pulse rather than a weapon. The line reads industrial without forcing ordnance onto the two
towers whose fiction is not ordnance.

**2. GROVE effects linger briefly — under half a second.** Organic in shape and motion, gone quickly.
Chosen against the more persistent options because the board already carries range halos,
role-marker labels and health bars, and both the Repair Drone tether and the tower-tier colour tell
proved hard to read against that clutter this week. Organic feel, no permanent noise.

**3. Tier changes the weapon.** A higher-tier shot is visibly thicker, brighter and hits harder.
This does double duty: the weapon carries tier information that the marker colour currently carries
badly (measured RGB delta of only 0.136 between tiers 2 and 3, against 0.392 from 1 to 2), and an
upgrade becomes something you can see rather than something you read off a panel.

**4. All three layers are in scope.** Built in dependency order regardless — Layer 1 first, captured
and judged before the per-tower work, since ten bespoke effects built on a bad primitive would be
decoration.

## Consequences for the plan above

- The Layer 2 FOUNDRY row now reads "projectiles **for Gatling, Barricade and Foundry Core**;
  Tesla keeps an electrical arc; Repair Drone keeps a support pulse".
- The Layer 3 table's Tesla row is unchanged — forked lightning was already the proposal.
- Every Layer 3 effect must additionally scale with tier, so the per-tower work carries a tier
  dimension that was not in the original estimate.
- GROVE durations are capped at ~0.5s, which constrains the Thorn Snare "tension held while braked"
  idea: the vine snaps and releases quickly rather than staying taut for the whole brake. The brake
  itself remains shown by the existing bramble zone decal.
