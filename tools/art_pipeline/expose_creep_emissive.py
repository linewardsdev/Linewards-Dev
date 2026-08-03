"""Re-expose baked creep emission maps that are authored too dark to cross the bloom threshold.

What actually reaches the screen for a creep is `linear(texel) * EmissionMultiplier`, with the
multiplier at 1.8 and the bloom threshold at 1.05 (`CreepBodyMaterialTuning`). Working back
through the sRGB transfer function, a map has to peak above **0.788 stored sRGB** for any of it
to bloom. That is the real acceptance test for one of these maps, and it is not the test the
editor validator applies -- `ValidateTuning` checks the material's emission multiplier, which is
1.8 for every creep that has a map at all, so a correctly-bound but far-too-dark map passes it.

The correction is a gain in LINEAR space, not in stored sRGB. A multiply on stored sRGB values
is not a multiply on light.

## Why this tool refuses most of the maps it is pointed at

Re-exposure only works when the source bake has the bit depth to survive it, and mostly it does
not. These are 8-bit PNGs. A map whose peak sits at 57/255 has roughly six bits of usable range
in its lit region, and the ~18x linear gain needed to lift it to cohort brightness amplifies the
quantization error along with the signal -- not uniformly across channels, so the hue of the
detail shifts. Measured on obsidianbrute: mean per-channel chromaticity drift of **0.104 on the
bright texels that carry the art**, versus 0.0027 for turretwalker, whose peak is 200/255 and
which needs only a 1.3x gain.

So this is not a knob that can be turned arbitrarily far. The tool measures the hue drift its
own output would cause and **refuses to write when the drift exceeds MAX_HUE_DRIFT**, leaving
the original untouched and reporting why. A map that fails that check does not need re-exposing;
it needs re-baking at proper exposure, which is a pipeline job and not an image edit.

Deliberately not done here:

- No curve, no gamma, no tone shaping -- a single linear scale preserves authored structure
  exactly. Anything else invents detail the bake does not contain.
- Channels are scaled by one shared factor, so hue is preserved up to quantization; the drift
  check above is what confirms the quantization part actually held.
- Maps with no authored content are not candidates at all. Revenant peaks at 0.047 with a
  largest connected blob of 5 pixels, which is bake noise.

Target peak is 0.88 stored sRGB -- Siege's, mid-cohort among maps that already ship and read
correctly, rather than a new number invented here.

Usage:
    python3 tools/art_pipeline/expose_creep_emissive.py --check
    python3 tools/art_pipeline/expose_creep_emissive.py --apply
"""

from __future__ import annotations

import argparse
import glob
import sys
from pathlib import Path

import numpy as np
from PIL import Image

# CreepBodyMaterialTuning.EmissionMultiplier / .BloomThreshold. If either moves there, the
# margins reported here move with it -- they are mirrored constants, not independent guesses.
EMISSION_MULTIPLIER = 1.8
BLOOM_THRESHOLD = 1.05

# Siege's stored peak: mid-cohort among maps that already ship and read correctly.
TARGET_STORED_PEAK = 0.88

# Mean per-channel chromaticity drift permitted on the bright texels that carry the detail.
# Calibrated against measurement, not chosen for roundness: turretwalker's accepted 1.3x gain
# lands at 0.0027, obsidianbrute's rejected 18.3x gain at 0.104. Anything approaching the
# latter is a visible hue shift in the authored art.
MAX_HUE_DRIFT = 0.01

CREEP_ROOT = "unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Creeps"

# Every creep whose map fails the 0.788 bloom test. Most will be refused by the drift check --
# that refusal is the useful output, since it says which maps need re-baking rather than editing.
CANDIDATES = ("Obsidianbrute", "Shade", "Turretwalker", "Revenant")


