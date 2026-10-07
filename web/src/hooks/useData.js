import { useEffect, useRef, useState } from 'react';
import { api, streamHost } from '../api.js';
import { LiveHistory } from '../liveHistory.js';

let catalogCache = null;

export function useCatalog() {
  const [catalog, setCatalog] = useState(catalogCache);
  useEffect(() => {
    if (catalogCache) return;
    api.catalog().then((c) => { catalogCache = c; setCatalog(c); }).catch(() => {});
  }, []);
  return catalog;
}

// Live snapshots of a host plus a rolling history; re-renders on every snapshot.
export function useHostStream(hostId) {
  const history = useRef(new LiveHistory());
  const [latest, setLatest] = useState(null);
  const [status, setStatus] = useState('connecting');

  useEffect(() => {
    history.current = new LiveHistory();
    setLatest(null);
    api.snapshot(hostId).then((s) => { history.current.add(s); setLatest(s); }).catch(() => {});
    const close = streamHost(hostId, (s) => { history.current.add(s); setLatest(s); }, setStatus);
    return close;
  }, [hostId]);

  return { latest, status, history: history.current };
}

// Poll a loader every intervalMs (0 = once) while deps are unchanged.
export function usePolling(loader, deps, intervalMs = 0) {
  const [state, setState] = useState({ data: null, error: null, loading: true });
  useEffect(() => {
    let cancelled = false;
    let timer = null;
    const run = async () => {
      try {
        const data = await loader();
        if (!cancelled) setState({ data, error: null, loading: false });
      } catch (error) {
        if (!cancelled) setState((s) => ({ ...s, error, loading: false }));
      }
      if (!cancelled && intervalMs > 0) timer = setTimeout(run, intervalMs);
    };
    setState((s) => ({ ...s, loading: true }));
    run();
    return () => { cancelled = true; clearTimeout(timer); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);
  return state;
}

export const RANGES = [
  { id: 'live', label: 'Live', seconds: 300 },
  { id: '15m', label: '15m', seconds: 900 },
  { id: '1h', label: '1h', seconds: 3600 },
  { id: '24h', label: '24h', seconds: 86400 },
  { id: '7d', label: '7d', seconds: 7 * 86400 },
  { id: '30d', label: '30d', seconds: 30 * 86400 },
];

// A set of series for a chart: live from the stream, or historical from the server.
// specs: [{ key, label, live: (history, windowSeconds) => {x,y}, fetch: (from, to, step) => Promise<points> }]
export function useSeries(range, specs, latest, deps) {
  const [historical, setHistorical] = useState(null);
  const live = range.id === 'live';

  useEffect(() => {
    if (live) { setHistorical(null); return; }
    let cancelled = false;
    const to = new Date();
    const from = new Date(to.getTime() - range.seconds * 1000);
    const step = Math.max(1, Math.ceil(range.seconds / 400));
    Promise.all(specs.map((s) => s.fetch(from, to, step).catch(() => [])))
      .then((results) => { if (!cancelled) setHistorical(results); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [range.id, ...deps]);

  if (live) {
    const parts = specs.map((s) => s.live(range.seconds));
    return align(parts.map((p) => p.x), parts.map((p) => p.y));
  }

  if (!historical) return null;
  return align(
    historical.map((pts) => pts.map((p) => Date.parse(p.timestampUtc) / 1000)),
    historical.map((pts) => pts.map((p) => p.value ?? null)));
}

// uPlot needs one shared x axis; series come from the same snapshots, so align on the first series' x.
function align(xs, ys) {
  if (!xs.length) return { x: [], ys: [] };
  const base = xs.reduce((a, b) => (b.length > a.length ? b : a), []);
  const out = ys.map((y, i) => {
    const map = new Map(xs[i].map((t, j) => [t, y[j]]));
    return base.map((t) => (map.has(t) ? map.get(t) : null));
  });
  return { x: base, ys: out };
}
