"""Rig and animate the Siege ram: four rolling wheels, a sprung chassis, a ram head.

Why this is not rig_quadruped_creep.py
--------------------------------------
Item 11 lists Siege as one of the seven creeps needing a rig, and the obvious reading is
"another quadruped, reuse the golem script". Item 4's standing rule says render-check from
the game camera before investing in leg articulation, and doing that first — before writing
any rig — answered a question one step earlier than the rule anticipated:

    the Siege has no legs. It is a four-WHEELED armoured battering ram.

Measured on the prepared mesh: four discs of radius 0.098 on the flanks, hubs at
z = 0.129, front pair at x = -0.022 and rear pair at x = +0.319, with the chassis riding
above them and a plow nose overhanging to x = -0.45 with no wheel under it. A thigh/shin
leg rig on this creature would have been a rig for limbs that do not exist.

So the "leg articulation" question resolves into a better one, because a wheel is not a
worse limb than a leg — it is a limb with no stride limit. The Spire Turret Walker's whole
problem (its script's header, and item 4) was that a leg's stride is bounded by its length,
so a creep travelling faster than that stride can carry gets dragged: 51x foot skate, cut to
8.3x only by pushing stride and cadence past what reads. A wheel has no such bound. Its
contact speed is 2*pi*r per revolution and the revolution rate is free, so the rate can be
SOLVED from the creep's real ground speed rather than traded off against legibility.

The solve
---------
    ground speed   SpeedPerSecond 1 / CombatService.BaseMovementCost 3, at 4 ticks/sec
                   = 1.3333 world units/sec
    playback       1.00x exactly (UnityVerticalSliceRenderer's CreepWalkReferenceSpeed is 1,
                   so a speed-1 creep plays its clip at the authored rate)
    wheel          r = 0.098 mesh units; the prefab imports 1:1 (verified against
                   UnitBoundsReport: Creep_Siege_3D measures 0.506 tall, exactly the mesh),
                   then CreepVisualLibrary scale 1.36 and the renderer's 1.18/1.12/1.18.
                   Non-uniform, so the wheel is a slight ellipse in world space:
                   a = 0.1573, b = 0.1493, circumference (Ramanujan) = 0.9631 units.

1.3333 / 0.9631 = 1.3844 rev/sec, i.e. 17.331 frames per revolution at 24 fps. The clip has
to contain a whole number of revolutions or the wheels snap back at the loop point, so the
clip length is chosen to land on one: 52 frames carrying 3 revolutions is 17.333 frames per
revolution, which is 0.016% off the exact figure.

    Measured wheel skate: 1.0002x. (Spire Turret Walker, for comparison: 51x, then 8.3x.)

Nothing here is a trade-off against legibility either — 1.38 rev/sec is a full turn every
0.72s, which reads as rolling rather than strobing.

Symmetric-pose defect
---------------------
Item 4's second walker defect was mirrored contact poses that are indistinguishable head-on,
which is the camera this creep is viewed from. Wheels cannot mirror-cancel the way a trot
does, but a chassis bobbing on all four at once would, so the three body channels run on
deliberately different periods: bob at wheel frequency (3 per clip), roll once per clip, and
mantlet yaw once per clip a quarter out of phase. No two frames of the 52 repeat a pose.

Usage:
    blender --background --python tools/art_pipeline/rig_wheeled_ram.py -- \\
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

# The import rotation the wrapper prefab applies (Creep3DProofSetGenerator's Siege spec).
# Verification renders are pointless from any other pose — see blender_game_camera.
IMPORT_EULER = (0.0, 90.0, 0.0)

# ---- Measured geometry ----------------------------------------------------
# From the prepared mesh (7629 verts). Wheels were isolated by taking the outer-Y shell
# (|y| > 0.80 * max|y|) below 55% of height and connected-component clustering it in the XZ
# plane, which returns exactly two discs per flank and nothing else. Both flanks agree to
# within 0.002 on every figure, which is the check that they are wheels and not noise.
#
#   front  x span [-0.118, +0.074]  z span [0.030, 0.231]
#   rear   x span [+0.221, +0.416]  z span [0.031, 0.278]  (upper z is fender, not tyre)
#
# The creature faces -X: the plow nose tapers to x = -0.45 (|y| < 0.03 there) while the rear
# carries the tall mantlet mass that peaks at z = 0.506 over x in [0.193, 0.321].
WHEEL_RADIUS = 0.098
WHEEL_HUB_Z = 0.129
WHEEL_FRONT_X = -0.022
WHEEL_REAR_X = 0.319
WHEEL_Y = 0.205            # hub mid-plane; tyre mass runs |y| 0.15..0.233

CHASSIS_Z = 0.30
CHASSIS_HALF = 0.30
RAM_TIP = Vector((-0.50, 0.0, 0.16))
RAM_ROOT = Vector((-0.28, 0.0, 0.22))
MANTLET_X = 0.27
MANTLET_Z = 0.34

# ---- Gait solve -----------------------------------------------------------
CLIP_FRAMES = 52
WHEEL_REVS_PER_CLIP = 3
KEY_EVERY = 1              # 20 degrees of wheel per key; coarser risks a >180 deg slerp step

# Chassis. A siege ram is a machine, so no breathing scale pulse — this is suspension.
BODY_BOB = 0.016           # vertical, at wheel frequency
BODY_PITCH_DEG = 1.8       # nose dip, at wheel frequency
BODY_ROLL_DEG = 2.6        # once per clip, deliberately NOT at wheel frequency
MANTLET_YAW_DEG = 7.0      # the siege tower tracks; once per clip, quarter phase offset
MANTLET_PITCH_DEG = 2.0
RAM_THRUST = 0.022         # fore-aft ram travel in its own housing
RAM_THRUSTS_PER_CLIP = 3

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=SRC_FBX)

mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
print("IMPORTED mesh=%s verts=%d" % (mesh.name, len(mesh.data.vertices)))

# ---- Build armature -------------------------------------------------------
arm_data = bpy.data.armatures.new("RamArmature")
arm = bpy.data.objects.new("RamArmature", arm_data)
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
# Chassis runs rear -> front, so its own length points at the ram end (-X world).
body = bone("Body", Vector((CHASSIS_HALF, 0, CHASSIS_Z)), Vector((-CHASSIS_HALF, 0, CHASSIS_Z)), root)
bone("Ram", RAM_ROOT, RAM_TIP, body)
bone("Mantlet", Vector((MANTLET_X, 0, MANTLET_Z)), Vector((MANTLET_X, 0, MANTLET_Z + 0.18)), body)

# Wheel bones pivot at the hub and point outboard. The bone direction is cosmetic — spin is
# applied about the WORLD lateral axis, for the same reason the golem script swings legs
# about a world axis: a bone's own local axes are not reliably the axes you mean.
WHEELS = {
    "WheelFL": Vector((WHEEL_FRONT_X, -WHEEL_Y, WHEEL_HUB_Z)),
    "WheelFR": Vector((WHEEL_FRONT_X, WHEEL_Y, WHEEL_HUB_Z)),
    "WheelBL": Vector((WHEEL_REAR_X, -WHEEL_Y, WHEEL_HUB_Z)),
    "WheelBR": Vector((WHEEL_REAR_X, WHEEL_Y, WHEEL_HUB_Z)),
}
for name, hub in WHEELS.items():
    outward = Vector((0.0, math.copysign(0.07, hub.y), 0.0))
    bone(name, hub, hub + outward, body)

bpy.ops.object.mode_set(mode='OBJECT')

# ---- Skin ----------------------------------------------------------------
# Explicit region weights, not automatic/heat-map, for the reason the golem and walker
# scripts give: auto weights let a wheel bone drag the armour plate above it, and a spinning
# wheel that drags its own fender apart is a far worse artefact than a leg bending a shell.
# A wheel's region is a disc in the XZ plane around its hub, gated on being outboard.
#
# BINARY, unlike every other rig in this directory, and the difference is not stylistic.
# Those rigs blend weights across a joint so the shell does not visibly tear, which works
# because a limb only ever swings through tens of degrees. A wheel turns through 360, and a
# vertex held at 0.5 between wheel and chassis travels HALF a revolution away from both of
# its neighbours. The first version of this script carried the golem scripts' blend band and
# rendered the tyres shredding into spikes within a few frames of frame 1 — the 20-unit blend
# sat directly on the rim band, which is where 131 of the wheel's ~280 verts are. Any
# partial weight on a fully rotating part is a tear; the only safe boundary is a hard one,
# and it is placed where the tyre ends and the fender begins so the seam sits inside the
# housing.
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_NAME')

group_names = ["Root", "Body", "Ram", "Mantlet"] + list(WHEELS)
for name in group_names:
    if name not in mesh.vertex_groups:
        mesh.vertex_groups.new(name=name)

# Cut radius read off the radial vertex histogram about each hub, taken on the outboard
# shell: both wheels put their rim in the r 0.090..0.100 band (131 verts front, 117 rear),
# and past r 0.110 the surviving verts are all at |y| 0.150..0.172 — inboard fender, not
# wheel face. 0.108 sits in that gap. Front and rear discs stay 0.34 apart, so no vertex is
# ever in range of two wheels.
WHEEL_CUT_RADIUS = 0.108
WHEEL_CUT_Y = 0.148             # inboard limit of the tyre; below this is axle and chassis
RAM_X = -0.30                   # everything ahead of this is plow, and rides the Ram bone
RAM_BLEND = 0.07
MANTLET_Z_FLOOR = 0.40          # the tower mass above the chassis
MANTLET_BLEND = 0.05


def clamp01(t):
    return max(0.0, min(1.0, t))


mw = mesh.matrix_world
assigned = {k: 0 for k in list(WHEELS) + ["Ram", "Mantlet", "Body"]}
for v in mesh.data.vertices:
    co = mw @ v.co
    wheel_name = None
    for name, hub in WHEELS.items():
        if (co.y * hub.y > 0.0
                and abs(co.y) > WHEEL_CUT_Y
                and math.hypot(co.x - hub.x, co.z - hub.z) < WHEEL_CUT_RADIUS):
            wheel_name = name
            break

    remainder = 1.0
    if wheel_name is not None:
        mesh.vertex_groups[wheel_name].add([v.index], 1.0, 'REPLACE')
        assigned[wheel_name] += 1
        remainder = 0.0

    if remainder > 0.0:
        # Ram and mantlet are mutually exclusive regions at opposite ends of the chassis, so
        # whichever claims the vertex takes its share of what the wheels left.
        ram_w = clamp01((RAM_X - co.x) / RAM_BLEND)
        mantlet_w = clamp01((co.z - MANTLET_Z_FLOOR) / MANTLET_BLEND) if co.x > 0.15 else 0.0
        part = min(1.0, ram_w + mantlet_w)
        if ram_w > 0.0:
            mesh.vertex_groups["Ram"].add([v.index], remainder * ram_w, 'REPLACE')
            assigned["Ram"] += 1
        if mantlet_w > 0.0:
            mesh.vertex_groups["Mantlet"].add([v.index], remainder * mantlet_w, 'REPLACE')
            assigned["Mantlet"] += 1
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

# The creature faces -X, so a wheel spins about world Y and the chassis rolls about world X.
SPIN_AXIS = Vector((0.0, 1.0, 0.0))
ROLL_AXIS = Vector((1.0, 0.0, 0.0))
PITCH_AXIS = Vector((0.0, 1.0, 0.0))
YAW_AXIS = Vector((0.0, 0.0, 1.0))

# Rolling without slip toward -X puts the contact point instantaneously at rest, which
# requires a NEGATIVE rotation about +Y (v = omega x r, with r pointing down from the hub).
# Derived rather than guessed, then confirmed by the measured contact-point direction the
# gait check reports.
SPIN_SIGN = -1.0


def world_axis_quaternion(pose_bone, world_axis, angle):
    """Rotate about a WORLD axis. A wheel bone points along world Y and a chassis bone along
    world X, so neither one's local frame is the frame the motion is described in."""
    rest = pose_bone.bone.matrix_local.to_3x3()
    return Quaternion((rest.inverted() @ Vector(world_axis)).normalized(), angle)


