"""Split a fused tower mesh into two rigid, independently-transformable objects.

Generalized from the Control ring split: separates one named "feature" part
(a turret head, a spinning dish, a spire) from the rest of the model ("Base"),
using either a Z-height threshold or an XY-radius threshold as the split rule.
Neither skinning nor an armature is used — both halves stay perfectly rigid,
which is correct for these stone/metal/crystal towers.

Find the threshold first with a per-band vertex/radius scan (see
tools/art_pipeline/blender_audit_model.py or hand-roll one: import the FBX,
bucket vertices into N height bands, print count/rMin/rMax per band — a
sharp drop in count marks a "neck" for a Z split, a small-radius cluster that
holds across the model's full height marks a radius split). Then run:

    /Applications/Blender.app/Contents/MacOS/Blender --background --python \
      tools/art_pipeline/split_tower_rigid_part.py -- \
      <source.fbx> <output.fbx> <FeatureName> <z|radius> <threshold> <0|1>

The final arg is ABOVE_IS_FEATURE: for z mode, 1 means verts at/above the
threshold become the feature (e.g. a turret head sitting above its base); for
radius mode, 1 means verts INSIDE the radius become the feature (e.g. a
central spire surrounded by an outer base ring/shards). Use 0 to invert.

A third mode, "annulus", isolates a donut-shaped band (radius AND height both
bounded) rather than everything on one side of a single cut — for a decal-like
ring feature sitting on an otherwise-unsplittable mesh (e.g. a glowing ring
raised on a dome that has no clean neck or radius elsewhere):

    ... <source.fbx> <output.fbx> <FeatureName> annulus <rInner> <rOuter> <zLo> <zHi>
"""
import bpy, sys

argv = sys.argv[sys.argv.index("--")+1:]
SRC = argv[0]
OUT = argv[1]
FEATURE_NAME = argv[2]
MODE = argv[3]           # "x", "y", "z", "radius", or "annulus"

# Optional, for splitting a model that has ALREADY been split once: --from names which existing
# object to cut (default: the first mesh found), and --remainder names what the leftover is called
# (default "Base"). Splitting the Gatling's Head into Head + Barrel needs both — without them the
# script would grab the Base mesh and rename the leftover "Base", clobbering the earlier split.
# Move the feature's origin onto its own geometry centre. REQUIRED for anything that spins about
# its own axis and does not already sit on the model's centre line. The origin of a split part stays
# at the SOURCE model's origin, which for a ring or dish around the tower's vertical axis happens to
# already be on the spin axis — so this never mattered before. A gatling barrel is offset along the
# bore, so spinning it about its own long axis swept it around the tower in a wide ellipse instead of
# turning in place.
CENTER_ORIGIN = "--center-origin" in argv
SOURCE_OBJECT = argv[argv.index("--from") + 1] if "--from" in argv else None
REMAINDER_NAME = argv[argv.index("--remainder") + 1] if "--remainder" in argv else "Base"

if MODE == "annulus":
    R_INNER = float(argv[4])
    R_OUTER = float(argv[5])
    Z_LO = float(argv[6])
    Z_HI = float(argv[7])
else:
    THRESHOLD = float(argv[4])
    ABOVE_IS_FEATURE = argv[5] == "1"   # z mode: verts >= threshold are FEATURE_NAME; radius mode: verts < threshold are FEATURE_NAME

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=SRC)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
mesh = next((o for o in meshes if o.name == SOURCE_OBJECT), None) if SOURCE_OBJECT else meshes[0]
if mesh is None:
    print(f"SPLIT_FAILED: no mesh named {SOURCE_OBJECT}; have {[o.name for o in meshes]}")
    sys.exit(1)
print(f"SOURCE mesh={mesh.name} verts={len(mesh.data.vertices)}")

base = mesh
base.name = REMAINDER_NAME
feature = base.copy()
feature.data = base.data.copy()
feature.name = FEATURE_NAME
bpy.context.collection.objects.link(feature)

def vert_is_feature(world_co):
    # x/y/z are the same cut with a different axis. A barrel assembly runs horizontally off the
    # front of a turret head, so it separates on x where no height or radius threshold can isolate
    # it — a radius cut would take the ammo belt too, since the belt sits at a similar radius on
    # the opposite side.
    if MODE in ("x", "y", "z"):
        is_above = getattr(world_co, MODE) >= THRESHOLD
        return is_above if ABOVE_IS_FEATURE else not is_above
    elif MODE == "radius":
        r = (world_co.x ** 2 + world_co.y ** 2) ** 0.5
        is_inside = r < THRESHOLD
        return is_inside if ABOVE_IS_FEATURE else not is_inside
    else:
        r = (world_co.x ** 2 + world_co.y ** 2) ** 0.5
        return R_INNER <= r <= R_OUTER and Z_LO <= world_co.z <= Z_HI

def keep_only(obj, keep_predicate):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT'); bpy.ops.object.mode_set(mode='OBJECT')
    mw = obj.matrix_world
    for v in obj.data.vertices:
        v.select = not keep_predicate(mw @ v.co)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.delete(type='VERT')
    bpy.ops.object.mode_set(mode='OBJECT')

keep_only(base, lambda co: not vert_is_feature(co))
print(f"{REMAINDER_NAME.upper()} remaining verts={len(base.data.vertices)}")
keep_only(feature, vert_is_feature)
print(f"{FEATURE_NAME.upper()} remaining verts={len(feature.data.vertices)}")

if len(base.data.vertices) == 0 or len(feature.data.vertices) == 0:
    print("SPLIT_FAILED: one half is empty")
    sys.exit(1)

import mathutils
verts_world = [feature.matrix_world @ v.co for v in feature.data.vertices]
cx = sum(v.x for v in verts_world) / len(verts_world)
cy = sum(v.y for v in verts_world) / len(verts_world)
cz = sum(v.z for v in verts_world) / len(verts_world)
print(f"{FEATURE_NAME.upper()} centroid xyz=({cx:.4f}, {cy:.4f}, {cz:.4f})")

if CENTER_ORIGIN:
    # ORIGIN_GEOMETRY keeps the mesh exactly where it is in world space and moves only the pivot,
    # so the part still lines up with the rest of the model — it just now turns about itself.
    bpy.ops.object.select_all(action='DESELECT')
    feature.select_set(True)
    bpy.context.view_layer.objects.active = feature
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    print(f"{FEATURE_NAME.upper()} origin moved to {tuple(round(v, 4) for v in feature.location)}")

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=True,
    object_types={'MESH', 'EMPTY'},
    apply_unit_scale=True,
    bake_space_transform=False,
    add_leaf_bones=False,
    path_mode='COPY',
    embed_textures=False,
)
print(f"EXPORTED {OUT}")
