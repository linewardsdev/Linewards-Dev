# Gameplay Review Findings

Review date: 2026-07-28. Method: `VisualReviewCaptureRunner.CaptureVisualReviewSet` (15 states)
plus `MotionCaptureRunner` sequences, inspected at full capture resolution rather than downscaled.
Every item below is something observed in a capture, with the evidence noted.

Run captures WITHOUT `-nographics` — see the remarks on both capture runners.

---

## P1 — Legibility (blocks reading the game)

- [ ] **UI text is rendered too coarsely to distinguish letters.** `N` reads as `M` throughout —
      the HUD looks like `LIME` for LINE, the dock title `SEMD` for SEND, the Runner button `RUM`
      for RUN.
      This is NOT a broken glyph. Segmenting the bitmaps and comparing them pixel-for-pixel shows
      `N` and `M` are genuinely different (`N`'s solid middle band spans 9 rows, `M`'s spans 6),
      so the font is correct and the mapping is fine. The problem is resolution: each glyph is
      only 9px wide with 3px strokes, i.e. three cells of horizontal detail, which cannot separate
      `N` from `M` at a glance. Any fix belongs in glyph size / stroke weight / render scale, not
      in the font.
      Evidence: `01-default-hud.png`, `04-send-menu-open.png`; bitmap comparison of the RUN and
      SWM button labels.

      **Investigation so far (all verified, none of it fixed the problem):**
      - Dynamic OS fonts are available and resolve: 559 OS fonts present,
        `Font.CreateDynamicFontFromOSFont` returns Arial successfully. Font availability is not
        the blocker.
      - Setting `GUI.skin.font` to that dynamic font before styles are constructed changed
        nothing — every static UI capture was byte-identical before and after.
      - Assigning `style.font` directly on `HudView`'s pill/label/value/meta styles also changed
        nothing, byte-identical again.
      - `HudView.cs:229` builds exactly the observed string
        (`$"L{Lives}  G{Gold}  +{Income}  P{Pressure}"`, note the double spaces) and draws it with
        `GUI.Label(..., valueStyle)`. Yet changing `valueStyle`'s font has no effect on the
        rendered output.

      **Conclusion / next lead:** the readout on screen is NOT coming from `HudView`'s styles,
      despite the format string matching character for character. Something else is drawing it —
      `RuntimeMatchHud` draws a similar readout and is the obvious next suspect. Identify the
      actual draw path first; changing fonts on the wrong component wastes a full capture cycle
      each time. Note also that both `+10` and the `P0` pressure box overlap, so the "no separator"
      reading below is really an overlap of two elements, not a missing space.

- [ ] **Send-button cost/meta text is unreadable at real resolution.** The line under each creep
      icon (intended to read like `10G  +1`) renders as overlapping strokes with no legible
      characters. It looks fine only when the capture is downscaled, which is why it has survived
      review before. This is a mobile target, so it needs to be legible at native size.
      Evidence: `04-send-menu-open.png` cropped to the dock at 2x.

- [ ] **HUD income and pressure overlap: reads as `+10P0`.** The source string already has two
      spaces (`+{Income}  P{Pressure}`), so this is not a missing separator — the pressure element
      is drawn in its own highlighted box that overlaps the income text.
      Evidence: `01-default-hud.png` at 5x.

- [ ] **Stray garbled text above the board, upper left.** A small cluster of illegible characters
      floats outside the board frame in every captured state. Appears to be a debug or
      mispositioned label; it is present even on the default HUD with nothing happening.
      Evidence: present in all 15 captures.

## P1 — Content reachability

- [ ] **Only 5 of the 10 creeps are reachable in the send dock.** The dock opens straight onto
      Category 1 (RUN/BRT/SWM/SHD/SGE) with no visible control for switching to Category 2, which
      is where all five newly-added creeps live (Wisp, Revenant, Obsidian Brute, Serpent, Turret
      Walker). `SendDockController` does implement a category picker at `selectedCategory < 0`, so
      either the picker is being skipped or its affordance is invisible at this size. Half the
      roster — including everything added most recently — is currently unshippable to players.
      Evidence: `04-send-menu-open.png`; `SendDockController.DrawCategoryPicker`.

## P2 — Clarity

- [ ] **Creep abbreviations are cryptic.** `RUN/BRT/SWM/SHD/SGE` require prior knowledge. The new
      category compounds this with `OBRT`, `COIL`, `WALK`. Consider full names, or leaning on the
      icons (which read well) and dropping the abbreviation.

- [ ] **Send dock covers roughly a third of the board.** With it open, the lower lane — including
      the leak gate, the highest-stakes area — is hidden. Worth checking whether the player needs
      to see the board while choosing a send.

- [ ] **Build cells read as undifferentiated grey slabs.** They carry almost no visual language for
      buildable vs blocked vs occupied. The lane itself reads clearly by contrast.

## P2 — Review tooling gaps (these hide the above)

- [ ] **`10-heavy-pressure` shows a single creep.** The state that exists specifically to test
      readability under load is not producing load, so nothing about clustering, overlap or
      health-bar collision is actually being reviewed.
      Evidence: `10-heavy-pressure.png`.

- [ ] **No towers are placed in the review set's combat states.** Tower aim tracking, muzzle
      anchoring, recoil and the expanding-ring VFX therefore cannot be reviewed from this set at
      all, despite being the bulk of recent visual work. `CaptureRoleLineupReviewSet` does place
      towers and should probably be the model.
      Evidence: `07-active-combat.png` through `11-reduced-effects-heavy.png`.

---

## Verified working (no action needed)

Recorded so these are not re-investigated:

- Creep motion applies exactly as authored, measured live rather than eyeballed: Revenant scale
  spread 0.0903 vs 0.09 authored, Serpent 0.1103 vs 0.11 and 34°/s yaw as written, Wisp 52°/s.
- All three rigged creeps (Brute, Obsidian Brute, Turret Walker) animate: 10/11 bones move under
  the clip, Root correctly static. Gait is fore-aft dominant by 9–19x and 14–24x respectively.
- Send-menu icons render correctly for all 10 creeps and read well at button size.
- Creep travel is smooth, confirming the Wisp snap-threshold fix.

## Known and accepted

- **Crystal Wisp is on screen ~43 frames** (vs 5,445 for Serpent) because it has 4 HP. Confirmed
  as intended: it is 5G chaff and dying instantly is its role. Do not invest further in tuning
  Wisp's motion curves — nothing authored can read at that lifetime.
