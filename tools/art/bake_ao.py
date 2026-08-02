"""Bake ambient occlusion maps for LTW unit meshes.

Open item 3 says no asset in this project has ambient occlusion, and resolved item 6
established why binding the existing ORM maps cannot fix that: their red channel is exactly
0.000 in every pixel of all 21 packed maps, so there is no AO on disk to recover. It has to
be generated, and this is the thing that generates it.

`GRAPHICS_AA_UPLIFT.md` section 4.1, from direct observation of the reference frames, calls
AO "the single largest contributor to units reading as solid objects rather than lit shapes".
`LTW/Stylized Unit` already has the AO path built and inert, waiting on these files.

Run headless, one role at a time:

    /Applications/Unity/Hub/Editor/... is not involved. Use Blender:

    blender --background --python tools/art/bake_ao.py -- \
        --fbx  unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Arrow/AIDrop/arrow_split_base_head_0727.fbx \
        --out  unity/LTW.UnityClient/Assets/Art/Towers/Production/Textures/tower_arrow_3d_ao_v01.png

Deliberately a background script rather than a live Blender-MCP call. `bpy.ops.object.bake`
polls against the editor context and fails through the MCP bridge, and more importantly a
committed script is reproducible and reviewable, which a one-off session command is not.

Every mesh in the file is baked into ONE image. That is correct for these assets because Meshy
gives each role a single material and a single UV layout, which is also how the Unity material
is set up — one texture set per unit. A file with genuinely separate UV spaces per mesh would
need one invocation per mesh.
"""

import argparse
import os
import sys

import bpy


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description="Bake an AO map from an FBX.")
    parser.add_argument("--fbx", required=True, help="Source mesh to bake from.")
    parser.add_argument("--out", required=True, help="PNG to write.")
    parser.add_argument("--size", type=int, default=1024, help="Square bake resolution.")
    parser.add_argument("--samples", type=int, default=128, help="Cycles samples.")
    parser.add_argument("--margin", type=int, default=8, help="Bake margin in pixels.")
    return parser.parse_args(argv)


def main() -> int:
    args = parse_args()
    fbx = os.path.abspath(args.fbx)
    out = os.path.abspath(args.out)
    if not os.path.exists(fbx):
        print(f"ERROR: source not found: {fbx}")
        return 1

    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = args.samples
    scene.cycles.use_denoising = True
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.margin = args.margin

    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    if not meshes:
        print("ERROR: no mesh objects in the imported file.")
        return 1

    missing_uv = [m.name for m in meshes if m.data.uv_layers.active is None]
    if missing_uv:
        # Baking without UVs writes nothing and reports success, which is the kind of silent
        # skip resolved item 12 had to go and fix in the intake scorecards. Fail loudly instead.
        print(f"ERROR: these meshes have no active UV layer, cannot bake: {missing_uv}")
        return 1

    os.makedirs(os.path.dirname(out), exist_ok=True)
    image = bpy.data.images.new("AO_Bake", args.size, args.size, alpha=False)
    image.filepath_raw = out
    image.file_format = "PNG"

    materials = {slot.material for mesh in meshes for slot in mesh.material_slots if slot.material}
    if not materials:
        print("ERROR: no materials found; the bake needs a material to host the target image.")
        return 1

    for material in materials:
        material.use_nodes = True
        node = material.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        node.select = True
        material.node_tree.nodes.active = node

    bpy.ops.object.select_all(action="DESELECT")
    for mesh in meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]

    print(f"Baking AO: {len(meshes)} mesh(es), {len(materials)} material(s), "
          f"{args.size}px, {args.samples} samples")
    bpy.ops.object.bake(type="AO")
    image.save()

    # Report the mean so a flat-white bake (which means "nothing was occluded", and is what a
    # broken UV layout or a bad margin produces) is visible in the log rather than discovered
    # later on a model that gained nothing.
    pixels = image.pixels[:]
    mean = sum(pixels[0::4]) / max(1, len(pixels[0::4]))
    print(f"Wrote {out}")
    print(f"Mean AO: {mean:.3f} (1.0 would mean nothing occluded at all)")
    if mean > 0.98:
        print("WARNING: bake is essentially white; check UVs and mesh scale.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
