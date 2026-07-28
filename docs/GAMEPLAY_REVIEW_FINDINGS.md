# Gameplay Review Findings

Review date: 2026-07-28. Method: `VisualReviewCaptureRunner.CaptureVisualReviewSet` (15 states)
plus `MotionCaptureRunner` sequences and live transform sampling in play mode.

> **Read this first.** An earlier version of this document listed five P1 UI defects. Four were
> wrong, and the fifth was a defect in the review tooling rather than in the game. The cause is
> recorded under "Why the first review pass was wrong", because the same trap will catch the next
> person who reviews UI from these captures.

Run captures WITHOUT `-nographics`; that flag disables the graphics device, and every frame comes
out a single flat colour while the run still reports success.

---

## P1 — The UI cannot currently be reviewed from captures at all

- [ ] **`VisualReviewCaptureRunner` composites a hand-painted mock of the HUD, not the real UI.**
      In batchmode, IMGUI (`OnGUI`) does not render into the capture RenderTexture, so
      `PaintBatchHudOverlay` reconstructs the HUD on the CPU after readback — drawing panels,
      buttons and text with its own hand-rolled 3x7 bitmap font (`GlyphRows`, ~line 1541) via
      `PaintRect`. This is deliberate and explained at `RenderBatchHudOverlay` (~line 1587), but
      the consequence is easy to miss: **in every capture from this runner, only the 3D board is
      real. Every panel, label and button is a painting.**
      Demonstrated: changing the game's font had zero effect on these captures — verified twice,
      byte-identical output — because the game's font is not what is in the image.

- [ ] **The painted mock is stale and no longer matches the game.** `PaintSendMenuOverlay`
      hard-codes exactly five `PaintCard` calls (RUN/BRT/SWM/SHD/SGE); it predates the 10-creep
      roster, so the capture depicts half the current content. The real `SendDockController` has a
      category picker and a populated `DrawCategoryTwoCreeps`.
      This is the more dangerous of the two failure modes: the mock does not fail loudly when the
      game changes, it quietly depicts an older build.

      Options, roughly by value: capture real UI instead (drive a Game view rather than a batch
      RenderTexture, so IMGUI actually draws); or keep the mock but assert it against the real
      roster so divergence fails; or drop the painting and accept board-only captures, reviewing
      UI by hand.

## P2 — Clarity (from code reading; NOT verified against real UI)

- [ ] **Creep abbreviations are cryptic.** `RUN/BRT/SWM/SHD/SGE`, with `OBRT`, `COIL`, `WALK` in
      the second category. Consider full names, or leaning on the icons and dropping the
      abbreviation.

- [ ] **Send dock geometry covers a large part of the board.** The panel is `282f * scale` tall,
      bottom-anchored. Worth confirming whether it hides the leak gate in the real UI.

## P2 — Review tooling gaps (these are real; they are board-layer, not painted)

- [ ] **`10-heavy-pressure` produces a single creep.** The state that exists to test readability
      under load is not producing load. Creep count comes from the simulation, so this one is
      genuine.

- [ ] **No towers are placed in the review set's combat states.** Tower aim, muzzle anchoring,
      recoil and the ring VFX cannot be reviewed there. `CaptureRoleLineupReviewSet` does place
      towers and is the better model.

---

## Why the first review pass was wrong

Recorded so the trap stays visible rather than being rediscovered.

The first pass reported: the font's `N` glyph renders as `M`; send-button cost text is unreadable;
`+10P0` has no separator; stray garbled text floats above the board; and only 5 of 10 creeps are
reachable. Every one came from reading painted pixels as if they were the game.

- `N` "rendering as `M`" is `GlyphRows('N') = {"101","111","111","111","101","101","101"}` — the
  harness's own 3-pixel-wide glyph, genuinely ambiguous at that size. The game font was never
  involved.
- The unreadable cost text is `PaintCard`'s painted meta string at the same 3px scale.
- `+10P0` is painted; the real `HudView` format already has two spaces.
- The "stray garbled text" is painted reference text sitting outside the board frame.
- "Only 5 creeps" is the stale `PaintSendMenuOverlay` above.

Two process lessons worth keeping:

1. **Verify that a fix changes the artefact.** The font fix was applied twice and both times the
   captures were byte-identical. That should immediately have been treated as evidence about the
   *measurement*, not as a reason to try a third variation.
2. **Establish what is real in a capture before drawing conclusions from it.** The board is
   rendered; the UI is painted. Nothing in the filename or the log says so.

## Verified working (measured, not eyeballed)

- Creep motion applies exactly as authored: Revenant scale spread 0.0903 vs 0.09 authored, Serpent
  0.1103 vs 0.11 with 34°/s yaw as written, Wisp 52°/s. Sampled from live transforms in play mode,
  which is board-layer and therefore real.
- All three rigged creeps (Brute, Obsidian Brute, Turret Walker) animate: 10/11 bones move, Root
  correctly static. Gait fore-aft dominant by 9–19x and 14–24x.
- Send-menu icons load for all 10 creeps at 128x128 with alpha, through
  `Resources.Load<Texture2D>` at the runtime path.
- Creep travel is smooth, confirming the snap-threshold fix.

## Known and accepted

- **Crystal Wisp is on screen ~43 frames** (vs 5,445 for Serpent) because it has 4 HP. Intended:
  it is 5G chaff. Do not tune Wisp's motion curves further; nothing authored reads at that
  lifetime.
