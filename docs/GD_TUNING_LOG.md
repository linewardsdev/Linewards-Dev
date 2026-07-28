# GD-04 Tuning Log

## Purpose

This log records the first gameplay pacing targets for the local vertical slice. It is intentionally lightweight: the goal is to make early/mid/closing pressure measurable before mobile-device validation resumes.

## Current Baseline

- Three lanes: one human lane and two bot lanes.
- Starting economy: 100 gold, 10 income, 220 lives.
- Income interval: 50 simulation ticks.
- Global send cooldown: 30 simulation ticks.
- Sell refund: 50% of tower cost.
- Leak life loss: 1 life per leaked creep; Siege currently leaks for 2.
- Prototype towers: Arrow, Control, Relay, Pulse, Prism.
- Prototype sends: Runner, Brute, Swarm, Shade, Siege.

## First Target Ranges

| Moment | Target Range | Reason |
| --- | --- | --- |
| First send | 0-30 ticks | Players should understand offense immediately. |
| First income tick | 50 ticks | The income clock should be felt early and often. |
| First meaningful defense correction | 30-120 ticks | The player should need to react before the match drifts. |
| First leak | 90-240 ticks | Leaks should arrive after some decisions, not instantly. |
| First elimination | 450-900 ticks | A local match needs escalation without immediate collapse. |
| Match completion | 900-1800 ticks | Long enough for economy tension, short enough for repeated tests. |

## Starter Content Intent

| Content | Role | Current Intent | Tuning Risk |
| --- | --- | --- | --- |
| Arrow Ward | Reliable single-target | Cheap rapid baseline damage, reduced from the prior high-DPS opener. | Could feel underpowered if specialized roles are too expensive or too situational. |
| Control Ward | Area/control placeholder | Lower damage, slower cadence, counters Shade resistance. | Needs real slow/control behavior before final balance. |
| Relay Ward | Utility/economy support | Low damage, improved cadence/range, and +1 gold whenever it hits. | Gold-on-hit could become too efficient during dense waves or too weak without steady pressure. |
| Pulse Ward | Dense-pressure answer | Short-range splash damages nearby creeps. | Can erase Swarm too efficiently if splash count/damage is too high. |
| Prism Ward | Long-range specialist | Prioritizes Shade/high-health pressure and bypasses Shade resistance. | High range plus high damage may become mandatory against Brute/Siege. |
| Runner | Basic speed pressure | Cheap opener with modest income. | Could feel bland without speed/readability tuning. |
| Brute | Health pressure | More health and income, higher cost. | Could be too efficient if tower damage is low. |
| Swarm | Volume pressure | Cheap fast group send. | Can clutter the board if quantity and speed are too high. |
| Shade | Low-visibility pressure | Resists non-Control/non-Prism damage. | Needs clear reveal/readability language before becoming a frustration unit. |
| Siege | Late high-threat pressure | High health and 2-life leak pressure. | Needs warning/windup language so extra leak loss feels fair. |

## Known Balance Questions

- Does the global 30-tick send cooldown create enough breathing room once bots and humans send together?
- Does 220 lives give enough room for defense corrections without making local matches drag?
- Is Relay's +1 gold-on-hit support effect enough to justify its cost without becoming mandatory economy scaling?
- Should Swarm quantity stay at 3, or should the unit be cheaper with a lower income reward?
- Are kill bounties large enough to make defense feel rewarding without defeating send-for-income pressure?
- Does Pulse splash need a stricter target cap or lower splash damage?
- Does Prism priority targeting overvalue Prism against mixed waves?
- Does Shade resistance need a visible reveal/detection state before player-facing tuning?
- Does Siege extra leak loss require a windup, warning, or special lane alert?

## Playtest Capture Template

| Run | Seed/Profile | Duration Ticks | Winner | First Send | First Leak | First Elimination | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Seed 1 / P2 Balanced, P3 Defensive | 476 | P1 | 7 | 16 | 473 | Unity Play Mode run saved as `playtest-476.md` / `match-476.json`. P1 ended untouched at 220 lives while both bots were eliminated, so local match completion is below the 900-1800 tick target and first leak is earlier than the 90-240 tick target. |
| 2 | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| 3 | TBD | TBD | TBD | TBD | TBD | TBD | TBD |

Use the local `P` hotkey after a completed Unity Play Mode match to write the Markdown playtest report. The runtime toast should show `Saved playtest-###.md`; reports are written under Unity's persistent data path, currently `C:\Users\engch\AppData\LocalLow\DefaultCompany\LTW_UnityClient\Playtests` on the Windows editor setup.

## Next Tuning Actions

