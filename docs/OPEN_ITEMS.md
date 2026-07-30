# Open Items

Known gaps and decisions left after the graphics overhaul and URP migration
(2026-07-25 to 2026-07-26). The full history that produced these — diagnosis,
migration phases, the tower material-assignment bug and its fix — lived in
`URP_MIGRATION.md` and `GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md`, both now
retired since the work they tracked is done; recover that detail from git
history (`git log --all --full-history -- docs/URP_MIGRATION.md`) if it's
ever needed again.

## 1. `_EMISSION` keyword loss on the five tower body materials — now self-healing, root cause still unknown

`mat_tower_{arrow,control,relay,pulse,prism}_3d_body_runtime_v01.mat` lost
their `_EMISSION` shader keyword and went silently non-emissive repeatedly
(at least 4 times) across two sessions, from a trigger that was never
isolated — it recurred from a plain compile-only Editor pass with no
material-touching code involved, and from ordinary batchmode capture runs.
Manually re-enabling it after each occurrence stopped scaling.

**Fixed with a guard, not a root-cause fix**: `Assets/Editor/TowerEmissionKeywordGuard.cs`
runs on every asset import pass (`AssetPostprocessor.OnPostprocessAllAssets`)
and silently re-enables `_EMISSION` + resets `globalIlluminationFlags` to
`.None` on any of the 5 materials found wrong. Verified working by
deliberately stripping the keyword on Control's body material and confirming
the guard corrected it within the same batchmode pass (visible in the log as
`TowerEmissionKeywordGuard: re-applied the _EMISSION fix to 1 tower body
material(s).`).

This means the symptom can no longer ship broken, but the actual trigger —
what in Unity's import/reimport pipeline keeps stripping this keyword in the
first place — is still unknown. If tower emission ever looks wrong despite
the guard, check the Editor log for repeated `TowerEmissionKeywordGuard`
corrections firing every session, which would mean something is fighting it
faster than expected; that would be the signal to actually root-cause this
rather than lean on the guard indefinitely.

## 2. Bloom cost on a physical Android device is unmeasured

Bloom and tonemapping were tuned and verified in the Editor and via headless
capture only. Nobody has profiled the post-processing cost on a real Android
device. Deferred deliberately — no device access during this work — and not
urgent; revisit before shipping.

## 3. No drop has a normal map (decision needed, not a bug)

Meshy was never asked to generate normal maps for any of the 5 towers or 5
creeps, so all ten currently render flatter than they could. The intake
scorecard already reports this (`has_normal_map` check in
`tools/art_pipeline/ai_asset_intake.py`), so it won't pass unnoticed. Two
options, neither pursued:

- Re-run Meshy generation for a given asset with normal maps requested (costs
  generation credits).
- Fake one from the existing albedo via a height-derived bake (Blender can do
  this) — a real visual/artistic tradeoff, not attempted without a look first.

## 4. Leg rigs on shell-bodied creeps may be invisible from the game camera — check before investing further

Building a two-segment (thigh+shin) leg rig for the Brute revealed that its
armor shell overhangs to the ground on every side, fully hiding the legs from
the actual ~30-degree top-down game camera — confirmed by rendering the rig
from that exact camera angle (not the eye-level angle `rig_quadruped_creep.py`
defaults its verification renders to), where zero leg geometry is visible at
any frame of the walk cycle. The knee-bend rig itself works and is kept
(Unity-verified, 10/11 bones driven, no regression to the existing silhouette
timing), but it produces no visible gameplay difference for this creature.

Follow-up (2026-07-28): the Spire Turret Walker was checked from the game camera this
way and its legs turned out fully visible, unlike the Brute's — but the check surfaced two
different defects instead (51x foot skate, and a symmetric trot whose mirrored contact
poses are indistinguishable head-on). It now has its own rig script,
`tools/art_pipeline/rig_turret_walker.py`; see `docs/GD_TUNING_LOG.md`. The camera-angle
check below remains the right first step and paid for itself again.

If `rig_quadruped_creep.py` is ever reused for another quadruped-shaped creep,
render-check from the actual game camera angle first, before investing in leg
articulation — for a low, wide, or heavily-armored silhouette the legs may
again be fully occluded, in which case body/head motion (bob, rock, and a
rigid uniform scale pulse — added for the Brute in this same pass) is the only
lever that will actually read to a player.

---

# Repo and Working-Tree Review (2026-07-29)

Findings from a codebase and git-state review while two agents were working
the repo in parallel (one in the `main` worktree, one in the
`repair-drone-servicing-tether` worktree).

**Two passes.** The first pass covered git state and structure only and
concluded "no functional bugs" — that conclusion was wrong, and it was wrong
because the pass grepped the code instead of reading it. A second pass that
actually read `src/LTW.Simulation/` and the Unity client found two critical
simulation bugs and three critical client bugs, recorded as items 10–14 below.
Treat "a review found nothing" as a claim about the review, not the code,
unless the review says what it read.

Neither pass compiled anything — there was no .NET 10 SDK on the review
machine and package sources were unreachable — so every item here is static
analysis. Confirm each against `dotnet build` / `dotnet test` before acting,
and note that items 10 and 11 in particular predict *match-scale* behaviour
that only a long-running match or a targeted test will actually demonstrate.

