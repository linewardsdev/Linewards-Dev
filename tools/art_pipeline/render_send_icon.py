#!/usr/bin/env python3
"""Render a 128x128 transparent send-menu icon from a creep's source model.

The authored icons for the original five creeps are 128x128 RGBA with a fully
transparent background and the creature framed tight in-frame. This reproduces
that from a model file so a newly wired creep gets an icon consistent with the
mesh it actually spawns, rather than a hand-drawn approximation.

Renders at 4x and downsamples, which is what keeps small details readable at
128px instead of aliasing into mush.

    /Applications/Blender.app/Contents/MacOS/Blender --background \
      --python tools/art_pipeline/render_send_icon.py -- <model> <output.png>
"""
import sys
import math

import bpy
from mathutils import Vector

SUPERSAMPLE = 4
ICON_SIZE = 128


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, out = argv[0], argv[1]

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    bpy.ops.import_scene.fbx(filepath=src)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        print("ICON_FAILED: no mesh in model")
        sys.exit(1)

    mins = Vector((float("inf"),) * 3)
    maxs = Vector((float("-inf"),) * 3)
    for obj in meshes:
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            for axis in range(3):
                mins[axis] = min(mins[axis], world[axis])
                maxs[axis] = max(maxs[axis], world[axis])

    center = (mins + maxs) * 0.5
    size = max(maxs.x - mins.x, maxs.y - mins.y, maxs.z - mins.z)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = ICON_SIZE * SUPERSAMPLE
    scene.render.resolution_y = ICON_SIZE * SUPERSAMPLE
    # Transparent film is the whole point: these composite onto the send button.
    scene.render.film_transparent = True

    world = bpy.data.worlds.new("IconWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.55, 0.62, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.85
    scene.world = world

    key = bpy.data.lights.new("Key", type="AREA")
    key.energy = 320
    key.size = 4.0
    key_obj = bpy.data.objects.new("Key", key)
    scene.collection.objects.link(key_obj)
    key_obj.location = (-2.4, -3.0, 3.6)
    key_obj.rotation_euler = (math.radians(48), 0, math.radians(-38))

    rim = bpy.data.lights.new("Rim", type="AREA")
    rim.energy = 140
    rim.size = 3.0
    rim_obj = bpy.data.objects.new("Rim", rim)
    scene.collection.objects.link(rim_obj)
    rim_obj.location = (2.8, 2.4, 2.2)
    rim_obj.rotation_euler = (math.radians(62), 0, math.radians(140))

    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.type = "ORTHO"
    # Tight framing with a small margin so the silhouette nearly fills the button.
    cam_data.ortho_scale = max(size * 1.12, 0.5)
    cam_obj = bpy.data.objects.new("IconCam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    cam_obj.location = center + Vector((1.9, -2.6, 1.7))
    cam_obj.rotation_euler = (center - cam_obj.location).to_track_quat("-Z", "Y").to_euler()

    scene.view_settings.view_transform = "Filmic"
    scene.view_settings.look = "Medium High Contrast"

    scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print(f"ICON_RENDERED {out}")


main()
