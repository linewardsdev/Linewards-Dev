# GD-04 Tuning Log

## Purpose

This log records the first gameplay pacing targets for the local vertical slice. It is intentionally lightweight: the goal is to make early/mid/closing pressure measurable before mobile-device validation resumes.

## Current Baseline

- Three lanes: one human lane and two bot lanes.
- Starting economy: 100 gold, 10 income, 220 lives.
- Income interval: 50 simulation ticks.
- Global send cooldown: 30 simulation ticks.
- Sell refund: 50% of tower cost.
- Leak life loss: 1 life per leaked creep.
- Starter towers: Arrow Tower, Slow Control Ward, Economy Relay Ward.
- Starter sends: Runner, Brute, Swarm.

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
| Arrow Tower | Reliable single-target | Cheap baseline damage. | May become the only rational defense if control/relay are too weak. |
| Slow Control Ward | Area/control placeholder | Lower damage, slower cadence, visually wider role. | Needs real slow/control behavior before final balance. |
| Economy Relay Ward | Utility/economy placeholder | Expensive support-looking tower with minimal damage. | Needs a gameplay payoff or disabled-state framing. |
| Runner | Basic speed pressure | Cheap opener with modest income. | Could feel bland without speed/readability tuning. |
| Brute | Health pressure | More health and income, higher cost. | Could be too efficient if tower damage is low. |
| Swarm | Volume pressure | Cheap fast group send. | Can clutter the board if quantity and speed are too high. |

## Known Balance Questions

- Does the global 30-tick send cooldown create enough breathing room once bots and humans send together?
- Does 220 lives give enough room for defense corrections without making local matches drag?
- Should Economy Relay Ward remain placeable before it has an economy/support effect?
- Should Swarm quantity stay at 3, or should the unit be cheaper with a lower income reward?
- Are kill bounties large enough to make defense feel rewarding without defeating send-for-income pressure?

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

Latest objective read: the first recorded Play Mode run ended at tick 476, well before the target match-completion range. Before changing presentation again, prioritize a tuning pass that slows player-driven bot collapse without removing early offensive feedback.
