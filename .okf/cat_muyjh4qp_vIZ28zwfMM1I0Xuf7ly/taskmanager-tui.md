---
type: "Project"
title: "TaskManager.Tui on TUIKit 1.5: structure and TUIKit gotchas"
description: "TaskManagerApp builds header Pane + TabView(ForwardKeys=true) of five views + StatusBar; snapshots marshalled with app.Post; TUIKit chords are case-insensitive, '+' cannot be bound, AddAppHint needs a real chord, focus the table on tab entry."
timestamp: "2026-10-07T22:08:48.4181269Z"
created: "2026-10-07T22:08:47.7667902Z"
slug: "taskmanager-tui"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["taskmanager-architecture", "taskmanager-tests"]
version: "1"
salience: "0.5"
---
Files: src/TaskManager.Tui. It uses the TUIKit NuGet package 1.5.*, not the source.
- Program.cs: argument parsing (--server --host --user --password --token --record --interval --theme), then TuiApp.RunAsync.
- TaskManagerApp:
  - Builds a header Pane (2 lines), the "main" TabView, and a footer StatusBar with BindKeyHints.
  - Binds the keys.
  - Wires ISnapshotSource.SnapshotReceived to app.Post(() => Apply(snapshot)). Widgets are not thread-safe.
- TaskManagerState: the latest snapshot, HistoryBuffer(900, 300), ProcessAccountant, filter, chart window, selection.
- Views:
  - Overview: CPU chart, per-core bars laid out in 1–4 columns by core count, memory and swap chart, memory/load text, disk and network charts.
  - Processes: filter TextField, DataTable with KeySelector = Key and columns from the catalog, detail DefinitionList, three LineCharts.
  - Network, Disks, Accounting.

TUIKit lessons, all found by the headless tests on 2026-10-07:
- TabView.ForwardKeys defaults to false. Content then never sees keys (no arrow keys in tables, no typing in fields), so set it to true.
- Chords are case-insensitive: binding "K" overrides "k". Kill tree therefore uses "t".
- "+" is the chord separator and cannot be bound, so faster sampling uses "=".
- KeyHintResolver.AddAppHint parses its chord: "1-5" throws; use AddAppHint("1", "Tabs 1-5").
- When entering the Processes tab, focus the table (app.Focus("main") then FramedStack.Focus(table)). Otherwise the filter TextField takes focus and swallows letter shortcuts. Escape returns focus to the table.
- Pane auto-scrolls to the bottom; call ScrollUp(10000) after rewriting it.
- Headless testing: new TuiApplication(new HeadlessBackend(160, 50)), Start(), then PumpInputOnce() (which drains Post) and RenderOnce(), then Snapshot.ToText(CaptureFrame()). Use backend.FeedKey("k") / "enter" / "escape".
