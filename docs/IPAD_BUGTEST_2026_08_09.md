# iPad Bug Test — 2026-08-09

Reported from live play on iPad, against a build containing the LOD fix (`8457192`) that
unfroze creep and ward animation. Eight items, unverified by me — this is the reporter's list
as given, with a first read on where each one probably lives. Nothing here has been reproduced
or diagnosed yet; the pointers are starting places, not conclusions.

Branch: `ipad-bugtest-2026-08-09`.

**Status 2026-08-09 (late).** Nine of twelve are addressed, across two sessions working in parallel.

| Item | State |
| --- | --- |
| 1 creep turn stall | **fixed** — braked creeps were interpolated against the wrong cost |
| 2 send-card click effect | **open, blocked** — needs a description of what "broken" looks like |
| 3 gates read as 2D | **fixed, unseen** — judged by eye, wants a device build |
| 4 blue corner squares | **fixed** |
| 5 leaderboard | **landed elsewhere** — `SeatLeaderboardView`, put in the rail a tablet was wasting |
| 6 MULTI button | **fixed** — button gone, feature deliberately kept |
| 7 upgrade row unreadable | **fixed elsewhere** (`e75b08a`) — card grown rather than text shrunk |
| 8 Bastion art | **open** — naming resolved, art regression not yet chased |
| 9 build menu blocks bottom row | **fixed elsewhere** (`2a744b6`) — board lifted clear of the drawer |
| 10 lives readout does nothing | **likely closed elsewhere** (`ecd3ba2`) — verify on device |
| 11 13" iPad Pro scaling | **in flight elsewhere** — `fix/tablet-viewport` |
| 12 BULWARK/Barricade name | **fixed** |

Items 5, 9, 10 and 11 all moved because the tablet layout work opened up the rail, which is what
this doc predicted would happen if 11 was settled before the UI items rather than after.

**Status 2026-08-21 — second device round.** A fresh device export from `main` (`e8b2844`) was cut
for another live session; the previous export dated 2026-08-09 and predated everything below.
Changed since the last time this game was on the iPad, and worth checking with hands:

