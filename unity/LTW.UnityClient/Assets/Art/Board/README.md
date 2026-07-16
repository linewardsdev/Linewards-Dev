# Board Art

Source art, material references, and concept notes for the lane board belong here.

Pipeline source of truth: `docs/art-pipeline/ui-board-art-pipeline.md`.
Execution checklist: `docs/art-pipeline/ui-board-pipeline-checklist.md`.

The first board material set should cover:

- Deep field/backplate.
- Buildable side bands.
- Center route core.
- Route edge guides.
- Spawn gate.
- Leak/life-loss gate.
- Lane ownership rails and accents.

Keep board materials quiet enough that towers, creeps, placement states, and leak feedback remain the brightest gameplay information.
Do not add noisy texture detail inside playable cells until heavy-send screenshots pass readability review.

## Runtime Material Baseline

The current first pass is procedural in `UnityVerticalSliceRenderer` so it can ship before authored board meshes/textures exist. It adds deterministic tile value variation, restrained route wear, route edge chips, build-band seams, small plate cracks, endpoint plate marks, and rail/gate contact shadows.

V01 UI/board polish adds thin build-band edge lines, a quiet center-route inlay, and endpoint chevrons. These are still procedural board-material cues, not final authored textures.

Authored replacements should preserve the same readability hierarchy:

- Route wear should be visible at phone size but darker than creeps, shots, health bars, and HUD decisions.
- Build bands should read as placeable side zones through seams and value, not bright labels.
- Spawn and leak plates should be understandable from color, gate shape, and plate markings even when labels are hidden.
- Rails and gutters should ground the lane without competing with ownership accents.
- Endpoint chevrons should clarify spawn/life-loss direction without becoming brighter than active creeps or shots.
