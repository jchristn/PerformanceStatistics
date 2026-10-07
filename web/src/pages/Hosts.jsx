import { Link } from 'react-router-dom';
import { api } from '../api.js';
import { bytes, format } from '../format.js';
import { usePolling } from '../hooks/useData.js';
import { Bar } from '../components/Controls.jsx';

export default function Hosts() {
  const { data: hosts, error, loading } = usePolling(() => api.hosts(), [], 5000);

  return (
    <main className="page">
      <h2>Hosts</h2>
      {error && <p className="error">{error.message}</p>}
      {loading && !hosts && <p className="muted">Loading…</p>}
      {hosts && hosts.length === 0 && (
        <div className="card">
          <p>No hosts are reporting yet. Install the agent on a machine and point it at this server:</p>
          <pre>TASKMGR__SERVER=http://this-server:8080 TASKMGR__AGENTKEY=&lt;key&gt; taskmgr-agent</pre>
          <p className="muted">Installers: scripts/agent/linux, scripts/agent/macos, scripts/agent/windows.</p>
        </div>
      )}
      <div className="grid">
        {(hosts || []).map((h) => {
          const memPct = h.info.totalMemoryBytes && h.memoryUsedBytes != null ? (100 * h.memoryUsedBytes) / h.info.totalMemoryBytes : null;
          return (
            <Link key={h.info.hostId} to={`/hosts/${encodeURIComponent(h.info.hostId)}`} className={`card host ${h.online ? '' : 'offline'}`}>
              <div className="host-head">
                <strong>{h.info.hostName || h.info.hostId}</strong>
                <span className={`badge ${h.online ? 'ok' : 'off'}`}>{h.online ? 'online' : 'offline'}</span>
              </div>
              <div className="muted small">{h.info.platform} · {h.info.operatingSystem} · {h.info.architecture}</div>
              <div className="muted small">{h.info.cpuModel || 'CPU'} · {h.info.logicalProcessors} cores · {h.info.totalMemoryBytes ? bytes(h.info.totalMemoryBytes) : 'n/a'} RAM</div>
              <div className="host-metric"><span>CPU</span><Bar percent={h.cpuPercent} /><span className="num">{format(h.cpuPercent, 'Percent')}</span></div>
              <div className="host-metric"><span>Memory</span><Bar percent={memPct} /><span className="num">{format(memPct, 'Percent')}</span></div>
              <div className="muted small">{h.lastSeenUtc ? `Last seen ${new Date(h.lastSeenUtc).toLocaleString()}` : 'Never seen'}{h.info.elevated ? '' : ' · agent not elevated'}</div>
            </Link>
          );
        })}
      </div>
    </main>
  );
}
