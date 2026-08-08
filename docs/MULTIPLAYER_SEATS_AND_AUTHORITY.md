# Multiplayer Seats And Command Authority

Groundwork for networked remote players. This document covers what landed on
`multiplayer-seats-and-authority` and what still stands between here and a real
networked match.

The guiding constraint is the one already stated in `docs/ARCHITECTURE.md`: online
multiplayer is "a later replacement for the source of player commands, not a rewrite of
combat or pathing." Everything below is about making the *command source* pluggable and
the command *rules* trustworthy, not about changing gameplay.

## Where The Simulation Already Was

More was ready than expected. Before this pass, `LTW.Simulation` already had:

- `LocalMatchTopology` supporting 2-8 lanes, one defender per lane, with carousel routing
  (`NextActiveOpponent`, leak transfer to the next *active* opponent).
- Fully player-parameterized commands: `PlaceTower(playerId, laneId, ...)`,
  `QueueSend(playerId, ...)`, `SellTowerAt(playerId, ...)`.
- Per-player economy isolation in `EconomyPlayerSet` (gold, income, lives, elimination).
- A deterministic fixed-tick loop plus `ReplayRecord` (seed, content version, accepted
  commands) — the substrate an authoritative server needs.
- Per-lane bot enable/disable already expressed as data (`BotLaneOptions`).

The gaps were concentrated in the *client* (which assumed it was always player 1) and in
*command authority* (rules that were designed and documented but never enforced).

## What Landed

### 1. The local seat is now explicit

`LocalMatchOptions.LocalPlayerId` names which seat the local human occupies, replacing
hardcoded `new PlayerId(1)` / `new LaneId(1)` literals that were spread across six call
sites in `UnityCommandAdapter` plus `HudView`, `RuntimeMatchHud`, and
`TouchPlacementController`. `LocalVerticalSlice` exposes `LocalPlayerId` and
`LocalPlayerLaneId`; `UnitySimulationDriver` re-exposes both so presentation code asks
rather than assumes.

Moving the seat moves the bots with it. `IsBotEnabledFor` returns false for the local
seat regardless of lane config, and `DefaultLanes` gained an entry for player 1 that only
activates when the human sits elsewhere. So seating the human in lane 3 makes lane 3
human-driven *and* lane 1 bot-driven, with no other configuration. `CreateBots` previously
hardcoded "player 1 is never a bot"; that rule is now expressed by the seat instead.

Verify it with the batch runner:

```
-ltwLocalPlayer 3
```

Evidence from two runs of the same seed, one per seat, is under
`docs/playtest-evidence/local-unity-batch-seats-*`. Both complete cleanly, with the bot
roster flipping exactly as intended.

### 2. Lane ownership is enforced on tower placement

**This was a live exploit, not a hypothetical.** `ValidateTowerPlacement` took the lane id
on trust. Confirmed empirically before fixing: `PlaceTower(P1, lane 5, ...)` returned
accepted, deducted P1's gold (100 → 86), and left a P1-owned tower defending P5's lane.

It never surfaced locally only because the client always passed lane 1. With remote clients
submitting their own commands it becomes griefing — reshaping an opponent's maze, or
spending into their lane to distort their route. Selling already checked ownership
(`SellTowerAt` filters on `OwnerId`); building did not.

Placement now requires `laneId == topology.HomeLaneFor(playerId)` and rejects with
`NotOwner` otherwise.

### 3. Out-of-match players are rejected instead of throwing

`PlayerId.IsValid` only means "positive", so an id outside the match reached the economy
lookup and threw `KeyNotFoundException`. Over the wire that is a rejectable command, not an
exceptional program state. `LocalMatchTopology.HasPlayer` now backs a clean `InvalidPlayer`
rejection.

### 4. The send cooldown is actually enforced

> **Superseded.** This section records enabling the cooldown, and it did happen — but the cooldown was
> later removed outright (`74b8519`), and `LocalVerticalSlice` now constructs with
> `sendCooldownTicks: 0`, so gold is the only thing gating a send. The enforcement code and its tests
> are intact and the rule can be switched back on by changing that one number; what follows describes
> why it was built, not how the game currently behaves. The 900-1,800 tick target quoted below has
> also since moved twice — see `MVP_STATUS.md`.

`EconomyRules.SendCooldownTicks` (30), `PlayerEconomyState.NextSendAvailableTick`,
`WithNextSendAvailableTick`, and `CommandRejectionReason.CooldownActive` all already
existed — and **nothing read or set any of them**. The "global 30-tick send cooldown"
described in `docs/GD_TUNING_LOG.md`'s baseline, and assumed by its own open question
("Does the global 30-tick send cooldown create enough breathing room...?"), was never
running. Sends were limited only by gold.

`EconomyService.QueueSend` now rejects with `CooldownActive` inside the window and stamps
the next available tick on accept.

