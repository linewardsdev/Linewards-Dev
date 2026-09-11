# Multiplayer Rollout Plan

**Drafted 2026-09-03.** Proposal for review — scope and order are the proposal; nothing here is
committed to, and nothing here is scheduled.

## Goal

Take Line Wards from a one-human, seven-bot offline match to real opponents, in the order that
lets each step ship on its own: deterministic command application first, then seats as data,
then recorded opponents that need no server, then a server-authoritative private match, then
sessions and matchmaking, then the operations that keep it alive. Every step reuses
`LTW.Simulation` unchanged in its gameplay; per `ARCHITECTURE.md`, online play is "a later
replacement for the source of player commands, not a rewrite of combat or pathing".

## How this plan relates to the others

- **It is gated behind tier B.** `LAUNCH_ROADMAP.md` freezes everything not on the path to
  TestFlight on 1 October. Only MP-00 is exempt (see Working Rules). The tier C decision at the
  end of that plan's week 4 is where this plan gets a date, and whether it starts at MP-02
  (recorded opponents) or goes straight for MP-04 (a live server) is decided on tester feedback.
- **It is the checklist shape, not the dated-roadmap shape**, because the work is ordered by
  dependency rather than calendar. When a push is cut from it, that push gets its own dated
  roadmap the way the launch did.
- **`MULTIPLAYER_SEATS_AND_AUTHORITY.md` is the inventory** of what already exists and why; this
  plan does not repeat it. `ARCHITECTURE.md`'s "Future Online Architecture" and "Security And
  Fair Play" sections are the constraints every initiative below inherits.
- Defects found along the way go to `OPEN_ITEMS.md`, as everywhere else.

## Working Rules

- `LTW.Simulation` stays pure: no networking, no Unity, no I/O. The server and the client both
  consume it; neither changes its rules.
- **The seat comes from the connection, never from the message.** Every command resolves its
  `PlayerId` through `ISeatAuthority`; a command that trusts its own id is a defect from MP-03 on.
- **Clients submit intent only.** Gold, income, lives, paths, combat, leaks and results are
  server-owned. The client renders what arrived in the snapshot or event stream.
- Every simulation initiative needs a test in `tests/LTW.Tests`. Every online initiative needs a
  reproducible private-match verification path, written down.
- No third-party package, cloud service or persistent backend without an entry in
  `MVP_DEPENDENCIES.md` and a line in this plan's Decisions section.
- One owner per initiative; the foundation owner approves changes to shared simulation
  contracts (commands, events, snapshot, seat table).
- **MP-00 is the only initiative allowed through the launch freeze**, because it needs no
  networking, is testable with the existing harness, fixes a defect already on record (replays do
  not reproduce a match), and every later step is built on it.

## Dependency Map

```text
MP-00 Command queue at tick boundaries          (no networking; exempt from the freeze)
        |
        v
MP-01 Seat table: human | bot | empty, bot-fill, reconnect as data
   |                    |
   v                    v
MP-02 Recorded       MP-03 Authority on every command
      opponents           |
   (no server)            v
   |               MP-04 Match server: headless, authoritative, private match
   |                      |
   |                      v
   |               MP-05 Session, identity, lobby, matchmaking
   |                      |
   +----------+-----------+
              v
MP-06 Client over the wire: snapshot-driven UI, latency, reconnect
              |
              v
MP-07 Operations: hosting, cost, abuse, telemetry, population
```

MP-02 and MP-03 are independent of each other; MP-02 ships a "real opponents" feature with
nothing past MP-01, which is why it is on its own branch of the map.

## Fork Board

| ID | Initiative | Owner | Depends On | Exit Signal |
| --- | --- | --- | --- | --- |
| MP-00 | Command queue at tick boundaries | Simulation | — | A recorded match replays bit-for-bit from its `ReplayRecord`; two clients' same-tick commands resolve identically regardless of arrival order. |
| MP-01 | Seat table | Simulation | MP-00 | A match is described by a seat table; any seat can be human, bot or empty; an empty or dropped human seat is bot-filled without stopping the match. |
| MP-02 | Recorded opponents | Simulation + Client | MP-01 | A player fights seats driven by other players' recorded command streams, with no server, and the match is indistinguishable from a bot match in the client. |
| MP-03 | Authority on every command | Simulation | MP-01 | No command path reads its own `PlayerId`; every command family is rate-limited; the invariants list below is a passing test suite. |
| MP-04 | Match server | Server | MP-03 | `LTW.MatchServer` hosts a private match over a real WebSocket transport to a real result, proven end to end in this environment on loopback; two real devices on two real networks is a deployment trial this environment cannot run, not an open task in the code. |
| MP-05 | Session, identity, lobby, matchmaking | Server + Client | MP-04 | A player queues, is matched with humans where available and bots elsewhere, plays, and sees a durable result. |
| MP-06 | Client over the wire | Client | MP-04 | The HUD and board render only from the wire; a 2 s network hole and a reconnect are survivable mid-match. |
| MP-07 | Operations | Server | MP-05 | A week of live matches with cost, crash, desync and abuse figures on a dashboard, and a written runbook. |

## Decisions needed before the relevant initiative starts

1. **Recorded opponents first, or straight to live?** MP-02 gives real opponents for a fraction
   of MP-04's cost and works with a small player base; MP-04 is the genre's expectation. The tier
   C decision answers this with tester evidence. Default if undecided: MP-02 first.
2. **Accounts — decided 2026-09-03: PlayFab.** Durable, cross-play accounts with Apple and Google
   sign-in are mandatory (owner decision, not a technical default). PlayFab was chosen over
   Firebase Authentication specifically because MP-05 also needs matchmaking and lobbies, which
   are native to PlayFab and would otherwise be hand-built on top of a general-purpose auth
   backend. See MP-05 for the resulting architecture and the external setup this decision requires
   before any of it can be tested end to end.
3. **Hosting.** One regional container to start (per `ARCHITECTURE.md`); the provider, region
   and cost ceiling are chosen at MP-04, not before.
4. **Simulation rate on the wire.** The simulation runs a fixed tick; whether the server
   broadcasts snapshots, events, or both, and at what cadence, is measured at MP-04 on real phones
   before MP-06 builds on it.
5. **Ranking.** Out of this plan. `ARCHITECTURE.md` lists it under production multiplayer; it is
   a product decision after MP-05 has produced real matches.

---

## MP-00: Command Queue At Tick Boundaries

**Owner:** Simulation. **Status:** half landed, 2026-09-03. Named "the largest structural change
remaining" in `MULTIPLAYER_SEATS_AND_AUTHORITY.md` and R4 in `OPEN_ITEMS.md`. Split in two
because the two halves carry very different risk: a full command log is additive and provable
today; deferred, multi-source ordering has no second command source to prove itself against
until MP-04 exists, and is the more invasive change to a 2000-line file this whole game already
runs on.

### Landed

- **A full command log.** Every command that mutates match state — `PlaceTower`, `SellTowerAt`
  (and the batch sells that share its private helper), `UpgradeTower` (and the batch upgrades
  that loop over it), `BuyCategoryTier`, and `QueueSend` — is recorded as a `RecordedCommand`
  (`src/LTW.Simulation/Replay/RecordedCommand.cs`) the instant it is accepted, with the fields
  needed to reissue it and a `Sequence` assigned in true call order. `EnqueueSend`,
  `CancelQueuedSend` and `ClearSendQueue` stay unrecorded, matching the send queue's existing
  documented split between intent and fact. `LocalVerticalSlice.GetMatchReplayRecord()` returns
  the log as a `MatchReplayRecord` — deliberately a new type, not an extension of `ReplayRecord`,
  which stays `ScenarioRunner`'s economy-only telemetry type and is untouched.
- **A working replay.** `LocalVerticalSlice.Replay(record, content, options)` reconstructs a
  match by reissuing every recorded command — bot-issued and human-issued alike, since both reach
  the match through the same public methods — against a fresh, bot-disabled instance, in
  `(Tick, Sequence)` order. `VerticalSliceSnapshot.Fingerprint()` is the new state-equality check
  this needed and MP-04 will need again: a hash over every player's economy and tiers, every
  tower, every creep.
- **Proof, not assertion:** `tests/LTW.Tests/MatchReplayTests.cs` drives a real eight-lane bot
  match, records it, replays it, and asserts the two fingerprints match. This is the first
  acceptance check below, passing.

### Not yet landed

- **Commands still apply the instant they are accepted**, exactly as before — this does NOT
  defer application to a tick boundary. There is only one call stack today (no second command
  source exists to race against), so `Sequence` already IS the canonical order; what is missing
  is the machinery that would still produce a canonical order once a second source (a network
  peer, in MP-04) can submit for the same tick. Building that now, before anything can exercise
  it, was judged higher risk than value: this file's own remarks stress how load-bearing the
  current call-order guarantees are (entity id assignment order feeds combat targeting), and a
  blind refactor of them without a second submitter to test against is a good way to introduce
  the exact non-determinism this initiative exists to remove.
- **Creep spawning still lives inside `QueueSend`**, not a separate tick-application step — there
  is no tick-application step yet to move it into.

### Acceptance Checks

- [x] A full eight-lane bot match replays from its `MatchReplayRecord` to an identical final
      state fingerprint (`MatchReplayTests.Full_eight_lane_bot_match_replays_to_an_identical_fingerprint`).
- [ ] Two commands for the same tick, submitted by two different SOURCES in opposite arrival
      order, produce identical matches. Not yet checkable — there is no second source until MP-04.
      `MatchReplayTests.Commands_in_the_same_tick_are_ordered_by_true_call_order` covers the
      single-source case today.
- [x] The send queue's "intent is not recorded, the send is" property still holds
      (`MatchReplayTests` records only realized sends; `EnqueueSend` et al. are untouched).
- [x] `dotnet test LTW.sln` green (343/343, +3) and `dotnet build` clean; match length and outcome
      unaffected, since nothing about WHEN a command applies changed.

### Remaining for MP-00 to close

- A real queue: commands submitted are buffered, then applied at the next `AdvanceOneTick` in
  `(tick, playerId, Sequence)` order, merging human/local submissions with bot-decided ones
  instead of applying both inline. Best sequenced alongside MP-03 (authority) or MP-04 (server),
  once a second submitter exists to prove the ordering against rather than assert it.
- Move creep spawning out of `QueueSend` into that application step.

**Estimate:** the landed half took under a day. The remaining half is still about a week, per the
original estimate — it did not get smaller, since the log and replay were the provable-and-safe
part, not the invasive part.

## MP-01: Seat Table

**Owner:** Simulation. **Status:** landed 2026-09-03, scoped to one human seat — see "Not yet
landed" below for what that leaves out. `LocalMatchOptions.LocalPlayerId`/`BotLaneOptions` are
UNCHANGED and still the actual source of truth; the table reads them rather than replacing them
(see Landed for why).

### Landed

- `Seats/SeatRole.cs`, `Seat.cs`, `SeatTable.cs`: a read model, not a new source of truth.
  `LocalVerticalSlice.GetSeatTable()` builds one fresh from `LocalMatchOptions` and the live
  `bots` dictionary — every seat is `Human` (today, only `LocalMatchOptions.LocalPlayerId`),
  `Bot` (has a live `BotController`), or `Empty` (neither). Deliberately NOT a replacement for
  `BotLaneOptions`/`LocalPlayerId`, which the original deliverable asked for: those are used
  throughout `LocalVerticalSlice`'s constructor, `CreateBots`, and every existing test, and
  migrating the actual source of truth was a materially bigger, riskier change than building the
  read model everything else in this initiative needs — the same call made in MP-00 about the
  deferred-apply queue. The seam is real (`GetSeatTable()` is the only place that would need to
  change), just not walked through yet.
- `Role` and `IsConnected` are separate: a dropped human seat stays `Human`, `IsConnected: false`
  — see `Seat`'s remarks for why a single richer enum would have made reconnect a role change
  instead of a flag flip.
