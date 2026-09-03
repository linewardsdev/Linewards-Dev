# Fewer, Stronger Creeps — measured starting point

Written 2026-08-09. **The experiment below was run and then reverted**; nothing here is in the
codebase. It exists because the numbers are the expensive part and they should not have to be
re-measured.

## The problem this attacks

Peak CONCURRENT creeps, not match length. An M4 iPad with 8 GB slows down late in a match, and peak
entity count is what the device has to draw. Match length matters only because peak tracks it.

Escalation cannot fix this and makes it worse: it shortens a match by raising creep HEALTH, so
creeps survive longer and more are alive at once. Measured on the shipped 1000/40 curve, mean peak
is 382; on a steeper 900/50 that closes matches faster still, mean peak rises to 470. Every
escalation setting trades entity count against duration in the wrong direction.

Raising creep cost and health together is the only lever measured so far that moves them the same
way, because the same gold buys fewer bodies carrying the same threat.

## What was measured

Six matches — 3 and 8 lanes, seeds 1-3. Baseline is commit `e266542` (lives 100, escalation
1000/40, leak 1:1). "2x creeps" doubles every creep's `Gold` cost and `maxHealth` in
`SampleVerticalSliceContent`, 15 definitions, towers untouched.

| config | mean ticks | max ticks | mean peak creeps | max peak creeps |
| --- | --- | --- | --- | --- |
| baseline (lives 100) | **3655** | 4705 | 382 | 899 |
| 2x creeps, lives 100 | 5404 | 6436 | 258 | 416 |
| 2x creeps, lives 60 | 4823 | 6039 | 234 | 446 |
| 2x creeps, lives 50 | 4498 | 5546 | 180 | 312 |
| 2x creeps, lives 40 | 4420 | 5835 | **158** | **329** |

**The headline: 2x creeps with lives at 40 cuts mean peak entities 382 -> 158, a 59% reduction, and
max peak 899 -> 329, a 63% reduction.** That is the frame-cost win, and it is large.

**The cost: matches get longer, not shorter.** 3655 -> 4420 mean at lives 40, and every 2x row has a
max above the 5000-tick pacing target `LocalThreePlayerMatchTests` defends. Halving the number of
creeps halves the leaks, and leaks are 1:1 with lives, so the match takes longer to resolve. Lives
compensate only partially — 100 to 40 recovers about a thousand ticks of the 1750 that doubling
cost adds.

## Why it was reverted rather than shipped

Every 2x row breaks the 5000-tick bound on at least one seed, and that bound is a pacing target
rather than a safety net — its own comment says so, and says raising it instead of tuning was
"the wrong move" last time. Shipping red, or loosening the bound to fit, would both be worse than
leaving the measurement written down.

## The combination works — measured 2026-08-09, still not shipped

Escalation on top of 2x creeps was the untried combination, and it resolves the tension. With 2x
creeps and lives 40, sweeping the escalation curve:

| escalation | mean ticks | max ticks | mean peak | max peak |
| --- | --- | --- | --- | --- |
| baseline (1x creeps, lives 100, 1000/40) | 3655 | 4705 | 382 | 899 |
| 1000/60 | 4052 | 5025 | 238 | 724 |
| 800/70 | 3859 | 5031 | 151 | 340 |
| 700/90 | 3535 | 4753 | 193 | 553 |
| 750/80 | 3966 | **4476** | 204 | 470 |
| **700/80** | **3693** | 4814 | **212** | **430** |
| 650/90 | 3440 | 4726 | 238 | 651 |

**700/80 is the pick.** Match length is unchanged against baseline — 3693 against 3655, inside
noise — while max peak concurrent creeps more than halves, 899 -> 430, and mean peak falls 45%.
Both axes move the right way at once, which no single lever managed.

Why it works: escalation buys duration by making creeps tougher, which costs entity count. With
half as many creeps alive the entity price of a steeper curve is roughly halved, so the curve can
be pushed much further before peak starts climbing again. 800/70 reaches peak 151/340 and misses
the 5000-tick bound by 31 ticks; 650/90 is faster still but peak climbs back to 651, which is the
curve overrunning its own budget.

**What stopped it shipping: 22 failing tests.** Not a defect — a balance shift this size moves
numbers pinned all over the suite (bot tier timings, all three GameplayScenario baselines, the
eight-lane carousel, the escalation table itself). Each wants deciding on its merits, the way
`GameplayScenarioTests` thresholds were restated at the same LOSS rather than relaxed when lives
changed. That is a session's work and doing it badly would bury a real regression in a batch of
"expected" updates.

