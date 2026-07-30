"""Give each Meshy-rigged biped creep its own walk clip.

Why this exists
---------------
The five ELITE creeps (Zephyr Wraith, Umbral Stalker, Fracture Burrower, Aegis Warden,
Siege Colossus) arrived from Meshy as 24-bone humanoid rigs with stock clips already on
them, so no Blender step was written for them (commit a88dae1). That was true but
incomplete: the stock clips are STOCK, and they are SHARED. Read out of the five FBXs
directly --

    running      20f, 249 fcurves  ->  Zephyr, Stalker
    walking_man  32f, 249 fcurves  ->  Burrower, Warden, Colossus

-- with Warden and Colossus carrying byte-identical key counts. Two animations across five
creeps. A 12 hp wraith and a 90 hp colossus walked the same walk, and procedural layering
on the root transform cannot fix that, because what reads as "how this thing moves" is the
limbs, and the limbs were identical.

Why this TRANSFORMS the stock clip instead of authoring one
-----------------------------------------------------------
The first version of this script authored a cycle from the rest pose, the way
rig_quadruped_creep.py and rig_turret_walker.py do. It was verified by rendering and it
came out WORSE than stock, for two reasons that only showed up on screen:

1. These rigs rest in a T-pose. The stock clip is what brings the arms down, so authoring
   from rest produced a Colossus marching with a horizontal plank for an arm.
2. These are stylised creatures -- the Colossus is a squat hunched golem -- wearing a
   GENERIC humanoid skeleton, and their skin weights were solved against the vendor clip.
   Driving them through an idealised human stride pushed bones outside that range and the
   mesh deformed badly: stretched spindly legs, broken silhouette.

The two hand-authored rigs in this repo build their own armature AND their own weights, so
they own the whole chain and can author freely. Here the rig and weights are Meshy's and
are fine. So this transforms motion that already fits the model rather than replacing it.
Each creep still ends up with its own clip asset carrying its own curve data -- not one
shared clip played at a different speed.

What actually differs per creep
-------------------------------
    cycle      frames the loop takes (Colossus runs 2.4x longer than Zephyr)
    amp        how far every joint travels, as a slerp from rest toward the stock pose
    crouch     constant hip drop -- most of what separates a prowl from a march
    lean       constant forward pitch spread through the spine
    roll/sway  extra lateral weight shift layered on the stock curve

Usage:
    blender --background --python tools/art_pipeline/rig_biped_creep.py -- \
        <creep_key> <in_rigged.fbx> <out_rigged.fbx> [render_prefix]
"""

import bpy
import math
import sys
from mathutils import Vector, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
CREEP_KEY = argv[0]
SRC_FBX = argv[1]
OUT_FBX = argv[2]
RENDER_PREFIX = argv[3] if len(argv) > 3 else ""

