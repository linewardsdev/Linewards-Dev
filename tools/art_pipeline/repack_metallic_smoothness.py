#!/usr/bin/env python3
"""Repack glTF metallic-roughness maps into Unity Standard metallic-smoothness maps.

AI 3D generators export the glTF convention, where one texture reserves R for occlusion,
and packs roughness in G and metallic in B. Unity's Standard shader reads a different
layout from _MetallicGlossMap: metallic in R and smoothness in A, where smoothness is the
inverse of roughness. Binding the source map directly therefore samples the wrong channels
and produces wrong metal and gloss response.

R is only RESERVED for occlusion by that convention, not necessarily populated. Measured
across all 21 maps in this repo, R is exactly 0.000 in every pixel of every file while G
and B both carry real signal — Meshy allocates the channel and bakes nothing into it. So
there is currently no AO to recover here, and recovering it anyway would be actively
destructive: 0 means fully occluded, so binding that channel as _OcclusionMap would
multiply every model's ambient contribution by zero.

This script therefore extracts occlusion only when the channel actually contains
occlusion, and reports when it does not. See extract_occlusion below.

This script rewrites each Baked_MetallicRoughness.png as a sibling
Baked_MetallicSmoothness.png in Unity's layout:

    R = source B          (metallic)
    G = 0
    B = 0
    A = 1 - source G      (smoothness)

Meshy also emits a second, unpacked shape: separate texture_0_metallic_png.png and
texture_0_roughness_png.png files with no combined map. Folders in that shape are handled
too, reading metallic from the metallic map's red channel and smoothness from the inverse
of the roughness map's red channel. Without this, those creeps silently bind no
_MetallicGlossMap at all and render with no metal response — which is the state every
Category 2 creep shipped in.

Run under Blender's bundled Python, which provides numpy:

    /Applications/Blender.app/Contents/MacOS/Blender --background \
        --python tools/art_pipeline/repack_metallic_smoothness.py -- --root <models dir>

The source map is left in place; the repacked sibling is what materials should bind.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy
import numpy as np

SOURCE_NAME = "Baked_MetallicRoughness.png"
OUTPUT_NAME = "Baked_MetallicSmoothness.png"
OCCLUSION_NAME = "Baked_Occlusion.png"
SEPARATE_METALLIC_NAME = "texture_0_metallic_png.png"
SEPARATE_ROUGHNESS_NAME = "texture_0_roughness_png.png"

# Minimum standard deviation for the R channel to be treated as occlusion data.
#
# The test is variation, not brightness, because occlusion IS variation — a constant channel
# describes no cavity anywhere no matter what its value is. This also distinguishes the two
# ways the channel can be empty, which a brightness test conflates: constant 1.0 is a
# legitimately unoccluded bake, constant 0.0 is an unwritten channel, and both are useless
# as an _OcclusionMap while only one of them is harmless if bound.
#
# 0.01 is well below any real bake (a plain sphere lands near 0.05) and well above PNG
# quantisation noise on a flat channel.
OCCLUSION_MIN_STD = 0.01


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, help="Directory searched recursively for source maps.")
    parser.add_argument(
        "--max-size",
        type=int,
        default=1024,
        help="Downsample the output to at most this many pixels per side. Metallic and smoothness "
        "are low-frequency data, so the source resolution is rarely worth keeping.",
    )
    parser.add_argument("--dry-run", action="store_true", help="Report what would be written and exit.")
    parser.add_argument(
        "--force",
        action="store_true",
        help=(
            "Overwrite outputs whose committed pixels differ from what this run produces. "
            "Without it those files are left alone and the run exits 2. See open item 18."
        ),
    )
    return parser.parse_args(argv)


def box_downsample(pixels: np.ndarray, width: int, height: int, max_size: int) -> tuple[np.ndarray, int, int]:
    """Average square blocks down to max_size per side, when it divides evenly."""
    factor = min(width, height) // max_size
    if factor < 2:
        return pixels, width, height

    target_width, target_height = width // factor, height // factor
    if target_width * factor != width or target_height * factor != height:
        return pixels, width, height

    blocks = pixels.reshape(target_height, factor, target_width, factor, 4)
    return blocks.mean(axis=(1, 3)).reshape(-1, 4), target_width, target_height


# Set from --force in main(). Module level because _write_linear is reached down several call
# paths that would otherwise each have to thread the flag through untouched.
ALLOW_OVERWRITE_DIVERGENT = False

# Outputs this run declined to overwrite, reported together at the end.
DIVERGENT: list[Path] = []


def _write_linear(pixels: np.ndarray, width: int, height: int, path: Path, name: str) -> None:
    """Save an RGBA buffer as a non-colour PNG, refusing to silently rewrite tracked art.

    Open item 18: eleven of the twenty-one committed maps were baked from 4096x4096 sources
    that a later commit replaced with 1024 versions, so a plain re-run rewrites them with up
    to 0.46 per-pixel difference in metallic. Which version is better is a genuine decision
    and this does not make it — it only stops the rewrite happening SILENTLY, which is what
    turned it into mystery churn in unrelated commits.

    Compares decoded pixels rather than file bytes: PNG encoders are free to differ in
    filtering and chunk layout for identical images, so a byte comparison would report drift
    that is not there.
    """
    if path.exists() and not ALLOW_OVERWRITE_DIVERGENT and _differs_on_disk(pixels, width, height, path):
        DIVERGENT.append(path)
        return

    _save_linear(pixels, width, height, path, name)


def _differs_on_disk(pixels: np.ndarray, width: int, height: int, path: Path) -> bool:
    """Whether the committed image at path decodes to something other than these pixels."""
    try:
        existing, existing_width, existing_height = _load_rgba(path)
    except Exception:
        # Unreadable or unexpected: treat as different so the caller reports rather than
        # overwrites. Being wrong in this direction costs a message; the other costs the art.
        return True

    if (existing_width, existing_height) != (width, height):
        return True

    return not np.array_equal(existing, pixels.reshape(existing.shape))


def _save_linear(pixels: np.ndarray, width: int, height: int, path: Path, name: str) -> None:
    """Unconditional save. See _write_linear for the guard in front of it."""
    output = bpy.data.images.new(name, width=width, height=height, alpha=True)
    try:
        # The source maps are linear data, so keep them out of the sRGB transfer path.
        output.colorspace_settings.name = "Non-Color"
        output.alpha_mode = "CHANNEL_PACKED"
        output.pixels = pixels.reshape(-1).tolist()
        output.filepath_raw = str(path)
        output.file_format = "PNG"
        output.save()
    finally:
        bpy.data.images.remove(output)


def extract_occlusion(
    occlusion: np.ndarray, source_path: Path, width: int, height: int, max_size: int
) -> str:
    """Write Baked_Occlusion.png if R holds occlusion, and describe what was decided.

    Returns a human-readable verdict either way, because "no AO was written" is a result
    worth printing rather than a silence. This item was raised on the assumption that AO
    was present and being discarded; it is the absence that is the finding, and a run that
    printed nothing would leave the next reader to make the same assumption again.
    """
    std = float(occlusion.std())
    if std < OCCLUSION_MIN_STD:
        constant = float(occlusion.mean())
        reason = "unwritten channel" if constant < 0.5 else "fully unoccluded bake"
        return f"no occlusion (R is constant {constant:.3f}, {reason}; std {std:.4f})"

    # Unity's _OcclusionMap samples green, so occlusion is replicated across RGB rather than
    # left in R alone. That costs nothing at this size and makes the file readable as a
    # greyscale image in any viewer, which matters for a map nobody can sanity-check by eye
    # once it is packed into a single channel.
    packed = np.zeros((height, width, 4), dtype=np.float32)
    packed[:, :, 0] = occlusion
    packed[:, :, 1] = occlusion
    packed[:, :, 2] = occlusion
    packed[:, :, 3] = 1.0

    packed, out_width, out_height = box_downsample(packed.reshape(-1, 4), width, height, max_size)
    _write_linear(packed, out_width, out_height, source_path.with_name(OCCLUSION_NAME), OCCLUSION_NAME)
    return f"occlusion ({out_width}x{out_height}, std {std:.3f}) -> {OCCLUSION_NAME}"


def repack(source_path: Path, max_size: int) -> tuple[int, int, str]:
    """Write the Unity-layout sibling for one source map and return its dimensions."""
    image = bpy.data.images.load(str(source_path))
    try:
        width, height = image.size
        pixels = np.array(image.pixels[:], dtype=np.float32).reshape(height, width, 4)

        occlusion_note = extract_occlusion(pixels[:, :, 0], source_path, width, height, max_size)

        packed = np.zeros_like(pixels)
        packed[:, :, 0] = pixels[:, :, 2]          # metallic   <- source blue
        packed[:, :, 3] = 1.0 - pixels[:, :, 1]    # smoothness <- inverse of source green

        packed, width, height = box_downsample(packed.reshape(-1, 4), width, height, max_size)
        _write_linear(packed, width, height, source_path.with_name(OUTPUT_NAME), OUTPUT_NAME)

        return width, height, occlusion_note
    finally:
        bpy.data.images.remove(image)


def _load_rgba(path: Path) -> tuple[np.ndarray, int, int]:
    image = bpy.data.images.load(str(path))
    try:
        width, height = image.size
        return np.array(image.pixels[:], dtype=np.float32).reshape(height, width, 4), width, height
    finally:
        bpy.data.images.remove(image)


def repack_separate(metallic_path: Path, roughness_path: Path, max_size: int) -> tuple[int, int]:
    """Combine separate metallic and roughness maps into Unity's packed layout.

    Both are authored greyscale, so the red channel carries the signal in each.
    """
    metallic, width, height = _load_rgba(metallic_path)
    roughness, r_width, r_height = _load_rgba(roughness_path)
    if (r_width, r_height) != (width, height):
        raise ValueError(
            f"{metallic_path.name} is {width}x{height} but {roughness_path.name} is "
            f"{r_width}x{r_height}; cannot combine maps of differing size"
        )

    packed = np.zeros_like(metallic)
    packed[:, :, 0] = metallic[:, :, 0]           # metallic   <- metallic map red
    packed[:, :, 3] = 1.0 - roughness[:, :, 0]    # smoothness <- inverse roughness red

    packed, width, height = box_downsample(packed.reshape(-1, 4), width, height, max_size)
    _write_linear(packed, width, height, metallic_path.with_name(OUTPUT_NAME), OUTPUT_NAME)

    return width, height


def main() -> int:
    args = parse_args()
    global ALLOW_OVERWRITE_DIVERGENT
    ALLOW_OVERWRITE_DIVERGENT = args.force
    DIVERGENT.clear()
    root = Path(args.root).resolve()
    if not root.is_dir():
        print(f"error: {root} is not a directory", file=sys.stderr)
        return 1

    sources = sorted(root.rglob(SOURCE_NAME))
    # Folders in the unpacked shape, skipping any that also carry a combined map (the
    # combined one is authoritative) or that have already been repacked.
    separate = sorted(
        path
        for path in root.rglob(SEPARATE_METALLIC_NAME)
        if (path.with_name(SEPARATE_ROUGHNESS_NAME).exists()
            and not path.with_name(SOURCE_NAME).exists())
    )

    if not sources and not separate:
        print(
            f"error: found neither {SOURCE_NAME} nor "
            f"{SEPARATE_METALLIC_NAME}+{SEPARATE_ROUGHNESS_NAME} under {root}",
            file=sys.stderr,
        )
        return 1

    occlusion_written = 0
    for source in sources:
        relative = source.relative_to(root)
        if args.dry_run:
            print(f"would repack {relative}")
            continue
        width, height, occlusion_note = repack(source, args.max_size)
        if OCCLUSION_NAME in occlusion_note:
            occlusion_written += 1
        written = source.with_name(OUTPUT_NAME) not in DIVERGENT
        verb = "repacked" if written else "SKIPPED (differs from committed)"
        print(f"{verb} {relative} ({width}x{height}) -> {OUTPUT_NAME}; {occlusion_note}")

    for metallic in separate:
        relative = metallic.relative_to(root)
        if args.dry_run:
            print(f"would combine {relative} + {SEPARATE_ROUGHNESS_NAME}")
            continue
        width, height = repack_separate(
            metallic, metallic.with_name(SEPARATE_ROUGHNESS_NAME), args.max_size
        )
        print(f"combined {relative} + {SEPARATE_ROUGHNESS_NAME} ({width}x{height}) -> {OUTPUT_NAME}")

    if not args.dry_run:
        # The unpacked shape has no occlusion source at all — Meshy emits metallic and
        # roughness as separate files and simply does not emit an occlusion one.
        print(
            f"done: {len(sources)} packed map(s), {len(separate)} separate pair(s), "
            f"{occlusion_written} occlusion map(s) written "
            f"({len(sources) - occlusion_written} packed source(s) carried no occlusion, "
            f"{len(separate)} separate pair(s) have no occlusion source)"
        )
    else:
        print(f"done: {len(sources)} packed map(s), {len(separate)} separate pair(s)")

    if DIVERGENT:
        print(
            f"\nrefused to overwrite {len(DIVERGENT)} committed map(s) that differ from what "
            f"this run produces:",
            file=sys.stderr,
        )
        for path in DIVERGENT:
            print(f"  {path}", file=sys.stderr)
        print(
            "\nThis is open item 18. Those maps were baked from 4096x4096 sources that a later "
            "commit replaced with 1024 versions, so the repo can no longer regenerate its own "
            "artefacts. Which version is better is a decision nobody has made — re-run with "
            "--force ONLY if you intend to adopt this run's output as the new committed art.",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
