import { useMemo, useState } from 'react';
import { api } from '../api.js';
import { DEFAULT_PROCESS_COLUMNS, PROCESS_COLUMNS, byId, filterProcesses } from '../catalog.js';
import { processKey } from '../liveHistory.js';
import { usePolling } from '../hooks/useData.js';
import ProcessTable from '../components/ProcessTable.jsx';
import ProcessDetail from '../components/ProcessDetail.jsx';

const COLUMNS_KEY = 'taskmgr.columns';

function loadColumns() {
  try {
    const raw = localStorage.getItem(COLUMNS_KEY);
    const cols = raw ? JSON.parse(raw) : null;
    return Array.isArray(cols) && cols.length ? cols : DEFAULT_PROCESS_COLUMNS;
  } catch {
    return DEFAULT_PROCESS_COLUMNS;
  }
}

export default function Processes(props) {
  const { hostId, latest, catalog, platform, range } = props;
  const [filter, setFilter] = useState('');
  const [selected, setSelected] = useState(null);
  const [columns, setColumns] = useState(loadColumns);
  const [showPicker, setShowPicker] = useState(false);
  const [showExited, setShowExited] = useState(false);

  const live = latest.processes || [];
  const rows = useMemo(() => filterProcesses(live, filter), [live, filter]);
  const current = selected ? live.find((p) => processKey(p) === selected) : null;

  const exited = usePolling(async () => {
    if (!showExited) return [];
    const to = new Date();
    const from = new Date(to.getTime() - Math.max(range.seconds, 3600) * 1000);
    const records = await api.processRecords(hostId, from, to);
    const running = new Set(live.map((p) => processKey(p)));
    return records.filter((r) => !running.has(r.key));
  }, [hostId, showExited, range.id], 30000);

  const toggleColumn = (id) => {
    const next = columns.includes(id) ? columns.filter((c) => c !== id) : PROCESS_COLUMNS.filter((c) => c === id || columns.includes(c));
    setColumns(next);
    try { localStorage.setItem(COLUMNS_KEY, JSON.stringify(next)); } catch { /* per-viewer convenience only */ }
  };

  return (
    <section className={`processes ${current ? 'with-detail' : ''}`}>
      <div className="toolbar">
        <input type="search" placeholder="Filter by PID, name, user, path, or command line" value={filter} onChange={(e) => setFilter(e.target.value)} aria-label="Filter processes" />
        <span className="muted small">{rows.length} of {live.length}</span>
        <label className="small"><input type="checkbox" checked={showExited} onChange={(e) => setShowExited(e.target.checked)} /> Show exited</label>
        <button type="button" onClick={() => setShowPicker((v) => !v)} aria-expanded={showPicker}>Columns</button>
      </div>
      {showPicker && (
        <div className="card picker">
          {PROCESS_COLUMNS.map((id) => (
            <label key={id} className="small" title={byId(catalog, id)?.description}>
              <input type="checkbox" checked={columns.includes(id)} onChange={() => toggleColumn(id)} /> {byId(catalog, id)?.name || id}
            </label>
          ))}
        </div>
      )}
      <div className="split">
        <ProcessTable processes={rows} catalog={catalog} platform={platform} columns={columns} selectedKey={selected} onSelect={(p) => setSelected(processKey(p))} elevated={props.elevated !== false} />
        {current && (
          <ProcessDetail process={current} catalog={catalog} platform={platform} history={props.history} hostId={hostId} range={range}
                         allowedActions={props.allowedActions} isAdmin={props.isAdmin} onClose={() => setSelected(null)} onNotify={props.notify} elevated={props.elevated !== false} />
        )}
      </div>
      {showExited && (
        <div className="card">
          <h4>Exited in the last {range.seconds >= 3600 ? range.label : '1h'}</h4>
          {(exited.data || []).length === 0 ? <p className="muted small">None recorded.</p> : (
            <table className="simple">
              <thead><tr><th>PID</th><th>Name</th><th>User</th><th>Started</th><th>Last seen</th><th>Path</th></tr></thead>
              <tbody>
                {exited.data.map((r) => (
                  <tr key={r.key}><td className="num">{r.pid}</td><td>{r.name}</td><td>{r.user}</td>
                    <td>{r.startTimeUtc ? new Date(r.startTimeUtc).toLocaleString() : 'n/a'}</td><td>{new Date(r.lastSeenUtc).toLocaleString()}</td><td className="mono">{r.executablePath}</td></tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </section>
  );
}
