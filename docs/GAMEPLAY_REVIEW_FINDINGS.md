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

## How to review real UI

`RealUiCaptureRunner` captures the actual runtime UI, including IMGUI. It uses ScreenCapture on a
real Game view instead of reading back an offscreen RenderTexture, which is what
`VisualReviewCaptureRunner` cannot do. It therefore must run WITHOUT `-batchmode`:

    Unity -projectPath <project> -executeMethod \
        LTW.UnityClient.Editor.RealUiCaptureRunner.Run \
        -ltwCaptureOutputDir <dir> -logFile <log>

It seeds a match, captures the default HUD and both send-dock categories, and quits.

## P1 — Real UI defects (found with RealUiCaptureRunner, verified at full resolution)

- [x] **FIXED — Send-card cost text was occluded by the card's accent bar.** On every creep card the
      cost/income line (`5G  +1`, `16G  +4`, …) is drawn where the coloured progress/accent bar is
      also drawn, so the lower half of the digits is covered. Legible only if you already know what
      it says.
      Cause: `CommandCardMetaRect` runs to `yMax - 5*scale` while the cost strip started at
      `yMax - 8*scale`, so the bar was drawn across the lower half of the digits. Moved the strip
      to `yMax - 4*scale` and thinned it to `2*scale`. Verified by recapture: `5G  +1` now reads
      cleanly.

- [x] **FIXED — A debug overlay was visible during normal play.** Top-left shows `Tick: … Towers: … Creeps:
      …`, per-player lines, `Lane flow:` and `Bot sends:`. Useful in development, but it is on by
      default with no obvious gate.
      `DiagnosticsOverlay.showRuntimeOverlay` defaulted to true with nothing gating it. Now
      defaults off and opts in with `-ltwDiagnostics`, matching the existing `-ltwBoardDetail` /
      `-ltwCameraTilt` switches. `LatestText` is still produced, since the playtest recorder
      consumes it. Verified by recapture: the top-left region is now clean.

- [x] **FIXED — two CLOSE buttons were visible at once** when the dock is open. Not an ownership
      question: both belonged to `SendDockController`, and `TouchPlacementController` had the same
      pair. The launcher slot must show something other than SEND/BUILD while expanded, so it is
      necessarily CLOSE; the header CLOSE was pure duplication. Kept the launcher (thumb zone, and
      where the finger already is after tapping SEND) and moved BACK into the vacated header slot.

- [x] **FIXED — the category picker drew its second card outside the dock.** Cards were
      `buttonHeight * 2 + gap` tall, putting the pair's bottom edge at 84 + 176 + 8 + 176 = 444
      inside a panel only 282 tall, so card two hung over the board with its lower half off the
      bottom of the screen. One `buttonHeight` each lands at 260, matching the creep grids that
      already fit. Verified by recapture.

- [x] **FIXED — cost text washed out over the card art.** The cost was drawn in the creep's raw
      accent colour, and the reference card art has a nameplate behind the label row but nothing
      behind the cost row, so the digits landed on pale stone. (The procedural path never showed
      this: its `CardInset` is already dark.) Added a dark plate behind `CommandCardMetaRect` in
      the art path and lifted the text 55% toward white, which keeps the per-creep colour coding.
      Verified by recapture across all 10 cards.

## P1 — The painted HUD mock (DELETED)

- [x] **`VisualReviewCaptureRunner`'s CPU-painted HUD is gone** (678 lines: `PaintBatchHudOverlay`,
      the `GlyphRows` 3x7 bitmap font, every `Paint*`/`Draw*Overlay`/`AddOverlay*` helper, and the
      Built-in-pipeline `RenderBatchHudOverlay` GPU pass).

      It existed because IMGUI does not draw into an offscreen RenderTexture in batchmode, so the
      HUD was reconstructed after readback. The problem was that the painting was convincing enough
      to review — and reviewing it produced five UI defects that were all artefacts of the mock,
      plus two font "fixes" that changed nothing because the game's font was not in the picture. It
      also drifted silently: it still painted the pre-expansion 5-creep send menu long after the
      roster reached 10, depicting an older build rather than failing.

      These captures now contain the board only. An empty region is honest; a convincing painting
      of a stale HUD is not. For UI, use `RealUiCaptureRunner`, which drives a real Game view.

## P1 — Open: two capture states regressed after the seats/authority merge

- [ ] **`runner-10-pressure` and `swarm-heavy-pressure` land nothing in the framed lane.**
      `heavy-pressure` and `active-combat` are fine (63 and 21 on camera), so the harness, the
      sender lookup and the cooldown bypass all work. These two differ in that they reset the match
      first and then queue a single modest send. Measured: the send is accepted and correctly
      targeted at lane 1, yet the capture shows `L2=9` / `L3=20` and `L1=0` — the creeps traverse
      the lane and leak-transfer onward before the shutter.
      Ruled out by measurement, not reasoning: it is not the send cooldown (bypassed, no
      rejections logged), not the sender (logged as P8 → lane 1), not the defence line (removing it
      changed nothing), and not the capture delay (11s behaved the same as 4.5s).
      Left open deliberately. The merge itself is sound — 92 tests pass — but these scenarios need
      retuning against the new match dynamics, and that is separate work from the merge.