**To land it:** set creep cost and maxHealth to 2x in `SampleVerticalSliceContent` (15
definitions), `StartingLives` to 40, `MatchEscalationRules` to StartTick 700 / PercentPerInterval
80, then work the 22 failures one at a time asking of each whether the assertion is about a number
that moved or a property that broke.

## Landed 2026-08-19 — 329 of 331 green

The three edits above are in. Working the failures one at a time (23 on this branch, not 22 —
category tiers had landed in between) turned up **two real defects and one open question**, which is
the whole reason for doing them individually.

### Defect 1: Rot silently doubled

`CombatService.RotHealthPerDamageMultiple` reads ABSOLUTE authored max health, so doubling the
roster doubled Spore Cloud's damage against exactly the fat targets it exists to answer — a buff
nobody asked for, arriving as a side effect of repricing creeps. Doubled the divisor 24 -> 48 with
the health. All six `TowerMechanicTests` Rot cases then passed **with no test edits at all**, because
they assert RATIOS; that they went green untouched is the evidence the call was right.

### Defect 2: bots stop attacking forever

The serious one. `BotController.TakeTurn` gives sending first claim on a tick's gold, which is only
enough while the send is affordable on the tick it is wanted. When a creep costs more than the
surplus one income payout brings, `TryBuild` (which has no cap) spends the difference on another
tower every tick and the balance never reaches the creep's price. Income only rises by sending, so
the bot cannot grow out of it — a closed loop.

Measured on the heavy scenario: two Greedy bots built **44 towers and sent nothing after tick 60**,
zero creeps on the board from tick 240 out to tick 900, income frozen at 14 and 16, match never
ending. The reprice exposed this rather than caused it — at the old prices the cheapest wall
happened to sit under one payout's surplus.

Fixed with `SendSavingsFor`: a bot that has gone `SendStarvationTicks` (50, one income payout)
without sending holds back the price of its cheapest wall from tower spending. Deliberately inert
otherwise — a first attempt that saved whenever a send was unaffordable made sends strictly dominate
and produced 58 sends with **zero** towers, as broken as the starvation it replaced. The window was
swept: by tick 900, 17 sends / 25 towers at 50, 11 / 35 at 100, 8 / 40 at 150.

### Defect 3: bots could never save for a category tier

Found by sweeping tier prices, which is how it became clear price was not the problem. `TakeTurn`
ran `TryBuild` before `TryBuyCategoryTier`, and `TryBuild` has no cap — it buys another tower every
tick the gold allows, so a bot's balance never climbs to a tier's price. Exactly the same shape as
defect 2, in a different place.

The proof it was structural rather than economic: on three lanes, seed 1, P3 bought no tier for a
whole match, yet probing `BuyCategoryTier` directly at any point showed it would have been
**accepted** — the bot held 122 gold against a tier it could afford. Dropping tier prices as far as
30% of list changed nothing at all, because the bot was never allowed to hold the money either way.

Fixed by moving `TryBuyCategoryTier` ahead of `TryBuild`, the same precedence sends were given on
2026-07-28 and for the same reason. Measured over six matches (3 and 8 lanes, seeds 1-3):

| | mean ticks | max peak creeps | mean towers upgraded | 3-lane seed 1 |
| --- | --- | --- | --- | --- |
| before | 3711 | 396 | 23 | tier never bought, 0 upgraded |
| after | 3776 | **348** | **32** | tier at 2900, 46 upgraded |

Match length moves 1.8%, against this project's 20% bar; peak creeps IMPROVES.

### Tier prices: swept, and left alone

Prices were swept at 100 / 85 / 70 / 60 / 50 percent of list, before and after the fix above.

**Correcting an earlier reading in this document's history:** a first pass concluded from three
lanes, seed 1 alone that the tier feature was "effectively dead" at these prices. That was too
strong and drawn from one match. Across six, tiers are bought in **6 of 6 runs even at full price**
(mean tick 2893, mean 23 towers upgraded). Seed 1 at three lanes was the outlier, and its cause was
defect 3 rather than price.

With defect 3 fixed, full list price is also the best of the five points measured: it produced the
most towers upgraded (32) and the lowest peak creeps (348) of any price tried. Lowering prices moved
the first purchase a few hundred ticks earlier while making peak creeps and match length worse and
noisier — 60% reached 4010 mean ticks and 437 peak. **So tier prices are unchanged.** The right
answer to "find the cheapest revival that keeps pacing" turned out to be that price was not the
lever holding the feature down.
