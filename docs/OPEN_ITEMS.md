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

The five criticals (items 10–14) share a shape worth naming: **each is a place
where one concept is computed in two places, and the copies disagree.** That is
the characteristic failure of two agents working in parallel, and it is the
thing to grep for when looking for the next one.

## 10. CRITICAL — bot pressure check omits `HasLeaked`, so bots stop sending

`LocalVerticalSlice.cs`, bot pressure check (`ShouldBotSend`, ~line 533):

```csharp
.Where(creep => creep.LaneId.Equals(myLane) && !creep.IsDead)
```

Every other creep filter in the codebase uses `!IsDead && !HasLeaked` —
verified at eight sites in `CombatService.cs` (lines 66, 98, 121, 290, 372,
562, 603 and the guard at 129). This one drops `HasLeaked`.

Because leaked creeps are never removed from `CombatState` (item 11) and keep
their full `Health` — they leaked, they were not killed — `incomingHealth` for
a bot's own lane **grows monotonically for the entire match**. Once it crosses
`PressureThreshold` (`BotController.cs`, ~line 65: roughly `70 + 15 × towers`
for Balanced), the bot never sends again for the rest of the match.

This is exactly the failure mode the `PressureThreshold` doc comment claims to
have fixed, and it is a plausible cause of the "bot sitting on thousands of
gold" symptom that `2cf96e1` attributed solely to the placement ceiling. Fixing
the placement ceiling did not fix this. **Any balance measurement involving bot
sends is suspect until this is fixed and re-measured.**

## 11. CRITICAL — leaked creeps are never removed from `CombatState`

`CombatState.RemoveCreep` has exactly one caller: `CombatService.cs` ~line 672,
in the death path (`DamageCreep`). `MoveCreeps` marks a leaked creep with
`MarkLeaked()` and calls `ReplaceCreep(moved)` — it never removes it. So every
creep that ever leaks stays in `state.Creeps` for the rest of the match.

With `StartingLives = 220` across up to 8 seats, and each leak also spawning a
transferred entity that can leak again down the carousel, this reaches the order
of 10⁴ permanently-resident entities. Every one of them is then paid for on
**every tick** by: the `MoveCreeps` filter, each tower's candidate filter in
`AttackWithTowers` / `ResolveLandedShells` / `GetTowerAimSnapshots`,
`CrowdBloomBonus`, the `creepsBeforeCombat` dictionary rebuild in
`LocalVerticalSlice`, and every `ReplaceCreep` copy.

That last one makes it quadratic: `CombatState.Replace` does `values.ToArray()`
and the `CombatState` constructor `ToArray()`s both lists again, so movement
alone is O(creeps × (creeps + towers)) array copies per tick — where `creeps`
means *all creeps ever*, not live ones. This is the single highest-value fix in
the codebase: it is both a correctness bug (it feeds item 10) and the dominant
performance cost, on a mobile target.

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
