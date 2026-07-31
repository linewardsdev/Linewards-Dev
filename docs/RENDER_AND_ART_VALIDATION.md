# Render And Art Validation

Every check in this document exists because something was wrong in a way that produced no
error, no warning, and no obviously broken frame — only a slightly worse image, which is
indistinguishable from an art problem unless you know to look at the asset.

That is the pattern this file is about. A render setting cannot be trusted to announce its
own failure, so each one is asserted by a script that exits non-zero, and each assertion is
proven to fail as well as to pass before it is believed.

## Why this is not in `OPEN_ITEMS.md`

`OPEN_ITEMS.md` deletes an item when its work lands. That is the right rule for a tracker
and the wrong home for the knowledge the work produced, so the durable half lives here and
the tracker keeps only what is still open.

---

## The validators

All run headless and exit non-zero on failure, so any of them can gate a build. Replace
`<method>` in:

```bash
/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -nographics \
  -projectPath unity/LTW.UnityClient \
  -executeMethod <method> \
  -logFile unity/LTW.UnityClient/Logs/validate.log
```

| Method | Asserts | Catches |
| --- | --- | --- |
| `LTW.UnityClient.Editor.UrpPostProcessingSetup.ValidateProfile` | The volume profile has exactly 3 non-null overrides, and they are specifically Tonemapping, Bloom and ColorAdjustments | The profile shipping with three null sub-assets — no tonemapper, no bloom — which it did for the entire life of the URP migration |
| `LTW.UnityClient.Editor.RenderSetupValidation.ValidateRenderSetup` | Soft shadows on, HDR colour grading, HDR colour buffer, MSAA ≥ 2, main light shadows, and LOD cross-fade consistent with whether any LODGroup exists | Settings that silently degrade the image; code requesting soft shadows while the pipeline strips the variant |
| `LTW.UnityClient.Editor.TowerBodyMaterialTuning.ValidateTuning` | All 15 tower bodies at the shared smoothness, every emission above the bloom threshold, reflections enabled | Bodies drifting back to dead matte, or emission set below the threshold where it cannot bloom at all |
| `LTW.UnityClient.Editor.CreepBodyMaterialTuning.ValidateTuning` | All 15 creep bodies likewise; reports by name any creep with no emission map | The emission multiplier falling to 1.0, which makes blooming arithmetically impossible against an LDR map |
| `LTW.UnityClient.Editor.QualityTierSetup.ValidateTierAssets` | Every quality level has a pipeline asset, **and** the tiers are not all the same asset | Six tiers that all resolve to one URP asset, so a budget phone renders what a flagship does |

Two more run under plain Python, no Unity:

| Command | Asserts |
| --- | --- |
| `python3 tools/art_pipeline/audit_intake_scores.py [--strict]` | Reads every intake `score.json` back and reports the roster's state; flags scorecards written before the `has_normal_map` fix |
| `python3 tools/art_pipeline/validate_role_coverage.py [--strict]` | Every asset path in the role coverage report resolves; `--strict` also fails on roles with no production reference |

**Both are expected to fail under `--strict` today**, and that is deliberate rather than
neglect — see "Gates that are off on purpose" below.

## Exit codes

`0` pass, `1` hard failure, `2` "needs review" under `--strict` for the two Python tools.

---

## The invariants, and why each one matters

### Bloom threshold 1.05 is a shared constant, not a local one

Three separate systems are tuned against it and will silently stop working if it moves:
`UrpPostProcessingSetup` authors it, `TowerBodyMaterialTuning` checks every tower emission
peak against it, and `CreepBodyMaterialTuning` checks the creep multiplier against it.

**Below the threshold, emission is not dim — it is absent.** Ten tower bodies sat at a
0.05 peak and could not bloom at any bloom intensity; all fifteen creeps sat at a 1.0
multiplier against LDR maps, so their product could never exceed 1.0. Neither looked
broken. Both were.

### Surface response is one number across towers and creeps

`_Smoothness` is **0.45** on all thirty bodies, and both tuning files carry it.

Chosen from measurement rather than taste: the baked metallic-gloss maps average 0.579
(towers) and 0.652 (creeps) in their smoothness channel, so 0.45 lands effective smoothness
near 0.26–0.29 — a broad, dim specular lobe with no hotspot. The two values it replaced
were wrong in opposite directions: 1.0 gives effective 0.579, a tight glossy highlight that
reads as wet plastic on cast stone; 0.12 gives 0.070, which is no specular response at all.

Creeps deliberately share the tower value. They share a frame and a light rig, and two
specular widths would read as two art styles.

This is a defensible starting point, not a tuned final value.

### Reflections and their off switch hid each other

Environment reflections are generated from the scene's own trilight ambient gradient rather
than assigned as a cubemap asset, so they cannot drift out of sync with the ambient they
exist to match. Before that, every smooth surface mirrored Unity's stock daylit sky while
the camera cleared to near-black navy.

