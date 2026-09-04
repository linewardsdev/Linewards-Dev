# LTW Match Server

A headless .NET service that hosts authoritative `LTW.Simulation` matches over WebSockets —
MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-04. See `docs/MULTIPLAYER_ROLLOUT.md`'s MP-04 section for
what this proves, what it explicitly does not, and the real bug an integration test caught while
building it (bots could not act at all under the first version of the server-side seat authority).

No new dependency: transport is `System.Net.HttpListener` and `System.Net.WebSockets`, both base
class library, per `MVP_DEPENDENCIES.md` rule 4 ("prefer small, well-supported dependencies over
framework stacks").

## Running it

```
dotnet run --project src/LTW.MatchServer -- 5117
```

```
POST /matches                              {"humanSeats":[1,2]} -> {"matchId","tokens"}
GET  /matches/{id}/join?seat=N&token=T     WebSocket upgrade, bound to seat N
```

Wire messages are JSON; see `Wire/ClientMessages.cs` and `Wire/ServerMessages.cs`. Every match
runs its own fixed-tick loop and writes a full replay (`LocalVerticalSlice.GetMatchReplayRecord`,
MP-00) to `replays/{matchId}.json` when it ends.

## What is not proven here

Two real devices on two real networks, a regional container, and measured production bandwidth —
none of which this environment can produce. `tests/LTW.MatchServer.Tests` proves the protocol,
the authority, and match-to-completion end to end over a real (loopback) WebSocket transport
instead, and reports real measured numbers (throughput, not bandwidth) from that.
