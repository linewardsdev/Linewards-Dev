#!/bin/sh
# Root for exactly one step, then the server runs as the non-root "app" user.
#
# PlayFab's Linux VM agent creates the GSDK log mount as root, and the C# GSDK opens its own log
# file there inside Start() — as uid 1654 that threw UnauthorizedAccessException before the first
# heartbeat, which Game Manager reports only as "too many restarts". Reproduced locally 2026-09-11
# (root-owned mode-755 mount: exit 134; same mount writable: runs) — see docs/MP07_RUNBOOK.md.
# This is the same pattern PlayFab's own Unreal Linux guide uses (chown $PF_SERVER_LOG_DIRECTORY,
# then drop privileges), so the image keeps SECURITY_AUDIT_2026-09-05's L4 non-root decision
# instead of reverting to root to get past it.
#
# Standalone mode (no PlayFab agent, no log mount) simply skips the chown and drops privileges.
set -eu

LOG_DIR="${PF_SERVER_LOG_DIRECTORY:-/data/GameLogs}"
if [ -d "$LOG_DIR" ]; then
    if chown -R app:app "$LOG_DIR"; then
        echo "entrypoint: log directory $LOG_DIR now owned by app"
    else
        echo "entrypoint: WARNING could not chown $LOG_DIR — GSDK Start() will likely fail" >&2
    fi
fi

# exec + setpriv rather than su: dotnet becomes the container's main process directly, so the
# agent's signals and exit code are its own, not a shell's.
exec setpriv --reuid=app --regid=app --init-groups dotnet LTW.MatchServer.dll "$@"
