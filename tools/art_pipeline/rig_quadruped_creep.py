"""Rig a prepared quadruped creep mesh and author a lumbering walk cycle, then
export an FBX carrying the rig + clip.

The Meshy source models ship completely unrigged (confirmed by inspecting every
raw .blend: static mesh, zero armatures, zero actions), so skeletal animation has
to be added here rather than requested at generation time.

Run against the PREPARED fbx, not the raw drop, so the existing Unity import spec
values (runtime scale, import yaw, seat-on-ground) stay valid.

Usage:
    blender --background --python tools/art_pipeline/rig_quadruped_creep.py -- \\
        <prepared.fbx> <out_rigged.fbx> [swing_axis=X] [render_prefix]

Bone placement is driven by measured vertex clusters, not eyeballed: the bottom
band of the mesh is split into quadrants and each cluster centre becomes a hip.
Weights are assigned explicitly by region rather than with Blender's automatic
heat-map weighting, because auto weights let the leg bones bend the rock shell,
which reads as rubber on a hard-surface creature.

Verified on: Brute / Rock Golem (7682 verts, four clean leg clusters).
"""

import bpy
import math
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC_FBX = argv[0]
OUT_FBX = argv[1]
SWING_AXIS = argv[2] if len(argv) > 2 else "X"   # which local axis swings the leg
RENDER_PREFIX = argv[3] if len(argv) > 3 else ""

# Measured from analyze_prepared.py (world space, Blender Z-up)
LEG_FL = Vector((-0.218, -0.215, 0.0))
LEG_FR = Vector((0.223, -0.213, 0.0))
LEG_BL = Vector((-0.218, 0.172, 0.0))
LEG_BR = Vector((0.219, 0.172, 0.0))
HIP_Z = 0.20
FOOT_Z = 0.02
BODY_Z = 0.22

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=SRC_FBX)

mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
print(f"IMPORTED mesh={mesh.name} verts={len(mesh.data.vertices)}")

# ---- Build armature -------------------------------------------------------
arm_data = bpy.data.armatures.new("BruteArmature")
arm = bpy.data.objects.new("BruteArmature", arm_data)
bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')

eb = arm_data.edit_bones


def bone(name, head, tail, parent=None):
    b = eb.new(name)
    b.head = head
    b.tail = tail
    if parent:
        b.parent = parent
        b.use_connect = False
    return b


root = bone("Root", Vector((0, 0, 0)), Vector((0, 0, 0.08)))
# Body runs rear -> front so its local +Y points at the head end (-Y world)
body = bone("Body", Vector((0, 0.16, BODY_Z)), Vector((0, -0.16, BODY_Z)), root)
head = bone("Head", Vector((0, -0.16, BODY_Z)), Vector((0, -0.40, BODY_Z + 0.02)), body)

legs = {}
for name, pos in (("LegFL", LEG_FL), ("LegFR", LEG_FR), ("LegBL", LEG_BL), ("LegBR", LEG_BR)):
    legs[name] = bone(name,
                      Vector((pos.x, pos.y, HIP_Z)),
                      Vector((pos.x, pos.y, FOOT_Z)),
                      body)

bpy.ops.object.mode_set(mode='OBJECT')

# ---- Skin mesh to armature ------------------------------------------------
# Explicit region-based weights, NOT automatic/heat-map. Auto weights let the leg
# bones bend the rock shell, which reads as rubber. This creature is hard-surface:
# the shell must stay rigid on Body, and only the stubby leg geometry follows the
# leg bones, with a short blend at the hip so the joint doesn't visibly tear.
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_NAME')   # creates groups, no weights

for name in ("Root", "Body", "Head", "LegFL", "LegFR", "LegBL", "LegBR"):
    if name not in mesh.vertex_groups:
        mesh.vertex_groups.new(name=name)

LEG_RADIUS = 0.145      # legs are ~0.44 apart in x, so this stays clear of overlap
RADIAL_BLEND = 0.055
HEIGHT_BLEND = 0.075

leg_pos = {"LegFL": LEG_FL, "LegFR": LEG_FR, "LegBL": LEG_BL, "LegBR": LEG_BR}


def clamp01(t):
    return max(0.0, min(1.0, t))


