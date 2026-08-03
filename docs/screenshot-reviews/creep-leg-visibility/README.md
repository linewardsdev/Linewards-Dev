# Creep leg visibility at the shipped game camera

Date: 2026-07-31
Unity: 6000.5.3f1
Capture: `LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureRoleLineupReviewSet`

Applies OPEN_ITEMS item 4's standing rule — *render-check from the actual game camera
before investing in leg articulation* — to the whole roster at once, and turns it from a
per-creep judgement call into a number.

> **Corrected 2026-08-03 — the two constants below are wrong and the height column is roughly
> 2x too large.** The camera is not the one at `orthographicSize = 15.5` (that is
> `LocalVerticalSliceLauncher`'s bootstrap camera); the match camera is
> `UnityVerticalSliceRenderer.Camera.cs` at 9.2 scaled by tilt compensation, and a real
> capture's grid measures **~113 px per world unit**, not 61.9. And meshes are not normalised
> to 1.25 units tall — the intake normalises the LARGEST dimension to 0.900, whatever axis
> that is, so `UnitBoundsReport` measures siege at 0.506 tall, runner 0.448, swarm and shade
> 0.750. With height projecting at sin(30) = 0.5, siege is **44 px** and not 105.
> The ORDERING below is unaffected, since every row is scaled by the same two constants, so
> the finding and the recommended sequence both still hold. Full working, and what it implies
> about which axis of motion to spend on, in
> [`../creep-rigs-wave-2-3/`](../creep-rigs-wave-2-3/).

## The measurement

The match camera is orthographic at `orthographicSize = 15.5`, and captures are 1080x1920.
So the visible world height is `2 x 15.5 = 31` units across 1920 pixels:

**61.9 pixels per world unit.**

Creep meshes are normalised to ~1.25 units tall by the intake, then scaled by
`CreepVisualLibrary`. On-screen height follows directly. Leg-segment and swing figures
below apply the rough proportions of a walking biped — a thigh-plus-shin is around a third
of standing height, and a walk cycle swings it through roughly a third of its own length —
so treat those two columns as order-of-magnitude, not measurement. The height column is
exact.

| Creep | Library scale | Height (px) | Leg segment (px) | Leg swing (px) | Animator |
| --- | ---: | ---: | ---: | ---: | --- |
| siege | 1.360 | 105 | 35 | 12 | **no** |
| obsidian_brute | 1.230 | 95 | 31 | 11 | yes |
| serpent | 1.230 | 95 | 31 | 11 | **no** |
| runner | 1.080 | 84 | 28 | 10 | **no** |
| brute | 1.070 | 83 | 27 | 10 | yes |
| revenant | 1.050 | 81 | 27 | 9 | **no** |
| shade | 0.990 | 77 | 25 | 9 | **no** |
| turret_walker | 0.960 | 74 | 25 | 9 | yes |
| swarm | 0.820 | 64 | 21 | 7 | **no** |
| wisp | 0.680 | 53 | 17 | 6 | **no** |
| warden | 0.428 | 33 | 11 | 4 | yes |
| colossus | 0.329 | 26 | 8 | 3 | yes |
| zephyr | 0.278 | 22 | 7 | 3 | yes |
| burrower | 0.245 | 19 | 6 | 2 | yes |
| stalker | 0.208 | 16 | 5 | 2 | yes |

## The finding: the animation investment is inverted

**Creeps that HAVE an animator average 46 px tall. Creeps that have none average 80 px.**

The five smallest creeps in the game — stalker, burrower, zephyr, colossus, warden, at 16
to 33 pixels — all have rigs and walk clips, and a leg on them swings through **2 to 4
pixels**. That is at or below the point where it can read as articulation rather than as
noise, before any consideration of whether the leg is occluded at all.

Meanwhile three of the five largest creeps — siege at 105 px, serpent at 95, runner at 84 —
have no animator at all.

This is not an argument that the existing rigs were wasted; they were built for specific
creeps for specific reasons, and the Turret Walker check in item 4 was done properly. It is
an argument about where the NEXT increment of animation effort goes, and it says: not
where item 11's list implies.

## What this means for item 11

Item 11 reads as "seven creeps have no animation, give them some". Rigging seven static
Meshy exports is a multi-session art initiative — these meshes have no armature at all, so
each needs a rig built and skinned before a clip can exist.

The ordering that falls out of the table above:

1. **siege, serpent, runner** — largest, no animator, best return per rig.
2. **revenant, shade** — mid, worth doing after.
3. **swarm, wisp** — smaller and abstract in silhouette; body/drift motion probably reads
   better than legs regardless of rig quality.

And the standing rule gains a threshold rather than staying a case-by-case judgement:
**below roughly 35 px on-screen height, leg articulation is not the lever — body, tilt and
silhouette motion are.** All fifteen creeps already carry per-creep procedural motion
through `CreepMotionProfile`, which is what is currently doing that job.

## Caveat

Occlusion still has to be checked per creep, exactly as item 4 says. Height sets a ceiling
on how much a leg COULD read; a shell that overhangs to the ground — the original Brute
finding — takes it to zero regardless of size. This table narrows where to look, it does
not replace looking.
