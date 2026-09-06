# MP-07 Operations Runbook

## Purpose

How to deploy, roll back, drain, and investigate a desync for `LTW.MatchServer` running on Azure
PlayFab Multiplayer Servers (MPS) — see `docs/MULTIPLAYER_ROLLOUT.md`'s MP-07 for how and why this
hosting choice was made. Written against what has actually been built and verified (locally, via
PlayFab's `LocalMultiplayerAgent` tool) as of 2026-09-05, not aspirational — sections that rely on
a real cloud deployment this project hasn't done yet are marked as such.

Every command below assumes the repo root as the working directory and `LTW.MatchServer.Dockerfile`
built from it (it references `../LTW.Simulation`, so the build context must be the repo root, not
`src/LTW.MatchServer/`).

## Prerequisites

- Docker installed and running (`docker info` succeeds).
- Access to PlayFab Game Manager for title `FBC34`.
- This repo checked out, with the .NET SDK matching `global.json`.

## 1. Deploy

1. Build the MPS-mode image (the `MATCHSERVER_MODE` build arg is what makes this image come up in
   `mps` mode by default — see `src/LTW.MatchServer/Dockerfile`'s own top-of-file remarks for why
   this exists instead of an orchestrator-injected environment variable):

   ```
   docker build -f src/LTW.MatchServer/Dockerfile --build-arg MATCHSERVER_MODE=mps -t ltw-matchserver:<version> .
   ```

2. Get registry credentials: Game Manager → title `FBC34` → **Multiplayer → Servers → New build**
   (or, for an existing build, wherever its "upload a new image" flow is) → select **Linux** →
   note the shown username, password, and server URL (`customerXXXXXXXX.azurecr.io`). PlayFab
   provisions this registry automatically per account — there is no separate Azure resource to
   create or manage for it.

3. Push:

   ```
   docker login <server-url> -u <username> --password-stdin   # paste the password, don't leave it in shell history
   docker tag ltw-matchserver:<version> <server-url>/ltw-matchserver:<version>
   docker push <server-url>/ltw-matchserver:<version>
   ```

4. Back in Game Manager: **Refresh Images**, select the pushed tag. Configure:
   - Port: name **`game`**, container port **`5117`**, protocol **TCP** — must match both
     `Program.cs`'s `gamePortName` constant and the client's `MultiplayerServerConfig.PortName`.
     A mismatch here fails loudly (`InvalidOperationException` at startup, not a silent
     misconfiguration) — see `RunUnderPlayFabMultiplayerServersAsync`'s own port lookup.
   - Region: **East US** (matches the free evaluation tier and `MultiplayerServerConfig.PreferredRegions`).
   - Standby/max server counts — this is MP-07's actual cost-ceiling mechanism, not a code
     artifact. Start conservative (e.g. 1 standby / 2 max for an early beta).
   - If a Dasv4/East US quota error appears, see "Dasv4 quota" below before anything else here
     will save.

5. Note the resulting **BuildId**. Update
   `unity/LTW.UnityClient/Assets/Scripts/Online/MultiplayerServerConfig.cs`'s `BuildId` constant,
   rebuild and redeploy the client.

6. Verify: request a server from a real (or test) client, confirm a match completes and the
   container exits cleanly — see "Investigate a desync" below for how to pull its logs if
   something looks wrong instead.

**A property of this architecture worth remembering for every deploy**: each match's server
process lives for exactly that match's own lifetime (see MULTIPLAYER_ROLLOUT.md's MP-07 — one
process per match, not one process serving many), independent of any Build change made after that
match's server was already allocated. Pushing a new image or changing standby/max counts affects
only servers requested **after** the change — an in-flight match is never disturbed by a deploy.

### Dasv4 quota (first deploy only, usually)

A brand-new title's default core quota is 16 Av2 cores + 8 Dv2 cores split across East US/West
US — **zero quota for Dasv4 in any region** until explicitly requested (the free 750-core-hour
evaluation tier is a *billing* concept, not an automatic *quota* grant — these are separate
systems). If the build form won't save with a quota error:

1. Game Manager → **Multiplayer Servers → Quota Summary → Change Quota**.
2. Describe the request, **+ Add change**, VM family **Dasv4**, region **East US**, request a
   small limit (8 cores comfortably covers a 1-standby/2-max beta config, well under the 24-core
   free-tier cap).
3. **Submit.** Small requests like this are typically approved and provisioned immediately; only
   large (1000+ core) requests need manual review.

## 2. Roll back

