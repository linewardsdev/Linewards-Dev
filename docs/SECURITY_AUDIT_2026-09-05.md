# Security & Standards Audit — 2026-09-05

## Purpose

A full read-through audit of `src/LTW.MatchServer/`, `unity/LTW.UnityClient/Assets/Scripts/Online/`
(excluding vendored third-party SDKs), and `src/LTW.Simulation/`, run while MP-07/MP-05 live
verification was blocked on a pending Azure quota approval. Three reviewers ran in parallel with
disjoint mandates (server security, client security, engineering standards); findings were
deduplicated where two reviewers independently caught the same bug, and every claim below was
re-verified by reading the cited code — the process-kill and zombie-match findings were additionally
confirmed by running probes against the real .NET 10 runtime and the real `LTW.Simulation`
assembly, not inferred.

Full report with per-finding severity, code excerpts, and fixes:
[published artifact](https://claude.ai/code/artifact/b87d8ac9-fea2-45ca-934e-5e0698b210ef). This
doc is the git-tracked record of the same findings, kept in sync as each is fixed (see the status
column) — see `docs/SECURITY_CONSIDERATIONS.md` for the standing threat-model doc this audit checks
against.

**Headline**: seat authorization and secret handling are sound. The real exposure is
*availability* — three separate one-message ways for any authenticated player to kill or freeze the
whole server, plus a lock that doesn't cover what its own comment says it covers. Two findings are
in code written during this session's MP-07 pivot and would bite the moment PlayFab Multiplayer
Servers goes live; both are called out explicitly below.

## Status legend

- [ ] Open
- [x] Fixed (commit noted)
- [~] Partially fixed / mitigated, not closed

## Fix order

1. C1 + H1 — single-message process kills, each under twenty lines to fix.
2. H2 + H3 — lock the connection tables, time out sends.
3. H4 — observe the fire-and-forget loops; fix the MPS health callback.
4. M-C4 + M-C3 — required before MPS go-live.
5. M1 + M2 — input validation at the two untrusted boundaries.
6. M3 + M5 — match eviction, join rate limiting.
7. M4/H5 + M-C2 — TLS, move credentials out of URLs, Keychain/Keystore.
8. M-DET1, then the Lows.

## Critical

- [x] **C1 — `queueSend.quantity` integer overflow: one authenticated client OOMs the process, or
      mints ~2³¹ gold.** `ServerMatch.cs:545` passes the raw wire `Quantity` straight into
      `EconomyService.SendCostFor` (`EconomyService.cs:145`, unchecked `unitCost * quantity`) and
      `LocalVerticalSlice.cs:1313`'s `Enumerable.Range(0, quantity)` spawn loop. The only validation
      anywhere is `quantity <= 0` (`CommandContentValidator.cs:73`). Verified:
      `{"type":"queueSend","creepId":"creep.runner","quantity":429496730}` overflows cost to 4,
      passes validation, and tries to allocate 429M creeps — OOM. `quantity=214748375` overflows
      cost negative and would credit ~2.1B gold. **Fix**: reject `Quantity` outside `[1, 10]` in
      `ServerMatch.Handle(PlayerId, QueueSendMessage)` before touching the slice; `checked`
      arithmetic in `SendCostFor`/`IncomeGainFor` as defense in depth.

## High

- [x] **H1 — Unbounded inbound WebSocket message size.** `HttpMatchHost.cs:197-212` accumulates
      frames into a `MemoryStream` with no cap. Any client with a valid token can send one
      multi-gigabyte message and OOM the server. **Fix**: cap at ~16KB, close `MessageTooBig` past
      it; cap the unauthenticated `POST /matches` body the same way (`:113-114`).
- [x] **H2 — Connection tables mutated outside `matchLock`, contradicting its own doc comment.**
      `ServerMatch.cs:19-21` claims "no cross-connection race to resolve here" — false. `BindAsync`
      (`:388-420`) and `Disconnect` (`:426-432`, called from every receive loop's `finally`) write
      three plain `Dictionary`s and call `slice.GetSnapshot()` lock-free, while the tick loop's
      `BroadcastAsync` (`:595`) enumerates them lock-free and `nextConnectionId++` (`:414`) is a
      non-atomic read-increment-write. Verified failure modes: an attacker-triggerable match freeze
      via rapid reconnects racing `BroadcastAsync`'s `ToArray()` (throws, caught nowhere but H4's
      silent fault); low-probability seat confusion on two simultaneous joins landing on the same
      connection id. **Fix**: take `matchLock` in `BindAsync`/`Disconnect` and around the reads, or
      swap to one immutable snapshot replaced under the lock; `Interlocked.Increment` for the id.
- [x] **H3 — One non-reading client stalls the tick loop for the whole match.**
      `ServerMatch.cs:226,595-597,611` awaits each socket's send sequentially with
      `CancellationToken.None` and no timeout. A player who stops reading fills their TCP window in
      seconds; the broadcast then blocks forever, freezing every other player's match (and, under
      MPS, `Completion`, so the process never exits and keeps billing). **Fix**: per-send timeout
      (~2s), abort and disconnect on timeout; longer term, per-connection bounded outbound queues
      (also fixes M-S4, two unsynchronized writers on the same socket).
