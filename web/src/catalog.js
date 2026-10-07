// Helpers over the metric catalog served by /api/v1/catalog.

export function byId(catalog, id) {
  if (!catalog) return null;
  const direct = catalog.find((m) => m.id === id);
  if (direct) return direct;
  const core = /^system\.cpu\.core\.(\d+)$/.exec(id);
  if (core) return { id, name: `CPU core ${core[1]}`, shortName: `CPU${core[1]}`, unit: 'Percent', scope: 'System', description: 'Per-core utilization.', availability: {} };
  return null;
}

// The "n/a" reason for a metric on a platform, or null if fully available.
export function unavailableReason(catalog, id, platform) {
  const m = byId(catalog, id);
  if (!m || !m.availability) return null;
  const a = m.availability[platform];
  if (!a || a.level === 'Full') return null;
  return a.note || null;
}

// Read a process field by metric id (mirrors MetricCatalog selectors).
export function processValue(p, id) {
  switch (id) {
    case 'process.pid': return p.pid;
    case 'process.ppid': return p.parentPid;
    case 'process.name': return p.name;
    case 'process.user': return p.user;
    case 'process.state': return p.state;
    case 'process.priority': return p.priority;
    case 'process.path': return p.executablePath;
    case 'process.cmdline': return p.commandLine;
    case 'process.start': return p.startTimeUtc;
    case 'process.cpu': return p.cpuPercent;
    case 'process.cputime': return p.cpuTimeSeconds;
    case 'process.memory.workingset': return p.workingSetBytes;
    case 'process.memory.private': return p.privateBytes;
    case 'process.memory.virtual': return p.virtualBytes;
    case 'process.disk.read': return p.diskReadBytesPerSecond;
    case 'process.disk.write': return p.diskWriteBytesPerSecond;
    case 'process.disk.read.total': return p.diskReadBytesTotal;
    case 'process.disk.write.total': return p.diskWriteBytesTotal;
    case 'process.handles': return p.handleCount;
    case 'process.threads': return p.threadCount;
    default: return undefined;
  }
}

// Read a system series value from a snapshot's system block (mirrors MetricCatalog selectors).
export function systemValue(s, id) {
  if (!s) return null;
  const core = /^system\.cpu\.core\.(\d+)$/.exec(id);
  if (core) return (s.perCoreCpuPercent || [])[Number(core[1])] ?? null;
  switch (id) {
    case 'system.cpu': return s.cpuPercent;
    case 'system.memory.used': return s.memoryUsedBytes;
    case 'system.memory.available': return s.memoryAvailableBytes;
    case 'system.memory.total': return s.memoryTotalBytes;
    case 'system.swap.used': return s.swapUsedBytes;
    case 'system.swap.total': return s.swapTotalBytes;
    case 'system.disk.read.bytes': return s.diskReadBytesPerSecond;
    case 'system.disk.write.bytes': return s.diskWriteBytesPerSecond;
    case 'system.disk.read.ops': return s.diskReadOpsPerSecond;
    case 'system.disk.write.ops': return s.diskWriteOpsPerSecond;
    case 'system.network.rx': return s.networkReceivedBytesPerSecond;
    case 'system.network.tx': return s.networkSentBytesPerSecond;
    case 'system.load.1': return s.loadAverage1;
    case 'system.processes': return s.processCount;
    case 'system.threads': return s.threadCount;
    case 'system.handles': return s.handleCount;
    default: return null;
  }
}

export const PROCESS_COLUMNS = [
  'process.pid', 'process.name', 'process.user', 'process.cpu', 'process.memory.workingset', 'process.memory.private',
  'process.disk.read', 'process.disk.write', 'process.handles', 'process.threads', 'process.state', 'process.priority',
  'process.cputime', 'process.ppid', 'process.start', 'process.path', 'process.cmdline',
];

export const DEFAULT_PROCESS_COLUMNS = [
  'process.pid', 'process.name', 'process.user', 'process.cpu', 'process.memory.workingset',
  'process.disk.read', 'process.disk.write', 'process.handles', 'process.threads', 'process.state', 'process.path',
];

export function filterProcesses(processes, filter) {
  const f = (filter || '').trim().toLowerCase();
  if (!f) return processes;
  return processes.filter((p) =>
    String(p.pid) === f ||
    [p.name, p.user, p.executablePath, p.commandLine].some((v) => v && v.toLowerCase().includes(f)));
}
