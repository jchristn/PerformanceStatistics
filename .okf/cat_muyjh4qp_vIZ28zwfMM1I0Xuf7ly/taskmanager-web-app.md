---
type: "Project"
title: "Web app (web/): React + Vite JS, uPlot, TanStack virtualized table"
description: "web/src: api.js (fetch + WebSocket, sessionStorage token), format.js (MetricFormatter parity), catalog.js, liveHistory.js, hooks/useData.js (useHostStream, useSeries live vs. history), pages per tab; nginx proxies /api in Docker, Vite proxies in dev."
timestamp: "2026-10-07T22:08:59.3501254Z"
created: "2026-10-07T22:08:58.7256655Z"
slug: "taskmanager-web-app"
category: "cat_muyjh4qp_vIZ28zwfMM1I0Xuf7ly"
links: ["taskmanager-architecture", "taskmanager-docker-deploy", "taskmanager-tests"]
version: "1"
salience: "0.5"
---
Stack: React 18, react-router-dom 6, uplot, @tanstack/react-table and react-virtual, Vite 5. Tests use Vitest + jsdom + @testing-library/react, and @playwright/test 1.64.

Structure:
- src/api.js:
  - REST calls with Authorization Bearer.
  - The session is kept in sessionStorage (key taskmgr.session); a 401 clears it and fires a 'taskmgr-session' event.
  - streamHost() opens a WebSocket to /api/v1/stream?host&access_token and reconnects after 2s.
- src/format.js: an exact port of MetricFormatter.
- src/catalog.js: byId, unavailableReason(catalog, id, platform), processValue/systemValue selectors that mirror MetricCatalog, PROCESS_COLUMNS, filterProcesses.
- src/liveHistory.js: LiveHistory, the counterpart of HistoryBuffer, built from streamed snapshots.
- hooks/useData.js:
  - useHostStream(hostId): the latest snapshot plus live history.
  - useSeries(range, specs): Live uses the rolling history; 15m–30d calls the /history APIs with step = range/400.
- components:
  - Chart: a uPlot wrapper with theme colors from CSS variables and a ResizeObserver. The legend shows '–' when no point is under the cursor; n/a is reserved for unavailable metrics.
  - ProcessTable: virtualized, with fixed column widths so the header aligns with the absolutely positioned rows.
  - ProcessDetail: actions with a ConfirmDialog (native dialog), enabled only when isAdmin and the host allows the action.
- pages: Login, Hosts, Host (tabs Overview, Processes, Network, Disks, Accounting, Audit, plus a range picker).
- styles.css: color tokens on :root, a dark override under prefers-color-scheme and [data-theme=dark], responsive down to phone width.

Commands:
- Dev: TASKMGR_SERVER=http://localhost:8080 npm run dev (port 3000, proxies /api including ws).
- Build: npm run build.
- Tests: npm test (Vitest), npm run e2e (Playwright against a running stack; set TASKMGR_WEB_URL, TASKMGR_ADMIN_PASSWORD, TASKMGR_E2E_HOST).

Gotcha: @playwright/test 1.48 hangs silently while loading the ESM config on Node 24. Use 1.64 or later.
