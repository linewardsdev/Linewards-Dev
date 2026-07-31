# GD-04 Tuning Log

## Purpose

This log records the first gameplay pacing targets for the local vertical slice. It is intentionally lightweight: the goal is to make early/mid/closing pressure measurable before mobile-device validation resumes.

## Current Baseline

- Three lanes: one human lane and two bot lanes.
- Starting economy: 100 gold, 10 income, 220 lives.
- Income interval: 50 simulation ticks.
- Global send cooldown: 30 simulation ticks (documented from the start, but only actually enforced as of 2026-07-28 — see the final entry in this log).
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

## 2026-07-28 (follow-up): The 30-Tick Send Cooldown Is Now Actually Enforced

Landed on `multiplayer-seats-and-authority` as part of the command-authority pass for
networked play. Full context in `docs/MULTIPLAYER_SEATS_AND_AUTHORITY.md`.

**The cooldown described in this log's Current Baseline was never running.**
`EconomyRules.SendCooldownTicks` (30), `PlayerEconomyState.NextSendAvailableTick`,
`WithNextSendAvailableTick`, and `CommandRejectionReason.CooldownActive` all existed, but
nothing anywhere read or set them — verified by grep across `src/` and `tests/`. Sends were
limited only by gold. That means this log's own open question, "Does the global 30-tick send
cooldown create enough breathing room once bots and humans send together?", has never
actually been testable until now, and every balance observation recorded above was made
against an *uncapped* send cadence.

**Balance impact is significant and should be watched.** Bots previously sent every tick.
With the cooldown enforced, the reference bot-vs-bot match (`Two_bots_complete_a_local_carousel_match`,
default seed/config) went from 1012 to **1643 ticks, +62%**. That lands inside the
900-1800 match-completion target from this log's very first entry rather than below it, so
the direction looks right, but it is a large enough change that the next round of tuning
observations should not be compared against pre-cooldown numbers.

The value is a single constant in `LocalVerticalSlice`'s `EconomyRules` construction if it
needs tuning. Enabling at the documented 30 (rather than a shorter anti-abuse-only value)
was a deliberate choice, not an inherited default.

**Related open question this now raises:** bot send *quantity* (`GetSendQuantity`: Greedy up
to 3, Balanced up to 2, Defensive 1) was tuned when bots could send every tick. With cadence
now rate-limited, per-send quantity may be the more appropriate lever for profile
aggression, and the current numbers may under-serve Greedy in particular.

## 2026-07-28 (art): Ash Revenant Size Increased, And Authored Scale Is Not Perceived Size

Raised `creep.revenant`'s `runtimeScale` from 0.85 to 1.05 in
`Creep3DProofSetGenerator.Specs` and promoted it into `CreepVisualLibrary.asset`.

**Why:** at 0.85 the Revenant sat inside the small cluster (Wisp 0.75, Swarm 0.80, Shade
0.82) and read as chaff. That works against its entire design question — "does the defender
finish off a fragile, high-value target, or let it feed the sender's economy?" — because a
player can only prioritise a target they can pick out of a wave.

**Measured effect.** Authored `runtimeScale` turns out to be a poor guide to on-screen size,
because each source FBX has a different intrinsic mesh size. Measuring combined renderer
bounds x library scale x the renderer's uniform 1.18/1.12/1.18 creep multiplier gives the
actual silhouette:

| Creep | Authored scale | Effective height | Effective width |
| --- | ---: | ---: | ---: |
| Obsidian Brute | 1.20 | 1.032 | 1.195 |
| **Ash Revenant** | **1.05** | **0.882** | **0.621** |
| Spire Turret Walker | 1.00 | 0.827 | 1.406 |
| Brute | 1.18 | 0.818 | 1.277 |
| Shade | 0.82 | 0.689 | 0.513 |
| Swarm | 0.80 | 0.672 | 0.673 |
| Siege | 1.13 | 0.640 | 1.200 |
| Crystal Wisp | 0.75 | 0.593 | 0.797 |
| Runner | 1.13 | 0.567 | 1.200 |
| Serpent Coil | 0.85 | 0.519 | 0.903 |

The Revenant is now the second-tallest creep, but also the second-narrowest — a tall, slender
wraith rather than a bulky one. By rough visual footprint (height x width) it lands at 0.548,
mid-pack: clearly above the small cluster (Shade 0.353, Swarm 0.452, Wisp 0.473) and clearly
below the heavies (Brute 1.045, Turret Walker 1.163, Obsidian Brute 1.233). That is the
intended read for an 8-health glass cannon: noticeable, not tanky.

**Separate readability problem this measurement exposed, not fixed here.** Authored scale and
perceived size disagree badly across the roster, and in at least one case the ordering is
backwards relative to threat:

- **Siege** (48 health, the "heavy leak-threat tank", authored 1.13) is only 0.640 tall —
  *shorter than Shade*, a 14-health unit. It is wide (1.200) but visually squat.
- **Runner** (authored 1.13) is the shortest creep in the roster at 0.567.
- **Serpent Coil** (authored 0.85) is the shortest overall at 0.519 despite 32 health.

This got its own pass immediately — see the entry below.

## 2026-07-28 (art): Creep Silhouette Pass — Size Now Tracks Health

Follow-up to the Revenant entry above, which exposed the problem. Every creep's authored
`runtimeScale` in `Creep3DProofSetGenerator.Specs` was re-solved from measured geometry and
promoted into `CreepVisualLibrary.asset`.

**The problem.** Authored scale was effectively arbitrary, because it is not comparable across
creeps: each source FBX has a different intrinsic mesh size, and the renderer applies a further
1.18/1.12/1.18 on top. Silhouette therefore carried no threat information, and in places
actively lied — Siege (48 health) rendered *shorter* than Shade (14 health), and Serpent Coil
(32 health) was the smallest creep on the board despite being a mid-tier tank.

**Root cause.** The art intake normalises every creep with `--target-height 0.75
--max-footprint 0.9`, whichever binds first. Measuring intrinsic prefab bounds shows Runner,
Siege, Serpent and Wisp all sit at *exactly* 0.900 width — they hit the footprint cap, which
squashed their heights to 0.45-0.55 while the height-bound models kept a full 0.75. Uniform
intake normalisation is precisely what strips size of meaning.

**The rule now applied.** Perceived size, taken as `sqrt(effectiveHeight * effectiveWidth)`,
tracks health along `0.62 + 0.52*sqrt((hp-4)/56)`. Each scale is solved from that creep's
measured prefab bounds, subject to two clamps: effective width <= 1.45 world units (one grid
cell is 1 unit; wider fouls neighbouring lane content) and a health-tracking height ceiling so
slender meshes cannot tower over heavier creeps.

| Creep | Health | Scale (old -> new) | Eff. height | Eff. width | Size (old -> new) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Crystal Wisp | 4 | 0.75 -> 0.68 | 0.537 | 0.722 | 0.688 -> 0.623 |
| Swarm | 5 | 0.80 -> 0.82 | 0.689 | 0.690 | 0.672 -> 0.689 |
| Ash Revenant | 8 | 1.05 (held) | 0.882 | 0.621 | 0.740 -> 0.740 |
| Runner | 10 | 1.13 -> 1.08 | 0.541 | 1.147 | 0.825 -> 0.788 |
| Shade | 14 | 0.82 -> 0.99 | 0.832 | 0.619 | 0.595 -> 0.718 |
| Brute | 24 | 1.18 -> 1.07 | 0.742 | 1.158 | 1.022 -> 0.927 |
| Serpent Coil | 32 | 0.85 -> 1.23 | 0.751 | 1.306 | 0.685 -> 0.991 |
| Spire Turret Walker | 40 | 1.00 -> 0.96 | 0.794 | 1.349 | 1.078 -> 1.035 |
| Siege | 48 | 1.13 -> 1.36 | 0.770 | 1.444 | 0.876 -> 1.055 |
| Obsidian Brute | 60 | 1.20 -> 1.23 | 1.058 | 1.225 | 1.111 -> 1.138 |

Biggest corrections: **Siege** 7th largest -> 2nd, and **Serpent Coil** last -> 4th. Brute and
Turret Walker came *down* slightly so they no longer outrank creeps that outlast them.

**Two accepted inversions, both understood.** Shade (14 health) and Ash Revenant (8 health) are
slender meshes that hit the height ceiling before reaching their size target, so both sit
slightly below where health alone would place them. Revenant is additionally a deliberate
exception: its threat is economic, not durability — best income-per-cost in the roster — so it
is held at 1.05 to stay pickable out of a wave, per the entry above.

**Note for anyone editing these numbers.** Changing a scale by hand will usually be wrong,
because the same number means a different on-screen size for every creep. Re-measure prefab
bounds and re-solve against the rule.

**Verification:** wrappers validated (10, no issues), promoted (9 of 10 library scales changed,
Revenant held), re-measured to confirm solved values landed, max width 1.444 within the 1.45
cap, and a full batch playtest passed with a clean reset.

**Not verified:** how this reads in motion to a human eye. The numbers are right; whether Siege
now *feels* like the second-biggest threat is a judgement only a real playtest can make.

## 2026-07-28 (art): Spire Turret Walker Split Off The Golem Rig; Foot Skate 51x -> 8.3x

The walker read badly in play. Investigating from the *actual* game camera (per
`docs/OPEN_ITEMS.md` item 4) found two measured causes, neither of which was the gait
tuning it looked like.

**1. Foot skate, 51x — the dominant problem.** Nothing syncs animator playback to
movement speed; the clip loops at its authored rate while the simulation translates the
creep independently. `SpeedPerSecond` is cells per *tick* at 4 ticks/sec, and the walker
is the only rigged creep at speed 2 — so it crosses the board at 8 world units/sec (one
grid cell = one world unit) while a measured 0.139-unit stride on a 1-second cycle implied
0.157 units/sec of walking. Its feet needed to move 51x faster. It was being dragged.