- [x] **H4 — Fire-and-forget loops fault silently; a dead match reports healthy to PlayFab
      forever.** `_ = RunLoopAsync(...)` (`ServerMatch.cs:170`) has no `try/finally` around
      `loopCompletion.TrySetResult()` (`:229`) — skipped by a bad `ticksPerSecond` (M1), the H2
      race, `WriteReplayAsync` throwing anything but `IOException` (`:326`), and verified: `Stop()`
      itself, since `WaitForNextTickAsync` throws `TaskCanceledException` on cancellation,
      contradicting this session's own doc comment that `Completion` resolves on `Stop()`. Under
      MPS, `Program.cs:194`'s `Task.WhenAny` then never returns and the health callback
      (`Program.cs:113`) reports healthy forever. **Fix**: catch-log-`finally` around both loop
      bodies (`ServerMatch.RunLoopAsync`, `HttpMatchHost.AcceptLoopAsync`); health callback returns
      false when the loop task is faulted; catch `Exception` (not just `IOException`) in
      `WriteReplayAsync`.
- [ ] **H5 — PlayFab session ticket travels in a cleartext `ws://` URL query string.**
      `OnlineMatchService.cs:199-200` hard-codes `ws://`/`http://`; the server is `HttpListener` on
      `http://+:{port}/` with no TLS path. The ticket is a bearer credential for the entire PlayFab
      Client API, not just this match, valid until title-configured expiry (24h default); the
      per-seat `token` is also replayable to evict the legitimate player. Documented as dev/LAN
      infrastructure pending real hosting (`HttpMatchHost.cs:16-18`, `MatchServerConfig.cs:6-8`) —
      reported for blast radius: it's the whole PlayFab identity, not just a match. **Fix**: TLS
      (terminating proxy in front of the server), move the credential to a header/first-frame, or
      exchange the long-lived ticket for a short-lived match-scoped token over HTTPS before it ever
      reaches the socket.
      **Decision 2026-09-06**: deferred, not fixed — the real mitigation is TLS termination on the
      real deployment, which is infrastructure blocked on the pending Azure/PlayFab hosting setup,
      not something fixable in code alone. A code-only move of the credential from the URL query
      string into a first WebSocket frame was considered and rejected: on a still-plaintext `ws://`
      socket the same bytes cross the wire in cleartext either way (this server does no URL
      logging today — grepped and confirmed), so that change would add real protocol complexity
      and ~14 test call-site churn for close to zero actual reduction in the sniffing risk this
      finding is about. Revisit once real TLS-terminated hosting exists.

## Medium

