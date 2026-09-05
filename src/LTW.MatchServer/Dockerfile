# Build context is the repo ROOT (not this directory) — LTW.MatchServer.csproj references
# ../LTW.Simulation, so the simulation project's source must be inside the build context too.
# docs/MULTIPLAYER_ROLLOUT.md's MP-07: this proves LTW.MatchServer runs correctly as a Linux
# container in its existing standalone mode before any PlayFab GSDK integration exists, so a
# broken container can never later be confused with a broken GSDK integration.
#
#   docker build -f src/LTW.MatchServer/Dockerfile -t ltw-matchserver:dev .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so `dotnet restore` is cached across source-only changes.
COPY src/LTW.MatchServer/LTW.MatchServer.csproj src/LTW.MatchServer/
COPY src/LTW.Simulation/LTW.Simulation.csproj src/LTW.Simulation/
RUN dotnet restore src/LTW.MatchServer/LTW.MatchServer.csproj

COPY src/LTW.MatchServer/ src/LTW.MatchServer/
COPY src/LTW.Simulation/ src/LTW.Simulation/
RUN dotnet publish src/LTW.MatchServer/LTW.MatchServer.csproj -c Release -o /app --no-restore

# Runtime image is the plain .NET runtime, not aspnet:10.0 — LTW.MatchServer is a console app on
# System.Net.HttpListener/WebSockets (base class library), not ASP.NET Core, matching the
# project's own deliberate no-web-framework decision (see HttpMatchHost.cs's own remarks).
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# No fixed EXPOSE: standalone mode's port comes from the existing args[0] CLI convention (see
# Program.cs), and MP-07's PlayFab-allocated mode reads its port from GSDK config instead — see
# docs/MULTIPLAYER_ROLLOUT.md's MP-07.
ENTRYPOINT ["dotnet", "LTW.MatchServer.dll"]
