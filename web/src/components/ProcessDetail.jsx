import { useState } from 'react';
import { api, ApiError } from '../api.js';
import { byId, processValue, unavailableReason } from '../catalog.js';
import { format } from '../format.js';
import { useSeries } from '../hooks/useData.js';
import { processKey } from '../liveHistory.js';
import Chart from './Chart.jsx';
import { ConfirmDialog } from './Controls.jsx';

const INFO = ['process.name', 'process.pid', 'process.ppid', 'process.user', 'process.state', 'process.priority', 'process.start', 'process.path', 'process.cmdline'];
const USAGE = ['process.cpu', 'process.cputime', 'process.memory.workingset', 'process.memory.private', 'process.memory.virtual',
  'process.disk.read', 'process.disk.write', 'process.disk.read.total', 'process.disk.write.total', 'process.handles', 'process.threads'];
const PRIORITIES = ['Idle', 'BelowNormal', 'Normal', 'AboveNormal', 'High', 'RealTime'];

export default function ProcessDetail({ hostId, process, catalog, platform, history, range, allowedActions, isAdmin, onClose, onNotify, elevated = true }) {
  const [pending, setPending] = useState(null);
  const [priority, setPriority] = useState('BelowNormal');
  const key = processKey(process);

  const live = (field) => (w) => history.processSeries(key, field, w);
  const remote = (metric) => (from, to, step) => api.processHistory(hostId, key, metric, from, to, step);
  const cpu = useSeries(range, [{ live: live('cpu'), fetch: remote('process.cpu') }], null, [key]);
  const mem = useSeries(range, [{ live: live('ws'), fetch: remote('process.memory.workingset') }], null, [key]);
  const disk = useSeries(range, [
    { live: live('rd'), fetch: remote('process.disk.read') },
    { live: live('wr'), fetch: remote('process.disk.write') },
  ], null, [key]);

  const show = (id) => {
    const m = byId(catalog, id);
    const v = processValue(process, id);
    if (v === null || v === undefined || v === '') {
      const reason = unavailableReason(catalog, id, platform) || (elevated ? null : 'admin privileges may be required');
      return <span className="na">n/a{reason ? ` (${reason})` : ''}</span>;
    }
    if (id === 'process.start') return new Date(v).toLocaleString();
    if (!m || m.unit === 'Text' || id === 'process.pid' || id === 'process.ppid') return <span className="mono wrap">{String(v)}</span>;
    return format(v, m.unit);
  };

  const run = async (action) => {
    setPending(null);
    try {
      const req = { pid: process.pid, startTimeUtc: process.startTimeUtc, action, priority: action === 'SetPriority' ? priority : undefined };
      let r = await api.requestAction(hostId, req);
      for (let i = 0; i < 30 && !r.completedUtc; i++) {
        await new Promise((res) => setTimeout(res, 500));
        r = await api.action(hostId, r.requestId);
      }
      onNotify(`${action} ${process.name} (${process.pid}): ${r.result}. ${r.message || ''}`, r.result === 'Success' ? 'ok' : 'error');
    } catch (e) {
      onNotify(e instanceof ApiError ? e.message : String(e), 'error');
    }
  };

  const can = (a) => isAdmin && allowedActions.includes(a);
  const verbs = { Kill: 'Kill', KillTree: 'Kill tree', Suspend: process.state === 'Stopped' ? null : 'Suspend', Resume: process.state === 'Stopped' ? 'Resume' : null, SetPriority: 'Priority…' };

  return (
    <aside className="detail" aria-label="Process details">
      <header className="detail-head">
        <h3>{process.name} <span className="muted">PID {process.pid}</span></h3>
        <button type="button" className="icon" onClick={onClose} aria-label="Close details">✕</button>
      </header>

      <div className="actions">
        {Object.entries(verbs).filter(([, label]) => label).map(([action, label]) => (
          <button key={action} type="button" disabled={!can(action)} className={action.startsWith('Kill') ? 'danger' : ''}
                  title={can(action) ? undefined : !isAdmin ? 'Only administrators can act on processes.' : `The agent on this host does not allow ${action}.`}
                  onClick={() => setPending(action)}>
            {label}
          </button>
        ))}
      </div>

      <dl className="kv">
        {INFO.map((id) => (<div key={id}><dt>{byId(catalog, id)?.name || id}</dt><dd>{show(id)}</dd></div>))}
      </dl>
      <h4>Usage</h4>
      <dl className="kv">
        {USAGE.map((id) => (<div key={id}><dt>{byId(catalog, id)?.name || id}</dt><dd>{show(id)}</dd></div>))}
      </dl>

      <Chart title="CPU %" data={cpu} series={[{ label: 'CPU' }]} unit="Percent" height={120} />
      <Chart title="Memory (resident)" data={mem} series={[{ label: 'Memory' }]} unit="Bytes" height={120} />
      <Chart title="Disk I/O" data={disk} series={[{ label: 'Read' }, { label: 'Write' }]} unit="BytesPerSecond" height={120} />

      {pending && (
        <ConfirmDialog
          title={`${verbs[pending].replace('…', '')} ${process.name}?`}
          message={pending === 'KillTree' ? `This ends PID ${process.pid} and every process it started.` : pending === 'SetPriority' ? `Choose a priority for PID ${process.pid}.` : `PID ${process.pid}, owned by ${process.user || 'unknown'}.`}
          confirmLabel={pending === 'SetPriority' ? 'Set priority' : verbs[pending].replace('…', '')}
          danger={pending.startsWith('Kill')}
          onCancel={() => setPending(null)}
          onConfirm={() => run(pending)}>
          {pending === 'SetPriority' && (
            <select value={priority} onChange={(e) => setPriority(e.target.value)} aria-label="Priority">
              {PRIORITIES.map((p) => <option key={p} value={p}>{p}</option>)}
            </select>
          )}
        </ConfirmDialog>
      )}
    </aside>
  );
}
