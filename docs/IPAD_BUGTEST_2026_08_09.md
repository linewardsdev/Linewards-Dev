# iPad Bug Test — 2026-08-09

Reported from live play on iPad, against a build containing the LOD fix (`8457192`) that
unfroze creep and ward animation. Eight items, unverified by me — this is the reporter's list
as given, with a first read on where each one probably lives. Nothing here has been reproduced
or diagnosed yet; the pointers are starting places, not conclusions.

Branch: `ipad-bugtest-2026-08-09`.

**Status 2026-08-09:** items 4 and 6 done and compile-verified. Item 11 (tablet scaling) is being
worked by another session. The remaining nine are untouched — item 1 is the one worth taking next,
being the only one likely to be a logic bug rather than presentation.

---

## 1. Creeps pause for a second or two when they have to turn

**Severity: high.** A multi-second stall is the most visible item on this list and the one most
likely to be a real logic bug rather than a presentation one.

Where to look: `b882af3` ("Stop creeps teleporting when a tower is placed, and make them turn")
introduced the turn behaviour. Suspect the turn is being played as a fixed-duration rotation that
gates forward travel, so a creep that re-mazes mid-lane stops moving until the turn completes.
Check whether the interpolation clamp in `UnityVerticalSliceRenderer.CreepPresentation` treats a
turn as a teleport — there is already a distance threshold there that snaps rather than
interpolates beyond a certain move, and a turn may be tripping it.

Worth confirming first whether the pause is presentation-only (simulation position keeps
advancing, the model lags) or whether the simulation itself stalls. Those are different bugs.

## 2. Send-card buttons have a broken effect when clicked

Reported as a "broken effect" on tap, on the creep send cards.

Where to look: `SendDockController` and `RuntimeUiChrome.DrawCommandCard`'s pressed/selected
state. The card chrome draws from `RuntimeUiArtLibrary.DrawChromeTexture` with a per-state
texture name; a missing or mis-named pressed-state texture would fall through to whatever the
fallback path draws. Note the project's own history here: a square outline with an X through it
is the recognisable "asset failed to load" glyph and has already been mistaken for a deliberate
cue once.

Needs a screenshot or a description of what "broken" looks like — flicker, wrong colour,
geometry, or a missing sprite. That distinction picks the search path.

## 3. Lift gate and spawn gate read as 2D sprites in a 3D world

They need to sit in the board rather than on top of it.

Where to look: these are drawn as `PrimitiveType.Quad` plates in
`UnityVerticalSliceRenderer.Board`. A camera-facing unlit quad on a 3D board is exactly what
reads as a sticker. Options in rough order of effort: give them the board's own vertex-colour
shader so they take the same lighting, inset them into the board plane with a depth bias instead
of floating above it, or replace with actual geometry.

Related: the contact-shadow system already exists (`UpdateContactShadow`) and is what grounds
other board objects. The gates may simply not be using it.

## 4. Weird blue squares at the four corners — DONE (`3824deb`)

**Fixed 2026-08-09.** Four `CreateCornerPylon` cubes per lane, untextured and flat-shaded, at the
plate corners. Compile-verified once the project lock freed up: Unity 6000.5.3f1 rebuilt
Assembly-CSharp with zero errors.

The find worth keeping: this was the **second** report of the same four objects. The method's own
remarks recorded the first, when they stood 0.11 proud of the board and read as "a solid blue
rectangle with no relationship to anything near it". That pass rescued them by flattening to a
0.30 x 0.30 x 0.045 chip — still an untextured cube, now with a perfectly square footprint. Blue
rectangle became blue square and came back.

Removed rather than rescued a third time: they state nothing the plate, gutters and frame do not
already say. `CreateLaneFlowTickMarks` below reached the same conclusion about its own "loose blue
shards" and survived only by being gated to full detail.

Original diagnosis, kept because it was right:

**Likely an asset failure rather than a design element.** Blue is the colour the missing-material
path produces, and "square at a corner" suggests either board corner plates or a debug overlay
that was never removed.

Where to look: `BoardMeshBuilder` and `UnityVerticalSliceRenderer.Board`'s plate drawing, plus
`RenderCompat.CreatePrimitive` — which exists specifically because `GameObject.CreatePrimitive`
returns an unmaterialed object in some paths and needed a guaranteed material. Check whether the
corner plates go through it.

