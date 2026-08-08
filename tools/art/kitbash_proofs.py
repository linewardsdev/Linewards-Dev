"""Kitbash proof-of-quality: two expansion units assembled from owned Meshy meshes.

Run headless:  /Applications/Blender.app/Contents/MacOS/Blender --background --python tools/art/kitbash_proofs.py

ROSTER_EXPANSION_PLAN.md routes 12 of the 18 new units through kitbashing — recombining
the 30 meshes the project already owns — on the claim that style survives by construction
because the parts, textures and silhouette language are literally the originals'. This
script is that claim's test: it builds two of those units and renders each BESIDE its
parents in one scene, one light rig, one frame, so the judgement is apples to apples
rather than against memory.

The two chosen are the plan's easiest and its hardest kitbash:

- **Twin Crescent Ward** (A6): the arrow tower with a second crescent head, counter-posed.
  Easiest because the split base/head FBX already exists (arrow_split_base_head_0727) —
  the parts were separated for exactly this kind of reuse.
- **Shard Runner** (C7): the runner with swarm crystal shards socketed along its spine.
  Hardest because it crosses two meshes with two texture sets, which is where a kitbash
  would first read as a collage rather than a unit.

Exports both as prepared-convention FBX into AIStaging (same normalization contract as
the prep scripts: Z-up on the floor, height-capped), so a pass verdict means they can
enter the standard pipeline unchanged.
"""

import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parents[2] if "__file__" in globals() else Path.cwd()
if not (REPO / "unity").exists():  # --python runs with cwd at invocation dir
    REPO = Path.cwd()
STAGING = REPO / "unity/LTW.UnityClient/Assets/Art/AIStaging/Models"
OUT_DIR = REPO / "docs/screenshot-reviews/roster-expansion-proofs-20260806"

ARROW_SPLIT = STAGING / "Towers/Arrow/AIDrop/arrow_split_base_head_0727.fbx"
RUNNER = STAGING / "Creeps/Runner/AIDrop/runner_meshy_blade_claw_blend_0725021347_prepared.fbx"
SWARM = STAGING / "Creeps/Swarm/AIDrop/swarm_meshy_crystal_swarm_blend_0725021324_prepared.fbx"


def clean_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path: Path) -> list:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return [o for o in bpy.data.objects if o not in before]


def meshes(objects) -> list:
    return [o for o in objects if o.type == "MESH"]


def join(objects, name: str):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    return joined


