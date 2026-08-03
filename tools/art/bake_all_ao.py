"""Bake ambient occlusion for every tower and creep role.

Driver around `bake_ao.py`, which bakes one role at a time. Open item 3 says no asset in this
project has ambient occlusion; the arrow tower and turret walker were baked first as a proof,
and this is the pass that covers the remaining 28.

`GRAPHICS_AA_UPLIFT.md` section 4.1 calls AO "the single largest contributor to units reading
as solid objects rather than lit shapes", and `LTW/Stylized Unit` has had the AO path built and
inert since it shipped — every material with no occlusion map is written with
`_OcclusionStrength` 0, so these files are the thing that switches it on.

Run from the repo root (plain python, NOT inside Blender):

    python3 tools/art/bake_all_ao.py                 # bakes what is missing
    python3 tools/art/bake_all_ao.py --force         # re-bakes everything
    python3 tools/art/bake_all_ao.py --only Prism    # one role
    python3 tools/art/bake_all_ao.py --list          # print the roster, bake nothing

Resumable by design: a role whose PNG already exists is skipped unless `--force`. Thirty Cycles
bakes is long enough that it will be interrupted, and re-running should cost only what is left.

Source selection prefers the non-rigged mesh where a role has both. The rigged export carries
the same UV layout and the same surface, so it bakes an identical map while costing more to
import; five creeps (Burrower, Colossus, Stalker, Warden, Zephyr) ship only the rigged variant
and use it.
"""

import argparse
import glob
import os
import subprocess
import sys

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = "unity/LTW.UnityClient/Assets/Art/AIStaging/Models"
BAKE_SCRIPT = "tools/art/bake_ao.py"
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"


def roster():
    """Every role, with the mesh to bake from and the texture to write."""
    out = []
    for folder, tex_folder, prefix in (("Towers", "Towers", "tower"), ("Creeps", "Creeps", "creep")):
        base = os.path.join(REPO_ROOT, MODELS, folder)
        for role in sorted(os.listdir(base)):
            if role.endswith(".meta") or not os.path.isdir(os.path.join(base, role)):
                continue
            sources = glob.glob(os.path.join(base, role, "AIDrop", "*.fbx"))
            if not sources:
                continue
            unrigged = [s for s in sources if "rigged" not in os.path.basename(s).lower()]
            source = sorted(unrigged or sources, key=len)[0]
            texture = os.path.join(
                REPO_ROOT,
                f"unity/LTW.UnityClient/Assets/Art/{tex_folder}/Production/Textures",
                f"{prefix}_{role.lower()}_3d_ao_v01.png")
            out.append((prefix, role, source, texture))
    return out


def main() -> int:
    parser = argparse.ArgumentParser(description="Bake AO for the whole unit roster.")
    parser.add_argument("--force", action="store_true", help="Re-bake roles that already have a map.")
    parser.add_argument("--only", help="Bake a single role by name, case-insensitive.")
    parser.add_argument("--list", action="store_true", help="Print the roster and exit.")
    parser.add_argument("--size", type=int, default=1024)
    parser.add_argument("--samples", type=int, default=128)
    args = parser.parse_args()

    entries = roster()
    if args.only:
        entries = [e for e in entries if e[1].lower() == args.only.lower()]
        if not entries:
            print(f"ERROR: no role named {args.only!r}")
            return 1

    if args.list:
        for kind, role, source, texture in entries:
            state = "have" if os.path.exists(texture) else "MISSING"
            print(f"{state:8} {kind:6} {role:16} {os.path.basename(source)}")
        return 0

    if not os.path.exists(BLENDER):
        print(f"ERROR: Blender not found at {BLENDER}")
        return 1

    baked, skipped, failed = 0, 0, []
    for index, (kind, role, source, texture) in enumerate(entries, start=1):
        if os.path.exists(texture) and not args.force:
            skipped += 1
            continue

        os.makedirs(os.path.dirname(texture), exist_ok=True)
        print(f"[{index}/{len(entries)}] baking {kind} {role} ...", flush=True)
        result = subprocess.run(
            [BLENDER, "--background", "--python", os.path.join(REPO_ROOT, BAKE_SCRIPT), "--",
             "--fbx", source, "--out", texture,
             "--size", str(args.size), "--samples", str(args.samples)],
            capture_output=True, text=True)

        if result.returncode != 0 or not os.path.exists(texture):
            # Blender's own error is far more useful than the exit code, so surface its tail.
            tail = "\n".join((result.stdout + result.stderr).strip().splitlines()[-6:])
            print(f"    FAILED {role}\n{tail}", flush=True)
            failed.append(role)
            continue

        size_kb = os.path.getsize(texture) // 1024
        print(f"    wrote {os.path.basename(texture)} ({size_kb} KB)", flush=True)
        baked += 1

    print(f"\nbaked {baked}   already had a map {skipped}   failed {len(failed)}")
    if failed:
        print("failed roles:", ", ".join(failed))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