## P2 — Open after the 15-tower expansion (2026-07-29)

- [ ] **Ten of fifteen towers render with procedural fallback visuals, not their meshes.** The models
      are imported and pass every intake gate, but no wrapper prefab or `TowerVisualLibrary` profile
      exists for them yet, so the board shows placeholder geometry where a Gatling Turret should be.

- [ ] **Six of the ten new towers have no special behaviour.** They are plain single-target towers
      separated only by cost/range/damage/cooldown. Barricade and Foundry have owner-specified
      mechanics pending; the four Grove totems have none designed.

- [ ] **Nothing in the tower expansion has been playtested.** Stats were sized off damage-per-gold
      arithmetic. Damage per gold spans 0.071 (Relay, subsidised by signal gold) to 0.286 (Arrow).

- [ ] **`TowerVisualRole` is a five-value enum driving 19 call sites.** Ten new towers currently have
      to borrow one of the original five roles for motion. Worth generalising the way the palette's
      role switches were, before the next batch of towers rather than after.

## P2 — Found once the mock stopped covering the board

- [ ] **World-space combat text overlaps itself and is hard to read under load.** In
      `10-heavy-pressure`, two `RUNNER` spawn labels print on top of each other, and `-2 LIFE`
      collides with a damage number. This is the game's own text, not the removed mock — it was
      simply hidden behind the painted HUD before. Needs staggering or de-duplication.

## P2 — Clarity (now verified against real UI captures)

- [x] **FIXED — creep abbreviations were cryptic, and one was wrong.** `ASH` was the Revenant:
      not merely terse but misleading. Cards now read RUNNER / BRUTE / SWARM / SHADE / SIEGE and
      WISP / REVENANT / OBSIDIAN / SERPENT / WALKER. `CompactCreepLabel`, which shortened the
      first five to BRT/SWM/SHD/SGE, is deleted — the cards are ~130*scale wide against a 10*scale
      font, so eight characters fit with room to spare. Verified by recapture of both categories.

- [x] **REFUTED — the send dock does not hide the leak gate.** Checked against
      `real-02-send-dock-open.png`: the dock covers roughly the middle-lower third of the board and
      the leak gate sits below it, fully visible. The concern came from reading the panel height in
      code without checking where it lands.

## P2 — Review tooling gaps (FIXED)

Both were real, but the first diagnosis of the first one was wrong, so the cause is recorded.

- [x] **FIXED — pressure states captured a near-empty board.** Reported as "`10-heavy-pressure`
      produces a single creep". It was in fact producing 182 — but only 7 of them in the lane the
      camera frames. A send goes to the home lane of the sender's *next active opponent*, and lane N
      is player N's home lane, so with 8 lanes the hardcoded `PlayerId(3)` in
      `QueueVisibleLineupCreep` (and in `SendStressReviewWave`) delivered every "visible lineup"
      send to lane 4 while the camera watched lane 1. The name said visible; the routing said
      otherwise. Sender is now derived from the framed lane via `SenderFeedingLane`.
      Measured: `heavy-pressure` went from 7 on-camera creeps to 112.

- [x] **FIXED — no towers on camera in the pressure states.** `ResetChecklistScenario` calls
      `ResetMatch`, which clears the towers `StartCombat` placed, and nothing re-placed them — so
      every state after `active-combat` captured 0 towers, which is most of what those states exist
      to show. `PlaceReviewDefenceLine` now puts all five roles in lane 1 for each pressure state.
      Measured: 0 towers on camera to 5.

- [x] **Captures now log what was on the board.** `LogBoardContents` prints
      `lane=N onCamera creeps=… towers=… | boardWide creeps=… towers=…` next to every capture, and
      `HeavySendStressHarness` warns on a rejected wave instead of discarding the result. Both
      failures above were invisible in the log precisely because a state that produced nothing
      succeeded and wrote its file exactly like one that worked.

`results-or-late-match` still captures an empty lane. That one is expected — the state runs the
match to completion, by which point lane 1 is over.

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

- **All 10 creeps ARE reachable.** `SEND › CATEGORY 2` opens correctly and shows WISP / ASH /
  OBRT / COIL / WALK, each with its generated icon. The earlier "only 5 reachable" finding was the
  stale painted mock, not the game. Confirmed in `real-03-send-category-two.png`.
- **UI text is crisp and legible in the real UI.** Titles, buttons, the HUD readout and card labels
  all render cleanly. Every legibility finding in the first pass came from the harness's 3x7
  painted glyphs.

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
