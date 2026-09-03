# Launch Roadmap

> **This file is the source.** [`launch-roadmap.html`](launch-roadmap.html) — the Gantt
> view — is generated from it by `tools/docs/render_roadmap.py`. Edit here, then run that
> script and commit both; never edit the HTML. CI's `--check` fails on a stale page, and
> generation fails if the timeline block below stops matching the week bullets.

**Drafted 2026-07-31. Updated 2026-08-04** (audio closed; shell screens, identity and
balance re-checked). Proposal for review — dates and scope are not committed to.

---

## First, the date

**Where this stands, 6 August.** Four of the ten P0 gaps have moved since the 31 July
draft: audio is closed, Graphics Wave 1's AO half is done roster-wide with LODs wired,
app identity is most of the way there, and a title/pause/results shell now exists.
**The binding constraint is now external, not technical** — neither store account is
enrolled, which blocks the bundle identifier, TestFlight and the Play track alike. **The one that has not moved is the one that matters most** — the game
still has never run on a phone, and roughly twenty acceptance boxes still need a human to
play it. Every balance number below is bot-versus-bot. The 31 August target still assumes
both store enrolments start immediately; Apple alone carries a 24–48h approval lead.

The roadmap was drafted on **Friday 31 July 2026**, built to **Sunday 31 August 2026** —
four weeks.

Four weeks is aggressive but not unreasonable. The project has produced **779 commits in
20 days** (first commit 2026-07-11), recently running 25–63 commits/day. The risk is not
throughput; it is that the remaining work is a *different kind* of work from the last three
weeks — device, store, audio, identity and human judgement, rather than simulation and
rendering.

## Define "launch", because it changes the date by a month

| Tier | Means | Realistic date |
| --- | --- | --- |
| **A. Playable on your own phone** | Sideloaded via free signing, no store | **~1 week** — nothing external blocks it |
| **B. Soft launch** | TestFlight + Play internal track, invited testers, no public listing | **End of August** — this roadmap's target |
| **C. Public store launch** | Live on the App Store and Google Play | **Mid-to-late September** — needs review cycles, store assets, a privacy policy and a first round of real-user fixes |

**Recommendation: target B for 31 August, and treat C as a September milestone.** Trying to
hit C by August compresses store review and the first real-user feedback into the same
week, which is where launches go wrong. Shipping free (as decided) makes B genuinely useful
on its own — it produces real players without needing any of the monetization work.

---

## What is actually left

Verified against the working tree on 2026-07-31. Grouped by whether it blocks a soft
launch.

### P0 — cannot soft-launch without these

