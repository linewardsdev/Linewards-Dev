"""Give a staged mesh the Unity intake root it is missing, without resizing it.

Run with Blender, not plain python:

    /Applications/Blender.app/Contents/MacOS/Blender --background \
      --python tools/art/repair_export_root.py -- <fbx> [<fbx> ...]

`blender_prepare_tower_source.py` is the normal intake stage, and it does two separable
things: it normalizes a mesh to the role's target height and footprint, and it exports under
Unity's contract (an `LTW_Unity_ExportRoot` parent, `apply_unit_scale`). The kitbash wave
needs only the second half.

The first half would actively destroy these units. A kitbash sibling's size *is* its design —
Bulk Brute is the Brute at 128%, Forge Tick is the turret walker at 55% and squashed — and
normalizing every unit to one target height is precisely the operation that throws that
away, leaving a roster of same-sized bodies whose stat cards claim otherwise. So this
re-exports at the size it finds.

Idempotent: a file that already has the root is left alone and reported as such.
"""

import sys
from pathlib import Path

import bpy

EXPORT_ROOT = "LTW_Unity_ExportRoot"


def already_prepared(path: Path) -> bool:
    return EXPORT_ROOT.encode() in path.read_bytes()


def repair(path: Path) -> str:
    if already_prepared(path):
        return f"skip   {path.name} (already has {EXPORT_ROOT})"

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))

    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if not meshes:
        return f"FAIL   {path.name}: no meshes after import"

    root = bpy.data.objects.new(EXPORT_ROOT, None)
    bpy.context.scene.collection.objects.link(root)
    for obj in meshes:
        if obj.parent is None:
            obj.parent = root
            obj.matrix_parent_inverse.identity()

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
    )
    return f"repaired {path.name}"


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        print("usage: ... repair_export_root.py -- <fbx> [<fbx> ...]")
        return
    for entry in argv:
        print(repair(Path(entry)), flush=True)


main()