1. Run one local desktop Play Mode match with the updated bot opening defense and 220-life baseline.
2. Press `P` after match completion and copy first send, first leak, first elimination, winner, and completion timing from the generated report.
3. Decide whether the next tuning lever should be send cooldown, creep stats, or tower damage after observing the 900-1800 tick match gate in Play Mode.
4. Promote any repeated confusion into GD-01/GD-02 usability fixes before changing numbers heavily.

Latest objective read: the first recorded Play Mode run ended at tick 476, well before the target match-completion range. The 5x2 prototype now adds Pulse splash, Prism priority targeting, Shade resistance, Siege extra leak pressure, and expanded bot roster usage. Before changing presentation again, prioritize a mixed-pressure playtest that checks whether these mechanics improve decision variety without shortening matches further.

## 2026-07-27: Tower Cost/Damage Cut To Encourage Earlier, Wider Building

Goal: make building several towers early feel like the natural response, not a luxury — cheaper towers so a normal opening gold reserve covers more than one or two, and lower damage per tower so a single placement is never enough on its own.

Applied a uniform ~30% cost cut and ~25-33% damage cut (Relay's damage left untouched — it's already a low-damage utility/economy support role, not a combat role, and cutting it further risked making it read as non-functional):

| Tower | Cost (old → new) | Damage (old → new) |
| --- | --- | --- |
| Arrow | 20 → 14 | 3 → 2 |
| Control | 35 → 24 | 3 → 2 |
| Relay | 40 → 28 | 2 → 2 (unchanged) |
| Pulse | 45 → 32 | 8 → 6 |
| Prism | 60 → 42 | 12 → 9 |

**Known consequence, not yet resolved:** bot opponents build a *fixed* opening tower count per profile (Balanced 3, Defensive 4 — see `DesiredOpeningTowerCount` in `LocalVerticalSlice.cs`), not a gold-scaled count. A human player facing cheaper towers can build more of them to make up for lower per-tower damage — that's the intended incentive — but bots don't, so bot-vs-bot matches now resolve faster with weaker total early defense than before this change. `LocalThreePlayerMatchTests.Two_bots_complete_a_local_carousel_match`'s completion-tick lower bound was dropped from 430 to 250 to reflect this rather than masking it. Also fixed a related bug this surfaced: Balanced/Defensive bots previously used a hardcoded gold-reserve number to avoid sending before their opening defense was built; cheaper towers left just enough spare gold to slip a cheap Swarm send in early, so the gate now checks completion of the actual opening tower package directly instead of an absolute gold amount (see `HasCompletedOpeningDefense`).

This is expected to be revisited once the planned per-lane, cost-aware bot system lands — bots that spend down to a gold-reserve floor (like a human would) rather than a fixed tower count should restore intended pacing without needing to touch these numbers again.

## 2026-07-27: Per-Lane Bot Toggle + Reactive Bot Spending

Follow-up to the tower rebalance above, landed in three steps on `swarm-multibot-cluster`.

**Step 1-2:** any lane 2-8 can now be independently bot-enabled/disabled (`BotLaneOptions`, `LocalMatchOptions.WithLane`), replacing the old "every player except 1" blanket rule. `LocalPlaytestBatchRunner`'s CLI args generalized from `-ltwP2`/`-ltwP3`-only to `-ltwP{n}`/`-ltwP{n}Creep`/`-ltwP{n}Enabled` for all 8 lanes.

**Step 3:** replaced the tick-scheduled heuristics with reactive ones, closing the "fixed tower count" gap noted above:

- Bot tuning (aggression, defense bias, minimum gold reserve) moved from hardcoded constants into content data — `ContentCatalog.BotProfiles`, previously always empty, now has one `BotProfileDefinition` per `BotDecisionProfile` (`SampleVerticalSliceContent.cs`). `BotController.ResolveProfile` reads it, throwing rather than silently falling back if a profile is missing an entry.
- `GoldReserve(tick)` → `GoldReserveFloor()`: a flat, content-driven floor instead of a tick-conditional hardcoded number. Recalibrated during this work from an initial 40/60 guess down to 20/20 (Balanced/Defensive) — the higher numbers looked reasonable on paper but actually *stalled* building against the cheaper post-rebalance tower costs, since sending is now separately gated by tower coverage, so the reserve no longer needs to double as a large safety buffer for the whole build-out phase.
- `DesiredOpeningTowerCount` (a hard cap) → `MinimumTowerCoverage` (a floor only) — `TryPlaceBotTower` now keeps building as long as gold (above the reserve floor) and an unused candidate position exist, instead of stopping at a fixed number. Verified empirically: a Defensive bot given enough ticks now builds past the old fixed cap of 4.
- New reactive lane-pressure gate: a non-Greedy bot holds sends while its own lane's incoming creep health exceeds a threshold derived from `DefenseBias`, scaled up by the bot's own tower count. That scaling term isn't cosmetic — an early flat threshold caused a real bug caught by test, where a 4-tower Balanced bot facing a sustained-aggressive neighbor got stuck permanently unable to send for the rest of a match, since more towers didn't clear an already-accumulated creep backlog. Greedy is exempt (sending is its defining lever, not something pressure should suppress).
- `SelectCreep`'s tick-tiered creep preference gates (`tick.Value >= 260` etc.) replaced with `player.Income.Amount >= N` gates — deterministic and state-driven (income only changes via accepted sends, never wall-clock), so a bot's creep variety now tracks how much it's actually accomplished rather than how long the match has run.

