---
type: "Project"
title: "TUI focusable charts (maximize with axes) and Enter-for-details modals"
description: "ChartPanel/FocusPane are Tab stops; Enter opens ChartModal (AxisChart.RenderWithAxes: legend, Y unit + ticks, local-time X ticks) or DetailsModal (live Func<DetailsContent> from Views/Details.cs); interface addresses/MAC/type come from the library's NetworkInterfaceCounters."
timestamp: "2026-10-07T23:34:19.9221890Z"
created: "2026-10-07T23:34:18.9416061Z"
slug: "tui-chart-and-details-modals"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["taskmanager-tui", "taskmanager-tests"]
version: "1"
salience: "0.5"
---
Added on 2026-10-07 at the maintainer's request: Tab into a chart, press Enter to maximize or restore it with X and Y axis labels; show interface IP addresses; Enter on things opens details in a modal.

The widgets are in src/TaskManager.Tui/Widgets:
- ChartData / ChartSeriesData: a title, a MetricUnitEnum unit, an optional fixed maximum, timestamps, and series (name, color, values). Views build them with TaskManagerState.Chart(...).
- ChartPanel: an IFocusable, IFocusAware, and IKeyHintSource widget. It renders a compact LineChart and shows " Enter: maximize " when focused. Enter raises Activated, and Charts.Panel(title, unit, show) wires that to ChartModal.
- ChartModal: a full-screen DialogModal over a Func<ChartData>, so it stays live. Enter, Esc, or q closes it.
- AxisChart.RenderWithAxes draws:
  - A legend row (latest and peak per series) and a "Y: <unit>" title.
  - Y tick labels at 0/25/50/75/100% of NiceMaximum: 100 for percent, 1/2/5×10^n, ×1024 steps for byte units.
  - X ticks in local HH:mm:ss at 5 positions, and an "X: Time (local), last <window>" title.
  - Below 20×7 it falls back to a compact chart.
- FocusPane: a focusable Pane wrapper; Enter raises Activated. The Overview core and memory panes open System details.
- DetailsModal(title, Func<DetailsContent>):
  - Rows are label/value pairs with section headings and wrapped values; they scroll with ↑↓/PgUp/PgDn/Home/End.
  - Charts go in the bottom half of the modal: side by side when each gets at least 40 columns (so the axes fit), otherwise stacked.
  - Content is re-evaluated on every render, so it updates live.

Builders are in Views/Details.cs:
- Process(state, key): identity, usage with n/a reasons, and CPU, memory, and disk charts. Exited processes come from GetKnownProcesses.
- Interface(state, name): type, MAC, every IPv4/IPv6 address, packets, and charts.
- Connection(state, tcp): service names and the owning interface.
- Also Volume, Accounting(state, group), and System(state).

Library (v2.2 line):
- NetworkInterfaceCounters and the Monitoring NetworkInterfaceSnapshot gained Addresses ("addr/prefix" CIDR, IPv4 first), MacAddress (colon hex), and InterfaceType.
- They are read in NetworkStatistics.ReadAddresses/ReadMac from System.Net.NetworkInformation.
- The web Network table also shows Addresses, Type, and MAC.

Tests (Test.Tui, 15 cases):
- ChartMaximizesWithAxes: Tab until "Enter: maximize", press Enter, assert "Y: " and "X: Time" and a local-time tick, then Esc.
- ProcessDetailsModal.
- InterfaceAddressesAndDetails: the fake eth0 has 3 addresses and a MAC.
- Pitfall: the Processes tab's inline detail panel also shows "Command line". Assert that the modal is closed by its title "(PID n)" instead.
