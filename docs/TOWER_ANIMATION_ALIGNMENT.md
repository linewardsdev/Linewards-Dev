# Tower Animation Alignment Pass

A holistic review of all fifteen towers, checking that what each one **does on screen**
matches what it **is**, what it's **called**, and what it's **for**.

## Method

Each tower is reviewed on five axes:

| Axis | Question |
| --- | --- |
| **Type** | What kind of object is it, physically? A machine, a building, a plant? |
| **Style** | Which build line, and what visual language does the mesh actually speak? |
| **Intent** | What does it do mechanically — the reason a player buys it? |
| **Name** | What does its name promise? |
| **Perceived Animation** | What does its current motion read as, to someone watching? |

Then **benchmark**: read the real `TowerMotionProfile`, the real prefab structure, and the
real mechanic out of the code — not the intent, the actual values. Where the perceived
animation contradicts Type/Intent/Name, it is a misalignment and gets fixed.

Benchmarks below were taken by rendering every tower mesh, dumping every motion profile,
and grepping every prefab for its moving parts. Nothing here is asserted from memory.

## How tower motion actually works

Three independent systems, which matters because a tower can be misaligned in one and fine
in the others:

1. **Idle** — `breatheHz`/`breatheAmp` (a uniform scale pulse, `sharpness` > 1 makes it
   peaked like a heartbeat) and `driftHz`/`driftAmp` (a lateral sway).
2. **Aim** — a tower yaws to face its target. If the prefab has a **`HeadPivot`**, only the
   head turns. If it does not, and `locksYaw` is false, **the entire tower body rotates.**
3. **Recoil** — a kick on firing, scaled by `recoilScale` over `recoilDuration`.
   `suppressRecoil` defaults to `locksYaw`.

## The headline finding

Only five prefabs have a `HeadPivot`. Seven towers have neither a `HeadPivot` **nor**
`locksYaw`, which means **the whole structure swivels to track creeps** — including five
trees, flowers and mushrooms, and a stone pagoda.

A tree does not rotate to face you. This is the dominant misalignment in the roster and it
accounts for most of the fixes below.

## The roster

### ARCANE

| | Arrow Tower |
| --- | --- |
| **Type** | Ballista/cannon on a fixed base |
| **Style** | Arcane — carved stone and gold, a weapon |
| **Intent** | Reliable single-target damage, the opener |
| **Name** | "Arrow" — a thing that is aimed and loosed |
| **Perceived** | Head tracks the target and kicks on release |
| **Benchmark** | `HeadPivot` ✅, `restHeading 90`, breathe 1.4/0.015, recoil 1.2 |
| **Verdict** | **Aligned.** The reference implementation for a turret. |

| | Control Ward |
| --- | --- |
| **Type** | Shrine — suspended core inside a ring of arches |
| **Style** | Arcane — floating, ceremonial |
| **Intent** | Area control against clustered pressure |
| **Name** | "Ward" — a held, sustained effect |
| **Perceived** | Arch assembly turns toward the target; ring spins continuously |
| **Benchmark** | `HeadPivot` ✅ + `Ring` spin, breathe 1.6/0.01, recoil 0.5 |
| **Verdict** | **Aligned.** Turning the arms to face pressure suits a ward that projects at something. |

| | Relay Ward |
| --- | --- |
| **Type** | Mast with a dish and crystal fins |
| **Style** | Arcane — signal architecture |
| **Intent** | Utility; pays gold on hit |
| **Name** | "Relay" — transmission, pointed somewhere |
| **Perceived** | Dish turns and spins on a swaying mast |
| **Benchmark** | `HeadPivot` ✅ + `Dish` spin, breathe 1.1/0.02, drift 0.6/0.025, recoil 0.4 |
| **Verdict** | **Aligned.** A dish that points and rotates is exactly what the name promises. |

| | Pulse Ward |
| --- | --- |
| **Type** | Low ground-set drum in a disc |
| **Style** | Arcane — flush, embedded emplacement |
| **Intent** | Splash damage to everything nearby |
| **Name** | "Pulse" — rhythmic, omnidirectional |
| **Perceived** | Sits still and throbs; ring spins |
| **Benchmark** | `locksYaw` ✅, `sharpness 3` (peaked), `Ring` spin, recoil suppressed |
| **Verdict** | **Aligned.** Omnidirectional splash has no facing, and the peaked pulse names itself. |

| | Prism Ward |
| --- | --- |
| **Type** | Crystal spire cluster |
| **Style** | Arcane — faceted, refractive |
| **Intent** | Long-range focus, prioritises high-value targets |
| **Name** | "Prism" — light bent toward a point |
| **Perceived** | Spire turns to face its pick and rotates |
| **Benchmark** | `HeadPivot` ✅ + `Spire` spin, breathe 1.8/0.025, recoil 0.8 |
| **Verdict** | **Aligned.** Deliberate turning suits a tower whose whole point is target selection. |

