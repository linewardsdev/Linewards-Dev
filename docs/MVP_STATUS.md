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

Latest local result: 55 tests passed.

Unity compile smoke also passes locally when `LTW.Simulation.dll` is built and copied to
`unity/LTW.UnityClient/Assets/Plugins`. The latest batch Play Mode evidence run loaded
`Assets/Scenes/LocalVerticalSlice.unity`, created the local match runtime objects, completed a
three-player match at tick 910 with P3 as winner, exported replay/report files, and verified reset
cleanup with zero active presentation objects. Repo evidence is tracked at
`docs/playtest-evidence/local-unity-batch-20260713-055905.md`.

The Unity editor pin is now `6000.5.3f1`. The latest GD-01 batch compile also succeeds under
that editor after the mobile HUD treatment merge.

## MVP-08 Rendering, Pooling, And Feedback

Current code evidence:

- `UnityVerticalSliceRenderer` renders side-by-side lane cells, towers, creeps, ownership colors, spawn boxes, and life-loss boxes.
- The current vertical-slice lane layout uses three side-by-side 7x18 long north-south lanes, with spawn at `(3, 0)` and life loss at `(3, 17)`.
- Presentation pools exist for towers, creeps, effects, and floating text.
- Simulation events create visual feedback for tower placement, tower damage, creep spawn, creep kill, leak, income tick, and elimination.
- Basic generated audio cues, mobile vibration hooks, reduced-effects preference, text scale, and disabled/simplified presentation modes exist.

Remaining acceptance evidence:

- Confirm a tester can visually distinguish owned towers, incoming creeps, leaks, and sends at a glance.
- Confirm full, simplified, and disabled presentation modes do not change simulation outcomes.
- Add a normal-speed or less-accelerated stress capture so presentation pool growth can be evaluated under realistic frame pacing.

## MVP-09 Full Local Three-Player Integration And Tuning

Current code evidence:

- `LocalVerticalSlice` runs a three-player carousel with Player 1 as the human lane and two bot players.
- The local sample map uses three 7x18 lanes.
- The bridge supports placement, sends, selling, reset, match summary, and replay records.
- `LocalThreePlayerMatchTests` verifies a deterministic local bot match completes in the current 900-1800 tick pacing target window.
- Creeps that leak through a lane now continue through active non-sender lanes, preserving carousel pressure while preventing a sender's own creeps from entering their lane.
- Bot opponents now build opening defensive packages before send pressure: Balanced builds two early towers, Defensive builds three and keeps a higher opening gold reserve.
- `LocalReplayExporter` writes diagnostic replay JSON.
- `LocalPlaytestRecorder` writes Markdown playtest reports and now confirms manual `P` exports with a runtime toast.
- `HeavySendStressHarness` can run a local stress pass and write diagnostics.

Remaining acceptance evidence:

- Capture at least two more local playtest reports using different seeds or bot profiles before GD-08 is considered complete.
- Record manual balance/usability notes from a human Play Mode run, not only automated batch evidence.

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

1. Run two more GD-08 local playtests with different seeds or bot profiles and save the generated Markdown reports.
2. Complete a manual Play Mode pass focused on visual readability, placement correction, send readability, and presentation detail modes.
3. Add a realistic pacing stress capture for presentation pooling; the accelerated batch pass proves reset cleanup but grows the effect pool aggressively.
4. Continue GD-03 through GD-07 tuning only from observed playtest notes.
5. Resume MVP-10 TestFlight/device validation only after GD-08 has three reports plus manual usability notes.

## GD-01 Board Readability And Camera

Current code evidence:

- The local camera is orthographic and now frames the three side-by-side lanes without the old top-to-bottom stack.
- Lanes now have darker backplates, stronger player-lane rails, top-to-bottom flow arrows, spawn/life-loss boxes, and clearer lane labels.
- The own lane is labeled as the defensive lane, while other lanes are labeled as send targets.
- The runtime HUD now creates the send dock, tower palette, placement ghost, and placement feedback when the empty local scene boots.
- The placement ghost now queries the simulation bridge before confirmation, distinguishing legal, occupied, unaffordable, invalid-lane, and path-blocking cells.
- Unity-side creep presentation now uses slower local ticks, larger creep markers, render interpolation, and a flipped vertical projection so creeps spawn at the top and move down.

Remaining acceptance evidence:

- Manually confirm the side-by-side 7x18 lane view is framed well across desktop and mobile aspect ratios.
- Confirm the new path, spawn, life-loss, and lane labels remain readable during active creeps and combat feedback.
- Confirm runtime HUD controls do not cover the active placement area during real Play Mode placement.