- [x] **M-C4 — `TryRestore` never hydrates the PlayFab SDK; every kill-and-relaunch under MPS
      leaves the player stuck "signed in" with no way out.** *(Written this session; latent only
      because `UseMultiplayerServers` is still `false`.)* `PlayFabSession.cs:65-83` sets only this
      class's own fields, never `PlayFabSettings.staticPlayer.ClientSessionTicket`. Verified:
      `GetEntityToken`'s auth type comes from `context.IsClientLoggedIn()`
      (`PlayFabAuthenticationAPI.cs:70-84`) — with nothing in context it sends no auth header and
      PlayFab rejects it, surfacing as a generic failure with no `ForgetOnAuthFailure()` call, while
      the sign-in button stays hidden because `IsSignedIn` is already true. **Fix**: in `TryRestore`,
      set `PlayFabSettings.staticPlayer.ClientSessionTicket`/`.PlayFabId`; treat PlayFab
      `NotAuthenticated`/`InvalidSessionTicket` from any call as an auth failure.
- [x] **M-C3 — Any disconnect during the 5s welcome window wipes a valid session.** *(Written this
      session.)* `OnlineMatchService.cs:224-236`'s comment claims a closed-during-join "is what a
      bad or expired ticket looks like," but `MatchWireClient.IsDisconnected` is set identically for
      a server close, any `WebSocketException`, or any other receive-loop exception (verified,
      `MatchWireClient.cs:116,139,149,208`) — `CloseStatus` is never inspected even though the
      server distinguishes `PolicyViolation "bad token"` from `InternalServerError "join failed"`.
      A network blip, not a bad ticket, wipes the session. **Fix**: surface `CloseStatus` from
      `MatchWireClient`; call `ForgetOnAuthFailure()` only on `PolicyViolation`.
- [ ] **M-C2 — Bearer session ticket persisted in plaintext `PlayerPrefs`.**
      `PlayFabSession.cs:16-17,42-44,100-102`. iOS: plaintext plist in backups. Android: plaintext
      `shared_prefs` XML. No sign-out UI exists; only `ForgetOnAuthFailure` clears it. **Fix**:
      Keychain (`WhenUnlockedThisDeviceOnly`, non-synchronizable) via a native bridge — the pattern
      already exists in `LTWGoogleSignInBridge.mm`; `EncryptedSharedPreferences` on Android.
      **Decision 2026-09-06**: held off, not fixed — this needs a real native Keychain/Keystore
      bridge (Obj-C + Java/Kotlin plus a C# P/Invoke layer touching entitlements and access
      groups), and this environment can only compile-check the C# side (Unity batchmode), not
      build or run the actual iOS/Android project. Shipping unverified native security code here
      risks a subtle bug (wrong access group, wrong entitlement) landing silently. Revisit with
      real device/simulator verification available.
- [x] **M1 — `CreateMatchRequest` numerics unvalidated, including from a client-authored MPS
      `SessionCookie`.** Verified `PeriodicTimer` behaviors (`ServerMatch.cs:177`): `ticksPerSecond
      = 0` → `OverflowException`; negative or `>1000` → `ArgumentOutOfRangeException`; exactly
      `-1000` → `Timeout.Infinite`, loop hangs forever (H4's zombie, but only *after* the client
      already got a 200 with tokens). `openingBuildWindowSeconds = 1e308` → `OverflowException` in
      the `TimeSpan` constructor — under MPS this crashes the process during allocation.
      `humanSeats: [999]` is accepted and binds, but any of that seat's economy commands throw
      `KeyNotFoundException` (self-harm, but see M2). **Fix**: one validator shared by
      `HandleCreateMatchAsync` and the MPS bootstrap — seats distinct and in
      `[1, LocalMatchOptions.MaxLaneCount]`, `PlayFabSeats` keys ⊆ `HumanSeats`, `TicksPerSecond` in
      `[1,1000]` (1000 is `PeriodicTimer`'s own ceiling, not 60 — the test suite legitimately runs
      matches up to 200 ticks/second), `OpeningBuildWindowSeconds` in `[0,300]`.
