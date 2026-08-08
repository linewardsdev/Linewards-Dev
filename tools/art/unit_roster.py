"""The one definition of which mesh each unit role is built from.

`bake_all_ao.py` and `make_all_lods.py` each used to carry their own copy of this scan, with
a comment in the LOD copy asserting the two rules agreed. They did agree — and the shared
rule was still wrong in a way neither could see, because the rule is only half the story:
the other half is where the files sit.

Roles are discovered by folder name, and the source is the shortest non-rigged mesh inside
that folder. So when the first kitbash wave exported each new unit into its *donor's* folder
(`TwinZephyr` into `Creeps/Zephyr/AIDrop`, and five more), two things happened silently: the
new units were invisible to both tools, and six parents had their AO and LOD source swapped
for their child's mesh. Nothing failed; the next `--force` re-bake would simply have written
the wrong geometry into six shipped textures.

Hence `check_placement`, and hence `roster()` raising rather than returning on a violation:
a kitbash mesh must live in the folder named for the unit it *is*, not the unit it came from.
"""

import glob
import os

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = "unity/LTW.UnityClient/Assets/Art/AIStaging/Models"

CATEGORIES = (("Towers", "tower"), ("Creeps", "creep"))


class RosterError(Exception):
    """A staged mesh sits somewhere that would make a tool derive it under the wrong role."""


def source_for(role_dir):
    """The mesh a role is built from, or None when the folder stages no FBX.

    Prefer the unrigged export where a role has both — it carries the same UVs and surface,
    so it bakes an identical map for less import cost. Five creeps ship rigged-only and use
    that. Shortest name breaks remaining ties.
    """
    sources = glob.glob(os.path.join(role_dir, "AIDrop", "*.fbx"))
    if not sources:
        return None
    unrigged = [s for s in sources if "rigged" not in os.path.basename(s).lower()]
    return sorted(unrigged or sources, key=len)[0]


def check_placement():
    """Return a list of human-readable misplacements; empty means the staging tree is sane.

    Only kitbash meshes are checkable this way: their filenames lead with the unit's own
    name (`twinzephyr_kitbash_zephyr_v01_prepared.fbx`), so the folder they belong in is
    derivable. Meshy drops carry generation ids instead and are exempt.
    """
    problems = []
    for folder, _ in CATEGORIES:
        base = os.path.join(REPO_ROOT, MODELS, folder)
        if not os.path.isdir(base):
            continue
        for role in sorted(os.listdir(base)):
            role_dir = os.path.join(base, role)
            if not os.path.isdir(role_dir):
                continue
            for path in sorted(glob.glob(os.path.join(role_dir, "AIDrop", "*.fbx"))):
                name = os.path.basename(path)
                if "kitbash" not in name.lower():
                    continue
                unit = name.lower().split("_", 1)[0]
                if unit != role.lower():
                    problems.append(
                        f"{folder}/{role}/AIDrop/{name} belongs in "
                        f"{folder}/{unit}/AIDrop (folder names the role; this names {unit!r})")
    return problems


EXPORT_ROOT = b"LTW_Unity_ExportRoot"


def check_prepared():
    """Return misnamed `*_prepared.fbx` files: ones that never went through preparation.

    `blender_prepare_tower_source.py` normalizes size, adds anchor empties, and parents
    everything to an `LTW_Unity_ExportRoot` exported with `apply_unit_scale`. A file that
    skips this but takes the name imports about a hundred times too small, and every stage
    downstream — AO, LODs, wrapper generation — succeeds anyway, so the first symptom is a
    unit missing from the board. Twin Crescent shipped that way until a review render caught
    a tower rendered as four pixels.

    FBX stores node names as plain bytes in both its binary and ASCII forms, so looking for
    the root is a substring search and needs no parser.
    """
    problems = []
    for folder, _ in CATEGORIES:
        base = os.path.join(REPO_ROOT, MODELS, folder)
        if not os.path.isdir(base):
            continue
        for role in sorted(os.listdir(base)):
            pattern = os.path.join(base, role, "AIDrop", "*_prepared.fbx")
            for path in sorted(glob.glob(pattern)):
                with open(path, "rb") as handle:
                    if EXPORT_ROOT not in handle.read():
                        problems.append(
                            f"{folder}/{role}/AIDrop/{os.path.basename(path)} is named "
                            f"_prepared but has no {EXPORT_ROOT.decode()} — it will import "
                            f"~100x too small. Run tools/art_pipeline/"
                            f"blender_prepare_tower_source.py over it.")
    return problems


def roster():
    """Every staged role as (prefix, folder, role, source_fbx), towers then creeps."""
    problems = check_placement()
    if problems:
        raise RosterError(
            "staged meshes are in the wrong role folders, which would make AO and LOD "
            "derive the wrong geometry:\n  " + "\n  ".join(problems))

    unprepared = check_prepared()
    if unprepared:
        raise RosterError(
            "staged meshes claim preparation they did not get:\n  " + "\n  ".join(unprepared))

    out = []
    for folder, prefix in CATEGORIES:
        base = os.path.join(REPO_ROOT, MODELS, folder)
        if not os.path.isdir(base):
            continue
        for role in sorted(os.listdir(base)):
            role_dir = os.path.join(base, role)
            if role.endswith(".meta") or not os.path.isdir(role_dir):
                continue
            source = source_for(role_dir)
            if source:
                out.append((prefix, folder, role, source))
    return out