## 5. ~~Uncommitted bot-mazing rework~~ — resolved 2026-07-29

Resolved while the review was still running. The bot-mazing rework
(`BestMazingPlacement` replacing the hardcoded nine-cell list) and its test
file `tests/LTW.Tests/BotMazingTests.cs` were both committed in
`2cf96e1 Teach the bots to maze, which invalidates most of the balance work`.
Kept here only so the numbering of the items that follow stays stable.

The commit message's own conclusion is the load-bearing part and is worth
restating: because the bots never mazed, **every balance measurement taken
against them was taken against a straight lane**, so the tuning record in
`docs/GD_TUNING_LOG.md` predating this commit describes a game that was never
being played. Items 10 and 15 compound this — re-measure rather than trusting
those numbers.

## 6. Orphaned `BotPlacementCandidates` left behind by that refactor

The mazing change replaced every caller of `BotPlacementCandidates(...)`
(`LocalVerticalSlice.cs`, ~line 666), but the method itself was left in place.
It now has no callers in source (the only remaining matches are stale build
DLLs under `bin/`/`obj/`). It is dead code — a half-finished refactor, not a
build break: an unused private method is an analyzer suggestion (IDE0051), not
a compiler warning, so `TreatWarningsAsErrors` does not catch it. Delete it
when committing item 5.

## 7. `.gitignore` ignores `*.csproj` and `*.sln` globally — mostly mitigated

The "Unity-generated IDE files" block lists bare `*.csproj` and `*.sln`. A
re-check found this is **already largely handled**: a "Keep the root .NET
solution and projects" block below it negates `!LTW.sln`, `!src/**/*.csproj`
and `!tests/**/*.csproj`, so everything currently in the solution is tracked.

The narrow remaining trap: a new .NET project created **outside** `src/` or
`tests/` still gets silently ignored. Either accept that as a convention (all
projects live in `src/` or `tests/`) and note it in `docs/AGENTS.md`, or add
the corresponding negation when such a project is first added. Not urgent.

Related and unresolved: `src/LTW.MatchServer/` contains only a `README.md` and
is **not** in `LTW.sln`. That is intentional (deferred online authority), but
it means the directory looks like a project and builds as nothing — worth a
line in its README saying so explicitly.

## 8. Two git author identities for the same contributor

Commit history carries `nanncee` under two emails —
`132762218+nanncee@users.noreply.github.com` (~157 commits) and
`eng.chase@gmail.com` (~145). This splits attribution and blame. Set a single
consistent `user.email` (per worktree if the two agents commit as the same
person) so history stays clean going forward.

## 9. Dangling worktree, and a note on `.git/index.lock`

- **`.git/index.lock`** — the first pass flagged a zero-byte lock file as a
  crashed git operation. On re-check it had cleared, then reappeared, then
  cleared again: it is the **live agent committing**, not a stale lock. Recorded
  as a correction, because "stale lock, delete it" is dangerous advice while a
  second agent is mid-commit — deleting a live lock can corrupt the index. Only
  remove it after confirming no git process is running (`pgrep git`).
- **Dangling worktree** — `/Users/admin/LTW-servicing-tether` (branch
  `repair-drone-servicing-tether`) is still registered and marked **prunable**;
  its directory is gone. `git worktree prune`, then delete the branch once its
  work is confirmed merged. This one is real and still outstanding.

## Working-tree churn is the norm here, not an incident

Over roughly 40 minutes of review, `main` advanced through at least six commits
and `HEAD` moved four times (`bedd03b` → `2cf96e1` → `fa9f5b5` → `3a7d221` →
`c92d6fb`); a test file changed mid-read; and untracked probe files
(`TempMazeProbe.cs`, then `MazedLane.cs`/`MazedLaneTests.cs`) appeared and
disappeared. This review's own edits to `docs/OPEN_ITEMS.md` were swept into
another agent's commit (`fa9f5b5`) rather than committed deliberately.

Two consequences worth planning around rather than fixing:

- Any review, benchmark or measurement of this repo needs to record the commit
  SHA it was taken at, or it cannot be reproduced or trusted later.
- An agent running `git add -A` / `git commit -a` will pick up whatever the
  other agent has in flight, which is how unrelated work ends up in a commit
  whose message does not mention it. Prefer explicit pathspecs when committing.

Short-lived scratch tests (`TempMazeProbe`, `MazedLane`) are fine as a working
style, but they should be deleted or promoted before commit — a probe committed
by accident becomes a test nobody owns.

---

# Code Review — Second Pass (2026-07-29)

Static review of `src/LTW.Simulation/` and `unity/LTW.UnityClient/`, reading the
code rather than grepping it. Line numbers were accurate at `c92d6fb` and the
file is moving; re-locate by symbol name, not line.

The criticals (items 10, 12, 13, 14) share a shape worth naming: **each is a
place where one concept is computed in two places, and the copies disagree.**
That is the characteristic failure of two agents working in parallel, and it is
the thing to grep for when looking for the next one.