### FOUNDRY

| | Gatling Turret |
| --- | --- |
| **Type** | Rotary gun on a pedestal |
| **Style** | Foundry — riveted brass and iron |
| **Intent** | Fastest fire rate in the roster (1-tick cooldown) |
| **Name** | "Gatling" — spinning barrels, specifically |
| **Perceived** | Gun tracks the target, barrel spins up while firing, light fast kick |
| **Benchmark** | `HeadPivot` ✅ + `Barrel` spin (40→900°/s), `restHeading 98.7`, recoil 0.3 over 0.12s |
| **Verdict** | **Aligned.** Kick is deliberately short so it finishes inside the 0.25s firing interval. |

| | Tesla Coil Spire |
| --- | --- |
| **Type** | Tiered stone pagoda with vents |
| **Style** | Foundry — static architecture, not a weapon |
| **Intent** | Chain arc — the bolt jumps between creeps |
| **Name** | "Coil"/"Spire" — a structure that discharges, not one that points |
| **Perceived** | **The entire pagoda swivels to face each target.** |
| **Benchmark** | No `HeadPivot`, no `locksYaw` → whole body yaws. breathe 3.2/0.014 `sharpness 2` |
| **Verdict** | **MISALIGNED.** A masonry pagoda rotating on its foundations contradicts both its Type and its mechanic — an arc that leaps between targets needs no facing at all. **Fix: lock yaw.** |

| | Foundry Core |
| --- | --- |
| **Type** | Furnace/boiler with chimney stacks |
| **Style** | Foundry — industrial, heavy |
| **Intent** | Delayed mortar shell, fired upward |
| **Name** | "Core" — a plant, not a gun |
| **Perceived** | Planted; slow heavy bellows swell; hard kick on launch |
| **Benchmark** | `locksYaw` ✅, `suppressRecoil: false`, `sharpness 2.5`, recoil **1.8** (heaviest) |
| **Verdict** | **Aligned.** Fires up its own stacks, so no facing, but keeps the biggest kick in the roster. |

| | Barricade Bastion |
| --- | --- |
| **Type** | Solid stone blockhouse |
| **Style** | Foundry — fortification |
| **Intent** | Fires along one fixed up-lane arc |
| **Name** | "Barricade"/"Bastion" — immovability is the whole word |
| **Perceived** | Does not turn; near-inert idle; solid thump when it fires |
| **Benchmark** | `locksYaw` ✅, `suppressRecoil: false`, breathe 0.7/0.006 (lowest), recoil 1.5 |
| **Verdict** | **Aligned.** The one tower whose stillness *is* the characterisation. |

| | Repair Drone Spire |
| --- | --- |
| **Type** | Slender pillar on a plinth with a dish |
| **Style** | Foundry — instrument, not ordnance |
| **Intent** | Servicing — adjacent towers fire faster |
| **Name** | "Spire" — bolted down; the *drone* is what it projects |
| **Perceived** | **The whole spire swivels to track creeps.** |
| **Benchmark** | No `HeadPivot`, no `locksYaw` → whole body yaws. breathe 1.2/0.008, recoil 0.5 |
| **Verdict** | **MISALIGNED.** A pillar on a plinth cannot rotate. Its real output is a tether to a neighbour, not a shot at a creep. **Fix: lock yaw.** |

### GROVE

