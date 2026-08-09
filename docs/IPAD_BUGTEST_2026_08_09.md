# iPad Bug Test — 2026-08-09

Reported from live play on iPad, against a build containing the LOD fix (`8457192`) that
unfroze creep and ward animation. Eight items, unverified by me — this is the reporter's list
as given, with a first read on where each one probably lives. Nothing here has been reproduced
or diagnosed yet; the pointers are starting places, not conclusions.

Branch: `ipad-bugtest-2026-08-09`.

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

## 4. Weird blue squares at the four corners

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

## 6. Remove the MULTI button

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
