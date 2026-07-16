# V1 Art Fast Track

Date: 2026-07-15

## Benchmark

Arrow v06 and Runner v07 define V1 for this art push.

V1 does not mean final shipped art. It means the object no longer reads as a primitive placeholder and is good enough to support gameplay iteration while we keep improving the game.

Current benchmark assets:

- Arrow: `Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed.png`
- Arrow prefab: `Assets/Prefabs/Towers/Tower_Arrow_AIPlate.prefab`
- Runner: `Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v07_trimmed.png`
- Runner prefab: `Assets/Prefabs/Creeps/Creep_Runner_AIPlate.prefab`

## What Worked

- Generate a role-specific production sprite, not a generic square source plate.
- Use exaggerated shapes that survive phone-scale gameplay.
- Favor one or two dominant silhouette features over small surface detail.
- Use chroma-key generation, local alpha removal, and a normalized 1024x1024 transparent production sprite.
- Wire the painted sprite to `AIPlateVisual`.
- Keep required contract children present, but render-disabled, so primitive scaffolding does not cover the art.
- Tune scale live in Unity after seeing overlap in active lane.

## What Did Not Work

- Directly using early square source plates in runtime prefabs.
- Letting primitive `Body`, `RoleMarker`, or support meshes render on top of the painted plate.
- Over-detailing the image before confirming the silhouette.
- Treating contact-sheet selection as implementation-ready art.
- Trying to judge final scale outside the real lane camera.

## Fast-Track Recipe

For each remaining role:

1. Use the already-selected contact-sheet candidate as direction, not as final source.
2. Generate one compact production sprite on a flat chroma-key background.
3. Make the prompt role-specific and V1-sized:
   - broad, readable silhouette;
   - exaggerated landmark shape;
   - bright role core;
   - fewer tiny details;
   - top-down three-quarter board token;
   - no background, shadow, UI, text, trail, or baked gameplay effect.
4. Remove chroma key to alpha.
5. Trim to visible silhouette and normalize onto a 1024x1024 transparent canvas.
6. Create a grayscale copy.
7. Build a sprite-only proof prefab with required contract children render-disabled.
8. Promote to the visual library only after the sprite beats the current placeholder at lane scale.
9. Tune the `AIPlateVisual` scale in live Unity.
10. Add a source note and generation-log entry.

## Target Role Landmarks

Towers:

- Control: wide containment dish/ring, suspended core, restraint arcs.
- Relay: tall beacon mast, antenna crown, capacitor fins.
- Pulse: heavy drum/reactor, shock rings, pressure vents.
- Prism: tall crystal/lens spire, beam aperture, focus facets.

Creeps:

- Brute: broad armored shell, heavy glowing core, squat tank read.
- Swarm: clustered shardlings, multiple small bodies as one token.
- Shade: dark glass/facet body, ghost echo outline, shimmer cue.
- Siege: directional ram/barrel, warning plate, forward impact nose.

Builder:

- Friendly worker/tool user, construction wand or signal tool, readable as player-controlled and non-combat.

## Recommended Execution Order

1. Control and Brute.
   These are the clearest opposites: wide tower dish and chunky tank creep.
2. Relay and Siege.
   Beacon/support tower plus directional ram creep should produce strong silhouettes quickly.
3. Pulse and Swarm.
   These need pressure/effect language but must avoid visual soup.
4. Prism and Shade.
   These are highest risk because both can become thin, dark, or too subtle.
5. Builder.
   Do after the combat roles so the builder can be visually distinct from both towers and creeps.

## Scale Starting Points

Use these as first guesses only; live Unity overlap wins.

- Towers: start `AIPlateVisual` around `0.145` to `0.165`.
- Large/chunky towers: start closer to `0.145`.
- Tall/narrow towers: start closer to `0.16`.
- Creeps: start around `0.20` to `0.24`.
- Swarm: start lower if the cluster spreads wider than one lane cell.
- Builder: start between creep and tower scale; it must be findable but not look like a tower.

## Acceptance Gate

A V1 role is done when:

- it reads without labels in active lane view;
- it does not overlap adjacent cells badly;
- it remains recognizable in grayscale;
- it does not hide the path, placement grid, health/pressure cues, or HUD;
- its menu icon can plausibly be cropped from the same source art;
- provenance/source notes are recorded.

## Immediate Next Work

- Batch-generate Control, Relay, Pulse, Prism, Brute, Swarm, Shade, Siege, and Builder production sprites using this recipe.
- Build one generalized AI plate prefab generator instead of hand-patching one prefab at a time.
- Promote in two-role batches, then live-scale them before moving to the next batch.