**Test fallout, all expected and fixed, not masked:** reactive building can finish a profile's opening tower package within a couple of ticks when starting gold covers it, so several tests that assumed a multi-tick or multi-hundred-tick delay before bots acted needed rewriting against the actual invariant ("never send before minimum coverage is met") instead of a time window. `LocalThreePlayerMatchTests.Two_bots_complete_a_local_carousel_match`'s completion-tick floor dropped again (250 → 150): reactive bots are now both better-defended *and* start attacking sooner (as soon as their own coverage is met, not after a large tick-gated reserve clears), so two bots fighting resolves faster than either the original or the interim fixed-count bots did. This is an accepted property of reactive play, not a regression.

83/83 tests pass, including three new ones added for this work: overbuild-past-the-old-cap, hold-sends-under-lane-pressure, and same-seed determinism.

## 2026-07-28: Category 2 Creeps — Crystal Wisp, Ash Revenant, Obsidian Brute, Serpent Coil, Spire Turret Walker

Five new Meshy-generated FBX models landed in Downloads, exactly the "5 new creeps" the send-menu category split (2026-07-28, same day) was built to make room for. Ran through the same intake pipeline as the original 5 (`tools/art_pipeline/ai_asset_intake.py`, `--target-height 0.75 --max-footprint 0.9`, same as every existing creep) — all 5 passed every gate check except `has_normal_map`, the same already-accepted gap documented in `docs/OPEN_ITEMS.md` for the original set.

First-pass stats, proposed rather than specified by the user, each answering a different defensive question from the existing 5 and from each other (kept in a similar cost/health range — Category 2 is a parallel set of answers, not a stronger tier):

| Creep | Id | Cost | Income | Kill/Leak bounty | Health | Speed | Defensive question |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| Crystal Wisp | `creep.wisp` | 5 | +1 | 1/1 | 4 | 3 | Can the defender handle constant cheap chip pressure, not just big single threats? |
| Ash Revenant | `creep.revenant` | 16 | +4 | 1/2 | 8 | 2 | Does the defender always finish off a fragile-but-high-value send, or let it feed the sender's economy? |
| Obsidian Brute | `creep.obsidian_brute` | 30 | +3 | 3/4 | 60 | 1 | Does defender DPS scale into the mid/late game, not just the opener? |
| Serpent Coil | `creep.serpent` | 22 | +2 | 2/3 | 32 | 1 | Can the defender handle sustained, unglamorous pressure without overinvesting? |
| Spire Turret Walker | `creep.turret_walker` | 38 | +4 | 4/5 | 40 | 2 | Can the defender's towers keep up with a heavy threat that isn't slow, unlike Siege? |

