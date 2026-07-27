# Open Items

Known gaps and decisions left after the graphics overhaul and URP migration
(2026-07-25 to 2026-07-26). The full history that produced these — diagnosis,
migration phases, the tower material-assignment bug and its fix — lived in
`URP_MIGRATION.md` and `GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md`, both now
retired since the work they tracked is done; recover that detail from git
history (`git log --all --full-history -- docs/URP_MIGRATION.md`) if it's
ever needed again.

## 1. `_EMISSION` keyword loss on the five tower body materials

`mat_tower_{arrow,control,relay,pulse,prism}_3d_body_runtime_v01.mat` have
lost their `_EMISSION` shader keyword and gone silently non-emissive twice
this session, from a trigger that was never isolated — once from a
`MaterialGlobalIlluminationFlags.EmissiveIsBlack` setting (fixed by setting
it to `.None`), and once again afterward from a plain compile-only Editor
pass with no material-touching code involved. Both times it was caught only
by explicitly grepping the `.mat` files for `_EMISSION`, not by looking at a
render.

**Before relying on tower emission rendering correctly, check:**

```bash
grep _EMISSION unity/LTW.UnityClient/Assets/Art/Towers/Production/Materials/mat_tower_*_3d_body_runtime_v01.mat
```

Each of the 5 files should list `_EMISSION` once. If any are missing it,
re-enable it and set `globalIlluminationFlags = MaterialGlobalIlluminationFlags.None`
via an Editor script, save, and re-check after a fresh Editor relaunch — do
not assume it will hold.

A real fix likely needs either a `MaterialPostprocessor.OnPostprocessAllAssets`
hook that force-corrects this on every import of these five assets, or
actually root-causing what re-triggers Unity's keyword sync. Neither has been
attempted.

## 2. Bloom cost on a physical Android device is unmeasured

Bloom and tonemapping were tuned and verified in the Editor and via headless
capture only. Nobody has profiled the post-processing cost on a real Android
device. Deferred deliberately — no device access during this work — and not
urgent; revisit before shipping.

## 3. No drop has a normal map (decision needed, not a bug)

Meshy was never asked to generate normal maps for any of the 5 towers or 5
creeps, so all ten currently render flatter than they could. The intake
scorecard already reports this (`has_normal_map` check in
`tools/art_pipeline/ai_asset_intake.py`), so it won't pass unnoticed. Two
options, neither pursued:

- Re-run Meshy generation for a given asset with normal maps requested (costs
  generation credits).
- Fake one from the existing albedo via a height-derived bake (Blender can do
  this) — a real visual/artistic tradeoff, not attempted without a look first.
