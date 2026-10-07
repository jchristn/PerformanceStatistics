---
type: "Project"
title: "Private field casing differs: Windows uses _PascalCase, Linux/Mac use _camelCase"
description: "Windows classes use _Disposed/_Lock/_Process; Linux and Mac classes use _disposed/_lock/_process. Match the file you are editing; ask the maintainer before unifying."
timestamp: "2026-10-07T20:09:54.8441600Z"
status: "deprecated"
created: "2026-10-07T20:09:42.5336860Z"
slug: "private-field-naming-inconsistency"
category: "cat_muyjhdkh_cnaoGm7oDvaV9bOYYyd"
links: ["never-throw-return-null", "orchestrator-duplication"]
supersededBy: "mem_muykm8l0_p0eb7RdlzP2dDOum7GE"
version: "2"
salience: "0.5"
---
Windows/*.cs (the v1 code) uses `_Disposed`, `_Lock`, `_CachedMonitoredProcesses`, `_MonitoredProcessNames`, and `_Process`. Linux/*.cs and Mac/*.cs (added in v2) use `_disposed`, `_lock`, `_cachedMonitoredProcesses`, `_monitoredProcessNames`, and `_process`. Until it is unified, follow the casing already used in the file you edit. Ask the maintainer which style is canonical before mass-renaming.
