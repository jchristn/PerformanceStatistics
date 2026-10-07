import { systemValue } from './catalog.js';

// Rolling in-memory history built from streamed snapshots (the web counterpart of HistoryBuffer):
// system snapshots, per-interface throughput, and compact per-process samples.
export class LiveHistory {
  constructor(capacity = 900, processCapacity = 300) {
    this.capacity = capacity;
    this.processCapacity = processCapacity;
    this.system = [];
    this.interfaces = new Map();
    this.processes = new Map();
  }

  add(snapshot) {
    const ts = Date.parse(snapshot.timestampUtc) / 1000;
    if (this.system.length && ts <= this.system[this.system.length - 1].ts) return;
    push(this.system, { ts, system: snapshot.system }, this.capacity);

    for (const ni of snapshot.network || []) {
      if (!this.interfaces.has(ni.name)) this.interfaces.set(ni.name, []);
      push(this.interfaces.get(ni.name), { ts, rx: ni.receivedBytesPerSecond, tx: ni.sentBytesPerSecond }, this.capacity);
    }

    for (const p of snapshot.processes || []) {
      const key = processKey(p);
      if (!this.processes.has(key)) this.processes.set(key, []);
      push(this.processes.get(key), {
        ts, cpu: p.cpuPercent ?? null, ws: p.workingSetBytes ?? null, rd: p.diskReadBytesPerSecond ?? null,
        wr: p.diskWriteBytesPerSecond ?? null, handles: p.handleCount ?? null, threads: p.threadCount ?? null,
      }, this.processCapacity);
    }

    const oldest = this.system[0].ts;
    for (const [key, points] of this.processes) if (points[points.length - 1].ts < oldest) this.processes.delete(key);
    for (const [key, points] of this.interfaces) if (points[points.length - 1].ts < oldest) this.interfaces.delete(key);
  }

  systemSeries(metricId, windowSeconds) {
    const pts = this.window(this.system, windowSeconds);
    return { x: pts.map((p) => p.ts), y: pts.map((p) => systemValue(p.system, metricId) ?? null) };
  }

  interfaceSeries(name, field, windowSeconds) {
    const pts = this.window(this.interfaces.get(name) || [], windowSeconds);
    return { x: pts.map((p) => p.ts), y: pts.map((p) => p[field] ?? null) };
  }

  processSeries(key, field, windowSeconds) {
    const pts = this.window(this.processes.get(key) || [], windowSeconds);
    return { x: pts.map((p) => p.ts), y: pts.map((p) => p[field] ?? null) };
  }

  window(points, windowSeconds) {
    if (!points.length || !windowSeconds) return points;
    const cutoff = points[points.length - 1].ts - windowSeconds;
    let i = 0;
    while (i < points.length && points[i].ts < cutoff) i++;
    return points.slice(i);
  }
}

// The server's identity for a process across PID reuse ("pid@startTicks"), included in every snapshot.
export function processKey(p) {
  return p.key || `${p.pid}@0`;
}

function push(arr, item, capacity) {
  arr.push(item);
  if (arr.length > capacity) arr.splice(0, arr.length - capacity);
}
