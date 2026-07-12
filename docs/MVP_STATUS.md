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

Latest local result: 39 tests passed.

Unity compile smoke also passes locally when `LTW.Simulation.dll` is built and copied to
`unity/LTW.UnityClient/Assets/Plugins`. The latest MCP-assisted Play Mode startup loaded
`Assets/Scenes/LocalVerticalSlice.unity`, created the local match runtime objects, and reported
no current Unity console errors.

The Unity editor pin is now `6000.5.3f1`. The latest GD-01 batch compile also succeeds under
that editor after the mobile HUD treatment merge.

## MVP-08 Rendering, Pooling, And Feedback

Current code evidence:

- `UnityVerticalSliceRenderer` renders lane cells, towers, creeps, ownership colors, spawn and exit cells.
- The current vertical-slice lane layout is 12 columns by 9 rows, with spawn at `(0, 4)` and exit at `(11, 4)`.
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
- The local sample map uses three 12x9 lanes.
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

Status:

- Deferred until the gameplay development fork in `GAMEPLAY_DEVELOPMENT_CHECKLIST.md` proves a coherent local Unity play loop.
- Xcode, signing, provisioning, and TestFlight are no longer the immediate blocker for the next phase.

Remaining acceptance evidence:

- Complete enough GD-00 through GD-08 evidence to justify testing on a mobile device.
- Configure iOS signing, bundle identifier, provisioning, and TestFlight-capable export.
- Fill in the actual iOS device matrix.
- Run normal and stress matches on the selected devices.
- Record performance results, thermal observations, replay paths, crashes, hitches, and defects in the repository.

## Next Work Order

1. Manually run the GD-00 Play Mode pass with the merged mobile HUD and GD-01 board readability work.
2. Close MVP-06, MVP-08, and MVP-09 acceptance evidence as part of that local play loop.
3. Continue GD-01 with manual camera/readability notes, then move into GD-02 placement and tower controls.
4. Work GD-03 through GD-07 to improve content variety, pacing, bots, session flow, and feedback.
5. Run GD-08 local playtests and prioritize fixes from real gameplay notes.
6. Resume MVP-10 TestFlight/device validation only after local play is coherent enough to benefit from mobile testing.

## GD-01 Board Readability And Camera

Current code evidence:

- The local camera is orthographic and framed around the full three-lane board stack.
- Lanes now have darker backplates, stronger player-lane rails, flow arrows along the route, spawn/exit labels, and clearer lane labels.
- The own lane is labeled as the defensive lane, while other lanes are labeled as send targets.
- The placement ghost now changes to a danger color when the selected cell is outside the playable own-lane grid.

Remaining acceptance evidence:

- Manually confirm the full 12x9 lane stack is framed well across desktop and mobile aspect ratios.
- Confirm the new path, spawn, exit, and lane labels remain readable during active creeps and combat feedback.
- Extend placement preview beyond out-of-board feedback to distinguish blocked cells, unaffordable placements, and path-blocking placements before confirmation.