- `LocalVerticalSlice.DropSeat(playerId)` / `ReconnectSeat(playerId)`: a connected human seat can
  be dropped (a `BotController` on `BotDecisionProfile.Balanced` stands in, added to `bots` the
  same way any bot is, from the next tick since `AdvanceOneTick` only consults `bots` once per
  tick) and reconnected (the stand-in is removed; the seat's identity never changed). Both are
  idempotent no-ops on a seat that is not eligible.
- `VerticalSliceSnapshot.SeatTable`: the table travels in the snapshot exactly like `SendQueues`
  does, with the same "absent means empty" convention for callers that do not care.
- Proof: `tests/LTW.Tests/SeatTableTests.cs`, four tests — roles match configuration including an
  explicit `Empty` seat; `WithAllBotsPassive` needs no special case; drop/reconnect round-trips;
  and the one that actually matters —
  `A_dropped_and_reconnected_seat_leaves_no_gap_a_replay_cannot_reproduce` drops the local seat
  mid-match, lets the stand-in play, reconnects it, and replays the whole match through MP-00's
  `LocalVerticalSlice.Replay` to an identical fingerprint. Drop/reconnect and full-match replay
  are proven to compose, not just each proven alone.

### Not yet landed

- **Only one human seat can ever exist.** `DropSeat`/`ReconnectSeat` only accept
  `options.LocalPlayerId`, because nothing else in the simulation can represent a second human
  identity yet — that is session and identity work (MP-05), not seat-table work. The acceptance
  check below asking for three simultaneous humans is not buildable without that, and was not
  attempted; forcing it here would have meant inventing session state this initiative was never
  scoped to own.
- **`BotLaneOptions`/`LocalMatchOptions` are still the actual source of truth**, per Landed above.

### Acceptance Checks

- [ ] A match started with three humans and five empty seats plays eight lanes with bots
      in the five. Not buildable yet — needs MP-05's multi-human identity, not seat-table work.
      What IS proven today: an eight-lane match with one human and any mix of bot/empty seats
      reports every role correctly (`Seat_roles_match_what_the_options_actually_configured`).
- [x] A human seat marked dropped at tick N is bot-driven at N+1 and human-driven again on
      reconnect, with no gap in commands on the replay
      (`A_dropped_and_reconnected_seat_leaves_no_gap_a_replay_cannot_reproduce`).
- [x] The Practice mode's all-passive bots (`WithAllBotsPassive`) is expressible as a seat table
      with no special case (`All_bots_passive_needs_no_special_case_in_the_seat_table`).

**Estimate:** the landed half took under a day, same story as MP-00 — the read-model-plus-tests
half is the safe, additive part. Replacing `BotLaneOptions` as the actual source of truth and
adding real multi-human identity are each their own remaining task, on the order of the original
three-to-four-day estimate combined.

## MP-02: Recorded Opponents

**Owner:** Simulation + Client. **Status:** simulation half landed 2026-09-03; client half not
started. The cheap "real opponents" step. Its original deliverable named a `ReplayRecord` slice
as the source — that type is send-only telemetry (see MP-00); this is built on `MatchReplayRecord`
instead, which did not exist when MP-02 was first drafted.

### Landed (Simulation)

- `Seats/RecordedSeatDriver.cs`: filters a `MatchReplayRecord` down to one source seat's commands,
  sorted by `(Tick, Sequence)`, and replays them onto a TARGET seat each tick through
  `IBotMatchContext` — the same narrow surface a live `BotController` acts through, so a recorded
  seat can never do anything a bot could not. `IBotMatchContext` gained `TrySellTower` for this: no
  live bot sells (R3 in OPEN_ITEMS.md), but a recorded HUMAN's play can include sells, and dropping
  them would replay an approximation, not that person's match.
- Lane re-homing, not reuse: a recorded command's `LaneId` named the SOURCE seat's home lane in
  ITS match, which is meaningless (often a different seat number entirely) in a new one. Grid
  `Position` IS reused as recorded — lane grids are lane-relative, confirmed directly from
  `GraphicsAuditCaptureRunner.PlaceLineups`, which places towers at identical (x, y) pairs across
  three different lanes.
- `LocalVerticalSlice.AssignRecordedOpponent(playerId, recording, sourcePlayerId, displayName)`:
  seats a ghost; rejects the local human's own seat; on a content-version mismatch, installs a
  live bot instead and emits `RecordedSeatFallbackEvent` (new), matching "falls back to a bot,
  silently, with a telemetry event" exactly.
- Exhaustion handled in `AdvanceOneTick`'s seat loop, unified across bots and recordings in ONE
  seat-number-ordered pass (not "every bot, then every recording") so a recorded seat never biases
  `NextEntityId()` assignment order differently than a bot in the same seat would have — the same
  determinism concern MP-00 already documents for the bot loop. A driver that reports nothing left
  to play is retired and replaced with a live bot in the same call.
- `SeatRole.Recorded` and `Seat.DisplayName`, so the seat table already carries what a client
  needs for "a named ghost, not a bot" — the CLIENT half itself (see Not yet landed).
- A real, found-by-testing fix along the way: `Reset()` never rebuilt `bots` at all before this —
  nothing had ever mutated it after construction until drop/reconnect (MP-01) and this initiative
  existed. A first version of this change only undid the specific seats it had touched and left
  them `Empty` after a reset; `Reset` now fully rebuilds `bots` from `CreateBots`, same as the
  constructor, which is what actually restores a stand-in seat to ITS OWN configuration rather
  than just removing the stand-in.
- Proof: `tests/LTW.Tests/RecordedSeatTests.cs`, five tests, including a full match played
  entirely by seven recorded seats to a real `MatchSummary` (probed empirically at tick 3849 for
  the reference seed, not guessed — OPEN_ITEMS.md already warns match length varies by an order of
  magnitude or more by seed), the version-mismatch fallback and its event, early exhaustion with
  correct hand-off timing, rejection on the local seat, and the `Reset` fix itself.

### Not yet landed

- **The client.** The seat table already reports `SeatRole.Recorded` and a `DisplayName`; nothing
  in Unity reads either yet. This is real, separate UI work (the seat/leaderboard views), not
  touched this pass.
- **A recording store.** `AssignRecordedOpponent` takes a `MatchReplayRecord` directly — there is
  no bundling, no fetch, no curation pipeline. Today a caller must already have one in memory
  (which is exactly what the tests do, by recording a reference match on the spot).
- **Curation itself.** No recordings have been captured, reviewed or shipped.

### Acceptance Checks

- [x] A match against seven recorded seats runs to a result with no server and no network
      (`A_match_of_seven_recorded_seats_runs_to_a_result_with_no_server_and_no_network`).
- [x] A recording made on a different content version falls back to a bot and reports it
      (`A_content_version_mismatch_falls_back_to_a_bot_and_reports_it`).
- [x] A recorded seat whose stream ends early is bot-filled from that tick
      (`A_recording_that_ends_early_is_bot_filled_from_that_tick`). Built and tested generically
      (a truncated recording), not specifically "its player was eliminated sooner in the source
      match" — elimination is one way a recording ends early, not the only one, and the driver
      does not need to know which.
- [ ] Ten testers cannot tell a curated-ghost match from a bot match in a blind session, or if
      they can, prefer it. Not checkable by automation, and not reachable yet regardless — it
      needs the client half and real curated recordings, neither of which exist.

**Estimate:** the simulation half landed in about a day. The client half (seat table UI) and
recording curation are each still real work, on the order of the original combined estimate.

## MP-03: Authority On Every Command

**Owner:** Simulation. **Status:** landed 2026-09-03 — seat authority on every command, and rate
limiting extended to the build family. See "Rate limiting" below for the one piece of the first
pass that turned out to be a real structural finding rather than an overcautious tuning worry.

### Landed

- `PlaceTower`, `QueueSend`, `BuyCategoryTier`, `UpgradeTower`, `SellTowerAt` and `SellTowers` all
  resolve their seat through `ISeatAuthority` and overwrite the id they were passed, via a new
  shared `TryResolveSeat` helper (`EnqueueSend`/`CancelQueuedSend`/`ClearSendQueue` already did
  this inline before this pass and were left as they were — already correct, not worth the diff
  to make them call the new helper too).
- Resolved BEFORE any lookup that uses the claimed id, not after: `UpgradeTower`'s and
  `SellTowerAt`'s own tower lookups filter by `OwnerId.Equals(playerId)`, so resolving too late
  would let a forged claim select an object it does not own and only get caught at the point of
  mutation — or worse, if the resolved id were substituted in AFTER an object was already chosen
  by the false claim, a command could act on one seat's object while crediting another's economy.
  `SellTowers` needed its own explicit check for this reason: it calls the same private `SellTower`
  helper `SellTowerAt` does, but reaches it through a DIFFERENT lookup (`OwnedTowersAt`), so fixing
  only `SellTowerAt` would have left the batch path exposed.
- `LocalVerticalSlice`'s constructor now accepts an optional `ISeatAuthority`, defaulting to
  today's `LocalSeatAuthority` for every existing caller — the seam the class's own pre-existing
  comment already named ("nothing yet has anywhere to inject from"). `CommandAuthorityTests` uses
  it to inject a seat authority that DENIES one seat and one that REMAPS a claim to a different
  seat entirely, and confirms every command actually acts as the resolved identity, not the claim
  — the one property this whole initiative exists for.
- `tests/LTW.Tests/CommandAuthorityTests.cs`: the invariants suite, one test per rule below.

### Rate limiting: landed, once the real risk in it was actually named

The first pass here deferred rate limiting beyond the send queue, on two concerns: `QueueSend` is
called both as a direct request AND internally by `DrainSendQueues` to realize an already-queued
send, and the build-family commands (`PlaceTower`, `UpgradeTower`, `BuyCategoryTier`,
`SellTowerAt`) are called by humans, live bots, AND (since MP-02) `RecordedSeatDriver` through the
same public methods, with no way to tell a request from a trusted in-process replay. The owner's
response: a bot or a recording needing throttled during a legitimate burst is itself a bug worth
finding and fixing, not a reason to leave the whole family unlimited. That reframed the two
concerns differently:

- **The `QueueSend` double-invocation was a real structural problem, not just a tuning question**,
  and stayed one under that framing too — throttling `DrainSendQueues`'s own realization step would
  still strand a queue behind a throttle unrelated to its rule (gold, not pacing), REGARDLESS of
  how generously the limiter were sized, because a large-enough queue draining in one gold-tick
  would eventually outrun any fixed burst. So `QueueSend` is now split: a public, rate-limited
  wrapper (sharing `enqueueRateLimiter`'s budget with `EnqueueSend`, so bypassing the enqueue path
  buys no extra allowance) and a private `QueueSendCore` with no authority or rate check at all,
  which `DrainSendQueues` calls directly. Both of `QueueSendCore`'s callers already carry a
  trustworthy, already-charged id by construction — the public wrapper just resolved and charged
  it, and `DrainSendQueues` only ever sees keys `EnqueueSend` put there after doing the same.
- **The build-family concern WAS just a tuning question**, and the fix is what the deliverable
  asked for all along: a shared `buildRateLimiter` (`TokenBucketRateLimiter`'s existing 240-burst,
  2-per-tick defaults — the same numbers already proven against the send family's own largest
  legitimate burst) now gates `PlaceTower`, `UpgradeTower`, `BuyCategoryTier`, `SellTowerAt`, and
  `SellTowers` (one token per tower actually touched in a batch, matching what requesting each
  individually would have cost). The empirical check is the existing suite, not a guess: full
  eight-lane bot matches running thousands of ticks, MP-02's recorded-seat replays, and MP-00's
  full-match replay all still pass unchanged — nothing about normal bot, human or ghost play comes
  close to the budget. Per-command in the literal sense (five isolated buckets) was not the
  interpretation taken — see the field's own remarks for why one shared budget per FAMILY is the
  stronger property, not a weaker one: it is what stops a client multiplying its allowance by
  spreading requests across different build commands.

### Rules an authoritative server must keep enforcing

Carried from `MULTIPLAYER_SEATS_AND_AUTHORITY.md`; each is an acceptance check here:

- [x] A player may only build in their own home lane (`A_player_may_only_build_in_their_own_home_lane`
      — pre-existing rule, verified it still runs against the resolved seat).
- [x] Send targets are derived from topology, never accepted from the client
      (`Send_targets_come_from_topology_and_cannot_be_supplied_by_a_caller`).
- [x] Send cadence is rate-limited per player; queue depth is capped per creep
      (`Queue_depth_is_capped_per_creep`; cadence limiting is the pre-existing `EnqueueSend` path).
- [x] Unknown or out-of-match player ids are rejected, never thrown on
      (`Unknown_or_out_of_match_ids_are_rejected_not_thrown`).
- [x] Gold, income, lives, bounties and leak results are server-owned; clients submit intent only
      (structural: no setter exists outside the command methods themselves —
      `Gold_income_and_lives_have_no_public_setter_only_validated_commands`).
- [x] Every command path, not only enqueue, resolves its seat through the authority
      (`A_denied_seat_cannot_place_upgrade_buy_sell_or_send`,
      `A_command_acts_as_the_resolved_seat_not_the_claim`).

**Estimate:** landed in under a day, including the `QueueSend` split.

## MP-04: Match Server

**Owner:** Server. **Status:** landed 2026-09-03, proven over a real (loopback) WebSocket
transport — not a real deployment, which this environment cannot produce. See "Not provable here"
below for exactly the line between the two.

### Landed

- `LTW.MatchServer` (new project, in `LTW.sln`) and `LTW.MatchServer.Tests` alongside it. Transport
  is `System.Net.HttpListener` + `System.Net.WebSockets` — base class library, not a new
  dependency; `MVP_DEPENDENCIES.md` rule 4 ("prefer small, well-supported dependencies over
  framework stacks") is why a full web framework was not reached for.
- `MatchRegistry` hosts N isolated matches by id (`Dictionary<string, ServerMatch>`); `POST
  /matches` creates one and returns per-seat join tokens, `GET /matches/{id}/join?seat=N&token=T`
  upgrades to a WebSocket bound to seat N. No matchmaking, no accounts beyond the token — exactly
  MP-04's own "private match" scope.
- `ConnectionSeatAuthority`: the server-side `ISeatAuthority` MULTIPLAYER_SEATS_AND_AUTHORITY.md
  asks for, answering "is this the seat on THIS CONNECTION" rather than "is this seat in the
  match". Wire messages carry no seat field at all — there is nothing for a client to spoof, which
  is a stronger property than the acceptance check below asks for, not a weaker one.
- `TickMessage`: one message per tick (events + a player/tower snapshot together), not one send
  per event plus a separate snapshot — see "What broke" for why this shape won it over the first
  one.
- Server-side replay capture: `LocalVerticalSlice.GetMatchReplayRecord()` (MP-00) written to
  `replays/{matchId}.json` the instant a match ends.
- Proof: `tests/LTW.MatchServer.Tests`, three tests — a two-human/six-bot match with a real
  placement round-trip, an invalid token refused, and a full one-human/seven-bot match played to
  an actual `MatchEndedEvent` over the real transport, at a tick rate raised only so the test
  finishes in seconds instead of the six-plus real-time minutes the default rate would take (see
  "What broke" for why the rate needed to be adjustable at all).

### What broke, and what it forced fixing

The first version of `ConnectionSeatAuthority` resolved a seat ONLY when a client connection was
bound to it — correct for a dispatched client command, and silently wrong for everything else.
Bots and (once wired) `RecordedSeatDriver` never dispatch through a connection at all; they call
`LocalVerticalSlice`'s command methods directly, the same way they do locally. Under the first
version, EVERY bot-issued command on the server was rejected by the seat authority, with nothing
in the protocol surfacing it as more than a match that never seemed to build anything. It was
caught by the `Eight_lane_match_of_one_human_and_seven_bots_runs_to_a_result` test simply
refusing to reach a result — not a targeted check for the bug, just the test that would only ever
pass if the whole path actually worked. `ConnectionSeatAuthority` now trusts the claimed seat
outright whenever no client request is in flight (the same trust `LocalSeatAuthority` gives every
caller locally), and a request is "in flight" only between `BeginClientRequest` and `EndRequest`
around one dispatched command.

That fix exposed a second, more fundamental issue underneath it: `ConnectionSeatAuthority` carries
which connection is calling as MUTABLE AMBIENT STATE, which is only a correct design if nothing
else can observe or change it concurrently — and nothing enforced that. The tick loop's own
`AdvanceOneTick()` (running every bot's turn) and a client's dispatched command are two independent
async paths with no serialization between them by default; interleaved, one could resolve against
the other's connection, or against none. `ServerMatch` now holds one `SemaphoreSlim` across both
paths, so a tick's bot decisions and a dispatched command can never interleave — which is also, not
incidentally, the same guarantee `LocalVerticalSlice` already needed anyway, since it was never
thread-safe to begin with. Neither of these was found by reasoning about the design beforehand;
both were found by an integration test that measured the actual outcome instead of assuming the
mechanism worked because the pieces compiled.

**A "throughput ceiling" reported earlier in this pass was wrong, and is corrected here rather
than left standing.** While bots could not act at all (the bug above), a match's tick loop ran
for a long time without ever producing a `MatchEndedEvent`, and the delivered-messages-per-second
figure measured across that non-terminating run was reported as a transport ceiling — "~34
ticks/second regardless of requested rate." It was never re-measured after the actual bug (bots
rejected, not the transport) was fixed, so it went into this document unverified. Re-measured
directly, with server-side send timing and client-side inter-arrival gaps instrumented on both
ends of the same loopback connection: a single `WebSocket.SendAsync` call takes about 0.01-0.04ms;
steady-state message delivery lands within a millisecond or two of the requested tick interval
(500 requested/second measured at roughly 440/second sustained, after a one-time ~150-200ms
connection-establishment cost); and the passing `Eight_lane_match_of_one_human_and_seven_bots_runs_to_a_result`
test's own numbers, recomputed, show 3849 messages delivered in 20 seconds against a 200/second
request — about 192/second, not 34. There is no meaningful transport bottleneck on loopback at the
rates this project ships at. `TickMessage`'s per-tick batching (one message per tick, not one send
per event plus a separate snapshot) is still worth keeping — it is simply better protocol design,
not the fix a real ceiling needed. What this pass has NOT measured, and should not be assumed from
the above, is many CONCURRENT matches on one process, or anything about a real (non-loopback)
network — both are MP-07 (operations) territory.

### Not provable here

- **Two real devices on two real networks.** This environment has no second device and no network
  path between two of them — only loopback. The transport, protocol, and authority are the same
  code that would run in that configuration; nothing about crossing a real network is exercised.
- **A regional container.** No deployment target exists to try one against.
- **Bandwidth measurement.** Tick COST is measured (see above); bytes on the wire are not — that
  needs a real client (Unity, not a test harness) producing realistic message volume and a real
  network path to measure loss and jitter against, neither of which exist here.
- **Rate-limit throttling logged over the wire.** MP-03's rate limiters are real and tested against
  `LocalVerticalSlice` directly (`CommandAuthorityTests`, in `LTW.Tests`); nothing in
  `LTW.MatchServer` yet turns a `CooldownActive` rejection into a distinct log line a real
  operator would search for — that is MP-07 (operations) territory, not this initiative's.

### Acceptance Checks

- [ ] Two phones on different networks complete a private eight-lane match (two humans, six bots)
      hosted on one regional container. Not provable in this environment — see above. What IS
      proven: the identical protocol and match lifecycle, two real client connections, over a real
      WebSocket transport, to a real result (`Two_humans_and_six_bots_complete_a_private_match_with_no_matchmaking`,
      `Eight_lane_match_of_one_human_and_seven_bots_runs_to_a_result`).
- [x] The server's replay of that match reproduces its final snapshot — MP-00's
      `LocalVerticalSlice.Replay` already proves this against the identical
      `MatchReplayRecord` the server now writes to disk; not re-proven a second way here, since it
      would be testing MP-00 again, not MP-04. "A client's local view never diverges from the wire
      by more than one tick" is not checked — there is no non-test client yet to diverge.
- [x] A client sending a spoofed `PlayerId` acts as its own seat — stronger than asked: the wire
      protocol has no `PlayerId` field for a client to put a claim into at all, so a "spoofed"
      value is structurally impossible, not merely rejected. Rate-limit throttling is real
      (MP-03) but not yet logged as a distinct server event — see "Not provable here."
- [x] Bandwidth and tick cost per match are measured and recorded in this document — tick cost is:
      no meaningful transport bottleneck at shipped tick rates on loopback (a single send takes
      hundredths of a millisecond; sustained delivery tracks the requested rate within a
      millisecond or two once connected — see "What broke" for the earlier, wrong number this
      corrects). Bandwidth is not measured, for the reason above.

**Estimate:** landed in about a day, faster than the two-week estimate because the estimate
priced in exactly the kind of integration surprise that landed instead of lurking — the two real
bugs above were each found and fixed in minutes once a real end-to-end test could fail
informatively, which is the case for building the real thing early rather than deferring it.

## MP-05: Session, Identity, Lobby, Matchmaking

**Owner:** Server + Client. **Status:** architecture decided 2026-09-03 (PlayFab); PlayFab Studio
and Title `FBC34` created 2026-09-03, Google Sign-In configured and active 2026-09-04. The
server-side identity check is built, wired into the join path, and confirmed end to end against
that real title — see "Landed and confirmed against a real title" below. Still blocked on Sign in
with Apple (needs the Apple Developer Program enrollment first — see "Before any of this can
start", step 2) before a real player can sign in with either provider on iOS; Google sign-in has
nothing further blocking it server-side.

### The architecture, now that identity is decided

PlayFab owns identity, matchmaking and the lobby. `LTW.MatchServer` keeps owning the actual match
— PlayFab's own docs call this a "custom game server" integration, and it is the same split
`ARCHITECTURE.md` already sketched (a separate matchmaking service handing off to an authoritative
match instance), just with PlayFab supplying the first half instead of it being hand-built:

```text
Unity client                         PlayFab                          LTW.MatchServer
  |  Sign in with Apple/Google          |                                    |
  |------------------------------------>|                                    |
  |  <--- PlayFab session ticket -------|                                    |
  |                                     |                                    |
  |  create/join a matchmaking ticket   |                                    |
  |------------------------------------>|                                    |
  |  <--- match found: connect info ----|                                    |
  |                                     |                                    |
  |  join (session ticket, not a bespoke join token) -------------------------------------->|
  |                                     |   AuthenticateSessionTicket        |
  |                                     |<------------------------------------|
  |                                     |--- PlayFabId, verified ------------>|
  |  <---------------------------------------------------------- WebSocket bound to a seat --|
```

The MP-04 join-token scheme (a per-seat GUID handed back from `POST /matches`) does not disappear
— it is what a private, non-matchmade match (a rematch, a friend invite) still uses. PlayFab
identity and tokens are two different ways to prove "you are seat N," not a replacement of one by
the other; `ConnectionSeatAuthority` only cares that SOME check ran before `BindConnection`, not
which one.

### Before any of this can start

Nothing below is buildable without external setup only the owner can do — the same shape as
"enrol in the Apple Developer Program" was for the launch roadmap:

1. ~~**Create a PlayFab Studio and Title** in Game Manager (Microsoft/Azure account).~~ Done
   2026-09-03 — Title ID `FBC34`. This produced the Title ID and Secret Key everything else needs.
2. **Configure Sign in with Apple and Google Sign-In as PlayFab identity providers** for that
   title:
   - ~~Google Sign-In~~ Done 2026-09-04 — a Web-application OAuth client created in Google Cloud
     Console, Client ID/Secret entered into PlayFab's Google add-on, status Active.
   - Sign in with Apple — still blocked on the Apple Developer Program enrollment
     `docs/STORE_SIGNING_PREREQUISITES.md` tracks for store distribution (not yet done as of this
     writing); needs an App ID capability, a Services ID, and a private key before PlayFab's Apple
     add-on can be configured. See `docs/PLAYFAB_SETUP.md` Section 2.
3. ~~**Decide a data-residency region** for the title.~~ Checked 2026-09-04 and retracted: this
   was an assumption written into this doc without verifying PlayFab actually exposes a
   user-facing region/data-residency control for a standard title — Game Manager for `FBC34` has
   no such setting visible, and no current PlayFab documentation confirms one exists outside
   Enterprise-tier titles. Not treating this as a real blocker; if it turns out to matter later
   (e.g. for a privacy policy once real accounts exist), it needs re-investigating then, not now.

### Deliverables

- Unity: PlayFab wired to Sign in with Apple/Google, replacing no existing local-play code path —
  Practice and a private rematch stay fully local, per MP-06's own "the tutorial and Practice stay
  local" deliverable. **Not** the v2 Unified SDK originally assumed here — see "Landed (client)"
  below for why the older, plain-REST `PlayFabClientAPI` was the right call instead.
- Server: `LTW.MatchServer` verifies a joining connection's PlayFab session ticket via PlayFab's
  Server API (`AuthenticateSessionTicket`) before `ConnectionSeatAuthority.BindConnection` — landed
  this pass, see below.
- Server: a matchmaking-result handler — when PlayFab reports a filled ticket, `MatchRegistry`
  creates the match (as it does today for a private one) and the seats matched by PlayFab connect
  using their PlayFab identity instead of a join token.
- Lobby: create, invite, ready-up, start; the seat table (MP-01) is the lobby's state — this seam
  already exists, since `SeatTable` was never local-only.
- Matchmaking: a regional queue that fills a match with humans as available and bots for the rest
  after a bounded wait, because a new game's population will not fill eight seats.
- Durable results: written to PlayFab player data (its own store) rather than a new database this
  project would have to run and back up itself — the same "prefer small, well-supported
  dependencies" reasoning that picked PlayFab over hand-building matchmaking in the first place.

### Landed (everything buildable without live credentials)

- `PlayFabSessionAuthority` (new, `LTW.MatchServer/PlayFab/`): calls PlayFab's Server API
  `AuthenticateSessionTicket` over plain `HttpClient` — no PlayFab NuGet SDK, matching MP-04's own
  "base class library over a framework stack" choice for a single REST endpoint. Given a session
  ticket, returns the verified `PlayFabId` or null; a network failure or a PlayFab-side rejection
  both resolve to null, never an exception a join handler would have to remember to catch.
- Wired into the join path: `MatchRegistry.CreateMatch` takes an optional seat-number-to-PlayFabId
  map, `ServerMatch.AcceptWithPlayFabAsync` verifies a joining connection's session ticket against
  the SPECIFIC PlayFabId reserved for that seat (a valid ticket for the wrong player is rejected,
  not just an invalid ticket), and `HttpMatchHost`'s join route accepts `playFabTicket=` as an
  alternative to `token=` — the two schemes coexist per the architecture above. `Program.cs` reads
  `PLAYFAB_TITLE_ID`/`PLAYFAB_SECRET_KEY` from the environment only (never a CLI argument or config
  file, so the secret never lands in shell history) and leaves PlayFab-identified seats refusing
  every join, with a startup log line saying so, when they are unset.
- Tested against a fake `HttpMessageHandler` returning PlayFab's own documented response shapes, at
  two levels: `PlayFabSessionAuthorityTests` proves the class's own request/response handling, and
  `PlayFabJoinTests` proves the join path end to end over a real `HttpListener` and `ClientWebSocket`
  — a matching verified PlayFabId is accepted, a verified-but-different one is refused, a ticket
  PlayFab itself rejects is refused, and a seat never reserved for PlayFab refuses a PlayFab ticket
  even when that ticket would otherwise verify. What none of this proves: that a REAL PlayFab title
  responds the same way as the fakes — that needs step 1 above.
- **A real, non-hypothetical bug this testing caught:** `PlayFabSessionAuthority` was serializing
  the request body with the shared `JsonSerializerOptions(JsonSerializerDefaults.Web)`, which
  camelCases property names by default — so the request sent `{"sessionTicket": ...}` instead of
  the PascalCase `{"SessionTicket": ...}` PlayFab's real API documents. The original unit test's
  assertion checked for that key case-insensitively, so it passed anyway. `PlayFabJoinTests`'s fake
  handler parses the body the way a real API would (case-sensitively) and threw immediately,
  surfacing the bug. Fixed by annotating the record's property with an explicit
  `[JsonPropertyName("SessionTicket")]`, and the unit test's assertion was tightened to
  case-sensitive so this class of bug cannot silently reappear. This would have failed against
  every real PlayFab title from the first connection attempt had it shipped unnoticed.
- One related hardening from the same pass: `HttpMatchHost.HandleJoinAsync` now wraps the
  `AcceptAsync`/`AcceptWithPlayFabAsync` call in a try/catch that closes the socket on any
  unexpected exception. The WebSocket upgrade has already happened by that point in the request, so
  an unhandled exception there cannot become an HTTP error response — without this, a surprise
  failure during verification left the connecting client's `ClientWebSocket` waiting forever instead
  of being refused.

### Landed and confirmed against a real title (2026-09-03)

Step 1 of "Before any of this can start" is done — a real PlayFab Studio and Title (`FBC34`) exist.
That made it possible to prove the last thing the fake-backed tests above could not:

- `LTW.MatchServer` run locally with `PLAYFAB_TITLE_ID`/`PLAYFAB_SECRET_KEY` pointed at the real
  title, then exercised with real session tickets obtained from PlayFab's own Server API
  (`LoginWithServerCustomId` — a server-authenticated test login that needed no client-side account
  creation setting enabled). Result: a real ticket for the PlayFabId a seat was reserved for is
  accepted and bound; a real, valid ticket for a *different* PlayFabId is rejected. Both directions
  now proven against PlayFab itself, not a fake.
- **A second real bug this surfaced, this time in the join URL, not the server:** a genuine PlayFab
  session ticket is base64-shaped and contains `+` and `=` characters. Placing one directly into a
  query string without percent-encoding it lets an unencoded `+` get silently decoded server-side as
  a space — corrupting the ticket before `AuthenticateSessionTicket` ever sees it, which then
  (correctly) reports it invalid. `HttpMatchHost` itself is fine here — `HttpListener`'s query-string
  parsing decodes correctly; the bug is entirely on whoever *builds* the join URL. Nothing in this
  repo builds that URL yet (MP-04's join tokens are GUIDs, which never contain such characters, so
  this never came up before) — but MP-06's Unity client will be the first real code to do so, and
  must call the equivalent of `Uri.EscapeDataString`/`UnityWebRequest.EscapeURL` on the ticket before
  appending it to the join URL. Recorded here so that code doesn't repeat this by hand.
- Two real player records now exist in the live title from this testing (`ServerCustomId`s
  `ltw-local-test-1`/`-2`) — harmless test accounts, not cleaned up since PlayFab has no destructive
  "delete player" step worth scripting for two rows, but worth knowing they're there if the title's
  player list looks non-empty later.

### Landed (client) — confirmed on a real iOS device, 2026-09-04

Scoped deliberately to iOS-only Google sign-in for now (Android needs a whole separate Google Play
Games Services setup — see the scope decision below). This is now proven, not just written: a real
device build, a real Google account, a real PlayFab session ticket, title `FBC34`, button reads
SIGNED IN. Three real bugs surfaced getting there, each is its own entry below.

- **SDK choice reversed from what this doc originally assumed.** The new "v2 Unified" PlayFab
  Unity SDK's own generated docs mark `AuthenticationLoginWithGoogleAccountAsync` as "available on
  Android" — its native core wraps Android's SDK internally rather than being a generic REST call
  a caller can feed any server auth code into. That makes it unusable for the iOS flow this needs.
  The older `PlayFab/UnitySDK` (`PlayFabClientAPI`, plain HTTP via `UnityWebRequest`, no platform
  restriction, still actively maintained — last commit 2026-08-07) is vendored instead, under
  `unity/LTW.UnityClient/Assets/ThirdParty/PlayFabSDK/`. **Vendored in full, not just Shared +
  Client as first attempted** — a real iOS Xcode export (which an Editor-only compile check does
  not equivalently exercise) revealed `Shared/Public/PlayFabEvents.cs` cross-references model types
  from every API category, not just Client; this SDK is not designed to be split by category
  despite the folder layout suggesting otherwise. `LoginWithGoogleAccount` on this SDK is a plain
  POST to `/Client/LoginWithGoogleAccount` — the exact same call `PlayFabSessionAuthority` verifies
  server-side, just issued from Unity instead of a REST client.
- **The official Unity Google Sign-In plugin (`google-signin-unity`) is archived** (April 2026,
  confirmed directly from the repo) — for both platforms, not just Android. There is no maintained
  off-the-shelf path any more, so `Assets/Plugins/iOS/LTWGoogleSignInBridge.mm` is a small
  hand-written native bridge to Google's still-current `GoogleSignIn-iOS` SDK
  (`signInWithPresentingViewController:completion:`, reading `GIDSignInResult.serverAuthCode` —
  verified against Google's current iOS docs while writing this, not assumed from memory), called
  from C# via `UnitySendMessage`. `com.google.external-dependency-manager` (EDM4U) was added to
  `Packages/manifest.json` as a git-URL UPM package (same pattern already used for `mcp-unity`) so
  its iOS Resolver links the `GoogleSignIn` CocoaPod into the exported Xcode project automatically —
  declared in `Assets/ThirdParty/GoogleSignIniOS/Editor/GoogleSignInDependencies.xml`.
- **First real bug, found on the first device test: "The user canceled the sign-in flow," even
  though the user completed the Google sign-in screen.** Missing piece: nothing forwarded the OAuth
  redirect URL (opened via the custom URL scheme Info.plist declares) back to `GIDSignIn`, so its
  completion handler eventually gave up and reported cancellation. Fixed properly, not by
  swizzling: `UnityAppController.mm` already posts a `kUnityOnOpenURL` notification to any
  registered `AppDelegateListener` specifically so third-party plugins can hook this without
  touching Unity's own generated code (which gets regenerated on every export anyway) — see
  `Assets/Plugins/iOS/LTWGoogleSignInUrlHandler.mm`, a `__attribute__((constructor))`-registered
  listener that forwards the URL to `[GIDSignIn.sharedInstance handleURL:url]`.
- **Second real bug, found on the next device test: PlayFab's own server rejected the login with
  `redirect_uri_mismatch`.** PlayFab's backend exchanges the server auth code with Google using a
  FIXED, PlayFab-hosted redirect URI (`https://oauth.playfab.com/oauth2/google` — the same for
  every PlayFab title, not title-specific), which has to be explicitly added to the Web-application
  OAuth client's Authorized redirect URIs in Google Cloud Console. Not something any code change
  could fix — pure Google Cloud Console configuration, now recorded in `docs/PLAYFAB_SETUP.md`.
- **A second Google OAuth client was needed beyond the Web-application one — done.** The
  Web-application client from Google Sign-In setup covers `GIDServerClientID` (server-side
  verification via PlayFab). Native iOS sign-in also needed a separate **iOS-type** OAuth client
  (`GIDClientID`, tied to the Bundle ID `com.ltwplaceholder.ltw`), created 2026-09-04. Both are
  filled into `Assets/Scripts/Online/GoogleSignInIOSConfig.cs` (`GoogleSignInPostProcessBuild.cs`
  still carries an `IsConfigured` check that logs a clear warning and skips Info.plist injection if
  either is ever a placeholder again, rather than silently shipping a broken build).
- **The client-side pieces**: `PlayFabConfig` (sets `PlayFabSettings.TitleId` in code, no inspector
  asset), `PlayFabSession` (static holder for the signed-in `PlayFabId`/`SessionTicket` — the
  session ticket is exactly what a future join would need to percent-encode into the query string,
  per the bug noted above), `IGoogleSignInProvider`/`GoogleSignInIOS`/`GoogleSignInReceiver` (the
  native bridge's C# side), and `PlayFabLoginService` (wires a provider's server auth code into
  `LoginWithGoogleAccount`). A "SIGN IN WITH GOOGLE" button was added to the title screen
  (`ShellScreenView`/`ShellScreens.uxml`) — wired directly rather than through
  `IShellScreenActions`, the same way HOW TO PLAY is, since identity is not a session/match action.
- **What this does NOT do**: join an actual match. That is MP-06's job, and does not exist client-
  side yet (no `ClientWebSocket` layer in Unity at all). This pass only proves identity end to end;
  `PlayFabSession.SessionTicket` sits unused until MP-06 exists to consume it.
- **Scope decision**: Android Google sign-in was explicitly deferred, not merely postponed. It
  needs a Google Play Console app entry (a real, separate cost — one-time $25 — from what's paid so
  far), Google Play Games Services configuration, an Android-type OAuth client tied to a signing
  SHA-1, and PlayFab's *different* `LoginWithGooglePlayGamesServices` identity path, since Google
  deprecated the classic Google Sign-In SDK for Android in February 2025. None of that is started.

### Real matchmaking, landed 2026-09-05 — pools opportunistically, no invite mechanism

Explicit product direction: no deliberate "invite my friends" feature. Players queue; if another
real player happens to be queuing at the same time, PlayFab pools them into the same match (bots
fill whatever seats are left). If nobody else shows up within a bounded wait, fall back to the
already-proven solo-vs-bots direct request from MP-07 — same server, same experience, no visible
difference to that player.

A hard PlayFab platform constraint shapes this, confirmed directly from Microsoft's docs, not
assumed: **a matchmaking queue's minimum match size must always be ≥ 2 — a lone ticket can never be
matched by itself, no matter how long it waits.** That makes the solo-fallback path mandatory, not
a convenience.

- **Server (`Program.cs`)**: a queue's `ServerAllocationEnabled` auto-allocation gives the server no
  `SessionCookie` of its own (nothing ever called `RequestMultiplayerServer` directly to set one) —
  confirmed from PlayFab's own docs that the matched players instead come through
  `GameserverSDK.GetInitialPlayers()`. New `QueuedMatchBootstrap.AssignSeatsFromInitialPlayers`
  (pure, GSDK-free, unit-tested) turns that list into the same `humanSeats`/`playFabIdBySeat` shape
  a direct request's `SessionCookie` already provides — `MatchRegistry.CreateMatch` needed zero
  changes, since that shape was deliberately built matchmaking-agnostic from the start (MP-04/MP-05's
  own design note). `Program.cs` forks on whether `SessionCookie` actually deserialized anything —
  present and populated means a direct request (today's path, unchanged); empty means
  queue-allocated, falling back to `GetInitialPlayers()`.
- **`HttpMatchHost` gains a `current` join alias** (`GET /matches/current/join?...`), resolving to
  "the only match this registry holds" — offered only when match creation is disallowed (MPS mode,
  which already guarantees exactly one match per process). Exists specifically because a
  queue-matched client only ever learns PlayFab's own `MatchId` (from `GetMatch`), which is not
  confirmed to equal this registry's internal match id — rather than gambling on that equivalence,
  "current" sidesteps needing it at all.
- **Client (`OnlineMatchService.cs`)**: new `QueueForMatchAsync` — creates a matchmaking ticket
  (`CreateMatchmakingTicket`, reusing the same `GetEntityTokenAsync` entity-token plumbing MP-07's
  `RequestServerAsync` already built), polls `GetMatchmakingTicket` every 0.5s, and on `Matched`
  resolves the allocated server via `GetMatch` and joins using the new `current` alias. On
  `Canceled` (the ticket's own 25-second `GiveUpAfterSeconds` elapsed with nobody else around — the
  expected common case at this population), falls back to the existing `RequestServerAsync` path
  unchanged. `RequestServerAsync` itself is untouched — it's now `QueueForMatchAsync`'s fallback
  callee rather than being called directly from `CreateAndJoinAsync`.
- **Not yet done**: the actual PlayFab Game Manager queue (name, `MinMatchSize: 2`/`MaxMatchSize: 8`,
  `ServerAllocationEnabled` tied to MP-07's `BuildId`) hasn't been created — portal work, needing
  MP-07's `BuildId` to exist first (see Phase 5 below: the Dasv4 quota is now approved and a build
  is provisioning, but no `BuildId` is recorded yet). Live verification (two real identities
  queuing together, and the solo-timeout-fallback path) needs that queue plus a real allocatable
  server. `dotnet test` (388/388) and Unity batchmode compile (0 `error CS`) are what's verified so
  far — matching this project's own established limit for GSDK/PlayFab-network code that can't be
  unit-tested without a real or simulated agent.

### Acceptance Checks

- [ ] A player queues alone and is in a match within the bounded wait, against bots. Code landed
      2026-09-05 (see above); live verification blocked on the same quota approval as MP-07.
- [ ] Four players queuing together land in one match with four bot seats. Reframed per explicit
      product direction — see above: players pool opportunistically if queuing concurrently, there
      is no deliberate "queue together" invite mechanism. Code landed 2026-09-05; live verification
      blocked on the same quota approval as MP-07.
- [ ] Results survive a client crash and a server restart.
- [x] Every third-party service used is in `MVP_DEPENDENCIES.md` — PlayFab is recorded there now.
- [x] A PlayFab session ticket claims the seat it was verified for, and only that seat — proven
      against a fake PlayFab response (`PlayFabJoinTests`) AND against the real title (`FBC34`).
- [x] A PlayFab session ticket authenticates a real connection end to end against a live title —
      confirmed 2026-09-03 against title `FBC34`, both the matching-seat and wrong-PlayFabId cases.
- [x] The Unity client can obtain a real PlayFab session ticket via Sign in with Google, on a real
      iOS device. Confirmed 2026-09-04: a real device build, a real Google account, PlayFab title
      `FBC34` — the title screen's button reads SIGNED IN after completing the flow.
- [ ] Same, for Android via Google Play Games Services. Not started — see the scope decision above.

**Estimate:** two to three weeks of engineering, once the external setup above is done. The
identity DECISION is no longer the dominating factor it was when this estimate was first written;
the PlayFab account/title setup and the Apple/Google identity-provider configuration inside it are.

## MP-06: Client Over The Wire

**Owner:** Client. **Status:** core loop built 2026-09-04 — a real match can be joined, played,
and rendered entirely from the wire. Compiles cleanly; NOT yet exercised on a real device against
a real running match (see "What this has NOT proven" below).

### Deliverables

- The HUD, rails, board and results render from the wire only; no code path reads the
  simulation directly when a server is present.
- Latency handling: input acknowledgement within a frame, authoritative correction without
  visible snapping for the common case.
- Reconnect UX: a network hole shows a state, not a freeze; a reconnect resumes the seat.
- The tutorial and Practice stay local; they never touch the server.

### Landed

- **`TickMessage` now carries creeps.** MP-04 deliberately scoped this out ("client rendering
  fidelity is Unity's concern, not this initiative's") — correct for proving the transport, but a
  client cannot render an actual match on Players/Towers alone. `CreepSnapshotDto`
  (`LTW.MatchServer/Wire/ServerMessages.cs`) mirrors `LTW.Simulation.Combat.CreepPresentationSnapshot`
  field for field, including `EffectiveMovementCost` (the brake-adjusted value, so a client never
  has to know the bramble penalty constant to place a creep between cells correctly) — the same
  shape the LOCAL renderer already consumes, so a wire-based renderer and Practice's renderer can
  eventually share interpolation logic instead of each inventing its own.
- Proven with a real test, not just a compile check: `Tick_messages_carry_creep_positions_once_bots_start_sending`
  joins a real match and waits for bots to actually send creeps, then asserts on the real wire
  shape — not a fake or a mocked snapshot.
- In passing: a stale "34 delivered ticks/second regardless of requested rate" comment survived in
  `MatchServerIntegrationTests.cs` after the actual doc correction (see MP-04's own "What broke")
  — the number was fixed there but this comment, a second copy of the same wrong claim, was missed.
  Corrected here too.

### Landed (client): a real match, joinable and playable from the wire

Investigated first, before writing any client code: `unity/LTW.UnityClient/Assets/Scripts/Simulation/`'s
existing local pipeline had NO abstraction between "a live `LocalVerticalSlice`" and the 8+ scripts
that read `UnitySimulationDriver`'s public surface directly (`UnityVerticalSliceRenderer`,
`HudView`, `SeatLeaderboardView`, `ShellScreenView`, `TouchPlacementController`, and others). Rather
than introduce a new interface and rewire all of them, `UnitySimulationDriver` and
`UnityCommandAdapter` were given a second, wire-backed mode internally — every existing consumer is
completely unchanged.

- **`Assets/Scripts/Online/Wire/`**: `ClientWireMessages.cs`/`ServerWireMessages.cs`, plain
  Newtonsoft-serializable classes mirroring `LTW.MatchServer/Wire/`'s DTOs field for field (added
  `com.unity.nuget.newtonsoft-json` — this project had no JSON library wired into gameplay code at
  all before this).
- **`MatchWireClient`**: owns one `ClientWebSocket`. Background tasks do the actual socket I/O;
  every received frame is only parsed enough to route it, then queued, and `Pump()` — called from
  `UnitySimulationDriver.Update()`, i.e. the main thread — is the only place that touches Unity
  state or fires events. The standard safe split for a background-socket/main-thread-game-loop
  pairing, chosen over a SynchronizationContext dispatch trick.
- **`OnlineMatchService`**: creates a private match via `POST /matches` (reserving the seat for the
  signed-in PlayFab identity — MP-05) and joins it via the `playFabTicket=` path, percent-encoding
  the ticket first (the MP-05 bug this doc already records under "second real bug").
- **`UnitySimulationDriver.BuildSnapshotFromWire`**: reconstructs a REAL `VerticalSliceSnapshot` —
  not a wire-shaped substitute — from a `TickMessage`, via `PlayerEconomyState`/`TowerCombatState`/
  `CreepPresentationSnapshot`'s own public constructors (verified against their actual signatures,
  not assumed). This is why the renderer, HUD, and every other existing reader needed zero changes:
  they already only know how to read a `VerticalSliceSnapshot`, and now sometimes get one built from
  a socket instead of a local sim. Tower aim-targets, bramble cells, send-queues and the seat table
  are passed empty (not yet on the wire — see MP-04's original scoping note, extended for creeps
  this pass but not the rest); every reader treats empty as "nothing to show," not a crash.
- **Command routing**: `UnityCommandAdapter.PlaceTower`/`SellTowerAt`/`UpgradeTowerAt`/
  `BuyCategoryTier`/`SendCreep` route to the wire client instead of the local simulation when wire-
  backed. Several of the class's read-only helpers (`CurrentPlayerGold`, `CurrentPlayerIncome`,
  `TowerLineTier`, `SendCategoryTier`, `CanUpgradeTowerAt`, `TowerCost`, `CreepCost`, and others)
  read `simulation.GetSnapshot()`/`simulation.Content` directly rather than through the driver — a
  separate data path that would have silently returned wrong fallback values (0 gold, tier always
  1) for a networked match. These were ported to read through `simulationDriver` instead, since
  `TowerLineTier`'s result feeds directly into `BuyCategoryTier`'s wire-sent `TargetTier` — a wrong
  read there would have sent the wrong tier to the server on every purchase past the first, not
  merely displayed a wrong number.
- **Local-only prediction, not full lockstep.** A real architectural fork surfaced here: the
  server's command replies are inherently asynchronous, but the existing UI expects a synchronous
  accept/reject result. True lockstep prediction (replaying the whole match — bots, opponents, RNG
  — client-side) would need a second wire-protocol change (broadcasting every seat's commands, not
  just resulting state) and deterministic bot/RNG replication client-side; decided against as
  disproportionate for this pass. Instead, `MatchWireClient` tracks `PendingPrediction`s for the
  LOCAL player's own place/sell/upgrade actions only — `BuildSnapshotFromWire` merges them into the
  towers list (synthetic negative `EntityId`s so they can never collide with a real one) so a
  placement shows instantly, and `CommandResultMessage`'s matching `Id` resolves (removes) the
  prediction either way, since the real effect (or its absence) is already knowable from there.
  `BuyCategoryTier`/`SendCreep` are NOT predicted (no single obvious visual for a tier bump or a
  queued send) — fire-and-wait, resolving on the next real tick.
- **Deliberately deferred, not overlooked**: the event stream (`EventDto.Data` has no fixed
  per-event-kind DTO catalog on the wire — a client would need a mirror of every
  `LTW.Simulation.Events.*` shape to consume it losslessly), so event-driven VFX/audio and automatic
  match-end/results-screen detection do not fire for a networked match yet — `LatestEvents`/
  `LatestMatchSummary` stay empty/null. `PreviewTower`'s ghost-color hint always shows red (cosmetic
  only — confirmed by reading `TouchPlacementController.cs`'s actual call sites that the real
  placement tap does not depend on the preview result). Batch operations
  (`UpgradeTowerLine`/`UpgradeTowers`/`SellTowers`) and the opening build countdown UX are local-only
  in this pass; a networked match starts immediately with no countdown.

### Self-audit (2026-09-04), requested immediately after landing this

A deliberate re-read of the above with fresh, skeptical eyes, given how much of it had not been
exercised against a real match at the time it was written. Found and fixed, not just noted:

- **Real, significant bug: `PlayerSnapshotDto` never carried `ChosenTowerLine` or either tier
  array.** A wire-reconstructed player always read as tier-1-everywhere and uncommitted to any
  line, regardless of real purchases — because the simple public `PlayerEconomyState` constructor
  defaults exactly those fields, and nothing overrode them. Consequence, concretely: the palette
  would show base prices forever, and — the more serious half —
  `UnityCommandAdapter.BuyCategoryTier`'s wire-sent `TargetTier` is `current + 1`, computed from
  this same wrong-always-1 value, so a second tier purchase in the same category would have sent
  `TargetTier: 2` again rather than `3`, which the server would reject. Fixed the same way the
  creep gap was fixed earlier: extended `PlayerSnapshotDto` server- and client-side, and rebuilt a
  wire player via `PlayerEconomyState`'s own `WithChosenTowerLine`/`WithTowerLineTier`/
  `WithSendCategoryTier` methods (the simple constructor cannot set these — this project has no
  access to the private, fuller one). Proven with a real test
  (`Tick_messages_carry_the_players_chosen_line_and_tier_state`) that places a tower and confirms
  the resulting line commitment and starting tier both show up in a real `TickMessage` — not
  extended to also prove a live tier-2 purchase, after a single arrow tower reliably lost the match
  to seven bots at 200 ticks/second before affording one, three attempts in a row; the
  reconstruction code path is identical for tier 1 and tier 2, so this would have proven nothing
  the passing test does not already cover, at the cost of a flaky economy-balance dependency.
- **Real, moderate bug: `MatchWireClient`'s receive-loop task was fire-and-forget.**
  `Dispose()` cancelled the shared cancellation token and immediately proceeded to close and
  dispose the same `ClientWebSocket` the background receive loop might still be calling
  `ReceiveAsync` on — a genuine race (cancellation unwinding is not instantaneous), not a
  theoretical one. Fixed by tracking the loop's `Task` and bounded-waiting on it before closing the
  socket. While there: the loop's catch clauses only handled `OperationCanceledException` and
  `WebSocketException` — any other exception type would have become a silently-dropped unobserved
  task exception, leaving the client looking connected while no more messages ever arrived, with
  nothing reported via `OnError`. Broadened to catch `ObjectDisposedException` (treated as normal
  shutdown) and any other `Exception` (routed to `OnError`, matching the class's own stated
  contract that failure is always reported there).
- Checked and found sound on this pass: real `EntityId`s only ever increment from 1 (server-side),
  so the synthetic negative IDs predicted towers use cannot collide with one; the JSON casing
  between `OnlineMatchService`'s anonymous request object and `HttpMatchHost.CreateMatchRequest`
  (both resolve to camelCase, one by explicit property naming, one by `JsonSerializerDefaults.Web`);
  and that `NextSendAvailableTick` — also defaulted incorrectly by the simple constructor, same as
  the tier fields — is currently harmless, since the send cooldown it feeds is presently 0 ticks
  for every match, local or networked, per its own pre-existing code comment.

### Title-screen entry point — landed, audit finding avoided

Audited `LocalSessionFlowOverlay.cs` before touching anything, per the concern raised when this
was first deferred. Found a real seam rather than a risk: `ActiveShellScreen` already polls
`simulationDriver.HasStarted`/`IsPaused`/`IsOpeningBuildCountdown` every frame to decide what to
show, and does not care WHO changed them — a local match starting and a wire-backed match
connecting look identical to it. So a PLAY ONLINE button was wired the same self-contained way
SIGN IN WITH GOOGLE was (`ShellScreenView`, not routed through `IShellScreenActions`/the overlay):
on tap, calls `OnlineMatchService.CreateAndJoinAsync`, and on success calls
`UnitySimulationDriver.Initialize(MatchWireClient)` plus finds and initializes
`UnityCommandAdapter` the same way `RebuildMatchFromPendingOptions` already does internally for
Practice. **Zero changes to `LocalSessionFlowOverlay.cs`** — the file this doc specifically flagged
as the regression risk. Once `HasStarted` flips true, the overlay's existing per-frame poll
switches away from Title on its own, exactly as it would for a local match.

No explicit "connecting" screen — the button's own text ("CONNECTING...") is the only feedback,
same minimal affordance as sign-in. That gap is already tracked below.

### Opening build window — found by the first live PLAY ONLINE test, fixed same day (2026-09-04)

The first real device-adjacent test of PLAY ONLINE (Unity Editor, real `LTW.MatchServer`, real
PlayFab session) surfaced a genuine gap the audit above did not catch: the player dropped straight
into an active match with creeps already inbound, no chance to place an opening tower first. Root
cause was architectural, not a typo — `ServerMatch.RunLoopAsync` called `slice.AdvanceOneTick()` on
its very first loop iteration, and `LocalVerticalSlice.StartMatch()`'s `SeedExpandedLaneBotOpeners`
fires inside that same call, so bots sent their opening creeps before tick 1. Local play's 30 second
"tap PLAY to begin" window is a purely client-side trick (`UnitySimulationDriver` just doesn't call
`AdvanceOneTick` yet) with no server-side equivalent at all — confirmed by grepping
`src/LTW.MatchServer` and `src/LTW.Simulation` for any "build window"/"lobby"/"ready" concept: none
existed.

Fixed by giving `ServerMatch` its own opening build window (`openingBuildWindowSeconds`, defaults to
30, matching local play): `RunLoopAsync` skips `AdvanceOneTick()` until it elapses, so the tick
counter holds at 0 and no bot opens fire, while `DispatchAsync` (placement commands) is untouched and
keeps working normally during the window. `TickMessage` grew `Sequence` (increments every loop
iteration whether or not the tick advanced — needed because `Tick` itself is frozen at 0 for the
whole window, so it can no longer be what a client dedupes "is this a new message" on),
`IsOpeningBuildCountdown`, and `OpeningBuildCountdownRemainingSeconds`. Auto-starts at match
creation rather than waiting for an explicit ready-up — today's online matches are solo-vs-bots, so
there is no second human to wait for; a real matchmaking flow would need a ready-up step instead
(not built here).

`UnitySimulationDriver.UpdateWire` now keys its "new message" dedupe on `Sequence`, and sets
`HasStarted`/`IsPaused`/`IsOpeningBuildCountdown`/`OpeningBuildCountdownRemaining` straight from each
tick — mirroring `BeginOpeningBuildCountdown`/`StartMatch`'s own flag pairing for local play closely
enough that `LocalSessionFlowOverlay`'s existing countdown panel renders correctly with **no changes
to its display logic**. Two of its buttons DID need gating, though: `DrawBuildCountdownPanel`'s
"START NOW" and "MENU" both only ever touch the local driver flags directly, which the next incoming
server tick would immediately overwrite again — clicking either during an online match would flicker
and do nothing. Both are now hidden behind a new `UnitySimulationDriver.IsWireBacked` check. Leaving
a live online match at all remains unbuilt (no disconnect/leave-match path exists yet, online or off)
— a separate, pre-existing gap this only surfaced, not introduced.

Proved with a new integration test, `Opening_build_window_holds_the_tick_and_still_accepts_placement`
(a fast, test-only `openingBuildWindowSeconds` override — 1s instead of 30 — keeps it from needing a
real wait): asserts the first tick is a 0-tick countdown message with no creeps, places a tower
during it, then polls past the window and confirms both a real advancing tick and the placed tower
surviving into it. Full suite (377 tests across both worktrees) and Unity batchmode compiles both
stayed clean.

**Confirmed live, 2026-09-04**: Unity Editor Play Mode (debug-injected PlayFab session — see
`DebugLocalOnlineTestSignIn.cs`, untracked) against a real, restarted local `LTW.MatchServer`. PLAY
ONLINE showed the BUILD PHASE panel with a live ticking countdown and no creeps, a tower placed
during it stuck, and the match went live into normal ticking play once the window ended.

### Leaving an online match — landed 2026-09-04

Confirming the build window live surfaced the next gap immediately: there was no way to leave an
online match at all. `UnitySimulationDriver.ResetMatch()` (what every existing exit path called)
only ever touched the local `simulation` field, never `wireClient` — so the socket kept pumping in
the background and the very next incoming tick silently overwrote the "reset" right back to
whatever the server was doing. First found through the opening-build-countdown panel's MENU
button (hidden entirely as a stopgap in the build-window fix above), this generalizes to the same
problem on the normal in-match Pause screen's EXIT TO TITLE and RESET MATCH buttons, and the
Results screen's REMATCH — all four route through the same
`LocalSessionFlowOverlay.ResetMatchLeavingPractice`.

Fixed at that single choke point rather than in each of the four callers: it now branches on the
new `UnitySimulationDriver.IsWireBacked`, and for a wire-backed match calls a new
`UnitySimulationDriver.LeaveOnlineMatch()` (disposes `MatchWireClient`, clears `wireClient`, resets
the same `HasStarted`/`IsPaused`/`IsOpeningBuildCountdown` flags `ResetMatch()` resets for local)
instead of `ResetMatch()`, and forces `preMatchScreen = Title` regardless of which of the four
buttons triggered it — RESET MATCH and REMATCH have no real server equivalent yet (a match cannot
be reset or rematched in place, only left), so all four honestly land on the same place: Title. The
countdown panel's MENU button is un-hidden now that it works correctly; START NOW stays hidden,
since starting the match early still has no server equivalent.

Not yet covered by an automated test (no Unity Editor Play Mode test harness exists in this repo to
drive `IShellScreenActions` through a real wire connection) — verified live instead, 2026-09-04:
MENU during a live match now cleanly returns to Title, and PLAY ONLINE works again on a second tap
(a related bug this same live test found — see below).

**Two more regressions the live test found immediately after, same day**

- The PLAY ONLINE button was only ever designed to be tapped once — nothing reset its
  `CONNECTING...`/disabled state on success, because there was previously no way back to Title to
  even notice. Fixed in `ShellScreenView.Show`: re-entering the Title screen now resets the button
  back to its default text and re-enables it.
- `UnitySimulationDriver.UpdateWire` (from the build-window fix above) set `IsPaused` from
  `tick.IsOpeningBuildCountdown` on every single incoming tick, not just the build-window
  transition — which is `false` for the whole rest of a live match, so it silently stomped a local
  PAUSE back to unpaused within one network tick interval. The server has no concept of a
  per-client pause and keeps ticking regardless, so once live, `IsPaused` is now purely a local
  concern again (`LocalSessionFlowOverlay.TogglePause`) — the wire layer only forces it at the two
  points it legitimately owns: `true` while the window runs, `false` exactly once when it ends.

Both confirmed live immediately after fixing: PLAY ONLINE works on a second connection, and PAUSE
now holds during live online play until RESUME is tapped.

### Real iPad device test, 2026-09-05 — the actual end-to-end proof, and eight bugs it found

The first real-device test (not Editor Play Mode): a physical iPad, a fresh Xcode export
(`IosBuildRunner`), against `LTW.MatchServer` running on the Mac's own LAN. This is the proof
MP-06's exit signal has been waiting on since MP-04 — and, true to how every other milestone in
this pass has gone, it immediately found real bugs the Editor test couldn't have, because the
Editor test runs client and server on the same machine with no real network in between at all.

**Connectivity (found in order, each fix re-exported and re-tested before the next was found):**

1. **PLAY ONLINE did nothing.** `LTW.MatchServer`'s `HttpListener` only ever bound
   `http://localhost:{port}/` — deliberately loopback-only since every test before this ran
   client and server in the same process's address space. A device on the LAN sends a Host
   header naming the Mac's own IP, which `localhost` was never registered to accept. Rebound to
   `http://+:{port}/` (any host) — the same thing a real container deployment (MP-07) would need
   anyway, since a container's own loopback is never reachable from outside it. `MatchServerConfig.Host`
   also had to be pointed at the Mac's LAN IP for this test build specifically (temporary, reverted
   before committing — see its own remarks: this is dev/LAN-testing infrastructure, not a real
   answer, which real hosting will replace with a domain and HTTPS).