Note the body advances one stride per *leg cycle*, not per beat — all legs must agree on
body displacement. An earlier estimate of 29x wrongly counted the trot's two beats as two
strides.

**2. A symmetric trot is self-cancelling from this camera.** The camera is orthographic,
tilted 30 degrees off vertical, and creeps travel *toward* it, so the walker is seen
head-on. A bilaterally symmetric diagonal trot's two contact poses are mirror images,
which head-on look nearly identical. Measured screen-space foot travel was 92.7px vertical
against **10.7px horizontal** — the screen's horizontal axis carried essentially nothing.

**Fix: the walker now has its own rig script.** `tools/art_pipeline/rig_turret_walker.py`,
split out of `rig_quadruped_creep.py` (which is built around the two rock golems — its
armature is literally named `BruteArmature`). A mechanical spider does not share a gait
with a squat golem, and keeping them together meant every walker change risked the golems.

| | Before | After |
| --- | ---: | ---: |
| Gait | 2-beat diagonal trot | 4-beat wave (FL, BR, FR, BL) |
| Stride (model units) | 0.139 | 0.213 |
| Leg cycle | 1.00s | 0.25s |
| Implied ground speed | 0.157 u/s | 0.965 u/s |
| **Foot skate** | **51x** | **8.3x** |
| Screen travel X / Y | 10.7 / 92.7 px | 27.5 / 153.3 px |

Also added, both aimed at the unused screen-horizontal axis: a turret scan (22 degrees yaw,
one sweep per clip) and a small chassis yaw. Clip length and leg cycle are deliberately
decoupled — the clip is 24 frames with the legs running four gaits inside it and the turret
scanning once, so the legs can be fast without the turret looking frantic, and it still
loops cleanly because both complete whole cycles.

**Honest residual: 8.3x skate remains.** 8 units/sec is roughly six body lengths per second;
no legged gait reads as that. Pushing to 5.5x needs 24 footfalls/sec, which trades skating
for blurring. The real fix is driving animator playback from creep speed at runtime, which
would also help Brute and Obsidian Brute (~13x and ~12x, both unaddressed here) — deliberately
left out of scope to keep this walker-only.

**Verification:** golem rigs proved unchanged by *semantic* comparison — freshly re-rigged
Brute and Obsidian Brute match their committed FBXs to 4 decimal places on every leg's stride
and lift. (Byte comparison is useless here: FBX export is non-deterministic, and two identical
runs of the same script produce different hashes.) Wrappers validated 10/10 with no issues,
promoted, and a full batch playtest passes with a clean reset. Incidental regeneration churn on
the golem prefabs/controllers was reverted — notably a 0.0132 drift in the Brute's ground
offset that was not a correction.

**Not verified:** how it reads in motion to a human eye. Frame renders from the game camera
confirm the poses are now genuinely distinct; whether the walker *feels* right at speed is a
judgement only a real playtest makes.
## 2026-07-28: Category 3 ("ELITE") — Five Meshy-Rigged Bipeds, And The Categories Get Real Names

Third five creeps, taking the roster to 15. These differ from every previous batch: Meshy
**auto-rigs bipeds**, so they arrived as 24-bone humanoid rigs (`Hips` root, Mixamo-style
naming) with walking and running clips already authored. No Blender rigging step was needed —
`rig_quadruped_creep.py` exists only because Meshy cannot rig quadrupeds.

**They deliberately skip `ai_asset_intake.py`.** Its normalize step exports
`object_types={"MESH","EMPTY"}` with no `bake_anim`, which would have silently stripped the
armature and every clip. Scale and orientation are handled by `Creep3DImportSpec`'s
`importScale`/`importEulerAngles` instead, which is what those fields are for.

### Category names

The send menu's `"CATEGORY 1"`/`"CATEGORY 2"` placeholders are retired. Each is now named for
what it actually does:

| Idx | Name | Meaning |
| --- | --- | --- |
| 0 | **CORE** | The founding five; all send-cooldown gated. |
| 1 | **RAPID** | Every Category 2 creep sets `ignoresSendCooldown` — that exemption is their identity. |
| 2 | **ELITE** | These bipeds: costlier, heavier, and back on the normal cooldown, because price is what paces them. |

### Stats (first pass, tunable)

A deliberately later tier — costs and health run past the first ten, which is self-limiting
because cost is the gate.

| Creep | Id | Cost | Inc | Kill/Leak | HP | Spd | Clip | Defensive question |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| Zephyr Wraith | `creep.zephyr` | 22 | +2 | 2/3 | 12 | 3 | Run | Can defence actually catch something fast, or only tank it? |
| Fracture Burrower | `creep.burrower` | 26 | +2 | 3/4 | 44 | 1 | Walk | Sustained mid-tier tank pressure with no gimmick. |
| Umbral Stalker | `creep.stalker` | 28 | +3 | 2/4 | 20 | 2 | Run | Does coverage extend past the opening cluster? |
| Aegis Warden | `creep.warden` | 34 | +3 | 3/4 | 55 | 1 | Walk | Can defence out-damage a heavily armoured advance? |
| Siege Colossus | `creep.colossus` | 52 | +5 | 5/8 | 90 | 1 | Walk | The true late wall — highest cost and health in the roster. |

Walk-vs-run follows speed: speed 1 takes the walking clip, speed 2–3 the running clip. A run
cycle's longer stride and faster cadence measurably reduces the foot skate documented in the
Turret Walker entry above. It mitigates rather than solves it — the runtime fix (driving
animator playback from creep speed) is still not done, and these will skate like everything else.

**Name collision, handled deliberately:** "Siege Colossus" against the existing `creep.siege`.
Same call as Obsidian Brute against Brute — kept and differentiated by stats rather than
renamed. It is labelled COLOSSUS, not SIEGE, and at 90 health / 52 gold it outclasses Siege's
48 / 40 rather than duplicating it.

### Silhouette scales, and a mistake worth recording

Scales were solved with the health-tracking rule from the silhouette pass above. The first
solve was **wrong**: it used the models' Blender bounds, but Unity's FBX import applies a
unit-scale conversion that makes the imported mesh roughly 1.8x larger. Every Category 3 creep
came out about double size, with a 2.04 effective width against a 1.45 cap. Re-solving against
the measured *Unity-space* prefab bounds fixed it. This is exactly why the rule says to measure
after promoting rather than trust the authored number.

| Creep | HP | Scale | Eff. height | Eff. width | Size |
| --- | ---: | ---: | ---: | ---: | ---: |
| Zephyr Wraith | 12 | 0.278 | 0.817 | 0.818 | 0.818 |
| Umbral Stalker | 20 | 0.208 | 0.855 | 0.947 | 0.900 |
| Fracture Burrower | 44 | 0.245 | 0.946 | 1.189 | 1.061 |
| Aegis Warden | 55 | 0.428 | 1.103 | 1.129 | 1.116 |
| Siege Colossus | 90 | 0.329 | 1.247 | 1.285 | 1.266 |

Across all 15, max width is 1.444 (cap 1.45) and only four size-vs-health inversions remain:
three are the known Shade slenderness case, and Siege/Burrower differ by 0.006.

### Bot preference ordering is now structural, fixing a latent bug

`BotController.SelectCreep`'s lists were hand-ordered by descending cost, with a comment
explaining that anything after a cheaper id is unreachable. Adding Category 3 exposed the flaw
in relying on that: `creepId.Value` — the bot's *configured primary creep* — was appended last
regardless of price, so a bot given the 40-gold Siege as its primary could never actually send
it from a tier whose other entries were cheaper. The list is now sorted by descending cost at
runtime, so the invariant holds by construction rather than by discipline. A new test,
`Every_creep_a_bot_profile_prefers_is_reachable_at_some_gold_level`, sweeps gold and asserts
every named creep is selectable somewhere — the check that would have caught the original
zero-sends bug in milliseconds instead of via replay analysis.

### Other fixes made while here

- **Serpent Coil's UI cost was stale.** Content was cut 22 → 20 in the rebalance above, but the
  card, its affordability gate and its feedback message all still said 22, so an affordable
  Serpent could read as unaffordable.
- **The send dock dispatched categories with a bare `else`**, which would have silently rendered
  Category 2's grid for Category 3. Now explicitly three-way.
- **The cooldown countdown was suppressed by a hardcoded `selectedCategory != 1`.** Category 3 is
  gated again, so that index test would have hidden a countdown that does apply; it now asks
  whether the category has any gated cards.
- **The category picker overflowed its panel.** Three 84px cards need 352px inside a 282px panel,
  so the panel grows to 374 while the picker is up — the same class of bug its own code comment
  records having fixed once already.
- **`repack_metallic_smoothness.py` gained a second input shape.** Meshy emits separate metallic
  and roughness maps as well as the combined glTF one, and the script only understood the latter.
  This meant **every Category 2 creep had been shipping with no metallic response at all**, since
  none of them ever got a `Baked_MetallicSmoothness.png`. Now fixed for all five — see the
  follow-up entry below.

**Verification:** 97/97 tests pass; 15 wrappers validate with no issues; scales re-measured after
promote; batch playtest passes with a clean reset. Incidental regeneration churn on the three
existing rigged creeps (a ~0.012 ground-offset drift each, plus controller id reshuffling) was
reverted, same as on the walker pass.

**Not verified:** how these look in motion. The models carry real skeletal animation while the
renderer also layers procedural motion on top, so `CreepVisualMotionStyle` is `Auto` for all five
to avoid double-animating — whether they need more is a judgement for a real playtest.