- [x] **M2 — A malformed client frame throws out of `DispatchAsync`; the connection is orphaned
      with no close frame.** Only `JsonDocument.Parse` is guarded (`ServerMatch.cs:452-460`).
      Verified throws from: a non-object root, non-string `type`, wrong-typed fields, an omitted
      `towerId`/`creepId` (DTO default `""` → `new ContentId("")` throws), non-positive `laneId`,
      negative coordinates, and the `new PlayerId(0)` fallback (`:471`) itself. `ReceiveLoopAsync`
      catches only `WebSocketException` (`HttpMatchHost.cs:215`), so a buggy-but-legitimate client
      silently stops receiving ticks with no error and no close. **Fix**: catch around
      deserialize+handle, answer with `ErrorMessage`; validate before constructing primitives;
      catch `Exception` in `ReceiveLoopAsync`, close `PolicyViolation`, log.
- [x] **M3 — Unauthenticated, unbounded match creation with permanent retention (standalone
      mode).** `MatchRegistry.cs` never removes a match (`:16,90`); each holds a slice, a
      bot-vs-bot loop running from creation, and every socket, never closed on match end.
      `POST /matches` has no auth, rate limit, or body cap, and the registry dict is unsynchronized
      under concurrent requests. **Fix**: evict on `Completion` and close sockets; cap concurrent
      matches; a creation secret for standalone; `ConcurrentDictionary` or a lock. (MPS mode
      unaffected — creation is disabled there by design.)
- [x] **M5 — Unauthenticated PlayFab `AuthenticateSessionTicket` amplification.** The socket
      upgrades before any credential check (`HttpMatchHost.cs:163`), then every request with any
      `playFabTicket` triggers a real PlayFab Server API call, no rate limit, no cache
      (`PlayFabSessionAuthority.cs:46-53`). Garbage tickets at volume exhaust PlayFab's quota and
      refuse legitimate players; each check pins a handler for `HttpClient`'s 100s default timeout.
      **Fix**: per-IP/seat join rate limit; short `HttpClient.Timeout`; `CloseOutputAsync` + abort
      on failed joins.
- [x] **M-S4 — Two unsynchronized writers on one WebSocket.** The tick loop's broadcast
      (`ServerMatch.cs:593-599`) and a dispatch reply (`:518-523`) both call `SendAsync` with no
      serialization. Linux's `ManagedWebSocket` queues internally (the reorder is already worked
      around in `MatchServerIntegrationTests.cs:219-225`); Windows' `HttpListener` WebSocket throws
      `InvalidOperationException`, uncaught, killing the tick loop (H4). **Fix**: per-connection
      `SemaphoreSlim` around sends, or one outbound queue per connection — same change as H3.
- [~] **M-DET1 — The match seed reaches gameplay through `System.Random`, and two comments say it
      doesn't.** `BotController.cs:76`'s `(int)(random.NextDouble() * CategoryCount)` is fed by
      `new System.Random(seed)` (`SeededRandomSource.cs:11`). The seeded algorithm isn't guaranteed
      stable across .NET runtimes; the server replays on .NET 10 while the client runs the same DLL
      under Unity Mono/IL2CPP — both use the same legacy generator today, so this is latent, but
      MP-04's "server replay matches what shipped" property rests on an unowned algorithm.
      `IRandomSource.cs:3-9` and `BotController.cs:832-833` both incorrectly say the seed is unused.
      **Fix**: an owned PRNG (xorshift/PCG) in `SeededRandomSource`; correct both comments.
      **Decision 2026-09-06**: comments fixed (`IRandomSource.cs`, `BotController.cs:832-833`); the
      PRNG swap itself was prototyped (a self-contained PCG32) and reverted. It changed
      `BotController`'s seeded line-preference draw for the SAME seed, which `BotMazingTests` and
      `VerticalSliceBridgeTests` tune against — three tests broke, not from pinning an exact random
      value (none do) but because the specific line a bot commits to for a given seed shifted with
      the algorithm, and those tests assert on the resulting gameplay. The swap is safe in
      isolation; landing it needs a coordinated re-tuning of those seeds/assertions, out of scope
      for a security-fix pass. Still open — latent, not active, since server and client currently
      share one generator family.