> **Revised 2026-07-29 after a design clarification.** The first version of this
> section misread the lane-transfer mechanic and described creeps continuing
> lane-to-lane as if it were unimplemented. It is implemented, intentional, and
> tested — see the design note below. Item 11 was reclassified from Critical to
> Moderate as a result, and item 10's recommended fix changed. The design note is
> placed before the items because two of them cannot be read correctly without it.

## Design note — creeps flow lane to lane, carrying damage

The intended mechanic, confirmed by the project owner and verified against the
code: **a creep that reaches the end of a lane is not despawned. It continues
into the next active opponent's lane, carrying its current damaged health, and
keeps flowing lane to lane until something kills it.**

This is implemented and correct. The path, end to end:

1. `CombatService.MoveCreeps` — when `pathIndex >= route.Count - 1`, calls
   `CreepCombatState.MarkLeaked()` and emits a `LeakEvent`.
2. `LocalVerticalSlice.AdvanceOneTick` — handles that `LeakEvent`: applies the
   lives loss and bounty via `EconomyService.ApplyLeak`, asks
   `NextActiveOpponentLaneId` (→ `LocalMatchTopology.NextActiveOpponentLaneAfterLeak`)
   for the next lane, and if one exists calls `CombatService.TransferCreep`.
3. `CombatService.TransferCreep` — constructs the continuing creep with
   **`creep.Health` passed straight through**, `pathIndex: 0`,
   `movementProgress: 0`, `hasLeaked: false`, under a **new `EntityId`**.

Health preservation is covered by tests, not just by inspection:
`VerticalSliceBridgeTests.Leaked_creeps_keep_current_health_when_entering_next_lane`
damages a 10 hp creep in lane 2 and asserts it arrives in lane 3 at **8** hp
(not `MaxHealth`), alongside `Leaked_creeps_continue_into_the_next_lane` and
`Eight_lane_leaked_creeps_flow_across_expanded_opponent_lanes`.

Two properties of the implementation are worth knowing before changing anything
here:

- **Identity is not preserved across lanes.** Each hop mints a new `EntityId`.
  The Unity renderer keys its GameObjects by `EntityId`, so a hopping creep is
  destroyed and recreated rather than continuing — animation and any
  interpolation reset at the lane boundary. There are deliberate "TRANSFER
  arrival" cues in the renderer, so this may be intended masking; if a creep is
  ever meant to visibly *flow* across the boundary, this is the thing that has
  to change. It also means per-creep telemetry cannot follow one creep across
  lanes.
- **`HasLeaked` is load-bearing within a tick, not a leftover.** `MoveCreeps`
  marks it partway through `CombatService.Advance`, and the two later phases in
  that same `Advance` — `ResolveLandedShells` and `AttackWithTowers` — must skip
  the spent creep. Removal cannot happen inside `Advance`, because choosing the
  next lane needs topology the `CombatService` does not have. So the flag is
  correct; only its *persistence beyond the tick* is wrong (item 11).

## 10. CRITICAL — bot pressure check omits `HasLeaked`, so bots stop sending

`LocalVerticalSlice.cs`, bot pressure check (`ShouldBotSend`, ~line 533):

```csharp
.Where(creep => creep.LaneId.Equals(myLane) && !creep.IsDead)
```

Every other creep filter in the codebase uses `!IsDead && !HasLeaked` —
verified at eight sites in `CombatService.cs` (lines 66, 98, 121, 290, 372,
562, 603 and the guard at 129). This one drops `HasLeaked`.

**This bug survives the design clarification, and is now the primary finding.**
The reasoning is if anything stronger than before. Per the design note, a
transferred creep is a *new entity in a new lane*; the spent entity it left
behind stays in `CombatState` forever (item 11), still tagged with the lane it
exited and still holding the health it had when it exited — it left the lane, it
was not killed, so its `Health` is `> 0` and `IsDead` is `false`.

So this filter counts, as "incoming pressure in my lane", every creep that has
ever *finished* walking that lane. `incomingHealth` **grows monotonically for the
whole match**. Once it crosses `PressureThreshold` (`BotController.cs`, ~line 65:
roughly `70 + 15 × towers` for Balanced), the bot never sends again.

What makes this the primary finding: those spent entities are filtered out
**everywhere else** — all eight `CombatService` sites above, and
`GetCreepSnapshots`, which is why nothing is visibly wrong on screen. This one
filter is the *only* consumer in the codebase that sees them. It is exactly the
failure mode the `PressureThreshold` doc comment claims to have fixed, and a
plausible second cause of the "bot sitting on thousands of gold" symptom that
`2cf96e1` attributed solely to the placement ceiling. Fixing the placement
ceiling did not fix this. **Any balance measurement involving bot sends is
suspect until this is fixed and re-measured.**

**Recommended fix — fix item 11, not this line.** Adding `&& !creep.HasLeaked`
here makes the symptom go away and is consistent with the other eight sites, so
it is a reasonable belt-and-braces addition. But it leaves a growing collection
of dead entries that every per-tick filter still pays for, and leaves the next
person who writes a creep query free to make the same mistake. Removing the spent
entity at transfer time (item 11) makes this filter correct as written.

