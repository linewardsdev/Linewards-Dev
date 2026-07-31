"""Rig and animate the Spire Turret Walker.

Split out of rig_quadruped_creep.py, which is built around the two rock golems (its
armature is literally named BruteArmature) and whose 2-beat diagonal trot is wrong for
this creature for two measured reasons:

1. The gameplay camera is orthographic, tilted 30 degrees off vertical, and creeps walk
   TOWARD it, so the walker is viewed head-on. A bilaterally symmetric trot's two contact
   poses are mirror images, which from head-on look nearly identical, so the cycle
   collapses into far fewer distinct poses than it has frames. A 4-beat wave gait
   (FL, BR, FR, BL, each a quarter cycle apart) never has two legs in the same phase, so
   every frame differs.

2. Foot skate. Nothing syncs animator playback to movement speed, and this creep travels
   at SpeedPerSecond 2 -- which is cells per TICK, at 4 ticks/sec, so 8 world units/sec
   across a board where one grid cell is one unit. Against a measured 0.139-unit stride
   on a 1-second cycle, its feet were moving ~29x too slowly: it was being dragged, not
   walking. This script attacks that with a much longer stride and a much shorter leg
   cycle. It cannot fully close a 29x gap -- 8 units/sec is roughly six body lengths per
   second, which no legged gait reads as -- so the goal is to get the residual into a
   range that reads as "fast" rather than "broken", and to report the real number.

Leg cycle and clip length are deliberately different: the clip is 24 frames (1s) and the
legs run LEG_CYCLES_PER_CLIP full gaits inside it, while the turret scans exactly once.
That lets the legs be fast without making the turret scan frantic, and still loops
cleanly because both complete a whole number of cycles per clip.

Usage:
    blender --background --python tools/art_pipeline/rig_turret_walker.py -- \\
        <prepared.fbx> <out_rigged.fbx> [render_prefix]
"""
import math
import sys

import bpy
from mathutils import Quaternion, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC_FBX = argv[0]
OUT_FBX = argv[1]
RENDER_PREFIX = argv[2] if len(argv) > 2 else ""

# Measured geometry, carried over unchanged from rig_quadruped_creep.py's "turretwalker"
# profile: four thin legs on clean corners, and the vertical profile jumps from ~20 verts
# per band in the legs to 543+ at z=0.178 where the chassis starts.
LEG_FL = Vector((-0.336, -0.206, 0.0))
LEG_FR = Vector((0.337, -0.206, 0.0))
LEG_BL = Vector((-0.335, 0.416, 0.0))
LEG_BR = Vector((0.335, 0.415, 0.0))
HIP_Z, FOOT_Z, KNEE_Z, BODY_Z = 0.17, 0.01, 0.09, 0.30
BODY_HALF, HEAD_REACH, LEG_RADIUS = 0.22, 0.40, 0.20

# --- Gait tuning -----------------------------------------------------------
# Stride was 22 degrees, which measured 0.139 world units per step. Widened hard: this is
# the single biggest lever on skate, and a spindly mechanical walker can plausibly take a
# long reaching step where a squat golem cannot.
SWING_DEG = 38.0
KNEE_PEAK_DEG = 40.0      # mid-recovery: foot lifted and tucked
KNEE_STANCE_DEG = 5.0     # planted, slight give

CLIP_FRAMES = 24          # 1 second at 24fps
# Legs run four full gaits per clip; the turret still scans once. This is the second big
# skate lever after stride. The body advances one stride per LEG cycle (not per beat --
# all legs must agree on body displacement), so shortening the leg cycle is what buys
# ground speed. Measured: the old 22-degree/1.00s trot implied 0.157 units/sec against an
# actual 8.0, i.e. 51x skate. Stride 38 degrees plus a 0.25s leg cycle implies 0.965
# units/sec, or 8.3x -- a 6x improvement. Going further (n=6 gives 5.5x) starts to read as
# a blur at 24 footfalls/sec, which trades one artefact for another.
LEG_CYCLES_PER_CLIP = 4
KEY_EVERY = 1             # 6 keys per leg cycle at n=4; coarser visibly stepped

# Machine, not creature: no breathing scale pulse, minimal roll. But NOT the near-zero
# values the shared script used -- with the legs previously self-cancelling head-on there
# was nothing left to read at all.
BODY_BOB = 0.022          # vertical, at leg-step frequency
BODY_ROLL_DEG = 3.0       # side-to-side lean, once per leg cycle
CHASSIS_YAW_DEG = 2.5     # NEW: screen-horizontal. Measured travelX was ~2% of frame,
                          # i.e. the screen's horizontal axis was carrying almost no
                          # motion at all from this camera. Yaw is the cheapest way to
                          # put something there.
TURRET_YAW_DEG = 22.0     # NEW: the turret scans. Thematic, and yaw projects almost
                          # entirely onto screen-horizontal from the game camera.
TURRET_PITCH_DEG = 3.0

