import { describe, expect, it } from 'vitest';
import { LiveHistory } from '../liveHistory.js';

function snap(sec, cpu, procs = []) {
  return {
    timestampUtc: new Date(Date.UTC(2026, 0, 1, 0, 0, sec)).toISOString(),
    system: { cpuPercent: cpu, perCoreCpuPercent: [cpu, cpu] },
    network: [{ name: 'eth0', receivedBytesPerSecond: sec, sentBytesPerSecond: 0 }],
    processes: procs,
  };
}

describe('LiveHistory', () => {
  it('keeps a bounded window and ignores out-of-order snapshots', () => {
    const h = new LiveHistory(3, 3);
    [0, 1, 2, 3, 2].forEach((s) => h.add(snap(s, s * 10)));
    const series = h.systemSeries('system.cpu', 0);
    expect(series.y).toEqual([10, 20, 30]);
    expect(h.interfaceSeries('eth0', 'rx', 0).y).toEqual([1, 2, 3]);
  });

  it('tracks processes by server key and forgets ones that aged out', () => {
    const h = new LiveHistory(3, 3);
    h.add(snap(0, 0, [{ key: 'a', pid: 1, cpuPercent: 5 }, { key: 'b', pid: 2, cpuPercent: 1 }]));
    for (let s = 1; s <= 4; s++) h.add(snap(s, 0, [{ key: 'a', pid: 1, cpuPercent: 5 + s }]));
    expect(h.processSeries('a', 'cpu', 0).y).toEqual([7, 8, 9]);
    expect(h.processes.has('b')).toBe(false);
  });

  it('windows by seconds', () => {
    const h = new LiveHistory(100, 10);
    for (let s = 0; s < 10; s++) h.add(snap(s, s));
    expect(h.systemSeries('system.cpu', 3).y).toEqual([6, 7, 8, 9]);
  });
});
