---
type: "Project"
title: "TaskManager.Tui on TUIKit 1.5: structure and TUIKit gotchas"
description: "TaskManagerApp builds header Pane + TabView(ForwardKeys=true) of five views + StatusBar; snapshots marshalled with app.Post; views take Action<Modal> show for chart/detail modals; TUIKit chords are case-insensitive, '+' cannot be bound, AddAppHint needs a real chord."
timestamp: "2026-10-07T23:34:10.1606504Z"
created: "2026-10-07T22:08:47.7667900Z"
slug: "taskmanager-tui"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["taskmanager-architecture", "taskmanager-tests", "tui-chart-and-details-modals"]
version: "2"
salience: "0.5"
---
Files: src/TaskManager.Tui. It uses the TUIKit NuGet package 1.5.*, not the source.
- Program.cs: argument parsing (--server --host --user --password --token --record --interval --theme), then TuiApp.RunAsync.
- TaskManagerApp:
  - Builds a header Pane (3 lines; the third is the elevation hint), the "main" TabView, and a footer StatusBar with BindKeyHints.
  - Binds the keys.
  - Creates `Action<Modal> show = m => _ = _App.ShowAsync(m)` and passes it to every view.
  - Wires ISnapshotSource.SnapshotReceived to app.Post(() => Apply(snapshot)). Widgets are not thread-safe.
- TaskManagerState:
  - Holds the latest snapshot, HistoryBuffer(900, 300), ProcessAccountant, filter, chart window, and selection.
  - State.Chart(title, unit, max, series...) builds a ChartData over the current window.
- Views (constructors take (state, show)):
  - Overview: ChartPanels for CPU, memory, disk, and network. FocusPanes for per-core bars (1–4 columns by core count) and memory/load text.
  - Processes: filter TextField; DataTable with KeySelector = Key and columns from the catalog; detail DefinitionList; three ChartPanels.
  - Network: the interfaces table has Addresses (Charts.AddressSummary: first routable address + "+N", zone suffix stripped), Type, and Description columns. It also has a ChartPanel and the TCP table.
  - Disks, Accounting.
  - Every table's RowActivated opens a DetailsModal. See [[tui-chart-and-details-modals]].

TUIKit lessons, all found by the headless tests on 2026-10-07:
- TabView.ForwardKeys defaults to false. Content then never sees keys (no arrow keys in tables, no typing in fields), so set it to true.
- Chords are case-insensitive: binding "K" overrides "k". Kill tree therefore uses "t".
- "+" is the chord separator and cannot be bound, so faster sampling uses "=".
- KeyHintResolver.AddAppHint parses its chord: "1-5" throws; use AddAppHint("1", "Tabs 1-5").
- When entering the Processes tab, focus the table (app.Focus("main") then FramedStack.Focus(table)). Otherwise the filter TextField takes focus and swallows letter shortcuts. Escape returns focus to the table.
- Pane auto-scrolls to the bottom; call ScrollUp(10000) after rewriting it. Pane lives in TUIKit.Content.
- Headless testing: new TuiApplication(new HeadlessBackend(160, 50)), Start(), then PumpInputOnce() (which drains Post) and RenderOnce(), then Snapshot.ToText(CaptureFrame()). Use backend.FeedKey("k") / "enter" / "escape" / "tab".
