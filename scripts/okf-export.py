#!/usr/bin/env python3
"""
Write the NotDory scope's memories to the repository's Open Knowledge Format (v0.2) bundle at .okf/.

The NotDory server cannot see this checkout, so the scope's filesystem mirror is off and the bundle is
kept in sync by hand (see .okf/<conventions>/okf-mirror-in-repo.md). Usage:

    1. Save the `objects` array (or the whole response) of NotDory memory_enumerate for the scope to a JSON file.
    2. python3 scripts/okf-export.py memories.json [more.json ...]

Later files override earlier ones by memory id. The script rewrites every memory file it is given, removes
memory files in .okf/ that are not in the input, and regenerates .okf/index.md. The output format matches
NotDory's own OkfDocument / FilesystemMemoryStore output.
"""

import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OKF = os.path.join(REPO, ".okf")


def load(paths):
    memories = {}
    for path in paths:
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
        if isinstance(data, dict):
            data = data.get("data", data)
            data = data.get("objects", data) if isinstance(data, dict) else data
        for m in data:
            memories[m["id"]] = m
    return list(memories.values())


def timestamp(value):
    # .NET "O" format: 7 fractional digits and a Z.
    match = re.match(r"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d+))?Z?$", value)
    if not match:
        return value
    fraction = (match.group(2) or "").ljust(7, "0")[:7]
    return match.group(1) + "." + fraction + "Z"


def quote(value):
    return json.dumps(str(value), ensure_ascii=False)


def render(m):
    superseded = bool(m.get("supersededBy"))
    lines = ["---"]
    lines.append("type: " + quote(m.get("type", "Project")))
    lines.append("title: " + quote(m.get("title", m["slug"])))
    lines.append("description: " + quote(m.get("summary", "")))
    lines.append("timestamp: " + quote(timestamp(m["lastUpdateUtc"])))
    if superseded:
        lines.append('status: "deprecated"')
    lines.append("created: " + quote(timestamp(m["createdUtc"])))
    lines.append("slug: " + quote(m["slug"]))
    lines.append("category: " + quote(m["categoryId"]))
    lines.append("links: " + json.dumps(m.get("links", []), ensure_ascii=False))
    if superseded:
        lines.append("supersededBy: " + quote(m["supersededBy"]))
    lines.append("version: " + quote(m.get("version", 1)))
    lines.append("salience: " + quote(m.get("salience", 0.5)))
    lines.append("---")
    return "\n".join(lines) + "\n" + m["body"] + "\n"


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2

    memories = load(argv[1:])
    os.makedirs(OKF, exist_ok=True)

    written = set()
    for m in memories:
        directory = os.path.join(OKF, m["categoryId"])
        os.makedirs(directory, exist_ok=True)
        path = os.path.join(directory, m["slug"] + ".md")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(render(m))
        written.add(os.path.relpath(path, OKF))

    for root, _, files in os.walk(OKF):
        for name in files:
            rel = os.path.relpath(os.path.join(root, name), OKF)
            if rel != "index.md" and name.endswith(".md") and rel not in written:
                os.remove(os.path.join(root, name))

    by_category = {}
    for m in memories:
        by_category.setdefault(m["categoryId"], []).append(m)

    out = ['---', 'okf_version: "0.2"', '---']
    for category in sorted(by_category, key=lambda c: c.lower()):
        out.append("# " + category)
        out.append("")
        for m in sorted(by_category[category], key=lambda x: category + "/" + x["slug"] + ".md"):
            line = "* [" + m.get("title", m["slug"]) + "](" + category + "/" + m["slug"] + ".md) - " + m.get("summary", "")
            if m.get("supersededBy"):
                line += " (deprecated)"
            out.append(line)
        out.append("")

    with open(os.path.join(OKF, "index.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))

    print("Wrote " + str(len(memories)) + " memories in " + str(len(by_category)) + " categories to " + OKF)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