| # | Gap | Evidence | Est. |
| --- | --- | --- | --- |
| 1 | **Never run on a phone** | Both device-validation docs are empty templates, zero runs | 1–2 d |
| 2 | **App identity — mostly done; bundle id BLOCKED on store enrolment** — *re-checked 2026-08-06* | Company is `Line Wards Games`, product `Line Wards`, and a 1024px icon is set. `applicationIdentifier` still reads `com.ltwplaceholder.ltw` on both platforms. **Blocked, not forgotten:** neither developer account is enrolled yet (owner, 2026-08-06), and the identifier binds permanently to whichever account first uploads under it, so setting it before enrolment risks binding the wrong string. Proposed value once enrolment happens: `com.linewardsgames.linewards` | <1 d, after enrolment |
| 3 | **Audio is seven sine beeps** — *closed 2026-08-04* | Was: 7 procedural tones, zero asset files. Now: 33 SFX takes + a three-stem adaptive score (bed/tension/combat, mixed by live lane pressure), all regenerable from `tools/audio/synthesize_game_audio.py` — per-family baked reverb, one D-minor key, Karplus-Strong and modal instruments, variant pools, camera-relative pan, a splash boom synced to the mortar's crater, and an `LTWAudioDirector` with rate limiting, ducking and sample-locked stems. **Auditioned by the owner 2026-08-04: "they all sound good."** See `AUDIO_DIRECTION.md`. A licensed pass remains optional polish — a file-for-file swap, no longer scheduled work | ~~4–6 d~~ done |
| 4 | **Shell screens exist; still not a scene** — *re-checked 2026-08-04* | Title, pause and results now render through UI Toolkit — `ShellScreens.uxml/.uss`, `ShellScreenView.cs`, a generated `PanelSettings`, and the brand mark on the title. Captured and verified. **But `Assets/Scenes/` still holds only `LocalVerticalSlice.unity`**: the shell is a panel over the match, not a scene, so the app still boots straight into a running board. Whether that matters for Tier B is a call worth making deliberately rather than by default | 1–2 d |
| 5 | **No crash reporting or analytics** | Zero references anywhere | 1 d |
| 6 | **Store accounts not enrolled** | Apple Developer Program $99/yr, 24–48h approval; Google Play Console $25 one-time | External |
| 7 | **No store listing assets** | No icon, screenshots, description, age rating, or privacy-policy URL | 2–3 d |
| 8 | **~20 acceptance boxes need a human to play** | GD-01→10 unchecked; all balance is bot-vs-bot | 2–3 d |
| 9 | **Graphics Wave 1 — AO done, LODs wired, normal maps deferred by decision** — *worked 2026-08-04/06* | **AO bound on all 30 roles** (was 2 of 119); evidence in `screenshot-reviews/stylized-shader-20260801/after_ao_bound_roster.png`. **LODs wired on all 30 prefabs** — every role decimated to 50%/25% (`tools/art/make_all_lods.py`) and given a LODGroup (`AuthorLodGroups`), closing open item 15's last sub-item; before this the two proof meshes were referenced by nothing. **Normal maps have a route and a verdict:** baking LOD0 onto LOD2 would work, but a high-to-low map is only correct on the low mesh and all three levels share one material per role, so it needs ~60 new materials and more SRP-batcher breaks to buy detail that is close to invisible at the 46–105px units occupy. **Owner decided 2026-08-06** to bank the LOD win and revisit only if LOD popping shows in play | ~~2–4 d~~ AO + LODs done · normals deferred |
| 10 | **Onboarding built, device-unverified** — *worked 2026-09-02/03* | How to Play rewritten as a five-card shell screen; first-run offer; PRACTICE mode with passive bots and a five-step coach strip (skippable, re-enterable). Captured at phone and iPad widths; see OPEN_ITEMS item 54 | ~~2–3 d~~ done |

### P1 — should land before public launch (tier C), not blocking soft launch

- **Bot quality is product quality** — bots are the shipped opponent. The specific claim
  this entry used to make ("bots reach only 5 of 15 towers", citing `BotTowerForSlot`) is
  **retired: it was measured false and the method it named no longer exists.** A seed-1
  8-lane match places all 15 tower types, from 120 Arrows down to 6 Barricades, with none at
  zero. What actually remains, per open item R3: bots never sell, `Decide` sees only an
  economy record rather than the board, and there is no randomness anywhere in bot
  decisions — so a human meets the identical opponent every single match.
- **No LODs**, ~15k tris × 30 units, CPU skinning. A performance problem before a visual one.
- **Settings persistence** — some `PlayerPrefs` use exists; needs an audit.
- **Two capture states land nothing in frame**, so a blocking readability category cannot
  currently be reviewed.

### Explicitly out of scope for this roadmap

Monetization (shipping free first, per decision), multiplayer, the match server, accounts
and entitlements, Waves 2–3 of the graphics uplift beyond what tier B needs.

**The roster expansion was never planned and is not committed.** It began as a spur-of-the-
moment idea rather than a roadmap item, which is why it appears in no P0 or P1 row and no
week below. `ROSTER_EXPANSION_PLAN.md` sketches 18 units to reach 8 per category; one of
them, Twin Crescent Ward, was built end to end on 2026-08-08 and eleven more kitbash meshes
are staged and idle. Nothing obliges the rest to happen, and the launch path does not wait
on any of it.

It is noted here only because it left two marks on work that *is* on this roadmap: the tower
count is 16 rather than 15, and the Wave A line-skin SKUs in `MONETIZATION_EARLY_SKUS.md`
are priced per line, so Arcane's skins now cover six towers instead of five. Continuing or
parking the remaining units is an open call — the staged meshes cost nothing while they sit.

---

## The four weeks

Sequenced so that **externally-gated items start on day one** and everything with a review
or approval delay is off the critical path by week three.

<!-- gantt
  Timeline geometry for docs/launch-roadmap.html, rendered by tools/docs/render_roadmap.py.
  Lives here rather than in the HTML so this file stays the single source: the prose and the
  chart are edited together or not at all.

  Columns:  week | item | sub-label | left% | width% | badge
    week    1-4, or "ext" for the externally-gated rows above week 1
    item    must match a bold bullet title in that week (or a Dependency in the critical
            path table, for ext rows). The renderer FAILS if it does not — that assertion
            is the whole point of keeping this here, since a renamed bullet cannot then
            silently leave a stale bar behind.
    badge   text shown inside the bar; prefix with "done:" to draw it as complete