# ---- Per-creep gait character ----------------------------------------------
# amp is a slerp factor from rest toward the stock pose: 1.0 is the vendor clip unchanged,
# below 1 is a tighter/stiffer version of the same motion, above 1 exaggerates it. Kept
# roughly within 0.75-1.30, because past that the skin weights start to complain -- which
# is exactly what sank the author-from-scratch version.
#
# crouch and sway are in units of the rig's own hip height, lean/roll/shoulder in degrees,
# so a value means the same thing on a 90 hp colossus as on a 12 hp wraith.
GAITS = {
    # Fast and loose. Widest joint travel of the five plus a real forward lean, so it reads
    # as covering ground rather than hurrying on the spot.
    "zephyr": dict(cycle=18, amp=1.26, crouch=0.000, lean=11.0, roll=3.0, sway=0.012, shoulder=5.0),
    # A prowl. Deep crouch and deliberately DAMPED limb travel -- something that swings its
    # arms as freely as it runs is not stalking.
    "stalker": dict(cycle=26, amp=0.82, crouch=0.085, lean=13.0, roll=2.0, sway=0.020, shoulder=2.0),
    # Hunched and wide. Heavy plod with a pronounced body roll.
    "burrower": dict(cycle=32, amp=0.96, crouch=0.060, lean=17.0, roll=7.0, sway=0.030, shoulder=8.0),
    # A shield march. The stillest of the five by design: tight travel, almost no lean,
    # minimal roll. Stillness is the characterisation, not an absence of one.
    "warden": dict(cycle=30, amp=0.78, crouch=0.010, lean=2.0, roll=1.5, sway=0.010, shoulder=1.5),
    # Ponderous. Longest cycle and the largest lateral weight shift on the board.
    "colossus": dict(cycle=44, amp=1.10, crouch=0.020, lean=5.0, roll=9.0, sway=0.042, shoulder=11.0),

    # --- Quadruped golems (11-bone BruteArmature, not the Meshy humanoid) --------------
    # These two shared rig_quadruped_creep.py's output verbatim: same 24-frame clip, same
    # 2420 keys, differing only in rest pose. Obsidian Brute is 2.5x the Brute's health and
    # moved identically. Brute keeps the tuned baseline; Obsidian Brute becomes the slower,
    # heavier read it should always have been.
    "brute": dict(cycle=24, amp=1.00, crouch=0.000, lean=0.0, roll=2.0, sway=0.010, shoulder=0.0),
    "obsidian_brute": dict(cycle=34, amp=1.14, crouch=0.030, lean=0.0, roll=6.5, sway=0.034, shoulder=0.0),
}

if CREEP_KEY not in GAITS:
    raise SystemExit("no gait defined for '%s'; known: %s" % (CREEP_KEY, sorted(GAITS)))
G = GAITS[CREEP_KEY]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC_FBX)

arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
if arm is None:
    raise SystemExit("no armature in source FBX")
bone_names = {b.name for b in arm.data.bones}
print("RIG %s bones=%d" % (arm.name, len(bone_names)))

# Two rig families run through here and neither is assumed: the Meshy 24-bone humanoid
# (Hips/Spine/LeftUpLeg/...) and the 11-bone quadruped BruteArmature the two rock golems
# share (Root/Body/Head/Leg{FL,FR,BL,BR}_{Thigh,Shin}). Everything resolves by looking for
# what is actually present, because the resample-and-rescale core is rig-agnostic — only
# the posture layer ever needs to know a bone by name.
ROOT_BONE = next((n for n in ("Hips", "Root") if n in bone_names), None)
if ROOT_BONE is None:
    ROOT_BONE = next((b.name for b in arm.data.bones if b.parent is None), None)
if ROOT_BONE is None:
    raise SystemExit("rig has no root bone")
SPINE = [n for n in ("Spine", "Spine01", "Spine02") if n in bone_names] or \
        [n for n in ("Body",) if n in bone_names]
SHOULDERS = [n for n in ("LeftShoulder", "RightShoulder") if n in bone_names]
print("RESOLVED root=%s spine=%s shoulders=%s" % (ROOT_BONE, SPINE, SHOULDERS))

action = arm.animation_data.action if arm.animation_data else None
if action is None:
    raise SystemExit("source FBX has no action to transform")


def fcurves_of(act):
    """Blender 5.x moved fcurves under layers/strips/channelbags (slotted actions)."""
    if hasattr(act, "fcurves") and len(getattr(act, "fcurves", [])):
        return list(act.fcurves)
    out = []
    for layer in getattr(act, "layers", []):
        for strip in getattr(layer, "strips", []):
            for cb in getattr(strip, "channelbags", []):
                out.extend(cb.fcurves)
    return out


src_fcurves = fcurves_of(action)
src_start, src_end = action.frame_range
src_len = max(1.0, src_end - src_start)
print("SOURCE action='%s' frames=%d-%d fcurves=%d" % (action.name, src_start, src_end, len(src_fcurves)))


def head_of(name):
    return (arm.matrix_world @ arm.data.bones[name].matrix_local).translation


hips_pos = head_of(ROOT_BONE)
_spine_top = next((n for n in ("Spine02", "Spine01", "Spine", "Body") if n in bone_names), None)
up = (head_of(_spine_top) - hips_pos) if _spine_top else Vector((0, 0, 1))
up = Vector((0, 0, 1)) if up.length < 1e-6 else up.normalized()