## 11. ~~MODERATE — every lane hop leaves a permanent spent entity behind~~ — resolved 2026-07-30

**Fixed as recommended below.** `LocalVerticalSlice.AdvanceOneTick`'s `LeakEvent`
handler now calls `combatState.RemoveCreep(leak.CreepEntityId)` unconditionally,
before the transfer branch, so a spent entity is removed whether or not a next
lane exists for it. Verified: all three lane-transfer tests pass unchanged, and
a new test, `Spent_transfer_entities_do_not_accumulate_across_lane_hops`, sends
one creep through 5 lane hops with no towers present (so it never dies, only
hops) and asserts `CombatState.Creeps.Count` stays at 1 after every hop instead
of growing — 175/175 tests pass. `DiagnosticCombatEntityCount()` was added to
`LocalVerticalSlice` to make the count observable, since `GetSnapshot` already
filters `HasLeaked` and so cannot show a tombstone even if one were still there.

**Reclassified from Critical before the fix, and re-framed.** The original text
said "leaked creeps are never removed" and implied they *should* be despawned on
reaching the lane end. That was the wrong design assumption — despawning them
would break the intended mechanic. The real defect was narrower and still real.

When a creep transfers, `TransferCreep` mints a **new** entity for the next lane
and the **old** entity is left in `combatState.Creeps`, frozen, `HasLeaked`, with
health `> 0`. `CombatState.RemoveCreep` has exactly one caller —
`CombatService.DamageCreep` (~line 672), the death path — so nothing ever removes
it. It is a tombstone: its successor is alive elsewhere, and it is not.

Under the clarified design this accumulates *faster* than the original text
assumed, because hopping many lanes is the normal life of a creep, not an edge
case: one creep crossing eight lanes leaves eight tombstones, and only the entity
that finally dies is ever removed. The transfer path also never removes the spent
entity when `nextLaneId is null` (every other seat eliminated), so those persist
too.

Consequences, stated precisely, because most of them are *not* correctness bugs:

- **Correctness: one site.** Only the bot pressure check (item 10) reads these.
  Combat targeting, movement and `GetCreepSnapshots` all filter `!HasLeaked`, so
  there are no phantom targets and **no ghost creeps rendered** at lane ends.
- **Performance: real, and quadratic.** Every tombstone is paid for on every tick
  by the `MoveCreeps` filter, each tower's candidate filter in `AttackWithTowers`
  / `ResolveLandedShells` / `GetTowerAimSnapshots`, `CrowdBloomBonus`, and the
  `creepsBeforeCombat` dictionary rebuild. Worse, `CombatState.Replace` does
  `values.ToArray()` and the constructor `ToArray()`s both lists again, so
  movement alone is O(creeps × (creeps + towers)) array copies per tick — where
  `creeps` counts tombstones. On a 4 Hz mobile sim over a long match this is the
  dominant cost, and it grows without bound.

### Minimal correct change

Remove the spent entity at the moment of transfer, in the `LeakEvent` handler in
`LocalVerticalSlice.AdvanceOneTick`. It currently does:

```csharp
var transferred = combat.TransferCreep(NextEntityId(), leakedCreep, laneId);
combatState = new CombatState(combatState.Creeps.Concat(new[] { transferred }), combatState.Towers);
```

Drop the spent entity in the same rebuild, and do it **unconditionally** — outside
the `if (nextLaneId is not null)` branch — so a creep with nowhere left to go is
also removed rather than becoming a permanent tombstone:

```csharp
combatState = combatState.RemoveCreep(leak.CreepEntityId);   // always
if (nextLaneId is not null)
{
    // ... existing transfer, then Concat the new entity
}
```

That is the whole fix. It gives `RemoveCreep` its second caller, makes item 10's
filter correct as written, and bounds `CombatState.Creeps` to live creeps. Keep
`MarkLeaked`/`HasLeaked` — per the design note they are still needed *within* the
tick, and the existing filters should stay as defence in depth.

Two things to verify rather than assume when making this change:

- **The three lane-transfer tests must still pass unchanged** —
  `Leaked_creeps_continue_into_the_next_lane`,
  `Leaked_creeps_keep_current_health_when_entering_next_lane`, and
  `Eight_lane_leaked_creeps_flow_across_expanded_opponent_lanes`. They assert on
  the *transferred* entity, so removing the spent one should not affect them. If
  one breaks, the removal is catching the wrong entity.
- **Add the test that is missing**: `CombatState.Creeps.Count` returns to zero
  after a creep flows through every lane and dies (or is removed at the end of
  the carousel). Nothing currently asserts that the collection is bounded, which
  is why this went unnoticed.

### Related fragility, worth a comment either way

The transfer reads `leakedCreep` from `creepsBeforeCombat` — the snapshot taken
*before* `combat.Advance` — rather than from the post-combat state. Today that is
equivalent, because `MoveCreeps` runs first in `Advance` and the two later damage
phases both skip `HasLeaked` creeps, so a creep's health cannot change after it
leaks within the same tick. But it is an unstated dependency on phase ordering: if
damage ever resolved before movement, transferred creeps would silently rewind to
their start-of-tick health — carrying *less* damage than they took. Either read
the creep from `result.State`, or add a comment recording why the pre-combat
snapshot is safe.

