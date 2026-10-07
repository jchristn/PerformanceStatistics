import { describe, expect, it } from 'vitest';
import { byId, filterProcesses, processValue, systemValue, unavailableReason } from '../catalog.js';

const catalog = [
  { id: 'system.load.1', name: 'Load', unit: 'Count', availability: { Windows: { level: 'Unavailable', note: 'Windows has no load average.' } } },
  { id: 'process.cpu', name: 'CPU', unit: 'Percent', availability: { Mac: { level: 'Partial', note: 'Needs elevation.' } } },
];

describe('catalog helpers', () => {
  it('resolves per-core ids', () => {
    expect(byId(catalog, 'system.cpu.core.7').name).toBe('CPU core 7');
    expect(systemValue({ perCoreCpuPercent: [1, 2, 42] }, 'system.cpu.core.2')).toBe(42);
  });

  it('gives n/a reasons by platform', () => {
    expect(unavailableReason(catalog, 'system.load.1', 'Windows')).toBe('Windows has no load average.');
    expect(unavailableReason(catalog, 'system.load.1', 'Linux')).toBeNull();
    expect(unavailableReason(catalog, 'process.cpu', 'Mac')).toBe('Needs elevation.');
  });

  it('reads process fields by metric id', () => {
    const p = { pid: 7, name: 'x', cpuPercent: 12, workingSetBytes: 100 };
    expect(processValue(p, 'process.pid')).toBe(7);
    expect(processValue(p, 'process.memory.workingset')).toBe(100);
  });

  it('filters by pid, name, user, path, and command line', () => {
    const ps = [
      { pid: 1, name: 'nginx', user: 'www', executablePath: '/usr/sbin/nginx', commandLine: 'nginx -g daemon off' },
      { pid: 22, name: 'postgres', user: 'postgres', executablePath: '/usr/lib/postgresql/bin/postgres', commandLine: 'postgres -D /data' },
    ];
    expect(filterProcesses(ps, '22').map((p) => p.pid)).toEqual([22]);
    expect(filterProcesses(ps, 'NGINX').map((p) => p.pid)).toEqual([1]);
    expect(filterProcesses(ps, '/data').map((p) => p.pid)).toEqual([22]);
    expect(filterProcesses(ps, '').length).toBe(2);
  });
});
