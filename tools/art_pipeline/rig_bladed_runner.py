"""Rig and animate the Runner: a bladed glaive that flies. Blades beat, body banks.

Why this creep has no leg rig, and no foot-skate number
-------------------------------------------------------
Item 4's standing rule sent a render from the match camera ahead of the rig, and as with the
Siege and the Serpent it answered a question one step earlier than the rule anticipated. The
Runner is not a walker with occluded legs. It is a floating blade — the "blade claw" source
name is literal.

The measurement that settles it: the prepared mesh is 0.900 x 0.452 x 0.230, and of its 7702
vertices, FIFTY-TWO sit below 18% of its height. Those fifty-two occupy a single stalk
spanning x [-0.070, +0.023], y [-0.038, +0.052] — one small stub, not four columns. The body
mass does not begin until z = 0.069 and peaks at z = 0.184..0.195. There is nothing to plant
and nothing to swing.

That has a consequence for reporting, and it should be stated rather than worked around:
**this creep has no foot-skate figure, because it has no ground contact to skate.** The
walker's metric compares contact-frame foot displacement to forward travel; with no contact
frame both terms are undefined. Producing a number anyway would be inventing one. What is
measurable and is reported instead is blade-tip travel per cycle against body length.

What actually moves
-------------------
Four blades, all clearly visible from the match camera (checked before rigging — nothing on
this creep occludes anything else, its whole silhouette is edge-on plates). The blades are
the silhouette: |y| runs to 0.226 at the widest, against a 0.452 total width, so the blades
ARE the width. Everything else is a segmented spine along x with a lance at +0.45 and a tail
spike at -0.45.

Against item 4's second walker defect — mirrored poses that read identically head-on — the
blades deliberately do NOT open and close together. Left and right run a half cycle apart,
so the pair is asymmetric at every frame of the clip except the two crossings, and the body
banks toward whichever side is trailing. A symmetric flap would have been the exact failure
the walker's trot was: two mirror-image poses that, viewed head-on, are one pose.

Usage:
    blender --background --python tools/art_pipeline/rig_bladed_runner.py -- \\
        <prepared.fbx> <out_rigged.fbx> [render_prefix]
"""
import math
import os
import sys

import bpy
from mathutils import Quaternion, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from blender_game_camera import render_game_camera_frames  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
SRC_FBX = argv[0]
OUT_FBX = argv[1]
RENDER_PREFIX = argv[2] if len(argv) > 2 else ""

# The import rotation the wrapper prefab applies. The spec was Euler(35, 90, 0), whose own
# comment flagged the yaw as "a first guess to test whether it turns the blade to face down
# the lane... verifying with a capture before trusting the sign". Verified here, and the sign
# was wrong: the lance at Blender +X maps to Unity -X, and yaw 90 takes that to +Z, which is
# away from the travel direction. Yaw 270 points it down the lane. The 35 is kept — despite
# its comment calling it a nose-up pitch it is actually a ROLL about the creep's own long
# axis, which is what turns a 0.230-tall flat plate into the 0.448 the prefab measures, and
# that effect is real whichever way the lance points.
IMPORT_EULER = (35.0, 270.0, 0.0)

# ---- Measured geometry ----------------------------------------------------
# Sliced along x in 18 bands. The lance tapers from x +0.30 (|y| <= 0.142) to the tip at
# +0.45 (|y| <= 0.012). The tail spike runs the other way from x -0.35 (|y| <= 0.143) to
# -0.45 (|y| <= 0.070). Blade mass is everything at |y| > 0.088: it peaks at |y| 0.226 over
# x [-0.10, 0.00] and the crescent tips curve forward to reach x +0.30 still at |y| 0.14.
# Nothing outside the blades exceeds |y| 0.088 anywhere along the body, which is what makes
# a single lateral threshold a clean cut.
SPINE_Z = 0.158
BLADE_ROOT_X = 0.02
BLADE_ROOT_Y = 0.090
BLADE_TIP = Vector((-0.22, 0.215, 0.148))
NOSE_X = 0.20
NOSE_TIP = Vector((0.45, 0.0, 0.185))
TAIL_X = -0.20
TAIL_TIP = Vector((-0.45, 0.0, 0.122))

BLADE_CUT_Y = 0.088
BLADE_BLEND = 0.045
NOSE_CUT_X = 0.28
NOSE_BLEND = 0.08
TAIL_CUT_X = -0.33
TAIL_BLEND = 0.06