# Heel to toe, NOT hips to toes: in the A-pose these rigs ship in, the toes sit almost
# directly under the hips, so hips->toes is nearly vertical and what survives flattening is
# noise -- measured as a 31-degree error on the Colossus, which would tilt every correction.
if "LeftFoot" in bone_names and "RightFoot" in bone_names:
    toe_key = "LeftToeBase" if "LeftToeBase" in bone_names else "LeftFoot"
    foot_l, foot_r = head_of("LeftFoot"), head_of("RightFoot")
    toe_l, toe_r = head_of(toe_key), head_of(toe_key.replace("Left", "Right"))
    facing = ((toe_l - foot_l) + (toe_r - foot_r)) * 0.5
    ground = (toe_l + toe_r) * 0.5
elif "Head" in bone_names:
    # Quadruped fallback: the head is the front of the animal. There are no feet to measure
    # from, and the four leg roots are symmetric about the body so they say nothing.
    facing = head_of("Head") - hips_pos
    ground = hips_pos
else:
    facing = Vector((0, -1, 0))
    ground = hips_pos
facing = facing - up * facing.dot(up)
facing = Vector((0, -1, 0)) if facing.length < 1e-6 else facing.normalized()
lateral = up.cross(facing).normalized()
hip_height = max(1e-4, abs((hips_pos - ground).dot(up)))
if hip_height < 1e-3:
    _h = []
    for _ob in bpy.data.objects:
        if _ob.type == 'MESH':
            _h.extend([(_ob.matrix_world @ Vector(c)).dot(up) for c in _ob.bound_box])
    if _h:
        hip_height = max(1e-4, max(_h) - min(_h))
print("MEASURED up=%s facing=%s hip_height=%.4f" % (
    tuple(round(v, 3) for v in up), tuple(round(v, 3) for v in facing), hip_height))


def world_axis_quaternion(bone, world_axis, degrees):
    """Rotate about a WORLD axis converted into the bone's own rest space. Local axes are
    untrustworthy here: a leg bone points straight down, parallel to world up, which is the
    degenerate case for Blender's bone roll."""
    rest = bone.matrix_local.to_3x3()
    return Quaternion((rest.inverted() @ Vector(world_axis)).normalized(), math.radians(degrees))


# ---- Resample the source clip ----------------------------------------------
# Sampled per output frame rather than by editing key positions in place: the stock curves
# are densely and unevenly keyed, so retiming by scaling key x-positions would drift.
curves = {}
for fc in src_fcurves:
    curves[(fc.data_path, fc.array_index)] = fc
paths = sorted(set(dp for dp, _ in curves))
idx_by_path = {}
for dp in paths:
    idx_by_path[dp] = sorted(i for (d, i) in curves if d == dp)

CYCLE = G["cycle"]
samples = {}
for f_new in range(1, CYCLE + 1):
    src_t = src_start + ((f_new - 1) / float(CYCLE)) * src_len
    for dp in paths:
        samples[(dp, f_new)] = [curves[(dp, i)].evaluate(src_t) for i in idx_by_path[dp]]

# ---- Mean pose per bone -----------------------------------------------------
# Amplitude must scale a pose's deviation from the clip's OWN MEAN, not from the rest pose.
# Scaling from rest looks right for amp > 1 and is badly wrong for amp < 1, because these
# rigs rest in a T-POSE: damping toward rest raises the arms back out sideways. The Aegis
# Warden (amp 0.78) rendered marching with both arms held up like a scarecrow, which is
# what caught it. Damping toward the mean tightens the swing around the pose the creature
# actually carries, which is what "a stiffer version of this walk" should mean.
mean_rot = {}
for dp in paths:
    if not dp.endswith("rotation_quaternion"):
        continue
    acc = None
    for f_new in range(1, CYCLE + 1):
        v = samples[(dp, f_new)]
        if len(v) < 4:
            continue
        q = Quaternion((v[0], v[1], v[2], v[3]))
        if q.magnitude < 1e-6:
            continue
        q.normalize()
        if acc is None:
            acc = q.copy()
            continue
        # Sign-align before averaging: q and -q are the same rotation, and averaging across
        # a sign flip collapses toward zero.
        if q.dot(acc) < 0.0:
            q.negate()
        acc = acc.slerp(q, 1.0 / (f_new))
    if acc is not None:
        acc.normalize()
        mean_rot[dp] = acc