**This is a real balance change, not just a bug fix**, and was made as a deliberate call
rather than silently: bots previously sent every tick. Enabling the cooldown lengthened the
reference bot-vs-bot match from 1012 to 1643 ticks (+62%) — which moves it *into* the
900-1800 match-completion target from `GD_TUNING_LOG.md`'s first entry, rather than below
it. Worth watching in playtests; the value is one constant in `LocalVerticalSlice`'s
`EconomyRules` if it needs tuning.

Two tests encoded the old behavior by name and had to be replaced rather than patched:

| Old | New | Why |
| --- | --- | --- |
| `EconomyTests.Repeat_sends_are_allowed_until_gold_runs_out` | `Repeat_sends_are_gated_by_the_send_cooldown_then_by_gold` | Old test built the service with `sendCooldownTicks: 30`, sent at ticks 10 and 20, and asserted acceptance — it described observed behavior, not the intended rule. |
| `VerticalSliceBridgeTests.Repeat_sends_are_limited_by_gold_only` | `Repeat_sends_within_the_cooldown_window_are_rejected` + `Sends_spaced_past_the_cooldown_are_all_accepted` | Same reason; split so cadence and gold limits are asserted separately. |

Three more tests did back-to-back sends only to set up a scenario and were spaced past the
cooldown; `Two_bots_complete_a_local_carousel_match`'s upper bound moved 1200 → 1800.

## What Is Still Missing

Ordered as recommended. Steps 1-2 need no networking and are testable with the existing
batch harness.

1. **Command queue and tick boundary.** Commands are applied immediately on call. They need
   to be submitted, then applied at a tick boundary in a deterministic order
   (tick, playerId, sequence). Without this, two clients' same-tick commands resolve by
   arrival order, which will not agree across peers. This is the largest structural change
   remaining and it makes the local and networked paths identical. Creep spawning should
   move out of `QueueSend` into that tick-application step at the same time.
2. **Seat assignment as data.** A seat should be `human | bot | empty` with bot-fill for
   empty seats, plus disconnect and reconnect handling. `LocalPlayerId` is a single local
   seat; a match needs a seat *table*.
3. **Session and identity.** Connection/account → seat mapping, lobby, ready-up, and match
   lifecycle. None of this exists.
4. **Transport and the server.** `src/LTW.MatchServer` is still a README-only placeholder
   and is not in `LTW.sln`. Per `ARCHITECTURE.md`: headless .NET service, WebSockets,
   server-authoritative, reusing `LTW.Simulation`.

## Rules An Authoritative Server Must Keep Enforcing

Collected here because they are easy to lose when the command source changes:

- A player may only build in their own home lane (`NotOwner`).
- Send targets are derived from topology (`NextActiveOpponent`), never accepted from the
  client. This was already correct and must stay that way.
- Send cadence is rate-limited per player (`CooldownActive`).
- Unknown or out-of-match player ids are rejected, never thrown on.
- Gold, income, lives, kill bounties, and leak results are server-owned; clients submit
  intent only.

---

## Command authority: the boundary is now a type (2026-08-08)

The gap this document names — rules "designed and documented but never enforced" — has its first
enforced piece.

`ISeatAuthority` answers *which seat is this command allowed to act as*. `EnqueueSend` resolves the
seat through it and **overwrites the `PlayerId` it was passed**. In a local match those are the same
value and the call looks pointless; over a wire it is the difference between a queue and a
seat-spoofing hole, because `PlayerId` is a field on the message and a client controls it.

`LocalSeatAuthority` is the single-process implementation. It still checks that the claimed seat is
in the match rather than accepting anything, so a client bug that acts as a bot's seat fails now
instead of at integration.

**What the server implementation must change, and it is not the interface.** `LocalSeatAuthority`
answers "is this a seat in this match", which is the right question locally and the wrong one on a
server. The server version answers "is this the seat on *this connection*" — same signature,
different question, and that substitution is the entire point of the boundary existing early.

### Still reading their own PlayerId

Only the enqueue path goes through the authority. `PlaceTower`, `QueueSend`, `BuyCategoryTier`,
`UpgradeTower`, `SellTowerAt` and the batch operations all still trust the id they are handed. That
is safe in-process and is a hole the moment a socket is involved, so routing them through the same
authority is prerequisite work for a networked build rather than a follow-up to it.

### Per-seat state that now travels in the snapshot

The send queue is per-seat private intent, and it is carried in `VerticalSliceSnapshot` rather than
read off the simulation. That matters for the same reason gold does: under a server the snapshot is
what arrived over the wire, and a client reaching into the simulation for the value is reading a
local guess that drifts on the first dropped message — silently, and only for the player affected.

Note what is *not* in the accepted-command stream: the enqueue. The queue is intent; the resulting
send is the fact, and only the fact is recorded, at the tick gold actually reached it. Recording
both would make a replay apply the intent and its effect and double every queued send.