# ---- Motion ---------------------------------------------------------------
# No ground contact means no speed constraint on the cycle, so cadence is chosen for
# legibility rather than solved. At SpeedPerSecond 1 the creep covers 1.3333 world units/sec
# and its own world length is 1.147, so a 30-frame clip is 1.45 body lengths of travel and
# two blade beats inside it reads as fast without blurring.
CLIP_FRAMES = 30
BLADE_BEATS_PER_CLIP = 2
KEY_EVERY = 1

BLADE_SWEEP_DEG = 15.0     # about vertical: the channel the camera projection preserves
BLADE_TWIST_DEG = 9.0      # about the blade's own reach, so the plate catches the light
BODY_ROLL_DEG = 6.0        # banks toward the trailing blade
BODY_YAW_DEG = 3.0
NOSE_WEAVE_DEG = 5.0
TAIL_WEAVE_DEG = 8.0

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=SRC_FBX)

mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
print("IMPORTED mesh=%s verts=%d" % (mesh.name, len(mesh.data.vertices)))

# ---- Build armature -------------------------------------------------------
arm_data = bpy.data.armatures.new("RunnerArmature")
arm = bpy.data.objects.new("RunnerArmature", arm_data)
bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones


def bone(name, head, tail, parent=None):
    b = eb.new(name)
    b.head, b.tail = head, tail
    if parent:
        b.parent = parent
        b.use_connect = False
    return b


root = bone("Root", Vector((0, 0, 0)), Vector((0, 0, 0.08)))
# Body runs rear -> front, so its length points at the lance end (+X world).
body = bone("Body", Vector((TAIL_X, 0, SPINE_Z)), Vector((NOSE_X, 0, SPINE_Z)), root)
bone("Nose", Vector((NOSE_X, 0, SPINE_Z)), NOSE_TIP, body)
bone("Tail", Vector((TAIL_X, 0, SPINE_Z)), TAIL_TIP, body)

BLADES = {"BladeL": -1.0, "BladeR": 1.0}
for name, sign in BLADES.items():
    head_pos = Vector((BLADE_ROOT_X, sign * BLADE_ROOT_Y, SPINE_Z))
    tail_pos = Vector((BLADE_TIP.x, sign * BLADE_TIP.y, BLADE_TIP.z))
    bone(name, head_pos, tail_pos, body)

bpy.ops.object.mode_set(mode='OBJECT')

# ---- Skin ----------------------------------------------------------------
# Explicit region weights on the same principle as the other rigs here: automatic weighting
# would let a blade bone bend the spine segments it passes over, and these are hard plates.
# Blended at the boundaries — unlike rig_wheeled_ram.py's binary wheel cut — because nothing
# on this creep rotates through more than 15 degrees, so a partial weight interpolates
# between two nearby poses rather than smearing a vertex across a revolution.
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_NAME')

group_names = ["Root", "Body", "Nose", "Tail"] + list(BLADES)
for name in group_names:
    if name not in mesh.vertex_groups:
        mesh.vertex_groups.new(name=name)


def clamp01(t):
    return max(0.0, min(1.0, t))


mw = mesh.matrix_world
assigned = {k: 0 for k in list(BLADES) + ["Nose", "Tail", "Body"]}
for v in mesh.data.vertices:
    co = mw @ v.co
    blade_w = clamp01((abs(co.y) - BLADE_CUT_Y) / BLADE_BLEND)
    blade_name = "BladeR" if co.y >= 0.0 else "BladeL"
    if blade_w > 0.0:
        mesh.vertex_groups[blade_name].add([v.index], blade_w, 'REPLACE')
        assigned[blade_name] += 1

    remainder = 1.0 - blade_w
    if remainder <= 0.0:
        continue

    nose_w = clamp01((co.x - NOSE_CUT_X) / NOSE_BLEND)
    tail_w = clamp01((TAIL_CUT_X - co.x) / TAIL_BLEND)
    part = min(1.0, nose_w + tail_w)
    if nose_w > 0.0:
        mesh.vertex_groups["Nose"].add([v.index], remainder * nose_w, 'REPLACE')
        assigned["Nose"] += 1
    if tail_w > 0.0:
        mesh.vertex_groups["Tail"].add([v.index], remainder * tail_w, 'REPLACE')
        assigned["Tail"] += 1
    if part < 1.0:
        mesh.vertex_groups["Body"].add([v.index], remainder * (1.0 - part), 'REPLACE')
        assigned["Body"] += 1

print("SKINNED explicit weights: %s" % assigned)
modifier = mesh.modifiers.new("Armature", 'ARMATURE')
modifier.object = arm
mesh.parent = arm

# ---- Author the clip ------------------------------------------------------
scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = CLIP_FRAMES
scene.render.fps = 24

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for pb in arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'

