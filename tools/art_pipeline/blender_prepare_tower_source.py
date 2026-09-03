#!/usr/bin/env python3
"""Prepare a tower source model for Unity intake.

Run with Blender, not regular Python:

    /Applications/Blender.app/Contents/MacOS/Blender --background --python tools/art_pipeline/blender_prepare_tower_source.py -- \
      --input unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_source_v01.fbx \
      --output unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_prepared_v01.fbx \
      --role control

This script is intentionally a cleanup/export stage. It does not create final art
quality from nothing and it does not promote anything to runtime.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROLE_MATERIAL_COLORS = {
    "body": (0.07, 0.11, 0.16, 1.0),
    "trim": (0.92, 0.66, 0.22, 1.0),
    "energy": (0.18, 0.78, 1.0, 1.0),
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []

    parser = argparse.ArgumentParser(description="Prepare a Line Wars tower model for Unity.")
    parser.add_argument("--input", required=False, help="Source model path: FBX, GLB/GLTF, OBJ, or BLEND.")
    parser.add_argument("--output", required=True, help="Prepared FBX output path.")
    parser.add_argument("--role", default="tower", help="Role label for exported metadata, e.g. control.")
    parser.add_argument("--target-height", type=float, default=1.25, help="Normalize visible mesh height to this many Blender units.")
    parser.add_argument("--max-footprint", type=float, default=1.45, help="Normalize X/Z footprint to fit within this size.")
    parser.add_argument("--self-test", action="store_true", help="Create a small synthetic test tower instead of importing input.")
    return parser.parse_args(argv)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def import_source(path: Path) -> None:
    suffix = path.suffix.lower()
    if suffix == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    elif suffix in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=str(path))
    elif suffix == ".obj":
        if hasattr(bpy.ops.wm, "obj_import"):
            bpy.ops.wm.obj_import(filepath=str(path))
        else:
            bpy.ops.import_scene.obj(filepath=str(path))
    elif suffix == ".blend":
        with bpy.data.libraries.load(str(path), link=False) as (data_from, data_to):
            data_to.objects = data_from.objects
        for obj in data_to.objects:
            if obj is not None:
                bpy.context.collection.objects.link(obj)
    else:
        raise ValueError(f"Unsupported source format: {path.suffix}")


def create_material(name: str, color: tuple[float, float, float, float], emission: bool = False) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = color
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.72
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.0
        if emission:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = color
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = 0.45
    return material


def ensure_role_materials() -> dict[str, bpy.types.Material]:
    return {
        "body": create_material("ltw_body_dark_slate", ROLE_MATERIAL_COLORS["body"]),
        "trim": create_material("ltw_trim_signal_gold", ROLE_MATERIAL_COLORS["trim"]),
        "energy": create_material("ltw_energy_control_cyan", ROLE_MATERIAL_COLORS["energy"], emission=True),
    }


def create_self_test_tower() -> None:
    mats = ensure_role_materials()

    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=0.48, depth=0.18, location=(0, 0, 0.09))
    base = bpy.context.object
    base.name = "Control_Base_Body"
    base.data.materials.append(mats["body"])

    bpy.ops.mesh.primitive_torus_add(major_radius=0.46, minor_radius=0.045, major_segments=64, minor_segments=10, location=(0, 0, 0.62))
    ring = bpy.context.object
    ring.name = "Control_Ring_Trim"
    ring.rotation_euler[0] = math.radians(0)
    ring.data.materials.append(mats["trim"])

    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=0.18, location=(0, 0, 0.62))
    core = bpy.context.object
    core.name = "Control_Core_Energy"
    core.data.materials.append(mats["energy"])

    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -0.38, 0.34))
    plate = bpy.context.object
    plate.name = "Control_Emitter_Body"
    plate.scale = (0.24, 0.08, 0.22)
    plate.data.materials.append(mats["body"])


def mesh_objects() -> list[bpy.types.Object]:
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


def remove_junk() -> None:
    for obj in list(bpy.context.scene.objects):
        if obj.type in {"CAMERA", "LIGHT", "ARMATURE", "EMPTY"}:
            bpy.data.objects.remove(obj, do_unlink=True)

    for obj in mesh_objects():
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        try:
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        except RuntimeError:
            pass
        obj.select_set(False)


def assign_fallback_materials(role: str) -> None:
    mats = ensure_role_materials()
    for obj in mesh_objects():
        if obj.data.materials:
            continue

        lowered = obj.name.lower()
        if "core" in lowered or "energy" in lowered or "glow" in lowered or "lens" in lowered:
            obj.data.materials.append(mats["energy"])
        elif "trim" in lowered or "gold" in lowered or "ring" in lowered:
            obj.data.materials.append(mats["trim"])
        else:
            obj.data.materials.append(mats["body"])

    root_name = f"LTW_{role.title()}_Tower_Source"
    for obj in mesh_objects():
        obj.name = obj.name.replace(" ", "_")
        obj.data.name = obj.name + "_Mesh"
    bpy.context.scene.name = root_name


MAX_EXPORTED_TEXTURE_SIZE = 1024


def export_packed_images(texture_dir: Path) -> list[str]:
    texture_dir.mkdir(parents=True, exist_ok=True)
    exported: list[str] = []

    for image in bpy.data.images:
        if image.packed_file is None:
            continue
        width, height = image.size
        if width > MAX_EXPORTED_TEXTURE_SIZE or height > MAX_EXPORTED_TEXTURE_SIZE:
            scale = MAX_EXPORTED_TEXTURE_SIZE / max(width, height)
            image.scale(max(1, round(width * scale)), max(1, round(height * scale)))
        safe_name = "".join(ch if ch.isalnum() or ch in {"_", "-"} else "_" for ch in image.name)
        texture_path = texture_dir / f"{safe_name}.png"
        image.filepath_raw = str(texture_path)
        image.file_format = "PNG"
        try:
            image.save()
        except RuntimeError:
            image.save_render(str(texture_path))
        image.filepath = str(texture_path)
        exported.append(str(texture_path))

    return exported


def bounds_for_objects(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    mins = Vector((float("inf"), float("inf"), float("inf")))
    maxs = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in objects:
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            mins.x = min(mins.x, world.x)
            mins.y = min(mins.y, world.y)
            mins.z = min(mins.z, world.z)
            maxs.x = max(maxs.x, world.x)
            maxs.y = max(maxs.y, world.y)
            maxs.z = max(maxs.z, world.z)
    return mins, maxs


def normalize_scale_and_pivot(target_height: float, max_footprint: float) -> dict[str, object]:
    objects = mesh_objects()
    if not objects:
        raise ValueError("No mesh objects found after import.")

    mins, maxs = bounds_for_objects(objects)
    size = maxs - mins
    height = max(size.z, 0.001)
    footprint = max(size.x, size.y, 0.001)
    scale_by_height = target_height / height
    scale_by_footprint = max_footprint / footprint
    scale_factor = min(scale_by_height, scale_by_footprint)

    center_x = (mins.x + maxs.x) * 0.5
    center_y = (mins.y + maxs.y) * 0.5
    bottom_z = mins.z

    root = bpy.data.objects.new("LTW_Unity_ExportRoot", None)
    bpy.context.collection.objects.link(root)
    root.location = (0, 0, 0)

    for obj in objects:
        obj.parent = root
        obj.matrix_parent_inverse.identity()
        obj.location.x = (obj.location.x - center_x) * scale_factor
        obj.location.y = (obj.location.y - center_y) * scale_factor
        obj.location.z = (obj.location.z - bottom_z) * scale_factor
        obj.scale = (obj.scale.x * scale_factor, obj.scale.y * scale_factor, obj.scale.z * scale_factor)

    bpy.context.view_layer.update()
    final_mins, final_maxs = bounds_for_objects(objects)
    final_size = final_maxs - final_mins
    return {
        "scale_factor": scale_factor,
        "initial_bounds": {"min": list(mins), "max": list(maxs), "size": list(size)},
        "final_bounds": {"min": list(final_mins), "max": list(final_maxs), "size": list(final_size)},
    }


def add_anchor_empties(role: str) -> None:
    anchors = {
        "Muzzle": (0.0, -0.55, 0.62),
        "Lens": (0.0, 0.0, 0.62),
        "ControlCore": (0.0, 0.0, 0.62),
        "ControlRing": (0.0, 0.0, 0.62),
        "PulseEmitter": (0.0, -0.55, 0.62),
    }
    if role.lower() != "control":
        anchors = {"Muzzle": (0.0, -0.5, 0.5), "Lens": (0.0, 0.0, 0.5)}

    for name, location in anchors.items():
        empty = bpy.data.objects.new(f"Anchor_{name}", None)
        empty.empty_display_type = "SPHERE"
        empty.empty_display_size = 0.05
        empty.location = location
        bpy.context.collection.objects.link(empty)


def export_fbx(output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "EMPTY"}:
            obj.select_set(True)

    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
    )


def write_report(output_path: Path, args: argparse.Namespace, normalization: dict[str, object], exported_textures: list[str]) -> None:
    report_path = output_path.with_suffix(".prep-report.json")
    report = {
        "role": args.role,
        "input": args.input,
        "output": str(output_path),
        "target_height": args.target_height,
        "max_footprint": args.max_footprint,
        "mesh_count": len(mesh_objects()),
        "exported_textures": exported_textures,
        "normalization": normalization,
        "notes": [
            "Prepared by Blender source cleanup script.",
            "This is a source/staging export only; Unity promotion remains separate.",
        ],
    }
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")


def main() -> None:
    args = parse_args()
    output_path = Path(args.output).resolve()

    clear_scene()
    if args.self_test:
        create_self_test_tower()
    else:
        if not args.input:
            raise ValueError("--input is required unless --self-test is used.")
        input_path = Path(args.input).resolve()
        if not input_path.exists():
            raise FileNotFoundError(f"Missing source model: {input_path}")
        import_source(input_path)

    remove_junk()
    assign_fallback_materials(args.role)
    exported_textures = export_packed_images(output_path.parent / f"{output_path.stem}_Textures")
    normalization = normalize_scale_and_pivot(args.target_height, args.max_footprint)
    add_anchor_empties(args.role)
    export_fbx(output_path)
    write_report(output_path, args, normalization, exported_textures)
    print(f"Prepared tower source exported to {output_path}")


if __name__ == "__main__":
    main()
