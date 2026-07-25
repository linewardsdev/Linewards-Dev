# Reference-Sprite Chained Refinement Process

Date: 2026-07-23

## Purpose

Use this process when improving a generated 3D tower from a strong 2D reference sprite. The goal is not to produce several unrelated variants. The goal is a chain where each pass builds from the last pass and fixes a specific mismatch against the reference.

## Rule

Every pass must explicitly reference the source sprite before changing geometry.

For the Control tower, the source sprite is:

```text
unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_control_source_plate_v03.png
```

## Loop

1. Inspect the current 3D preview next to the source sprite.
2. Name the top sprite mismatch in plain language.
3. Add one focused geometry/material layer that fixes that mismatch.
4. Generate source FBX, prepared FBX, and preview PNG.
5. Inspect the new preview against the same sprite.
6. Use the new preview as the base for the next pass.
7. Promote only the final pass unless an intermediate pass is visibly better.

## Token-Efficient Improvement Steps

Use these rules to avoid spending effort on impressive-looking but low-value variations:

1. Stop making random variants. Use the chained process only.
2. Cap each pass to one visual objective, such as “fix front pendant hierarchy” or “reduce dome noise.”
3. Put silhouette in geometry and polish in material/decal treatment. Do not model tiny scratches, bevel glints, or gem sparkle as heavy geometry unless they change the gameplay read.
4. Start each cycle by classifying the missing sprite feature:
   - silhouette issue;
   - material/value issue;
   - scale/composition issue;
   - live Unity camera issue.
5. Run shorter cycles:
   - inspect sprite/current preview;
   - make one patch;
   - generate one preview;
   - inspect;
   - promote only after two or three chained passes.

## Current Best Next Move

After v24, the broad tower silhouette is closer. The remaining gap is mostly painted premium finish, not raw mass. Future passes should prefer:

- broad color/value strips;
- controlled cyan/gold glow accents;
- darker material cuts;
- low-cost panel/decal-like geometry;
- fewer free-floating blocks.

## Pass Discipline

Good pass notes:

- “v19 built from v17: ring jewel shields were too blocky and not sprite-faithful, so this adds sharper gold shield housings and carved dark ring panels.”
- “v20 built from v19: center crystal still lacked blue/violet/white faceting, so this adds layered crystal shards and a darker socket.”

Bad pass notes:

- “Added more details.”
- “Made it cooler.”
- “Generated three options.”

## Control Tower Current Chain

The successful pattern started at v17 and continued:

| Pass | Built From | Sprite Mismatch Fixed |
| --- | --- | --- |
| v19 sprite arc refit | v17 | Ring shields, dark ring cuts, and gold restraint ribs were weaker than the sprite. |
| v20 crystal base refit | v19 | Crystal/base lacked the sprite's faceted blue/violet/white core and front gem pedestal. |
| v21 painted depth refit | v20 | Model lacked painted value separation: black cuts, hot gold bevels, and blue underglow. |

## Promotion Checklist

- [ ] Final pass source FBX exists in `Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/`.
- [ ] Final pass prepared FBX exists in the same folder.
- [ ] Preview PNG exists in `docs/art-pipeline/blender/`.
- [ ] `Tower3DProofSetGenerator` points at the final prepared FBX.
- [ ] Unity wrapper generation passes.
- [ ] Unity wrapper validation passes.
- [ ] `TowerVisualLibrary` points `tower.control` at `Tower_Control_3D.prefab`.
- [ ] Review doc records each chained pass and why it changed.