## 2026-07-28 (art, follow-up): Every Creep Now Has A Correct Metallic Map

Closing the Category 2 gap found during the Category 3 work, plus a colour-space bug that would
otherwise have shipped with Category 3 itself.

**Category 2 had no metal response.** Those five creeps arrived from Meshy with separate metallic
and roughness maps rather than the combined glTF map the repack tool understood, so none of them
ever got a `Baked_MetallicSmoothness.png` and their materials bound no `_MetallicGlossMap`. All
five now have one.

**Category 3's maps were importing as sRGB.** A packed metallic-smoothness map is data — metallic
in R, smoothness in A — so importing it through the sRGB transfer curve gives wrong metal and
gloss. Unity defaults new PNGs to sRGB, and the Category 1 maps have `sRGBTexture: 0` only because
someone set it by hand. The five Category 3 maps did not, and would have rendered subtly wrong.

Both are fixed durably rather than by hand:

- A `LinearMetallicMapPostprocessor` (`CreepMaterialMapBackfill.cs`) forces `sRGBTexture` off for
  any `Baked_MetallicSmoothness.png` at import, so this cannot be forgotten for a future creep.
- A one-time `Line Wards/Art/Backfill Creep Metallic Maps` menu item binds the map into body
  materials that predate it. This is needed because `Creep3DImportPipeline.CreateBodyMaterial`
  deliberately returns existing materials untouched so hand-tuning survives regeneration — which
  also means a map added later never gets picked up.

**Verified:** all 15 metallic maps now import linear (`sRGBTexture: 0`), all 15 creep body
materials have a bound `_MetallicGlossMap`, and a batch playtest passes with a clean reset.

**Not verified:** how much visual difference this actually makes. The maps are bound and correctly
interpreted, but whether Category 2's creeps now read as convincingly metallic is a judgement for
a real look at the board.


## 2026-07-29 (content): Ten New Towers, Three Build Lines, And One Price

The tower roster goes from 5 to 15: a Foundry line (Gatling Turret, Tesla Coil Spire, Foundry Core,
Barricade Bastion, Repair Drone Spire) and a Grove line (Elder Canopy, Sapling Sentinel, Bloomheart
Totem, Thorn Snare Totem, Spore Cloud Bloom). The build palette is now a category picker over
ARCANE / FOUNDRY / GROVE, mirroring the send dock.

**The palette had three different prices for the same tower.** The buttons advertised 20 gold for
the Arrow Tower, `SelectedTowerCost()` returned 25, and the simulation charged 14. Both the
displayed price and the affordability gate disagreed with what the player was actually charged, on
every tower. The cause was six parallel switch statements on the palette's role index, each
extended by hand per tower. `TowerCatalog` replaces them with one table, and cost is deliberately
NOT in it — the client reads `ContentCatalog` at display time, because a client-side copy of a price
is exactly what went stale.

**The Control Ward was unbuildable by any rational player.** At 24 gold, range 2, damage 2, cooldown
3 it was strictly worse than the 14-gold Arrow Tower on every axis at once — dearer, same reach,
same damage, slower — and unlike the Relay Ward it has no compensating mechanic. Range goes 2 to 3,
making it the only tower under 30 gold with reach. `TowerRosterTests.No_tower_is_strictly_dominated_by_another`
now enforces this; the Relay Ward is explicitly exempt because its gold-per-hit payoff cannot be
expressed in those four numbers.

**Thorn Snare Totem was trimmed before it landed.** At 26 gold for 7 damage on a 3-tick cooldown its
damage-per-gold was 0.359 against the Pulse Ward's 0.188 — nearly double, for the same range-1
shape. Damage dropped to 5 (0.256).

Damage-per-gold across the roster now spans 0.071 (Relay Ward, subsidised by signal gold) to 0.286
(Arrow Tower). First pass only.

**Verified:** all ten models pass `ai_asset_intake` (~15k triangles each against 25k, one material,
footprint inside 1.45; the whole batch shares a soft "no normal map" warning because Meshy did not
export normals). 102 tests pass. Headless Unity compile clean.

**Also fixed here, both pre-existing:**

- The send dock's category picker overflowed its panel again. An earlier fix sized cards to fit two
  categories; a third arrived later, putting the last card's bottom edge at 352 inside a 282-tall
  panel, hanging over the board. Card height is now divided out of the panel's actual height, so a
  fourth category cannot bring it back.
- `render_send_icon.py` never downsampled. Its docstring promised "renders at 4x and downsamples"
  and it set resolution to `ICON_SIZE * SUPERSAMPLE`, but nothing resolved it back down, so every
  icon it ever produced was 512x512 against the 128x128 the hand-authored originals use. Five creep
  icons had already shipped that way. Fixed, and those five brought down; all 30 icons are uniform.

**Not verified:** balance. Nothing here has been playtested — the stats are a first pass sized off
damage-per-gold arithmetic, not play. Six of the ten also have no special behaviour yet, so they are
currently plain single-target towers distinguished only by numbers.

**Open design question, deliberately not settled:** whether the Foundry Core's mortar shell resolves
immediately (with the arc as pure presentation) or lands after a flight time and can therefore miss
when the creep walks on. The second is the more interesting tower but needs scheduled per-shell
state, and at a 2-tick flight against a 2-cell/tick creep it means leading the target by 4 cells,
which changes how the tower plays rather than just how it looks.


## 2026-07-29 (balance, measured): Towers Only Get One To Three Shots Per Creep

A headless duel harness (`TowerDuelBalanceTests`) now puts each tower alone beside the lane and
counts what it achieves as one creep walks the full 18 cells. The numbers are worse than the
damage-per-gold arithmetic suggested, and the cause is not the towers.

| tower | gold | kills a Runner (10hp)? | shots landed on a Brute |
| --- | ---: | --- | ---: |
| sapling | 10 | no, leaked | 1 |
| arrow | 14 | no, leaked | 2 |
| barricade | 18 | no, leaked | 1 |
| bloomheart | 22 | no, leaked | 1 |
| control | 24 | no, leaked | 2 |
| thorn_snare | 26 | no, leaked | 1 |
| relay | 28 | no, leaked | 1 |
| gatling | 30 | no, leaked | 3 |
| pulse | 32 | no, leaked | 1 |
| repair_drone | 34 | no, leaked | 3 |
| spore_cloud | 36 | 2.50s | 2 |
| tesla | 38 | 2.25s | 2 |
| prism | 42 | 2.75s | 2 |
| elder_canopy | 46 | 2.50s | 2 |
| foundry | 52 | 1.75s | 1 |

**Only 5 of 15 towers can kill even the cheapest creep during a full pass, and none can kill a
Brute.** Ten of fifteen land a single shot.

**The cause is creep speed, not tower stats.** `CombatService.MoveCreeps` adds
`definition.SpeedPerSecond` to movement progress once **per tick**, and the client runs 4 ticks per
second — so a creep authored at speed 1 travels 4 cells per second and crosses the whole 18-cell
lane in 4.5 seconds. A range-2 tower covers 5 cells of lane, which is 5 ticks of exposure; at a
2-tick cooldown that is 2 shots. The field is named `SpeedPerSecond`, and
`TOWER_AND_CREEP_ROSTER.md` already notes it "is currently applied once per simulation tick, so
current local-client cells/sec is `SpeedPerSecond * 4`" — which reads like someone measured this and
wrote it down rather than intended it.

If the field were applied once per second as its name says, exposure would quadruple:

| tower | shots per pass now | shots if applied per second |
| --- | ---: | ---: |
| Arrow (range 2, cd 2) | 2.5 | 10.0 |
| Gatling (range 2, cd 1) | 5.0 | 20.0 |
| Prism (range 4, cd 6) | 1.5 | 6.0 |

**Deliberately not changed.** Multiplying every tower's effective output by four is the single
largest balance lever in the game and would invalidate every cost on the roster, so it is a decision
to make on purpose rather than a bug to quietly fix on the way past. Two honest readings exist: the
semantics are wrong and creeps should be 4x slower, or the semantics are fine and the cooldowns are
4x too long. The first is the more likely given the field's name.

**Verified:** `Every_tower_lands_a_shot_on_a_creep_walking_past` and
`No_tower_is_an_order_of_magnitude_more_gold_efficient_than_the_median` pass. The first began life
asserting every tower could solo a Runner, which was wrong about the game rather than a finding
about it — no single tower solos anything at current speeds, so the assertion was lowered to the
real floor.


## 2026-07-29 (mechanics): Nine Of Fifteen Towers Now Do Something Specific

Six mechanics landed, designed through a panel of independent proposals and then adversarially
reviewed — most proposals were refuted and are recorded here so they are not resurrected.

**Barricade Bastion — Fixed Emplacement.** Never turns; can only engage creeps that have not passed
its own row. Range 1→2, damage 3→5, cost unchanged at 18. The restriction is a net improvement even
before the mechanic: at range 1 with a full diamond, 66 of the 110 legal placements could hit nothing
at all; at range 2 with the up-lane half-plane that falls to 34, all of them columns 0 and 6, which no
range-2 tower reaches the lane from anyway. Damage 5 also crosses the renderer's `damage >= 5`
threshold, so the shot changes colour and starts printing numbers — the compensation is literally
visible. Measured damage-per-gold 0.17 → 0.28.

REFUTED: a 1-cell-wide, 4-cell-deep firing line. It required the route to run up the tower's own
column, so barricades cannibalised each other by diverting the path, and dead placements rose to 96
of 110.

**Foundry Core — Stack Mortar.** Deals no damage when it fires. The shell leaves the stacks and lands
2 ticks (0.5s) later on a pre-computed cell, damaging every live creep standing there. It leads the
target by asking the same `StepCreep` function movement uses — computing the lead independently would
silently miss every shot in a bramble-braked lane with no test failing. It can genuinely whiff, which
is the point of aiming at ground rather than at an entity.

