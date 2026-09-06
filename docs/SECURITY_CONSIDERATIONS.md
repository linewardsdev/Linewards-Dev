# Security Considerations

This document outlines the security architecture, threat model, and defense strategies for Line Wards (LTW), covering the transition from the offline MVP to authoritative online play.

See `docs/SECURITY_AUDIT_2026-09-05.md` for a dated, line-level audit of the actual current code
against this doc's own mitigation claims — several are aspirational rather than built yet (noted
inline below where the audit found a gap).

## Architectural Context

LTW separates match execution into three distinct layers:
* `LTW.Simulation`: Pure .NET engine enforcing match rules, pathing, economy, and deterministic ticks.
* `LTW.UnityClient`: Unity presentation layer (input translation, rendering, audio, UI).
* `LTW.MatchServer`: Headless .NET server authority host for online matches.

Because security in competitive mobile games rests on **strict server authority**, security boundaries are defined at the library and protocol interfaces.

---

## 1. Match Authority & Anti-Cheat

### Threat Model
* **Memory Editing**: Rooted (Android) or jailbroken (iOS) devices using tools like GameGuardian or Frida to modify client RAM (e.g. inflating gold, income, lives, or zeroing cooldowns).
* **Speed Hacking**: Tampering with device clocks or Unity `Time.timeScale` to accelerate local ticks relative to opponents.
* **Client Command Spoofing**: Sending invalid or out-of-turn commands to manipulate match state.

### Mitigation Strategy
* **Seat Authority**: every command carries a `PlayerId`, and that field must never be believed
  over a wire — it is the one value an attacker most wants to change, and trusting it lets a client
  queue sends into another player's queue, build in their lane, or spend their gold.
  `ISeatAuthority` (2026-08-08) exists to enforce **the seat comes from the connection, never from
  the message**: `EnqueueSend` resolves the seat through it and overwrites the argument it was
  given. In-process the two are the same value, which is exactly why the boundary was written
  before the server — retrofitting it later means auditing every call site under time pressure.
  Currently applied to the enqueue path only; every other command still reads its own `PlayerId`.
* **Zero Client Authority**: `LTW.UnityClient` never publishes state assertions (e.g., "Player 1 gold is 500"). It accepts user input and transmits immutable requests (e.g., `PlaceTowerCommand`, `SendCreepCommand`).
* **Authoritative Server Validation**: `LTW.MatchServer` executes `LTW.Simulation` independently. Every received command is validated before execution against:
  1. **Resource Availability**: Player gold and minimum income gates (`CategoryTierRules.MinimumIncomeFor`).
  2. **Grid & Pathing Rules**: Placement cell availability and open-path validation.
  3. **Cooldowns & Sequence**: Action timing and turn ordering.
* **State Hash Auditing**: Clients regularly calculate and submit a lightweight state hash (`StateRevision` / hash of live entities, gold, and lives). If a client hash diverges from the server, a desync is logged and the client is forced to resynchronize or forfeit.

---

## 2. Determinism & Cross-Platform Integrity

### Threat Model
* **Floating-Point Drift**: Microscopic floating-point math variations across CPU architectures (ARM64 vs x86_64, iOS vs Android) causing divergent simulation outcomes over extended ticks.
* **RNG Manipulation**: Predictable pseudo-random number generator seeds allowing players to foresee critical hits or targeting.

### Mitigation Strategy
* **Discrete & Integer-Based Rules**: Match state logic in `LTW.Simulation` relies strictly on fixed ticks, discrete integer grids (`GridPosition`), and integer value types for health, damage, and economy.
* **Server-Managed Seeding**: RNG seeds are generated solely by `LTW.MatchServer` upon match init and distributed securely to participants.

---

## 3. Monetization & Payment Integrity

### Threat Model
* **Receipt Replay Attacks**: Submitting stolen or previously used Apple App Store / Google Play purchase receipts to unlock cosmetic items without payment.
* **Client-Side Entitlement Tampering**: Modifying local configuration files (`PlayerPrefs` or saved state) to access locked SKUs.

### Mitigation Strategy
* **Server-Side Receipt Validation**: All transactions must be validated directly against official store endpoints (App Store Server API and Google Play Developer API) via `LTW.MatchServer` or an API gateway.
* **Nonce & Account Binding**: Receipts are bound to unique, server-generated transaction nonces and authenticated player account IDs to prevent cross-account replay.

---

## 4. Network & Protocol Security

### Threat Model
* **Man-In-The-Middle (MitM) Interception**: Sniffing network packets to expose opponent strategies or alter input packets.
* **Command Flooding / DoS**: Spamming thousands of invalid placement attempts per second to crash or stall the match host.

### Mitigation Strategy
* **Transport Encryption**: All client-server traffic uses TLS / WSS / QUIC encryption.
  *Status 2026-09-05:* not yet true — `LTW.MatchServer` is plain `http://`/`ws://` today (dev/LAN
  infrastructure pending real hosting), and the PlayFab session ticket travels in the join URL's
  query string in cleartext. See `docs/SECURITY_AUDIT_2026-09-05.md`'s H5.
* **Input Rate Limiting**: The match host rate-limits input packets per seat per tick, silently dropping flood attempts before they touch `LTW.Simulation`.
  *Status 2026-08-08:* partly built, and in a different place than this sentence describes.
  `ICommandRateLimiter` and a per-seat token bucket exist **inside** `LTW.Simulation`
  (`Authority/`), not in front of it, and are wired to **three** commands as of 2026-08-09 —
  `EnqueueSend`, `CancelQueuedSend` and `ClearSendQueue`. Cancel and clear take the same authority
  and the same bucket as the enqueue they undo, deliberately: emptying another seat's queue is a
  cheaper attack than filling one, and a cancel that trusted its argument would be the easier of the
  two to reach. Every other command (`PlaceTower`, `QueueSend`, `BuyCategoryTier`, the batch
  operations) is still unthrottled. In-process that costs nothing; over a wire it is a flood surface. The in-simulation
  limiter is the floor, not the transport-level defence this line promises.
  The bucket is measured in simulation ticks rather than wall clock, deliberately: a wall-clock
  limiter throttles the 300x batch harness while letting a real client through, which is the wrong
  way round for a control whose job is to be tested.
* **Information Filtering**: `LTW.MatchServer` only broadcasts state visibility data appropriate for a given player seat, preventing client hacks from inspecting unrevealed opponent build plans.

---

## 5. Binary & Asset Protection

### Threat Model
* **Assembly Decompilation**: Reversing managed .NET assemblies to inspect internal APIs or extract client logic.
* **Leaked Diagnostic Tooling**: Unauthorized access to internal debug overlays or capture harnesses in production builds.

### Mitigation Strategy
* **IL2CPP Compilation**: Production mobile builds compile C# IL down to C++ native code via Unity's IL2CPP toolchain.
* **Diagnostic Code Stripping**: Tools like `RealUiCaptureRunner` or internal diagnostic overlays are strictly guarded with conditional flags (`#if UNITY_EDITOR` / `#if DEVELOPMENT_BUILD`) to ensure zero exposure in release binaries.
