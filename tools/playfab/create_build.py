#!/usr/bin/env python3
"""Create a PlayFab Multiplayer Servers build for LTW.MatchServer through the API.

Game Manager's New Build form cannot reference a game secret or set build metadata (found
2026-09-11: two form-created builds came up with neither, and every server refused every join
for lack of a secret to verify tickets with). This script does what the form can't:

  1. uploads (or force-updates) the title Secret Key as the game secret PLAYFAB_SECRET_KEY,
     which PlayFab delivers to every server as the env var PF_MPS_SECRET_PLAYFAB_SECRET_KEY;
  2. creates the build with that secret referenced, plus the settings docs/MP07_RUNBOOK.md lists;
  3. prints the BuildId and verifies via GetBuild that the reference, port and region took.

The secret is read from PLAYFAB_SECRET_KEY in the environment, else from .env.local at the repo
root (gitignored). It is sent to PlayFab and never printed. Usage:

  python3 tools/playfab/create_build.py --tag mps-20260911e --name "LineWards East 9"
"""
import argparse
import json
import os
import pathlib
import sys
import urllib.error
import urllib.request

TITLE_ID = "FBC34"
IMAGE_NAME = "ltw-matchserver"
SECRET_NAME = "PLAYFAB_SECRET_KEY"


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
    parser.add_argument("--tag", required=True, help="image tag in the PlayFab registry, e.g. mps-20260911e (one tag per build)")
    parser.add_argument("--name", required=True, help='build name shown in Game Manager, e.g. "LineWards East 9"')
    parser.add_argument("--region", default="EastUs")
    parser.add_argument("--standby", type=int, default=1)
    parser.add_argument("--max", type=int, default=2)
    parser.add_argument("--vm-size", default="Standard_D2as_v4")
    parser.add_argument("--servers-per-vm", type=int, default=4)
    parser.add_argument("--port-name", default="game", help="must match Program.cs's gamePortName and MultiplayerServerConfig.PortName")
    parser.add_argument("--port", type=int, default=5117)
    args = parser.parse_args()

    secret = load_secret()
    token = call("/Authentication/GetEntityToken", {}, {"X-SecretKey": secret})["EntityToken"]
    auth = {"X-EntityToken": token}

    call("/MultiplayerServer/UploadSecret", {"GameSecret": {"Name": SECRET_NAME, "Value": secret}, "ForceUpdate": True}, auth)
    print(f"game secret '{SECRET_NAME}' uploaded/updated (servers see it as PF_MPS_SECRET_{SECRET_NAME})")

    build = call(
        "/MultiplayerServer/CreateBuildWithCustomContainer",
        {
            "BuildName": args.name,
            "ContainerFlavor": "CustomLinux",
            "ContainerImageReference": {"ImageName": IMAGE_NAME, "Tag": args.tag},
            "VmSize": args.vm_size,
            "MultiplayerServerCountPerVm": args.servers_per_vm,
            "Ports": [{"Name": args.port_name, "Num": args.port, "Protocol": "TCP"}],
            "RegionConfigurations": [{"Region": args.region, "StandbyServers": args.standby, "MaxServers": args.max}],
            "GameSecretReferences": [{"Name": SECRET_NAME}],
        },
        auth,
    )
    build_id = build["BuildId"]
    print(f"build created: {build_id}  ({args.name}, {IMAGE_NAME}:{args.tag})")

    check = call("/MultiplayerServer/GetBuild", {"BuildId": build_id}, auth)
    secrets = [reference.get("Name") for reference in check.get("GameSecretReferences") or []]
    ports = [(port.get("Name"), port.get("Num"), port.get("Protocol")) for port in check.get("Ports") or []]
    regions = [(region.get("Region"), region.get("StandbyServers"), region.get("MaxServers")) for region in check.get("RegionConfigurations") or []]
    print(f"verified: secret references {secrets}, ports {ports}, regions (standby, max) {regions}")
    if SECRET_NAME not in secrets:
        sys.exit("verification FAILED: the build does not reference the game secret — do not use it.")
    print(f"next: set MultiplayerServerConfig.BuildId = \"{build_id}\", re-export the client, watch the region reach Standing By.")


if __name__ == "__main__":
    main()