ext | Apple Developer Program | $99/yr · 24–48h approval | 0.5 | 14 | enrol → active
ext | Google Play Console | $25 · identity verification | 0.5 | 14 | enrol → active
1 | Fix app identity | name, company, icon done · bundle id still placeholder | 0.5 | 8 | done:✓ most + 9 | 4 | id
1 | Build to a physical iOS device | iOS then Android | 8 | 14 | 1–2d
1 | Play the game, with hands, and write notes | unblocks ~20 acceptance boxes | 18 | 7 | ★
1 | Act on what the play session finds | hold this time loosely | 22 | 12 | 2–3d
2 | Audio pass | 33 SFX + adaptive 3-stem score | 0.5 | 16 | done:✓ done Aug 3–4
2 | Graphics Wave 1 | AO + LODs on all 30 roles · normal maps deferred | 25 | 19 | done:✓ AO done Aug 6
2 | Title/menu scene | title, pause, results built · still a panel, not a scene | 25 | 12 | done:✓ done Aug 3 + 38 | 8 | scene?
2 | App icon and splash | icon, splash and brand mark landed | 42 | 11 | done:✓ done Aug 4
3 | Crash reporting and basic analytics | else feedback is anecdote | 50 | 9 | 1d
3 | ~~Onboarding / first-run teaching~~ built 2026-09-03 (OPEN_ITEMS 54) | teach mazing or players bounce | 53 | 14 | ~~2–3d~~ done
3 | Performance validation on device | frame rate + thermals, heavy send | 60 | 11 | 2d
3 | Store listing assets | screenshots, rating, privacy URL | 63 | 12 | 2–3d
3 | Bot roster fix | only if week 1 flagged opponent quality | 68 | 7 | if needed
4 | Upload to TestFlight and the Play internal track | 1–3d first review each | 75 | 11 | ext:submit → review
4 | Recruit 10–20 testers | anyone who isn't you | 79 | 10 | &nbsp;
4 | Triage and fix | no features scheduled here | 86 | 13.5 | reserved
4 | Decide on tier C | submit publicly in September? | 95.5 | 4 | decision
-->

### Week 1 (Aug 1–7) — Prove it is a game, on a phone

The theme is *stop guessing*. Two of these have external lead times and must start Monday.

- **Enrol in the Apple Developer Program and Google Play Console.** Day one. Everything in
  weeks 3–4 waits on these.
- **Fix app identity** — real bundle identifier, product name "Line Wards", company name,
  version scheme. Unblocks every build after it.
- **Build to a physical iOS device** via free signing, then Android.
- **Play the game, with hands, and write notes.** This is the highest-leverage hour in the
  whole roadmap — it unblocks ~20 acceptance boxes and is the only thing that can invalidate
  work before it compounds.
- **Act on what the play session finds.** Reserve real time here; do not schedule around the
  assumption that it will find nothing.

*Exit criteria:* the game runs on a phone, has a real identity, both store accounts are
pending or live, and there is a written human account of what it is like to play.

### Week 2 (Aug 8–14) — Make it look and sound shipped

- **Audio pass.** ~~The largest single unbudgeted item.~~ *Done ahead of schedule
  (2026-08-03/04): full synthesized cue set, adaptive three-stem score, director with rate
  limiting — auditioned and approved. See `AUDIO_DIRECTION.md`.* Remaining audio work is
  optional: dock tap ticks, and a licensed swap if synthesis ever stops being enough.
- **Graphics Wave 1** — *AO done 2026-08-06: baked and bound across all 30 roles, with LOD
  groups on every prefab.* Normal maps are deferred by decision (see P0 row 9) rather than
  outstanding: they need per-LOD materials to be correct, for detail that does not survive
  at unit size.
- **Title/menu scene** — a real scene, not an in-match overlay. Play, settings, quit.
- **App icon and splash**, from the branding guide.

*Exit criteria:* a stranger watching a 30-second clip would call it a game, not a prototype.

### Week 3 (Aug 15–21) — Make it survivable

