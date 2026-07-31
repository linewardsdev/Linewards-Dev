# Tower Weapon Effects — Review And Proposal

Status: **proposed, not implemented** (2026-07-31).

Prompted by a review note that the tower weapons "are basically cheap looking lasers and they do
not align with the theme of the towers". Both halves of that are correct, and this document records
what the code actually does, why it reads that way, and what to do about it.

Nothing here is built. The per-tower table in Layer 3 is the part most worth marking up before any
of it is.

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

## Verification

Effects that look right in isolation have twice this week turned out invisible in play, so:

1. **Capture every effect at the real `ActiveLane` camera**, not a close-up. The board is busy with
   range halos, role-marker labels and creep health bars, and the earlier Repair Drone tether and
   tower-tier colour tell both proved hard to read against it.
2. **Capture at `ReducedEffects` as well.** `SpawnBeam` and `SpawnEffect` early-out entirely under
   that setting, so a mechanic whose only tell is a beam becomes invisible on low-end devices.
3. **Check frame cost.** Gatling fires every tick; a tracer-and-casings effect on it is the most
   likely thing here to cost real performance on a phone.
4. **Compare against the Foundry mortar**, which is the in-house benchmark for a weapon that reads
   as its theme.

## Open questions for the owner

1. **Is FOUNDRY meant to fire physical projectiles?** This is the single biggest visual departure
   proposed. It is also the most likely to be wrong if the intended fiction is that every tower is
   an energy ward and the Foundry line is merely styled as industrial.
2. **How far should GROVE effects linger?** Spores and vines that persist read as organic, but
   persistent effects on a busy board are what made the earlier decal work hard to see. There is a
   real tension between "organic" and "legible".
3. **Does the tower's tier change its weapon effect?** Tiers currently change only a marker colour,
   which is a weak tell (measured: RGB delta 0.392 from tier 1 to 2, 0.136 from 2 to 3). A visibly
   heavier shot at tier 3 would carry that information far better than the marker does, and would
   solve two problems at once.
4. **Priority against everything else.** Layer 1 is a contained change to one method. Layer 3 is ten
   separate effects and is the bulk of the work.