# ---- Rebuild the action, transformed ---------------------------------------
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for pb in arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'

# Preserve the source take name. Unity derives an AnimationClip's fileID from the FBX take
# name, and the animator controllers bind that id — renaming the take to something tidier
# would silently unbind every controller on re-import. The clip's CONTENT is what differs
# per creep; its name is load-bearing plumbing.
src_action_name = action.name
action.name = src_action_name + "_stock"
arm.animation_data.action = None
new_action = bpy.data.actions.new(src_action_name)
arm.animation_data.action = new_action
try:
    if len(new_action.slots) == 0:
        slot = new_action.slots.new(id_type='OBJECT', name="Object")
        arm.animation_data.action_slot = slot
except Exception:
    pass

scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = CYCLE
scene.render.fps = 24
IDENTITY = Quaternion((1, 0, 0, 0))

for f_new in range(1, CYCLE + 1):
    phase = (f_new - 1) / float(CYCLE) * math.tau

    for pb in arm.pose.bones:
        dp_rot = 'pose.bones["%s"].rotation_quaternion' % pb.name
        q = IDENTITY.copy()
        if (dp_rot, f_new) in samples:
            v = samples[(dp_rot, f_new)]
            if len(v) >= 4:
                cand = Quaternion((v[0], v[1], v[2], v[3]))
                if cand.magnitude > 1e-6:
                    cand.normalize()
                    mean = mean_rot.get(dp_rot)
                    if mean is None:
                        q = cand
                    else:
                        # Deviation of this frame from the clip's mean pose, with its angle
                        # scaled. Quaternion.slerp cannot do this on its own: it clamps its
                        # factor to [0,1], so it can damp but never exaggerate, and half
                        # these creeps want amp > 1. Scaling components directly would not
                        # stay a unit rotation, so it goes through axis-angle.
                        delta = mean.inverted() @ cand
                        if delta.w < 0.0:
                            delta.negate()
                        axis, angle = delta.to_axis_angle()
                        scaled = Quaternion(axis, angle * G["amp"]) if angle > 1e-6 else IDENTITY.copy()
                        q = mean @ scaled
                        q.normalize()
        pb.rotation_quaternion = q

        # Location and scale are copied VERBATIM, not amplitude-scaled. The stock clip
        # animates all three channels on all 24 bones (249 fcurves, not the 99 that
        # rotation alone would need), and an earlier version of this script wrote only
        # rotation -- leaving every other channel snapped to rest, which stretched the mesh
        # into spindly legs and a dislocated arm. Rotation is the channel that carries gait
        # character; translation here is rig plumbing, and exaggerating it pulls joints
        # apart.
        dp_loc_b = 'pose.bones["%s"].location' % pb.name
        if (dp_loc_b, f_new) in samples and pb.name != ROOT_BONE:
            lv = samples[(dp_loc_b, f_new)]
            if len(lv) >= 3:
                pb.location = Vector((lv[0], lv[1], lv[2]))
        dp_scale_b = 'pose.bones["%s"].scale' % pb.name
        if (dp_scale_b, f_new) in samples:
            sv2 = samples[(dp_scale_b, f_new)]
            if len(sv2) >= 3:
                pb.scale = Vector((sv2[0], sv2[1], sv2[2]))

    if SPINE:
        share = G["lean"] / len(SPINE)
        for nm in SPINE:
            pb = arm.pose.bones[nm]
            pb.rotation_quaternion = pb.rotation_quaternion @ world_axis_quaternion(pb.bone, lateral, share)
    for nm in SHOULDERS:
        sgn = 1.0 if nm.startswith("Left") else -1.0
        pb = arm.pose.bones[nm]
        pb.rotation_quaternion = pb.rotation_quaternion @ world_axis_quaternion(
            pb.bone, facing, sgn * G["shoulder"] * math.sin(phase))

    hips = arm.pose.bones[ROOT_BONE]
    hips.rotation_quaternion = hips.rotation_quaternion @ world_axis_quaternion(
        hips.bone, facing, G["roll"] * math.sin(phase))

    dp_loc = 'pose.bones["%s"].location' % ROOT_BONE
    base = Vector((0, 0, 0))
    if (dp_loc, f_new) in samples:
        sv = samples[(dp_loc, f_new)]
        base = Vector((sv + [0, 0, 0])[:3])
    rest_hips = hips.bone.matrix_local.to_3x3()
    extra_world = up * (-G["crouch"] * hip_height) + lateral * (G["sway"] * hip_height * math.sin(phase))
    hips.location = base + (rest_hips.inverted() @ extra_world)

    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=f_new)
        pb.keyframe_insert("location", frame=f_new)
        pb.keyframe_insert("scale", frame=f_new)

