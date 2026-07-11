# LTW Unity Client

This directory is the Unity presentation boundary for the mobile client. Open this directory as the project in Unity `6000.3.12f1`.

The Unity client will reference `LTW.Simulation` for match rules. Unity scripts must not duplicate simulation logic or introduce a reverse dependency from `LTW.Simulation` to Unity.

MVP-06 adds source adapters under `Assets/Scripts/Simulation`:

- `UnityMatchBootstrapper` creates the local sample vertical slice.
- `UnitySimulationDriver` advances simulation with a fixed-step accumulator and exposes snapshots/events.
- `UnityCommandAdapter` converts Unity-facing actions into simulation commands.
- `UnityVerticalSliceRenderer` displays the sample lane, towers, and creeps with simple primitives.
- `DiagnosticsOverlay` provides a development-only text overlay and reset path support.

Unity must reference the built `LTW.Simulation` assembly before these scripts compile in-editor. Build `LTW.Simulation` from the repository root and place/reference the resulting `LTW.Simulation.dll` under `Assets/Plugins` or configure an equivalent Unity assembly reference. Do not copy simulation rules into Unity scripts.
