"""Sibling wave: the four same-mesh kitbash units, built headless and judged beside parents.

Run:  /Applications/Blender.app/Contents/MacOS/Blender --background --python tools/art/kitbash_sibling_wave.py

ROSTER_EXPANSION_PLAN.md wave 2, restricted to what the proofs validated: whole-mesh
duplicate/scale/pose of a single parent. A probe first tried `separate(type='LOOSE')`
hoping to mine semantic parts out of the merged meshes, and the result closes that door:
Meshy meshes are either fragment soup (Gatling: 3,480 islands) or a single welded body
(Brute: 1) — there are no barrel-sized parts to find automatically. Anything needing a
region of one mesh moved onto another stays routed through interactive sessions.

Standalone rather than importing from kitbash_proofs.py, which is frozen as the evidence
behind the plan's route decisions and should not grow a second purpose.
"""

import math
from pathlib import Path

import bpy
from mathutils import Vector

REPO = Path("/Users/admin/LTW")
STAGING = REPO / "unity/LTW.UnityClient/Assets/Art/AIStaging/Models"
OUT_DIR = REPO / "docs/screenshot-reviews/roster-expansion-proofs-20260806"

SOURCES = {
    "gatling": STAGING / "Towers/Gatling/AIDrop",
    "brute": STAGING / "Creeps/Brute/AIDrop",
    "zephyr": STAGING / "Creeps/Zephyr/AIDrop",
    "turretwalker": STAGING / "Creeps/Turretwalker/AIDrop",
}


def source_fbx(role: str) -> Path:
    # Prefer the un-rigged prepared mesh; fall back to the rigged export when that is all a
    # role has (zephyr ships rigged-only). The armature is stripped on import either way —
    # kitbashes are silhouette work, and rigs are re-applied by the rig scripts
    # post-promotion per the plan's checklist.
    candidates = [p for p in SOURCES[role].glob("*prepared.fbx") if "rigged" not in p.name]
    if not candidates:
        candidates = list(SOURCES[role].glob("*.fbx"))
    return sorted(candidates)[0]


def import_unit(role: str, name: str):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(source_fbx(role)))
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    non_mesh = [o for o in bpy.data.objects if o not in before and o.type != "MESH"]
    for o in non_mesh:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in new:
        o.select_set(True)
    bpy.context.view_layer.objects.active = new[0]
    if len(new) > 1:
        bpy.ops.object.join()
    unit = bpy.context.view_layer.objects.active
    unit.name = name

    # The rigged-only exports skipped the prep normalization and may reference textures by
    # stale absolute paths — the zephyr imported 40x scene scale and shaded magenta. Relink
    # any missing image from the FBX's sibling *_Textures folder, and cap height to the
    # creep prep contract (0.75) when the import is clearly unnormalized.
    fbx = source_fbx(role)
    for image in bpy.data.images:
        if image.source == "FILE" and not image.has_data:
            for tex_dir in fbx.parent.glob("*_Textures"):
                candidate = tex_dir / Path(image.filepath).name
                # Basename first; failing that, map by role — the rigged exports reference
                # fbm-embedded names (texture_0.png) that never existed as files, while the
                # folder ships the baked set under semantic names.
                if not candidate.exists() and "basecolor" in image.name.lower():
                    candidate = tex_dir / "Baked_BaseColor.png"
                if candidate.exists():
                    image.filepath = str(candidate)
                    image.reload()
                    break
    lo, hi = bounds(unit)
    height = hi.z - lo.z
    if height > 1.5:
        unit.scale *= 0.75 / height
        bpy.context.view_layer.update()
    return unit


def duplicate(obj, name: str):
    copy = obj.copy()
    copy.data = obj.data.copy()
    bpy.context.scene.collection.objects.link(copy)
    copy.name = name
    return copy


def bounds(obj):
    bpy.context.view_layer.update()
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def join(objects, name: str):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    unit = bpy.context.view_layer.objects.active
    unit.name = name
    return unit


def seat(obj, x: float, y: float = 0.0):
    lo, hi = bounds(obj)
    obj.location.x += x - (lo.x + hi.x) / 2
    obj.location.y += y - (lo.y + hi.y) / 2
    obj.location.z -= lo.z
    bpy.context.view_layer.update()


# ----------------------------------------------------------------------- the four units

def build_flak_battery():
    """F6: two gatlings at 82%, toed slightly inward, footprints merged.

    A battery is a formation, not a machine — two whole guns sharing a mount reads as
    exactly the saturation-fire promise the mechanic makes."""
    left = import_unit("gatling", "flak_left")
    right = duplicate(left, "flak_right")
    for unit, side in ((left, -1), (right, 1)):
        unit.scale *= 0.82
        lo, hi = bounds(unit)
        width = hi.x - lo.x
        unit.location.x += side * width * 0.34
        unit.rotation_euler.z += math.radians(-9 * side)  # toe-in: guns converge downrange
    return join([left, right], "LTW_FlakBattery_Kitbash")