# The creature faces +X, so it banks about world X and sweeps its blades about world Z.
SWEEP_AXIS = Vector((0.0, 0.0, 1.0))
ROLL_AXIS = Vector((1.0, 0.0, 0.0))
PITCH_AXIS = Vector((0.0, 1.0, 0.0))


def world_axis_quaternion(pose_bone, world_axis, angle):
    """Rotate about a WORLD axis. The blade bones reach out diagonally and the tail reaches
    back and down, so no two of these bones share a local frame."""
    rest = pose_bone.bone.matrix_local.to_3x3()
    return Quaternion((rest.inverted() @ Vector(world_axis)).normalized(), angle)


for frame in range(1, CLIP_FRAMES + 1, KEY_EVERY):
    clip_u = (frame - 1) / float(CLIP_FRAMES)
    beat_u = clip_u * BLADE_BEATS_PER_CLIP

    # Half a cycle apart. This is the whole answer to the walker's symmetric-trot defect:
    # there is no frame at which the left and right blades hold mirrored poses, so the
    # head-on silhouette cannot collapse into a single pose the way a symmetric flap does.
    for name, sign in BLADES.items():
        offset = 0.0 if name == "BladeL" else 0.5
        u = beat_u + offset
        pb = arm.pose.bones[name]
        sweep = math.radians(BLADE_SWEEP_DEG) * math.sin(2.0 * math.pi * u)
        twist = math.radians(BLADE_TWIST_DEG) * math.cos(2.0 * math.pi * u)
        pb.rotation_quaternion = (world_axis_quaternion(pb, SWEEP_AXIS, sweep)
                                  @ world_axis_quaternion(pb, ROLL_AXIS, sign * twist))
        pb.keyframe_insert("rotation_quaternion", frame=frame)

    # Body banks toward the trailing blade, which is the left blade's own phase inverted.
    bp = arm.pose.bones["Body"]
    roll = math.radians(BODY_ROLL_DEG) * math.sin(2.0 * math.pi * beat_u + math.pi)
    yaw = math.radians(BODY_YAW_DEG) * math.sin(2.0 * math.pi * clip_u)
    bp.rotation_quaternion = (world_axis_quaternion(bp, ROLL_AXIS, roll)
                              @ world_axis_quaternion(bp, SWEEP_AXIS, yaw))
    bp.keyframe_insert("rotation_quaternion", frame=frame)

    # Lance and tail weave once per CLIP rather than per beat, so the 30 frames never repeat
    # a whole-body pose even though the blades repeat twice.
    np_ = arm.pose.bones["Nose"]
    np_.rotation_quaternion = (
        world_axis_quaternion(np_, SWEEP_AXIS, math.radians(NOSE_WEAVE_DEG)
                              * math.sin(2.0 * math.pi * clip_u))
        @ world_axis_quaternion(np_, PITCH_AXIS, math.radians(NOSE_WEAVE_DEG * 0.5)
                                * math.cos(2.0 * math.pi * clip_u)))
    np_.keyframe_insert("rotation_quaternion", frame=frame)

    tp = arm.pose.bones["Tail"]
    tp.rotation_quaternion = world_axis_quaternion(
        tp, SWEEP_AXIS, math.radians(TAIL_WEAVE_DEG) * math.sin(2.0 * math.pi * clip_u + math.pi * 0.6))
    tp.keyframe_insert("rotation_quaternion", frame=frame)

action = arm.animation_data.action
action.name = "Walk"
print("ACTION name=%s frames=%s" % (action.name, action.frame_range))


def iter_fcurves(act):
    if hasattr(act, "fcurves") and len(getattr(act, "fcurves", [])):
        return list(act.fcurves)
    out = []
    for layer in getattr(act, "layers", []):
        for strip in getattr(layer, "strips", []):
            for cb in getattr(strip, "channelbags", []):
                out.extend(cb.fcurves)
    return out


for fc in iter_fcurves(action):
    for kp in fc.keyframe_points:
        kp.interpolation = 'LINEAR'

bpy.ops.object.mode_set(mode='OBJECT')

if RENDER_PREFIX:
    render_game_camera_frames(
        RENDER_PREFIX,
        [1 + i * (CLIP_FRAMES // 6) for i in range(6)],
        import_euler=IMPORT_EULER)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=OUT_FBX,
    use_selection=True,
    object_types={'MESH', 'ARMATURE', 'EMPTY'},
    apply_unit_scale=True,
    bake_space_transform=False,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    path_mode='COPY',
    embed_textures=False,
)
print("EXPORTED " + OUT_FBX)