Chose delayed over immediate resolution deliberately. Resolving at launch would leave the renderer
two options, both broken: play the damage before the shell arrives, or animate a shell whose impact
already happened. The delay also needs an impact telegraph to be fair rather than hidden dice — if
that cannot be built, resolve immediately and drop the arc instead.

**Sapling Sentinel — Grovebond.** +1 damage per orthogonally adjacent Grove tower of the same owner
and lane, capped at +3. Diagonals do not bond. Self-limiting in a way that resists a runaway: in a
solid block the highest-bonus towers are the interior ones, and interior towers see no route cells, so
they never fire.

**Spore Cloud Bloom — Rot.** Damage is `max(authored, target max health / 6)`. Cost 36→34, damage 6→4,
cooldown 4→6: the authored number is now only a floor. Be honest about the shape — rot only exceeds
the floor above 30 max health, so it is a step at the 32hp line rather than a curve, and it is inert
on 8 of the 15 creeps. It reads AUTHORED max health, so chipping a creep first cannot inflate the hit.

**Bloomheart Totem — Reaping Bloom.** Shoots the creep it can kill outright this shot, else the
weakest, else the leader. Stats unchanged. Lethality is tested after `AdjustDamageForRoles`, not
against raw damage: without that a 3hp Shade (which halves incoming damage) would enter the "lethal"
partition, outrank a genuinely killable creep, and eat the totem's one shot per pass without dying —
the mechanic visibly failing at the exact moment it should read as working.

**Thorn Snare Totem — Bramble Hold.** Creeps that START a tick inside its zone advance at exactly half
speed. Cost 26→30, range 1→2. The range bump is required by the mechanic, not a buff: at range 1 the
zone could not reliably cover 3 route cells and a speed-3 creep would step clean over it. Uses the
`MovementProgress` accumulator that already existed and was always zero until now.

REFUTED: a version that only slowed slow creeps (inert on 8 of 15), and one that damaged everything in
contact (assumed at most 4 creeps can touch a tower, but creeps stack on a cell — a quantity-N send
spawns N creeps on one index).

**Verified:** 124 tests pass, 19 of them new and specific to these mechanics. Headless Unity compile
clean. Measured after the change: Barricade 0.28 damage-per-gold, Thorn now kills a Runner in 2.50s
where it previously could not, Spore correctly drops to 0.12 against a 24hp Brute (inert) while
scaling to 15 per shot against a 90hp Colossus, Foundry kills in 2.25s.

**Not verified — needs real play, in priority order:**

1. **Foundry's whiff rate is a ship/no-ship gate.** A 52-gold tower that visibly does nothing some of
   the time is a trap, and the whiff sources compound exactly when the player has built WELL (an
   over-defended lane kills the target before the shell lands). Measure whiffs bucketed by tower row
   and creep speed. Above roughly 25%, the first lever is flight time 2→1, not more damage.
2. **Thorn Snare may be a mandatory purchase.** A 30-gold tower that roughly doubles the shot
   opportunities of every tower covering three cells is suspicious. The only safe lever is cost;
   zone width cannot drop below 3.
3. **Bloomheart may be a near-no-op.** In the modal case — a same-type send, which stays stacked at
   one path index with equal health — all four sort keys tie and it degrades to the default rule.
   Measure the fraction of its shots that pick a different creep than the default would.

**Still without any mechanic:** Tesla Coil Spire, Repair Drone Spire, Elder Canopy. Their names all
promise something (chain lightning, repair/support, area denial) that the simulation has no vocabulary
for yet.


## 2026-07-29 (mechanics, completion): All Fifteen Towers Now Do Something

The last three had names promising behaviour the simulation had no vocabulary for. Each got that
vocabulary rather than a reskin of an existing rule.

**Tesla Coil Spire — Chain Arc.** After the primary hit the bolt jumps back down the queue up to two
more times, halving each hop, each link within 2 cells of the last. This is deliberately NOT Pulse:
Pulse hits everything within one cell of the target at flat half damage and rewards a clump, while the
chain walks a line and decays, rewarding a single-file column — what a trickle send looks like.

The direction is load-bearing, and the first implementation had it wrong. Chaining FORWARD looked
natural but target selection picks the front-most creep, so the arc would search ahead of the leader,
find nothing, and the mechanic would have been a total no-op in every real game. A test caught it
immediately. Hitting the leader and arcing back through the queue behind it is also the better read.

**Repair Drone Spire — Overwatch Uplink.** +1 range to every orthogonally adjacent tower of the same
owner and lane. The name promised repair, but towers never take damage, so support is expressed as
reach instead. This is the only mechanic that modifies another tower's range, and it pairs
particularly well with the Barricade, whose limitation is a shallow forward arc. Bonuses do not stack
— two drones beside one tower still give +1, or a drone sandwich would be a cheaper Prism.

**Elder Canopy — Deep Roots.** Targets the creep furthest BACK in range instead of the leader. With
the roster's longest reach (5) it engages arrivals at the mouth of the lane, softening a wave before
anything else sees it, which is what "area denial" means here. A pure selection rule — the cheapest
possible change — and nothing else on the roster targets back-most, so the tell is that it visibly
shoots the far creep while its neighbours shoot the near one.

**Verified:** 135 tests, 7 new for these three. Headless Unity compile clean. Damage-per-gold is
unchanged for every tower whose stats did not move, so none of this rebalanced the roster by accident.

**Not verified:** Repair Drone's range buff has no visual yet — the neighbour's range halo should grow,
and without that the mechanic is invisible. Chain Arc reuses the generic beam cue rather than drawing
the hops as separate arcs.


## 2026-07-29 (design): No Mandatory Buys, And No Purchase Timers

Two directions from the owner, both acted on.

**The send cooldown is gone.** `sendCooldownTicks` goes 30 to 0, so gold is the only thing that gates a
send. The rule had been described in the design docs for a long time without ever being enforced; the
seats/authority pass switched it on, and in play it was clearly wrong for this game. 7.5 seconds between
any two sends makes 5-gold chaff like the Crystal Wisp unusable AS chaff, and the send dock had to grow
a countdown purely to explain why a card the player could plainly afford refused to work. When the UI
has to apologise for a rule, the rule is the problem.

`EconomyService`'s enforcement and `CreepDefinition.IgnoresSendCooldown` are deliberately left intact
and still covered by tests with explicit non-zero values, so the rule can return by changing one
number. A new test pins the shipped value at zero, because the last cooldown arrived as a side effect
of an unrelated change and should not be able to do that again.

**Thorn Snare was the one mandatory buy, and it has been cut back.** Its bramble zone widened to every
route cell the tower could see — 5 cells at range 2 — because the width calculation took whichever was
LARGER of the covered span and the 3-cell minimum. Slowing everything that crosses is a force
multiplier for every other tower, and at 5 cells wide that is value no build would decline. The zone is
now exactly 3 cells, which still guarantees a speed-3 creep cannot step clean over it (the only
constraint the width exists to satisfy) while cutting the affected span by 40%. Cost 30 to 34 prices
what remains. Damage-per-gold 0.33 to 0.29.

Worth stating plainly: "no mandatory buys" cannot be enforced by a test the way strict domination can.
`No_tower_is_strictly_dominated_by_another` catches the opposite failure — a tower nobody would ever
build — but a tower EVERYONE builds looks fine on all four stat axes and is only visible in play. Thorn
Snare was caught by reasoning about the mechanic, not by a measurement, and the same class of problem
could hide in Repair Drone's range buff (also strictly additive, also helps every neighbour).


## 2026-07-29 (measured): Reaping Bloom Was Decoration, So It Was Replaced

Bloomheart Totem was flagged as possibly a near-no-op. It was, and the measurement is worth keeping
because the method generalises.

`BloomheartDivergenceTests` runs each scenario TWICE against bit-identical state — once with the real
Bloomheart, once with a baseline tower carrying Bloomheart's exact stats under an id the mechanic does
not match — and compares shot-by-shot target choice. The difference between the two runs is the
mechanic's entire contribution, measured through the real CombatService rather than inferred.

Reaping Bloom (finish the weakest, else lead) changed the shot in:

| scenario | shots | shot changed |
| --- | ---: | ---: |
| same-type send x6 (the modal case) | 1 | 0% |
| same-type send x6 + a second tower chipping them | 1 | 0% |
| trickle x8 | 8 | 0% |
| trickle x8 + support | 8 | 0% |
| wounded trailer (hand-seeded) | 2 | 50% |
| **overall** | **20** | **5%** |

Zero in every organic scenario. The only divergence came from a wounded trailer constructed by hand.
The cause is structural: a quantity-N send spawns all N creeps on one cell, in one tick, at full health,
and they move as a pure function of position and speed — so all four of that rule's tie-breakers
(is-lethal, lowest health, furthest forward, entity id) tied, and the last one picked the same creep the
default front-most rule would. Adding a second tower to wound the group did not help, because creeps
cross the whole lane in 4.5 seconds and there is no time for health to diverge while they are in range.

**Replaced with Crowd Bloom:** +1 damage for every other creep sharing the target's cell, capped at +3.
This keys off exactly the thing that made the old rule inert — a stacked send is the modal case — so the
mechanic engages in the common situation rather than an exotic one.

Re-measured, same harness, now reading damage rather than target choice, since it is a damage rule:

| scenario | Bloomheart | stat-identical baseline |
| --- | ---: | ---: |
| same-type send x6 (stacked) | 7 | 4 |
| trickle x8 (single file) | 32 | 32 |

Strong against a stacked send, exactly baseline against a trickle. That shape matters for "no mandatory
buys": it counters a specific play rather than adding flat value, so declining it is a real option.

