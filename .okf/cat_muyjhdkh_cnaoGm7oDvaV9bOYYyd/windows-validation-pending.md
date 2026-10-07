---
type: "Project"
title: "Windows code paths (v2.1–v2.2 library, agent service) have not been executed yet; verify via CI"
description: "All Windows-specific code (kernel32/ntdll/advapi32 interop, PhysicalDisk counters, process metadata, suspend/resume, service installer) compiles but has only run on macOS/Linux; the CI matrix runs Test.Automated, Test.Tui, and Test.Server on windows-latest."
timestamp: "2026-10-07T22:09:53.7992704Z"
created: "2026-10-07T20:39:27.0118600Z"
slug: "windows-validation-pending"
category: "cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd"
links: ["windows-backend", "taskmanager-tests"]
version: "2"
salience: "0.5"
---
As of 2026-10-07, development happened on macOS (Apple Silicon) and on Linux in Docker. Nothing Windows-specific has been executed. The code that needs verification:
- WindowsSystemCounters: GetSystemTimes, GlobalMemoryStatusEx, NtQuerySystemInformation per-core, and the PhysicalDisk(_Total) RawValue and NextValue counters, including Disk Read/Write Bytes/sec.
- WindowsNativeMethods: QueryFullProcessImageName, NtQueryInformationProcess (basic info for the parent PID; class 60 for the command line), OpenProcessToken plus LookupAccountSid, GetProcessIoCounters, NtSuspendProcess/NtResumeProcess.
- WindowsProcessCounters.ReadState (every thread suspended → Stopped).
- The ExtendedMetricsSuite ActionsLifecycle test uses `ping -n 120 127.0.0.1` as its Windows child process.
- scripts/agent/windows/install.bat: sc.exe plus the services registry Environment value.

How to verify: push branch multiplatform-hardening or open a PR. .github/workflows/ci.yml runs Test.Automated, Test.Tui, and Test.Server on windows-latest. Delete this memory once CI is green on Windows and the service installer has been tried by hand.