Since matches don't share server processes, a rollback is entirely **client-side** — nothing on
the server side needs to change for an in-flight match, because nothing about an already-allocated
server ever changes underneath it.

1. Find the previous known-good `BuildId` (Game Manager's build history, or git history of
   `MultiplayerServerConfig.cs`).
2. Set `MultiplayerServerConfig.BuildId` back to it, redeploy the client.
3. Optionally, drain the bad build (see below) so PlayFab stops allocating new servers on it,
   without deleting it — keeps it around in case you need to roll forward again.
4. Matches already running on the bad build continue until they end naturally. Force-terminating
   them (`ShutdownMultiplayerServer` API) is destructive to whoever is mid-match on one — reserve
   that for a genuine emergency, not a routine rollback.

## 3. Drain

Stop a build from being allocated new matches, without touching what's already running:

1. Game Manager → the build → its region configuration → set **Standby** and **Max Servers** to
   `0` for the region(s) being drained.
2. Anything already running keeps running until its match ends naturally.
3. Watch Game Manager's active-server count for that build trend to `0` over time. A count stuck
   above `0` past a match's expected maximum length is worth investigating on its own — either a
   real bug, or the still-unhandled gap noted in MULTIPLAYER_ROLLOUT.md's MP-07 (Azure VM
   maintenance recycling an already-allocated server has no in-match mitigation today, so a
   maintenance-triggered server might not exit the way a normal match-end does).

## 4. Investigate a desync from its replay

The mechanism this section relies on — replay files surviving a match's ephemeral server past its
own death — was a real bug found and fixed on 2026-09-05 (see MULTIPLAYER_ROLLOUT.md's MP-07):
`Program.cs` originally wrote replays to a path inside the container's own filesystem, gone the
instant PlayFab deleted it. Fixed by writing into `GameserverSDK.GetLogsDirectory()` instead — the
same directory PlayFab's VM agent zips and archives after a server ends. **Confirmed working** via
`LocalMultiplayerAgent`: a completed match's `replays/<matchId>.json` was found inside the
collected `GameLogs/<id>/` folder, containing the real seed, content version, full player roster,
completion tick, and command history.

1. Get the affected match's id (its `SessionId`, reused as `MatchId` — see MP-07's own design
   note on why). Sources: a player report, PlayFab's own session tracking, or the bootstrap log
   line every match writes to both GSDK's log file and stdout: `Match <id> bootstrapped. Join
   tokens: {...}`.
2. **Retrieve the logs** (this step is the one piece of this runbook not yet exercised against a
   real cloud deployment — confirmed only via the local mechanism above, not PlayFab's actual
   cloud archival):
   - **Game Manager**: the build → **Servers** tab → find the server → **Download logs** (a zip).
   - **API**: `ListArchivedMultiplayerServers` to find the Server ID, then
     `GetMultiplayerServerLogs` with that ID. Archived logs are retained **28 days** after a
     server terminates.
3. Unzip, find `GameLogs/.../replays/<matchId>.json` inside it.
4. Feed it to `LocalVerticalSlice.Replay(...)` to reconstruct the match deterministically from its
   recorded seed/commands — see `tests/LTW.Tests/MatchReplayTests.cs` for the exact call pattern
   already proven to round-trip a replay to an identical final state.
5. Compare the reconstructed state/fingerprint against whatever was reported as desynced (e.g. a
   client's own local state at the time of the report) to find where they diverge.

**Not yet built**: any tooling for step 5's comparison — today this is a manual process. A proper
desync-report format/diff tool is still-unstarted MP-07 scope (see MULTIPLAYER_ROLLOUT.md's own
"Not yet done" list).

## Known gaps this runbook doesn't cover

- No real cloud deployment has exercised this runbook end to end yet — everything server-side is
  confirmed via `LocalMultiplayerAgent`, which is a faithful local simulation of the real GSDK
  protocol but not a substitute for having actually done a real deploy/rollback/drain once.
- No telemetry dashboard (match health, cost, crash, abuse) exists — MP-07's other three
  deliverables haven't been started.
- Nobody but the person who built this replay-retrieval path has tried to reproduce a desync from
  it — MP-07's own acceptance check ("a desync report can be reproduced from its replay by a
  *second* person using the runbook") is still open for exactly that reason.
- Abuse handling (reporting/muting/banning) doesn't exist.
- Azure VM maintenance recycling an already-allocated (live, in-match) server has no in-match
  mitigation — `GameserverSDK.RegisterMaintenanceCallback` only logs a warning today.
