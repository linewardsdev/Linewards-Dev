# Board Text — Review And Proposal

Status: **built** (2026-08-01), except the aggregation item — see "What shipped" at the end. Prompted by a review note that the board text is
"very blocky and plain and there is far too much of it popping up". Both halves measure out.

## Everything that puts text on the board

Sixteen call sites, all through `UnityVerticalSliceRenderer.SpawnFloatingText`. Counts are from one
full 8-lane bot match, seed 1, 2400 ticks (~600s of play), counted off the simulation events that
drive them.

| Text | Trigger | Count | Share |
| --- | --- | --- | --- |
| Damage number | `CreepDamagedEvent`, only when damage >= 5 | 7,501 | 34.7% |
| Creep name on spawn | `CreepSpawnedEvent` | 4,470 | 20.6% |
| `+N` kill bounty | `CreepKilledEvent` | 3,674 | 17.0% |
| `SEND` banner | `CreepQueuedEvent` | 2,623 | 12.1% |
| Relay `+1` | `TowerEarnedGoldEvent` | 1,900 | 8.8% |
| `-N LIFE`, `+N` bounty, `+N LIFE` steal | `LeakEvent`, up to 3 texts per leak | 692 | 3.2% |
| `WARD` | `TowerPlacedEvent` | 432 | 2.0% |
| `+N income` | `IncomeTickEvent` | 354 | 1.6% |
| `TRANSFER` | creep crossing to the next lane | (in spawn count) | — |
| `PLAYER n OUT` / `PLAYER n WINS` | elimination, match end | 1 | ~0% |
| `REVEAL` | Shade creep revealed | rare | — |
| `+N` sell refund | `TowerSoldEvent` | rare | — |

**21,647 popups in 600 seconds — 36 per second.**

## Why there is too much of it

Three separate causes, and only one of them is about volume per se.

### 1. None of it is filtered to the lane you are watching

The event loop draws text for all eight lanes. The camera frames one. So roughly **seven eighths of
every text object spawned is for a lane nobody can see** — it is instantiated, positioned,
billboarded, sorted and pooled, then expires off-camera.

This is the single cheapest fix in this document and it costs nothing in design: the text that
matters to the player is not affected at all, because the player is looking at their own lane.

### 2. The high-frequency items duplicate information already on screen

- **Damage numbers (34.7%)** sit on top of a creep that already has a health bar. The bar shows the
  same fact continuously and more precisely than a number that appears for 0.32s.
- **Creep names on spawn (20.6%)** label a model whose entire silhouette pass exists to make it
  identifiable without a label. `TOWER_ANIMATION_ALIGNMENT.md` and the creep silhouette work both
  argue the shape should carry the identity; the label says it does not.
- **`WARD` on build (2%)** announces an action the player just took, at the cell they just tapped.

### 3. It is Unity's default font, unstyled

`SpawnFloatingText` uses the legacy `TextMesh` component and never assigns `font`, so every label on
the board is **Unity's built-in Arial**, unlit, flat-coloured, with no outline and no shadow, over a
busy board. That is the "blocky and plain" read, and it is also exactly what the improvement cycle
scores as **C10 Typography: "Default engine font"** against a target of "an authored typeface,
consistently applied, legible at phone size".

There is a `sortingOrder` override on the renderer to keep it above board decoration, which works,
but it is the only styling the text has.

## Proposal

### Cut

| Text | Why |
| --- | --- |
| Damage number | The health bar already says it, continuously and exactly |
| Creep name on spawn | The silhouette is supposed to carry this; if it does not, fix the model |
| `WARD` on build | Confirms an action the player just performed at a cell they just tapped |
| `REVEAL` | The Shade's own reveal VFX is the tell; the word is redundant |

That removes **57% of all board text** and no information the player did not already have.

### Keep, because each is a resource change the player cannot otherwise see

`+N` kill bounty, `+N income`, `-N LIFE` / `+N LIFE` steal, Relay `+1`, `PLAYER n OUT` / `WINS`.

Two adjustments to what remains:

- **Aggregate the fast ones.** Relay `+1` fires on every hit and kill bounty on every kill; both
  read as spam at rate. Accumulate per tower per second and emit one `+7` rather than seven `+1`s.
- **Restrict `SEND` to the local player's own sends.** It is 12% of all text, and a banner for an
  opponent's send into someone else's lane is not actionable.

### Make what is left look better

The constraint worth deciding first: **TextMeshPro is not in the package manifest and there are no
font assets in the project at all.** The manifest is deliberately slim, so this is a dependency
decision rather than a styling one — the same shape as the particle module in
`TOWER_WEAPON_VFX_PROPOSAL.md`, which turned out to be the reason fourteen VFX prefabs had never
been authorable.

Two routes:

**A. Add TextMeshPro and an authored font.** Crisp at any scale, real outline and gradient, proper
kerning. Costs a package dependency and a font asset with a license to record in the art pipeline's
provenance log. This is what C10 actually asks for.

**B. Style the legacy TextMesh.** No new dependency. An outline can be faked by drawing the label
four times offset by a pixel behind the main draw, plus a drop shadow and a rise-and-fade with a
small scale punch on spawn. Cheaper to do, visibly better than today, and still Arial underneath —
it would not clear C10.