def build_bulk_brute():
    """C6: the brute at 128%, leaning into its walk.

    Pure stat body, pure scale-and-posture art. The lean is what stops it reading as the
    same model with a bigger number: mass carried forward reads as momentum."""
    unit = import_unit("brute", "LTW_BulkBrute_Kitbash")
    unit.scale *= 1.28
    unit.rotation_euler.x += math.radians(6)
    return unit


def build_twin_zephyr():
    """E6: two zephyrs in echelon — trailing wing 88%, offset back and up.

    The pair IS the unit: one entity in the sim, two bodies on screen, like a bird pair
    read as one contact. Echelon rather than side-by-side so the silhouette stays narrow
    on the lane."""
    lead = import_unit("zephyr", "zephyr_lead")
    wing = duplicate(lead, "zephyr_wing")
    lo, hi = bounds(lead)
    length = hi.y - lo.y
    height = hi.z - lo.z
    wing.scale *= 0.88
    wing.location += Vector((-(hi.x - lo.x) * 0.42, -length * 0.38, height * 0.22))
    return join([lead, wing], "LTW_TwinZephyr_Kitbash")


def build_forge_tick():
    """S6: the turret walker at 55%, squashed 12% — the chunky little cousin.

    Chaff must read as chaff: smaller AND squatter, because uniform shrink alone reads as
    'far away' rather than 'small creature' at a fixed camera."""
    unit = import_unit("turretwalker", "LTW_ForgeTick_Kitbash")
    unit.scale = Vector((0.55, 0.55, 0.48))
    return unit


# ----------------------------------------------------------------------- stage, render, export

def stage():
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_EEVEE"
    scn.view_settings.view_transform = "AgX"
    scn.view_settings.look = "AgX - Medium High Contrast"

    bpy.ops.mesh.primitive_plane_add(size=60)
    ground = bpy.context.active_object
    material = bpy.data.materials.new("board")
    material.use_nodes = True
    bsdf = material.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.045, 0.055, 0.075, 1)
    bsdf.inputs["Roughness"].default_value = 0.85
    ground.data.materials.append(material)

    def light(name, energy, loc, rot, size, color=(1, 1, 1)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = energy, size, color
        obj = bpy.data.objects.new(name, data)
        bpy.context.scene.collection.objects.link(obj)
        obj.location, obj.rotation_euler = loc, rot

    light("Key", 1100, (3.2, -3.4, 5.0), (math.radians(40), math.radians(8), math.radians(40)), 6, (1.0, 0.96, 0.90))
    light("Fill", 260, (-4.2, -2.6, 2.2), (math.radians(70), 0, math.radians(-55)), 8, (0.62, 0.70, 0.95))
    light("Rim", 600, (-1.6, 4.4, 3.0), (math.radians(105), 0, math.radians(195)), 5, (0.55, 0.80, 1.0))

    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    scn.camera = cam
    cam.data.type = "ORTHO"
    tilt = 28.0
    cam.rotation_euler = (math.radians(90 - tilt), 0, 0)
    cam.location = (0, -12 * math.cos(math.radians(tilt)), 12 * math.sin(math.radians(tilt)) + 0.5)
    return cam


def render(path: Path, ortho: float, res):
    scn = bpy.context.scene
    scn.camera.data.ortho_scale = ortho
    scn.render.resolution_x, scn.render.resolution_y = res
    scn.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    print(f"rendered {path.name}")


def export(obj, path: Path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, path_mode="COPY", embed_textures=False)
    print(f"exported {path.name}")


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)

    # Parent / sibling pairs, interleaved so every judgement is a neighbour comparison.
    pairs = [
        (import_unit("gatling", "parent_gatling"), build_flak_battery()),
        (import_unit("brute", "parent_brute"), build_bulk_brute()),
        (import_unit("zephyr", "parent_zephyr"), build_twin_zephyr()),
        (import_unit("turretwalker", "parent_walker"), build_forge_tick()),
    ]

    x = -8.5
    for parent, sibling in pairs:
        seat(parent, x)
        seat(sibling, x + 2.1)
        x += 4.8

    stage()
    render(OUT_DIR / "sibling_wave_pairs.png", ortho=20.0, res=(1900, 800))
    render(OUT_DIR / "sibling_wave_game_size.png", ortho=34.0, res=(1300, 320))

    exports = {
        "LTW_FlakBattery_Kitbash": SOURCES["gatling"] / "flakbattery_kitbash_gatling_v01_prepared.fbx",
        "LTW_BulkBrute_Kitbash": SOURCES["brute"] / "bulkbrute_kitbash_brute_v01_prepared.fbx",
        "LTW_TwinZephyr_Kitbash": SOURCES["zephyr"] / "twinzephyr_kitbash_zephyr_v01_prepared.fbx",
        "LTW_ForgeTick_Kitbash": SOURCES["turretwalker"] / "forgetick_kitbash_turretwalker_v01_prepared.fbx",
    }
    for name, path in exports.items():
        export(bpy.data.objects[name], path)

    print("sibling wave complete")


main()
