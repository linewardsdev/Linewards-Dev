# Launch Roadmap

> **This file is the source.** [`launch-roadmap.html`](launch-roadmap.html) — the Gantt
> view — is generated from it by `tools/docs/render_roadmap.py`. Edit here, then run that
> script and commit both; never edit the HTML. CI's `--check` fails on a stale page, and
> generation fails if the timeline block below stops matching the week bullets.

**Drafted 2026-09-03.** A rewrite of the 31 July plan, made after its 31 August window was
missed. The August plan and what became of it are kept at the bottom, because the reason it
slipped is the main input to this one. Proposal for review — dates and scope are not
committed to.

---

## First, why August slipped

**Where this stands, 3 September.** The August plan named its binding constraint on 6 August:
store enrolment. It did not happen — the bundle identifier is still `com.ltwplaceholder.ltw`
on both platforms and no signing team is set. Meanwhile the four weeks went almost entirely
into work that plan ranked P1 or put out of scope: graphics Waves 1–5 (it excluded "Waves 2–3
beyond what tier B needs"), the tablet rail, and the tutorial. The game is materially better
than it was on 31 July, and it is not one step closer to a store. Two things did move that
matter here: it has run on a real iPad twice (9 and 21 August, 13 of 17 findings closed), and
onboarding is built. It has never run on a phone, never been built for Android, has no crash
reporting, no listing assets and no privacy policy. **This plan is one week of unblocking,
one week of proving it on a phone, one week of making it submittable, and one week of
testers — with all polish frozen.**

## Define "launch", because it changes the date by a month

| Tier | Means | Realistic date |
| --- | --- | --- |
| **A. Playable on your own device** | Sideloaded via free signing, no store | **Done, on iPad** — two device rounds in August; a phone is week 2 |
| **B. Soft launch** | TestFlight, invited testers, no public listing | **1 October** — this plan's target; iOS only |
| **C. Public store launch** | Live on the App Store, and Google Play if Android is taken on | **Late October** — review cycles, a first round of real-user fixes, and Android if chosen |

**Recommendation: target B for 1 October, iOS only, and decide Android at the end of week 4.**
Android has never been built; taking it on now doubles the device and store work in the
exact weeks that slipped last time. A Play internal track can follow in tier C without
delaying anything an iOS tester will see.

---

## What is actually left

Verified against the working tree on 2026-09-03, not carried over from the August table.
Grouped by whether it blocks a soft launch.

### P0 — cannot soft-launch without these

| # | Gap | Evidence | Est. |
| --- | --- | --- | --- |
| 1 | **Apple Developer Program not enrolled** | No team id in `ProjectSettings.asset`; `STORE_SIGNING_PREREQUISITES.md` boxes unchecked. Gates 2, 6 and 7 — every store step waits on it | External — start 4 Sep |
| 2 | **Bundle identifier and signing are placeholders** | `applicationIdentifier` is `com.ltwplaceholder.ltw` for iPhone and Android; `appleEnableAutomaticSigning: 0`. Proposed final value: `com.linewardsgames.linewards`, set only after enrolment so it binds to the right account | <1 d, after 1 |
| 3 | **No crash reporting or analytics** | Zero references in `Packages/manifest.json` or `Assets/Scripts`. Without it tester feedback is anecdote | 1 d |
| 4 | **Never run on a phone, no performance numbers** | Both iPad rounds were a 13" iPad Pro. `IOS_DEVICE_VALIDATION.md` is an empty template: no frame rate, thermal or memory figure exists for any device. Item 44's LODs are static, so the perf win is parked | 1–2 d |
| 5 | **No human acceptance pass** | `GAMEPLAY_DEVELOPMENT_CHECKLIST.md` at 75 checked / 42 open; the iPad rounds produced bug lists, not an acceptance pass. The tutorial (OPEN_ITEMS 54) has never been played on a device | 2 d |
| 6 | **No store listing assets, no privacy policy** | No screenshot set, description, age rating or hosted privacy URL. Screenshots should be taken after week 2's phone run, not before | 2–3 d |
| 7 | **Not on TestFlight** | Nothing has ever been uploaded. First review is 1–3 days | 2–3 d, external |
| 8 | **No testers** | Nobody who is not the owner has played it | 1 d to recruit, then ongoing |

### Decided out of scope for tier B

- **The shell stays a panel over the match, not a scene.** The August plan asked for this
  decision to be made deliberately; it is made here. The title, pause, results, codex, how-to
  and first-run screens all work as a panel and nothing a TestFlight tester sees depends on a
  scene boundary.
- **Android waits for tier C.** See the recommendation above. Enrol in the Play Console now
  anyway ($25, same day) so tier C is not gated on it later.
- **All render, roster and rail polish is frozen.** OPEN_ITEMS item 53's residuals stay
  parked; no Wave 6; the eleven staged roster meshes stay staged. New findings from the
  phone run go into OPEN_ITEMS, and only ones that block a tester's first ten minutes come
  back into this plan.
- **Multiplayer has its own plan and waits for the tier C decision.** `MULTIPLAYER_ROLLOUT.md`
  is the dependency-ordered checklist; its MP-00 (command queue at tick boundaries) is the
  one initiative allowed through the freeze, into week 2's reserved time only if the phone
  run comes back clean.

### P1 — should land before public launch (tier C), not blocking soft launch

- **Bot quality is product quality** — per open item R3: bots never sell, `Decide` sees only
  an economy record rather than the board, and there is no randomness anywhere in bot
  decisions, so a human meets the identical opponent every match. Week 4's testers will say
  whether this matters before the public launch.
- **Android build path** — none exists; `IosBuildRunner` has no counterpart.
- **Static LODs** (item 44) — the perf win is parked until the meshes are skinned; whether it
  is needed is what week 2's phone numbers answer.
- **Settings persistence** — some `PlayerPrefs` use exists (reduced effects, text scale, the
  tutorial flag); needs an audit before a public build.

---

## The four weeks

Sequenced so the one externally-gated item starts on day one and nothing with a review delay
sits on the critical path after week 3.

<!-- gantt
  Timeline geometry for docs/launch-roadmap.html, rendered by tools/docs/render_roadmap.py.
  Lives here rather than in the HTML so this file stays the single source: the prose and the
  chart are edited together or not at all.

  Columns:  week | item | sub-label | left% | width% | badge
    week    1-4, or "ext" for the externally-gated rows above week 1
    item    must match a bold bullet title in that week (or a Dependency in the critical
            path table, for ext rows). The renderer FAILS if it does not.
    badge   text shown inside the bar; prefix with "done:" to draw it as complete, "ext:"
            for an external wait

ext | Apple Developer Program | $99/yr · 24–48h approval · gates everything below | 0.5 | 12 | enrol → active
ext | Google Play Console | $25 · same day · for tier C | 0.5 | 6 | enrol
1 | Enrol, and freeze polish | day one; nothing else in this plan moves without it | 0.5 | 5 | ★
1 | Set the bundle identifier and signing | after enrolment; the id binds to the account that uploads first | 6 | 6 | <1d
1 | Add crash reporting and analytics | Unity Cloud Diagnostics or Sentry, whichever sets up faster | 6 | 10 | 1d
1 | Cut a signed iPhone build | the first build that is not an iPad | 16 | 9 | 1d
2 | Run it on a phone and write the numbers down | frame rate, thermals, memory, an 8-lane heavy send | 25 | 9 | 1–2d
2 | Play the acceptance pass with hands | GD-01 to GD-10, the tutorial included, on the phone | 32 | 10 | 2d
2 | Act on what the phone finds | reserved; only tester-blocking fixes | 41 | 9 | reserved
3 | Store listing assets and the privacy policy | screenshots from the phone build, description, age rating, hosted URL | 50 | 12 | 2–3d
3 | Upload to TestFlight | first review 1–3 days | 60 | 9 | ext:submit → review
3 | Recruit 10–20 testers | anyone who isn't you | 69 | 6 | 1d
4 | Testers play; triage and fix | no features scheduled here | 75 | 17 | reserved
4 | Decide tier C and Android | public submission in late October? Play track? | 92.5 | 7 | decision
-->

### Week 1 (Sep 4–10) — Unblock the store

The theme is *stop being gated*. The first bullet is the whole reason August slipped.

- **Enrol, and freeze polish.** Apple Developer Program on day one, and the Play Console in
  the same sitting so tier C is not gated later. From this day, no render, roster or rail
  work lands unless a tester could not get through their first ten minutes without it.
- **Set the bundle identifier and signing.** `com.linewardsgames.linewards` on both
  platforms, automatic signing with the enrolled team — only after enrolment, because the
  identifier binds permanently to the first account that uploads under it.
- **Add crash reporting and analytics.** One package, wired at boot, with a deliberate crash
  in a debug build to prove a report arrives. Sessions, match starts, tutorial skip or
  finish, and leaks per match are the four events worth counting.
- **Cut a signed iPhone build.** The first build in this project's history that is not an
  iPad. Free signing is fine until enrolment is active; the build path is `IosBuildRunner`.

*Exit criteria:* both accounts enrolled or pending, a real identifier in the project, a
crash report received, and an iPhone build in hand.

### Week 2 (Sep 11–17) — Prove it on a phone

- **Run it on a phone and write the numbers down.** Frame rate, thermal state and memory
  through an eight-lane match with a heavy send, on the oldest iPhone available and a
  current one. Written into `IOS_DEVICE_VALIDATION.md`, which has been an empty template
  since July. This is the first real data on whether the parked LOD work matters.
- **Play the acceptance pass with hands.** GD-01 through GD-10 from the gameplay checklist,
  on the phone, starting from a cleared tutorial flag so Practice is judged as a new player
  meets it. Write the result down; unchecked boxes with a reason beat checked ones.
- **Act on what the phone finds.** Reserved time. Only fixes a tester would hit early belong
  here; anything else goes to OPEN_ITEMS.

*Exit criteria:* the game runs acceptably on a phone with numbers to show it, and there is a
written human acceptance pass.

### Week 3 (Sep 18–24) — Make it submittable

- **Store listing assets and the privacy policy.** Screenshots from the phone build (now that
  the board and rails are final), a description, the age-rating questionnaire, and a privacy
  policy at a real URL — which needs a domain, so start that on the first day of the week.
- **Upload to TestFlight.** The first submission carries a 1–3 day review; budget for a
  rejection on metadata and resubmit inside the week.
- **Recruit 10–20 testers.** Friends, a subreddit, a Discord. The number matters less than
  that they are not you.

*Exit criteria:* a build approved for external TestFlight testing and a list of people who
have agreed to play it.

### Week 4 (Sep 25–Oct 1) — Testers, then decide

- **Testers play; triage and fix.** The whole week. Crash reports and the four analytics
  events say what happened; testers say what it felt like. Nothing new is scheduled here.
- **Decide tier C and Android.** With a week of real feedback: submit publicly in late
  October or not, and whether Android is worth the second device and store track.

*Exit criteria:* real people who are not you have played it, you know what they thought, and
the tier C decision is made on evidence.

---

## Critical path and external dependencies

Everything with a delay you cannot compress:

| Dependency | Lead time | Start by |
| --- | --- | --- |
| Apple Developer Program | 24–48h, occasionally longer | **Sep 4** |
| Google Play Console | Usually same day; identity checks can add days | **Sep 4** (for tier C) |
| TestFlight first build review | 1–3 days, longer on a metadata rejection | Sep 18 |
| Privacy policy hosted at a public URL | Needs a domain | Sep 18 |

**Enrolment on day one.** It cost the whole of August; it costs $124 and an afternoon.

## The three risks worth naming

1. **Enrolment slips again.** Every P0 row but 3, 4 and 5 waits on it, and the August plan
   showed what happens when it drifts: the weeks fill with polish. Mitigation is procedural,
   not technical — it is the first bullet of week 1 and nothing else in week 1 starts before
   it is submitted.
2. **The phone run finds a performance problem.** All device evidence so far is an 8 GB M4
   iPad; the quality tier drops below 6 GB to Medium, but no phone has ever run a full
   eight-lane match with bloom and shadows on. The static LODs (item 44) are the known lever.
   Week 2's reserved days exist for this.
3. **Polish creep.** It is what consumed August: five graphics waves and a tutorial, each
   individually justified, none on the critical path. The freeze in week 1 is the mitigation,
   and the test for an exception is written down: would a tester fail to get through their
   first ten minutes without it?

## Landed since the draft — the August plan, as it played out

### Graphics Waves 1–5 (1–3 Sep)

A live-capture render review found twenty issues; five waves closed the units, the board
material, grounding, combat VFX and tier silhouettes, with a re-audit at the same frames
between them. Ready for store screenshots, which is the one way this work feeds the plan.

### The iPad rounds and the tablet rail (9–30 Aug)

Two device rounds on a 13" iPad Pro produced seventeen findings, thirteen closed; the tablet
UI became a full-width rail with list rows, a seat leaderboard and a send panel. This is the
nearest thing to the August plan's "play it with hands" session, and it is why tier A is
marked done.

### How to Play and Practice (2–3 Sep)

A five-card How to Play screen, a first-run offer, and a skippable, re-enterable Practice
match against passive bots with a coach strip — OPEN_ITEMS item 54. Built after August's
window, captured at phone and iPad widths, unverified on a device.

## What "done" means on 1 October

- Runs on an iPhone, with a real identifier, and there are written frame-rate, thermal and
  memory numbers for it.
- Reports its own crashes and counts sessions, match starts, tutorial outcomes and leaks.
- Has passed a written, with-hands acceptance pass including the tutorial.
- Has a store listing, an age rating and a hosted privacy policy.
- Is on TestFlight with 10–20 external testers, and their feedback exists in writing.
- Is free, iOS only, with the Android and public-launch decisions made on that feedback.
