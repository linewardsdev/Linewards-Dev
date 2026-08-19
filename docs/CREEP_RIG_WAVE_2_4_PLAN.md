# Creep Rig Wave 2.4 — Revenant, Shade, Swarm, Wisp

The last four creeps without an `Animator`, closing item 11. Written 2026-08-09, not started.

## The question this wave has to answer first

Item 4's standing rule is **render-check from the actual game camera before investing in leg
articulation**. Wave 2.3 applied it and found the rule answered a question one step earlier than it
anticipates: none of siege, serpent or runner was a walker, and reusing `rig_quadruped_creep.py`
"would have built three thigh-and-shin rigs for limbs that do not exist."

This wave has to go one step earlier still, because there is evidence in the client that **three of
these four may not want a skeleton at all**:

| creep | motion style already assigned | what the client does today |
| --- | --- | --- |
| Swarm | `ClusterJitter` | `ConfigureSwarmCluster` **replaces the single body with a cluster of shards**, each wandering on `Time.time` |
| Shade | `Shimmer` | grouped with `invisible`/`stealth`; bob, sway, drift and pulse |
| Wisp | `Hover` if tagged flying/air, else `RunnerDart` — **confirm which** | speed 3, the fastest creep in the roster |
| Revenant | not special-cased; falls to `RunnerDart` | nothing bespoke |

An unrigged creep is not an unanimated creep. `CreepMotionProfileForStyle` gives every creep a full
procedural profile, and `CreepRoleMotion` deliberately **suppresses most of it once a creep is
rigged** — a rigged creep gets sway and scale pulse only, because "a rigged creep's clip already
animates its body, so bob, roll, pitch and drift would fight it."

So rigging one of these is not additive. It **replaces** the motion it has. For a swarm of shards,
or a shade whose whole identity is being incorporeal, that trade may be a downgrade, and this wave
should be prepared to close item 11 with fewer than four rigs and a written argument rather than
treat four rigs as the deliverable.

## Step 1 — body-plan audit, all four, before any rigging

Per creep, using `blender_audit_model.py` and `blender_game_camera.py` the way wave 2.3 did, produce
the same kind of finding it recorded (four wheels at r=0.098, a closed coil, 52 of 7702 verts below
18% height):

- Does it have legs, and are they legible at board scale? `creep-leg-visibility` answers the second
  half — but note **its height column is about 2x too large**, corrected in wave 2.3's commit; it
  uses the bootstrap camera's 61.9 px/unit rather than the shipped board camera. Read the corrected
  figure, not the raw one.
- Does it touch the ground at all? Runner "floats" and that decided its rig.
- Which way does it face? **Two of wave 2.3's three were facing backwards down the lane**, both
  flagged in source as unverified guesses and both confirmed wrong only by capture. Assume nothing
  here; it is the cheapest defect to find now and an embarrassing one to ship.

**Exit criterion:** a one-line body-plan statement per creep, backed by numbers, of the kind wave
2.3 produced. Not "it looks like a biped."

### Step 1 results, 2026-08-09 — geometry half done, render half outstanding

Measured on each prefab's LOD0 (the AIStaging `_prepared.fbx`; LOD1/2 live in `Production/LODs`).
Ground band is the bottom 18% of height, the band wave 2.3 used. Columns are XY clusters at 22% of
the footprint diagonal holding at least 8% of the band.

| creep | band % of verts | columns (share of band) | band width / body width | band mass in outer half | sectors of 12 |
| --- | --- | --- | --- | --- | --- |
| revenant | 5.22 | 5 — 29.2 / 21.2 / 17.8 / 17.0 / 9.3 | **0.824** | 51.7% | 12 |
| shade | **2.60** | **1 — 92.7** | **0.347** | 71.4% | **6** |
| swarm | **8.14** | 5 — 42.9 / 18.5 / 14.9 / 12.7 / 11.0 | 0.767 | **24.4%** | 12 |
| wisp | 7.33 | 4 — 28.6 / 25.7 / 24.6 / 21.0 | 0.633 | 58.6% | 12 |

**Settled by geometry — two of four.**

- **Shade: a single stalk, no legs.** One cluster holds 92.7% of a ground band that is itself the
  smallest of the four and only a third as wide as the body, occupying half the sectors. This is
  wave 2.3's runner finding restated — "one stalk, not four columns" — and it means a walk cycle has
  nothing to walk on. **Recommend no rig; `Shimmer` is already the right read.**
- **Swarm: a core with satellites, no gait.** It has the MOST ground-band mass of the four and the
  LEAST of it at the periphery (24.4% against 51-71% for the others), so the mass is central with
  smaller clusters around it. That is a cluster body, and `ConfigureSwarmCluster` already replaces
  the single mesh with shards. **Recommend no rig.**

**Not settled by geometry — the other two need the render check.**

- **Wisp: four columns at 28.6 / 25.7 / 24.6 / 21.0.** Four near-equal columns is the one pattern
  these numbers genuinely cannot read: it is what four legs look like AND what four-fold radial
  symmetry looks like. Real legs usually weight front and back differently; this is almost uniform,
  which leans radial — but leaning is not measuring. **Needs the game-camera render.**
- **Revenant: the widest base of the four (0.824) with 51.7% peripheral mass across five columns.**
  The most leg-like profile here, and the only genuine rig candidate — but five columns is not two,
  so it may be a robe or ash skirt with tendrils rather than a biped. **Needs the game-camera
  render**, and that render decides whether this wave produces any rig at all.

