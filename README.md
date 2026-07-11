# Line Tower Wars Mobile

LTW is a Unity and C# mobile adaptation of Line Tower Wars. The current milestone is an offline three-player MVP with one human player and two simulated opponents.

## Project Documentation

- [Project guide](PROJECT_GUIDE.md)
- [Architecture](ARCHITECTURE.md)
- [MVP dependencies](MVP_DEPENDENCIES.md)
- [MVP implementation checklist](MVP_IMPLEMENTATION_CHECKLIST.md)
- [Monetization and payments](MONETIZATION_AND_PAYMENTS.md)
- [Agent and contributor guidance](AGENTS.md)

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
