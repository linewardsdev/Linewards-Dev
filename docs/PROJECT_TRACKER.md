# Project Tracker

## Purpose

This is the single, standardized status board for Line Wards, combining what used to be five
separate, drifting trackers: `MVP_STATUS.md`, `MVP_IMPLEMENTATION_CHECKLIST.md`,
`GAMEPLAY_DEVELOPMENT_CHECKLIST.md`, `OPEN_ITEMS.md`, and `LAUNCH_ROADMAP.md`. It also folds in
the two newer initiative-specific trackers (`MULTIPLAYER_ROLLOUT.md`, `SECURITY_AUDIT_2026-09-05.md`)
as summarized sections, since a tracker that omitted the two largest active workstreams would not
be much of a tracker.

**This was already the project's own recommendation.** `OPEN_ITEMS.md`'s own R5 (2026-07-31) named
the exact problem this file fixes: "the status docs contradict each other faster than two agents
reconcile them" — citing `GAMEPLAY_DEVELOPMENT_CHECKLIST` claiming tiers "not implemented" after
they shipped, `MVP_IMPLEMENTATION_CHECKLIST` sitting 18 days stale, and `MVP_STATUS` describing a
three-lane game that had been eight-lane for weeks. Every one of those contradictions was real and
is reconciled below against the actual working tree, not carried forward.