It is also not a duplicate of Pulse, which is the roster's other answer to a clump. Pulse SPREADS half
damage across the group and thins it; Crowd Bloom CONCENTRATES on one creep because the others are
there. Thin the crowd or punch through it.

**Method note worth reusing:** the "run it twice against a stat-identical control" comparison is how any
mechanic can be checked for being decoration, and it needs no production changes. Repair Drone's +1
range to neighbours is the obvious next candidate, since strictly-additive buffs are hard to judge by
eye.


## 2026-07-29 (measured): Repair Drone's Range Buff Was Decoration; Servicing Replaces It

Ran the Repair Drone Spire through the same run-it-twice-against-a-stat-identical-control harness that
condemned Reaping Bloom, and asked two separate questions.

**Question 1, is it decoration?** The +1 range to neighbours moved an adjacent Arrow Tower from 48 damage
to 50 across twelve creeps — one extra shot in the whole run, about 4%. Technically non-zero, which is
why the first version of the test (greater than zero) passed it. The bar is now a ratio: a support tower
that cannot move its neighbour by a fifth is not doing anything a player would notice.

The reason generalises and is the useful part. **Under continuous pressure every tower is
COOLDOWN-limited, not range-limited.** Extra reach only helps a tower idling for want of a target, so the
buff paid out in the sparse case where you did not need it and paid nothing in the dense case where you
did — exactly backwards for a support tower.

**Servicing replaces it:** every orthogonally adjacent tower of the same owner fires one tick faster,
floored at 1. That attacks the binding constraint directly. Re-measured, the adjacent Arrow goes 48 to 72
damage — a 50% gain, which a player can see.

It is deliberately NOT universally useful: a Gatling already at cooldown 1 gains nothing, so the drone is
good beside slow, heavy towers and worthless beside fast ones. That is a placement decision rather than a
flat buff, and there is a test for it.

**Question 2, is it a mandatory buy?** At its old 34 gold, yes — and the test caught it: a drone bundle
returned 3.00 damage per gold against 2.86 for the best plain-damage bundle at comparable gold, so taking
one was strictly correct and the choice was fake. Cost 34 to 40 brings both drone bundles just under
plain damage:

| bundle | gold | damage | dmg/gold |
| --- | ---: | ---: | ---: |
| arrow x3 (best plain damage) | 42 | 120 | 2.86 |
| arrow + drone | 54 | 144 | 2.67 |
| drone + two arrows | 68 | 192 | 2.82 |
| arrow + stat-identical control spire | 54 | 120 | 2.22 |

Just under, not far under, which is what a support tower should be — a lateral option whose real
advantage is board cells rather than raw throughput. One drone lifting one tower occupies fewer cells
than the extra towers needed to match it, and cells are the scarcest resource on the board.

**The opportunity-cost comparison is the reusable part.** Strict domination catches a tower nobody would
build; nothing catches a tower everyone builds. Comparing a bundle containing the tower against equal
gold spent on plain damage does, and it is now a standing test rather than a judgement call.


## 2026-07-29 (measured): Every Mechanic Contribution, And A Harness Bug That Faked The First Answer

Ran all the mechanics through the stat-identical-control harness, now generalised into
`MechanicContributionTests` so a future change cannot quietly make one inert.

**First, the harness itself was wrong, and its first table was fiction.** On the control run it mirrored
`tower.arrow`'s stats instead of the subject's, so the "stat-identical baseline" carried Arrow's cost,
range, damage and cooldown whatever tower was under test. Every number in the first run was a comparison
between two different towers. Two of my own no-op assertions caught it — Grovebond and Chain Arc both
reported a contribution in scenarios where they should have reported none — which is the argument for
writing the no-op cases as well as the positive ones.

Contributions after the fix, measured as the difference against a control with identical stats:

| mechanic | metric | stacked send | trickle |
| --- | --- | ---: | ---: |
| Grovebond (sapling) | own damage | +100% | +100% |
| Rot (spore cloud) | own damage | +275% | +275% |
| Bramble Hold (thorn) | lane damage | +78% | +15% |
| Crowd Bloom (bloomheart) | own damage | +75% | 0% |
| Chain Arc (tesla) | own damage | +60% | +49% |
| Servicing (repair drone) | neighbour damage | 0% | +25% |
| *Pulse splash (reference)* | own damage | +100% | 0% |

**Grovebond and Chain Arc were both fine** — the suspicion that prompted this was wrong for both. But the
run found two other things.

**Chain Arc was 0% against a stacked send, which is the modal case.** The hop test was "strictly behind",
and a quantity-N send puts every creep on the SAME path index, so the strict inequality excluded all of
them and the chain died on the leader. That is precisely the failure that killed Reaping Bloom — a
comparison that ties in the common case — arrived at independently in a different mechanic. Changed to
"at or behind"; already-struck creeps are excluded by entity id so it cannot loop. Now +60% stacked.

**Chain Arc then became a mandatory buy.** At 38 gold a Tesla returned 1.37 damage per gold against 1.24
for equal gold spent on plain Arrow Towers, so taking one was strictly correct. Cost 38 to 44.

**The 20% contribution bar is applied to each mechanic's DESIGNED case, not uniformly.** Crowd Bloom is
+75% stacked and 0% in a trickle; Servicing is the reverse. Pulse's long-standing splash has exactly the
same shape as Crowd Bloom, which is the reference point for calling it correct rather than a shortfall.
Demanding 20% in every scenario would be demanding that every mechanic be unconditional — which is the
definition of the mandatory buys we are trying to avoid. The off case is required only to be
non-negative.

Bramble Hold's +15% in a trickle is the direct result of cutting it back from 5 zone cells to 3: one
creep walking past loses a single tick, while a stack of six loses it all at once.


**Deep Roots checked too, and it holds.** Elder Canopy orders candidates by ascending path index, the same
shape as the two rules that failed, so it was measured rather than assumed: 0% target divergence against a
stacked send and 75% in a trickle. The stacked result is correct degenerate behaviour rather than a bug —
when every creep shares one path index, "furthest back" has no meaning and falling through to the default
is the only sensible answer. Its designed case is a spread stream at the mouth of the lane, and there it
changes the shot three times in four.

That closes the sweep: every mechanic on the roster now has a measured contribution, and the three that
were keyed on strict creep ordering have each been checked against the tie case that a quantity-N send
produces.


## 2026-07-29 (investigated, NOT shipped): What Slowing Creeps Actually Costs

The owner confirmed creeps feel too fast, which matches the arithmetic: `MoveCreeps` adds
`SpeedPerSecond` once per TICK against a 4-tick/second clock, so a creep authored at 1 cell per second
travels 4. I implemented the correction, measured the consequences, and then reverted it, because it is a
balance initiative rather than the one-line bug fix it looks like. The numbers are recorded here so the
decision can be made once rather than rediscovered.

The fix itself is clean: `MovementProgress` already exists as a sub-cell accumulator, so making a cell
cost `TicksPerSecond` movement instead of 1 gives exact integer arithmetic and Bramble Hold's doubling
still works untouched. That part is three lines.

**At the semantically correct 4x slower, the game stops working.**

- Matches never complete. A seed that finished at tick 331 did not finish in 24,000.
- Bots stop sending entirely at around tick 811 and then hoard gold — one reached 3,700 gold at income
  82 with zero creeps on the board and its tower count frozen. Their thresholds were tuned against creeps
  that crossed a lane in 4.5 seconds.
- Defence becomes overwhelming, which is the mirror image of the original complaint: towers get four
  times the shots, so almost nothing leaks and the 220-life pool never drains.

**At 2x slower the game still works**, and this is the shippable increment:

- Matches complete around tick 1,500 (the seed above finished with two players eliminated).
- Bots keep sending; creep counts stay healthy.
- Lane transit goes from 4.5 to 9 seconds.

**But even 2x is not free.** It changed enough that seven tests needed retuning, and the retuning kept
surfacing more:

- Every "advance N ticks and expect a leak" budget encodes the old speed.
- Tests that trickle creeps to form a single file need their spacing rescaled, or the trickle silently
  becomes a stack and the scenario stops measuring what it claims to.
- The Foundry's whiff rate went from 0% to 46% against a Runner with any supporting tower, because a
  10-health creep now spends long enough in range for a Gatling to finish it inside the shell's flight.
  Fixing that meant raising the mortar's minimum target from half a shell's damage to a full one, which
  changes which creeps it engages — from 13 of 15 to 10 of 15.
- Repair Drone and Tesla both became mandatory buys again on the opportunity-cost test, because the
  longer engagement window scales their mechanics more than it scales plain damage.

**What shipping this properly requires, in order:** the movement change; a bot-threshold pass so
`IsLaneUnderPressure` and the gold-reserve logic are sized for the new transit time; a starting-lives
number chosen against the new leak rate; re-pricing Repair Drone and Tesla; and a re-run of all four
measurement harnesses, since every damage-per-gold figure in this document was measured at the old speed.

Not shipped, and the repo is green at the old speed. The change is right and the game will be better for
it, but it is a coordinated rebalance, not a constant.


## 2026-07-29 (bots): They Were Not Playing The Game, And Fixing That Broke The Ending

The owner's call, and it is correct: the bots were dumb scripts, and this game is about mazing. Their
placements came from a hardcoded list of NINE cells in columns 1 and 5, picked with no reference to the
creep route. Two consequences, both worse than "the bots are weak":

1. **They never mazed.** They defended a straight lane that no human player would leave straight.
2. **They stopped building after nine towers**, because once those cells were occupied nothing else was
   ever tried. This is what left a bot in an earlier probe sitting on 3,700 gold at income 82 with its
   tower count frozen at nine — which I had misread as a send-gate bug.