## 12. CRITICAL — client invents creep max health; wrong for 10 of 15 creeps

`UnityVerticalSliceRenderer.CreepMaxHealth(string creepId)` (~line 3976) guesses
max health by substring-matching the creep id, because
`CreepPresentationSnapshot` carries only `Health` and no `MaxHealth`. The
guesses are stale at the 5-creep roster and wrong for every creep added since:

| Creep | Sim max health | Client assumes |
|---|---:|---:|
| `runner` / `brute` / `swarm` / `shade` / `siege` | 10 / 24 / 5 / 14 / 48 | correct |
| `creep.obsidian_brute` | 60 | **24** (matches `"brute"`) |
| `creep.colossus` | 90 | **10** |
| `creep.warden` | 55 | **10** (`"boss"` never matches) |
| `creep.burrower` | 44 | **10** |
| `creep.turret_walker` | 40 | **10** |
| `creep.serpent` | 32 | **10** |
| `creep.stalker` | 20 | **10** (`"stalker"` ≠ `"stealth"`) |
| `creep.zephyr` / `wisp` / `revenant` | 12 / 4 / 8 | **10** |

This feeds `CreepHealthFraction` → health bars and damage tinting every frame.
A full-health Colossus clamps to 1.0 and its bar does not visibly move for the
first 89% of its health; a full-health Wisp renders as though at 40% health.

**The design note raises this item's severity.** A creep at partial health is not
an edge case in this game — it is the normal state of any creep that has crossed a
lane boundary, since transfers deliberately carry damage forward. Health-bar
fractions are therefore on the main path, and a Colossus arriving in the next lane
at 45/90 renders as untouched. The mechanic the health bar exists to communicate —
"this creep has been worn down by the lane before yours" — is precisely the one it
currently cannot show for 10 of 15 creeps.

**Fix at the boundary, not in the client**: add `MaxHealth` to
`CreepPresentationSnapshot` and delete `CreepMaxHealth` entirely. Guessing sim
stats from an id string in the presentation layer is the bug; the wrong numbers
are a symptom. A test comparing every `CreepDefinition.MaxHealth` against
whatever the client uses would have caught this the day the roster grew.

## 13. CRITICAL — two placeholder generators truncate the 15-entry visual libraries to 5

`Assets/Resources/TowerVisualLibrary.asset` and `CreepVisualLibrary.asset` each
carry 15 profiles, written by a non-destructive find-or-append convention
(`Tower3DImportPipeline`, `Creep3DImportPipeline`,
`TowerVisualPrefabGenerator.UpdateSingleVisualProfile`).

Two **destructive** writers survive alongside it:

- `Editor/CreepVisualPrefabGenerator.cs` ~line 411 — `profiles.arraySize = 5;`
- `Editor/TowerVisualPrefabGenerator.cs` ~line 438 —
  `profiles.arraySize = TowerSpecs.Length`, where `TowerSpecs` has 5 entries

Both are one menu click away (`Line Wards/Art/Generate Placeholder … Prefabs`)
and either truncates the shipped library to 5 entries repointed at procedural
placeholders. `TowerVisualPrefabGenerator` contains *both* conventions about 25
lines apart. Delete the destructive paths, or gate them behind a confirmation
that names what will be lost.

## 14. CRITICAL — board tower colour and name still only know the 5 original roles

`TowerRolePalette` documents itself as "the single source of truth for tower
role colour," written because "the colour a player learned from a card was not
the colour the placed tower carried." Its `For(string)` substring-matches the
**5 arcane roles only**, so the bug it exists to prevent is currently live:

- `UnityVerticalSliceRenderer.TowerMarkerColor` (~line 4210) →
  `TowerRolePalette.For(...)`, used for board markers, halos and base tint. All
  10 Foundry/Grove towers fall through to Arrow blue, while `TowerCatalog`
  advertises 10 distinct accent colours on their palette cards.
- `TouchPlacementController.TowerRoleName` (~line 864) returns `"Arrow ward"`
  for all 10, so selecting a Gatling Turret toasts **"Arrow ward selected"**.
  `TowerSelectionRingScale` and `TowerAccent` have the same 5-role shape.

`TowerCatalog`'s own comment claims it "replaces a set of parallel switch
statements on the palette's role index" — these four switches survived that
conversion, in the file that consumes `TowerCatalog`.

## 15. Bots can only ever build 5 of the 15 towers

`LocalVerticalSlice.BotTowerForSlot` (~line 639) can return only Arrow,
Control, Pulse and Prism. Gatling, Tesla, Foundry, Barricade, Repair Drone,
Elder Canopy, Sapling, Bloomheart, Thorn Snare and Spore Cloud are unreachable
— so **every tower mechanic added in the 15-tower expansion, and every
mechanic-contribution measurement taken against a bot opponent, was measured
with a bot that never builds the tower in question.** Read alongside item 5's
note about mazing and item 10's about sends, the honest position is that the
bot-derived balance record needs re-taking, not adjusting.