**"Obsidian Brute" naming/identity overlap, flagged and resolved deliberately:** the asset visually reads very close to the existing `creep.brute` (same rock-golem tank silhouette, different palette). Decided to keep the asset and differentiate by stats rather than swap it — it now holds the highest health in the entire roster (60, above even Siege's 48), reading as a heavier, later-game tank tier rather than a duplicate.

**Not in scope for this pass:** bot AI awareness of the 5 new creeps. `BotController.SelectCreep`'s preferred-id lists still reference only the original 5 — bots won't send these on their own. Extending bot preferences to use Category 2 is a clean, separate follow-up whenever it's wanted; it wasn't required to make the creeps sendable by a human, and the reactive bot-spending work (previous entry) already gives bots plenty to do with the existing roster.

**Orientation/scale values are first guesses, not verified in-engine.** Each creep needed a `Creep3DProofSetGenerator.Specs` entry with `runtimeScale`/`importEulerAngles`/`accentRadius`, same as every existing creep. Facing direction was checked via a Blender-space render (camera looking along the intended travel axis) rather than a real Unity capture, because the FBX Blender(Z-up)→Unity(Y-up) axis conversion means a rotation that looks correct in Blender doesn't necessarily transfer 1:1 to Unity's import-angle space — the same caveat the original Runner/Brute/Siege entries already carry in their code comments ("first guess... verify with a capture before trusting the sign"). Concretely: Wisp/Revenant/Serpent needed no correction (already faced the travel direction in the Blender-space check); Obsidian Brute got yaw 180 by analogy to the original Brute (which needed the same fix despite looking front-facing uncorrected); Turret Walker's cannon measured 135° off-axis and was corrected to match. **Next step before considering this fully done:** an actual in-game capture (Play Mode or a batch screenshot) to confirm these hold, and adjust if not.

**Verification performed:**
- `dotnet test` — 84/84 pass (10-creep content contract updated, new creep ids added).
- Unity batchmode: `Creep3DProofSetGenerator.GenerateCreep3DWrappers` → `ValidateCreep3DWrappers` (0 issues across all 10 wrappers) → `PromoteCreep3DSet` (registered into `CreepVisualLibrary.asset`).
- A throwaway diagnostic (mirroring `RiggedCreepVerify`'s style, removed after use) confirmed all 5 new creep ids resolve through `CreepVisualLibrary.FindProfile` to a prefab with a real mesh and material — the same runtime path the new send buttons trigger.
- Full local batch playtest ran clean (0 exceptions) with the updated `SendDockController`/`UnityCommandAdapter`/content wiring in place.
- **Not yet done:** an actual human playtest tapping each of the 5 new send buttons and eyeballing the result in Play Mode. The verification above proves the pipeline is wired correctly end-to-end; it doesn't replace looking at it.

## 2026-07-28: 10-Creep Rebalance — Serpent Coil Cost Fix + Bots Extended To Full Roster

Follow-up to the Category 2 landing above, after merge to `main`. Two changes, both driven by real data rather than stat math alone.

**Serpent Coil cost cut 22 → 20.** At 22 gold, Serpent was strictly dominated by Obsidian Brute: 1.45 HP/gold and 0.091 income/gold vs. Obsidian Brute's 2.00 HP/gold and 0.100 income/gold, for only 8 more gold and no compensating advantage (same speed, same general profile). A rational spender never picks the dominated option, which meant Serpent could only ever be a trap pick for a human, not a real "sustained mid-health pressure" answer. At 20 gold it's no longer strictly worse on every axis, though it hasn't yet been proven meaningfully differentiated in practice — see the bot-usage finding below.

**Bots extended to the full 10-creep roster** (previously they only used the original 5 — flagged as explicitly out of scope in the Category 2 entry above, now landed per direct request to validate the new creeps aren't just human-only content). `BotController.SelectCreep`'s per-profile preference lists were extended to include Category 2 ids, matching each profile's existing character: Greedy leans on Turret Walker/Revenant for income efficiency and Wisp as a cheap opener; Balanced/Defensive lean on Obsidian Brute/Serpent as tankier alternatives to Brute.

**Bug found and fixed via real playtest data, not caught by review:** the first version of these extended lists was ordered by "thematic preference" rather than cost. The affordability loop returns the *first* id in the list whose cost fits available gold — so if a cheaper id appears before a pricier one in the same tier, the pricier one is mathematically unreachable (being able to afford the cheaper one never implies being unable to afford the pricier one, so the cheaper entry always wins first). This silently made `creep.brute`, `.shade`, `.siege`, `.serpent`, and `.obsidian_brute` completely unreachable — confirmed by parsing a real batch-playtest replay (`match-536.json`): `creep.runner: 250, creep.revenant: 207, creep.turret_walker: 73, creep.wisp: 18, creep.swarm: 3`, with the five listed above all at 0 of 486 total sends. Fixed by re-sorting every tier's list to strictly descending cost. Re-running confirmed the fix: `creep.runner: 267, creep.shade: 99, creep.siege: 82, creep.brute: 69, creep.revenant: 30, creep.wisp: 26, creep.swarm: 5, creep.turret_walker: 3` — 8 of 10 creeps now demonstrably reachable in bot play.

**Known limitation, not fixed this pass:** `creep.obsidian_brute` and `creep.serpent` still showed 0 uses across multiple playtests (different seeds; seed turned out not to matter for all-bot matches at all — see below) even though both are content-eligible for Balanced/Defensive well before their higher income gates (Serpent needs only `available >= 20`, reachable from the 100-gold starting state). Root cause, traced through `LocalVerticalSlice.TryPlaceBotTower` and `EconomyService`: income only increases when a bot successfully *sends* a creep (`sender.Income += creep.IncomeGain`), and tower-building is uncapped and runs every tick *before* the send decision — so a Balanced/Defensive bot's gold above its reserve floor tends to get absorbed into "one more tower" before it accumulates into the 20-30g band Serpent/Obsidian Brute need, even though cheaper sends (Brute, Runner) still slip through often enough to keep income climbing. This is the same no-cap tower-building dynamic flagged as a known consequence in the 2026-07-27 tower-cost-cut entry above, not a new bug and not something a creep-stat change can fix on its own — it would need either a send-side gold carve-out or a tower-building cap/pacing change, both out of scope for a creep rebalance. Documented here rather than silently left unmentioned, consistent with how the reachability bug itself was surfaced.

**Side finding:** all-bot matches are fully deterministic given the same profile/lane assignment — `grep`ing `BotController.cs` and `LocalVerticalSlice.cs` for `Random` turns up nothing, and two runs differing only in `-ltwSeed` (1 vs. 7) produced identical `CompletedAtTick` and replay content. The seed isn't consulted anywhere in bot decision-making or match flow for an all-bot lineup; it likely only matters where a human player's timing varies. Varying seed alone is not a way to get more data points from bot-only playtests — varying profile/lane assignment is.

**Match length increased as an accepted side effect:** fixing the reachability bug means bots now actually reach pricier, tankier creeps instead of always falling through to the cheapest option, so bot-vs-bot matches run longer (observed 1012 ticks for one seed/config, up from under 900). `LocalThreePlayerMatchTests.Two_bots_complete_a_local_carousel_match`'s upper bound widened 900 → 1200 to match — this lands back inside the original 900-1800 target range from this log's very first entry, rather than the artificially short range the reachability bug had been producing. Not a regression.

**Verification performed:**
- `dotnet test` — full suite green after updating `Bot_profiles_choose_expanded_roster_sends_when_available`'s expected creep ids and widening the match-length test's upper bound.
- Three Unity batchmode playtests (`LocalPlaytestBatchRunner.Run`) with real replay analysis, evidence above and under `docs/playtest-evidence/local-unity-batch-rebalance-10-roster-*`.
- `docs/TOWER_AND_CREEP_ROSTER.md`'s Serpent Coil cost entry updated to 20 to match.

## 2026-07-28 (follow-up): Send Decision Reordered Before Tower Building — Obsidian Brute/Serpent Fix Confirmed

Fixes the known limitation from the entry above. `LocalVerticalSlice.AdvanceOneTick` previously called `TryPlaceBotTower` before the bot's send decision every tick; since tower-building is uncapped, it absorbed a bot's surplus gold into "one more tower" before the 20-30g Serpent/Obsidian Brute band was ever reached. Reordered so the send decision (gated the same as before, by `HasMinimumDefenseCoverage`/`IsLaneUnderPressure`) runs first and `TryPlaceBotTower` only spends what's left — a bot's send now gets first claim on its own surplus gold instead of last.

**Confirmed fixed via replay analysis**, isolated 3-lane run (P1 silent, P2 Balanced, P3 Defensive, P4-8 disabled, seed 1 — full brawl configs with all 8 default-Greedy lanes enabled aren't a fair test for this, since Balanced/Defensive get eliminated too fast by 5 Greedy bots to ever reach their higher income gates): `creep.swarm: 54, creep.runner: 41, creep.brute: 8, creep.wisp: 3, creep.obsidian_brute: 3, creep.serpent: 2, creep.shade: 2`. Both previously-stuck-at-0 creeps now appear. P2 (Balanced) survived to income 130, well past its 35-income Obsidian Brute gate.

**Test fallout:** `Defensive_bot_builds_past_the_old_fixed_tower_cap_when_gold_allows` broke, for a legitimate reason — an isolated, unpressured Defensive bot no longer keeps stacking towers indefinitely, since it now prefers spending surplus gold on a send once its minimum coverage (4) is met, exactly the behavior this fix intends. Replaced with `Defensive_bot_prioritizes_sending_over_stacking_further_towers_once_coverage_is_met`, which checks coverage is still met (>=4) *and* that the bot actually sent something, to keep catching a real regression to the old hardcoded tower cap without asserting a tower count that's no longer guaranteed under normal (unpressured) conditions.

**Verification performed:**
- `dotnet test` — 86/86 green.
- Two Unity batchmode playtests: an 8-lane brawl (uninformative for this question, all Balanced/Defensive lanes died too fast) and the isolated 3-lane run above, which is the one that confirms the fix.
