# Relay Ward reads flat against the board camera — UNRESOLVED

Reported from play, 2026-08-03: *"relay tower looks off perspective wise… it feels like it is
tilting away from the POV, almost as if it's not quite 3D."*

Reproduced, cause **not** found. This file exists so the next person does not repeat the five
hypotheses already eliminated below.

## What is actually visible

`02-role-contact-sheet.png` is the clearest. Every other tower shows its top surface — Arrow's
base, Control's dome, Pulse's rings, Prism's body all read as three-quarter views. **The Relay
shows almost no top face.** It reads dead-on, like an elevation drawing dropped into a perspective
scene, which is what "not quite 3D" describes.

`01-board-lineup.png` is the same thing in gameplay, under the real near-top-down camera: most
towers sit flat on their cells, a few stand upright and read side-on.

## Ruled out

| Hypothesis | Finding |
| --- | --- |
| Prefab root rotation | Identity on every tower in the roster |
| The ±90° X rotation | Relay has one — but so do Prism and Control, and both read correctly |
| Seating height | Relay's root sits at y 0.4; Barricade and Foundry are at 0.5 and look fine |
| **Flat geometry** | **Bounds `1.05 x 1.30 x 1.05`, thinnest/thickest 0.81 — solid, not planar** |
| Missing ambient occlusion | Only Arrow has an `_OcclusionMap` bound; Control and Prism read fine without one |

The geometry row is the direct test of "not quite 3D" and it came back negative. Whatever this is,
the mesh has real volume.

## The lead worth pulling

**Five towers share byte-identical bounds.** Relay, Gatling, Spore Cloud, Tesla and Repair Drone
are all *exactly* `1.05 x 1.30 x 1.05`. Every tower that reads correctly has distinct bounds:

```
  tower            sizeX  sizeY  sizeZ   thinnest/thickest
  Arrow             1.45   0.90   1.05      0.62    reads correctly
  Control           1.45   1.00   1.45      0.69    reads correctly
  Sapling           1.43   1.30   1.30      0.91    reads correctly
  ElderCanopy       1.29   1.30   1.29      1.00    reads correctly
  Bloomheart        1.26   1.30   1.09      0.84    reads correctly
  ThornSnare        1.26   1.30   1.19      0.92    reads correctly
  Barricade         1.20   1.30   1.20      0.93    reads correctly
  Pulse             1.45   0.70   1.44      0.48    reads correctly

  Relay             1.05   1.30   1.05      0.81    <-- reported
  Gatling           1.05   1.30   1.05      0.81    <-- identical
  SporeCloud        1.05   1.30   1.05      0.81    <-- identical
  Tesla             1.05   1.30   1.05      0.81    <-- identical
  RepairDrone       1.05   1.30   1.05      0.81    <-- identical
  Foundry           1.05   1.30   1.21      0.81    <-- nearly identical
```

Five visually different models landing on the same dimensions to two decimal places is not chance.
Either those bounds are being driven by a shared child object rather than each tower's own mesh, or
those five are not getting their real model at all. The correlation with "looks wrong" is what makes
it worth checking first — it would explain a cluster of towers rather than one.

Worth confirming before acting on it: the probe measured `Renderer.bounds` across the whole prefab,
so a shared base plate or range halo could dominate the number without the model being wrong. That
is the first thing to eliminate.

## How to reproduce

Captures:

```
Unity -batchmode -projectPath <project> \
  -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureRoleContactSheet
Unity -batchmode -projectPath <project> \
  -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureRoleLineupReviewSet
```

**Not** `-nographics` — with it the capture writes a blank grey PNG and still exits 0, which looks
exactly like success. A real capture is ~150KB and up; the blank one was 17KB.

The bounds table came from a throwaway editor script that instantiated each prefab under
`Assets/Prefabs/Towers`, encapsulated every enabled `Renderer.bounds`, and printed the extents. It
was deleted rather than committed; it is about thirty lines to rewrite.

## Why this stopped here

This is art-pipeline work, and that pipeline has an owner with current context — the Prism scale
fix (`ed5e4c7`, 0.62 to 0.79, with recorded reasoning) and the roster-wide AO bake are recent
examples of the same class of change. Adjusting model rotations by trial and error against an
intended orientation that is being inferred rather than known is how four more towers end up wrong.

What would unblock it: knowing whether the Relay is meant to be too **tall**, facing the wrong
**way**, or tilted so its front shows instead of its top. Those are three different fixes — scale,
yaw, pitch — and neither capture disambiguates them.