**How to use this doc.** Status here is authoritative as of **2026-09-07**. The five source docs
are kept, unedited in their historical content, as detail archives — each now carries a banner
pointing here. Where an item needs paragraphs of investigative narrative to make sense (most of
`OPEN_ITEMS.md`'s art/graphics findings do), this file gives the one-line status and a pointer
rather than reproducing the story. `MULTIPLAYER_ROLLOUT.md` and `SECURITY_AUDIT_2026-09-05.md`
remain the living detail docs for their own areas — update status in both places when either
initiative moves, or this file rots exactly the way R5 described.

**Legend:** `[x]` done · `[ ]` open · `[~]` in progress / partially done · **DECISION** flags
something that needs the owner's judgment call, not more engineering.

---

## 1. Launch (soft launch target: 1 October 2026)

Source: `LAUNCH_ROADMAP.md` (kept live — this section mirrors it, that doc keeps the week-by-week
plan and the reasoning).

### P0 — cannot soft-launch without these

- [ ] **Apple Developer Program enrollment.** Not enrolled. Gates the bundle identifier, the store
      listing, and TestFlight. External, needs the owner. **This is the current blocking item.**
- [ ] **Real bundle identifier and signing.** Still `com.ltwplaceholder.ltw`. Blocked on enrollment.
- [x] **Crash reporting and analytics.** Closed 2026-09-07 — Unity Diagnostics linked to a fresh
      Unity Cloud org (`developermtrakdqr`) under `developers@linewards.com`. Session telemetry
      confirmed flowing; a crash-specific event was not separately confirmed (owner's call to
      close anyway).
- [ ] **Phone performance numbers.** Never run on a phone — only a 13" iPad Pro. `IOS_DEVICE_VALIDATION.md`
      is an empty template.
- [ ] **Human acceptance pass.** `GAMEPLAY_DEVELOPMENT_CHECKLIST.md` is 75 checked / 42 open; no
      pass has been played with hands, tutorial included, on a device.
- [ ] **Store listing assets and privacy policy.** No screenshots, description, age rating, or
      hosted privacy URL.
- [ ] **TestFlight upload.** Nothing has ever been uploaded.
- [ ] **Testers.** Nobody who isn't the owner has played it.

### P1 — should land before public launch, not blocking soft launch

- [ ] Bot quality as a product feature (bots never sell, no randomness in decisions) — see R3 below.
- [ ] Android build path — none exists.
- [ ] Static LODs — perf win parked until the meshes are skinned (`OPEN_ITEMS.md` #44).
- [ ] Settings persistence audit (`PlayerPrefs` usage for reduced effects / text scale / tutorial flag).

---

## 2. Core simulation & local MVP (MVP-00 through MVP-11)

Source: `MVP_IMPLEMENTATION_CHECKLIST.md`. Status below is corrected against the working tree —
that doc's own "eight lanes, not three" caveat is already folded in here.

- [x] **MVP-00 — Solution foundation and CI.** Done locally; the one open box is "CI passes on a
      PR containing only a trivial test" — unverified, not failing.
- [x] **MVP-01 — Simulation contracts and content model.** Complete.
- [x] **MVP-02 — Grid, occupancy, and path validation.** Complete.
- [x] **MVP-03 — Economy, carousel, lives, and results.** Complete.
- [x] **MVP-04 — Creep movement, tower combat, and events.** Complete.
- [x] **MVP-05 — Bots, replay records, and scenario suite.** Complete.
- [x] **MVP-06 — Unity bridge and local vertical slice.** Complete.
- [x] **MVP-07 — Touch placement and match HUD.** Complete.
- [~] **MVP-08 — Rendering, pooling, and feedback.** Source and batch evidence pass; two manual
      visual-acceptance boxes still open (distinguishable-at-a-glance, presentation-mode toggle
      doesn't change outcomes).
- [x] **MVP-09 — Full local integration and tuning.** Complete (now eight-player, not three).
- [~] **MVP-10 — iOS TestFlight and device validation.** This is now literally `LAUNCH_ROADMAP.md`'s
      Weeks 1–3 — tracked there, not separately here, to avoid exactly the drift R5 warned about.
- [ ] **MVP-11 — Android compatibility validation.** Not started; deferred to the tier C decision
      (`LAUNCH_ROADMAP.md` Week 4). Store-facing deployment work (Play Console, signing, policy
      gates) is tracked separately in §9.

---

## 3. Gameplay development (GD-00 through GD-10)

Source: `GAMEPLAY_DEVELOPMENT_CHECKLIST.md`.

- [x] **GD-00 — Playable-loop baseline.** Done.
- [~] **GD-01 — Board readability and camera.** Deliverables done; all three acceptance checks are
      unverified by a human (need hands-on play, not more code).
- [~] **GD-02 — Placement and tower controls.** Deliverables done; acceptance checks need
      hands-on play.
- [~] **GD-03 — Tower, creep, and send content.** 15/15 towers have distinct mechanics; Chain
      Arc's hops still reuse the generic beam cue. Acceptance checks need hands-on play to confirm
      each tower has a situation where it's the right buy.
- [ ] **GD-04 — Economy, pacing, and match length.** Deliverables and tuning done; all three
      acceptance checks need hands-on play.
- [x] **GD-05 — Bot behavior as opponents.** Done, including acceptance checks (deterministic,
      profile-distinct, reaches a winner — all measured).
- [x] **GD-06 — Session flow and results.** Done.
- [~] **GD-07 — Game feel and feedback.** Deliverables done; acceptance checks need hands-on play.
- [x] **GD-08 — Playtest evidence and tuning notes.** Done (three recorded playtests, prioritized
      fixes).
- [ ] **GD-09 — Category upgrade tiers.** Designed (`CATEGORY_UPGRADE_TIERS_PLAN.md`), not
      implemented. Blocked on a rebalance of the original justification (the stalemate it was meant
      to fix was a bot bug, already fixed elsewhere).
- [~] **GD-10 — Unit animation across the full roster.** Towers 15/15 done. Creeps: 7 procedural +
      8 rigged, each with exactly one `Walk` state. Open residuals: watch/tune the speed-scaling
      exponent, no death/hit-reaction clips, Aegis Warden's arm pose (not fixable by animation),
      Chain Arc's hops, Bramble Hold's visible slow effect blocked on a data plumbing gap.
- [~] **Send queue (2026-08-08).** Simulation and adapter fully done and tested. **Closed
      2026-09-07**: `SendDockController` now has a cancel badge reaching `CancelQueuedSend` for
      local play (`OPEN_ITEMS.md` #47). **Found while closing it**: `CancelQueuedSend`/
      `ClearSendQueue` have no online-multiplayer wire path at all (§4/§5's MP-06 work landed
      after the send queue did, and nothing had exercised this until a real control existed) —
      new `OPEN_ITEMS.md` #55. Still not played by a human.

---

## 4. Online multiplayer (MP-00 through MP-07)

Source and living detail doc: `MULTIPLAYER_ROLLOUT.md`. Summarized here; update status in both
places.

- [~] **MP-00 — Command queue at tick boundaries.** Replay/determinism half landed (a full
      eight-lane match replays to an identical fingerprint). Still open: commands still apply the
      instant they're accepted rather than being buffered and merged at a tick boundary — needs a
      second real command source to prove against, sequenced alongside MP-03/MP-04.
- [~] **MP-01 — Seat table.** Reconnect and Practice-mode read-model landed and tested. Still open:
      replacing `BotLaneOptions` as the actual source of truth, and real multi-human identity.
- [~] **MP-02 — Recorded opponents.** Simulation half (fallback to bot on version mismatch, bot-fill
      on early stream end) landed and tested. Still open: the client-side seat table UI and curated
      recordings — nothing to test the "can't tell it's a ghost" acceptance check with yet.
- [x] **MP-03 — Authority on every command.** Landed 2026-09-03. Every command resolves its seat
      through `ISeatAuthority`; rate limiting extended to the whole build family.
- [x] **MP-04 — Match server.** Built and live-verified locally end to end (real WebSocket
      transport, real match to a real result). The one unchecked box ("two phones on different
      networks") is an environment limitation, not a product gap — the protocol and lifecycle are
      proven identically otherwise.
- [~] **MP-05 — Session, identity, lobby, matchmaking.** Identity (Google Sign-In via PlayFab) and
      opportunistic matchmaking code are landed and verified against both a fake handler and the
      real title (`FBC34`) and a real iOS device. **Blocked on the Azure Dasv4 quota approval**
      for live matchmaking verification (two real identities pooling). Not started: Android
      identity (Google Play Games Services). Unchecked: "results survive a client crash and a
      server restart."
- [x] **MP-06 — Client over the wire.** Every acceptance check in this initiative's own scope is
      done — real device, real match, reconnect survivability (including kill-and-relaunch),
      clean-exit paths, all confirmed live 2026-09-04/05.
- [~] **MP-07 — Operations.** Hosting mechanism built and live-verified locally (`LocalMultiplayerAgent`).
      **Blocked on the same Azure quota approval** for the real PlayFab Game Manager build.
      Telemetry (structured JSON lifecycle events) and PlayFab-ban enforcement landed 2026-09-07.
      Still open: a real log-ingestion dashboard, the runbook's telemetry/abuse sections, and
      client-side crash reporting was resolved separately (see Launch section above, via Unity
      Diagnostics rather than a bespoke pipeline).

**Owner decision open, MP-05 self-audit note carried here:** the M-C2/H5 items below (session
ticket storage, transport encryption) block two of MP-06/MP-07's own "done" claims from being
fully trustworthy in production — see the security section.

---

## 5. Security & standards audit (2026-09-05)

Source and living detail doc: `SECURITY_AUDIT_2026-09-05.md`. 19 of 23 findings fixed and
test-verified (398+ tests passing throughout). Four remain, two of them deliberately deferred:

- [ ] **H5 — PlayFab session ticket travels over plaintext `ws://`.** Open. Real fix is TLS
      termination on real hosting — infrastructure, blocked on the same MP-07 quota approval.
      A code-only mitigation was considered and rejected (no real benefit without TLS).
- [~] **M-C2 — Session ticket in plaintext `PlayerPrefs`.** iOS Keychain bridge drafted
      2026-09-07 (`LTWKeychainBridge.mm` + `SecureSessionStore.cs`, wired into `PlayFabSession`).
      **Compile-checked only — UNTESTED ON A REAL DEVICE.** Android has no online identity path
      yet, so no Android bridge was started.
- [~] **M-DET1 — Match seed reaches gameplay through `System.Random`, not an owned algorithm.**
      Stale doc comments fixed. An owned-PRNG swap was prototyped and reverted — it shifted tuned
      bot behavior in `BotMazingTests`/`VerticalSliceBridgeTests`. Latent, not active (client and
      server share one generator family today). **Note:** this is the same underlying fact as
      `OPEN_ITEMS.md` #42 below — that item's "nothing consumes the seed" is now stale; the seed
      does drive a bot's opening line choice (see `BotController`), it's just not an owned
      algorithm and isn't varied anywhere else.
- [ ] **L3 — Persisted match host/port dialed unvalidated on launch.** Open, blocked on both H5
      and M-C2 actually closing (not just drafting).

---

## 6. Art, graphics & code health

Source: `OPEN_ITEMS.md` — kept as the detail archive; dozens of items there are already resolved
and ledgered (2026-07-31 through 2026-09-02 batches, ~45 items). Below are only the numbered items
still open as of 2026-09-07, grouped by domain, with one line each.

### Rendering / graphics polish

- [ ] **#53 — Render review residuals after Wave 5.** Four small polish items (~1 day total):
      label-on-label stacking, hit-flash timing, elimination/victory cue style, Control ward base
      dish alpha.
- [ ] **#52 — Tower body loses its own shadow via `NormalizeRendererPolicy`.** Runtime workaround
      shipped; root fix needs regenerating 15 tower prefabs in an interactive Editor session.
- [ ] **#33 — MSAA sample-count mismatch on Metal.** Needs a real device (Metal) re-test; not
      reproducible in the Editor.
- [ ] **#48 — Send dock category cards render content over the card art.** Needs the art's inner
      region measured from source, not guessed.
- [ ] **#39 — Every creep and tower body material is at smoothness 0.42 vs. a 0.45 constant.**
      Roster-wide validator failure. Fix is likely one click + committing 15 materials — **DECISION**:
      confirm 0.45 is actually the intended value before an owner approves the change.

### Art content

- [ ] **#2 — Bloom cost on a physical Android device is unmeasured.** More urgent now that
      post-processing was fixed to actually run (was silently disabled).
- [ ] **#3 — No asset has a normal map or bound AO.** **DECISION**: regenerate via Meshy vs. bake
      in Blender.
- [~] **#19 — Nine of fifteen creeps have no usable emissive detail.** Turretwalker fixed. Still
      open: obsidianbrute/shade need a re-bake at proper exposure; revenant/burrower/colossus/
      stalker/warden/zephyr need authoring from scratch.
- [ ] **#18 — Eleven metallic/smoothness maps can't be regenerated from committed sources.**
      Silent-overwrite risk is fixed (the tool now refuses and reports); **DECISION**: which
      version (4096-derived committed, or 1024-derived regenerable) is the art that ships.
- [ ] **#20 — Target-reference gate measures 10 roles against retired 2D art, 20 against nothing.**
      **DECISION**: retire the target-reference score, or promote current captures as the new
      reference.
- [ ] **#41 — `PromoteCreep3DSet` silently overwrites committed motion styles with spec defaults.**
      **DECISION**: which side (spec or committed library) is authoritative; tool should refuse or
      report either way.
- [x] **#4 — Leg-rig visibility on shell-bodied creeps.** Resolved into a standing methodology
      rule (render-check from the game camera before rigging), applied project-wide and paid off
      twice since.
- [~] **#11 — Seven creeps with no animation.** Effectively closed by the wave 2.4 audit: all four
      remaining are non-walkers by body plan (stalk, cluster, orb, coil) — no rig needed, only
      confirming their existing procedural motion fits.
- [x] **#1 — `_EMISSION` keyword loss on tower materials.** Mitigated (an import-time guard
      self-heals it every time); root trigger narrowed to `VisualReviewCaptureRunner`'s shutdown
      import pass but not fully isolated to a line.

### HUD / UX

- [~] **#10 — In-match HUD structurally can't be animated.** Font and shell screens (title/pause/
      results) migrated to UI Toolkit and resolved. In-match HUD is still 8 files of IMGUI — the
      expensive remaining part, explicitly not scheduled this wave.
- [ ] **#35 — A defeated seat gets a spectator state but no defeat moment.** Spectator state
      shipped; the "you lost, here are your numbers" beat is a **DECISION** (what it says
      mid-match, modal or not, whether there's an exit).
- [x] **#47 — Send-queue cancel has no UI control.** Closed 2026-09-07 — see the GD checklist's
      send-queue entry above for what shipped and what it surfaced (#55).
- [x] **#54 — How to Play rewrite and Practice tutorial.** Shipped and verified 2026-09-03 (batch
      compile, 340/340 tests, capture evidence). Minor residuals noted, device-unverified.
- [ ] **#55 — `CancelQueuedSend`/`ClearSendQueue` have no online-multiplayer wire path.** Found
      2026-09-07 while closing #47 — every other command (`PlaceTower`, `UpgradeTower`,
      `BuyCategoryTier`) sends a real wire message in online mode; these two silently do nothing
      instead. Needs a new wire message type, a server dispatch case, and its own
      `MatchServerIntegrationTests` coverage — server-and-wire work, not attempted as part of the
      UI-only fix that found it.

### Code / repo health

- [ ] **#21 — Repo is 1.58 GiB and growing, no LFS.** **DECISION**: adopt Git LFS (and whether to
      rewrite history); decide whether AI-staging intermediates belong in the repo at all.
- [~] **#28 — CI never compiles the Unity client.** Gate is written (`docs/ci/unity-compile.yml`).
      Blocked on two owner actions: a push credential with GitHub's `workflow` scope, and a Unity
      license in repo secrets. Unverified until then — cannot even run once without a license.
- [ ] **#40 — Seventeen compiler warnings in the Editor assembly.** Invisible unless the assembly
      recompiles. Categorized (obsolete API x6, nullable annotation x5, one dead field, one real
      possible-null worth a look).
- [x] **#29 — WITHDRAWN.** "Creeps invisible in device build" was a reviewer misread; confirmed
      false the same day.
- [ ] **#44 — Unit LODs are static (no skinned decimation).** Perf win parked until Week 2's phone
      performance pass says whether it's actually needed.
- [~] **#46 — Forcing a category pick (line lock).** Tier-sink pricing and Foundry's brake done.
      Still open: Arcane needs its own brake before the lock is fair, and the lock itself needs
      per-line bot build orders (a content restructure) before it can ship without breaking bots.
- [ ] **#45 — Income pins at the ceiling before eliminations happen.** Measured and reproduced a
      fix; reverted rather than shipped. **DECISION**: keep income contested late (accept gain-1
      creeps stop paying near the cap) vs. keep the low end of the roster alive (accept the pin).
- [~] **#42 — Match seed under-consumed, measurements are one sample.** Superseded/updated by the
      security audit's M-DET1 above — the seed now drives one bot decision (opening line), so
      "nothing consumes it" is stale. Still true: no owned PRNG, no variation anywhere else (send
      quantity, mazing), so most balance numbers are still effectively one trajectory.
- [x] **#43 — Two bot profiles hoard gold and die holding it.** Closed 2026-08-09 by
      re-measurement — the underlying pressure-threshold and build-order bugs were already fixed
      by unrelated work; the premise no longer held.
- [ ] **#17 — Owner decisions still pending.** Confirm the raised graphics quality target
      (`GRAPHICS_AA_UPLIFT.md` §3); pick the normal-map route (#3); decide whether to re-source
      albedo generation entirely (the most expensive, most identity-defining choice in that plan).

---

## 7. All open owner decisions, in one place

Pulled together from every section above — nothing here needs more engineering, only a call:

1. Enroll in the Apple Developer Program (§1 P0 — blocks nearly everything else in Launch).
2. Income ceiling: keep it contested late, or keep the low end of the roster alive (§6, #45).
3. Which smoothness value (0.42 vs 0.45) is correct before 15 materials change (§6, #39).
4. Normal maps: Meshy regeneration vs. Blender bake (§6, #3) — also gates #17's albedo question.
5. Metallic/smoothness source of truth: 4096-derived committed vs. 1024-derived regenerable (§6, #18).
6. Target-reference art gate: retire the score vs. promote current captures as the new baseline (§6, #20).
7. `PromoteCreep3DSet`: spec values or committed library values win (§6, #41).
8. Defeat-moment design: what a mid-match loss screen says and whether it's dismissible (§6, #35).
9. Git LFS adoption and repo history rewrite (§6, #21).
10. Raised graphics quality target confirmation (§6, #17).
11. Whether Android is taken on for tier C, and the public-launch date (§1 — Week 4 of the launch
    plan; §9 has the full Android deployment checklist this decision would greenlight).

---

## 8. Standing recommendations not yet fully acted on

Source: `OPEN_ITEMS.md`'s 2026-07-31 review. Kept because they're still true, not because they're
new.

- [ ] **R1 — Play the game with human hands.** Still the single highest-leverage open action across
      this entire tracker — roughly 20 acceptance boxes in §2/§3 are blocked on nothing else.
- [~] **R2 — Put a build on a physical phone.** This is now literally `LAUNCH_ROADMAP.md` Week 2.
- [~] **R3 — Treat bot quality as a product feature.** The specific gap it named (5-of-15 towers,
      degenerate profiles) is closed. Still open: bots never sell, `Decide` sees only an economy
      record not the board, and there's no randomness anywhere in bot decisions.
- [~] **R4 — Command queue at a tick boundary.** Half landed as MP-00 (§4) — replay reproduction
      works; true deferred-application-at-a-tick-boundary is still open.
- [x] **R5 — Consolidate status into fewer living documents.** This file.

---

## 9. Android marketplace deployment (not started)

**Standing decision, not reversed here:** `LAUNCH_ROADMAP.md` explicitly deferred Android to tier
C — "Android waits for tier C... taking it on now doubles the device and store work in the exact
weeks that slipped last time" — with the actual go/no-go decision scheduled for Week 4 (§7, item
11). This section exists so that decision is made against a real checklist instead of a blank
page, not to jump the freeze. Nothing below is scheduled work yet.

**Current state, updated 2026-09-07:** `applicationIdentifier.Android` is still the placeholder
`com.ltwplaceholder.ltw`; no keystore is configured (`androidUseCustomKeystore: 0`,
`AndroidKeystoreName`/`AndroidKeyaliasName` empty) — the package-name/signing decision is still
fully open. `MVP-11` (§2) device compatibility validation is still "Not started" — a build
existing is not the same as it being tested on a device. **What changed**: `AndroidBuildRunner.cs`
now exists (`IosBuildRunner`'s counterpart), and a real local build succeeded — see the policy-gate
findings below for what that build proved. Local sandbox testing (sideload to a physical device
over USB debugging, no store account needed) already worked before this and still does, per
`STORE_SIGNING_PREREQUISITES.md`/`ANDROID_DEVICE_VALIDATION.md` — what's still genuinely untouched
is the store-facing half (account, signing, listing).

### What Play Console publishing specifically needs, beyond local sideload testing

- [ ] **Google Play Console developer account** — $25 one-time, same-day per `LAUNCH_ROADMAP.md`'s
      own critical-path table (identity checks can occasionally add days).
- [ ] **Package name decision.** `STORE_SIGNING_PREREQUISITES.md` already recommends keeping this
      identical to the iOS bundle identifier (`com.linewardsgames.linewards`, proposed) for simple
      cross-platform account linking later — same **DECISION** blocker as iOS's bundle ID (§7),
      not a separate one.
- [ ] **Play App Signing enrollment** (Google's recommended path: Google holds the app signing
      key, the studio holds an upload key) and Unity's Android Publishing Settings pointed at that
      keystore — through the Editor UI, not by hand-editing `ProjectSettings.asset`, since the
      keystore password must never be committed.
- [ ] **Release build settings confirmed before first upload**: IL2CPP scripting backend, ARM64
      target architecture, `.aab` output (Play Console requires it; `.apk` does not satisfy
      submission).
- [ ] **Content rating questionnaire** (Play Console's own IARC-based flow — separate from
      Apple's age-rating questionnaire, needs answering independently even though the app is the
      same).
- [ ] **Data Safety section** — Play Console's own disclosure form for what data the app collects
      and shares; PlayFab's Google/session-ticket identity flow (MP-05) is the main thing to
      declare accurately here.
- [ ] **Privacy policy at a hosted URL** — same requirement iOS already needs (§1 P0), reusable
      as-is once it exists.

### Two current Play Store policy gates — both now verified, 2026-09-07

Both were open questions when this section was first written; both are now closed by an actual
build and a direct inspection of its output, not by assumption. `Assets/Editor/AndroidBuildRunner.cs`
now exists (mirrors `IosBuildRunner.cs`) — this project's first-ever Android build tooling.

- [x] **Target API level.** Google requires new app submissions to target **Android 16 (API
      level 36)** as of **31 August 2026** — a deadline already passed as of this writing, so
      this was a precondition for the very first upload, not a future concern. **Confirmed**:
      Unity `6000.5.3f1`'s bundled Android SDK includes `android-36` outright
      (`PlaybackEngines/AndroidPlayer/SDK/platforms/android-36`), and `AndroidBuildRunner`
      defaults to `AndroidSdkVersions.AndroidApiLevel36`. A real build with this target
      succeeded.
- [x] **16 KB memory page size support.** Google has been actively rejecting uploads with
      native (`.so`) libraries not aligned for 16 KB pages since around May 2026. This was a
      real unknown, not a formality: Unity's bundled NDK here is r27c, which defaults new
      binaries to **4 KB** alignment, not 16 KB. **Confirmed by direct measurement**, not
      inferred from the NDK version: a real local build (`LineWards.apk`, 1224 MB, first-ever
      Android build of this project) was produced via `AndroidBuildRunner`, and every LOAD
      program-header segment in all 7 ARM64 native libraries it contains
      (`lib_burst_generated.so`, `libc++_shared.so`, `libgame.so`, `libil2cpp.so`, `libmain.so`,
      `libswappywrapper.so`, `libunity.so`) reads `Align = 0x4000` (16384 bytes) via
      `llvm-readelf -l` — checked twice independently, same result both times. So despite the
      NDK's own default, something in Unity `6000.5.3f1`'s Android build pipeline is already
      forcing 16 KB alignment at link time for this project's output. **Not yet identified
      which mechanism does it** (a Unity-side default for this Editor version, an implicit
      Gradle/AGP setting, or something else) — worth knowing before assuming every future build
      configuration stays compliant, but the current default pipeline, as used here, is not an
      open compliance risk.
- **One real, unrelated bug this build surfaced and fixed**: the first-ever Android compile
  failed on `Assets/ThirdParty/PlayFabSDK/Shared/Public/PlayFabSettings.cs` referencing
  `AndroidJavaClass`/`AndroidJavaObject` — the vendored PlayFab SDK needs the built-in
  `com.unity.modules.androidjni` package, which had never been enabled since only iOS ever
  compiled before. Added to `Packages/manifest.json`; the re-run succeeded.

### Already tracked elsewhere — cross-referenced, not duplicated

- [ ] **MVP-11 — Android compatibility validation** (§2): device performance/compatibility testing
      itself, distinct from store publishing.
- [ ] **Google Play Games Services sign-in** (§4, MP-05): the Android equivalent of the iOS
      Google Sign-In bridge — not started. Needed before an Android build can reach online
      multiplayer at all, independent of Play Console publishing.
- [ ] **M-C2's Android half** (§5): `SecureSessionStore`'s Keystore/`EncryptedSharedPreferences`
      bridge was deliberately not built since there's no Android identity flow yet to protect —
      revisit once Google Play Games Services sign-in above lands.

**Bottom line:** the build-tooling gap and both live policy-compliance unknowns are now closed —
real, unblocked engineering that needed no account or decision. Everything else in this section
(Play Console account, package name, signing, listing, Google Play Games Services) is still
genuinely zero-progress and stays that way until the Week 4 tier-C decision says otherwise — this
section remains the answer to "what would it actually take", not a proposal to start the rest now.