`BestMazingPlacement` replaces the list. It scores every legal empty cell in the bot's lane as
`route length gained x 4 + route cells this tower covers`, and takes the best. Length dominates because
an extra step of walking helps every tower the bot owns and every one it builds later, while coverage
only helps the one being placed. Blocking placements never get scored — GridPathService rejects them
first. Cost is one BFS per candidate per placement, bounded by a 7x18 grid and one placement per tick.

Measured on lane 2, seed 1, after 1,200 ticks: **route length 16 cells straight to 40 cells mazed**, and
the busiest bot goes from 9 towers to 68.

**This invalidates a lot of the balance work in this document, and that needs saying plainly.** Every
figure in the tower duel, mechanic contribution, whiff rate and opportunity cost harnesses was measured
against a STRAIGHT route, because that is what the harnesses construct and what the bots produced. Real
play has a 40-cell path. A tower beside a 40-cell maze sees roughly two and a half times the exposure it
saw in those measurements, which means:

- "Towers only get 1-3 shots per creep, and 10 of 15 cannot kill a Runner" was measured on a straight
  lane and overstates the problem.
- The creep-speed conclusion from earlier today needs revisiting BEFORE any of it is acted on. Slowing
  creeps 4x on top of a 2.5x longer path would compound to roughly 10x more exposure.

**And competent bots exposed that the game cannot end.** With both bots mazing, neither can break the
other: verified to 80,000 ticks — about five and a half hours of game time — with no match summary. The
undefended human seat dies on schedule, then the two surviving bots sit above 180 lives each, sending
into defences that hold forever. Two match-completion tests are skipped with that reason recorded rather
than rewritten to bless the stalemate, because completion is the behaviour we want.

The game needs a closing mechanism against competent defence: escalating creep strength over time, an
income cap, or a sudden-death phase. That is a design decision, not a tuning one.


## 2026-07-29 (design): Category Upgrade Tiers

Designed, not implemented: `docs/CATEGORY_UPGRADE_TIERS_PLAN.md`, tracked as GD-09 in
`GAMEPLAY_DEVELOPMENT_CHECKLIST.md`.

Three tiers for each of the six categories. Tier 1 free and default, tiers 2 and 3 purchased at roughly
2.5x the previous cost. Creeps scale on health (100 / 150 / 225%), towers on damage (100 / 140 / 190%),
one stat each and nothing else.

Numbers worth recording here because they are the balance claims the plan will be judged on:

- Creep tier 2 costs 120, tier 3 costs 300. Tower tier 2 costs 100, tier 3 costs 260. Grounded against
  towers at 10–52 gold, creeps at 5–52, and mid-match income of 30–80 per interval, so tier 2 is about
  three towers' worth and tier 3 about eight.
- Tower tiers are priced under creep tiers at the same level because a tower tier applies to every tower
  in the line forever, while a creep tier only helps creeps bought afterwards. Equal pricing would make
  tower tiers strictly better.
- **Creep scaling is deliberately ahead of tower scaling at maximum investment**, 225% against 190%.
  That gap is the closing mechanism for the P1 stalemate where two mazing bots hold out past 80,000
  ticks. Defence still wins early and mid-game, which is correct; a fully-invested attacker gets ahead.

Two things the plan flags that will bite whoever implements it:

- Flat mechanic bonuses do not scale. Grovebond's `+1 damage per adjacent Grove tower` is worth
  proportionally less at tier 3 than tier 1, so tiers quietly weaken it. Every mechanic needs
  re-measuring at tier 3 through `MechanicContributionTests`.
- Pulse's splash reads `towerDefinition.Damage` directly rather than the shared `shotDamage` local, so
  it will silently stay at tier 1 unless updated. That separation was deliberate when Grovebond landed
  and is now a trap.

Also recorded: the existing `TechDefinition` / `BuyTechCommand` scaffolding is NOT reused. It models
unlocking content, not levelling it, and no tech content has ever been authored — the catalog passes
`Array.Empty<TechDefinition>()`. Whether to delete it is a separate decision.


## 2026-07-29 (prep): The Three Upgrade-Tier Traps Are Now Removed

Owner confirmed the 225% creep / 190% tower scaling gap, and asked for the three implementation traps in
the tier plan to be fixed up front rather than left as warnings. Done, with all 159 tests unchanged —
which is the point: none of this alters current behaviour, it only makes the behaviour reachable by a
multiplier.

**Pulse splash now scales.** `AttackWithTowers` computes two locals instead of one. `baseDamage` is the
tower's damage before any mechanic and is what every secondary effect reads; `shotDamage` is `baseDamage`
plus that tower's own mechanic, and applies to the primary hit only. Splash moved from
`towerDefinition.Damage` onto `baseDamage`, so a tier multiplier applied in one place reaches the primary
hit, the mechanics and the splash together. Splash still does not read `shotDamage` — that separation was
deliberate, so a tower matching both the pulse and sapling tokens cannot have its splash inflated by
Grovebond.

**Flat mechanic bonuses are now proportional.** Grovebond was `+1 damage per adjacent Grove tower` and
Crowd Bloom `+1 per creep sharing the cell`. Both are now percentages of `baseDamage` — 50% and 25% —
because a flat bonus shrinks as a share of a scaled base, so investing in a line would have quietly
weakened that line's own mechanic. The percentages were picked to reproduce the current numbers exactly
at authored damage: Grovebond 2 → 3/4/5 for one/two/three neighbours, Crowd Bloom 4 → 5/6/7. That is why
the test suite did not move.

**Both category pickers now share one sizing helper.** `RuntimeUiChrome.CategoryCardHeight` derives card
height from the panel and clamps a preferred height against it. This had been got wrong twice, in both
pickers, the same way — a fixed height, which at three categories pushed the last card's bottom edge to
352 inside a 282-tall panel and hung it over the board. A tier row that wants more space raises the
preferred height and lets the helper clamp it.

**Also clarified in the plan, at the owner's direction: each category upgrades on its own.** Six
independent tracks, no shared "tower tier" or "creep tier" and no cross-category discount. A player
wanting tier 3 in all three tower lines pays 100 + 260 three times. The intent is specialisation — twelve
purchases exist and gold only ever covers a few, so a player commits to the lines they actually build.


## 2026-07-29 (prep, follow-up): Rot And Chain Arc, And A Fourth Damage Path Nobody Had Noticed

The two mechanics flagged as interacting oddly with scaling are fixed, and looking for them turned up a
site that had been missed twice.

**Rot was the real problem of the two.** It was `max(baseDamage, maxHealth / 6)`, so a tower-line tier
would have raised only the FLOOR. Against a Colossus the rot term is 15 against a floor of 4 — the floor
never binds, so upgrading GROVE would have done nothing at all for this tower against exactly the fat
targets it exists to answer. It is now `baseDamage × (maxHealth / 24)` floored at 100%, where 24 is the
old divisor of 6 times the authored damage of 4. That reproduces every previous value across all fifteen
creeps: 4 up to Brute at 24 health, then 5 / 5 / 6 / 7 / 8 / 10 / 15 for Serpent through Colossus.

Deliberate consequence, now documented rather than accidental: a creep-category tier raises max health,
so an attacker upgrading their creeps makes this tower hit harder. That is correct for the roster's
anti-fat counter, and it is one of the few places a defender benefits from the attacker's investment.

**Chain Arc needed less than expected.** Halving is already proportional, so it scaled fine; what it was
doing wrong was decaying from the primary hit's post-mechanic damage rather than from base. Identical
today, since nothing modifies a Tesla's shot — but reading base is what puts the whole chain behind one
multiplier and matches the rule splash follows, so a mechanic bonus can never propagate through a
secondary effect.

**The find: the Foundry's shell was still reading authored damage directly.** It resolves in
`ResolveLandedShells`, a different phase where the shot's `baseDamage` local is out of scope, which is why
it survived both earlier passes. A 52-gold artillery piece would have been the one tower a GROVE-equivalent
FOUNDRY tier did nothing for.

All four paths now read one accessor, `BaseDamageFor(towerDefinition)` — primary shot, Pulse splash, Chain
Arc hops, Foundry shell. It is a pass-through today and exists purely so the tier multiplier is a one-line
change against a single function. Six new ratio tests pin the shapes, so a future path that bypasses the
accessor fails instead of silently sitting at tier 1.

168 tests passing, 2 skipped against the stalemate.


## 2026-07-29 (re-measured): Every Balance Number, Against A Real Maze

The harnesses all built their own route as a straight line, which was wrong twice over: the real map is
7x16 with spawn (3,0) and exit (3,15), so a straight route is 16 cells and not the 18 they used — and this
game is about mazing, where a real route runs far longer. `MazedLane` now builds one with the real
`GridPathService` against the real map, adding cells one at a time and keeping only those that leave a path,
exactly as placement does in play. **Straight 16 cells, mazed 52 cells, 36 maze cells.** The maze is
geometry only, so it does not shoot; that isolates the tower under test.

### Tower duel, re-measured

Exposure roughly doubled. Shots landed on a Brute went from 1–3 to 1–5, damage-per-gold from 0.07–0.43 to
0.14–0.64, and towers that can kill a Runner alone went from 5 of 15 to 7. Two towers can now kill a Brute
single-handed (Prism 11.50s, Elder Canopy 11.25s) where none could before.

**So the earlier "towers only get 1–3 shots per creep" finding was overstated, but not wrong.** Most towers
still cannot solo a Brute, which is fine for a layered defence. The creep-speed question is softened, not
answered — and it should be re-derived from these numbers rather than from the straight-lane ones.

### Two mechanics had their verdicts reversed