# Periodic by construction, so linear interpolation between evenly spaced samples is right
# and avoids Bezier overshoot at the loop point.
new_fcurves = fcurves_of(new_action)
for fc in new_fcurves:
    for kp in fc.keyframe_points:
        kp.interpolation = 'LINEAR'
print("AUTHORED %s cycle=%df amp=%.2f fcurves=%d" % (CREEP_KEY, CYCLE, G["amp"], len(new_fcurves)))

bpy.ops.object.mode_set(mode='OBJECT')

# ---- Verification renders from the ACTUAL game camera ----------------------
# Not optional: this check found the Brute's fully occluded legs, the Turret Walker's
# self-cancelling trot, and the broken from-scratch version of this very script.
if RENDER_PREFIX:
    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.05, 0.06, 1)
    for nm, loc, e in (("K", (1.4, -1.4, 1.8), 500), ("F", (-1.4, -1.2, 0.9), 200)):
        ld = bpy.data.lights.new(nm, type='AREA')
        ld.energy = e
        ld.size = 2.0
        lo = bpy.data.objects.new(nm, ld)
        lo.location = loc
        bpy.context.collection.objects.link(lo)
        lo.rotation_euler = (Vector((0, 0, 0.25)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()

    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 384
    scene.render.resolution_y = 384

    # Framed on world-space mesh bounds. Deriving framing from hip height cropped the
    # Colossus to its shins -- these models differ far too much in proportion for one bone
    # to predict the rest.
    pts = []
    for ob in bpy.data.objects:
        if ob.type == 'MESH':
            pts.extend([ob.matrix_world @ Vector(c) for c in ob.bound_box])
    lo_b = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi_b = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo_b + hi_b) * 0.5
    extent = max((hi_b - lo_b).x, (hi_b - lo_b).y, (hi_b - lo_b).z)
    cd = bpy.data.cameras.new("C")
    cd.type = 'ORTHO'
    cd.ortho_scale = extent * 1.35
    cam = bpy.data.objects.new("C", cd)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    r = extent * 4.0
    tilt = math.radians(30.0)
    views = (("gamecam", facing * (-r * math.sin(tilt)) + up * (r * math.cos(tilt))),
             ("side", lateral * (r * math.sin(tilt)) + up * (r * math.cos(tilt))))
    for view_name, loc in views:
        cam.location = center + loc
        cam.rotation_euler = (center - (center + loc)).to_track_quat('-Z', 'Y').to_euler()
        step = max(1, CYCLE // 6)
        for f in range(1, CYCLE + 1, step):
            scene.frame_set(f)
            scene.render.filepath = "%s_%s_f%02d.png" % (RENDER_PREFIX, view_name, f)
            bpy.ops.render.render(write_still=True)
    print("RENDERED verification frames")

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=OUT_FBX,
    use_selection=True,
    object_types={'MESH', 'ARMATURE'},
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
