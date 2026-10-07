import { describe, expect, it } from 'vitest';
import { bytes, duration, format } from '../format.js';

// Same cases as MonitoringSuite.FormatterIsConsistent in the .NET tests: the web and the TUI must agree.
describe('format parity with MetricFormatter', () => {
  it('formats units identically', () => {
    expect(format(null, 'Bytes')).toBe('n/a');
    expect(format(undefined, 'Percent')).toBe('n/a');
    expect(format(1.5 * 1024 * 1024 * 1024, 'Bytes')).toBe('1.5 GiB');
    expect(format(512, 'BytesPerSecond')).toBe('512 B/s');
    expect(format(12.34, 'Percent')).toBe('12.3%');
    expect(format(90061, 'Seconds')).toBe('1d 01:01:01');
    expect(format(3.0, 'Count')).toBe('3');
    expect(format(2.5, 'PerSecond')).toBe('2.5/s');
  });

  it('uses binary units with one decimal under ten', () => {
    expect(bytes(0)).toBe('0 B');
    expect(bytes(1023)).toBe('1023 B');
    expect(bytes(1024)).toBe('1.0 KiB');
    expect(bytes(10 * 1024 * 1024)).toBe('10 MiB');
    expect(bytes(-2048)).toBe('-2.0 KiB');
  });

  it('formats durations', () => {
    expect(duration(59)).toBe('00:59');
    expect(duration(3600)).toBe('01:00:00');
  });
});
