"""Measure ground-contact travel across a rigged creep's clip, and report skate.

Restored and generalised. The original (removed in ab0553f as dead code) read the tails of
four bones named LegFL..LegBR, which made it a quadruped tool: it could not answer anything
about the Siege's wheels, the Serpent's coil or the Runner's blades, none of which have legs.
This version measures the DEFORMED MESH instead of named bones, so it works on any rig, and
it still catches the failure the original was written for.

What "skate" means here
-----------------------
The definition is the one rig_turret_walker.py used when it reported 51x: the ratio of how
fast the creep actually crosses the board to how fast its ground-contact geometry implies it
should be crossing.

The clip carries no root motion — the simulation translates the creep — so in the exported
FBX the body does not move, and a vertex's per-frame displacement IS its velocity in the
body frame. For a gait that does not skate, the geometry touching the ground must travel
backward through the body at exactly the creep's ground speed, so that it is stationary in
world space while the body advances over it. Skate is the shortfall:

    skate = ground_speed / mean_contact_speed

It is evaluated at the CONTACT PATCH — the lowest few vertices of each part, re-selected
every frame — and not over a band of geometry near the ground. That distinction is the whole
correctness of the measurement, and it took two wrong answers to arrive at:

  * A band mixes parts. The Siege's plow nose hangs 0.030 LOWER than its tyres, so any band
    measured from the mesh floor averages four correctly rolling wheels together with several
    hundred static hull vertices. Reported 6.1x for a rig rolling at 0.97x.
  * A band mixes a part's surface with its interior. Narrowing to each group's own footprint
    fixed the first problem and exposed a second: a 6%-of-height band on a 0.098-radius wheel
    still spans +-29 degrees of rim, and a rim point only moves along the travel axis at the
    very bottom — at the sides it is moving vertically. Averaging |dx| around that arc
    under-reports by the mean of |cos| over the arc, which tends to 2/pi. Measured: 0.526
    against a true rim speed of 0.85, i.e. exactly the 0.637 that predicts. Reported 1.58x
    for the same 0.97x rig.

At the contact patch itself both problems vanish without a tuning parameter, because the
lowest point of a rolling wheel is moving exactly backward at exactly omega*r, the lowest
point of a planted foot is moving exactly backward at the stride rate, and the lowest point
of a hull that is just being carried along is not moving at all.

Why the report is broken out per vertex group
---------------------------------------------
Parts of one creep do different jobs, and a single figure across them describes none of them.
Each vertex is attributed to the group that owns most of it and each group gets its own
contact patch and its own line — the same shape the original script had when it printed a
line per leg. The headline is taken from the fastest-moving group, because that is the
geometry driving the creep, and every other group is printed beside it so that a creep whose
hull is being dragged along shows that rather than hiding it in an average.

It is also honest about creeps that have no gait at all. A creep that floats, or whose belly
never moves within its own body, produces a contact speed at or near zero and therefore an
unbounded ratio. That is the correct answer, not a failure of the measurement — it means
100% of the creep's travel is skate, which is true of every unrigged creep in the roster and
cannot be fixed by a clip.

The waddle check
----------------
Kept from the original, which existed because the Rock Golem shipped a convincing-looking
clip whose feet were swinging SIDEWAYS — Blender's degenerate bone-roll case for a bone
pointing straight down world Z. Renders did not make it obvious; the axis split did. Reported
per axis so the same failure is caught mechanically rather than by eye.

    blender --background --python tools/art_pipeline/measure_creep_gait.py -- \\
        <rigged.fbx> <travel_axis:x|y> <ground_speed> [patch_verts=4]

`ground_speed` is in the mesh's own units per second, so the caller converts once from world
units rather than this script guessing at CreepVisualLibrary scales.

`patch_verts` is how many of each group's lowest vertices count as its contact patch on any
given frame. It is a smoothing width, not a threshold — the answer is the same for 2 or 8,
because they are all within a degree or two of the same point on the same part. It exists
only because the identity of "the lowest vertex" flips between neighbours as a part turns,
and averaging a handful takes the step out of the result.
"""
import math
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = argv[0]
TRAVEL_AXIS = argv[1].lower()
GROUND_SPEED = float(argv[2])
PATCH_VERTS = int(argv[3]) if len(argv) > 3 else 4

if TRAVEL_AXIS not in ("x", "y"):
    raise SystemExit("travel axis must be x or y")
AXIS_INDEX = {"x": 0, "y": 1}[TRAVEL_AXIS]
LATERAL_INDEX = 1 - AXIS_INDEX

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=SRC)