Also: `Defensive` builds an unbounded run of 42-gold Prisms and `Greedy` an
unbounded run of Arrows, so `BotMazingTests`' "keeps building past nine towers"
assertion is satisfied by repeating a single tower forever.

## 16. `README.md` test count is wrong by 92, and three docs disagree

Actual count in `tests/LTW.Tests/` (19 files): **169** `[Fact]`/`[Theory]`
attributes — 167 `[Fact]` (2 of them `Skip`ped) plus 2 `[Theory]` with 7
`[InlineData]` rows, so ~174 discovered cases.

Claims in the repo: `README.md` line 43 says **77**;
`docs/MVP_STATUS.md` says **55**; `docs/GAMEPLAY_DEVELOPMENT_CHECKLIST.md`
says **78**. All three are stale and all three disagree.

A hardcoded test count in prose is guaranteed to rot. Either drop the number
and say "the suite passes", or have CI write it. The two `Skip`ped tests are
the "two mazing bots stalemate" cases in `LocalThreePlayerMatchTests` — and
`MVP_STATUS.md` still cites those same tests as *verifying* the tick window
they no longer run.

## 17. Lane geometry documented as 7x18; the actual map is 7x16

`SampleVerticalSliceContent` defines `width: 7, height: 16`, spawn `(3,0)`,
leak `(3,15)`, and `LaneLength = 16` is duplicated in three Unity scripts.
Docs saying 7x18 with life loss at `(3,17)`: `docs/MVP_STATUS.md`,
`docs/GAMEPLAY_DEVELOPMENT_CHECKLIST.md`, `docs/GD_TUNING_LOG.md` — and the
`BestMazingPlacement` doc comment added in `2cf96e1` also says "7x18".
Lane count is also inconsistent (`MVP_STATUS.md` three, the checklist eight;
the default is eight).

`MVP09_INTEGRATION_NOTES.md` in the repo root is worse and fully stale — it
describes a 12x9 grid, spawn `(0,4)`, exit `(11,4)`, 10 ticks/second (actual: 4)
and a 3,000–6,000 tick window (actual: 900–1800, in skipped tests). It is not
linked from `docs/README.md`. **Retire it** rather than repair it.

## 18. Send cooldown is 0, but the UI still describes a 7.5-second one

`LocalVerticalSlice` constructs with `sendCooldownTicks: 0` (per
`74b8519 Remove the send cooldown…`). Left behind in `SendDockController`: two
comments asserting a 7.5s / 30-tick cooldown, the `isSendCoolingDown` greying
branch, the `ignoresCooldown` parameter, and the RAPID category name —
documented in `docs/GAME_MENU_AND_RUNTIME_FLOW.md` as meaning "the
cooldown-exempt set", which is now every category. All inert. Either restore a
cooldown or delete the machinery; leaving it makes the dock's behaviour
unpredictable to the next person who reads it.

Same file, same shape: `CategoryHasCooldownGatedCards(int category) =>
category != 1;` sits directly beneath a comment stating it "replaces a
hardcoded `selectedCategory != 1` test" and is "keyed off the same fact the
cards themselves use". It is that hardcoded test.

## 19. Creep costs are hardcoded three times each in the client

`UnityCommandAdapter.TowerCost` deliberately reads tower cost from the sim, with
a comment explaining the drift that motivated it. Creeps never followed:
`SendDockController` hardcodes each creep's cost in its `Send*` method, again in
the `gold >= N` affordability gate, and a third time in the card meta string —
~45 literals. All 15 currently match `SampleVerticalSliceContent`, but a comment
in that same file records that Serpent already drifted once (22 vs 20) across
all three copies. Highest-probability future drift point in the client; route
creep cost and income through the adapter the way tower cost already is.

## 20. Bramble zone is computed from one index and is wrong on a mazed route

`CombatService.BrambleZoneFor` (~line 216) assigns a local `last` that is never
read, and derives the zone as the contiguous *index* range `(first, first + 2)`
from the first covered route index. On a route that weaves in and out of the
tower's radius, covered indices are non-contiguous, so Thorn Snare brakes up to
3 indices it may not cover while leaving covered ones unbraked. Rare on a
straight lane; **normal on every route the new bot mazing produces** — so this
regressed in practice the moment `2cf96e1` landed, without being touched.

## 21. `BestMazingPlacement` is a full-grid BFS scan per bot per tick

For each bot, each tick, it calls `ValidateTowerPlacement` on all
`Width × Height` = 112 cells; each call allocates a command, runs two LINQ
scans over `content.Towers`, builds a fresh `LaneGrid` with two new `HashSet`s,
and runs a BFS allocating a `HashSet`, `Dictionary` and `Queue`. `PlaceTower`
then re-validates, so ~113 BFS per placement. At 8 lanes that is ~800 BFS and
thousands of collection allocations per tick, at 4 ticks/second, on mobile.

The method's doc comment calls this "bounded and small" and cites a 7x18 grid
(see item 17). It is bounded, but it is a full-grid scan, not small — and only
the affordability early-out keeps it off the hot path. Worth profiling on device
before it is treated as settled; caching per-cell length-gain and invalidating
on placement would remove most of it.

