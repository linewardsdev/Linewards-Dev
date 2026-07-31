# AA Uplift Wave 0 Re-baseline

Date: 2026-07-31
Unity: 6000.5.3f1
Command:

```
Unity -batchmode -projectPath unity/LTW.UnityClient \
  -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureVisualReviewSet \
  -ltwCaptureOutputDir docs/screenshot-reviews/aa-uplift-wave0-rebaseline
```

The `GRAPHICS_AA_UPLIFT.md` wave 0.8 re-baseline. This is the first capture set taken with
post-processing actually running, and it is the reference every later wave is compared
against.

## What changed under these captures

Everything in wave 0, all landed before this run:

- Post-processing profile sub-assets persisted, so tonemapping, bloom and colour
  adjustments run at all for the first time since the URP migration.
- Soft shadows enabled, HDR colour grading, SMAA on the camera, environment reflections
  pointed at the scene's own ambient gradient rather than Unity's stock procedural sky.
- Thirty tower and creep body materials retuned to a single smoothness, with per-role
  tower emission above the bloom threshold and `_GlossyReflections` switched on.

## Two capture states this run also settles

`08-runner-10-pressure` and `09-swarm-heavy-pressure` were recorded in OPEN_ITEMS item 14
as landing nothing in the framed lane, which made heavy-pressure readability — a *blocking*
scorecard category — impossible to review.

Both now land in the framed lane. Measured from the runner's own board-contents log:

| State | Framed lane | On-camera creeps | On-camera towers |
| --- | --- | --- | --- |
| `07-active-combat` | 1 | 30 | 2 |
| `08-runner-10-pressure` | 1 | 64 | 9 |
| `09-swarm-heavy-pressure` | 1 | 75 | 9 |
| `10-heavy-pressure` | 1 | 190 | 9 |
| `11-reduced-effects-heavy` | 1 | 263 | 9 |

The fix was already in the tree — `QueueVisibleLineupCreep` resolves the sender that
actually routes to `FramedLaneId()` rather than taking the next active opponent — so the
item was stale rather than outstanding. These captures are the evidence.

## Not yet assessed

Whether the image is *better*. That is a human judgement against these frames, and it is
the thing wave 0 has not had. The numbers above say the captures contain what they are
supposed to contain; they do not say the game looks good.