- **Late-match slowness attacked directly** — the thing reported from this device ("too many creeps
  causing slowness, even on an M4 iPad with 8 GB"). Creeps cost and take 2x, lives cut to 40,
  escalation steepened to 700/80: max peak concurrent creeps 899 -> 430 in measurement, match
  length unchanged. *Check: does the late game still stutter? Do matches feel the same length?*
- **Match feel changes that ride along:** every leak costs 1 life (siege/colossus no longer take 2),
  send prices doubled roster-wide, matches start at 40 lives. *Check: does the send dock economy
  still feel readable at the new prices?*
- **Bots attack and upgrade properly now** — two structural defects fixed (a bot could stop sending
  forever once prices outran one payout's surplus; a bot could never save for a category tier).
  *Check: do late-game opponents feel more alive? Towers visibly upgrade around tick ~2900.*
- **Tower breathe gated to Grove** — Arcane and Foundry towers no longer breathe at idle.
- **Send dock sized to its cards** — the picker no longer shrinks the board or leaves the gap above
  the close button (the two-things-to-fix screenshot from 2026-08-09).
- **Items 3 and 10 land on a device for the first time** — gate sprites tinted into the 3D scene,
  and the lives readout opening the leaderboard. Both were "fixed, unseen" last round.

Still open going into this round: **item 2** (send-card click effect — this session is the chance to
describe what "broken" looks like), **item 8** (Bastion art), **item 47** (send-queue cancel has no
button yet), **item 48** (send-dock card content drawn over the card frame, NEED +60 clipped).

Remaining to take: **item 8** (Bastion art, has a live lead — `5e81e50` decimated the roster and
`8457192`'s LOD fix covered only animated units) and **item 2** (blocked on you).

### Beyond this list, same session

- **Item 43 (bots hoard gold) closed by measurement, not by a fix.** Re-measured at 79-126 gold held
  and 94-152 sends per bot, against the ~29,000 it was filed on. Two earlier changes had already
  closed it. See `OPEN_ITEMS.md` 43 — the lesson recorded there is to re-measure a balance finding
  before building on it.
- **The send queue can now be undone.** `CancelQueuedSend` and `ClearSendQueue`, with the same seat
  authority and rate limiter as the enqueue. **The client adapter is wired but no button calls it
  yet — filed as `OPEN_ITEMS.md` 47** — the send dock needs a cancel affordance, deliberately not placed while the tablet-layout
  work is reshaping that surface. That is the one loose end from this pass.
- **Not done, and not mine to do:** `OPEN_ITEMS.md` 45 (income pin) needs an owner decision between
  two conflicting documented properties; Arcane's missing brake is a deliberate design choice, not a
  gap; store enrolment needs an Apple account and a real reverse-domain. The four unrigged creeps
  (Revenant, Shade, Swarm, Wisp) were **audited 2026-08-09 and need no rigs** — all four are
  non-walkers. See `CREEP_RIG_WAVE_2_4_PLAN.md`.

---

## 1. Creeps pause for a second or two when they have to turn — FIXED

**Fixed 2026-08-09.** It was the brake, presented wrongly.

`StepCreep` makes a braked creep bank against `MovementCost + BrambleMovementPenalty` — 3 + 6 = 9
ticks — but `CreepPresentationSnapshot` reported `MovementCost` alone. The renderer divides progress
by what it is given, so the interpolation fraction hit 1 after 3 of those 9 ticks and clamped there.
The creep crossed its cell in a third of the time and then stood **perfectly still for the remaining
6 ticks**. At 4 ticks/second that is 1.5 seconds motionless out of a 2.25 second cell.

Why it looked like turning: brake zones sit in the maze, and the maze is where the corners are, so
the pause and the turn always arrived together. The turn was never the cause — the model rotates at
540 deg/sec, so a corner takes 0.17s.

Fix is `EffectiveMovementCost` on the snapshot, derived beside the code that applies the penalty
rather than in the client — a renderer cannot get this right on its own, since it would need both
constants and to know they are added rather than multiplied.

**Two theories this replaced, both wrong, both tested before being dropped:** the stale-`PathIndex`
reading and the progress-reset-starvation reading. `Creeps_keep_walking_after_a_tower_reshapes_the_lane`
and `Building_steadily_does_not_hold_creeps_still` were written to catch them, both pass on
unmodified code, and both are kept — they pin real properties and they are the reason the search
moved on to the brake.

### Original notes and the correction that preceded the fix

**Mechanism found 2026-08-09. No code changed yet.** Ruled out the turn rotation itself first:
`CreepFacingYaw` turns at 540 deg/sec, so a 90-degree corner takes 0.17s and cannot be a
two-second stall.

The freeze is `NextPosition == Position`. Two presentation sites early-out on exactly that
condition, so when it holds the creep stops moving AND stops turning together, which is what makes
it read as a deliberate pause rather than a hitch:

- `CreepTravelPosition` (`UnityVerticalSliceRenderer.CreepPresentation.cs`) returns `from`
  unlerped.
- `CreepFacingYaw` (`UnityVerticalSliceRenderer.cs`) returns the previous yaw, because
  `heading.sqrMagnitude` is zero.

**CORRECTION, later the same day.** The paragraph below overstated the stale-index theory. A resync
already exists (`LocalVerticalSlice.cs`, the route-rebuild loop): it finds the nearest valid index
in the rebuilt route and calls `WithMovement(nearest, 0)`, which largely prevents
`route[PathIndex + 1]` from landing on the creep's own cell by accident. Two better candidates,
both in that resync:

1. **Its fallback can pin a creep to the last cell.** When no candidate passes the guard, it does
   `nearest = Math.Max(0, Math.Min(route.Count - 1, route.Count - 1 - remaining))`. With `remaining`
   at 0 that is `route.Count - 1`, and `ResolveNextPosition` clamps to the same index — so
   `NextPosition == Position` permanently and both presentation early-outs latch until the creep
   leaks. A freeze that never recovers fits "pauses for a second or two" better than a transient
   does. Check how often the fallback is actually reached.
2. **Progress is reset on every rebuild.** `WithMovement(nearest, 0)` drops `MovementProgress`
   deliberately (the comment explains why: it is a fraction of a step into a cell that has moved).
   But at `BaseMovementCost` 3 a creep needs three ticks to earn its next step, so repeated tower
   placements keep zeroing it and the creep can be kept from ever stepping.

Both are testable without a device. Neither is confirmed. The original reasoning follows because
the presentation half of it still holds — the two early-outs are real and are what turn any of
these into a visible stall.

Why the condition arises is the actual bug, and it is simulation-side.
`CombatService.ResolveNextPosition` derives the next cell by INDEX into the live route:

    var route = routes.For(creep.LaneId, creep.IgnoresMaze);
    return route[Math.Min(creep.PathIndex + 1, route.Count - 1)];

`creep.Position` is stored on the creep; `NextPosition` is looked up from the route array. Those
are two different sources of truth for where a creep is. When a tower placement re-mazes a lane the
route array is replaced while `PathIndex` carries over, so `route[PathIndex + 1]` can resolve to the
creep's own current cell — and re-mazing is precisely what puts corners in a lane, which is why the
report ties the stall to turning.

The clamp at the end of the route produces the same condition legitimately (a creep at the gate has
no next cell), so any fix has to keep that case and separate it from the stale-index case.

**Next step, and it is cheap:** log `PathIndex`, `Position`, `NextPosition` and `route.Count` for one
creep across a tower placement. If `NextPosition` equals `Position` while `PathIndex` is well short
of `route.Count - 1`, the stale-index reading is confirmed. `RouteRebuildTests` already exists from
`b882af3` and is the natural home for the regression test.

Not attempted here: this is a simulation change, needs a plugin rebuild and a Release test run, and
should not be started without the budget to finish and verify it.

### Original notes

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

## 3. Lift gate and spawn gate read as 2D sprites in a 3D world — FIXED, unseen on device

**Changed 2026-08-09, compile-verified, NOT seen on a device.** They are literally 2D sprites — a
`SpriteRenderer` on a flat plate — so the question was only why that read as one.

It was brightness, not geometry. A `SpriteRenderer` uses Unity's default UNLIT sprite material and
these were drawn at `Color.white`, against a board surface authored at 0.075-0.13. Roughly eight
times the value of everything touching it, and flat where lit surfaces fall off toward their edges.
Now tinted via `EndpointSpriteTint` — a neutral exposure drop rather than a hue, so the artwork
keeps its own colour — and non-player lanes take the same relative drop every other element on a
non-player lane takes.

A second fault sat behind the first: `UpdateSpawnGatePulse` assigned a flat grey to
`renderer.color` every frame, so the tint applied at creation was overwritten on the next frame and
**spawn gates would have kept blazing while only leak gates took the fix**. The pulse now multiplies
the base tint instead of replacing it, at the same depth.

**The two follow-ups, now resolved — and one of them was my own bad call.**

`sortingOrder = 3` is **not** a defect and was left as it was. The claim that it draws the plates
over the board regardless of depth was wrong: the default sprite material is `ZWrite Off` /
`ZTest LEqual`, so these still depth-test against opaque board geometry. It orders them against
other transparents only, and the only other one in the renderer is floating text. Documented in
place so the suspicion is not raised a third time.

Height was half right. The sprite plate path is an early `return` that REPLACES the whole procedural
endpoint, and the disc stack it stands in for spans -0.055 to +0.106 — so the spawn gate's +0.06 was
already inside the range its own furniture uses and did not need moving. The **leak** gate at +0.18
sat above everything, hovering over its own board furniture and parallaxing against the surface as
the camera moves, which is the part of "looks like a 2D sprite" that tinting cannot reach. Both gates
are now at 0.06. The comment above that call already recorded that lifted geometry at the far end of
the lane projects past the board's top edge under the tilted camera, and two builders had been
deleted for it; 0.18 was the same mistake left standing on the plate itself.

Noted in passing, not fixed: `spawnGateSpriteRenderers` is never cleared, so it grows on every
board rebuild and the update loop walks a lengthening list of nulls.

### Original notes

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

## 5. Add a leaderboard button — other players' lives and income — DONE (`5270592`, `c7af8cb`)

**Built as `SeatLeaderboardView`, with two homes rather than one.** The scope question below —
persistent strip or panel behind a button — turned out to have different answers by screen, and
answering it per screen is what let this close item 11 as well.

On a tablet the seat list is permanently up in the side rail, beside the board, using the margin
item 11 was complaining about. On a phone there is no rail (the margin was given back to the
board), so it is a panel the lives readout opens — which is item 10's answer as well as item 5's,
and the reason both are one component. That phone half matters more than it looks: until it
existed the component stood down completely without a rail, so this feature was quietly
tablet-only and a phone player could not see another seat's lives at all except by finishing the
match.

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

## 7. Upgrade button for creeps and wards is hard to read and too small — DONE elsewhere (`e75b08a`)

**Fixed by the parallel session**, by growing the card rather than shrinking the text — which is what the note below recommended.

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

## 9. Build menu blocks the bottom row — DONE (`2a744b6`, `de2f269`)

**Fixed by the parallel session**, and not by making the panel draggable: the board is lifted clear of the drawer instead. That is the alternative the note below argued for, on the grounds that a draggable panel competes with the board's own tap and drag handling.

**`de2f269` then did the same for the panel that appears *after* a tower is selected**, which was
the half of this the first fix left standing: a fixed 360x160 card over roughly four rows of lane,
at the moment the player is choosing a cell. It is now a two-row bar on a phone (76 units, board
lifts clear of it, and it stops short of the SEND launcher rather than running under it) and a
block in the side rail on a tablet, where it costs the board nothing. The rail variant publishes
no camera inset — the inset is measured from the screen bottom, so a rail block reported a huge
one and collapsed the board; only the bar is over the board, so only the bar contributes.

The build palette covers the last row of the lane, so you cannot see or place on the cells the
menu sits over.

Where to look: `TouchPlacementController.TowerPalettePanelRect`, currently pinned at
`282f * scale` at the bottom of the frame. Note this is the same fixed height already implicated
in item 7's cramped upgrade button — one change may serve both.

"Moveable while open" is one answer; worth considering the alternatives before building a drag
interaction on a touch surface, since a draggable panel competes with the board's own tap and
drag handling. A collapse/peek toggle, or shifting the board column up while the palette is open,
may get the same result with less to go wrong.

## 10. The lives readout in the top right does nothing — DONE (`ecd3ba2`, `c7af8cb`), unverified on device

**It was the affordance fault, not the binding one.** The readout updated correctly all along; it
was never a button. `ecd3ba2` stood it up in the left rail and off the board, and `c7af8cb` made
it do something: on a phone it now opens item 5's seat list, and the dead LIVE state cell next to
it — which showed a label with no value — is gone. On a tablet there is no toggle, because the
seat list is permanently in the rail and a control that hides information the screen has room for
is not worth the tap.

Still worth a device tap before closing: the button is wired the way `DrawCommandCard` is
(`GUI.Button` over the strip centre with `GUIStyle.none`) and there is no automated assertion that
the hit rect lands where the numbers are.

Reported as not responding. Two different bugs wear this shape and they need separating first:
the readout is **not updating** (a data binding problem), or it is **not tappable** when it looks
like it should be (an affordance problem — it may never have been a button).

Check what the top-right element is bound to before assuming either. If it is meant to open
something, that overlaps item 5's leaderboard — a lives readout that expands into the full seat
list may be the natural home for that feature rather than a separate button.

## 11. The game does not fill a 13" iPad Pro screen — DONE (`5270592`, `ecd3ba2`, `fa97a05`)

**Closed by the third option below: the extra width became the leaderboard.** The board itself
could not grow to fill it — `orthographicSize` is vertical and the lane already fills the view at
16.9 world units against 16 cells, so widening the column only crops the player's own lane. What
changed instead is that the margin stopped being padding: `BoardColumnFraction()` adapts to the
screen, `HasSideRails` turns on where there is room for at least 12% a side, and the rails carry
the seat list on the right and the HUD, and now the placement controls, on the left.

The scope note at the bottom of this section was right and worth recording: items 5, 7, 9 and 10
all place UI, and settling the tablet question first is what let each of them land in one shape
per screen rather than being built phone-only and redone.

### Original diagnosis

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

## 13. The seats table should be clickable and jump to that lane — replacing the lane-view button — DONE (`fc10745`)

Reported 2026-08-21, second device round. **Done the same day:** rows are buttons (invisible,
drawn after the row content per HudView's draw-order rule), the viewed lane's row carries a mint
rail on its right edge — left rail says who you are, right says where you are looking — and
`LaneViewToggleController` is deleted. Desktop Tab cycles lanes through the renderer directly; the
capture runner's lane-selector state now captures the resting HUD. Unverified on device. Tapping a row in the SEATS table should move the board to
that seat's lane. That makes the standalone lane-view button (`LaneViewToggleController`'s L1-L8
selector) redundant — two controls for the same navigation, and the seats table is the one that
carries context (who you are looking at, how they are doing) rather than a bare lane number.

First read: `SeatLeaderboardView` draws the rows but holds no renderer reference; the jump is
`UnityVerticalSliceRenderer.SetActiveLaneCameraId`, which the lane button already calls. A row needs
to become a button, the view needs the renderer, and the current camera's row wants an indicator so
the table also absorbs the button's second job — showing WHICH lane you are on.

## 14. The send sub panel should be built out like the build sub panel on tablets — DONE (`fc10745`)

Reported 2026-08-21. **Done the same day:** on a tablet the expanded dock now lives in the right
rail below the seats table, exactly as the placement controls live in the left one — no board
coverage, no camera lift (SendDockInset stays 0 in rail mode). States stack: compact category rows
with tier/upgrade lines, compact creep rows with cost/income/queue meta. Compact rows rather than
art cards is the placement stack's own trade — at rail width an aspect-held card outruns the rail.
Phone drawer unchanged. **Verified by capture at both aspects 2026-08-21** (`a8dd17a`): the phone
drawer photographs identically to its pre-rail behaviour, the tablet rail panel lands correctly
below the seats table with the board at full height. Three header collisions found and fixed from
the captures. Follow-up filed by the same captures: the BUILD palette on a tablet is still a bottom
drawer that lifts the camera to half height — after this item the send side has the better tablet
treatment, and the build palette wants the same rail move. The tablet layout work gave the build (tower) picker a fuller treatment; the
send dock's category/creep sub panel should match it — same structural pattern, not a phone panel
scaled up.

## 15. A data view for how many creeps you have in queue — DONE (`fc10745`)

Reported 2026-08-21. **Done the same day:** the closed SEND launcher shows the total (`SEND x3`),
the open panel shows a QUEUE line in both layouts, per-creep counts stay on the cards. New
`TotalQueuedSends()` on the adapter, read off the snapshot like `QueuedSendCount`. The QUEUE
readout is the natural anchor for item 47's still-missing cancel control. Queued sends are invisible: `CancelQueuedSend`/`ClearSendQueue` landed
simulation-side (see item 47's history) but nothing on screen even says how many are waiting. A
count readout is the minimum; it is also the natural anchor for item 47's missing cancel control —
whichever surface shows the count is where cancelling belongs.

## 16. Creep health bars: blocky, hard to read, little value — DONE (pass on 2026-08-21)

Reported 2026-08-21 in chat: clean them up properly (styling, lighting, readability) or remove
them. Root cause of the blockiness: the bars were two LIT 3D cubes — scene lighting shaded them
like crates, the tilted camera saw their side faces as a second tone, and the fill-cube stacked
over the backing-cube seamed.

Rebuilt as one unlit quad per creep through the same LTW/Fill Bar shader the lane pressure gauges
use: lighting cannot touch it, fill is the shader's anti-aliased _Fill threshold rather than scaled
geometry, the housing rides _BackgroundColor so nothing seams, and every bar shares the gauges' one
instanced material — which also retires the CreatePrimitive path behind 2026-08-01's magenta bars.
Kept: bars only appear once a creep is damaged, and the mint/gold/red state colours.

Calibrated against captures, three rounds: a camera billboard was tried first and photographed
WORSE — tilting a bar toward the camera leans it into the screen space of the creep marching
behind, and in a packed train each bar vanished behind its neighbour — so the bar lies flat like
the gauges, with height drawn 1.6x to buy back the tilt's foreshortening and width 1.35x because
the authored constants were tuned against the old 2D plates and spanned about a third of the 3D
bodies. Verified in capture at gameplay zoom; wants a device look for the removal question — if it
still carries too little value on the iPad, the show/hide gate is one line.

## Related open work, not on this list

- **Twin Crescent has no visual profile.** ~~It renders a primitive fallback that nothing
  animates and measures 0.00/0.00 on the motion probe.~~ **Fixed 2026-08-09:** the claim was
  correct — the wrapper prefab existed but `TowerVisualLibrary.asset` was never given a role-15
  entry (the promote step was skipped during integration), and the motion switch had no
  TwinCrescent case. Both are in now. Not yet sighted on a device, and the motion probe still
  needs a run once the editor releases the project lock.
- **Four creeps have no Animator** — Revenant, Shade, Swarm, Wisp (item 11 wave 2.4). **Audited
  2026-08-09: none of them needs one.** All four are non-walkers — a stalk, a core with satellites,
  a petal mass and a floating orb — and each already carries a procedural motion style, so they do
  animate. The earlier wording here ("will not animate whatever else is fixed") was wrong: unrigged
  has never meant unanimated. See `CREEP_RIG_WAVE_2_4_PLAN.md`.
- **`m_CullingMode` on the rigged creeps** was set to `AlwaysAnimate` in `1145a0d` during the
  device freeze. Do not read the LOD fix (`8457192`) as proving that change unmotivated: the
  LOD bug fully explains the original report — including the tell that the builder kept
  animating, since the builder is the one animated unit with no LODGroup — but it does not
  prove `CullUpdateTransforms` was innocent. The bounds-don't-follow-the-rig mechanism 1145a0d
  describes is real and device-only, and every working device build since has had
  `AlwaysAnimate` in place, so its necessity has never been tested. Revert it only with a
  device build showing clips still play under `CullUpdateTransforms`; until then it is a small
  CPU cost buying known-correct behaviour.
