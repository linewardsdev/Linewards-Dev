"""Measure per-axis foot travel across a rigged creep's walk cycle.

This exists because a walk cycle cannot be validated by looking at it. The Rock
Golem rig originally shipped a convincing-looking clip whose feet were actually
swinging SIDEWAYS — a lateral waddle rather than a fore-aft step — caused by
Blender's degenerate bone-roll case for a bone pointing straight down world Z.
Renders did not make that obvious; measuring the axis split did, immediately.

A correct fore-aft walk moves each foot mostly along Y (the facing axis) and
almost not at all along X. A waddle is the reverse. This reports both and gives
a verdict, so the failure mode is caught mechanically rather than by eye.

    blender --background --python tools/art_pipeline/measure_creep_gait.py -- \
        <rigged.fbx> [frame_count=25]

Foot position is the SHIN bone's tail (legs are two-segment: Thigh + Shin), which
is the actual ground-contact end of the limb.
"""
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = argv[0]
FRAMES = int(argv[1]) if len(argv) > 1 else 25

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=SRC)

armatures = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
if not armatures:
    print("GAIT_FAILED: no armature in file")
    sys.exit(1)
arm = armatures[0]
scene = bpy.context.scene

legs = ["LegFL", "LegFR", "LegBL", "LegBR"]
# Two-segment legs put the foot at the shin's tail; fall back to a single bone
# for rigs that predate the thigh/shin split.
resolved = {}
for name in legs:
    for candidate in (f"{name}_Shin", name):
        if arm.pose.bones.get(candidate):
            resolved[name] = candidate
            break

if len(resolved) != len(legs):
    print(f"GAIT_FAILED: could not resolve foot bones, found {sorted(resolved)}")
    sys.exit(1)

track = {n: [] for n in legs}
for f in range(1, FRAMES + 1):
    scene.frame_set(f)
    bpy.context.view_layer.update()
    for name, bone_name in resolved.items():
        pb = arm.pose.bones[bone_name]
        track[name].append((arm.matrix_world @ pb.tail).copy())

ok = True
for name in legs:
    pts = track[name]
    rx = max(p.x for p in pts) - min(p.x for p in pts)
    ry = max(p.y for p in pts) - min(p.y for p in pts)
    rz = max(p.z for p in pts) - min(p.z for p in pts)
    # Fore-aft travel must dominate lateral travel, and the foot must actually
    # move at all (a rig that never lifts is not a walk).
    forward_dominant = ry > rx * 3.0
    moves = ry > 0.01
    verdict = "OK" if (forward_dominant and moves) else "BAD"
    if verdict == "BAD":
        ok = False
    print(f"GAIT {name}: travelX={rx:.4f} travelY={ry:.4f} travelZ={rz:.4f}  {verdict}")

print("GAIT_RESULT=PASS" if ok else "GAIT_RESULT=FAIL")
sys.exit(0 if ok else 1)
