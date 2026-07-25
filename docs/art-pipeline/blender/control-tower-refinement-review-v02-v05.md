# Control Tower Blender Refinement Review v02-v39

Date: 2026-07-23  
Status: v39 selected for next Unity review

## Context

The first authored Blender Control tower proved that the repo can produce, clean, import, wrap, validate, and review a real 3D mesh. User feedback was that the result was many revisions worse than the old 2D sprite, despite being 3D.

The refinement goal is therefore incremental fidelity from the source sprite, not merely more geometry.

## Source Sprite Strengths To Preserve

- ornate gold/dark containment ring;
- blue/cyan central crystal;
- translucent containment field;
- strong dark body contrast;
- readable ward-tech control identity;
- premium painted detail without losing mobile silhouette.

## Variants

| Variant | Preview | Verdict |
| --- | --- | --- |
| v02 segmented | `docs/art-pipeline/blender/control_tower_v02_segmented_preview.png` | Selected for Unity review. Best balance of added geometry, darker contrast, and mobile readability. |
| v03 source dense | `docs/art-pipeline/blender/control_tower_v03_source_dense_preview.png` | Too noisy; ring ornaments and extra field lines risk clutter at phone scale. |
| v04 mobile bold | `docs/art-pipeline/blender/control_tower_v04_mobile_bold_preview.png` | Readable but too simplified; not enough fidelity gain over v01. |
| v05 gold crown | `docs/art-pipeline/blender/control_tower_v05_gold_crown_preview.png` | Added gold, but the caps read too blocky/cube-like and less elegant than the sprite. |
| v06 faceted crown | `docs/art-pipeline/blender/control_tower_v06_faceted_crown_preview.png` | Selected for next Unity review. Better source-sprite crown language than v02 while avoiding v05's cuboid caps. |
| v07 integrated crown | `docs/art-pipeline/blender/control_tower_v07_integrated_crown_preview.png` | Selected for next Unity review. Keeps v06's fidelity but embeds the crown plates into the ring so the top reads more engineered and less like floating ornaments. |
| v08 ring inlay | `docs/art-pipeline/blender/control_tower_v08_ring_inlay_preview.png` | Cleaner ring inlays, but lost some source-sprite ornament character. |
| v09 crystal cage | `docs/art-pipeline/blender/control_tower_v09_crystal_cage_preview.png` | Stronger central crystal/core treatment; good step, but dome/visual density is still a little high. |
| v10 premium balanced | `docs/art-pipeline/blender/control_tower_v10_premium_balanced_preview.png` | Selected. Best balance of v07 ring fidelity, v09 core polish, quieter dome, and mobile readability. |
| v11 material premium | `docs/art-pipeline/blender/control_tower_v11_material_premium_preview.png` | Added richer value/material cues but made the ring too busy. Not selected. |
| v12 sprite fidelity | `docs/art-pipeline/blender/control_tower_v12_sprite_fidelity_preview.png` | Made the crystal visible but overcorrected into a giant cyan spike. Not selected. |
| v13 material clean | `docs/art-pipeline/blender/control_tower_v13_material_clean_preview.png` | Previous selected pass. Keeps v10's cleaner silhouette while adding stronger material/value cues without v11 clutter or v12 proportion drift. |
| v14 fortress ring | `docs/art-pipeline/blender/control_tower_v14_fortress_ring_preview.png` | Much stronger ring mass and source-sprite jewel housings. Good direction, but the center still reads too softly at tower scale. |
| v15 hero crystal | `docs/art-pipeline/blender/control_tower_v15_hero_crystal_preview.png` | Central crystal finally becomes readable. However, the tall blue spike starts competing with the ring instead of feeling suspended inside it. |
| v16 front identity | `docs/art-pipeline/blender/control_tower_v16_front_identity_preview.png` | Adds a face/lens and visible front direction. Useful idea, but the asymmetry and loose side pieces create clutter in the gameplay silhouette. |
| v17 arcane machine | `docs/art-pipeline/blender/control_tower_v17_arcane_machine_preview.png` | Selected. Best aggressive pass: stacked orbit rings, vertical cage rails, stronger controlled-machine read, and less clutter than v18. |
| v18 boss silhouette | `docs/art-pipeline/blender/control_tower_v18_boss_silhouette_preview.png` | Most aggressive, but too busy. The oversized crown/pylons make it read like a chandelier/landmark instead of a mobile-scale tower. Not selected. |
| v19 sprite arc refit | `docs/art-pipeline/blender/control_tower_v19_sprite_arc_refit_preview.png` | Built from v17. Improved sprite-faithful ring shields, carved dark ring panels, and taller gold restraint ribs. Center/base still lacked the sprite's faceted richness. |
| v20 crystal base refit | `docs/art-pipeline/blender/control_tower_v20_crystal_base_refit_preview.png` | Built from v19. Added blue/violet/white crystal facets, darker socket, front gem pedestal, and seated side pylons. Stronger source read, but still needed painted value separation. |
| v21 painted depth refit | `docs/art-pipeline/blender/control_tower_v21_painted_depth_refit_preview.png` | Selected. Built from v20. Adds black cuts, hot gold bevel strokes, blue underside glow, subtler glass rim, and base panel value separation without reverting to v18 clutter. |
| v22 elegant gold ribs | `docs/art-pipeline/blender/control_tower_v22_elegant_gold_ribs_preview.png` | Built from v21. Re-referenced the sprite's arched gold restraint ribs and added stronger dark/gold rib sockets so the mid-body reads less like straight rails. |
| v23 ring gem polish | `docs/art-pipeline/blender/control_tower_v23_ring_gem_polish_preview.png` | Built from v22. Re-referenced the sprite's front/back pendant hierarchy and added a larger front ring gem plus smaller cyan gem cases around the ring. |
| v24 sprite scale unify | `docs/art-pipeline/blender/control_tower_v24_sprite_scale_unify_preview.png` | Selected. Built from v23. Re-referenced the sprite's lower blue field, front bridge/gem, and dark base buttresses to unify the composition after v23 became top-heavy. Corrected from purple-heavy to blue-field base before promotion. |
| v25 painted panel cuts | `docs/art-pipeline/blender/control_tower_v25_painted_panel_cuts_preview.png` | Built from v24 using the token-efficient process. Added low-cost decal-like black/gold cuts to the ring/base to mimic the sprite's painted bevel/detail language without adding new bulk. |
| v26 gem glow focus | `docs/art-pipeline/blender/control_tower_v26_gem_glow_focus_preview.png` | Built from v25. Intended to add controlled gem focal glints, but first preview exposed an over-noisy pylon halo/UI-marker read. The generator was trimmed before v27 so this regression does not carry forward. |
| v27 mobile finish trim | `docs/art-pipeline/blender/control_tower_v27_mobile_finish_trim_preview.png` | Selected. Built from corrected v26. Keeps v25's material cuts, subdues gem glints, lowers dome haze, and adds final base/ring silhouette strokes for mobile readability. |
| v28 ring wall depth | `docs/art-pipeline/blender/control_tower_v28_ring_wall_depth_preview.png` | Selected. Built from v27. Best pass in this cycle: adds chunky carved dark/gold top-ring wall depth so the ring reads closer to the source sprite without adding loose ornament noise. |
| v29 crystal refraction lines | `docs/art-pipeline/blender/control_tower_v29_crystal_refraction_lines_preview.png` | Built from v28. Did not visibly move the needle from this camera because the ring/dome hide most crystal facet detail. Not selected. |
| v30 source readability lock | `docs/art-pipeline/blender/control_tower_v30_source_readability_lock_preview.png` | Built from v29. Front-axis idea is valid, but the preview does not beat v28; it risks adding lower-front clutter without enough source-fidelity gain. Not selected. |
| v31 crown material focus | `docs/art-pipeline/blender/control_tower_v31_crown_material_focus_preview.png` | Built from v28. Valid target, but too subtle from the preview angle; did not beat v28. Not selected. |
| v32 field edge cleanup | `docs/art-pipeline/blender/control_tower_v32_field_edge_cleanup_preview.png` | Built from v31. Trimmed field/ring noise after the first attempt cluttered the lower read. Still did not clearly beat v28. Not selected. |
| v33 mobile gold read | `docs/art-pipeline/blender/control_tower_v33_mobile_gold_read_preview.png` | Built from v32. Added too much lower-ring clutter and risked noisy phone-scale read. Not selected. |
| v34 visible crown gold | `docs/art-pipeline/blender/control_tower_v34_visible_crown_gold_preview.png` | Built from v28. Cleaner than v33, but too timid; visible crown still read too cream/ivory rather than amber gold. Not selected. |
| v35 warm gold grade | `docs/art-pipeline/blender/control_tower_v35_warm_gold_grade_preview.png` | Built from v28. Material-grade pass made the gold warmer, but the improvement was too small to justify promotion by itself. Not selected. |
| v36 source panel value | `docs/art-pipeline/blender/control_tower_v36_source_panel_value_preview.png` | Built from v35. Restored the source sprite's dark segmented crown-panel direction, but added white pebble-like gem noise. Not selected. |
| v37 cyan panel read | `docs/art-pipeline/blender/control_tower_v37_cyan_panel_read_preview.png` | Built from v35/v36. Kept the darker crown segmentation while converting new inset marks to cyan. Better direction, but inherited white caps still looked chalky. Not selected. |
| v38 blue crystal grade | `docs/art-pipeline/blender/control_tower_v38_blue_crystal_grade_preview.png` | Selected. Built from v37. Keeps the darker segmented crown-panel read, preserves v28's stable silhouette, and shifts inherited white crystal highlights toward cyan-blue so the tower is closer to the source sprite without adding more loose geometry. |
| v39 clear read | `docs/art-pipeline/blender/control_tower_v39_clear_read_preview.png` | Selected. Built from v38 after Unity testing showed a grain/fuzzy read. Keeps v38's solid geometry and cyan crystal grade, but removes the broad translucent dome haze so the tower reads cleaner on the dark lane board. |

## Selected Candidate

Use:

```text
unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_prepared_v39_clear_read.fbx
```

Unity intake spec now points at v39 for `tower.control`.

## Next Refinement Direction

If v28 still fails in-game, the next pass should not add random blocks. It should specifically:

- tune scale/rotation in the live lane view before adding more geometry;
- reduce dome/ring overlay only if it hides creeps/grid;
- compress vertical height only if the top ring blocks HUD/gameplay readability;
- consider a real texture/decal pass for ring panel cracks and gold bevel highlights instead of more physical cubes;
- keep v19-v30 as the pattern for future work: each pass must name the sprite mismatch it fixes and inspect the preview before the next pass;
- treat v29/v30 as evidence that more micro-detail is currently lower ROI than camera/scale/material tuning.
