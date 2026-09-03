#!/usr/bin/env python3
"""Run the no-human-design AI model intake loop for one candidate export.

This script assumes the design came from an external AI 3D generator. Codex does
not hand-model the asset here; it only audits, normalizes, previews, and writes a
scorecard so candidates can be accepted/rejected consistently.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_BLENDER = Path("/Applications/Blender.app/Contents/MacOS/Blender")
SUPPORTED_EXTENSIONS = {".fbx", ".glb", ".gltf", ".obj", ".blend"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Audit, prepare, preview, and score an AI-generated model candidate.")
    parser.add_argument("--input", required=True, help="AI-generated model export: FBX, GLB/GLTF, OBJ, or BLEND.")
    parser.add_argument("--asset-id", required=True, help="Line Wars asset id, e.g. tower.control.")
    parser.add_argument("--role", required=True, help="Pipeline role, e.g. control, arrow, runner.")
    parser.add_argument("--candidate", required=True, help="Candidate slug, e.g. meshy_batch01_c03.")
    parser.add_argument("--kind", choices=("tower", "creep", "builder"), default="tower")
    parser.add_argument("--blender", default=str(DEFAULT_BLENDER), help="Path to Blender executable.")
    parser.add_argument("--target-height", type=float, default=1.25)
    parser.add_argument("--max-footprint", type=float, default=1.45)
    parser.add_argument("--max-triangles", type=int, default=25000)
    parser.add_argument("--max-transparent-materials", type=int, default=2)
    parser.add_argument("--max-materials", type=int, default=12)
    parser.add_argument(
        "--metallic-smoothness-size",
        type=int,
        default=1024,
        help="Resolution of the repacked metallic-smoothness map. Metallic and smoothness are "
        "low-frequency data, so the generator's source resolution is rarely worth keeping.",
    )
    parser.add_argument("--skip-preview", action="store_true")
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Exit non-zero on needs_review as well as fail, so intake can gate a build. Off by "
        "default because 14 of the 30 shipped assets would not pass today; see exit_code_for.",
    )
    return parser.parse_args()


def run(command: list[str]) -> None:
    subprocess.run(command, cwd=REPO_ROOT, check=True)


def score_candidate(audit: dict[str, object], prep: dict[str, object] | None, args: argparse.Namespace) -> dict[str, object]:
    triangle_count = int(audit.get("triangle_count", 0))
    material_count = int(audit.get("unique_material_count", 0))
    transparent_count = int(audit.get("transparent_material_count", 0))
    mesh_count = int(audit.get("mesh_count", 0))
    texture_count = int(audit.get("texture_count", 0))

    checks = [
        {
            "id": "has_meshes",
            "pass": mesh_count > 0,
            "detail": f"{mesh_count} mesh object(s)",
        },
        {
            "id": "triangle_budget",
            "pass": 0 < triangle_count <= args.max_triangles,
            "detail": f"{triangle_count} / {args.max_triangles} triangles",
        },
        {
            "id": "material_budget",
            "pass": material_count <= args.max_materials,
            "detail": f"{material_count} / {args.max_materials} unique materials",
        },
        {
            "id": "transparent_material_budget",
            "pass": transparent_count <= args.max_transparent_materials,
            "detail": f"{transparent_count} / {args.max_transparent_materials} transparent materials",
        },
        {
            "id": "has_textures_or_materials",
            "pass": texture_count > 0 or material_count >= 3,
            "detail": f"{texture_count} texture(s), {material_count} material(s)",
        },
    ]

    # Surface-map coverage. These are reported rather than hard-failed: a candidate without a
    # normal map is still usable, it just renders flatter than one with it, and every drop to date
    # has shipped without one because the generator was not asked for it. Making that visible on
    # the scorecard is the point.
    audit_textures = {
        Path(str(name)).name
        for key in ("texture_paths", "packed_texture_names")
        for name in (audit.get(key) or [])
    }
    exported = {Path(str(name)).name for name in (prep.get("exported_textures") or [])} if prep else set()
    available = audit_textures | exported

    def has_map(*needles: str) -> bool:
        return any(any(needle.lower() in name.lower() for needle in needles) for name in available)

    # Deliberately NOT conditional on `available`. It used to be, and that inverted the check:
    # an asset that shipped with no texture maps whatsoever skipped the normal-map check entirely
    # and scored a clean pass, while one that shipped with maps but no normal map was marked
    # needs_review. The worse asset scored better. Absence of every map is the strongest possible
    # failure of "has a normal map", not an exemption from being asked.
    if available:
        normal_detail = (
            "normal map present" if has_map("normal", "_nrm", "_n.")
            else "no normal map: surface detail will read flat, re-export with one if the silhouette needs it"
        )
    else:
        normal_detail = "no texture maps of any kind were found for this candidate"

    checks.append(
        {
            "id": "has_normal_map",
            "pass": bool(available) and has_map("normal", "_nrm", "_n."),
            "detail": normal_detail,
        }
    )

    if prep is not None:
        final_bounds = prep.get("normalization", {}).get("final_bounds", {}) if isinstance(prep.get("normalization"), dict) else {}
        final_size = final_bounds.get("size", []) if isinstance(final_bounds, dict) else []
        if len(final_size) == 3:
            footprint = max(float(final_size[0]), float(final_size[1]))
            checks.append(
                {
                    "id": "prepared_footprint",
                    "pass": footprint <= args.max_footprint + 0.001,
                    "detail": f"{footprint:.3f} / {args.max_footprint:.3f} footprint after prepare",
                }
            )

    passed = sum(1 for check in checks if check["pass"])
    status = "pass" if passed == len(checks) else "needs_review"
    if triangle_count <= 0 or mesh_count <= 0:
        status = "fail"

    return {
        "status": status,
        "passed_checks": passed,
        "total_checks": len(checks),
        "checks": checks,
        "failed_checks": [check["id"] for check in checks if not check["pass"]],
    }


# Exit codes, so a caller can gate on intake without parsing anything.
EXIT_OK = 0
EXIT_FAIL = 1
EXIT_NEEDS_REVIEW = 2


def exit_code_for(status: str, strict: bool) -> int:
    """Map a scorecard status to a process exit code.

    `needs_review` is only fatal under --strict, and that default is a deliberate,
    temporary compromise rather than an oversight. Turning it on today would block
    every asset in the game: 14 of the 30 shipped assets carry needs_review, and all 30
    are in production. That is the finding, not an argument against gating — but a gate
    that fails everything on the day it lands gets switched off within the hour.

    So the mechanism ships now and the default flips once the assets can pass it. The
    blocker is the normal-map decision (OPEN_ITEMS item 3): has_normal_map is the check
    the shipped assets fail, and no asset can pass it until normal maps are either
    generated or the check is retired as inapplicable.
    """
    if status == "fail":
        return EXIT_FAIL
    if status == "needs_review":
        return EXIT_NEEDS_REVIEW if strict else EXIT_OK
    return EXIT_OK


def write_markdown_scorecard(path: Path, data: dict[str, object]) -> None:
    checks = data["score"]["checks"]
    lines = [
        f"# AI Asset Intake Scorecard: {data['asset_id']} / {data['candidate']}",
        "",
        f"Date: {data['date']}",
        f"Status: {data['score']['status']}",
        "",
        "## Candidate",
        "",
        f"- Source: `{data['source']}`",
        f"- Prepared FBX: `{data['prepared_fbx']}`",
        f"- Preview: `{data['preview_png']}`" if data.get("preview_png") else "- Preview: skipped",
        f"- Audit JSON: `{data['audit_json']}`",
        "",
        "## Gate Checks",
        "",
        "| Check | Result | Detail |",
        "| --- | --- | --- |",
    ]
    for check in checks:
        result = "pass" if check["pass"] else "fail"
        lines.append(f"| `{check['id']}` | {result} | {check['detail']} |")
    lines.extend(
        [
            "",
            "## Human Design Rule",
            "",
            "This candidate must only be altered by automated cleanup, normalization, material assignment, or rejection.",
            "Do not hand-model corrective shapes into this candidate. If it fails art quality, reject it and generate a new AI candidate batch.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def resolve_staging_dir(args: argparse.Namespace) -> Path:
    role_folder = args.role.title().replace("_", "")
    models_dir = REPO_ROOT / "unity" / "LTW.UnityClient" / "Assets" / "Art" / "AIStaging" / "Models"

    if args.kind == "tower":
        return models_dir / "Towers" / role_folder / "AIDrop"
    if args.kind == "creep":
        return models_dir / "Creeps" / role_folder / "AIDrop"
    if args.kind == "builder":
        return models_dir / "Builder" / role_folder / "AIDrop"

    raise ValueError(f"Unsupported asset kind: {args.kind}")


def main() -> int:
    args = parse_args()
    blender = Path(args.blender)
    source = Path(args.input).resolve()
    if source.suffix.lower() not in SUPPORTED_EXTENSIONS:
        raise ValueError(f"Unsupported input extension: {source.suffix}")
    if not source.exists():
        raise FileNotFoundError(source)

    slug = args.candidate.replace(" ", "_")
    staging_dir = resolve_staging_dir(args)
    docs_dir = REPO_ROOT / "docs" / "art-pipeline" / "ai-model-intake" / "runs" / args.role / slug
    staging_dir.mkdir(parents=True, exist_ok=True)
    docs_dir.mkdir(parents=True, exist_ok=True)

    prepared = staging_dir / f"{args.role}_{slug}_prepared.fbx"
    audit_json = docs_dir / "audit.json"
    preview_png = docs_dir / "preview.png"
    scorecard_md = docs_dir / "scorecard.md"

    run(
        [
            str(blender),
            "--background",
            "--python",
            "tools/art_pipeline/blender_audit_model.py",
            "--",
            "--input",
            str(source),
            "--output",
            str(audit_json),
        ]
    )

    run(
        [
            str(blender),
            "--background",
            "--python",
            "tools/art_pipeline/blender_prepare_tower_source.py",
            "--",
            "--input",
            str(source),
            "--output",
            str(prepared),
            "--role",
            args.role,
            "--target-height",
            str(args.target_height),
            "--max-footprint",
            str(args.max_footprint),
        ]
    )

    if not args.skip_preview:
        run(
            [
                str(blender),
                "--background",
                "--python",
                "tools/art_pipeline/blender_render_model_preview.py",
                "--",
                "--input",
                str(prepared),
                "--output",
                str(preview_png),
            ]
        )

    # The generators export the glTF metallic-roughness packing, which Unity's Standard shader
    # cannot read: it wants metallic in R and smoothness in A. Converting here means a drop is
    # usable the moment intake finishes, instead of rendering with wrong metal and gloss until
    # someone remembers to run the repacker by hand.
    texture_dir = prepared.parent / f"{prepared.stem}_Textures"
    if (texture_dir / "Baked_MetallicRoughness.png").exists():
        run(
            [
                str(blender),
                "--background",
                "--python",
                "tools/art_pipeline/repack_metallic_smoothness.py",
                "--",
                "--root",
                str(texture_dir),
                "--max-size",
                str(args.metallic_smoothness_size),
            ]
        )

    audit = json.loads(audit_json.read_text(encoding="utf-8"))
    prep_report_path = prepared.with_suffix(".prep-report.json")
    prep = json.loads(prep_report_path.read_text(encoding="utf-8")) if prep_report_path.exists() else None
    score = score_candidate(audit, prep, args)
    data = {
        "date": datetime.now(timezone.utc).isoformat(),
        "asset_id": args.asset_id,
        "role": args.role,
        "kind": args.kind,
        "candidate": slug,
        "source": str(source),
        "prepared_fbx": str(prepared),
        "preview_png": str(preview_png) if not args.skip_preview else "",
        "audit_json": str(audit_json),
        "prep_report_json": str(prep_report_path),
        "score": score,
    }
    (docs_dir / "score.json").write_text(json.dumps(data, indent=2), encoding="utf-8")
    write_markdown_scorecard(scorecard_md, data)
    print(f"AI asset intake complete: {score['status']}")
    if score["failed_checks"]:
        print(f"Failed checks: {', '.join(score['failed_checks'])}")
    print(f"Prepared FBX: {prepared}")
    print(f"Scorecard: {scorecard_md}")
    return exit_code_for(str(score["status"]), args.strict)


if __name__ == "__main__":
    sys.exit(main())
