# Launch Roadmap

**Drafted 2026-07-31.** Proposal for review — dates and scope are not committed to.

---

## First, the date

Today is **Friday 31 July 2026**. "Launch by the end of July" is today, so this roadmap is
built to **Sunday 31 August 2026** — four weeks. If the intended target really was July,
the honest answer is that it is not reachable: the game has never run on a phone, has no
app icon or bundle identifier, and neither store account is enrolled (Apple alone has a
24–48h approval lead).

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
| 2 | **App identity is entirely placeholder** | `applicationIdentifier` is **empty**; `productName: LTW.UnityClient`; `companyName: LTWPlaceholder`; no app icon; no splash. Brand is "Line Wards" and nothing uses it | 1 d |
| 3 | **Audio is seven sine beeps** | `CreateTone(...)` generates 7 procedural tones; **zero** audio asset files in the project. No music, no real SFX | 4–6 d |
| 4 | **No menu or title scene** | `Assets/Scenes/` contains only `LocalVerticalSlice.unity`; the app-shell is an in-match overlay | 3–4 d |
| 5 | **No crash reporting or analytics** | Zero references anywhere | 1 d |
| 6 | **Store accounts not enrolled** | Apple Developer Program $99/yr, 24–48h approval; Google Play Console $25 one-time | External |
| 7 | **No store listing assets** | No icon, screenshots, description, age rating, or privacy-policy URL | 2–3 d |
| 8 | **~20 acceptance boxes need a human to play** | GD-01→10 unchecked; all balance is bot-vs-bot | 2–3 d |
| 9 | **Graphics Wave 1 outstanding** | No normal maps, no bound AO on any asset | 3–5 d |
| 10 | **Onboarding is partial** | Some flow in `LocalSessionFlowOverlay`; no first-run teaching | 2–3 d |

### P1 — should land before public launch (tier C), not blocking soft launch

- **Bots reach only 5 of 15 towers** (`BotTowerForSlot`), and two profiles repeat one tower
  forever. Bots are the shipped opponent — bot quality *is* product quality here.
- **No LODs**, ~15k tris × 30 units, CPU skinning. A performance problem before a visual one.
- **Settings persistence** — some `PlayerPrefs` use exists; needs an audit.
- **Two capture states land nothing in frame**, so a blocking readability category cannot
  currently be reviewed.

### Explicitly out of scope for this roadmap

Monetization (shipping free first, per decision), multiplayer, the match server, accounts
and entitlements, Waves 2–3 of the graphics uplift beyond what tier B needs.

---

## The four weeks

Sequenced so that **externally-gated items start on day one** and everything with a review
or approval delay is off the critical path by week three.

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

- **Audio pass.** The largest single unbudgeted item. Needs: a music bed, and real SFX for
  build, hit, kill, send, income, leak, elimination, victory. The 7-tone scaffold means the
  *hooks already exist* — this is asset work, not systems work. Licensed library audio is
  the sane route at this stage.
- **Graphics Wave 1** — un-discard AO (a ~15-line script change recovering data already on
  disk), then normal maps. This is what makes the game stop looking like a prototype.
- **Title/menu scene** — a real scene, not an in-match overlay. Play, settings, quit.
- **App icon and splash**, from the branding guide.

*Exit criteria:* a stranger watching a 30-second clip would call it a game, not a prototype.

### Week 3 (Aug 15–21) — Make it survivable

- **Crash reporting and basic analytics.** Without this, soft-launch feedback is anecdote.
- **Onboarding / first-run teaching.** Mazing is not obvious; a player who does not
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
2. **Audio is underestimated.** It is the least-tracked P0 item, appears in no checklist,
   and is the one thing on this list with no existing scaffolding beyond the code hooks. If
   anything slips the date, it will probably be this.
3. **Store review rejects the first submission.** Common causes for a first-time game:
   missing privacy policy, incomplete age rating, placeholder metadata. Weeks 3–4 exist to
   surface these early.

## What "done" means on 31 August

- Runs on iOS and Android phones, with a real name, icon and identity.
- Has music and sound effects.
- Has a title screen, teaches the player how to maze, and reports its own crashes.
- Assets carry normal maps and AO, and render through the intended post-processing.
- Is on TestFlight and the Play internal track, with real testers giving feedback.
- Is free, with no store screen and no payment code — monetization follows, per the
  cosmetic SKU roadmap.
