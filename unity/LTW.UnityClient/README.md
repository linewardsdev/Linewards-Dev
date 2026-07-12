# LTW Unity Client

This directory is the Unity presentation boundary for the mobile client. Open this directory as the project in Unity `6000.5.3f1`.

The Unity client will reference `LTW.Simulation` for match rules. Unity scripts must not duplicate simulation logic or introduce a reverse dependency from `LTW.Simulation` to Unity.

MVP-06 adds source adapters under `Assets/Scripts/Simulation`:

- `UnityMatchBootstrapper` creates the local sample vertical slice.
- `UnitySimulationDriver` advances simulation with a fixed-step accumulator and exposes snapshots/events.
- `UnityCommandAdapter` converts Unity-facing actions into simulation commands.
- `UnityVerticalSliceRenderer` displays the three long 7x18 north-south lanes, towers, and creeps with simple primitives.
- `DiagnosticsOverlay` provides a development-only text overlay, including bot profiles and recent sends.
- `LocalPlaytestRecorder` exports Markdown playtest reports and replay paths for tuning review.
- `HudView` shows the local playtest counters for lives, gold, income, match tick, kills, leaks, income timing, and active pressure.

## Local Visual Test Controls

When the local vertical slice scene is running, development hotkeys can exercise the current graphics placeholders:

- `B`: place the default single-target ward.
- `C`: place the control ward placeholder.
- `U`: place the utility/economy ward placeholder.
- `S`: send a runner creep.
- `V`: send a brute creep.
- `W`: send a swarm group.
- `X`: sell the last sample tower.
- `R`: reset the match.
- `E`: export the current replay JSON.
- `P`: export a Markdown playtest report. The runtime toast shows `Saved playtest-###.md` when the report is written, or `Finish match first` if no completed replay exists yet.
- `H`: run the heavy-send stress harness.
- `F`: toggle reduced-effects mode.
- `M`: mute or unmute feedback audio.
- `-`, `+`: lower or raise feedback audio volume.
- `1`, `2`, `3`: switch full, simplified, and disabled presentation modes.

Unity must reference the built `LTW.Simulation` assembly before these scripts compile in-editor. Build `LTW.Simulation` from the repository root and place/reference the resulting `LTW.Simulation.dll` under `Assets/Plugins` or configure an equivalent Unity assembly reference. Do not copy simulation rules into Unity scripts.

Playtest reports are written under Unity's persistent data path. On the current Windows editor setup, that resolves to `C:\Users\engch\AppData\LocalLow\DefaultCompany\LTW_UnityClient\Playtests`.

```powershell
dotnet build src\LTW.Simulation\LTW.Simulation.csproj --configuration Release
Copy-Item src\LTW.Simulation\bin\Release\netstandard2.1\LTW.Simulation.dll unity\LTW.UnityClient\Assets\Plugins\LTW.Simulation.dll -Force
```
