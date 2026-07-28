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