Separately, `_GlossyReflections` was **off** on ten towers and ten creeps. A reflection
probe that reaches nothing and a surface that reflects nothing look identical, so the two
defects concealed each other. Both validators now assert it is on.

### A flag can be wrong relative to the project rather than wrong in itself

`enableLODCrossFade` is checked in **both** directions: on with no LODGroup anywhere
(paying for the `LOD_FADE_CROSSFADE` variant of every shader for a transition that cannot
happen), and off when LODGroups exist (transitions will pop). Whoever adds LODs later gets
told to switch it back on rather than finding it silently disabled.

---

## Gates that are off on purpose

Two gates ship able to block and configured not to. Both would currently fail everything,
and **a gate that fails everything on the day it lands gets switched off within the hour.**

- **Intake `--strict`.** 19 of 30 scorecards are `needs_review`, dominated by
  `has_normal_map`. No asset can pass until normal maps are generated or the check is
  retired — `OPEN_ITEMS.md` item 3.
- **Role coverage `--strict`.** 20 of 30 roles have no production reference at all, because
  only the original ten went through the paint-then-model era — `OPEN_ITEMS.md` item 20.

The mechanism ships now; the default flips when the assets can meet it.

---

## Running Unity headlessly without breaking the next session

- **Editor version is `6000.5.3f1` and nothing else.** `6000.3.12f1` silently downgrades
  `ProjectSettings.asset` from serialized version 29 to 28, and the damage is not visible
  until something else fails.
- **Clear `Temp/__Backupscenes` before any batch run.** A leftover backup makes Unity open
  a scene-recovery modal, which in batchmode blocks forever with a completely healthy log.
- **Quit the interactive editor from the menu, not with a kill.** A force-kill is what
  leaves those backups behind.
- **`EditorApplication.update` is not pumped in batchmode Play Mode.** Anything that has to
  observe a running match needs a MonoBehaviour tick instead — see the remarks on
  `LocalPlaytestBatchRunner.InstallPlayModePump`, which exists entirely because of this.

## Capture conventions

### Batch-mode captures contain no UI

`VisualReviewCaptureRunner.QueueCapture` branches on `InternalEditorUtility.inBatchMode`:

- **Batch mode** issues a URP `SingleCameraRequest` and reads the RenderTexture. That renders
  the *scene*. IMGUI is drawn to the backbuffer during Repaint and is not part of a camera
  render, so it cannot appear.
- **Non-batch** uses `ScreenCapture.CaptureScreenshot`, which grabs the composited backbuffer
  and **does** include IMGUI.

Every capture set in this repo was taken in batch mode, so every one shows the board and
none shows the HUD. The improvement cycle scores "UI edge discipline and touch clearance" as
a **blocking** category and "C11 UI craft" on the craft axis; both have been scored against
evidence that structurally cannot contain the thing being scored.

Workaround, used for `screenshot-reviews/ui-fit-and-finish/`: run the capture **without**
`-batchmode`. It works, but the editor window is landscape 3840x2160 while the game is
portrait phone, so layout in those frames is not the shipped layout. Good evidence about
chrome, state and modality; useless for spacing and placement.

A real fix means drawing IMGUI into the capture RenderTexture, since batch mode's own screen
is a small landscape surface and cannot simply be grabbed at 1080x1920.


- Cite a capture by its **state name**, never its number. States get inserted and the
  numbers shift: `VFX_AND_ANIMATION_TARGETS.md` once told readers to judge effects in
  `05-active-combat`, which by then opened `05-send-card-disabled` — a static UI frame with
  no creeps in it at all.
- Score against a real match capture with the game's lighting and post-processing. Never a
  contact sheet, never `-nographics`, never a painted mock. A contact sheet has no board, no
  light rig and no post stack, so it cannot show grounding, lighting craft or tone.

### The camera resolves 61.9 pixels per world unit

Orthographic at `orthographicSize = 15.5` over a 1920px capture: `1920 / (2 × 15.5)`.

Useful whenever the question is "will this detail read?" — multiply the feature's world
size by 61.9. It is what turned the leg-articulation question from a judgement call into a
threshold: **below roughly 35px of on-screen height, a leg swings 2–4 pixels and
articulation is not the lever.** See
[`screenshot-reviews/creep-leg-visibility/`](screenshot-reviews/creep-leg-visibility/).

---

## Proving a check works

Every validator here was run against a deliberately broken state before being trusted, and
the failure message was checked for naming the right thing. That step is not optional and
it has already paid for itself: the original post-processing guard tested `profile == null`,
which passed, because the asset existed and was merely hollow.

When adding a check, break the thing it watches and confirm it exits non-zero for the
reason you expect.
