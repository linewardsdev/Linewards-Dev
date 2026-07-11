# MVP Status Snapshot

This snapshot reconciles the current `main` branch with `MVP_IMPLEMENTATION_CHECKLIST.md`.

## Branch State

- `main` contains the stacked MVP-00 through MVP-07 branch history.
- No remote branches currently exist for MVP-08, MVP-09, MVP-10, or MVP-11.
- The MVP-07 branch tip includes implementation work that reaches into MVP-08, MVP-09, and MVP-10 scope.

## Verified Gates

The following commands pass from the repository root:

```powershell
dotnet restore LTW.sln --locked-mode
dotnet format LTW.sln --no-restore --verify-no-changes
dotnet test LTW.sln --no-restore --configuration Release
```

Latest local result: 38 tests passed.

## MVP-08 Rendering, Pooling, And Feedback

Current code evidence:

- `UnityVerticalSliceRenderer` renders lane cells, towers, creeps, ownership colors, spawn and exit cells.
- Presentation pools exist for towers, creeps, effects, and floating text.
- Simulation events create visual feedback for tower placement, creep spawn, creep kill, leak, income tick, and elimination.
- Basic generated audio cues, mobile vibration hooks, reduced-effects preference, text scale, and disabled/simplified presentation modes exist.

Remaining acceptance evidence:

- Run repeated creep waves in Unity Play Mode and confirm active plus pooled presentation objects remain bounded.
- Confirm a tester can visually distinguish owned towers, incoming creeps, leaks, and sends at a glance.
- Confirm full, simplified, and disabled presentation modes do not change simulation outcomes.

## MVP-09 Full Local Three-Player Integration And Tuning

Current code evidence:

- `LocalVerticalSlice` runs a three-player carousel with Player 1 as the human lane and two bot players.
- The bridge supports placement, sends, selling, reset, match summary, and replay records.
- `LocalThreePlayerMatchTests` verifies a deterministic local bot match completes in the 3,000-6,000 tick target window.
- `LocalReplayExporter` writes diagnostic replay JSON.
- `HeavySendStressHarness` can run a local stress pass and write diagnostics.

Remaining acceptance evidence:

- Run the Unity scene end to end and confirm a complete match starts, reaches a winner, shows results, and resets to a usable state.
- Confirm the winner can be reached without manual intervention in the Unity client, not only in pure .NET tests.
- Record the observed match duration, seed, replay export, and any balance/usability issues from a Play Mode or device run.

## MVP-10 iOS TestFlight And Device Validation

Current code/documentation evidence:

- `IOS_DEVICE_VALIDATION.md` defines prerequisites, the test matrix, and per-run records.
- `DevicePerformanceSampler` captures average frame time, managed memory, active creeps, towers, and presentation objects.
- `HeavySendStressHarness` writes a 60-second heavy-send diagnostic report.

Remaining acceptance evidence:

- Configure iOS signing, bundle identifier, provisioning, and TestFlight-capable export.
- Fill in the actual iOS device matrix.
- Run normal and stress matches on the selected devices.
- Record performance results, thermal observations, replay paths, crashes, hitches, and defects in the repository.

## Next Work Order

1. Close MVP-06 Play Mode acceptance because MVP-08 and MVP-09 depend on proven Unity execution.
2. Close MVP-08 visual and pooling acceptance in Play Mode.
3. Close MVP-09 full-match results and reset acceptance in Unity.
4. Configure signing and perform MVP-10 TestFlight/device validation.
5. Start MVP-11 only after MVP-10 has real device evidence.
