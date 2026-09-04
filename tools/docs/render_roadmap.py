#!/usr/bin/env python3
"""Renders docs/launch-roadmap.html from docs/LAUNCH_ROADMAP.md.

The HTML Gantt view was hand-authored on 2026-07-31 and immediately began doing what a
second copy of a document always does: on 2026-08-04 it still sold audio as an open 4-6 day
item and the likeliest thing to slip the date, four days after that work closed. Nobody had
lied; the markdown had simply moved and the HTML had not. This makes that impossible by
deriving one from the other.

--------------------------------------------------------------------------------------
The one thing prose cannot carry, and how it is kept honest anyway
--------------------------------------------------------------------------------------
Everything on the page except the timeline is genuinely in the markdown: tiers, the P0
table, dependencies, risks, done-criteria. Bar POSITIONS are not — no sentence in a
roadmap says "this bar starts at 22% and runs 12% wide", and inventing durations from
estimates would draw a chart the document never claimed.

So the geometry lives in an HTML comment block inside LAUNCH_ROADMAP.md: same file, so
there is still exactly one thing to edit, and invisible in every markdown reader, so the
prose stays prose.

That alone would only shrink the drift surface. What removes it is that **every gantt row
must name a real bullet in its week** (or a real dependency, for the externally-gated
rows), and this renderer FAILS if one does not. Rename a week bullet and the build stops
until the chart is updated with it — the same trick as the DLL sync test and the bramble
cell test elsewhere in this repo: two representations that cannot silently disagree
because agreement is asserted.

Usage:
    render_roadmap.py            write docs/launch-roadmap.html
    render_roadmap.py --check    exit 1 if the file on disk is not what would be written
"""

from __future__ import annotations

import argparse
import html
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SOURCE = REPO / "docs/LAUNCH_ROADMAP.md"
TARGET = REPO / "docs/launch-roadmap.html"


# --------------------------------------------------------------------------- model

@dataclass
class Week:
    number: str
    dates: str
    title: str
    bullets: list[tuple[str, str]] = field(default_factory=list)  # (bold title, rest)


@dataclass
class GanttRow:
    week: str
    item: str
    sub: str
    left: float
    width: float
    badge: str
    # Optional second segment, for work that is partly done: the first bar is what landed,
    # this is what remains. Hand-drawn by the 4 Aug review for identity and the shell; the
    # format carries it so generating cannot flatten that back to one estimate bar.
    extra: tuple[float, float, str] | None = None


@dataclass
class Roadmap:
    drafted: str = ""
    updated: str = ""
    status: str = ""
    tiers: list[tuple[str, str, str]] = field(default_factory=list)
    p0: list[tuple[str, str, str, str]] = field(default_factory=list)
    weeks: list[Week] = field(default_factory=list)
    gantt: list[GanttRow] = field(default_factory=list)
    deps: list[tuple[str, str, str]] = field(default_factory=list)
    risks: list[tuple[str, str]] = field(default_factory=list)
    landed: list[tuple[str, str]] = field(default_factory=list)
    done: list[str] = field(default_factory=list)


# --------------------------------------------------------------------------- parsing

def rows_of(table_block: str) -> list[list[str]]:
    """Body rows of a markdown table, header and separator dropped."""
    rows = []
    for line in table_block.splitlines():
        line = line.strip()
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if all(set(c) <= set("-: ") for c in cells):
            continue  # separator
        rows.append(cells)
    return rows[1:] if rows else []


def table_after(text: str, heading: str) -> list[list[str]]:
    """The first markdown table following a heading."""
    start = text.index(heading)
    chunk = text[start:]
    match = re.search(r"^\|.*(?:\n\|.*)+", chunk, re.MULTILINE)
    return rows_of(match.group(0)) if match else []


def inline(md: str) -> str:
    """Markdown inline formatting to HTML, escaping everything else.

    Deliberately narrow: bold, italic, code, strikethrough and nothing more. A roadmap is
    prose and tables; supporting more syntax would mean maintaining a markdown engine to
    render one page."""
    out = html.escape(md, quote=False)
    out = re.sub(r"`([^`]+)`", r"<code>\1</code>", out)
    out = re.sub(r"~~([^~]+)~~", r"<s>\1</s>", out)
    out = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", out)
    out = re.sub(r"(?<!\*)\*([^*]+)\*(?!\*)", r"<em>\1</em>", out)
    return out