2. **Still did nothing, even pointed at the right IP.** iOS's App Transport Security silently
   blocks any plain `http://`/`ws://` request by default — no error surfaces in-app, the request
   simply never leaves the device. `PlayerSettings.iOS.allowHTTPDownload` is what injects the
   `NSAllowsArbitraryLoads` exception into the generated Info.plist; added as a new
   `IosBuildRunner` flag (`-ltwAllowInsecureHttp 1`), opt-in and never the default, since it is an
   App Store review flag and real hosting will use HTTPS/WSS instead.

**Gameplay pace and feel:**

3. **The whole match ran 2.5x too fast.** `ServerMatch`'s default tick rate was 10/second, picked
   from `ARCHITECTURE.md`'s aspirational "10 to 20" prototype-target range — but the actual shipped
   local client runs its own fixed-timestep loop at 4/second (confirmed: no scene or prefab
   anywhere overrides `UnitySimulationDriver`'s `ticksPerSecond` field, so the compiled default is
   the live value). Since every game number (creep speed, income, cooldowns) is defined per
   simulation tick, running more of them per real second just makes everything happen faster, not
   more precisely. Fixed the server's default to match local's real rate.
4. **Creep motion visibly stuttered.** Wire-mode rendering held a creep completely still between
   network ticks — fine in the Editor at a fast test tick rate where updates arrive almost every
   frame, very visible at a real 4/second against real Wi-Fi jitter on top. Added client-side
   extrapolation: `UnitySimulationDriver` now re-derives `MovementProgress` every render frame
   using elapsed wall-clock time and the creep's own speed, self-correcting the instant the next
   real tick lands, rather than freezing until it does.

