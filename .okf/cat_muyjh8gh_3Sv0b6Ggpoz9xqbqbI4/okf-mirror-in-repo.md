---
type: "Project"
title: "This scope's OKF bundle is committed at .okf/ and regenerated with scripts/okf-export.py"
description: "The NotDory server can't see the checkout (filesystemMirror off), so after memory changes agents save memory_enumerate output to JSON and run scripts/okf-export.py to rewrite .okf/ (OKF v0.2), committed with the code it describes."
timestamp: "2026-10-07T20:47:29.4606118Z"
created: "2026-10-07T20:39:38.5577850Z"
slug: "okf-mirror-in-repo"
category: "cat_muyjh8gh_3Sv0b6Ggpoz9xqbqbI4"
links: ["release-process"]
version: "2"
salience: "0.5"
---
Since 2026-10-07, the PerformanceStatistics scope (scp_muyjg6zy_IOIIvK4fT8eKj9dIR0C) is mirrored at <repo>/.okf/. session_start reported that the NotDory server cannot see /Users/joelchristner/Code/PerformanceStatistics, so filesystemMirror is off and agents maintain the bundle.

How to sync:
1. Call memory_enumerate (maxResults 100 or more) for the scope.
2. Save its objects array as JSON in the scratchpad. If a tool unescapes " into raw quotes and breaks the JSON, re-escape the quotes before saving.
3. Run `python3 scripts/okf-export.py <file.json> [more.json]`. Later files override earlier ones by id.

The script:
- Rewrites .okf/<categoryId>/<slug>.md for every memory.
- Deletes stale memory files.
- Regenerates .okf/index.md.

The format matches the NotDory repo's own .okf output (OkfDocument / FilesystemMemoryStore):
- Frontmatter keys, in order: type, title, description (= summary), timestamp (= lastUpdateUtc), status deprecated (only when superseded), created, slug, category (= categoryId), links (JSON array), supersededBy (only when superseded), version, salience.
- Every scalar is double-quoted. Timestamps use the .NET O format with 7 fractional digits.
- index.md has okf_version 0.2 frontmatter and one section per category ID, sorted case-insensitively. Each entry is title, path, and description, with (deprecated) appended for superseded memories.

Commit .okf/ changes together with the code they describe.
