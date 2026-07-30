# Line Tower Wars Mobile

LTW is a Unity and C# mobile adaptation of Line Tower Wars. The current milestone is an offline three-player MVP with one human player and two simulated opponents.

The Unity client renders on URP 17.5.0 with 15 3D towers and 15 3D creeps (Meshy-generated, Blender-prepared) fully wired into the match presentation — the roster grew from the original five of each; see [docs/TOWER_AND_CREEP_ROSTER.md](docs/TOWER_AND_CREEP_ROSTER.md) for the current list. A guard (`Assets/Editor/TowerEmissionKeywordGuard.cs`) self-heals a recurring `_EMISSION` keyword loss on tower body materials; its root cause is still unknown, so watch the Editor log for repeated corrections if tower emission ever looks wrong. Bloom cost on a physical Android device is still unmeasured.

## Project Documentation

See [docs/README.md](docs/README.md) for the organized documentation index.

- [Project guide](docs/PROJECT_GUIDE.md)
- [Architecture](docs/ARCHITECTURE.md)
- [MVP dependencies](docs/MVP_DEPENDENCIES.md)
- [MVP implementation checklist](docs/MVP_IMPLEMENTATION_CHECKLIST.md)
- [Gameplay development checklist](docs/GAMEPLAY_DEVELOPMENT_CHECKLIST.md)
- [MVP status snapshot](docs/MVP_STATUS.md)
- [Monetization and payments](docs/MONETIZATION_AND_PAYMENTS.md)
- [Branding guide](docs/BRANDING_GUIDE.md)
- [Agent and contributor guidance](docs/AGENTS.md)

## Repository Layout

```text
src/LTW.Simulation/            Engine-independent match rules
src/LTW.MatchServer/           Deferred online authority boundary
tests/LTW.Tests/               Simulation tests
unity/LTW.UnityClient/         Unity mobile client project
tools/art_pipeline/            Meshy/Blender AI art intake and prep scripts
docs/                          Project documentation (see docs/README.md)
```

## Foundation Commands

Install the .NET SDK pinned by `global.json`, then run:

```powershell
dotnet restore LTW.sln --locked-mode
dotnet format LTW.sln --no-restore --verify-no-changes
dotnet test LTW.sln --no-restore --configuration Release
```

The full simulation test suite passes under `tests/LTW.Tests/`. A hardcoded
test count belongs in CI output, not prose — three docs have quoted three
different stale counts before, so this one deliberately doesn't state a
number.

## Unity Editor

Unity editor verification requires **exactly** Unity `6000.5.3f1`. Open `unity/LTW.UnityClient` through Unity Hub.

A second `6000.3.12f1` install has been seen on this machine — do not open the project with it. It silently downgrades `ProjectSettings.asset` (serialized version 29 → 28) and several package versions on save, which is easy to miss until something in CI or on-device breaks for no visible reason in a diff.

Headless verification (compile check, tests, visual capture) runs via Unity batchmode and requires the Editor to be fully closed first — a running Editor instance holds a project lock that blocks a second, headless Unity process from opening the same project.