for frame in range(1, CLIP_FRAMES + 1, KEY_EVERY):
    clip_u = (frame - 1) / float(CLIP_FRAMES)
    wheel_u = clip_u * WHEEL_REVS_PER_CLIP

    for name in WHEELS:
        pb = arm.pose.bones[name]
        pb.rotation_quaternion = world_axis_quaternion(
            pb, SPIN_AXIS, SPIN_SIGN * 2.0 * math.pi * wheel_u)
        pb.keyframe_insert("rotation_quaternion", frame=frame)

    # Chassis. Bob and pitch ride the wheels; roll deliberately does not, so the three never
    # line up twice inside one clip and the silhouette never repeats a pose.
    bp = arm.pose.bones["Body"]
    rest = bp.bone.matrix_local.to_3x3()
    bp.location = rest.inverted() @ Vector((0.0, 0.0, BODY_BOB * math.sin(2.0 * math.pi * wheel_u)))
    pitch = math.radians(BODY_PITCH_DEG) * math.sin(2.0 * math.pi * wheel_u + math.pi * 0.5)
    roll = math.radians(BODY_ROLL_DEG) * math.sin(2.0 * math.pi * clip_u)
    bp.rotation_quaternion = (world_axis_quaternion(bp, PITCH_AXIS, pitch)
                              @ world_axis_quaternion(bp, ROLL_AXIS, roll))
    bp.keyframe_insert("location", frame=frame)
    bp.keyframe_insert("rotation_quaternion", frame=frame)

    # Mantlet scan. Yaw projects almost entirely onto the screen's horizontal axis from the
    # match camera, which is the axis a creep walking straight at the camera otherwise leaves
    # completely unused — the same finding that put a turret scan on the walker.
    mp = arm.pose.bones["Mantlet"]
    mp.rotation_quaternion = (
        world_axis_quaternion(mp, YAW_AXIS, math.radians(MANTLET_YAW_DEG)
                              * math.sin(2.0 * math.pi * clip_u + math.pi * 0.5))
        @ world_axis_quaternion(mp, PITCH_AXIS, math.radians(MANTLET_PITCH_DEG)
                                * math.sin(4.0 * math.pi * clip_u)))
    mp.keyframe_insert("rotation_quaternion", frame=frame)

    # Ram head working in its housing, on the wheel beat.
    rp = arm.pose.bones["Ram"]
    rest_ram = rp.bone.matrix_local.to_3x3()
    thrust = RAM_THRUST * math.sin(2.0 * math.pi * clip_u * RAM_THRUSTS_PER_CLIP)
    rp.location = rest_ram.inverted() @ Vector((-thrust, 0.0, 0.0))
    rp.keyframe_insert("location", frame=frame)

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


# LINEAR, not BEZIER. The wheels turn at a constant rate by construction and Bezier easing
# between evenly spaced keys would make a constant-speed wheel visibly surge, which is the
# one artefact this rig exists to avoid.
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