SWING = math.radians(SWING_DEG)
KNEE_PEAK = -math.radians(KNEE_PEAK_DEG)     # sign matches rig_quadruped_creep's BEND_SIGN
KNEE_STANCE = -math.radians(KNEE_STANCE_DEG)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=SRC_FBX)

mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
print(f"IMPORTED mesh={mesh.name} verts={len(mesh.data.vertices)}")

# ---- Build armature -------------------------------------------------------
arm_data = bpy.data.armatures.new("WalkerArmature")
arm = bpy.data.objects.new("WalkerArmature", arm_data)
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
body = bone("Body", Vector((0, BODY_HALF, BODY_Z)), Vector((0, -BODY_HALF, BODY_Z)), root)
head = bone("Head", Vector((0, -BODY_HALF, BODY_Z)), Vector((0, -HEAD_REACH, BODY_Z + 0.02)), body)

leg_pos = {"LegFL": LEG_FL, "LegFR": LEG_FR, "LegBL": LEG_BL, "LegBR": LEG_BR}
for name, pos in leg_pos.items():
    thigh = bone(f"{name}_Thigh", Vector((pos.x, pos.y, HIP_Z)), Vector((pos.x, pos.y, KNEE_Z)), body)
    bone(f"{name}_Shin", Vector((pos.x, pos.y, KNEE_Z)), Vector((pos.x, pos.y, FOOT_Z)), thigh)

bpy.ops.object.mode_set(mode='OBJECT')

# ---- Skin ----------------------------------------------------------------
# Same explicit region weighting as the golem script: automatic/heat-map weights let leg
# bones bend the chassis, which reads as rubber on a hard-surface machine.
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_NAME')

group_names = ["Root", "Body", "Head"] + \
    [f"{n}_Thigh" for n in leg_pos] + [f"{n}_Shin" for n in leg_pos]
for name in group_names:
    if name not in mesh.vertex_groups:
        mesh.vertex_groups.new(name=name)

RADIAL_BLEND, HEIGHT_BLEND, KNEE_BLEND = 0.055, 0.075, 0.035


def clamp01(t):
    return max(0.0, min(1.0, t))


mw = mesh.matrix_world
assigned = {k: 0 for k in list(leg_pos) + ["Body"]}
for v in mesh.data.vertices:
    co = mw @ v.co
    best_name, best_w = None, 0.0
    for name, pos in leg_pos.items():
        d = math.hypot(co.x - pos.x, co.y - pos.y)
        w = clamp01((LEG_RADIUS - d) / RADIAL_BLEND) * clamp01((HIP_Z - co.z) / HEIGHT_BLEND)
        if w > best_w:
            best_name, best_w = name, w
    if best_name and best_w > 0.0:
        knee_t = clamp01((co.z - (KNEE_Z - KNEE_BLEND)) / (2 * KNEE_BLEND))
        if best_w * knee_t > 0.0:
            mesh.vertex_groups[f"{best_name}_Thigh"].add([v.index], best_w * knee_t, 'REPLACE')
        if best_w * (1.0 - knee_t) > 0.0:
            mesh.vertex_groups[f"{best_name}_Shin"].add([v.index], best_w * (1.0 - knee_t), 'REPLACE')
        assigned[best_name] += 1
    if best_w < 1.0:
        mesh.vertex_groups["Body"].add([v.index], 1.0 - best_w, 'REPLACE')
        assigned["Body"] += 1

print(f"SKINNED explicit weights: {assigned}")
mod = mesh.modifiers.new("Armature", 'ARMATURE')
mod.object = arm
mesh.parent = arm

# ---- Author the walk cycle ------------------------------------------------
scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = CLIP_FRAMES
scene.render.fps = 24

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for pb in arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'

WALK_AXIS = Vector((1.0, 0.0, 0.0))   # creature faces -Y, so legs swing about world X
ROLL_AXIS = Vector((0.0, 1.0, 0.0))
YAW_AXIS = Vector((0.0, 0.0, 1.0))


def world_axis_quaternion(pose_bone, world_axis, angle):
    """Rotate about a WORLD axis. A bone pointing straight down is parallel to world Z,
    the degenerate case for Blender's roll, so local axes cannot be trusted here."""
    rest = pose_bone.bone.matrix_local.to_3x3()
    return Quaternion((rest.inverted() @ Vector(world_axis)).normalized(), angle)


# 4-beat wave gait. Standard lateral-sequence quadruped walk order, each a quarter cycle
# apart, so no two legs ever share a phase -- which is the whole point from a head-on
# camera, where mirrored pairs are indistinguishable.
PHASE = {"LegFL": 0.00, "LegBR": 0.25, "LegFR": 0.50, "LegBL": 0.75}


def leg_pose(u):
    """u = normalized phase in [0,1). Returns (thigh_swing, knee_bend).

    Thigh sweeps forward at u=0 and back at u=0.5. Recovery -- foot lifted, travelling
    back to front -- runs u in (0.5, 1.0), so the knee tuck peaks at u=0.75 and is flat
    through stance, where the foot should stay planted.
    """
    swing = SWING * math.cos(2.0 * math.pi * u)
    recovery = max(0.0, math.sin(2.0 * math.pi * (u - 0.5)))
    bend = KNEE_STANCE + (KNEE_PEAK - KNEE_STANCE) * recovery
    return swing, bend


