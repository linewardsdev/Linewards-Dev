# The shell screens were the only surface not in the portrait column

2026-08-03. Reported as "the aspect ratio is messed up on the menu".

## What was wrong

Two faults, compounding.

**The panel ignored the column.** The board camera and every IMGUI surface lay out inside
`MobileViewportLayout.CameraRect()` — a centred, full-height column that narrows as the window
gets wider than 9:19.5. A UI Toolkit `PanelSettings` panel has no viewport concept and always
fills the window, so the menu was the only thing spanning the whole surface:

| surface | board column | menu |
|---|---|---|
| phone 9:19.5 | 100% | 100% |
| capture runner 9:16 | 82% | 100% |
| 16:9 Game view | **26%** | **100%** |

**The scale factor blew up with it.** `match = 0` scales by width, so at 1920x1080 the factor is
1.78 and a design 1920 units tall demands 3413 pixels of a 1080-pixel window.

**Underneath both:** the reference resolution was 1080x1920, which is **9:16**, while the game
targets **9:19.5**. The menu was authored against a different aspect from the board behind it.
The old comment on `match = 0` chose to match width specifically to stop the wordmark
overflowing — real reasoning, but it was compensating for the wrong reference aspect instead of
correcting it.

## The fix

Reference resolution to 1080x2340 (9:19.5), match to height, and the shell root inset to the
column. Those combine so the design's 1080 units resolve to `1080 * screenHeight / 2340`, which
is exactly `PortraitAspect * screenHeight` — the column width — at every aspect:

| surface | column | design | delta |
|---|---|---|---|
| phone 9:19.5 | 1080.0px | 1080.0px | 0.0% |
| capture 9:16 | 886.2px | 886.2px | 0.0% |
| game view 16:9 | 498.5px | 498.5px | 0.0% |
| desktop 16:9 | 664.6px | 664.6px | 0.0% |
| tablet 3:4 | 630.5px | 630.5px | 0.0% |

So the horizontal overflow the old comment guarded against cannot occur — prevented by the
reference aspect being right, not by the match axis.

## Evidence

Captured through `RealUiCaptureRunner` at 1080x1920, where the column is 886px wide and centred,
so its edges should fall at x=97 and x=983.

- `01-title-in-column.png` — measured field edges at **x=96 and x=982**, one pixel out because
  the edge detector reports the pixel before the transition.
- `02-pause-over-board.png` — the case that matters: the menu composites over the live board and
  the two now occupy the same column.

**Note for whoever runs the capture runner next:** it drives itself from `EditorApplication.update`
and calls `EditorApplication.Exit` when finished, so passing `-quit` kills it before the first
tick — it logs "pinned the Game view" and writes nothing. Pass no `-quit`, and no `-batchmode`
either, since `ScreenCapture` needs a real Game view.
