#!/usr/bin/env python3
"""Create a Markdown screenshot UI review report scaffold."""

from __future__ import annotations

import argparse
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(description="Create a screenshot UI review report scaffold.")
    parser.add_argument("screenshots", nargs="*", help="Screenshot paths or labels to include.")
    parser.add_argument("-o", "--output", default="screenshot-ui-review.md", help="Output Markdown path.")
    parser.add_argument("--title", default="Screenshot UI Review", help="Report title.")
    args = parser.parse_args()

    screenshots = args.screenshots or ["<add screenshot path>"]
    rows = "\n".join("|  |  |  |  |" for _ in screenshots)
    notes = "\n".join(f"- `{shot}`: " for shot in screenshots)

    report = f"""# {args.title}

Status: Needs Review

## Summary

- 

## Findings

| Severity | Area | Evidence | Recommendation |
| --- | --- | --- | --- |
{rows}

## Screenshot Notes

{notes}

## Missing Coverage

- 
"""

    output = Path(args.output)
    output.write_text(report, encoding="utf-8")
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