**Bramble Hold's nerf was calibrated on the wrong board.** Its zone was capped at 3 cells after it measured
as an automatic purchase — on a straight 16-cell lane, where 3 braked cells is a fifth of the whole walk. On
a 52-cell maze the same cap contributed **0%**: three slowed cells out of fifty-two is noise. The zone now
follows the tower's real coverage again, with 3 as a minimum rather than a cap, and measures +24% against a
burst and +36% in a trickle. Covering what the tower actually reaches is also the more honest rule on a
maze, where a snaking route can pass one tower several times.

**Servicing's designed case flipped.** On a straight lane a stack crossed a slow tower's range inside a
single cooldown, so the cadence buff measured 0% against a burst and +25% in a trickle. On a maze the stack
lingers long enough for the extra shot to land: **+33% burst, +17% trickle**. The mechanic did not change;
the board it was judged on did. Its test asserted the old 0% explicitly and is now reversed.

### What held

| mechanic | metric | burst | trickle |
| --- | --- | ---: | ---: |
| Rot | own damage | +275% | +275% |
| Grovebond | own damage | +100% | +100% |
| Crowd Bloom | own damage | +75% | 0% |
| Chain Arc | own damage | +60% | +53% |
| Bramble Hold | lane damage | +24% | +36% |
| Servicing | neighbour damage | +33% | +17% |
| *Pulse splash (reference)* | own damage | +100% | 0% |

Foundry whiff rate stays 0% on the maze, so the lead filter and the worth-a-shell threshold both hold. Both
mandatory-buy checks still pass, so the Tesla and Repair Drone repricing was not an artefact of the straight
lane. Roster domination unchanged.

### Deliberately left on a straight lane

`TowerMechanicTests` asserts RULES — that the Barricade will not shoot behind itself, that Grovebond ignores
diagonals, that a shell lands where the event advertised. Those need a geometry a reader can hold in their
head, not a realistic one; converting them would add noise without adding truth. Only the harnesses that
produce balance MAGNITUDES were moved.

## 2026-07-29: Repair Drone's Servicing Buff Gets A Visual — The Servicing Tether

Closes the P2 finding "Repair Drone's range buff is invisible" in `GAMEPLAY_REVIEW_FINDINGS.md`,
which had gone stale: it was written against the mechanic's original +1 RANGE shape, three entries
before that same document replaced it with Servicing (adjacent towers fire one tick faster). A
range halo would now show the wrong thing, since Servicing does not touch range at all — the
finding's own "or a servicing tether" alternative was the one that still matched the shipped
mechanic.

A persistent tether now connects a serviced tower to whichever Repair Drone Spire is servicing it,
in the drone's own catalog colour (`TowerCatalog.cs` id 9, `(0.95, 0.82, 0.45)`) so it reads as
belonging to the drone rather than as a generic effect. Mirrors `CombatService.IsServicedByDrone`
client-side in `UnityVerticalSliceRenderer.UpdateTowerServicingTether` — the same tradeoff already
accepted for Grovebond's ring (`CountAdjacentGroveTowers`): duplicating the adjacency rule risks
drift from the simulation, but the tether being its only consumer keeps that honest, and the
alternative is a snapshot field that exists only to be drawn.

**Verified two ways, not one.** Reflected directly into the renderer's private
`towerServicingTethers` dictionary in a throwaway probe (removed after use) to confirm the object
count, position and scale independent of reading pixels — a tether between grid (1,8) and (2,8)
landed exactly at their midpoint with the expected 1-unit length, before any screenshot was taken.
Then captured both a close-up and the real in-game `ActiveLane` camera distance to check it
actually reads, not just exists.