### Step 1 render half, 2026-08-09 — all four settled, and the answer is zero rigs

Rendered from the ACTUAL match camera via `blender_game_camera.render_game_camera_frames`, not
`blender_render_model_preview`, which builds its own camera — using that would repeat the exact
mistake the shared camera module exists to prevent, and which item 4 was filed over. Images kept in
`docs/art-pipeline/wave-2-4-body-plan/`.

- **Revenant — a layered petal/shard mass, no legs.** A closed bundle of overlapping blades under a
  pointed crown. The five columns and 51.7% peripheral mass are the fanned skirt reaching the
  ground, not limbs. The only rig candidate in the wave, and it is not one.
- **Wisp — a crystal orb inside a gyroscopic ring assembly, no legs.** Radially symmetric. The four
  near-equal columns are ring segments, which is what the suspiciously uniform 28.6/25.7/24.6/21.0
  weighting was pointing at. Its prefab also sits at `y: 0.5` — authored half a unit off the ground,
  where Revenant sits at 0. It floats, and the geometry agrees.

**Conclusion: wave 2.4 produces no rigs.** All four creeps are non-walkers — a stalk (shade), a core
with satellites (swarm), a petal mass (revenant) and a floating orb (wisp). None has a limb to
articulate, and rigging would REPLACE the procedural motion each already carries with a walk cycle
for legs that do not exist. That is the Brute's invisible leg rig, four more times.

This is item 11 closed by four answers, exactly as this plan was written to allow. What remains is
not rigging but confirming each creep's motion style is the right one for its body plan — a much
smaller job, and steps 3 and 4 below do not apply.

Wave 2.3 found none of its three had legs. Wave 2.4 found none of its four had legs. Seven creeps
across two waves, and the standing rule caught every one: **the roster is mostly not walkers, and
"unrigged" has never been the same finding as "unanimated"**.

**Method note for whoever runs the render half:** the first clustering pass used a 8% tolerance and
returned 6-8 columns for every creep, which is fragmentation rather than structure — it could not
tell a leg from a lump. The 22% tolerance above separates them, and `band_footprint_ratio` plus
`band_mass_outer_half_pct` are what actually discriminate a stalk from legs. Numbers that agree with
every hypothesis are not evidence; check the spread before trusting a column count.

## Step 2 — decide rig vs. keep procedural, per creep, in writing

For each, answer: *what does a skeletal clip give a player that the current procedural style does
not?* Expected outcomes on current evidence, all of which step 1 may overturn:

- **Swarm** — almost certainly **no rig**. A cluster of shards has no skeleton to pose, and
  `ConfigureSwarmCluster` already hides the single body. Rigging it would mean rigging a mesh the
  renderer replaces.
- **Shade** — likely **no rig**. A walk cycle asserts feet and weight, which contradicts the
  incorporeal read that `Shimmer` and the stealth grouping exist to produce.
- **Wisp** — likely **no rig** if it floats; confirm whether it is tagged flying/air, because that
  decides `Hover` versus `RunnerDart` and the two read very differently at speed 3.
- **Revenant** — the **most likely genuine rig** of the four, and the one to do first if any.

A decision to leave a creep unrigged is a real outcome and closes its share of item 11, provided it
is written down with the measurement behind it. What must not happen is item 11 being left open
forever because "four creeps are unrigged" was recorded as a defect rather than a question.

## Step 3 — rig only what step 2 justifies

Follow `rig_turret_walker.py`'s precedent, restated by wave 2.3: **a script per body plan, splitting
out rather than widening a shared script.** Six exist (`rig_biped_creep`, `rig_quadruped_creep`,
`rig_coiled_serpent`, `rig_bladed_runner`, `rig_turret_walker`, `rig_wheeled_ram`); reuse one only
if the body plan genuinely matches, and add a new one if it does not.

One `Walk` clip each, matching the eleven that already have one.

## Step 4 — measure, do not eyeball

`measure_creep_gait.py`, reporting foot skate against the walker baseline of **51x**. Wave 2.3's
figures were siege 0.99x, serpent 8.34x, runner 11.85x, and it read them honestly: only the siege's
was a gait number, because a wheel's rotation could be **solved** from the creep's real ground speed
(1.3333 u/s, landing 0.016% off) while the other two "have no ground contact pushing them along, so
their figures say carried down the lane, which is true and no clip can change it."

Expect the same for anything that floats here. A bad skate number on a floater is not a failure to
fix; it is a body plan telling you it is not walking.

## Step 5 — verify in a player, not the editor

Two traps this project has already paid for, both of which apply directly:

- **LOD.** `8457192` — "the static LODs froze every unit on device." A newly rigged creep needs its
  animated LOD0 held at gameplay size, or it will animate in the editor and freeze on device. All
  four already have LOD1/LOD2 FBXs, so this is live for this wave, not hypothetical.
- **`PromoteCreep3DSet` silently overwrites committed motion styles** with spec defaults — open item
  41, found during wave 2.3, affecting zephyr, stalker, burrower, warden and colossus. If this wave
  runs the promote step, check the committed `CreepVisualLibrary` afterwards rather than trusting it.

Then a device build. The editor's Scene view camera keeps `Animator`s alive and hides a whole class
of failure that only appears in a player.

## What "done" looks like

Item 11 closed, with each of the four resolved either by a rig and a measured gait figure, or by a
written argument for staying procedural. Four rigs is not the target; four **answers** is.
