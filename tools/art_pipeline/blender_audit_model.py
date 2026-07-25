#!/usr/bin/env python3
"""Audit an AI-generated Line Wards model and write objective intake metrics.

Run with Blender, not regular Python:

    /Applications/Blender.app/Contents/MacOS/Blender --background \
      --python tools/art_pipeline/blender_audit_model.py -- \
      --input unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/model.fbx \
      --output docs/art-pipeline/ai-model-intake/runs/control/candidate_audit.json

This script does not make art decisions. It measures the generated asset so the
pipeline can reject noisy or technically risky AI outputs without hand design.
"""

from __future__ import annotations

import argparse
import json
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
    parser = argparse.ArgumentParser(description="Audit an imported AI model.")
    parser.add_argument("--input", required=True, help="Source model path: FBX, GLB/GLTF, OBJ, or BLEND.")
    parser.add_argument("--output", required=True, help="JSON audit output path.")
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
    elif suffix == ".blend":
        with bpy.data.libraries.load(str(path), link=False) as (data_from, data_to):
            data_to.objects = data_from.objects
        for obj in data_to.objects:
            if obj is not None:
                bpy.context.collection.objects.link(obj)
    else:
        raise ValueError(f"Unsupported source format: {path.suffix}")


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


def material_is_transparent(material: bpy.types.Material) -> bool:
    if material.blend_method not in {"OPAQUE", "CLIP"}:
        return True
    if material.diffuse_color[3] < 0.98:
        return True
    if material.use_nodes:
        bsdf = material.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None and "Alpha" in bsdf.inputs:
            try:
                return float(bsdf.inputs["Alpha"].default_value) < 0.98
            except TypeError:
                return False
    return False


def audit_model(input_path: Path) -> dict[str, object]:
    meshes = mesh_objects()
    if not meshes:
        raise ValueError("No mesh objects found in imported model.")

    depsgraph = bpy.context.evaluated_depsgraph_get()
    triangle_count = 0
    vertex_count = 0
    material_slots = 0
    transparent_materials: set[str] = set()
    texture_paths: set[str] = set()
    packed_texture_names: set[str] = set()

    for obj in meshes:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            triangle_count += sum(len(poly.vertices) - 2 for poly in mesh.polygons)
            vertex_count += len(mesh.vertices)
        finally:
            evaluated.to_mesh_clear()

        material_slots += len(obj.data.materials)
        for material in obj.data.materials:
            if material is None:
                continue
            if material_is_transparent(material):
                transparent_materials.add(material.name)
            if material.use_nodes:
                for node in material.node_tree.nodes:
                    image = getattr(node, "image", None)
                    if image is not None and image.filepath:
                        texture_paths.add(bpy.path.abspath(image.filepath))
                    if image is not None and image.packed_file is not None:
                        packed_texture_names.add(image.name)

    mins, maxs = bounds_for_objects(meshes)
    size = maxs - mins
    object_names = [obj.name for obj in meshes]
    material_names = sorted({mat.name for obj in meshes for mat in obj.data.materials if mat is not None})

    return {
        "input": str(input_path),
        "mesh_count": len(meshes),
        "object_count": len(bpy.context.scene.objects),
        "triangle_count": triangle_count,
        "vertex_count": vertex_count,
        "material_slot_count": material_slots,
        "unique_material_count": len(material_names),
        "transparent_material_count": len(transparent_materials),
        "transparent_materials": sorted(transparent_materials),
        "texture_count": len(texture_paths) + len(packed_texture_names),
        "texture_paths": sorted(texture_paths),
        "packed_texture_names": sorted(packed_texture_names),
        "bounds": {
            "min": [mins.x, mins.y, mins.z],
            "max": [maxs.x, maxs.y, maxs.z],
            "size": [size.x, size.y, size.z],
        },
        "object_name_sample": object_names[:50],
        "material_names": material_names,
    }


def main() -> None:
    args = parse_args()
    input_path = Path(args.input).resolve()
    output_path = Path(args.output).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)

    clear_scene()
    import_model(input_path)
    report = audit_model(input_path)
    output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"Model audit written to {output_path}")


if __name__ == "__main__":
    main()