scene = bpy.context.scene
meshes = [o for o in scene.objects if o.type == 'MESH']
armatures = [o for o in scene.objects if o.type == 'ARMATURE']
if not meshes or not armatures:
    print("GAIT_FAILED: need one mesh and one armature, found %d/%d" % (len(meshes), len(armatures)))
    sys.exit(1)
mesh = meshes[0]
arm = armatures[0]

action = arm.animation_data.action if arm.animation_data else None
if action is None:
    print("GAIT_FAILED: armature carries no action")
    sys.exit(1)
first, last = (int(round(v)) for v in action.frame_range)
frame_count = last - first + 1
fps = scene.render.fps
clip_seconds = frame_count / float(fps)
print("GAIT clip frames=%d..%d fps=%d seconds=%.4f" % (first, last, fps, clip_seconds))


def evaluated_points():
    """World-space vertices of the mesh with the armature modifier applied."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(depsgraph)
    data = evaluated.to_mesh()
    matrix = evaluated.matrix_world
    points = [matrix @ v.co.copy() for v in data.vertices]
    evaluated.to_mesh_clear()
    return points


scene.frame_set(first)
bpy.context.view_layer.update()
rest = evaluated_points()

# Attribute every vertex to the group that owns most of it. Weights are the rig's own
# statement about which part a vertex belongs to, so no threshold is invented here.
group_names = {g.index: g.name for g in mesh.vertex_groups}
dominant = {}
for i in range(len(rest)):
    best_name, best_weight = "(unweighted)", 0.0
    for element in mesh.data.vertices[i].groups:
        if element.weight > best_weight:
            best_name, best_weight = group_names.get(element.group, "?"), element.weight
    dominant[i] = best_name

by_group = {}
for i, name in dominant.items():
    by_group.setdefault(name, []).append(i)
for name in sorted(by_group):
    indices = by_group[name]
    print("GAIT %-14s verts=%5d z=[%.4f,%.4f]"
          % (name, len(indices), min(rest[i].z for i in indices), max(rest[i].z for i in indices)))

frames = []
for frame in range(first, last + 1):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    frames.append(evaluated_points())
# Close the loop: frame `last + 1` is frame `first` again, so the step across the loop point
# is measured rather than dropped. A cycle measured on frame_count-1 steps under-reports by
# one step, which on a 24-frame clip is 4%.
frames.append(frames[0])

dt = 1.0 / fps
steps = len(frames) - 1
speeds = {}
lateral_speeds = {}
for name, indices in by_group.items():
    patch_size = min(PATCH_VERTS, len(indices))
    travel = lateral = 0.0
    for index in range(steps):
        here, ahead = frames[index], frames[index + 1]
        # The contact patch is re-chosen every frame, because on anything that turns it is a
        # different piece of the part each time. Chosen on `here` and measured across the
        # step to `ahead`.
        for i in sorted(indices, key=lambda v: here[v].z)[:patch_size]:
            delta = ahead[i] - here[i]
            travel += abs(delta[AXIS_INDEX])
            lateral += abs(delta[LATERAL_INDEX])
    speeds[name] = (travel / patch_size / steps) / dt
    lateral_speeds[name] = (lateral / patch_size / steps) / dt

driver = max(speeds, key=lambda name: speeds[name])
driver_speed = speeds[driver]

for name in sorted(speeds, key=lambda n: -speeds[n]):
    print("GAIT %-14s contact patch speed=%.4f lateral=%.4f"
          % (name, speeds[name], lateral_speeds[name]))
print("GAIT driving group = %s at %.4f units/sec" % (driver, driver_speed))
print("GAIT implied travel/cycle = %.4f   actual travel/cycle = %.4f"
      % (driver_speed * clip_seconds, GROUND_SPEED * clip_seconds))

if driver_speed < 1e-5:
    print("GAIT_SKATE=undefined (no contact geometry moves in the body frame: no gait)")
else:
    print("GAIT_SKATE=%.4fx" % (GROUND_SPEED / driver_speed))
    # The waddle check. A gait that travels along the lane moves its contact geometry mostly
    # along the travel axis; the Rock Golem's original bug moved it mostly sideways.
    ratio = lateral_speeds[driver] / driver_speed
    print("GAIT lateral/travel = %.3f  %s" % (ratio, "OK" if ratio < 0.5 else "WADDLE"))

# Per-axis extremes for the driving group, closest to what the original script printed, so a
# rig can still be compared against the numbers recorded for the golems and the walker.
for label, index in (("X", 0), ("Y", 1), ("Z", 2)):
    span = max(max(f[i][index] for i in by_group[driver]) for f in frames) - \
           min(min(f[i][index] for i in by_group[driver]) for f in frames)
    print("GAIT %s span %s = %.4f" % (driver, label, span))