## 5. Add a leaderboard button — other players' lives and income

**New feature, not a bug.** All the data already exists: `snapshot.Players.Players` carries every
seat's `Lives`, `Gold`, `Income`, `IsEliminated` and `Placement`, and eight-seat matches are the
default. Nothing new is needed from the simulation.

Scope to settle before building: is this a persistent always-visible strip, or a panel behind a
button? On a phone-first portrait layout with a 9:19.5 board column, screen space is the binding
constraint — a panel is likely, and the results screen already has a seat-ranking layout that
could be reused rather than designed again.

## 6. Remove the MULTI button — DONE (`14d1f4f`)

**Fixed 2026-08-09**, compile-verified alongside item 4.

The launcher is gone. **DONE is deliberately still drawn while the mode is on**, because the
button was never the only way in: double-tapping a tower selects every tower of its type and turns
multi-select on. Deleting the whole draw call would have left that gesture with no way out and no
visible state — trading a button nobody wanted for a trap.

The mode and its batch upgrade/sell paths are untouched, per the open question below, which is
still open: if multi-select should be gone entirely rather than just its button, the double-tap
gesture and `TowerSelectionBatchTests` go with it and that is a larger, separate change.

Original notes:

Straightforward. `TouchPlacementController` draws the launcher strip (BUILD / MULTI). Removing the
launcher is the easy half; the multi-select machinery behind it (`PruneMultiSelection`,
`SellTowers`, `UpgradeTowers`, and the batch tests that cover them) should be left intact unless
you want it genuinely gone, because the batch upgrade/sell paths are also reached from the
category cards.

Confirm intent: hide the entry point, or delete multi-select entirely? The tests
(`TowerSelectionBatchTests`) pin the batch behaviour and would need retiring for the latter.

## 7. Upgrade button for creeps and wards is hard to read and too small

Where to look: the tier row on the category cards — `DrawTowerCategoryTier` in
`TouchPlacementController.Gui` and its send-dock counterpart. The row was fitted into an already
tight card; `RuntimeUiChrome.CategoryCardHeight`'s own comment warns the stack has about one
scaled pixel of slack, and the tower palette panel is fixed at `282f * scale` while the send dock's
grows. That fixed height is the likely reason the tower side is the more cramped of the two.

Fixing legibility probably means growing the card rather than shrinking the text again.

## 12. Name mismatch: the board calls it BULWARK, the codex calls it Barricade Bastion — DONE

**Fixed 2026-08-09.** `TowerCatalog` entry 8 read
`new(8, "tower.barricade", "BULWARK", "Barricade bastion", ...)` — a short label for the build
button and a display name for the codex, and the two were different words.

Audited all sixteen towers. Three others differ between label and name (`CTRL`/Control ward,
`DRONE`/Repair drone spire, `CANOPY`/Elder canopy) but every one of those is a truncation of its
own name and still reads as the same unit. Only Barricade's was a *different word*: "bulwark"
appears nowhere in "Barricade bastion", and nowhere in the simulation either, where the content id
is `tower.barricade` and the name is "Barricade Bastion".

Changed the label to `BASTION`, taking a word from the unit's own name the way `CANOPY` and
`DRONE` do, and matching BULWARK's seven-character width so no card layout moves. `BARRICADE` would
also be defensible and matches the content id, but is two characters longer and risks the fit.

This is why item 8 below was unfindable: grepping the project for "bulwark" returns only this
label and an unrelated `CreepSupportRole.Bulwark` on the Obsidian Brute, so the reported unit
looked like it might be a creep.

## 8. Bulwark looks rough — review art, lighting, animation, all of it

**Resolved: this is the Barricade Bastion**, the Foundry wall — see item 12. Original note follows.

**Needs a naming check before anyone starts.** Grep finds no prefab, mesh or texture named
`bulwark` anywhere in `Assets`. What exists is `CreepSupportRole.Bulwark`, a support role carried
by the **Obsidian Brute creep**, and separately a tower called **Barricade Bastion** — which is a
wall and the closest thing to a "bulwark tower" in the roster.