def bounds(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def seat(obj, x: float, y: float = 0.0):
    """Feet on the floor at (x, y) — the prep scripts' normalization contract."""
    bpy.context.view_layer.update()
    lo, hi = bounds(obj)
    obj.location.x += x - (lo.x + hi.x) / 2
    obj.location.y += y - (lo.y + hi.y) / 2
    obj.location.z -= lo.z
    bpy.context.view_layer.update()


# --------------------------------------------------------------------- the two units

def build_twin_crescent() -> object:
    """Arrow tower, second crescent head counter-posed above the first.

    The two heads share one texture set, so this can only ever match the parent — the
    design work is purely placement: the upper head is rotated 180° and raised so both
    silhouettes read at game size, and scaled 88% so the pair tapers the way the plinth
    already does."""
    objs = import_fbx(ARROW_SPLIT)
    parts = meshes(objs)
    # The split file carries base + head as separate meshes; tallest-bounds part is the head.
    parts.sort(key=lambda o: bounds(o)[0].z)
    base, head = parts[0], parts[-1]

    second = head.copy()
    second.data = head.data.copy()
    bpy.context.scene.collection.objects.link(second)
    lo, hi = bounds(head)
    second.rotation_euler.z += math.pi
    second.scale *= 0.88
    second.location.z += (hi.z - lo.z) * 0.55

    unit = join([base, head, second], "LTW_TwinCrescent_Kitbash")
    return unit


def build_shard_runner() -> object:
    """Runner with three swarm crystal shards socketed along the spine, tapering back.

    The cross-mesh case. The shards keep the swarm's own material and texture — no
    retexturing, or the style-by-construction claim would be quietly false. Placement digs
    each shard slightly into the back so they read as grown, not glued."""
    runner_objs = import_fbx(RUNNER)
    runner = join(meshes(runner_objs), "runner_body")

    swarm_objs = import_fbx(SWARM)
    swarm = join(meshes(swarm_objs), "swarm_source")

    r_lo, r_hi = bounds(runner)
    spine_z = r_hi.z * 0.82
    length = r_hi.y - r_lo.y

    shards = []
    # First attempt used scales 0.34/0.27/0.20 and the shards swallowed the runner - the
    # render read as a shard cluster with legs, failing the sibling brief. Halved and sunk
    # deeper so the parent silhouette stays dominant and the shards read as growths.
    for i, (back_frac, scale) in enumerate([(0.34, 0.17), (0.50, 0.13), (0.64, 0.10)]):
        shard = swarm.copy()
        shard.data = swarm.data.copy()
        bpy.context.scene.collection.objects.link(shard)
        shard.scale = (scale, scale, scale)
        bpy.context.view_layer.update()
        s_lo, s_hi = bounds(shard)
        shard.location = Vector((
            0.0,
            r_lo.y + length * back_frac - (s_lo.y + s_hi.y) / 2,
            spine_z - (s_hi.z - s_lo.z) * 0.45 - s_lo.z,
        ))
        shard.rotation_euler = (math.radians(-14 + 6 * i), 0, math.radians(20 * (i - 1)))
        shards.append(shard)

    bpy.data.objects.remove(swarm, do_unlink=True)
    unit = join([runner] + shards, "LTW_ShardRunner_Kitbash")
    return unit


# --------------------------------------------------------------------- stage & render

def stage_and_render():
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"  # 5.x name; Eevee Next dropped the suffix
    scn.render.resolution_x = 1600
    scn.render.resolution_y = 900
    scn.view_settings.view_transform = "AgX"
    scn.view_settings.look = "AgX - Medium High Contrast"

    # Ground + three-point rig, matching the meshy-asset-review scenes so these renders
    # are comparable with that folder's captures as well as internally.
    bpy.ops.mesh.primitive_plane_add(size=40)
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("board")
    gm.use_nodes = True
    bsdf = gm.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.045, 0.055, 0.075, 1)
    bsdf.inputs["Roughness"].default_value = 0.85
    ground.data.materials.append(gm)

    def light(name, energy, loc, rot, size, color=(1, 1, 1)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size
        data.color = color
        obj = bpy.data.objects.new(name, data)
        bpy.context.scene.collection.objects.link(obj)
        obj.location = loc
        obj.rotation_euler = rot

    light("Key", 900, (3.2, -3.4, 5.0), (math.radians(40), math.radians(8), math.radians(40)), 6, (1.0, 0.96, 0.90))
    light("Fill", 220, (-4.2, -2.6, 2.2), (math.radians(70), 0, math.radians(-55)), 8, (0.62, 0.70, 0.95))
    light("Rim", 500, (-1.6, 4.4, 3.0), (math.radians(105), 0, math.radians(195)), 5, (0.55, 0.80, 1.0))

    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    scn.camera = cam
    cam.data.type = "ORTHO"
    tilt = 28.0
    cam.rotation_euler = (math.radians(90 - tilt), 0, 0)
    cam.location = (0, -10 * math.cos(math.radians(tilt)), 10 * math.sin(math.radians(tilt)) + 0.5)

    return cam


def render(path: Path, ortho_scale: float, res=(1600, 900)):
    scn = bpy.context.scene
    scn.camera.data.ortho_scale = ortho_scale
    scn.render.resolution_x, scn.render.resolution_y = res
    scn.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    print(f"rendered {path.name}")


def export_prepared(obj, path: Path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, path_mode="COPY", embed_textures=False)
    print(f"exported {path}")


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    clean_scene()

    # Parents for the side-by-side: untouched imports of the same source files.
    arrow_parent = join(meshes(import_fbx(ARROW_SPLIT)), "parent_arrow")
    runner_parent = join(meshes(import_fbx(RUNNER)), "parent_runner")
    swarm_parent = join(meshes(import_fbx(SWARM)), "parent_swarm")

    twin = build_twin_crescent()
    shard = build_shard_runner()

    # Line-up: parents left, kitbashes right, gap in the middle.
    seat(arrow_parent, -4.4)
    seat(swarm_parent, -2.9)
    seat(runner_parent, -1.6)
    seat(twin, 1.8)
    seat(shard, 4.0)

    cam = stage_and_render()

    render(OUT_DIR / "lineup_parents_vs_kitbash.png", ortho_scale=11.0)

    # Close pair shots for detail judgement.
    cam.location.x = 3.0
    render(OUT_DIR / "kitbash_pair_close.png", ortho_scale=5.2)

    # Game-size strip: the proof that matters. ~100px tall units, the top of the real range.
    cam.location.x = 0
    render(OUT_DIR / "lineup_game_size.png", ortho_scale=22.0, res=(1100, 300))

    staging_out = STAGING / "Towers/Arrow/AIDrop"
    export_prepared(twin, staging_out / "twincrescent_kitbash_arrow_v01_prepared.fbx")
    export_prepared(shard, STAGING / "Creeps/Runner/AIDrop/shardrunner_kitbash_runner_swarm_v01_prepared.fbx")

    print("kitbash proofs complete")


main()
