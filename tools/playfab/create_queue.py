#!/usr/bin/env python3
"""Create/update the PlayFab matchmaking queue for LTW.MatchServer through the API.

MP-05 Phase 2 (docs/MULTIPLAYER_ROLLOUT.md). Game Manager's portal has no UI for matchmaking
queues at all — SetMatchmakingQueue is API-only, the same reason tools/playfab/create_build.py
exists for builds. This script:

  1. calls SetMatchmakingQueue with MinMatchSize 2 / MaxMatchSize 8, ServerAllocationEnabled
     pointed at a BuildId, and the RegionSelectionRule a server-allocation queue requires (a
     queue with ServerAllocationEnabled needs one, even with a single region — PlayFab rejects
     a ticket carrying no matching "Latencies" attribute with MatchmakingAttributeInvalid, which
     is why OnlineMatchService.CreateMatchmakingTicketAsync sends a synthetic single-region
     value rather than a real QoS beacon measurement);
  2. verifies via GetMatchmakingQueue that the config actually took.

A hard PlayFab constraint, confirmed from Microsoft's own docs (see MULTIPLAYER_ROLLOUT.md's
MP-05): MinMatchSize must be >= 2 — a lone ticket can never be matched by itself, no matter how
long it waits. That's why OnlineMatchService falls back to a direct solo-vs-bots request when
matchmaking times out; it is not a bug in this script or the queue.

The title secret is read the same way create_build.py reads it (env var, else .env.local) and is
sent to PlayFab only — never printed. Usage:

  python3 tools/playfab/create_queue.py --build-id b922cefb-e900-49fa-84d4-f3d8cf40999a
"""
import argparse
import json
import os
import pathlib
import sys
import urllib.error
import urllib.request

TITLE_ID = "FBC34"


def load_secret() -> str:
    value = os.environ.get("PLAYFAB_SECRET_KEY")
    if value:
        return value
    env_file = pathlib.Path(__file__).resolve().parents[2] / ".env.local"
    if env_file.is_file():
        for line in env_file.read_text().splitlines():
            if line.startswith("PLAYFAB_SECRET_KEY="):
                return line.split("=", 1)[1].strip()
    sys.exit("PLAYFAB_SECRET_KEY not set and not found in .env.local — see docs/PLAYFAB_SETUP.md.")


def call(path: str, body: dict, headers: dict) -> dict:
    request = urllib.request.Request(
        f"https://{TITLE_ID}.playfabapi.com{path}",
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json", **headers},
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)["data"]
    except urllib.error.HTTPError as error:
        sys.exit(f"{path} failed: HTTP {error.code} — {error.read().decode()[:400]}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--name", default="ltw-quickmatch", help="must match MultiplayerServerConfig.MatchmakingQueueName in Unity")
    parser.add_argument("--build-id", required=True, help="must match MultiplayerServerConfig.BuildId in Unity")
    parser.add_argument("--min-match-size", type=int, default=2, help="PlayFab requires >= 2 — a lone ticket can never match itself")
    parser.add_argument("--max-match-size", type=int, default=8, help="this game's seat count (8 lanes)")
    parser.add_argument("--region", default="EastUs", help="must match the build's own region and MultiplayerServerConfig.RegionSelectionRuleRegion")
    parser.add_argument("--max-latency-ms", type=int, default=500, help="must stay above MultiplayerServerConfig.SyntheticRegionLatencyMs")
    args = parser.parse_args()

    if args.min_match_size < 2:
        sys.exit("--min-match-size must be >= 2 — PlayFab never matches a ticket against itself.")

    secret = load_secret()
    token = call("/Authentication/GetEntityToken", {}, {"X-SecretKey": secret})["EntityToken"]
    auth = {"X-EntityToken": token}

    call(
        "/Match/SetMatchmakingQueue",
        {
            "MatchmakingQueue": {
                "Name": args.name,
                "MinMatchSize": args.min_match_size,
                "MaxMatchSize": args.max_match_size,
                "ServerAllocationEnabled": True,
                "BuildId": args.build_id,
                "RegionSelectionRule": {
                    "Name": "Region",
                    "Path": "Latencies",
                    "MaxLatency": args.max_latency_ms,
                    "Weight": 1,
                },
            }
        },
        auth,
    )
    print(f"queue '{args.name}' set: MinMatchSize {args.min_match_size}, MaxMatchSize {args.max_match_size}, BuildId {args.build_id}, region {args.region}")

    check = call("/Match/GetMatchmakingQueue", {"QueueName": args.name}, auth)
    queue = check.get("MatchmakingQueue") or {}
    print(
        "verified: "
        f"BuildId {queue.get('BuildId')}, "
        f"MinMatchSize {queue.get('MinMatchSize')}, MaxMatchSize {queue.get('MaxMatchSize')}, "
        f"ServerAllocationEnabled {queue.get('ServerAllocationEnabled')}, "
        f"RegionSelectionRule {queue.get('RegionSelectionRule')}"
    )
    if queue.get("BuildId") != args.build_id or not queue.get("ServerAllocationEnabled"):
        sys.exit("verification FAILED: the queue does not reference the build or has server allocation off — do not rely on it.")
    print(f"next: confirm MultiplayerServerConfig.MatchmakingQueueName == \"{args.name}\" in Unity, then verify with two real players (or one real player plus a deliberately-created second ticket) that pooling actually occurs before trusting it live.")


if __name__ == "__main__":
    main()
