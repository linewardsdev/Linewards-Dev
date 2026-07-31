# UI fit-and-finish pass

Date: 2026-07-31
Unity: 6000.5.3f1

```
Unity -projectPath unity/LTW.UnityClient \
  -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureVisualReviewSet \
  -ltwCaptureOutputDir docs/screenshot-reviews/ui-fit-and-finish
```

**Note the missing `-batchmode`.** That is the finding, not a typo — see below.

## The finding that had to come first: UI captures contained no UI

`VisualReviewCaptureRunner.QueueCapture` branches on `InternalEditorUtility.inBatchMode`:

- **Batch mode** → `WriteImmediateCapture`, which issues a URP `SingleCameraRequest` and reads
  the resulting RenderTexture. That renders the *scene*. IMGUI is not part of a camera
  render — it is drawn to the backbuffer during the Repaint event — so it cannot appear.
- **Non-batch** → `ScreenCapture.CaptureScreenshot`, which grabs the composited backbuffer
  and **does** include IMGUI.

Every capture set in this repository was taken in batch mode. So every one of them shows
the board and none of them shows the HUD.

That matters beyond inconvenience. `MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md` scores "UI
edge discipline and touch clearance" as a **blocking** promotion category, and craft
category C11 is "UI craft". Both have been scored against evidence that structurally cannot
contain the thing being scored.

The captures here were taken without `-batchmode` to get around it. That is a workaround,
not a fix, and it has a real cost: the editor window is landscape 3840x2160, while the game
is portrait phone. **Layout in these frames is not the shipped layout** — treat them as
evidence about chrome, states and modality, and not about spacing or placement.

Recorded under "Capture conventions" in
[`RENDER_AND_ART_VALIDATION.md`](../../RENDER_AND_ART_VALIDATION.md). It needs an
`OPEN_ITEMS.md` entry too; that file was being actively edited by another agent when this
landed, so it was left alone rather than raced.

## What changed

### Panels had three different looks, none of them ours

Four files each defined a private `DrawPanel`:

- `SendDockController`, `TouchPlacementController` and `PlacementFeedbackView` held
  byte-identical copies that built a `GUIStyle` from `GUI.skin.box` and overrode only border
  and padding — so the background was **Unity's built-in editor-skin box**, tinted navy.
  Tinting the engine default does not stop it reading as the engine default.
- `LocalSessionFlowOverlay` drew a flat rect plus four 1px hairline edges. Hard corners, no
  bevel, no depth.

Meanwhile the buttons have had a chamfered bevel the whole time. All four now use
`RuntimeUiChrome.DrawPanel`, which adopts the buttons' chamfer language and adds a cast
shadow — the shadow being the part that matters, because a dark panel on a dark board with
no shadow reads as a hole rather than as something on top.

### The modal screens were not modal

Captured before the change: the pre-match title panel rendering over a fully drawn, fully
interactive build palette. Two panels overlapping, neither dimmed, and the one underneath
still taking input.

Fixed in two halves, because in IMGUI one half cannot do it alone:

- **Visual** — `RuntimeUiChrome.DrawModalScrim` dims what is behind.
- **Input** — `RuntimeUiChrome.ModalScreenActive`, set from `LocalSessionFlowOverlay.Update`
  and read by every HUD component's `OnGUI`.

A scrim cannot block input here. IMGUI dispatches an event to components in draw order and
the first control under the cursor consumes it, so a full-screen button drawn last blocks
only what is drawn after it — which is nothing. The overlay draws last, which is right for
painting over the HUD and exactly wrong for intercepting its clicks. Asking each component
to stand down is the only ordering-independent answer.

### A destructive control labelled "R"

The button under PAUSE that abandons the match in progress was labelled `R`, 34px wide
against PAUSE's 62, and right-aligned so the two did not form a column. Now `RESET`, same
width, and in the warning colour rather than the neutral one.

### The capture scenario never started the match

Exposed by the modality fix rather than caused by it. States 01–06 are all named for HUD
elements — `default-hud`, `build-menu-open`, `build-card-selected`, `send-menu-open`,
`send-card-disabled`, `lane-selector-open` — and the runner captured all six before calling
`StartMatch`, so the pre-match title screen owned the display.

This was always wrong and was invisible only because the HUD used to draw underneath the
title panel: the captures *looked* populated while showing two screens at once. Once the HUD
correctly stands down, the same states went empty, which is the honest rendering of a
scenario that never set itself up. The runner now starts the match first.

## Still open

- **Card tier rows overlap their own art.** `UP 120G` and `TIER 1` sit directly on the metal
  bar graphic on every category card, at low contrast. Visible on `02-build-menu-open` and
  `04-send-menu-open`.
- **Layout and grouping are unassessed.** The HUD sits in four separate islands in these
  frames, but that is the landscape editor window, not the phone. Judging it needs the
  capture fix above.
- **No font asset**, and IMGUI still blocks all UI motion — `OPEN_ITEMS.md` item 10, which
  needs a technology decision before any of it can be scheduled.