**Missing feedback (the wire carried the state; nothing read it, or nothing sent it):**

5. **No audio or tower recoil/attack VFX at all.** A known, documented scoped-out gap from the
   original MP-06 pass — `LatestEvents` stayed empty forever for a wire match, since the wire had
   no per-event-kind DTO catalog. Turned out the server was already sending every event's full
   field data (serialized by its own runtime type); the client just had nothing to parse it back
   into. Added `WireEventReconstruction`, a client-side reconstructor for the 11 event kinds that
   actually drive presentation (tower placed/sold/upgraded/fired, creep damaged/killed, income
   tick, leak, player eliminated, match ended, tier purchased) — no server change needed.
6. **Send queue silently did nothing when a send couldn't be afforded instantly.** The wire layer
   only ever had `queueSend` — an immediate, all-or-nothing spend that rejects outright if unaffordable
   — never the actual send-dock behavior (`EnqueueSend`: queue now, pay when affordable, no upfront
   check). Added a real `enqueueSend` wire command routed to `LocalVerticalSlice.EnqueueSend`.
7. **Even once queuing worked, nothing showed it — and the income number was wrong regardless.**
   Two distinct bugs found together: the wire protocol never carried a player's send-queue contents
   at all (`PlayerSnapshotDto` gained `SendQueue`, mirroring how `TowerLineTiers`/`SendCategoryTiers`
   already do), and separately `UnityCommandAdapter.SendIncomeGain` always returned 0 online (asked
   `simulation`, always null for a wire match) — read live as "the creep's income isn't correct."
   Fixed by reconstructing the same income-taper calculation client-side from the wire's own current
   income (`EconomyRules`'s two relevant fields are left at their documented defaults, so this
   cannot drift from the real recipe even if the rules class's other fields ever do).
8. **The send-dock and build-panel descriptions were generic to the point of being wrong.** These
   panels show only one short trait line (no numeric stat grid, unlike the full Codex screen) — for
   any of the five plain CORE creeps or any plain single-target tower, that line was a single bare
   fallback phrase ("Pays 1 gold to whoever kills it" / "Single-target damage") true of roughly half
   the roster and distinguishing none of them, with no movement or health information at all.
   Rewrote both fallbacks in `CodexScreenView` to lead with the tower's/creep's real numbers instead.

**Missing feedback at match end, and a design change:**

9. **No results screen at all.** Same shape as bug 5: `ActiveShellScreen`'s existing check
   (`LatestMatchSummary is not null -> Results`) was a dead zone for wire matches since nothing
   ever set it. Now that `MatchEndedEvent` reconstructs via `WireEventReconstruction`, hooked its
   arrival to build a `MatchSummary` from the wire's own per-tick player data — placement ranking
   isn't carried over the wire yet, so the results table shows "—" for that column (already
   handled gracefully by the existing UI), but the winner banner reads `WinnerId` directly and is
   unaffected.