## 22. Replay records cannot reproduce a match

`acceptedCommands` is appended to only in `QueueSend`. `PlaceTower` and
`SellTower` are never recorded, and `AcceptedCommandRecord` has no fields
(command type, lane, position) that could carry them. Since towers determine
the route and every kill, `GetReplayRecord()` cannot reproduce a
`LocalVerticalSlice` match, and no code path replays one —
`ScenarioRunner.Replay` runs a separate economy-only model. Tests assert
command *counts* only, so nothing catches it.

Related: `LocalMatchOptions.Seed` is recorded into `ReplayRecord` and consumed
nowhere, and the whole `Random/` namespace (`IRandomSource`,
`SeededRandomSource`) is referenced only from one contract test. The sim is
all-integer and deterministic today, so the seed is decorative — fine, but the
replay format implies a guarantee it does not provide. Either finish replay or
mark `GetReplayRecord` as send-only telemetry.

## 23. Determinism: one real hazard, otherwise clean

`LocalVerticalSlice` ~line 333 does `foreach (var bot in bots)` over a
`Dictionary<PlayerId, BotController>`. Iteration order sets the order of
`QueueSend` calls, which sets `NextEntityId()` assignment, which is the final
tie-breaker in `SelectTarget`, Pulse splash `Take(2)` and `ChainArc`. The
codebase contradicts itself here — `SeedExpandedLaneBotOpeners`, ~140 lines
later, correctly does `bots.OrderBy(bot => bot.Key.Value)`. It works today
(insertion-ordered, no removals) but it is reliance on documented-unspecified
behaviour in a sim that records and replays. Sort it.

Otherwise the determinism story is genuinely good and worth protecting: no
`float`/`double` arithmetic anywhere in `src/LTW.Simulation`, no unseeded
`Random`, no `DateTime`/`TickCount`, and every other `Dictionary`/`HashSet` is
keyed lookup only. `LaneGrid.OccupiedCells` exposes `HashSet` enumeration order
and has no callers — a latent trap if anyone starts using it.

## 24. Smaller items, grouped

Simulation:

- `ScenarioRunner.TryApplySend` charges the runner's single configured creep
  rather than `send.CreepId`, so cost, income and cooldown are computed against
  a creep that was not sent, while the record stores the real one. Live and
  replay are wrong identically, so `ScenarioReplayTests` passes.
- Two sources of truth for lives-lost-per-leak: `EconomyRules.LeakLifeLoss`
  (used by `ScenarioRunner` and tests) vs `CombatService.LeakLifeLossFor`
  (substring `"siege"`, used by the real match). The configured number and the
  production number are unrelated. Consequence to check: `creep.siege` (48 hp)
  costs 2 lives while `creep.colossus` (90 hp, *named* "Siege Colossus") costs 1.
- Selling a tower shortens the route without remapping live creep `PathIndex`,
  so any creep past the new end leaks immediately on the next tick.
- `LeadPathIndex` rebuilds all bramble zones on every call — once per candidate
  creep per Foundry tower per tick — defeating the once-per-tick build that a
  doc comment says exists for exactly this reason.
- Rounding convention is asserted but not uniform: Shade mitigation rounds *up*
  (`(damage + 1) / 2`) while splash and rot round down, next to a comment
  claiming a single round-down convention.
- `LanePathCache` is dead in production (test-only), keys on `laneId` while
  ignoring its `grid` argument, and nothing calls `Invalidate` — so it would
  return stale routes if adopted. It duplicates the `routes` dictionary
  `LocalVerticalSlice` already maintains. Delete or fix before someone uses it.
- `VerticalSliceSnapshot` hands out the live `TowerCombatState[]` behind an
  `IReadOnlyList`, unlike `State/Snapshots.cs` and `ReplayRecord`, which copy.
  `ContentValidation` likewise stores the caller's list without copying.
- `ApplyLeak` does not guard participants the way `ApplyKillBounty` does: an
  eliminated sender is still credited bounty, and a defender at 0 lives yields
  `livesLost == 0` but still pays out.
- `BotController.SelectCreep`'s `.First(...)` fallback can throw —
  `BotLaneOptions.PrimaryCreepId` is never validated against `content.Creeps`.
- `LocalVerticalSlice` route lookup uses `FindRoute(...).Route` without checking
  `.Found`, and `ContentValidator.ValidateMap` never checks spawn→exit
  reachability, so a map that seals a lane validates clean and then indexes
  `route[-1]`. Unreachable with the sample map.

Unity client:

- Three orphaned UI scripts with zero references: `RuntimeMatchHud.cs` (a
  complete second IMGUI HUD duplicating `HudView`), `MatchHudPresenter.cs`
  (would double-count kills/leaks if wired, since `HudView` self-drives from
  its own `Update`), and `ViewSwapController.cs`. Delete or wire deliberately.
- `TowerEmissionKeywordGuard` (item 1 of this document) lists **5** materials;
  `Assets/Art/Towers/Production/Materials/` now has **15**
  `mat_tower_*_3d_body_runtime_v01.mat`. The 10 newer towers are still exposed
  to the `_EMISSION` stripping the guard exists to absorb. **Update item 1's
  "five tower body materials" wording along with the guard's list.**