def srgb_to_linear(c: np.ndarray) -> np.ndarray:
    c = np.asarray(c, np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c: np.ndarray) -> np.ndarray:
    c = np.clip(np.asarray(c, np.float64), 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def chromaticity(rgb: np.ndarray) -> np.ndarray:
    total = rgb.sum(axis=2, keepdims=True)
    return rgb / np.where(total == 0.0, 1.0, total)


def find_map(repo_root: Path, creep: str) -> Path | None:
    hits = glob.glob(str(repo_root / CREEP_ROOT / creep / "AIDrop" / "*_Textures" / "Baked_Emit.png"))
    return Path(hits[0]) if hits else None


def re_expose(creep: str, path: Path, apply: bool) -> tuple[bool, str]:
    """Returns (written, report line)."""
    original = Image.open(path)
    has_alpha = original.mode in ("RGBA", "LA") or "transparency" in original.info

    rgb = np.asarray(original.convert("RGB"), dtype=np.float64) / 255.0
    value = rgb.max(axis=2)
    stored_peak = float(value.max())
    name = creep.lower()

    if stored_peak <= 0.0:
        return False, f"{name:15s} REFUSED  map is entirely black"

    on_screen = float(srgb_to_linear(stored_peak) * EMISSION_MULTIPLIER)
    if on_screen > BLOOM_THRESHOLD:
        return False, f"{name:15s} skipped   already blooms at {on_screen:.3f}"

    gain = float(srgb_to_linear(TARGET_STORED_PEAK) / srgb_to_linear(stored_peak))
    scaled = linear_to_srgb(np.clip(srgb_to_linear(rgb) * gain, 0.0, 1.0))
    quantized = np.round(scaled * 255.0).astype(np.uint8)

    # Measure the drift the WRITTEN file would have, so 8-bit rounding is inside the check
    # rather than outside it. Judged on the bright texels, which is where the art lives.
    bright = value > 0.5 * stored_peak
    peak_8bit = int(round(stored_peak * 255.0))
    if not bright.any():
        return False, f"{name:15s} REFUSED  no lit texels to measure"

    drift = float(np.abs(chromaticity(rgb) - chromaticity(quantized / 255.0))[bright].mean())
    if drift > MAX_HUE_DRIFT:
        return False, (
            f"{name:15s} REFUSED  source peaks at {peak_8bit}/255, so the x{gain:.1f} gain needed "
            f"shifts hue by {drift:.4f} (limit {MAX_HUE_DRIFT}) -- needs re-baking, not re-exposing"
        )

    after = float(srgb_to_linear(quantized.max() / 255.0) * EMISSION_MULTIPLIER)
    if apply:
        out = Image.fromarray(quantized)
        if has_alpha:
            # The bake's alpha is a coverage mask; re-exposing emission must not disturb it.
            out.putalpha(original.convert("RGBA").getchannel("A"))
        out.save(path)

    verb = "re-exposed" if apply else "would fix "
    return True, (
        f"{name:15s} {verb} peak {stored_peak:.3f} -> {quantized.max() / 255.0:.3f}, "
        f"on-screen {on_screen:.3f} -> {after:.3f} (x{gain:.1f} linear, hue drift {drift:.4f})"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--check", action="store_true", help="report without writing")
    group.add_argument("--apply", action="store_true", help="rewrite the maps that pass the checks")
    parser.add_argument("--repo-root", default=".", help="repository root (default: cwd)")
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    if not (repo_root / CREEP_ROOT).is_dir():
        print(f"error: {CREEP_ROOT} not found under {repo_root}", file=sys.stderr)
        return 1

    needed = float(linear_to_srgb(BLOOM_THRESHOLD / EMISSION_MULTIPLIER))
    print(f"emission multiplier {EMISSION_MULTIPLIER}, bloom threshold {BLOOM_THRESHOLD} "
          f"-> a map must peak above {needed:.3f} stored sRGB to bloom at all\n")

    written = 0
    for creep in CANDIDATES:
        path = find_map(repo_root, creep)
        if path is None:
            print(f"{creep.lower():15s} REFUSED  no Baked_Emit.png -- needs a map authored")
            continue
        ok, line = re_expose(creep, path, apply=args.apply)
        written += ok
        print(line)

    print(f"\n{written} of {len(CANDIDATES)} re-exposable; the rest need re-baking or authoring.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