| | Elder Canopy |
| --- | --- |
| **Type** | Ancient tree |
| **Style** | Grove — organic, rooted |
| **Intent** | Longest range (5); strikes the back-most creep |
| **Name** | "Canopy" — a spreading crown |
| **Perceived** | **The whole tree rotates to face targets**, over a wide sway |
| **Benchmark** | No `HeadPivot`, no `locksYaw` → whole body yaws. breathe 0.6/0.03, drift 0.4/**0.055** (widest) |
| **Verdict** | **MISALIGNED.** Rooted trees do not pivot. The wide sway is right and should carry it alone. **Fix: lock yaw, keep the sway.** |

| | Sapling Sentinel |
| --- | --- |
| **Type** | Young spiked tree |
| **Style** | Grove — small, eager |
| **Intent** | Cheapest tower (10g); Grovebond with adjacent Grove towers |
| **Name** | "Sapling" — a growing plant |
| **Perceived** | **The whole sapling rotates**, with a quick springy sway |
| **Benchmark** | No `HeadPivot`, no `locksYaw` → whole body yaws. breathe 2.0/0.028, drift 1.2/0.03 |
| **Verdict** | **MISALIGNED.** Same as its elder. **Fix: lock yaw, keep the springy sway.** |

| | Bloomheart Totem |
| --- | --- |
| **Type** | Flower on a twisted stalk |
| **Style** | Grove — botanical |
| **Intent** | Crowd Bloom — stronger against dense groups |
| **Name** | "Bloom" — opening and closing |
| **Perceived** | **The whole flower rotates**, breathing in a peaked bloom |
| **Benchmark** | No `HeadPivot`, no `locksYaw` → whole body yaws. breathe 0.9/0.035 `sharpness 2`, drift 0.5/0.02 |
| **Verdict** | **MISALIGNED** on yaw only — the peaked bloom pulse is exactly right for the name. **Fix: lock yaw, keep the bloom.** |

| | Thorn Snare Totem |
| --- | --- |
| **Type** | Knotted thorn mass |
| **Style** | Grove — hostile, coiled |
| **Intent** | Bramble zone brakes creeps to half speed |
| **Name** | "Snare" — a trap, which by definition waits |
| **Perceived** | **The whole thicket rotates**; almost no idle |
| **Benchmark** | No `HeadPivot`, no `locksYaw` → whole body yaws. breathe 0.5/0.008 (near-inert), recoil 1.4 |
| **Verdict** | **MISALIGNED.** A snare that turns to watch you is not a snare. Stillness then a snap is the read. **Fix: lock yaw, keep the near-inert idle and the sharp recoil.** |

| | Spore Cloud Bloom |
| --- | --- |
| **Type** | Mushroom cluster |
| **Style** | Grove — fungal, diffuse |
| **Intent** | Rot — damage over time in an area |
| **Name** | "Cloud" — diffuse, directionless |
| **Perceived** | **The whole cluster rotates**, with a slow swell and lazy drift |
| **Benchmark** | No `HeadPivot`, no `locksYaw` → whole body yaws. breathe 0.7/0.032 `sharpness 1.6`, drift 0.35/0.035 |
| **Verdict** | **MISALIGNED.** A cloud has no facing — the name says so. **Fix: lock yaw, keep the swell.** |

## Summary

| Verdict | Count | Towers |
| --- | --- | --- |
| Aligned | 8 | Arrow, Control, Relay, Pulse, Prism, Gatling, Foundry, Barricade |
| Misaligned | 7 | Tesla, Repair Drone, Elder Canopy, Sapling, Bloomheart, Thorn Snare, Spore Cloud |

Every misalignment is the same defect: **a structure that cannot physically rotate is
rotating**, because `locksYaw` was only ever set on the three towers whose mechanic made
the problem obvious (Pulse fires everywhere, Foundry fires upward, Barricade fires one
way). It was never applied on the grounds of *what the object is*, which is why every plant
in the Grove line swivels like a turret.

None of the idle motion is wrong. In each case the sway, swell or stillness already suits
the tower, and locking yaw lets it carry the tower on its own instead of competing with a
rotation that should not exist.

## Fixes applied

- `locksYaw: true` on all seven misaligned towers.
- `suppressRecoil: false` alongside it on every one of them. This is load-bearing:
  `suppressRecoil` **defaults to `locksYaw`**, so locking yaw would otherwise have silently
  deleted Thorn Snare's snap, Elder Canopy's lurch and every other firing reaction as a
  side effect of a rotation fix. Only Pulse still suppresses recoil, which is correct — an
  omnidirectional splash emitter has nothing to kick against.
- Idle motion left untouched on all fifteen. It was never the problem.

## The invariant this establishes

After the pass, the roster obeys a single rule that did not hold before:

> **A tower yaws if and only if its prefab has a `HeadPivot`.**

The five yaw-free roles — Arrow, Control, Relay, Prism, Gatling — are exactly the five
prefabs that carry a `HeadPivot`, so every rotation in the game is now a *head* turning on
a body that stays put. The other ten do not rotate at all.

That is checkable rather than a matter of taste, and it is the thing to re-check when a
tower is added: if a new tower has no `HeadPivot`, it must set `locksYaw`, or it will
swivel its whole structure like the seven fixed here.

## Verification

| Check | Result |
| --- | --- |
| Yaw-free roles vs `HeadPivot` prefabs | exact match, 5/5 |
| Recoil preserved on all seven fixed towers | yes (`suppressRecoil: false`) |
| Recoil still suppressed where intended | Pulse only |
| Idle motion changed | none |
| Unity compile | 0 `error CS` |
| Simulation tests | 208/208 |
| Batch playtest | pass, seed 1 |

Not verified: how the fixed towers look in motion. Locking a rotation that should not exist
is hard to get visually wrong, but the Grove line in particular now leans entirely on its
sway, and that sway has never been watched at the current board scale.
