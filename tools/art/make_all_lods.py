#!/usr/bin/env python3
"""Generate decimated LOD meshes for every tower and creep role.

Driver around `make_lods.py`, which decimates one role at a time — the same shape as
`bake_all_ao.py` and deliberately sharing its roster logic, so a role added to the project
is picked up by both with no edit here.

Open item 15's last sub-item: every unit is ~15,000 triangles at LOD0 forever, and the
capture harness has measured 266 creeps on camera — roughly four million triangles of units
in one frame on a mobile target. The decimation stage existed and had been run for exactly
two roles as a proof; this covers the other 28.

    python3 tools/art/make_all_lods.py            # generates what is missing
    python3 tools/art/make_all_lods.py --force    # regenerates everything
    python3 tools/art/make_all_lods.py --report   # counts triangles, writes nothing

Generating the meshes is only half. `LTW/Art/Author LOD Groups` on the Unity side is what
makes them do anything — without a LODGroup component referencing them, these files are
inert bytes, which is exactly what they were between the proof run and this one.
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys

import unit_roster

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = "unity/LTW.UnityClient/Assets/Art/AIStaging/Models"
LOD_SCRIPT = "tools/art/make_lods.py"
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"

# Conservative, and chosen against measured on-screen size rather than by eye: these assets
# occupy 46-105px, where LOD2 at a quarter of the triangles was indistinguishable from LOD0
# in the earlier proof. See make_lods.py's note on COLLAPSE preserving silhouette.
RATIOS = ["0.5", "0.25"]


def roster():
    """Every role, with the mesh to decimate and the directory its LODs belong in."""
    out = []
    for prefix, folder, role, source in unit_roster.roster():
        out_dir = os.path.join(
            REPO_ROOT, f"unity/LTW.UnityClient/Assets/Art/{folder}/Production/LODs")
        out.append((f"{prefix}_{role.lower()}_3d", source, out_dir))
    return out


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--force", action="store_true", help="regenerate roles that already have LODs")
    parser.add_argument("--report", action="store_true", help="list what exists and exit")
    parser.add_argument("--only", help="one role by name, case-insensitive — same flag as bake_all_ao.py")
    args = parser.parse_args()

    entries = roster()
    if args.only:
        # Names here are the full "tower_arrow_3d" form; match on the role in the middle.
        wanted = args.only.lower()
        entries = [e for e in entries if e[0].split("_", 1)[1].rsplit("_3d", 1)[0] == wanted]
        if not entries:
            print(f"FAIL  no role named {args.only!r}")
            return 1

    if args.report:
        have = sum(1 for name, _, out_dir in entries
                   if os.path.exists(os.path.join(out_dir, f"{name}_LOD1.fbx")))
        print(f"{len(entries)} roles, {have} with LODs, {len(entries) - have} without")
        for name, _, out_dir in entries:
            mark = "have" if os.path.exists(os.path.join(out_dir, f"{name}_LOD1.fbx")) else "MISSING"
            print(f"  {mark:8} {name}")
        return 0

    if not os.path.exists(BLENDER):
        print(f"FAIL  Blender not found at {BLENDER}")
        return 1

    made, skipped, failed = 0, 0, []
    for name, source, out_dir in entries:
        lod1 = os.path.join(out_dir, f"{name}_LOD1.fbx")
        if os.path.exists(lod1) and not args.force:
            skipped += 1
            continue

        os.makedirs(out_dir, exist_ok=True)
        result = subprocess.run(
            [BLENDER, "--background", "--python", os.path.join(REPO_ROOT, LOD_SCRIPT), "--",
             "--fbx", source, "--out-dir", out_dir, "--name", name, "--ratios", *RATIOS],
            capture_output=True, text=True, cwd=REPO_ROOT)
        if result.returncode != 0 or not os.path.exists(lod1):
            failed.append(name)
            print(f"FAIL  {name}")
            print("      " + (result.stderr.strip().splitlines() or ["no stderr"])[-1])
            continue

        made += 1
        print(f"made  {name}  (LOD1, LOD2)")

    print(f"\n{made} generated, {skipped} already present, {len(failed)} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except unit_roster.RosterError as error:
        # An arrangement problem in the staging tree, not a crash — say so plainly.
        print(f"ERROR: {error}", file=sys.stderr)
        sys.exit(2)
