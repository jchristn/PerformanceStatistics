// Formatting identical to PerformanceStatistics.Monitoring.MetricFormatter, so the web app and the TUI
// print the same text for the same value.

export const NOT_AVAILABLE = 'n/a';

const BYTE_UNITS = ['B', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB'];

export function bytes(value) {
  let v = Math.abs(value);
  let unit = 0;
  while (v >= 1024 && unit < BYTE_UNITS.length - 1) {
    v /= 1024;
    unit++;
  }
  const number = unit === 0 ? Math.round(v).toFixed(0) : v < 10 ? round(v, 1).toFixed(1) : Math.round(v).toFixed(0);
  return (value < 0 ? '-' : '') + number + ' ' + BYTE_UNITS[unit];
}

export function duration(seconds) {
  const total = Math.max(0, Math.floor(seconds));
  const days = Math.floor(total / 86400);
  const h = Math.floor((total % 86400) / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const pad = (n) => String(n).padStart(2, '0');
  if (days >= 1) return `${days}d ${pad(h)}:${pad(m)}:${pad(s)}`;
  if (h >= 1) return `${pad(h)}:${pad(m)}:${pad(s)}`;
  return `${pad(m)}:${pad(s)}`;
}

export function number(value) {
  if (Math.abs(value - Math.round(value)) < 0.005) return Math.round(value).toFixed(0);
  return String(round(value, 2));
}

export function format(value, unit) {
  if (value === null || value === undefined || Number.isNaN(value)) return NOT_AVAILABLE;
  switch (unit) {
    case 'Percent':
      return round(value, 1).toFixed(1) + '%';
    case 'Bytes':
      return bytes(value);
    case 'BytesPerSecond':
      return bytes(value) + '/s';
    case 'PerSecond':
      return number(value) + '/s';
    case 'Seconds':
      return duration(value);
    case 'Timestamp':
      return new Date(value).toLocaleString();
    default:
      return number(value);
  }
}

function round(value, digits) {
  const f = Math.pow(10, digits);
  // Round half away from zero, like .NET's default for these values.
  return Math.sign(value) * Math.round(Math.abs(value) * f + Number.EPSILON) / f;
}