10. **Elimination behavior, revised by request rather than found broken.** Investigated carefully
    before changing anything, since `WipeEliminatedLane` is core simulation logic shared by local
    and online play, with dedicated tests pinning a deliberate, previously-shipped design (a
    2026-08-29 incident: letting an eliminated player's creeps keep marching caused leaks that
    could not be credited to anyone, breaking life conservation). Confirmed towers already wipe
    correctly — that part of the report was a misread. The creep half was a genuine, deliberate
    change request: an eliminated player's attacking creeps now redirect to the next active
    opponent, restarting at that lane's entrance, using the SAME transfer mechanism a creep that
    survives reaching a lane's end already gets (`NextActiveOpponentLaneId`/
    `CombatService.TransferCreep`) — which resolves the original 2026-08-29 problem differently
    than deleting them did: the creep gets a new, live, creditable target instead of either
    vanishing or being orphaned. Two tests added; one caught a real scheduling edge case (two
    players eliminated within the same tick) during writing, confirmed correct once the test's own
    assertion was fixed to check redirect order rather than tick membership.

Every fix above went through the full loop: implemented, `dotnet test` (currently 379 tests) and a
Unity batchmode compile in both worktrees, a fresh `IosBuildRunner` export, then re-tested live on
the same physical iPad before moving to the next finding — not just compiled and assumed correct.

### Reconnect survivability, 2026-09-05 — both acceptance checks confirmed live

Investigated before touching anything, since this is core networking/session code: found the
server's seat-binding had **no protection against double-binding at all**.
`ServerMatch.BindAsync` never checked whether a seat already had a live connection — a second
connection presenting the same token/PlayFab ticket would simply bind alongside the first, and
both would go on receiving every broadcast and dispatching commands as that seat. Harmless purely
by accident until reconnect made rebinding an already-bound seat a real, expected path (a network
hole's stale socket has often not yet noticed it is dead when the replacement connection arrives)
— fixed by evicting whatever connection already held the seat before binding the new one. Hit a
real deadlock in the first version of that fix: closing the stale socket with `CloseAsync` waits
for the peer's own close reply, which a genuinely stale/idle peer never sends — switched to
`CloseOutputAsync`, which does not wait. A new test pins the eviction.

With that in place, the two reconnect mechanisms themselves:

- **A 2 s network hole**: `MatchWireClient` now exposes when its connection has actually died
  (`IsDisconnected`), distinct from an ordinary protocol-level `ErrorMessage`. On that,
  `UnitySimulationDriver.AttemptReconnect` retries rejoining the SAME match (up to 5 attempts, 1 s
  apart) using the PlayFab identity already in memory — no user interaction. Because a fresh
  connection re-syncs from a real server snapshot the moment it reconnects, there is nothing to
  reconcile: this is "no desync" by construction, not by careful state-merging.
- **Kill and relaunch**: `OnlineMatchService.PendingMatchId` persists the current match id to
  `PlayerPrefs` on every successful join, cleared only on a deliberate leave or a real match end —
  specifically NOT cleared when `AttemptReconnect` itself exhausts its retries while the app stays
  alive, since an outage longer than that loop's own window should still be resumable by a later
  kill-and-relaunch. `ShellScreenView` checks for a pending match right after a successful Google
  sign-in and silently rejoins it instead of waiting for PLAY ONLINE to be tapped.

**A second real bug found live-testing the kill-and-relaunch path**: the first attempt reported
"dropped me into a new session instead of rejoining." Root cause was a gap in `JoinAsync` itself,
not the resume logic: `ClientWebSocket.ConnectAsync` succeeding only proves the WebSocket UPGRADE
succeeded — `HttpMatchHost.HandleJoinAsync` accepts that upgrade FIRST and only afterward checks
the token/PlayFab ticket, closing the socket with a policy violation if it doesn't match. A caller
trusting `ConnectAsync` alone treats that as a successful join and hands back a client that is
already dying — indistinguishable from an ordinary disconnect a frame or two later, and (for a
rejoin specifically) silently never actually rejoins anything. Fixed by waiting for the server's
own `WelcomeMessage` (genuine acceptance) or the connection dying (rejection) before declaring a
join successful, rather than trusting the handshake alone. Confirmed live immediately after: sign
back in, silently rejoined the same match.

Both confirmed live on the real iPad, same device/LAN setup as the initial device-test pass above.

### Sign-in persistence, 2026-09-05 — the kill-and-relaunch flow is now fully automatic

Requested immediately after confirming reconnect above: Google Sign-In itself did not survive a
kill, so a player who had just been mid-match still had to tap SIGN IN WITH GOOGLE by hand on
every relaunch before the rejoin above could even run. Fixed not by restoring Google's own native
session, but by persisting what this game actually authenticates every online operation with: the
PlayFab session ticket. `PlayFabSession.SetSignedIn` now saves the PlayFabId and ticket to
`PlayerPrefs`; `PlayFabSession.TryRestore` — called once at Title startup, before the player can
tap anything — brings them back with no network call and no Google interaction at all. Restoring
is deliberately optimistic: a saved ticket may have expired since it was written, and nothing here
spends a request just to check that ahead of time. The real check is the one every online
operation already makes — `OnlineMatchService.JoinAsync`'s own join-acceptance wait (see
"Reconnect survivability" above) now calls `PlayFabSession.ForgetOnAuthFailure` specifically on a
closed-during-join rejection (a bad or expired ticket, as opposed to a network blip or a
genuinely-gone match), resetting cleanly to the ordinary SIGN IN WITH GOOGLE state rather than
leaving the player stuck behind a session that looks signed in but can never do anything.

