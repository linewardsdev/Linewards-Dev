# URP Migration Baseline

Full capture set taken from `main` on 2026-07-26, immediately before the `urp-migration`
branch was opened. Captured with `Line Wars/Review/Capture Visual Review Set` under Unity
6000.5.3f1.

These fifteen frames are the reference every migration comparison is made against. They are
committed deliberately, despite the repository's history of screenshot bloat, because a
pipeline change cannot be judged from memory and the older Tier 1 captures predate the
creep, board and role colour fixes.

State captured here:

- Built-in Render Pipeline, linear colour space
- Three-point runtime light rig with gradient ambient
- Board baked to one vertex-coloured mesh per lane, `BoardSurfaceLift` at 0.35
- Five 3D creeps at measured scales, health bars placed from measured body height
- Tower role colour unified through `TowerRolePalette`, per-role emission on body materials

If the migration is abandoned, this folder can be deleted with the branch.
