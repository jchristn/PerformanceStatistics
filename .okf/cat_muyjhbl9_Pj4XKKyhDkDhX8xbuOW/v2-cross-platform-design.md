---
type: "Project"
title: "v2.0.0 (2026-01-05): cross-platform via interfaces, a factory, and per-OS backends with nullable metrics"
description: "v2 introduced IPerformanceStatistics/ISystemCounters/IProcessCounters, PerformanceStatisticsFactory, and Linux/Mac backends while keeping the Windows classes usable directly."
timestamp: "2026-10-07T20:09:14.5115030Z"
created: "2026-10-07T20:09:14.1971560Z"
slug: "v2-cross-platform-design"
category: "cat_muyjhbl9_Pj4XKKyhDkDhX8xbuOW"
links: ["public-api-and-factory", "mac-backend", "linux-backend", "windows-backend"]
version: "1"
salience: "0.5"
---
Decision (v2.0.0, commit 3ae1f09, 2026-01-05): turn the Windows-only v1 library (PerformanceCounter-based, v1.1.x 2023) into a cross-platform one.
- Interfaces plus a static factory with runtime OS detection, so consumers write platform-agnostic code. The Windows classes stay directly constructible for backward compatibility.
- Metrics made nullable so a platform can say "not available" instead of throwing or faking a value.
- Linux reads /proc directly (no P/Invoke). macOS shells out to stock CLI tools (top, vm_stat, iostat, sysctl) rather than P/Invoking host_statistics/IOKit, trading speed for simplicity.
- Follow-ups: "Fix for M1 Mac" (c0e1e03) and moving the backends into OS-specific namespaces (33519cd), both on 2026-01-05. v1.1.6 (783e1f5) had added IDisposable earlier.