def parse(text: str) -> Roadmap:
    r = Roadmap()

    drafted = re.search(r"\*\*Drafted ([\d-]+)\.", text)
    if drafted:
        r.drafted = drafted.group(1)
    updated = re.search(r"Updated ([\d-]+)\*\*", text)
    if updated:
        r.updated = updated.group(1)

    status = re.search(r"(\*\*Where this stands[^\n]*(?:\n(?!\n)[^\n]*)*)", text)
    if status:
        r.status = " ".join(status.group(1).split())

    # Tiers: | **A. Playable...** | Means | Realistic date |
    for cells in table_after(text, "## Define"):
        if len(cells) >= 3:
            r.tiers.append((cells[0], cells[1], cells[2]))

    for cells in table_after(text, "### P0 —"):
        if len(cells) >= 4:
            r.p0.append((cells[0], cells[1], cells[2], cells[3]))

    for cells in table_after(text, "## Critical path"):
        if len(cells) >= 3:
            r.deps.append((cells[0], cells[1], cells[2]))

    # Weeks and their bullets.
    for match in re.finditer(
        r"^### Week (\d) \(([^)]+)\) — (.+?)$(.*?)(?=^### |^---|\Z)",
        text, re.MULTILINE | re.DOTALL,
    ):
        week = Week(match.group(1), match.group(2), match.group(3).strip())
        for bullet in re.finditer(r"^- \*\*(.+?)\*\*(.*?)(?=^- |\n\n|\Z)", match.group(4),
                                  re.MULTILINE | re.DOTALL):
            title = bullet.group(1).strip().rstrip(".")
            rest = " ".join(bullet.group(2).split())
            week.bullets.append((title, rest))
        r.weeks.append(week)

    # Gantt geometry block.
    block = re.search(r"<!-- gantt(.*?)-->", text, re.DOTALL)
    if block:
        for line in block.group(1).splitlines():
            line = line.strip()
            # Only "ext |" or "<digit> |" start a data row; everything else in the block is
            # the explanatory header, which contains pipes of its own.
            if not re.match(r"^(ext|\d)\s*\|", line) or line.count("|") < 5:
                continue
            week, item, sub, left, width, badge = [c.strip() for c in line.split("|", 5)]
            extra = None
            if " + " in badge:
                badge, second = badge.split(" + ", 1)
                parts = [c.strip() for c in second.split("|")]
                if len(parts) == 3:
                    extra = (float(parts[0]), float(parts[1]), parts[2])
            r.gantt.append(GanttRow(week, item, sub, float(left), float(width),
                                    badge.strip(), extra))

    # Risks: a numbered list of "**Title.** body".
    risks = re.search(r"## The three risks worth naming(.*?)(?=^## )", text, re.MULTILINE | re.DOTALL)
    if risks:
        for match in re.finditer(r"^\d+\. \*\*(.+?)\*\*(.*?)(?=^\d+\. |\Z)",
                                 risks.group(1), re.MULTILINE | re.DOTALL):
            r.risks.append((match.group(1).strip().rstrip("."), " ".join(match.group(2).split())))

    landed = re.search(r"## Landed since the draft(.*?)(?=^## )", text, re.MULTILINE | re.DOTALL)
    if landed:
        for match in re.finditer(r"^### (.+?)$\n(.*?)(?=^### |\Z)", landed.group(1),
                                 re.MULTILINE | re.DOTALL):
            r.landed.append((match.group(1).strip(), " ".join(match.group(2).split())))

    done = re.search(r'## What "done" means[^\n]*\n(.*?)\Z', text, re.DOTALL)
    if done:
        for match in re.finditer(r"^- (.+?)(?=^- |\Z)", done.group(1), re.MULTILINE | re.DOTALL):
            r.done.append(" ".join(match.group(1).split()))

    return r


def validate(r: Roadmap) -> list[str]:
    """Every gantt row must name something the prose actually contains.

    This is the assertion the whole design rests on. Without it the comment block is just a
    second document in a trench coat; with it, prose and chart cannot disagree without the
    renderer saying so."""
    problems = []
    by_week = {w.number: [title for title, _ in w.bullets] for w in r.weeks}
    dependencies = [d[0] for d in r.deps]

    for row in r.gantt:
        if row.week == "ext":
            if not any(row.item.lower() in dep.lower() for dep in dependencies):
                problems.append(
                    f"gantt row '{row.item}' (ext) matches no dependency in the critical path table")
            continue
        titles = by_week.get(row.week)
        if titles is None:
            problems.append(f"gantt row '{row.item}' names week {row.week}, which has no section")
        elif not any(row.item.lower() in title.lower() or title.lower() in row.item.lower()
                     for title in titles):
            problems.append(
                f"gantt row '{row.item}' matches no bullet in week {row.week} "
                f"(bullets: {', '.join(titles) or 'none'})")

    charted = {row.item.lower() for row in r.gantt}
    for week in r.weeks:
        for title, _ in week.bullets:
            if not any(c in title.lower() or title.lower() in c for c in charted):
                problems.append(
                    f"week {week.number} bullet '{title}' has no gantt row — add one, "
                    f"or the chart understates the week")
    return problems