mw = mesh.matrix_world
assigned = {k: 0 for k in list(leg_pos) + ["Body"]}
for v in mesh.data.vertices:
    co = mw @ v.co
    best_name, best_w = None, 0.0
    for name, pos in leg_pos.items():
        d = math.hypot(co.x - pos.x, co.y - pos.y)
        t_radial = clamp01((LEG_RADIUS - d) / RADIAL_BLEND)
        t_height = clamp01((HIP_Z - co.z) / HEIGHT_BLEND)
        w = t_radial * t_height
        if w > best_w:
            best_name, best_w = name, w
    if best_name and best_w > 0.0:
        mesh.vertex_groups[best_name].add([v.index], best_w, 'REPLACE')
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
scene.frame_end = 24
scene.render.fps = 24

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')

for pb in arm.pose.bones:
    pb.rotation_mode = 'XYZ'

SWING = math.radians(22.0)     # leg swing amplitude, lumbering
BODY_BOB = 0.022
BODY_ROCK = math.radians(3.5)
HEAD_BOB = math.radians(5.0)

axis_index = {"X": 0, "Y": 1, "Z": 2}[SWING_AXIS]


def set_leg(pb, amount, frame):
    rot = [0.0, 0.0, 0.0]
    rot[axis_index] = amount
    pb.rotation_euler = rot
    pb.keyframe_insert("rotation_euler", frame=frame)


def set_body(z_off, rock, frame):
    arm.pose.bones["Body"].location = (0.0, 0.0, z_off)
    arm.pose.bones["Body"].rotation_euler = (rock, 0.0, 0.0)
    arm.pose.bones["Body"].keyframe_insert("location", frame=frame)
    arm.pose.bones["Body"].keyframe_insert("rotation_euler", frame=frame)


def set_head(pitch, frame):
    arm.pose.bones["Head"].rotation_euler = (pitch, 0.0, 0.0)
    arm.pose.bones["Head"].keyframe_insert("rotation_euler", frame=frame)


# Diagonal pairs: A = FL+BR, B = FR+BL
pairA = ("LegFL", "LegBR")
pairB = ("LegFR", "LegBL")

# f1 contact, f7 pass, f13 contact (mirrored), f19 pass, f25 = f1
keys = [
    (1,  +SWING, -SWING, -BODY_BOB, +BODY_ROCK, +HEAD_BOB),
    (7,   0.0,    0.0,   +BODY_BOB, 0.0,        -HEAD_BOB * 0.5),
    (13, -SWING, +SWING, -BODY_BOB, -BODY_ROCK, +HEAD_BOB),
    (19,  0.0,    0.0,   +BODY_BOB, 0.0,        -HEAD_BOB * 0.5),
    (25, +SWING, -SWING, -BODY_BOB, +BODY_ROCK, +HEAD_BOB),
]

for frame, a, b, bob, rock, hp in keys:
    for n in pairA:
        set_leg(arm.pose.bones[n], a, frame)
    for n in pairB:
        set_leg(arm.pose.bones[n], b, frame)
    set_body(bob, rock, frame)
    set_head(hp, frame)

action = arm.animation_data.action
action.name = "Walk"
print(f"ACTION name={action.name} frames={action.frame_range}")

# Make it loop smoothly (Blender 4.4+ moved fcurves under action slots/layers)
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

# ---- Optional verification renders ----------------------------------------
if RENDER_PREFIX:
    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.05, 0.06, 1)

    for nm, loc, e in (("K", (1.4, -1.4, 1.4), 320), ("F", (-1.4, -1.0, 0.7), 120)):
        ld = bpy.data.lights.new(nm, type='AREA'); ld.energy = e; ld.size = 2.0
        lo = bpy.data.objects.new(nm, ld); lo.location = loc
        bpy.context.collection.objects.link(lo)
        lo.rotation_euler = (Vector((0, 0, 0.25)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()

    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 480
    scene.render.resolution_y = 480
    cd = bpy.data.cameras.new("C"); cd.type = 'ORTHO'; cd.ortho_scale = 1.05
    cam = bpy.data.objects.new("C", cd)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    # Low side-on view so the legs are not occluded by the shell
    target = Vector((0, 0, 0.10))
    cam.location = (1.8, -0.55, 0.16)
    cam.rotation_euler = (target - Vector(cam.location)).to_track_quat('-Z', 'Y').to_euler()

    for f in (1, 4, 7, 10, 13, 16, 19, 22):
        scene.frame_set(f)
        scene.render.filepath = f"{RENDER_PREFIX}_f{f:02d}.png"
        bpy.ops.render.render(write_still=True)
        print(f"RENDERED frame {f}")

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