**That capture caught a real miss, the same one Grovebond's own comment already warns about**
("Floors were originally 0.55 scale / 0.16 alpha, which at the common bonus of 1 was invisible
under the tower mesh"). The first-guess tether (0.05 thick, height 0.18, alpha 0.62) was
essentially invisible at the real gameplay camera distance, lost against both towers' own
range-halo spheres. Thickness, height and alpha were raised (0.11 / 0.34 / 0.85) to clear the
halo rather than cut through its middle, which is a measurable improvement over the first guess.
A further push to 0.48 height read WORSE, not better — it rose into a role-marker text layer that
renders on top of world geometry regardless of depth, so more height stopped helping past that
point and started hurting. Stayed at the value that improved on the first guess without competing
with that text.

**Known limitation, not fixed here:** at real gameplay zoom the tether is genuinely visible but
still modest, competing with existing on-screen elements (the towers' own range-halo spheres and
role-marker text) that were present before this change and are unrelated to it. Getting it to read
as clearly as Grovebond's ring likely needs the same kind of iteration Grovebond went through, or a
different visual language entirely (a pulsing glow rather than a static bar) — a follow-up for
someone with eyes on a live capture, not a batch log.

**Verification performed:** a full `LocalPlaytestBatchRunner` run across the current 15-tower
roster passes with a clean reset (326 peak creeps, 25 peak towers). No `dotnet test` changes —
this is a Unity presentation-layer change only, `src/LTW.Simulation` untouched.


## 2026-07-30 (correction): The Stalemate Was A Bug, Not Balance

A second-pass code review (`docs/OPEN_ITEMS.md` items 10 and 11) found by READING the simulation what four
sessions of measurement did not. The bot pressure check filtered `!IsDead` but not `!HasLeaked`:

```csharp
.Where(creep => creep.LaneId.Equals(myLane) && !creep.IsDead)
```

Creeps that finish a lane are not despawned — they transfer to the next opponent's lane as a new entity,
and the spent entity stays in `CombatState` tagged with the lane it exited, health intact. So this filter
counted every creep that had ever finished walking the lane. `incomingHealth` grew monotonically for the
whole match, crossed `PressureThreshold`, and the bot stopped sending **permanently**. Every other creep
filter in the codebase already excluded `HasLeaked` — eight sites in `CombatService` plus
`GetCreepSnapshots` — which is why nothing looked wrong on screen. This was the only consumer that saw them.

**Adding one condition makes the same seed complete at tick 926 instead of running past 80,000 ticks.**

What this corrects, and it is a lot:

- **The P1 "two mazing bots stalemate" finding was misdiagnosed.** I attributed it to defence out-scaling
  attack and designed an entire upgrade-tier system as the closing mechanism. The game closes fine; the
  attackers had stopped attacking.
- **The bot sitting on 3,700 gold** was blamed on the placement ceiling alone. The ceiling was real, but
  this was the other half, and fixing the ceiling did not fix this.
- **The creep-speed experiment needs redoing.** Its conclusion — that 4x slower makes defence overwhelming
  and matches unendable — was measured with bots that had stopped sending.
- **`BotMazingTests`' 2x route bar was measuring the bug.** With sends working, bots split gold between
  towers and creeps and reach 24 cells rather than 40. Rebased to 1.4x.
- **Two scenario expectations were rebased**, both for the same reason: bots that cannot send dump all gold
  into towers, so tower counts in a fixed window were inflated. `Mixed_pressure`'s Greedy bot now builds 1
  tower rather than 2, which is correct behaviour for a profile defined as prioritising sends.
- **Matches now complete in 245–926 ticks**, which may be too FAST. The opposite of the problem we thought
  we had, and the next thing to look at.

Also landed from the same review: `BotPlacementCandidates` deleted (dead since the mazing rework, item 6),
and my own `BestMazingPlacement` comment corrected from "7x18" to the actual 7x16 map (item 17).

**The transferable lesson is about method.** Four sessions of increasingly careful measurement — duel
harnesses, contribution harnesses, whiff rates, opportunity cost — all produced real findings and none of
them found this, because every one measured OUTPUTS while the bug was in an INPUT they all shared. A review
that reads the code found it in one pass. The review's own note says it best: treat "a review found nothing"
as a claim about the review, not the code.

174 tests passing, zero skipped.

## 2026-07-30: Two More Balance-Relevant Fixes From The Same Review Pass

Continuing through `docs/OPEN_ITEMS.md`'s remaining items, two more turned out to change actual match
economics rather than being cosmetic:

- **Item 15 — bots could only ever build 4 of the 15 towers** (`BotTowerForSlot`'s switch fell through to
  a single repeated tower once past its first few slots). Every mechanic-contribution measurement taken
  against a bot opponent in this log was therefore measured against a bot that could never build the
  tower being measured, and `BotMazingTests`' "keeps building past nine towers" check passed by watching a
  bot repeat one tower forever. Fixed with a per-profile build-order array cycled by
  `ownedTowerCount % array.Length`; the three profiles' arrays collectively reach all 15. Deliberately kept
  each array's opening slots identical to the old switch's early cases (same tower, same cost) rather than
  reordering from scratch, because income only ticks every 50 simulation ticks and even a few gold of
  difference in an early slot can shift a bot's opening send/build timing by a whole income cycle —
  confirmed the hard way when a first attempt reordered from slot 0 and broke three tests purely on timing.
- **Item 20 — Thorn Snare's bramble zone collapsed a tower's first-and-last covered route index into one
  contiguous span**, which is only correct when coverage is one unbroken run. On a mazed route a tower can
  be passed twice with an unreached stretch in between, and the old code braked that stretch anyway. Fixed
  by segmenting into one span per contiguous covered run (`BrambleZonesFor`, plural) instead of one span
  total. This makes Thorn Snare weaker than previously measured on any mazed lane with a double-visit
  tower — it was over-braking before.

Both required rebasing a test that measures match-level outcomes rather than the mechanic directly:
`BotMazingTests`' mazing-window test needed more ticks (1200 → 1600) since bots now spend some early gold
on a costlier tower on the way to the rest of the roster; `Two_bots_complete_a_local_carousel_match`'s
completion window widened again (150-2000 → 150-3500, completes at 2911) since Thorn Snare's real strength
dropped. Neither is a new stalemate — both matches still complete comfortably inside their tests' outer
safety nets. Every mechanic-contribution number in this log that involved a bot-built Thorn Snare on a
mazed lane, or that relied on the old 4-tower bot roster, should be treated the same way item 5's mazing
fix and item 10/11's stalemate fix were: re-measured, not adjusted.

176 tests passing, zero skipped.

## 2026-07-30: Creep Pace Cut To A Third, And What That Did To Three Mechanics

Playtest note: "all creeps move too fast." Measured, it was worse than it sounded. `SpeedPerSecond`
is cells per TICK at 4 ticks/second on a board where one cell is one world unit, so:

| creep | cells/sec | crosses a straight 16-cell lane in |
| --- | ---: | ---: |
| Brute, Colossus, Runner, Warden (speed 1) | 4 | **4.0s** |
| Shade, Swarm, Stalker (speed 2) | 8 | 2.0s |
| Wisp, Zephyr (speed 3) | 12 | **1.33s** |

The *slowest* creep in the roster crossed an undefended lane in four seconds. This is the same
number `rig_turret_walker.py` ran into from the art side on 2026-07-28 — 8 world units/sec is about
six body lengths per second, which no legged gait can read as anything but skating. Two independent
routes to the same finding.

**Fix: `CombatService.BaseMovementCost`, 1 → 3.** A creep banks movement each tick and steps one
cell when it reaches the cost, so this divides every creep's speed by three while preserving the
roster's relative pacing exactly. It was chosen over editing 15 `SpeedPerSecond` values because
those are small integers and cannot express anything slower than one cell per tick. Thorn Snare's
brake is now defined as `BaseMovementCost * 2` rather than a bare 2, so retuning pace cannot
silently change what the brake is worth.

Measured at three candidate values before choosing:

| cost | Runner crosses | Wisp crosses | match length |
| ---: | ---: | ---: | ---: |
| 1 (was) | 3.50s | 1.00s | 12.1 min |
| 2 | 7.25s | 2.25s | 13.9 min |
| **3 (chosen)** | **11.0s** | **3.5s** | **14.7 min** |

At 3 the FASTEST creep now takes about as long as the slowest one used to, which is what "all
creeps" being too fast asks for.

### Three consequences, none of which were the pacing itself

**1. Movement had to stop being cell-quantised on screen.** A creep now holds a cell for three ticks
and then jumps, and the renderer could not smooth it because the snapshot only carried the current
cell. `CreepPresentationSnapshot` gained `NextPosition`, `MovementProgress` and `MovementCost`, and
the renderer interpolates between the two cells. Exposed as two integers rather than a ready-made
fraction because the simulation is deliberately all-integer and `ArchitectureBoundaryTests` guards
that; the division happens client-side. Without this the change would have traded speed for stutter.

**2. Mechanics that buy MARGINAL SHOTS were diluted; mechanics that buy damage per shot were not.**
This is the general shape and worth remembering before reading any contribution number taken at a
different pace. A tower already gets ~3x as many shots at the same creep, so one more is a smaller
share of a bigger total:

| mechanic | contribution before | after |
| --- | ---: | ---: |
| Servicing (neighbour fires 1 tick sooner) | 33% | **14%** |
| Bramble Hold, trickle case | never worse | **-4%** (87 vs 91) |

Bramble Hold's small trickle regression is explainable rather than noise: at this pace the tower
kills the braked creep and then stands idle waiting for the next one to walk in, so the lane runs
dry inside the measurement window. Its burst case is unaffected, which is the case it exists for.

**3. A real balance problem surfaced: the Foundry Core beside a Gatling now wastes almost every
shell.** The mortar commits a shell three ticks before impact, so anything that kills the target in
those three ticks wastes it — and creeps now spend three times as long under a supporting tower.
Measured whiff rates with support:

| creep | rows 0.20 / 0.45 | rows 0.70 / 0.90 |
| --- | ---: | ---: |
| `creep.swarm` (5 hp) | **100%** | 22% |
| `creep.wisp` (4 hp) | **95%** | 92% |
| `creep.runner` (10 hp) | 11% | 0% |

Crucially this is **not** a lead-arithmetic bug: unsupported, the mortar still whiffs 0% across
every creep and every row, so its prediction is exact. `A_launched_shell_always_lands_on_something`
was scoped to the unsupported case, which is the property it was really guarding, and the supported
case became its own reporting test rather than being folded in and hidden. It wants a design answer
— re-target on landing, a shorter flight, or accept it as an anti-heavy tower and price it there.
Tracked on GD-10.

**Not verified:** how the new pace actually feels in a played match. The numbers say the fastest
creep now moves like the slowest used to; whether that is right is a judgement only playing it makes.

## 2026-07-30: Category Upgrade Tiers — Shipped, And What Measuring Them Changed

Six independent upgrade tracks (three tower lines, three send categories), tiers 1-3, tier 1 free.
A tower tier scales that line's damage at shot time; a send tier scales creep health at spawn. The
design in `docs/CATEGORY_UPGRADE_TIERS_PLAN.md` shipped structurally intact. Four of its specific
conclusions did not survive measurement.

**The designed multipliers made the game unable to end.** At the documented 140% / 190% tower
damage, two bot defences ran past 6000 ticks with neither able to break the other — the exact
"re-create the stalemate at a higher number" outcome that document warned against, reproduced on
the first run. Isolating the sides settled which one causes it:

| Configuration | Result |
| --- | --- |
| No tiers at all (baseline) | completes, tick 3627 |
| Creep tiers only, tower tiers inert | completes, tick 3337 |
| Tower tiers only, creep tiers inert | **stalemate past 6000** |

Creep scaling shortens matches, tower scaling lengthens them, and a sweep put the cliff between
130% and 140%. Shipped at 115/130.

**Tower tiers were priced backwards.** The design priced them BELOW send tiers (100/260 against
120/300) on the reasoning that a tower tier is worth less — then, two paragraphs later, explained
why the opposite is true: a tower tier multiplies every tower in that line forever, while a send
tier only helps creeps bought after it. The cheaper option was the stronger one. Shipped at
140/360 against 120/300, so the dearer purchase is the one that compounds.

**The multipliers turned out not to govern pacing at all — the bots' preference does.** The design's
bot rule was "a tower tier while its lane is under pressure, a send tier otherwise". It reads
sensibly and measures terribly: bots are under pressure most of the time, so they bought almost
only defence, and could not finish a match at ANY tower multiplier — still stalemating with tower
scaling cut to 112%. Driving the choice from each bot profile's existing Aggression/DefenseBias
instead lets the full 115/130 stand:

| Bot tier preference | Tower scaling | Result |
| --- | --- | --- |
| Tower tier when pressured (as designed) | 115/130 | stalemate |
| Tower tier when pressured | 112 (cut) | stalemate |
| Profile-driven (shipped) | 115/130 | **tick 3249** |

3249 is faster than the 3627 the game takes with no tiers at all, so the feature now shortens
matches slightly rather than lengthening them. Verified completing at 2, 3, 4, 6 and 8 lanes
(414 / 3249 / 2545 / 1986 / 1985 ticks).

**Rounding decided whether the feature did anything.** Damage is a small integer and the multiplier
is capped, so `2 * 130 / 100` truncates back to 2 — meaning a tier purchase was worth literally
nothing to the five 2-damage towers (Arrow, Control, Relay, Gatling, Sapling). Measured all three
rules across the roster:

| Rule | Towers improved at tier 2 | Better at tier 3 than tier 1 |
| --- | ---: | ---: |
| Truncate | 3 of 15 | 9 of 15 |
| Ceiling | 15 of 15 | 15 of 15 |
| **Nearest (shipped)** | **9 of 15** | **15 of 15** |

Ceiling improved everything by handing every cheap tower a flat +50% regardless of the percent,
which was itself enough to re-trigger the stalemate. Nearest ships. A 2-damage tower still gains
nothing at tier 2 and +1 at tier 3 — the floor of what integers express at this multiplier, and
raising the multiplier is exactly what is not available.

**Two defects the tests caught, unrelated to balance.** Creep health bars would have rendered wrong:
`CreepCombatState` never stored a max health, so the presentation snapshot reported the AUTHORED
value while Health carried the scaled one, drawing a tier-3 creep as a 225%-full bar. Max health is
now per-creep, fixed at spawn and carried through damage and lane transfers. And the build palette's
panel was hard-fixed at 282px while the send dock's grew to 374 — a pre-existing mismatch that
silently squashed its category cards; both now grow to 424 for the tier row.

**Bots maze slightly less**, and that is the measured cost of the feature: they have a third thing
to spend on, so lane 2 settles at 22 cells instead of 24. Disabling bot tier buying alone restores
exactly 24. `BotMazingTests`' bar moved 1.4x to 1.35x for that reason. A gate requiring bots to keep
a tower's worth of gold before upgrading was tried to win those cells back — it did not, and cost
1650 ticks of match length by starving the creep tiers that close a game out, so it was dropped.

**Verification:** 205 tests passing, including 24 new ones covering the rules, all four damage paths
(primary shot, Pulse splash, Chain Arc hops, Foundry shell), the not-retroactive property, and bot
purchasing. Batch playtest passes with a clean reset. Both category pickers captured through
`RealUiCaptureRunner` (the only capture path that sees IMGUI) and reviewed —
`docs/screenshot-reviews/category-upgrade-tiers/`. That review caught a live layout defect: the
"5 SENDS" line and the new "TIER n" line rendered on top of each other, because the card's label
positions were proportions tuned for the shorter card.

**Not verified:** how any of this feels to a human. Every number here comes from bot-versus-bot
runs, which is what makes them reproducible and also what makes them a poor guide to whether
spending 360 gold on a tower line is a satisfying decision to make.
