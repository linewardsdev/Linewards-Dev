"""The shipped match camera, reproduced in Blender, for rig verification renders.

Why this is shared rather than inlined per rig script
----------------------------------------------------
Every other helper in the rig scripts is duplicated per script on purpose — they are
self-contained tools and a divergent `world_axis_quaternion` would be a local bug. This one
is different. OPEN_ITEMS item 4's standing rule is "render-check from the ACTUAL game camera
angle first", and the Brute's invisible leg rig is what happens when that check is done from
a camera that is not the real one. A per-script copy of the camera maths is a per-script
opportunity to verify against the wrong angle and conclude the wrong thing, silently. There
is one match camera, so there is one definition of it here.

What the match camera is
------------------------
`UnityVerticalSliceRenderer.Camera.cs`: orthographic, tilted `TILT_DEGREES` off vertical,
orbiting the board centre, looking at it. Creeps travel toward Unity -Z and the camera sits
on the -Z side, so creeps are viewed HEAD-ON and from above — the geometry a rig has to read
against.

The Unity->Blender part
-----------------------
A creep's on-screen pose is not the Blender pose. The wrapper prefab applies a per-creep
import rotation (`Creep3DImportSpec.ImportEulerAngles`) on top of the FBX axis conversion, so
a rig verified in Blender's own frame is verified against a pose the game never shows. Both
transforms are applied here:

    Blender(Z-up, RH)  ->  Unity(Y-up, LH):   (bx, by, bz) -> (-bx, bz, -by)

which is the FBX round trip Blender's exporter and Unity's importer perform between them.
Checked against a known case rather than asserted: the Brute rig faces Blender -Y, and
Blender -Y maps to Unity +Z, which is why its spec carries yaw 180 to turn it to the travel
direction. Any change to this mapping should be re-checked the same way.
"""
import math

import bpy
from mathutils import Matrix, Vector

# Tilt off vertical, matching DefaultActiveLaneTiltDegrees in the renderer.
TILT_DEGREES = 30.0

# Blender -> Unity basis change for the FBX round trip.
_BLENDER_TO_UNITY = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))


def _unity_rx(a):
    """Unity pitch. Left-handed about +X, so +Y rolls toward +Z."""
    c, s = math.cos(a), math.sin(a)
    return Matrix(((1, 0, 0), (0, c, -s), (0, s, c)))


def _unity_ry(a):
    """Unity yaw. Left-handed about +Y, so +Z rolls toward +X."""
    c, s = math.cos(a), math.sin(a)
    return Matrix(((c, 0, s), (0, 1, 0), (-s, 0, c)))


def _unity_rz(a):
    c, s = math.cos(a), math.sin(a)
    return Matrix(((c, -s, 0), (s, c, 0), (0, 0, 1)))


def blender_rotation_for_unity_euler(euler_xyz):
    """The Blender-space rotation equivalent to a Unity `Quaternion.Euler(x, y, z)`.

    Unity composes its euler as Z, then X, then Y, hence Ry @ Rx @ Rz.
    """
    x, y, z = (math.radians(v) for v in euler_xyz)
    rotation = _unity_ry(y) @ _unity_rx(x) @ _unity_rz(z)
    return _BLENDER_TO_UNITY.inverted() @ rotation @ _BLENDER_TO_UNITY


def render_game_camera_frames(prefix, frames, import_euler=(0.0, 0.0, 0.0),
                              resolution=512, extra_views=True):
    """Render `frames` from the match camera, with the creep posed as the game poses it.

    Leaves the scene's own objects untouched apart from the temporary camera/lights/world it
    adds, and restores the frame it found — the caller still has to export afterwards.

    A second view down the lane axis is rendered alongside by default. It is what
    disambiguates the failure the game view cannot: from head-on, motion along the travel
    axis is heavily foreshortened, so a limb that is barely moving and a limb that is moving
    straight at the camera look the same. They do not from the side.
    """
    scene = bpy.context.scene
    posed = [ob for ob in bpy.data.objects if ob.parent is None and ob.type in {'MESH', 'ARMATURE'}]
    rotation = blender_rotation_for_unity_euler(import_euler).to_4x4()
    original = {ob: ob.matrix_world.copy() for ob in posed}
    for ob in posed:
        ob.matrix_world = rotation @ original[ob]

    world = bpy.data.worlds.new("GameCamWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.06, 0.07, 0.09, 1)
    added = []
    for name, location, energy in (("Key", (1.4, 1.4, 1.9), 520), ("Fill", (-1.5, -1.1, 0.9), 210)):
        light_data = bpy.data.lights.new(name, type='AREA')
        light_data.energy = energy
        light_data.size = 2.0
        light = bpy.data.objects.new(name, light_data)
        light.location = location
        bpy.context.collection.objects.link(light)
        light.rotation_euler = (Vector((0, 0, 0.2)) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()
        added.append(light)

    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution

    points = []
    for ob in bpy.data.objects:
        if ob.type == 'MESH':
            points.extend([ob.matrix_world @ Vector(corner) for corner in ob.bound_box])
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    centre = (low + high) * 0.5
    extent = max((high - low).x, (high - low).y, (high - low).z)

    camera_data = bpy.data.cameras.new("GameCam")
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = extent * 1.30
    camera = bpy.data.objects.new("GameCam", camera_data)
    bpy.context.collection.objects.link(camera)
    added.append(camera)
    previous_camera = scene.camera
    scene.camera = camera

    radius = extent * 5.0
    tilt = math.radians(TILT_DEGREES)
    # Unity's camera offset (0, +R cos t, -R sin t) mapped through the basis change above.
    views = [("gamecam", Vector((0.0, radius * math.sin(tilt), radius * math.cos(tilt))))]
    if extra_views:
        views.append(("lane", Vector((radius * math.sin(tilt), 0.0, radius * math.cos(tilt)))))

    previous_frame = scene.frame_current
    for view_name, offset in views:
        camera.location = centre + offset
        camera.rotation_euler = (-offset).to_track_quat('-Z', 'Y').to_euler()
        for frame in frames:
            scene.frame_set(frame)
            scene.render.filepath = "%s_%s_f%03d.png" % (prefix, view_name, frame)
            bpy.ops.render.render(write_still=True)
    scene.frame_set(previous_frame)

    scene.camera = previous_camera
    for ob in added:
        bpy.data.objects.remove(ob, do_unlink=True)
    for ob in posed:
        ob.matrix_world = original[ob]
    print("RENDERED %d frame(s) x %d view(s) from the match camera" % (len(frames), len(views)))