Confirmed live immediately after: killed the app mid-match, relaunched, and it dropped straight
back into the same match with no sign-in tap at all.

### RealUiCaptureRunner online-flow coverage, 2026-09-05 — the last acceptance check closes

Added `real-26-title-play-online-connecting` to `RealUiCaptureRunner`'s existing 25-shot set. The
other two states this check named needed no new shot at all: there is no separate lobby screen —
PLAY ONLINE lives right on Title, so the existing `real-16-shell-title` shot already covers it —
and a network-hole or kill-and-relaunch reconnect is silent by design (see "Reconnect
survivability" above), so a "reconnecting" shot would photograph the exact same idle board
`real-01` already does, under a different name.

The connecting shot itself needed two iterations. The first version signed in with a fake PlayFab
identity and genuinely tapped PLAY ONLINE, so the capture raced a real network round trip (match
creation, then a PlayFab ticket check against PlayFab's real servers): one run caught
`CONNECTING...` because that round trip was still in flight at the capture's 0.6 s settle mark, an
identically-configured run right after did not. Fixed by having `ShowPlayOnlineConnecting` set the
button's disabled state and text directly — exactly the two things
`ShellScreenView.OnPlayOnlineTapped` itself sets before ever awaiting anything — so the shot needs
no PlayFab identity, no match server and no race.

That same fake-identity setup also surfaced a real, unrelated bug: `PlayFabSession.Clear()` was
deleting its PlayerPrefs keys without calling `PlayerPrefs.Save()`, so a signed-out identity could
still be read back by `TryRestore()` in a later process if the app exited (or, here, the Editor
batch run quit) before Unity's own next autosave — the same lesson `SetSignedIn` had already
learned. Confirmed via two successive capture runs: the first still showed the stale "SIGNED IN"
state (leftover from a run made with the old, unfixed code), the second was clean.

Reviewed a broader spot-check of the 28 shots beyond the two new/changed ones (HUD, send dock,
tower inspector, both results screens, the eliminated/spectating state) at phone width for
regressions from today's changes — none found. `PlayFabSession.cs` and `RealUiCaptureRunner.cs`
mirrored into `LTW-integrate`; `dotnet test` still 380/380 green; both worktrees batchmode-compile
clean.

### What this has NOT proven

The core online loop (connect, build window, place/upgrade/sell, send and queue, pause, leave,
eliminate, win, a network hole, a kill-and-relaunch, sign-in persistence) is now confirmed on a
real device over a real (if LAN-local) network. Still not exercised: a real *hosted* deployment
(the LAN IP / `allowHTTPDownload` combination is dev-only infrastructure, not what MP-07 will
ship), two simultaneous real human players (today's matches are still solo-vs-bots), and what
happens once a restored PlayFab ticket has genuinely expired (the reset path is implemented and
reasoned through, but the actual expiry window has not been waited out live).

### Acceptance Checks

- [x] A 2 s network hole mid-match is survivable with no desync. Landed and confirmed live
      2026-09-05 — see "Reconnect survivability" above.
- [x] Kill the app and relaunch inside the reconnect window: the seat resumes. Landed and
      confirmed live 2026-09-05, along with a second real bug that same test found (a join whose
      WebSocket handshake succeeded but whose application-level acceptance had not, silently
      treated as successful) — see "Reconnect survivability" above. Fully automatic as of the
      same day's sign-in persistence fix too: no manual re-sign-in needed for the resume to fire.
- [x] A player can leave an online match cleanly (socket closed, driver state reset) from any of
      the existing exit paths (Pause's EXIT TO TITLE/RESET MATCH, Results' REMATCH, the
      build-countdown panel's MENU) — landed and confirmed live 2026-09-04, along with two
      regressions the live test itself found and fixed the same day (PLAY ONLINE button not
      resetting after a match, PAUSE not holding during live online play).
- [x] `RealUiCaptureRunner` shots for the lobby, the connecting state and a reconnect exist and
      are reviewed at phone and iPad widths. Landed and confirmed 2026-09-05 — see "RealUiCapture
      Runner online-flow coverage" above; the lobby and reconnect states needed no new shot since
      existing shots already cover them pixel-for-pixel.
- [x] The wire protocol carries enough state (players, towers, creeps) to render a match without
      reading the simulation directly — confirmed 2026-09-04 against a real running match.
- [x] The client can build a real `VerticalSliceSnapshot` from wire data alone, compatible with
      every existing renderer/HUD/UI consumer with zero changes to them — confirmed 2026-09-04 by
      compiling cleanly against the actual consumer set, not a synthetic test harness.
- [x] A real device joins a real match over the wire and plays it — the actual end-to-end proof.
      Landed and confirmed 2026-09-05: a real iPad, over the Mac's LAN, playing a full match
      through to a result — see "Real iPad device test" above for the ten bugs that test found
      and fixed along the way.
- [x] A title-screen entry point exists to start/join an online match — PLAY ONLINE, landed
      2026-09-04 with zero changes to `LocalSessionFlowOverlay.cs`. Confirmed live the same day
      (Editor Play Mode against a real running `LTW.MatchServer`), including the opening build
      window fix that live test itself found.

**Estimate:** every acceptance check in this plan's own scope is now done. What remains before
MP-06's work is truly finished — beyond this plan's own scope — is MP-07's real hosting to replace
the LAN-IP/`allowHTTPDownload` dev setup this pass used to prove the wire layer (and reconnect)
works at all on a real device.

## MP-07: Operations

**Owner:** Server. **Status:** the GSDK-integrated hosting mechanism itself is built and
live-verified end to end (locally, via `LocalMultiplayerAgent`) on Azure PlayFab Multiplayer
Servers (MPS), not a generic container host. Not yet done: the real PlayFab Game Manager portal
setup, telemetry, the runbook, and abuse handling.

### Why PlayFab Multiplayer Servers, not a generic Azure container host

Work initially started toward a generic Azure Container Apps deployment (a resource group, an
Azure Container Registry, a Log Analytics workspace — all since deleted) before recognizing PlayFab
already has a purpose-built option for exactly this shape of workload. PlayFab is already this
project's identity provider (`PlayFabSessionAuthority` server-side, `PlayFabSession`/
`PlayFabLoginService` client-side), so MPS avoids standing up a second cloud relationship, and its
free evaluation tier (750 Dasv4 core-hours/month, confirmed against Microsoft Learn docs as of
2026-09-05) comfortably covers this project's current scale — no live population yet, one real
device tested (see "Real iPad device test" under MP-06).

The trade-off: this is not a drop-in swap. MPS's model — an ephemeral process per match, integrated
via PlayFab's Game Server SDK (GSDK) — is the opposite of `LTW.MatchServer`'s existing shape (one
process, always running, hosts arbitrarily many matches via `MatchRegistry`). The work is
sequenced so the parts needing no PlayFab/Docker/cloud access land — and are `dotnet test`-verified
— first, with live-verification-only work pushed as late as possible.

### Landed 2026-09-05 — lifecycle fix and client-side plumbing, no live PlayFab MPS yet

**A real, independent bug fix, found while designing around it:** a match that ended never
actually stopped. `ServerMatch.RunLoopAsync`'s tick loop only checked a cancellation token — once
`slice.MatchSummary` became non-null the only thing that happened was a one-time replay write; the
loop kept ticking and broadcasting forever afterward. Fixed by canceling the loop's own token
right after that replay write, letting the loop's existing `while` condition end it on its next
iteration. `ServerMatch` now exposes `Completion` (a `Task` that resolves once the loop actually
stops, from either this path or the existing manual `Stop()`), which MPS's GSDK integration will
await to know when to exit the process. Also added: `ServerMatch` accepts optional
`onSeatBound`/`onSeatDisconnected` delegates (plain BCL types, no GSDK reference) for MPS's
eventual `GameserverSDK.UpdateConnectedPlayers` reporting; `MatchRegistry.CreateMatch` accepts an
optional explicit `matchId` (so MPS can reuse PlayFab's own `SessionId` instead of minting a fresh
one); `HttpMatchHost` accepts `allowMatchCreation: false` (so an MPS-hosted process, reachable at a
real address for the life of one allocated match, can't have a second PlayFab-invisible match
created on it via `POST /matches`). The `CreateMatchRequest` DTO moved out of `HttpMatchHost` into
its own file so MPS's bootstrap can deserialize the identical JSON shape out of a PlayFab
`SessionCookie` instead of an HTTP body. All covered by new tests in
`tests/LTW.MatchServer.Tests/MatchServerIntegrationTests.cs` (dotnet test: 383/383 including
these).

**Client-side (`unity/LTW.UnityClient/Assets/Scripts/Online/OnlineMatchService.cs`)**: confirmed by
reading the code that `MatchWireClient` already takes an arbitrary `Uri` and `ShellScreenView`'s
two call sites only ever consume the returned `MatchWireClient?` — the entire client pivot is
containable inside `OnlineMatchService.cs` alone, with zero changes needed elsewhere. Added a
`OnlineMatchService.UseMultiplayerServers` toggle (default `false`): when false, behavior is
byte-for-byte what it always was (direct-connect to `MatchServerConfig`'s known address) — this is
deliberate, so ordinary local/LAN iteration (MP-06's whole rollout was tested this way) never needs
a build uploaded to PlayFab or Docker running just to test gameplay changes. When true, a new
`RequestServerAsync` calls PlayFab's `RequestMultiplayerServer` (via a `GetEntityToken` call first
— PlayFab Multiplayer's REST API authenticates with an Entity Token, not the classic session
ticket) with the same `{humanSeats, playFabSeats}` JSON as today's direct-create body, now carried
in the `SessionCookie` field, and polls `GetMultiplayerServerDetails` until the allocation reaches
`"Active"`. `JoinAsync` now takes an explicit `(host, port)` instead of reading `MatchServerConfig`
implicitly; `RejoinAsync` persists and reuses that resolved address rather than ever calling
`RequestMultiplayerServer` again — a match's server instance doesn't move during its life, so a
rejoin (network hole, kill-and-relaunch) is just re-dialing the same address, exactly like today.
New `MultiplayerServerConfig.cs` holds `BuildId`/`PreferredRegions`/`PortName` as placeholders until
a real build is uploaded (see "Not yet done" below) — mirrors `PlayFabConfig.TitleId`'s own
portal-sourced-constant shape. Verified via Unity batchmode compile only (0 `error CS`) — this
cannot be functionally tested until a real PlayFab build exists to request a server from.

**Containerization, built and live-verified 2026-09-05**: `src/LTW.MatchServer/Dockerfile`
(multi-stage, `dotnet/sdk:10.0` → `dotnet/runtime:10.0` — not `aspnet:10.0`, since this is a
console app on `HttpListener`/`WebSockets`, no ASP.NET Core) plus a repo-root `.dockerignore`. Once
Docker was installed, built and ran cleanly: the "PlayFab not configured" degrade path works
identically inside the container with no env vars set, `PlayFab configured: title FBC34` prints
correctly with `PLAYFAB_TITLE_ID`/`PLAYFAB_SECRET_KEY` passed via `-e`, and a real create-match
`POST` plus a real WebSocket join (receiving a genuine `welcome` message) both succeeded from the
host across the Docker NAT boundary — the first real (non-loopback) network hop this project has
exercised, directly chipping at MP-04's still-open "not provable here: a real network" note.

**GSDK compatibility spike, done 2026-09-05 — both flagged unknowns resolved empirically**: added
`com.playfab.csharpgsdk` 0.11.210519 to `LTW.MatchServer.csproj` (pinning `Newtonsoft.Json` to
13.0.3 directly — the GSDK package's own transitive 11.0.2 has a known high-severity vulnerability,
NU1903, that this project's restore treats as an error). Reflection against the installed package,
then an actual local run with no PlayFab agent present, found:

- The package **loads and runs cleanly on net10.0** — no `TypeLoadException`/
  `MissingMethodException`/`BadImageFormatException`. Confirmed by actually calling
  `GameserverSDK.RegisterShutdownCallback` (see below) and observing a clean, typed failure, not a
  loader-level crash.
- **The port to bind is not in `getConfigSettings()`'s string dictionary at all** — the docs never
  named a key for it because there isn't one. It comes from
  `GameserverSDK.GetGameServerConnectionInfo().GamePortsConfiguration`, a list of `GamePort` records
  (`Name`, `ServerListeningPort` — what this process binds to — `ClientConnectionPort` — what
  PlayFab reports back to a connecting client). This is available immediately after `Start()`,
  unlike `SessionCookieKey`/`SessionIdKey`, which really are only populated post-allocation exactly
  as documented.
- `GameserverSDK.Start(bool debugLogs = false)` has a defaulted parameter, so the docs' own
  parameterless `Start()` sample still compiles — a non-issue, checked directly.
- **Initialization is lazy, triggered by the first GSDK API call touched at all** — not
  specifically `Start()`. A local run with no agent config present threw
  `GSDKInitializationException: GSDK file -  not found` from inside the very first
  `RegisterShutdownCallback` call (registered before this code's own explicit `Start()`, per this
  section's own ordering rule below) — the internal SDK initializes itself lazily on whichever
  GSDK method is touched first, not necessarily the one that reads as "the" start call. Fails fast,
  does not hang — safe to let it crash the process loudly rather than needing a guard.

**Full GSDK integration, landed 2026-09-05 in `src/LTW.MatchServer/Program.cs`**: a new
`LTW_MATCHSERVER_MODE` env var (`standalone`, default, unchanged behavior — confirmed identical via
a real local run and a real create-match/join over it — or `mps`). The `mps` branch registers
shutdown/health/maintenance callbacks before `Start()`, binds `HttpMatchHost` to the
`GamePortsConfiguration` port found above with `allowMatchCreation: false`, awaits the documented
blocking `ReadyForPlayers()` off the entry-point thread via `Task.Run`, then bootstraps the one
match this process will ever host by deserializing the allocated `SessionCookie` into the shared
`CreateMatchRequest` DTO (now `public`, not `internal` — the repo has no `InternalsVisibleTo`
precedent, and this type has no reason to hide from the test assembly) and calling
`registry.CreateMatch(..., matchId: sessionId)`. Seat bind/disconnect events feed
`GameserverSDK.UpdateConnectedPlayers` (informational only — `AcceptWithPlayFabAsync` remains the
real enforcement). The process awaits `Task.WhenAny(shutdownSignal, match.Completion)` and exits —
its own exit is the only "I'm done" signal GSDK needs. Also added: a bootstrap log line
(`GameserverSDK.LogMessage` + `Console.WriteLine`, so it reaches both a real deployment's zipped
GSDK logs and local `docker logs`) recording the match id and join tokens — otherwise unobservable
under MPS, since there's no HTTP create-response to read them from the way standalone mode has.

**Live GSDK lifecycle verification, done 2026-09-05 via PlayFab's `LocalMultiplayerAgent` (LMA) —
the full loop closes.** Built LMA from source for macOS/Apple Silicon (`PlayFab/MpsAgent`, official
cross-platform support, "beta" on macOS) since Docker was already set up. Two local-environment
snags, neither about this project's own code:

- LMA's `MultiplayerSettingsValidator` throws if `OutputFolder` and `TitleId` are BOTH left empty
  simultaneously (its own `SetDefaultsIfNotSpecified()`/validation ordering bug) — worked around by
  setting both explicitly rather than relying on its auto-defaults.
- LMA's Docker client **hardcodes** `unix:///var/run/docker.sock` and ignores `DOCKER_HOST`
  entirely (confirmed by reading `DockerContainerEngine.cs` directly) — Docker Desktop for Mac's
  real socket lives at `~/.docker/run/docker.sock`. Fixed via Docker Desktop's own "Allow the
  default Docker socket to be used" setting (Settings → Advanced), which symlinks the default path
  for you. A separate, real wrinkle worth remembering for Phase 5: LMA's `ContainerStartParameters`
  has no field for custom container environment variables at all, so `LTW_MATCHSERVER_MODE=mps`
  couldn't be injected via `MultiplayerSettings.json` — worked around locally with a one-line
  throwaway image layer (`FROM ltw-matchserver:dev` + `ENV LTW_MATCHSERVER_MODE=mps`, discarded
  after testing). The real PlayFab Game Manager build-upload flow may or may not expose a way to
  set this env var directly — confirm during Phase 5; if it doesn't, the image built and uploaded
  for the real MPS build should set it permanently in its own Dockerfile layer, since that image's
  only purpose is running under MPS.

With those worked around, the full lifecycle ran exactly as designed, end to end:
`GameserverSDK.Start()` heartbeated successfully (`CurrentGameState: Active` in LMA's log —
confirming allocation), `ReadyForPlayers()` unblocked, the bootstrap log line showed the match id
came out **identical to the `SessionId` configured in the simulated allocation** (confirming the
`matchId: sessionId` reuse design works), a real `ClientWebSocket` joined using the logged token
and received a real `welcome`, the match played a real bot-heavy match out to a genuine
`MatchEndedEvent`, and — the one thing this entire pivot most needed proof of — **the container
exited on its own with exit code 0** immediately after, which LMA's own log confirms
(`Container ... exited with exit code 0`) before it collected the container's logs and deleted it.
Phase 0's lifecycle fix and Phase 3's GSDK integration are now verified working together, not just
individually plausible.

### Phase 5 — PlayFab Game Manager configuration, in progress 2026-09-05

Two things found while actually going through the portal flow (title `FBC34`), neither obvious
from the docs alone:

- **PlayFab provides its own free Azure Container Registry per account — no separate ACR needs to
  be created or managed.** Confirmed directly from Microsoft's own Linux-build docs: "instead of
  using a managed container image, you have to create and upload your container image to a
  container registry. To make it easy for you to upload containers, your account comes with an
  Azure container registry." Its credentials (hostname, username, password) are shown right on
  Game Manager's **New build** page when **Linux** is selected. This corrects an earlier
  assumption in this doc that a separate registry decision/provisioning step (recreating the
  deleted `line-wards-prod` ACR, or picking an alternative like Docker Hub) would be needed —
  it isn't.
- **The free 750-Dasv4-core-hour evaluation tier is a *billing* concept, not an automatic *quota*
  grant.** A brand-new title's default core quota is 16 Av2 cores + 8 Dv2 cores split across East
  US/West US — **zero quota for Dasv4 in any region** until explicitly requested. Hit this live as
  a save-blocking quota error when first trying to create a build. Fixed via Game Manager's
  self-service **Multiplayer Servers → Quota Summary → Change Quota** flow: describe the request,
  **+ Add change** for VM family **Dasv4** / region **East US**, request a small limit (8 cores is
  plenty for a small beta config, well under the 24-core free-tier cap). Per PlayFab's own docs,
  small requests like this are typically approved and provisioned immediately, unlike large
  (1000+ core) requests, which need manual review. **Approved 2026-09-08/09** — provisioned same
  day as requested, matching that expectation.
- `src/LTW.MatchServer/Dockerfile` gained a `MATCHSERVER_MODE` build arg (`ARG
  MATCHSERVER_MODE=standalone` / `ENV LTW_MATCHSERVER_MODE=$MATCHSERVER_MODE`) — resolves the
  earlier-flagged gap that PlayFab's build creation flow has no field for custom container
  environment variables. Ordinary local `docker build` (Phase 1's smoke test, future debugging)
  needs no extra flags and still defaults to standalone mode; the real deployment image is built
  with `--build-arg MATCHSERVER_MODE=mps` so it comes up in MPS mode by default, since that image's
  only purpose is running under a real GSDK agent.

### Phase 5 continued — standby sizing, image push, client access, 2026-09-09

**Standby VM sizing: 4× 2-core over 1× 8-core or 2× 4-core, all within the same 8-core quota.**
Game Manager's own cost estimator, checked directly for all three splits of the same 8-core
request, showed dramatically different "hours of usage" before the free/requested core-hour
budget is exhausted: 1×8-core ≈ 96 hours, 2×4-core ≈ 186 hours, 4×2-core ≈ 475 hours — despite
identical total cores. Reasoned through rather than taken on faith: PlayFab MPS allocates exactly
one match-server process per VM and bills for the *full allocated VM size* while that VM is
active, not the CPU it actually uses. `LTW.MatchServer` is a lightweight console app
(`HttpListener`/WebSockets, no per-match heavy compute) that doesn't approach even 2 cores of real
use, so a bigger VM per match burns quota for no benefit. 4× 2-core strictly dominates the other
two splits on this project's actual workload shape: more concurrent instant-join standby slots (4
vs 1) *and* ~5x the usage-hours of the single-8-core option, for the same requested quota.

**Image built and pushed, 2026-09-09**: `docker build --build-arg MATCHSERVER_MODE=mps -t
ltw-matchserver:mps -f src/LTW.MatchServer/Dockerfile .`, then pushed to the account's own free
ACR at `customerxm4ogh3zbvndm.azurecr.io/ltw-matchserver:mps` (the registry Phase 5's first bullet
above already established comes free with the account — confirmed live, no separate registry
needed). `docker login`/`push` succeeded cleanly against the credentials shown on Game Manager's
own New Build page.

**"Enable game client access" — corrected, this doc's own earlier phrasing was too vague to act
on.** It is not a field on the build creation form. It's a **title-level** setting: Game Manager →
title `FBC34` → **Settings** (left menu) → **API Features** tab → **"Allow Client to start
games"**, confirmed against Microsoft's own current docs (not guessed from memory) at
[Enable PlayFab Multiplayer Server feature](https://learn.microsoft.com/en-us/xbox/playfab/multiplayer/servers/enable-playfab-multiplayer-servers).
Without it, `RequestMultiplayerServer` only accepts the title's own secret-key-authenticated
service calls; with it, a player's own entity token (what this project's client-side
`RequestServerAsync` already sends) is accepted directly. **Enabled 2026-09-09.**

Build form submitted (image `ltw-matchserver:mps` from the pushed registry, region East US,
4-standby/2-core as sized above, port named `game`/5117/TCP) and provisioned — build named
"LineWards East 1" (`BuildId faee9e3e-1558-425f-9474-35e9adeb4e01`).

**Real bug found and fixed, 2026-09-09: that build's East US region came up `Unhealthy`.** Traced
without any container log to go on (a build-level health failure has no per-server download-logs
entry — those only exist once a server is actually allocated) by process of elimination: the
image genuinely had `LTW_MATCHSERVER_MODE=mps` baked in (confirmed directly via `docker inspect`
on the pushed image, not assumed), and the Dockerfile's `ARG`/`ENV` are correctly placed in the
runtime stage, not lost across the multi-stage build — ruling out the two most likely code-side
causes before touching any code. The actual cause: Game Manager's own build form capitalizes a
typed port name back on display — typing `game` shows back as `Game` — and both
`Program.cs`'s and the client's `OnlineMatchService.cs`'s port lookups did a case-sensitive
`==` against the lowercase constant. Every real deployment attempt threw
`InvalidOperationException` before `GameserverSDK.Start()`'s heartbeat could ever begin, which
PlayFab reports as "Unhealthy" (no heartbeat within its own ~10-minute window) with nothing in
Game Manager pointing at the actual cause. Fixed in three places — the server's own lookup and
both of the client's (`RequestServerAsync` and the matchmaking-queue join path) — to compare
case-insensitively (`StringComparison.OrdinalIgnoreCase`), since a human retyping a name into a
portal field is exactly the kind of case drift worth being lenient about, not a real
configuration difference worth failing loudly over. `dotnet test` 411/411, Unity batchmode
compile clean (0 `warning CS`, 0 `error CS`).

**A PlayFab build's image is effectively immutable once created** (confirmed from Microsoft's own
docs — updating an existing build's image is a "Build Alias"/blue-green concept, not an in-place
swap), so the fix couldn't be deployed by pushing a new image to the same build. Rebuilt and
re-pushed `ltw-matchserver:mps` with the fix, then created a **second** build against it rather
than trying to update the first: **`BuildId 4dbf4418-7048-4d7e-a8ed-68c617dd6c0a`, same settings
(East US, 4-standby/2-core, port `game`/5117/TCP) — reported healthy.** Recorded into
`MultiplayerServerConfig.cs`. (That health report turned out to be hollow — see 2026-09-11 below.) The original `faee9e3e-...` build is abandoned — it can never come
up healthy regardless of image changes, since it's pinned to the broken one — and should be
drained (standby/max to 0) or deleted in Game Manager to stop it holding quota for nothing.

Still to do: run the actual live create-a-real-server verification against the new build — that's
the one MP-07/MP-05 acceptance check this pass didn't reach.

**2026-09-11 — two more real startup defects, stacked under the first.** The first live Play
Online attempt found `4dbf4418` Unhealthy with no servers listed. Deleting and re-adding its region
(Standby 1 / Max 2) produced a real provisioning attempt and a new status, **"propping failed"**:
the pushed image was **arm64** (`docker manifest inspect` — no `amd64` entry at all). Docker
Desktop on an Apple Silicon Mac builds for the host architecture unless told otherwise, and Azure's
Dasv4 VMs are x86-64, so no container of either earlier build had ever actually run — both
"healthy" reports were standby capacity that never existed. Rebuilt with `docker buildx build
--platform linux/amd64,linux/arm64` (the arm64 slice is what lets `LocalMultiplayerAgent` run the
same tag locally). A fresh build against that pulled fine and then showed **"too many
restarts"**: the container started and exited repeatedly. Reproduced locally by running the image
as PlayFab does — as the non-root `app` user from the 2026-09-06 security-audit L4 hardening —
against a root-owned `mode=755` `/data/GameLogs` mount: `UnauthorizedAccessException` inside the
C# GSDK's own `FileSystemLogger.Start()`, exit 134, before the first heartbeat. Writable mount:
runs. `LocalMultiplayerAgent` alone had passed (`StandingBy → Active`) because Docker Desktop's
macOS bind mounts let any uid write — which is also why the 2026-09-05 LMA verification, done the
day before the `USER` change, never saw it. Fixed with `src/LTW.MatchServer/docker-entrypoint.sh`
(root for one `chown` of the log mount, then `exec setpriv` to `app` — the pattern PlayFab's own
Unreal Linux guide documents; L4's non-root decision is kept, not reverted). Verified: the
root-owned-mount test passes (GSDK log written, owned by `app`), standalone mode still starts and
receives its port argument, full LMA lifecycle `StandingBy → Active`. Pushed multi-arch as
**`ltw-matchserver:mps-20260911`** — a unique tag, because `mps` was re-pushed three times during
this and any VM that pulled it earlier may hold a different image; PlayFab's guidance is one tag
per build, now written into the runbook along with the two-part local gate. `4dbf4418` is
abandoned like `faee9e3e` before it. Build created against `mps-20260911`:
**`BuildId 36cfe0aa-ce3d-45a3-9ed8-b740f0b9b588`**, servers observed starting (not "propping
failed", not "too many restarts") — recorded into `MultiplayerServerConfig.cs`.

**First Play Online tap against it (iPad, same day) found a fourth defect, client-side this time:**
`Could not get an entity token: You must set PlayFabSettings.TitleId before making API Calls.`
`PlayFabConfig.EnsureConfigured()` was only ever called from a live login
(`PlayFabLoginService`), but the iPad had a persisted session from an earlier build and
`PlayFabSession.TryRestore()` — which M-C4 already taught to re-arm the SDK's *auth* for a
restored session — never re-armed its *configuration*. Any process that restores rather than logs
in therefore had a session ticket and no title ID, and the MPS path's `GetEntityToken` (its first
SDK call) was the first thing to notice. Fixed by calling `EnsureConfigured()` from `TryRestore()`
itself. Never reachable before: the direct-connect path never touched the PlayFab SDK after
restore, so this only existed once `UseMultiplayerServers` was on.

**Second tap, same iPad: the entity token now succeeded and the client reached
`/Match/CreateMatchmakingTicket`, which failed with "Could not find a queue config for the
referenced queue"** — `MultiplayerServerConfig.MatchmakingQueueName` was still its placeholder,
because MP-05's Phase 2 (create the queue in Game Manager) needs a healthy BuildId to point at
and one only existed as of that hour. Two consequences. The client-side one is fixed:
`QueueForMatchAsync` treated a ticket-creation failure as fatal (`onFailure`, return) and never
reached the direct-request fallback sitting right below it — contradicting its own design, in
which matchmaking is opportunistic and the direct request is the guaranteed solo route. It now
logs a warning and falls through to `RequestServerAsync`, so a missing or momentarily unpublished
queue can't stop a solo player; mid-flow failures after a ticket exists still fail loudly rather
than starting a second attempt on top of a reported one (`AwaitPooledMatchAsync`'s
`PooledOutcome`). The portal-side one is Phase 2 itself, still to do.

**Third tap: the fallback worked and a real `RequestMultiplayerServer` reached PlayFab for the
first time** — answered `NoHostsAvailableInRegion — No Hosts available in regions 'EastUs'`: a
capacity statement (no server at Standing By at that instant), not a code error; being checked
against the build's Regions tab. Reading ahead to what waits behind it found a fifth defect,
structural rather than a bug: under MPS the container receives no `PLAYFAB_TITLE_ID` /
`PLAYFAB_SECRET_KEY` (PlayFab's build form has no environment field — the very reason the mode is
a build arg), so `playFabAuthority` is null and `ServerMatch.AcceptWithPlayFabAsync` refuses every
PlayFab-identified seat. Every allocated server would have accepted the WebSocket and rejected the
join. Fixed via the GSDK's own channel: build **metadata** is merged verbatim into
`getConfigSettings()` (confirmed in `InternalSdk.cs`, lines 140–143, not assumed), and `titleId`
is already in the config. `Program.cs` now builds the authority from the environment first and,
under MPS, from `titleId` + a `PLAYFAB_SECRET_KEY` metadata entry second, warning loudly through
GSDK's own log if neither exists. Metadata is build-definition (immutable), so this needs one more
build — `36cfe0aa` joins the abandoned list. Trade-off recorded in PLAYFAB_SETUP.md's "Handling
the Secret Key". `dotnet test` 411/411. Verified under `LocalMultiplayerAgent` with a dummy
metadata value: `PlayFab configured: title FBC34 (from GSDK config + build metadata)`, then
`StandingBy → Active`, match bootstrapped. Pushed multi-arch as **`ltw-matchserver:mps-20260911b`**;
build created against it with the metadata: **`BuildId b1f71d89-87ab-40e4-82fc-01acbf88d4e3`**,
recorded into `MultiplayerServerConfig.cs`.

**Fourth tap, against `b1f71d89`: PlayFab allocated a real server** — `RequestMultiplayerServer`
succeeded, the poll reached `Active`, the client dialed `ws://40.76.70.77:30100/matches/<id>/join`
— and the WebSocket connect failed: "Unable to connect to the remote server". Established, in
order, rather than guessed: (1) the same cookie shape replayed under `LocalMultiplayerAgent`
bootstraps a match and the listener answers a WebSocket upgrade with HTTP 101 through the port
mapping; (2) from the Mac, on the same LAN, that exact Azure address answered a TCP handshake and
an upgrade to that exact match path with HTTP 101 in 77 ms — **the server was alive and reachable
while the iPad failed**; (3) Safari on the iPad itself loads `http://40.76.70.77:30100/` (the
listener's empty 404) — the iPad's network path is fine too. So the failure is inside the app on
that iPad: its socket reaches a LAN address (the earlier direct-connect test) but not this public
one, while Safari does. Open suspects, being checked on the device: iCloud Private Relay / "Limit
IP Address Tracking", a Wi-Fi proxy or filtering profile. Two client changes landed meanwhile:
`JoinAsync` now logs the host and port it dials (this failure was blind without it), and
`RequestServerAsync`/the matchmade path prefer PlayFab's `FQDN` over the IPv4 literal — PlayFab
added the name specifically for IPv6-only iOS networks, and it's the more robust choice whatever
this turns out to be.

**Fifth tap: dialing by hostname connected.** `SHELL joining match … at
dnsfbc34-….eastus.cloudapp.azure.com:30100`, and the WebSocket handshake succeeded — so that
iPad could not dial a raw IPv4 literal to a public address from inside the app while Safari
could, and the FQDN change is the fix rather than a hedge (root cause on the device side left
unidentified; it no longer matters). The join then reached `ServerMatch.AcceptWithPlayFabAsync`
for the first time and was **refused** — the server closed with `PolicyViolation`, the client
correctly called `ForgetOnAuthFailure` (M-C4), and the shell dropped back to SIGN IN WITH GOOGLE.
Which of the three refusal branches fired was unknowable: none of them, nor any of
`PlayFabSessionAuthority`'s six silent exits, logged a reason. Both now do (reason only — never
the ticket or the secret; a 401 there means the *secret* was rejected, distinct from an expired
ticket). Leading suspect: an expired ticket — the iPad's session was persisted from a sign-in
several exports ago and restored without a live login ever since; PlayFab tickets live 24 hours.
`dotnet test` 47/47 on the server suite.

**Sixth tap, after a fresh Google sign-in: refused again** — so not (only) an expired ticket.
Remaining branches: no secret reached the container (e.g. a metadata key not spelled exactly
`PLAYFAB_SECRET_KEY` — the lookup is case-sensitive), a wrong secret (PlayFab answers 401 to the
verification call), or a seat map that didn't survive the trip. Pushed the reason-logging server
as **`ltw-matchserver:mps-20260911c`**; build created against it with the metadata:
**`BuildId 6c5906bf-e800-48f9-a0df-0f6da6b30aeb`**, recorded into `MultiplayerServerConfig.cs`.
**Seventh tap, against `6c5906bf`: refused again — and "no logs to download".** Which exposed a
sixth gap: PlayFab archives a server's log only when the server *exits*, and
`ServerMatch` "happily plays a bot-vs-bot match for a human who was refused at the door or
never dialed", so a refused join left the VM allocated (billed) for a whole bot match with its
log — the one that now names the refusal reason — unreachable the entire time. Fixed in
`Program.cs`: under MPS, if no human binds a seat within 60 s of allocation the server ends the
match and exits; a match that had a human and lost them stays MP-06 reconnect's concern. Verified
under `LocalMultiplayerAgent` with its own terminate threshold raised to 300 heartbeats so only
this timeout could act: exactly 60 s after bootstrap, `no human joined within 60s of allocation —
ending it so this server can exit`, container exit 0. Pushed as **`ltw-matchserver:mps-20260911d`**
(reason logging + this). `dotnet test` 47/47.

**Root cause of the refusals, read through PlayFab's API once the title secret was back in
`.env.local`: `GetBuild` on `6c5906bf` returned `metadata: {}`** — no metadata at all — and the
refused session's archived log (fetched via `ListArchivedMultiplayerServers` +
`GetMultiplayerServerLogs`) said it in the server's own words: `PlayFab not configured: no
PLAYFAB_SECRET_KEY in this build's metadata`, then `refused seat 1 — this server has no PlayFab
authority`. The next build created through the form, "LineWards East 8" (`4a31e6b5`, on
`mps-20260911d`, every other setting correct), had `metadata: {}` too — so Game Manager's New
Build form simply has no way to set metadata, and the runbook step that said to type it there was
wrong. Two of two. Pivoted to PlayFab's sanctioned channel, *game secrets* (`UploadSecret` +
`GameSecretReferences`; delivered to servers as `PF_MPS_SECRET_<name>`, per
learn.microsoft.com/gaming/playfab/multiplayer/servers/manage-secrets): `Program.cs` now reads
`PF_MPS_SECRET_PlayFabSecretKey` first (the secret is named `PlayFabSecretKey` — `UploadSecret`
rejects underscores, names must match `^[0-9a-zA-Z-]+$`; the server also accepts any
`PF_MPS_SECRET_*` matching ignoring case and separators and logs which name it found, since the
agent's exact spelling can only be confirmed on a real VM), metadata second; `tools/playfab/create_build.py` does
the whole deploy through the API (upload/refresh the secret, create the build referencing it,
verify via `GetBuild`) since the form can't. Pushed as **`ltw-matchserver:mps-20260911e`**.
`dotnet test` 47/47. One more consequence: the title secret was pasted into chat once during
this and should be rotated; the script's upload step then distributes the new one. Pushed as
**`ltw-matchserver:mps-20260911f`** after the rename; `create_build.py` then uploaded the secret
and created **"LineWards East 9", `BuildId b922cefb-e900-49fa-84d4-f3d8cf40999a`**, with
`GetBuild` confirming `GameSecretReferences: ['PlayFabSecretKey']`, port `game`/5117/TCP, East US
1/2 — the first build that provably carries the secret. Recorded into
`MultiplayerServerConfig.cs`. `dotnet test` 47/47. Four servers Standing By 82 s after creation. Then an API probe — a
`RequestMultiplayerServer` from the title with a cookie for a player who'd never join — proved
the whole server side on a real VM: allocated Active immediately, and its archived log read
`entrypoint: log directory /data/GameLogs/ now owned by app` → `PlayFab configured: title FBC34
(from GSDK config + PlayFab game secret (PF_MPS_SECRET_PlayFabSecretKey))` (the documented
name, verbatim, no fallback needed) → match bootstrapped → `no human joined within 60s of
allocation — ending it so this server can exit`, archived 73 s after allocation. **Then the real tap, 2026-09-11 20:49 UTC: the first match on PlayFab Multiplayer Servers,
played to the end.** iPad → fresh Google sign-in → Play Online → matchmaking fell back (queue
still not created) → `RequestMultiplayerServer` → `ws://dnsfbc34-….eastus.cloudapp.azure.com`
→ ticket verified → seat 1 bound → 541 s of play → `match_ended` with `winnerId 1`; the server
exited and its log plus `replays/<matchId>.json` were archived. "No major errors" from the
player's side; the server's archived log shows `PlayFab configured … (PF_MPS_SECRET_
PlayFabSecretKey)`, `match_started`, `match_ended`, nothing refused, nothing thrown. This closes
MP-07's "live create-a-real-server" acceptance check and MP-05's "a real session ticket
authenticating a real connection", both open since 2026-09-05. Seven builds, six real defects
(port-name case; arm64 image; non-root vs root-owned log mount; TitleId unset on session
restore; matchmaking failure not falling back; no secret reaching the container — plus the
unjoined-match exit and the IPv4-literal dial found along the way), each hidden by the one before.
Still open from this pass: create the matchmaking queue (MP-05 Phase 2 — pooling is untested,
solo works); delete the six dead builds; rotate the title secret; H5 (`ws://` → `wss://`).

- **A known, accepted gap, not solved by this pass**: Azure can recycle the VM hosting an already-
  allocated (live, in-match) server for maintenance (`GameserverSDK.RegisterMaintenanceCallback`
  gives advance notice, but no in-match mitigation exists). MP-06's reconnect logic assumes the
  same server process survives a network hole or kill-and-relaunch — it does not survive its own
  process being recycled out from under a live match. Revisit if this is observed live.
- **The runbook has a first draft**: `docs/MP07_RUNBOOK.md` — deploy, roll back, drain, and
  investigate a desync from its replay. Writing it surfaced a real bug (see below), now fixed.
  Telemetry and abuse handling (this section's other two deliverables) have not been started.
- **A real bug found and fixed while writing the runbook, 2026-09-05**: `Program.cs` originally
  computed `replayDirectory` once, before the standalone/mps mode branch even ran, always pointing
  inside `AppContext.BaseDirectory` — a path that lives only inside a match's own ephemeral
  container under MPS and is gone the instant PlayFab deletes it. Replays were silently
  unrecoverable for every MPS-hosted match, making the runbook's own "investigate a desync from
  its replay" section impossible to actually follow. Fixed by moving `MatchRegistry` construction
  into each mode branch, and in `mps` mode pointing `replayDirectory` at
  `GameserverSDK.GetLogsDirectory()` instead — the same directory PlayFab's VM agent zips and
  archives after a server ends (confirmed via `com.playfab.csharpgsdk`'s "Logging" contract: any
  file placed there, not just ones written through `GameserverSDK.LogMessage`, gets included).
  **Confirmed working live** via `LocalMultiplayerAgent`: a completed match's
  `replays/<matchId>.json` was found inside the collected `GameLogs/<id>/` folder afterward, with
  real seed/content-version/roster/command data intact. `dotnet test` (384/384, standalone mode's
  own path unaffected) stayed green throughout.
- `com.playfab.csharpgsdk` is recorded in `docs/MVP_DEPENDENCIES.md`'s third-party dependency
  table — a real new runtime dependency, unlike `PlayFabSessionAuthority`'s deliberate
  hand-rolled-REST non-dependency.

### Estimated cost, 2026-09-05 — pre-Phase-5, replace with real billing data once live

Worked out before any real PlayFab build exists, to sanity-check the free tier actually covers
this project's near-term scale before spending time on Phase 5's portal setup. **Not a quote** —
PlayFab's exact per-core-hour consumption rate for the Dasv4 family isn't published as static,
fetchable text (it's a JS-rendered pricing table); the one confirmed source
([Billing for PlayFab Multiplayer Servers 2.0](https://learn.microsoft.com/en-us/xbox/playfab/multiplayer/servers/billing-for-thunderhead))
gives the free allotment and egress rate, not the overage rate. Overage figures below are anchored
to Azure's own published raw Dasv4 IaaS pricing (~$0.048/core-hour for a 1-core VM) as a floor —
PlayFab's managed rate is likely somewhat higher.

Confirmed: **750 Dasv4 core-hours/month free in East US**, up to 24 simultaneous cores, 10GB free
egress (then $0.05/GB in East US). The architecture this session landed — one `LTW.MatchServer`
process per match under MPS, exiting when the match ends (see "Landed 2026-09-05" above) — means
cost tracks (standby VMs kept warm) + (VM-hours actually running matches), not shared server
capacity.

For a 10-person beta on a 1-core VM, over a month:

| Posture | Core-hours/month | Within free tier? | Estimated cost |
| --- | --- | --- | --- |
| No standby — allocate on demand (slower join, no pre-warmed server) | ~50 (10 testers × ~5 matches/week × ~15 min × 4 weeks) | Yes, with wide margin | **$0** |
| 1 standby server kept warm 24/7 (faster, more consistent join) | ~720 (standby alone) + ~50 (play) ≈ 770 | No — ~20 hours over | **~$1–5** (overage estimate only) |

Egress is a non-factor at this population — a tower-defense wire protocol's traffic per client is
tiny relative to the 10GB free allotment. **Bottom line: a 10-person beta is realistically $0–$5/
month**, dominated by whether a server is kept warm for responsiveness, not by actual play volume.
Replace this whole section with real billing data once Phase 5's build is live for a full month.

### Deliverables

- Hosting with a cost ceiling and a scale-to-zero posture for a small population.
- Telemetry: match health, desync reports, crash reports (from the launch roadmap's crash
  reporting), abuse signals.
- A runbook: deploy, roll back, drain, investigate a desync from its replay. First draft:
  `docs/MP07_RUNBOOK.md`, 2026-09-05; telemetry and abuse-handling sections added 2026-09-09.
- Abuse handling: rate limits are MP-03; here it is reporting, muting and banning at the
  identity level.

### Telemetry (emission side) and ban enforcement, landed 2026-09-07

Started while still blocked on the Dasv4 quota approval — scoped to what is buildable and
`dotnet test`-verifiable without a live PlayFab title or real Azure resources, which a genuine
dashboard needs.

- **`ServerMatch` now emits one structured JSON line per match lifecycle event** —
  `match_started` (human seat count, ticks/second), `match_ended` (winner, completed-at tick,
  duration), `match_faulted` (exception type/message, duration) — to stdout via a swappable
  `ServerMatch.TelemetrySink` (defaults to `Console.WriteLine`; tests capture it instead of
  redirecting the real `Console`, to stay safe under parallel test runs). Deliberately stdout, not
  `GameserverSDK.LogMessage`: that requires `GameserverSDK.Start()`, which standalone mode never
  calls, and `ServerMatch` has no way to know which mode it's running under — stdout already flows
  into both `docker logs` (standalone) and PlayFab's own MPS log collection (per `Program.cs`'s
  own bootstrap-message logging, which already relies on the same channel). Verified with two new
  tests: `Match_lifecycle_emits_structured_telemetry_for_start_and_end` (a real match run to a
  real conclusion) and `A_faulted_match_emits_structured_telemetry` (a `ticksPerSecond: 0` match,
  the same `OverflowException` H4's own fix already catches).
- **This is the emission side only, not the dashboard.** "Cost, crash, desync and abuse figures on
  one dashboard" needs something ingesting these lines — Azure Monitor, Application Insights, or
  similar — which needs real Azure resources this environment cannot provision. What exists now:
  every match's start/end/fault is a structured, greppable line instead of only free-text
  `Console.Error.WriteLine` prose.
- **Crash reporting (client-side) has not been started** — `docs/LAUNCH_ROADMAP.md` item 3/#1
  calls for "Unity Cloud Diagnostics or Sentry, whichever sets up faster," and either needs a real
  vendor account and API key, which is a product decision plus a credential only the project owner
  can provide. Not attempted here.
- **Ban enforcement, at the identity level, landed**: `PlayFabSessionAuthority.AuthenticateAsync`
  now also rejects a ticket whose `UserInfo.TitleInfo.isBanned` is `true` (confirmed field, from
  `AuthenticateSessionTicket`'s own documented response shape, fetched 2026-09-07) — one extra
  check on a response this class already fetches and parses, no new API call. This is explicitly
  defense-in-depth, not the primary mechanism: PlayFab's own ban system (Game Manager → Players →
  select player → Bans → Add Ban) already invalidates a banned player's existing session tickets
  outright and rejects future login attempts (confirmed from Microsoft's own docs) — no custom
  ban-issuing tool was built, because PlayFab's portal already is one. New tests:
  `A_banned_players_ticket_is_rejected_even_when_not_reported_as_expired` and
  `A_non_banned_players_ticket_with_TitleInfo_present_is_still_accepted`.
- **Reporting and muting were assessed and NOT built**: this game has no chat or other
  player-to-player communication channel today (checked — no such feature exists anywhere in
  `Assets/Scripts`), so there is nothing concrete yet for a player to report or for a mute to
  silence. Building either now would be speculative UI for a threat that doesn't exist in this
  game yet. Revisit if/when any player-to-player communication feature is added.

### Acceptance Checks

- [ ] One week of live matches with cost, crash, desync and abuse figures on one dashboard.
- [ ] A desync report can be reproduced from its replay by a second person using the runbook.
- [ ] The monthly cost at ten times the observed population is known.

**Estimate:** the hosting mechanism itself is built and live-verified end to end (locally, via
`LocalMultiplayerAgent`). Telemetry's emission side and ban enforcement are landed (see above), and
the runbook's telemetry/abuse sections are now written (`docs/MP07_RUNBOOK.md` sections 5–6, how to
actually find and read the emitted data and issue a ban today, by hand). Phase 5's PlayFab Game
Manager portal work is done as of 2026-09-09 (quota approved, image pushed, client access enabled,
build submitted and provisioning — see Phase 5 above); once a `BuildId` exists, what remains before
these acceptance checks are even attemptable is a real log-ingestion dashboard and client-side
crash reporting (blocked on a vendor decision and account credentials). Those two become
substantially a matter of wiring existing pieces together, not further from-scratch engineering.

---

## Two shapes this plan can be cut into

| Push | Initiatives | Delivers | Rough size |
| --- | --- | --- | --- |
| **Recorded opponents** | MP-00, MP-01, MP-02 (MP-03 in parallel) | Real opponents, no server, works at any population | 3–4 weeks |
| **Live multiplayer** | MP-00 through MP-07 | Server-authoritative matches with matchmaking | 9–12 weeks before operations settle |

Both begin with MP-00, which is why it is the one exception to the launch freeze.

## Explicitly not in this plan

Ranking and seasons, cosmetics and monetization (`MONETIZATION_AND_PAYMENTS.md`), spectating,
cross-play concerns beyond a shared content version, and any change to gameplay rules. A
multiplayer push that finds a balance problem records it in `GD_TUNING_LOG.md` and fixes it as a
gameplay change, not as part of this plan.
