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

## What to try next, in order

1. ~~**Steepen escalation again on top of 2x creeps.**~~ **Done — see above.** The two levers were only ever measured
   separately. Escalation raises health, but with half as many creeps alive the entity cost of a
   steeper curve is roughly half what it was — this is the one combination most likely to land
   under 5000 while keeping peak low, and it has not been tried.
2. **A uniform 2x is a blunt instrument.** It was chosen to isolate the variable, not because every
   creep should double. Swarm (cost 6, health 5) is the roster's chaff and is the single largest
   contributor to entity count; a steeper multiplier on the cheap end and a shallower one on
   Colossus (52/78) would cut peak harder for less length. Per-creep numbers want authoring, not
   scaling.
3. **The fourth tier** raised in the same conversation is untested and orthogonal — it changes what
   a seat can reach late, where this changes what a send costs throughout.

## How to measure

The harness is ~30 lines and was deleted with the experiment: run 3- and 8-lane matches on seeds
1-3 to `MatchSummary`, sampling `GetSnapshot().Creeps.Count` every tick for the peak. **Report peak
creeps alongside ticks — a change that shortens matches while raising peak is a regression for the
device even though the duration number improves.** That is the trap this whole line of work exists
to avoid.
