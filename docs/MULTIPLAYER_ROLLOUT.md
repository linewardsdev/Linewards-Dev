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

### Acceptance Checks

- [ ] A player queues alone and is in a match within the bounded wait, against bots.
- [ ] Four players queuing together land in one match with four bot seats.
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

**Owner:** Client. **Status:** partial — the snapshot already carries per-seat state for this reason.

### Deliverables

- The HUD, rails, board and results render from the wire only; no code path reads the
  simulation directly when a server is present.
- Latency handling: input acknowledgement within a frame, authoritative correction without
  visible snapping for the common case.
- Reconnect UX: a network hole shows a state, not a freeze; a reconnect resumes the seat.
- The tutorial and Practice stay local; they never touch the server.

### Acceptance Checks

- [ ] A 2 s network hole mid-match is survivable with no desync.
- [ ] Kill the app and relaunch inside the reconnect window: the seat resumes.
- [ ] `RealUiCaptureRunner` shots for the lobby, the connecting state and a reconnect exist and
      are reviewed at phone and iPad widths.

**Estimate:** one to two weeks.

## MP-07: Operations

**Owner:** Server. **Status:** nothing exists.

### Deliverables

- Hosting with a cost ceiling and a scale-to-zero posture for a small population.
- Telemetry: match health, desync reports, crash reports (from the launch roadmap's crash
  reporting), abuse signals.
- A runbook: deploy, roll back, drain, investigate a desync from its replay.
- Abuse handling: rate limits are MP-03; here it is reporting, muting and banning at the
  identity level.

### Acceptance Checks

- [ ] One week of live matches with cost, crash, desync and abuse figures on one dashboard.
- [ ] A desync report can be reproduced from its replay by a second person using the runbook.
- [ ] The monthly cost at ten times the observed population is known.

**Estimate:** one week to stand up, then ongoing.

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