- [~] **M-T1 — A flaky test, a write-only replay format, and specific coverage gaps.** *(Not in
      the original numbered fix order above — this is test-suite hygiene, not itself a
      vulnerability, so it was never prioritized against the others.)*
      `MatchServerIntegrationTests.cs:156-158`'s `.Single(...)` on the first post-command tick can
      throw per the race the file itself documents at `:219-225`. The replay-capture test
      (`:416-418`) only checks the file contains `"commands"` — the written JSON has no reader
      anywhere in `src`, so replay capture is currently write-only. Untested: `ServerMatch.Stop()`
      → `Completion` (would have caught H4), every `DispatchAsync` error path, `ConnectionSeatAuthority`
      directly, `LocalVerticalSlice.QuoteTowerUpgrades` (called by the client, zero test references).
      **Partially closed as a byproduct of other fixes**: `ServerMatch_completion_resolves_when_Stop_is_called_directly`
      (added for H4) closes the `Stop()` → `Completion` gap;
      `Malformed_command_message_receives_an_error_instead_of_orphaning_the_connection` (added for
      M2) covers one `DispatchAsync` error path. Still open: the flaky `.Single(...)`, the
      write-only replay format, `ConnectionSeatAuthority` direct coverage, and
      `QuoteTowerUpgrades`.

## Low

- [x] **L1** — MPS `MatchId` (client-generated, `OnlineMatchService.cs:317`) reaches
      `Path.Combine(replayDirectory, $"{MatchId}.json")` (`ServerMatch.cs:323`) unchecked; a rooted
      value replaces the directory entirely (verified). Fix: `Guid.TryParse` or `Path.GetFileName`.
- [x] **L2** — `IosBuildRunner.cs:181-191`'s `-ltwAllowInsecureHttp` flag: only `"0"` disables (
      `false`/`no`/`off` all enable), and the simulator build path's `AssetDatabase.SaveAssets()`
      calls can flush the override into the committed `ProjectSettings.asset`. Currently clean
      (verified `insecureHttpOption: 0` committed) but fragile. Fix: strict parsing; snapshot/restore
      in a `finally` like MSAA already does.
      **Fixed 2026-09-06**: `ApplyInsecureHttpOverride` now accepts only exactly `"1"`/`"0"`
      (anything else warns and leaves the setting unchanged) and returns the pre-override value;
      `Export()`'s existing MSAA `finally` now also calls a new `RestoreInsecureHttpOverride`,
      restoring and `AssetDatabase.SaveAssets()`-ing only if it actually changed. No test harness
      for this Editor-only script; verified via a Unity 6000.5.3f1 batchmode compile (0 `error CS`).
- [ ] **L3** — Persisted `PendingMatchHost`/`Port` (`OnlineMatchService.cs:162-170`) are dialed
      unvalidated on launch. Not a privilege escalation while M-C2 is open (same trust boundary);
      matters once M-C2 is fixed. Fix: re-resolve via `GetMultiplayerServerDetails` on rejoin instead
      of trusting the persisted address, once wss/cert validation exists.
      Still open — blocked on M-C2, which was itself deliberately held off (see M-C2's own decision
      note): no native Keychain/Keystore verification available in this environment.
