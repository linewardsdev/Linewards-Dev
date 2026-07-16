# AI Runner Candidate V05/V06 Review

Date: 2026-07-15

## Why This Pass Exists

The v04 Runner proof was contract-safe but too dark/magenta-heavy in active-lane captures. This pass tested two cleaner Runner variants:

- v05: brighter cyan and cleaner material read, but too wide/chunky for a fast creep.
- v06: long, narrow cyan dart silhouette with better Runner role language.
- v06b/v06c: local cleanup passes to remove magenta fringe and push the gameplay read toward teal/cyan.

## Assets

- v05 source key: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_runner_candidate_v05_key.png`
- v05 alpha: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_runner_candidate_v05_alpha.png`
- v05 trimmed: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v05_trimmed.png`
- v06 source key: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_runner_candidate_v06_key.png`
- v06 alpha: `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/ProductionCandidates/creep_runner_candidate_v06_alpha.png`
- v06 trimmed: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v06_trimmed.png`
- v06c tuned: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v06c_trimmed.png`

## Review Evidence

- Source comparison: `runner-v04-v05-v06-review-sheet.png`
- Color cleanup comparison: `runner-v06-v06b-v06c-color-review-sheet.png`
- Unity proof contact sheet: `01-role-contact-sheet.png`
- Unity proof contact sheet grayscale: `grayscale/01-role-contact-sheet.png`
- Active-lane proof capture: `01-ai-v04-active-lane.png`
- Active-lane proof capture grayscale: `grayscale/01-ai-v04-active-lane.png`
- Active-lane zoom diagnostic: `active-lane-v06c-zoom-sheet.png`

## Result

- v06 beats v05 for Runner role language because it is long, narrow, and directional.
- v06c is the current staged proof sprite because it removes most of the magenta/purple read that hurt v04/v06 in active-lane review.
- The proof generator now forces generated production PNGs to import as Unity sprites before prefab generation.
- `Creep_Runner_AIPlate.prefab` now uses `creep_runner_candidate_v06c_trimmed.png`.
- The AI proof active-lane capture now suppresses gameplay health/readability overlays, clears stale prefab pools, and captures Runner-only pressure so combat/projectile effects do not hide the creep silhouette.
- Validation passed via `ValidateAiSourcePlateProofPrefabs`.
- Runtime visual libraries were not changed by proof generation.
- Runtime promotion was later approved by the user and applied manually to `TowerVisualLibrary.asset` and `CreepVisualLibrary.asset`.

## Gate State

- Source/tiny-scale review: v06 pass.
- Unity contact-sheet review: v06c pass with scale-tuning caveat.
- Grayscale contact-sheet review: v06c pass.
- Active-lane review: improved. Runner-only proof now shows the sprite without combat clutter, but final scale still needs one more tune before runtime promotion.
- Runtime promotion: approved and applied for `tower.arrow` and `creep.runner`.

## Next

After runtime promotion, do one final scale pass against real gameplay pressure with overlays restored deliberately. The source art is close enough to test live, but proof scale and runtime overlay behavior still need to be balanced together.
