#!/usr/bin/env python3
"""Verify every asset path in the V1 role coverage report resolves, and report gate gaps.

The coverage report is not a status document. `MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md`
resolves the promotion gate's target references through it, so a path that stops resolving
silently disables the gate for that role.

That is not hypothetical. The report named `Tower_*_AIPlate.prefab` and
`Creep_*_AIPlate.prefab` for all ten of its original roles; those were deleted on
2026-07-26 when the 2D-plate era was retired. Every Runtime Asset path in the report
pointed at a missing file for five days, and nothing noticed, because nothing read it back.

Exit codes:
    0  every path resolves
    1  at least one path is missing, or the report cannot be read
    2  --strict and at least one role has no production reference

--strict is expected to fail today: twenty of the thirty roles have no reference, so the
target-reference match score cannot be assigned for them. See the report's Target Reference
Gap section, and OPEN_ITEMS item 14.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
REPORT = REPO_ROOT / "docs" / "art-pipeline" / "v1-role-coverage-report.md"

EXIT_OK = 0
EXIT_MISSING = 1
EXIT_NO_REFERENCE = 2

# Rows are `| Category | Role | `id` | `runtime` | reference | Status |`.
ROW = re.compile(r"^\|\s*(Tower|Creep|Builder)\s*\|\s*([^|]+?)\s*\|\s*`?([^|`]+)`?\s*\|(.+)\|([^|]*)\|\s*([^|]+?)\s*\|\s*$")
PATH = re.compile(r"`([^`]+/[^`]+)`")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--strict", action="store_true", help="Also fail when a role has no production reference.")
    parser.add_argument("--report", default=str(REPORT), help="Path to the coverage report.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    report = Path(args.report)
    if not report.is_file():
        print(f"error: no coverage report at {report}", file=sys.stderr)
        return EXIT_MISSING

    text = report.read_text(encoding="utf-8")
    rows = [line for line in text.splitlines() if line.startswith("| ") and "| ---" not in line]
    if len(rows) < 2:
        print(f"error: {report} has no table rows", file=sys.stderr)
        return EXIT_MISSING

    missing: list[tuple[str, str]] = []
    without_reference: list[str] = []
    checked = 0
    roles = 0

    for line in rows[1:]:  # skip the header row
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) < 6:
            continue
        roles += 1
        role_id = cells[2].strip("`")

        # Column 4 is the runtime asset, column 5 the production reference. Only backticked
        # cells hold paths; prose cells ("none", "procedural avatar") are not path claims.
        for cell in (cells[3], cells[4]):
            for candidate in PATH.findall(cell):
                checked += 1
                if not (REPO_ROOT / candidate).exists():
                    missing.append((role_id, candidate))

        if not PATH.search(cells[4]):
            without_reference.append(role_id)

    print(f"{roles} role(s), {checked} asset path(s) checked.")

    for role_id, path in missing:
        print(f"MISSING: {role_id} -> {path}", file=sys.stderr)

    if without_reference:
        print(
            f"{len(without_reference)} role(s) have no production reference, so the "
            f"target-reference match score cannot be assigned for them: {', '.join(without_reference)}"
        )

    if missing:
        print(f"FAIL: {len(missing)} path(s) in {report.name} do not resolve.", file=sys.stderr)
        return EXIT_MISSING

    print(f"OK: every asset path in {report.name} resolves.")

    if args.strict and without_reference:
        return EXIT_NO_REFERENCE
    return EXIT_OK


if __name__ == "__main__":
    sys.exit(main())
