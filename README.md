# Line Tower Wars Mobile

LTW is a Unity and C# mobile adaptation of Line Tower Wars. The current milestone is an offline three-player MVP with one human player and two simulated opponents.

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
```

## Foundation Commands

Install the .NET SDK pinned by `global.json`, then run:

```powershell
dotnet restore LTW.sln --locked-mode
dotnet format LTW.sln --no-restore --verify-no-changes
dotnet test LTW.sln --no-restore --configuration Release
```

Unity editor verification requires Unity `6000.3.12f1`. Open `unity/LTW.UnityClient` through Unity Hub.