**Recommendation: A.** Board text is the last place in this game still shipping engine defaults, the
improvement cycle scores it as a blocking category from Wave 3, and route B leaves the underlying
typeface unchanged while spending most of the same effort.

Either way the motion should change with it: text currently appears, holds and vanishes. A short
rise with a fade-out and a scale punch on the first frames reads as an event rather than as a label
switching on.

## What is not in scope

Balance, and the HUD stats bar. This is only about text drawn into the world on the board.

## What shipped

Route A, as recommended.

- **TextMeshPro added** (`com.unity.ugui@2.5.0`) and its essential resources imported, which is what
  creates a usable font asset. `TmpEssentialsImporter` does that import in batch, because the normal
  path is a modal editor prompt a batch run never sees — and until the import happens
  `TMP_Settings.defaultFontAsset` is null and every TMP component renders **nothing, silently**.
- **Board labels are now SDF text with a dark outline**, on one shared material so they batch.
- **Motion**: labels rise, hold solid for the first half of their life, then fade, with a short scale
  overshoot on arrival. They used to appear, hold and vanish.
- **Lane filter**: labels are skipped entirely for lanes the camera is not framing.
- **Cuts**: damage numbers, creep spawn names, `WARD` on build, and `REVEAL` are gone, and the `SEND`
  banner is now only drawn for the local player's own sends.

### Getting the outline to render took three attempts

Recorded because the first two look correct and produce flat glyphs with no error:

1. `fontMaterial.EnableKeyword("OUTLINE_ON")` plus `SetFloat("_OutlineWidth", ...)` — no outline.
2. TMP's per-component `outlineWidth` / `outlineColor` — no outline.
3. A shared `Material` built from the font's own material with the keyword enabled, assigned through
   `fontSharedMaterial` — works, and batches.

### Not done

**Aggregation.** The proposal called for accumulating the fast labels — Relay `+1` and kill bounty —
into one `+7` per tower per second rather than seven `+1`s. That is still outstanding, and it is the
remaining volume item now that the cuts have landed.

### Layout pass (2026-09-01)

Prompted by the live render review: a leak drew `-1 LIFE`, `+4` and a cue word in one cell on top of
each other, two creeps leaking on the same tick drew two `-1 LIFE` through each other, and
`+441 income` floated beside the spawn gate looking like a different font. Three changes, all in
`SpawnBoardLabel` (which `SpawnFloatingText` / `SpawnFloatingAmount` feed):

- **Kinds.** Every label now carries a `BoardLabelKind` — `Text`, `Gold`, `Income`, `LifeLost`,
  `LifeStolen`, `Send` — passed by the caller, never parsed back out of the string. The kind owns
  the wording, so `-2 LIVES` and `+7` are produced by one formatter rather than by the call sites.
- **Merge.** A same-kind label within 0.6 units and 0.2s of a live one is folded into it: the
  earlier label's amount absorbs the new one, its text is rewritten as the aggregate, and its clock
  restarts. `Text` never merges. `Income` merges for as long as the earlier label is alive, which
  is what caps it to one per lane. This is the same-tick half of the aggregation item above — the
  per-second accumulation of Relay `+1` is still not done, and 0.2s is deliberately shorter than
  one tick so that it does not become that by accident.
- **Stack.** Anything else within 0.6 units of a live label is lifted 0.35 units per neighbour,
  capped at four, so simultaneous labels at one cell read as a column. No kind test: a cue word and
  a bounty at the same creep still want separate lines. The leak bounty lost its old half-cell
  sideways nudge for this — a nudge on top of a lift read as a diagonal.

Typeface: the board was already in the HUD's family — `RuntimeUiChrome.SharedFont` is LiberationSans
as a legacy Font, and the board's TMP default is LiberationSans SDF — but only by inheriting
`TMP_Settings.defaultFontAsset` without ever setting `font`. It is now set explicitly from
`RuntimeUiChrome.SharedBoardFont`, and the outlined material moved next to it as
`RuntimeUiChrome.SharedBoardTextMaterial`, so both halves of the typeface question are answered in
one file. The three-attempts note about the outline lives on that property now. One point size
(`BoardLabelFontSize`) for every kind — `+441 income` was never larger, just longer.

The legacy `TextMesh` lane text on the board (`YOUR LINE - DEFEND`, `TARGET n`, the never-called
`SPAWN` / `LEAK`) went in the same pass: 0.03-0.04 scale at the shipped camera is a 2-px smudge, and
the HUD's seats table already names every lane. The badge's underline band went with it — it was in
the inter-lane margin, tinted the same way as the NorthAnchor purple line the 2026-08-31 pass
removed.

### The batch playtest is timing out, and it is not this work

Worth flagging separately. `LocalPlaytestBatchRunner` has a 180s timeout, and the last three passing
runs took 99s, 62s and **145s** with match length climbing (4069, 3815, 4471 ticks). It now fails
intermittently, and it failed on a **clean tree with every change here stashed**, so it is not caused
by this pass.

It also failed *silently*: `Finish` recorded the reason only into the report it writes on success, so
a timeout exited 1 with an empty log. An hour went into bisecting changes that were not the cause.
`Finish` now logs the reason. The timeout itself, and whatever is making matches longer, is left for
whoever owns the economy work.