So this is either the Obsidian Brute creep or the Barricade Bastion ward, and they live in
completely different places. Confirm which before spending time.

"Something happened and it looks really rough" implies a regression rather than a unit that was
never finished, so worth bisecting: find the last build where it looked right and diff the mesh,
material and prefab between then and now. The recent kitbash and re-bake work is the likely
window — `33913f3` converged six cross-mesh kitbash units, and several units are on record as
still needing their emissive re-baked (item 19 fixed TurretWalker and proved the other eight
were not).

Scope as asked is art + lighting + animation together, so treat it as one pass over that unit
rather than three separate tickets.

## 9. Build menu blocks the bottom row — make it moveable while open

The build palette covers the last row of the lane, so you cannot see or place on the cells the
menu sits over.

Where to look: `TouchPlacementController.TowerPalettePanelRect`, currently pinned at
`282f * scale` at the bottom of the frame. Note this is the same fixed height already implicated
in item 7's cramped upgrade button — one change may serve both.

"Moveable while open" is one answer; worth considering the alternatives before building a drag
interaction on a touch surface, since a draggable panel competes with the board's own tap and
drag handling. A collapse/peek toggle, or shifting the board column up while the palette is open,
may get the same result with less to go wrong.

## 10. The lives readout in the top right does nothing

Reported as not responding. Two different bugs wear this shape and they need separating first:
the readout is **not updating** (a data binding problem), or it is **not tappable** when it looks
like it should be (an affordance problem — it may never have been a button).

Check what the top-right element is bound to before assuming either. If it is meant to open
something, that overlaps item 5's leaderboard — a lives readout that expands into the full seat
list may be the natural home for that feature rather than a separate button.

## 11. The game does not fill a 13" iPad Pro screen

**This one is by design and the design is the problem.** `MobileViewportLayout.CameraRect()`
computes `width = clamp(PortraitAspect / screenAspect, 0.22, 1)` against a `PortraitAspect` of
`9/19.5` — a tall phone. The board is drawn as a centred, full-height column of that aspect and
everything either side is empty.

On a 9:19.5 phone the column is ~82% of the width and reads as full-screen. On a 13" iPad Pro it
is far narrower, which is exactly the report. The `0.22` floor means it never collapses entirely,
but it was never meant to fill a tablet.

This is a layout decision rather than a bug fix, and it is the largest item on this list. The
options differ a lot in cost: letterbox as now (status quo), widen the column on tablet aspects
and accept a different board framing, or use the extra width for something — the leaderboard from
item 5 is the obvious candidate, and a tablet layout that puts seat status beside the board rather
than over it would turn this from a defect into the reason to own the bigger screen.

Worth settling early, because items 5, 7, 9 and 10 all place UI, and doing them phone-only then
re-doing them for tablet is the expensive order.

---

## Related open work, not on this list

- **Twin Crescent has no visual profile.** ~~It renders a primitive fallback that nothing
  animates and measures 0.00/0.00 on the motion probe.~~ **Fixed 2026-08-09:** the claim was
  correct — the wrapper prefab existed but `TowerVisualLibrary.asset` was never given a role-15
  entry (the promote step was skipped during integration), and the motion switch had no
  TwinCrescent case. Both are in now. Not yet sighted on a device, and the motion probe still
  needs a run once the editor releases the project lock.
- **Four creeps are unrigged** — Revenant, Shade, Swarm, Wisp (item 11 wave 2.4). They have no
  Animator at all and will not animate whatever else is fixed.
- **`m_CullingMode` on the rigged creeps** was set to `AlwaysAnimate` in `1145a0d` during the
  device freeze. Do not read the LOD fix (`8457192`) as proving that change unmotivated: the
  LOD bug fully explains the original report — including the tell that the builder kept
  animating, since the builder is the one animated unit with no LODGroup — but it does not
  prove `CullUpdateTransforms` was innocent. The bounds-don't-follow-the-rig mechanism 1145a0d
  describes is real and device-only, and every working device build since has had
  `AlwaysAnimate` in place, so its necessity has never been tested. Revert it only with a
  device build showing clips still play under `CullUpdateTransforms`; until then it is a small
  CPU cost buying known-correct behaviour.