for frame in range(1, CLIP_FRAMES + 1, KEY_EVERY):
    clip_u = (frame - 1) / CLIP_FRAMES          # 0..1 across the whole clip
    leg_u = (clip_u * LEG_CYCLES_PER_CLIP) % 1.0

    for name, phase in PHASE.items():
        swing, bend = leg_pose((leg_u - phase) % 1.0)
        tp = arm.pose.bones[f"{name}_Thigh"]
        tp.rotation_quaternion = world_axis_quaternion(tp, WALK_AXIS, swing)
        tp.keyframe_insert("rotation_quaternion", frame=frame)
        sp = arm.pose.bones[f"{name}_Shin"]
        sp.rotation_quaternion = world_axis_quaternion(sp, WALK_AXIS, bend)
        sp.keyframe_insert("rotation_quaternion", frame=frame)

    # Body: bob at leg-step frequency (4 steps per leg cycle), roll once per leg cycle,
    # and a slow yaw that is deliberately NOT locked to the legs so the chassis reads as
    # tracking rather than wobbling in sympathy.
    bp = arm.pose.bones["Body"]
    rest = bp.bone.matrix_local.to_3x3()
    bob = BODY_BOB * math.sin(2.0 * math.pi * leg_u * 4.0)
    bp.location = rest.inverted() @ Vector((0.0, 0.0, bob))
    roll = math.radians(BODY_ROLL_DEG) * math.sin(2.0 * math.pi * leg_u)
    yaw = math.radians(CHASSIS_YAW_DEG) * math.sin(2.0 * math.pi * clip_u)
    bp.rotation_quaternion = (world_axis_quaternion(bp, YAW_AXIS, yaw)
                              @ world_axis_quaternion(bp, ROLL_AXIS, roll))
    bp.keyframe_insert("location", frame=frame)
    bp.keyframe_insert("rotation_quaternion", frame=frame)

    # Turret: one full scan per clip, independent of the leg cycle. Yaw projects almost
    # entirely onto the screen's horizontal axis from the game camera, which the previous
    # animation left essentially unused.
    hp_bone = arm.pose.bones["Head"]
    t_yaw = math.radians(TURRET_YAW_DEG) * math.sin(2.0 * math.pi * clip_u)
    t_pitch = math.radians(TURRET_PITCH_DEG) * math.sin(4.0 * math.pi * clip_u)
    hp_bone.rotation_quaternion = (world_axis_quaternion(hp_bone, YAW_AXIS, t_yaw)
                                   @ world_axis_quaternion(hp_bone, WALK_AXIS, t_pitch))
    hp_bone.keyframe_insert("rotation_quaternion", frame=frame)

action = arm.animation_data.action
action.name = "Walk"
print(f"ACTION name={action.name} frames={action.frame_range}")


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
        kp.interpolation = 'BEZIER'

bpy.ops.object.mode_set(mode='OBJECT')

# ---- Verification renders from the ACTUAL game camera ---------------------
# OPEN_ITEMS.md's retired 2026-07-29 review, "leg rigs on shell-bodied creeps": render-check from the real camera, not an eye-level artist
# view, because that is where the Brute's legs turned out to be fully occluded.
if RENDER_PREFIX:
    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.05, 0.06, 1)
    for nm, loc, e in (("K", (1.4, -1.4, 1.8), 400), ("F", (-1.4, -1.2, 0.9), 150)):
        ld = bpy.data.lights.new(nm, type='AREA'); ld.energy = e; ld.size = 2.0
        lo = bpy.data.objects.new(nm, ld); lo.location = loc
        bpy.context.collection.objects.link(lo)
        lo.rotation_euler = (Vector((0, 0, 0.25)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()

    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    cd = bpy.data.cameras.new("C"); cd.type = 'ORTHO'; cd.ortho_scale = 1.15
    cam = bpy.data.objects.new("C", cd)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    target = Vector((0, 0, 0.18))
    r, tilt = 3.0, math.radians(30.0)
    for view_name, loc in (("gamecam", Vector((0.0, -r * math.sin(tilt), r * math.cos(tilt)))),
                           ("side30", Vector((r * math.sin(tilt), 0.0, r * math.cos(tilt))))):
        cam.location = target + loc
        cam.rotation_euler = (target - (target + loc)).to_track_quat('-Z', 'Y').to_euler()
        for f in range(1, CLIP_FRAMES + 1, 3):
            scene.frame_set(f)
            scene.render.filepath = f"{RENDER_PREFIX}_{view_name}_f{f:02d}.png"
            bpy.ops.render.render(write_still=True)
            print(f"RENDERED {view_name} frame {f}")

# ---- Export ---------------------------------------------------------------
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
print(f"EXPORTED {OUT_FBX}")
