"""Generate decimated LOD meshes for LTW unit assets.

Open item 15's one remaining sub-item: "no LOD groups and no decimation stage. Every unit is
~15,000 triangles at LOD0 forever; a 40-creep swarm is ~600k triangles, and the harness has
measured frames with 266 creeps on camera. There is no decimation step anywhere in the Blender
pipeline, so this needs one built before LOD groups can be authored."

This is that step. It does not author LOD groups — that is Unity-side and deliberately separate,
see the note at the bottom.

    blender --background --python tools/art/make_lods.py -- \
        --fbx unity/.../arrow_split_base_head_0727.fbx \
        --out-dir unity/.../Art/Towers/Production/LODs \
        --name tower_arrow_3d --ratios 0.5 0.25

Ratios are triangle-count fractions of LOD0. 0.5/0.25 is a conservative starting pair for
assets that are already only 46-105px on screen; the measured budgets printed at the end are
what should drive any change to them, not these defaults.

COLLAPSE mode is used rather than UNSUBDIVIDE or PLANAR. Collapse preserves silhouette best at
aggressive ratios, and silhouette is the one axis `GRAPHICS_AA_UPLIFT.md` records as already
being at target — so it is the thing a decimation stage must not spend.
"""

import argparse
import os
import sys

import bpy


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description="Generate decimated LOD meshes from an FBX.")
    parser.add_argument("--fbx", required=True)
    parser.add_argument("--out-dir", required=True)
    parser.add_argument("--name", required=True, help="Base name, e.g. tower_arrow_3d")
    parser.add_argument("--ratios", type=float, nargs="+", default=[0.5, 0.25],
                        help="Triangle fractions of LOD0, one per generated LOD.")
    return parser.parse_args(argv)


def triangle_count(scene) -> int:
    total = 0
    for obj in scene.objects:
        if obj.type == "MESH":
            obj.data.calc_loop_triangles()
            total += len(obj.data.loop_triangles)
    return total


def main() -> int:
    args = parse_args()
    fbx = os.path.abspath(args.fbx)
    if not os.path.exists(fbx):
        print(f"ERROR: source not found: {fbx}")
        return 1

    out_dir = os.path.abspath(args.out_dir)
    os.makedirs(out_dir, exist_ok=True)

    budgets = []
    for index, ratio in enumerate(args.ratios, start=1):
        # Re-import per LOD rather than decimating cumulatively. Stacking decimations compounds
        # error, so LOD2 generated from an already-halved LOD1 is measurably worse than LOD2
        # generated from the original at 0.25.
        bpy.ops.wm.read_homefile(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=fbx)
        scene = bpy.context.scene

        if index == 1:
            base = triangle_count(scene)
            budgets.append(("LOD0", base, 1.0))

        meshes = [obj for obj in scene.objects if obj.type == "MESH"]
        if not meshes:
            print("ERROR: no meshes in source.")
            return 1

        for mesh in meshes:
            modifier = mesh.modifiers.new(f"LOD{index}", "DECIMATE")
            modifier.decimate_type = "COLLAPSE"
            modifier.ratio = ratio
            modifier.use_collapse_triangulate = True
            bpy.context.view_layer.objects.active = mesh
            bpy.ops.object.modifier_apply(modifier=modifier.name)

        produced = triangle_count(scene)
        out_path = os.path.join(out_dir, f"{args.name}_LOD{index}.fbx")
        bpy.ops.export_scene.fbx(
            filepath=out_path,
            use_selection=False,
            apply_unit_scale=True,
            bake_space_transform=False,
            mesh_smooth_type="FACE",
            add_leaf_bones=False,
        )
        budgets.append((f"LOD{index}", produced, ratio))
        print(f"Wrote {out_path}")

    print("\n  level    tris   requested   actual")
    base_tris = budgets[0][1]
    for level, tris, ratio in budgets:
        actual = tris / base_tris if base_tris else 0.0
        print(f"  {level:<6} {tris:>7}   {ratio:>8.2f}   {actual:>6.2f}")

    # Deliberately NOT authored here. Adding LODGroup components is a Unity-side change, and
    # item 15 records a coupled constraint that must be honoured with it: m_EnableLODCrossFade
    # is currently OFF because there were zero LODGroups, and RenderSetupValidation fails if the
    # flag and the project disagree in either direction. So the flag has to flip back on in the
    # same change that gives the project its first LODGroup - and that should happen when
    # coverage is broad, not at two prefabs out of forty.
    print("\nNext: author LODGroups in Unity, and flip m_EnableLODCrossFade back on in the")
    print("same change (see OPEN_ITEMS item 15 and RenderSetupValidation).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
