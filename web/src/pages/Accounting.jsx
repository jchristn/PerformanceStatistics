import { useState } from 'react';
import { api } from '../api.js';
import { bytes, duration, format } from '../format.js';
import { usePolling } from '../hooks/useData.js';

export default function Accounting({ hostId, range }) {
  const [groupBy, setGroupBy] = useState('name');
  const seconds = range.id === 'live' ? 3600 : range.seconds;
  const { data, loading, error } = usePolling(() => {
    const to = new Date();
    return api.accounting(hostId, new Date(to.getTime() - seconds * 1000), to, groupBy);
  }, [hostId, groupBy, seconds], 60000);
  const rows = data || [];

  const download = (kind) => {
    const text = kind === 'json'
      ? JSON.stringify(rows, null, 2)
      : ['group,cpuSeconds,averageCpuPercent,peakWorkingSetBytes,diskReadBytes,diskWriteBytes,processCount,firstSeenUtc,lastSeenUtc',
        ...rows.map((r) => [csv(r.group), r.cpuSeconds, r.averageCpuPercent, r.peakWorkingSetBytes, r.diskReadBytes, r.diskWriteBytes, r.processCount, r.firstSeenUtc, r.lastSeenUtc].join(','))].join('\n');
    const url = URL.createObjectURL(new Blob([text], { type: kind === 'json' ? 'application/json' : 'text/csv' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `accounting-${hostId}-${groupBy}.${kind}`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <section>
      <div className="toolbar">
        <span className="small">Last {range.id === 'live' ? '1h' : range.label}, from completed minutes. Group by</span>
        <div className="segmented">
          {['name', 'user'].map((g) => <button key={g} type="button" className={g === groupBy ? 'active' : ''} onClick={() => setGroupBy(g)}>{g}</button>)}
        </div>
        <button type="button" onClick={() => download('csv')} disabled={!rows.length}>CSV</button>
        <button type="button" onClick={() => download('json')} disabled={!rows.length}>JSON</button>
      </div>
      {error && <p className="error">{error.message}</p>}
      {loading && !data && <p className="muted">Loading…</p>}
      {data && rows.length === 0 && <p className="muted">No completed minutes in this range yet.</p>}
      {rows.length > 0 && (
        <table className="simple">
          <thead><tr><th>{groupBy === 'user' ? 'User' : 'Process'}</th><th className="num">CPU time</th><th className="num">Avg CPU</th><th className="num">Peak memory</th><th className="num">Disk read</th><th className="num">Disk write</th><th className="num">Processes</th></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.group}><td>{r.group}</td><td className="num">{duration(r.cpuSeconds)}</td><td className="num">{format(r.averageCpuPercent, 'Percent')}</td>
                <td className="num">{bytes(r.peakWorkingSetBytes)}</td><td className="num">{bytes(r.diskReadBytes)}</td><td className="num">{bytes(r.diskWriteBytes)}</td><td className="num">{r.processCount}</td></tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

function csv(value) {
  const s = String(value ?? '');
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}
