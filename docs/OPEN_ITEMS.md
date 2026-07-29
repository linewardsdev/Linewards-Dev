# Open Items

Known gaps and decisions left after the graphics overhaul and URP migration
(2026-07-25 to 2026-07-26). The full history that produced these — diagnosis,
migration phases, the tower material-assignment bug and its fix — lived in
`URP_MIGRATION.md` and `GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md`, both now
retired since the work they tracked is done; recover that detail from git
history (`git log --all --full-history -- docs/URP_MIGRATION.md`) if it's
ever needed again.

## 1. `_EMISSION` keyword loss on the five tower body materials — now self-healing, root cause still unknown

`mat_tower_{arrow,control,relay,pulse,prism}_3d_body_runtime_v01.mat` lost
their `_EMISSION` shader keyword and went silently non-emissive repeatedly
(at least 4 times) across two sessions, from a trigger that was never
isolated — it recurred from a plain compile-only Editor pass with no
material-touching code involved, and from ordinary batchmode capture runs.
Manually re-enabling it after each occurrence stopped scaling.

**Fixed with a guard, not a root-cause fix**: `Assets/Editor/TowerEmissionKeywordGuard.cs`
runs on every asset import pass (`AssetPostprocessor.OnPostprocessAllAssets`)
and silently re-enables `_EMISSION` + resets `globalIlluminationFlags` to
`.None` on any of the 5 materials found wrong. Verified working by
deliberately stripping the keyword on Control's body material and confirming
the guard corrected it within the same batchmode pass (visible in the log as
`TowerEmissionKeywordGuard: re-applied the _EMISSION fix to 1 tower body
material(s).`).

This means the symptom can no longer ship broken, but the actual trigger —
what in Unity's import/reimport pipeline keeps stripping this keyword in the
first place — is still unknown. If tower emission ever looks wrong despite
the guard, check the Editor log for repeated `TowerEmissionKeywordGuard`
corrections firing every session, which would mean something is fighting it
faster than expected; that would be the signal to actually root-cause this
rather than lean on the guard indefinitely.

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

## 4. Leg rigs on shell-bodied creeps may be invisible from the game camera — check before investing further

Building a two-segment (thigh+shin) leg rig for the Brute revealed that its
armor shell overhangs to the ground on every side, fully hiding the legs from
the actual ~30-degree top-down game camera — confirmed by rendering the rig
from that exact camera angle (not the eye-level angle `rig_quadruped_creep.py`
defaults its verification renders to), where zero leg geometry is visible at
any frame of the walk cycle. The knee-bend rig itself works and is kept
(Unity-verified, 10/11 bones driven, no regression to the existing silhouette
timing), but it produces no visible gameplay difference for this creature.

Follow-up (2026-07-28): the Spire Turret Walker was checked from the game camera this
way and its legs turned out fully visible, unlike the Brute's — but the check surfaced two
different defects instead (51x foot skate, and a symmetric trot whose mirrored contact
poses are indistinguishable head-on). It now has its own rig script,
`tools/art_pipeline/rig_turret_walker.py`; see `docs/GD_TUNING_LOG.md`. The camera-angle
check below remains the right first step and paid for itself again.

If `rig_quadruped_creep.py` is ever reused for another quadruped-shaped creep,
render-check from the actual game camera angle first, before investing in leg
articulation — for a low, wide, or heavily-armored silhouette the legs may
again be fully occluded, in which case body/head motion (bob, rock, and a
rigid uniform scale pulse — added for the Brute in this same pass) is the only
lever that will actually read to a player.