- [x] **L4** — Dockerfile has no `USER` directive (root); `Program.cs:188-190` logs join tokens
      under MPS (deliberate, currently `{}` since seats are PlayFab-reserved); `ServerMatch.cs:340`'s
      token compare (`expected != token`) isn't constant-time (impractical to exploit over 128-bit
      GUIDs, but `CryptographicOperations.FixedTimeEquals` is one line).
      **Fixed 2026-09-06**: `ServerMatch.AcceptAsync` now compares via
      `CryptographicOperations.FixedTimeEquals`. Dockerfile now runs as `USER $APP_UID` (the
      built-in non-root account Microsoft's .NET 8+ images ship); verified by building the image
      and running it — `id` inside the container reports `uid=1654(app)`, and `POST /matches`
      still returns 200. The join-token logging under MPS is unchanged — already deliberate, per
      the finding's own text.
- [x] **L5** — Undisposed resources (`HttpMatchHost` never closes the WebSocket after
      `HandleJoinAsync` returns, no `IDisposable` on `HttpMatchHost`/`ServerMatch`), the shared
      `HttpClient` keeps a 100s default timeout, `Wire/ClientMessages.cs:5-10` references a
      `MatchConnection.Dispatch` that doesn't exist, `MatchRegistry.cs:87`'s `?? 30` duplicates a
      private const, five `[Fact]`s that only `output.WriteLine` and pass unconditionally.
      **Fixed 2026-09-06**, mostly: `ReceiveLoopAsync` and `CloseFailedJoinAsync` now dispose the
      `WebSocket` in every path; `HttpMatchHost` and `ServerMatch` both implement `IDisposable`
      (`ServerMatch.Dispose` is called from `MatchRegistry`'s own M3 eviction continuation, only
      after eviction so nothing can reach `matchLock` again); the shared `HttpClient` timeout was
      already fixed as part of M5; `ClientMessages.cs`'s doc comment now points at
      `ServerMatch.DispatchAsync` instead of the nonexistent `MatchConnection`; `MatchRegistry.cs`'s
      `?? 30` now reads `ServerMatch.DefaultOpeningBuildWindowSeconds`. New test:
      `HttpMatchHost_disposes_without_throwing`. The five/six `Report_*` `[Fact]`s were assessed and
      deliberately left alone: adding a "must produce nonzero output" assertion to
      `FoundryWhiffRateTests.Report_whiff_rate_by_row_and_creep_speed` was tried first and it broke
      immediately — a Foundry legitimately fires zero shells in a documented lead-timing dead zone
      (see that file's own remarks), so "unconditional" is this test's actual design, not an
      oversight; each `Report_*` test already has a sibling test asserting the real invariant on
      the same underlying data. Reverted rather than risk the same false failure in the other four.
- [x] **L6** — Dead code: `PauseSimulationCommand`, `TechPurchasedEvent`, `LanePathCache` have no
      production references; the seed derivation `(Seed * 397) ^ playerId` is copy-pasted 4x.
      **Fixed 2026-09-06**: `PauseSimulationCommand`/`TechPurchasedEvent` deleted outright (verified
      zero references anywhere, including tests). `LanePathCache` kept, not deleted — it has a real
      behavioral contract test (`PathingTests.Path_cache_invalidates_only_requested_lane`) and is
      genuinely pending future wiring (every route lookup today calls `GridPathService.FindRoute`
      directly, uncached), the same "kept as scaffolding" precedent as `IRandomSource`; added a doc
      comment saying so instead. The seed derivation is now one `SeededRandomFor` helper on
      `LocalVerticalSlice`, used at all 4 former call sites.

## Verified as sound (do not re-litigate)

- Seat authorization: every command re-resolves through `ConnectionSeatAuthority.ResolveSeat`,
  which ignores the claimed id during a client request; wire DTOs carry no seat field at all; token
  and PlayFab claim paths cannot cross; `EndRequest` runs in a `finally`. Only residual risk is H2's
  race.
- Secret handling: `PLAYFAB_SECRET_KEY` is read once from env, never logged, only sent via
  `X-SecretKey` over HTTPS; no secret of any kind found in client code (grep-verified).
- `LTW.Simulation` dependency discipline (rule 3) holds — zero package/project refs, no
  Unity/networking/file-system/threading usage.
- Determinism otherwise clean: no `DateTime`/`Guid.NewGuid`/unseeded `Random`/threading; every
  dictionary iteration is sorted-first or contains-only. M-DET1 is the sole exception.
- `JsonDocument.Parse`'s default depth limit (64) rejects deeply nested payloads rather than stack-
  overflowing.
- Native iOS bridge, retry bounds, and nullable annotations all checked clean — see the full report.

## Corrections to standing docs

- `ServerMatch.cs:19-21`'s claim "there is no cross-connection race to resolve here" is false in
  practice — see H2. The comment should be corrected once H2 is fixed.
- `docs/SECURITY_CONSIDERATIONS.md` section 4 states "All client-server traffic uses TLS / WSS /
  QUIC encryption" as the mitigation strategy — this is aspirational, not current state; see H5.