- **Crash reporting and basic analytics.** Without this, soft-launch feedback is anecdote.
- **Onboarding / first-run teaching** (built 2026-09-03, OPEN_ITEMS item 54). Mazing is not obvious; a player who does not
  understand it will bounce and you will never know why.
- **Performance validation on device** — frame rate and thermals under a heavy send. First
  real data on whether LODs are needed before launch.
- **Store listing assets** — screenshots (now that Wave 1 has landed), description, age
  rating questionnaire, privacy policy hosted at a real URL.
- **Bot roster fix** if week 1's play session flagged opponent quality.

*Exit criteria:* a build you would put in a stranger's hands, with the means to learn what
happened.

### Week 4 (Aug 22–31) — Ship it

- **Upload to TestFlight and the Play internal track.** Both have review steps; budget
  2–3 days for the first submission of each.
- **Recruit 10–20 testers.** Friends, a subreddit, a Discord — the number matters less than
  that they are not you.
- **Triage and fix.** Reserve the last four days entirely for this. Do not schedule features
  into week 4.
- **Decide on tier C** — whether to submit for public release in September based on what
  testers say.

*Exit criteria:* real people who are not you have played it, and you know what they thought.

---

## Critical path and external dependencies

Everything with a delay you cannot compress:

| Dependency | Lead time | Start by |
| --- | --- | --- |
| Apple Developer Program enrolment | 24–48h, occasionally longer | **Aug 1** |
| Google Play Console enrolment | Usually same-day, identity verification can add days | **Aug 1** |
| TestFlight first build review | 1–3 days | Aug 22 |
| Play internal track first review | Usually hours, can be days | Aug 22 |
| Privacy policy hosted at a public URL | Needs a domain | Week 3 |

**Both enrolments on day one.** They cost $124 total and nothing else in weeks 3–4 can
proceed without them.

## The three risks worth naming

1. **Week 1's play session finds something structural.** This is the intended outcome — it
   is better to learn it in week 1 than week 4 — but it can consume days. Mitigate by
   holding week 2 lightly, not by skipping the session.
2. **Audio is underestimated.** *Resolved 2026-08-04 — this risk inverted: audio went from
   the least-tracked P0 item to closed (synthesis, not licensing), auditioned, with its own
   direction doc and regeneration pipeline. Kept here because the mitigation is worth
   remembering: the fix was making asset work reproducible code, not budgeting more days.*
3. **Store review rejects the first submission.** Common causes for a first-time game:
   missing privacy policy, incomplete age rating, placeholder metadata. Weeks 3–4 exist to
   surface these early.

## Landed since the draft — not on the critical path

### Board readability (3–4 Aug)

Five separate defects made the board look marked-up: a send beam drawn to an off-screen
lane, additive bursts accumulating past white into flat saturated slabs, opponents' send
beams crossfiring in the all-lanes view, an arrival cue that drew a square with an X
through it, and lane direction markers that read as a cross rather than an arrow. **Worth
knowing:** four of the five were independently authored as two beams crossing at a point,
which is the universal "missing asset" glyph. That shape keeps getting written because each
instance looks reasonable alone.

### A defeated seat could rebuild (4 Aug)

Bots kept taking turns after elimination, and the commands disagreed about whether to stop
them: upgrade, sell-batch and tier-buy each refused, while `PlaceTower` and `SellTowerAt`
did not — so a wiped lane came back. **Why it matters here:** this is exactly the class of
thing a first tester finds in the first ten minutes, and it was invisible to every existing
test because they all run two or three lanes rather than the eight that ship.

### Economy: tiers now cost, twice (3–4 Aug)

Category tiers escalate 25% per tier already held, and a tier now also raises what its own
units cost — 65% of the power increase, so a tier-3 creep carries 225% health for 181%
price. Previously the tier's own price was the entire lever and every unit after it was
free power. The income ceiling moved 600 → 900 because the gate is half the price and the
last upgrade had become unreachable rather than expensive. **Unverified:** all of it is
bot-versus-bot on one seed. Match length landed within 3% of where it started, but by two
changes pulling opposite ways rather than by design.

## What "done" means on 31 August

- Runs on iOS and Android phones, with a real name, icon and identity.
- Has music and sound effects.
- Has a title screen, teaches the player how to maze, and reports its own crashes.
- Assets carry normal maps and AO, and render through the intended post-processing.
- Is on TestFlight and the Play internal track, with real testers giving feedback.
- Is free, with no store screen and no payment code — monetization follows, per the
  cosmetic SKU roadmap.
