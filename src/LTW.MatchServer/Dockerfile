# Build context is the repo ROOT (not this directory) — LTW.MatchServer.csproj references
# ../LTW.Simulation, so the simulation project's source must be inside the build context too.
# docs/MULTIPLAYER_ROLLOUT.md's MP-07: this proves LTW.MatchServer runs correctly as a Linux
# container in its existing standalone mode before any PlayFab GSDK integration exists, so a
# broken container can never later be confused with a broken GSDK integration.
#
#   docker build -f src/LTW.MatchServer/Dockerfile -t ltw-matchserver:dev .
#
# For a real PlayFab Multiplayer Servers deployment (which always runs under GSDK, never
# standalone), build with MATCHSERVER_MODE=mps so the image defaults into that mode without
# needing the orchestrator to inject a custom environment variable — PlayFab's build creation
# flow has no field for that, confirmed during MP-07's Phase 5:
#
#   docker build -f src/LTW.MatchServer/Dockerfile --build-arg MATCHSERVER_MODE=mps -t ltw-matchserver:mps .

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

# Default "standalone" so ordinary local `docker build`/`docker run` (Phase 1's smoke test, and
# any future local debugging) needs no extra flags — override with --build-arg for a real MPS
# deployment image, see this file's own top-of-file remarks.
ARG MATCHSERVER_MODE=standalone
ENV LTW_MATCHSERVER_MODE=$MATCHSERVER_MODE

# $APP_UID is the non-root "app" account Microsoft's .NET 8+ images ship built in specifically for
# this — without it the process runs as root, which a container escape or a dependency RCE would
# then inherit for nothing this process actually needs. See docs/SECURITY_AUDIT_2026-09-05.md's L4.
USER $APP_UID

# No fixed EXPOSE: standalone mode's port comes from the existing args[0] CLI convention (see
# Program.cs), and MP-07's PlayFab-allocated mode reads its port from GSDK config instead — see
# docs/MULTIPLAYER_ROLLOUT.md's MP-07.
ENTRYPOINT ["dotnet", "LTW.MatchServer.dll"]
