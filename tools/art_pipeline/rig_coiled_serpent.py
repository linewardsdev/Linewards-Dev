"""Rig and animate the Serpent Coil: a wave travelling around a coiled body, and a head.

Why this is not rig_quadruped_creep.py, and not a spine chain either
--------------------------------------------------------------------
Item 4's standing rule — render-check from the game camera before investing in leg
articulation — answered the question before the rig started, the same way it did for the
Siege: the Serpent has no legs. It is a snake coiled on the board, and the roster already
knew it. `Creep3DProofSetGenerator` moved this creep off the golems' HeavyBob motion style
with the note "that is a leg-driven lumber and this creep has no limbs to step on".

The obvious alternative — a head-to-tail spine chain, the way you would rig a snake that is
extended — is also wrong here, and measurably so. The prepared mesh is a closed coil: at
every one of twelve 30-degree sectors the ground band is occupied, outer radius 0.41 to 0.47
all the way round, with the head raised in the middle at z 0.48..0.545. There is no free end
to lead a chain from and no straight axis to run it along. Tracing the spiral to lay a chain
on it would be a fit to two overlapping turns (the radial histogram peaks twice, at r 0.26
and r 0.40) with nothing to check the fit against.

So the coil is rigged as what it geometrically is: a RING. Twelve sectors -- eight bones
radiating from the coil axis, each owning an angular slice -- and the motion is a wave
travelling around that ring once per clip. That is what a coiled snake looks like when it
moves, it loops exactly by construction, and it needs no spiral fit.

Why the wave is tangential rather than vertical
-----------------------------------------------
The match camera is tilted 30 degrees off vertical, so only sin(30) = 50% of any vertical
motion reaches the screen while a horizontal shift keeps cos(30) = 87% or better. The same
projection argument put a yaw scan on the Spire Turret Walker after its measured screen-
horizontal travel came out at ~2% of frame. A vertical breathing coil at an amplitude the
mesh can absorb moves 2-3 screen pixels; the same amplitude spent on a tangential shear
moves three times that, and reads as the body flowing around the coil rather than as the
whole creep inflating. Vertical lift is kept, small and in quadrature, so sectors rise as
they shear forward and the wave has some dimensionality — it is the second channel, not the
first.

The gait number
---------------
A coiled serpent has no footfall, so the walker's foot-skate measure has no direct
equivalent; the honest analogue is serpentine, where the body wave travels backward along
the body faster than the body travels forward, and the ratio is what characterises the gait.
Real snakes run roughly 1.2-2.0x.

    coil ring     mean contact radius 0.35 mesh units, and the prefab imports 1:1 (checked
                  against UnitBoundsReport), so with CreepVisualLibrary scale 1.23 and the
                  renderer's 1.18 horizontal that is a 3.192 world-unit lap.
    ground speed  SpeedPerSecond 1 / CombatService.BaseMovementCost 3 at 4 ticks/sec
                  = 1.3333 world units/sec, played back at 1.00x.
    clip          38 frames = 1.5833s, carrying exactly one lap of the wave.

    wave 3.192 / 1.5833 = 2.016 units/sec against 1.3333 travelled: ratio 1.51x.

Usage:
    blender --background --python tools/art_pipeline/rig_coiled_serpent.py -- \\
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

# The import rotation the wrapper prefab applies. Serpent's spec was yaw 0, which pointed
# the head down the lane AWAY from the camera — the coil presented its back. Corrected to
# 180 alongside this rig, because a head animation on a head nobody can see is not an
# animation. See the Serpent entry in Creep3DProofSetGenerator.
IMPORT_EULER = (0.0, 180.0, 0.0)

# ---- Measured geometry ----------------------------------------------------
# Prepared mesh, 7568 verts, bounds x +-0.450, y -0.443..+0.443, z 0..0.545.
# Radial histogram about the coil axis: mass peaks at r 0.236..0.284 (1671 verts) and again
# at r 0.378..0.426 (1099), i.e. two visible turns of coil. Ground band (z <= 0.065) is
# occupied at all twelve 30-degree sectors, outer radius 0.407..0.473 — a closed ring, which
# is what licenses the sector rig below. The 120 highest verts centre on (-0.060, +0.032) at
# z 0.481..0.545: the head, raised at the middle of the coil.
COIL_CENTRE = Vector((0.0, 0.0))
COIL_MEAN_RADIUS = 0.35
COIL_Z = 0.16               # height the sector bones sit at, mid-coil
SECTOR_COUNT = 8

HEAD_RADIUS = 0.14          # head column, measured off the top-vertex cluster
HEAD_Z = 0.36
NECK_RADIUS = 0.19
NECK_Z = 0.20

# ---- Gait solve -----------------------------------------------------------
CLIP_FRAMES = 38            # one full lap of the wave; see the module docstring
KEY_EVERY = 1

SECTOR_SHEAR_DEG = 8.0      # tangential, the primary channel
SECTOR_LIFT = 0.030         # vertical, in quadrature with the shear
HEAD_YAW_DEG = 13.0
HEAD_PITCH_DEG = 7.0
HEAD_RISE = 0.022
BODY_YAW_DEG = 2.5          # slow counter-rotation of the whole coil

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=SRC_FBX)

mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
print("IMPORTED mesh=%s verts=%d" % (mesh.name, len(mesh.data.vertices)))

# ---- Build armature -------------------------------------------------------
arm_data = bpy.data.armatures.new("SerpentArmature")
arm = bpy.data.objects.new("SerpentArmature", arm_data)
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
body = bone("Body", Vector((COIL_CENTRE.x, COIL_CENTRE.y, 0.04)),
            Vector((COIL_CENTRE.x, COIL_CENTRE.y, COIL_Z)), root)

SECTOR_ANGLES = [2.0 * math.pi * i / SECTOR_COUNT for i in range(SECTOR_COUNT)]
for index, angle in enumerate(SECTOR_ANGLES):
    outward = Vector((math.cos(angle), math.sin(angle), 0.0))
    head_pos = Vector((COIL_CENTRE.x, COIL_CENTRE.y, COIL_Z)) + outward * 0.08
    tail_pos = Vector((COIL_CENTRE.x, COIL_CENTRE.y, COIL_Z)) + outward * COIL_MEAN_RADIUS
    bone("Coil%d" % index, head_pos, tail_pos, body)

neck = bone("Neck", Vector((0, 0, NECK_Z)), Vector((0, 0, HEAD_Z)), body)
bone("Head", Vector((0, 0, HEAD_Z)), Vector((0, 0, 0.545)), neck)

bpy.ops.object.mode_set(mode='OBJECT')

# ---- Skin ----------------------------------------------------------------
# Angular weighting round the ring: each vertex splits between the two sector bones it lies
# between, linearly in angle, which sums to exactly 1 and gives a continuous shear with no
# seam at any sector boundary. This is the one place where blended weights are RIGHT rather
# than merely tolerable — the coil is a continuous body and the wave is supposed to be
# smeared across it. (Contrast rig_wheeled_ram.py, where a blended weight on a part that
# rotates through 360 degrees tears it apart.)
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_NAME')

group_names = ["Root", "Body", "Neck", "Head"] + ["Coil%d" % i for i in range(SECTOR_COUNT)]
for name in group_names:
    if name not in mesh.vertex_groups:
        mesh.vertex_groups.new(name=name)


def clamp01(t):
    return max(0.0, min(1.0, t))


SECTOR_SPAN = 2.0 * math.pi / SECTOR_COUNT
mw = mesh.matrix_world
assigned = {"Head": 0, "Neck": 0, "Coil": 0}
for v in mesh.data.vertices:
    co = mw @ v.co
    dx, dy = co.x - COIL_CENTRE.x, co.y - COIL_CENTRE.y
    radius = math.hypot(dx, dy)

    head_w = clamp01((HEAD_RADIUS - radius) / 0.05) * clamp01((co.z - HEAD_Z) / 0.06)
    neck_w = (clamp01((NECK_RADIUS - radius) / 0.05)
              * clamp01((co.z - NECK_Z) / 0.06)
              * (1.0 - head_w))
    if head_w > 0.0:
        mesh.vertex_groups["Head"].add([v.index], head_w, 'REPLACE')
        assigned["Head"] += 1
    if neck_w > 0.0:
        mesh.vertex_groups["Neck"].add([v.index], neck_w, 'REPLACE')
        assigned["Neck"] += 1

    coil_share = max(0.0, 1.0 - head_w - neck_w)
    if coil_share <= 0.0:
        continue
    angle = math.atan2(dy, dx) % (2.0 * math.pi)
    lower = int(angle // SECTOR_SPAN) % SECTOR_COUNT
    upper = (lower + 1) % SECTOR_COUNT
    t = (angle - lower * SECTOR_SPAN) / SECTOR_SPAN
    mesh.vertex_groups["Coil%d" % lower].add([v.index], coil_share * (1.0 - t), 'REPLACE')
    mesh.vertex_groups["Coil%d" % upper].add([v.index], coil_share * t, 'REPLACE')
    assigned["Coil"] += 1

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

YAW_AXIS = Vector((0.0, 0.0, 1.0))


def world_axis_quaternion(pose_bone, world_axis, angle):
    """Rotate about a WORLD axis. Sector bones radiate in eight different directions, so
    their local frames differ from each other — only a world axis means the same thing to
    all eight, which is what makes the wave a wave and not eight unrelated wobbles."""
    rest = pose_bone.bone.matrix_local.to_3x3()
    return Quaternion((rest.inverted() @ Vector(world_axis)).normalized(), angle)


for frame in range(1, CLIP_FRAMES + 1, KEY_EVERY):
    clip_u = (frame - 1) / float(CLIP_FRAMES)

    for index in range(SECTOR_COUNT):
        # Phase runs BACKWARD around the ring relative to travel, which is the direction a
        # serpentine wave runs: the body pushes back, the creep goes forward.
        phase = clip_u + index / float(SECTOR_COUNT)
        pb = arm.pose.bones["Coil%d" % index]
        shear = math.radians(SECTOR_SHEAR_DEG) * math.sin(2.0 * math.pi * phase)
        pb.rotation_quaternion = world_axis_quaternion(pb, YAW_AXIS, shear)
        rest = pb.bone.matrix_local.to_3x3()
        lift = SECTOR_LIFT * math.cos(2.0 * math.pi * phase)
        pb.location = rest.inverted() @ Vector((0.0, 0.0, lift))
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)

    # The whole coil counter-rotates slightly against the wave. Small, and deliberately at
    # the wave's own period rather than a multiple of it, so it reads as the coil settling
    # under the wave rather than as a second effect.
    bp = arm.pose.bones["Body"]
    bp.rotation_quaternion = world_axis_quaternion(
        bp, YAW_AXIS, math.radians(BODY_YAW_DEG) * math.sin(2.0 * math.pi * clip_u + math.pi))
    bp.keyframe_insert("rotation_quaternion", frame=frame)

    # Head. Yaw is the channel that survives the camera projection best, so the head casts
    # side to side rather than nodding; the pitch and rise are secondary and run at double
    # rate so the head never repeats a pose inside one lap of the wave.
    for name, yaw_scale, pitch_scale, rise_scale, offset in (
            ("Neck", 0.45, 0.35, 0.4, 0.0),
            ("Head", 0.55, 0.65, 0.6, 0.12)):
        pb = arm.pose.bones[name]
        u = clip_u + offset
        yaw = math.radians(HEAD_YAW_DEG) * yaw_scale * math.sin(2.0 * math.pi * u)
        pitch = math.radians(HEAD_PITCH_DEG) * pitch_scale * math.sin(4.0 * math.pi * u)
        pb.rotation_quaternion = (world_axis_quaternion(pb, YAW_AXIS, yaw)
                                  @ world_axis_quaternion(pb, Vector((1.0, 0.0, 0.0)), pitch))
        rest = pb.bone.matrix_local.to_3x3()
        pb.location = rest.inverted() @ Vector(
            (0.0, 0.0, HEAD_RISE * rise_scale * math.sin(4.0 * math.pi * u)))
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)

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


# LINEAR between evenly spaced samples of a sine, for the same reason rig_biped_creep.py
# uses it: the curve is periodic by construction and Bezier handles overshoot at the loop.
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