# --------------------------------------------------------------------------- rendering

CSS = (REPO / "tools/docs/roadmap.css").read_text(encoding="utf-8")

WEEK_CLASS = {"ext": "bext2", "1": "b1", "2": "b2", "3": "b3", "4": "b4"}


def week_label(group: str, r: "Roadmap") -> str:
    """Group headings come from the parsed week titles, so a rewritten plan cannot leave the
    August themes behind in the chart the way a hard-coded table did."""
    if group == "ext":
        return "External — start day one"
    for week in r.weeks:
        if week.number == group:
            return f"Week {group} — {inline(week.title.lower())}"
    return f"Week {group}"


def span_label(r: "Roadmap") -> str:
    """'Sep 4 – Oct 1 2026' from the first and last week's date ranges."""
    if not r.weeks:
        return ""
    first = r.weeks[0].dates.split("–")[0].strip()
    last = r.weeks[-1].dates.split("–")[-1].strip()
    return f"{first} – {last} 2026"


def render(r: Roadmap) -> str:
    o: list[str] = []
    a = o.append

    stamp = f"Drafted {r.drafted}"
    if r.updated:
        stamp += f" · updated {r.updated}"
    stamp += " · proposal for review, dates and scope not committed"

    a("<!DOCTYPE html>")
    a('<html lang="en">')
    a("<head>")
    a('<meta charset="utf-8">')
    a('<meta name="viewport" content="width=device-width, initial-scale=1">')
    a("<title>Line Wards — Launch Roadmap</title>")
    a("<!-- GENERATED by tools/docs/render_roadmap.py from docs/LAUNCH_ROADMAP.md. Do not edit;")
    a("     edit the markdown and re-run, or `--check` in CI will fail. -->")
    a("<style>")
    a(CSS.rstrip())
    a("</style>")
    a("</head>")
    a("<body>")
    a('<div class="wrap">')
    a("")
    a("  <h1>Line Wards — Launch Roadmap</h1>")
    a(f'  <p class="sub">Four weeks to a soft launch · {span_label(r)}</p>')
    a(f'  <p class="stamp">{stamp}</p>')

    if r.status:
        a("")
        a('  <div class="alert">')
        a(f"    {inline(r.status)}")
        a("  </div>")

    # Tiers
    a("")
    a("  <h2>What “launch” means</h2>")
    a('  <div class="tiers">')
    for index, (tier, means, when) in enumerate(r.tiers):
        label = re.sub(r"\*\*|[A-C]\.\s*", "", tier).strip()
        letter = re.search(r"([A-C])\.", tier)
        target = index == 1
        a(f'    <div class="tier{" on" if target else ""}">')
        badge = ' <span class="badge">TARGET</span>' if target else ""
        a(f'      <div class="t">Tier {letter.group(1) if letter else "?"}{badge}</div>')
        a(f'      <div class="n">{inline(label)}</div>')
        a(f'      <div class="d">{inline(means)}</div>')
        a(f'      <div class="when">{inline(when)}</div>')
        a("    </div>")
    a("  </div>")

    # Gantt
    a("")
    a("  <h2>Timeline</h2>")
    a('  <div class="gantt">')
    a('   <div class="g-min">')
    a('    <div class="g-head">')
    a("      <div>&nbsp;</div>")
    for week in r.weeks:
        a(f'      <div class="wk">Week {week.number} <span>{inline(week.dates)} · '
          f'{inline(week.title.lower())}</span></div>')
    a("    </div>")

    for group in ("ext", "1", "2", "3", "4"):
        rows = [row for row in r.gantt if row.week == group]
        if not rows:
            continue
        a("")
        a(f'    <div class="grp">{week_label(group, r)}</div>')
        for row in rows:
            badge, cls, extra = row.badge, WEEK_CLASS[group], ""
            if badge.startswith("done:"):
                badge, cls, extra = badge[5:], "b4", ";opacity:.75"
            elif badge.startswith("ext:"):
                badge, cls = badge[4:], "bext2"
            a('    <div class="row">')
            a(f'      <div class="lbl">{inline(row.item)}<small>{inline(row.sub)}</small></div>')
            bars = (f'<div class="bar {cls}" '
                    f'style="left:{row.left}%;width:{row.width}%{extra}">{badge}</div>')
            if row.extra:
                left2, width2, badge2 = row.extra
                bars += (f'<div class="bar {WEEK_CLASS[group]}" '
                         f'style="left:{left2}%;width:{width2}%">{badge2}</div>')
            a(f'      <div class="track">{bars}</div>')
            a("    </div>")

    a("")
    a('    <div class="legend">')
    for week in r.weeks:
        a(f'      <span><i style="background:var(--w{week.number})"></i>'
          f'Week {week.number} · {inline(week.title.lower())}</span>')
    a('      <span><i style="background:rgba(92,200,255,.15);border:1px dashed var(--ext)"></i>'
      "External / review — not compressible</span>")
    a("      <span>★ highest-leverage single hour in the plan</span>")
    a("    </div>")
    a("   </div>")
    a("  </div>")

    # P0
    a("")
    a("  <h2>P0 — cannot soft-launch without these</h2>")
    a("  <table>")
    a('    <tr><th class="n">#</th><th>Gap</th><th>Evidence</th>'
      '<th style="text-align:right">Est.</th></tr>')
    for number, gap, evidence, est in r.p0:
        closed = "closed" in gap.lower() or "~~" in est
        style = ' style="opacity:.6"' if closed else ""
        a(f"    <tr{style}><td class=\"n\">{inline(number)}</td><td>{inline(gap)}</td>"
          f'<td class="e">{inline(evidence)}</td><td class="est">{inline(est)}</td></tr>')
    a("  </table>")

    # Dependencies
    a("")
    a("  <h2>Critical path — the uncompressible bits</h2>")
    a('  <div class="card" style="margin-bottom:14px">')
    for dep, lead, start in r.deps:
        a(f'    <div class="dep"><span>{inline(dep)}</span>'
          f"<b>{inline(start)} · {inline(lead)}</b></div>")
    a("  </div>")

    # Risks
    a("")
    a("  <h2>Risks worth naming</h2>")
    a('  <div class="cols">')
    for index, (title, body) in enumerate(r.risks, start=1):
        a('    <div class="card">')
        a(f"      <h3>{index}. {inline(title)}</h3>")
        a(f"      <p>{inline(body)}</p>")
        a("    </div>")
    a("  </div>")

    # Landed since the draft
    if r.landed:
        a("")
        a("  <h2>Landed since the draft — not on the critical path</h2>")
        a('  <div class="cols">')
        for title, body in r.landed:
            heading = re.sub(r"\s*\(([^)]+)\)\s*$", r" <em>\1</em>", title)
            a('    <div class="card">')
            a(f"      <h3>{inline(heading).replace('&lt;em&gt;', '<em>').replace('&lt;/em&gt;', '</em>')}</h3>")
            a(f"      <p>{inline(body)}</p>")
            a("    </div>")
        a("  </div>")

    # Done
    a("")
    a("  <h2>What “done” means on 31 August</h2>")
    a('  <div class="card">')
    a('    <ul class="check">')
    for item in r.done:
        a(f"      <li>{inline(item.rstrip('.'))}</li>")
    a("    </ul>")
    a("  </div>")

    a("")
    a('  <p class="foot">')
    a("    Generated from <code>docs/LAUNCH_ROADMAP.md</code> by")
    a("    <code>tools/docs/render_roadmap.py</code> — edit the markdown, not this file.<br>")
    a("    Context in <code>docs/OPEN_ITEMS.md</code> and <code>docs/GRAPHICS_AA_UPLIFT.md</code>")
    a("  </p>")
    a("")
    a("</div>")
    a("</body>")
    a("</html>")
    return "\n".join(o) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true",
                        help="fail if the committed HTML differs from what the markdown renders")
    args = parser.parse_args()

    roadmap = parse(SOURCE.read_text(encoding="utf-8"))
    problems = validate(roadmap)
    if problems:
        for problem in problems:
            print(f"FAIL  {problem}")
        print(f"\n{len(problems)} inconsistency(ies) between the prose and the gantt block in "
              f"{SOURCE.relative_to(REPO)}.")
        return 1

    rendered = render(roadmap)

    if args.check:
        current = TARGET.read_text(encoding="utf-8") if TARGET.exists() else ""
        if current != rendered:
            print(f"FAIL  {TARGET.relative_to(REPO)} is stale.")
            print("      Run: python3 tools/docs/render_roadmap.py")
            return 1
        print(f"OK: {TARGET.relative_to(REPO)} matches {SOURCE.relative_to(REPO)}")
        return 0

    TARGET.write_text(rendered, encoding="utf-8")
    print(f"wrote {TARGET.relative_to(REPO)}  "
          f"({len(roadmap.gantt)} gantt rows, {len(roadmap.p0)} P0 items, "
          f"{len(roadmap.risks)} risks, {len(roadmap.done)} done-criteria)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
