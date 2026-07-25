#!/usr/bin/env python3
"""Build an authored Control tower source mesh in Blender.

This is a practical bridge when we have a strong 2D source plate but no external
FBX yet. It creates real Blender geometry inspired by the Control tower plate:
base disk, containment ring, central crystal, gold restraint arcs, field pylons,
and a translucent control dome.

Run with Blender:

    /Applications/Blender.app/Contents/MacOS/Blender --background \
      --python tools/art_pipeline/blender_build_control_tower_source.py -- \
      --output unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_source_v01.fbx
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []
    parser = argparse.ArgumentParser(description="Build Line Wards Control tower source FBX.")
    parser.add_argument("--output", required=True, help="Output FBX path.")
    parser.add_argument(
        "--variant",
        default="v02_segmented",
        choices=(
            "v01_baseline",
            "v02_segmented",
            "v03_source_dense",
            "v04_mobile_bold",
            "v05_gold_crown",
            "v06_faceted_crown",
            "v07_integrated_crown",
            "v08_ring_inlay",
            "v09_crystal_cage",
            "v10_premium_balanced",
            "v11_material_premium",
            "v12_sprite_fidelity",
            "v13_material_clean",
            "v14_fortress_ring",
            "v15_hero_crystal",
            "v16_front_identity",
            "v17_arcane_machine",
            "v18_boss_silhouette",
            "v19_sprite_arc_refit",
            "v20_crystal_base_refit",
            "v21_painted_depth_refit",
            "v22_elegant_gold_ribs",
            "v23_ring_gem_polish",
            "v24_sprite_scale_unify",
            "v25_painted_panel_cuts",
            "v26_gem_glow_focus",
            "v27_mobile_finish_trim",
            "v28_ring_wall_depth",
            "v29_crystal_refraction_lines",
            "v30_source_readability_lock",
            "v31_crown_material_focus",
            "v32_field_edge_cleanup",
            "v33_mobile_gold_read",
            "v34_visible_crown_gold",
            "v35_warm_gold_grade",
            "v36_source_panel_value",
            "v37_cyan_panel_read",
            "v38_blue_crystal_grade",
            "v39_clear_read",
        ),
        help="Art-directed refinement variant.",
    )
    return parser.parse_args(argv)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def mat(name: str, color: tuple[float, float, float, float], emission: float = 0.0, alpha: float | None = None) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    if alpha is not None or color[3] < 1.0:
        material.blend_method = "BLEND"
        material.use_screen_refraction = True
        material.show_transparent_back = True

    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = color
        if "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = alpha if alpha is not None else color[3]
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 0.48
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.0
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = color
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission
    return material


def make_materials(variant: str) -> dict[str, bpy.types.Material]:
    if variant in {
        "v04_mobile_bold",
        "v05_gold_crown",
        "v06_faceted_crown",
        "v07_integrated_crown",
        "v08_ring_inlay",
        "v09_crystal_cage",
        "v10_premium_balanced",
        "v11_material_premium",
        "v12_sprite_fidelity",
        "v13_material_clean",
        "v14_fortress_ring",
        "v15_hero_crystal",
        "v16_front_identity",
        "v17_arcane_machine",
        "v18_boss_silhouette",
        "v19_sprite_arc_refit",
        "v20_crystal_base_refit",
        "v21_painted_depth_refit",
        "v22_elegant_gold_ribs",
        "v23_ring_gem_polish",
        "v24_sprite_scale_unify",
        "v25_painted_panel_cuts",
        "v26_gem_glow_focus",
        "v27_mobile_finish_trim",
        "v28_ring_wall_depth",
        "v29_crystal_refraction_lines",
        "v30_source_readability_lock",
        "v31_crown_material_focus",
        "v32_field_edge_cleanup",
        "v33_mobile_gold_read",
        "v34_visible_crown_gold",
        "v35_warm_gold_grade",
        "v36_source_panel_value",
        "v37_cyan_panel_read",
        "v38_blue_crystal_grade",
        "v39_clear_read",
    }:
        body = (0.038, 0.052, 0.075, 1.0)
        body_lite = (0.10, 0.13, 0.18, 1.0)
        field_alpha = 0.10 if variant in {
            "v04_mobile_bold",
            "v10_premium_balanced",
            "v11_material_premium",
            "v12_sprite_fidelity",
            "v13_material_clean",
            "v14_fortress_ring",
            "v15_hero_crystal",
            "v16_front_identity",
            "v17_arcane_machine",
            "v18_boss_silhouette",
            "v19_sprite_arc_refit",
            "v20_crystal_base_refit",
            "v21_painted_depth_refit",
            "v22_elegant_gold_ribs",
            "v23_ring_gem_polish",
            "v24_sprite_scale_unify",
            "v25_painted_panel_cuts",
            "v26_gem_glow_focus",
            "v27_mobile_finish_trim",
            "v28_ring_wall_depth",
            "v29_crystal_refraction_lines",
            "v30_source_readability_lock",
            "v31_crown_material_focus",
            "v32_field_edge_cleanup",
            "v33_mobile_gold_read",
            "v34_visible_crown_gold",
            "v35_warm_gold_grade",
            "v36_source_panel_value",
            "v37_cyan_panel_read",
            "v38_blue_crystal_grade",
            "v39_clear_read",
        } else 0.12
        if variant in {"v27_mobile_finish_trim", "v28_ring_wall_depth", "v29_crystal_refraction_lines", "v30_source_readability_lock", "v31_crown_material_focus", "v32_field_edge_cleanup", "v33_mobile_gold_read", "v34_visible_crown_gold", "v35_warm_gold_grade", "v36_source_panel_value", "v37_cyan_panel_read", "v38_blue_crystal_grade"}:
            field_alpha = 0.075
        if variant == "v39_clear_read":
            field_alpha = 0.025
    elif variant == "v03_source_dense":
        body = (0.045, 0.062, 0.090, 1.0)
        body_lite = (0.115, 0.14, 0.20, 1.0)
        field_alpha = 0.16
    else:
        body = (0.055, 0.075, 0.105, 1.0)
        body_lite = (0.13, 0.16, 0.22, 1.0)
        field_alpha = 0.20

    if variant in {"v35_warm_gold_grade", "v36_source_panel_value", "v37_cyan_panel_read", "v38_blue_crystal_grade", "v39_clear_read"}:
        trim = (0.94, 0.48, 0.10, 1.0)
        trim_dark = (0.48, 0.22, 0.055, 1.0)
        trim_bright = (1.0, 0.68, 0.18, 1.0)
        trim_hot = (1.0, 0.78, 0.25, 1.0)
    else:
        trim = (0.98, 0.68, 0.20, 1.0)
        trim_dark = (0.52, 0.34, 0.10, 1.0)
        trim_bright = (1.0, 0.84, 0.36, 1.0)
        trim_hot = (1.0, 0.94, 0.52, 1.0)

    energy_white = (0.30, 0.94, 1.0, 1.0) if variant in {"v38_blue_crystal_grade", "v39_clear_read"} else (0.70, 1.0, 1.0, 1.0)

    return {
        "body": mat("ltw_control_body_dark_slate", body),
        "body_lite": mat("ltw_control_body_beveled_bluegrey", body_lite),
        "body_dark": mat("ltw_control_body_blackblue_shadow", (0.018, 0.025, 0.038, 1.0)),
        "body_edge": mat("ltw_control_body_cool_edge_highlight", (0.18, 0.22, 0.30, 1.0), emission=0.02),
        "trim": mat("ltw_control_trim_signal_gold", trim, emission=0.03),
        "trim_dark": mat("ltw_control_trim_aged_gold_shadow", trim_dark),
        "trim_bright": mat("ltw_control_trim_bright_edge_gold", trim_bright, emission=0.08),
        "trim_hot": mat("ltw_control_trim_hot_edge_gold", trim_hot, emission=0.14),
        "energy": mat("ltw_control_energy_cyan_core", (0.11, 0.82, 1.0, 1.0), emission=0.8),
        "energy_deep": mat("ltw_control_energy_deep_blue_core", (0.04, 0.12, 0.75, 1.0), emission=0.55),
        "energy_white": mat("ltw_control_energy_white_cyan_edge", energy_white, emission=1.1),
        "energy_blue": mat("ltw_control_energy_blue_field", (0.16, 0.24, 1.0, 0.72), emission=0.35, alpha=0.72),
        "energy_violet": mat("ltw_control_energy_violet_inner_field", (0.18, 0.08, 1.0, 0.72), emission=0.28, alpha=0.72),
        "field": mat("ltw_control_translucent_field_dome", (0.25, 0.62, 1.0, field_alpha), emission=0.08, alpha=field_alpha),
        "shadow": mat("ltw_control_contact_shadow", (0.02, 0.025, 0.035, 1.0)),
    }


def shade_smooth(obj: bpy.types.Object, bevel: float = 0.0) -> bpy.types.Object:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    try:
        bpy.ops.object.shade_smooth()
    except RuntimeError:
        pass
    obj.select_set(False)
    if bevel > 0.0:
        modifier = obj.modifiers.new("soft_mobile_bevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.affect = "EDGES"
        obj.modifiers.new("weighted_mobile_normals", "WEIGHTED_NORMAL")
    return obj


def cylinder(name: str, radius: float, depth: float, loc: tuple[float, float, float], material: bpy.types.Material, vertices: int = 64) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name}_Mesh"
    obj.data.materials.append(material)
    return shade_smooth(obj, bevel=0.015)


def torus(name: str, major: float, minor: float, loc: tuple[float, float, float], material: bpy.types.Material) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(major_segments=96, minor_segments=14, major_radius=major, minor_radius=minor, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name}_Mesh"
    obj.data.materials.append(material)
    return shade_smooth(obj)


def cube(name: str, loc: tuple[float, float, float], scale: tuple[float, float, float], material: bpy.types.Material, rot_z: float = 0.0) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=(0, 0, rot_z))
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name}_Mesh"
    obj.scale = scale
    obj.data.materials.append(material)
    return shade_smooth(obj, bevel=0.025)


def ring_block(
    name: str,
    angle_deg: float,
    radius: float,
    z: float,
    tangent_width: float,
    radial_depth: float,
    height: float,
    material: bpy.types.Material,
    radial_offset: float = 0.0,
) -> bpy.types.Object:
    theta = math.radians(angle_deg)
    loc_radius = radius + radial_offset
    x = loc_radius * math.cos(theta)
    y = loc_radius * math.sin(theta)
    # cube local X roughly radial, local Y tangent after rotation.
    return cube(name, (x, y, z), (radial_depth, tangent_width, height), material, rot_z=theta)


def crystal(name: str, loc: tuple[float, float, float], radius: float, height: float, material: bpy.types.Material) -> list[bpy.types.Object]:
    x, y, z = loc
    top_h = height * 0.42
    mid_h = height * 0.34
    bot_h = height * 0.24
    parts = []

    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=radius, radius2=radius * 0.82, depth=mid_h, location=(x, y, z))
    mid = bpy.context.object
    mid.name = f"{name}_FacetedBody"
    mid.data.name = f"{mid.name}_Mesh"
    mid.data.materials.append(material)
    parts.append(shade_smooth(mid))

    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=radius * 0.84, radius2=0.0, depth=top_h, location=(x, y, z + mid_h * 0.5 + top_h * 0.5))
    top = bpy.context.object
    top.name = f"{name}_TopPoint"
    top.data.name = f"{top.name}_Mesh"
    top.data.materials.append(material)
    parts.append(shade_smooth(top))

    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.0, radius2=radius * 0.78, depth=bot_h, location=(x, y, z - mid_h * 0.5 - bot_h * 0.5))
    bottom = bpy.context.object
    bottom.name = f"{name}_LowerPoint"
    bottom.data.name = f"{bottom.name}_Mesh"
    bottom.data.materials.append(material)
    parts.append(shade_smooth(bottom))
    return parts


def curve_arc(name: str, radius: float, z_min: float, z_max: float, angle_deg: float, material: bpy.types.Material, bevel: float = 0.025) -> bpy.types.Object:
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 18
    curve.bevel_depth = bevel
    curve.bevel_resolution = 4
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(3)

    theta = math.radians(angle_deg)
    points = [
        (radius * math.cos(theta - 0.22), radius * math.sin(theta - 0.22), z_min),
        (radius * 1.02 * math.cos(theta - 0.09), radius * 1.02 * math.sin(theta - 0.09), (z_min + z_max) * 0.52),
        (radius * 1.02 * math.cos(theta + 0.09), radius * 1.02 * math.sin(theta + 0.09), (z_min + z_max) * 0.52),
        (radius * math.cos(theta + 0.22), radius * math.sin(theta + 0.22), z_min),
    ]
    # Raise middle handles to form upward gold ribs.
    points[1] = (points[1][0], points[1][1], z_max)
    points[2] = (points[2][0], points[2][1], z_max)

    for point, co in zip(spline.bezier_points, points):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"

    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def make_gem(name: str, angle_deg: float, radius: float, z: float, mats: dict[str, bpy.types.Material]) -> None:
    theta = math.radians(angle_deg)
    x = radius * math.cos(theta)
    y = radius * math.sin(theta)
    cube(f"{name}_GoldSetting", (x, y, z), (0.075, 0.034, 0.045), mats["trim"], rot_z=theta)
    crystal(f"{name}_CyanGem", (x, y, z + 0.035), 0.035, 0.11, mats["energy"])


def add_segmented_ring_detail(mats: dict[str, bpy.types.Material], variant: str) -> None:
    compact_ring_variants = {
        "v04_mobile_bold",
        "v05_gold_crown",
        "v06_faceted_crown",
        "v07_integrated_crown",
        "v08_ring_inlay",
        "v09_crystal_cage",
        "v10_premium_balanced",
        "v14_fortress_ring",
        "v15_hero_crystal",
        "v16_front_identity",
        "v17_arcane_machine",
        "v18_boss_silhouette",
        "v19_sprite_arc_refit",
        "v20_crystal_base_refit",
        "v21_painted_depth_refit",
        "v22_elegant_gold_ribs",
        "v23_ring_gem_polish",
        "v24_sprite_scale_unify",
        "v25_painted_panel_cuts",
        "v26_gem_glow_focus",
        "v27_mobile_finish_trim",
        "v28_ring_wall_depth",
        "v29_crystal_refraction_lines",
        "v30_source_readability_lock",
        "v31_crown_material_focus",
        "v32_field_edge_cleanup",
        "v33_mobile_gold_read",
        "v34_visible_crown_gold",
    }
    segment_count = 12 if variant not in compact_ring_variants else 8
    for index in range(segment_count):
        angle = index * (360 / segment_count)
        ring_block(
            f"Control_TopRing_SlateArmorSegment_{index:02d}",
            angle,
            0.51,
            1.012,
            0.105 if segment_count == 12 else 0.135,
            0.115,
            0.025,
            mats["body_dark"],
        )
        if index % 2 == 0 or variant in {
            "v03_source_dense",
            "v05_gold_crown",
            "v06_faceted_crown",
            "v07_integrated_crown",
            "v08_ring_inlay",
            "v09_crystal_cage",
            "v10_premium_balanced",
            "v14_fortress_ring",
            "v15_hero_crystal",
            "v16_front_identity",
            "v17_arcane_machine",
            "v18_boss_silhouette",
            "v19_sprite_arc_refit",
            "v20_crystal_base_refit",
            "v21_painted_depth_refit",
            "v22_elegant_gold_ribs",
            "v23_ring_gem_polish",
            "v24_sprite_scale_unify",
            "v25_painted_panel_cuts",
            "v26_gem_glow_focus",
            "v27_mobile_finish_trim",
            "v28_ring_wall_depth",
            "v29_crystal_refraction_lines",
            "v30_source_readability_lock",
            "v31_crown_material_focus",
            "v32_field_edge_cleanup",
            "v33_mobile_gold_read",
            "v34_visible_crown_gold",
        }:
            ring_block(
                f"Control_TopRing_GoldCapSegment_{index:02d}",
                angle,
                0.565,
                1.060,
                0.060,
                0.060,
                0.035,
                mats["trim_bright"] if variant == "v05_gold_crown" else mats["trim_bright"],
            )
            crystal(
                f"Control_TopRing_MicroGem_{index:02d}",
                (0.585 * math.cos(math.radians(angle)), 0.585 * math.sin(math.radians(angle)), 1.095),
                0.022 if variant != "v03_source_dense" else 0.026,
                0.070,
                mats["energy"],
            )


def add_base_detail(mats: dict[str, bpy.types.Material], variant: str) -> None:
    segment_count = 12 if variant == "v03_source_dense" else 8
    for index in range(segment_count):
        angle = index * (360 / segment_count)
        ring_block(
            f"Control_Base_OuterSlatePanel_{index:02d}",
            angle,
            0.49,
            0.215,
            0.115,
            0.105,
            0.040,
            mats["body_dark"],
        )
        if index % 2 == 0:
            ring_block(
                f"Control_Base_GoldClamp_{index:02d}",
                angle,
                0.43,
                0.315,
                0.055,
                0.050,
                0.050,
                mats["trim"],
            )


def add_crystal_facets_and_struts(mats: dict[str, bpy.types.Material], variant: str) -> None:
    for index, angle in enumerate([0, 60, 120, 180, 240, 300]):
        theta = math.radians(angle)
        cube(
            f"Control_CrystalSocket_BlackFacet_{index:02d}",
            (0.16 * math.cos(theta), 0.16 * math.sin(theta), 0.39),
            (0.030, 0.105, 0.085),
            mats["body_dark"],
            rot_z=theta,
        )
        if variant != "v04_mobile_bold":
            cube(
                f"Control_Crystal_GoldProng_{index:02d}",
                (0.115 * math.cos(theta), 0.115 * math.sin(theta), 0.54),
                (0.020, 0.036, 0.155),
                mats["trim_dark"],
                rot_z=theta,
            )


def add_inner_energy_rings(mats: dict[str, bpy.types.Material], variant: str) -> None:
    torus("Control_InnerViolet_FieldLine_Low", 0.34, 0.010, (0, 0, 0.365), mats["energy_violet"])
    torus("Control_InnerCyan_FieldLine_Mid", 0.27, 0.009, (0, 0, 0.555), mats["energy_blue"])
    if variant == "v03_source_dense":
        torus("Control_InnerViolet_FieldLine_High", 0.39, 0.008, (0, 0, 0.765), mats["energy_violet"])


def add_gold_crown_language(mats: dict[str, bpy.types.Material]) -> None:
    # Big readable gold housings at the four major compass points, closer to
    # the 2D source plate's ornate top-ring crests.
    for index, angle in enumerate([0, 90, 180, 270]):
        ring_block(
            f"Control_SourceGoldCrown_PrimaryHousing_{index:02d}",
            angle,
            0.565,
            1.050,
            0.120,
            0.085,
            0.060,
            mats["trim_bright"],
        )
        ring_block(
            f"Control_SourceGoldCrown_DarkInset_{index:02d}",
            angle,
            0.515,
            1.085,
            0.066,
            0.052,
            0.030,
            mats["body_dark"],
        )

    # Four diagonal bracket pieces give the ring a built/engineered silhouette
    # instead of a smooth toy donut.
    for index, angle in enumerate([45, 135, 225, 315]):
        ring_block(
            f"Control_SourceGoldCrown_DiagonalBracket_{index:02d}",
            angle,
            0.555,
            1.020,
            0.082,
            0.055,
            0.040,
            mats["trim"],
        )

    # A dark inset lip under the gold rail improves contrast against the board.
    torus("Control_SourceGoldCrown_DarkInsetLip", 0.485, 0.018, (0, 0, 0.965), mats["body_dark"])


def faceted_plate(
    name: str,
    angle_deg: float,
    radius: float,
    z: float,
    material: bpy.types.Material,
    width: float = 0.125,
    height: float = 0.045,
) -> bpy.types.Object:
    # A flattened 4-sided cone gives us a diamond-ish bevel/read without
    # another rectangular box sitting on the ring.
    theta = math.radians(angle_deg)
    x = radius * math.cos(theta)
    y = radius * math.sin(theta)
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=width, radius2=width * 0.62, depth=height, location=(x, y, z), rotation=(0, 0, theta + math.radians(45)))
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name}_Mesh"
    obj.scale.y = 0.58
    obj.data.materials.append(material)
    return shade_smooth(obj, bevel=0.010)


def add_faceted_crown_language(mats: dict[str, bpy.types.Material]) -> None:
    # More source-sprite-faithful than v05: use faceted crowns and inset gems
    # instead of chunky cuboid caps.
    for index, angle in enumerate([0, 90, 180, 270]):
        faceted_plate(f"Control_FacetedCrown_GoldMajor_{index:02d}", angle, 0.565, 1.070, mats["trim_bright"], width=0.118, height=0.055)
        faceted_plate(f"Control_FacetedCrown_DarkInset_{index:02d}", angle, 0.536, 1.103, mats["body_dark"], width=0.070, height=0.026)
        crystal(
            f"Control_FacetedCrown_CyanGem_{index:02d}",
            (0.590 * math.cos(math.radians(angle)), 0.590 * math.sin(math.radians(angle)), 1.120),
            0.028,
            0.085,
            mats["energy"],
        )

    for index, angle in enumerate([45, 135, 225, 315]):
        faceted_plate(f"Control_FacetedCrown_GoldMinor_{index:02d}", angle, 0.555, 1.050, mats["trim"], width=0.080, height=0.036)
        ring_block(
            f"Control_FacetedCrown_BlackSupport_{index:02d}",
            angle,
            0.505,
            1.030,
            0.078,
            0.065,
            0.025,
            mats["body_dark"],
        )

    # Source-like gold bands crossing the darker body mass.
    torus("Control_FacetedCrown_BrightOuterGoldBand", 0.570, 0.014, (0, 0, 1.020), mats["trim_bright"])
    torus("Control_FacetedCrown_LowShadowGroove", 0.488, 0.012, (0, 0, 0.925), mats["body_dark"])


def add_integrated_crown_language(mats: dict[str, bpy.types.Material]) -> None:
    # v07 keeps v06's source-sprite fidelity, but sinks the crown language into
    # the ring so it feels engineered instead of like loose decorations.
    for index, angle in enumerate([0, 90, 180, 270]):
        faceted_plate(
            f"Control_IntegratedCrown_GoldFlushPlate_{index:02d}",
            angle,
            0.548,
            1.030,
            mats["trim_bright"],
            width=0.132,
            height=0.036,
        )
        ring_block(
            f"Control_IntegratedCrown_BlackUndercut_{index:02d}",
            angle,
            0.508,
            1.010,
            0.105,
            0.070,
            0.022,
            mats["body_dark"],
        )
        crystal(
            f"Control_IntegratedCrown_CyanInset_{index:02d}",
            (0.572 * math.cos(math.radians(angle)), 0.572 * math.sin(math.radians(angle)), 1.068),
            0.024,
            0.072,
            mats["energy"],
        )

    for index, angle in enumerate([45, 135, 225, 315]):
        faceted_plate(
            f"Control_IntegratedCrown_GoldCornerPlate_{index:02d}",
            angle,
            0.542,
            1.010,
            mats["trim"],
            width=0.082,
            height=0.030,
        )

    # Stronger source-like double rail: dark groove between gold and cyan bands.
    torus("Control_IntegratedCrown_OuterGoldLip", 0.585, 0.012, (0, 0, 1.018), mats["trim_bright"])
    torus("Control_IntegratedCrown_BlackRingGroove", 0.505, 0.014, (0, 0, 0.978), mats["body_dark"])
    torus("Control_IntegratedCrown_InnerCyanThread", 0.445, 0.010, (0, 0, 0.950), mats["energy_blue"])

    # Front hero crest gets a shaped backing instead of only a vertical block.
    faceted_plate("Control_IntegratedFront_GoldHeroCrest", 270, 0.585, 0.790, mats["trim_bright"], width=0.110, height=0.060)
    crystal("Control_IntegratedFront_CyanHeroGem", (0, -0.612, 0.825), 0.035, 0.120, mats["energy"])


def add_ring_inlay_language(mats: dict[str, bpy.types.Material]) -> None:
    # v08: improve the premium read by adding carved/inlaid bands and cyan
    # enamel-like chips that sit flush with the ring.
    add_integrated_crown_language(mats)

    for index in range(16):
        angle = index * 22.5
        if index % 2 == 0:
            ring_block(
                f"Control_RingInlay_DarkCut_{index:02d}",
                angle,
                0.465,
                1.004,
                0.040,
                0.050,
                0.018,
                mats["body_dark"],
            )
        else:
            ring_block(
                f"Control_RingInlay_CyanChip_{index:02d}",
                angle,
                0.462,
                1.012,
                0.032,
                0.034,
                0.014,
                mats["energy_blue"],
            )

    torus("Control_RingInlay_UpperShadowGroove", 0.522, 0.010, (0, 0, 1.043), mats["body_dark"])
    torus("Control_RingInlay_LowerGoldBead", 0.412, 0.012, (0, 0, 0.912), mats["trim"])


def add_crystal_cage_language(mats: dict[str, bpy.types.Material]) -> None:
    # v09: make the middle feel closer to the source plate: gold/black cage
    # around a faceted blue crystal, plus clearer vertical energy control.
    add_integrated_crown_language(mats)

    for index, angle in enumerate([30, 90, 150, 210, 270, 330]):
        theta = math.radians(angle)
        curve_arc(
            f"Control_CrystalCage_GoldRib_{index:02d}",
            0.175,
            0.355,
            0.785,
            angle,
            mats["trim_bright"],
            bevel=0.010,
        )
        cube(
            f"Control_CrystalCage_BlackFoot_{index:02d}",
            (0.205 * math.cos(theta), 0.205 * math.sin(theta), 0.350),
            (0.028, 0.046, 0.055),
            mats["body_dark"],
            rot_z=theta,
        )

    crystal("Control_CrystalCage_InnerHighlight", (0, 0, 0.64), 0.080, 0.46, mats["energy_blue"])
    torus("Control_CrystalCage_GoldCollarTop", 0.190, 0.014, (0, 0, 0.790), mats["trim"])
    torus("Control_CrystalCage_GoldCollarBottom", 0.205, 0.015, (0, 0, 0.430), mats["trim_bright"])
    torus("Control_CrystalCage_CyanPulseThread", 0.255, 0.008, (0, 0, 0.690), mats["energy_blue"])


def add_premium_balanced_language(mats: dict[str, bpy.types.Material]) -> None:
    # v10: combine the best of v08 and v09 but trim the excess: strong ring
    # inlays, better center cage, and cleaner silhouette for mobile.
    add_integrated_crown_language(mats)

    for index in range(8):
        angle = index * 45
        ring_block(
            f"Control_PremiumBalanced_CyanInlay_{index:02d}",
            angle + 22.5,
            0.462,
            1.012,
            0.040,
            0.038,
            0.014,
            mats["energy_blue"],
        )

    torus("Control_PremiumBalanced_OuterBrightGoldBead", 0.585, 0.011, (0, 0, 1.030), mats["trim_bright"])
    torus("Control_PremiumBalanced_InnerDarkGroove", 0.505, 0.011, (0, 0, 0.982), mats["body_dark"])
    torus("Control_PremiumBalanced_LowCyanThread", 0.420, 0.009, (0, 0, 0.918), mats["energy_blue"])

    for index, angle in enumerate([45, 135, 225, 315]):
        curve_arc(
            f"Control_PremiumBalanced_CageRib_{index:02d}",
            0.175,
            0.375,
            0.755,
            angle,
            mats["trim"],
            bevel=0.009,
        )
    crystal("Control_PremiumBalanced_CrystalHighlight", (0, 0, 0.66), 0.065, 0.36, mats["energy_blue"])
    torus("Control_PremiumBalanced_CrystalGoldCollar", 0.188, 0.013, (0, 0, 0.435), mats["trim_bright"])


def add_material_premium_language(mats: dict[str, bpy.types.Material]) -> None:
    # v11: same readable silhouette as v10, but the surface treatment does more
    # of the work: hot gold edges, dark grooves, cyan/white crystal accents, and
    # fewer static transparent distractions.
    add_premium_balanced_language(mats)

    # Top-ring material richness: broad dark panels plus thin hot-gold bevels.
    for index in range(8):
        angle = index * 45
        ring_block(
            f"Control_MaterialPremium_RingDarkPanel_{index:02d}",
            angle,
            0.512,
            1.040,
            0.110,
            0.094,
            0.018,
            mats["body_dark"],
        )
        ring_block(
            f"Control_MaterialPremium_RingGoldEdgeA_{index:02d}",
            angle + 12,
            0.575,
            1.066,
            0.040,
            0.026,
            0.016,
            mats["trim_hot"],
        )
        ring_block(
            f"Control_MaterialPremium_RingGoldEdgeB_{index:02d}",
            angle - 12,
            0.575,
            1.066,
            0.040,
            0.026,
            0.016,
            mats["trim_dark"],
        )

    # Cooler slate highlights on the ring/body make the dark body read beveled
    # instead of plastic-black.
    torus("Control_MaterialPremium_CoolSlateTopHighlight", 0.525, 0.010, (0, 0, 1.074), mats["body_edge"])
    torus("Control_MaterialPremium_DeepInnerShadowGroove", 0.392, 0.014, (0, 0, 0.944), mats["body_dark"])
    torus("Control_MaterialPremium_HotGoldInnerLip", 0.425, 0.010, (0, 0, 0.962), mats["trim_hot"])

    # Central crystal gets a darker core and brighter ridge accents, closer to
    # the painted source sprite's faceted value stack.
    crystal("Control_MaterialPremium_DeepBlueInnerCore", (0, 0, 0.635), 0.072, 0.430, mats["energy_deep"])
    for index, angle in enumerate([0, 90, 180, 270]):
        theta = math.radians(angle)
        cube(
            f"Control_MaterialPremium_CrystalWhiteRidge_{index:02d}",
            (0.058 * math.cos(theta), 0.058 * math.sin(theta), 0.705),
            (0.010, 0.026, 0.220),
            mats["energy_white"],
            rot_z=theta,
        )

    # Gold cage anchors should frame the hero crystal at gameplay scale.
    for index, angle in enumerate([45, 135, 225, 315]):
        curve_arc(
            f"Control_MaterialPremium_HeroGoldCage_{index:02d}",
            0.205,
            0.350,
            0.805,
            angle,
            mats["trim_hot"],
            bevel=0.011,
        )
        theta = math.radians(angle)
        cube(
            f"Control_MaterialPremium_CageDarkRoot_{index:02d}",
            (0.235 * math.cos(theta), 0.235 * math.sin(theta), 0.355),
            (0.026, 0.052, 0.062),
            mats["body_dark"],
            rot_z=theta,
        )

    # Base polish: fewer but larger value marks so it survives phone scale.
    for index, angle in enumerate([0, 90, 180, 270]):
        ring_block(
            f"Control_MaterialPremium_BaseGoldAnchor_{index:02d}",
            angle,
            0.385,
            0.335,
            0.070,
            0.060,
            0.036,
            mats["trim_hot"],
        )
        ring_block(
            f"Control_MaterialPremium_BaseCoolEdge_{index:02d}",
            angle + 22.5,
            0.505,
            0.252,
            0.080,
            0.040,
            0.020,
            mats["body_edge"],
        )


def add_sprite_fidelity_language(mats: dict[str, bpy.types.Material]) -> None:
    # v12: fix the biggest reference mismatch. The source sprite's central
    # crystal is the hero and the top ring frames it; prior passes made the ring
    # too dominant. This pass reduces tiny ring noise and pushes the crystal/cage.
    add_integrated_crown_language(mats)

    # Fewer, larger inlays: readable as premium from game camera.
    for index, angle in enumerate([0, 60, 120, 180, 240, 300]):
        ring_block(
            f"Control_SpriteFidelity_RingDarkInset_{index:02d}",
            angle,
            0.505,
            1.018,
            0.120,
            0.055,
            0.018,
            mats["body_dark"],
        )
    for index, angle in enumerate([30, 150, 270]):
        faceted_plate(
            f"Control_SpriteFidelity_BigGoldGemHousing_{index:02d}",
            angle,
            0.575,
            1.060,
            mats["trim_hot"],
            width=0.135,
            height=0.052,
        )
        crystal(
            f"Control_SpriteFidelity_BigCyanGem_{index:02d}",
            (0.602 * math.cos(math.radians(angle)), 0.602 * math.sin(math.radians(angle)), 1.105),
            0.032,
            0.092,
            mats["energy_white"],
        )

    torus("Control_SpriteFidelity_DarkPanelGroove", 0.500, 0.014, (0, 0, 0.972), mats["body_dark"])
    torus("Control_SpriteFidelity_BrightGoldUpperEdge", 0.563, 0.012, (0, 0, 1.030), mats["trim_hot"])
    torus("Control_SpriteFidelity_VioletInnerGlow", 0.430, 0.010, (0, 0, 0.930), mats["energy_violet"])

    # Tall heroic crystal stack: dark core, cyan shell, bright ridges.
    crystal("Control_SpriteFidelity_DeepHeroCore", (0, 0, 0.690), 0.088, 0.620, mats["energy_deep"])
    crystal("Control_SpriteFidelity_BrightHeroShell", (0, 0, 0.720), 0.055, 0.540, mats["energy_white"])
    for index, angle in enumerate([0, 72, 144, 216, 288]):
        theta = math.radians(angle)
        cube(
            f"Control_SpriteFidelity_CrystalFacetRidge_{index:02d}",
            (0.070 * math.cos(theta), 0.070 * math.sin(theta), 0.755),
            (0.010, 0.022, 0.260),
            mats["energy_white"],
            rot_z=theta,
        )

    # Gold cage ribs should echo the sprite's curved gold supports.
    for index, angle in enumerate([35, 145, 215, 325]):
        curve_arc(
            f"Control_SpriteFidelity_HeroGoldRib_{index:02d}",
            0.260,
            0.345,
            0.890,
            angle,
            mats["trim_hot"],
            bevel=0.014,
        )

    torus("Control_SpriteFidelity_HeroGoldBaseCollar", 0.230, 0.016, (0, 0, 0.405), mats["trim_hot"])
    torus("Control_SpriteFidelity_HeroDarkSocket", 0.265, 0.018, (0, 0, 0.355), mats["body_dark"])


def add_material_clean_language(mats: dict[str, bpy.types.Material]) -> None:
    # v13: conservative correction after v11/v12. Keep v10's cleaner
    # silhouette, then add readable material/value accents that should survive
    # game scale without producing ring clutter.
    add_premium_balanced_language(mats)

    # Four major ring crests get hot highlights; minor details stay quiet.
    for index, angle in enumerate([0, 90, 180, 270]):
        faceted_plate(
            f"Control_MaterialClean_MajorGoldCrest_{index:02d}",
            angle,
            0.558,
            1.052,
            mats["trim_hot"],
            width=0.112,
            height=0.038,
        )
        crystal(
            f"Control_MaterialClean_CrestGem_{index:02d}",
            (0.586 * math.cos(math.radians(angle)), 0.586 * math.sin(math.radians(angle)), 1.090),
            0.024,
            0.070,
            mats["energy_white"],
        )

    # Thin value bands create painted-bevel feel without adding many boxes.
    torus("Control_MaterialClean_OuterHotGoldEdge", 0.582, 0.010, (0, 0, 1.026), mats["trim_hot"])
    torus("Control_MaterialClean_UpperSlateHighlight", 0.526, 0.009, (0, 0, 1.060), mats["body_edge"])
    torus("Control_MaterialClean_InnerDeepShadow", 0.392, 0.012, (0, 0, 0.946), mats["body_dark"])
    torus("Control_MaterialClean_InnerBlueThread", 0.430, 0.008, (0, 0, 0.918), mats["energy_blue"])

    # Improve crystal material without changing hero proportions too much.
    crystal("Control_MaterialClean_DeepCore", (0, 0, 0.650), 0.055, 0.360, mats["energy_deep"])
    for index, angle in enumerate([0, 120, 240]):
        theta = math.radians(angle)
        cube(
            f"Control_MaterialClean_CrystalEdge_{index:02d}",
            (0.052 * math.cos(theta), 0.052 * math.sin(theta), 0.700),
            (0.008, 0.018, 0.185),
            mats["energy_white"],
            rot_z=theta,
        )

    # Four cage ribs, not six, to frame the crystal without visual chatter.
    for index, angle in enumerate([45, 135, 225, 315]):
        curve_arc(
            f"Control_MaterialClean_GoldCageRib_{index:02d}",
            0.185,
            0.365,
            0.780,
            angle,
            mats["trim_hot"],
            bevel=0.009,
        )


def add_fortress_ring_language(mats: dict[str, bpy.types.Material]) -> None:
    # v14: aggressive source-sprite correction. Make the upper ring feel like a
    # heavy, segmented artifact with big jewel housings instead of a toy hoop.
    add_integrated_crown_language(mats)

    torus("Control_FortressRing_HeavyDarkUpperBand", 0.560, 0.046, (0, 0, 1.070), mats["body_dark"])
    torus("Control_FortressRing_BroadGoldOuterBand", 0.590, 0.028, (0, 0, 1.088), mats["trim_hot"])
    torus("Control_FortressRing_DeepVioletInnerBand", 0.438, 0.018, (0, 0, 0.990), mats["energy_violet"])
    torus("Control_FortressRing_BlueUnderGlow", 0.592, 0.010, (0, 0, 0.955), mats["energy_blue"])

    for index, angle in enumerate([0, 90, 180, 270]):
        faceted_plate(
            f"Control_FortressRing_CardinalGoldShield_{index:02d}",
            angle,
            0.610,
            1.135,
            mats["trim_hot"],
            width=0.170,
            height=0.085,
        )
        faceted_plate(
            f"Control_FortressRing_CardinalDarkInset_{index:02d}",
            angle,
            0.575,
            1.178,
            mats["body_dark"],
            width=0.090,
            height=0.030,
        )
        crystal(
            f"Control_FortressRing_CardinalLargeGem_{index:02d}",
            (0.645 * math.cos(math.radians(angle)), 0.645 * math.sin(math.radians(angle)), 1.205),
            0.038,
            0.120,
            mats["energy_white"],
        )

    for index, angle in enumerate([45, 135, 225, 315]):
        ring_block(
            f"Control_FortressRing_MassiveSlateSegment_{index:02d}",
            angle,
            0.548,
            1.080,
            0.165,
            0.095,
            0.045,
            mats["body_lite"],
        )
        ring_block(
            f"Control_FortressRing_InnerGoldLock_{index:02d}",
            angle,
            0.458,
            1.030,
            0.082,
            0.050,
            0.032,
            mats["trim"],
        )

    for index, angle in enumerate([30, 150, 210, 330]):
        curve_arc(
            f"Control_FortressRing_ThickGoldSuspensionArc_{index:02d}",
            0.305,
            0.330,
            0.930,
            angle,
            mats["trim_hot"],
            bevel=0.017,
        )


def add_hero_crystal_language(mats: dict[str, bpy.types.Material]) -> None:
    # v15: make the center crystal the unmistakable hero, like the source plate.
    # This accepts more height and mass than previous passes.
    add_fortress_ring_language(mats)

    cylinder("Control_HeroCrystal_TallDarkSocket", 0.270, 0.160, (0, 0, 0.355), mats["body_dark"], vertices=8)
    cylinder("Control_HeroCrystal_BrightGoldCollar", 0.235, 0.080, (0, 0, 0.465), mats["trim_hot"], vertices=48)
    crystal("Control_HeroCrystal_DeepBlueShard", (0, 0, 0.755), 0.135, 0.820, mats["energy_deep"])
    crystal("Control_HeroCrystal_WhiteCyanFrontFacet", (0, -0.025, 0.790), 0.070, 0.700, mats["energy_white"])

    for index, angle in enumerate([0, 72, 144, 216, 288]):
        theta = math.radians(angle)
        cube(
            f"Control_HeroCrystal_LongBrightFacet_{index:02d}",
            (0.092 * math.cos(theta), 0.092 * math.sin(theta), 0.835),
            (0.012, 0.028, 0.360),
            mats["energy_white"],
            rot_z=theta,
        )

    for index, angle in enumerate([35, 145, 215, 325]):
        curve_arc(
            f"Control_HeroCrystal_LargeGoldCageArc_{index:02d}",
            0.325,
            0.345,
            1.035,
            angle,
            mats["trim_hot"],
            bevel=0.019,
        )

    for index, angle in enumerate([60, 120, 240, 300]):
        theta = math.radians(angle)
        cube(
            f"Control_HeroCrystal_BlackVerticalStrut_{index:02d}",
            (0.225 * math.cos(theta), 0.225 * math.sin(theta), 0.655),
            (0.026, 0.038, 0.330),
            mats["body_dark"],
            rot_z=theta,
        )


def add_front_identity_language(mats: dict[str, bpy.types.Material]) -> None:
    # v16: enforce an obvious front/read direction and large board-scale face.
    # Good for gameplay: the tower has a face, a lens, and a strong owner side.
    add_hero_crystal_language(mats)

    cube("Control_FrontIdentity_LongDarkSpine", (0, -0.520, 0.380), (0.105, 0.420, 0.058), mats["body_dark"], rot_z=0)
    cube("Control_FrontIdentity_BrightGoldSpineTrim", (0, -0.602, 0.425), (0.080, 0.245, 0.035), mats["trim_hot"], rot_z=0)
    faceted_plate("Control_FrontIdentity_HugeGoldMask", 270, 0.635, 0.850, mats["trim_hot"], width=0.185, height=0.095)
    faceted_plate("Control_FrontIdentity_DarkMaskInset", 270, 0.592, 0.895, mats["body_dark"], width=0.105, height=0.040)
    crystal("Control_FrontIdentity_HugeCyanLens", (0, -0.675, 0.930), 0.060, 0.185, mats["energy_white"])

    for index, side in enumerate([-1, 1]):
        cube(
            f"Control_FrontIdentity_SideGoldCheek_{index:02d}",
            (side * 0.155, -0.585, 0.770),
            (0.040, 0.092, 0.240),
            mats["trim"],
            rot_z=side * math.radians(14),
        )
        crystal(
            f"Control_FrontIdentity_SideBlueRelay_{index:02d}",
            (side * 0.245, -0.500, 0.515),
            0.036,
            0.180,
            mats["energy_blue"],
        )

    torus("Control_FrontIdentity_CommandHaloFront", 0.405, 0.012, (0, -0.020, 0.720), mats["energy_white"])


def add_arcane_machine_language(mats: dict[str, bpy.types.Material]) -> None:
    # v17: less statue, more controlled machinery. Stacked orbit rings,
    # readable dark gaps, and strong cyan lanes give a 2000s fantasy-tech vibe.
    add_fortress_ring_language(mats)

    for index, z in enumerate([0.500, 0.640, 0.780]):
        torus(
            f"Control_ArcaneMachine_StackedCyanOrbit_{index:02d}",
            0.285 + index * 0.035,
            0.010,
            (0, 0, z),
            mats["energy_blue"] if index != 1 else mats["energy_white"],
        )

    for index, angle in enumerate([0, 60, 120, 180, 240, 300]):
        theta = math.radians(angle)
        cube(
            f"Control_ArcaneMachine_TallGoldRail_{index:02d}",
            (0.345 * math.cos(theta), 0.345 * math.sin(theta), 0.610),
            (0.022, 0.046, 0.480),
            mats["trim_hot"] if index % 2 == 0 else mats["trim"],
            rot_z=theta,
        )
        cube(
            f"Control_ArcaneMachine_DarkRailBacker_{index:02d}",
            (0.385 * math.cos(theta), 0.385 * math.sin(theta), 0.610),
            (0.020, 0.034, 0.420),
            mats["body_dark"],
            rot_z=theta,
        )

    crystal("Control_ArcaneMachine_CleanTallCore", (0, 0, 0.730), 0.105, 0.720, mats["energy"])
    crystal("Control_ArcaneMachine_WhiteCoreEdge", (0, 0, 0.780), 0.050, 0.560, mats["energy_white"])
    torus("Control_ArcaneMachine_GoldEquatorClamp", 0.170, 0.014, (0, 0, 0.700), mats["trim_hot"])
    torus("Control_ArcaneMachine_BlackSocketUnderClamp", 0.220, 0.018, (0, 0, 0.440), mats["body_dark"])


def add_boss_silhouette_language(mats: dict[str, bpy.types.Material]) -> None:
    # v18: intentionally bold. This is the "can we finally see it changed?"
    # pass: tall ring, large jewel crown, raised pylons, and big cage silhouette.
    add_front_identity_language(mats)

    torus("Control_BossSilhouette_TopMassiveGoldCrown", 0.610, 0.034, (0, 0, 1.205), mats["trim_hot"])
    torus("Control_BossSilhouette_TopBlackInnerCut", 0.482, 0.026, (0, 0, 1.165), mats["body_dark"])
    torus("Control_BossSilhouette_LuminousLowerBasin", 0.575, 0.014, (0, 0, 0.820), mats["energy_violet"])

    for index, angle in enumerate([0, 90, 180, 270]):
        theta = math.radians(angle)
        faceted_plate(
            f"Control_BossSilhouette_OversizedCrownGemCase_{index:02d}",
            angle,
            0.650,
            1.265,
            mats["trim_hot"],
            width=0.205,
            height=0.110,
        )
        crystal(
            f"Control_BossSilhouette_OversizedCrownGem_{index:02d}",
            (0.690 * math.cos(theta), 0.690 * math.sin(theta), 1.330),
            0.050,
            0.160,
            mats["energy_white"],
        )
        cube(
            f"Control_BossSilhouette_GoldDropFin_{index:02d}",
            (0.545 * math.cos(theta), 0.545 * math.sin(theta), 1.010),
            (0.050, 0.085, 0.260),
            mats["trim"],
            rot_z=theta,
        )

    for index, angle in enumerate([30, 150, 210, 330]):
        curve_arc(
            f"Control_BossSilhouette_GiantCageArc_{index:02d}",
            0.405,
            0.345,
            1.150,
            angle,
            mats["trim_hot"],
            bevel=0.022,
        )

    for index, angle in enumerate([75, 195, 315]):
        theta = math.radians(angle)
        cylinder(f"Control_BossSilhouette_RaisedPylonBase_{index:02d}", 0.130, 0.140, (0.720 * math.cos(theta), 0.720 * math.sin(theta), 0.210), mats["body_dark"], vertices=8)
        cylinder(f"Control_BossSilhouette_RaisedPylonGoldCollar_{index:02d}", 0.116, 0.050, (0.720 * math.cos(theta), 0.720 * math.sin(theta), 0.320), mats["trim_hot"], vertices=32)
        crystal(
            f"Control_BossSilhouette_RaisedPylonCrystal_{index:02d}",
            (0.720 * math.cos(theta), 0.720 * math.sin(theta), 0.485),
            0.070,
            0.330,
            mats["energy_white"],
        )


def add_sprite_arc_refit_language(mats: dict[str, bpy.types.Material]) -> None:
    # v19, built directly from v17. Sprite reference correction:
    # - top-ring jewel housings should be sharp gold shields, not pale blocks;
    # - dark ring panels need visible carved separations;
    # - gold cage arcs should rise from the base like elegant restraint ribs.
    add_arcane_machine_language(mats)

    for index, angle in enumerate([0, 90, 180, 270]):
        theta = math.radians(angle)
        faceted_plate(
            f"Control_SpriteArcRefit_GoldShieldOuter_{index:02d}",
            angle,
            0.622,
            1.145,
            mats["trim_hot"],
            width=0.155,
            height=0.092,
        )
        faceted_plate(
            f"Control_SpriteArcRefit_GoldShieldInnerFacet_{index:02d}",
            angle,
            0.594,
            1.188,
            mats["trim_dark"],
            width=0.088,
            height=0.034,
        )
        crystal(
            f"Control_SpriteArcRefit_ShieldCyanGem_{index:02d}",
            (0.662 * math.cos(theta), 0.662 * math.sin(theta), 1.225),
            0.034,
            0.115,
            mats["energy_white"],
        )

    for index in range(12):
        angle = index * 30
        ring_block(
            f"Control_SpriteArcRefit_CarvedDarkRingPanel_{index:02d}",
            angle,
            0.515,
            1.120,
            0.090,
            0.045,
            0.024,
            mats["body_dark"],
        )
        if index % 2 == 1:
            ring_block(
                f"Control_SpriteArcRefit_ThinGoldPanelLip_{index:02d}",
                angle,
                0.575,
                1.150,
                0.048,
                0.026,
                0.018,
                mats["trim_hot"],
            )

    for index, angle in enumerate([40, 140, 220, 320]):
        curve_arc(
            f"Control_SpriteArcRefit_ElegantTallGoldRib_{index:02d}",
            0.365,
            0.330,
            1.080,
            angle,
            mats["trim_hot"],
            bevel=0.017,
        )
        theta = math.radians(angle)
        cube(
            f"Control_SpriteArcRefit_DarkRibRoot_{index:02d}",
            (0.300 * math.cos(theta), 0.300 * math.sin(theta), 0.340),
            (0.038, 0.065, 0.080),
            mats["body_dark"],
            rot_z=theta,
        )


def add_crystal_base_refit_language(mats: dict[str, bpy.types.Material]) -> None:
    # v20, built from v19. Sprite reference correction:
    # - the crystal should be faceted blue/white/violet, not a simple cyan cone;
    # - base front should have the source sprite's blue gem pylon and dark walkway;
    # - side pylons should feel seated in gold/dark mechanical sockets.
    add_sprite_arc_refit_language(mats)

    cylinder("Control_CrystalBaseRefit_DeepOctagonalSocket", 0.275, 0.120, (0, 0, 0.405), mats["body_dark"], vertices=8)
    cylinder("Control_CrystalBaseRefit_GoldSocketRim", 0.235, 0.050, (0, 0, 0.495), mats["trim_hot"], vertices=48)
    crystal("Control_CrystalBaseRefit_VioletBackShard", (0, 0.020, 0.745), 0.125, 0.760, mats["energy_violet"])
    crystal("Control_CrystalBaseRefit_DeepBlueHeroShard", (0, -0.010, 0.770), 0.105, 0.820, mats["energy_deep"])
    crystal("Control_CrystalBaseRefit_WhiteFrontShard", (0, -0.050, 0.795), 0.052, 0.680, mats["energy_white"])

    for index, angle in enumerate([0, 72, 144, 216, 288]):
        theta = math.radians(angle)
        cube(
            f"Control_CrystalBaseRefit_PaintedCrystalRidge_{index:02d}",
            (0.078 * math.cos(theta), 0.078 * math.sin(theta), 0.815),
            (0.011, 0.024, 0.340),
            mats["energy_white"] if index in {0, 1} else mats["energy_blue"],
            rot_z=theta,
        )

    cube("Control_CrystalBaseRefit_FrontDarkRunway", (0, -0.485, 0.250), (0.090, 0.390, 0.044), mats["body_dark"], rot_z=0)
    cube("Control_CrystalBaseRefit_FrontGoldRunwayLip", (0, -0.535, 0.302), (0.062, 0.270, 0.032), mats["trim"], rot_z=0)
    cylinder("Control_CrystalBaseRefit_FrontGemPedestalDark", 0.115, 0.095, (0, -0.695, 0.330), mats["body_dark"], vertices=8)
    cylinder("Control_CrystalBaseRefit_FrontGemPedestalGold", 0.095, 0.045, (0, -0.695, 0.400), mats["trim_hot"], vertices=32)
    crystal("Control_CrystalBaseRefit_FrontLargeBlueFlame", (0, -0.695, 0.530), 0.060, 0.295, mats["energy_white"])

    for index, angle in enumerate([205, 335]):
        theta = math.radians(angle)
        cylinder(f"Control_CrystalBaseRefit_SidePylonSocketDark_{index:02d}", 0.125, 0.105, (0.695 * math.cos(theta), 0.695 * math.sin(theta), 0.245), mats["body_dark"], vertices=8)
        cylinder(f"Control_CrystalBaseRefit_SidePylonGoldSeat_{index:02d}", 0.105, 0.050, (0.695 * math.cos(theta), 0.695 * math.sin(theta), 0.323), mats["trim_hot"], vertices=32)


def add_painted_depth_refit_language(mats: dict[str, bpy.types.Material]) -> None:
    # v21, built from v20. Sprite reference correction:
    # - painted source has high contrast: black inner cuts, gold bevel lights,
    #   blue underglow, and readable glass dome rim.
    # - final pass reduces loose cube chatter by using bands/grooves as the
    #   detail carrier.
    add_crystal_base_refit_language(mats)

    torus("Control_PaintedDepthRefit_BlackInnerRingCut", 0.472, 0.022, (0, 0, 1.080), mats["body_dark"])
    torus("Control_PaintedDepthRefit_HotGoldTopPaintStroke", 0.604, 0.014, (0, 0, 1.205), mats["trim_hot"])
    torus("Control_PaintedDepthRefit_DarkLowerRingShadow", 0.552, 0.022, (0, 0, 0.955), mats["body_dark"])
    torus("Control_PaintedDepthRefit_ElectricBlueUnderside", 0.585, 0.011, (0, 0, 0.918), mats["energy_blue"])
    torus("Control_PaintedDepthRefit_DomeSubtleBlueGlassRim", 0.705, 0.005, (0, 0, 0.610), mats["field"])

    for index, angle in enumerate([30, 150, 210, 330]):
        theta = math.radians(angle)
        cube(
            f"Control_PaintedDepthRefit_SpriteBlackArcShadow_{index:02d}",
            (0.390 * math.cos(theta), 0.390 * math.sin(theta), 0.710),
            (0.026, 0.052, 0.410),
            mats["body_dark"],
            rot_z=theta,
        )

    for index, angle in enumerate([0, 60, 120, 180, 240, 300]):
        ring_block(
            f"Control_PaintedDepthRefit_BaseStonePanel_{index:02d}",
            angle,
            0.335,
            0.335,
            0.100,
            0.060,
            0.038,
            mats["body_lite"] if index % 2 == 0 else mats["body_dark"],
        )


def add_elegant_gold_ribs_language(mats: dict[str, bpy.types.Material]) -> None:
    # v22, built from v21. Sprite reference correction:
    # - v21 still feels like vertical rails around the crystal;
    # - the sprite's restraint supports are elegant gold arcs rising from
    #   dark base sockets and leaning inward toward the ring.
    add_painted_depth_refit_language(mats)

    for index, angle in enumerate([38, 142, 218, 322]):
        theta = math.radians(angle)
        cylinder(
            f"Control_ElegantGoldRibs_DarkFootSocket_{index:02d}",
            0.060,
            0.085,
            (0.335 * math.cos(theta), 0.335 * math.sin(theta), 0.355),
            mats["body_dark"],
            vertices=8,
        )
        cylinder(
            f"Control_ElegantGoldRibs_HotGoldFootCap_{index:02d}",
            0.047,
            0.040,
            (0.335 * math.cos(theta), 0.335 * math.sin(theta), 0.425),
            mats["trim_hot"],
            vertices=24,
        )
        curve_arc(
            f"Control_ElegantGoldRibs_SpriteLongArc_{index:02d}",
            0.405,
            0.405,
            1.020,
            angle,
            mats["trim_hot"],
            bevel=0.021,
        )
        curve_arc(
            f"Control_ElegantGoldRibs_InnerDarkArcShadow_{index:02d}",
            0.365,
            0.390,
            0.960,
            angle,
            mats["body_dark"],
            bevel=0.010,
        )

    torus("Control_ElegantGoldRibs_LowGoldSocketRing", 0.305, 0.014, (0, 0, 0.420), mats["trim_hot"])
    torus("Control_ElegantGoldRibs_LowDarkSocketGroove", 0.245, 0.016, (0, 0, 0.385), mats["body_dark"])


def add_ring_gem_polish_language(mats: dict[str, bpy.types.Material]) -> None:
    # v23, built from v22. Sprite reference correction:
    # - the source top ring has a very deliberate big front jewel and smaller
    #   cyan gems around the ring; v22 still spreads attention too evenly.
    add_elegant_gold_ribs_language(mats)

    faceted_plate("Control_RingGemPolish_FrontLargeGoldPendant", 270, 0.655, 1.145, mats["trim_hot"], width=0.190, height=0.120)
    faceted_plate("Control_RingGemPolish_FrontDarkPendantCut", 270, 0.615, 1.205, mats["body_dark"], width=0.100, height=0.040)
    crystal("Control_RingGemPolish_FrontTallCyanPendantGem", (0, -0.700, 1.245), 0.046, 0.165, mats["energy_white"])

    faceted_plate("Control_RingGemPolish_BackLargeGoldPendant", 90, 0.640, 1.160, mats["trim_hot"], width=0.145, height=0.095)
    crystal("Control_RingGemPolish_BackCyanPendantGem", (0, 0.675, 1.225), 0.034, 0.115, mats["energy_white"])

    for index, angle in enumerate([30, 75, 120, 210, 255, 300]):
        theta = math.radians(angle)
        faceted_plate(
            f"Control_RingGemPolish_SmallGoldGemCase_{index:02d}",
            angle,
            0.603,
            1.178,
            mats["trim"],
            width=0.070,
            height=0.030,
        )
        crystal(
            f"Control_RingGemPolish_SmallCyanGem_{index:02d}",
            (0.630 * math.cos(theta), 0.630 * math.sin(theta), 1.205),
            0.020,
            0.062,
            mats["energy_white"],
        )

    torus("Control_RingGemPolish_PaintedGoldOuterStroke", 0.620, 0.010, (0, 0, 1.225), mats["trim_hot"])
    torus("Control_RingGemPolish_DeepBlueInnerStroke", 0.452, 0.010, (0, 0, 1.010), mats["energy_deep"])


def add_sprite_scale_unify_language(mats: dict[str, bpy.types.Material]) -> None:
    # v24, built from v23. Sprite reference correction:
    # - after v22/v23, the asset has the right parts but needs unified source
    #   composition: stronger circular base field, clearer front axis, and
    #   value grouping so it reads as one premium ward rather than stacked parts.
    add_ring_gem_polish_language(mats)

    cylinder("Control_SpriteScaleUnify_GlowingBlueFloorDisc", 0.650, 0.020, (0, 0, 0.115), mats["energy_blue"], vertices=96)
    torus("Control_SpriteScaleUnify_OuterGoldFloorLip", 0.640, 0.012, (0, 0, 0.155), mats["trim"])
    torus("Control_SpriteScaleUnify_InnerBlueFloorLine", 0.455, 0.008, (0, 0, 0.175), mats["energy_blue"])

    cube("Control_SpriteScaleUnify_FrontDarkBridge", (0, -0.540, 0.190), (0.115, 0.360, 0.042), mats["body_dark"], rot_z=0)
    cube("Control_SpriteScaleUnify_FrontHotGoldNeedle", (0, -0.545, 0.235), (0.040, 0.265, 0.035), mats["trim_hot"], rot_z=0)
    crystal("Control_SpriteScaleUnify_FrontBridgeCyanTip", (0, -0.735, 0.285), 0.032, 0.120, mats["energy_white"])

    for index, angle in enumerate([45, 135, 225, 315]):
        ring_block(
            f"Control_SpriteScaleUnify_HeavyDarkBaseButtress_{index:02d}",
            angle,
            0.405,
            0.285,
            0.145,
            0.090,
            0.052,
            mats["body_dark"],
        )


def add_painted_panel_cuts_language(mats: dict[str, bpy.types.Material]) -> None:
    # v25, built from v24. Step 3 rule: polish belongs in material/decal-like
    # marks, not more mass. Sprite mismatch fixed: the source ring/base have
    # crisp black cuts and hot gold bevel strokes that v24 still lacks.
    add_sprite_scale_unify_language(mats)

    for index in range(16):
        angle = index * 22.5
        ring_block(
            f"Control_PaintedPanelCuts_RingBlackInsetStroke_{index:02d}",
            angle,
            0.505,
            1.206,
            0.055,
            0.018,
            0.012,
            mats["body_dark"],
        )
        if index % 2 == 0:
            ring_block(
                f"Control_PaintedPanelCuts_RingHotGoldSlash_{index:02d}",
                angle + 7,
                0.575,
                1.235,
                0.044,
                0.014,
                0.010,
                mats["trim_hot"],
            )

    for index, angle in enumerate([0, 60, 120, 180, 240, 300]):
        ring_block(
            f"Control_PaintedPanelCuts_BaseDarkPanelCut_{index:02d}",
            angle,
            0.492,
            0.365,
            0.090,
            0.020,
            0.020,
            mats["body_dark"],
        )
        ring_block(
            f"Control_PaintedPanelCuts_BaseBlueGlowChip_{index:02d}",
            angle + 12,
            0.565,
            0.205,
            0.036,
            0.018,
            0.012,
            mats["energy_blue"],
        )


def add_gem_glow_focus_language(mats: dict[str, bpy.types.Material]) -> None:
    # v26, built from v25. Sprite mismatch fixed: the painted source's gems are
    # tiny but high-value focal points. Add controlled cyan/white glints near
    # existing gems instead of adding more tower mass.
    add_painted_panel_cuts_language(mats)

    for index, angle in enumerate([30, 75, 120, 210, 255, 300]):
        theta = math.radians(angle)
        cube(
            f"Control_GemGlowFocus_RingGemWhiteGlint_{index:02d}",
            (0.615 * math.cos(theta), 0.615 * math.sin(theta), 1.210),
            (0.008, 0.018, 0.010),
            mats["energy_blue"],
            rot_z=theta,
        )

    cube("Control_GemGlowFocus_FrontPendantWhiteFacet", (0, -0.700, 1.300), (0.012, 0.034, 0.042), mats["energy_white"], rot_z=0)
    cube("Control_GemGlowFocus_CenterCrystalWhiteFacetWide", (0, -0.075, 0.890), (0.016, 0.030, 0.260), mats["energy_white"], rot_z=0)
    torus("Control_GemGlowFocus_CenterSocketCyanHalo", 0.205, 0.008, (0, 0, 0.525), mats["energy_blue"])


def add_mobile_finish_trim_language(mats: dict[str, bpy.types.Material]) -> None:
    # v27, built from v26. Sprite mismatch fixed: v26 has better glints, but
    # mobile readability benefits from fewer competing haze lines and stronger
    # silhouette grouping. Add final dark grounding and controlled blue/gold
    # outline strokes while relying on lower dome alpha from make_materials().
    add_gem_glow_focus_language(mats)

    torus("Control_MobileFinishTrim_DeepOuterBaseShadow", 0.720, 0.018, (0, 0, 0.128), mats["body_dark"])
    torus("Control_MobileFinishTrim_FinalBlueUnderglow", 0.675, 0.008, (0, 0, 0.172), mats["energy_blue"])
    torus("Control_MobileFinishTrim_FinalGoldRingSilhouette", 0.612, 0.010, (0, 0, 1.242), mats["trim_hot"])
    torus("Control_MobileFinishTrim_FinalBlackRingUndercut", 0.545, 0.012, (0, 0, 1.000), mats["body_dark"])

    for index, angle in enumerate([45, 135, 225, 315]):
        ring_block(
            f"Control_MobileFinishTrim_BaseButtressGoldLip_{index:02d}",
            angle,
            0.440,
            0.342,
            0.080,
            0.022,
            0.018,
            mats["trim"],
        )


def add_ring_wall_depth_language(mats: dict[str, bpy.types.Material]) -> None:
    # v28, built from v27. Sprite mismatch fixed: the source top ring has a
    # chunky carved vertical wall. v27 reads as layered hoops. Add wall panels
    # and bevel shelves that are broad enough to survive mobile scale.
    add_mobile_finish_trim_language(mats)

    for index in range(12):
        angle = index * 30
        ring_block(
            f"Control_RingWallDepth_DarkVerticalWallPanel_{index:02d}",
            angle,
            0.545,
            1.075,
            0.118,
            0.030,
            0.095,
            mats["body_dark"] if index % 2 == 0 else mats["body_lite"],
        )
        ring_block(
            f"Control_RingWallDepth_GoldLowerBevelShelf_{index:02d}",
            angle + 7.5,
            0.575,
            1.020,
            0.082,
            0.022,
            0.020,
            mats["trim_hot"] if index % 3 == 0 else mats["trim"],
        )

    torus("Control_RingWallDepth_DarkUpperCarvedGroove", 0.508, 0.010, (0, 0, 1.172), mats["body_dark"])
    torus("Control_RingWallDepth_BlueInnerWallGlow", 0.448, 0.008, (0, 0, 1.058), mats["energy_deep"])


def add_crystal_refraction_lines_language(mats: dict[str, bpy.types.Material]) -> None:
    # v29, built from v28. Sprite mismatch fixed: the source crystal has painted
    # facets/refraction streaks, while v28 still reads like a simple prism.
    # Use very few long facets so it does not become confetti.
    add_ring_wall_depth_language(mats)

    for index, (angle, material, z_offset) in enumerate([
        (0, mats["energy_white"], 0.000),
        (72, mats["energy_blue"], 0.018),
        (144, mats["energy_violet"], -0.006),
        (216, mats["energy_deep"], 0.012),
        (288, mats["energy_white"], -0.012),
    ]):
        theta = math.radians(angle)
        cube(
            f"Control_CrystalRefraction_LongFacetLine_{index:02d}",
            (0.088 * math.cos(theta), 0.088 * math.sin(theta), 0.805 + z_offset),
            (0.010, 0.020, 0.390),
            material,
            rot_z=theta,
        )

    torus("Control_CrystalRefraction_DarkSocketOcclusion", 0.250, 0.012, (0, 0, 0.472), mats["body_dark"])
    torus("Control_CrystalRefraction_HotGoldSocketHighlight", 0.215, 0.008, (0, 0, 0.525), mats["trim_hot"])


def add_source_readability_lock_language(mats: dict[str, bpy.types.Material]) -> None:
    # v30, built from v29. Final lock pass: emphasize source sprite's front
    # axis and reduce competing micro-detail with broad grouping marks. This is
    # meant to be the in-game candidate, not a Blender beauty render.
    add_crystal_refraction_lines_language(mats)

    cube("Control_SourceReadabilityLock_FrontAxisDarkPlate", (0, -0.575, 0.335), (0.110, 0.420, 0.045), mats["body_dark"], rot_z=0)
    cube("Control_SourceReadabilityLock_FrontAxisGoldEdge", (0, -0.605, 0.382), (0.052, 0.305, 0.026), mats["trim_hot"], rot_z=0)
    crystal("Control_SourceReadabilityLock_FrontAxisCyanJewel", (0, -0.760, 0.440), 0.040, 0.170, mats["energy_white"])

    torus("Control_SourceReadabilityLock_FinalOuterGoldRead", 0.635, 0.011, (0, 0, 1.238), mats["trim_hot"])
    torus("Control_SourceReadabilityLock_FinalInnerBlackRead", 0.470, 0.014, (0, 0, 1.098), mats["body_dark"])
    torus("Control_SourceReadabilityLock_FinalBaseBlueRead", 0.612, 0.008, (0, 0, 0.188), mats["energy_blue"])

    # Four broad dark anchors reduce the "pile of pieces" read in the lower
    # half and echo the sprite's dark radial platform around the crystal.
    for index, angle in enumerate([45, 135, 225, 315]):
        ring_block(
            f"Control_SourceReadabilityLock_LowerDarkAnchor_{index:02d}",
            angle,
            0.335,
            0.315,
            0.135,
            0.072,
            0.042,
            mats["body_dark"],
        )


def add_crown_material_focus_language(mats: dict[str, bpy.types.Material]) -> None:
    # v31, built from v28. Sprite mismatch fixed: the ring crown/gem housings
    # still read as pale plastic blocks. Add faceted hot-gold overlays, dark
    # inset cuts, and compact cyan jewel faces to make the top ring feel painted.
    add_ring_wall_depth_language(mats)

    for index, angle in enumerate([0, 90, 180, 270]):
        theta = math.radians(angle)
        faceted_plate(
            f"Control_CrownMaterialFocus_HotGoldFacetOverlay_{index:02d}",
            angle,
            0.612,
            1.245,
            mats["trim_hot"],
            width=0.125,
            height=0.060,
        )
        faceted_plate(
            f"Control_CrownMaterialFocus_DarkInsetTriangle_{index:02d}",
            angle,
            0.588,
            1.275,
            mats["body_dark"],
            width=0.060,
            height=0.022,
        )
        crystal(
            f"Control_CrownMaterialFocus_CyanInsetGem_{index:02d}",
            (0.642 * math.cos(theta), 0.642 * math.sin(theta), 1.295),
            0.026,
            0.085,
            mats["energy_white"],
        )

    # Small bevel strokes on the four big crown supports sell gold material
    # without increasing the silhouette.
    for index, angle in enumerate([45, 135, 225, 315]):
        ring_block(
            f"Control_CrownMaterialFocus_CornerGoldBevelStroke_{index:02d}",
            angle,
            0.628,
            1.180,
            0.072,
            0.016,
            0.014,
            mats["trim_hot"],
        )


def add_field_edge_cleanup_language(mats: dict[str, bpy.types.Material]) -> None:
    # v32, built from v31. Sprite mismatch fixed: the lower blue field on the
    # source is a clean painted disc/rim; previous passes leave several visible
    # competing rings. Add broad blue/gold separation and dark occlusion bands.
    add_crown_material_focus_language(mats)

    torus("Control_FieldEdgeCleanup_GoldFieldContainmentEdge", 0.632, 0.008, (0, 0, 0.222), mats["trim"])
    torus("Control_FieldEdgeCleanup_DarkFieldOcclusionEdge", 0.565, 0.012, (0, 0, 0.236), mats["body_dark"])

    for index, angle in enumerate([0, 90, 180, 270]):
        ring_block(
            f"Control_FieldEdgeCleanup_DarkBasePanelGroup_{index:02d}",
            angle,
            0.505,
            0.255,
            0.145,
            0.052,
            0.030,
            mats["body_dark"],
        )


def add_mobile_gold_read_language(mats: dict[str, bpy.types.Material]) -> None:
    # v33, built from v32. Final candidate for this cycle. Sprite mismatch
    # fixed: the gold is still too pale in preview. Add final hot-gold read
    # strokes on existing major forms only; avoid small new objects.
    add_field_edge_cleanup_language(mats)

    torus("Control_MobileGoldRead_TopHotGoldSilhouette", 0.615, 0.012, (0, 0, 1.255), mats["trim_hot"])
    torus("Control_MobileGoldRead_MidHotGoldArcRead", 0.400, 0.010, (0, 0, 0.770), mats["trim_hot"])
    torus("Control_MobileGoldRead_BaseHotGoldRead", 0.645, 0.010, (0, 0, 0.250), mats["trim_hot"])

    for index, angle in enumerate([38, 142, 218, 322]):
        curve_arc(
            f"Control_MobileGoldRead_RibHotEdge_{index:02d}",
            0.395,
            0.420,
            1.040,
            angle,
            mats["trim_hot"],
            bevel=0.008,
        )


def add_visible_crown_gold_language(mats: dict[str, bpy.types.Material]) -> None:
    # v34, built from current winner v28, not v33. v33 proved lower field
    # ring additions get noisy. This pass targets only the visible top crown
    # material read: warm gold facets and dark insets on existing crown masses.
    add_ring_wall_depth_language(mats)

    for index, angle in enumerate([45, 135, 225, 315]):
        ring_block(
            f"Control_VisibleCrownGold_HotGoldCrownFace_{index:02d}",
            angle,
            0.615,
            1.175,
            0.145,
            0.042,
            0.035,
            mats["trim_hot"],
        )
        ring_block(
            f"Control_VisibleCrownGold_DarkInsetCrownCut_{index:02d}",
            angle,
            0.575,
            1.205,
            0.078,
            0.024,
            0.018,
            mats["body_dark"],
        )
        ring_block(
            f"Control_VisibleCrownGold_CyanGemFace_{index:02d}",
            angle,
            0.640,
            1.230,
            0.040,
            0.018,
            0.016,
            mats["energy_white"],
        )

    torus("Control_VisibleCrownGold_QuietTopGoldEdge", 0.612, 0.010, (0, 0, 1.245), mats["trim_hot"])


def add_warm_gold_grade_language(mats: dict[str, bpy.types.Material]) -> None:
    # v35, built from current winner v28. This is intentionally a grading pass:
    # preserve the readable v28 silhouette, but push the sprite's amber/gold
    # language away from cream/ivory and back toward a clear mobile-size metal read.
    add_ring_wall_depth_language(mats)
    torus("Control_WarmGoldGrade_TopGoldConfirmEdge", 0.612, 0.010, (0, 0, 1.245), mats["trim_hot"])


def add_source_panel_value_language(mats: dict[str, bpy.types.Material]) -> None:
    # v36, built from v35. The source sprite's read is not just "gold";
    # it is warm gold interrupted by dark inset stone panels with cyan gem
    # punctuation. This pass restores those value breaks at the crown level.
    add_warm_gold_grade_language(mats)

    for index, angle in enumerate([0, 45, 90, 135, 180, 225, 270, 315]):
        ring_block(
            f"Control_SourcePanelValue_DarkStoneTopPanel_{index:02d}",
            angle,
            0.500,
            1.232,
            0.150 if index % 2 == 0 else 0.105,
            0.040,
            0.020,
            mats["body_dark"],
        )
        ring_block(
            f"Control_SourcePanelValue_GoldPanelFrontLip_{index:02d}",
            angle,
            0.578,
            1.248,
            0.070,
            0.026,
            0.020,
            mats["trim_hot"] if index % 2 == 0 else mats["trim_bright"],
        )

    for index, angle in enumerate([0, 60, 120, 180, 240, 300]):
        faceted_plate(
            f"Control_SourcePanelValue_CyanInsetGem_{index:02d}",
            angle,
            0.585,
            1.275,
            mats["energy_white"],
            width=0.052,
            height=0.034,
        )


def add_cyan_panel_read_language(mats: dict[str, bpy.types.Material]) -> None:
    # v37, built from v35 after v36 exposed the right direction but wrong
    # material read. Keep the source-sprite dark-panel rhythm, but replace
    # white-pebble highlights with smaller cyan insets so the ring reads as
    # polished arcane metal instead of scattered debris.
    add_warm_gold_grade_language(mats)

    for index, angle in enumerate([0, 45, 90, 135, 180, 225, 270, 315]):
        ring_block(
            f"Control_CyanPanelRead_DarkInsetTopPanel_{index:02d}",
            angle,
            0.500,
            1.230,
            0.132 if index % 2 == 0 else 0.092,
            0.036,
            0.018,
            mats["body_dark"],
        )
        if index % 2 == 0:
            ring_block(
                f"Control_CyanPanelRead_WarmGoldPanelCap_{index:02d}",
                angle,
                0.590,
                1.248,
                0.060,
                0.024,
                0.018,
                mats["trim_hot"],
            )

    for index, angle in enumerate([0, 60, 120, 180, 240, 300]):
        faceted_plate(
            f"Control_CyanPanelRead_BlueInsetGem_{index:02d}",
            angle,
            0.586,
            1.270,
            mats["energy"],
            width=0.042,
            height=0.028,
        )


def add_blue_crystal_grade_language(mats: dict[str, bpy.types.Material]) -> None:
    # v38, built from v37. Geometry stays stable; material grade handles the
    # inherited chalky white crystals by shifting them toward source-plate cyan.
    add_cyan_panel_read_language(mats)


def add_clear_read_language(mats: dict[str, bpy.types.Material]) -> None:
    # v39, built from v38. Corrects the user-visible grain/fuzzy read by
    # keeping the solid source-fidelity shapes while letting the build skip the
    # translucent dome haze below.
    add_blue_crystal_grade_language(mats)


def build_control_tower(variant: str) -> None:
    mats = make_materials(variant)

    # Ground/control field layers.
    cylinder("Control_ContactShadow", 0.78, 0.025, (0, 0, 0.012), mats["shadow"], vertices=96)
    cylinder("Control_BlueField_Disc", 0.72, 0.035, (0, 0, 0.055), mats["energy_blue"], vertices=96)
    cylinder("Control_FieldDarkOuterRim", 0.75, 0.030, (0, 0, 0.075), mats["body_dark"], vertices=96)
    cylinder("Control_MainBase_DarkSlate", 0.46, 0.16, (0, 0, 0.14), mats["body"], vertices=80)
    cylinder("Control_BaseInner_RaisedPlate", 0.30, 0.10, (0, 0, 0.26), mats["body_lite"], vertices=64)
    add_base_detail(mats, variant)

    # Radial base fins.
    for index, angle in enumerate([0, 60, 120, 180, 240, 300]):
        theta = math.radians(angle)
        cube(
            f"Control_RadialPlate_{index:02d}",
            (0.32 * math.cos(theta), 0.32 * math.sin(theta), 0.18),
            (0.22, 0.055, 0.035),
            mats["body_lite"],
            rot_z=theta,
        )

    # Top containment ring: dark mass + gold rails + inner blue energy.
    aggressive_ring_variants = {
        "v14_fortress_ring",
        "v15_hero_crystal",
        "v16_front_identity",
        "v17_arcane_machine",
        "v18_boss_silhouette",
        "v19_sprite_arc_refit",
        "v20_crystal_base_refit",
        "v21_painted_depth_refit",
        "v22_elegant_gold_ribs",
        "v23_ring_gem_polish",
        "v24_sprite_scale_unify",
        "v25_painted_panel_cuts",
        "v26_gem_glow_focus",
        "v27_mobile_finish_trim",
        "v28_ring_wall_depth",
        "v29_crystal_refraction_lines",
        "v30_source_readability_lock",
        "v31_crown_material_focus",
        "v32_field_edge_cleanup",
        "v33_mobile_gold_read",
        "v34_visible_crown_gold",
        "v35_warm_gold_grade",
        "v36_source_panel_value",
        "v37_cyan_panel_read",
        "v38_blue_crystal_grade",
        "v39_clear_read",
    }
    ring_minor = 0.052 if variant == "v12_sprite_fidelity" else 0.082 if variant in aggressive_ring_variants else 0.065
    ring_z = 0.975 if variant == "v12_sprite_fidelity" else 1.015 if variant in aggressive_ring_variants else 0.96
    torus("Control_TopRing_DarkOuterBody", 0.52, ring_minor, (0, 0, ring_z), mats["body_lite"])
    torus("Control_TopRing_GoldOuterRail", 0.54, 0.021 if variant == "v12_sprite_fidelity" else 0.023, (0, 0, 1.005 if variant == "v12_sprite_fidelity" else 0.995), mats["trim"])
    torus("Control_TopRing_GoldInnerRail", 0.43, 0.016 if variant == "v12_sprite_fidelity" else 0.018, (0, 0, 0.940 if variant == "v12_sprite_fidelity" else 0.93), mats["trim_dark"])
    torus("Control_TopRing_CyanInnerField", 0.455, 0.012 if variant == "v12_sprite_fidelity" else 0.013, (0, 0, 0.910 if variant == "v12_sprite_fidelity" else 0.90), mats["energy_blue"])
    add_segmented_ring_detail(mats, variant)
    if variant == "v05_gold_crown":
        add_gold_crown_language(mats)
    if variant == "v06_faceted_crown":
        add_faceted_crown_language(mats)
    if variant == "v07_integrated_crown":
        add_integrated_crown_language(mats)
    if variant == "v08_ring_inlay":
        add_ring_inlay_language(mats)
    if variant == "v09_crystal_cage":
        add_crystal_cage_language(mats)
    if variant == "v10_premium_balanced":
        add_premium_balanced_language(mats)
    if variant == "v11_material_premium":
        add_material_premium_language(mats)
    if variant == "v12_sprite_fidelity":
        add_sprite_fidelity_language(mats)
    if variant == "v13_material_clean":
        add_material_clean_language(mats)
    if variant == "v14_fortress_ring":
        add_fortress_ring_language(mats)
    if variant == "v15_hero_crystal":
        add_hero_crystal_language(mats)
    if variant == "v16_front_identity":
        add_front_identity_language(mats)
    if variant == "v17_arcane_machine":
        add_arcane_machine_language(mats)
    if variant == "v18_boss_silhouette":
        add_boss_silhouette_language(mats)
    if variant == "v19_sprite_arc_refit":
        add_sprite_arc_refit_language(mats)
    if variant == "v20_crystal_base_refit":
        add_crystal_base_refit_language(mats)
    if variant == "v21_painted_depth_refit":
        add_painted_depth_refit_language(mats)
    if variant == "v22_elegant_gold_ribs":
        add_elegant_gold_ribs_language(mats)
    if variant == "v23_ring_gem_polish":
        add_ring_gem_polish_language(mats)
    if variant == "v24_sprite_scale_unify":
        add_sprite_scale_unify_language(mats)
    if variant == "v25_painted_panel_cuts":
        add_painted_panel_cuts_language(mats)
    if variant == "v26_gem_glow_focus":
        add_gem_glow_focus_language(mats)
    if variant == "v27_mobile_finish_trim":
        add_mobile_finish_trim_language(mats)
    if variant == "v28_ring_wall_depth":
        add_ring_wall_depth_language(mats)
    if variant == "v29_crystal_refraction_lines":
        add_crystal_refraction_lines_language(mats)
    if variant == "v30_source_readability_lock":
        add_source_readability_lock_language(mats)
    if variant == "v31_crown_material_focus":
        add_crown_material_focus_language(mats)
    if variant == "v32_field_edge_cleanup":
        add_field_edge_cleanup_language(mats)
    if variant == "v33_mobile_gold_read":
        add_mobile_gold_read_language(mats)
    if variant == "v34_visible_crown_gold":
        add_visible_crown_gold_language(mats)
    if variant == "v35_warm_gold_grade":
        add_warm_gold_grade_language(mats)
    if variant == "v36_source_panel_value":
        add_source_panel_value_language(mats)
    if variant == "v37_cyan_panel_read":
        add_cyan_panel_read_language(mats)
    if variant == "v38_blue_crystal_grade":
        add_blue_crystal_grade_language(mats)
    if variant == "v39_clear_read":
        add_clear_read_language(mats)

    # Central suspended crystal.
    if variant in {
        "v15_hero_crystal",
        "v16_front_identity",
        "v17_arcane_machine",
        "v18_boss_silhouette",
        "v19_sprite_arc_refit",
        "v20_crystal_base_refit",
        "v21_painted_depth_refit",
        "v22_elegant_gold_ribs",
        "v23_ring_gem_polish",
        "v24_sprite_scale_unify",
        "v25_painted_panel_cuts",
        "v26_gem_glow_focus",
        "v27_mobile_finish_trim",
        "v28_ring_wall_depth",
        "v29_crystal_refraction_lines",
        "v30_source_readability_lock",
        "v31_crown_material_focus",
        "v32_field_edge_cleanup",
        "v33_mobile_gold_read",
        "v34_visible_crown_gold",
        "v35_warm_gold_grade",
        "v36_source_panel_value",
        "v37_cyan_panel_read",
        "v38_blue_crystal_grade",
        "v39_clear_read",
    }:
        crystal("Control_CentralCrystal", (0, 0, 0.72), 0.11, 0.70, mats["energy"])
    elif variant == "v12_sprite_fidelity":
        crystal("Control_CentralCrystal", (0, 0, 0.70), 0.13, 0.76, mats["energy"])
    else:
        crystal("Control_CentralCrystal", (0, 0, 0.60), 0.15, 0.66, mats["energy"])
    cylinder("Control_CrystalSocket_Gold", 0.18, 0.06, (0, 0, 0.30), mats["trim"], vertices=48)
    cylinder("Control_CrystalSocket_Dark", 0.23, 0.08, (0, 0, 0.24), mats["body"], vertices=48)
    add_crystal_facets_and_struts(mats, variant)
    add_inner_energy_rings(mats, variant)

    # Gold containment ribs around dome.
    dense_arc_variants = {
        "v03_source_dense",
        "v05_gold_crown",
        "v06_faceted_crown",
        "v07_integrated_crown",
        "v08_ring_inlay",
        "v09_crystal_cage",
        "v14_fortress_ring",
        "v15_hero_crystal",
        "v16_front_identity",
        "v17_arcane_machine",
        "v18_boss_silhouette",
        "v19_sprite_arc_refit",
        "v20_crystal_base_refit",
        "v21_painted_depth_refit",
        "v22_elegant_gold_ribs",
        "v23_ring_gem_polish",
        "v24_sprite_scale_unify",
        "v25_painted_panel_cuts",
        "v26_gem_glow_focus",
        "v27_mobile_finish_trim",
        "v28_ring_wall_depth",
        "v29_crystal_refraction_lines",
        "v30_source_readability_lock",
        "v31_crown_material_focus",
        "v32_field_edge_cleanup",
        "v33_mobile_gold_read",
        "v34_visible_crown_gold",
        "v35_warm_gold_grade",
        "v36_source_panel_value",
        "v37_cyan_panel_read",
        "v38_blue_crystal_grade",
        "v39_clear_read",
    }
    arc_angles = [35, 145, 215, 325] if variant not in dense_arc_variants else [25, 85, 145, 205, 265, 325]
    for index, angle in enumerate(arc_angles):
        curve_arc(f"Control_GoldContainmentArc_{index:02d}", 0.50, 0.28, 0.82, angle, mats["trim"], bevel=0.018)

    # Three/four field pylons with cyan crystals.
    for index, angle in enumerate([90, 210, 330]):
        theta = math.radians(angle)
        x = 0.66 * math.cos(theta)
        y = 0.66 * math.sin(theta)
        cylinder(f"Control_FieldPylon_{index:02d}_BaseDark", 0.105, 0.10, (x, y, 0.13), mats["body"], vertices=32)
        cylinder(f"Control_FieldPylon_{index:02d}_GoldRing", 0.095, 0.04, (x, y, 0.205), mats["trim"], vertices=32)
        crystal(f"Control_FieldPylon_{index:02d}_Crystal", (x, y, 0.32), 0.055, 0.25, mats["energy"])

    # Top ring gems.
    if variant == "v01_baseline":
        for idx, angle in enumerate([0, 45, 90, 135, 180, 225, 270, 315]):
            make_gem(f"Control_TopRingGem_{idx:02d}", angle, 0.54, 1.06, mats)

    # Front hero setting mirrors the 2D plate's gold/cyan center piece.
    cube("Control_FrontGoldCrest", (0, -0.55, 0.75), (0.08, 0.035, 0.16), mats["trim"], rot_z=0)
    crystal("Control_FrontCyanGem", (0, -0.585, 0.78), 0.038, 0.18, mats["energy"])

    if variant != "v39_clear_read":
        # Translucent dome as a squashed UV sphere. V04 uses a quieter dome because
        # the in-game view can get noisy when the field canopy overlaps creeps/grid.
        bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=16, radius=0.72, location=(0, 0, 0.42))
        dome = bpy.context.object
        dome.name = "Control_TranslucentContainmentDome"
        dome.data.name = "Control_TranslucentContainmentDome_Mesh"
        dome_scale_z = 0.28 if variant == "v12_sprite_fidelity" else 0.30 if variant in {"v11_material_premium", "v13_material_clean", "v14_fortress_ring", "v15_hero_crystal", "v16_front_identity", "v17_arcane_machine", "v18_boss_silhouette", "v19_sprite_arc_refit", "v20_crystal_base_refit", "v21_painted_depth_refit", "v22_elegant_gold_ribs", "v23_ring_gem_polish", "v24_sprite_scale_unify", "v25_painted_panel_cuts", "v26_gem_glow_focus", "v27_mobile_finish_trim", "v28_ring_wall_depth", "v29_crystal_refraction_lines", "v30_source_readability_lock", "v31_crown_material_focus", "v32_field_edge_cleanup", "v33_mobile_gold_read", "v34_visible_crown_gold", "v35_warm_gold_grade", "v36_source_panel_value", "v37_cyan_panel_read", "v38_blue_crystal_grade"} else 0.34 if variant in {"v04_mobile_bold", "v10_premium_balanced"} else 0.42
        dome.scale = (1.0, 1.0, dome_scale_z)
        dome.data.materials.append(mats["field"])
        shade_smooth(dome)

        # Remove lower dome vertices to make it less like a bubble ball and more like a field canopy.
        bpy.context.view_layer.objects.active = dome
        dome.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_mode(type="VERT")
        bpy.ops.mesh.select_all(action="DESELECT")
        bpy.ops.object.mode_set(mode="OBJECT")
        for vert in dome.data.vertices:
            world_z = (dome.matrix_world @ vert.co).z
            vert.select = world_z < 0.15
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.delete(type="VERT")
        bpy.ops.object.mode_set(mode="OBJECT")
        dome.select_set(False)

    # Source reference empties for Unity wrapper/animation planning.
    for name, loc in {
        "Anchor_Muzzle": (0, -0.58, 0.72),
        "Anchor_Lens": (0, 0, 0.70),
        "Anchor_ControlCore": (0, 0, 0.62),
        "Anchor_ControlRing": (0, 0, 0.96),
        "Anchor_PulseEmitter": (0, -0.58, 0.72),
    }.items():
        empty = bpy.data.objects.new(name, None)
        empty.empty_display_type = "SPHERE"
        empty.empty_display_size = 0.05
        empty.location = loc
        bpy.context.collection.objects.link(empty)


def export_fbx(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "EMPTY"}:
            obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
    )


def main() -> None:
    args = parse_args()
    clear_scene()
    build_control_tower(args.variant)
    export_fbx(Path(args.output).resolve())
    print(f"Authored Control tower source exported to {Path(args.output).resolve()}")


if __name__ == "__main__":
    main()