- Per-frame allocations in `UnityVerticalSliceRenderer.Update`: `new int[]`,
  four `new List<string>()` release sweeps, two `new HashSet<string>()`, plus
  ~3 string allocations per entity per frame (`EntityId.Value.ToString()`,
  `"t" + key`, `"c" + key`, and a `$"{lane}:{x}:{y}"` tower key). The class
  already holds ~40 reusable `readonly` collections, so the convention exists
  and these were missed.
- `DiagnosticsOverlay` builds its text with LINQ and a `StringBuilder` every
  frame per player and per lane, gated only at `OnGUI` — so a hidden overlay
  still costs full price at 60 Hz.
- `RuntimeUiIconLibrary` and `RuntimeUiArtLibrary` are the same class with
  different roots and two divergences: the art library does not cache nulls (so
  a missing texture re-hits `Resources.Load` every `OnGUI` pass) and has an
  `#if UNITY_EDITOR` `AssetDatabase` fallback the icon library lacks — meaning a
  texture missing from a player build resolves fine in the Editor and in every
  headless capture, hiding exactly the case that matters.
- Four editor capture runners reflect into a private `simulation` field by
  string name and return `false` on failure, so a rename presents as a
  60–180 second timeout rather than an error.
- `TouchPlacementController` does `Camera.main!` then dereferences it —
  `NullReferenceException` on every tap if no camera is tagged `MainCamera`.
  The renderer handles the same case correctly.
- Duplicated sim constants in the client: `IncomeIntervalTicks = 50`,
  `SimulationTicksPerSecond = 4f` (already exposed as
  `UnitySimulationDriver.TicksPerSecond`), and `LaneCount = 8` hardcoded in two
  scripts against a runtime-configurable 2–8 `LocalMatchOptions.LaneCount` that
  `LocalPlaytestBatchRunner` already overrides.
- Dead 5-tower icon path inside `TouchPlacementController`
  (`DrawPaletteButton`, `TowerIconResourceName`, `CompactTowerLabel`, ~55
  lines); the live path is `DrawCatalogCard`. Development hotkeys and the
  in-placement switch strip also still cover only the 5 original towers.
- Two orphaned Python scripts in `tools/art_pipeline/`:
  `blender_build_control_tower_source.py` (2,211 lines, superseded by the Meshy
  intake path) and `measure_creep_gait.py`. Zero references to either.

Docs:

- `docs/README.md` omits 9 of 32 docs, including `GD_TUNING_LOG.md` — the
  primary balance record, and the largest doc in the repo.
- "five towers / five creeps" is still stated as the current baseline in
  `README.md`, `docs/AI_ART_PIPELINE.md`,
  `docs/PROPER_ART_REPLACEMENT_PASS_CHECKLIST.md`,
  `docs/STYLIZED_WEAPON_KIT_INTEGRATION_CHECKLIST.md` and
  `docs/GRAPHICS_THEME_WORK_BREAKDOWN.md`.
  `docs/CONTENT_ROSTER_EXPANSION_PLAN.md` handles it correctly with an explicit
  superseded banner — copy that pattern.
- `docs/TOWER_AND_CREEP_ROSTER.md` calls Obsidian Brute (60) "highest health in
  the roster" in one table and Colossus (90) the same in another. Otherwise all
  30 rows were verified against `SampleVerticalSliceContent` and match exactly.
- `docs/GAMEPLAY_REVIEW_FINDINGS.md` still points at `PaintSendMenuOverlay`,
  which no longer exists in any source file, and `RealUiCaptureRunner`'s class
  comment still justifies itself against the painted HUD mock that was deleted
  in `ece2ae8`.

## What is solid, and worth not breaking

Stated explicitly because a list of defects gives a skewed picture:

- **The architecture boundary is real and enforced.** `LTW.Simulation` targets
  `netstandard2.1` and has no Unity dependency; `ArchitectureBoundaryTests`
  guards it. All-integer, no wall-clock, no unseeded randomness (item 23).
- **Build discipline** is stronger than most projects this age:
  `TreatWarningsAsErrors`, `Deterministic`, lockfile restore, pinned SDK and
  a pinned exact Unity version with a documented reason.
- **Content-ID sync between Unity and the sim is clean** — all 15 `tower.*`
  literals match and are guarded by `TowerRosterTests`; there are zero
  `"creep.*"` literals in `Assets/Scripts/`. Both `.asset` libraries and both
  icon sets carry all 15 entries. This is the drift that would hurt most, and
  it is the one that was handled.
- **`Resources.Load` failure handling is uniformly correct** — every call site
  null-checks and falls back.
- **169 tests** covering economy, pathing, combat, mechanics and replay, with
  real balance-measurement harnesses rather than only unit tests.
- **The commit history is unusually good** — descriptive, evidence-carrying
  messages that state what was measured and what was wrong. Items 10–15 were
  findable *because* the history says plainly what each change did and did not
  verify. Keep writing them this way.
