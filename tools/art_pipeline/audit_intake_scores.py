#!/usr/bin/env python3
"""Report every AI asset intake scorecard in the repo, and optionally gate on them.

`ai_asset_intake.py` writes a score.json per candidate run and, until this existed,
nothing read them back. Each run reported its own status and exited; whether the roster as
a whole was in a shippable state was not a question anything could answer.

That mattered more than it sounds. Fourteen of the thirty shipped assets carry
`needs_review`, and all thirty are in production — a state nobody chose, because there was
no view in which it was visible.

Two modes:

    python3 tools/art_pipeline/audit_intake_scores.py
        Report and exit 0. Always safe to run.

    python3 tools/art_pipeline/audit_intake_scores.py --strict
        Exit 1 if any asset is `fail`, 2 if any is `needs_review`. This is the CI form.

--strict is expected to fail today. That is the point of it: it makes the backlog a number
that shows up rather than a paragraph in a document. See OPEN_ITEMS item 12 — the gate can
only be turned on for real once the assets can pass it, which is blocked on the normal-map
decision in item 3.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
RUNS_ROOT = REPO_ROOT / "docs" / "art-pipeline" / "ai-model-intake" / "runs"

EXIT_OK = 0
EXIT_FAIL = 1
EXIT_NEEDS_REVIEW = 2


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Exit non-zero when any asset is not a clean pass.",
    )
    parser.add_argument(
        "--runs-root",
        default=str(RUNS_ROOT),
        help="Directory searched recursively for score.json files.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = Path(args.runs_root)
    if not root.is_dir():
        print(f"error: {root} is not a directory", file=sys.stderr)
        return EXIT_FAIL

    scores = sorted(root.rglob("score.json"))
    if not scores:
        print(f"error: no score.json found under {root}", file=sys.stderr)
        return EXIT_FAIL

    statuses: Counter[str] = Counter()
    failed_check_counts: Counter[str] = Counter()
    rows = []

    for path in scores:
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as error:
            print(f"error: cannot read {path.relative_to(REPO_ROOT)}: {error}", file=sys.stderr)
            return EXIT_FAIL

        score = data.get("score") or {}
        status = str(score.get("status", "unknown"))
        statuses[status] += 1

        # failed_checks was added alongside this script; derive it for scorecards written
        # before that, so old runs report the same detail as new ones rather than blank.
        failed = score.get("failed_checks")
        if failed is None:
            failed = [c.get("id") for c in (score.get("checks") or []) if not c.get("pass")]
        failed_check_counts.update(failed)

        # A scorecard with no has_normal_map row was written before that check was fixed to
        # run unconditionally, and its status cannot be compared with a current one. This is
        # not hypothetical: every `pass` currently in the repo is one of these, and none of
        # them was ever asked the question.
        stale = "has_normal_map" not in [c.get("id") for c in (score.get("checks") or [])]
        rows.append((status, str(data.get("asset_id", "?")), str(data.get("candidate", "?")), failed, stale))

    rows.sort(key=lambda row: ({"fail": 0, "needs_review": 1, "pass": 2}.get(row[0], 3), row[1]))

    width = max(len(row[1]) for row in rows)
    print(f"{'status':13} {'asset':{width}}  failed checks")
    print("-" * (13 + width + 24))
    for status, asset, _candidate, failed, stale in rows:
        detail = ", ".join(failed) if failed else "-"
        print(f"{status:13} {asset:{width}}  {detail}{'   [pre-fix scorecard]' if stale else ''}")

    print()
    print(f"{len(rows)} scorecard(s): " + ", ".join(f"{count} {status}" for status, count in statuses.most_common()))
    if failed_check_counts:
        print("Most common failures: " + ", ".join(f"{check} ({count})" for check, count in failed_check_counts.most_common()))

    stale_rows = [row for row in rows if row[4]]
    if stale_rows:
        stale_passes = sum(1 for row in stale_rows if row[0] == "pass")
        print(
            f"{len(stale_rows)} scorecard(s) predate the has_normal_map fix and never ran that check"
            f"{f', including ALL {stale_passes} of the passes above' if stale_passes else ''}. "
            "Re-run intake on those candidates before trusting their status."
        )

    if not args.strict:
        return EXIT_OK

    if statuses.get("fail"):
        return EXIT_FAIL
    if statuses.get("needs_review"):
        return EXIT_NEEDS_REVIEW
    return EXIT_OK


if __name__ == "__main__":
    sys.exit(main())
