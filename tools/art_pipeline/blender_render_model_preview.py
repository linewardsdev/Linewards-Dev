#!/usr/bin/env python3
"""Render a quick preview PNG for a source model using Blender."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def import_model(path: Path) -> None:
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
    else:
        raise ValueError(f"Unsupported preview input: {path}")


def mesh_objects() -> list[bpy.types.Object]:
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


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


def setup_camera_and_lights() -> None:
    objects = mesh_objects()
    mins, maxs = bounds_for_objects(objects)
    center = (mins + maxs) * 0.5
    size = max(maxs.x - mins.x, maxs.y - mins.y, maxs.z - mins.z)

    bpy.ops.object.light_add(type="AREA", location=(-2.5, -3.0, 4.0))
    key = bpy.context.object
    key.name = "Preview_KeyLight"
    key.data.energy = 450
    key.data.size = 4.0

    bpy.ops.object.camera_add(location=(2.2, -3.0, 2.0), rotation=(1.1, 0.0, 0.62))
    camera = bpy.context.object
    bpy.context.scene.camera = camera
    direction = center - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = max(size * 1.45, 1.8)


def render(output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    engine_items = {item.identifier for item in bpy.context.scene.render.bl_rna.properties["engine"].enum_items}
    if "BLENDER_EEVEE_NEXT" in engine_items:
        bpy.context.scene.render.engine = "BLENDER_EEVEE_NEXT"
    elif "BLENDER_EEVEE" in engine_items:
        bpy.context.scene.render.engine = "BLENDER_EEVEE"
    else:
        bpy.context.scene.render.engine = "BLENDER_WORKBENCH"
    bpy.context.scene.render.resolution_x = 1200
    bpy.context.scene.render.resolution_y = 1200
    bpy.context.scene.view_settings.view_transform = "Filmic"
    bpy.context.scene.view_settings.look = "Medium High Contrast"
    bpy.context.scene.render.film_transparent = False
    bpy.context.scene.world.color = (0.035, 0.045, 0.06)
    bpy.context.scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    args = parse_args()
    clear_scene()
    import_model(Path(args.input).resolve())
    setup_camera_and_lights()
    render(Path(args.output).resolve())
    print(f"Preview rendered to {Path(args.output).resolve()}")


if __name__ == "__main__":
    main()
